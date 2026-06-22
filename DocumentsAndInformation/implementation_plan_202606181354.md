# DataListPage 放大鏡篩選面板實作計劃

## 目標

為 Admin DataList Page 的 🔍 按鈕實作完整的篩選面板，涵蓋三個維度：
日期範圍、資料類型（Type）、操作員（Operator，僅 Admin），
可多重組合篩選，並針對 7 吋直式觸控面板優化操作體驗。

---

## 現狀分析

### 已存在的基礎

| 項目 | 現狀 |
|------|------|
| `BtnFilter` (🔍) | 存在，Click → `OnFilterClick`，目前顯示「開發中」 |
| `_filterReportType` | 單值字串欄位，需升級為 HashSet |
| Operator 篩選 | 已有 `OperatorFilterItem`、`_operatorFilterList`，但在 Admin Bar Popup |
| Admin Filter Bar | 頂部獨立 Popup，未與放大鏡整合 |
| `ExperimentDate` 欄位 | `string?`，格式 `yyyy/MM/dd`（字串字典序 ≡ 時間序） |
| ReportType 值 | `IntelliPlex`（顯示 IPlex）和 `Custom`（顯示 QPlex） |
| 多語系字串 | `FilterDateRange`, `FilterApply`, `FilterReset` 已存在 |

### 需要新增的多語系字串

```
Data.FilterTitle      → Filter / 篩選 / 筛选 / フィルタ
Data.FilterDateFrom   → Start Date / 開始日期 / 开始日期 / 開始日
Data.FilterDateTo     → End Date / 結束日期 / 结束日期 / 終了日
Data.FilterType       → Type / 資料類型 / 资料类型 / タイプ
Data.FilterTypeIPlex  → IPlex / IPlex / IPlex / IPlex
Data.FilterTypeQPlex  → QPlex / QPlex / QPlex / QPlex
Data.FilterDateToday  → Today / 今日 / 今日 / 今日
Data.FilterDate7D     → 7 Days / 近 7 日 / 近 7 日 / 7日
Data.FilterDate30D    → 30 Days / 近 30 日 / 近 30 日 / 30日
Data.FilterDate3M     → 3 Months / 近 3 月 / 近 3 月 / 3ヶ月
Data.FilterDateCustom → Custom Range / 自訂範圍 / 自定范围 / カスタム
Data.FilterOperatorAll → All Operators / 全部操作員 / 全部操作员 / すべて
```

---

## UI 設計（7 吋直式面板）

### 版面示意

```
┌─────────────────────────┐
│  Data Records  [▦][✓][🔍●]  │  ← 🔍 有篩選時顯示亮藍色
├─────────────────────────┤
│  Operator: ▼ All Records │  ← Admin Quick Bar（保留）
├─────────────────────────┤
│                         │
│      [紀錄清單]          │
│                         │
└─────────────────────────┘

點 🔍 後，Bottom Sheet 從底部滑上（高度 72%）：

┌─────────────────────────┐
│ ▓▓▓▓▓▓▓▓ 半透明遮罩 ▓▓▓  │  ← 點擊關閉
├─────────────────────────┤
│  篩選條件        [✕]    │  ← 標題列 h=52
├─────────────────────────┤
│ 📅 日期範圍              │
│ [今日][7日][30日][3月]  │  ← Chip h=48, 4等寬
│ ─── 或自訂範圍 ───      │
│ 開始 [DatePicker]       │  ← h=48
│ 結束 [DatePicker]       │
├─────────────────────────┤
│ 🏷 資料類型              │
│ [✓ IPlex  ] [  QPlex  ]│  ← Toggle h=52, 各50%寬
├─────────────────────────┤
│ 👤 操作員  (Admin only) │
│ ┌──────────────────┐   │
│ │ ☑ Alice          │   │  ← ScrollViewer max 4.5行
│ │ ☑ Bob            │   │  ← 每行 h=56
│ │ ☐ Charlie        │   │
│ └──────────────────┘   │
├─────────────────────────┤
│ [   重設 (Reset)   ]   │  ← h=52 各50%
│ [      套用       ]   │
└─────────────────────────┘
```

### 觸控設計規格

| 元素 | 高度 | 說明 |
|------|------|------|
| 快速 Chip | 48px | 圓角 12px，等寬排列 |
| DatePicker | 48px | 完整行寬，觸控友善 |
| Type Toggle | 52px | 兩欄等寬，選中有亮邊框 |
| Operator CheckBox 行 | 56px | 字型 18px，左對齊 |
| Reset / Apply 按鈕 | 52px | 各 50% 寬 |
| 最小字型 | 16px | 所有標籤與按鈕 |
| 標題字型 | 18px | 區段標題 |

---

## 篩選邏輯設計

### 新增狀態欄位

```csharp
// ── 篩選面板暫存（確認後才套用）──
private string _draftDateFrom = "";
private string _draftDateTo = "";
private readonly HashSet<string> _draftTypes = new() { "IntelliPlex", "Custom" };
private readonly HashSet<int> _draftOperatorIds = new(); // 空 = 全部

// ── 正式篩選值（LoadRecords 使用）──
private string _filterDateFrom = "";
private string _filterDateTo = "";
private readonly HashSet<string> _filterTypes = new() { "IntelliPlex", "Custom" };
private readonly HashSet<int> _filterOperatorIds = new(); // 空 = 全部（Admin）
```

