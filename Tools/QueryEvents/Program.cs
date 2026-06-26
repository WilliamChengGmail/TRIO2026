using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

// ══════════════════════════════════════════════════════════════
// TRIO2026 事件日誌查閱工具（開發階段用）
// 
// 用法：dotnet run --project tools/QueryEvents
//       dotnet run --project tools/QueryEvents -- 5     (最近 5 分鐘)
//       dotnet run --project tools/QueryEvents -- 30    (最近 30 分鐘)
// 
// 製作者: Office of William
// ══════════════════════════════════════════════════════════════

var dbName = "system_event.db";
var currentDir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
string dbPath = "";
while (currentDir != null)
{
    var p1 = System.IO.Path.Combine(currentDir.FullName, "Database", dbName);
    if (System.IO.File.Exists(p1)) { dbPath = p1; break; }
    var p2 = System.IO.Path.Combine(currentDir.FullName, "App", "Database", dbName);
    if (System.IO.File.Exists(p2)) { dbPath = p2; break; }
    currentDir = currentDir.Parent;
}

if (string.IsNullOrEmpty(dbPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n  ❌ 找不到資料庫: {dbName}");
    Console.ResetColor();
    return;
}
var validMinutes = new[] { 1, 3, 5, 10, 30, 60 };

// 解析命令列參數或互動選擇
int minutes;
if (args.Length > 0 && int.TryParse(args[0], out var argMin) && Array.IndexOf(validMinutes, argMin) >= 0)
{
    minutes = argMin;
}
else
{
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════╗");
    Console.WriteLine("║     📋 TRIO2026 事件日誌查閱工具             ║");
    Console.WriteLine("╠══════════════════════════════════════════════╣");
    Console.WriteLine("║   查閱時間範圍：                              ║");
    Console.WriteLine("║     [1]  最近  1 分鐘                        ║");
    Console.WriteLine("║     [2]  最近  3 分鐘                        ║");
    Console.WriteLine("║     [3]  最近  5 分鐘                        ║");
    Console.WriteLine("║     [4]  最近 10 分鐘                        ║");
    Console.WriteLine("║     [5]  最近 30 分鐘                        ║");
    Console.WriteLine("║     [6]  最近 60 分鐘                        ║");
    Console.WriteLine("║     [0]  全部紀錄                             ║");
    Console.WriteLine("╚══════════════════════════════════════════════╝");
    Console.Write("\n  請選擇 [0-6]: ");

    var key = Console.ReadLine()?.Trim() ?? "3";
    minutes = key switch
    {
        "1" => 1,
        "2" => 3,
        "3" => 5,
        "4" => 10,
        "5" => 30,
        "6" => 60,
        "0" => 0,
        _ => 5
    };
}

var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
conn.Open();

// 統計
var countCmd = conn.CreateCommand();
countCmd.CommandText = "SELECT COUNT(*) FROM SystemEvent";
var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

// 查詢
var cmd = conn.CreateCommand();
if (minutes > 0)
{
    var since = DateTime.Now.AddMinutes(-minutes).ToString("yyyy-MM-dd HH:mm:ss");
    cmd.CommandText = @"
        SELECT Id, TimestampLocal, Level, Category, Source, ErrorId, UserId, UserName, 
               Message, Detail, ExceptionType
        FROM SystemEvent 
        WHERE TimestampLocal >= @since
        ORDER BY Id ASC";
    cmd.Parameters.AddWithValue("@since", since);
}
else
{
    cmd.CommandText = @"
        SELECT Id, TimestampLocal, Level, Category, Source, ErrorId, UserId, UserName, 
               Message, Detail, ExceptionType
        FROM SystemEvent 
        ORDER BY Id ASC";
}

var reader = cmd.ExecuteReader();
var events = new List<EventRow>();
while (reader.Read())
{
    events.Add(new EventRow
    {
        Id = reader.GetInt32(0),
        Time = reader.IsDBNull(1) ? "" : reader.GetString(1),
        Level = reader.IsDBNull(2) ? "" : reader.GetString(2),
        Category = reader.IsDBNull(3) ? "" : reader.GetString(3),
        Source = reader.IsDBNull(4) ? "" : reader.GetString(4),
        ErrorId = reader.IsDBNull(5) ? "" : reader.GetString(5),
        UserId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
        UserName = reader.IsDBNull(7) ? "" : reader.GetString(7),
        Message = reader.IsDBNull(8) ? "" : reader.GetString(8),
        Detail = reader.IsDBNull(9) ? "" : reader.GetString(9),
        ExceptionType = reader.IsDBNull(10) ? "" : reader.GetString(10),
    });
}
conn.Close();

// 輸出
var rangeLabel = minutes > 0 ? $"最近 {minutes} 分鐘" : "全部";
var levelColors = new Dictionary<string, ConsoleColor>
{
    ["Info"] = ConsoleColor.Cyan,
    ["Warning"] = ConsoleColor.Yellow,
    ["Error"] = ConsoleColor.Red,
    ["Fatal"] = ConsoleColor.Magenta,
};

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"  📊 資料庫: {dbPath}");
Console.WriteLine($"  📋 總筆數: {totalCount} | 查詢範圍: {rangeLabel} | 查得: {events.Count} 筆");
Console.WriteLine($"  ⏰ 查詢時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.ResetColor();
Console.WriteLine();

if (events.Count == 0)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("  (無事件紀錄)");
    Console.ResetColor();
    return;
}

