# Guest Account 設計 — 免登入模式帳號實體化

## 背景

目前免登入模式下，`SessionService.SetGuestSession()` 直接以 hard code 建構一個 `User` 物件（`Id=0`, `DisplayName="Guest"`），沒有對應的 DB 記錄。這導致：

1. 右上角永遠顯示 "Guest"（hard code）
2. Guest 沒有實體 DB 記錄，未來 data ownership 追蹤無法建立
3. 無法透過後台設定 Guest 帳號的顯示名稱

### 設計目標

| 需求 | 設計方案 |
|------|----------|
| 免登入模式也有實體帳號 | 在 `main.db` User 表 seed 一筆 Guest 帳號 |
| 後台可設定 Guest 角色 | 保留現有 `Auth.default_role_level` 設定 |
| 後台可設定 Guest 的 Username/DisplayName | 新增 `Auth.guest_account_username`、`Auth.guest_account_display_name` 設定 |
| 右上角顯示 DB 中的名稱 | `SetGuestSession()` 改為從 DB 載入 User 記錄 |
| 未來 data ownership | Guest User 有固定 `Id`（seed 時指定），所有操作記錄可關聯 |

---

## Proposed Changes

### 1. SystemSetting 新增設定項

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

在 `Auth` category 新增兩筆設定：

| Id | Category | Key | Value | Description |
|----|----------|-----|-------|-------------|
| 18 | Auth | guest_account_username | `local_operator` | 免登入時使用的帳號名稱 |
| 19 | Auth | guest_account_display_name | `Local Operator` | 免登入時右上角顯示的名稱 |

> [!NOTE]
> 預設值選用 `local_operator` / `Local Operator` 而非 `Guest`，因為這個帳號代表「本機操作者」而非「訪客」，語義更精確。

---

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

新增兩個便利屬性：

```csharp
/// <summary>免登入時使用的帳號名稱（預設 local_operator）</summary>
public string GuestAccountUsername
    => GetLiveString("Auth", "guest_account_username", "local_operator");

/// <summary>免登入時顯示的名稱（預設 Local Operator）</summary>
public string GuestAccountDisplayName
    => GetLiveString("Auth", "guest_account_display_name", "Local Operator");
```

---

### 2. Guest User Seed 帳號

#### [MODIFY] [UserSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/UserSeed.cs)

新增一筆 Guest User（`Id = 100`，預留空間避免與正式帳號衝突）：

```csharp
new()
{
    Id = 100,
    Username = "local_operator",
    PasswordHash = "",                // 不需密碼（免登入）
    RoleLevel = 1,                    // 由 DB 設定覆寫
    IsActive = 1,
    CreatedAt = now,
    CreatedBy = "SYSTEM",
    DisplayName = "Local Operator",
    ForcePasswordChange = 0,
    Notes = "免登入模式專用帳號，不需密碼。角色等級由 SystemSetting Auth.default_role_level 控制。",
},
```

> [!IMPORTANT]
> **Guest User 使用固定 `Id = 100`**，這樣所有免登入模式下的操作記錄都能追溯到同一個帳號。正式使用者帳號 Id 從 1 開始（目前 1-3），自增 Id 不會跳到 100。如果您偏好其他 Id，請告知。

---

### 3. SessionService 改從 DB 載入

#### [MODIFY] [SessionService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SessionService.cs)

改為兩階段：
1. 先從 `main.db` 讀取 Guest User 記錄（by username from SystemSetting）
2. 用 SystemSetting 的 `default_role_level` 覆寫角色等級
3. 用 SystemSetting 的 `guest_account_display_name` 覆寫顯示名稱

```csharp
/// <summary>免登入模式 — 從 DB 載入 Guest 帳號並套用系統設定</summary>
public void SetGuestSession(User guestUser, int roleLevel, string displayName)
{
    // 使用 DB 實體但覆寫動態設定
    guestUser.RoleLevel = roleLevel;
    guestUser.DisplayName = displayName;
    CurrentUser = guestUser;
    SessionChanged?.Invoke(this, EventArgs.Empty);
}
```

