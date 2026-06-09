# Guest 特殊帳號功能實作計畫

> 撰寫者: Office of William | 日期: 2026-05-28

## 背景

新增一個 `guest` 特殊帳號，與現有的「免登入模式」(`login_required=0` + `local_operator`) 不同。
Guest 帳號是**在登入模式下 (`login_required=1`) 的免密碼特權帳號**，透過 DB 管理和 SystemSetting 開關控制。

### 與現有 IsGuestMode 的區別

| 維度 | 現有 IsGuestMode (免登入) | 新 Guest 帳號 |
|------|--------------------------|---------------|
| 觸發條件 | `login_required=0` | `login_required=1` + `guest_account_enabled=1` |
| 登入畫面 | 不顯示 | 顯示，選擇 guest 後免密碼登入 |
| DB 帳號 | `local_operator` (Id=100) | **`guest` (Id=101)** |
| Session 標記 | `IsGuestMode=true` | `IsGuestLogin=true` (新增) |
| UserMenu | 有 ServiceMode 切換 | **僅 Home + Logout + 語系(可選)** |
| 功能限制 | 全部可用 (Operator 權限) | **Setting + UV disabled** |

---

## Open Questions

> [!IMPORTANT]
> **Guest 帳號是否應出現在帳號管理的使用者清單中？**
> 建議：不顯示（與 `local_operator` 相同，排除系統帳號）。

> [!IMPORTANT]
> **Guest 登入後 Data 功能是否可用？**
> 目前規格僅提到 Setting 和 UV disabled。建議 Data 保持可用（唯讀查看）。

---

## Proposed Changes

### 1. DB 種子資料 — User

#### [MODIFY] [UserSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/UserSeed.cs)

新增 `guest` 帳號 (Id=101)：

```csharp
new()
{
    Id = 101,
    Username = "guest",
    PasswordHash = "",              // 免密碼
    RoleLevel = 1,                  // Operator
    IsActive = 1,
    CreatedAt = now,
    CreatedBy = "SYSTEM",
    DisplayName = "Anonymous",
    ForcePasswordChange = 0,
    Notes = "Guest 特殊帳號。免密碼登入，由 SystemSetting 控制啟停。",
},
```

---

### 2. DB 種子資料 — SystemSetting

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

新增兩筆 Auth 類別設定：

| Id | Category | Key | 預設值 | 說明 |
|----|----------|-----|--------|------|
| 37 | Auth | `guest_login_enabled` | `0` | 是否啟用 Guest 免密碼登入 |
| 38 | Auth | `guest_multilanguage_enabled` | `0` | Guest 登入後是否啟用語系切換 |

---

### 3. SystemSettingService 新增屬性

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

```csharp
/// <summary>是否啟用 Guest 免密碼帳號（預設 false）</summary>
public bool GuestLoginEnabled
    => GetLiveString("Auth", "guest_login_enabled", "0") == "1";

/// <summary>Guest 登入後是否允許切換語系</summary>
public bool GuestMultiLanguageEnabled
    => GetLiveString("Auth", "guest_multilanguage_enabled", "0") == "1";
```

---

### 4. SessionService 新增 Guest Login 標記

#### [MODIFY] [SessionService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SessionService.cs)

新增 `IsGuestLogin` 屬性，區別於 `IsGuestMode`（免登入）：

```csharp
/// <summary>是否為 Guest 免密碼登入（與 IsGuestMode 免登入模式不同）</summary>
public bool IsGuestLogin { get; private set; }

// SetCurrentUser 中：若 username == "guest" 則標記
public void SetCurrentUser(User user)
{
    CurrentUser = user;
    IsGuestMode = false;
    IsGuestLogin = user.Username == "guest";
    SessionChanged?.Invoke(this, EventArgs.Empty);
}

// ClearSession 中重置
public void ClearSession()
{
    CurrentUser = null;
    IsGuestMode = false;
    IsGuestLogin = false;
    SessionChanged?.Invoke(this, EventArgs.Empty);
}
```

---

### 5. LoginViewModel — Guest 免密碼邏輯

#### [MODIFY] [LoginViewModel.cs](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/LoginViewModel.cs)

**CanLogin 修改**：當 Username 為 "guest" 時忽略密碼檢查：

```csharp
public bool CanLogin => !string.IsNullOrWhiteSpace(Username) &&
                        (IsGuestUser || !string.IsNullOrWhiteSpace(Password)) &&
                        !IsLoading;

/// <summary>當前輸入的帳號是否為 Guest 帳號</summary>
public bool IsGuestUser => Username?.Trim().Equals("guest", 
    StringComparison.OrdinalIgnoreCase) == true;
```

**ExecuteLoginAsync 修改**：Guest 帳號使用特殊 token 登入（跳過密碼驗證）：

```csharp
if (IsGuestUser)
{
    // Guest 免密碼登入 — 直接載入 guest 帳號
    var guestUser = await _authService.GetUserByUsernameAsync("guest");
    if (guestUser != null && guestUser.IsActive == 1)
    {
        _sessionService.SetCurrentUser(guestUser);
        EventLogService.Instance.LogAuth("Login", "guest", true, "GuestLogin");
        LoginSucceeded?.Invoke(this, EventArgs.Empty);
        return;
    }
    ErrorMessage = "Guest account is disabled";
    return;
}
// ... 原有密碼登入邏輯
```

**Username setter 修改**：觸發 `IsGuestUser` 更新通知：

```csharp
set { SetProperty(ref _username, value); 
      OnPropertyChanged(nameof(CanLogin)); 
      OnPropertyChanged(nameof(IsGuestUser)); }
```