// 表頭
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  ┌────┬─────────────────────┬─────────┬──────────────┬──────────────────┬────────────┬───────────────────────────────────────┐");
Console.WriteLine("  │ ID │ 時間                │ 等級    │ 分類         │ 來源             │ 使用者     │ 訊息                                  │");
Console.WriteLine("  ├────┼─────────────────────┼─────────┼──────────────┼──────────────────┼────────────┼───────────────────────────────────────┤");
Console.ResetColor();

foreach (var e in events)
{
    var timeStr = e.Time.Length >= 19 ? e.Time[..19] : e.Time;
    var levelStr = e.Level.PadRight(7);
    var catStr = Truncate(e.Category, 12).PadRight(12);
    var srcStr = Truncate(e.Source, 16).PadRight(16);
    var userStr = Truncate(string.IsNullOrEmpty(e.UserName) ? (e.UserId?.ToString() ?? "-") : e.UserName, 10).PadRight(10);
    var msgStr = e.Message;

    // 等級顏色
    var color = levelColors.GetValueOrDefault(e.Level, ConsoleColor.Gray);

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("  │ ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"{e.Id,-3}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($"{timeStr,-20}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = color;
    Console.Write($"{(e.Level == "Info" ? "ℹ️" : e.Level == "Warning" ? "⚠️" : e.Level == "Error" ? "❌" : "💀")} {levelStr}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"{catStr}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($"{srcStr}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.Write($"{userStr}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("│ ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.Write($"{Truncate(msgStr, 37)}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("│");

    // Detail 行（若有）
    if (!string.IsNullOrEmpty(e.Detail))
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  │    │                     │         │              │                  │            │ ");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"↳ {Truncate(e.Detail, 35)}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("│");
    }

    // ErrorId 行（若有）
    if (!string.IsNullOrEmpty(e.ErrorId))
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  │    │                     │         │              │                  │            │ ");
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.Write($"🔖 {e.ErrorId}");
        if (!string.IsNullOrEmpty(e.ExceptionType))
            Console.Write($" ({Truncate(e.ExceptionType, 25)})");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(new string(' ', Math.Max(0, 37 - 4 - e.ErrorId.Length - (string.IsNullOrEmpty(e.ExceptionType) ? 0 : e.ExceptionType.Length + 3))) + "│");
    }
}

Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("  └────┴─────────────────────┴─────────┴──────────────┴──────────────────┴────────────┴───────────────────────────────────────┘");
Console.ResetColor();
Console.WriteLine();

static string Truncate(string s, int max)
{
    if (string.IsNullOrEmpty(s)) return "";
    return s.Length <= max ? s : s[..(max - 2)] + "..";
}

record EventRow
{
    public int Id;
    public string Time = "";
    public string Level = "";
    public string Category = "";
    public string Source = "";
    public string ErrorId = "";
    public int? UserId;
    public string UserName = "";
    public string Message = "";
    public string Detail = "";
    public string ExceptionType = "";
}
