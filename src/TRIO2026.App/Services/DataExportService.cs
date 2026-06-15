using System.IO;
using System.Text;
using System.Text.Json;
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
    public async Task<ExportResult> ExportAsync(
        IReadOnlyList<int> recordIds,
        UsbDeviceInfo targetDrive,
        CancellationToken cancellationToken = default)
    {
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
        var baseDir = Path.Combine(driveLetter, "trio_data", record.RunId);
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
            sb.AppendLine(string.Join(",",
                s.SamplePosition?.ToString() ?? "",
                s.Concentration?.ToString("F4") ?? "",
                CsvEscape(s.ConcentrationDisplay ?? ""),
                s.Volume?.ToString("F2") ?? "",
                s.UtilizedElutedVolume?.ToString("F2") ?? "",
                CsvEscape(s.PcrWellKit1 ?? ""),
                CsvEscape(s.PcrWellKit2 ?? ""),
                CsvEscape(s.SampleId ?? ""),
                CsvEscape(s.ElutionTubeId ?? ""),
                CsvEscape(s.QualityFlag ?? "")
            ));
        }

        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);

        // 驗證：確認檔案已建立
        if (!File.Exists(jsonPath) || !File.Exists(csvPath))
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
