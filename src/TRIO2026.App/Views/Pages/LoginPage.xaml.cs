using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TRIO2026.App.Services;
using TRIO2026.App.ViewModels;
using TRIO2026.Core;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// LoginPage — 登入頁面（UserControl）
/// 由 AppShell 託管，登入成功後透過事件通知 Shell 切換頁面
/// </summary>
public partial class LoginPage : UserControl
{
    private readonly LoginViewModel _viewModel;
    private readonly SystemSettingService _settings;
    private bool _suppressKeypadOnce = true; // 跳過初始化的自動聚焦

    /// <summary>登入成功事件 — Shell 收到後切換到 MenuPage</summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>關閉請求事件 — Shell 收到後關閉應用程式</summary>
    public event EventHandler? CloseRequested;

    public LoginPage(LoginViewModel viewModel, SystemSettingService settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = _viewModel;

        // 訂閱登入成功事件
        _viewModel.LoginSucceeded += OnLoginSucceeded;

        // 訂閱抖動動畫
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.ShowShakeAnimation) && _viewModel.ShowShakeAnimation)
            {
                var storyboard = (Storyboard)FindResource("ShakeAnimation");
                storyboard.Begin(LoginCard);
            }
            // Guest 帳號狀態變更時更新密碼框
            if (e.PropertyName == nameof(LoginViewModel.IsGuestUser))
            {
                UpdateGuestPasswordState();
            }
        };

        // 關閉按鈕（受 DB 控制 — 讀取 system_config.db）
        CloseButton.Visibility = _settings.CloseButtonEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        // 使用者下拉選單（受 DB 控制 — show_user_dropdown）
        _viewModel.ShowUserDropdown = _settings.ShowUserDropdown;

        // 記住密碼開關（受 DB 控制 — remember_password_enabled）
        ChkRemember.Visibility = _settings.RememberPasswordEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        _viewModel.RememberPasswordEnabled = _settings.RememberPasswordEnabled;

        // 多語系
        ApplyLocalization();

        // 載入記住的密碼 + 使用者清單
        Loaded += async (s, e) =>
        {
            if (!string.IsNullOrEmpty(_viewModel.Password))
            {
                PasswordBox.Password = _viewModel.Password;
            }

            if (_viewModel.ShowUserDropdown)
            {
                await _viewModel.LoadUsersAsync();
                // 如果有記住的帳號，自動選中對應的使用者
                if (!string.IsNullOrEmpty(_viewModel.Username))
                {
                    var match = _viewModel.UserList
                        .FirstOrDefault(u => u.Username == _viewModel.Username);
                    if (match != null)
                        _viewModel.SelectedUser = match;
                }

                // 聚焦帳號下拉選單，讓使用者先確認帳號
                UserDropdown.Focus();
            }
            else
            {
                // 聚焦到登入卡片本身（不聚焦帳號框以避免觸控鍵盤自動彈出）
                LoginCard.Focus();
            }

            // 密碼框阻擋實體鍵盤（強制透過觸控鍵盤/數字鍵盤輸入）
            PasswordBox.PreviewKeyDown += InputBox_PreviewKeyDown_Block;
            PasswordBox.PreviewTextInput += InputBox_PreviewTextInput_Block;

            // 初始化完成，等 UI 事件處理完後解除壓制旗標
            Dispatcher.BeginInvoke(() => _suppressKeypadOnce = false,
                System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    /// <summary>刷新多語系與 UI 顯示（登出後或語系切換時呼叫）</summary>
    public void RefreshDisplay()
    {
        ApplyLocalization();

        // 重新壓制鍵盤彈出（避免聚焦變更觸發數字鍵盤）
        _suppressKeypadOnce = true;

        // 清空密碼欄位
        PasswordBox.Password = "";
        _viewModel.Password = "";

        // 聚焦到登入卡片（避免聚焦帳號/密碼框觸發鍵盤）
        LoginCard.Focus();

        // 等 UI 事件處理完後解除壓制
        Dispatcher.BeginInvoke(() => _suppressKeypadOnce = false,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        LblUsername.Text = loc["Login.Username"];
        LblPassword.Text = loc["Login.Password"];
        ChkRemember.Content = loc["Login.RememberPassword"];
        BtnLogin.Content = loc["Login.Submit"];
        CloseButton.ToolTip = loc["Login.Close"];
    }

    /// <summary>下拉選單選擇變更 — 不自動聚焦密碼框（由使用者主動點選）</summary>
    private void UserDropdown_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (UserDropdown.SelectedItem != null)
        {
            PasswordBox.Password = "";
            var selectedUser = UserDropdown.SelectedItem?.ToString() ?? "";
            EventLogService.Instance?.LogInfo("Auth", "LoginPage",
                ErrorCodes.UiInput, "UserDropdown Changed",
                $"SelectedUser={selectedUser}");

            // Guest 帳號檢查
            UpdateGuestPasswordState();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogButtonClick("LoginPage", "CloseApp");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }

    private void InputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // 跳過初始化/登出後的自動聚焦
        if (_suppressKeypadOnce)
            return;

        bool isPassword = sender is PasswordBox;

        if (isPassword)
        {
            // === 密碼框：依設定決定鍵盤類型 ===
            if (_settings.NumericKeypadOnly)
            {
                // 數字鍵盤
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoginCard.Focus();
                    NumericKeypad.Show(
                        password =>
                        {
                            PasswordBox.Password = password;
                            _viewModel.Password = password;
                        },
                        () => { });
                }));
            }
            else
            {
                // 一般觸控鍵盤（密碼模式）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoginCard.Focus();
                    TouchKeyboard.Show(true, _viewModel.Password ?? "",
                        result =>
                        {
                            PasswordBox.Password = result;
                            _viewModel.Password = result;
                        },
                        () => { });
                }));
            }
        }
        else if (sender is TextBox)
        {
            // === 帳號框：一般觸控鍵盤（帳號模式） ===
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoginCard.Focus();
                TouchKeyboard.Show(false, _viewModel.Username ?? "",
                    result =>
                    {
                        _viewModel.Username = result;
                        // 觸控鍵盤關閉後檢查 Guest 狀態
                        UpdateGuestPasswordState();
                    },
                    () => { });
            }));
        }

        // 遊框高亮
        if (sender is FrameworkElement fe && fe.Parent is Border border)
        {
            border.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#42A5F5"));
            border.BorderThickness = new Thickness(2);
        }
    }

    private void InputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Parent is Border border)
        {
            border.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#4A6A9A"));
            border.BorderThickness = new Thickness(1.5);
        }
    }

    /// <summary>觸控模式：阻擋實體鍵盤輸入（允許 Tab 導航）</summary>
    private void InputBox_PreviewKeyDown_Block(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab)
            e.Handled = true;
    }

    /// <summary>觸控模式：阻擋文字輸入</summary>
    private void InputBox_PreviewTextInput_Block(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        LoginSucceeded?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════
    // Guest 帳號密碼框控制
    // ═══════════════════════════════════════

    /// <summary>
    /// 檢查當前帳號是否為 Guest，並控制密碼框啟用/停用狀態
    /// - Guest + GuestLoginEnabled: 密碼框 disable，清空密碼
    /// - 其他: 密碼框 enable
    /// </summary>
    private void UpdateGuestPasswordState()
    {
        bool isGuest = _viewModel.IsGuestUser && _settings.GuestLoginEnabled;
        PasswordBox.IsEnabled = !isGuest;
        ChkRemember.IsEnabled = !isGuest;

        // Guest 提示文字
        GuestHintText.Visibility = isGuest ? Visibility.Visible : Visibility.Collapsed;
        if (isGuest)
        {
            GuestHintText.Text = LocalizationService.Instance["Login.GuestHint"];

            PasswordBox.Password = "";
            _viewModel.Password = "";
            ChkRemember.IsChecked = false;

            EventLogService.Instance?.LogInfo("UI", "LoginPage",
                ErrorCodes.GuestRestrictionApplied,
                "Guest Password Bypass",
                "PasswordBox=Disabled, Password=Cleared, RememberMe=Disabled");
        }
    }
}
