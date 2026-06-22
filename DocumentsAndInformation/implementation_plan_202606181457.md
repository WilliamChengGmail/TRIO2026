# DataListPage 放大鏡篩選面板實作計劃 (更新版)

## 目標

為 Admin DataList Page 的 🔍 按鈕實作完整的篩選面板，涵蓋三個維度：
日期範圍、資料類型（Type）、操作員（Operator，僅 Admin），
可多重組合篩選，並特別針對 **7 吋直式觸控面板與戴手套操作** 的高便利性進行 UI/UX 優化。

---

## 決策與設計調整 (基於使用者回饋)

### Q1: 日期輸入與觸控優化 (戴手套、直式空間有限)
- **多行式佈局**：在直式螢幕上放棄左右並排，將「開始日期」與「結束日期」分為**上下兩行**排列，釋放水平空間。
- **大按鈕與下拉選單**：
  - 不使用鍵盤輸入 TextBox 以免觸控打字出錯。
  - 使用 WPF `DatePicker`，並對其彈出的 `Calendar` 進行樣式放大優化（Day Button 大小調整為 `50x50px`，字型 `18px`），方便戴手套精準點選。
  - 或者提供三個大型 `ComboBox`（年、月、日，高度 `56px`）並排，下拉清單項目高度 `56px`，極易滑動點選。
  - 本次實作將採用**自訂放大版 Calendar 觸控樣式的 DatePicker**，並採**上下雙行**配置。

### Q2: 頂部 Quick Bar 與放大鏡面板共存 (方案 A)
- **優先級與衝突處理**：
  - **進階篩選優先**：當放大鏡面板的「日期範圍」、「資料類型」或「自訂操作員」被套用時，進階篩選具有高優先權。
  - **狀態同步**：
    - 若進階篩選中操作員僅選「自己」→ 頂部 Quick Bar 同步顯示 `▼ 我的紀錄 (My Records)`。
    - 若操作員選「全部」或未限制 → 頂部 Quick Bar 同步顯示 `▼ 全部紀錄 (All Records)`。
    - 若操作員多選特定幾位 → 頂部 Quick Bar 同步顯示 `▼ 已選 X 位 (X Selected)`。
  - **視覺清晰指示**：
    - 當「日期」或「資料類型」有啟用非預設篩選時，頂部 Quick Bar 旁將顯示醒目的亮橘/藍色 `[進階篩選中 / Advanced Active]` 提示標籤與 `[✕]` 快速清除按鈕。
    - 🔍 放大鏡按鈕保持亮藍色呼吸效果或醒目背景，讓使用者一目了然。
    - 點擊頂部 Quick Bar 的 "My Records" 或 "All Records" 會**強制覆蓋**當前面板的操作員篩選，但保留日期與類型篩選（若有），並同步更新面板暫存。

---

## UI 設計 (7 吋直式面板，戴手套優化)

### 篩選面板 (Bottom Sheet) 佈局

```
┌─────────────────────────────────┐
│ ▓▓▓▓▓▓▓▓▓▓ 半透明遮罩 ▓▓▓▓▓▓▓▓▓▓ │  ← 點擊可關閉
├─────────────────────────────────┤
│  進階篩選 (Advanced Filter)  [✕] │  ← 標題列 h=56
├─────────────────────────────────┤
│ 📅 日期範圍                      │  ← 區段標題
│ [今日] [近7日] [近30日] [近3月]  │  ← Chip h=48
│                                 │
│  開始日期:                      │
│  [ yyyy / MM / dd            ▼ ] │  ← 超大 DatePicker h=56
│  結束日期:                      │
│  [ yyyy / MM / dd            ▼ ] │  ← 超大 DatePicker h=56
├─────────────────────────────────┤
│ 🏷 資料類型                      │
│ ┌───────────────┬──────────────┐ │
│ │  ☑ IPlex     │  ☑ QPlex     │ │  ← 大 Toggle 按鈕 h=60, 各50%
│ └───────────────┴──────────────┘ │
├─────────────────────────────────┤
│ 👤 操作員 (僅 Admin 顯示)        │
│ ┌──────────────────────────────┐ │
│ │ ☑ Alice (Me)                 │ │  ← CheckBox 滾動清單 h=250
│ │ ☑ Bob                        │ │  ← 每行 h=56，點擊範圍大
│ │ ☐ Charlie                    │ │
│ └──────────────────────────────┘ │
├─────────────────────────────────┤
│ ┌───────────────┬──────────────┐ │
│ │  重設 (Reset) │  套用 (Apply)│ │  ← 底部按鈕 h=56, 各50%寬
│ └───────────────┴──────────────┘ │
└─────────────────────────────────┘
```

---

## 篩選邏輯設計

### 新增狀態與同步欄位

```csharp
// 進階篩選正式值
private string _filterDateFrom = "";
private string _filterDateTo = "";
private readonly HashSet<string> _filterTypes = new() { "IntelliPlex", "Custom" };
private readonly HashSet<int> _filterOperatorIds = new(); // 空 = 全部

// 暫存值 (Draft)
private string _draftDateFrom = "";
private string _draftDateTo = "";
private readonly HashSet<string> _draftTypes = new() { "IntelliPlex", "Custom" };
private readonly HashSet<int> _draftOperatorIds = new();
```

### 頂部列 Quick Bar 同步與清除

在 XAML 中，頂部 Quick Bar 新增一個醒目的提示：
```xml
<StackPanel x:Name="AdvancedFilterIndicator" Orientation="Horizontal" Visibility="Collapsed" Margin="8,0,0,0" VerticalAlignment="Center">
    <Border Background="#FFA726" CornerRadius="4" Padding="6,4">
        <TextBlock Text="{Binding [Data.FilterActive], Source={x:Static svc:LocalizationService.Instance}}" Foreground="#121212" FontSize="14" FontWeight="Bold"/>
    </Border>
    <Button Content="✕" Style="{StaticResource ToolbarButton}" Click="OnClearAdvancedFilterClick" Foreground="#EF5350" Padding="8,4" Margin="4,0,0,0"/>
</StackPanel>
```

---

## 要修改的檔案

### 資料層
- [LocalizedStringSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/LocalizedStringSeed.cs) - 新增多語系字串。

### UI 層
- [DataListPage.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataListPage.xaml)
  - 新增大尺寸 DatePicker Calendar 觸控樣式。
  - 新增進階篩選指示燈與清除按鈕。
  - 新增 Bottom Sheet 面板與動畫。
- [DataListPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataListPage.xaml.cs)
  - 實作雙向同步邏輯 (Top Quick Bar ⇌ 放大鏡面板)。
  - 實作 DatePicker 觸控事件。
  - 實作篩選組合邏輯並重新加載紀錄。

---

## 驗證計劃

1. **建置測試**：`dotnet build` 確保無編譯錯誤。
2. **戴手套觸控模擬**：驗證 Calendar 彈出視窗與 CheckBox 大小是否易於點選。
3. **優先順序驗證**：
   - 設定日期範圍後，頂部顯示 `[進階篩選中] ✕`。
   - 點擊頂部 Quick Bar 切換為 `My Records`，驗證 Operator 是否更新，且日期範圍篩選仍維持有效。
   - 點擊 `✕` 清除進階篩選，驗證清單與 🔍 按鈕是否恢復預設狀態。
4. **多語系驗證**：切換日文/繁中/簡中/英文，確認字串翻譯無誤。
