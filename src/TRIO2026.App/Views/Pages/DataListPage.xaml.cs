using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.App.Controls;
using TRIO2026.App.Services;
using TRIO2026.Core;
using TRIO2026.Core.Enums;
using TRIO2026.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace TRIO2026.App.Views.Pages;

/// <summary>
/// DataListPage — 數據紀錄清單頁
///
/// 支援三種版面：Card / Compact / Table
/// 權限篩選：Operator 僅自己 / Admin 全部
/// 多選模式：長按或選取按鈕
///
/// 製作者: Office of William
/// </summary>
public partial class DataListPage : UserControl
{
    private readonly SessionService _sessionService;
    private readonly SystemSettingService _systemSettings;
    private readonly OverlayDialog _dialogOverlay;
    private readonly IServiceProvider _serviceProvider;

    // 狀態
    private string _currentLayout = "card"; // card, compact, table
    private bool _isSelectMode;
    private bool _isAdminScopeAll = true; // Admin 預設顯示全部
    private readonly HashSet<int> _selectedIds = new();
    private List<DataRecordItem> _records = new();
    private DispatcherTimer? _longPressTimer;
    private int _longPressTargetId;

    // 篩選
    private string _filterReportType = "";
    private string _filterStatus = "";
    private string _filterProgram = "";
    private int? _filterOperatorId;

    public DataListPage(SessionService sessionService,
        OverlayDialog dialogOverlay, LoginOverlay loginOverlay,
        AuthService authService, TokenService tokenService,
        SystemSettingService systemSettings, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _systemSettings = systemSettings;
        _dialogOverlay = dialogOverlay;
        _serviceProvider = serviceProvider;

        // 初始化共用使用者選單
        UserMenu.Initialize(sessionService,
            dialogOverlay, loginOverlay, authService, tokenService, systemSettings);

        // 讀取版面配置
        _currentLayout = systemSettings.DataListLayout;

        // Admin 顯示篩選列
        if (_sessionService.CurrentRole == RoleLevel.Admin)
        {
            AdminFilterBar.Visibility = Visibility.Visible;
            UpdateScopeDisplay();
        }

        // 初始化選取按鈕文字
        UpdateSelectButtonText();

        // 載入資料
        LoadRecords();

        // 埋點
        EventLogService.Instance?.LogNavigation("menu", "data");
    }

    /// <summary>供外部呼叫刷新使用者顯示</summary>
    public void RefreshUserDisplay() => UserMenu.RefreshUserDisplay();

    // ═══════════════════════════════════════
    // 資料載入
    // ═══════════════════════════════════════

    private void LoadRecords()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataDbContext>();

            var query = db.TestRecords.AsQueryable();

            // 權限篩選
            //   Admin + ScopeAll → 看全部（含 OperatorUserId=NULL 的 legacy 資料）
            //   Admin + ScopeMine / Operator / Guest → 嚴格限自己（不含 NULL）
            if (_sessionService.CurrentRole == RoleLevel.Admin && _isAdminScopeAll)
            {
                // Admin 全域模式：不加任何篩選
            }
            else
            {
                var userId = _sessionService.CurrentUser?.Id ?? 0;
                query = query.Where(r => r.OperatorUserId == userId);
            }

            // Admin 依操作員篩選
            if (_filterOperatorId.HasValue)
                query = query.Where(r => r.OperatorUserId == _filterOperatorId.Value);

            // 篩選條件
            if (!string.IsNullOrEmpty(_filterReportType))
                query = query.Where(r => r.ReportType == _filterReportType);
            if (!string.IsNullOrEmpty(_filterStatus))
                query = query.Where(r => r.Status == _filterStatus);
            if (!string.IsNullOrEmpty(_filterProgram))
                query = query.Where(r => r.ExtractionProgram == _filterProgram);

            // 排序：日期新→舊
            query = query.OrderByDescending(r => r.EndTime);

