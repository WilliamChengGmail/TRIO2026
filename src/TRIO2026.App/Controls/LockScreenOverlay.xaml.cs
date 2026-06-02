using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using TRIO2026.App.Services;
using TRIO2026.Core;
using TRIO2026.Core.Entities;

namespace TRIO2026.App.Controls;

/// <summary>
/// 鎖定畫面 Overlay — 閒置超時後顯示，需密碼解鎖
/// 
/// 功能：
///   - 密碼驗證解鎖
///   - 切換使用者（設定控制）
///   - 進行中工作狀態顯示
///   - 穿透訊息（門板警告等高優先級訊息）
///
/// 製作者: Office of William
/// </summary>
public partial class LockScreenOverlay : UserControl
{
    private TaskCompletionSource<LockScreenResult>? _tcs;
    private AuthService? _authService;
    private SystemSettingService? _settings;
    private User? _lockedUser;

    public LockScreenOverlay()
    {
        InitializeComponent();
    }

    /// <summary>顯示鎖定畫面</summary>
    public Task<LockScreenResult> ShowAsync(
        User lockedUser,
        DateTime lockedAt,
        AuthService authService,
        SystemSettingService settings)
    {
        _lockedUser = lockedUser;
        _authService = authService;
        _settings = settings;

        // 多語系文字
        var loc = LocalizationService.Instance;
        TitleText.Text = loc["Lock.Title"];
        LblPassword.Text = loc["Login.Password"];
        UnlockButton.Content = loc["Lock.Unlock"];
        SwitchUserButton.Content = loc["Lock.SwitchUser"];

        // 使用者資訊
        UserInfoText.Text = $"👤 {lockedUser.DisplayName} ({lockedUser.Username})";
        LockedAtText.Text = string.Format(loc["Lock.LockedAt"], lockedAt.ToString("HH:mm"));

        // 重置狀態
        PasswordBox.Password = "";
        ErrorText.Visibility = Visibility.Collapsed;

        // 切換使用者按鈕（設定控制）
        SwitchUserButton.Visibility = settings.LockScreenSwitchUserEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        _tcs = new TaskCompletionSource<LockScreenResult>();
        Visibility = Visibility.Visible;

        // 進場動畫
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fadeIn);

        // 不自動聚焦密碼框，等使用者主動點擊

        return _tcs.Task;
    }

    /// <summary>更新進行中工作狀態</summary>
    /// <param name="statusText">狀態文字（null 或空字串=隱藏）</param>
    public void UpdateWorkStatus(string? statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            WorkStatusPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            WorkStatusText.Text = statusText;
            WorkStatusPanel.Visibility = Visibility.Visible;
        }
    }

    /// <summary>顯示穿透訊息（高優先級，不需使用者操作）</summary>
    public void ShowPassthroughMessage(string title, string message)
    {
        PassthroughTitle.Text = title;
        PassthroughMessage.Text = message;
        PassthroughPanel.Visibility = Visibility.Visible;

        // 動畫
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        PassthroughPanel.BeginAnimation(OpacityProperty, fadeIn);

        // EventLog
        EventLogService.Instance?.LogInfo("UI", "LockScreen",
            ErrorCodes.LockPassthroughMsg,
            "Passthrough Message Shown",
            $"Title={title}");
    }

    /// <summary>隱藏穿透訊息</summary>
    public void HidePassthroughMessage()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) => PassthroughPanel.Visibility = Visibility.Collapsed;
        PassthroughPanel.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>鎖定畫面是否正在顯示</summary>
    public bool IsShowing => Visibility == Visibility.Visible;

    private void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        AttemptUnlock();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        // ReadOnly 模式下，Enter 鍵觸發解鎖
        if (e.Key == Key.Enter)
        {
            AttemptUnlock();
            e.Handled = true;
        }
    }

    /// <summary>⌨ 按鈕點擊 → 彈出鍵盤</summary>
    private void KeyboardButton_Click(object sender, RoutedEventArgs e)
    {
        ShowAppropriateKeyboard();
    }

    /// <summary>密碼框外框點擊也觸發鍵盤</summary>
    private void PasswordBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // 鍵盤已開就不重複
        if (NumericKeypad.Visibility == Visibility.Visible || TouchKeyboard.Visibility == Visibility.Visible)
            return;
        ShowAppropriateKeyboard();
    }

    /// <summary>依據設定決定顯示數字鍵盤或全鍵盤</summary>
    private void ShowAppropriateKeyboard()
    {
        var useNumeric = _settings != null
            && _settings.NumericKeypadOnly
            && !_settings.LockScreenSwitchUserEnabled;

        if (useNumeric)
        {
            NumericKeypad.Show(
                password =>
                {
                    PasswordBox.Password = password;
                    NumericKeypad.Hide();
                    AttemptUnlock();
                },
                () => NumericKeypad.Hide());
        }
        else
        {
            TouchKeyboard.Show(
                isPassword: true,
                initialText: "",
                onConfirm: password =>
                {
                    PasswordBox.Password = password;
                    TouchKeyboard.Hide();
                    AttemptUnlock();
                },
                onCancel: () => TouchKeyboard.Hide());
        }
    }

    private void AttemptUnlock()
    {
        if (_lockedUser == null || _authService == null) return;

        var password = PasswordBox.Password ?? "";
        if (string.IsNullOrEmpty(password))
        {
            ShowError(LocalizationService.Instance["Lock.InvalidPassword"]);
            return;
        }

        // 驗證密碼
        var isValid = _authService.VerifyPassword(password, _lockedUser.PasswordHash);
        if (isValid)
        {
            EventLogService.Instance?.LogInfo("Auth", "LockScreen",
                ErrorCodes.SessionUnlocked,
                "Session Unlocked",
                $"User={_lockedUser.Username}");

            Hide(LockScreenResult.Unlocked);
        }
        else
        {
            EventLogService.Instance?.LogWarning("Auth", "LockScreen",
                ErrorCodes.LockInvalidPassword,
                "Lock Screen - Invalid Password",
                $"User={_lockedUser.Username}");

            ShowError(LocalizationService.Instance["Lock.InvalidPassword"]);
            PlayShakeAnimation();

            // 重新彈出鍵盤
            Dispatcher.BeginInvoke(() => ShowAppropriateKeyboard());
        }
    }

    private void SwitchUserButton_Click(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogInfo("Auth", "LockScreen",
            ErrorCodes.LockSwitchUser,
            "Lock Screen - Switch User",
            $"User={_lockedUser?.Username}");

        Hide(LockScreenResult.SwitchUser);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.Password = "";
        PasswordBox.Focus();
    }

    private void PlayShakeAnimation()
    {
        if (TryFindResource("ShakeAnimation") is Storyboard sb)
        {
            Storyboard.SetTarget(sb, LockCard);
            sb.Begin();
        }
    }

    private void Hide(LockScreenResult result)
    {
        // 隱藏鍵盤
        NumericKeypad.Hide();
        TouchKeyboard.Hide();

        // 清除穿透訊息
        PassthroughPanel.Visibility = Visibility.Collapsed;
        WorkStatusPanel.Visibility = Visibility.Collapsed;

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) =>
        {
            Visibility = Visibility.Collapsed;
            PasswordBox.Password = "";
            ErrorText.Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(result);
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }
}

/// <summary>鎖定畫面操作結果</summary>
public enum LockScreenResult
{
    /// <summary>密碼驗證成功，解鎖</summary>
    Unlocked,

    /// <summary>切換使用者（完整登出）</summary>
    SwitchUser
}
