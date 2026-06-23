using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.App.Models;
using TRIO2026.Core;
using TRIO2026.Core.Entities;
using TRIO2026.Data.Contexts;

namespace TRIO2026.App.Services;

/// <summary>
/// 數據匯出服務 — 將 TestRecord + SampleResults 匯出至 USB
///
/// 匯出格式：
///   USB:\trio_data\{RunId}\
///     ├── report.csv        (SampleResult 表格)
///     └── runinfo.json      (TestRecord 元資料)
///
/// 設計考量：
///   - 每筆寫入前檢查 USB 路徑是否存在（防拔除）
///   - 寫入中 try-catch IOException
///   - 進度回報以筆數為單位
///   - IdleTimer 在匯出期間暫停
///
/// 製作者: Office of William
/// </summary>
public class DataExportService
{
    private readonly IServiceProvider _serviceProvider;

    public DataExportService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>匯出進度回報</summary>
    public event Action<ExportProgress>? ProgressChanged;

    /// <summary>
    /// 匯出多筆 TestRecord 至 USB
    /// </summary>
    /// <param name="recordIds">要匯出的 TestRecord Id 清單</param>
    /// <param name="targetDrive">目標 USB 碟（如 E:\）</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>匯出結果</returns>
    /// <summary>批次匯出上限（防 DoS）</summary>
    private const int MaxBatchSize = 1000;

    public async Task<ExportResult> ExportAsync(
        IReadOnlyList<int> recordIds,
        UsbDeviceInfo targetDrive,
        CancellationToken cancellationToken = default)
    {
        // ── P2: DoS 防護 — 批次匯出筆數上限 ──
        if (recordIds.Count > MaxBatchSize)
        {
            EventLogService.Instance?.LogWarning("Data", "DataExportService",
                ErrorCodes.DataExportFailed, "Export batch size exceeded",
                $"Count={recordIds.Count}, Max={MaxBatchSize}");
            throw new ArgumentException(
                $"Export batch size ({recordIds.Count}) exceeds maximum ({MaxBatchSize})");
        }

        var result = new ExportResult
        {
            TotalCount = recordIds.Count,
            TargetPath = Path.Combine(targetDrive.DriveLetter, "trio_data")
        };

        EventLogService.Instance?.LogInfo("Data", "DataExportService",
            ErrorCodes.DataExportStarted, "Export started",
            $"Count={recordIds.Count}, Target={targetDrive.DriveLetter}");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataDbContext>();

            for (int i = 0; i < recordIds.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    EventLogService.Instance?.LogInfo("Data", "DataExportService",
                        ErrorCodes.DataExportCancelled, "Export cancelled by user",
                        $"Completed={result.CompletedCount}/{result.TotalCount}");
                    break;
                }

                var recordId = recordIds[i];

                // 每筆寫入前：檢查 USB 路徑是否存在
                if (!Directory.Exists(targetDrive.DriveLetter))
                {
                    result.ErrorMessage = "USB_REMOVED";
                    result.InterruptedRunId = result.LastRunId;
                    EventLogService.Instance?.LogWarning("Data", "DataExportService",
                        ErrorCodes.DataUsbRemoved, "USB removed during export",
                        $"Completed={result.CompletedCount}/{result.TotalCount}");
                    break;
                }

                var record = await db.TestRecords
                    .Include(r => r.SampleResults)
                    .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);

                if (record == null) continue;

                // 回報進度
                ProgressChanged?.Invoke(new ExportProgress
                {
                    CurrentIndex = i + 1,
                    TotalCount = recordIds.Count,
                    CurrentRunId = record.RunId
                });