---

### 6. LoginPage — 密碼框控制

#### [MODIFY] [LoginPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/LoginPage.xaml.cs)

**下拉選單模式**：選到 guest 時 disable 密碼框
**文字輸入模式**：輸入 "guest" 後 disable 密碼框

```csharp
// UserDropdown_SelectionChanged 或 Username TextChanged 時
private void UpdatePasswordBoxState()
{
    bool isGuest = _viewModel.IsGuestUser && _settings.GuestLoginEnabled;
    PasswordBox.IsEnabled = !isGuest;
    if (isGuest)
    {
        PasswordBox.Password = "";
        _viewModel.Password = "";
    }
}
```

> [!WARNING]
> 若 `GuestLoginEnabled=0`，即使輸入 "guest" 仍需密碼（但 guest 帳號沒有密碼所以登入會失敗）。
> 這是刻意設計：透過 SystemSetting 開關可完全啟停此功能。

---

### 7. UserMenuControl — Guest 精簡選單

#### [MODIFY] [UserMenuControl.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Controls/UserMenuControl.xaml.cs)

`RefreshUserDisplay()` 增加 `IsGuestLogin` 分支：

```csharp
if (_sessionService.IsGuestLogin)
{
    UserRoleText.Visibility = Visibility.Collapsed;
    BtnLogout.Tag = loc["UserMenu.Logout"];

    // Guest：隱藏所有功能性按鈕
    BtnServiceMode.Visibility = Visibility.Collapsed;
    ServiceModeSeparator.Visibility = Visibility.Collapsed;
    BtnChangePassword.Visibility = Visibility.Collapsed;
    ChangePasswordSeparator.Visibility = Visibility.Collapsed;
    BtnAccountMgmt.Visibility = Visibility.Collapsed;
    AccountMgmtSeparator.Visibility = Visibility.Collapsed;

    // Home：依頁面位置決定
    BtnHome.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;
    HomeSeparator.Visibility = ShowHomeButton ? Visibility.Visible : Visibility.Collapsed;

    // 語系：由 guest_multilanguage_enabled 控制
    var guestLang = _systemSettings?.GuestMultiLanguageEnabled ?? false;
    BtnSwitchLanguage.Visibility = guestLang ? Visibility.Visible : Visibility.Collapsed;
    LangSeparator.Visibility = guestLang ? Visibility.Visible : Visibility.Collapsed;
}
```

---

### 8. MenuPage — 功能限制

#### [MODIFY] [MenuPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/MenuPage.xaml.cs)

Guest 登入後 Setting + UV 按鈕 disabled：

```csharp
// 在 constructor 或 RefreshUserDisplay 中加入：
if (_sessionService.IsGuestLogin)
{
    BtnSetting.IsEnabled = false;
    BtnUV.IsEnabled = false;
}
```

---

### 9. AuthService — 新增 GetUserByUsernameAsync

#### [MODIFY] [AuthService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/AuthService.cs)

新增方法供 Guest 登入使用：

```csharp
public async Task<User?> GetUserByUsernameAsync(string username)
{
    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppMainDbContext>();
    return await db.Users.FirstOrDefaultAsync(
        u => u.Username == username && u.IsDeleted == 0);
}
```

---

### 10. AppShell — Guest 登入後 ForcePasswordChange 跳過

#### [MODIFY] [AppShell.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/AppShell.xaml.cs)

```csharp
// OnLoginSucceeded 中，Guest 帳號不檢查 ForcePasswordChange
if (user != null && user.ForcePasswordChange == 1 && !_sessionService.IsGuestLogin)
{
    // ... 原有強制密碼變更邏輯
}
```

---

## 修改檔案清單

| # | 檔案 | 操作 | 說明 |
|---|------|------|------|
| 1 | `UserSeed.cs` | MODIFY | 新增 guest 帳號 (Id=101) |
| 2 | `SystemSettingSeed.cs` | MODIFY | 新增 2 筆設定 (Id=37,38) |
| 3 | `SystemSettingService.cs` | MODIFY | 新增 2 個便利屬性 |
| 4 | `SessionService.cs` | MODIFY | 新增 IsGuestLogin 屬性 |
| 5 | `LoginViewModel.cs` | MODIFY | IsGuestUser + 免密碼登入邏輯 |
| 6 | `LoginPage.xaml.cs` | MODIFY | 密碼框 disable + UI 控制 |
| 7 | `UserMenuControl.xaml.cs` | MODIFY | Guest 精簡選單 |
| 8 | `MenuPage.xaml.cs` | MODIFY | Setting/UV disabled |
| 9 | `AuthService.cs` | MODIFY | 新增 GetUserByUsernameAsync |
| 10 | `AppShell.xaml.cs` | MODIFY | Guest 跳過 ForcePasswordChange |

---

## Verification Plan

### Automated Tests
- `dotnet build` 確認無編譯錯誤

### Manual Verification
1. **`guest_login_enabled=0`**：guest 帳號不出現在下拉 / 輸入 guest 仍需密碼
2. **`guest_login_enabled=1` + 下拉模式**：選擇 guest → 密碼框 disable → 登入成功 → UserMenu 僅 Home+Logout
3. **`guest_login_enabled=1` + 文字模式**：輸入 guest → 密碼框 disable → 登入成功
4. **Setting/UV disabled**：Guest 登入後確認按鈕灰化
5. **`guest_multilanguage_enabled=1`**：UserMenu 出現語系切換
6. **`guest_multilanguage_enabled=0`**：UserMenu 無語系切換
7. **登出再登入**：確認狀態正確清除
