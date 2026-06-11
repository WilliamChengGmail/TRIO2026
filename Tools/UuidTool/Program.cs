using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using System.Text;
using Microsoft.Data.Sqlite;

/// <summary>
/// TRIO2026 Installation UUID 查詢工具
/// 
/// 用途：查看當前工業電腦上 TRIO2026 安裝實例的唯一識別碼 (UUID)。
///       UUID 於首次執行時以 Guid.NewGuid() 產生（密碼學等級隨機），
///       寫入 system_config.db，後續每次執行僅讀取顯示。
/// 
/// UUID 唯一性保證：
///   - 使用 .NET Guid.NewGuid()（底層為 OS 密碼學隨機數產生器）
///   - 碰撞機率 < 10^-38，實務上不可能重複
///   - 每次全新安裝（含重裝 OS）都會產生全新 UUID
///   - 硬體資訊（BIOS UUID、機器名、CPU、OS）另存於 SystemSetting 供追蹤
/// 
/// 用於 CFS（Configuration File Security）機制：
///   - 確保每台 air-gapped 工業電腦的設定檔不會被誤用
///   - 設定匯入/匯出時可依 UUID 驗證來源機台
/// 
/// 製作者: Office of William
/// </summary>

// 註冊額外編碼（.NET 8 預設不含 Big5 等，net localgroup 中文 Windows 輸出需要）
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║     TRIO2026 Installation UUID Viewer                ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// ── 定位 Database ──
string toolDir = AppContext.BaseDirectory;
string? solutionRoot = FindSolutionRoot(toolDir);
if (solutionRoot == null)
{
    PrintError("Cannot locate TRIO2026 solution root.",
               "Please run this tool from within the TRIO2026 project tree.");
    WaitAndExit(1);
    return;
}

string dbPath = Path.Combine(solutionRoot, "Database", "system_config.db");

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine($"  Solution Root : {solutionRoot}");
Console.WriteLine($"  Database Path : {dbPath}");
Console.ResetColor();
Console.WriteLine();

if (!File.Exists(dbPath))
{
    PrintError($"Database not found: {dbPath}",
               "Please ensure TRIO2026.App has been run at least once to initialize the DB.");
    WaitAndExit(1);
    return;
}

// ── 讀取或產生 UUID ──
string connStr = $"Data Source={dbPath}";

