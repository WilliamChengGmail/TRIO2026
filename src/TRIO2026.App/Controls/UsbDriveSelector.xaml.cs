using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TRIO2026.App.Helpers;
using TRIO2026.App.Models;
using TRIO2026.App.Services;
using TRIO2026.Core;

namespace TRIO2026.App.Controls;

/// <summary>
/// USB 多碟選擇器 Overlay
///
/// 顯示所有可移除式 USB 碟片卡片，使用者點擊選取目標碟。
/// 無碟時顯示等待提示，USB 插入由外部事件觸發 RefreshDrives()。
///
/// 製作者: Office of William
/// </summary>
public partial class UsbDriveSelector : UserControl
{
    private TaskCompletionSource<UsbDeviceInfo?>? _tcs;

    public UsbDriveSelector()
    {
        InitializeComponent();
    }

    /// <summary>顯示選擇器並等待使用者選取</summary>
    /// <returns>選取的 USB 裝置，取消回傳 null</returns>
    public Task<UsbDeviceInfo?> ShowAsync()
    {
        _tcs = new TaskCompletionSource<UsbDeviceInfo?>();
        RootOverlay.Visibility = Visibility.Visible;
        RefreshDrives();

        EventLogService.Instance?.LogInfo("UI", "UsbDriveSelector",
            ErrorCodes.GeneralInfo, "USB selector opened");

        return _tcs.Task;
    }

    /// <summary>掃描並刷新可用 USB 碟片列表</summary>
    public void RefreshDrives()
    {
        DriveList.Children.Clear();

        var removable = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .ToList();

        if (removable.Count == 0)
        {
            WaitingPanel.Visibility = Visibility.Visible;
            DriveList.Visibility = Visibility.Collapsed;
            return;
        }

        WaitingPanel.Visibility = Visibility.Collapsed;
        DriveList.Visibility = Visibility.Visible;

        foreach (var drive in removable)
        {
            var info = UsbDeviceQueryHelper.GetDeviceInfo(drive.Name.TrimEnd('\\'));
            if (info == null) continue;

            var card = BuildDriveCard(info);
            DriveList.Children.Add(card);
        }
    }

    /// <summary>外部呼叫：USB 插入時刷新</summary>
    public void OnUsbInserted()
    {
        Dispatcher.Invoke(RefreshDrives);
    }

    /// <summary>外部呼叫：USB 移除時刷新</summary>
    public void OnUsbRemoved()
    {
        Dispatcher.Invoke(RefreshDrives);
    }

    /// <summary>外部呼叫：強制關閉（如 Session Timeout）</summary>
    public void ForceClose()
    {
        RootOverlay.Visibility = Visibility.Collapsed;
        _tcs?.TrySetResult(null);
    }

    private Button BuildDriveCard(UsbDeviceInfo info)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 圖示
        var icon = new TextBlock
        {
            Text = "💾",
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        // 磁碟資訊
        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoPanel.Children.Add(new TextBlock
        {
            Text = $"{info.DriveLetter}  {info.VolumeLabel}",
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8))
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = $"{info.FileSystem} · {info.CapacityDisplay}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5))
        });
        if (!string.IsNullOrEmpty(info.DeviceModel))
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = info.DeviceModel,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C))
            });
        }
        Grid.SetColumn(infoPanel, 1);
        grid.Children.Add(infoPanel);

        // 箭頭
        var arrow = new TextBlock
        {
            Text = "▶",
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(arrow);

        var btn = new Button
        {
            Style = (Style)FindResource("UsbCardButton"),
            Content = grid,
            Tag = info
        };
        btn.Click += OnDriveSelected;
        return btn;
    }

    private void OnDriveSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is UsbDeviceInfo info)
        {
            EventLogService.Instance?.LogInfo("UI", "UsbDriveSelector",
                ErrorCodes.GeneralInfo, "USB drive selected", info.ToLogString());

            RootOverlay.Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(info);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogInfo("UI", "UsbDriveSelector",
            ErrorCodes.GeneralInfo, "USB selector cancelled");

        RootOverlay.Visibility = Visibility.Collapsed;
        _tcs?.TrySetResult(null);
    }
}