                try
                {
                    await ExportSingleRecordAsync(record, targetDrive.DriveLetter);
                    result.CompletedCount++;
                    result.LastRunId = record.RunId;
                }
                catch (IOException ex)
                {
                    // USB 拔除或空間不足
                    result.ErrorMessage = ex.Message;
                    result.InterruptedRunId = record.RunId;
                    EventLogService.Instance?.LogWarning("Data", "DataExportService",
                        ErrorCodes.DataExportFailed, "Write failed",
                        $"RunId={record.RunId}, Error={ex.Message}");
                    break;
                }
            }

            if (result.IsSuccess)
            {
                EventLogService.Instance?.LogInfo("Data", "DataExportService",
                    ErrorCodes.DataExportCompleted, "Export completed",
                    $"Count={result.CompletedCount}, Path={result.TargetPath}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.ErrorMessage = ex.Message;
            EventLogService.Instance?.LogError("Data", "DataExportService",
                ErrorCodes.DataExportFailed, "Export error", ex.Message);
        }

        return result;
    }

    /// <summary>匯出單筆 TestRecord</summary>
    private async Task ExportSingleRecordAsync(TestRecord record, string driveLetter)
    {
        // ── P0: Path Traversal 防護 ──
        var safeRunId = ValidateRunId(record.RunId);
        var baseDir = Path.Combine(driveLetter, "trio_data", safeRunId);

        // 路徑歸屬檢查：確認最終路徑落在 USB trio_data 目錄下
        var fullPath = Path.GetFullPath(baseDir);
        var expectedRoot = Path.GetFullPath(Path.Combine(driveLetter, "trio_data"));
        if (!fullPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            EventLogService.Instance?.LogError("Data", "DataExportService",
                ErrorCodes.DataExportFailed, "Path traversal detected",
                $"RunId={record.RunId}, ResolvedPath={fullPath}, ExpectedRoot={expectedRoot}");
            throw new UnauthorizedAccessException(
                $"Path traversal detected: {fullPath} is outside {expectedRoot}");
        }

        Directory.CreateDirectory(baseDir);

        // 1. runinfo.json
        var runInfo = new
        {
            record.RunId,
            record.ReportType,
            record.FlowName,
            record.ExperimentDate,
            record.SampleCount,
            record.Status,
            record.ExtractionProgram,
            record.ExtractionKitLotNo,
            record.ElutionVolume,
            record.PcrPlateId,
            record.OperatorUsername,
            record.OperatorDisplayName,
            record.DeviceSerialNo,
            record.SoftwareVersion,
            record.StartTime,
            record.EndTime,
            record.ErrorCode,
            record.ErrorMessage,
            record.Notes,
            ExportedAt = DateTime.Now.ToString("o")
        };

        var jsonPath = Path.Combine(baseDir, "runinfo.json");
        var jsonContent = JsonSerializer.Serialize(runInfo, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        await File.WriteAllTextAsync(jsonPath, jsonContent, Encoding.UTF8);

        // 2. report.csv (SampleResults)
        var csvPath = Path.Combine(baseDir, "report.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Position,Concentration,ConcentrationDisplay,Volume,ElutedVolume,WellKit1,WellKit2,SampleId,TubeId,QualityFlag");

        foreach (var s in record.SampleResults.OrderBy(s => s.SamplePosition))
        {
            // P1: CSV Injection 防護 — 對使用者可控欄位先消毒再跳脫
            sb.AppendLine(string.Join(",",
                s.SamplePosition?.ToString() ?? "",
                s.Concentration?.ToString("F4") ?? "",
                CsvEscape(SanitizeCellValue(s.ConcentrationDisplay)),
                s.Volume?.ToString("F2") ?? "",
                s.UtilizedElutedVolume?.ToString("F2") ?? "",
                CsvEscape(s.PcrWellKit1 ?? ""),
                CsvEscape(s.PcrWellKit2 ?? ""),
                CsvEscape(SanitizeCellValue(s.SampleId)),
                CsvEscape(SanitizeCellValue(s.ElutionTubeId)),
                CsvEscape(SanitizeCellValue(s.QualityFlag))
            ));
        }

        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);

        // 3. report.xlsx (Excel — 與機台產出格式一致)
        var xlsxPath = Path.Combine(baseDir, $"{record.RunId}.xlsx");
        ExcelReportGenerator.Generate(record, xlsxPath);

        // 驗證：確認檔案已建立
        if (!File.Exists(jsonPath) || !File.Exists(csvPath) || !File.Exists(xlsxPath))
        {
            throw new IOException($"File verification failed for {record.RunId}");
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// [P1] 消毒儲存格內容，防止 Excel/CSV 公式注入攻擊。
    /// 若字串以危險字元開頭（=, +, -, @, \t, \r），加上單引號前綴使 Excel 視為純文字。
    /// </summary>
    internal static string SanitizeCellValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        char first = value[0];
        if (first == '=' || first == '+' || first == '-' || first == '@' ||
            first == '\t' || first == '\r')
        {
            return "'" + value;
        }
        return value;
    }

    /// <summary>
    /// [P0] 驗證 RunId 是否為安全的檔名（只允許數字、字母、底線、連字號）。
    /// 防止 Path Traversal 攻擊。
    /// </summary>
    private static string ValidateRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("RunId cannot be empty");

        // 只允許英數字、底線、連字號（涵蓋 yyyyMMdd_HHmmss 格式）
        if (!Regex.IsMatch(runId, @"^[\w\-]+$"))
        {
            EventLogService.Instance?.LogError("Data", "DataExportService",
                ErrorCodes.DataExportFailed, "Invalid RunId characters",
                $"RunId={runId}");
            throw new ArgumentException($"RunId contains invalid characters: {runId}");
        }

        // 額外防禦：禁止路徑穿越字元
        if (runId.Contains("..") || runId.Contains('/') || runId.Contains('\\'))
        {
            EventLogService.Instance?.LogError("Data", "DataExportService",
                ErrorCodes.DataExportFailed, "RunId path traversal attempt",
                $"RunId={runId}");
            throw new ArgumentException($"RunId contains path traversal characters: {runId}");
        }

        return runId;
    }
}

/// <summary>匯出進度</summary>
public class ExportProgress
{
    public int CurrentIndex { get; set; }
    public int TotalCount { get; set; }
    public string CurrentRunId { get; set; } = "";
}

/// <summary>匯出結果</summary>
public class ExportResult
{
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public string TargetPath { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? InterruptedRunId { get; set; }
    public string? LastRunId { get; set; }
    public bool WasCancelled { get; set; }

    public bool IsSuccess => CompletedCount == TotalCount && ErrorMessage == null && !WasCancelled;
    public bool IsUsbRemoved => ErrorMessage == "USB_REMOVED";
}