using (var conn = new SqliteConnection(connStr))
{
    conn.Open();

    // 確認 SystemSetting 表存在
    using var checkCmd = conn.CreateCommand();
    checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SystemSetting'";
    if (checkCmd.ExecuteScalar() == null)
    {
        PrintError("SystemSetting table not found in database.", null);
        WaitAndExit(1);
        return;
    }

    // 讀取現有 UUID
    string? existingUuid = ReadSetting(conn, "System", "installation_uuid");
    string? installedAt  = ReadSetting(conn, "System", "installation_timestamp");

    if (!string.IsNullOrEmpty(existingUuid))
    {
        // ── UUID 已存在：顯示所有資訊 ──
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  [OK] Installation UUID found in database.");
        Console.ResetColor();
        Console.WriteLine();

        PrintUuidBox(existingUuid, installedAt, solutionRoot);
        PrintStoredHardwareInfo(conn);
    }
    else
    {
        // ── UUID 不存在：產生並寫入 ──
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  [INFO] No Installation UUID found. Generating...");
        Console.ResetColor();
        Console.WriteLine();

        // 收集硬體資訊
        var hw = CollectHardwareInfo();
        PrintLiveHardwareInfo(hw);

        // 產生 UUID（密碼學隨機，保證唯一）
        string newUuid = Guid.NewGuid().ToString();
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 寫入 DB — UUID + 時間戳 + 硬體快照
        int nextId = GetNextId(conn);

        InsertSetting(conn, nextId, "System", "installation_uuid", newUuid,
            "TRIO2026 安裝實例 UUID（首次啟動時產生，用於 CFS 設定檔安全驗證）",
            null);

        InsertSetting(conn, nextId + 1, "System", "installation_timestamp", timestamp,
            "TRIO2026 UUID 首次產生時間",
            null);

        InsertSetting(conn, nextId + 2, "System", "hw_bios_uuid", hw.BiosUuid,
            "首次偵測到的主機板 SMBIOS UUID（硬體識別碼，不可變更）",
            "來源: Win32_ComputerSystemProduct.UUID");

        InsertSetting(conn, nextId + 3, "System", "hw_machine_name", hw.MachineName,
            "首次偵測到的 Windows 電腦名稱",
            null);

        InsertSetting(conn, nextId + 4, "System", "hw_os_version", hw.OsVersion,
            "首次偵測到的作業系統版本",
            null);

        InsertSetting(conn, nextId + 5, "System", "hw_processor", hw.ProcessorName,
            "首次偵測到的處理器型號",
            "來源: Win32_Processor.Name");

        InsertSetting(conn, nextId + 6, "System", "hw_total_memory_gb", hw.TotalMemoryGb,
            "首次偵測到的實體記憶體容量 (GB)",
            "來源: Win32_ComputerSystem.TotalPhysicalMemory");

        InsertSetting(conn, nextId + 7, "System", "hw_baseboard", hw.Baseboard,
            "首次偵測到的主機板型號",
            "來源: Win32_BaseBoard.Product");

        // OS 帳號資訊（資安稽核：出廠時帳號基線）
        InsertSetting(conn, nextId + 8, "System", "os_current_user", hw.CurrentUser,
            "首次執行時的 OS 登入帳號",
            null);

        InsertSetting(conn, nextId + 9, "System", "os_current_user_groups", hw.CurrentUserGroups,
            "首次執行時登入帳號所屬的本機群組",
            null);

        InsertSetting(conn, nextId + 10, "System", "os_administrators", hw.AdminGroupMembers,
            "首次偵測到的 Administrators 群組成員（資安基線）",
            "用途: 出廠後比對是否有未授權帳號加入管理群組");

        InsertSetting(conn, nextId + 11, "System", "os_users", hw.UsersGroupMembers,
            "首次偵測到的 Users 群組成員",
            "用途: 出廠後比對帳號變更紀錄");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  [OK] UUID generated and hardware/OS info saved to database.");
        Console.ResetColor();
        Console.WriteLine();

        PrintUuidBox(newUuid, timestamp, solutionRoot);
    }
}

WaitAndExit(0);

// ══════════════════════════════════════════════════════
// Helper Methods
// ══════════════════════════════════════════════════════

static string? ReadSetting(SqliteConnection conn, string category, string key)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Value FROM SystemSetting WHERE Category=$cat AND Key=$key";
    cmd.Parameters.AddWithValue("$cat", category);
    cmd.Parameters.AddWithValue("$key", key);
    return cmd.ExecuteScalar()?.ToString();
}

static int GetNextId(SqliteConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COALESCE(MAX(Id), 0) + 1 FROM SystemSetting";
    return Convert.ToInt32(cmd.ExecuteScalar());
}

static void InsertSetting(SqliteConnection conn, int id, string category, string key,
    string value, string? description, string? remark)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT OR IGNORE INTO SystemSetting (Id, Category, Key, Value, Description, Remark)
        VALUES ($id, $cat, $key, $val, $desc, $remark)";
    cmd.Parameters.AddWithValue("$id", id);
    cmd.Parameters.AddWithValue("$cat", category);
    cmd.Parameters.AddWithValue("$key", key);
    cmd.Parameters.AddWithValue("$val", value);
    cmd.Parameters.AddWithValue("$desc", (object?)description ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$remark", (object?)remark ?? DBNull.Value);
    cmd.ExecuteNonQuery();
}

