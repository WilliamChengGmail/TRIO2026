using System.Windows.Input;
using System.Windows.Threading;
using TRIO2026.App.Helpers;
using TRIO2026.App.Services;
using TRIO2026.Core;
using TRIO2026.Core.Entities;
using TRIO2026.Core.Interfaces;

namespace TRIO2026.App.ViewModels;

/// <summary>
/// UV Decontamination 頁面 ViewModel
/// 
/// 負責：
///   - 從 DB 載入時間選項（UvConfigService）
///   - 左右方向鍵切換時間選項
///   - Start/Stop 控制與倒數計時
///   - 門板開啟/關閉的中斷與恢復
///   - 畫面鎖定（倒數期間禁止返回 HOME）
///   - 多語系字串綁定（LocalizationService）
/// </summary>
public class UvDecontaminationViewModel : ViewModelBase
{
    private readonly UvConfigService _configService;
    private readonly IUvHardwareService _hardwareService;
    private readonly DispatcherTimer _timer;

    private List<UvTimerOption> _timeOptions = new();
    private int _selectedIndex = -1;
    private int _remainingSeconds;
    private bool _isRunning;
    private bool _isDoorOpen;

    public UvDecontaminationViewModel(
        UvConfigService configService,
        IUvHardwareService hardwareService)
    {
        _configService = configService;
        _hardwareService = hardwareService;

        // 倒數計時器（1 秒間隔）
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;

        // 門板事件訂閱
        _hardwareService.DoorOpened += (_, _) => OnDoorOpened();
        _hardwareService.DoorClosed += (_, _) => OnDoorClosed();

        // Commands
        StartStopCommand = new AsyncRelayCommand(ExecuteStartStopAsync, _ => !IsRunning ? !IsDoorOpen : true);
        PreviousCommand = new RelayCommand(_ => SelectPrevious(), _ => !IsRunning && HasPrevious && !IsDoorOpen);
        NextCommand = new RelayCommand(_ => SelectNext(), _ => !IsRunning && HasNext && !IsDoorOpen);
    }

    // ═══════════════════════════════════════
    // 繫結屬性
    // ═══════════════════════════════════════

