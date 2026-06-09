using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using TRIO2026.App.Models;

namespace TRIO2026.App.Helpers;

public static class UsbDeviceQueryHelper
{
    public static UsbDeviceInfo? GetDeviceInfo(string driveLetter)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(driveLetter)) return null;
            
            driveLetter = driveLetter.Trim().ToUpper();
            if (driveLetter.Length == 1) driveLetter += ":";
            if (driveLetter.Length > 2) driveLetter = driveLetter.Substring(0, 2);

            var driveInfo = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.StartsWith(driveLetter));
            if (driveInfo == null) return null;

            var info = new UsbDeviceInfo
            {
                DriveLetter = driveLetter,
                DriveType = driveInfo.DriveType.ToString(),
                VolumeLabel = driveInfo.IsReady ? driveInfo.VolumeLabel : string.Empty,
                FileSystem = driveInfo.IsReady ? driveInfo.DriveFormat : string.Empty,
                CapacityBytes = driveInfo.IsReady ? driveInfo.TotalSize : 0,
                CapacityDisplay = driveInfo.IsReady ? FormatBytes(driveInfo.TotalSize) : "Unknown"
            };

            // Query WMI for hardware info
            // Win32_LogicalDisk -> Win32_LogicalDiskToPartition -> Win32_DiskPartition -> Win32_DiskDriveToDiskPartition -> Win32_DiskDrive
            
            string query = $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";
            using var searcher1 = new ManagementObjectSearcher(query);
            foreach (ManagementObject partition in searcher1.Get().Cast<ManagementObject>())
            {
                using (partition)
                {
                    string query2 = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                    using var searcher2 = new ManagementObjectSearcher(query2);
                    foreach (ManagementObject drive in searcher2.Get().Cast<ManagementObject>())
                    {
                        using (drive)
                        {
                            string serial = drive["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                            string model = drive["Model"]?.ToString()?.Trim() ?? string.Empty;
                            string pnpId = drive["PNPDeviceID"]?.ToString()?.Trim() ?? string.Empty;

                            string vid = "";
                            string pid = "";
                            var match = Regex.Match(pnpId, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                vid = match.Groups[1].Value;
                                pid = match.Groups[2].Value;
                            }

                            return info with 
                            { 
                                SerialNumber = serial, 
                                DeviceModel = model, 
                                VendorId = vid, 
                                ProductId = pid, 
                                DeviceInstanceId = pnpId 
                            };
                        }
                    }
                }
            }
            
            return info;
        }
        catch (Exception)
        {
            // Fallback or log
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
