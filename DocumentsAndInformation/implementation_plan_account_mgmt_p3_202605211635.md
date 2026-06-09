# 帳號管理完整實作計畫 — Part 3：服務層、DB 設定、i18n、路由與驗證計畫

> 製作者: Office of William
> 接續 Part 1 & Part 2，本部分定義後端服務、設定項目與實作完整度。

> [!IMPORTANT]
> **全域規範提醒（本計畫所有實作均適用）**
> - 所有畫面文字透過 `LocalizationService` 鍵值，**不寫死字串**
> - 所有使用者行為均呼叫 `EventLogService` 記錄完整稽核軌跡
> - 所有「刪除」操作均為**假刪除（Soft Delete）**，資料庫保留原始紀錄

---

## AccountManagementService（新建）

### [NEW] Services/AccountManagementService.cs

```csharp
/// <summary>
/// 帳號管理服務 — Admin 專用
/// 所有方法須確認呼叫者為 Admin (RoleLevel=3)
///
/// 製作者: Office of William
/// </summary>
public class AccountManagementService
{
    private readonly AppMainDbContext _db;
    private readonly SystemSettingService _systemSettings;

    public AccountManagementService(AppMainDbContext db,
        SystemSettingService systemSettings) { ... }

    // ── 查詢 ──

    /// <summary>
    /// 取得所有非 local_operator、IsDeleted=0 的帳號清單（含停用帳號）
    /// </summary>
    Task<List<User>> GetAllManagedUsersAsync();

    // ── 建立 ──

    /// <summary>新增帳號，回傳臨時密碼明文（只呼叫一次）</summary>
    Task<(bool Success, string? Error, string? TempPassword)>
        CreateUserAsync(string username, string? displayName,
                        int roleLevel, string createdBy);

    // ── 假刪除 ──

    /// <summary>
    /// 假刪除帳號（含安全守衛）
    /// 設定 IsDeleted=1, IsActive=0, DeletedAt, DeletedBy
    /// </summary>
    Task<(bool Success, string? Error)>
        DeleteUserAsync(int userId, string operatorUsername);

    // ── 狀態變更 ──

    /// <summary>停用 / 啟用帳號</summary>
    Task<(bool Success, string? Error)>
        SetActiveAsync(int userId, bool active, int operatorUserId);

    /// <summary>鎖定帳號（LockedUntil = 9999-12-31，永久直到手動解鎖）</summary>
    Task<(bool Success, string? Error)>
        LockUserAsync(int userId, int operatorUserId);

    /// <summary>解鎖帳號（LockedUntil = null, FailedLoginCount = 0）</summary>
    Task<(bool Success, string? Error)>
        UnlockUserAsync(int userId, int operatorUserId);

    // ── 密碼管理 ──

    /// <summary>
    /// Admin 重設指定帳號密碼，回傳臨時密碼明文（只呼叫一次）
    /// 同時設定 ForcePasswordChange=1，清除 LockedUntil / FailedLoginCount
    /// </summary>
    Task<(bool Success, string? Error, string? TempPassword)>
        ResetPasswordAsync(int userId, string operatorUsername);

    // ── 安全守衛輔助 ──

    /// <summary>確認系統中啟用且未刪除的 Admin 帳號數量（防止刪除唯一 Admin）</summary>
    Task<int> CountActiveAdminsAsync();
}
```

### DI 註冊：`Transient`

在 `App.xaml.cs` 的 DI 設定中新增：
```csharp
services.AddTransient<AccountManagementService>();
```

---

## 安全守衛一覽（服務層雙重驗證）

| 操作 | 守衛規則 |
|------|----------|
| 假刪除 | 不可刪自己 / 不可刪唯一啟用且未刪除的 Admin / 不可刪 local_operator |
| 停用 | 不可停用自己 / 不可停用唯一啟用的 Admin |
| 鎖定 | 不可鎖定自己 |
| 新增 RoleLevel | 僅 1=Operator 或 3=Admin（UI 不建立 Service 帳號） |
| 重設密碼 | 不限制（Admin 可對任何帳號重設） |
| 所有操作 | 驗證 operatorUserId 對應的帳號 RoleLevel=3 且 IsDeleted=0 |

---

## SystemSetting 新增設定

