namespace TRIO2026.Core.Interfaces;

/// <summary>
/// UV 燈硬體控制與門板感測介面
/// 
/// 此介面將底層硬體通訊（預設 Modbus）與 UI 邏輯解耦。
/// 目前由 MockUvHardwareService 模擬實作，
/// 待底層韌體團隊完成後替換為真實實作並注入即可。
/// 
/// 事件驅動設計：
///   - DoorOpened / DoorClosed 由底層 sensor 觸發
///   - UI 層訂閱事件後執行暫停/恢復邏輯
/// </summary>
public interface IUvHardwareService
{
    /// <summary>啟動 UV 燈</summary>
    /// <param name="durationSeconds">照射時長（秒）</param>
    /// <returns>true=啟動成功, false=啟動失敗</returns>
    Task<bool> StartUvLampAsync(int durationSeconds);

    /// <summary>停止 UV 燈</summary>
    /// <returns>true=停止成功, false=停止失敗</returns>
    Task<bool> StopUvLampAsync();

    /// <summary>主動查詢當前門板狀態（true=開啟, false=關閉）</summary>
    bool IsDoorOpen { get; }

    /// <summary>門板開啟事件（底層 sensor 中斷通知 UI）</summary>
    event EventHandler? DoorOpened;

    /// <summary>門板關閉事件（底層 sensor 通知門板已關閉，可恢復 UV）</summary>
    event EventHandler? DoorClosed;
}
