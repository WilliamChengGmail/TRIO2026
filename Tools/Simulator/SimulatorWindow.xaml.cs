using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace TRIO2026.Simulator;

/// <summary>
/// TRIO2026 硬體模擬器主視窗
/// 
/// TCP Server (127.0.0.1:5020)，與 MockUvHardwareService (TCP Client) 雙向通訊：
///   - Simulator → App: 門板事件 (DoorOpened / DoorClosed)
///   - App → Simulator: UV 控制命令 (StartUV / StopUV)
/// 
/// 製作者: Office of William
/// </summary>
public partial class SimulatorWindow : Window
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<TcpClient> _clients = new();
    private readonly object _clientLock = new();
    private bool _isDoorOpen;
    private bool _isUvOn;
    private bool _isUvPaused;
    private DispatcherTimer? _uvTimer;
    private int _uvRemainingSeconds;

    public SimulatorWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => StartTcpServerAsync(_cts.Token));
        AddLog("Simulator 已啟動");
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts?.Cancel();

        lock (_clientLock)
        {
            foreach (var c in _clients)
            {
                try { c.Close(); } catch { }
            }
            _clients.Clear();
        }

        _listener?.Stop();
    }

    // ═══════ TCP Server ═══════

    private async Task StartTcpServerAsync(CancellationToken ct)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 5020);
            _listener.Start();
            AddLog("TCP Server 啟動 — 監聽 127.0.0.1:5020");
            UpdateConnectionStatus();

            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                lock (_clientLock) { _clients.Add(client); }

                AddLog($"✅ Client 已連線 ({_clients.Count} 個)");
                UpdateConnectionStatus();

                _ = Task.Run(() => HandleClientAsync(client, ct));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AddLog($"❌ TCP Server 錯誤: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            while (client.Connected && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                ProcessAppMessage(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AddLog($"Client 斷線: {ex.Message}");
        }
        finally
        {
            lock (_clientLock) { _clients.Remove(client); }
            try { client.Close(); } catch { }

            AddLog($"Client 已斷線 ({_clients.Count} 個)");
            UpdateConnectionStatus();
        }
    }

    /// <summary>處理 App 發來的命令</summary>
    private void ProcessAppMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Command", out var cmdProp))
            {
                var cmd = cmdProp.GetString();
                AddLog($"📥 收到命令: {cmd}");

                switch (cmd)
                {
                    case "StartUV":
                        _isUvOn = true;
                        _isUvPaused = false;
                        // 解析 Duration
                        _uvRemainingSeconds = 0;
                        if (doc.RootElement.TryGetProperty("Duration", out var durProp))
                            _uvRemainingSeconds = durProp.GetInt32();
                        AddLog($"📥 UV Duration = {_uvRemainingSeconds}s");
                        // DispatcherTimer 必須在 UI 執行緒建立
                        Dispatcher.Invoke(() => { StartUvCountdown(); UpdateUvStatus(); });
                        break;
                    case "StopUV":
                        _isUvOn = false;
                        _isUvPaused = false;
                        Dispatcher.Invoke(() => { StopUvCountdown(); UpdateUvStatus(); });
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            AddLog($"⚠ 解析訊息失敗: {ex.Message}");
        }
    }

    // ═══════ UV 倒數計時 ═══════

    private void StartUvCountdown()
    {
        StopUvCountdown();

        if (_uvRemainingSeconds <= 0) return;

        _uvTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uvTimer.Tick += UvTimer_Tick;
        _uvTimer.Start();
    }

    private void StopUvCountdown()
    {
        _uvTimer?.Stop();
        _uvTimer = null;
    }

    private void UvTimer_Tick(object? sender, EventArgs e)
    {
        if (_uvRemainingSeconds > 0)
        {
            _uvRemainingSeconds--;
            UpdateUvStatus();
        }

        if (_uvRemainingSeconds <= 0)
        {
            StopUvCountdown();
        }
    }

    /// <summary>廣播事件給所有已連線的 Client</summary>
    private async Task BroadcastEventAsync(string eventName)
    {
        var json = $"{{\"Event\": \"{eventName}\"}}\n";
        var bytes = Encoding.UTF8.GetBytes(json);

        List<TcpClient> snapshot;
        lock (_clientLock) { snapshot = new List<TcpClient>(_clients); }

        foreach (var client in snapshot)
        {
            try
            {
                if (client.Connected)
                {
                    var stream = client.GetStream();
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                AddLog($"⚠ 發送失敗: {ex.Message}");
            }
        }
    }

    // ═══════ 門板控制 ═══════

    private async void BtnDoorOpen_Click(object sender, RoutedEventArgs e)
    {
        _isDoorOpen = true;
        UpdateDoorStatus();

        // UV 照射中 → 暫停
        if (_isUvOn && !_isUvPaused)
        {
            _isUvPaused = true;
            _uvTimer?.Stop();
            UpdateUvStatus();
            AddLog("⏸ UV 照射暫停（門板開啟）");
        }

        AddLog("📤 發送事件: DoorOpened");
        await BroadcastEventAsync("DoorOpened");
    }

    private async void BtnDoorClose_Click(object sender, RoutedEventArgs e)
    {
        _isDoorOpen = false;
        UpdateDoorStatus();

        // UV 暫停中 → 恢復
        if (_isUvOn && _isUvPaused)
        {
            _isUvPaused = false;
            _uvTimer?.Start();
            UpdateUvStatus();
            AddLog("▶ UV 照射恢復（門板關閉）");
        }

        AddLog("📤 發送事件: DoorClosed");
        await BroadcastEventAsync("DoorClosed");
    }

    // ═══════ UI 更新 ═══════

    private void UpdateDoorStatus()
    {
        Dispatcher.Invoke(() =>
        {
            if (_isDoorOpen)
            {
                DoorStatusText.Text = "門板狀態：🔓 開啟";
                DoorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                BtnDoorOpen.IsEnabled = false;
                BtnDoorClose.IsEnabled = true;
            }
            else
            {
                DoorStatusText.Text = "門板狀態：🔒 關閉";
                DoorStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
                BtnDoorOpen.IsEnabled = true;
                BtnDoorClose.IsEnabled = false;
            }
        });
    }

    private void UpdateUvStatus()
    {
        Dispatcher.Invoke(() =>
        {
            if (_isUvOn)
            {
                var min = _uvRemainingSeconds / 60;
                var sec = _uvRemainingSeconds % 60;
                var timeStr = _uvRemainingSeconds > 0 ? $" — 剩餘 {min:D2}:{sec:D2}" : "";

                if (_isUvPaused)
                {
                    UvStatusText.Text = $"UV 燈：⏸ 暫停（門板開啟）{timeStr}";
                    UvStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)); // 橘色
                    UvStatusPanel.Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x2C, 0x1B));
                }
                else
                {
                    UvStatusText.Text = $"UV 燈：🟣 照射中{timeStr}";
                    UvStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0x86, 0xFC));
                    UvStatusPanel.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x1B, 0x4E));
                }
            }
            else
            {
                UvStatusText.Text = "UV 燈：⚫ 關閉";
                UvStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6));
                UvStatusPanel.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));
            }
        });
    }

    private void UpdateConnectionStatus()
    {
        Dispatcher.Invoke(() =>
        {
            int count;
            lock (_clientLock) { count = _clients.Count; }

            if (count > 0)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
                StatusText.Text = "已連線";
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                StatusText.Text = "等待連線...";
            }
            ClientCountText.Text = $"{count} client{(count != 1 ? "s" : "")}";
        });
    }

    private void AddLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            LogList.Items.Add($"[{timestamp}] {message}");

            // 保持最多 200 筆
            while (LogList.Items.Count > 200)
                LogList.Items.RemoveAt(0);

            // 自動捲到底
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        });
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogList.Items.Clear();
    }
}