### [MODIFY] SystemSettingSeed.cs

新增 `AccountMgmt` 分類，共 1 項：

| Id | Category | Key | 預設值 | 說明 |
|----|----------|-----|--------|------|
| 33 | `AccountMgmt` | `account_lock_enabled` | `0` | 是否啟用帳號鎖定/解鎖功能（0=停用按鈕隱藏；1=啟用） |

### [MODIFY] User 實體（新增假刪除欄位）

```csharp
// ── 假刪除（新增） ──
/// <summary>是否已刪除（軟刪除）：0=正常, 1=已刪除</summary>
public int IsDeleted { get; set; } = 0;

/// <summary>刪除時間（ISO8601）</summary>
public string? DeletedAt { get; set; }

/// <summary>刪除操作者帳號名稱</summary>
public string? DeletedBy { get; set; }
```

> [!NOTE]
> 新增欄位後需建立 EF Core Migration，更新 `main.db` Schema。
> Migration 指令：`dotnet ef migrations add AddUserSoftDelete --project TRIO2026.Data`

### [MODIFY] SystemSettingService.cs

新增便利屬性：
```csharp
// ═══════════════════════════════════════
// AccountMgmt 設定
// ═══════════════════════════════════════

/// <summary>是否啟用帳號鎖定功能（控制 UI 按鈕可見性）</summary>
public bool AccountLockEnabled
    => GetLiveString("AccountMgmt", "account_lock_enabled", "0") == "1";
```

---

## i18n 鍵值清單（Module = AccountMgmt）

所有字串需新增至 `LocalizedString` 種子資料（4 種語系：en / zh-TW / zh-CN / ja）：

| Key | en | zh-TW |
|-----|----|-------|
| `AccountMgmt.Title` | Account Management | 帳號管理 |
| `AccountMgmt.AddAccount` | + Add Account | + 新增帳號 |
| `AccountMgmt.SelectPrompt` | Select an account to manage | 請選擇帳號以進行操作 |
| `AccountMgmt.Disable` | Disable Account | 停用帳號 |
| `AccountMgmt.Enable` | Enable Account | 啟用帳號 |
| `AccountMgmt.Delete` | Delete Account | 刪除帳號 |
| `AccountMgmt.Lock` | Lock Account | 鎖定帳號 |
| `AccountMgmt.Unlock` | Unlock Account | 解鎖帳號 |
| `AccountMgmt.ResetPassword` | Reset Password | 重設密碼 |
| `AccountMgmt.ChangePassword` | Change Password | 變更密碼 |
| `AccountMgmt.ViewDetails` | View Details | 檢視詳細資料 |
| `AccountMgmt.StatusActive` | Active | 啟用 |
| `AccountMgmt.StatusDisabled` | Disabled | 已停用 |
| `AccountMgmt.StatusLocked` | Locked | 已鎖定 |
| `AccountMgmt.RoleOperator` | Operator | 操作員 |
| `AccountMgmt.RoleService` | Service | 服務工程師 |
| `AccountMgmt.RoleAdmin` | Admin | 管理員 |
| `AccountMgmt.TempPasswordTitle` | Temporary Password | 臨時密碼 |
| `AccountMgmt.TempPasswordNote` | Please provide this to the user. They will be required to change it on first login. | 請將此密碼提供給使用者。首次登入後系統將強制要求變更密碼。 |
| `AccountMgmt.ConfirmDelete` | Are you sure you want to delete this account? This action cannot be undone. | 確定要刪除此帳號？此操作無法復原。 |
| `AccountMgmt.ConfirmDisable` | Are you sure you want to disable this account? | 確定要停用此帳號？ |
| `AccountMgmt.ConfirmLock` | Locking the account will prevent the user from logging in until manually unlocked. | 鎖定後該使用者將無法登入，直到手動解鎖為止。 |
| `AccountMgmt.ErrorSelf` | Cannot perform this action on your own account. | 無法對自己的帳號執行此操作。 |
| `AccountMgmt.ErrorLastAdmin` | Cannot remove the last active admin account. | 無法移除最後一個啟用的管理員帳號。 |
| `AccountMgmt.UsernameExists` | Username already exists. | 帳號名稱已存在。 |
| `AccountMgmt.InvalidUsername` | Username must be 3-20 alphanumeric characters or underscores. | 帳號名稱須為 3~20 個英數字或底線。 |

