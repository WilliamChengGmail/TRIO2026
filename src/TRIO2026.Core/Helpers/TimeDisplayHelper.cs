using System.Globalization;

namespace TRIO2026.Core.Helpers;

/// <summary>
/// 時間顯示工具 — 統一 UTC → 本地時間的轉換格式
/// 
/// 設計原則：
///   - DB 儲存永遠用 UTC (ISO 8601)
///   - UI 顯示永遠轉換為系統本地時間
///   - 本地時間附帶時區偏移量，方便研發人員跨時區識別
/// 
/// 製作者: Office of William
/// </summary>
public static class TimeDisplayHelper
{
    /// <summary>
    /// 將 ISO 8601 UTC 字串轉換為本地時間顯示格式
    /// </summary>
    /// <param name="iso8601">ISO 8601 格式的 UTC 時間字串</param>
    /// <returns>本地時間字串，如 "2026-06-03 14:30:00 (+08:00)"</returns>
    public static string ToLocalDisplay(string? iso8601)
    {
        if (string.IsNullOrEmpty(iso8601)) return "-";

        if (DateTimeOffset.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out var dto))
        {
            var local = dto.ToLocalTime();
            return local.ToString("yyyy-MM-dd HH:mm:ss") + $" ({local:zzz})";
        }

        // fallback: 嘗試 DateTime 解析
        if (DateTime.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out var dt))
        {
            var local = dt.ToLocalTime();
            var offset = TimeZoneInfo.Local.GetUtcOffset(local);
            var sign = offset >= TimeSpan.Zero ? "+" : "-";
            return local.ToString("yyyy-MM-dd HH:mm:ss") +
                   $" ({sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2})";
        }

        return iso8601; // 無法解析時原樣回傳
    }

    /// <summary>
    /// 將 ISO 8601 UTC 字串轉換為簡短本地時間（不含時區）
    /// </summary>
    /// <param name="iso8601">ISO 8601 格式的 UTC 時間字串</param>
    /// <returns>本地時間字串，如 "2026-06-03 14:30:00"</returns>
    public static string ToLocalDisplayShort(string? iso8601)
    {
        if (string.IsNullOrEmpty(iso8601)) return "-";

        if (DateTime.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        return iso8601;
    }

    /// <summary>
    /// 產生本地時間字串（含毫秒與時區偏移）— 用於 EventLog TimestampLocal
    /// </summary>
    /// <param name="utcNow">UTC 時間</param>
    /// <returns>如 "2026-06-03 14:30:00.123 (+08:00)"</returns>
    public static string ToLocalTimestampWithOffset(DateTimeOffset utcNow)
    {
        var local = utcNow.ToLocalTime();
        return local.ToString("yyyy-MM-dd HH:mm:ss.fff") + $" ({local:zzz})";
    }
}
