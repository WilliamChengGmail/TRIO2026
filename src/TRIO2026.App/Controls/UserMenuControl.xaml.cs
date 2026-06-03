using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TRIO2026.App.Services;
using TRIO2026.App.Views;
using TRIO2026.Core.Enums;

namespace TRIO2026.App.Controls;

/// <summary>
/// 共用使用者選單控制元件
/// 
/// 用法：在頁面根 Grid 末尾加入，設定 Grid.RowSpan 覆蓋全頁。
///   <controls:UserMenuControl x:Name="UserMenu" Grid.RowSpan="3"/>
/// 
/// 在頁面 code-behind 呼叫 Initialize() 傳入所需服務。
/// 
/// 功能：
///   - 右上角使用者圖示（始終可見）
///   - 點擊後顯示選單 Overlay（HOME / 語系 / 登出）
///   - 10 秒無操作自動關閉
///   - Overlay 阻擋背景互動
///   - 點擊外部關閉
/// 
/// 適用於所有非 MenuPage 的頁面。
/// </summary>
public partial class UserMenuControl : UserControl
{
    private SessionService? _sessionService;

    private SystemSettingService? _systemSettings;
    private OverlayDialog? _dialogOverlay;
    private LoginOverlay? _loginOverlay;
    private AuthService? _authService;
    private TokenService? _tokenService;
    private ChangePasswordOverlay? _changePasswordOverlay;
    private PasswordPolicyService? _policyService;

    /// <summary>自動關閉計時器</summary>
    private readonly DispatcherTimer _autoCloseTimer;

    /// <summary>
    /// 使用者圖示是否可點擊（UV 運行時設為 false）
    /// </summary>
    public bool IsUserIconEnabled
    {
        get => UserIconPanel.IsEnabled;
        set
        {
            UserIconPanel.IsEnabled = value;
            UserIconPanel.Opacity = value ? 1.0 : 0.4;
        }
    }

    /// <summary>
    /// HOME 按鈕是否啟用（倒數期間可由外部設為 false）
    /// </summary>
    public bool IsHomeEnabled
    {
        get => BtnHome.IsEnabled;
        set => BtnHome.IsEnabled = value;
    }

    /// <summary>
    /// 是否顯示 HOME 按鈕（MenuPage 設為 false）
    /// </summary>
    public static readonly DependencyProperty ShowHomeButtonProperty =
        DependencyProperty.Register(nameof(ShowHomeButton), typeof(bool),
            typeof(UserMenuControl), new PropertyMetadata(true, OnShowHomeButtonChanged));

    public bool ShowHomeButton
    {
        get => (bool)GetValue(ShowHomeButtonProperty);
        set => SetValue(ShowHomeButtonProperty, value);
    }

