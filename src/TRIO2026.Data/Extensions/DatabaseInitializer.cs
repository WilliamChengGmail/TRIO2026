using Microsoft.EntityFrameworkCore;
using TRIO2026.Data.Contexts;
using TRIO2026.Data.Seeding;

namespace TRIO2026.Data.Extensions;

/// <summary>
/// 資料庫初始化器：建立資料庫檔案、套用表結構、設定 PRAGMA、植入種子資料。
/// 
/// 所有日誌透過 StartupLogger 寫入 startup.log，
/// 因為此階段 EventLogService 尚未初始化。
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>Database 目錄的根路徑</summary>
    private static string _databaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");

    /// <summary>
    /// 設定資料庫目錄的根路徑（預設為執行目錄下的 Database 子目錄）
    /// </summary>
    public static void SetDatabaseDirectory(string path)
    {
        _databaseDir = path;
    }

    /// <summary>
    /// 取得指定資料庫檔案的完整路徑
    /// </summary>
    public static string GetDatabasePath(string dbFileName)
    {
        return Path.Combine(_databaseDir, dbFileName);
    }

    /// <summary>
    /// 初始化全部四個資料庫：建立目錄、建表、設定 PRAGMA、植入種子資料
    /// </summary>
    /// <summary>
    /// 密碼雜湊函數（由外部注入，如 BCrypt.HashPassword）
    /// </summary>
    public static Func<string, string>? PasswordHasher { get; set; }

    public static async Task InitializeAllAsync()
    {
        var log = StartupLogger.Current;

        // 確保 Database 目錄存在
        Directory.CreateDirectory(_databaseDir);

        log?.Info("DatabaseInitializer", $"資料庫目錄: {_databaseDir}");

        // 載入或產生種子密碼（從外部檔案，不編譯進 DLL）
        var credentials = SeedCredentialProvider.LoadOrGenerate(_databaseDir);

        // 初始化資料庫
        await InitializeSystemConfigDbAsync();
        await InitializeAppMainDbAsync(credentials);
        await InitializeEventLogDbAsync();
        await InitializeDataDbAsync();

        // 初始化完成後安全銷毀密碼檔
        SeedCredentialProvider.DeleteCredentialFile(_databaseDir);

        log?.Info("DatabaseInitializer", "全部初始化完成");
    }

    // [已移除] InitializeConfigDbAsync — trio240plus_config.db 已廢棄

    /// <summary>初始化 SystemConfig DB（system_config.db）+ Seed Data</summary>
    private static async Task InitializeSystemConfigDbAsync()
    {
        const string dbFile = "system_config.db";
        var dbPath = GetDatabasePath(dbFile);
        var log = StartupLogger.Current;
        log?.Info("DbInit", $"初始化系統配置庫 ({dbFile})...");

        try
        {
            var options = new DbContextOptionsBuilder<SystemConfigDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new SystemConfigDbContext(options);
            await context.Database.MigrateAsync();
            log?.Info("DbInit", $"[{dbFile}] Migration 完成");

            await SetPragmasAsync(context);

            // 植入 UvTimerOption 種子資料
            if (!await context.UvTimerOptions.AnyAsync())
            {
                var uvSeeds = UvTimerOptionSeed.GetSeedData();
                context.UvTimerOptions.AddRange(uvSeeds);
                await context.SaveChangesAsync();
                log?.Info("DbInit", $"[{dbFile}] 已植入 {uvSeeds.Count} 筆 UV 照射時間選項");
            }
            else
            {
                log?.Info("DbInit", $"[{dbFile}] UV 照射時間選項已存在，跳過植入");
            }

            // 植入 LocalizedString 多語系種子資料（增量：只補入缺少的 key）
            {
                var i18nSeeds = LocalizedStringSeed.GetSeedData();
                var existingKeys = await context.LocalizedStrings
                    .Select(s => s.Module + "." + s.ResourceKey + "." + s.LanguageCode)
                    .ToListAsync();
                var existingSet = new HashSet<string>(existingKeys);

                var newSeeds = i18nSeeds
                    .Where(s => !existingSet.Contains(s.Module + "." + s.ResourceKey + "." + s.LanguageCode))
                    .ToList();

                if (newSeeds.Count > 0)
                {
                    // 重新分配 ID，避免主鍵衝突
                    var maxId = await context.LocalizedStrings.AnyAsync()
                        ? await context.LocalizedStrings.MaxAsync(s => s.Id)
                        : 0;
                    foreach (var seed in newSeeds)
                    {
                        seed.Id = ++maxId;
                    }

                    context.LocalizedStrings.AddRange(newSeeds);
                    await context.SaveChangesAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已補入 {newSeeds.Count} 筆多語系字串");
                }
                else
                {
                    log?.Info("DbInit", $"[{dbFile}] 多語系字串已是最新，無需補入");
                }
            }

            // ── Schema 升級：確保 IsReadOnly 欄位存在 ──
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                using var pragmaCmd = conn.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA table_info(SystemSetting)";

                var existingCols = new HashSet<string>();
                using (var reader = await pragmaCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingCols.Add(reader.GetString(1));
                }

                // 確保 Remark 欄位存在（舊版相容）
                if (!existingCols.Contains("Remark"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE SystemSetting ADD COLUMN Remark TEXT";
                    await alterCmd.ExecuteNonQueryAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已新增 Remark 欄位");
                }

                // 新增 IsReadOnly 欄位
                if (!existingCols.Contains("IsReadOnly"))
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE SystemSetting ADD COLUMN IsReadOnly INTEGER NOT NULL DEFAULT 0";
                    await alterCmd.ExecuteNonQueryAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已新增 IsReadOnly 欄位");
                }
            }

            // ── 資料遷移：舊 Id 54~65 的硬體/OS 偵測資料 → 901~912 ──
            // 若舊版 DB 中仍存在 Id 54~65 的執行時期偵測資料，遷移至正確 Id 範圍
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                // 舊 Id → 新 Id 的對應表
                var idMigrationMap = new Dictionary<int, int>
                {
                    [54] = 901, [55] = 902, [56] = 903, [57] = 904,
                    [58] = 905, [59] = 906, [60] = 907, [61] = 908,
                    [62] = 909, [63] = 910, [64] = 911, [65] = 912,
                };

                var migratedCount = 0;
                foreach (var (oldId, newId) in idMigrationMap)
                {
                    // 確認舊 Id 存在且新 Id 不存在（避免重複遷移）
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.CommandText = $"SELECT COUNT(*) FROM SystemSetting WHERE Id={oldId}";
                    var oldExists = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L) > 0;

                    using var checkNewCmd = conn.CreateCommand();
                    checkNewCmd.CommandText = $"SELECT COUNT(*) FROM SystemSetting WHERE Id={newId}";
                    var newExists = (long)(await checkNewCmd.ExecuteScalarAsync() ?? 0L) > 0;

                    if (oldExists && !newExists)
                    {
                        using var migrateCmd = conn.CreateCommand();
                        migrateCmd.CommandText =
                            $"UPDATE SystemSetting SET Id={newId}, IsReadOnly=1 WHERE Id={oldId}";
                        await migrateCmd.ExecuteNonQueryAsync();
                        migratedCount++;
                    }
                    else if (oldExists && newExists)
                    {
                        // 新 Id 已存在，直接刪除舊的重複資料
                        using var deleteCmd = conn.CreateCommand();
                        deleteCmd.CommandText = $"DELETE FROM SystemSetting WHERE Id={oldId}";
                        await deleteCmd.ExecuteNonQueryAsync();
                        migratedCount++;
                    }
                }

                if (migratedCount > 0)
                    log?.Info("DbInit", $"[{dbFile}] 已完成 {migratedCount} 筆硬體/OS 偵測資料的 Id 遷移（54~65 → 901~912）");
            }

            // 植入 SystemSetting 系統設定（增量：只補入缺少的 key）
            {
                var settingSeeds = SystemSettingSeed.GetSeedData();
                var existingKeys = await context.SystemSettings
                    .Select(s => s.Category + "." + s.Key)
                    .ToListAsync();
                var existingSet = new HashSet<string>(existingKeys);

                var newSettings = settingSeeds
                    .Where(s => !existingSet.Contains(s.Category + "." + s.Key))
                    .ToList();

                if (newSettings.Count > 0)
                {
                    // RuntimeDetect 項目（Id 901+）直接使用 Seed 中的指定 Id，不重新分配
                    // 一般設定項則按現有最大 Id 遞增
                    var maxId = await context.SystemSettings.AnyAsync()
                        ? await context.SystemSettings.MaxAsync(s => s.Id)
                        : 0;
                    // 取最大值時排除 RuntimeDetect 的 Id 範圍（900+）
                    if (maxId >= 900) 
                    {
                        var maxIdNullable = await context.SystemSettings
                            .Where(s => s.Id < 900)
                            .MaxAsync(s => (int?)s.Id);
                        maxId = maxIdNullable ?? 0;
                    }

                    foreach (var seed in newSettings)
                    {
                        // Id >= 901 的 RuntimeDetect 項目保留 Seed 指定 Id
                        if (seed.Id < 901)
                            seed.Id = ++maxId;
                        // else: 保留 seed.Id（901~912）
                    }

                    context.SystemSettings.AddRange(newSettings);
                    await context.SaveChangesAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已補入 {newSettings.Count} 筆系統設定");
                }
                else
                {
                    log?.Info("DbInit", $"[{dbFile}] 系統設定已是最新，無需補入");
                }

                // 同步 Description 與 Remark（每次執行都從 Seed 更新到 DB）
                // 說明：Value 等使用者可修改欄位不更動；只允許 Seed 更新說明性文字
                var metaUpdated = 0;
                var allSettings = await context.SystemSettings.ToListAsync();
                foreach (var seed in settingSeeds)
                {
                    var existing = allSettings.FirstOrDefault(
                        s => s.Category == seed.Category && s.Key == seed.Key);
                    if (existing == null) continue;

                    var changed = false;
                    if (existing.Description != seed.Description)
                    {
                        existing.Description = seed.Description;
                        changed = true;
                    }
                    if (existing.Remark != seed.Remark)
                    {
                        existing.Remark = seed.Remark;
                        changed = true;
                    }
                    if (changed) metaUpdated++;
                }
                if (metaUpdated > 0)
                {
                    await context.SaveChangesAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已更新 {metaUpdated} 筆設定的說明/備註");
                }

            }
        }
        catch (Exception ex)
        {
            log?.Error("DbInit", $"[{dbFile}] 初始化失敗", ex);
            throw; // 重新拋出讓上層處理
        }
    }

    // [已移除] InitializeMainDbAsync   — trio240plus_main.db 已廢棄（改用 main.db）
    // [已移除] InitializeLogDbAsync     — trio240plus_log.db 已廢棄（改用 system_event.db）

    /// <summary>初始化 Data DB（data.db）— 實驗數據與報告</summary>
    private static async Task InitializeDataDbAsync()
    {
        var dbPath = GetDatabasePath("data.db");
        var log = StartupLogger.Current;
        log?.Info("DbInit", "初始化 data.db ...");

        try
        {
            var options = new DbContextOptionsBuilder<DataDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var context = new DataDbContext(options);
            await context.Database.EnsureCreatedAsync();
            await SetPragmasAsync(context);

            var recordCount = await context.TestRecords.CountAsync();
            log?.Info("DbInit", $"[data.db] 現有 {recordCount} 筆 TestRecord");
        }
        catch (Exception ex)
        {
            log?.Error("DbInit", "[data.db] 初始化失敗", ex);
            throw;
        }
    }

    /// <summary>
    /// 設定 SQLite PRAGMA（WAL 模式、外鍵、快取等）
    /// </summary>
    private static async Task SetPragmasAsync(DbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        var pragmas = new[]
        {
            "PRAGMA journal_mode = WAL;",           // 允許讀寫並發
            "PRAGMA synchronous = NORMAL;",         // 平衡效能與安全
            "PRAGMA foreign_keys = ON;",            // 啟用外鍵約束
            "PRAGMA cache_size = -2000;",           // 2MB 快取
            "PRAGMA busy_timeout = 5000;",          // 忙碌等待 5 秒
        };

        foreach (var pragma in pragmas)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = pragma;
            await cmd.ExecuteNonQueryAsync();
        }

        StartupLogger.Current?.Info("DbInit", "PRAGMA 設定完成");
    }

    /// <summary>初始化 EventLog DB（system_event.db）— Migration + EventCodeDefinition 種子</summary>
    private static async Task InitializeEventLogDbAsync()
    {
        const string dbFile = "system_event.db";
        var dbPath = GetDatabasePath(dbFile);
        var isNew = !File.Exists(dbPath);
        var log = StartupLogger.Current;

        log?.Info("DbInit", $"初始化事件日誌庫 ({dbFile})...");

        try
        {
            var options = new DbContextOptionsBuilder<EventLogDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var context = new EventLogDbContext(options);
            await context.Database.MigrateAsync();
            log?.Info("DbInit", isNew
                ? $"[{dbFile}] 資料庫已建立"
                : $"[{dbFile}] Migration 完成");

            // Schema 遷移：將舊表名 ErrorDefinition 重命名為 EventCodeDefinition
            var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ErrorDefinition'";
                var oldExists = await cmd.ExecuteScalarAsync();
                if (oldExists != null)
                {
                    using var renameCmd = conn.CreateCommand();
                    renameCmd.CommandText = "ALTER TABLE ErrorDefinition RENAME TO EventCodeDefinition";
                    await renameCmd.ExecuteNonQueryAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已將 ErrorDefinition 表重命名為 EventCodeDefinition");
                }
            }

            // 增量植入 + 同步更新 EventCodeDefinition（by Id + Code）
            var seedErrors = EventCodeDefinitionSeed.GetSeedData();
            var existingAll = context.EventCodeDefinitions.ToList();
            var existingIds = existingAll.Select(e => e.Id).ToHashSet();
            var existingCodes = existingAll.Select(e => e.Code).ToHashSet();

            // 補入新記錄（Id 和 Code 都不存在才插入，避免唯一約束衝突）
            var newErrors = seedErrors
                .Where(s => !existingIds.Contains(s.Id) && !existingCodes.Contains(s.Code))
                .ToList();
            if (newErrors.Count > 0)
            {
                context.EventCodeDefinitions.AddRange(newErrors);
                await context.SaveChangesAsync();
                log?.Info("DbInit", $"[{dbFile}] 已補入 {newErrors.Count} 筆事件定義",
                    $"Codes={string.Join(", ", newErrors.Select(e => e.Code))}");
            }

            // 同步既有記錄的欄位（按 Id 匹配，更新有差異的欄位）
            var codeUpdated = 0;
            foreach (var seed in seedErrors)
            {
                var existing = existingAll.FirstOrDefault(e => e.Id == seed.Id);
                if (existing == null) continue;

                var changed = false;
                if (existing.Code != seed.Code) { existing.Code = seed.Code; changed = true; }
                if (existing.Severity != seed.Severity) { existing.Severity = seed.Severity; changed = true; }
                if (existing.Title != seed.Title) { existing.Title = seed.Title; changed = true; }
                if (existing.Description != seed.Description) { existing.Description = seed.Description; changed = true; }
                if (existing.Resolution != seed.Resolution) { existing.Resolution = seed.Resolution; changed = true; }
                if (existing.UserMessageKey != seed.UserMessageKey) { existing.UserMessageKey = seed.UserMessageKey; changed = true; }
                if (existing.UserMessageFallback != seed.UserMessageFallback) { existing.UserMessageFallback = seed.UserMessageFallback; changed = true; }

                if (changed) codeUpdated++;
            }
            if (codeUpdated > 0)
            {
                await context.SaveChangesAsync();
                log?.Info("DbInit", $"[{dbFile}] 已更新 {codeUpdated} 筆事件代碼");
            }

            if (newErrors.Count == 0 && codeUpdated == 0)
            {
                log?.Info("DbInit", $"[{dbFile}] 事件定義已是最新，無需補入");
            }

            await SetPragmasAsync(context);
        }
        catch (Exception ex)
        {
            log?.Error("DbInit", $"[{dbFile}] 初始化失敗", ex);
            throw;
        }
    }

    /// <summary>初始化正式業務核心庫（main.db）— User 表 + 種子資料</summary>
    private static async Task InitializeAppMainDbAsync(Dictionary<string, string> credentials)
    {
        const string dbFile = "main.db";
        var dbPath = GetDatabasePath(dbFile);
        var isNew = !File.Exists(dbPath);
        var log = StartupLogger.Current;

        log?.Info("DbInit", $"初始化正式業務核心庫 ({dbFile})...");

        try
        {
            var options = new DbContextOptionsBuilder<AppMainDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using var context = new AppMainDbContext(options);
            await context.Database.MigrateAsync();
            log?.Info("DbInit", isNew
                ? $"[{dbFile}] 資料庫已建立"
                : $"[{dbFile}] Migration 完成");

            // Schema 遷移：確保 User 表有 IsDeleted / DeletedAt / DeletedBy 欄位
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                // 讀取現有欄位
                using var pragmaCmd = conn.CreateCommand();
                pragmaCmd.CommandText = "PRAGMA table_info(User)";
                var existingColumns = new HashSet<string>();
                using (var reader = await pragmaCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingColumns.Add(reader.GetString(1));
                }

                // 需要的新欄位（名稱 → ALTER TABLE SQL）
                var requiredColumns = new Dictionary<string, string>
                {
                    ["IsDeleted"] = "ALTER TABLE User ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0",
                    ["DeletedAt"] = "ALTER TABLE User ADD COLUMN DeletedAt TEXT",
                    ["DeletedBy"] = "ALTER TABLE User ADD COLUMN DeletedBy TEXT"
                };

                foreach (var (col, sql) in requiredColumns)
                {
                    if (!existingColumns.Contains(col))
                    {
                        using var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = sql;
                        await alterCmd.ExecuteNonQueryAsync();
                        log?.Info("DbInit", $"[{dbFile}] 已新增 User.{col} 欄位");
                    }
                }
            }

            // 增量植入 User（按 Id + Username 補入缺少的帳號）
            {
                var users = UserSeed.GetSeedData(credentials, PasswordHasher);
                var existingIds = context.Users.Select(u => u.Id).ToHashSet();
                var existingUsernames = context.Users.Select(u => u.Username).ToHashSet();

                var newUsers = users
                    .Where(u => !existingIds.Contains(u.Id) && !existingUsernames.Contains(u.Username))
                    .ToList();

                // 偵測 Id 衝突：Seed 中的 Id 已被其他帳號佔用
                var idConflicts = users
                    .Where(u => existingIds.Contains(u.Id) && !existingUsernames.Contains(u.Username))
                    .ToList();
                foreach (var conflict in idConflicts)
                {
                    log?.Warn("DbInit", $"[{dbFile}] Seed 帳號跳過：Id={conflict.Id} 已被佔用",
                        $"Username={conflict.Username} 未被建立，請檢查 UserSeed Id 配置");
                }

                if (newUsers.Count > 0)
                {
                    context.Users.AddRange(newUsers);
                    await context.SaveChangesAsync();
                    log?.Info("DbInit", $"[{dbFile}] 已補入 {newUsers.Count} 筆使用者帳號",
                        $"Users={string.Join(", ", newUsers.Select(u => u.Username))}");
                }
                else if (idConflicts.Count == 0)
                {
                    log?.Info("DbInit", $"[{dbFile}] 使用者帳號已是最新，無需補入");
                }
            }

            await SetPragmasAsync(context);
        }
        catch (Exception ex)
        {
            log?.Error("DbInit", $"[{dbFile}] 初始化失敗", ex);
            throw;
        }
    }
}
