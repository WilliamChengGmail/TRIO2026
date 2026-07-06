using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.App.Views.Pages;
using TRIO2026.Core;
using TRIO2026.Core.Entities;
using TRIO2026.Core.Enums;
using TRIO2026.Core.Interfaces;
using TRIO2026.Data.Contexts;

namespace TRIO2026.App.Views;

/// <summary>
/// 應用程式主殼層 — 單一 Window，所有頁面在此切換
/// 對應舊系統 MainWindow + widgetmap 的架構
/// </summary>
public partial class AppShell : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SessionService _sessionService;
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;
    private readonly UvConfigService _uvConfigService;
    private readonly IUvHardwareService _uvHardwareService;
    private readonly SystemSettingService _systemSettings;
    private readonly IdleTimerService _idleTimer = new();

    // 頁面實例（預先建立，hide/show 切換）
    private LoginPage? _loginPage;
    private InitPage? _initPage;
    private MenuPage? _menuPage;
    private UvDecontaminationPage? _uvPage;
    private ServiceModePage? _serviceModePage;
    private AccountManagementPage? _accountMgmtPage;
    private DataListPage? _dataListPage;

    public AppShell(IServiceProvider serviceProvider,
        SessionService sessionService,
        AuthService authService, TokenService tokenService,
        UvConfigService uvConfigService, IUvHardwareService uvHardwareService,
        SystemSettingService systemSettings)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _sessionService = sessionService;
        _authService = authService;
        _tokenService = tokenService;
        _uvConfigService = uvConfigService;
        _uvHardwareService = uvHardwareService;
        _systemSettings = systemSettings;


        // 視窗關閉事件 — 根據 DB 設定決定是否允許
        Closing += OnWindowClosing;

        // ESC 鍵 — 根據 DB 設定決定是否允許關閉
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_systemSettings.EscKeyCloseEnabled)
                {
                    EventLogService.Instance?.LogInfo("System", "AppShell", ErrorCodes.AppShutdown,
                        "ESC 鍵關閉");
                    Application.Current.Shutdown();
                }
                else
                {
                    e.Handled = true; // DB 設定為停用 → 攔截
                }
            }
        };

        // 初始化 ChangePasswordOverlay 服務
        var policyService = serviceProvider.GetRequiredService<PasswordPolicyService>();
        var authForOverlay = serviceProvider.GetRequiredService<AuthService>();
        ChangePasswordOverlayHost.Initialize(authForOverlay, policyService);

        // 初始化 USB 格式化確認面板
        var usbSecurityService = serviceProvider.GetRequiredService<IUsbSecurityService>();
        var locService = serviceProvider.GetRequiredService<LocalizationService>();
        
        UsbFormatConfirmHost.Completed += (s, confirmed) =>
        {
            if (UsbFormatConfirmHost.Tag is TRIO2026.App.Models.UsbDeviceInfo info)
            {
                usbSecurityService.ReportFormatResultAsync(info, confirmed);
            }
        };

        // 訂閱強制取消事件 — 記錄來源（SessionLock / DeviceRemoved 等）
        UsbFormatConfirmHost.ForceCancelled += (s, reason) =>
        {
            EventLogService.Instance?.LogWarning("UsbSecurity", "AppShell",
                ErrorCodes.GeneralInfo, "USB Format Force Cancelled",
                $"Reason={reason}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
        };

        usbSecurityService.FormatRequired += (s, info) =>
        {
            Dispatcher.Invoke(() =>
            {
                UsbFormatConfirmHost.Tag = info;
                UsbFormatConfirmHost.Show(locService, info, _systemSettings.UsbFormatConfirmDelaySeconds);
            });
        };

        // 訂閱 USB 讀取背景檢查完成事件
        usbSecurityService.ReadCheckCompleted += (s, args) =>
        {
            Dispatcher.Invoke(async () =>
            {
                var (info, mode, hasThreat) = args;
                var loc = LocalizationService.Instance;

                if (!hasThreat) return; // 通過 → 不彈窗

                if (mode == 1)
                {
                    // 埋點：阻擋模式 — 彈窗已顯示
                    EventLogService.Instance?.LogWarning("UsbSecurity", "AppShell",
                        ErrorCodes.UsbReadCheckBlocked, "USB Read Check Blocked Dialog Shown",
                        $"{info.ToLogString()} | Mode=1, Action=ShowBlockedDialog, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                    // 阻擋模式：顯示 Error 提示（僅「確定」按鈕）
                    await DialogOverlay.ShowAsync(
                        loc["UsbSecurity.ReadCheckBlocked.Title"] ?? "Security Alert",
                        loc["UsbSecurity.ReadCheckBlocked"] ?? "This USB drive contains potentially dangerous files. Access has been blocked.",
                        loc["Common.OK"],
                        Controls.OverlayDialogIcon.Error);

                    // 埋點：使用者已按下「確定」關閉阻擋警告
                    EventLogService.Instance?.LogInfo("UsbSecurity", "AppShell",
                        ErrorCodes.GeneralInfo, "USB Read Check Blocked Dialog Dismissed",
                        $"{info.ToLogString()} | Mode=1, Action=UserDismissedBlockedDialog, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                }
                else if (mode == 2)
                {
                    // 埋點：提示模式 — 彈窗已顯示
                    EventLogService.Instance?.LogInfo("UsbSecurity", "AppShell",
                        ErrorCodes.UsbReadCheckBlocked, "USB Read Check Warning Dialog Shown",
                        $"{info.ToLogString()} | Mode=2, Action=ShowWarningDialog, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                    // 提示模式：顯示 Warning + 「我已了解」按鈕
                    await DialogOverlay.ShowAsync(
                        loc["UsbSecurity.ReadCheckWarning.Title"] ?? "Security Notice",
                        loc["UsbSecurity.ReadCheckWarning"] ?? "This USB drive contains files that may pose a security risk. Please confirm you have been notified.",
                        loc["UsbSecurity.ReadCheckAcknowledged"] ?? "I Understand",
                        Controls.OverlayDialogIcon.Warning);

                    // 埋點：使用者已按下「我已了解」
                    EventLogService.Instance?.LogInfo("UsbSecurity", "AppShell",
                        ErrorCodes.UsbReadCheckUserAcknowledged, "USB Read Check Warning Acknowledged by User",
                        $"{info.ToLogString()} | Mode=2, Action=UserClickedAcknowledge, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                    // 回報 UsbSecurityService 解除等待
                    await usbSecurityService.ReportReadCheckAcknowledgedAsync(info);
                }
            });
        };
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 觸發 → 檢查 DB 設定
        if (!_systemSettings.AltF4CloseEnabled)
        {
            e.Cancel = true; // DB 設定為停用 → 取消關閉
            return;
        }

        // 允許關閉 → 記錄日誌
        EventLogService.Instance?.LogInfo("System", "AppShell", ErrorCodes.AppShutdown,
            "視窗關閉請求（Alt+F4）");
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // 根據設定決定起始頁面（讀取 system_config.db）
        if (_systemSettings.LoginRequired)
            NavigateTo("login");
        else
            NavigateTo("init");
    }

    // ═══════ 頁面切換（對應舊系統 slotswitchpage） ═══════

    private string? _currentPage;

    public void NavigateTo(string page)
    {
        var fromPage = _currentPage;
        _currentPage = page;

        // 通知舊頁面即將離開
        if (fromPage == "uv" && _uvPage != null)
            _uvPage.OnNavigatingFrom(page);

        // 操作追蹤：頁面導航
        EventLogService.Instance.LogNavigation(fromPage, page);

        // 資安守衛：Guest 帳號禁止進入受限頁面（data 允許進入但僅限個人資料）
        if (_sessionService.IsGuestLogin && page is "uv" or "service" or "accountMgmt")
        {
            EventLogService.Instance?.LogWarning("Auth", "AppShell",
                ErrorCodes.GuestNavigationBlocked,
                "Guest Navigation Blocked",
                $"From={fromPage ?? "None"}, Blocked={page}");
            return;
        }

        switch (page)
        {
            case "login":
                // 清除角色敏感的頁面快取（切換使用者後需重建）
                _dataListPage = null;
                _accountMgmtPage = null;
                _menuPage = null;
                _loginPage ??= CreateLoginPage();
                _loginPage.RefreshDisplay();
                PageHost.Content = _loginPage;
                break;

            case "init":
                _initPage ??= CreateInitPage();
                PageHost.Content = _initPage;
                break;

            case "menu":
                _menuPage ??= CreateMenuPage();
                _menuPage.RefreshUserDisplay();
                PageHost.Content = _menuPage;
                break;

            case "uv":
                _uvPage ??= CreateUvPage();
                PageHost.Content = _uvPage;
                _uvPage.OnNavigatedTo(fromPage);
                break;

            case "service":
                _serviceModePage ??= CreateServiceModePage();
                _serviceModePage.RefreshUserDisplay();
                PageHost.Content = _serviceModePage;
                break;

            case "accountMgmt":
                _accountMgmtPage ??= CreateAccountMgmtPage();
                _accountMgmtPage.RefreshUserDisplay();
                PageHost.Content = _accountMgmtPage;
                break;

            case "data":
                _dataListPage ??= CreateDataListPage();
                _dataListPage.RefreshUserDisplay();
                PageHost.Content = _dataListPage;
                break;
        }
    }

    // ═══════ 頁面工廠 ═══════

    private LoginPage CreateLoginPage()
    {
        var vm = new ViewModels.LoginViewModel(_authService, _sessionService, _tokenService, _systemSettings);
        var page = new LoginPage(vm, _systemSettings);
        page.LoginSucceeded += OnLoginSucceeded;
        page.CloseRequested += OnCloseRequested;
        return page;
    }

    private InitPage CreateInitPage()
    {
        var page = new InitPage(_systemSettings);
        page.CountdownCompleted += OnInitCompleted;
        return page;
    }

    private MenuPage CreateMenuPage()
    {
        return new MenuPage(_sessionService, DialogOverlay, LoginOverlayHost,
            _authService, _tokenService, _systemSettings);
    }

    private UvDecontaminationPage CreateUvPage()
    {
        var vm = new ViewModels.UvDecontaminationViewModel(_uvConfigService, _uvHardwareService);
        return new UvDecontaminationPage(vm, _sessionService,
            DialogOverlay, LoginOverlayHost, _authService, _tokenService, _systemSettings);
    }

    private ServiceModePage CreateServiceModePage()
    {
        return new ServiceModePage(_sessionService, DialogOverlay, LoginOverlayHost,
            _authService, _tokenService, _systemSettings);
    }

    private AccountManagementPage CreateAccountMgmtPage()
    {
        var accountService = _serviceProvider.GetRequiredService<AccountManagementService>();
        var policyService = _serviceProvider.GetRequiredService<PasswordPolicyService>();
        return new AccountManagementPage(_sessionService, _authService, _tokenService,
            _systemSettings, accountService, policyService);
    }

    private DataListPage CreateDataListPage()
    {
        return new DataListPage(_sessionService,
            DialogOverlay, LoginOverlayHost, _authService, _tokenService,
            _systemSettings, _serviceProvider);
    }

    // ═══════ 頁面事件處理 ═══════

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        var user = _sessionService.CurrentUser;
        var role = _sessionService.CurrentRole;

        // 記錄最後登入使用者的語系（供 App 重啟時決定登入頁語系）
        if (user != null && !string.IsNullOrEmpty(user.LanguagePreference))
            _systemSettings.LastUserLanguage = user.LanguagePreference;

        // ForcePasswordChange 檢查（Guest 帳號跳過）
        if (user != null && user.ForcePasswordChange == 1 && !_sessionService.IsGuestLogin)
        {
            var loc = LocalizationService.Instance;

            // 強制密碼變更（不可取消）
            var result = await ChangePasswordOverlayHost.ShowAsync(
                user.Id, user.RoleLevel, isForced: true);

            if (result.IsSuccess)
            {
                // 密碼變更成功 → 強制登出重新登入
                await DialogOverlay.ShowAsync(
                    loc["PasswordUI.SuccessTitle"],
                    loc["PasswordUI.ForceSuccessMessage"],
                    loc["Common.OK"],
                    OverlayDialogIcon.Success);

                EventLogService.Instance?.LogAuth("ForcePasswordChange",
                    user.Username, true, "Password changed, forcing re-login");

                await ApplyLoginScreenLanguageAsync();
                _idleTimer.Stop();
                _sessionService.ClearSession();
                _loginPage = null;
                NavigateTo("login");
                return;
            }
            // 如果取消（理論上不應發生，因為 isForced=true 隱藏了取消按鈕）
            // 但安全起見，強制登出
            await ApplyLoginScreenLanguageAsync();
            _idleTimer.Stop();
            _sessionService.ClearSession();
            NavigateTo("login");
            return;
        }

        if (role == RoleLevel.Service)
        {
            // Service 登入 → ServiceModePage
            _serviceModePage = null;
            NavigateTo("service");
        }
        else
        {
            // Operator / Admin 登入 → MenuPage
            _menuPage = null;
            NavigateTo("menu");
        }

        // ── 啟動閒置計時器（Guest 不啟動）──
        StartIdleTimerIfNeeded();
    }

    private void OnInitCompleted(object? sender, EventArgs e)
    {
        // Init 倒數結束 → 從 DB 載入免登入帳號 → 進入選單
        var guestUser = LoadGuestUser();
        _sessionService.SetGuestSession(guestUser, _systemSettings.GuestAccountDisplayName);
        _menuPage = null;
        NavigateTo("menu");
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Application.Current.Shutdown();
    }

    // ═══════ Guest User 載入 ═══════

    /// <summary>從 main.db 載入免登入專用帳號</summary>
    private User LoadGuestUser()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppMainDbContext>();
            var username = _systemSettings.GuestAccountUsername;
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            if (user != null) return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] LoadGuestUser failed: {ex.Message}");
        }

        // Fallback: DB 中無此帳號，建構最小化物件
        return new User
        {
            Id = 0,
            Username = _systemSettings.GuestAccountUsername,
            DisplayName = _systemSettings.GuestAccountDisplayName,
            RoleLevel = (int)RoleLevel.Operator,
            IsActive = 1,
            PasswordHash = "",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            CreatedBy = "SYSTEM"
        };
    }

    /// <summary>恢復免登入模式的 Guest Session（Service Mode 退出時使用）</summary>
    public void RestoreGuestSession()
    {
        var guestUser = LoadGuestUser();
        _sessionService.SetGuestSession(guestUser, _systemSettings.GuestAccountDisplayName);
        _menuPage = null; // 重新建立以刷新使用者資訊
        _dataListPage = null; // 重新建立以反映權限變更
    }

    /// <summary>
    /// 顯示密碼變更 Overlay（由 UserMenuControl 呼叫）
    /// 使用 AppShell 頂層的 ChangePasswordOverlayHost
    /// </summary>
    public async Task<bool> ShowChangePasswordAsync(int userId, int roleLevel, bool isForced = false)
    {
        var result = await ChangePasswordOverlayHost.ShowAsync(userId, roleLevel, isForced);
        return result.IsSuccess;
    }

    /// <summary>
    /// 根據 login_screen_language_mode 設定切換登入頁語系
    /// 
    /// 必須在 ClearSession() 之前呼叫，以取得當前使用者的語系偏好。
    /// - "last_user"：使用當前使用者的 LanguagePreference（找不到則 fallback 到 DefaultLanguage）
    /// - "fixed"：統一使用 DefaultLanguage
    /// </summary>
    public async Task ApplyLoginScreenLanguageAsync()
    {
        var mode = _systemSettings.LoginScreenLanguageMode;
        string targetLang;

        if (mode == "last_user")
        {
            // 取得當前使用者的語系偏好（必須在 ClearSession 之前呼叫）
            var userLang = _sessionService.CurrentUser?.LanguagePreference;
            targetLang = !string.IsNullOrEmpty(userLang) ? userLang : _systemSettings.DefaultLanguage;
        }
        else
        {
            // fixed 模式 → 統一使用系統預設語系
            targetLang = _systemSettings.DefaultLanguage;
        }

        await LocalizationService.Instance.SwitchLanguageAsync(targetLang);
    }

    // ═══════ Session Lock / Idle Timer ═══════

    /// <summary>根據設定啟動閒置計時器（Guest / GuestMode 不啟動）</summary>
    private void StartIdleTimerIfNeeded()
    {
        // Guest 不需要 timeout
        if (_sessionService.IsGuestLogin || _sessionService.IsGuestMode) return;

        var timeoutMin = _systemSettings.SessionTimeoutMinutes;
        if (timeoutMin <= 0) return;

        // 掛載事件
        _idleTimer.WarningTriggered -= OnIdleWarning;
        _idleTimer.TimeoutTriggered -= OnIdleTimeout;
        _idleTimer.CountdownTick -= OnCountdownTick;
        _idleTimer.TimerReset -= OnTimerReset;
        _idleTimer.WarningTriggered += OnIdleWarning;
        _idleTimer.TimeoutTriggered += OnIdleTimeout;
        _idleTimer.CountdownTick += OnCountdownTick;
        _idleTimer.TimerReset += OnTimerReset;

        _idleTimer.Start(timeoutMin, _systemSettings.SessionTimeoutWarningSeconds);

        // 初始顯示倒數
        UpdateCountdownDisplay(timeoutMin * 60);
    }

    private void OnIdleWarning(object? sender, int remainingSeconds)
    {
        // 倒數警告（使用非阻塞 toast — 暫以 Title 顯示）
        var msg = string.Format(
            LocalizationService.Instance["Lock.TimeoutWarning"],
            remainingSeconds);
        Dispatcher.Invoke(() => Title = $"TRIO2026 — {msg}");
    }

    private async void OnIdleTimeout(object? sender, EventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            var action = _systemSettings.SessionTimeoutAction;
            if (action == "logout")
            {
                // 完整登出
                EventLogService.Instance?.LogInfo("Auth", "AppShell",
                    ErrorCodes.SessionLocked, "Session Timeout - Logout",
                    $"User={_sessionService.CurrentUser?.Username}");
                await ApplyLoginScreenLanguageAsync();
                _idleTimer.Stop();
                _sessionService.ClearSession();
                _loginPage = null;
                NavigateTo("login");
            }
            else
            {
                // 鎖定畫面
                await HandleLockScreenAsync();
            }
        });
    }

    private async Task HandleLockScreenAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user == null) return;

        _sessionService.LockSession();
        Title = "TRIO2026";

        EventLogService.Instance?.LogInfo("Auth", "AppShell",
            ErrorCodes.SessionLocked, "Session Locked",
            $"User={user.Username}, TimeoutMin={_systemSettings.SessionTimeoutMinutes}");

        var result = await LockScreenHost.ShowAsync(
            user, _sessionService.LockedAt ?? DateTime.Now,
            _authService, _systemSettings);

        if (result == Controls.LockScreenResult.SwitchUser)
        {
            // Admin 強制登出 → 完整登出
            _sessionService.UnlockSession();
            await ApplyLoginScreenLanguageAsync();
            _idleTimer.Stop();
            _sessionService.ClearSession();
            _loginPage = null;
            NavigateTo("login");
        }
        else
        {
            // 解鎖成功（原使用者密碼解鎖 或 Admin 代理解鎖）
            _sessionService.UnlockSession();
            StartIdleTimerIfNeeded(); // 重新啟動 timer

            // Data 頁面後續：關閉中途對話框、強制關閉 USB 選擇器
            UsbDriveSelectorHost.ForceClose();

            // Session Lock 期間如有格式化確認視窗仍在等待，強制取消（標記來源為 SessionLock）
            UsbFormatConfirmHost.ForceCancel("SessionLock");

            // 若解鎖後當前頁面是 DataDetailPage，回到清單頁並重整資料
            if (PageHost.Content is Pages.DataDetailPage)
            {
                if (_dataListPage != null) PageHost.Content = _dataListPage;
                else NavigateTo("data");
            }

            // 處理鎖定期間累積的訊息
            await ProcessPendingMessagesAsync();
        }
    }

    /// <summary>處理鎖定期間累積的待處理訊息</summary>
    private async Task ProcessPendingMessagesAsync()
    {
        while (_sessionService.HasPendingMessages)
        {
            var msg = _sessionService.DequeueMessage();
            if (msg == null) break;

            await DialogOverlay.ShowAsync(
                msg.Title, msg.Message,
                LocalizationService.Instance["Common.OK"],
                OverlayDialogIcon.Info);
        }
    }

    /// <summary>暫停閒置計時器（UV 等長時間流程呼叫）</summary>
    public void PauseIdleTimer() => _idleTimer.Pause();

    /// <summary>恢復閒置計時器</summary>
    public void ResumeIdleTimer() => _idleTimer.Resume();

    /// <summary>取得 LockScreen Overlay 參考（供 UV 頁面穿透訊息使用）</summary>
    public Controls.LockScreenOverlay LockScreen => LockScreenHost;

    /// <summary>取得 USB 碟選擇器參考（供 Data 頁面下載流程使用）</summary>
    public Controls.UsbDriveSelector UsbDriveSelector => UsbDriveSelectorHost;

    // ═══════ Session Countdown Display ═══════

    private void OnCountdownTick(object? sender, int remainingSeconds)
    {
        Dispatcher.Invoke(() => UpdateCountdownDisplay(remainingSeconds));
    }

    private void OnTimerReset(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => UpdateCountdownDisplay(_idleTimer.RemainingSeconds));
    }

    /// <summary>更新當前頁面的底部列倒數顯示</summary>
    private void UpdateCountdownDisplay(int remainingSeconds)
    {
        if (!_systemSettings.SessionTimeoutCountdownVisible)
        {
            SetPageCountdownVisibility(false, "");
            return;
        }

        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        var timeStr = $"{minutes:D2}:{seconds:D2}";
        var loc = LocalizationService.Instance;
        var text = string.Format(loc["Lock.CountdownLabel"], timeStr);

        SetPageCountdownVisibility(true, text);
    }

    /// <summary>尋找當前頁面的 CountdownText 並更新</summary>
    private void SetPageCountdownVisibility(bool visible, string text)
    {
        // MenuPage
        if (PageHost.Content is Pages.MenuPage menuPage)
        {
            var tb = menuPage.FindName("CountdownText") as System.Windows.Controls.TextBlock;
            if (tb != null)
            {
                tb.Text = text;
                tb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // UvDecontaminationPage
        if (PageHost.Content is Pages.UvDecontaminationPage uvPage)
        {
            var tb = uvPage.FindName("CountdownText") as System.Windows.Controls.TextBlock;
            if (tb != null)
            {
                tb.Text = text;
                tb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // DataListPage
        if (PageHost.Content is Pages.DataListPage dataPage)
        {
            var tb = dataPage.FindName("CountdownText") as System.Windows.Controls.TextBlock;
            if (tb != null)
            {
                tb.Text = text;
                tb.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

