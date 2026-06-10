using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TRIO2026.Core.IPC;

namespace TRIO2026.PrivilegedService.Handlers;

/// <summary>
/// USB 格式化處理器 — 在特權 Service 中執行 format 指令
/// 
/// 安全性設計（雙重防護，與 App 端獨立驗證）：
///   - driveLetter 經 Regex 白名單驗證（僅允許 "X:" 格式）
///   - fileSystem 經白名單驗證（僅允許 exFAT/FAT32/NTFS）
///   - stdout/stderr 擷取並截斷，避免回應過大
///   - 所有操作寫入日誌
/// 
/// 製作者: Office of William
/// </summary>
public static class FormatHandler
{
    private static readonly string[] AllowedFileSystems = { "exFAT", "FAT32", "NTFS" };

    public static PipeResponse Handle(PipeRequest request, ILogger logger)
    {
        string driveLetter = request.DriveLetter ?? "";
        string fileSystem = request.FileSystem ?? "";

        // ── 安全性驗證（Service 端獨立驗證，不信任 App 端） ──

        if (!Regex.IsMatch(driveLetter, @"^[A-Za-z]:$"))
        {
            logger.LogWarning("[{RequestId}] SECURITY BLOCK: Invalid driveLetter '{DriveLetter}'",
                request.RequestId, driveLetter);
            return PipeResponse.Fail(request.RequestId,
                $"SecurityBlock: Invalid driveLetter format '{driveLetter}'");
        }

        if (!AllowedFileSystems.Contains(fileSystem, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("[{RequestId}] SECURITY BLOCK: Invalid fileSystem '{FileSystem}'",
                request.RequestId, fileSystem);
            return PipeResponse.Fail(request.RequestId,
                $"SecurityBlock: Invalid fileSystem '{fileSystem}'");
        }

        // ── 執行格式化 ──

        try
        {
            string cmdArgs = $"/c format {driveLetter} /FS:{fileSystem} /Q /Y";
            logger.LogInformation("[{RequestId}] Executing: cmd.exe {Args}, Caller={Caller}",
                request.RequestId, cmdArgs, request.CallerUser ?? "(unknown)");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdArgs,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(950), // Big5 (繁體中文 Windows)
                StandardErrorEncoding = Encoding.GetEncoding(950)
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return PipeResponse.Fail(request.RequestId, "Process.Start returned null");
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            // 30 秒逾時防護
            bool exited = process.WaitForExit(30_000);
            if (!exited)
            {
                try { process.Kill(); } catch { }
                logger.LogWarning("[{RequestId}] Format process timed out", request.RequestId);
                return PipeResponse.Fail(request.RequestId, "Format process timed out after 30 seconds");
            }

            string output = $"ExitCode={process.ExitCode}, Cmd=cmd.exe {cmdArgs}, Stdout={stdout.Trim()}, Stderr={stderr.Trim()}";

            // 截斷過長的輸出
            if (output.Length > 500)
                output = output[..500] + "...";

            if (process.ExitCode == 0)
            {
                logger.LogInformation("[{RequestId}] Format SUCCESS: {Output}",
                    request.RequestId, output);
                return PipeResponse.Ok(request.RequestId, output);
            }
            else
            {
                logger.LogWarning("[{RequestId}] Format FAILED: {Output}",
                    request.RequestId, output);
                return PipeResponse.Fail(request.RequestId, output);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{RequestId}] Format exception", request.RequestId);
            return PipeResponse.Fail(request.RequestId, $"Exception: {ex.Message}");
        }
    }
}
