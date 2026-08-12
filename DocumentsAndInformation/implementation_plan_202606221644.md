# Excel 報告匯出功能實作計畫

**分析者 / 製作者：Office of William**

## 背景

目前 `DataExportService.ExportSingleRecordAsync` 匯出時只產生 `report.csv` 和 `runinfo.json`。需新增 **Excel (.xlsx) 報告檔案**，格式與機台原始產出的 Excel 一致。

## 範例 Excel 分析結果

從 `\\vmware-host\Shared Folders\[TRIO] 專案\機台產出的excel報告\trio_data` 分析出兩種報告格式：

### 格式一：IntelliPlex Report

```
Row 1:  [A1:B1 合併] "IntelliPlex Report"
Row 3:  Experiment Date          | 2026/01/22
Row 4:  Extraction Program       | QC Dilution Test_0.5ng/ul
Row 5:  Extraction Kit Lot. No.  | 26275878
Row 6:  Extraction Sample Volume | N/A
Row 7:  Elution Volume           | N/A
Row 8:  PCR Total Nucleic Acid Input | 10.00 ng
Row 9:  IntelliPlex Kit 1 Product Name | ...
Row 10: IntelliPlex Kit 1 Lot No.     | ...
Row 11: IntelliPlex Kit 2 Product Name | N/A
Row 12: IntelliPlex Kit 2 Lot No.     | N/A
Row 13: PCR Plate ID             | N/A
Row 14: S1 A/D Value             | 1598
Row 15: S2 A/D Value             | 19822
Row 16-19: (空白)

Row 20: [表頭 — 多欄合併]
  A20:A21 "Sample Position"
  B20:B21 "Concentration\n(ng/μL)"
  C20:C21 "Utilized Eluted\nSample(μL)"
  D20:E20 "PCR Plate Well Position"  ← 合併
  F20:F21 "Sample ID"
  G20:G21 "Elution Tube ID"
Row 21: D21="PCR Kit 1" | E21="PCR Kit 2"

Row 22+: 數據列（1~24 + NC + PC 空列）
```

### 格式二：Custom Program Report

```
Row 1:  [A1:B1 合併] "Custom Program Report"
Row 3:  Experiment Date           | 2026/01/16
Row 4:  Function Modules Selected | Quantification(DNA) + PCR Setup
Row 5:  Extraction Program        | N/A
Row 6:  Extraction Kit Lot. No.   | N/A
Row 7:  Extraction Sample Volume  | N/A
Row 8:  Elution Volume            | N/A
Row 9:  PCR Plate ID              | N/A
Row 10: Custom PCR Setup    | Rxn1 | Rxn2 | Rxn3 | Rxn4
Row 11: Control 1 Assignment| Yes  | Yes  | Yes  | N/A
Row 12: Control 2 Assignment| Yes  | Yes  | Yes  | N/A
Row 13: PCR Total Nucleic Acid Input (ng) | 10 | 10 | 10 | N/A
Row 14: PCR Sample Volume (μL)            | 20 | 20 | 20 | N/A
Row 15: PCR Master Mix Volume (μL)        | 20 | 20 | 20 | N/A
Row 16: S1 A/D Value  | N/A
Row 17: S2 A/D Value  | N/A
Row 18-21: (空白)

Row 22: [表頭 — 多欄合併]
  A22:A23 "Sample Position"
  B22:B23 "Concentration\n(ng/μL)"
  C22:C23 "Utilized Eluted\nSample(μL)"
  D22:G22 "PCR Plate Well Position"  ← 合併
  H22:H23 "Sample ID"
  I22:I23 "Elution Tube ID"
Row 23: D23="Rxn 1" | E23="Rxn 2" | F23="Rxn 3" | G23="Rxn 4"

Row 24+: 數據列（1~22 + Ctrl1 + Ctrl2 空列）
```

---

## DB → Excel 欄位映射

### Header 區域

