namespace TRIO2026.Core.Entities;

/// <summary>
/// 系統事件日誌 Entity — 對應 system_event.db 的 SystemEvent 表
/// 
/// 用途：結構化記錄系統級事件（錯誤、狀態變更、使用者操作追蹤）。
/// 與 trio240plus_log.db 的 OperationLog / CommunicationLog 完全獨立。
/// 
/// ErrorId 欄位引用 EventCodeDefinition 對照表的預定義代碼（如 INF-1004, WRN-2001, ERR-1001），
/// end user 可據此回報給 CFS/客服人員進行初步評估。
/// 
/// 製作者: Office of William
/// </summary>
public class SystemEvent
{
    public int Id { get; set; }

    // ── 時間與追蹤 ──
    /// <summary>ISO8601 UTC 時間戳</summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>本地時間戳（方便人工閱讀）</summary>
    public string TimestampLocal { get; set; } = string.Empty;

    /// <summary>關聯 ID — 追蹤同一操作鏈的多筆日誌</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 事件代碼 — 引用 EventCodeDefinition 對照表（如 INF-1004, ERR-1001）
    /// end user 可據此回報給 CFS/客服人員。
    /// 技術團隊透過對照表進行初步評估。
    /// </summary>
    public string? ErrorId { get; set; }

    // ── 分類 ──
    /// <summary>日誌等級: Trace/Debug/Info/Warning/Error/Fatal</summary>
    public string Level { get; set; } = "Info";

    /// <summary>分類: UV/Login/Navigation/Hardware/System</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>來源類別名稱（如 UvDecontaminationViewModel）</summary>
    public string Source { get; set; } = string.Empty;

    // ── 內容 ──
    /// <summary>事件代碼（如 UV_START, DOOR_OPEN）</summary>
    public string? EventCode { get; set; }

    /// <summary>事件訊息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>詳細資訊（JSON 或自由格式）</summary>
    public string? Detail { get; set; }

    // ── 錯誤追蹤 ──
    /// <summary>例外類別名稱</summary>
    public string? ExceptionType { get; set; }

    /// <summary>完整堆疊追蹤</summary>
    public string? StackTrace { get; set; }

    /// <summary>內部例外訊息</summary>
    public string? InnerException { get; set; }

    // ── 上下文 ──
    /// <summary>當前使用者 ID（對應 User 表主鍵）</summary>
    public int? UserId { get; set; }

    /// <summary>當前使用者名稱（快照，避免更名後無法追溯）</summary>
    public string? UserName { get; set; }

    /// <summary>Session ID</summary>
    public string? SessionId { get; set; }

    /// <summary>自訂標籤（JSON array）</summary>
    public string? Tags { get; set; }

    // ── 環境 ──
    /// <summary>機器名稱</summary>
    public string? MachineName { get; set; }

    /// <summary>應用版本</summary>
    public string? AppVersion { get; set; }

    // ═══════════════════════════════════════
    // 靜態工廠方法
    // ═══════════════════════════════════════

    private static SystemEvent Create(string level, string category, string source,
        string message, string? errorId = null, string? eventCode = null, string? detail = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new SystemEvent
        {
            Timestamp = now.ToString("o"),
            TimestampLocal = Helpers.TimeDisplayHelper.ToLocalTimestampWithOffset(now),
            ErrorId = errorId,
            Level = level,
            Category = category,
            Source = source,
            EventCode = eventCode,
            Message = message,
            Detail = detail,
            MachineName = Environment.MachineName,
            AppVersion = "2026.1.0"
        };
    }

    /// <summary>建立 Info 等級事件</summary>
    public static SystemEvent CreateInfo(string category, string source,
        string message, string? errorId = null, string? eventCode = null, string? detail = null)
        => Create("Info", category, source, message, errorId, eventCode, detail);

    /// <summary>建立 Warning 等級事件</summary>
    public static SystemEvent CreateWarning(string category, string source,
        string message, string? errorId = null, string? eventCode = null, string? detail = null)
        => Create("Warning", category, source, message, errorId, eventCode, detail);

    /// <summary>建立 Error 等級事件</summary>
    public static SystemEvent CreateError(string category, string source,
        string message, string? errorId = null, string? eventCode = null, string? detail = null)
        => Create("Error", category, source, message, errorId, eventCode, detail);

    /// <summary>建立 Fatal 等級事件</summary>
    public static SystemEvent CreateFatal(string category, string source,
        string message, string? errorId = null, string? eventCode = null, string? detail = null)
        => Create("Fatal", category, source, message, errorId, eventCode, detail);

    /// <summary>從例外建立 Error 事件</summary>
    public static SystemEvent CreateFromException(string category, string source,
        Exception ex, string? errorId = null, string? message = null, string? eventCode = null)
    {
        var evt = Create("Error", category, source,
            message ?? ex.Message, errorId, eventCode);
        evt.ExceptionType = ex.GetType().FullName;
        evt.StackTrace = ex.StackTrace;
        evt.InnerException = ex.InnerException?.Message;
        return evt;
    }
}