static void PrintUuidBox(string uuid, string? installedAt, string installPath)
{
    Console.WriteLine("  ┌───────────────────────────────────────────────────────┐");
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  │  Installation UUID                                    │");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"  │  {uuid}      │");
    Console.ResetColor();
    Console.WriteLine("  ├───────────────────────────────────────────────────────┤");
    Console.WriteLine($"  │  Machine     : {Environment.MachineName,-39}│");
    Console.WriteLine($"  │  Install Path: {Truncate(installPath, 39),-39}│");
    if (!string.IsNullOrEmpty(installedAt))
        Console.WriteLine($"  │  Generated   : {installedAt,-39}│");
    Console.WriteLine("  └───────────────────────────────────────────────────────┘");
    Console.WriteLine();
}

static void PrintStoredHardwareInfo(SqliteConnection conn)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Stored Hardware Info (from first detection):");

    var hwFields = new[] {
        ("hw_bios_uuid",       "SMBIOS UUID "),
        ("hw_machine_name",    "Machine Name"),
        ("hw_os_version",      "OS Version  "),
        ("hw_processor",       "Processor   "),
        ("hw_total_memory_gb", "Memory (GB) "),
        ("hw_baseboard",       "Baseboard   "),
    };

    foreach (var (key, label) in hwFields)
    {
        string? val = ReadSetting(conn, "System", key);
        if (!string.IsNullOrEmpty(val))
            Console.WriteLine($"    {label}: {val}");
    }

    Console.ResetColor();
    Console.WriteLine();

    // OS 帳號資訊
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Stored OS Account Info (security baseline):");

    var osFields = new[] {
        ("os_current_user",        "Login User   "),
        ("os_current_user_groups", "User Groups  "),
        ("os_administrators",      "Administrators"),
        ("os_users",               "Users Group  "),
    };

    foreach (var (key, label) in osFields)
    {
        string? val = ReadSetting(conn, "System", key);
        if (!string.IsNullOrEmpty(val))
            Console.WriteLine($"    {label}: {val}");
    }

    Console.ResetColor();
    Console.WriteLine();
}

static void PrintLiveHardwareInfo(HardwareInfo info)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  Detected Hardware:");
    Console.WriteLine($"    SMBIOS UUID : {info.BiosUuid}");
    Console.WriteLine($"    Machine Name: {info.MachineName}");
    Console.WriteLine($"    OS Version  : {info.OsVersion}");
    Console.WriteLine($"    Processor   : {Truncate(info.ProcessorName, 50)}");
    Console.WriteLine($"    Memory (GB) : {info.TotalMemoryGb}");
    Console.WriteLine($"    Baseboard   : {info.Baseboard}");
    Console.ResetColor();
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  OS Account Info:");
    Console.WriteLine($"    Login User    : {info.CurrentUser}");
    Console.WriteLine($"    User Groups   : {info.CurrentUserGroups}");
    Console.WriteLine($"    Administrators: {info.AdminGroupMembers}");
    Console.WriteLine($"    Users Group   : {info.UsersGroupMembers}");
    Console.ResetColor();
    Console.WriteLine();
}

