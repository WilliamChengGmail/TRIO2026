using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using TRIO2026.App.Helpers;
using TRIO2026.App.Services;
using TRIO2026.Core.Entities;
using TRIO2026.Core.Enums;

namespace TRIO2026.App.ViewModels;

/// <summary>
/// 登入頁面 ViewModel — MVVM 模式
/// </summary>
public class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private readonly SessionService _sessionService;
    private readonly TokenService _tokenService;
    private readonly SystemSettingService _systemSettings;

    public LoginViewModel(AuthService authService, SessionService sessionService,
        TokenService tokenService, SystemSettingService systemSettings)
    {
        _authService = authService;
        _sessionService = sessionService;
        _tokenService = tokenService;
        _systemSettings = systemSettings;

        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, _ => CanLogin);

        // 螢幕偵測
        IsTouchScreen = ScreenDetector.IsTouchSupported;
        ScreenInfo = $"{ScreenDetector.ScreenWidth}×{ScreenDetector.ScreenHeight}" +
                     (IsTouchScreen ? " (觸控)" : " (非觸控)");

        // 嘗試載入記住的密碼（由 LoginPage 設定 RememberPasswordEnabled 後再決定是否生效）
        LoadRememberedCredentials();
    }

    // ===== 系統設定 =====

    /// <summary>記住密碼功能是否啟用（由 LoginPage 從 SystemSettingService 注入）</summary>
    public bool RememberPasswordEnabled { get; set; }

    // ===== 繫結屬性 =====

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); OnPropertyChanged(nameof(CanLogin)); OnPropertyChanged(nameof(IsGuestUser)); }
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set { SetProperty(ref _password, value); OnPropertyChanged(nameof(CanLogin)); }
    }

    private bool _rememberMe;
    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set { SetProperty(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(CanLogin)); }
    }

    private bool _showShakeAnimation;
    public bool ShowShakeAnimation
    {
        get => _showShakeAnimation;
        set => SetProperty(ref _showShakeAnimation, value);
    }

    private ImageSource? _avatarImage;
    public ImageSource? AvatarImage
    {
        get => _avatarImage;
        set => SetProperty(ref _avatarImage, value);
    }

    public bool IsTouchScreen { get; }
    public string ScreenInfo { get; }

    // ===== 使用者下拉清單 =====

    private bool _showUserDropdown;
    /// <summary>是否顯示使用者下拉選單（由 DB 設定控制）</summary>
    public bool ShowUserDropdown
    {
        get => _showUserDropdown;
        set => SetProperty(ref _showUserDropdown, value);
    }

    /// <summary>使用者清單（下拉選單資料來源）</summary>
    public ObservableCollection<User> UserList { get; } = new();

    private User? _selectedUser;
    /// <summary>下拉選單選中的使用者</summary>
    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value != null)
            {
                Username = value.Username;
            }
        }
    }

    /// <summary>從 DB 載入使用者清單（供下拉選單使用）</summary>
    public async Task LoadUsersAsync()
    {
        try
        {
            // Guest 啟用時，下拉選單包含 guest 帳號
            var users = _systemSettings.GuestLoginEnabled
                ? await _authService.GetAllUsersWithGuestAsync()
                : await _authService.GetAllUsersAsync();
            UserList.Clear();
            foreach (var u in users)
                UserList.Add(u);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoginVM] LoadUsersAsync failed: {ex.Message}");
        }
    }

    /// <summary>當前輸入的帳號是否為已啟用的 Guest 帳號</summary>
    public bool IsGuestUser => _systemSettings.GuestLoginEnabled
        && Username?.Trim().Equals("guest", StringComparison.OrdinalIgnoreCase) == true;

    public bool CanLogin => !string.IsNullOrWhiteSpace(Username) &&
                            (IsGuestUser || !string.IsNullOrWhiteSpace(Password)) &&
                            !IsLoading;

    // ===== Commands =====

    public ICommand LoginCommand { get; }

    /// <summary>登入成功事件（由 Window 訂閱以關閉或導航）</summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>取得當前使用者顯示資訊（名稱 + 角色）</summary>
    public (string name, string role) CurrentUserDisplay
    {
        get
        {
            var user = _sessionService.CurrentUser;
            if (user == null) return ("", "");
            var displayName = user.DisplayName ?? user.Username;
            var roleName = user.RoleLevel switch { 1 => "Operator", 2 => "Service", 3 => "Admin", _ => $"Level {user.RoleLevel}" };
            return (displayName, $"{roleName} (Level {user.RoleLevel})");
        }
    }

    // ===== 邏輯 =====

    private async Task ExecuteLoginAsync(object? parameter)
    {
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            // Guest 免密碼登入
            if (IsGuestUser)
            {
                // 資安檢查：必須 GuestLoginEnabled 才允許免密碼登入
                if (!_systemSettings.GuestLoginEnabled)
                {
                    ErrorMessage = LocalizationService.Instance["Login.InvalidCredentials"];
                    EventLogService.Instance?.LogAuth("GuestLogin", "guest", false,
                        "Reason=GuestLoginDisabled (SystemSetting)");
                    TriggerShake();
                    return;
                }

                var guestUser = await _authService.GetUserByUsernameAsync("guest");
                if (guestUser != null && guestUser.IsActive == 1)
                {
                    _sessionService.SetCurrentUser(guestUser);
                    EventLogService.Instance?.LogAuth("GuestLogin", "guest", true, "PasswordBypass");

                    // Guest 不記憶密碼
                    _tokenService.ClearRememberedCredentials();

                    LoginSucceeded?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorMessage = LocalizationService.Instance["Login.AccountDisabled"];
                    EventLogService.Instance?.LogAuth("GuestLogin", "guest", false, "Reason=GuestAccountDisabledOrNotFound");
                    TriggerShake();
                }
                return;
            }

            var (result, user, detail) = await _authService.LoginAsync(Username, Password);

            switch (result)
            {
                case AuthResult.Success:
                    _sessionService.SetCurrentUser(user!);

                    if (RememberPasswordEnabled && RememberMe)
                        _tokenService.SaveRememberedCredentials(Username, Password);
                    else
                        _tokenService.ClearRememberedCredentials();

                    // 操作追蹤：登入成功
                    EventLogService.Instance.LogAuth("Login", Username, true,
                        $"RoleLevel={user!.RoleLevel}");

                    // 切換至使用者的偏好語系
                    if (!string.IsNullOrEmpty(user.LanguagePreference))
                    {
                        await LocalizationService.Instance.SwitchLanguageAsync(user.LanguagePreference);
                    }

                    LoginSucceeded?.Invoke(this, EventArgs.Empty);
                    break;

                case AuthResult.UserNotFound:
                case AuthResult.WrongPassword:
                    ErrorMessage = LocalizationService.Instance["Login.InvalidCredentials"];
                    EventLogService.Instance.LogAuth("Login", Username, false,
                        $"Reason={result}, {detail}");
                    TriggerShake();
                    break;

                case AuthResult.AccountDisabled:
                    ErrorMessage = LocalizationService.Instance["Login.AccountDisabled"];
                    EventLogService.Instance.LogAuth("Login", Username, false,
                        $"Reason=AccountDisabled, {detail}");
                    TriggerShake();
                    break;

                case AuthResult.AccountLocked:
                    ErrorMessage = LocalizationService.Instance["Login.AccountLocked"];
                    EventLogService.Instance.LogAuth("Login", Username, false,
                        $"Reason=AccountLocked, {detail}");
                    TriggerShake();
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{LocalizationService.Instance["Login.SystemError"]}: {ex.Message}";
            EventLogService.Instance?.LogException("Auth", "LoginViewModel", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadRememberedCredentials()
    {
        if (!RememberPasswordEnabled)
        {
            _tokenService.ClearRememberedCredentials();
            return;
        }

        var saved = _tokenService.LoadRememberedCredentials();
        if (saved.HasValue)
        {
            Username = saved.Value.Username;
            Password = saved.Value.Password;
            RememberMe = true;
        }
    }

    private async void TriggerShake()
    {
        ShowShakeAnimation = true;
        await Task.Delay(500);
        ShowShakeAnimation = false;
    }
}
