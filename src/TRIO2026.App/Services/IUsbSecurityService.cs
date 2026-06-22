using System;
using System.Threading.Tasks;
using TRIO2026.App.Models;

namespace TRIO2026.App.Services;

public interface IUsbSecurityService
{
    /// <summary>啟動 USB 事件監聽（通常在 AppShell 或 Bootstrapper 啟動）</summary>
    void StartListening();

    /// <summary>停止 USB 事件監聽</summary>
    void StopListening();

    /// <summary>事件：當偵測到隨身碟，且需要顯示格式化確認面板時觸發</summary>
    event EventHandler<UsbDeviceInfo> FormatRequired;

    /// <summary>由 UI 呼叫：回報格式化確認結果，決定是否執行格式化並處理下一個佇列</summary>
    /// <param name="info">隨身碟資訊</param>
    /// <param name="confirmed">是否確認執行格式化</param>
    /// <param name="reason">取消原因（使用者主動取消傳 null 或空字串；系統強制取消傳 SessionLock 等）</param>
    Task ReportFormatResultAsync(UsbDeviceInfo info, bool confirmed, string? reason = null);

    /// <summary>掃描指定的隨身碟內容，依據設定的黑白名單判斷是否有風險檔案</summary>
    Task<bool> ScanDeviceContentAsync(UsbDeviceInfo info);

    /// <summary>對指定隨身碟執行快速格式化（exFAT）</summary>
    /// <returns>(成功, 輸出訊息)</returns>
    Task<(bool Success, string Output)> FormatDriveAsync(UsbDeviceInfo info);
}
