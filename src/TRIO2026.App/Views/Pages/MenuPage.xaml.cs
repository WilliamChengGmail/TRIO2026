using System.Windows;
using System.Windows.Controls;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.App.Views;
using TRIO2026.Core;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// MenuPage — 02 主選單頁面，5 個功能按鈕
/// 使用者選單由共用 UserMenuControl 處理
/// </summary>
public partial class MenuPage : UserControl
{
    private readonly SessionService _sessionService;
    private readonly OverlayDialog _dialogOverlay;
    private readonly SystemSettingService _systemSettings;

    public MenuPage(SessionService sessionService,
        OverlayDialog dialogOverlay, LoginOverlay loginOverlay,
        AuthService authService, TokenService tokenService,
        SystemSettingService systemSettings)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _dialogOverlay = dialogOverlay;
        _systemSettings = systemSettings;

        // 初始化共用使用者選單
        UserMenu.Initialize(sessionService,
            dialogOverlay, loginOverlay, authService, tokenService, systemSettings);

        // 根據 Device.operation_mode 控制按鈕啟用狀態
        ApplyOperationMode();

        // Guest 登入時限制功能
        ApplyGuestRestrictions();
    }

    /// <summary>
    /// 依據 DeviceOperationMode 設定決定 IntelliPlex / Custom 按鈕啟用狀態
    /// Combo       → 兩者皆啟用
    /// IntelliPlex → IntelliPlex 啟用, Custom 停用
    /// Custom      → Custom 啟用, IntelliPlex 停用
    /// </summary>
    private void ApplyOperationMode()
    {
        var mode = _systemSettings.DeviceOperationMode;
        Console.WriteLine($"[MenuPage] Device operation mode: {mode}");

        switch (mode)
        {
            case "Combo":
                BtnIntelliPlex.IsEnabled = true;
                BtnCustom.IsEnabled = true;
                break;
            case "Custom":
                BtnIntelliPlex.IsEnabled = false;
                BtnCustom.IsEnabled = true;
                break;
            case "IntelliPlex":
            default:
                BtnIntelliPlex.IsEnabled = true;
                BtnCustom.IsEnabled = false;
                break;
        }
    }

    /// <summary>
    /// Guest 登入時限制 Setting / UV 功能
    /// </summary>
    private void ApplyGuestRestrictions()
    {
        if (_sessionService.IsGuestLogin)
        {
            BtnSetting.IsEnabled = false;
            BtnUV.IsEnabled = false;
            EventLogService.Instance?.LogInfo("UI", "MenuPage",
                ErrorCodes.GuestRestrictionApplied,
                "Guest Feature Restriction",
                "Disabled=Setting,UV");
        }
    }

    /// <summary>供外部呼叫刷新使用者顯示</summary>
    public void RefreshUserDisplay()
    {
        UserMenu.RefreshUserDisplay();
        ApplyGuestRestrictions();
    }

    // ── 功能按鈕 ──

    private async void OnIntelliPlexClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance.LogButtonClick("MenuPage", "IntelliPlex");
        await _dialogOverlay.ShowAsync("IntelliPlex Program", "功能開發中...", "確定", OverlayDialogIcon.Info);
    }

    private async void OnCustomClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance.LogButtonClick("MenuPage", "Custom");
        await _dialogOverlay.ShowAsync("Custom Program", "功能開發中...", "確定", OverlayDialogIcon.Info);
    }

    private void OnDataClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance.LogButtonClick("MenuPage", "Data");

        // Guest / local_operator 不可進入 Data（已在 ApplyGuestRestrictions 中處理按鈕停用）
        var shell = Window.GetWindow(this) as AppShell;
        shell?.NavigateTo("data");
    }

    private async void OnSettingClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance.LogButtonClick("MenuPage", "Setting");
        await _dialogOverlay.ShowAsync("Setting", "功能開發中...", "確定", OverlayDialogIcon.Info);
    }

    private void OnUVClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance.LogButtonClick("MenuPage", "UV");
        var shell = Window.GetWindow(this) as AppShell;
        shell?.NavigateTo("uv");
    }
}
