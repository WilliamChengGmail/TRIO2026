using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using TRIO2026.Core.IPC;

/// <summary>
/// PrivilegedService Named Pipe 測試工具
/// 
/// 用途：獨立驗證 Named Pipe IPC 通訊是否正常
/// 前提：TRIO2026.PrivilegedService 必須先以管理員身分啟動
/// 
/// 製作者: Office of William
/// </summary>

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║  TRIO2026 PrivilegedService IPC Test Tool    ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// Test 1: Ping
Console.Write("[Test 1] Ping ... ");
var pingResult = await PipeClient.SendRequestAsync(
    new PipeRequest { Command = PipeCommand.Ping }, timeoutMs: 5000);
if (pingResult.Success)
    Console.WriteLine($"✅ OK ({pingResult.Output})");
else
    Console.WriteLine($"❌ FAIL ({pingResult.Error})");

Console.WriteLine();

// Test 2: Format with invalid drive (should be blocked)
Console.Write("[Test 2] Format SecurityBlock (invalid drive) ... ");
var blockResult = await PipeClient.SendRequestAsync(
    new PipeRequest
    {
        Command = PipeCommand.FormatDrive,
        DriveLetter = "E: && malicious",  // Command injection attempt
        FileSystem = "exFAT",
        CallerUser = "TestTool"
    });
if (!blockResult.Success && (blockResult.Error?.Contains("SecurityBlock") == true))
    Console.WriteLine($"✅ Blocked correctly ({blockResult.Error})");
else
    Console.WriteLine($"❌ UNEXPECTED ({blockResult.Output ?? blockResult.Error})");

Console.WriteLine();

// Test 3: Format real USB (optional)
Console.Write("是否要測試真實的 USB 格式化？(輸入磁碟代號如 E: 或按 Enter 跳過): ");
string? input = Console.ReadLine()?.Trim();

if (!string.IsNullOrEmpty(input) && input.Length == 2 && input[1] == ':')
{
    Console.Write($"[Test 3] Format {input} /FS:exFAT /Q /Y ... ");
    var formatResult = await PipeClient.SendRequestAsync(
        new PipeRequest
        {
            Command = PipeCommand.FormatDrive,
            DriveLetter = input,
            FileSystem = "exFAT",
            CallerUser = "TestTool"
        });
    if (formatResult.Success)
        Console.WriteLine($"✅ SUCCESS");
    else
        Console.WriteLine($"❌ FAIL");
    Console.WriteLine($"   Output: {formatResult.Output ?? formatResult.Error}");
}

Console.WriteLine();
Console.Write("[Test 4] Service Available Check ... ");
bool available = await PipeClient.IsServiceAvailableAsync();
Console.WriteLine(available ? "✅ Service is running" : "❌ Service not reachable");

Console.WriteLine();
Console.WriteLine("測試完成。按任意鍵結束...");
Console.ReadKey();
