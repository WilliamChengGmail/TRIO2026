using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

// ══════════════════════════════════════════════════════════════
// TRIO2026 帳號密碼測試工具（開發階段專用，不部署）
// 
// 讀取 main.db 的 User 表
// 功能：
//   [1] 列出所有帳號
//   [2] 驗證密碼（輸入帳號+密碼 → 驗證是否正確）
//   [3] 重設密碼（輸入帳號+新密碼 → 更新 DB）
//   [4] 清除鎖定（重設失敗次數與鎖定時間）
// 
// 製作者: Office of William
// ══════════════════════════════════════════════════════════════

var dbName = "main.db";
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

while (true)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("  ╔══════════════════════════════════════╗");
    Console.WriteLine("  ║   TRIO2026 Account Tool              ║");
    Console.WriteLine("  ╠══════════════════════════════════════╣");
    Console.WriteLine("  ║   [1]  列出所有帳號                  ║");
    Console.WriteLine("  ║   [2]  驗證密碼                      ║");
    Console.WriteLine("  ║   [3]  重設密碼                      ║");
    Console.WriteLine("  ║   [4]  清除鎖定                      ║");
    Console.WriteLine("  ║   [0]  離開                          ║");
    Console.WriteLine("  ╚══════════════════════════════════════╝");
    Console.ResetColor();
    Console.Write("\n  請選擇 [0-4]: ");
    var choice = Console.ReadLine()?.Trim() ?? "";

    switch (choice)
    {
        case "1": ListAccounts(); break;
        case "2": VerifyPassword(); break;
        case "3": ResetPassword(); break;
        case "4": ClearLock(); break;
        case "0": return;
        default: Console.WriteLine("  無效選項"); break;
    }
}

// ── 列出所有帳號 ──
void ListAccounts()
{
    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT Id, Username, DisplayName, RoleLevel, IsActive, 
               ForcePasswordChange, FailedLoginCount, LockedUntil,
               LastLoginAt, Notes
        FROM User
        ORDER BY Id";

    var reader = cmd.ExecuteReader();

    var roleName = new Dictionary<int, string> { [1] = "Operator", [2] = "Service", [3] = "Admin" };

    // 先收集所有帳號資料
    var accounts = new List<(int Id, string Username, string Display, int Level, bool Active,
        bool ForceChange, int FailedCount, string Locked, string LastLogin, string Notes)>();

    while (reader.Read())
    {
        accounts.Add((
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4) == 1,
            !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
            reader.GetInt32(6),
            !reader.IsDBNull(7) ? reader.GetString(7) : "",
            reader.IsDBNull(8) ? "-" : reader.GetString(8),
            reader.IsDBNull(9) ? "" : reader.GetString(9)
        ));
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  帳號總數: {accounts.Count}");
    Console.ResetColor();
    Console.WriteLine();

    foreach (var a in accounts)
    {
        var role = roleName.GetValueOrDefault(a.Level, $"Level {a.Level}");
        var lastLogin = FormatToLocal(a.LastLogin);

        // 帳號標題列
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  ── ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"[{a.Id}] {a.Username}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" ── ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(a.Display);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" ──");

        // 詳細資訊
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("     角色: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{role} (Level {a.Level})");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("   狀態: ");
        Console.ForegroundColor = a.Active ? ConsoleColor.Green : ConsoleColor.Red;
        Console.Write(a.Active ? "啟用" : "停用");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("   需改密碼: ");
        Console.ForegroundColor = a.ForceChange ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
        Console.WriteLine(a.ForceChange ? "是" : "否");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write("     上次登入: ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(lastLogin);

        // 鎖定/失敗次數
        if (!string.IsNullOrEmpty(a.Locked) || a.FailedCount > 0)
        {
            if (!string.IsNullOrEmpty(a.Locked))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     !! 鎖定原始值 (UTC): {a.Locked}");

                if (DateTime.TryParse(a.Locked, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lockedUtc))
                {
                    var lockedLocal = lockedUtc.ToLocalTime();
                    var isStillLocked = DateTime.UtcNow < lockedUtc;

                    Console.ForegroundColor = isStillLocked ? ConsoleColor.Red : ConsoleColor.DarkYellow;
                    var offset = TimeZoneInfo.Local.GetUtcOffset(lockedLocal);
                    var tzStr = $"{(offset >= TimeSpan.Zero ? "+" : "-")}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
                    Console.Write($"     !! 鎖定至 (本地): {lockedLocal:yyyy-MM-dd HH:mm:ss} ({tzStr})");

                    if (isStillLocked)
                    {
                        var remaining = (int)(lockedUtc - DateTime.UtcNow).TotalMinutes;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ← 鎖定中（剩餘 {remaining} 分鐘）");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("  ← 已過期（下次登入自動解鎖）");
                    }
                }
            }

            if (a.FailedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     !! 連續登入失敗 {a.FailedCount} 次");
            }
        }

        // 備註
        if (!string.IsNullOrEmpty(a.Notes))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"     備註: {a.Notes}");
        }

        Console.ResetColor();
    }

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("\n  ────────────────────────────────────────");
    Console.ResetColor();
}

