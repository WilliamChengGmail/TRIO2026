# 鎖定畫面安全強化 — Admin 強制登出 + 代理解鎖

## 背景

當前鎖定畫面的「切換使用者」按鈕（`lock_screen_switch_user_enabled`）無任何身份驗證，任何人按下即可強制登出當前 Session。需要強化為：

1. **只有 Admin 等級使用者可以操作**
2. **需輸入 Admin 帳密驗證後才能執行**
3. **驗證成功後可選擇「強制登出」或「代理解鎖」**（由新設定控制）
4. **完善的安全審計日誌**

## Proposed Changes

### 1. 新增系統設定

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

新增 1 組設定：

| Key | Category | Value | Description |
|-----|----------|-------|-------------|
| `lock_screen_admin_action` | LoginUI | `logout` | Admin 鎖定畫面驗證後動作（`logout`=強制登出回登入頁, `unlock`=代理解鎖繼續操作） |

> `lock_screen_switch_user_enabled` 保留，語意改為「是否顯示 Admin 介入按鈕」

---

### 2. SystemSettingService 新增屬性

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

```csharp
/// <summary>Admin 鎖定畫面操作：logout=強制登出, unlock=代理解鎖</summary>
public string LockScreenAdminAction
    => GetLiveString("LoginUI", "lock_screen_admin_action", "logout");
```

---

### 3. ErrorCodes 新增

#### [MODIFY] [ErrorCodes.cs](file:///d:/TRIO2026/src/TRIO2026.Core/ErrorCodes.cs)

```
LockAdminAuthSuccess   = "INF-9011"  // Admin 驗證成功
LockAdminAuthFailed    = "WRN-9012"  // Admin 驗證失敗（密碼錯誤/權限不足）
LockAdminForceLogout   = "WRN-9013"  // Admin 強制登出另一使用者
LockAdminProxyUnlock   = "WRN-9014"  // Admin 代理解鎖另一使用者的 Session
```

> [!IMPORTANT]
> 強制登出和代理解鎖使用 **WRN 等級**，因為這些都是「代替他人操作」的敏感行為，需要在日誌中醒目標記。

---

### 4. LockScreenOverlay 重構

#### [MODIFY] [LockScreenOverlay.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Controls/LockScreenOverlay.xaml.cs)

**「切換使用者」按鈕改為「Admin 介入」按鈕**

流程變更：

```
[原有] 切換使用者按鈕 → 直接登出（無驗證）

[修改後]
Admin 介入按鈕 → 彈出 TouchKeyboard 要求輸入 Admin 帳密
  → 驗證帳密（呼叫 AuthService.LoginAsync）
  → 檢查 RoleLevel >= 3 (Admin)
    → 失敗：顯示錯誤「需要管理員權限」+ 記錄 WRN-9012
    → 成功：記錄 INF-9011
      → 讀取 lock_screen_admin_action 設定
        → "logout"：記錄 WRN-9013 → 回傳 SwitchUser（走原有登出流程）
        → "unlock"：記錄 WRN-9014 → 回傳 Unlocked（代理解鎖）
```

**日誌 Detail 欄位內容：**

| 事件 | Detail |
|------|--------|
| Admin 驗證成功 | `AdminUser={admin帳號}, LockedUser={被鎖帳號}, Action={logout/unlock}` |
| Admin 驗證失敗 | `AttemptUser={輸入帳號}, LockedUser={被鎖帳號}, Reason={WrongPassword/InsufficientRole/AccountLocked}` |
| 強制登出 | `AdminUser={admin帳號}, ForcedOutUser={被登出帳號}` |
| 代理解鎖 | `AdminUser={admin帳號}, UnlockedUser={被解鎖帳號}` |

---

### 5. LockScreenResult 擴充

#### [MODIFY] [LockScreenOverlay.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Controls/LockScreenOverlay.xaml.cs) (enum 部分)

```csharp
public enum LockScreenResult
{
    Unlocked,           // 密碼驗證成功，解鎖
    SwitchUser,         // Admin 強制登出（完整登出）
    AdminProxyUnlock    // Admin 代理解鎖（繼續操作）
}
```

> [!NOTE]
> AppShell 中 `AdminProxyUnlock` 走 `Unlocked` 同樣的解鎖流程，但日誌上已經有不同的記錄。

---

### 6. EventCodeDefinitionSeed 同步

#### [MODIFY] [EventCodeDefinitionSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/EventCodeDefinitionSeed.cs)

新增 4 筆 ErrorCode 定義對應 INF-9011 ~ WRN-9014。

---

## 安全審計完整流程

```
Session Timeout
  ↓
鎖定畫面顯示 [INF-9006 SessionLocked]
  ↓
┌─ 原使用者輸入自己密碼 → 解鎖 [INF-9007 SessionUnlocked]
├─ 密碼錯誤 → [WRN-9008 LockInvalidPassword]
└─ 按下「Admin 介入」按鈕
    ↓
    輸入 Admin 帳密
    ├─ 驗證失敗 → [WRN-9012 LockAdminAuthFailed]
    ├─ 權限不足 → [WRN-9012 LockAdminAuthFailed, Reason=InsufficientRole]
    └─ 驗證成功 → [INF-9011 LockAdminAuthSuccess]
        ↓
        讀取 lock_screen_admin_action
        ├─ "logout" → [WRN-9013 LockAdminForceLogout] → 回登入頁
        └─ "unlock" → [WRN-9014 LockAdminProxyUnlock] → 繼續操作
```

## Verification Plan

### Automated Tests
- `dotnet build -c Release` — 確認編譯通過

### Manual Verification
1. 設定 `lock_screen_switch_user_enabled=1`, `lock_screen_admin_action=logout`
   - 鎖定後按「Admin 介入」→ 輸入非 Admin 帳密 → 應被拒絕
   - 輸入 Admin 帳密 → 應強制登出回登入頁
2. 改設定 `lock_screen_admin_action=unlock`
   - 輸入 Admin 帳密 → 應解鎖回原畫面
3. 檢查 EventLog 是否有完整記錄（INF-9011 ~ WRN-9014）
