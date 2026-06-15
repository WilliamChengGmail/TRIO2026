using TRIO2026.Core.Entities;

namespace TRIO2026.Data.Seeding;

/// <summary>
/// SystemSetting 種子資料（system_config.db）
/// 
/// 設定分類（依字母排序）：
///   - AccountMgmt    帳號管理設定
///   - AppClose       關閉控制設定
///   - Auth           認證設定
///   - Device         裝置運作模式設定
///   - EventLog       事件日誌歸檔設定
///   - LoginUI        登入介面設定
///   - PasswordPolicy 密碼原則設定（依角色分級）
///   - System         系統全域設定
///   - UsbSecurity    USB 資安專碟專用設定
///   - UserMenu       使用者選單相關設定
/// 
/// 製作者: Office of William
/// </summary>
public static class SystemSettingSeed
{
    public static List<SystemSetting> GetSeedData()
    {
        return new List<SystemSetting>
        {
            // ══════════════════════════════════════
            // AccountMgmt — 帳號管理
            // ══════════════════════════════════════
            new()
            {
                Id = 1,
                Category = "AccountMgmt",
                Key = "account_lock_enabled",
                Value = "0",
                Description = "是否啟用帳號手動鎖定/解鎖功能（0=按鈕隱藏；1=啟用）",
                Remark = "✅ 已實作 — AccountManagementPage 控制鎖定/解鎖按鈕可見性"
            },
            new()
            {
                Id = 2,
                Category = "AccountMgmt",
                Key = "user_detail_visible_fields",
                Value = "Username,DisplayName,Role,Status,EmployeeId,Department,Email,LastLogin,PasswordChanged,ForceChange,LockedUntil,FailedCount,Created,CreatedBy,Notes",
                Description = "帳號詳細資料顯示欄位（逗號分隔，移除不需要的欄位即可隱藏）全部欄位 (Username,DisplayName,Role,Status,EmployeeId,Department,Email,LastLogin,PasswordChanged,ForceChange,LockedUntil,FailedCount,Created,CreatedBy,Notes)",
                Remark = "✅ 已實作 — AccountManagementPage.OnViewDetailsClick 讀取過濾"
            },

            // ══════════════════════════════════════
            // AppClose — 關閉控制
            // ══════════════════════════════════════
            new()
            {
                Id = 3,
                Category = "AppClose",
                Key = "button_enabled",
                Value = "0",
                Description = "關閉按鈕是否顯示（0=隱藏, 1=顯示）",
                Remark = "✅ 已實作 — LoginPage.cs 控制 CloseButton 可見性"
            },
            new()
            {
                Id = 4,
                Category = "AppClose",
                Key = "esc_key_enabled",
                Value = "1",
                Description = "ESC 鍵關閉是否啟用（0=停用, 1=啟用）",
                Remark = "✅ 已實作 — AppShell.cs PreviewKeyDown 攔截"
            },
            new()
            {
                Id = 5,
                Category = "AppClose",
                Key = "alt_f4_enabled",
                Value = "0",
                Description = "Alt+F4 關閉是否啟用（0=停用, 1=啟用）",
                Remark = "✅ 已實作 — AppShell.cs Closing 事件攔截"
            },

            // ══════════════════════════════════════
            // Auth — 認證設定
            // ══════════════════════════════════════
            new()
            {
                Id = 6,
                Category = "Auth",
                Key = "login_required",
                Value = "0",
                Description = "是否啟動帳號密碼檢查（0=免登入, 1=需登入）",
                Remark = "✅ 已實作 — AppShell.cs 讀取決定起始頁面"
            },
            new()
            {
                Id = 7,
                Category = "Auth",
                Key = "init_wait_seconds",
                Value = "2",
                Description = "Init 畫面等待秒數",
                Remark = "✅ 已實作 — InitPage.cs 讀取控制倒數秒數"
            },
            new()
            {
                Id = 8,
                Category = "Auth",
                Key = "default_role_level",
                Value = "1",
                Description = "免登入時預設角色等級（1=Operator, 2=Service, 3=Admin）",
                Remark = "✅ 已實作 — AppShell.cs 免登入模式設定 Guest Session"
            },
            new()
            {
                Id = 9,
                Category = "Auth",
                Key = "guest_account_username",
                Value = "local_operator",
                Description = "免登入模式專用帳號的 username（對應 main.db User 表）",
                Remark = "✅ 已實作 — AppShell.cs 免登入模式從 DB 載入此帳號"
            },
            new()
            {
                Id = 10,
                Category = "Auth",
                Key = "guest_account_display_name",
                Value = "Local Operator",
                Description = "免登入模式右上角顯示的名稱",
                Remark = "✅ 已實作 — SessionService + UserMenuControl 讀取顯示"
            },
            new()
            {
                Id = 37,
                Category = "Auth",
                Key = "guest_login_enabled",
                Value = "0",
                Description = "是否啟用 Guest 免密碼帳號登入（0=停用, 1=啟用；啟用時 guest 帳號可免密碼登入）",
                Remark = "✅ 已實作 — LoginViewModel + LoginPage 控制免密碼登入流程"
            },
            new()
            {
                Id = 38,
                Category = "Auth",
                Key = "guest_multilanguage_enabled",
                Value = "0",
                Description = "Guest 登入後是否允許切換語系（0=使用預設語系, 1=可切換語系）",
                Remark = "✅ 已實作 — UserMenuControl 控制 Guest 語系按鈕可見性"
            },

            // ══════════════════════════════════════
            // Device — 裝置運作模式
            // ══════════════════════════════════════
            new()
            {
                Id = 11,
                Category = "Device",
                Key = "operation_mode",
                Value = "IntelliPlex",
                Description = "裝置運作模式（Combo=雙模式皆啟用, IntelliPlex=僅 IntelliPlex, Custom=僅 Custom）",
                Remark = "✅ 已實作 — MenuPage.cs 讀取控制功能按鈕啟用狀態"
            },

            // ══════════════════════════════════════
            // EventLog — 事件日誌歸檔
            // ══════════════════════════════════════
            new()
            {
                Id = 12,
                Category = "EventLog",
                Key = "archive_interval",
                Value = "monthly",
                Description = "事件日誌歸檔區間（monthly=按月, weekly=按週, quarterly=按季）",
                Remark = "✅ 已實作 — EventLogArchiveService.cs 讀取控制歸檔週期"
            },
            new()
            {
                Id = 13,
                Category = "EventLog",
                Key = "backup_schedule_days",
                Value = "30",
                Description = "歸檔 DB 搬移至備份目錄的排程天數（預設 30 天）",
                Remark = "✅ 已實作 — EventLogArchiveService.cs 控制備份搬移排程"
            },
            new()
            {
                Id = 14,
                Category = "EventLog",
                Key = "last_archive_date",
                Value = "",
                Description = "上次歸檔執行日期（系統自動更新，格式 yyyy-MM-dd）",
                Remark = "✅ 已實作 — EventLogArchiveService.cs 自動寫入"
            },
            new()
            {
                Id = 15,
                Category = "EventLog",
                Key = "last_backup_date",
                Value = "",
                Description = "上次備份搬移執行日期（系統自動更新，格式 yyyy-MM-dd）",
                Remark = "✅ 已實作 — EventLogArchiveService.cs 自動寫入"
            },

            // ══════════════════════════════════════
            // LoginUI — 登入介面
            // ══════════════════════════════════════
            new()
            {
                Id = 16,
                Category = "LoginUI",
                Key = "show_user_dropdown",
                Value = "0",
                Description = "登入頁面是否顯示使用者下拉清單（0=停用, 1=啟用）",
                Remark = "✅ 已實作 — LoginPage 根據此設定切換帳號文字框/下拉選單"
            },
            new()
            {
                Id = 17,
                Category = "LoginUI",
                Key = "remember_password_enabled",
                Value = "0",
                Description = "是否允許記住密碼功能（0=停用, 1=啟用）",
                Remark = "✅ 已實作 — LoginPage/ViewModel 根據此開關控制 CheckBox 與 credentials 存取"
            },
            new()
            {
                Id = 18,
                Category = "LoginUI",
                Key = "max_failed_attempts",
                Value = "5",
                Description = "最大連續登入失敗次數（超過後鎖定帳號）",
                Remark = "✅ 已實作 — AuthService 從 DB 讀取"
            },
            new()
            {
                Id = 19,
                Category = "LoginUI",
                Key = "lockout_minutes",
                Value = "15",
                Description = "帳號鎖定持續時間（分鐘）",
                Remark = "✅ 已實作 — AuthService 從 DB 讀取"
            },
            new()
            {
                Id = 20,
                Category = "LoginUI",
                Key = "session_timeout_minutes",
                Value = "15",
                Description = "Session 閒置逾時（分鐘，0=不逾時）",
                Remark = "✅ 已實作 — IdleTimerService 讀取"
            },

            // ══════════════════════════════════════
            // PasswordPolicy — 密碼原則
            // ══════════════════════════════════════
            new()
            {
                Id = 21,
                Category = "PasswordPolicy",
                Key = "enabled",
                Value = "1",
                Description = "密碼原則是否啟用（0=不檢查，任何密碼都放行；1=依角色規則驗證）",
                Remark = "✅ 已實作 — PasswordPolicyService.Validate() 讀取"
            },
            new()
            {
                Id = 22,
                Category = "PasswordPolicy",
                Key = "operator_min_length",
                Value = "6",
                Description = "Operator 最短密碼長度",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 23,
                Category = "PasswordPolicy",
                Key = "operator_max_length",
                Value = "20",
                Description = "Operator 最大密碼長度",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 24,
                Category = "PasswordPolicy",
                Key = "operator_require_mixed",
                Value = "0",
                Description = "Operator 是否要求英數混合（0=允許純數字 PIN；1=需含英文字母+數字）",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 25,
                Category = "PasswordPolicy",
                Key = "operator_require_special",
                Value = "0",
                Description = "Operator 是否要求含特殊符號（預設停用）",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 26,
                Category = "PasswordPolicy",
                Key = "admin_min_length",
                Value = "10",
                Description = "Admin/Service 最短密碼長度",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 27,
                Category = "PasswordPolicy",
                Key = "admin_max_length",
                Value = "64",
                Description = "Admin/Service 最大密碼長度（BCrypt 72B 安全範圍內，保留 8B 餘裕）",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 28,
                Category = "PasswordPolicy",
                Key = "admin_require_upper",
                Value = "1",
                Description = "Admin/Service 是否要求含大寫字母",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 29,
                Category = "PasswordPolicy",
                Key = "admin_require_lower",
                Value = "1",
                Description = "Admin/Service 是否要求含小寫字母",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 30,
                Category = "PasswordPolicy",
                Key = "admin_require_digit",
                Value = "1",
                Description = "Admin/Service 是否要求含數字",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 31,
                Category = "PasswordPolicy",
                Key = "admin_require_special",
                Value = "0",
                Description = "Admin/Service 是否要求含特殊符號（預設停用）",
                Remark = "✅ 已實作 — PasswordPolicyService"
            },
            new()
            {
                Id = 32,
                Category = "PasswordPolicy",
                Key = "numeric_keypad_only",
                Value = "0",
                Description = "[密碼格式為數字]僅限動態數字鍵盤輸入密碼（0=停用, 1=啟用；啟用時自動忽略複雜度規則，僅保留長度限制）",
                Remark = "✅ 已實作 — LoginPage 動態數字鍵盤 + PasswordPolicyService 規則過濾"
            },

            // ══════════════════════════════════════
            // System — 系統全域設定
            // ══════════════════════════════════════
            new()
            {
                Id = 33,
                Category = "System",
                Key = "multilanguage_enabled",
                Value = "1",
                Description = "是否啟用多語系功能（1=啟用, 0=停用，停用時以 English 為預設語言）",
                Remark = "✅ 已實作 — UserMenuControl.cs 控制語系按鈕可見性"
            },
            new()
            {
                Id = 34,
                Category = "System",
                Key = "default_language",
                Value = "en",
                Description = "系統預設語系（當未登入或免登入模式時使用，例: en, zh-TW）",
                Remark = "✅ 已實作 — App.xaml.cs 啟動時初始化 + UserMenuControl 免登入模式切換語系時寫入"
            },
            new()
            {
                Id = 35,
                Category = "System",
                Key = "login_screen_language_mode",
                Value = "last_user",
                Description = "登入/首頁畫面的語系決定方式：last_user=依據前一位使用者語系 | fixed=統一使用 default_language",
                Remark = "✅ 已實作 — AppShell 登出/退出時切換語系"
            },

            // ══════════════════════════════════════
            // UserMenu — 使用者選單
            // ══════════════════════════════════════
            new()
            {
                Id = 36,
                Category = "UserMenu",
                Key = "auto_close_seconds",
                Value = "10",
                Description = "使用者選單自動關閉秒數（預設 10 秒，0=不自動關閉）",
                Remark = "✅ 已實作 — UserMenuControl.cs 讀取控制自動關閉"
            },

            // ══════════════════════════════════════
            // LoginUI — Session Lock（閒置鎖定）
            // ══════════════════════════════════════
            new()
            {
                Id = 37,
                Category = "LoginUI",
                Key = "session_timeout_action",
                Value = "lock",
                Description = "Session 超時動作（lock=鎖定畫面 / logout=完整登出）",
                Remark = "✅ 已實作 — AppShell 讀取控制超時行為"
            },
            new()
            {
                Id = 38,
                Category = "LoginUI",
                Key = "session_timeout_warning_seconds",
                Value = "60",
                Description = "鎖定前倒數警告秒數（0=不預警）",
                Remark = "✅ 已實作 — IdleTimerService 觸發 WarningTriggered"
            },
            new()
            {
                Id = 39,
                Category = "LoginUI",
                Key = "lock_screen_switch_user_enabled",
                Value = "0",
                Description = "鎖定畫面是否顯示『Admin 介入』按鈕（0=隱藏, 1=顯示，需 Admin 帳密驗證）",
                Remark = "✅ 已實作 — LockScreenOverlay 控制按鈕顯示，需 Admin 驗證"
            },
            new()
            {
                Id = 40,
                Category = "LoginUI",
                Key = "session_timeout_countdown_visible",
                Value = "0",
                Description = "是否在底部狀態列顯示 Session Timeout 倒數（0=隱藏, 1=顯示）",
                Remark = "✅ 已實作 — MenuPage / UvPage 底部列倒數顯示"
            },
            new()
            {
                Id = 41,
                Category = "LoginUI",
                Key = "lock_screen_admin_action",
                Value = "logout",
                Description = "Admin 鎖定畫面驗證後動作（logout=強制登出回登入頁, unlock=代理解鎖繼續操作）",
                Remark = "✅ 已實作 — LockScreenOverlay 依此設定決定 Admin 介入後行為"
            },

            // ══════════════════════════════════════
            // UsbSecurity — USB 資安專碟專用
            // ══════════════════════════════════════
            new()
            {
                Id = 42,
                Category = "UsbSecurity",
                Key = "usb_cybersecurity_enabled",
                Value = "0",
                Description = "USB 資安專碟專用總開關（0=停用, 1=啟用）。停用時所有子功能一律不執行",
                Remark = "✅ 已實作 — UsbSecurityService 總開關"
            },
            new()
            {
                Id = 43,
                Category = "UsbSecurity",
                Key = "usb_auto_format_on_insert",
                Value = "0",
                Description = "偵測到 USB 隨身碟插入時是否觸發快速格式化提示（0=否, 1=是）",
                Remark = "✅ 已實作 — 僅限 Removable Disk，嚴禁完整格式化，使用 exFAT"
            },
            new()
            {
                Id = 44,
                Category = "UsbSecurity",
                Key = "usb_format_confirm_delay_seconds",
                Value = "2",
                Description = "格式化確認對話框中「執行」按鈕的延遲出現秒數（防止誤觸）",
                Remark = "✅ 已實作 — UsbFormatConfirmOverlay 面板按鈕停等 N 秒後才可點選"
            },
            new()
            {
                Id = 45,
                Category = "UsbSecurity",
                Key = "usb_content_scan_enabled",
                Value = "0",
                Description = "是否掃描 USB 內容中已知有風險的檔案（0=否, 1=是）",
                Remark = "✅ 已實作 — 以副檔名黑白名單 + 精確檔名白名單為基礎（air-gapped，不接外部 CVE DB）"
            },
            new()
            {
                Id = 46,
                Category = "UsbSecurity",
                Key = "usb_scan_safe_extensions",
                Value = ".pdf,.csv,.xlsx,.docx,.txt,.png,.jpg,.xml,.json",
                Description = "安全檔案副檔名白名單（逗號分隔），掃描時放行",
                Remark = "✅ 已實作 — 依實際需求調整副檔名清單"
            },
            new()
            {
                Id = 47,
                Category = "UsbSecurity",
                Key = "usb_scan_block_extensions",
                Value = ".exe,.bat,.cmd,.ps1,.vbs,.js,.msi,.scr,.dll,.sys,.com,.inf,.reg,.bin",
                Description = "封鎖檔案副檔名黑名單（逗號分隔），偵測到即報警",
                Remark = "✅ 已實作 — .bin 可透過 usb_scan_allowed_files 排除"
            },
            new()
            {
                Id = 48,
                Category = "UsbSecurity",
                Key = "usb_read_background_check",
                Value = "0",
                Description = "GUI 讀取隨身碟時，背景檢查是否有非法格式檔案（0=否, 1=是）",
                Remark = "✅ 設定已預留 — 供後續 GUI 讀取模組介接"
            },
            new()
            {
                Id = 49,
                Category = "UsbSecurity",
                Key = "usb_format_before_write",
                Value = "0",
                Description = "GUI 寫入隨身碟前是否執行快速格式化（0=否, 1=是）。若功能 1 已執行過則自動跳過",
                Remark = "✅ 設定已預留 — 供後續 GUI 寫入模組介接"
            },
            new()
            {
                Id = 50,
                Category = "UsbSecurity",
                Key = "usb_scan_allowed_files",
                Value = "",
                Description = "儀器專用檔案白名單（逗號分隔精確檔名），優先於 block_extensions。例: firmware_v3.2.bin,calibration_data.bin",
                Remark = "✅ 已實作 — 允許特定檔名繞過副檔名黑名單封鎖"
            },

            // ══════════════════════════════════════
            // DataPage — 數據紀錄頁面設定
            // ══════════════════════════════════════
            new()
            {
                Id = 51,
                Category = "DataPage",
                Key = "data_list_layout",
                Value = "card",
                Description = "清單頁版面配置（card=卡片模式, compact=緊湊列表, table=表格模式）",
                Remark = "⏳ 待實作 — DataListPage 讀取此值決定版面"
            },
        };
    }
}
