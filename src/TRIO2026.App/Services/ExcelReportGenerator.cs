using System.IO;
using System.Text.Json;
using ClosedXML.Excel;
using TRIO2026.Core.Entities;

namespace TRIO2026.App.Services;

/// <summary>
/// Excel 報告產生器 — 依機台原始產出格式，產生 .xlsx 報告
///
/// 支援兩種報告樣板：
///   - IntelliPlex Report：A~G 欄，PCR 欄為 Kit1 + Kit2
///   - Custom Program Report：A~I 欄，PCR 欄為 Rxn1~Rxn4
///
/// 製作者: Office of William
/// </summary>
public static class ExcelReportGenerator
{
    /// <summary>SampleResult 筆數上限（防 DoS / Memory Exhaustion）</summary>
    private const int MaxSampleCount = 500;

    /// <summary>CustomPcrSetupJson 大小上限（防 JSON Bomb）</summary>
    private const int MaxJsonLength = 10_000;

    public static void Generate(TestRecord record, string outputPath)
    {
        // ── P2: DoS 防護 — SampleResult 筆數上限 ──
        if (record.SampleResults.Count > MaxSampleCount)
        {
            EventLogService.Instance?.LogWarning("Data", "ExcelReportGenerator",
                Core.ErrorCodes.DataExportFailed, "Sample count exceeds limit",
                $"RunId={record.RunId}, Count={record.SampleResults.Count}, Max={MaxSampleCount}");
            throw new InvalidOperationException(
                $"Sample count ({record.SampleResults.Count}) exceeds maximum ({MaxSampleCount})");
        }

        bool isCustom = string.Equals(record.ReportType, "Custom", StringComparison.OrdinalIgnoreCase);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");

        if (isCustom)
            BuildCustomReport(ws, record);
        else
            BuildIntelliPlexReport(ws, record);

        // 設定所有欄自動寬度
        ws.Columns().AdjustToContents();

        wb.SaveAs(outputPath);
    }

