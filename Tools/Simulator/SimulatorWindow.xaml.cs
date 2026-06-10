using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

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
        RefreshUsbDisks();
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

    // ==========================================
    // USB 實體拔插模擬 (OS 層級)
    // ==========================================
    
    public class UsbDiskItem
    {
        public uint Number { get; set; } = 1;
        public string FriendlyName { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("InstanceId")]
        public string Path { get; set; } = "";
        public string DisplayName => $"{FriendlyName}";
    }

    private void BtnRefreshUsb_Click(object sender, RoutedEventArgs e)
    {
        RefreshUsbDisks();
    }

    private void RefreshUsbDisks()
    {
        try
        {
            CmbUsbDisks.Items.Clear();
            AddLog("🔄 正在掃描 USB 隨身碟...");

            var ps = new ProcessStartInfo("powershell")
            {
                Arguments = "-NoProfile -Command \"Get-PnpDevice -Class DiskDrive | Where-Object Status -eq 'OK' | Where-Object InstanceId -match '^USBSTOR' | Select-Object FriendlyName, InstanceId | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(ps);
            if (process == null) return;

            string json = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrWhiteSpace(json))
            {
                AddLog("⚠️ 找不到任何 USB 隨身碟");
                return;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ConvertTo-Json might return a single object or an array
            List<UsbDiskItem> items = new();
            if (json.StartsWith("["))
            {
                items = JsonSerializer.Deserialize<List<UsbDiskItem>>(json, options) ?? new();
            }
            else
            {
                var single = JsonSerializer.Deserialize<UsbDiskItem>(json, options);
                if (single != null) items.Add(single);
            }

            foreach (var item in items)
            {
                CmbUsbDisks.Items.Add(item);
            }

            if (CmbUsbDisks.Items.Count > 0)
                CmbUsbDisks.SelectedIndex = 0;

            AddLog($"✅ 找到 {items.Count} 台 USB 隨身碟");
        }
        catch (Exception ex)
        {
            AddLog($"❌ 掃描 USB 失敗: {ex.Message}");
        }
    }

    private async void BtnSimulateReplug_Click(object sender, RoutedEventArgs e)
    {
        if (CmbUsbDisks.SelectedItem is not UsbDiskItem selectedDisk)
        {
            MessageBox.Show("Please select a USB drive first!", "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            BtnSimulateReplug.IsEnabled = false;
            AddLog($"🔌 Simulating replug: {selectedDisk.DisplayName}");

            // 三路分流：與 App 的 format 機制一致
            bool isElevated = IsRunningAsAdmin();

            if (isElevated)
            {
                // ── 路徑 1: 已提權 → 直接執行 ──
                AddLog("▶ Route: Direct (Admin)");
                await RunPnpDirect(selectedDisk.Path);
            }
            else if (await TRIO2026.Core.IPC.PipeClient.IsServiceAvailableAsync())
            {
                // ── 路徑 2: PrivilegedService 可用 → Named Pipe ──
                AddLog("▶ Route: PrivilegedService (Pipe)");
                var response = await TRIO2026.Core.IPC.PipeClient.SendRequestAsync(
                    new TRIO2026.Core.IPC.PipeRequest
                    {
                        Command = TRIO2026.Core.IPC.PipeCommand.RestartPnp,
                        InstanceId = selectedDisk.Path,
                        CallerUser = "Simulator"
                    });

                if (response.Success)
                    AddLog($"✅ PnP restart via service: {response.Output}");
                else
                    AddLog($"❌ PnP restart failed: {response.Error}");
            }
            else
            {
                // ── 路徑 3: 開發環境 fallback → UAC ──
                AddLog("▶ Route: UAC Elevation (fallback)");
                AddLog("⚠️ UAC prompt will appear...");
                await RunPnpElevated(selectedDisk.Path);
            }

            AddLog("✅ PnP replug simulation completed.");
            AddLog("👀 Main app should have detected WMI event.");
        }
        catch (Exception ex)
        {
            AddLog($"❌ Replug failed: {ex.Message}");
        }
        finally
        {
            BtnSimulateReplug.IsEnabled = true;
        }
    }

    /// <summary>已提權時直接執行 PnP 重啟</summary>
    private async Task RunPnpDirect(string instanceId)
    {
        string script = $"-NoProfile -ExecutionPolicy Bypass -Command \"Disable-PnpDevice -InstanceId '{instanceId}' -Confirm:0; Start-Sleep -Seconds 3; Enable-PnpDevice -InstanceId '{instanceId}' -Confirm:0\"";
        var psi = new ProcessStartInfo("powershell")
        {
            Arguments = script,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        await Task.Run(() =>
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
        });
    }

    /// <summary>未提權時使用 UAC fallback</summary>
    private async Task RunPnpElevated(string instanceId)
    {
        string script = $"-NoProfile -ExecutionPolicy Bypass -Command \"Disable-PnpDevice -InstanceId '{instanceId}' -Confirm:0; Start-Sleep -Seconds 3; Enable-PnpDevice -InstanceId '{instanceId}' -Confirm:0\"";
        var psi = new ProcessStartInfo("powershell")
        {
            Arguments = script,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        await Task.Run(() =>
        {
            using var process = Process.Start(psi);
            process?.WaitForExit();
        });
    }

    /// <summary>偵測當前是否以管理員權限執行</summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
