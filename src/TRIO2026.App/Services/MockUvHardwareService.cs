using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using TRIO2026.Core.Interfaces;

namespace TRIO2026.App.Services;

/// <summary>
/// UV 硬體服務的模擬實作（開發/測試用）
/// 
/// 實作 TCP Client 與 Tools/TRIO2026.Simulator (TCP Server: 127.0.0.1:5020) 連線，
/// 進行雙向的狀態同步與事件觸發。
/// 若無法連線，則退回單純的 Log 模擬模式。
/// </summary>
public class MockUvHardwareService : IUvHardwareService, IDisposable
{
    public event EventHandler? DoorOpened;
    public event EventHandler? DoorClosed;

    public bool IsDoorOpen { get; private set; }

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;

    public MockUvHardwareService()
    {
        _cts = new CancellationTokenSource();
        // 在背景嘗試連接 Simulator
        _ = Task.Run(ConnectToSimulatorAsync);
    }

    private async Task ConnectToSimulatorAsync()
    {
        while (!_cts!.IsCancellationRequested)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("127.0.0.1", 5020);
                _stream = _client.GetStream();
                Debug.WriteLine("[MockUV] 已連線至 Simulator (127.0.0.1:5020)");

                // 開始接收迴圈
                using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
                while (_client.Connected && !_cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    ProcessSimulatorMessage(line);
                }
            }
            catch (Exception)
            {
                // 連線失敗或斷線，等待 3 秒後重試
            }
            finally
            {
                _stream?.Dispose();
                _client?.Close();
                _stream = null;
                _client = null;
            }

            if (!_cts.IsCancellationRequested)
            {
                await Task.Delay(3000);
            }
        }
    }

    private void ProcessSimulatorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Event", out var eventProp))
            {
                var evt = eventProp.GetString();
                if (evt == "DoorOpened")
                {
                    SimulateDoorOpen();
                }
                else if (evt == "DoorClosed")
                {
                    SimulateDoorClose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MockUV] 解析 Simulator 訊息失敗: {ex.Message}");
        }
    }

    private async Task SendCommandAsync(string command, string? extraJson = null)
    {
        if (_client == null || !_client.Connected || _stream == null)
        {
            Debug.WriteLine($"[MockUV] Simulator 未連線，退回單純模擬。命令: {command}");
            return;
        }

        try
        {
            string json;
            if (extraJson != null)
                json = $"{{\"Command\": \"{command}\", {extraJson}}}\n";
            else
                json = $"{{\"Command\": \"{command}\"}}\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await _stream.WriteAsync(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MockUV] 發送命令失敗: {ex.Message}");
        }
    }

    public async Task<bool> StartUvLampAsync(int durationSeconds)
    {
        Debug.WriteLine($"[MockUV] 請求啟動 UV 燈 (Duration={durationSeconds}s)");
        await SendCommandAsync("StartUV", $"\"Duration\": {durationSeconds}");
        return true;
    }

    public async Task<bool> StopUvLampAsync()
    {
        Debug.WriteLine("[MockUV] 請求停止 UV 燈");
        await SendCommandAsync("StopUV");
        return true;
    }

    /// <summary>模擬門板開啟（也可由 UI/開發直接呼叫）</summary>
    public void SimulateDoorOpen()
    {
        Debug.WriteLine("[MockUV] 模擬門板開啟");
        IsDoorOpen = true;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            DoorOpened?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>模擬門板關閉（也可由 UI/開發直接呼叫）</summary>
    public void SimulateDoorClose()
    {
        Debug.WriteLine("[MockUV] 模擬門板關閉");
        IsDoorOpen = false;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            DoorClosed?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _client?.Close();
        _client?.Dispose();
    }
}
