using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TRIO2026.App.Services;

/// <summary>
/// 全域閒置計時器 — 監聽 WPF 輸入事件，超時觸發鎖定/登出
/// 
/// 使用 InputManager.PreProcessInput 掛載全域輸入監聽，
/// 任何 Mouse/Touch/Keyboard 事件都會重置計時器。
/// 
/// Guest 帳號不啟動計時器。
/// UV 等長時間流程進行中可暫停計時。
/// 
/// 製作者: Office of William
/// </summary>
public class IdleTimerService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private int _timeoutSeconds;
    private int _warningSeconds;
    private int _elapsedSeconds;
    private bool _disposed;
    private bool _warningFired;

    /// <summary>計時器是否正在運行</summary>
    public bool IsRunning { get; private set; }

    /// <summary>計時器是否暫停中</summary>
    public bool IsPaused { get; private set; }

    /// <summary>剩餘秒數（供 UI 顯示倒數）</summary>
    public int RemainingSeconds => Math.Max(0, _timeoutSeconds - _elapsedSeconds);

    /// <summary>進入警告倒數時觸發</summary>
    public event EventHandler<int>? WarningTriggered;

    /// <summary>超時觸發（應鎖定或登出）</summary>
    public event EventHandler? TimeoutTriggered;

    /// <summary>使用者活動重置時觸發</summary>
    public event EventHandler? TimerReset;

    /// <summary>每秒觸發，傳遞剩餘秒數（供底部列倒數顯示）</summary>
    public event EventHandler<int>? CountdownTick;

    public IdleTimerService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>啟動閒置計時器</summary>
    /// <param name="timeoutMinutes">超時分鐘數（0=不啟動）</param>
    /// <param name="warningSeconds">倒數警告秒數</param>
    public void Start(int timeoutMinutes, int warningSeconds)
    {
        if (timeoutMinutes <= 0) return;

        _timeoutSeconds = timeoutMinutes * 60;
        _warningSeconds = warningSeconds;
        _elapsedSeconds = 0;
        _warningFired = false;
        IsRunning = true;
        IsPaused = false;

        // 掛載全域輸入監聽
        InputManager.Current.PreProcessInput += OnPreProcessInput;
        _timer.Start();
    }

    /// <summary>停止計時器（登出時呼叫）</summary>
    public void Stop()
    {
        _timer.Stop();
        IsRunning = false;
        IsPaused = false;

        // 移除全域輸入監聽
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
    }

    /// <summary>重置計時器（使用者活動時自動呼叫）</summary>
    public void Reset()
    {
        if (!IsRunning || IsPaused) return;

        _elapsedSeconds = 0;
        _warningFired = false;
        TimerReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>暫停計時（UV 進行中等長時間流程）</summary>
    public void Pause()
    {
        if (!IsRunning) return;
        IsPaused = true;
    }

    /// <summary>恢復計時（長時間流程結束後）</summary>
    public void Resume()
    {
        if (!IsRunning) return;
        IsPaused = false;
        _elapsedSeconds = 0; // 恢復後重新計時
        _warningFired = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning || IsPaused) return;

        _elapsedSeconds++;

        // 每秒通知 UI 剩餘秒數
        CountdownTick?.Invoke(this, RemainingSeconds);

        // 警告倒數
        var warningThreshold = _timeoutSeconds - _warningSeconds;
        if (!_warningFired && _warningSeconds > 0 && _elapsedSeconds >= warningThreshold)
        {
            _warningFired = true;
            WarningTriggered?.Invoke(this, RemainingSeconds);
        }

        // 超時觸發
        if (_elapsedSeconds >= _timeoutSeconds)
        {
            Stop();
            TimeoutTriggered?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>全域輸入事件監聯 — 任何使用者互動都重置計時器</summary>
    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (!IsRunning || IsPaused) return;

        var inputEvent = e.StagingItem.Input;

        // 只監聽實際的使用者互動事件
        // ⚠ 排除 MouseMoveEventArgs：UI 動畫/更新會產生內部 MouseMove，
        //    導致計時器在 UV 倒數等場景中被持續重置
        var isUserInput = inputEvent switch
        {
            MouseButtonEventArgs => true,   // 滑鼠點擊
            MouseWheelEventArgs => true,     // 滑鼠滾輪
            TouchEventArgs => true,          // 觸控
            KeyEventArgs => true,            // 鍵盤
            _ => false
        };

        if (isUserInput && _elapsedSeconds > 0)
        {
            _elapsedSeconds = 0;
            _warningFired = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