// ── 驗證密碼 ──
void VerifyPassword()
{
    Console.Write("\n  輸入帳號: ");
    var username = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(username)) return;

    Console.Write("  輸入密碼: ");
    var password = ReadPasswordMasked();

    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT PasswordHash, RoleLevel, DisplayName FROM [User] WHERE Username = @u";
    cmd.Parameters.AddWithValue("@u", username);

    var reader = cmd.ExecuteReader();
    if (!reader.Read())
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n  ❌ 找不到帳號: {username}");
        Console.ResetColor();
        return;
    }

    var hash = reader.GetString(0);
    var level = reader.GetInt32(1);
    var display = reader.IsDBNull(2) ? "" : reader.GetString(2);

    // 免登入帳號無密碼
    if (string.IsNullOrEmpty(hash))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  ⚠ 此帳號無密碼（免登入專用帳號）");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"     帳號: {username} ({display})");
        Console.ResetColor();
        return;
    }

    try
    {
        var valid = BCrypt.Net.BCrypt.Verify(password, hash);
        if (valid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✅ 密碼正確！");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"     帳號: {username} ({display})");
            Console.WriteLine($"     角色: RoleLevel={level}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  ❌ 密碼錯誤");
        }
    }
    catch
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n  ❌ 密碼雜湊格式異常（可能未正確初始化）");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"     Hash: {hash[..Math.Min(30, hash.Length)]}...");
    }
    Console.ResetColor();
}

// ── 重設密碼 ──
void ResetPassword()
{
    Console.Write("\n  輸入帳號: ");
    var username = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrEmpty(username)) return;

    Console.Write("  輸入新密碼: ");
    var newPassword = ReadPasswordMasked();
    if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 4)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n  ❌ 密碼太短（至少 4 字元）");
        Console.ResetColor();
        return;
    }

    Console.Write("  確認新密碼: ");
    var confirm = ReadPasswordMasked();
    if (newPassword != confirm)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n  ❌ 兩次輸入不一致");
        Console.ResetColor();
        return;
    }

    var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);

    using var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"UPDATE [User] 
        SET PasswordHash = @hash, ForcePasswordChange = 0, FailedLoginCount = 0, LockedUntil = NULL,
            PasswordChangedAt = @now, UpdatedAt = @now, UpdatedBy = 'AccountTool'
        WHERE Username = @u";
    cmd.Parameters.AddWithValue("@hash", newHash);
    cmd.Parameters.AddWithValue("@u", username);
    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

    var affected = cmd.ExecuteNonQuery();
    if (affected > 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n  ✅ 密碼已重設！帳號: {username}");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"     (鎖定已清除, ForcePasswordChange 已關閉)");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n  ❌ 找不到帳號: {username}");
    }
    Console.ResetColor();
}

// ── 清除鎖定 ──
void ClearLock()
{
    Console.Write("\n  輸入帳號 (留空=全部解鎖): ");
    var username = Console.ReadLine()?.Trim() ?? "";

    using var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();

    var cmd = conn.CreateCommand();

    if (string.IsNullOrEmpty(username))
    {
        // 全部解鎖
        cmd.CommandText = @"UPDATE [User] 
            SET FailedLoginCount = 0, LockedUntil = NULL,
                UpdatedAt = @now, UpdatedBy = 'AccountTool'
            WHERE FailedLoginCount > 0 OR LockedUntil IS NOT NULL";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        var affected = cmd.ExecuteNonQuery();
        if (affected > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✅ 已清除 {affected} 個帳號的鎖定狀態");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n  目前沒有帳號被鎖定或有失敗記錄");
        }
    }
    else
    {
        // 指定帳號解鎖
        cmd.CommandText = @"UPDATE [User] 
            SET FailedLoginCount = 0, LockedUntil = NULL,
                UpdatedAt = @now, UpdatedBy = 'AccountTool'
            WHERE Username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        var affected = cmd.ExecuteNonQuery();
        if (affected > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✅ 已清除鎖定！帳號: {username}");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"     (FailedLoginCount=0, LockedUntil=NULL)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  ❌ 找不到帳號: {username}");
        }
    }
    Console.ResetColor();
}

// ── 密碼遮罩輸入 ──
string ReadPasswordMasked()
{
    var pwd = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (pwd.Count > 0)
            {
                pwd.RemoveAt(pwd.Count - 1);
                Console.Write("\b \b");
            }
        }
        else
        {
            pwd.Add(key.KeyChar);
            Console.Write("*");
        }
    }
    Console.WriteLine();
    return new string(pwd.ToArray());
}

// ── UTC → 本地時間格式化 ──
string FormatToLocal(string isoUtc)
{
    if (string.IsNullOrEmpty(isoUtc) || isoUtc == "-") return "-";
    if (DateTime.TryParse(isoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
    {
        var local = dt.ToLocalTime();
        var offset = TimeZoneInfo.Local.GetUtcOffset(local);
        var sign = offset >= TimeSpan.Zero ? "+" : "-";
        return $"{local:yyyy-MM-dd HH:mm:ss} ({sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2})";
    }
    return isoUtc;
}
