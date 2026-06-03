using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.Core.Entities;
using TRIO2026.Data.Contexts;

namespace TRIO2026.App.Services;

/// <summary>
/// 系統事件日誌服務 — 非同步佇列 + 批次寫入 + Dead Letter 備援 + Console 同步輸出
/// 
/// 架構：
///   1. 呼叫端透過 LogInfo/LogError 等方法將事件推入 Channel 佇列（不阻塞）
///   2. 同時輸出至 Console（開發階段診斷用）
///   3. 背景 Task 從佇列取出事件，累積至 BatchSize 或超時後統一寫入 DB
///   4. DB 寫入失敗時，自動降級至 Dead Letter JSON 檔案
///   5. App 關閉時透過 Dispose() flush 剩餘佇列
/// 
/// ErrorId 使用預定義代碼（對應 EventCodeDefinition 對照表），
/// 非隨機產生，方便 end user 回報給 CFS/客服人員。
/// 
/// 使用範例：
///   EventLogService.Instance.LogInfo("UV", "ViewModel", ErrorCodes.UvStart, "UV 照射啟動");
///   EventLogService.Instance.LogException("System", "App", ex, ErrorCodes.UnhandledException);
/// 
/// 製作者: Office of William
/// </summary>
public class EventLogService : IDisposable
{
    /// <summary>全域單例（由 App.xaml.cs 設定）</summary>
    public static EventLogService? Instance { get; set; }

    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<SystemEvent> _channel;
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _deadLetterDir;

    /// <summary>累積幾筆後統一寫入 DB</summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>超過此時間強制 flush（即使未達 BatchSize）</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>SessionService 參考（可選，用於自動填入 UserName/SessionId）</summary>
    public SessionService? SessionService { get; set; }

    public EventLogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _channel = Channel.CreateUnbounded<SystemEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Dead Letter 目錄
        var baseDir = FindProjectRoot();
        _deadLetterDir = Path.Combine(baseDir, "Logs", "db-failover-logs");
        Directory.CreateDirectory(_deadLetterDir);