| Excel 標籤 | TestRecord 屬性 | 適用 |
|-----------|----------------|------|
| 報告標題（Row 1） | `ReportType` → "IntelliPlex Report" 或 "Custom Program Report" | 全部 |
| Experiment Date | `ExperimentDate` | 全部 |
| Function Modules Selected | `FunctionModulesSelected` | Custom |
| Extraction Program | `ExtractionProgram` | 全部 |
| Extraction Kit Lot. No. | `ExtractionKitLotNo` | 全部 |
| Extraction Sample Volume | `ExtractionSampleVolume` | 全部 |
| Elution Volume | `ElutionVolume` | 全部 |
| PCR Plate ID | `PcrPlateId` | 全部 |
| PCR Total Nucleic Acid Input | `PcrTotalNucleicAcidInput` | IntelliPlex |
| IntelliPlex Kit 1 Product Name | `IntelliPlexKit1Name` | IntelliPlex |
| IntelliPlex Kit 1 Lot No. | `IntelliPlexKit1LotNo` | IntelliPlex |
| IntelliPlex Kit 2 Product Name | `IntelliPlexKit2Name` | IntelliPlex |
| IntelliPlex Kit 2 Lot No. | `IntelliPlexKit2LotNo` | IntelliPlex |
| Custom PCR Setup (Rxn1~4) | `CustomPcrSetupJson` (JSON 解析) | Custom |
| S1 A/D Value | `S1AdValue` | 全部 |
| S2 A/D Value | `S2AdValue` | 全部 |

### Data 區域

| Excel 欄 | SampleResult 屬性 |
|----------|-------------------|
| Sample Position | `SamplePosition` |
| Concentration | `ConcentrationDisplay` |
| Utilized Eluted Sample(μL) | `UtilizedElutedVolume` |
| PCR Kit 1 / Rxn 1 | `PcrWellKit1` |
| PCR Kit 2 / Rxn 2 | `PcrWellKit2` |
| Rxn 3 | `PcrWellRxn3` (Custom) |
| Rxn 4 | `PcrWellRxn4` (Custom) |
| Sample ID | `SampleId` |
| Elution Tube ID | `ElutionTubeId` |

---

## Proposed Changes

### NuGet 安裝

#### [MODIFY] [TRIO2026.App.csproj](file:///d:/TRIO2026/src/TRIO2026.App/TRIO2026.App.csproj)

- 新增 `ClosedXML` NuGet 套件（MIT License，無需商業授權）。

---

### DataExportService

#### [MODIFY] [DataExportService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/DataExportService.cs)

在 `ExportSingleRecordAsync` 中新增第 3 步：產生 Excel 報告。

```csharp
// 3. report.xlsx (Excel Report — 與機台產出格式一致)
var xlsxPath = Path.Combine(baseDir, $"{record.RunId}.xlsx");
ExcelReportGenerator.Generate(record, xlsxPath);
```

---

### Excel 報告產生器 (新增)

#### [NEW] [ExcelReportGenerator.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/ExcelReportGenerator.cs)

靜態工具類，負責產生與機台一致的 Excel 報告。

**主要結構：**
```csharp
public static class ExcelReportGenerator
{
    public static void Generate(TestRecord record, string outputPath)
    {
        if (record.ReportType == "Custom")
            GenerateCustomReport(record, outputPath);
        else
            GenerateIntelliPlexReport(record, outputPath);
    }
    
    private static void GenerateIntelliPlexReport(TestRecord record, string path) { ... }
    private static void GenerateCustomReport(TestRecord record, string path) { ... }
}
```

> [!IMPORTANT]
> 兩種報告的差異在於：Header 區域的欄位不同、資料表的 PCR 欄位數量不同（IntelliPlex: Kit1+Kit2=2欄 → G欄, Custom: Rxn1~4=4欄 → I欄）。

---

## 匯出後的資料夾結構

```
USB:\trio_data\{RunId}\
  ├── runinfo.json        (元資料 — 已有)
  ├── report.csv          (CSV 表格 — 已有)
  └── {RunId}.xlsx        (Excel 報告 — 新增)
```

---

## Verification Plan

### Automated Tests
1. `dotnet build` 確認 0 錯誤
2. 模擬器中匯出一筆資料，確認 USB 碟中出現 `.xlsx` 檔案

### Manual Verification
- 以 Excel / LibreOffice 開啟產生的 `.xlsx`
- 對照機台範例確認標題、合併儲存格、資料列的格式正確
