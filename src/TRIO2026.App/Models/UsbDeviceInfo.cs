using System;

namespace TRIO2026.App.Models;

/// <summary>USB 裝置指紋 — 用於審計日誌的唯一識別</summary>
public record UsbDeviceInfo
{
    // ── 磁碟層級（DriveInfo / WMI Win32_LogicalDisk）──
    public string DriveLetter { get; init; } = string.Empty;     // E:
    public string DriveType { get; init; } = string.Empty;       // Removable / Fixed / Network
    public string VolumeLabel { get; init; } = string.Empty;     // KINGSTON
    public string FileSystem { get; init; } = string.Empty;      // exFAT / NTFS / FAT32
    public long CapacityBytes { get; init; } = 0;                // 15502147584
    public string CapacityDisplay { get; init; } = string.Empty; // 14.4 GB

    // ── 硬體層級（WMI Win32_DiskDrive + Win32_USBHub）──
    public string SerialNumber { get; init; } = string.Empty;    // 0123456789ABCDEF（硬體序號）
    public string DeviceModel { get; init; } = string.Empty;     // Kingston DataTraveler 3.0
    public string VendorId { get; init; } = string.Empty;        // VID_0951（USB Vendor ID）
    public string ProductId { get; init; } = string.Empty;       // PID_1666（USB Product ID）
    public string DeviceInstanceId { get; init; } = string.Empty;// USB\VID_0951&PID_1666\...（裝置實例路徑）

    public string ToLogString()
    {
        return $"[Device] Drive={DriveLetter}, Type={DriveType}, Label={VolumeLabel}, FS={FileSystem}, Size={CapacityDisplay}, Serial={SerialNumber}, Model={DeviceModel}, VID={VendorId}, PID={ProductId}, InstanceId={DeviceInstanceId}";
    }
}