        // ── 啟動健康檢查：探測 EventLog DB 可寫性 ──
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventLogDbContext>();
            if (!db.Database.CanConnect())
            {
                var msg = "⚠️ [EventLog] DB 連線失敗 — 事件將寫入 db-failover-logs";
                Console.WriteLine(msg);
                Data.Extensions.StartupLogger.Current?.Warn("EventLogService", msg);
            }
        }
        catch (Exception ex)
        {
            var msg = $"⚠️ [EventLog] DB 健康檢查失敗: {ex.Message} — 事件將寫入 db-failover-logs";
            Console.WriteLine(msg);
            Data.Extensions.StartupLogger.Current?.Warn("EventLogService", msg, ex.Message);
        }

        // 啟動背景消費者
        _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    // ═══════════════════════════════════════
    // 公開 API — 生產者端（非阻塞）
    // ═══════════════════════════════════════

    /// <summary>寫入 Info 日誌（eventCode → EventCode 欄位，ErrorId = null）</summary>
    public void LogInfo(string category, string source, string? eventCode,
        string message, string? detail = null)
        => Enqueue(SystemEvent.CreateInfo(category, source, message,
            errorId: null, eventCode: eventCode, detail: detail));

    /// <summary>寫入 Warning 日誌（eventCode → EventCode 欄位，ErrorId = null）</summary>
    public void LogWarning(string category, string source, string? eventCode,
        string message, string? detail = null)
        => Enqueue(SystemEvent.CreateWarning(category, source, message,
            errorId: null, eventCode: eventCode, detail: detail));

    /// <summary>寫入 Error 日誌（errorId → ErrorId + EventCode 雙寫）</summary>
    public void LogError(string category, string source, string? errorId,
        string message, string? detail = null)
        => Enqueue(SystemEvent.CreateError(category, source, message,
            errorId: errorId, eventCode: errorId, detail: detail));

    /// <summary>寫入 Fatal 日誌（errorId → ErrorId + EventCode 雙寫）</summary>
    public void LogFatal(string category, string source, string? errorId,
        string message, string? detail = null)
        => Enqueue(SystemEvent.CreateFatal(category, source, message,
            errorId: errorId, eventCode: errorId, detail: detail));

    /// <summary>從例外寫入 Error 日誌（errorId → ErrorId + EventCode 雙寫）</summary>
    public void LogException(string category, string source, Exception ex,
        string? errorId = null, string? message = null)
    {
        errorId ??= ResolveOrCreateErrorId(category, ex);
        var evt = SystemEvent.CreateFromException(category, source, ex, errorId, message);
        evt.EventCode = errorId; // 確保 EventCode 也有值
        Enqueue(evt);
    }

    /// <summary>
    /// 根據例外類型解析預定義 ErrorId，若無對應則動態註冊新的 ERR-9xxx
    /// </summary>
    private string ResolveOrCreateErrorId(string category, Exception ex)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventLogDbContext>();

            var exType = ex.GetType().Name;

            // 嘗試找到對應的 EventCodeDefinition（依 Category + ExceptionType 模糊比對）
            var existing = db.EventCodeDefinitions
                .FirstOrDefault(e => e.Category == category &&
                                     e.Description.Contains(exType));

            if (existing != null)
                return existing.Code;

            // 動態註冊：取 ERR-9xxx 系列的最大流水號 + 1
            var maxDynamic = db.EventCodeDefinitions
                .Where(e => e.Code.StartsWith("ERR-9"))
                .Select(e => e.Code)
                .AsEnumerable()
                .Select(c => int.TryParse(c.Replace("ERR-", ""), out var n) ? n : 0)
                .DefaultIfEmpty(9000)
                .Max();

            var newCode = $"ERR-{maxDynamic + 1}";
            var newDef = new EventCodeDefinition
            {
                Code = newCode,
                Category = category,
                Severity = "Error",
                Title = $"Dynamic: {exType}",
                Description = $"動態註冊的錯誤 — {exType}: {ex.Message}",
                Resolution = "提供此 Error ID 給技術支援人員",
                UserMessageKey = null,
                UserMessageFallback = $"An error occurred ({newCode}). Please report this ID."
            };

            db.EventCodeDefinitions.Add(newDef);
            db.SaveChanges();

            Console.WriteLine($"📝 [EventLog] 動態註冊新錯誤代碼: {newCode} ({exType})");
            return newCode;
        }
        catch
        {
            return TRIO2026.Core.ErrorCodes.UnhandledException;
        }
    }

    // ═══════════════════════════════════════
    // CorrelationId 追蹤 Scope
    // ═══════════════════════════════════════

    [ThreadStatic]
    private static string? _currentCorrelationId;

    /// <summary>
    /// 建立追蹤範圍 — 範圍內所有日誌自動附加同一 CorrelationId
    /// </summary>
    public IDisposable BeginScope(string operationName)
    {
        _currentCorrelationId = $"{operationName}_{Guid.NewGuid():N}";
        return new CorrelationScope();
    }

    private class CorrelationScope : IDisposable
    {
        public void Dispose() => _currentCorrelationId = null;
    }

    // ═══════════════════════════════════════
    // 內部 — 佇列與消費者
    // ═══════════════════════════════════════

    private void Enqueue(SystemEvent evt)
    {
        // 自動填入上下文資訊
        evt.CorrelationId ??= _currentCorrelationId;

        if (SessionService?.CurrentUser != null)
        {
            evt.UserId ??= SessionService.CurrentUser.Id;
            evt.UserName ??= SessionService.CurrentUser.DisplayName
                          ?? SessionService.CurrentUser.Username;
        }

        // Console 同步輸出（開發階段診斷）
        PrintToConsole(evt);

        // 非阻塞寫入佇列
        if (!_channel.Writer.TryWrite(evt))
        {
            WriteDeadLetter(new List<SystemEvent> { evt }, "Channel write failed");
        }
    }

    /// <summary>Console 輸出格式化日誌（開發階段）</summary>
    private static void PrintToConsole(SystemEvent evt)
    {
        var prefix = evt.Level switch
        {
            "Fatal" => "💀",
            "Error" => "❌",
            "Warning" => "⚠️",
            "Info" => "ℹ️",
            _ => "📋"
        };

        var errorTag = string.IsNullOrEmpty(evt.ErrorId) ? "" : $" [{evt.ErrorId}]";
        Console.WriteLine(
            $"{prefix} [{evt.TimestampLocal}] [{evt.Level}]{errorTag} " +
            $"{evt.Category}/{evt.Source}: {evt.Message}");

        if (!string.IsNullOrEmpty(evt.ExceptionType))
            Console.WriteLine($"   Exception: {evt.ExceptionType}");
        if (!string.IsNullOrEmpty(evt.Detail))
            Console.WriteLine($"   Detail: {evt.Detail}");
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        var buffer = new List<SystemEvent>(BatchSize);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                buffer.Clear();

                if (await _channel.Reader.WaitToReadAsync(ct))
                {
                    var deadline = DateTime.UtcNow + FlushInterval;

                    while (buffer.Count < BatchSize &&
                           DateTime.UtcNow < deadline &&
                           _channel.Reader.TryRead(out var evt))
                    {
                        buffer.Add(evt);
                    }

                    if (buffer.Count > 0)
                        await FlushAsync(buffer);
                }
            }
        }
        catch (OperationCanceledException) { }

        // Graceful shutdown — flush 剩餘
        while (_channel.Reader.TryRead(out var remaining))
            buffer.Add(remaining);

        if (buffer.Count > 0)
            await FlushAsync(buffer);
    }

    private async Task FlushAsync(List<SystemEvent> events)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventLogDbContext>();
            db.SystemEvents.AddRange(events);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            WriteDeadLetter(events, ex.Message);
        }
    }

    // ═══════════════════════════════════════
    // Dead Letter 備援
    // ═══════════════════════════════════════

    private void WriteDeadLetter(List<SystemEvent> events, string writeError)
    {
        try
        {
            var payload = new
            {
                writeError,
                timestamp = DateTimeOffset.UtcNow.ToString("o"),
                events = events.Select(e => new
                {
                    e.Timestamp, e.TimestampLocal, e.ErrorId,
                    e.Level, e.Category, e.Source, e.EventCode,
                    e.Message, e.Detail, e.ExceptionType,
                    e.StackTrace, e.InnerException,
                    e.UserName, e.CorrelationId,
                    e.MachineName, e.AppVersion
                })
            };

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.json";
            var filePath = Path.Combine(_deadLetterDir, fileName);
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(filePath, json);

            Console.WriteLine($"⚠️ [EventLog] Dead Letter 已寫入: {filePath}");
        }
        catch (Exception dlEx)
        {
            Console.WriteLine($"❌ [EventLog] Dead Letter 寫入失敗: {dlEx.Message}");
            foreach (var evt in events)
                Console.WriteLine($"❌ [EventLog][LOST] {evt.ErrorId} {evt.Level} {evt.Category}/{evt.Source}: {evt.Message}");
        }
    }

    // ═══════════════════════════════════════
    // 匯出 CSV
    // ═══════════════════════════════════════

    /// <summary>
    /// 匯出指定期間的日誌為 CSV 檔案
    /// </summary>
    public async Task<string> ExportToCsvAsync(DateTime? startDate = null, DateTime? endDate = null, string? exportDir = null)
    {
        // 確保先將仍在記憶體佇列中的日誌寫入 DB
        if (_channel.Reader.TryPeek(out _))
        {
            // 此處為簡化，實際應用中應考慮使用 Flush() 或稍待背景寫入
            await Task.Delay(100); 
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventLogDbContext>();

        var query = db.SystemEvents.AsNoTracking().AsQueryable();

        if (startDate.HasValue)
        {
            var startStr = startDate.Value.ToUniversalTime().ToString("o");
            query = query.Where(e => string.Compare(e.Timestamp, startStr) >= 0);
        }

        if (endDate.HasValue)
        {
            // 將結束時間設為該日的最後一刻 (23:59:59)
            var endStr = endDate.Value.Date.AddDays(1).ToUniversalTime().ToString("o");
            query = query.Where(e => string.Compare(e.Timestamp, endStr) < 0);
        }

        var events = await query.OrderByDescending(e => e.Timestamp).ToListAsync();

        exportDir ??= Path.Combine(FindProjectRoot(), "ExportedLogs");
        Directory.CreateDirectory(exportDir);

        var fileName = $"EventLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(exportDir, fileName);

        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        // 寫入 UTF-8 BOM，讓 Excel 能正確辨識中文
        writer.Write("\xFEFF");

        // 寫入標題列
        await writer.WriteLineAsync("TimestampLocal,Level,Category,Source,EventCode,ErrorId,UserName,Message,Detail");

        foreach (var e in events)
        {
            var timestamp = EscapeCsv(e.TimestampLocal);
            var level = EscapeCsv(e.Level);
            var category = EscapeCsv(e.Category);
            var source = EscapeCsv(e.Source);
            var eventCode = EscapeCsv(e.EventCode);
            var errorId = EscapeCsv(e.ErrorId);
            var userName = EscapeCsv(e.UserName);
            var message = EscapeCsv(e.Message);
            var detail = EscapeCsv(e.Detail);

            await writer.WriteLineAsync($"{timestamp},{level},{category},{source},{eventCode},{errorId},{userName},{message},{detail}");
        }

        Console.WriteLine($"[EventLog] 已匯出 {events.Count} 筆日誌至 CSV: {filePath}");
        return filePath;
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        var str = field.Replace("\"", "\"\"");
        if (str.Contains(',') || str.Contains('\n') || str.Contains('\r') || str.Contains('"'))
        {
            return $"\"{str}\"";
        }
        return str;
    }

    // ═══════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════

    private int _disposed;

    public void Dispose()
    {
        // 防重入（ProcessExit + OnExit + Closing 可能同時觸發）
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        Console.WriteLine("[EventLog] Dispose — flushing remaining events...");
        _channel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            _consumerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { }

        Console.WriteLine("[EventLog] Dispose — flush completed");
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "Database")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return @"D:\TRIO2026";
    }
}
