using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TRIO2026.App.Helpers;
using TRIO2026.App.Models;
using TRIO2026.Core;
using TRIO2026.Core.IPC;

namespace TRIO2026.App.Services;

public class UsbSecurityService : IUsbSecurityService, IDisposable
{
    private readonly SystemSettingService _settings;
    private readonly SessionService _sessionService;

    private ManagementEventWatcher? _insertWatcher;
    private ManagementEventWatcher? _removeWatcher;

    private readonly ConcurrentQueue<string> _pendingDrives = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private string? _currentProcessingDrive;
    private TaskCompletionSource<bool>? _currentFormatTcs;

    private readonly ConcurrentDictionary<string, UsbDeviceInfo> _activeDevices = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<UsbDeviceInfo>? FormatRequired;

    public UsbSecurityService(SystemSettingService settings, SessionService sessionService)
    {
        _settings = settings;
        _sessionService = sessionService;
    }

    public void StartListening()
    {
        try
        {
            StopListening();

            var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_LogicalDisk' AND TargetInstance.DriveType = 2");
            _insertWatcher = new ManagementEventWatcher(insertQuery);
            _insertWatcher.EventArrived += OnDeviceInserted;
            _insertWatcher.Start();

            var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_LogicalDisk' AND TargetInstance.DriveType = 2");
            _removeWatcher = new ManagementEventWatcher(removeQuery);
            _removeWatcher.EventArrived += OnDeviceRemoved;
            _removeWatcher.Start();
        }
        catch (Exception ex)
        {
            EventLogService.Instance?.LogError("UsbSecurity", "UsbSecurityService",
                "ERR-9000", "Failed to start USB listener", ex.Message);
        }
    }

    public void StopListening()
    {
        if (_insertWatcher != null)
        {
            _insertWatcher.Stop();
            _insertWatcher.EventArrived -= OnDeviceInserted;
            _insertWatcher.Dispose();
            _insertWatcher = null;
        }
        if (_removeWatcher != null)
        {
            _removeWatcher.Stop();
            _removeWatcher.EventArrived -= OnDeviceRemoved;
            _removeWatcher.Dispose();
            _removeWatcher = null;
        }
    }

    private void OnDeviceInserted(object sender, EventArrivedEventArgs e)
    {
        var driveLetter = GetDriveLetterFromEvent(e);
        if (string.IsNullOrEmpty(driveLetter)) return;

        _pendingDrives.Enqueue(driveLetter);
        _ = ProcessQueueAsync();
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        var driveLetter = GetDriveLetterFromEvent(e);
        if (string.IsNullOrEmpty(driveLetter)) return;

        // Automatically cancel pending confirmation if the processing drive is removed
        if (string.Equals(driveLetter, _currentProcessingDrive, StringComparison.OrdinalIgnoreCase) && _currentFormatTcs != null)
        {
            _currentFormatTcs.TrySetResult(false);
        }

        if (_activeDevices.TryRemove(driveLetter, out var info))
        {
            EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                ErrorCodes.UsbDeviceRemoved, "USB Device Removed",
                $"{info.ToLogString()} | Status=Removed, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
        }
    }

    private string? GetDriveLetterFromEvent(EventArrivedEventArgs e)
    {
        try
        {
            if (e.NewEvent["TargetInstance"] is ManagementBaseObject mbo)
            {
                return mbo["DeviceID"]?.ToString()?.ToUpper();
            }
        }
        catch { }
        return null;
    }

    private async Task ProcessQueueAsync()
    {
        // Only one queue processor at a time
        if (!_processingLock.Wait(0)) return;

        try
        {
            while (_pendingDrives.TryDequeue(out var driveLetter))
            {
                _currentProcessingDrive = driveLetter;
                var info = UsbDeviceQueryHelper.GetDeviceInfo(driveLetter);
                
                if (info == null) 
                {
                    _currentProcessingDrive = null;
                    continue; // Device might have been removed before we query
                }

                _activeDevices[driveLetter] = info;

                // ── 無條件記錄 USB 插入事件（不受安全檢查影響） ──
                bool cyberEnabled = _settings.UsbCybersecurityEnabled;
                int queueCount = _pendingDrives.Count;
                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                    ErrorCodes.UsbDeviceInserted, "USB Device Inserted",
                    $"{info.ToLogString()} | CybersecurityEnabled={cyberEnabled}, QueueRemaining={queueCount}, User={_sessionService.CurrentUser?.Username ?? "(NotLoggedIn)"}");

                if (!cyberEnabled)
                {
                    _currentProcessingDrive = null;
                    continue;
                }

                // 安全性檢查：未登入時禁止所有 USB 操作
                if (!_sessionService.IsAuthenticated)
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbNotAuthenticated, "USB Blocked - Not Authenticated",
                        $"{info.ToLogString()} | Status=Blocked, Reason=NoSession, CybersecurityEnabled={cyberEnabled}");
                    _currentProcessingDrive = null;
                    continue;
                }

