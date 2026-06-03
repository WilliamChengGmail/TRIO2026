using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.App.Views;
using TRIO2026.Core;
using TRIO2026.Core.Entities;
using TRIO2026.Core.Enums;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// 帳號管理頁面 — Admin 專用
/// 
/// 佈局：左右二段式
///   - 左側：帳號列表 + 新增按鈕
///   - 右側：操作面板 / 詳細資料面板（二選一切換）
/// 
/// 所有操作透過 AccountManagementService 執行，
/// 所有文字透過 LocalizationService 讀取。
/// 
/// 製作者: Office of William
/// </summary>
public partial class AccountManagementPage : UserControl
{
    private readonly SessionService _sessionService;
    private readonly AuthService _authService;
    private readonly TokenService _tokenService;
    private readonly SystemSettingService _systemSettings;
    private readonly AccountManagementService _accountService;
    private readonly PasswordPolicyService _policyService;

    private User? _selectedUser;
    private List<User> _userList = new();

    public AccountManagementPage(
        SessionService sessionService,
        AuthService authService,
        TokenService tokenService,
        SystemSettingService systemSettings,
        AccountManagementService accountService,
        PasswordPolicyService policyService)
    {
        InitializeComponent();

        _sessionService = sessionService;
        _authService = authService;
        _tokenService = tokenService;
        _systemSettings = systemSettings;
        _accountService = accountService;
        _policyService = policyService;

        // 初始化子元件
        UserMenu.Initialize(sessionService, DialogOverlay, LoginOverlayHost,
            authService, tokenService, systemSettings,
            ChangePasswordOverlayHost, policyService);

        ChangePasswordOverlayHost.Initialize(authService, policyService);

        var operatorUsername = sessionService.CurrentUser?.Username ?? "SYSTEM";
        CreateAccountOverlayHost.Initialize(accountService, operatorUsername);

        Loaded += (_, _) => ApplyLocalization();
    }

    /// <summary>頁面導航到時刷新</summary>
    public void RefreshUserDisplay()
    {
        UserMenu.RefreshUserDisplay();
        ApplyLocalization();
        _ = LoadUsersAsync();
    }

    // ═══════════════════════════════════════
    // 多語系
    // ═══════════════════════════════════════

    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        PageTitle.Text = loc["AccountMgmt.Title"];
        SelectPromptText.Text = loc["AccountMgmt.SelectPrompt"];

        // 動態取得 BtnAddText（在 ControlTemplate 中）
        if (BtnAddAccount.Template?.FindName("BtnAddText", BtnAddAccount) is TextBlock addText)
            addText.Text = loc["AccountMgmt.AddAccount"];

