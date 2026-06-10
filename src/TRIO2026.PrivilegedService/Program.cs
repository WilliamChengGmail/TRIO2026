using TRIO2026.PrivilegedService;

/// <summary>
/// TRIO2026 特權服務 — Windows Service 入口
/// 
/// 以 LOCAL SYSTEM 權限執行，負責處理需要管理員權限的操作：
///   - USB 隨身碟格式化
///   - PnP 裝置控制
///   - (未來) UV 燈硬體通訊
/// 
/// 安裝方式：
///   sc.exe create TRIO2026.PrivilegedService binPath= "C:\TRIO2026\Service\TRIO2026.PrivilegedService.exe" start= auto obj= "LocalSystem"
/// 
/// 製作者: Office of William
/// </summary>
var builder = Host.CreateApplicationBuilder(args);

// 啟用 Windows Service 支援（可同時作為 Console App 和 Windows Service 執行）
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "TRIO2026.PrivilegedService";
});

// 註冊 Worker
builder.Services.AddHostedService<PrivilegedServiceWorker>();

var host = builder.Build();
host.Run();