    /// <summary>可選時間列表</summary>
    public List<UvTimerOption> TimeOptions
    {
        get => _timeOptions;
        private set
        {
            if (SetProperty(ref _timeOptions, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>當前選中的時間索引</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                OnPropertyChanged(nameof(SelectedOption));
                OnPropertyChanged(nameof(SelectedDisplayLabel));
                OnPropertyChanged(nameof(HasPrevious));
                OnPropertyChanged(nameof(HasNext));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>當前選中的選項</summary>
    public UvTimerOption? SelectedOption =>
        _timeOptions.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _timeOptions.Count
            ? _timeOptions[_selectedIndex]
            : null;

    /// <summary>當前選中的顯示標籤（例: "15:00"）</summary>
    public string SelectedDisplayLabel => 
        _isDoorOpen && !_isRunning ? "--:--" : (SelectedOption?.DisplayLabel ?? "--:--");

    /// <summary>是否可往左切換</summary>
    public bool HasPrevious => _selectedIndex > 0;

    /// <summary>是否可往右切換</summary>
    public bool HasNext => _selectedIndex < _timeOptions.Count - 1;

    /// <summary>倒數剩餘秒數</summary>
    public int RemainingSeconds
    {
        get => _remainingSeconds;
        private set
        {
            if (SetProperty(ref _remainingSeconds, value))
                OnPropertyChanged(nameof(RemainingDisplay));
        }
    }

    /// <summary>倒數顯示（mm:ss 格式）</summary>
    public string RemainingDisplay
    {
        get
        {
            var minutes = _remainingSeconds / 60;
            var seconds = _remainingSeconds % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }
    }

    /// <summary>UV 是否運行中</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(ShowTimeSelector));
                OnPropertyChanged(nameof(ShowCountdown));
            }
        }
    }

    /// <summary>門板是否開啟</summary>
    public bool IsDoorOpen
    {
        get => _isDoorOpen;
        private set
        {
            if (SetProperty(ref _isDoorOpen, value))
            {
                OnPropertyChanged(nameof(ShowDoorWarning));
                OnPropertyChanged(nameof(SelectedDisplayLabel));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>是否允許返回 HOME（僅非運行時可返回）</summary>
    public bool CanNavigateBack => !IsRunning;

    /// <summary>是否顯示時間選擇器（未運行時顯示）</summary>
    public bool ShowTimeSelector => !IsRunning;

    /// <summary>是否顯示倒數計時（運行中顯示）</summary>
    public bool ShowCountdown => IsRunning;

    /// <summary>是否顯示門板未關閉警告（未運行且門板開啟時）</summary>
    public bool ShowDoorWarning => !IsRunning && IsDoorOpen;

    // ═══════════════════════════════════════
    // Commands
    // ═══════════════════════════════════════

    public ICommand StartStopCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    // ═══════════════════════════════════════
    // 事件（通知 View 執行 UI 操作）
    // ═══════════════════════════════════════

    /// <summary>倒數結束 — View 顯示完成提示</summary>
    public event EventHandler? CountdownCompleted;

    /// <summary>使用者按下 Stop — View 顯示確認對話框（倒數繼續）</summary>
    public event EventHandler? StopRequested;

    /// <summary>門板開啟 — View 顯示錯誤 Overlay</summary>
    public event EventHandler? DoorInterrupted;

    /// <summary>門板關閉 — View 隱藏錯誤 Overlay</summary>
    public event EventHandler? DoorResumed;

    /// <summary>門板未關閉時嘗試啟動 — View 顯示警告對話框</summary>
    public event EventHandler? StartBlockedByDoor;

    // ═══════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════

    /// <summary>從 DB 載入時間選項</summary>
    public async Task InitializeAsync()
    {
        // 第一道防護：進入頁面時主動詢問硬體狀態，確保 UI 正確被 Disable
        IsDoorOpen = _hardwareService.IsDoorOpen;

        TimeOptions = await _configService.GetEnabledOptionsAsync();
        Console.WriteLine($"[UV-VM] 載入 {TimeOptions.Count} 筆時間選項");

        await ResetToDefaultAsync();
    }

    /// <summary>重置為 DB 的預設時間</summary>
    public async Task ResetToDefaultAsync()
    {
        // 找預設選項的索引
        var defaultOption = await _configService.GetDefaultOptionAsync();
        if (defaultOption != null)
        {
            var idx = TimeOptions.FindIndex(o => o.Id == defaultOption.Id);
            SelectedIndex = idx >= 0 ? idx : 0;
            Console.WriteLine($"[UV-VM] 預設選項 Id={defaultOption.Id}, Index={SelectedIndex}");
        }
        else
        {
            Console.WriteLine("[UV-VM] 無預設選項");
        }

        // 設定初始顯示秒數
        if (SelectedOption != null)
        {
            RemainingSeconds = SelectedOption.DurationSeconds;
            Console.WriteLine($"[UV-VM] 初始秒數={RemainingSeconds}, 顯示={SelectedDisplayLabel}");
        }

        // 強制通知 UI 更新（防止首次 index=0 與預設值相同而跳過）
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(SelectedDisplayLabel));
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(HasNext));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    // ═══════════════════════════════════════
    // 方向鍵切換
    // ═══════════════════════════════════════

    private void SelectPrevious()
    {
        if (HasPrevious)
        {
            SelectedIndex--;
            RemainingSeconds = SelectedOption?.DurationSeconds ?? 0;
            EventLogService.Instance.LogUvAction("DurationChanged",
                $"Duration={SelectedOption?.DurationSeconds}s, Label={SelectedDisplayLabel}");
        }
    }

    private void SelectNext()
    {
        if (HasNext)
        {
            SelectedIndex++;
            RemainingSeconds = SelectedOption?.DurationSeconds ?? 0;
            EventLogService.Instance.LogUvAction("DurationChanged",
                $"Duration={SelectedOption?.DurationSeconds}s, Label={SelectedDisplayLabel}");
        }
    }

    // ═══════════════════════════════════════
    // Start / Stop
    // ═══════════════════════════════════════

    private async Task ExecuteStartStopAsync(object? parameter)
    {
        if (IsRunning)
        {
            // 不直接停止，而是通知 View 彈出確認視窗（倒數繼續）
            StopRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // 第二道防護：啟動前主動向底層做最後一次同步確認
            if (!IsDoorOpen && _hardwareService.IsDoorOpen)
            {
                IsDoorOpen = true; // 同步狀態
            }

            if (IsDoorOpen)
            {
                StartBlockedByDoor?.Invoke(this, EventArgs.Empty);
                return;
            }
            await StartAsync();
        }
    }

    private async Task StartAsync()
    {
        if (SelectedOption == null) return;

        RemainingSeconds = SelectedOption.DurationSeconds;
        var success = await _hardwareService.StartUvLampAsync(SelectedOption.DurationSeconds);
        if (!success)
        {
            EventLogService.Instance?.LogError("UV", "UvViewModel",
                ErrorCodes.UvLampFailure, "UV 燈管啟動失敗");
            return;
        }

        IsRunning = true;
        _timer.Start();
        EventLogService.Instance.LogUvAction("Start",
            $"Duration={SelectedOption.DurationSeconds}s", ErrorCodes.UvStart);
    }

    private async Task StopAsync()
    {
        _timer.Stop();
        await _hardwareService.StopUvLampAsync();
        IsRunning = false;

        EventLogService.Instance.LogUvAction("Stop",
            $"RemainingSeconds={RemainingSeconds}", ErrorCodes.UvStop);

        // 重置為選中的時間
        if (SelectedOption != null)
            RemainingSeconds = SelectedOption.DurationSeconds;
    }

    /// <summary>使用者確認停止 UV — 由 View 在確認對話框按下 YES 後調用</summary>
    public async Task ConfirmStopAsync()
    {
        await StopAsync();
    }

    // ═══════════════════════════════════════
    // 倒數計時
    // ═══════════════════════════════════════

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        RemainingSeconds--;

        if (RemainingSeconds <= 0)
        {
            _timer.Stop();
            await _hardwareService.StopUvLampAsync();
            IsRunning = false;

            // 重置為選中的時間
            if (SelectedOption != null)
                RemainingSeconds = SelectedOption.DurationSeconds;

            CountdownCompleted?.Invoke(this, EventArgs.Empty);
            EventLogService.Instance.LogUvAction("Complete", null, ErrorCodes.UvComplete);
        }
    }

    // ═══════════════════════════════════════
    // 門板中斷
    // ═══════════════════════════════════════

    private void OnDoorOpened()
    {
        IsDoorOpen = true;

        if (IsRunning)
        {
            _timer.Stop(); // 暫停倒數
            DoorInterrupted?.Invoke(this, EventArgs.Empty);
            EventLogService.Instance?.LogError("UV", "UvViewModel",
                ErrorCodes.UvDoorInterrupted, "UV 門板中斷",
                $"RemainingSeconds={RemainingSeconds}");
        }
    }

    private void OnDoorClosed()
    {
        if (!IsDoorOpen) return;

        IsDoorOpen = false;

        if (IsRunning)
        {
            DoorResumed?.Invoke(this, EventArgs.Empty);
            EventLogService.Instance.LogUvAction("DoorResumed",
                $"RemainingSeconds={RemainingSeconds}");

            _timer.Start(); // 恢復倒數
        }
    }
}
