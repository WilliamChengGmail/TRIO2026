using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using System.Text;

namespace TRIO2026.App.Services;

/// <summary>
/// 安裝實例 UUID 自動產生服務
/// 
/// 首次啟動 App 時自動偵測硬體與 OS 帳號資訊，產生唯一 UUID 並寫入 system_config.db。
/// 後續啟動僅讀取已存在的 UUID。
/// 
/// UUID 唯一性保證：
///   - 使用 Guid.NewGuid()（密碼學等級隨機數）
///   - 碰撞機率 < 10^-38
///   - 重裝 OS 後因 DB 被清空而產生新 UUID
/// 
/// 記錄的資訊（共 12 筆 SystemSetting）：
///   - installation_uuid        安裝實例 UUID
///   - installation_timestamp   UUID 產生時間
///   - hw_bios_uuid             主機板 SMBIOS UUID
///   - hw_machine_name          Windows 電腦名稱
///   - hw_os_version            作業系統版本
///   - hw_processor             CPU 型號
///   - hw_total_memory_gb       記憶體容量
///   - hw_baseboard             主機板型號
///   - os_current_user          首次啟動時的 OS 登入帳號
///   - os_current_user_groups   登入帳號所屬群組
///   - os_administrators        Administrators 群組成員（資安基線）
///   - os_users                 Users 群組成員
/// 
/// 用於 CFS（Configuration File Security）機制：
///   - 確保 air-gapped 工業電腦的設定檔不會被誤用
///   - 出廠後可比對帳號變更，偵測未授權管理帳號
/// 
/// 製作者: Office of William
/// </summary>
public static class InstallationUuidService
{
    /// <summary>
    /// 確保 UUID 已產生。若尚未存在則自動產生並寫入 DB。
    /// 回傳 UUID 字串。
    /// </summary>
    public static string EnsureUuid(SystemSettingService settings)
    {
        var existingUuid = settings.GetLiveString("System", "installation_uuid", "");
        if (!string.IsNullOrEmpty(existingUuid))
            return existingUuid;

        // ── 首次啟動：產生 UUID + 硬體快照 + OS 帳號基線 ──
        string uuid = Guid.NewGuid().ToString();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // UUID + 時間
        settings.SetLiveString("System", "installation_uuid", uuid);
        settings.SetLiveString("System", "installation_timestamp", timestamp);

        // 硬體資訊
        settings.SetLiveString("System", "hw_bios_uuid", GetWmiValue(
            "SELECT UUID FROM Win32_ComputerSystemProduct", "UUID"));
        settings.SetLiveString("System", "hw_machine_name", Environment.MachineName);
        settings.SetLiveString("System", "hw_os_version", Environment.OSVersion.ToString());
        settings.SetLiveString("System", "hw_processor", GetWmiValue(
            "SELECT Name FROM Win32_Processor", "Name"));
        settings.SetLiveString("System", "hw_total_memory_gb", GetTotalMemoryGb());
        settings.SetLiveString("System", "hw_baseboard", GetBaseboard());

        // OS 帳號資訊（資安基線）
        settings.SetLiveString("System", "os_current_user",
            $"{Environment.UserDomainName}\\{Environment.UserName}");
        settings.SetLiveString("System", "os_current_user_groups", GetCurrentUserGroups());
        settings.SetLiveString("System", "os_administrators", GetLocalGroupMembers("Administrators"));
        settings.SetLiveString("System", "os_users", GetLocalGroupMembers("Users"));

        return uuid;
    }

    // ═══════════════════════════════════════
    // WMI Helpers
    // ═══════════════════════════════════════

    private static string GetWmiValue(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var obj in searcher.Get())
                return obj[property]?.ToString() ?? "N/A";
        }
        catch (Exception ex) { return $"ERROR:{ex.Message}"; }
        return "N/A";
    }

    private static string GetTotalMemoryGb()
    {
        try
        {
            using var q = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in q.Get())
            {
                if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes))
                    return $"{Math.Round(bytes / 1073741824.0, 1)}";
            }
        }
        catch { }
        return "N/A";
    }

    private static string GetBaseboard()
    {
        try
        {
            using var q = new ManagementObjectSearcher(
                "SELECT Product, Manufacturer FROM Win32_BaseBoard");
            foreach (var obj in q.Get())
            {
                string mfg = obj["Manufacturer"]?.ToString() ?? "";
                string prod = obj["Product"]?.ToString() ?? "";
                return $"{mfg} {prod}".Trim();
            }
        }
        catch { }
        return "N/A";
    }

    // ═══════════════════════════════════════
    // OS Account Helpers
    // ═══════════════════════════════════════

    private static string GetCurrentUserGroups()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var groups = new List<string>();
            if (identity.Groups != null)
            {
                foreach (var gid in identity.Groups)
                {
                    try
                    {
                        var name = gid.Translate(typeof(NTAccount))?.Value;
                        if (name != null) groups.Add(name);
                    }
                    catch { /* 某些 SID 無法翻譯（如 Logon SID），跳過 */ }
                }
            }
            return groups.Count > 0 ? string.Join(", ", groups) : "(none)";
        }
        catch (Exception ex) { return $"ERROR:{ex.Message}"; }
    }

    /// <summary>
    /// 使用 net localgroup 取得本機群組成員清單
    /// 比 WMI Win32_GroupUser 更可靠（WMI 在某些 IoT SKU 上有限制）
    /// </summary>
    private static string GetLocalGroupMembers(string groupName)
    {
        try
        {
            // 註冊 Big5 等額外編碼（中文 Windows 的 net 指令輸出需要）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var psi = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"localgroup \"{groupName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding("big5")
            };

            using var proc = Process.Start(psi);
            if (proc == null) return "N/A";

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            var lines = output.Split('\n', StringSplitOptions.TrimEntries);
            var members = new List<string>();
            bool inSection = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("---")) { inSection = true; continue; }
                if (inSection)
                {
                    if (string.IsNullOrWhiteSpace(line) ||
                        line.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("順利完成") ||
                        line.Contains("成功完成") ||
                        line.Contains("命令已"))
                        break;
                    members.Add(line);
                }
            }
            return members.Count > 0 ? string.Join(", ", members) : "(empty)";
        }
        catch (Exception ex) { return $"ERROR:{ex.Message}"; }
    }
}