                // 安全性檢查：畫面鎖定時比照未登入，禁止所有 USB 操作
                if (_sessionService.IsLocked)
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbSessionLocked, "USB Blocked - Session Locked",
                        $"{info.ToLogString()} | Status=Blocked, Reason=SessionLocked, CybersecurityEnabled={cyberEnabled}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                    _currentProcessingDrive = null;
                    continue;
                }

                if (_sessionService.IsGuestLogin)
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbGuestBlocked, "USB Guest Mode Blocked",
                        $"{info.ToLogString()} | Status=Blocked, CybersecurityEnabled={cyberEnabled}, User=guest");
                    _currentProcessingDrive = null;
                    continue;
                }

                // Auto Format
                if (_settings.UsbAutoFormatOnInsert)
                {
                    if (info.DriveType != "Removable")
                    {
                        EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                            ErrorCodes.UsbFormatBlockedNonRemovable, "Format Blocked - Non Removable",
                            $"{info.ToLogString()} | Action=QuickFormat, Result=Blocked(NotRemovable), User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                    }
                    else if (IsAlreadyFormatted(info, "exFAT"))
                    {
                        // 空碟偵測：已經是目標檔案系統且無檔案，跳過格式化
                        EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                            ErrorCodes.UsbFormatSkipped, "USB Format Skipped - Already Clean",
                            $"{info.ToLogString()} | Action=QuickFormat, Result=Skipped(AlreadyClean), FileSystem={info.FileSystem}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                    }
                    else
                    {
                        // 埋點：格式化確認面板已彈出
                        EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                            ErrorCodes.UsbFormatPromptShown, "USB Format Prompt Shown",
                            $"{info.ToLogString()} | Action=FormatPrompt, Status=WaitingUserResponse, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                        _currentFormatTcs = new TaskCompletionSource<bool>();
                        FormatRequired?.Invoke(this, info);
                        
                        bool confirmed = await _currentFormatTcs.Task;
                        _currentFormatTcs = null;

                        if (confirmed)
                        {
                            string formatCmd = $"cmd.exe /c format {info.DriveLetter} /FS:exFAT /Q /Y";

                            // 埋點：使用者已確認執行格式化（含預計執行的完整指令）
                            EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                                ErrorCodes.UsbFormatUserConfirmed, "USB Format User Confirmed",
                                $"{info.ToLogString()} | Action=QuickFormat, Decision=Confirmed, TargetFS=exFAT, Cmd={formatCmd}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                            var (success, output) = await RunFormatCommandAsync(info.DriveLetter, "exFAT");
                            if (success)
                            {
                                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                                    ErrorCodes.UsbFormatSuccess, "USB Quick Format Success",
                                    $"{info.ToLogString()} | Action=QuickFormat, TargetFS=exFAT, Result=Success, Cmd={formatCmd}, CmdOutput={output}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                            }
                            else
                            {
                                EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                                    ErrorCodes.UsbFormatFailed, "USB Quick Format Failed",
                                    $"{info.ToLogString()} | Action=QuickFormat, TargetFS=exFAT, Result=Failed, Cmd={formatCmd}, CmdOutput={output}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                            }
                        }
                        else
                        {
                            // 埋點：使用者取消格式化（含裝置拔除自動取消）
                            EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                                ErrorCodes.UsbFormatCancelled, "USB Format Cancelled",
                                $"{info.ToLogString()} | Action=QuickFormat, Decision=Cancelled, TargetFS=exFAT, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                        }
                    }
                }

                // Scan
                if (_settings.UsbContentScanEnabled)
                {
                    await ScanDeviceContentAsync(info);
                }

                _currentProcessingDrive = null;
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    public Task ReportFormatResultAsync(UsbDeviceInfo info, bool confirmed)
    {
        if (string.Equals(info.DriveLetter, _currentProcessingDrive, StringComparison.OrdinalIgnoreCase) && _currentFormatTcs != null)
        {
            _currentFormatTcs.TrySetResult(confirmed);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ScanDeviceContentAsync(UsbDeviceInfo info)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(info.DriveLetter)) return true;

                string[] allowedFiles = _settings.UsbScanAllowedFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string[] blockExts = _settings.UsbScanBlockExtensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string[] safeExts = _settings.UsbScanSafeExtensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var files = Directory.GetFiles(info.DriveLetter, "*.*", SearchOption.AllDirectories);
                bool hasThreat = false;

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string ext = Path.GetExtension(file);

                    // 1. Exact Match (Allowed Files)
                    if (allowedFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // 2. Blocked Extensions
                    if (blockExts.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                            ErrorCodes.UsbScanThreatDetected, "USB Threat Detected",
                            $"{info.ToLogString()} | Action=ContentScan, File={fileName}, Extension={ext}, Verdict=Blocked(InBlacklist), User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                        hasThreat = true;
                        continue;
                    }

                    // 3. Safe Extensions
                    if (safeExts.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // 4. Suspicious (NotInAnyList)
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbScanSuspiciousFile, "USB Suspicious File",
                        $"{info.ToLogString()} | Action=ContentScan, File={fileName}, Extension={ext}, Verdict=Suspicious(NotInAnyList), User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                }

                if (!hasThreat)
                {
                    EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbScanClean, "USB Scan Clean",
                        $"{info.ToLogString()} | Action=ContentScan, Result=Clean, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                }

                return !hasThreat;
            }
            catch (Exception ex)
            {
                EventLogService.Instance?.LogError("UsbSecurity", "UsbSecurityService",
                    "ERR-9000", "USB Scan Exception", $"{info.ToLogString()} | Ex={ex.Message}");
                return false;
            }
        });
    }
    /// <summary>
    /// 空碟偵測：檢查 USB 是否已經是目標檔案系統且無任何使用者檔案。
    /// 若已經是乾淨的格式化狀態，則跳過不必要的重複格式化。
    /// </summary>
    private bool IsAlreadyFormatted(UsbDeviceInfo info, string targetFileSystem)
    {
        try
        {
            // 檢查檔案系統是否已經是目標格式
            if (!string.Equals(info.FileSystem, targetFileSystem, StringComparison.OrdinalIgnoreCase))
                return false;

            // 檢查磁碟根目錄是否存在且可存取
            string rootPath = info.DriveLetter + "\\";
            if (!Directory.Exists(rootPath))
                return false;

            // 檢查是否有任何檔案或子目錄（System Volume Information 除外）
            var entries = Directory.GetFileSystemEntries(rootPath);
            foreach (var entry in entries)
            {
                string name = Path.GetFileName(entry);
                // System Volume Information 是 Windows 自動建立的系統目錄，不視為使用者檔案
                if (string.Equals(name, "System Volume Information", StringComparison.OrdinalIgnoreCase))
                    continue;
                // 發現任何使用者檔案或目錄，視為非空碟
                return false;
            }

            return true; // 已經是目標格式且無使用者檔案
        }
        catch
        {
            return false; // 無法讀取時保守處理，視為需要格式化
        }
    }

    /// <summary>
    /// 執行 Windows 快速格式化指令。
    /// 
    /// 安全性設計：
    ///   - driveLetter 與 fileSystem 經 Regex 白名單驗證，防止 Command Injection。
    ///   - 自動偵測當前權限：已提權 (Win11 IoT Kiosk) 直接執行，不觸發 UAC。
    ///   - 未提權 (開發環境) 才使用 Verb="runas" fallback。
    ///   - 輸出截斷至 500 字元，避免 DB 欄位爆炸。
    /// </summary>
    private Task<(bool Success, string Output)> RunFormatCommandAsync(string driveLetter, string fileSystem)
    {
        return Task.Run(async () =>
        {
            try
            {
                // 安全性驗證：嚴格限制 driveLetter 格式（僅允許 "X:" 單一磁碟代號）
                if (!Regex.IsMatch(driveLetter, @"^[A-Za-z]:$"))
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbFormatFailed, "USB Format SecurityBlock",
                        $"Action=QuickFormat, Result=SecurityBlock, Reason=InvalidDriveLetter, Input='{driveLetter}'");
                    return (false, $"SecurityBlock: Invalid driveLetter format '{driveLetter}'");
                }

                // 安全性驗證：嚴格限制 fileSystem 為已知的安全值
                string[] allowedFileSystems = { "exFAT", "FAT32", "NTFS" };
                if (!allowedFileSystems.Contains(fileSystem, StringComparer.OrdinalIgnoreCase))
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbFormatFailed, "USB Format SecurityBlock",
                        $"Action=QuickFormat, Result=SecurityBlock, Reason=InvalidFileSystem, Input='{fileSystem}'");
                    return (false, $"SecurityBlock: Invalid fileSystem '{fileSystem}'");
                }

                // 三路分流：依據當前環境選擇最適合的格式化執行方式
                bool isElevated = IsRunningAsAdmin();

                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                    ErrorCodes.UsbFormatPromptShown, "Format Route Check",
                    $"IsElevated={isElevated}");

                if (isElevated)
                {
                    // ── 路徑 1: 已提權（測試環境以 Admin 執行）──
                    EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbFormatPromptShown, "Format Route Selected",
                        $"Route=Direct(Admin), Drive={driveLetter}");
                    return RunFormatDirect(driveLetter, fileSystem);
                }

                // ── 路徑 2: PrivilegedService 可用（IoT 生產環境）──
                bool serviceAvailable = await PipeClient.IsServiceAvailableAsync();

                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                    ErrorCodes.UsbFormatPromptShown, "Format Route Check",
                    $"ServiceAvailable={serviceAvailable}");

                if (serviceAvailable)
                {
                    EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbFormatPromptShown, "Format Route Selected",
                        $"Route=PrivilegedService(Pipe), Drive={driveLetter}");
                    return await RunFormatViaService(driveLetter, fileSystem);
                }

                // ── 路徑 3: 開發環境 fallback（UAC 提權）──
                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                    ErrorCodes.UsbFormatPromptShown, "Format Route Selected",
                    $"Route=ElevatedFallback(UAC), Drive={driveLetter}");
                return RunFormatElevated(driveLetter, fileSystem);
            }
            catch (Exception ex)
            {
                return (false, $"Exception={ex.Message}");
            }
        });
    }
    /// <summary>透過 PrivilegedService Named Pipe 執行 format（IoT 生產環境，App 無 Admin 權限）</summary>
    private async Task<(bool Success, string Output)> RunFormatViaService(string driveLetter, string fileSystem)
    {
        var request = new PipeRequest
        {
            Command = PipeCommand.FormatDrive,
            DriveLetter = driveLetter,
            FileSystem = fileSystem,
            CallerUser = _sessionService.CurrentUser?.Username ?? "Unknown"
        };

        var response = await PipeClient.SendRequestAsync(request);
        string output = $"Via=PrivilegedService, RequestId={request.RequestId}, {response.Output ?? response.Error}";

        if (output.Length > 500)
            output = output[..500] + "...";

        return (response.Success, output);
    }

    /// <summary>已具管理員權限時直接執行 format（無 UAC）</summary>
    private (bool Success, string Output) RunFormatDirect(string driveLetter, string fileSystem)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c format {driveLetter} /FS:{fileSystem} /Q /Y",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(950),
            StandardErrorEncoding = System.Text.Encoding.GetEncoding(950)
        };

        using var process = Process.Start(psi);
        if (process == null) return (false, "Process.Start returned null");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        string output = $"ExitCode={process.ExitCode}, Elevated=true, Stdout={stdout.Trim()}, Stderr={stderr.Trim()}";
        if (output.Length > 500)
            output = output[..500] + "...";

        return (process.ExitCode == 0, output);
    }

    /// <summary>未提權時透過 bat + runas 執行 format（開發環境 fallback，會觸發 UAC）</summary>
    private (bool Success, string Output) RunFormatElevated(string driveLetter, string fileSystem)
    {
        string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        Directory.CreateDirectory(tempDir);
        string batFile = Path.Combine(tempDir, $"format_{Guid.NewGuid():N}.bat");
        string resultFile = Path.ChangeExtension(batFile, ".result.txt");

        string batContent = $"""
            @echo off
            format {driveLetter} /FS:{fileSystem} /Q /Y > "{resultFile}" 2>&1
            echo EXIT_CODE=%ERRORLEVEL% >> "{resultFile}"
            """;
        File.WriteAllText(batFile, batContent);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = batFile,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, "Process.Start returned null");

            bool exited = process.WaitForExit(30_000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                return (false, "Format process timed out after 30 seconds");
            }

            if (File.Exists(resultFile))
            {
                string output = File.ReadAllText(resultFile).Trim();
                if (output.Length > 500)
                    output = output[..500] + "...";

                bool success = output.Contains("EXIT_CODE=0");
                return (success, $"Elevated=false(UAC), {output}");
            }
            else
            {
                return (false, "Result file not found (UAC may have been declined)");
            }
        }
        finally
        {
            try { File.Delete(batFile); } catch { }
            try { File.Delete(resultFile); } catch { }
        }
    }

    /// <summary>偵測當前程序是否以管理員權限執行</summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        StopListening();
        _processingLock.Dispose();
    }
}
