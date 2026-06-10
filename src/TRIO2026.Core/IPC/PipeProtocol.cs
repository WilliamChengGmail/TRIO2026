using System.Text.Json;
using System.Text.Json.Serialization;

namespace TRIO2026.Core.IPC;

/// <summary>
/// TRIO2026 特權服務 IPC 協議定義
/// 
/// App (標準使用者) ↔ PrivilegedService (SYSTEM) 之間的通訊協議。
/// 使用 Named Pipe 傳輸 JSON 序列化的請求與回應。
/// 
/// 支援的命令：
///   - FormatDrive: USB 隨身碟快速格式化
///   - RestartPnp: PnP 裝置重啟（模擬 USB 拔插）
///   - Ping: 健康檢查（確認 Service 是否在線）
///   - (未來) UvControl: UV 燈硬體控制
/// 
/// 製作者: Office of William
/// </summary>
public static class PipeProtocol
{
    /// <summary>Named Pipe 名稱（App 與 Service 必須一致）</summary>
    public const string PipeName = "TRIO2026_PrivilegedService";

    /// <summary>協議版本（用於未來升級相容性檢查）</summary>
    public const int ProtocolVersion = 1;

    /// <summary>序列化選項（統一使用 camelCase）</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>IPC 命令類型</summary>
public enum PipeCommand
{
    /// <summary>健康檢查</summary>
    Ping = 0,

    /// <summary>USB 隨身碟快速格式化</summary>
    FormatDrive = 1,

    /// <summary>PnP 裝置重啟（模擬 USB 拔插）</summary>
    RestartPnp = 2,

    /// <summary>UV 燈硬體控制（預留）</summary>
    UvControl = 10,
}

/// <summary>
/// IPC 請求訊息
/// App → PrivilegedService
/// </summary>
public class PipeRequest
{
    /// <summary>協議版本</summary>
    public int Version { get; set; } = PipeProtocol.ProtocolVersion;

    /// <summary>命令類型</summary>
    public PipeCommand Command { get; set; }

    /// <summary>磁碟代號（FormatDrive 用，例如 "E:"）</summary>
    public string? DriveLetter { get; set; }

    /// <summary>目標檔案系統（FormatDrive 用，例如 "exFAT"）</summary>
    public string? FileSystem { get; set; }

    /// <summary>PnP 裝置 InstanceId（RestartPnp 用）</summary>
    public string? InstanceId { get; set; }

    /// <summary>呼叫端使用者名稱（稽核用）</summary>
    public string? CallerUser { get; set; }

    /// <summary>請求識別碼（用於日誌追蹤）</summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>序列化為 JSON</summary>
    public string ToJson() => JsonSerializer.Serialize(this, PipeProtocol.JsonOptions);

    /// <summary>從 JSON 反序列化</summary>
    public static PipeRequest? FromJson(string json) =>
        JsonSerializer.Deserialize<PipeRequest>(json, PipeProtocol.JsonOptions);
}

/// <summary>
/// IPC 回應訊息
/// PrivilegedService → App
/// </summary>
public class PipeResponse
{
    /// <summary>操作是否成功</summary>
    public bool Success { get; set; }

    /// <summary>輸出訊息（成功時的詳細資訊）</summary>
    public string? Output { get; set; }

    /// <summary>錯誤訊息（失敗時的原因）</summary>
    public string? Error { get; set; }

    /// <summary>原始請求的 RequestId（用於日誌對照）</summary>
    public string? RequestId { get; set; }

    /// <summary>序列化為 JSON</summary>
    public string ToJson() => JsonSerializer.Serialize(this, PipeProtocol.JsonOptions);

    /// <summary>從 JSON 反序列化</summary>
    public static PipeResponse? FromJson(string json) =>
        JsonSerializer.Deserialize<PipeResponse>(json, PipeProtocol.JsonOptions);

    /// <summary>快速建立成功回應</summary>
    public static PipeResponse Ok(string? requestId, string output) =>
        new() { Success = true, Output = output, RequestId = requestId };

    /// <summary>快速建立失敗回應</summary>
    public static PipeResponse Fail(string? requestId, string error) =>
        new() { Success = false, Error = error, RequestId = requestId };
}