            _records = query.Select(r => new DataRecordItem
            {
                Id = r.Id,
                RunId = r.RunId,
                ReportType = r.ReportType ?? "",
                ExperimentDate = r.ExperimentDate ?? "",
                SampleCount = r.SampleCount ?? 0,
                Status = r.Status ?? "Completed",
                ExtractionProgram = r.ExtractionProgram ?? "",
                EndTime = r.EndTime ?? "",
                OperatorUsername = r.OperatorUsername ?? "",
                ErrorCode = r.ErrorCode ?? "",
                ErrorMessage = r.ErrorMessage ?? ""
            }).ToList();

            RenderList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataListPage] LoadRecords error: {ex.Message}");
            EventLogService.Instance?.LogError("Data", "DataListPage",
                ErrorCodes.GeneralError, "Load records failed", ex.Message);
        }
    }

    // ═══════════════════════════════════════
    // 渲染
    // ═══════════════════════════════════════

    private void RenderList()
    {
        ListContainer.Children.Clear();

        if (_records.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            TxtRecordCount.Text = "0";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        TxtRecordCount.Text = $"{_records.Count}";

        var isAdmin = _sessionService.CurrentRole == RoleLevel.Admin;

        foreach (var r in _records)
        {
            switch (_currentLayout)
            {
                case "compact":
                    ListContainer.Children.Add(BuildCompactItem(r, isAdmin));
                    break;
                case "table":
                    // Table 模式先用 compact，後續擴充
                    ListContainer.Children.Add(BuildCompactItem(r, isAdmin));
                    break;
                default:
                    ListContainer.Children.Add(BuildCardItem(r, isAdmin));
                    break;
            }
        }
    }

    /// <summary>Card 模式 — 80px 高，5 個資訊</summary>
    private UIElement BuildCardItem(DataRecordItem r, bool showOperator)
    {
        var loc = LocalizationService.Instance;
        var container = new StackPanel();

        // Row 1: 類型色碼 + 日期
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var typeBadge = new Border
        {
            Background = r.ReportType == "IntelliPlex"
                ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        typeBadge.Child = new TextBlock
        {
            Text = r.ReportType,
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        Grid.SetColumn(typeBadge, 0);
        row1.Children.Add(typeBadge);

        var dateTxt = new TextBlock
        {
            Text = FormatDate(r.ExperimentDate),
            FontSize = 18, Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dateTxt, 1);
        row1.Children.Add(dateTxt);
        container.Children.Add(row1);

        // Row 2: 樣本數 + 狀態
        var statusColor = r.Status switch
        {
            "Error" => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            "Aborted" => new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)),
            _ => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A))
        };
        var statusKey = r.Status switch
        {
            "Error" => loc["Data.StatusError"],
            "Aborted" => loc["Data.StatusAborted"],
            _ => loc["Data.StatusCompleted"]
        };
        var statusIcon = r.Status == "Completed" ? "" : " ⚠";

        var row2 = new TextBlock
        {
            FontSize = 18, Margin = new Thickness(0, 4, 0, 0)
        };
        row2.Inlines.Add(new System.Windows.Documents.Run($"{r.SampleCount} {loc["Data.Samples"]} · ")
        {
            Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush")
        });
        row2.Inlines.Add(new System.Windows.Documents.Run($"{statusKey}{statusIcon}")
        {
            Foreground = statusColor, FontWeight = FontWeights.SemiBold
        });
        container.Children.Add(row2);

        // Row 3: 萃取程式 + 操作員(Admin)
        var row3 = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (showOperator)
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var progTxt = new TextBlock
        {
            Text = string.IsNullOrEmpty(r.ExtractionProgram) ? "—" : Truncate(r.ExtractionProgram, 24),
            FontSize = 17, Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush")
        };
        Grid.SetColumn(progTxt, 0);
        row3.Children.Add(progTxt);

        if (showOperator)
        {
            var opTxt = new TextBlock
            {
                Text = r.OperatorUsername,
                FontSize = 17, Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(opTxt, 1);
            row3.Children.Add(opTxt);
        }
        container.Children.Add(row3);

        // 包裝為按鈕或 checkbox 容器
        if (_isSelectMode)
        {
            return BuildSelectableWrapper(r.Id, container);
        }

        var btn = new Button { Style = (Style)FindResource("CardItemButton"), Content = container, Tag = r.Id };
        btn.Click += OnRecordClick;
        btn.PreviewMouseLeftButtonDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewMouseLeftButtonUp += (s, e) => CancelLongPress();
        btn.PreviewTouchDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewTouchUp += (s, e) => CancelLongPress();
        return btn;
    }

    /// <summary>Compact 模式 — 44px 單行</summary>
    private UIElement BuildCompactItem(DataRecordItem r, bool showOperator)
    {
        var loc = LocalizationService.Instance;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });  // 色碼
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 日期
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // 樣本數（含右間距）
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 狀態
        if (showOperator)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 操作員

        // 色碼點
        var dot = new Border
        {
            Width = 10, Height = 10, CornerRadius = new CornerRadius(5),
            Background = r.ReportType == "IntelliPlex"
                ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        // 日期
        var dateTxt = new TextBlock
        {
            Text = FormatDateShort(r.ExperimentDate),
            FontSize = 17, Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dateTxt, 1);
        grid.Children.Add(dateTxt);

        // 樣本數
        var smpTxt = new TextBlock
        {
            Text = $"{r.SampleCount}smp",
            FontSize = 16, Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(smpTxt, 2);
        grid.Children.Add(smpTxt);

        // 狀態
        var statusColor = r.Status switch
        {
            "Error" => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)),
            "Aborted" => new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)),
            _ => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A))
        };
        var statusKey = r.Status switch
        {
            "Error" => loc["Data.StatusError"],
            "Aborted" => loc["Data.StatusAborted"],
            _ => loc["Data.StatusCompleted"]
        };
        var statTxt = new TextBlock
        {
            Text = statusKey + (r.Status != "Completed" ? " ⚠" : ""),
            FontSize = 16, Foreground = statusColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statTxt, 3);
        grid.Children.Add(statTxt);

        // 操作員
        if (showOperator)
        {
            var opTxt = new TextBlock
            {
                Text = Truncate(r.OperatorUsername, 8),
                FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(opTxt, 4);
            grid.Children.Add(opTxt);
        }

        if (_isSelectMode)
        {
            return BuildSelectableWrapper(r.Id, grid);
        }

        var btn = new Button { Style = (Style)FindResource("CompactItemButton"), Content = grid, Tag = r.Id };
        btn.Click += OnRecordClick;
        btn.PreviewMouseLeftButtonDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewMouseLeftButtonUp += (s, e) => CancelLongPress();
        btn.PreviewTouchDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewTouchUp += (s, e) => CancelLongPress();
        return btn;
    }

    /// <summary>多選模式包裝：CheckBox + 內容</summary>
    private UIElement BuildSelectableWrapper(int recordId, UIElement content)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var cb = new CheckBox
        {
            Style = (Style)FindResource("SelectCheckBox"),
            IsChecked = _selectedIds.Contains(recordId),
            Tag = recordId
        };
        cb.Checked += (s, e) => { _selectedIds.Add(recordId); UpdateDownloadBar(); };
        cb.Unchecked += (s, e) => { _selectedIds.Remove(recordId); UpdateDownloadBar(); };

        sp.Children.Add(cb);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x2D, 0x4A)),
            CornerRadius = new CornerRadius(8),
            BorderBrush = _selectedIds.Contains(recordId)
                ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x3D, 0x5E)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10, 12, 10),
            Child = content
        };
        border.MouseLeftButtonUp += (s, e) => { cb.IsChecked = !cb.IsChecked; };
        border.TouchUp += (s, e) => { cb.IsChecked = !cb.IsChecked; };

        sp.Children.Add(border);
        return sp;
    }

    // ═══════════════════════════════════════
    // 工具列事件
    // ═══════════════════════════════════════

    private void OnLayoutSwitchClick(object sender, RoutedEventArgs e)
    {
        _currentLayout = _currentLayout switch
        {
            "card" => "compact",
            "compact" => "table",
            _ => "card"
        };

        _systemSettings.SetDataListLayout(_currentLayout);
        EventLogService.Instance?.LogInfo("UI", "DataListPage",
            ErrorCodes.GeneralInfo, "Layout switched", $"Layout={_currentLayout}");
        RenderList();
    }

    private void OnSelectClick(object sender, RoutedEventArgs e)
    {
        _isSelectMode = !_isSelectMode;

        if (!_isSelectMode)
        {
            _selectedIds.Clear();
        }

        UpdateSelectButtonText();
        UpdateDownloadBar();
        RenderList();

        EventLogService.Instance?.LogInfo("UI", "DataListPage",
            ErrorCodes.GeneralInfo, "Select mode toggled", $"IsSelect={_isSelectMode}");
    }

    private void UpdateSelectButtonText()
    {
        var loc = LocalizationService.Instance;
        BtnSelect.Content = _isSelectMode ? loc["Data.CancelSelect"] : loc["Data.Select"];
    }

    private async void OnFilterClick(object sender, RoutedEventArgs e)
    {
        // TODO: 顯示篩選面板 Overlay
        EventLogService.Instance?.LogButtonClick("DataListPage", "Filter");
        await _dialogOverlay.ShowAsync(
            LocalizationService.Instance["Data.FilterReportType"],
            "Filter panel under development",
            LocalizationService.Instance["Common.OK"],
            OverlayDialogIcon.Info);
    }

    private void OnScopeToggle(object sender, RoutedEventArgs e)
    {
        _isAdminScopeAll = !_isAdminScopeAll;
        UpdateScopeDisplay();
        LoadRecords();

        EventLogService.Instance?.LogInfo("UI", "DataListPage",
            ErrorCodes.GeneralInfo, "Admin scope toggled",
            $"ScopeAll={_isAdminScopeAll}");
    }

    private void UpdateScopeDisplay()
    {
        var loc = LocalizationService.Instance;
        TxtScopeLabel.Text = $"{loc["Data.FilterReportType"]}:";
        BtnScopeToggle.Content = _isAdminScopeAll
            ? $"▼ {loc["Data.FilterAll"]}"
            : $"▼ {loc["Data.FilterMy"]}";
    }

    // ═══════════════════════════════════════
    // 清單互動
    // ═══════════════════════════════════════

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int recordId)
        {
            EventLogService.Instance?.LogInfo("UI", "DataListPage",
                ErrorCodes.DataRecordView, "Record clicked", $"RecordId={recordId}");

            // 先抓住 AppShell 參考（進入 DetailPage 後 DataListPage 離開 visual tree）
            var shell = Window.GetWindow(this) as AppShell;
            if (shell == null) return;

            var pageHost = shell.FindName("PageHost") as ContentControl;
            if (pageHost == null) return;

            var listPage = this; // capture for closure

            // 建立 DataDetailPage 並導航
            var detailPage = new DataDetailPage(recordId, _serviceProvider, _dialogOverlay);
            detailPage.BackRequested += (s, args) =>
            {
                // 返回清單頁：重新載入 + 切回
                listPage.LoadRecords();
                pageHost.Content = listPage;
            };

            // 替換 PageHost 內容為 DetailPage
            pageHost.Content = detailPage;
        }
    }

    // ── 長按多選 ──
    private void StartLongPress(int recordId)
    {
        if (_isSelectMode) return;

        _longPressTargetId = recordId;
        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _longPressTimer.Tick += (s, e) =>
        {
            _longPressTimer!.Stop();
            EnterSelectMode(_longPressTargetId);
        };
        _longPressTimer.Start();
    }

    private void CancelLongPress()
    {
        _longPressTimer?.Stop();
    }

    private void EnterSelectMode(int firstSelectedId)
    {
        _isSelectMode = true;
        _selectedIds.Clear();
        _selectedIds.Add(firstSelectedId);
        UpdateSelectButtonText();
        UpdateDownloadBar();
        RenderList();

        EventLogService.Instance?.LogInfo("UI", "DataListPage",
            ErrorCodes.GeneralInfo, "Long-press select mode",
            $"FirstId={firstSelectedId}");
    }

    // ═══════════════════════════════════════
    // 下載
    // ═══════════════════════════════════════

    private void UpdateDownloadBar()
    {
        var loc = LocalizationService.Instance;
        if (_isSelectMode)
        {
            DownloadBar.Visibility = Visibility.Visible;
            BtnDownload.Content = string.Format(loc["Data.DownloadSelected"], _selectedIds.Count);
            BtnDownload.IsEnabled = _selectedIds.Count > 0;
        }
        else
        {
            DownloadBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_selectedIds.Count == 0) return;

        var loc = LocalizationService.Instance;
        var shell = Window.GetWindow(this) as AppShell;
        EventLogService.Instance?.LogButtonClick("DataListPage", "Download");

        // ── Step 1：確認下載 ──
        var confirmResult = await _dialogOverlay.ShowConfirmAsync(
            loc["Data.DetailDownload"],
            string.Format(loc["Data.DownloadConfirm"], _selectedIds.Count),
            loc["Common.Confirm"],
            loc["Common.Cancel"]);

        if (!confirmResult)
        {
            EventLogService.Instance?.LogInfo("UI", "DataListPage",
                ErrorCodes.DataExportCancelled, "Download cancelled by user");
            return;
        }

        // ── Step 2：選擇 USB 碟 ──
        if (shell == null) return;
        var selectedDrive = await shell.UsbDriveSelector.ShowAsync();

        if (selectedDrive == null)
        {
            EventLogService.Instance?.LogInfo("UI", "DataListPage",
                ErrorCodes.DataExportCancelled, "USB selection cancelled");
            return;
        }

        EventLogService.Instance?.LogInfo("Data", "DataListPage",
            ErrorCodes.GeneralInfo, "USB drive selected", selectedDrive.ToLogString());

        // ── Step 3：Cybersecurity 掃描 ──
        StatusText.Text = loc["Data.UsbPreparing"];

        var usbSecurity = _serviceProvider.GetService(typeof(IUsbSecurityService)) as IUsbSecurityService;
        if (usbSecurity != null)
        {
            var scanPassed = await usbSecurity.ScanDeviceContentAsync(selectedDrive);
            if (!scanPassed)
            {
                EventLogService.Instance?.LogWarning("Data", "DataListPage",
                    ErrorCodes.DataCyberBlocked, "Cybersecurity check failed",
                    selectedDrive.ToLogString());

                await _dialogOverlay.ShowAsync(
                    loc["Data.CyberBlocked"],
                    loc["Data.CyberBlocked"],
                    loc["Common.OK"],
                    OverlayDialogIcon.Error);
                return;
            }
        }

        // ── Step 4：格式化判斷（空碟跳過，有內容詢問） ──
        try
        {
            var usbRoot = selectedDrive.DriveLetter;
            if (Directory.Exists(usbRoot))
            {
                var hasContent = Directory.GetFiles(usbRoot, "*", SearchOption.AllDirectories).Length > 0;
                if (hasContent)
                {
                    // 按鈕順序：取消(主/預設) vs 格式化(次)
                    var skipFormat = await _dialogOverlay.ShowConfirmAsync(
                        loc["Data.DetailDownload"],
                        loc["Data.FormatConfirm"],
                        loc["Common.Cancel"],
                        loc["Common.Confirm"]);

                    if (skipFormat)
                    {
                        // 預設：不格式化，直接匯出
                        EventLogService.Instance?.LogInfo("Data", "DataListPage",
                            ErrorCodes.GeneralInfo, "Format declined, continue export");
                    }
                    else if (usbSecurity != null)
                    {
                        // 使用者選擇格式化 → 顯示進度 → 等待完成
                        StatusText.Text = loc["Data.UsbPreparing"];

                        var (formatOk, formatOutput) = await usbSecurity.FormatDriveAsync(selectedDrive);

                        if (formatOk)
                        {
                            EventLogService.Instance?.LogInfo("Data", "DataListPage",
                                ErrorCodes.UsbFormatSuccess, "USB formatted before export",
                                formatOutput);
                        }
                        else
                        {
                            EventLogService.Instance?.LogError("Data", "DataListPage",
                                ErrorCodes.UsbFormatFailed, "USB format failed", formatOutput);

                            await _dialogOverlay.ShowAsync(
                                loc["Data.DownloadFail"],
                                $"USB Format Failed: {formatOutput}",
                                loc["Common.OK"],
                                OverlayDialogIcon.Error);

                            StatusText.Text = loc["Common.Ready"];
                            return;
                        }
                    }
                }
                else
                {
                    EventLogService.Instance?.LogInfo("Data", "DataListPage",
                        ErrorCodes.DataFormatSkipped, "USB empty, format skipped");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DataListPage] Format check error: {ex.Message}");
        }

        // ── Step 5：暫停 IdleTimer + 開始匯出 ──
        shell.PauseIdleTimer();
        EventLogService.Instance?.LogInfo("Data", "DataListPage",
            ErrorCodes.GeneralInfo, "IdleTimer paused for export");

        try
        {
            var exportService = new DataExportService(_serviceProvider);
            var recordIds = _selectedIds.ToList();

            // 進度回報
            exportService.ProgressChanged += (progress) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = string.Format(loc["Data.ExportingCurrent"], progress.CurrentRunId);
                    TxtRecordCount.Text = $"{progress.CurrentIndex}/{progress.TotalCount}";
                });
            };

            // 顯示匯出中
            StatusText.Text = loc["Data.Exporting"];

            var result = await Task.Run(() =>
                exportService.ExportAsync(recordIds, selectedDrive));

            // ── Step 6：恢復 IdleTimer ──
            shell.ResumeIdleTimer();
            EventLogService.Instance?.LogInfo("Data", "DataListPage",
                ErrorCodes.GeneralInfo, "IdleTimer resumed after export");

            // ── Step 7：結果對話框 ──
            if (result.IsSuccess)
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.DownloadDone"],
                    string.Format(loc["Data.DownloadDoneMsg"],
                        result.CompletedCount, result.TargetPath),
                    loc["Common.OK"],
                    OverlayDialogIcon.Success);

                // 離開多選模式
                _isSelectMode = false;
                _selectedIds.Clear();
                UpdateSelectButtonText();
                UpdateDownloadBar();
                RenderList();
            }
            else if (result.IsUsbRemoved)
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.UsbRemoved"],
                    string.Format(loc["Data.UsbRemovedMsg"],
                        result.CompletedCount, result.TotalCount,
                        result.InterruptedRunId ?? "—"),
                    loc["Common.OK"],
                    OverlayDialogIcon.Warning);
            }
            else
            {
                await _dialogOverlay.ShowAsync(
                    loc["Data.DownloadFail"],
                    result.ErrorMessage ?? "Unknown error",
                    loc["Common.OK"],
                    OverlayDialogIcon.Error);
            }

            StatusText.Text = loc["Common.Ready"];
        }
        catch (Exception ex)
        {
            shell.ResumeIdleTimer();
            Console.WriteLine($"[DataListPage] Export error: {ex.Message}");
            EventLogService.Instance?.LogError("Data", "DataListPage",
                ErrorCodes.DataExportFailed, "Export exception", ex.Message);

            await _dialogOverlay.ShowAsync(
                loc["Data.DownloadFail"],
                ex.Message,
                loc["Common.OK"],
                OverlayDialogIcon.Error);

            StatusText.Text = loc["Common.Ready"];
        }
    }

    // ═══════════════════════════════════════
    // 工具
    // ═══════════════════════════════════════

    private static string FormatDate(string? isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";
        if (DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("yyyy/MM/dd HH:mm");
        return isoDate.Length >= 10 ? isoDate[..10] : isoDate;
    }

    private static string FormatDateShort(string? isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "—";
        if (DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MM/dd");
        return isoDate.Length >= 5 ? isoDate[5..10] : isoDate;
    }

    private static string Truncate(string s, int max)
        => s.Length > max ? s[..max] + "…" : s;
}

/// <summary>清單項目 ViewModel</summary>
internal class DataRecordItem
{
    public int Id { get; set; }
    public string RunId { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string ExperimentDate { get; set; } = "";
    public int SampleCount { get; set; }
    public string Status { get; set; } = "";
    public string ExtractionProgram { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string OperatorUsername { get; set; } = "";
    public string ErrorCode { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