static HardwareInfo CollectHardwareInfo()
{
    var info = new HardwareInfo
    {
        MachineName = Environment.MachineName,
        OsVersion = Environment.OSVersion.ToString()
    };

    // ── 硬體資訊 ──
    try
    {
        using var q = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
        foreach (var obj in q.Get()) { info.BiosUuid = obj["UUID"]?.ToString() ?? "N/A"; break; }
    }
    catch (Exception ex) { info.BiosUuid = $"ERROR:{ex.Message}"; }

    try
    {
        using var q = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
        foreach (var obj in q.Get()) { info.ProcessorName = obj["Name"]?.ToString() ?? "N/A"; break; }
    }
    catch { }

    try
    {
        using var q = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        foreach (var obj in q.Get())
        {
            if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong bytes))
                info.TotalMemoryGb = $"{Math.Round(bytes / 1073741824.0, 1)}";
            break;
        }
    }
    catch { }

    try
    {
        using var q = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard");
        foreach (var obj in q.Get())
        {
            string mfg = obj["Manufacturer"]?.ToString() ?? "";
            string prod = obj["Product"]?.ToString() ?? "";
            info.Baseboard = $"{mfg} {prod}".Trim();
            break;
        }
    }
    catch { }

    // ── OS 帳號資訊 ──
    try
    {
        // 當前登入帳號
        info.CurrentUser = $"{Environment.UserDomainName}\\{Environment.UserName}";

        // 當前帳號所屬群組
        var identity = WindowsIdentity.GetCurrent();
        var groups = new List<string>();
        if (identity.Groups != null)
        {
            foreach (var gid in identity.Groups)
            {
                try
                {
                    var groupName = gid.Translate(typeof(NTAccount))?.Value;
                    if (groupName != null)
                        groups.Add(groupName);
                }
                catch { /* 某些 SID 無法翻譯（如 Logon SID），跳過 */ }
            }
        }
        info.CurrentUserGroups = string.Join(", ", groups);
    }
    catch (Exception ex) { info.CurrentUser = $"ERROR:{ex.Message}"; }

    // Administrators 群組成員
    info.AdminGroupMembers = GetLocalGroupMembers("Administrators");

    // Users 群組成員
    info.UsersGroupMembers = GetLocalGroupMembers("Users");

    return info;
}

/// <summary>
/// 使用 net localgroup 取得本機群組成員清單
/// 比 WMI Win32_GroupUser 更可靠（WMI 在某些 IoT SKU 上有限制）
/// </summary>
static string GetLocalGroupMembers(string groupName)
{
    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = "net",
            Arguments = $"localgroup \"{groupName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding("big5")  // Windows 中文環境
        };

        using var proc = Process.Start(psi);
        if (proc == null) return "N/A";

        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);

        // net localgroup 輸出格式：
        //   成員名稱 會在兩條 "---" 分隔線之間
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);
        var members = new List<string>();
        bool inMemberSection = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("---"))
            {
                inMemberSection = true;
                continue;
            }
            if (inMemberSection)
            {
                // 結束標記：空行、中英文完成訊息
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
    catch (Exception ex)
    {
        return $"ERROR:{ex.Message}";
    }
}

static string? FindSolutionRoot(string startDir)
{
    string? dir = startDir;
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "TRIO2026.sln")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }

    string candidate = Path.GetFullPath(Path.Combine(startDir, "..", "..", "..", ".."));
    if (File.Exists(Path.Combine(candidate, "TRIO2026.sln")))
        return candidate;

    return null;
}

static string Truncate(string value, int maxLen)
{
    if (string.IsNullOrEmpty(value)) return "";
    return value.Length <= maxLen ? value : value[..(maxLen - 3)] + "...";
}

static void PrintError(string message, string? hint)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  [ERROR] {message}");
    if (hint != null)
        Console.WriteLine($"          {hint}");
    Console.ResetColor();
}

static void WaitAndExit(int code)
{
    Console.WriteLine();
    Console.Write("Press any key to exit...");
    try { Console.ReadKey(); } catch { /* non-interactive console */ }
    Environment.Exit(code);
}

class HardwareInfo
{
    // 硬體
    public string BiosUuid { get; set; } = "N/A";
    public string MachineName { get; set; } = "N/A";
    public string OsVersion { get; set; } = "N/A";
    public string ProcessorName { get; set; } = "N/A";
    public string TotalMemoryGb { get; set; } = "N/A";
    public string Baseboard { get; set; } = "N/A";

    // OS 帳號
    public string CurrentUser { get; set; } = "N/A";
    public string CurrentUserGroups { get; set; } = "N/A";
    public string AdminGroupMembers { get; set; } = "N/A";
    public string UsersGroupMembers { get; set; } = "N/A";
}
