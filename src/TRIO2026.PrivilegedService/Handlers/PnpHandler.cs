using System.Diagnostics;
using TRIO2026.Core.IPC;

namespace TRIO2026.PrivilegedService.Handlers;

/// <summary>
/// PnP 裝置重啟處理器 — 在特權 Service 中執行裝置停用/啟用
/// 
/// 用於模擬 USB 拔插（觸發 WMI 事件重新偵測）
/// 
/// 製作者: Office of William
/// </summary>
public static class PnpHandler
{
    public static async Task<PipeResponse> HandleAsync(PipeRequest request, ILogger logger)
    {
        string instanceId = request.InstanceId ?? "";

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return PipeResponse.Fail(request.RequestId, "InstanceId is required for RestartPnp");
        }

        // 基本安全驗證：InstanceId 應以 USBSTOR 或 USB 開頭
        if (!instanceId.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[{RequestId}] SECURITY BLOCK: Invalid InstanceId '{InstanceId}'",
                request.RequestId, instanceId);
            return PipeResponse.Fail(request.RequestId,
                $"SecurityBlock: InstanceId must start with 'USB', got '{instanceId[..Math.Min(30, instanceId.Length)]}'");
        }

        try
        {
            logger.LogInformation("[{RequestId}] Restarting PnP device: {InstanceId}, Caller={Caller}",
                request.RequestId, instanceId, request.CallerUser ?? "(unknown)");

            // Step 1: Disable
            var disableResult = await RunPowerShellAsync(
                $"Disable-PnpDevice -InstanceId '{instanceId}' -Confirm:0");

            if (!disableResult.Success)
            {
                return PipeResponse.Fail(request.RequestId, $"Disable failed: {disableResult.Output}");
            }

            // 等待裝置狀態更新
            await Task.Delay(1500);

            // Step 2: Enable
            var enableResult = await RunPowerShellAsync(
                $"Enable-PnpDevice -InstanceId '{instanceId}' -Confirm:0");

            if (!enableResult.Success)
            {
                return PipeResponse.Fail(request.RequestId, $"Enable failed: {enableResult.Output}");
            }

            logger.LogInformation("[{RequestId}] PnP restart completed successfully", request.RequestId);
            return PipeResponse.Ok(request.RequestId, $"PnP device restarted: {instanceId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{RequestId}] PnP restart exception", request.RequestId);
            return PipeResponse.Fail(request.RequestId, $"Exception: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string Output)> RunPowerShellAsync(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process == null) return (false, "Process.Start returned null");

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        string output = $"ExitCode={process.ExitCode}, Stdout={stdout.Trim()}, Stderr={stderr.Trim()}";
        return (process.ExitCode == 0, output);
    }
}