        // BackToOps 按鈕
        if (BtnBackToOps.Template?.FindName("BackText", BtnBackToOps) is TextBlock backText)
            backText.Text = $"← {loc["AccountMgmt.BackToOps"]}";
    }

    // ═══════════════════════════════════════
    // 資料載入
    // ═══════════════════════════════════════

    private async Task LoadUsersAsync()
    {
        _userList = await _accountService.GetAllManagedUsersAsync();

        var loc = LocalizationService.Instance;
        var displayList = _userList.Select(u => new UserListItem
        {
            Id = u.Id,
            DisplayLabel = u.DisplayName ?? u.Username,
            SubLabel = $"@{u.Username} · {GetRoleName(u.RoleLevel, loc)}",
            StatusIcon = GetStatusIcon(u),
            StatusColor = GetStatusBrush(u)
        }).ToList();

        UserListPanel.ItemsSource = displayList;
    }

    private static string GetRoleName(int roleLevel, LocalizationService loc) => roleLevel switch
    {
        1 => loc["AccountMgmt.RoleOperator"],
        2 => loc["AccountMgmt.RoleService"],
        3 => loc["AccountMgmt.RoleAdmin"],
        _ => $"Level {roleLevel}"
    };

    private static string GetStatusIcon(User u)
    {
        if (u.IsActive == 0) return "⛔";
        if (!string.IsNullOrEmpty(u.LockedUntil)) return "🔒";
        return "✅";
    }

    private static Brush GetStatusBrush(User u)
    {
        if (u.IsActive == 0) return new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        if (!string.IsNullOrEmpty(u.LockedUntil)) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
        return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    }

    // ═══════════════════════════════════════
    // 帳號選擇
    // ═══════════════════════════════════════

    private void OnUserItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int userId) return;

        _selectedUser = _userList.FirstOrDefault(u => u.Id == userId);
        if (_selectedUser == null) return;

        ShowOperationsPanel();
    }

    private void ShowOperationsPanel()
    {
        if (_selectedUser == null) return;

        var loc = LocalizationService.Instance;

        EmptyPrompt.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Visible;

        SelectedUserName.Text = _selectedUser.DisplayName ?? _selectedUser.Username;
        SelectedUserRole.Text = $"@{_selectedUser.Username} · {GetRoleName(_selectedUser.RoleLevel, loc)}";

        // 動態產生操作按鈕
        BuildActionButtons();
    }

    private void BuildActionButtons()
    {
        ActionButtonsPanel.Children.Clear();
        if (_selectedUser == null) return;

        var loc = LocalizationService.Instance;
        var currentUser = _sessionService.CurrentUser;
        var isSelf = currentUser?.Id == _selectedUser.Id;
        var isService = _selectedUser.RoleLevel == 2;

        // Guest 特殊帳號：僅可檢視，不可修改/刪除
        var isGuestAccount = string.Equals(_selectedUser.Username, "guest", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(_selectedUser.Username, "local_operator", StringComparison.OrdinalIgnoreCase);

        // 檢視詳細資料
        AddActionButton("📋", loc["AccountMgmt.ViewDetails"], OnViewDetailsClick);

        if (isGuestAccount) return; // Guest/local_operator：僅可檢視

        // 停用/啟用
        if (!isSelf && !isService)
        {
            if (_selectedUser.IsActive == 1)
                AddActionButton("⛔", loc["AccountMgmt.Disable"], OnDisableClick, "#EF5350");
            else
                AddActionButton("✅", loc["AccountMgmt.Enable"], OnEnableClick, "#4CAF50");
        }

        // 鎖定/解鎖（依 DB 設定控制可見性）
        if (!isSelf && _systemSettings.AccountLockEnabled)
        {
            if (string.IsNullOrEmpty(_selectedUser.LockedUntil))
                AddActionButton("🔒", loc["AccountMgmt.Lock"], OnLockClick, "#FFB74D");
            else
                AddActionButton("🔓", loc["AccountMgmt.Unlock"], OnUnlockClick, "#4FC3F7");
        }

        // 重設密碼
        if (!isSelf && !isService)
            AddActionButton("🔑", loc["AccountMgmt.ResetPassword"], OnResetPasswordClick);

        // 刪除
        if (!isSelf && !isService)
            AddActionButton("🗑️", loc["AccountMgmt.Delete"], OnDeleteClick, "#EF5350");
    }

    private void AddActionButton(string icon, string text, RoutedEventHandler click,
        string fgColor = "#F0F4F8")
    {
        var btn = new Button { Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 8, 8) };
        btn.Click += click;

        var template = new ControlTemplate(typeof(Button));
        var bdFactory = new FrameworkElementFactory(typeof(Border), "Bd");
        bdFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x1A, 0x2D, 0x47)));
        bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        bdFactory.SetValue(Border.PaddingProperty, new Thickness(16, 12, 16, 12));
        bdFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x3D, 0x5E)));
        bdFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var spFactory = new FrameworkElementFactory(typeof(StackPanel));
        spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.TextProperty, icon);
        iconFactory.SetValue(TextBlock.FontSizeProperty, 18.0);
        iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        spFactory.AppendChild(iconFactory);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, text);
        textFactory.SetValue(TextBlock.FontSizeProperty, 15.0);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(fgColor)!;
        textFactory.SetValue(TextBlock.ForegroundProperty, brush);
        spFactory.AppendChild(textFactory);

        bdFactory.AppendChild(spFactory);
        template.VisualTree = bdFactory;

        // Pressed trigger
        var trigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x2A, 0x40, 0x60)), "Bd"));
        template.Triggers.Add(trigger);

        btn.Template = template;
        ActionButtonsPanel.Children.Add(btn);
    }

    // ═══════════════════════════════════════
    // 操作事件
    // ═══════════════════════════════════════

    private async void OnAddAccountClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogButtonClick("AccountMgmt", "AddAccount");

        var result = await CreateAccountOverlayHost.ShowAsync();
        if (result.IsCreated && result.TempPassword != null)
        {
            var loc = LocalizationService.Instance;

            // 顯示臨時密碼
            await DialogOverlay.ShowAsync(
                loc["AccountMgmt.TempPasswordTitle"],
                $"{loc["AccountMgmt.TempPasswordNote"]}\n\n🔐  {result.TempPassword}",
                loc["AccountMgmt.TempPasswordCopied"],
                OverlayDialogIcon.Success);

            await LoadUsersAsync();
        }
    }

    private async void OnDisableClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var confirmed = await DialogOverlay.ShowConfirmAsync(
            loc["AccountMgmt.Disable"],
            loc["AccountMgmt.ConfirmDisable"],
            loc["Common.Confirm"], loc["Common.Cancel"],
            OverlayDialogIcon.Warning);
        if (!confirmed) return;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error) = await _accountService.SetActiveAsync(_selectedUser.Id, false, operatorName);

        if (success)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.AccountDisabled,
                "Account Disabled",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(loc["AccountMgmt.Disable"],
                loc["AccountMgmt.SuccessDisabled"], loc["Common.OK"], OverlayDialogIcon.Success);

            await LoadUsersAsync();
            ShowSelectPrompt();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    private async void OnEnableClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var confirmed = await DialogOverlay.ShowConfirmAsync(
            loc["AccountMgmt.Enable"],
            loc["AccountMgmt.ConfirmEnable"],
            loc["Common.Confirm"], loc["Common.Cancel"],
            OverlayDialogIcon.Warning);
        if (!confirmed) return;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error) = await _accountService.SetActiveAsync(_selectedUser.Id, true, operatorName);

        if (success)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.AccountEnabled,
                "Account Enabled",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(loc["AccountMgmt.Enable"],
                loc["AccountMgmt.SuccessEnabled"], loc["Common.OK"], OverlayDialogIcon.Success);

            await LoadUsersAsync();
            ShowSelectPrompt();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    private async void OnLockClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var confirmed = await DialogOverlay.ShowConfirmAsync(
            loc["AccountMgmt.Lock"],
            loc["AccountMgmt.ConfirmLock"],
            loc["Common.Confirm"], loc["Common.Cancel"],
            OverlayDialogIcon.Warning);
        if (!confirmed) return;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error) = await _accountService.LockUserAsync(_selectedUser.Id, operatorName);

        if (success)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.AccountLocked,
                "Account Locked",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(loc["AccountMgmt.Lock"],
                loc["AccountMgmt.SuccessLocked"], loc["Common.OK"], OverlayDialogIcon.Success);

            await LoadUsersAsync();
            ShowSelectPrompt();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    private async void OnUnlockClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error) = await _accountService.UnlockUserAsync(_selectedUser.Id, operatorName);

        if (success)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.AccountUnlocked,
                "Account Unlocked",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(loc["AccountMgmt.Unlock"],
                loc["AccountMgmt.SuccessUnlocked"], loc["Common.OK"], OverlayDialogIcon.Success);

            await LoadUsersAsync();
            ShowSelectPrompt();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    private async void OnResetPasswordClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var confirmed = await DialogOverlay.ShowConfirmAsync(
            loc["AccountMgmt.ResetPassword"],
            loc["AccountMgmt.ConfirmResetPassword"],
            loc["Common.Confirm"], loc["Common.Cancel"],
            OverlayDialogIcon.Warning);
        if (!confirmed) return;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error, tempPassword) = await _accountService.ResetPasswordAsync(
            _selectedUser.Id, operatorName);

        if (success && tempPassword != null)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.PasswordReset,
                "Password Reset",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(
                loc["AccountMgmt.TempPasswordTitle"],
                $"{loc["AccountMgmt.TempPasswordNote"]}\n\n🔐  {tempPassword}",
                loc["AccountMgmt.TempPasswordCopied"],
                OverlayDialogIcon.Success);

            await LoadUsersAsync();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;
        var loc = LocalizationService.Instance;

        var confirmed = await DialogOverlay.ShowConfirmAsync(
            loc["AccountMgmt.Delete"],
            loc["AccountMgmt.ConfirmDelete"],
            loc["Common.Confirm"], loc["Common.Cancel"],
            OverlayDialogIcon.Error);
        if (!confirmed) return;

        var operatorName = _sessionService.CurrentUser?.Username ?? "SYSTEM";
        var (success, error) = await _accountService.DeleteUserAsync(_selectedUser.Id, operatorName);

        if (success)
        {
            EventLogService.Instance?.LogAccountMgmt(ErrorCodes.AccountDeleted,
                "Account Deleted (Soft)",
                $"Username={_selectedUser.Username}, By={operatorName}");

            await DialogOverlay.ShowAsync(loc["AccountMgmt.Delete"],
                loc["AccountMgmt.SuccessDeleted"], loc["Common.OK"], OverlayDialogIcon.Success);

            await LoadUsersAsync();
            ShowSelectPrompt();
        }
        else
        {
            await ShowOperationError(error);
        }
    }

    // ═══════════════════════════════════════
    // 詳細資料
    // ═══════════════════════════════════════

    private void OnViewDetailsClick(object sender, RoutedEventArgs e)
    {
        if (_selectedUser == null) return;

        var loc = LocalizationService.Instance;

        // 每個欄位用 (fieldKey, label, value) 三元組定義
        var allFields = new List<(string Key, string Label, string Value)>
        {
            ("Username",        loc["AccountMgmt.DetailUsername"],         _selectedUser.Username),
            ("DisplayName",     loc["AccountMgmt.DetailDisplayName"],     _selectedUser.DisplayName ?? "-"),
            ("Role",            loc["AccountMgmt.DetailRole"],            GetRoleName(_selectedUser.RoleLevel, loc)),
            ("Status",          loc["AccountMgmt.DetailStatus"],          GetStatusText(_selectedUser, loc)),
            ("EmployeeId",      loc["AccountMgmt.DetailEmployeeId"],      _selectedUser.EmployeeId ?? loc["AccountMgmt.None"]),
            ("Department",      loc["AccountMgmt.DetailDepartment"],      _selectedUser.Department ?? loc["AccountMgmt.None"]),
            ("Email",           loc["AccountMgmt.DetailEmail"],           _selectedUser.Email ?? loc["AccountMgmt.None"]),
            ("LastLogin",       loc["AccountMgmt.DetailLastLogin"],
                string.IsNullOrEmpty(_selectedUser.LastLoginAt)
                    ? loc["AccountMgmt.None"]
                    : FormatIso8601(_selectedUser.LastLoginAt)),
            ("PasswordChanged", loc["AccountMgmt.DetailPasswordChanged"],
                string.IsNullOrEmpty(_selectedUser.PasswordChangedAt)
                    ? loc["AccountMgmt.None"]
                    : FormatIso8601(_selectedUser.PasswordChangedAt)),
            ("ForceChange",     loc["AccountMgmt.DetailForceChange"],
                _selectedUser.ForcePasswordChange == 1 ? "⚠️ Yes" : "No"),
            ("LockedUntil",     loc["AccountMgmt.DetailLockedUntil"],
                string.IsNullOrEmpty(_selectedUser.LockedUntil)
                    ? loc["AccountMgmt.NotLocked"]
                    : FormatIso8601(_selectedUser.LockedUntil)),
            ("FailedCount",     loc["AccountMgmt.DetailFailedCount"],     _selectedUser.FailedLoginCount.ToString()),
            ("Created",         loc["AccountMgmt.DetailCreated"],
                string.IsNullOrEmpty(_selectedUser.CreatedAt) ? "-" : FormatIso8601(_selectedUser.CreatedAt)),
            ("CreatedBy",       loc["AccountMgmt.DetailCreatedBy"],       _selectedUser.CreatedBy ?? "-"),
            ("Notes",           loc["AccountMgmt.DetailNotes"],           _selectedUser.Notes ?? loc["AccountMgmt.None"]),
        };

        // 從 DB 讀取要顯示的欄位（可透過 SystemSetting 設定）
        var visibleKeys = _systemSettings.UserDetailVisibleFields;

        var details = allFields
            .Where(f => visibleKeys.Contains(f.Key))
            .Select(f => new DetailItem(f.Label, f.Value))
            .ToList();

        DetailsItemsPanel.ItemsSource = details;

        OperationsPanel.Visibility = Visibility.Collapsed;
        EmptyPrompt.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Visible;
    }

    private void OnBackToOpsClick(object sender, RoutedEventArgs e)
    {
        DetailsPanel.Visibility = Visibility.Collapsed;
        ShowOperationsPanel();
    }

    // ═══════════════════════════════════════
    // 輔助方法
    // ═══════════════════════════════════════

    private void ShowSelectPrompt()
    {
        _selectedUser = null;
        OperationsPanel.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Collapsed;
        EmptyPrompt.Visibility = Visibility.Visible;
    }

    private static string GetStatusText(User u, LocalizationService loc)
    {
        if (u.IsActive == 0) return loc["AccountMgmt.StatusDisabled"];
        if (!string.IsNullOrEmpty(u.LockedUntil)) return loc["AccountMgmt.StatusLocked"];
        return loc["AccountMgmt.StatusActive"];
    }

    private static string FormatIso8601(string iso)
        => Core.Helpers.TimeDisplayHelper.ToLocalDisplay(iso);

    private async Task ShowOperationError(string? error)
    {
        var loc = LocalizationService.Instance;
        var msg = error switch
        {
            "ERROR_SELF" => loc["AccountMgmt.ErrorSelf"],
            "ERROR_LAST_ADMIN" => loc["AccountMgmt.ErrorLastAdmin"],
            "ERROR_SERVICE_DELETE" => loc["AccountMgmt.ErrorServiceDelete"],
            "ERROR_SYSTEM_ACCOUNT" => loc["AccountMgmt.ErrorSystemAccount"],
            _ => error ?? "Unknown error"
        };

        // 系統帳號守衛觸發時額外記錄 log
        if (error == "ERROR_SYSTEM_ACCOUNT")
        {
            EventLogService.Instance?.LogWarning("AccountMgmt", "AccountManagementPage",
                ErrorCodes.SystemAccountGuard,
                "System Account Guard Triggered",
                $"TargetUser={_selectedUser?.Username}, Operator={_sessionService.CurrentUser?.Username}");
        }

        await DialogOverlay.ShowAsync("Error", msg, loc["Common.OK"], OverlayDialogIcon.Error);
    }
}

// ═══════════════════════════════════════
// 顯示用資料模型
// ═══════════════════════════════════════

/// <summary>帳號列表項目</summary>
public class UserListItem
{
    public int Id { get; set; }
    public string DisplayLabel { get; set; } = "";
    public string SubLabel { get; set; } = "";
    public string StatusIcon { get; set; } = "";
    public Brush StatusColor { get; set; } = Brushes.Gray;
}

/// <summary>詳細資料欄位</summary>
public record DetailItem(string Label, string Value);