> [!NOTE]
> zh-CN / ja 的翻譯在實作時補充，此處僅列 en / zh-TW 作為計畫審核依據。

---

## UserMenu i18n 新增鍵值（Module = UserMenu）

| Key | en | zh-TW |
|-----|----|-------|
| `UserMenu.ChangePassword` | Change Password | 變更密碼 |
| `UserMenu.AccountMgmt` | Account Management | 帳號管理 |

---

## AppShell 路由新增

### [MODIFY] AppShell.xaml.cs

```csharp
// 頁面實例（新增）
private AccountManagementPage? _accountMgmtPage;

// NavigateTo 新增 case
case "accountMgmt":
    _accountMgmtPage ??= CreateAccountMgmtPage();
    PageHost.Content = _accountMgmtPage;
    break;

// 頁面工廠（新增）
private AccountManagementPage CreateAccountMgmtPage()
{
    var svc = _serviceProvider.GetRequiredService<AccountManagementService>();
    return new AccountManagementPage(_sessionService, DialogOverlay,
        _authService, svc, _systemSettings);
}
```

> [!NOTE]
> `_accountMgmtPage` **不快取**（每次導航重建），確保每次進入都讀取最新帳號資料。
> 即：`_accountMgmtPage = null` 在每次 NavigateTo("accountMgmt") 前先清空。

---

## 全部受影響檔案對照表

| 層級 | 檔案 | 操作 | 說明 |
|------|------|------|------|
| **Core/Entities** | `User.cs` | MODIFY | 新增假刪除欄位：IsDeleted / DeletedAt / DeletedBy |
| **Core/Entities** | `SystemSetting.cs` | MODIFY | 文件更新：加入 AccountMgmt 分類 |
| **Data/Migrations** | `AddUserSoftDelete` | NEW | EF Core Migration，更新 main.db Schema |
| **Data/Seeding** | `SystemSettingSeed.cs` | MODIFY | 新增 Id=33 account_lock_enabled |
| **Data/Seeding** | `LocalizedStringSeed.cs` | MODIFY | 新增 AccountMgmt + UserMenu + ServiceMode 鍵值 |
| **App/Services** | `AccountManagementService.cs` | NEW | 帳號管理服務（含假刪除邏輯） |
| **App/Services** | `AuthService.cs` | MODIFY | LoginAsync 新增 `IsDeleted=0` 過濾；新增 ChangePasswordAsync |
| **App/Services** | `SystemSettingService.cs` | MODIFY | 新增 AccountLockEnabled 屬性 |
| **App/Controls** | `UserMenuControl.xaml` | MODIFY | 新增 BtnChangePassword / BtnAccountMgmt |
| **App/Controls** | `UserMenuControl.xaml.cs` | MODIFY | RefreshUserDisplay 角色判斷 + 事件處理 |
| **App/Controls** | `ChangePasswordOverlay.xaml` | NEW | 密碼變更 Overlay（password_ui 計畫） |
| **App/Controls** | `ChangePasswordOverlay.xaml.cs` | NEW | 同上 |
| **App/Controls** | `CreateAccountOverlay.xaml` | NEW | 新增帳號 Overlay |
| **App/Controls** | `CreateAccountOverlay.xaml.cs` | NEW | 同上 |
| **App/Pages** | `AccountManagementPage.xaml` | NEW | 帳號管理主頁面（整頁切換） |
| **App/Pages** | `AccountManagementPage.xaml.cs` | NEW | 帳號管理邏輯 |
| **App/Pages** | `ServiceModePage.xaml` | MODIFY | 建立四功能殼 UI |
| **App/Pages** | `ServiceModePage.xaml.cs` | MODIFY | 功能殼點擊事件 + 重設管理者密碼邏輯 |
| **App/Views** | `AppShell.xaml.cs` | MODIFY | 新增 accountMgmt 路由（每次重建頁面） |

---

## 強制重新登入過渡流程（密碼重設後）

