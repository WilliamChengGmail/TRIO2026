namespace TRIO2026.Data.Extensions;

/// <summary>
/// 啟動階段日誌 — 在 EventLogService 初始化之前提供檔案級日誌記錄
/// 
/// 設計考量：
///   - DB 初始化階段 EventLogService 尚未可用（雞生蛋問題）
///   - 寫入純文字檔，格式與 EventLog 類似但更輕量
///   - 支援同時輸出到 Console 和檔案
///   - App 啟動完成後可關閉，後續由 EventLogService 接管
/// 
/// 檔案位置：Logs/startup-init-logs/startup_*.log
/// 
/// 製作者: Office of William
/// </summary>
public sealed class StartupLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _logPath;
    private bool _disposed;

    /// <summary>最近一次初始化的 StartupLogger 實例（可由外部切換）</summary>
    public static StartupLogger? Current { get; set; }

    /// <summary>是否有任何錯誤被記錄</summary>
    public bool HasErrors { get; private set; }

    public StartupLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logPath = Path.Combine(logDirectory, $"startup_{timestamp}.log");

        _writer = new StreamWriter(_logPath, append: false, encoding: System.Text.Encoding.UTF8)
        {
            AutoFlush = true
        };

        Current = this;

        WriteHeader();
    }

    private void WriteHeader()
    {
        _writer.WriteLine("═══════════════════════════════════════════════════════");
        _writer.WriteLine($"  TRIO2026 Startup Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        _writer.WriteLine("═══════════════════════════════════════════════════════");
        _writer.WriteLine();
    }

    /// <summary>記錄 Info 等級訊息</summary>
    public void Info(string source, string message, string? detail = null)
    {
        Write("INFO", source, message, detail);
    }

    /// <summary>記錄 Warning 等級訊息</summary>
    public void Warn(string source, string message, string? detail = null)
    {
        Write("WARN", source, message, detail);
    }

    /// <summary>記錄 Error 等級訊息</summary>
    public void Error(string source, string message, string? detail = null)
    {
        HasErrors = true;
        Write("ERROR", source, message, detail);
    }

    /// <summary>記錄 Exception</summary>
    public void Error(string source, string message, Exception ex)
    {
        HasErrors = true;
        Write("ERROR", source, message, $"{ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null)
            Write("ERROR", source, "InnerException", $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        _writer.WriteLine($"  StackTrace: {ex.StackTrace}");
    }

    private void Write(string level, string source, string message, string? detail)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level,-5}] [{source}] {message}";
        if (!string.IsNullOrEmpty(detail))
            line += $" | {detail}";

        _writer.WriteLine(line);
        Console.WriteLine(line);
    }

    /// <summary>取得 log 檔案的完整路徑</summary>
    public string LogPath => _logPath;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _writer.WriteLine();
        _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Startup log closed.");
        _writer.Dispose();

        if (Current == this)
            Current = null;
    }
}
