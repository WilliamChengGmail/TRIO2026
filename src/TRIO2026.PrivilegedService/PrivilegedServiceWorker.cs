using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using TRIO2026.Core.IPC;
using TRIO2026.PrivilegedService.Handlers;

namespace TRIO2026.PrivilegedService;

/// <summary>
/// Named Pipe Server Worker — 監聽來自 TRIO2026.App 的 IPC 請求
/// 
/// 安全性設計：
///   - Pipe ACL 限制僅允許本機使用者連線（拒絕網路存取）
///   - 每個請求都經過 Command 白名單驗證
///   - 所有操作都寫入日誌（Console / EventLog）
/// 
/// 製作者: Office of William
/// </summary>
public class PrivilegedServiceWorker : BackgroundService
{
    private readonly ILogger<PrivilegedServiceWorker> _logger;

    public PrivilegedServiceWorker(ILogger<PrivilegedServiceWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TRIO2026 PrivilegedService starting. PipeName={PipeName}", PipeProtocol.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 建立 Named Pipe Server（含 ACL 安全設定）
                var pipeSecurity = CreatePipeSecurity();

                await using var pipeServer = NamedPipeServerStreamAcl.Create(
                    PipeProtocol.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 4096,
                    outBufferSize: 4096,
                    pipeSecurity);

                _logger.LogDebug("Waiting for client connection...");
                await pipeServer.WaitForConnectionAsync(stoppingToken);

                _logger.LogInformation("Client connected. Processing request...");
                await HandleClientAsync(pipeServer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常關閉
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipe server loop");
                // 短暫等待後重試，避免錯誤迴圈
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("TRIO2026 PrivilegedService stopped.");
    }

    /// <summary>處理單一客戶端連線</summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            // 讀取請求
            var buffer = new byte[4096];
            var requestBuilder = new StringBuilder();

            while (true)
            {
                int bytesRead = await pipe.ReadAsync(buffer, ct);
                if (bytesRead == 0) return;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                requestBuilder.Append(chunk);

                if (chunk.Contains('\n')) break;
            }

            string requestJson = requestBuilder.ToString().Trim();
            _logger.LogDebug("Received: {Request}", requestJson);

            var request = PipeRequest.FromJson(requestJson);
            PipeResponse response;

            if (request == null)
            {
                response = PipeResponse.Fail(null, "Invalid request format");
                _logger.LogWarning("Invalid request received: {Raw}", requestJson[..Math.Min(200, requestJson.Length)]);
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Command={Command}, Caller={Caller}",
                    request.RequestId, request.Command, request.CallerUser ?? "(unknown)");

                response = await DispatchCommandAsync(request);

                _logger.LogInformation("[{RequestId}] Result={Success}, Output={Output}",
                    request.RequestId, response.Success,
                    (response.Output ?? response.Error)?[..Math.Min(200, (response.Output ?? response.Error ?? "").Length)]);
            }

            // 發送回應
            string responseJson = response.ToJson() + "\n";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
            await pipe.WriteAsync(responseBytes, ct);
            await pipe.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client request");
        }
    }

    /// <summary>根據命令類型分派處理</summary>
    private async Task<PipeResponse> DispatchCommandAsync(PipeRequest request)
    {
        return request.Command switch
        {
            PipeCommand.Ping => PipeResponse.Ok(request.RequestId, "Pong"),

            PipeCommand.FormatDrive => FormatHandler.Handle(request, _logger),

            PipeCommand.RestartPnp => await PnpHandler.HandleAsync(request, _logger),

            _ => PipeResponse.Fail(request.RequestId, $"Unknown command: {request.Command}")
        };
    }

    /// <summary>
    /// 建立 Named Pipe ACL 安全設定
    /// - 允許本機所有使用者連線（Interactive Users）
    /// - 拒絕網路存取（Network Users）
    /// </summary>
    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();

        // 允許本機互動式使用者（KioskUser）讀寫
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // 允許 SYSTEM（Service 自身）完全控制
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // 明確拒絕網路使用者（Air-gapped 防護）
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Deny));

        return security;
    }
}
