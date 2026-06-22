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
    public const string AbnormalShutdownDetected = "WRN-1006";  // 偵測到上次非正常關閉

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
    public const string LockAdminAuthSuccess = "INF-9011";    // Admin 鎖定畫面驗證成功
    public const string LockAdminAuthFailed = "WRN-9012";     // Admin 驗證失敗（密碼錯誤/權限不足）
    public const string LockAdminForceLogout = "WRN-9013";    // Admin 強制登出另一使用者
    public const string LockAdminProxyUnlock = "WRN-9014";    // Admin 代理解鎖另一使用者的 Session

    // ── 4xxx USB Cybersecurity ──
    public const string UsbDeviceInserted = "INF-4010";       // USB 儲存裝置插入偵測
    public const string UsbFormatSuccess = "INF-4011";        // USB 快速格式化成功
    public const string UsbFormatFailed = "WRN-4012";         // USB 快速格式化失敗
    public const string UsbFormatCancelled = "INF-4013";      // 使用者取消格式化（含裝置拔除自動取消）
    public const string UsbFormatBlockedNonRemovable = "WRN-4014"; // 非可卸除式裝置被阻擋格式化
    public const string UsbScanClean = "INF-4015";            // USB 掃描通過（無威脅）
    public const string UsbScanThreatDetected = "WRN-4016";   // USB 掃描偵測到風險檔案（黑名單命中）
    public const string UsbScanSuspiciousFile = "WRN-4017";   // USB 掃描偵測到可疑檔案（不在白名單）
    public const string UsbDeviceRemoved = "INF-4018";        // USB 儲存裝置拔除
    public const string UsbGuestBlocked = "WRN-4019";         // Guest 模式下 USB 功能被禁用
    public const string UsbFormatPromptShown = "INF-4020";     // 格式化確認面板已彈出（等待使用者回應）
    public const string UsbFormatUserConfirmed = "INF-4021";   // 使用者已確認執行格式化（格式化開始前）
    public const string UsbNotAuthenticated = "WRN-4022";      // 未登入狀態下 USB 操作被攔截
    public const string UsbSessionLocked = "WRN-4023";          // 畫面鎖定時 USB 操作被攔截
    public const string UsbFormatSkipped = "INF-4024";           // USB 已是目標格式且無檔案，跳過格式化
    public const string UsbReadCheckStarted = "INF-4025";        // USB 讀取背景檢查開始（記錄模式 1/2）
    public const string UsbReadCheckPassed = "INF-4026";         // USB 讀取背景檢查通過（無威脅）
    public const string UsbReadCheckBlocked = "WRN-4027";        // USB 讀取背景檢查偵測威脅（模式 1 阻擋 / 模式 2 提示）
    public const string UsbReadCheckUserAcknowledged = "INF-4028"; // 模式 2：使用者已確認收到安全提醒

    // ── 10xxx Data Page ──
    public const string DataRecordView = "INF-10001";             // 檢視紀錄詳情
    public const string DataLayoutChanged = "INF-10002";          // 切換版面配置
    public const string DataSelectMode = "INF-10003";             // 進入/離開多選模式
    public const string DataAdminScopeChanged = "INF-10004";      // Admin 切換資料範圍
    public const string DataFilterApplied = "INF-10005";          // 套用篩選
    public const string DataExportStarted = "INF-10006";          // 匯出開始
    public const string DataExportCompleted = "INF-10007";        // 匯出完成
    public const string DataExportFailed = "WRN-10008";           // 匯出失敗
    public const string DataExportCancelled = "INF-10009";        // 使用者取消匯出
    public const string DataUsbRemoved = "WRN-10010";             // 匯出中 USB 被移除
    public const string DataFormatSkipped = "INF-10011";          // USB 空碟略過格式化
    public const string DataCyberBlocked = "WRN-10012";           // Cybersecurity 檢查未通過
    public const string DataLoadError = "ERR-10013";              // 資料載入失敗

    // ── 通用日誌 ──
    public const string GeneralInfo = "INF-0001";                 // 通用資訊事件
    public const string GeneralError = "ERR-0001";                // 通用錯誤事件
}
