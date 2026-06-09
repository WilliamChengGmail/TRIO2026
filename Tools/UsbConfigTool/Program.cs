using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace UsbConfigTool;

class Program
{
    private static string _dbPath = "";

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("==============================================");
        Console.WriteLine("= TRIO2026 USB 專碟專用模組 設定與檢視工具   =");
        Console.WriteLine("= 作者: Office of William                    =");
        Console.WriteLine("==============================================");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // 開發環境中：目前在 tools/UsbConfigTool/bin/Debug/net8.0/
        // 要退回 src 同層的 Database/system_config.db
        _dbPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "Database", "system_config.db"));

        if (!File.Exists(_dbPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[錯誤] 找不到資料庫檔案: {_dbPath}");
            Console.WriteLine("請確認 TRIO2026 應用程式是否已至少執行過一次以初始化資料庫。");
            Console.ResetColor();
            Console.WriteLine("按任意鍵退出...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"已連接資料庫: {_dbPath}");
        Console.WriteLine();

        while (true)
        {
            var settings = LoadUsbSettings();
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("目前 USB 資安設定列表:");
            Console.ResetColor();
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine($"{"ID",-4} | {"設定鍵 (SettingKey)",-35} | 設定值 (SettingValue)");
            Console.WriteLine("------------------------------------------------------------");

            for (int i = 0; i < settings.Count; i++)
            {
                var s = settings[i];
                Console.WriteLine($"{i + 1,-4} | {s.Key,-35} | {s.Value}");
            }
            Console.WriteLine("------------------------------------------------------------");

            Console.WriteLine();
            Console.WriteLine("請輸入要修改的項目編號 (1~9)，輸入 R 重新整理，輸入 Q 離開:");
            string? input = Console.ReadLine()?.Trim().ToUpper();

            if (input == "Q") break;
            if (input == "R") continue;

            if (int.TryParse(input, out int index) && index >= 1 && index <= settings.Count)
            {
                var selected = settings[index - 1];
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"您選擇了: {selected.Key}");
                Console.WriteLine($"目前設定值: {selected.Value}");
                Console.ResetColor();
                Console.WriteLine("請輸入新的設定值 (直接按下 Enter 則取消變更):");

                string? newValue = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(newValue) && newValue != selected.Value)
                {
                    UpdateSetting(selected.Key, newValue);
                }
                else
                {
                    Console.WriteLine("已取消變更。");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("無效的輸入，請重新嘗試。");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }

    private static List<(string Key, string Value)> LoadUsbSettings()
    {
        var list = new List<(string, string)>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM SystemSetting WHERE Category = 'UsbSecurity' ORDER BY Id ASC";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((reader.GetString(0), reader.GetString(1)));
        }

        return list;
    }

    private static void UpdateSetting(string key, string value)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE SystemSetting SET Value = @val WHERE Key = @key";
            cmd.Parameters.AddWithValue("@val", value);
            cmd.Parameters.AddWithValue("@key", key);

            int rows = cmd.ExecuteNonQuery();
            if (rows > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[成功] 設定 {key} 已更新為: {value}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[失敗] 找不到設定鍵 {key} 或無異動。");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[例外] 寫入資料庫發生錯誤: {ex.Message}");
            Console.ResetColor();
        }
    }
}