    private static void OnShowHomeButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UserMenuControl ctrl)
        {
            ctrl.BtnHome.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            ctrl.HomeSeparator.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public UserMenuControl()
    {
        InitializeComponent();

        // 預設 10 秒，Initialize() 時會從 DB 覆寫
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoCloseTimer.Tick += (_, _) => CloseAllOverlays();
    }

    /// <summary>
    /// 初始化 — 傳入所需服務（由父頁面 code-behind 呼叫）
    /// </summary>
    public void Initialize(
        SessionService sessionService,
        OverlayDialog dialogOverlay,
        LoginOverlay loginOverlay,
        AuthService authService,
        TokenService tokenService,
        SystemSettingService? systemSettings = null,
        ChangePasswordOverlay? changePasswordOverlay = null,
        PasswordPolicyService? policyService = null)
    {
        _sessionService = sessionService;
        _dialogOverlay = dialogOverlay;
        _loginOverlay = loginOverlay;
        _authService = authService;
        _tokenService = tokenService;
        _systemSettings = systemSettings;
        _changePasswordOverlay = changePasswordOverlay;
        _policyService = policyService;

        // 初始化 ChangePasswordOverlay 服務
        if (_changePasswordOverlay != null && _authService != null && _policyService != null)
            _changePasswordOverlay.Initialize(_authService, _policyService);

        // 從 DB 讀取自動關閉秒數
        if (_systemSettings != null)
        {
            var seconds = _systemSettings.UserMenuAutoCloseSeconds;
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(seconds);
        }

        // 從 DB 讀取多語系開關
        var multiLangEnabled = _systemSettings?.MultiLanguageEnabled ?? true;
        BtnSwitchLanguage.Visibility = multiLangEnabled ? Visibility.Visible : Visibility.Collapsed;
        LangSeparator.Visibility = multiLangEnabled ? Visibility.Visible : Visibility.Collapsed;

        RefreshUserDisplay();
    }

    /// <summary>更新使用者顯示名稱與角色</summary>
    public void RefreshUserDisplay()
    {
        if (_sessionService?.CurrentUser != null)
        {
            var user = _sessionService.CurrentUser;
            var loc = LocalizationService.Instance;
            UserNameText.Text = user.DisplayName ?? user.Username;

            if (_sessionService.IsGuestLogin)
            {
                // ── Guest 免密碼登入模式：精簡選單 ──
                UserRoleText.Visibility = Visibility.Collapsed;
                BtnLogout.Tag = loc["UserMenu.Logout"];

                // 隱藏所有管理功能按鈕
                BtnServiceMode.Visibility = Visibility.Collapsed;
                ServiceModeSeparator.Visibility = Visibility.Collapsed;
                BtnChangePassword.Visibility = Visibility.Collapsed;
                ChangePasswordSeparator.Visibility = Visibility.Collapsed;
                BtnAccountMgmt.Visibility = Visibility.Collapsed;
                AccountMgmtSeparator.Visibility = Visibility.Collapsed;

                // Home 按鈕：依頁面位置決定
                BtnHome.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
                HomeSeparator.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;

                // 語系切換：由 guest_multilanguage_enabled 控制
                var guestLangEnabled = _systemSettings?.GuestMultiLanguageEnabled ?? false;
                BtnSwitchLanguage.Visibility = guestLangEnabled ? Visibility.Visible : Visibility.Collapsed;
                LangSeparator.Visibility = guestLangEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_sessionService.IsGuestMode)
            {
                // 免登入模式：不顯示角色資訊
                UserRoleText.Visibility = Visibility.Collapsed;

                // 登出按鈕文字改為「關閉」
                BtnLogout.Tag = loc["UserMenu.CloseApp"];

                // 顯示 Service Mode 切換按鈕
                BtnServiceMode.Visibility = Visibility.Visible;
                ServiceModeSeparator.Visibility = Visibility.Visible;

                // Home 按鈕：尊重頁面的 ShowHomeButton 設定
                BtnHome.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
                HomeSeparator.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // 登入模式：顯示角色等級
                UserRoleText.Visibility = Visibility.Visible;
                var roleName = user.RoleLevel switch { 1 => "Operator", 2 => "Service", 3 => "Admin", _ => $"Level {user.RoleLevel}" };
                UserRoleText.Text = $"{roleName} (Level {user.RoleLevel})";

                // 登出按鈕文字恢復為「登出」
                BtnLogout.Tag = loc["UserMenu.Logout"];

                // 隱藏 Service Mode 按鈕（登入模式不需要）
                BtnServiceMode.Visibility = Visibility.Collapsed;
                ServiceModeSeparator.Visibility = Visibility.Collapsed;

                // 變更密碼：登入模式一律顯示（所有登入角色都需要能改密碼）
                BtnChangePassword.Visibility = Visibility.Visible;
                ChangePasswordSeparator.Visibility = Visibility.Visible;

                // 帳號管理：僅 Admin 可見
                var isAdmin = _sessionService.CurrentRole == RoleLevel.Admin;
                BtnAccountMgmt.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
                AccountMgmtSeparator.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

                // Home 按鈕：Service 角色或 ShowHomeButton=False 時隱藏
                if (_sessionService.CurrentRole == RoleLevel.Service || !ShowHomeButton)
                {
                    BtnHome.Visibility = Visibility.Collapsed;
                    HomeSeparator.Visibility = Visibility.Collapsed;
                }
                else
                {
                    BtnHome.Visibility = Visibility.Visible;
                    HomeSeparator.Visibility = Visibility.Visible;
                }
            }
        }
    }

    // ═══════════════════════════════════════
    // Overlay 控制
    // ═══════════════════════════════════════

    private void ShowOverlay(Grid overlay)
    {
        overlay.Visibility = Visibility.Visible;

        // 即時從 DB 讀取秒數；0 = 不自動關閉
        var seconds = _systemSettings?.UserMenuAutoCloseSeconds ?? 10;
        if (seconds > 0)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(seconds);
            ResetAutoCloseTimer();
        }
        else
        {
            _autoCloseTimer.Stop();
        }
    }

    private void CloseAllOverlays()
    {
        UserMenuOverlay.Visibility = Visibility.Collapsed;
        LanguageOverlay.Visibility = Visibility.Collapsed;
        _autoCloseTimer.Stop();
    }

    private void ResetAutoCloseTimer()
    {
        // 0 = 不自動關閉，不啟動 timer
        var seconds = _systemSettings?.UserMenuAutoCloseSeconds ?? 10;
        if (seconds <= 0) return;

        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }

    // ═══════════════════════════════════════
    // XAML 事件處理
    // ═══════════════════════════════════════

    private void OnUserIconClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        // 即時從 DB 讀取多語系開關，控制語系按鈕可見性
        var multiLangEnabled = _systemSettings?.MultiLanguageEnabled ?? true;
        BtnSwitchLanguage.Visibility = multiLangEnabled ? Visibility.Visible : Visibility.Collapsed;
        LangSeparator.Visibility = multiLangEnabled ? Visibility.Visible : Visibility.Collapsed;

        // 免登入模式才顯示 Service Mode 切換
        var isGuest = _sessionService?.IsGuestMode ?? false;
        BtnServiceMode.Visibility = isGuest ? Visibility.Visible : Visibility.Collapsed;
        ServiceModeSeparator.Visibility = isGuest ? Visibility.Visible : Visibility.Collapsed;

        ShowOverlay(UserMenuOverlay);
        EventLogService.Instance.LogMenuAction("Open");
    }

    private void OnOverlayClick(object sender, MouseButtonEventArgs e)
    {
        CloseAllOverlays();
        e.Handled = true;
    }

    private void OnMenuPanelClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ResetAutoCloseTimer();
    }

    /// <summary>Overlay 範圍內偵測到滑鼠移動 → 重置自動關閉計時器</summary>
    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        ResetAutoCloseTimer();
    }

    /// <summary>HOME — 返回主畫面（根據角色導向不同頁面）</summary>
    private void OnHomeClick(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        EventLogService.Instance.LogButtonClick("UserMenu", "Home");
        var shell = Window.GetWindow(this) as AppShell;
        if (shell == null) return;

        if (_sessionService != null && !_sessionService.IsGuestMode
            && _sessionService.CurrentRole == RoleLevel.Service)
        {
            // 登入模式的 Service → ServiceModePage 是主畫面
            shell.NavigateTo("service");
        }
        else
        {
            // Operator 或免登入模式 → MenuPage
            shell.NavigateTo("menu");
        }
    }

    /// <summary>Service Mode — 免登入模式下切換到 Service 角色</summary>
    private async void OnServiceModeClick(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        if (_loginOverlay == null || _authService == null || _sessionService == null) return;

        var loc = LocalizationService.Instance;

        while (true)
        {
            var loginResult = await _loginOverlay.ShowAsync(
                loc["UserMenu.ServiceModeTitle"],
                loc["UserMenu.ServiceModeMessage"]);

            if (loginResult.IsCancelled) return;

            var (authResult, user, _) = await _authService.LoginAsync(
                loginResult.Username, loginResult.Password);

            if (authResult != Core.Enums.AuthResult.Success)
            {
                var errorMsg = authResult switch
                {
                    Core.Enums.AuthResult.UserNotFound => loc["Login.UserNotFound"],
                    Core.Enums.AuthResult.WrongPassword => loc["Login.WrongPassword"],
                    Core.Enums.AuthResult.AccountDisabled => loc["Login.AccountDisabled"],
                    Core.Enums.AuthResult.AccountLocked => loc["Login.AccountLocked"],
                    _ => loc["Login.AuthFailed"]
                };
                _loginOverlay.ShowError(errorMsg);
                continue;
            }

            // 驗證角色：僅 Service level 可進入
            if (user!.RoleLevel != (int)RoleLevel.Service)
            {
                _loginOverlay.ShowError(loc["UserMenu.ServiceModeInsufficientRole"]);
                continue;
            }

            // 登入成功 → 設定會話 → 導航至 ServiceModePage
            _sessionService.SetCurrentUser(user);
            EventLogService.Instance.LogAuth("ServiceModeLogin",
                user.Username, true, $"RoleLevel={user.RoleLevel}");

            var shell = Window.GetWindow(this) as AppShell;
            shell?.NavigateTo("service");
            return;
        }
    }

    /// <summary>變更密碼</summary>
    private async void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        EventLogService.Instance.LogButtonClick("UserMenu", "ChangePassword");

        if (_sessionService?.CurrentUser == null) return;

        var shell = Window.GetWindow(this) as AppShell;
        if (shell == null) return;

        var user = _sessionService.CurrentUser;
        var success = await shell.ShowChangePasswordAsync(user.Id, user.RoleLevel);

        if (success && _dialogOverlay != null)
        {
            var loc = LocalizationService.Instance;
            await _dialogOverlay.ShowAsync(
                loc["PasswordUI.SuccessTitle"],
                loc["PasswordUI.SuccessMessage"],
                loc["Common.OK"],
                OverlayDialogIcon.Success);
        }
    }

    /// <summary>帳號管理 — 導向 AccountManagementPage</summary>
    private void OnAccountMgmtClick(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        EventLogService.Instance.LogButtonClick("UserMenu", "AccountMgmt");

        var shell = Window.GetWindow(this) as AppShell;
        shell?.NavigateTo("accountMgmt");
    }

    /// <summary>切換語系按鈕</summary>
    private void OnSwitchLanguageClick(object sender, RoutedEventArgs e)
    {
        UserMenuOverlay.Visibility = Visibility.Collapsed;
        ShowOverlay(LanguageOverlay);
    }

    /// <summary>語系選擇</summary>
    private async void OnLangSelected(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();
        if (sender is Button btn && btn.Tag is string langCode)
        {
            EventLogService.Instance.LogButtonClick("UserMenu", "Language", $"LangCode={langCode}");
            
            if (_sessionService != null && !_sessionService.IsGuestMode
                && _sessionService.CurrentUser != null && _authService != null)
            {
                // 登入模式：將語系偏好寫入帳號設定
                await _authService.UpdateLanguagePreferenceAsync(_sessionService.CurrentUser.Id, langCode);
                _sessionService.CurrentUser.LanguagePreference = langCode; // 同步記憶體

                // 同步更新 LastUserLanguage（供 App 重啟時使用）
                if (_systemSettings != null)
                    _systemSettings.LastUserLanguage = langCode;
            }
            else if (_systemSettings != null)
            {
                // 免登入模式：寫入系統預設語系
                _systemSettings.DefaultLanguage = langCode;
            }

            await LocalizationService.Instance.SwitchLanguageAsync(langCode);

            // 語系切換後更新手動設定的 UI 文字（如 BtnLogout.Tag）
            RefreshUserDisplay();
        }
    }

    /// <summary>登出/關閉</summary>
    private async void OnLogoutOrCloseClick(object sender, RoutedEventArgs e)
    {
        CloseAllOverlays();

        if (_dialogOverlay == null) return;

        var shell = Window.GetWindow(this) as AppShell;
        if (shell == null) return;

        var loc = LocalizationService.Instance;

        if (_systemSettings != null && _systemSettings.LoginRequired)
        {
            var currentUser = _sessionService?.CurrentUser;
            var roleLevel = currentUser?.RoleLevel ?? 0;

            if (roleLevel > 1)
            {
                var choice = await _dialogOverlay.ShowTripleAsync(
                    loc["UserMenu.LogoutConfirmTitle"],
                    loc["UserMenu.LogoutConfirmMessage"],
                    loc["UserMenu.Logout"],
                    loc["UserMenu.LogoutAndClose"],
                    loc["Common.Cancel"],
                    OverlayDialogIcon.Warning);

                switch (choice)
                {
                    case 0:
                        EventLogService.Instance.LogAuth("Logout",
                            _sessionService?.CurrentUser?.Username, true);
                        await shell.ApplyLoginScreenLanguageAsync();
                        _sessionService?.ClearSession();
                        shell.NavigateTo("login");
                        break;
                    case 1:
                        EventLogService.Instance.LogAuth("Logout",
                            _sessionService?.CurrentUser?.Username, true, "WithAppClose");
                        _sessionService?.ClearSession();
                        Application.Current.Shutdown();
                        break;
                }
            }
            else
            {
                var confirmed = await _dialogOverlay.ShowConfirmAsync(
                    loc["UserMenu.LogoutConfirmTitle"],
                    loc["UserMenu.LogoutConfirmMessage"],
                    loc["UserMenu.Logout"],
                    loc["Common.Cancel"],
                    OverlayDialogIcon.Warning);
                if (confirmed)
                {
                    EventLogService.Instance.LogAuth("Logout",
                        _sessionService?.CurrentUser?.Username, true);
                    await shell.ApplyLoginScreenLanguageAsync();
                    _sessionService?.ClearSession();
                    shell.NavigateTo("login");
                }
            }
        }
        else
        {
            // 免登入模式
            if (_sessionService != null && !_sessionService.IsGuestMode)
            {
                // 當前是 Service Mode（已提權登入）→ 退出回到 Guest Session
                var confirmed = await _dialogOverlay.ShowConfirmAsync(
                    loc["UserMenu.ExitServiceModeTitle"],
                    loc["UserMenu.ExitServiceModeMessage"],
                    loc["Common.Confirm"],
                    loc["Common.Cancel"],
                    OverlayDialogIcon.Warning);
                if (confirmed)
                {
                    EventLogService.Instance.LogAuth("ExitServiceMode",
                        _sessionService.CurrentUser?.Username, true);
                    // 恢復 Guest Session
                    shell.RestoreGuestSession();
                    shell.NavigateTo("menu");
                }
            }
            else
            {
                // 一般免登入模式 → 關閉應用
                var confirmed = await _dialogOverlay.ShowConfirmAsync(
                    loc["UserMenu.CloseConfirmTitle"],
                    loc["UserMenu.CloseConfirmMessage"],
                    loc["Common.Confirm"],
                    loc["Common.Cancel"],
                    OverlayDialogIcon.Warning);
                if (confirmed)
                {
                    await VerifyAndClose();
                }
            }
        }
    }

    private async Task VerifyAndClose()
    {
        if (_loginOverlay == null || _authService == null || _dialogOverlay == null) return;

        var loc = LocalizationService.Instance;

        while (true)
        {
            var loginResult = await _loginOverlay.ShowAsync(
                loc["UserMenu.AuthTitle"],
                loc["UserMenu.AuthMessage"]);

            if (loginResult.IsCancelled) return;

            var (authResult, user, _) = await _authService.LoginAsync(
                loginResult.Username, loginResult.Password);

            if (authResult != Core.Enums.AuthResult.Success)
            {
                var errorMsg = authResult switch
                {
                    Core.Enums.AuthResult.UserNotFound => loc["Login.UserNotFound"],
                    Core.Enums.AuthResult.WrongPassword => loc["Login.WrongPassword"],
                    Core.Enums.AuthResult.AccountDisabled => loc["Login.AccountDisabled"],
                    Core.Enums.AuthResult.AccountLocked => loc["Login.AccountLocked"],
                    _ => loc["Login.AuthFailed"]
                };
                _loginOverlay.ShowError(errorMsg);
                continue;
            }

            if (user!.RoleLevel >= 2)
            {
                Application.Current.Shutdown();
            }
            else
            {
                await _dialogOverlay.ShowAsync(
                    loc["UserMenu.InsufficientPermission"],
                    loc["UserMenu.InsufficientPermissionMessage"],
                    loc["Common.OK"],
                    OverlayDialogIcon.Error);
            }
            return;
        }
    }
}
