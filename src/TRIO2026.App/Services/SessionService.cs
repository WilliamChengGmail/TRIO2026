using TRIO2026.Core.Entities;
using TRIO2026.Core.Enums;

namespace TRIO2026.App.Services;

/// <summary>
/// 會話管理服務 — 管理當前登入使用者狀態
/// 
/// 使用 User 實體（main.db）
/// 
/// 製作者: Office of William
/// </summary>
public class SessionService
{
    /// <summary>當前登入的使用者（null 表示未登入）</summary>
    public User? CurrentUser { get; private set; }

    /// <summary>是否已認證</summary>
    public bool IsAuthenticated => CurrentUser != null;

    /// <summary>是否為免登入模式（Guest Session）</summary>
    public bool IsGuestMode { get; private set; }

    /// <summary>是否為 Guest 免密碼登入（與 IsGuestMode 免登入模式不同）</summary>
    public bool IsGuestLogin { get; private set; }

    /// <summary>當前使用者的角色等級</summary>
    public RoleLevel CurrentRole => IsAuthenticated
        ? (RoleLevel)CurrentUser!.RoleLevel
        : 0;

    /// <summary>會話變更事件</summary>
    public event EventHandler? SessionChanged;

    /// <summary>設定當前使用者（登入成功後呼叫）</summary>
    public void SetCurrentUser(User user)
    {
        CurrentUser = user;
        IsGuestMode = false;
        IsGuestLogin = string.Equals(user.Username, "guest", StringComparison.OrdinalIgnoreCase);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清除會話（登出）</summary>
    public void ClearSession()
    {
        CurrentUser = null;
        IsGuestMode = false;
        IsGuestLogin = false;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>免登入模式 — 載入 DB Guest 帳號並套用系統設定</summary>
    public void SetGuestSession(User guestUser, string displayName)
    {
        guestUser.RoleLevel = (int)RoleLevel.Operator; // 固定 Operator
        guestUser.DisplayName = displayName;
        CurrentUser = guestUser;
        IsGuestMode = true;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>檢查當前使用者是否有指定權限等級</summary>
    public bool HasPermission(RoleLevel required)
    {
        return IsAuthenticated && CurrentRole >= required;
    }

    // ═══════════════════════════════════════
    // 鎖定狀態管理
    // ═══════════════════════════════════════

    /// <summary>畫面是否已鎖定</summary>
    public bool IsLocked { get; private set; }

    /// <summary>鎖定時間</summary>
    public DateTime? LockedAt { get; private set; }

    /// <summary>畫面鎖定事件</summary>
    public event EventHandler? SessionLocked;

    /// <summary>畫面解鎖事件</summary>
    public event EventHandler? SessionUnlocked;

    /// <summary>鎖定畫面</summary>
    public void LockSession()
    {
        if (IsLocked) return;
        IsLocked = true;
        LockedAt = DateTime.Now;
        SessionLocked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>解鎖畫面</summary>
    public void UnlockSession()
    {
        if (!IsLocked) return;
        IsLocked = false;
        LockedAt = null;
        SessionUnlocked?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════
    // 訊息佇列（鎖定期間累積，解鎖後依序顯示）
    // ═══════════════════════════════════════

    private readonly Queue<PendingMessage> _pendingMessages = new();

    /// <summary>是否有待處理的訊息</summary>
    public bool HasPendingMessages => _pendingMessages.Count > 0;

    /// <summary>鎖定期間排入待處理訊息</summary>
    public void EnqueueMessage(string title, string message, string icon = "ℹ️")
    {
        _pendingMessages.Enqueue(new PendingMessage(title, message, icon, DateTime.Now));
    }

    /// <summary>取出下一筆待處理訊息</summary>
    public PendingMessage? DequeueMessage()
    {
        return _pendingMessages.Count > 0 ? _pendingMessages.Dequeue() : null;
    }
}

/// <summary>鎖定期間排隊等待的訊息</summary>
public record PendingMessage(string Title, string Message, string Icon, DateTime EnqueuedAt);
