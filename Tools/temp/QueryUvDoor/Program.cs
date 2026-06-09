using Microsoft.Data.Sqlite;

var dbPath = @"D:\TRIO2026\Database\system_event.db";
using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
conn.Open();

// 1) Schema
Console.WriteLine("=== SystemEvent Schema ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='SystemEvent'";
    Console.WriteLine(cmd.ExecuteScalar()?.ToString());
}

// 2) 查詢 UV 相關事件
Console.WriteLine("\n=== UV Events (最近 50 筆，按時間倒序) ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "PRAGMA table_info(SystemEvent)";
    using var r = cmd.ExecuteReader();
    var cols = new List<string>();
    while (r.Read()) cols.Add(r.GetString(1));
    Console.WriteLine($"Columns: {string.Join(", ", cols)}");
}

// 先用通配查詢找 UV 相關
Console.WriteLine();
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"
        SELECT * FROM SystemEvent 
        WHERE EventCode IN ('INF-3001','WRN-3002','INF-3003','ERR-3004')
        ORDER BY Timestamp DESC
        LIMIT 50";

    using var reader = cmd.ExecuteReader();
    var fieldCount = reader.FieldCount;
    // print header
    for (int i = 0; i < fieldCount; i++) Console.Write($"{reader.GetName(i),-22} ");
    Console.WriteLine();
    Console.WriteLine(new string('-', fieldCount * 22));
    
    while (reader.Read())
    {
        for (int i = 0; i < fieldCount; i++)
        {
            var val = reader.IsDBNull(i) ? "(null)" : reader.GetValue(i)?.ToString() ?? "";
            if (val.Length > 20) val = val[..20] + "…";
            Console.Write($"{val,-22} ");
        }
        Console.WriteLine();
    }
}

// 3) 門板誤判分析
Console.WriteLine("\n=== 分析：UV Start 後緊接 DoorInterrupted (Δ < 5s = 疑似誤判) ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"
        WITH uv_events AS (
            SELECT Id, Timestamp, EventCode, Detail,
                   LEAD(EventCode) OVER (ORDER BY Id) AS NextCode,
                   LEAD(Timestamp) OVER (ORDER BY Id) AS NextTimestamp
            FROM SystemEvent
            WHERE EventCode IN ('INF-3001','WRN-3002','INF-3003','ERR-3004')
        )
        SELECT Timestamp, Detail, NextCode, NextTimestamp,
               CAST((julianday(NextTimestamp) - julianday(Timestamp)) * 86400 AS INTEGER) AS DiffSeconds
        FROM uv_events
        WHERE EventCode = 'INF-3001' AND NextCode = 'ERR-3004'
        ORDER BY Timestamp DESC
        LIMIT 20";

    using var reader = cmd.ExecuteReader();
    var found = false;
    while (reader.Read())
    {
        found = true;
        var ts = reader.IsDBNull(0) ? "" : reader.GetString(0);
        var det = reader.IsDBNull(1) ? "" : reader.GetString(1);
        var nextTs = reader.IsDBNull(3) ? "" : reader.GetString(3);
        var diffSec = reader.IsDBNull(4) ? -1 : reader.GetInt32(4);
        var flag = diffSec >= 0 && diffSec < 5 ? "🔴 疑似誤判" : "🟡 正常中斷";
        Console.WriteLine($"  {flag} | Start: {ts} → DoorInterrupt: {nextTs} (Δ {diffSec}s) | {det}");
    }
    if (!found)
        Console.WriteLine("  ✅ 未發現 UV Start 後出現 DoorInterrupted 的紀錄");
}

// 4) 統計
Console.WriteLine("\n=== UV 事件統計 ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"
        SELECT EventCode, COUNT(*) as Cnt
        FROM SystemEvent
        WHERE EventCode IN ('INF-3001','WRN-3002','INF-3003','ERR-3004')
        GROUP BY EventCode
        ORDER BY EventCode";

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var code = reader.GetString(0);
        var label = code switch {
            "INF-3001" => "UV Start",
            "WRN-3002" => "UV Stop (user)",
            "INF-3003" => "UV Complete",
            "ERR-3004" => "Door Interrupted",
            _ => code
        };
        Console.WriteLine($"  {code} ({label}): {reader.GetInt32(1)} 筆");
    }
}