    // ──────────────────────────────────────────────────────────────
    // IntelliPlex Report（7 欄 A~G）
    // ──────────────────────────────────────────────────────────────
    private static void BuildIntelliPlexReport(IXLWorksheet ws, TestRecord record)
    {
        // Row 1: 標題
        ws.Cell("A1").Value = "IntelliPlex Report";
        ws.Range("A1:B1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        // Row 3~15: Header 參數
        SetHeaderRow(ws, 3, "Experiment Date", record.ExperimentDate);
        SetHeaderRow(ws, 4, "Extraction Program", record.ExtractionProgram);
        SetHeaderRow(ws, 5, "Extraction Kit Lot. No.", record.ExtractionKitLotNo);
        SetHeaderRow(ws, 6, "Extraction Sample Volume", record.ExtractionSampleVolume);
        SetHeaderRow(ws, 7, "Elution Volume", record.ElutionVolume);
        SetHeaderRow(ws, 8, "PCR Total Nucleic Acid Input", FormatPcrInput(record.PcrTotalNucleicAcidInput));
        SetHeaderRow(ws, 9, "IntelliPlex Kit 1 Product Name", record.IntelliPlexKit1Name);
        SetHeaderRow(ws, 10, "IntelliPlex Kit 1 Lot No.", record.IntelliPlexKit1LotNo);
        SetHeaderRow(ws, 11, "IntelliPlex Kit 2 Product Name", record.IntelliPlexKit2Name ?? "N/A");
        SetHeaderRow(ws, 12, "IntelliPlex Kit 2 Lot No.", record.IntelliPlexKit2LotNo ?? "N/A");
        SetHeaderRow(ws, 13, "PCR Plate ID", record.PcrPlateId ?? "N/A");
        SetHeaderRow(ws, 14, "S1 A/D Value", record.S1AdValue ?? "N/A");
        SetHeaderRow(ws, 15, "S2 A/D Value", record.S2AdValue ?? "N/A");

        // Row 20~21: 表頭（合併儲存格）
        //  A20:A21 = "Sample Position"
        //  B20:B21 = "Concentration\n(ng/μL)"
        //  C20:C21 = "Utilized Eluted\nSample(μL)"
        //  D20:E20 = "PCR Plate Well Position"  → D21="PCR Kit 1", E21="PCR Kit 2"
        //  F20:F21 = "Sample ID"
        //  G20:G21 = "Elution Tube ID"
        SetTableHeader(ws, 20,
            colHeaders: new[] { "Sample Position", "Concentration\n(ng/μL)", "Utilized Eluted\nSample(μL)", "PCR Plate Well Position", "Sample ID", "Elution Tube ID" },
            wellHeader: "PCR Plate Well Position",
            wellSubHeaders: new[] { "PCR Kit 1", "PCR Kit 2" },
            firstDataCol: 4,   // D
            lastWellCol: 5,    // E
            sampleIdCol: 6,    // F
            tubeIdCol: 7       // G
        );

        // Row 22+: 數據列
        var samples = record.SampleResults.OrderBy(s => s.SamplePosition).ToList();
        int dataStartRow = 22;

        foreach (var s in samples)
        {
            var row = ws.Row(dataStartRow);
            row.Cell(1).Value = FormatPosition(s);
            row.Cell(2).Value = Sanitize(s.ConcentrationDisplay);
            row.Cell(3).Value = s.UtilizedElutedVolume?.ToString("F2") ?? "0.00";
            row.Cell(4).Value = s.PcrWellKit1 ?? "N/A";
            row.Cell(5).Value = s.PcrWellKit2 ?? "N/A";
            row.Cell(6).Value = Sanitize(s.SampleId);
            row.Cell(7).Value = Sanitize(s.ElutionTubeId);
            dataStartRow++;
        }

        // NC / PC 空列
        AddControlRows(ws, dataStartRow, 7);
    }

    // ──────────────────────────────────────────────────────────────
    // Custom Program Report（9 欄 A~I）
    // ──────────────────────────────────────────────────────────────
    private static void BuildCustomReport(IXLWorksheet ws, TestRecord record)
    {
        // Row 1: 標題
        ws.Cell("A1").Value = "Custom Program Report";
        ws.Range("A1:B1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;

        // Row 3~9: 基礎 Header
        SetHeaderRow(ws, 3, "Experiment Date", record.ExperimentDate);
        SetHeaderRow(ws, 4, "Function Modules Selected", record.FunctionModulesSelected);
        SetHeaderRow(ws, 5, "Extraction Program", record.ExtractionProgram);
        SetHeaderRow(ws, 6, "Extraction Kit Lot. No.", record.ExtractionKitLotNo);
        SetHeaderRow(ws, 7, "Extraction Sample Volume", record.ExtractionSampleVolume);
        SetHeaderRow(ws, 8, "Elution Volume", record.ElutionVolume);
        SetHeaderRow(ws, 9, "PCR Plate ID", record.PcrPlateId ?? "N/A");

        // Row 10~15: CustomPcrSetup（解析 JSON）
        var pcrSetup = ParseCustomPcrSetup(record.CustomPcrSetupJson);
        SetCustomPcrSetupRows(ws, pcrSetup);

        SetHeaderRow(ws, 16, "S1 A/D Value", record.S1AdValue ?? "N/A");
        SetHeaderRow(ws, 17, "S2 A/D Value", record.S2AdValue ?? "N/A");

        // Row 22~23: 表頭（合併儲存格）
        //  A22:A23 = "Sample Position"
        //  B22:B23 = "Concentration\n(ng/μL)"
        //  C22:C23 = "Utilized Eluted\nSample(μL)"
        //  D22:G22 = "PCR Plate Well Position"  → D23="Rxn 1" ~ G23="Rxn 4"
        //  H22:H23 = "Sample ID"
        //  I22:I23 = "Elution Tube ID"
        SetTableHeader(ws, 22,
            colHeaders: new[] { "Sample Position", "Concentration\n(ng/μL)", "Utilized Eluted\nSample(μL)", "PCR Plate Well Position", "Sample ID", "Elution Tube ID" },
            wellHeader: "PCR Plate Well Position",
            wellSubHeaders: new[] { "Rxn 1", "Rxn 2", "Rxn 3", "Rxn 4" },
            firstDataCol: 4,   // D
            lastWellCol: 7,    // G
            sampleIdCol: 8,    // H
            tubeIdCol: 9       // I
        );

        // Row 24+: 數據列
        var samples = record.SampleResults.OrderBy(s => s.SamplePosition).ToList();
        int dataStartRow = 24;

        foreach (var s in samples)
        {
            var row = ws.Row(dataStartRow);
            row.Cell(1).Value = FormatPosition(s);
            row.Cell(2).Value = Sanitize(s.ConcentrationDisplay);
            row.Cell(3).Value = s.UtilizedElutedVolume?.ToString("F2") ?? "0.00";
            row.Cell(4).Value = s.PcrWellKit1 ?? "N/A";
            row.Cell(5).Value = s.PcrWellKit2 ?? "N/A";
            row.Cell(6).Value = s.PcrWellRxn3 ?? "N/A";
            row.Cell(7).Value = s.PcrWellRxn4 ?? "N/A";
            row.Cell(8).Value = Sanitize(s.SampleId);
            row.Cell(9).Value = Sanitize(s.ElutionTubeId);
            dataStartRow++;
        }

        // Ctrl1 / Ctrl2 空列
        AddControlRows(ws, dataStartRow, 9, ctrl1: "Ctrl1", ctrl2: "Ctrl2");
    }

    // ──────────────────────────────────────────────────────────────
    // 共用輔助方法
    // ──────────────────────────────────────────────────────────────

    private static void SetHeaderRow(IXLWorksheet ws, int row, string label, string? value)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = value ?? "N/A";
        ws.Cell(row, 1).Style.Font.Bold = true;
    }

    /// <summary>
    /// 設定資料表頭區（含合併儲存格）
    /// </summary>
    private static void SetTableHeader(
        IXLWorksheet ws, int headerRow,
        string[] colHeaders,
        string wellHeader,
        string[] wellSubHeaders,
        int firstDataCol,   // D=4
        int lastWellCol,    // Kit2=5 or Rxn4=7
        int sampleIdCol,
        int tubeIdCol)
    {
        int subRow = headerRow + 1;

        // A = Sample Position（合併兩行）
        ws.Cell(headerRow, 1).Value = "Sample Position";
        ws.Range(ws.Cell(headerRow, 1), ws.Cell(subRow, 1)).Merge();

        // B = Concentration
        ws.Cell(headerRow, 2).Value = "Concentration\n(ng/μL)";
        ws.Cell(headerRow, 2).Style.Alignment.WrapText = true;
        ws.Range(ws.Cell(headerRow, 2), ws.Cell(subRow, 2)).Merge();

        // C = Utilized Eluted Sample
        ws.Cell(headerRow, 3).Value = "Utilized Eluted\nSample(μL)";
        ws.Cell(headerRow, 3).Style.Alignment.WrapText = true;
        ws.Range(ws.Cell(headerRow, 3), ws.Cell(subRow, 3)).Merge();

        // D~lastWellCol = PCR Plate Well Position（合併 headerRow 的多欄）
        ws.Cell(headerRow, firstDataCol).Value = wellHeader;
        ws.Range(ws.Cell(headerRow, firstDataCol), ws.Cell(headerRow, lastWellCol)).Merge();

        // subRow 各 PCR 子欄（Rxn 1~N 或 Kit1/Kit2）
        for (int i = 0; i < wellSubHeaders.Length; i++)
            ws.Cell(subRow, firstDataCol + i).Value = wellSubHeaders[i];

        // Sample ID（合併兩行）
        ws.Cell(headerRow, sampleIdCol).Value = "Sample ID";
        ws.Range(ws.Cell(headerRow, sampleIdCol), ws.Cell(subRow, sampleIdCol)).Merge();

        // Elution Tube ID（合併兩行）
        ws.Cell(headerRow, tubeIdCol).Value = "Elution Tube ID";
        ws.Range(ws.Cell(headerRow, tubeIdCol), ws.Cell(subRow, tubeIdCol)).Merge();

        // 表頭樣式：加粗、背景淺灰
        var headerRange = ws.Range(ws.Cell(headerRow, 1), ws.Cell(subRow, tubeIdCol));
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void AddControlRows(IXLWorksheet ws, int startRow, int totalCols, string ctrl1 = "NC", string ctrl2 = "PC")
    {
        ws.Cell(startRow, 1).Value = ctrl1;
        // NC/PC 的濃度、體積欄留空（與範例一致）
        for (int col = 2; col <= totalCols; col++)
            ws.Cell(startRow, col).Value = "";

        ws.Cell(startRow + 1, 1).Value = ctrl2;
        for (int col = 2; col <= totalCols; col++)
            ws.Cell(startRow + 1, col).Value = "";
    }

    private static void SetCustomPcrSetupRows(IXLWorksheet ws, Dictionary<string, Dictionary<string, string>> setup)
    {
        // Row 10: Custom PCR Setup label + Rxn1~4 欄名
        ws.Cell(10, 1).Value = "Custom PCR Setup";
        ws.Cell(10, 1).Style.Font.Bold = true;
        var rxnLabels = new[] { "Rxn1", "Rxn2", "Rxn3", "Rxn4" };
        for (int i = 0; i < rxnLabels.Length; i++)
            ws.Cell(10, 2 + i).Value = rxnLabels[i];

        string[] pcrKeys = new[]
        {
            "Control 1 Assignment",
            "Control 2 Assignment",
            "PCR Total Nucleic Acid Input (ng)",
            "PCR Sample Volume (μL)",
            "PCR Master Mix Volume (μL)"
        };

        for (int ki = 0; ki < pcrKeys.Length; ki++)
        {
            int row = 11 + ki;
            ws.Cell(row, 1).Value = pcrKeys[ki];
            ws.Cell(row, 1).Style.Font.Bold = true;

            if (setup.TryGetValue(pcrKeys[ki], out var rxnValues))
            {
                for (int ri = 0; ri < rxnLabels.Length; ri++)
                {
                    string rxn = rxnLabels[ri];
                    ws.Cell(row, 2 + ri).Value = rxnValues.TryGetValue(rxn, out var v) ? Sanitize(v) : "N/A";
                }
            }
            else
            {
                for (int ri = 0; ri < rxnLabels.Length; ri++)
                    ws.Cell(row, 2 + ri).Value = "N/A";
            }
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ParseCustomPcrSetup(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, Dictionary<string, string>>();

        // P2: JSON 大小上限防護
        if (json.Length > MaxJsonLength)
        {
            EventLogService.Instance?.LogWarning("Data", "ExcelReportGenerator",
                Core.ErrorCodes.DataExportFailed, "CustomPcrSetupJson exceeds size limit",
                $"Length={json.Length}, Max={MaxJsonLength}");
            return new Dictionary<string, Dictionary<string, string>>();
        }

        try
        {
            var options = new JsonSerializerOptions { MaxDepth = 5 };
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, options)
                   ?? new Dictionary<string, Dictionary<string, string>>();
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, string>>();
        }
    }

    private static string FormatPosition(SampleResult s)
    {
        if (s.SamplePosition == null) return "";
        // 若是特殊位置（NC/PC 等，SamplePosition 通常 > 24 或為 null）
        return s.SamplePosition.ToString()!;
    }

    private static string FormatPcrInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input == "N/A") return "N/A";
        // 嘗試格式化為 "10.00 ng"
        if (double.TryParse(input, out var d))
            return $"{d:F2} ng";
        return Sanitize(input);
    }

    /// <summary>
    /// [P1] 消毒儲存格內容，防止 Excel 公式注入。
    /// 委託給 DataExportService.SanitizeCellValue 統一處理。
    /// </summary>
    private static string Sanitize(string? value)
        => DataExportService.SanitizeCellValue(value);
}
