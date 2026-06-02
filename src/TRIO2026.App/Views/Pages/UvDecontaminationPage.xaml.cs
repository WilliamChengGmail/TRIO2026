using System.Windows;
using System.Windows.Controls;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.App.ViewModels;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// UV Decontamination 頁面 Code-Behind
/// 
/// 負責：
///   - ViewModel 初始化與事件訂閱
///   - 倒數完成提示（使用 OverlayDialog）
///   - 門板警示 Overlay 的顯示/隱藏
///   - UserMenuControl 初始化
/// </summary>
public partial class UvDecontaminationPage : UserControl
{
    private readonly UvDecontaminationViewModel _viewModel;
    private readonly SessionService _sessionService;

    public UvDecontaminationPage(
        UvDecontaminationViewModel viewModel,
        SessionService sessionService,
        OverlayDialog dialogOverlay,
        LoginOverlay loginOverlay,
        AuthService authService,
        TokenService tokenService,
        SystemSettingService systemSettings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _sessionService = sessionService;
        DataContext = _viewModel;

        // 初始化共用使用者選單
        UserMenu.Initialize(sessionService,
            dialogOverlay, loginOverlay, authService, tokenService, systemSettings);

        // 訂閱 ViewModel 事件
        _viewModel.CountdownCompleted += OnCountdownCompleted;
        _viewModel.StopRequested += OnStopRequested;
        _viewModel.StartBlockedByDoor += OnStartBlockedByDoor;
        _viewModel.DoorInterrupted += OnDoorInterrupted;
        _viewModel.DoorResumed += OnDoorResumed;

        // IsRunning 變更時同步 UserMenu 狀態
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UvDecontaminationViewModel.IsRunning))
            {
                UserMenu.IsHomeEnabled = !_viewModel.IsRunning;
                UserMenu.IsUserIconEnabled = !_viewModel.IsRunning;

                // 註：UV 執行中不暫停閒置計時器
                // 安全考量：即使 UV 進行中，超時仍應鎖定畫面
                // LockScreen 已支援穿透訊息（門板事件）和完成訊息佇列
            }
        };
    }

    private bool _initialized;

    public async void OnNavigatedTo(string? fromPage)
    {
        try
        {
            if (!_initialized)
            {
                await _viewModel.InitializeAsync();
                _initialized = true;
            }
            else if (fromPage == "menu")
            {
                await _viewModel.ResetToDefaultAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UvPage] OnNavigatedTo 失敗: {ex.Message}");
        }
    }

    /// <summary>倒數結束 — 自動關閉停止確認視窗（若開啟中），並顯示完成提示</summary>
    private async void OnCountdownCompleted(object? sender, EventArgs e)
    {
        // 若停止確認視窗正在顯示，自動關閉它
        if (StopConfirmOverlay.IsShowing)
        {
            StopConfirmOverlay.Dismiss();
        }

        var shell = Window.GetWindow(this) as AppShell;
        if (shell != null)
        {
            var loc = LocalizationService.Instance;

            // 鎖定中 → 完成訊息排隊，解鎖後才顯示
            if (shell.LockScreen.IsShowing)
            {
                _sessionService.EnqueueMessage(
                    loc["UV.CompleteTitle"],
                    loc["UV.CompleteMessage"],
                    "✅");
                // 清除工作狀態顯示
                shell.LockScreen.UpdateWorkStatus(null);
            }
            else
            {
                var dialog = shell.FindName("DialogOverlay") as OverlayDialog;
                if (dialog != null)
                {
                    await dialog.ShowAsync(
                        loc["UV.CompleteTitle"],
                        loc["UV.CompleteMessage"],
                        loc["Common.OK"],
                        OverlayDialogIcon.Success);
                }
            }
        }
    }

    /// <summary>使用者按 Stop — 彈出確認視窗（倒數繼續）</summary>
    private async void OnStopRequested(object? sender, EventArgs e)
    {
        var confirmed = await StopConfirmOverlay.ShowAsync();
        if (confirmed)
        {
            await _viewModel.ConfirmStopAsync();
        }
        // 按 No：不做任何事，倒數繼續
    }

    /// <summary>門板未關閉時嘗試啟動 — 顯示警告對話框</summary>
    private async void OnStartBlockedByDoor(object? sender, EventArgs e)
    {
        var shell = Window.GetWindow(this) as AppShell;
        if (shell != null)
        {
            var dialog = shell.FindName("DialogOverlay") as OverlayDialog;
            if (dialog != null)
            {
                var loc = LocalizationService.Instance;
                await dialog.ShowAsync(
                    loc["UV.DoorErrorTitle"],
                    loc["UV.DoorErrorMessage"],
                    loc["Common.OK"],
                    OverlayDialogIcon.Warning);
            }
        }
    }

    /// <summary>門板開啟 — 顯示錯誤 Overlay（鎖定中穿透顯示）</summary>
    private void OnDoorInterrupted(object? sender, EventArgs e)
    {
        DoorErrorOverlay.Show();

        // 鎖定中 → 穿透訊息顯示在鎖定畫面上
        var shell = Window.GetWindow(this) as AppShell;
        if (shell?.LockScreen.IsShowing == true)
        {
            var loc = LocalizationService.Instance;
            shell.LockScreen.ShowPassthroughMessage(
                loc["UV.DoorErrorTitle"],
                loc["UV.DoorErrorMessage"]);
        }
    }

    /// <summary>門板關閉 — 隱藏錯誤 Overlay（鎖定中自動關閉穿透訊息）</summary>
    private void OnDoorResumed(object? sender, EventArgs e)
    {
        DoorErrorOverlay.Hide();

        // 鎖定中 → 自動關閉穿透訊息
        var shell = Window.GetWindow(this) as AppShell;
        if (shell?.LockScreen.IsShowing == true)
        {
            shell.LockScreen.HidePassthroughMessage();
        }
    }
}