### LoadRecords 新增篩選步驟

日期比較在 DB Query 取出後（in-memory）執行，確保 SQLite 相容性：

```csharp
// 1. DB Query（含 Operator 權限篩選）→ .ToList() → _records

// 2. In-memory 日期篩選（yyyy/MM/dd 字串字典序 = 時間序）
if (!string.IsNullOrEmpty(_filterDateFrom))
    _records = _records.Where(r =>
        !string.IsNullOrEmpty(r.ExperimentDate) &&
        string.Compare(r.ExperimentDate, _filterDateFrom) >= 0).ToList();

if (!string.IsNullOrEmpty(_filterDateTo))
    _records = _records.Where(r =>
        !string.IsNullOrEmpty(r.ExperimentDate) &&
        string.Compare(r.ExperimentDate, _filterDateTo) <= 0).ToList();

// 3. Type 多選（兩種都選 = 不過濾）
if (_filterTypes.Count < 2)
    _records = _records.Where(r => _filterTypes.Contains(r.ReportType)).ToList();

// 4. Operator 多選（空集 = 不過濾，已在 DB Query 層處理）
```

### 🔍 按鈕視覺狀態

```csharp
private bool HasActiveFilter =>
    !string.IsNullOrEmpty(_filterDateFrom) ||
    !string.IsNullOrEmpty(_filterDateTo) ||
    _filterTypes.Count < 2 ||
    _filterOperatorIds.Count > 0;

private void UpdateFilterButtonState()
{
    BtnFilter.Foreground = HasActiveFilter
        ? new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)) // 亮藍
        : new SolidColorBrush(Color.FromRgb(0xB0, 0xBE, 0xC5)); // 預設灰
}
```

---

## 要修改的檔案

### 資料層

#### [MODIFY] [LocalizedStringSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/LocalizedStringSeed.cs)

新增 12 條多語系字串（EN / zh-TW / zh-CN / ja）。

---

### UI 層

#### [MODIFY] [DataListPage.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataListPage.xaml)

1. `<UserControl.Resources>` 新增 DatePicker 和 Type Toggle 觸控樣式
2. 新增半透明遮罩層 `FilterOverlayMask`（`Grid.RowSpan="5"`）
3. 新增 Bottom Sheet 面板 `FilterSheetPanel`（`Grid.RowSpan="5"`，含 `TranslateTransform`）

#### [MODIFY] [DataListPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataListPage.xaml.cs)

1. 新增篩選狀態欄位（draft + 正式）
2. `OnFilterClick` → 開啟 Bottom Sheet + 複製正式篩選到 draft + 同步 UI
3. `OnFilterClose` → 關閉面板（不套用）
4. `OnFilterApply` → draft 寫入正式值 → 關閉 → `LoadRecords()`
5. `OnFilterReset` → 清除所有篩選 → 套用
6. 日期快速 Chip：Today / 7D / 30D / 3M
7. Type Toggle 切換邏輯
8. 修改 `LoadRecords()` 加入新篩選邏輯
9. `UpdateFilterButtonState()` 更新 🔍 外觀
10. `_filterOperatorIds` 整合現有 Operator 篩選邏輯

---

## 驗證計劃

### 建置
```
dotnet build src/TRIO2026.App/TRIO2026.App.csproj --no-restore
```
目標：0 錯誤

### 功能測試清單

| 場景 | 預期結果 |
|------|---------|
| 點 🔍 | Bottom Sheet 從底部滑入，遮罩出現 |
| 點遮罩或 ✕ | 面板關閉，篩選不變 |
| 選「今日」Chip | From/To 自動填入今日 `yyyy/MM/dd` |
| From > To | 「套用」顯示警告，不執行 |
| 只選 IPlex | 清單只顯示 IntelliPlex 紀錄 |
| Admin 選特定操作員 | 清單只顯示該員記錄 |
| 三維組合篩選 | 取交集，數量正確 |
| 非 Admin 登入 | 篩選面板不顯示操作員區段 |
| 有篩選條件 | 🔍 顯示亮藍色 |
| 語系切換 | 面板文字正確切換 |
| 重設 | 恢復全部，🔍 恢復灰色 |

---

## 開放問題

> [!IMPORTANT]
> **Q1 - 日期輸入方式**：
> 建議使用 WPF 內建 `DatePicker`（彈出日曆）。
> 優點：觸控友善、不需切換虛擬鍵盤、格式不易錯。
> 若您希望用鍵盤輸入，可改為 `TextBox` 搭配 `TouchKeyboardOverlay`。
> **請確認採用 DatePicker 還是 TextBox？**

> [!IMPORTANT]
> **Q2 - Admin 頂部 Operator 快速列**：
> 目前頂部已有一列「Operator: ▼ All Records」Popup。
> 方案 A：**保留**，兩者共用 `_filterOperatorIds`，互相同步。
> 方案 B：**移除**，所有篩選統一由放大鏡面板管理（更簡潔）。
> **建議方案 B**（移除頂部列），避免使用者困惑兩個入口。請確認？
