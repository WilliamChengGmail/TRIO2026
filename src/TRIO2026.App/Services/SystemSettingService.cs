using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.Data.Contexts;

namespace TRIO2026.App.Services;

/// <summary>
/// 系統設定服務 — 從 system_config.db 的 SystemSetting 表讀取設定
/// 
/// 所有便利屬性皆為即時 DB 讀取，修改 DB 值不需重啟即可生效。
/// 啟動時 LoadAsync() 用於初始化驗證與日誌輸出。
/// 
/// 值域規則：
///   - auto_close_seconds: ≤0 視為不自動關閉
///   - multilanguage_enabled: 僅 "1" 為啟用，其餘值（含非0非1）皆視為停用
/// 
/// 製作者: Office of William
/// </summary>
public class SystemSettingService
{
    private readonly IServiceProvider _serviceProvider;

    public SystemSettingService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>啟動時載入驗證（日誌輸出用）</summary>
    public async Task LoadAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SystemConfigDbContext>();
        var count = await db.SystemSettings.CountAsync();
        Console.WriteLine($"[SystemSettingService] system_config.db 共 {count} 筆系統設定");
    }

    // ═══════════════════════════════════════
    // 即時 DB 讀取（每次呼叫都查 DB，改值不需重啟）
    // ═══════════════════════════════════════

    /// <summary>直接從 DB 讀取字串值</summary>
    public string GetLiveString(string category, string key, string defaultValue = "")
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SystemConfigDbContext>();
            var setting = db.SystemSettings
                .FirstOrDefault(s => s.Category == category && s.Key == key);
            return setting?.Value ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>直接從 DB 讀取整數值</summary>
    public int GetLiveInt(string category, string key, int defaultValue = 0)
    {
        var val = GetLiveString(category, key);
        return int.TryParse(val, out var n) ? n : defaultValue;
    }

    // ═══════════════════════════════════════
    // 便利屬性（即時 DB 讀取，改 DB 不需重啟）
    // ═══════════════════════════════════════

    /// <summary>
    /// 使用者選單自動關閉秒數（預設 10）
    /// ≤0 = 不自動關閉（負數也視為 0）
    /// </summary>
    public int UserMenuAutoCloseSeconds
    {
        get
        {
            var val = GetLiveInt("UserMenu", "auto_close_seconds", 10);
            return val < 0 ? 0 : val;
        }
    }

    /// <summary>
    /// 是否啟用多語系（預設 true）
    /// 僅 "1" 為啟用，其餘值（含 "2", "abc", "" 等）皆視為停用
    /// </summary>
    public bool MultiLanguageEnabled
    {
        get
        {
            var val = GetLiveString("System", "multilanguage_enabled", "1");
            return val == "1";
        }
    }

    /// <summary>
    /// 系統預設語系（當未登入或 login_required=0 時使用）
    /// 預設為 "en"
    /// </summary>
    public string DefaultLanguage
    {
        get => GetLiveString("System", "default_language", "en");
        set => SetLiveString("System", "default_language", value);
    }

    /// <summary>
    /// 登入/首頁畫面的語系決定方式
    ///   - "last_user"：依據前一位使用者語系
    ///   - "fixed"：統一使用 DefaultLanguage
    /// </summary>
    public string LoginScreenLanguageMode
        => GetLiveString("System", "login_screen_language_mode", "last_user");

    /// <summary>
    /// 最後一位登入使用者的語系偏好
    /// 登入成功時自動寫入，App 重啟時讀取以決定登入頁語系
    /// </summary>
    public string? LastUserLanguage
    {
        get
        {
            var val = GetLiveString("System", "last_user_language", "");
            return string.IsNullOrEmpty(val) ? null : val;
        }
        set => SetLiveString("System", "last_user_language", value ?? "");
    }

    // ═══════════════════════════════════════
    // EventLog 歸檔設定
    // ═══════════════════════════════════════

    /// <summary>歸檔區間：monthly / weekly / quarterly</summary>
    public string ArchiveInterval
        => GetLiveString("EventLog", "archive_interval", "monthly");

    /// <summary>備份排程天數</summary>
    public int BackupScheduleDays
    {
        get
        {
            var val = GetLiveInt("EventLog", "backup_schedule_days", 30);
            return val < 1 ? 30 : val;
        }
    }

    /// <summary>上次歸檔日期</summary>
    public DateTime? LastArchiveDate
    {
        get
        {
            var val = GetLiveString("EventLog", "last_archive_date");
            return DateTime.TryParse(val, out var d) ? d : null;
        }
    }

    /// <summary>上次備份搬移日期</summary>
    public DateTime? LastBackupDate
    {
        get
        {
            var val = GetLiveString("EventLog", "last_backup_date");
            return DateTime.TryParse(val, out var d) ? d : null;
        }
    }

    // ═══════════════════════════════════════
    // 寫入 API
    // ═══════════════════════════════════════

    /// <summary>寫入設定值到 DB</summary>
    public void SetLiveString(string category, string key, string value)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SystemConfigDbContext>();
            var setting = db.SystemSettings
                .FirstOrDefault(s => s.Category == category && s.Key == key);

            if (setting != null)
            {
                setting.Value = value;
            }
            else
            {
                setting = new TRIO2026.Core.Entities.SystemSetting
                {
                    Category = category,
                    Key = key,
                    Value = value,
                    Description = "Auto-generated",
                    Remark = ""
                };
                db.SystemSettings.Add(setting);
            }
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SystemSettingService] SetLiveString failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════
    // App Close 設定（轉移自 trio240plus_config.db）
    // ═══════════════════════════════════════

    /// <summary>關閉按鈕是否顯示</summary>
    public bool CloseButtonEnabled
        => GetLiveString("AppClose", "button_enabled", "0") == "1";

    /// <summary>ESC 鍵關閉是否啟用</summary>
    public bool EscKeyCloseEnabled
        => GetLiveString("AppClose", "esc_key_enabled", "1") == "1";

    /// <summary>Alt+F4 關閉是否啟用</summary>
    public bool AltF4CloseEnabled
        => GetLiveString("AppClose", "alt_f4_enabled", "0") == "1";

    // ═══════════════════════════════════════
    // Login UI 設定（轉移自 trio240plus_config.db）
    // ═══════════════════════════════════════

    /// <summary>登入頁面是否顯示使用者下拉清單</summary>
    public bool ShowUserDropdown
        => GetLiveString("LoginUI", "show_user_dropdown", "0") == "1";

    /// <summary>是否允許記住密碼</summary>
    public bool RememberPasswordEnabled
        => GetLiveString("LoginUI", "remember_password_enabled", "1") == "1";

    /// <summary>最大連續登入失敗次數</summary>
    public int MaxFailedAttempts
        => GetLiveInt("LoginUI", "max_failed_attempts", 5);

    /// <summary>帳號鎖定時間（分鐘）</summary>
    public int LockoutMinutes
        => GetLiveInt("LoginUI", "lockout_minutes", 15);

    /// <summary>Session 閒置逾時（分鐘，0=不逾時）</summary>
    public int SessionTimeoutMinutes
        => GetLiveInt("LoginUI", "session_timeout_minutes", 15);

    /// <summary>Session 超時動作（lock=鎖定畫面 / logout=完整登出）</summary>
    public string SessionTimeoutAction
        => GetLiveString("LoginUI", "session_timeout_action", "lock");

    /// <summary>鎖定前倒數警告秒數（0=不預警）</summary>
    public int SessionTimeoutWarningSeconds
        => GetLiveInt("LoginUI", "session_timeout_warning_seconds", 60);

    /// <summary>鎖定畫面是否顯示 Admin 介入按鈕</summary>
    public bool LockScreenSwitchUserEnabled
        => GetLiveString("LoginUI", "lock_screen_switch_user_enabled", "0") == "1";

    /// <summary>Admin 鎖定畫面操作：logout=強制登出, unlock=代理解鎖</summary>
    public string LockScreenAdminAction
        => GetLiveString("LoginUI", "lock_screen_admin_action", "logout");

    /// <summary>底部狀態列是否顯示 Session Timeout 倒數</summary>
    public bool SessionTimeoutCountdownVisible
        => GetLiveString("LoginUI", "session_timeout_countdown_visible", "0") == "1";

    // ═══════════════════════════════════════
    // Auth 設定（轉移自 trio240plus_config.db）
    // ═══════════════════════════════════════

    /// <summary>是否啟動帳號密碼檢查（預設 false = 免登入）</summary>
    public bool LoginRequired
        => GetLiveString("Auth", "login_required", "0") == "1";

    /// <summary>Init 畫面等待秒數（預設 2）</summary>
    public int InitWaitSeconds
        => GetLiveInt("Auth", "init_wait_seconds", 2);

    /// <summary>免登入時預設角色等級（預設 1 = Operator）</summary>
    public int DefaultRoleLevel
        => GetLiveInt("Auth", "default_role_level", 1);

    /// <summary>免登入帳號的 username（預設 local_operator）</summary>
    public string GuestAccountUsername
        => GetLiveString("Auth", "guest_account_username", "local_operator");

    /// <summary>免登入時顯示的名稱（預設 Local Operator）</summary>
    public string GuestAccountDisplayName
        => GetLiveString("Auth", "guest_account_display_name", "Local Operator");

    /// <summary>是否啟用 Guest 免密碼帳號登入（預設 false）</summary>
    public bool GuestLoginEnabled
        => GetLiveString("Auth", "guest_login_enabled", "0") == "1";

    /// <summary>Guest 登入後是否允許切換語系（預設 false）</summary>
    public bool GuestMultiLanguageEnabled
        => GetLiveString("Auth", "guest_multilanguage_enabled", "0") == "1";

    // ═══════════════════════════════════════
    // Device 設定
    // ═══════════════════════════════════════

    /// <summary>
    /// 裝置運作模式（Combo / IntelliPlex / Custom）
    /// 無法辨識的值一律 fallback 為 IntelliPlex
    /// </summary>
    public string DeviceOperationMode
    {
        get
        {
            var val = GetLiveString("Device", "operation_mode", "IntelliPlex");
            return val switch
            {
                "Combo" => "Combo",
                "IntelliPlex" => "IntelliPlex",
                "Custom" => "Custom",
                _ => "IntelliPlex"   // 無法辨識 → 預設
            };
        }
    }

    // ═══════════════════════════════════════
    // PasswordPolicy 設定（密碼原則）
    // ═══════════════════════════════════════

    /// <summary>密碼原則是否啟用</summary>
    public bool PasswordPolicyEnabled
        => GetLiveString("PasswordPolicy", "enabled", "1") == "1";

    /// <summary>Operator 最短密碼長度</summary>
    public int OperatorMinLength
        => GetLiveInt("PasswordPolicy", "operator_min_length", 6);

    /// <summary>Operator 最大密碼長度</summary>
    public int OperatorMaxLength
        => GetLiveInt("PasswordPolicy", "operator_max_length", 20);

    /// <summary>Operator 是否要求英數混合（0=允許純數字）</summary>
    public bool OperatorRequireMixed
        => GetLiveString("PasswordPolicy", "operator_require_mixed", "0") == "1";

    /// <summary>Operator 是否要求含特殊符號</summary>
    public bool OperatorRequireSpecial
        => GetLiveString("PasswordPolicy", "operator_require_special", "0") == "1";

    /// <summary>Admin/Service 最短密碼長度</summary>
    public int AdminMinLength
        => GetLiveInt("PasswordPolicy", "admin_min_length", 10);

    /// <summary>Admin/Service 最大密碼長度</summary>
    public int AdminMaxLength
        => GetLiveInt("PasswordPolicy", "admin_max_length", 64);

    /// <summary>Admin/Service 是否要求含大寫字母</summary>
    public bool AdminRequireUpper
        => GetLiveString("PasswordPolicy", "admin_require_upper", "1") == "1";

    /// <summary>Admin/Service 是否要求含小寫字母</summary>
    public bool AdminRequireLower
        => GetLiveString("PasswordPolicy", "admin_require_lower", "1") == "1";

    /// <summary>Admin/Service 是否要求含數字</summary>
    public bool AdminRequireDigit
        => GetLiveString("PasswordPolicy", "admin_require_digit", "1") == "1";

    /// <summary>Admin/Service 是否要求含特殊符號</summary>
    public bool AdminRequireSpecial
        => GetLiveString("PasswordPolicy", "admin_require_special", "0") == "1";

    /// <summary>
    /// 是否啟用「僅限動態數字鍵盤輸入密碼」模式。
    /// 啟用時：密碼只能透過隨機排列的數字鍵盤輸入，
    /// 且自動忽略複雜度規則（[使用數字當作密碼]僅保留長度限制）。
    /// </summary>
    public bool NumericKeypadOnly
        => PasswordPolicyEnabled && GetLiveString("PasswordPolicy", "numeric_keypad_only", "0") == "1";

    // ═══════════════════════════════════════
    // AccountMgmt 設定
    // ═══════════════════════════════════════

    /// <summary>是否啟用帳號手動鎖定/解鎖功能（控制 UI 按鈕可見性）</summary>
    public bool AccountLockEnabled
        => GetLiveString("AccountMgmt", "account_lock_enabled", "0") == "1";

    /// <summary>
    /// 帳號詳細資料要顯示的欄位 key 集合。
    /// DB 值格式為逗號分隔，例: "Username,DisplayName,Role,Status"
    /// </summary>
    public HashSet<string> UserDetailVisibleFields
    {
        get
        {
            var raw = GetLiveString("AccountMgmt", "user_detail_visible_fields",
                "Username,DisplayName,Role,Status,EmployeeId,Department,Email,LastLogin,PasswordChanged,ForceChange,LockedUntil,FailedCount,Created,CreatedBy,Notes");
            return new HashSet<string>(
                raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    // ═══════════════════════════════════════
    // UsbSecurity 設定 — USB 資安專碟專用
    // ═══════════════════════════════════════

    /// <summary>USB 資安專碟專用總開關</summary>
    public bool UsbCybersecurityEnabled
        => GetLiveString("UsbSecurity", "usb_cybersecurity_enabled", "0") == "1";

    /// <summary>偵測到 USB 插入時是否觸發快速格式化提示</summary>
    public bool UsbAutoFormatOnInsert
        => GetLiveString("UsbSecurity", "usb_auto_format_on_insert", "0") == "1";

    /// <summary>格式化確認按鈕延遲出現秒數（防誤觸）</summary>
    public int UsbFormatConfirmDelaySeconds
        => int.TryParse(GetLiveString("UsbSecurity", "usb_format_confirm_delay_seconds", "2"), out var v) ? v : 2;

    /// <summary>是否掃描 USB 內容中已知有風險的檔案</summary>
    public bool UsbContentScanEnabled
        => GetLiveString("UsbSecurity", "usb_content_scan_enabled", "0") == "1";

    /// <summary>安全檔案副檔名白名單（逗號分隔）</summary>
    public string UsbScanSafeExtensions
        => GetLiveString("UsbSecurity", "usb_scan_safe_extensions", ".pdf,.csv,.xlsx,.docx,.txt,.png,.jpg,.xml,.json");

    /// <summary>封鎖檔案副檔名黑名單（逗號分隔）</summary>
    public string UsbScanBlockExtensions
        => GetLiveString("UsbSecurity", "usb_scan_block_extensions", ".exe,.bat,.cmd,.ps1,.vbs,.js,.msi,.scr,.dll,.sys,.com,.inf,.reg,.bin");

    /// <summary>儀器專用檔案白名單（逗號分隔精確檔名），優先於 block_extensions</summary>
    public string UsbScanAllowedFiles
        => GetLiveString("UsbSecurity", "usb_scan_allowed_files", "");

    /// <summary>GUI 讀取隨身碟時背景檢查非法格式檔案（0=否, 1=是並阻擋, 2=是僅提示）</summary>
    public int UsbReadBackgroundCheck
        => int.TryParse(GetLiveString("UsbSecurity", "usb_read_background_check", "0"), out var v) ? v : 0;

    /// <summary>GUI 寫入前是否執行快速格式化（預留，若功能 1 已執行則跳過）</summary>
    public bool UsbFormatBeforeWrite
        => GetLiveString("UsbSecurity", "usb_format_before_write", "0") == "1";

    // ═══════════════════════════════════════
    // DataPage — 數據紀錄頁面
    // ═══════════════════════════════════════

    /// <summary>清單頁版面配置：card / compact / table</summary>
    public string DataListLayout
        => GetLiveString("DataPage", "data_list_layout", "card");

    /// <summary>設定清單頁版面配置（寫入 DB）</summary>
    public void SetDataListLayout(string layout)
        => SetLiveString("DataPage", "data_list_layout", layout);
}