> [!NOTE]
> 簽章從 `SetGuestSession(int roleLevel)` 改為 `SetGuestSession(User guestUser, int roleLevel, string displayName)`。呼叫端（AppShell）需配合調整。

---

### 4. AppShell 呼叫端調整

#### [MODIFY] [AppShell.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/AppShell.xaml.cs)

`OnInitCompleted` 改為先查詢 DB：

```csharp
private async void OnInitCompleted(object? sender, EventArgs e)
{
    // 從 DB 載入 Guest 帳號
    var guestUsername = _systemSettings.GuestAccountUsername;
    var guestUser = await LoadGuestUserAsync(guestUsername);

    if (guestUser == null)
    {
        // Fallback: DB 中無此帳號，建構最小化物件
        guestUser = new User
        {
            Id = 0,
            Username = guestUsername,
            DisplayName = _systemSettings.GuestAccountDisplayName,
            PasswordHash = "",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            CreatedBy = "SYSTEM"
        };
    }

    _sessionService.SetGuestSession(
        guestUser,
        _systemSettings.DefaultRoleLevel,
        _systemSettings.GuestAccountDisplayName);

    _menuPage = null;
    NavigateTo("menu");
}
```

需在 AppShell 中注入 `IServiceProvider` 或 `AppMainDbContext` 來查詢 Guest User。

---

### 5. UserMenuControl XAML 預設值

#### [MODIFY] [UserMenuControl.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Controls/UserMenuControl.xaml)

將 hard code `Text="Guest"` 改為空字串（因為 `Initialize()` 時會被 `RefreshUserDisplay()` 覆寫）：

```xml
<TextBlock x:Name="UserNameText" Text=""
```

---

## 設定變更總覽

| 設定項 | 類型 | 預設值 | 說明 |
|--------|------|--------|------|
| `Auth.login_required` | bool | `0` | 是否需登入（已有） |
| `Auth.default_role_level` | int | `1` | 免登入時的角色等級（已有） |
| `Auth.guest_account_username` | string | `local_operator` | 免登入時的帳號 username（**新增**） |
| `Auth.guest_account_display_name` | string | `Local Operator` | 免登入時右上角顯示名稱（**新增**） |

---

## Open Questions

> [!IMPORTANT]
> **Guest User Id 的選擇**：目前規劃使用 `Id = 100`，正式帳號 1-3。如果未來會有更多正式帳號（例如 10+ 個 operator），需要確認這個間距是否足夠，或是否改用更大的 Id（如 `9999`）。

> [!IMPORTANT]
> **預設名稱偏好**：預設使用 `local_operator` / `Local Operator` 作為免登入帳號名稱。如果您有其他偏好（如 `default_user`、`trio_operator` 等），請告知。

> [!IMPORTANT]
> **AppMainDbContext 注入**：目前 `AppShell` 沒有注入 `AppMainDbContext`。需要透過 `IServiceProvider` 建立 scope 來查詢，或是讓 `SessionService` 直接持有 DB 存取能力。您偏好哪種方式？
> - **方案 A**：`SessionService` 注入 `IServiceProvider`，新增 `LoadGuestSessionFromDb()` 方法（封裝性較好）
> - **方案 B**：`AppShell` 透過已有的 DI 容器查詢（職責分離較清楚）

---

## Verification Plan

### Automated Tests
- 刪除現有 `main.db`，重新啟動確認 Guest User seed 正確建立
- 確認 `system_config.db` 新增設定項正確植入
- 啟動應用確認右上角顯示 `Local Operator` 而非 `Guest`

### Manual Verification
- 修改 DB 中 `Auth.guest_account_display_name` 的值，重新啟動確認顯示隨之變更
- 修改 DB 中 `Auth.default_role_level`，確認角色等級正確套用
