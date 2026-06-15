using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.App.Views;
using TRIO2026.Core;
using TRIO2026.Core.Entities;
using TRIO2026.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// DataDetailPage — 數據紀錄詳情頁
///
/// 整頁垂直滾動，包含摺疊式 Section：
///   1. 實驗設定（程式名稱、Kit 批號、洗脫體積等）
///   2. 樣本結果（表格）
///   3. 操作紀錄（操作員、RunId、裝置序號）
///
/// 製作者: Office of William
/// </summary>
public partial class DataDetailPage : UserControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OverlayDialog _dialogOverlay;
    private readonly int _recordId;
    private TestRecord? _record;

    // Section 摺疊狀態
    private bool _settingExpanded = true;
    private bool _resultsExpanded = true;
    private bool _auditExpanded = true;

    // Section 面板參考
    private StackPanel? _settingPanel;
    private StackPanel? _resultsPanel;
    private StackPanel? _auditPanel;

    public DataDetailPage(int recordId, IServiceProvider serviceProvider,
        OverlayDialog dialogOverlay)
    {
        InitializeComponent();
        _recordId = recordId;
        _serviceProvider = serviceProvider;
        _dialogOverlay = dialogOverlay;

        var loc = LocalizationService.Instance;
        BtnDownloadSingle.Content = $"⬇  {loc["Data.DetailDownload"]}";

        LoadDetail();

        EventLogService.Instance?.LogInfo("UI", "DataDetailPage",
            ErrorCodes.DataRecordView, "Detail opened", $"RecordId={recordId}");
    }

    // ═══════════════════════════════════════
    // 資料載入
    // ═══════════════════════════════════════

    private void LoadDetail()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataDbContext>();

            _record = db.TestRecords
                .Include(r => r.SampleResults)
                .FirstOrDefault(r => r.Id == _recordId);

            if (_record == null)
            {
                TxtRunId.Text = "Record not found";
                return;
            }

            TxtRunId.Text = _record.RunId;

            // 狀態列
            var loc = LocalizationService.Instance;
            var statusKey = _record.Status switch
            {
                "Error" => loc["Data.StatusError"],
                "Aborted" => loc["Data.StatusAborted"],
                _ => loc["Data.StatusCompleted"]
            };
            TxtStatus.Text = $"{statusKey} · {_record.SampleCount ?? 0} {loc["Data.Samples"]}";

            RenderSections();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataDetailPage] LoadDetail error: {ex.Message}");
            EventLogService.Instance?.LogError("Data", "DataDetailPage",
                ErrorCodes.DataLoadError, "Load detail failed", ex.Message);
        }
    }

    // ═══════════════════════════════════════
    // 渲染 Sections
    // ═══════════════════════════════════════

    private void RenderSections()
    {
        if (_record == null) return;
        ContentPanel.Children.Clear();
        var loc = LocalizationService.Instance;

        // Section 1: 實驗設定
        _settingPanel = new StackPanel();
        BuildSettingSection(_settingPanel);
        _settingPanel.Visibility = _settingExpanded ? Visibility.Visible : Visibility.Collapsed;
        ContentPanel.Children.Add(BuildSectionHeader(
            $"📋  {loc["Data.DetailSetting"]}", "setting"));
        ContentPanel.Children.Add(_settingPanel);

        // Section 2: 樣本結果
        _resultsPanel = new StackPanel();
        BuildResultsSection(_resultsPanel);
        _resultsPanel.Visibility = _resultsExpanded ? Visibility.Visible : Visibility.Collapsed;
        ContentPanel.Children.Add(BuildSectionHeader(
            $"🧪  {loc["Data.DetailResults"]}", "results"));
        ContentPanel.Children.Add(_resultsPanel);

        // Section 3: 操作紀錄
        _auditPanel = new StackPanel();
        BuildAuditSection(_auditPanel);
        _auditPanel.Visibility = _auditExpanded ? Visibility.Visible : Visibility.Collapsed;
        ContentPanel.Children.Add(BuildSectionHeader(
            $"📝  {loc["Data.DetailAudit"]}", "audit"));
        ContentPanel.Children.Add(_auditPanel);
    }

    private Button BuildSectionHeader(string text, string sectionKey)
    {
        var arrow = sectionKey switch
        {
            "setting" => _settingExpanded ? "▼" : "▶",
            "results" => _resultsExpanded ? "▼" : "▶",
            "audit" => _auditExpanded ? "▼" : "▶",
            _ => "▶"
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleTb = new TextBlock
        {
            Text = text,
            FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleTb, 0);
        grid.Children.Add(titleTb);

        var arrowTb = new TextBlock
        {
            Text = arrow,
            FontSize = 18,
            Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrowTb, 1);
        grid.Children.Add(arrowTb);

        var btn = new Button
        {
            Style = (Style)FindResource("SectionHeaderButton"),
            Content = grid,
            Tag = sectionKey
        };
        btn.Click += OnSectionToggle;
        return btn;
    }

    private void OnSectionToggle(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string key) return;

        switch (key)
        {
            case "setting":
                _settingExpanded = !_settingExpanded;
                if (_settingPanel != null)
                    _settingPanel.Visibility = _settingExpanded ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "results":
                _resultsExpanded = !_resultsExpanded;
                if (_resultsPanel != null)
                    _resultsPanel.Visibility = _resultsExpanded ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "audit":
                _auditExpanded = !_auditExpanded;
                if (_auditPanel != null)
                    _auditPanel.Visibility = _auditExpanded ? Visibility.Visible : Visibility.Collapsed;
                break;
        }

        // 重繪 section headers（更新箭頭）
        RenderSections();
    }

    // ═══════════════════════════════════════
    // Section 1: 實驗設定
    // ═══════════════════════════════════════

    private void BuildSettingSection(StackPanel panel)
    {
        if (_record == null) return;

        AddField(panel, "Report Type", _record.ReportType);
        AddField(panel, "Flow Name", _record.FlowName);
        AddField(panel, "Experiment Date", _record.ExperimentDate);
        AddField(panel, "Extraction Program", _record.ExtractionProgram);
        AddField(panel, "Extraction Kit Lot#", _record.ExtractionKitLotNo);
        AddField(panel, "Sample Volume", _record.ExtractionSampleVolume);
        AddField(panel, "Elution Volume", _record.ElutionVolume);
        AddField(panel, "PCR Plate ID", _record.PcrPlateId);

        if (_record.ReportType == "IntelliPlex")
        {
            AddField(panel, "Kit 1", $"{_record.IntelliPlexKit1Name} ({_record.IntelliPlexKit1LotNo})");
            AddField(panel, "Kit 2", $"{_record.IntelliPlexKit2Name} ({_record.IntelliPlexKit2LotNo})");
            AddField(panel, "Nucleic Acid Input", _record.PcrTotalNucleicAcidInput);
        }

        AddField(panel, "S1 A/D", _record.S1AdValue);
        AddField(panel, "S2 A/D", _record.S2AdValue);
    }

    // ═══════════════════════════════════════
    // Section 2: 樣本結果
    // ═══════════════════════════════════════

    private void BuildResultsSection(StackPanel panel)
    {
        if (_record == null) return;

        var samples = _record.SampleResults.OrderBy(s => s.SamplePosition).ToList();
        if (samples.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No sample data",
                FontSize = 16,
                Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(16, 8, 0, 8)
            });
            return;
        }

        // 表頭
        var headerGrid = BuildResultRow("Pos", "Conc", "Vol", "Well", "Sample ID", isHeader: true);
        panel.Children.Add(headerGrid);

        // 資料列
        foreach (var s in samples)
        {
            var row = BuildResultRow(
                s.SamplePosition?.ToString() ?? "—",
                s.ConcentrationDisplay ?? s.Concentration?.ToString("F2") ?? "—",
                s.Volume?.ToString("F1") ?? "—",
                s.PcrWellKit1 ?? "—",
                s.SampleId ?? "—",
                isHeader: false
            );
            panel.Children.Add(row);
        }
    }

    private Border BuildResultRow(string pos, string conc, string vol,
        string well, string sampleId, bool isHeader)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fg = isHeader
            ? (SolidColorBrush)FindResource("TextSecondaryBrush")
            : (SolidColorBrush)FindResource("TextPrimaryBrush");
        var fw = isHeader ? FontWeights.Bold : FontWeights.Normal;
        var fs = isHeader ? 13.0 : 14.0;

        string[] values = { pos, conc, vol, well, sampleId };
        for (int i = 0; i < values.Length; i++)
        {
            var tb = new TextBlock
            {
                Text = values[i],
                FontSize = fs, FontWeight = fw,
                Foreground = fg,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }

        return new Border
        {
            Child = grid,
            Padding = new Thickness(16, 8, 8, 8),
            BorderBrush = (SolidColorBrush)FindResource("DividerBrush"),
            BorderThickness = new Thickness(0, 0, 0, isHeader ? 1 : 0),
            Background = isHeader
                ? new SolidColorBrush(Color.FromArgb(0x30, 0x1A, 0x2D, 0x4C))
                : Brushes.Transparent
        };
    }

    // ═══════════════════════════════════════
    // Section 3: 操作紀錄
    // ═══════════════════════════════════════

    private void BuildAuditSection(StackPanel panel)
    {
        if (_record == null) return;

        AddField(panel, "Run ID", _record.RunId);
        AddField(panel, "Operator", $"{_record.OperatorDisplayName} ({_record.OperatorUsername})");
        AddField(panel, "Start Time", FormatDateTime(_record.StartTime));
        AddField(panel, "End Time", FormatDateTime(_record.EndTime));
        AddField(panel, "Device S/N", _record.DeviceSerialNo);
        AddField(panel, "Software Ver", _record.SoftwareVersion);

        if (!string.IsNullOrEmpty(_record.ErrorCode))
        {
            AddField(panel, "Error Code", _record.ErrorCode);
            AddField(panel, "Error Message", _record.ErrorMessage);
        }

        if (!string.IsNullOrEmpty(_record.Notes))
            AddField(panel, "Notes", _record.Notes);
    }

    // ═══════════════════════════════════════
    // 共用 UI 工具
    // ═══════════════════════════════════════

    private void AddField(StackPanel panel, string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var grid = new Grid { Margin = new Thickness(16, 6, 16, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelTb = new TextBlock
        {
            Text = label,
            FontSize = 15,
            Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var valueTb = new TextBlock
        {
            Text = value,
            FontSize = 15, FontWeight = FontWeights.SemiBold,
            Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueTb, 1);
        grid.Children.Add(valueTb);

        panel.Children.Add(grid);
    }

    private static string FormatDateTime(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return "—";
        return DateTime.TryParse(iso, out var dt) ? dt.ToString("yyyy/MM/dd HH:mm:ss") : iso;
    }

    // ═══════════════════════════════════════
    // 事件
    // ═══════════════════════════════════════

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogButtonClick("DataDetailPage", "Back");
        // 通知父頁面返回清單
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_record == null) return;

        EventLogService.Instance?.LogButtonClick("DataDetailPage", "DownloadSingle");

        var loc = LocalizationService.Instance;
        var shell = Window.GetWindow(this) as AppShell;
        if (shell == null) return;

        // Step 1：選擇 USB 碟
        var selectedDrive = await shell.UsbDriveSelector.ShowAsync();
        if (selectedDrive == null)
        {
            EventLogService.Instance?.LogInfo("UI", "DataDetailPage",
                ErrorCodes.DataExportCancelled, "USB selection cancelled");
            return;
        }

        // Step 2：Cybersecurity 掃描
        var usbSecurity = _serviceProvider.GetService(typeof(IUsbSecurityService)) as IUsbSecurityService;
        if (usbSecurity != null)
        {
            var scanPassed = await usbSecurity.ScanDeviceContentAsync(selectedDrive);
            if (!scanPassed)
            {
                EventLogService.Instance?.LogWarning("Data", "DataDetailPage",
                    ErrorCodes.DataCyberBlocked, "Scan failed", selectedDrive.ToLogString());
                await _dialogOverlay.ShowAsync(
                    loc["Data.CyberBlocked"], loc["Data.CyberBlocked"],
                    loc["Common.OK"], OverlayDialogIcon.Error);
                return;
            }
        }

        // Step 3：格式化判斷
        try
        {
            if (System.IO.Directory.Exists(selectedDrive.DriveLetter))
            {
                var hasContent = System.IO.Directory.GetFiles(
                    selectedDrive.DriveLetter, "*", System.IO.SearchOption.AllDirectories).Length > 0;
                if (hasContent)
                {
                    var skipFormat = await _dialogOverlay.ShowConfirmAsync(
                        loc["Data.DetailDownload"], loc["Data.FormatConfirm"],
                        loc["Common.Cancel"], loc["Common.Confirm"]);

                    if (!skipFormat && usbSecurity != null)
                    {
                        TxtStatus.Text = loc["Data.UsbPreparing"];
                        var (fmtOk, fmtOut) = await usbSecurity.FormatDriveAsync(selectedDrive);
                        if (!fmtOk)
                        {
                            await _dialogOverlay.ShowAsync(
                                loc["Data.DownloadFail"], $"USB Format Failed: {fmtOut}",
                                loc["Common.OK"], OverlayDialogIcon.Error);
                            return;
                        }
                    }
                }
            }
        }
        catch { /* 非致命，繼續 */ }

        // Step 4：暫停 IdleTimer + 匯出
        shell.PauseIdleTimer();
        try
        {
            var exportService = new DataExportService(_serviceProvider);
            TxtStatus.Text = loc["Data.Exporting"];

            var result = await Task.Run(() =>
                exportService.ExportAsync(new[] { _recordId }, selectedDrive));

            shell.ResumeIdleTimer();

            if (result.IsSuccess)
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.DownloadDone"],
                    string.Format(loc["Data.DownloadDoneMsg"], 1, result.TargetPath),
                    loc["Common.OK"], OverlayDialogIcon.Success);
            }
            else if (result.IsUsbRemoved)
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.UsbRemoved"], loc["Data.UsbRemoved"],
                    loc["Common.OK"], OverlayDialogIcon.Warning);
            }
            else
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.DownloadFail"], result.ErrorMessage ?? "Unknown error",
                    loc["Common.OK"], OverlayDialogIcon.Error);
            }
        }
        catch (Exception ex)
        {
            shell.ResumeIdleTimer();
            EventLogService.Instance?.LogError("Data", "DataDetailPage",
                ErrorCodes.DataExportFailed, "Export exception", ex.Message);
            await _dialogOverlay.ShowAsync(
                loc["Data.DownloadFail"], ex.Message,
                loc["Common.OK"], OverlayDialogIcon.Error);
        }

        var loc2 = LocalizationService.Instance;
        TxtStatus.Text = $"{_record.Status} · {_record.SampleCount ?? 0} {loc2["Data.Samples"]}";
    }

    /// <summary>返回清單頁請求</summary>
    public event EventHandler? BackRequested;
}
