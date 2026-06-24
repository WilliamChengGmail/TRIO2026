namespace TRIO2026.Core.Entities;

/// <summary>
/// 系統設定（system_config.db — SystemSetting 表）
/// 
/// 通用 Key-Value 設定表，用於存放各種系統級參數。
/// Category 用來分組，Key 為設定名稱，Value 為設定值。
/// 
/// 設定分類：
///   - UserMenu       使用者選單相關設定
///   - System         系統全域設定（多語系、預設語系）
///   - EventLog       事件日誌歸檔設定
///   - LoginUI        登入介面設定
///   - Auth           認證設定（登入要求、免登入帳號）
///   - AppClose       關閉控制設定（ESC / Alt+F4 / 按鈕）
///   - Device         裝置運作模式設定
///   - PasswordPolicy 密碼原則設定（複雜度、長度）
///   - AccountMgmt    帳號管理設定（鎖定功能開關）
///   - UsbSecurity    USB 資安專碟專用設定
/// 
/// 製作者: Office of William
/// </summary>
public class SystemSetting
{
    /// <summary>主鍵</summary>
    public int Id { get; set; }

    /// <summary>設定分類（如 UserMenu, System）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>設定鍵值</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>設定值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>說明描述</summary>
    public string? Description { get; set; }

    /// <summary>備註（實作狀態追蹤等）</summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 是否唯讀（0=可修改，1=系統自動寫入不可修改）
    /// 執行時期動態偵測的硬體/OS 資料（Id 901+）標記為 1，
    /// 防止管理介面誤覆寫真實偵測值。
    /// </summary>
    public int IsReadOnly { get; set; } = 0;
}
