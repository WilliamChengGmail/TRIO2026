using System.IO;
using TRIO2026.App.Views;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRIO2026.App.Services;
using TRIO2026.App.Views;
using TRIO2026.Core;
using TRIO2026.Core.Interfaces;
using TRIO2026.Data.Contexts;

namespace TRIO2026.App;

/// <summary>
/// WPF 應用程式入口 — DI 容器配置 + 啟動 AppShell（單一 Window）
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>Heartbeat 檔案路徑 — 存在表示 App 正在執行中</summary>
    private string? _heartbeatPath;
    private DateTime _appStartTime;
    private System.Windows.Threading.DispatcherTimer? _heartbeatTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 註冊非 Unicode 編碼（Big5 = 950）— format 等 cmd 指令在繁中 Windows 輸出 Big5
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // 全域 Dispatcher 例外處理（結構化日誌 + 顯示 ErrorId）
        DispatcherUnhandledException += (s, ex) =>
        {
            var errorId = ErrorCodes.UnhandledException;
            try
            {
                EventLogService.Instance?.LogException(
                    "System", "App", ex.Exception, errorId,
                    "Dispatcher 未處理例外");
            }
            catch { }

            var loc = LocalizationService.Instance;
            MessageBox.Show(
                $"Error ID: {errorId}\n\n" +
                $"{ex.Exception.Message}\n\n" +
                loc["Error.ReportHint"],
                loc["Error.Title"], MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // AppDomain 未處理例外
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            if (ex.ExceptionObject is Exception e2)
                EventLogService.Instance?.LogException(
                    "System", "App", e2, ErrorCodes.UnhandledException,
                    "AppDomain 未處理例外");
        };

        // ProcessExit — 工作管理員關閉、系統登出等非正常結束
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            try
            {
                EventLogService.Instance?.LogInfo("System", "App", ErrorCodes.AppShutdown,
                    "ProcessExit 偵測到程序結束");
                if (EventLogService.Instance is IDisposable d) d.Dispose();
            }
            catch { }

            // 清除 heartbeat + fallback 寫入檔案
            CleanupHeartbeat();
            WriteCrashFallbackLog("ProcessExit detected — possible forced shutdown");
        };

        // TaskScheduler 未觀察到的 Task 例外
        TaskScheduler.UnobservedTaskException += (s, ex) =>
        {
            EventLogService.Instance?.LogException(
                "System", "App", ex.Exception, ErrorCodes.UnhandledException,
                "Task 未觀察到的例外");
            ex.SetObserved();
        };

        // ── 提前解析模擬器參數 ──
        var simArgs = ParseSimulationArgs(e.Args);

        // ── 透過極速直連 SQLite 預先查出上一次的主題設定，確保啟動畫面第一秒即顯示正確主題 ──
        string preloadedTheme = "Dark"; // 預設為深色
        try
        {
            var baseDir = FindProjectRoot();
            var dbPath = Path.Combine(baseDir, "Database", "system_config.db");
            if (File.Exists(dbPath))
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM SystemSettings WHERE Category = 'UI' AND Key = 'Theme' LIMIT 1";
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    preloadedTheme = result.ToString() ?? "Dark";
                }
            }
        }
        catch { }

        // ── 預載查出的佈景主題 ──
        string preThemeUri = preloadedTheme == "Light" 
            ? "Themes/LightTheme.xaml" 
            : "Themes/DarkTheme.xaml";
        try
        {
            var defaultTheme = new ResourceDictionary { Source = new Uri(preThemeUri, UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(defaultTheme);
        }
        catch { }

        // ── 立即顯示 SplashWindow（消除初始化期間的黑畫面）──
        var splashWindow = new SplashWindow();
        var splashStartTime = DateTime.UtcNow;
        if (simArgs.Embedded)
        {
            // 模擬器嵌入模式：設定面板尺寸並置中顯示（不使用 off-screen 定位）
            // DevLauncher 會偵測到此視窗並用 SetParent 嵌入面板
            splashWindow.WindowState = WindowState.Normal;
            splashWindow.Topmost = false;
            splashWindow.ShowInTaskbar = false;
            if (simArgs.Width > 0) splashWindow.Width = simArgs.Width;
            if (simArgs.Height > 0) splashWindow.Height = simArgs.Height;
            splashWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else if (simArgs.Width > 0 && simArgs.Height > 0 && !simArgs.Fullscreen)
        {
            // 非嵌入模擬模式：以面板尺寸置中顯示
            splashWindow.WindowState = WindowState.Normal;
            splashWindow.Width = simArgs.Width;
            splashWindow.Height = simArgs.Height;
            splashWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        splashWindow.Show();
        splashWindow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => { }));

        // ── 啟動階段日誌（必須在 try 外宣告，確保 catch 也能寫入）──
        TRIO2026.Data.Extensions.StartupLogger? startupLog = null;

        try
        {
            // 取得專案根目錄（Database/ 所在位置）
            var baseDir = FindProjectRoot();
            var dbDir = Path.Combine(baseDir, "Database");

            var startupLogDir = Path.Combine(baseDir, "Logs", "startup-init-logs");
            startupLog = new TRIO2026.Data.Extensions.StartupLogger(startupLogDir);
            startupLog.Info("App", "應用程式啟動", $"BaseDir={baseDir}");

            splashWindow.UpdateStatus("Initializing database...");

            // 確保 Database 目錄存在
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            // ── DB 初始化（獨立日誌，寫到 db-init-logs/）──
            var dbInitLogDir = Path.Combine(baseDir, "Logs", "db-init-logs");
            using var dbInitLog = new TRIO2026.Data.Extensions.StartupLogger(dbInitLogDir);
            dbInitLog.Info("DbInit", "DB 初始化開始", $"DatabaseDir={dbDir}");

            TRIO2026.Data.Extensions.DatabaseInitializer.SetDatabaseDirectory(dbDir);
            TRIO2026.Data.Extensions.DatabaseInitializer.PasswordHasher =
                pw => AuthService.HashPassword(pw);
            TRIO2026.Data.Extensions.DatabaseInitializer.InitializeAllAsync()
                .GetAwaiter().GetResult();

            dbInitLog.Info("DbInit", "DB 初始化完成");

            // 切回 App 啟動日誌
            TRIO2026.Data.Extensions.StartupLogger.Current = startupLog;
            startupLog.Info("App", "DB 初始化階段完成");

            splashWindow.UpdateStatus("Loading services...");

            // DI 容器
            var services = new ServiceCollection();

            // DbContext 註冊（新 DB）
            services.AddDbContext<SystemConfigDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(dbDir, "system_config.db")}"));
            services.AddDbContext<EventLogDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(dbDir, "system_event.db")}"));
            services.AddDbContext<AppMainDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(dbDir, "main.db")}"));
            services.AddDbContext<DataDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(dbDir, "data.db")}"));

            // Services
            services.AddSingleton<SessionService>();
            services.AddSingleton<TokenService>();
            services.AddSingleton<SystemSettingService>();
            services.AddTransient<AuthService>();
            services.AddTransient<PasswordPolicyService>();
            services.AddTransient<AccountManagementService>();

            // UV 相關服務
            services.AddSingleton<UvConfigService>();
            services.AddSingleton<IUvHardwareService, MockUvHardwareService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<EventLogService>();
            services.AddSingleton<EventLogArchiveService>();
            services.AddSingleton<IUsbSecurityService, UsbSecurityService>();

            _serviceProvider = services.BuildServiceProvider();

            // 初始化事件日誌服務
            var eventLog = _serviceProvider.GetRequiredService<EventLogService>();
            eventLog.SessionService = _serviceProvider.GetRequiredService<SessionService>();
            EventLogService.Instance = eventLog;

            // ── 偵測上次是否非正常關閉（heartbeat 檔案殘留）──
            _heartbeatPath = Path.Combine(baseDir, "Logs", ".app_running");
            DetectAbnormalShutdown(eventLog);

            // 寫入 heartbeat（標記 App 正在執行）
            WriteHeartbeat();

            // 啟動事件日誌延後到 sysSettings 載入後記錄（需讀取 UUID）

            splashWindow.UpdateStatus("Checking archives...");

            // 啟動歸檔檢查
            var archiveService = _serviceProvider.GetRequiredService<EventLogArchiveService>();
            archiveService.CheckAndArchiveAsync().GetAwaiter().GetResult();


            splashWindow.UpdateStatus("Loading settings...");

            // 載入系統設定（system_config.db）
            var sysSettings = _serviceProvider.GetRequiredService<SystemSettingService>();
            sysSettings.LoadAsync().GetAwaiter().GetResult();

            // 套用 UI 佈景主題
            string themeName = sysSettings.UITheme;
            string themeUri = themeName == "Light" 
                ? "Themes/LightTheme.xaml" 
                : "Themes/DarkTheme.xaml";
            try
            {
                var resourceDict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };
                // 清除之前的預載主題，避免重複或衝突
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(resourceDict);
            }
            catch (Exception ex)
            {
                startupLog.Error("App", $"載入佈景主題 {themeUri} 失敗", ex);
            }

            // 確保 Installation UUID 已產生（首次啟動自動產生 + 硬體/OS 快照，後續僅讀取）
            var installUuid = InstallationUuidService.EnsureUuid(sysSettings);
            eventLog.LogInfo("System", "App", ErrorCodes.AppStartup, "應用程式啟動",
                $"UUID={installUuid}, " + (startupLog.HasErrors
                    ? $"StartupLog=HasErrors, LogPath={startupLog.LogPath}"
                    : $"StartupLog=OK, LogPath={startupLog.LogPath}"));

            splashWindow.UpdateStatus("Starting security...");

            // 啟動 USB 安全服務監聽
            var usbSecurity = _serviceProvider.GetRequiredService<IUsbSecurityService>();
            usbSecurity.StartListening();

            // 初始化多語系服務（受 DB 開關控制）
            var locService = _serviceProvider.GetRequiredService<LocalizationService>();
            string defaultLang;
            if (!sysSettings.MultiLanguageEnabled)
            {
                defaultLang = "en";
            }
            else if (sysSettings.LoginRequired)
            {
                // 需要登入 → 根據 login_screen_language_mode 決定登入頁語系
                if (sysSettings.LoginScreenLanguageMode == "last_user")
                    defaultLang = sysSettings.LastUserLanguage ?? sysSettings.DefaultLanguage;
                else
                    defaultLang = sysSettings.DefaultLanguage;
            }
            else
            {
                // 不需要登入 → 讀取 local_operator 的語系偏好
                defaultLang = GetGuestUserLanguage(sysSettings) ?? sysSettings.DefaultLanguage;
            }
            var langDebug = $"[{DateTime.Now:HH:mm:ss}] MultiLang={sysSettings.MultiLanguageEnabled}, " +
                $"LoginRequired={sysSettings.LoginRequired}, Mode={sysSettings.LoginScreenLanguageMode}, " +
                $"LastUserLang={sysSettings.LastUserLanguage ?? "(null)"}, DefaultLang={sysSettings.DefaultLanguage}, " +
                $"Result={defaultLang}";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language_debug.txt"), langDebug);
            locService.InitializeAsync(defaultLang).GetAwaiter().GetResult();

            splashWindow.UpdateStatus("Starting application...");

            // 建立 AppShell
            var shell = new AppShell(
                _serviceProvider,
                _serviceProvider.GetRequiredService<SessionService>(),
                _serviceProvider.GetRequiredService<AuthService>(),
                _serviceProvider.GetRequiredService<TokenService>(),
                _serviceProvider.GetRequiredService<UvConfigService>(),
                _serviceProvider.GetRequiredService<IUvHardwareService>(),
                sysSettings);

            // 模擬器參數
            ApplySimArgs(shell, simArgs);

            // 先顯示 AppShell（SplashWindow Topmost 蓋在上方，使用者看不到 AppShell）
            shell.Show();

            // 計算剩餘等待時間（確保至少 3 秒）後淡出 SplashWindow
            var elapsed = DateTime.UtcNow - splashStartTime;
            var remaining = TimeSpan.FromSeconds(3) - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                // 等待剩餘時間後淡出
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = remaining };
                timer.Tick += (s2, e2) => { timer.Stop(); splashWindow.FadeOutAndClose(); };
                timer.Start();
            }
            else
            {
                splashWindow.FadeOutAndClose();
            }
        }
        catch (Exception ex)
        {
            try { splashWindow.Close(); } catch { }

            // 嘗試寫入 EventLog（若已初始化）
            try
            {
                EventLogService.Instance?.LogException(
                    "System", "App", ex, ErrorCodes.UnhandledException, "啟動失敗");
            }
            catch { }

            // 寫入 StartupLogger（EventLogService 不可用時的 fallback）
            startupLog?.Error("App", "啟動失敗", ex);

            // 確保日誌 flush 到 DB，避免 Shutdown 太快導致遺失
            try
            {
                if (EventLogService.Instance is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }

            MessageBox.Show(
                $"Error ID: {ErrorCodes.UnhandledException}\n\n" +
                $"{ex.Message}\n\n{ex.InnerException?.Message}",
                "TRIO2026 啟動錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        finally
        {
            startupLog?.Dispose();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 記錄關閉事件並 flush 日誌
        EventLogService.Instance?.LogInfo("System", "App", ErrorCodes.AppShutdown, "應用程式正常關閉");

        if (EventLogService.Instance is IDisposable disposable)
            disposable.Dispose();

        // 清除 heartbeat — 表示正常結束
        CleanupHeartbeat();

        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    /// <summary>偵測上次是否非正常關閉</summary>
    private void DetectAbnormalShutdown(EventLogService eventLog)
    {
        if (_heartbeatPath == null || !File.Exists(_heartbeatPath)) return;

        try
        {
            string content = File.ReadAllText(_heartbeatPath).Trim();
            eventLog.LogWarning("System", "App",
                ErrorCodes.AbnormalShutdownDetected,
                "Abnormal Shutdown Detected",
                $"PreviousSession={content}");
        }
        catch (Exception ex)
        {
            eventLog.LogWarning("System", "App",
                ErrorCodes.AbnormalShutdownDetected,
                "Abnormal Shutdown Detected",
                $"HeartbeatReadError={ex.Message}");
        }
        finally
        {
            try { File.Delete(_heartbeatPath); } catch { }
        }
    }

    /// <summary>寫入 heartbeat 檔案（標記 App 正在執行）並啟動定期更新</summary>
    private void WriteHeartbeat()
    {
        try
        {
            if (_heartbeatPath == null) return;
            var dir = Path.GetDirectoryName(_heartbeatPath);
            if (dir != null) Directory.CreateDirectory(dir);

            _appStartTime = DateTime.Now;
            UpdateHeartbeatFile();

            // 每 30 秒更新一次 LastAlive 時間戳
            _heartbeatTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _heartbeatTimer.Tick += (s, e) => UpdateHeartbeatFile();
            _heartbeatTimer.Start();
        }
        catch { }
    }

    /// <summary>更新 heartbeat 檔案內容（含最後存活時間）</summary>
    private void UpdateHeartbeatFile()
    {
        try
        {
            if (_heartbeatPath == null) return;
            var info = $"PID={Environment.ProcessId}, StartedAt={_appStartTime:yyyy-MM-dd HH:mm:ss}, LastAlive={DateTime.Now:yyyy-MM-dd HH:mm:ss}, Machine={Environment.MachineName}";
            File.WriteAllText(_heartbeatPath, info);
        }
        catch { }
    }

    /// <summary>清除 heartbeat 檔案並停止計時器（表示正常結束）</summary>
    private void CleanupHeartbeat()
    {
        try
        {
            _heartbeatTimer?.Stop();
            _heartbeatTimer = null;
            if (_heartbeatPath != null && File.Exists(_heartbeatPath))
                File.Delete(_heartbeatPath);
        }
        catch { }
    }

    /// <summary>Fallback: 當 DB 不可用時寫入本機檔案</summary>
    private void WriteCrashFallbackLog(string reason)
    {
        try
        {
            var logDir = Path.Combine(FindProjectRoot(), "Logs", "crash-logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {reason}\n" +
                          $"PID={Environment.ProcessId}\n" +
                          $"Machine={Environment.MachineName}\n";
            File.WriteAllText(logFile, content);
        }
        catch { }
    }

    /// <summary>
    /// 從 exe 位置向上尋找 Database/ 目錄所在的專案根目錄
    /// </summary>
    private static string FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "Database")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        // 回退: 使用 D:\TRIO2026（開發環境硬編碼）
        return @"D:\TRIO2026";
    }

    // ── 模擬器參數結構 ──
    private record SimArgs(int Width, int Height, bool Touch, bool Fullscreen, bool Embedded);

    private static SimArgs ParseSimulationArgs(string[] args)
    {
        int w = 0, h = 0;
        bool fs = false, touch = false, embed = false;
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--sim-width":    int.TryParse(args[i + 1], out w); break;
                case "--sim-height":   int.TryParse(args[i + 1], out h); break;
                case "--sim-touch":    touch = args[i + 1] == "1"; break;
                case "--sim-fullscreen": fs = args[i + 1] == "1"; break;
                case "--sim-embedded": embed = args[i + 1] == "1"; break;
            }
        }
        return new SimArgs(w, h, touch, fs, embed);
    }

    /// <summary>將模擬器參數套用到 AppShell</summary>
    private static void ApplySimArgs(Window window, SimArgs sim)
    {
        if (sim.Embedded)
        {
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.SizeToContent = SizeToContent.Manual;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;          // 初始位置在螢幕外，避免嵌入前閃爍
            window.Top = -10000;
            if (sim.Width > 0) window.Width = sim.Width;
            if (sim.Height > 0) window.Height = sim.Height;
            window.Title = "TRIO2026";
        }
        else if (sim.Width > 0 && sim.Height > 0 && !sim.Fullscreen)
        {
            window.WindowState = WindowState.Normal;
            window.Width = sim.Width;
            window.Height = sim.Height;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Title = $"TRIO2026 — 模擬模式 ({sim.Width}×{sim.Height}{(sim.Touch ? " 觸控" : "")})";
        }
    }

    /// <summary>
    /// 查詢免登入帳號（local_operator）的語系偏好
    /// </summary>
    private string? GetGuestUserLanguage(SystemSettingService sysSettings)
    {
        try
        {
            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppMainDbContext>();
            var username = sysSettings.GuestAccountUsername;
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            return string.IsNullOrEmpty(user?.LanguagePreference) ? null : user.LanguagePreference;
        }
        catch
        {
            return null;
        }
    }
}
