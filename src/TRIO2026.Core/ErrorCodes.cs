namespace TRIO2026.Core;

/// <summary>
/// 事件代碼常數 — 對應 EventCodeDefinition 表的 Code 欄位
/// 
/// 命名規則：
///   INF-XNNN = Info（資訊事件）
///   WRN-XNNN = Warning（警告事件）
///   ERR-XNNN = Error / Fatal（錯誤/致命）
///   X = 分類碼（1=System, 2=Auth, 3=UV, 4=Hardware, 5=Config, 6=Navigation）
/// 
/// 程式碼中統一使用此常數引用，確保與 DB 對照表一致。
/// 新增時需同步更新此類別與 EventCodeDefinitionSeed。
/// 
/// 製作者: Office of William
/// </summary>
public static class ErrorCodes
{
    // ── 1xxx System ──
    public const string UnhandledException = "ERR-1001";
    public const string DatabaseConnectionFailure = "ERR-1002";
    public const string EventLogWriteFailure = "WRN-1003";
    public const string AppStartup = "INF-1004";
    public const string AppShutdown = "INF-1005";

    // ── 2xxx Auth ──
    public const string LoginFailed = "WRN-2001";
    public const string LoginSuccess = "INF-2002";
    public const string UserLogout = "INF-2003";
    public const string ServiceModeLogin = "INF-2004";
    public const string ExitServiceMode = "INF-2005";
    public const string ForcePasswordChange = "INF-2006";
    public const string PasswordChanged = "INF-2007";
    public const string PasswordChangeFailed = "WRN-2008";

    // ── 3xxx UV ──
    public const string UvStart = "INF-3001";
    public const string UvStop = "WRN-3002";
    public const string UvComplete = "INF-3003";
    public const string UvDoorInterrupted = "ERR-3004"; // 門板中斷 — Error 等級（需 CFS 回報）
    public const string UvLampFailure = "ERR-3005";
    public const string UvConfigLoadFailure = "ERR-3006";
    public const string UvStartBlockedByDoor = "WRN-3007"; // 門板開啟時嘗試啟動 UV
    public const string UvDoorResumed = "INF-3008";         // 門板關閉，UV 恢復照射
    public const string UvDurationChanged = "INF-3009";     // 使用者切換照射時長
    public const string UvPageEnter = "INF-3010";           // 進入 UV 頁面
    public const string UvPageLeave = "INF-3011";           // 離開 UV 頁面

    // ── 4xxx Hardware ──
    public const string HardwareCommunicationFailure = "ERR-4001";

    // ── 5xxx Config ──
    public const string ConfigLoadFailure = "WRN-5001";

    // ── 6xxx Navigation ──
    public const string PageNavigation = "INF-6001";

    // ── 7xxx UI / Interaction ──
    public const string UiButtonClick = "INF-7001";
    public const string UiMenuAction = "INF-7002";
    public const string UiInput = "INF-7003";

    // ── 8xxx Account Management ──
    public const string AccountCreated = "INF-8001";
    public const string AccountDeleted = "INF-8002";
    public const string AccountDisabled = "INF-8003";
    public const string AccountEnabled = "INF-8004";
    public const string AccountLocked = "INF-8005";
    public const string AccountUnlocked = "INF-8006";
    public const string PasswordReset = "INF-8007";

    // ── 9xxx Guest / Access Control ──
    public const string GuestLoginSuccess = "INF-9001";
    public const string GuestLoginBlocked = "WRN-9002";
    public const string GuestNavigationBlocked = "WRN-9003";
    public const string GuestRestrictionApplied = "INF-9004";
    public const string SystemAccountGuard = "WRN-9005";
    public const string SessionLocked = "INF-9006";
    public const string SessionUnlocked = "INF-9007";
    public const string LockInvalidPassword = "WRN-9008";
    public const string LockSwitchUser = "INF-9009";
    public const string LockPassthroughMsg = "INF-9010";
}
