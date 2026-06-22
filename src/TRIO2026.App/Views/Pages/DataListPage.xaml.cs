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
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

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
    private OperatorFilterMode _operatorFilterMode = OperatorFilterMode.All;
    private readonly ObservableCollection<OperatorFilterItem> _operatorFilterList = new();
    private bool _operatorsLoaded = false;

    private readonly HashSet<int> _selectedIds = new();
    private List<DataRecordItem> _records = new();
    private DispatcherTimer? _longPressTimer;
    private int _longPressTargetId;

    // Table 排序
    private string _tableSortColumn = "date";
    private bool _tableSortAscending = false; // 預設降冪（最新在前）

    // ── 進階篩選：正式生效值（LoadRecords 使用）──
    private string _filterDateFrom = "";      // yyyy/MM/dd
    private string _filterDateTo = "";        // yyyy/MM/dd
    private readonly HashSet<string> _filterTypes = new() { "IntelliPlex", "Custom" }; // 全選=不過濾
    // Operator 篩選透過 _operatorFilterMode + _operatorFilterList 管理

    // ── 進階篩選：暫存草稿（點「套用」才寫入）──
    private string _draftDateFrom = "";
    private string _draftDateTo = "";
    private readonly HashSet<string> _draftTypes = new() { "IntelliPlex", "Custom" };

    // 舊版相容篩選欄位（保留供其他篩選邏輯使用）
    private string _filterReportType = "";
    private string _filterStatus = "";
    private string _filterProgram = "";


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

        // 監聽語系切換 → 重繪列表（code-behind 產生的文字不會自動更新）
        UserMenu.LanguageChanged += (s, e) =>
        {
            UpdateSelectButtonText();
            UpdateScopeDisplay();
            RenderList();
        };

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
            if (_sessionService.CurrentRole == RoleLevel.Admin)
            {
                if (_operatorFilterMode == OperatorFilterMode.My)
                {
                    var userId = _sessionService.CurrentUser?.Id ?? 0;
                    query = query.Where(r => r.OperatorUserId == userId);
                }
                else if (_operatorFilterMode == OperatorFilterMode.Custom)
                {
                    var selectedIds = _operatorFilterList.Where(x => x.IsSelected).Select(x => x.UserId).ToList();
                    query = query.Where(r => r.OperatorUserId.HasValue && selectedIds.Contains(r.OperatorUserId.Value));
                }
                // All 模式不加篩選（含 NULL）
            }
            else
            {
                var userId = _sessionService.CurrentUser?.Id ?? 0;
                query = query.Where(r => r.OperatorUserId == userId);
            }

            // 舊有篩選條件（保留相容性）
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

            // ── 進階篩選（In-memory，確保 SQLite 字串相容）──
            // 日期範圍：ExperimentDate 格式為 yyyy/MM/dd，字串字典序等同時間序
            if (!string.IsNullOrEmpty(_filterDateFrom))
                _records = _records.Where(r =>
                    !string.IsNullOrEmpty(r.ExperimentDate) &&
                    string.Compare(r.ExperimentDate, _filterDateFrom, StringComparison.Ordinal) >= 0).ToList();

            if (!string.IsNullOrEmpty(_filterDateTo))
                _records = _records.Where(r =>
                    !string.IsNullOrEmpty(r.ExperimentDate) &&
                    string.Compare(r.ExperimentDate, _filterDateTo, StringComparison.Ordinal) <= 0).ToList();

            // Type 多選（若兩種都選 = 不過濾）
            if (_filterTypes.Count < 2)
                _records = _records.Where(r => _filterTypes.Contains(r.ReportType)).ToList();

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

        // Table 模式需先排序
        if (_currentLayout == "table") ApplyTableSort();

        for (int i = 0; i < _records.Count; i++)
        {
            var r = _records[i];
            switch (_currentLayout)
            {
                case "compact":
                    ListContainer.Children.Add(BuildCompactItem(r, isAdmin));
                    break;
                case "table":
                    if (i == 0) ListContainer.Children.Add(BuildTableHeader(isAdmin));
                    ListContainer.Children.Add(BuildTableRow(r, isAdmin, i % 2 == 1));
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 樣本數（含右間距，支援 4 位數）
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 狀態
        if (showOperator)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // 操作員（18 字元）

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
            FontSize = 19, Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dateTxt, 1);
        grid.Children.Add(dateTxt);

        // 樣本數
        var smpTxt = new TextBlock
        {
            Text = $"{r.SampleCount}smp",
            FontSize = 18, Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
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
            FontSize = 18, Foreground = statusColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statTxt, 3);
        grid.Children.Add(statTxt);

        // 操作員
        if (showOperator)
        {
            var opTxt = new TextBlock
            {
                Text = Truncate(r.OperatorUsername, 18),
                FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
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

    // ═══════════════════════════════════════
    // Table 模式
    // ═══════════════════════════════════════

    /// <summary>Table 模式 — 表頭列（可點擊排序）</summary>
    private UIElement BuildTableHeader(bool showOperator)
    {
        var loc = LocalizationService.Instance;
        var grid = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x17, 0x2A)) };

        // 欄位定義
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 日期
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 類型
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 樣本
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 狀態
        if (showOperator)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 操作員

        var headers = new (string key, string col, int idx)[]
        {
            (loc["Data.HeaderDate"],     "date",     0),
            (loc["Data.HeaderType"],     "type",     1),
            (loc["Data.HeaderSamples"],  "samples",  2),
            (loc["Data.HeaderStatus"],   "status",   3),
        };

        foreach (var (text, col, idx) in headers)
        {
            var sortIndicator = _tableSortColumn == col
                ? (_tableSortAscending ? " ▲" : " ▼")
                : "";
            var btn = new Button
            {
                Content = text + sortIndicator,
                Tag = col,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = _tableSortColumn == col
                    ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                    : new SolidColorBrush(Color.FromRgb(0x8B, 0x9D, 0xBF)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 10, 4, 10),
            };
            btn.Click += OnTableHeaderClick;
            Grid.SetColumn(btn, idx);
            grid.Children.Add(btn);
        }

        if (showOperator)
        {
            var sortIndicator = _tableSortColumn == "operator"
                ? (_tableSortAscending ? " ▲" : " ▼")
                : "";
            var opBtn = new Button
            {
                Content = loc["Data.HeaderOperator"] + sortIndicator,
                Tag = "operator",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = _tableSortColumn == "operator"
                    ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                    : new SolidColorBrush(Color.FromRgb(0x8B, 0x9D, 0xBF)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 10, 4, 10),
            };
            opBtn.Click += OnTableHeaderClick;
            Grid.SetColumn(opBtn, 4);
            grid.Children.Add(opBtn);
        }

        // 底部分隔線
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Child = grid
        };
        return border;
    }

    /// <summary>Table 模式 — 資料列</summary>
    private UIElement BuildTableRow(DataRecordItem r, bool showOperator, bool isAltRow)
    {
        var loc = LocalizationService.Instance;
        var bgColor = isAltRow
            ? Color.FromRgb(0x16, 0x22, 0x3A)
            : Color.FromRgb(0x1E, 0x2D, 0x4A);

        var grid = new Grid();

        // 欄位定義（與表頭一致）
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 日期
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 類型
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 樣本
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 狀態
        if (showOperator)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 操作員

        // 日期
        var dateTxt = new TextBlock
        {
            Text = FormatDateShort(r.ExperimentDate),
            FontSize = 16, Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0)
        };
        Grid.SetColumn(dateTxt, 0);
        grid.Children.Add(dateTxt);

        // 類型（色碼 + 縮寫）
        var typeSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var dot = new Border
        {
            Width = 8, Height = 8, CornerRadius = new CornerRadius(4),
            Background = r.ReportType == "IntelliPlex"
                ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0xA7, 0x26)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        typeSp.Children.Add(dot);
        typeSp.Children.Add(new TextBlock
        {
            Text = r.ReportType == "IntelliPlex" ? "IPlex" : "QPlex",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x9D, 0xBF)),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(typeSp, 1);
        grid.Children.Add(typeSp);

        // 樣本數
        var smpTxt = new TextBlock
        {
            Text = $"{r.SampleCount}",
            FontSize = 16, Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
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
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(statTxt, 3);
        grid.Children.Add(statTxt);

        // 操作員
        if (showOperator)
        {
            var opTxt = new TextBlock
            {
                Text = Truncate(r.OperatorUsername, 14),
                FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(opTxt, 4);
            grid.Children.Add(opTxt);
        }

        if (_isSelectMode)
        {
            return BuildSelectableWrapper(r.Id, grid);
        }

        var btn = new Button
        {
            Content = grid,
            Tag = r.Id,
            Cursor = Cursors.Hand,
            Template = CreateTableRowTemplate(bgColor),
        };
        btn.Click += OnRecordClick;
        btn.PreviewMouseLeftButtonDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewMouseLeftButtonUp += (s, e) => CancelLongPress();
        btn.PreviewTouchDown += (s, e) => StartLongPress(r.Id);
        btn.PreviewTouchUp += (s, e) => CancelLongPress();
        return btn;
    }

    /// <summary>Table 行的 ControlTemplate（背景 + 按壓效果）</summary>
    private static ControlTemplate CreateTableRowTemplate(Color bgColor)
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(bgColor));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x1A, 0x27, 0x42)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        border.SetValue(Border.PaddingProperty, new Thickness(0, 10, 0, 10));
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        template.VisualTree = border;

        // 按壓效果
        var trigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x2A, 0x45, 0x70)), "border"));
        // 需要命名才能在 Trigger 中引用
        border.Name = "border";
        template.VisualTree = border;
        template.Triggers.Add(trigger);

        return template;
    }

    /// <summary>Table 表頭點擊排序</summary>
    private void OnTableHeaderClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string col)
        {
            if (_tableSortColumn == col)
                _tableSortAscending = !_tableSortAscending;
            else
            {
                _tableSortColumn = col;
                _tableSortAscending = col == "date" ? false : true; // 日期預設降冪
            }
            RenderList();
        }
    }

    /// <summary>套用 Table 排序到 _records</summary>
    private void ApplyTableSort()
    {
        _records = _tableSortColumn switch
        {
            "date" => _tableSortAscending
                ? _records.OrderBy(r => r.ExperimentDate).ToList()
                : _records.OrderByDescending(r => r.ExperimentDate).ToList(),
            "type" => _tableSortAscending
                ? _records.OrderBy(r => r.ReportType).ToList()
                : _records.OrderByDescending(r => r.ReportType).ToList(),
            "samples" => _tableSortAscending
                ? _records.OrderBy(r => r.SampleCount).ToList()
                : _records.OrderByDescending(r => r.SampleCount).ToList(),
            "status" => _tableSortAscending
                ? _records.OrderBy(r => r.Status).ToList()
                : _records.OrderByDescending(r => r.Status).ToList(),
            "operator" => _tableSortAscending
                ? _records.OrderBy(r => r.OperatorUsername).ToList()
                : _records.OrderByDescending(r => r.OperatorUsername).ToList(),
            _ => _records
        };
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

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        EventLogService.Instance?.LogButtonClick("DataListPage", "Filter");
        OpenFilterPanel();
    }

    /// <summary>開啟進階篩選 Bottom Sheet，並將正式篩選值複製到暫存草稿</summary>
    private void OpenFilterPanel()
    {
        if (_sessionService.CurrentRole == RoleLevel.Admin)
        {
            LoadOperatorFilterList();
            OperatorFilterSection.Visibility = Visibility.Visible;
            FilterPanelOperatorList.ItemsSource = _operatorFilterList;
        }
        else
        {
            OperatorFilterSection.Visibility = Visibility.Collapsed;
        }

        // 將正式篩選值複製到草稿
        _draftDateFrom = _filterDateFrom;
        _draftDateTo = _filterDateTo;
        _draftTypes.Clear();
        foreach (var t in _filterTypes) _draftTypes.Add(t);

        // 同步 DatePicker UI
        DpFilterFrom.SelectedDate = string.IsNullOrEmpty(_draftDateFrom) ? null
            : DateTime.TryParse(_draftDateFrom.Replace('/', '-'), out var d1) ? d1 : (DateTime?)null;
        DpFilterTo.SelectedDate = string.IsNullOrEmpty(_draftDateTo) ? null
            : DateTime.TryParse(_draftDateTo.Replace('/', '-'), out var d2) ? d2 : (DateTime?)null;

        UpdateTypeToggleUI();

        FilterOverlayMask.Visibility = Visibility.Visible;
        FilterSheetPanel.Visibility = Visibility.Visible;
        FilterSheetTransform.Y = 0;
    }

    private void CloseFilterPanel()
    {
        FilterSheetPanel.Visibility = Visibility.Collapsed;
        FilterOverlayMask.Visibility = Visibility.Collapsed;
        FilterSheetTransform.Y = 600;
    }

    private void OnFilterClose(object sender, RoutedEventArgs e) => CloseFilterPanel();

    private void OnFilterOverlayMaskClick(object sender, MouseButtonEventArgs e) => CloseFilterPanel();

    private void OnFilterApply(object sender, RoutedEventArgs e)
    {
        _filterDateFrom = _draftDateFrom;
        _filterDateTo = _draftDateTo;
        _filterTypes.Clear();
        foreach (var t in _draftTypes) _filterTypes.Add(t);

        CloseFilterPanel();
        UpdateFilterButtonState();
        UpdateAdvancedFilterIndicator();
        LoadRecords();

        EventLogService.Instance?.LogInfo("UI", "DataListPage", ErrorCodes.GeneralInfo,
            "Advanced filter applied",
            $"From={_filterDateFrom}, To={_filterDateTo}, Types={string.Join(",", _filterTypes)}");
    }

    private void OnFilterReset(object sender, RoutedEventArgs e)
    {
        _draftDateFrom = _filterDateFrom = "";
        _draftDateTo = _filterDateTo = "";
        _draftTypes.Clear(); _draftTypes.Add("IntelliPlex"); _draftTypes.Add("Custom");
        _filterTypes.Clear(); _filterTypes.Add("IntelliPlex"); _filterTypes.Add("Custom");

        DpFilterFrom.SelectedDate = null;
        DpFilterTo.SelectedDate = null;
        UpdateTypeToggleUI();

        CloseFilterPanel();
        UpdateFilterButtonState();
        UpdateAdvancedFilterIndicator();
        LoadRecords();
    }

    private void OnClearAdvancedFilterClick(object sender, RoutedEventArgs e)
    {
        _filterDateFrom = _filterDateTo = "";
        _filterTypes.Clear(); _filterTypes.Add("IntelliPlex"); _filterTypes.Add("Custom");
        UpdateFilterButtonState();
        UpdateAdvancedFilterIndicator();
        LoadRecords();
    }

    // ── 日期快速 Chip ──
    private void OnChipToday(object sender, RoutedEventArgs e) => SetDraftDateRange(0);
    private void OnChip7D(object sender, RoutedEventArgs e) => SetDraftDateRange(7);
    private void OnChip30D(object sender, RoutedEventArgs e) => SetDraftDateRange(30);
    private void OnChip3M(object sender, RoutedEventArgs e) => SetDraftDateRange(90);

    private void SetDraftDateRange(int pastDays)
    {
        var to = DateTime.Today;
        var from = pastDays == 0 ? to : to.AddDays(-pastDays);
        _draftDateFrom = from.ToString("yyyy/MM/dd");
        _draftDateTo = to.ToString("yyyy/MM/dd");
        DpFilterFrom.SelectedDate = from;
        DpFilterTo.SelectedDate = to;
    }

    private void OnFilterDateChanged(object sender, SelectionChangedEventArgs e)
    {
        _draftDateFrom = DpFilterFrom.SelectedDate?.ToString("yyyy/MM/dd") ?? "";
        _draftDateTo = DpFilterTo.SelectedDate?.ToString("yyyy/MM/dd") ?? "";
    }

    // ── Type Toggle ──
    private void OnTypeIPlexClick(object sender, RoutedEventArgs e)
    {
        if (_draftTypes.Contains("IntelliPlex") && _draftTypes.Count == 1) return;
        if (_draftTypes.Contains("IntelliPlex")) _draftTypes.Remove("IntelliPlex");
        else _draftTypes.Add("IntelliPlex");
        UpdateTypeToggleUI();
    }

    private void OnTypeQPlexClick(object sender, RoutedEventArgs e)
    {
        if (_draftTypes.Contains("Custom") && _draftTypes.Count == 1) return;
        if (_draftTypes.Contains("Custom")) _draftTypes.Remove("Custom");
        else _draftTypes.Add("Custom");
        UpdateTypeToggleUI();
    }

    private void UpdateTypeToggleUI()
    {
        bool iPlexOn = _draftTypes.Contains("IntelliPlex");
        bool qPlexOn = _draftTypes.Contains("Custom");

        BtnTypeIPlex.Background = new SolidColorBrush(iPlexOn
            ? Color.FromRgb(0x1A, 0x48, 0x7A) : Color.FromRgb(0x1E, 0x2D, 0x4A));
        BtnTypeIPlex.BorderBrush = new SolidColorBrush(iPlexOn
            ? Color.FromRgb(0x42, 0xA5, 0xF5) : Color.FromRgb(0x2A, 0x3D, 0x5E));
        BtnTypeIPlex.Foreground = new SolidColorBrush(iPlexOn
            ? Color.FromRgb(0xF0, 0xF4, 0xF8) : Color.FromRgb(0xB0, 0xBE, 0xC5));

        BtnTypeQPlex.Background = new SolidColorBrush(qPlexOn
            ? Color.FromRgb(0x1A, 0x48, 0x7A) : Color.FromRgb(0x1E, 0x2D, 0x4A));
        BtnTypeQPlex.BorderBrush = new SolidColorBrush(qPlexOn
            ? Color.FromRgb(0x42, 0xA5, 0xF5) : Color.FromRgb(0x2A, 0x3D, 0x5E));
        BtnTypeQPlex.Foreground = new SolidColorBrush(qPlexOn
            ? Color.FromRgb(0xF0, 0xF4, 0xF8) : Color.FromRgb(0xB0, 0xBE, 0xC5));
    }

    private void OnFilterPanelOperatorClick(object sender, RoutedEventArgs e)
    {
        _operatorFilterMode = OperatorFilterMode.Custom;
        UpdateScopeDisplay();
    }

    private bool HasAdvancedFilter =>
        !string.IsNullOrEmpty(_filterDateFrom) ||
        !string.IsNullOrEmpty(_filterDateTo) ||
        _filterTypes.Count < 2;

    private void UpdateFilterButtonState()
    {
        BtnFilter.Foreground = HasAdvancedFilter
            ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5))
            : new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5));
    }

    private void UpdateAdvancedFilterIndicator()
    {
        if (_sessionService.CurrentRole != RoleLevel.Admin) return;
        var loc = LocalizationService.Instance;
        bool active = HasAdvancedFilter;
        AdvancedFilterIndicator.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (active)
            TxtAdvancedFilterLabel.Text = loc["Data.FilterActive"] ?? "Advanced Active";
    }

    private void UpdateScopeDisplay()
    {
        var loc = LocalizationService.Instance;
        TxtScopeLabel.Text = $"{loc["Data.HeaderOperator"] ?? "Operator"}:";

        if (_operatorFilterMode == OperatorFilterMode.All)
            BtnScopeToggle.Content = $"▼ {loc["Data.FilterAll"]}";
        else if (_operatorFilterMode == OperatorFilterMode.My)
            BtnScopeToggle.Content = $"▼ {loc["Data.FilterMy"]}";
        else
        {
            var selectedCount = _operatorFilterList.Count(x => x.IsSelected);
            BtnScopeToggle.Content = $"▼ {string.Format(loc["Data.FilterSelectedCount"] ?? "{0} Selected", selectedCount)}";
        }

        UpdateAdvancedFilterIndicator();
    }

    // ── 頂部 Quick Bar Operator 相關 ──

    private void LoadOperatorFilterList()
    {
        if (_operatorsLoaded) return;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataDbContext>();

            var operators = db.TestRecords
                .Where(r => r.OperatorUserId.HasValue)
                .Select(r => new { Id = r.OperatorUserId!.Value, Name = r.OperatorUsername })
                .Distinct().ToList();

            var currentUserId = _sessionService.CurrentUser?.Id ?? 0;
            var currentUserName = _sessionService.CurrentUser?.Username ?? "";

            if (currentUserId > 0 && !operators.Any(x => x.Id == currentUserId))
                operators.Add(new { Id = currentUserId, Name = (string?)currentUserName });

            var sorted = operators.OrderBy(x => x.Name).ToList();
            foreach (var op in sorted)
                _operatorFilterList.Add(new OperatorFilterItem
                { UserId = op.Id, Username = op.Name ?? "Unknown", IsSelected = true });

            OperatorFilterItemsControl.ItemsSource = _operatorFilterList;
            _operatorsLoaded = true;
        }
        catch (Exception ex)
        {
            EventLogService.Instance?.LogError("UI", "DataListPage",
                ErrorCodes.DatabaseConnectionFailure, "Failed to load operators", ex.Message);
        }
    }

    private void OnScopeToggle(object sender, RoutedEventArgs e)
    {
        LoadOperatorFilterList();
        if (_operatorFilterMode == OperatorFilterMode.All)
            foreach (var item in _operatorFilterList) item.IsSelected = true;
        else if (_operatorFilterMode == OperatorFilterMode.My)
        {
            var myId = _sessionService.CurrentUser?.Id ?? 0;
            foreach (var item in _operatorFilterList)
                item.IsSelected = (item.UserId == myId);
        }
        OperatorFilterPopup.IsOpen = true;
    }

    private void OnFilterMyRecordsClick(object sender, RoutedEventArgs e)
    {
        _operatorFilterMode = OperatorFilterMode.My;
        OperatorFilterPopup.IsOpen = false;
        UpdateScopeDisplay();
        LoadRecords();
    }

    private void OnFilterAllRecordsClick(object sender, RoutedEventArgs e)
    {
        _operatorFilterMode = OperatorFilterMode.All;
        OperatorFilterPopup.IsOpen = false;
        UpdateScopeDisplay();
        LoadRecords();
    }

    private void OnOperatorCheckboxClick(object sender, RoutedEventArgs e)
    {
        _operatorFilterMode = OperatorFilterMode.Custom;
        UpdateScopeDisplay();
        LoadRecords();
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

        // ── Step 3：Cybersecurity 讀取背景掃描 ──
        // 條件：usb_read_background_check=1 AND usb_cybersecurity_enabled=1
        var usbSecurity = _serviceProvider.GetService(typeof(IUsbSecurityService)) as IUsbSecurityService;
        if (_systemSettings.UsbReadBackgroundCheck && _systemSettings.UsbCybersecurityEnabled)
        {
            StatusText.Text = loc["Data.UsbPreparing"];
            if (usbSecurity != null)
            {
                var scanPassed = await usbSecurity.ScanDeviceContentAsync(selectedDrive);
                if (!scanPassed)
                {
                    EventLogService.Instance?.LogWarning("Data", "DataListPage",
                        ErrorCodes.DataCyberBlocked, "USB read background check failed",
                        selectedDrive.ToLogString());

                    await _dialogOverlay.ShowAsync(
                        loc["Data.CyberBlocked"],
                        loc["Data.UsbReadBlocked"] ?? "Security check failed. Read aborted.",
                        loc["Common.OK"],
                        OverlayDialogIcon.Error);

                    StatusText.Text = loc["Common.Ready"];
                    return;   // ← 中止，不繼續讀取/匯出
                }
            }
        }

        // ── Step 4：格式化判斷 ──
        // 規則 A：usb_format_before_write=1 AND usb_cybersecurity_enabled=1 → 有內容才跳出提示，空碟跳過並紀錄
        // 規則 B：usb_format_before_write=0 → 不跳出提示，直接進行下載
        try
        {
            var usbRoot = selectedDrive.DriveLetter;
            bool doFormat = false;

            bool forceFormatPolicy = _systemSettings.UsbFormatBeforeWrite && _systemSettings.UsbCybersecurityEnabled;

            if (forceFormatPolicy && Directory.Exists(usbRoot))
            {
                var hasContent = Directory.GetFiles(usbRoot, "*", SearchOption.AllDirectories).Length > 0;
                if (hasContent)
                {
                    EventLogService.Instance?.LogInfo("Data", "DataListPage",
                        ErrorCodes.GeneralInfo, "Format prompt shown (USB has content)");

                    // 強制格式化確認（安全政策）- 三按鈕垂直排列
                    var formatConfirmResult = await _dialogOverlay.ShowTripleAsync(
                        loc["Data.DetailDownload"],
                        loc["Data.FormatRequired"] ?? "Security policy requires formatting before write. Proceed?",
                        loc["Data.CancelFormat"] ?? "Skip Formatting",
                        loc["Data.ConfirmFormat"] ?? "Format",
                        loc["Data.CancelDownload"] ?? "Cancel Download");

                    // 傳回值：0=主要(取消格式化), 1=中間(確認格式化), 2=取消(取消下載)
                    if (formatConfirmResult == 2)
                    {
                        EventLogService.Instance?.LogInfo("Data", "DataListPage",
                            ErrorCodes.GeneralInfo, "Format declined and export aborted by user");
                        StatusText.Text = loc["Common.Ready"];
                        return;
                    }
                    else if (formatConfirmResult == 1)
                    {
                        EventLogService.Instance?.LogInfo("Data", "DataListPage",
                            ErrorCodes.GeneralInfo, "Format confirmed by user");
                        doFormat = true;
                    }
                    else // formatConfirmResult == 0
                    {
                        EventLogService.Instance?.LogInfo("Data", "DataListPage",
                            ErrorCodes.GeneralInfo, "Format skipped by user, continue export");
                        // doFormat 維持 false
                    }
                }
                else
                {
                    // 隨身碟是空碟, 則不需跳出format視窗, 直接進行下載 (視窗沒有出現但Log 要紀錄)
                    EventLogService.Instance?.LogInfo("Data", "DataListPage",
                        ErrorCodes.DataFormatSkipped, "USB empty, format skipped");
                }
            }

            if (doFormat && usbSecurity != null)
            {
                StatusText.Text = loc["Data.UsbPreparing"];
                var (formatOk, formatOutput) = await usbSecurity.FormatDriveAsync(selectedDrive);
                if (formatOk)
                {
                    EventLogService.Instance?.LogInfo("Data", "DataListPage",
                        ErrorCodes.UsbFormatSuccess, "USB formatted before export", formatOutput);
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

public class OperatorFilterItem : INotifyPropertyChanged
{
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum OperatorFilterMode
{
    All,
    My,
    Custom
}
