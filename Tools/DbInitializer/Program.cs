using TRIO2026.Data.Extensions;

// ============================================================
// TRIO2026 資料庫初始化工具
// 執行此程式可建立四個 SQLite 資料庫檔案及其表結構
// ============================================================

Console.WriteLine("=== TRIO2026 資料庫初始化工具 ===");
Console.WriteLine($"時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine();

// 設定資料庫目錄為專案根目錄下的 Database
var projectRoot = FindProjectRoot();
var databaseDir = Path.Combine(projectRoot, "Database");

Console.WriteLine($"專案根目錄: {projectRoot}");
DatabaseInitializer.SetDatabaseDirectory(databaseDir);
DatabaseInitializer.PasswordHasher = pwd => BCrypt.Net.BCrypt.HashPassword(pwd, workFactor: 12);

// 建立日誌（寫入 Logs/db-init-logs/）
var logDir = Path.Combine(projectRoot, "Logs", "db-init-logs");
using var startupLog = new StartupLogger(logDir);
startupLog.Info("DbInitializer", "獨立工具啟動", $"DatabaseDir={databaseDir}");

try
{
    await DatabaseInitializer.InitializeAllAsync();

    Console.WriteLine();
    Console.WriteLine("=== 驗證結果 ===");

    // 列出已建立的 .db 檔案
    var dbFiles = Directory.GetFiles(databaseDir, "*.db");
    foreach (var file in dbFiles)
    {
        var fi = new FileInfo(file);
        Console.WriteLine($"  ✓ {fi.Name} ({fi.Length:N0} bytes)");
    }

    Console.WriteLine();
    Console.WriteLine($"共 {dbFiles.Length} 個資料庫檔案已建立。");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ 初始化失敗: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
    Environment.Exit(1);
}

/// <summary>
/// 從工具執行目錄向上搜尋專案根目錄（包含 TRIO2026.sln）
/// </summary>
static string FindProjectRoot()
{
    var dir = AppDomain.CurrentDomain.BaseDirectory;
    // 如果是 debug 模式（bin/Debug/net10.0），向上走到包含 .sln 的目錄
    while (dir != null)
    {
        if (Directory.GetFiles(dir, "TRIO2026.sln").Length > 0)
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }

    // 若找不到 .sln，嘗試 D:\TRIO2026
    var fallback = @"D:\TRIO2026";
    if (Directory.Exists(fallback))
        return fallback;

    throw new DirectoryNotFoundException("找不到 TRIO2026.sln 所在目錄");
}
