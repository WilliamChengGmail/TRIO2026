using System.IO.Pipes;
using System.Text;

namespace TRIO2026.Core.IPC;

/// <summary>
/// Named Pipe Client — App 端用於與 PrivilegedService 通訊
/// 
/// 使用方式：
///   var response = await PipeClient.SendRequestAsync(new PipeRequest { Command = PipeCommand.Ping });
///   if (response.Success) { ... }
/// 
/// 安全性：
///   - 僅連線至本機 Named Pipe（不支援遠端）
///   - 逾時機制防止永久等待
///   - 回應長度限制防止記憶體攻擊
/// 
/// 製作者: Office of William
/// </summary>
public static class PipeClient
{
    /// <summary>回應最大長度（防止惡意 Service 回傳過大資料）</summary>
    private const int MaxResponseLength = 64 * 1024; // 64 KB

    /// <summary>
    /// 檢查 PrivilegedService 是否在線
    /// </summary>
    /// <param name="timeoutMs">連線逾時（毫秒）</param>
    /// <returns>true = Service 可用</returns>
    public static async Task<bool> IsServiceAvailableAsync(int timeoutMs = 2000)
    {
        try
        {
            var response = await SendRequestAsync(
                new PipeRequest { Command = PipeCommand.Ping },
                timeoutMs);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 向 PrivilegedService 發送請求並等待回應
    /// </summary>
    /// <param name="request">請求內容</param>
    /// <param name="timeoutMs">逾時毫秒數（預設 30 秒）</param>
    /// <returns>Service 回應</returns>
    public static async Task<PipeResponse> SendRequestAsync(PipeRequest request, int timeoutMs = 30_000)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);

            // 建立 Named Pipe Client（僅本機連線）
            await using var pipe = new NamedPipeClientStream(
                ".",                           // 本機
                PipeProtocol.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // 連線至 Service
            await pipe.ConnectAsync(cts.Token);

            // 發送請求（UTF-8 JSON + 換行符作為訊息邊界）
            string requestJson = request.ToJson();
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson + "\n");
            await pipe.WriteAsync(requestBytes, cts.Token);
            await pipe.FlushAsync(cts.Token);

            // 讀取回應
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            int totalRead = 0;

            while (true)
            {
                int bytesRead = await pipe.ReadAsync(buffer, cts.Token);
                if (bytesRead == 0) break;

                totalRead += bytesRead;
                if (totalRead > MaxResponseLength)
                {
                    return PipeResponse.Fail(request.RequestId,
                        $"Response exceeded max length ({MaxResponseLength} bytes)");
                }

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                responseBuilder.Append(chunk);

                // 以換行符作為訊息邊界
                if (chunk.Contains('\n')) break;
            }

            string responseJson = responseBuilder.ToString().Trim();
            if (string.IsNullOrEmpty(responseJson))
            {
                return PipeResponse.Fail(request.RequestId, "Empty response from service");
            }

            return PipeResponse.FromJson(responseJson)
                   ?? PipeResponse.Fail(request.RequestId, "Failed to deserialize response");
        }
        catch (TimeoutException)
        {
            return PipeResponse.Fail(request.RequestId, "Connection to PrivilegedService timed out");
        }
        catch (OperationCanceledException)
        {
            return PipeResponse.Fail(request.RequestId, "Request cancelled (timeout)");
        }
        catch (IOException ex)
        {
            return PipeResponse.Fail(request.RequestId, $"Pipe IO error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return PipeResponse.Fail(request.RequestId, $"Unexpected error: {ex.Message}");
        }
    }
}
