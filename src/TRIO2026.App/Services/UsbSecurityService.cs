using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using TRIO2026.App.Helpers;
using TRIO2026.App.Models;
using TRIO2026.Core;

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

                if (!_settings.UsbCybersecurityEnabled)
                {
                    _currentProcessingDrive = null;
                    continue;
                }

                if (_sessionService.IsGuestLogin)
                {
                    EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                        ErrorCodes.UsbGuestBlocked, "USB Guest Mode Blocked",
                        $"{info.ToLogString()} | Status=Blocked, User=guest");
                    _currentProcessingDrive = null;
                    continue;
                }

                int queueCount = _pendingDrives.Count;
                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                    ErrorCodes.UsbDeviceInserted, "USB Device Inserted",
                    $"{info.ToLogString()} | Status=Processing, QueueRemaining={queueCount}, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");

                // Auto Format
                if (_settings.UsbAutoFormatOnInsert)
                {
                    if (info.DriveType != "Removable")
                    {
                        EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                            ErrorCodes.UsbFormatBlockedNonRemovable, "Format Blocked - Non Removable",
                            $"{info.ToLogString()} | Action=QuickFormat, Result=Blocked(NotRemovable), User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                    }
                    else
                    {
                        _currentFormatTcs = new TaskCompletionSource<bool>();
                        FormatRequired?.Invoke(this, info);
                        
                        bool confirmed = await _currentFormatTcs.Task;
                        _currentFormatTcs = null;

                        if (confirmed)
                        {
                            bool success = await RunFormatCommandAsync(info.DriveLetter, "exFAT");
                            if (success)
                            {
                                EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                                    ErrorCodes.UsbFormatSuccess, "USB Quick Format Success",
                                    $"{info.ToLogString()} | Action=QuickFormat, TargetFS=exFAT, Result=Success, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                            }
                            else
                            {
                                EventLogService.Instance?.LogWarning("UsbSecurity", "UsbSecurityService",
                                    ErrorCodes.UsbFormatFailed, "USB Quick Format Failed",
                                    $"{info.ToLogString()} | Action=QuickFormat, TargetFS=exFAT, Result=Failed, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
                            }
                        }
                        else
                        {
                            EventLogService.Instance?.LogInfo("UsbSecurity", "UsbSecurityService",
                                ErrorCodes.UsbFormatCancelled, "USB Format Cancelled",
                                $"{info.ToLogString()} | Action=QuickFormat, TargetFS=exFAT, Result=Cancelled, User={_sessionService.CurrentUser?.Username ?? "Unknown"}");
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

    private Task<bool> RunFormatCommandAsync(string driveLetter, string fileSystem)
    {
        return Task.Run(() =>
        {
            try
            {
                // cmd.cs /c format E: /FS:exFAT /Q /Y
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c format {driveLetter} /FS:{fileSystem} /Q /Y",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public void Dispose()
    {
        StopListening();
        _processingLock.Dispose();
    }
}