```
使用者以臨時密碼登入
     │ LoginAsync 成功
     ▼
AppShell.OnLoginSucceeded
     │ 檢查 user.ForcePasswordChange == 1
     ▼
顯示 ChangePasswordOverlay（不可取消）
     │ 變更成功
     ▼
清除 ForcePasswordChange = 0
寫入 PasswordChangedAt
     │
     ▼
強制登出 → ClearSession()
     │
     ▼
NavigateTo("login")
+ 顯示提示：「密碼已變更，請以新密碼重新登入」
```

---

## 驗證計畫

### 自動化驗證

```
dotnet build  → 0 error / 0 warning
dotnet test   → 後續 AccountManagementServiceTests 覆蓋安全守衛
```

### 手動驗證清單

| 測試案例 | 預期結果 |
|----------|----------|
| Admin 登入後 UserMenu 有「帳號管理」節點 | ✅ 顯示 |
| Operator 登入後 UserMenu 有「變更密碼」節點 | ✅ 顯示 |
| Service 登入後 UserMenu 無「變更密碼」節點 | ✅ 絕對隱藏 |
| Admin 登入後 UserMenu 無「Service Mode」節點 | ✅ 隱藏 |
| MenuPage 上所有角色的 UserMenu 無「返回主畫面」 | ✅ ShowHomeButton=False |
| 停用帳號後，該帳號不出現在登入下拉選單 | ✅ IsActive 過濾 |
| 假刪除帳號後，帳號從管理列表消失，DB 仍保留 | ✅ IsDeleted=1 |
| 假刪除帳號的 Username 無法再次建立 | ✅ 唯一性含已刪除帳號 |
| 刪除最後一個啟用且未刪除的 Admin → 被阻擋 | ✅ 錯誤提示 |
| 刪除自己 → 被阻擋 | ✅ 錯誤提示 |
| 重設密碼 → 臨時密碼登入 → 強制改密碼 → 強制登出重新登入 | ✅ 完整流程 |
| account_lock_enabled=0 時，鎖定/解鎖按鈕不顯示 | ✅ 隱藏 |
| account_lock_enabled=1 時，鎖定/解鎖按鈕顯示 | ✅ 顯示 |
| ServiceModePage 功能殼按鈕點擊顯示「功能開發中」 | ✅ OverlayDialog |
| 所有帳號操作皆有 EventLog 紀錄 | ✅ 稽核完整 |
| 切換語系後帳號管理所有文字正確切換 | ✅ 多語系 |
| 七吋螢幕（600×960）列表每列 64px 可正常觸控 | ✅ 觸控友善 |

---

## Open Questions 總覽（三份計畫彙整）

### 帳號管理計畫（已決議）

| # | 問題 | 決議 |
|---|------|------|
| P1-1 | 帳號管理頁面開啟方式 | ✅ 獨立新 Page，整頁切換 |
| P1-2 | ServiceModePage 功能按鈕是否本次實作 | ✅ 建立功能殼，「重設管理者密碼」完整實作 |
| P1-3 | ServiceModePage 子頁面導覽 | ✅ 選項 B：ServiceModePage 內部自管 |

### 帳號管理計畫（待確認）

| # | 問題 | 建議 |
|---|------|------|
| P2-1 | ForcePasswordChange 後強制重新登入 | 強制登出重新登入（醫療級標準） |
| P2-2 | 帳號列表是否顯示 Service 帳號 | 顯示，但 UI 層新增/刪除受限 |
| P2-3 | 是否支援編輯帳號資料 | 本次僅唯讀，後續版本擴充 |

### 密碼管理 UI 計畫（待確認）

| # | 問題 | 建議 |
|---|------|------|
| PW-Q1 | ForcePasswordChange 是否可跳過 | 不可跳過（選項 A） |
| PW-Q2 | 觸控鍵盤方案 | 選項 C（系統鍵盤/外接鍵盤） |
| PW-Q3 | 顯示明碼切換 | 建議加入 👁 按鈕 |
| PW-Q4 | 密碼原則提示方式 | 即時逐條打勾（選項 B） |
| PW-Q5 | 變更密碼觸發點 | UserMenu + ForcePasswordChange 兩者皆有 |
| PW-Q6 | PasswordExpiryDays 是否本次實作 | 暫不實作 |
| PW-Q7 | 取消按鈕可見情境 | 依觸發情境決定（強制時不顯示）|
