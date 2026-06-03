using TRIO2026.Core.Entities;
using TRIO2026.Core.Enums;
using TRIO2026.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace TRIO2026.App.Services;

/// <summary>
/// 認證服務 — 負責登入驗證、密碼雜湊、鎖定機制、密碼變更
/// 
/// 資料來源：main.db 的 User 表
/// 
/// 製作者: Office of William
/// </summary>
public class AuthService
{
    private readonly AppMainDbContext _db;
    private readonly PasswordPolicyService? _passwordPolicy;
    private readonly SystemSettingService? _systemSettings;

    /// <summary>最大連續登入失敗次數（從 DB 讀取，預設 5）</summary>
    private int MaxFailedAttempts
        => _systemSettings?.MaxFailedAttempts ?? 5;

    /// <summary>帳號鎖定持續分鐘數（從 DB 讀取，預設 15）</summary>
    private int LockoutMinutes
        => _systemSettings?.LockoutMinutes ?? 15;

    public AuthService(AppMainDbContext db)
    {
        _db = db;
    }

    public AuthService(AppMainDbContext db, PasswordPolicyService passwordPolicy)
    {
        _db = db;
        _passwordPolicy = passwordPolicy;
    }

    public AuthService(AppMainDbContext db, PasswordPolicyService passwordPolicy,
        SystemSettingService systemSettings)
    {
        _db = db;
        _passwordPolicy = passwordPolicy;
        _systemSettings = systemSettings;
    }

    /// <summary>
    /// 驗證使用者帳號密碼
    /// </summary>
    /// <returns>(Result, User, Detail) — Detail 供 EventLog 記錄上下文</returns>
    public async Task<(AuthResult Result, User? User, string? Detail)> LoginAsync(string username, string password)
    {
        // 清除 Change Tracker 快取，確保從 DB 讀取最新資料
        // （WPF 中 DbContext 為長生命週期，外部工具修改 DB 後需要此步驟）
        _db.ChangeTracker.Clear();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsDeleted == 0);

        if (user == null)
        {
            return (AuthResult.UserNotFound, null, null);
        }

        if (user.IsActive == 0)
        {
            return (AuthResult.AccountDisabled, null, null);
        }

        // 檢查鎖定狀態
        if (!string.IsNullOrEmpty(user.LockedUntil))
        {
            if (DateTime.TryParse(user.LockedUntil, out var lockedUntil))
            {
                if (DateTime.UtcNow < lockedUntil)
                {
                    var remaining = (int)(lockedUntil - DateTime.UtcNow).TotalMinutes;
                    return (AuthResult.AccountLocked, null,
                        $"Status=AlreadyLocked, RemainingMinutes={remaining}");
                }
                // 鎖定已過期，清除
                user.LockedUntil = null;
                user.FailedLoginCount = 0;
            }
        }

        // 免登入專用帳號（PasswordHash 為空）不允許密碼登入
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return (AuthResult.AccountDisabled, null, "Reason=NoPasswordHash");
        }

        // 驗證密碼
        bool isValid;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch
        {
            // 如果 PasswordHash 格式不正確（如 PLACEHOLDER），直接比對字串
            isValid = false;
        }

        if (!isValid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes).ToString("O");
                await _db.SaveChangesAsync();
                return (AuthResult.AccountLocked, null,
                    $"Status=JustLocked, FailedCount={user.FailedLoginCount}, LockoutMinutes={LockoutMinutes}");
            }
            await _db.SaveChangesAsync();
            return (AuthResult.WrongPassword, null,
                $"FailedCount={user.FailedLoginCount}/{MaxFailedAttempts}");
        }

        // 登入成功
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow.ToString("O");
        await _db.SaveChangesAsync();

        return (AuthResult.Success, user, null);
    }

    /// <summary>
    /// 雜湊密碼（用於建立帳號或變更密碼）
    /// </summary>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// 取得可登入的使用者清單（排除免登入專用帳號和已刪除帳號）
    /// </summary>
    public async Task<List<User>> GetAllUsersAsync()
    {
        _db.ChangeTracker.Clear();
        return await _db.Users
            .Where(u => u.IsActive == 1 && u.PasswordHash != "" && u.IsDeleted == 0)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    /// <summary>
    /// 取得可登入的使用者清單（含 Guest 免密碼帳號，供下拉選單使用）
    /// Guest 帳號排在最前面
    /// </summary>
    public async Task<List<User>> GetAllUsersWithGuestAsync()
    {
        _db.ChangeTracker.Clear();
        return await _db.Users
            .Where(u => u.IsActive == 1 && u.IsDeleted == 0
                && (u.PasswordHash != "" || u.Username == "guest"))
            .OrderBy(u => u.Username == "guest" ? 0 : 1) // Guest 排最前
            .ThenBy(u => u.Username)
            .ToListAsync();
    }

    /// <summary>
    /// 依帳號名稱取得使用者（供 Guest 免密碼登入使用）
    /// </summary>
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        _db.ChangeTracker.Clear();
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsDeleted == 0);
    }

    /// <summary>
    /// 更新使用者的語系偏好
    /// </summary>
    public async Task UpdateLanguagePreferenceAsync(int userId, string langCode)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.LanguagePreference = langCode;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 變更使用者密碼
    /// 1. 驗證舊密碼（BCrypt.Verify）
    /// 2. PasswordPolicyService.Validate(newPassword, roleLevel)
    /// 3. 更新 PasswordHash + PasswordChangedAt + ForcePasswordChange=0
    /// </summary>
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        int userId, string oldPassword, string newPassword)
    {
        _db.ChangeTracker.Clear();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsDeleted == 0);
        if (user == null)
            return (false, "User not found.");

        // 驗證舊密碼
        bool isValid;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash);
        }
        catch
        {
            isValid = false;
        }

        if (!isValid)
            return (false, "WRONG_CURRENT_PASSWORD");

        // 新舊密碼不可相同
        if (oldPassword == newPassword)
            return (false, "SAME_PASSWORD");

        // 密碼原則驗證
        if (_passwordPolicy != null)
        {
            var policyError = _passwordPolicy.Validate(newPassword, user.RoleLevel);
            if (policyError != null)
                return (false, policyError);
        }

        // 更新密碼
        user.PasswordHash = HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow.ToString("O");
        user.ForcePasswordChange = 0;
        user.UpdatedAt = DateTime.UtcNow.ToString("O");
        user.UpdatedBy = user.Username;
        await _db.SaveChangesAsync();

        return (true, null);
    }

    /// <summary>驗證密碼是否與 hash 匹配（供鎖定畫面等場景使用）</summary>
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}

