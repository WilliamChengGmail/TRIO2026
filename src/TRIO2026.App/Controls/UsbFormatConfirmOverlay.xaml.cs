using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TRIO2026.App.Models;
using TRIO2026.App.Services;

namespace TRIO2026.App.Controls;

public partial class UsbFormatConfirmOverlay : UserControl
{
    private LocalizationService? _locService;
    private UsbDeviceInfo? _deviceInfo;
    private int _delaySeconds;
    private CancellationTokenSource? _countdownCts;

    public event EventHandler<bool>? Completed;

    public UsbFormatConfirmOverlay()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public void Show(LocalizationService locService, UsbDeviceInfo deviceInfo, int delaySeconds)
    {
        _locService = locService;
        _deviceInfo = deviceInfo;
        _delaySeconds = delaySeconds;

        TitleText.Text = "⚠️ " + locService["UsbSecurity.FormatTitle"];
        DetectedText.Text = string.Format(locService["UsbSecurity.FormatDetected"], deviceInfo.DriveType);
        
        string volumeInfoStr = string.Format(locService["UsbSecurity.FormatVolumeInfo"], 
            string.IsNullOrWhiteSpace(deviceInfo.VolumeLabel) ? "(none)" : deviceInfo.VolumeLabel, 
            deviceInfo.FileSystem);

        DeviceInfoText.Text = $"Drive: {deviceInfo.DriveLetter}\n{volumeInfoStr}\nCapacity: {deviceInfo.CapacityDisplay}\nModel: {deviceInfo.DeviceModel}";
        WarningText.Text = locService["UsbSecurity.FormatWarning"];

        CancelButton.Content = locService["Common.Cancel"];
        ConfirmButton.IsEnabled = false;

        Visibility = Visibility.Visible;

        StartCountdown();
    }

    private async void StartCountdown()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = new CancellationTokenSource();

        try
        {
            for (int i = _delaySeconds; i > 0; i--)
            {
                ConfirmButton.Content = string.Format(_locService?["UsbSecurity.FormatCountdown"] ?? "{0}", i);
                await Task.Delay(1000, _countdownCts.Token);
            }

            ConfirmButton.Content = _locService?["UsbSecurity.FormatExecute"];
            ConfirmButton.IsEnabled = true;
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _countdownCts?.Cancel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = null;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        Completed?.Invoke(this, false);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        Completed?.Invoke(this, true);
    }
}
