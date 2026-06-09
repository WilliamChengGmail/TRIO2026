# 密碼管理 UI 實作計畫（最終版）

> 製作者: Office of William
> 此計畫與「密碼複雜度驗證」及「帳號管理」為平行計畫，待審核通過後一起執行。

---

## 現狀分析

| 項目 | 狀態 |
|------|------|
| `ForcePasswordChange` 欄位 | ✅ DB 存在，種子帳號初始值=1，但無任何程式碼讀取 |
| `PasswordExpiryDays` 欄位 | ✅ DB 存在，**本次不實作** |
| `PasswordChangedAt` 欄位 | ✅ DB 存在，但無任何程式碼寫入 |
| 密碼變更 UI | ❌ 不存在 |
| `AuthService.ChangePasswordAsync` | ❌ 不存在 |

---

## Proposed Changes

### 1. ChangePasswordOverlay（新建）

#### [NEW] Controls/ChangePasswordOverlay.xaml + .xaml.cs

採用 `TaskCompletionSource` 模式（同 LoginOverlay）。

**UI 佈局**：

```
┌───────────────────────────────────────┐
│  🔑 變更密碼                           │
│  請輸入新密碼以完成變更                 │
├───────────────────────────────────────┤
│  當前密碼  [________________] [👁]    │
│  新密碼    [________________] [👁]    │
│  確認密碼  [________________] [👁]    │
│                                       │
│  ── 密碼原則即時驗證 ────────────────  │
│  ✅ 至少 6 碼                          │
│  ❌ 需包含英文字母                     │
│  ✅ 未超過最大長度（20 碼）             │
│                                       │
│  ⚠️ 錯誤訊息區                        │
│                                       │
│  [ 確認變更 ]    [ 取消 ]（選配）      │
└───────────────────────────────────────┘
```

**功能設計**：

| 功能 | 說明 |
|------|------|
| 👁 顯示明碼 | 三個輸入框各有獨立 👁 按鈕，點擊切換明碼/隱碼 |
| 即時原則驗證 | 輸入新密碼時即時逐條顯示 ✅/❌ |
| 新舊密碼比對 | Submit 前驗證不可相同 |
| 確認密碼比對 | 輸入時即時比對一致性 |
| 取消按鈕 | 依 `canCancel` 參數控制顯示/隱藏 |

**即時驗證顯示規則**：
呼叫 `PasswordPolicyService.GetPolicyHint(roleLevel)` 取得規則清單，
每次 `PasswordBox.PasswordChanged` 事件時即時更新各條 ✅/❌：

```
✅ 至少 N 碼            → 字元數 >= min_length
❌ 需包含英文字母       → 含字母（require_mixed=1 才顯示此條）
✅ 未超過最大長度       → 字元數 <= max_length
□  需包含大寫字母       → Admin only（require_upper=1 才顯示）
□  需包含數字          → Admin only（require_digit=1 才顯示）
```

**取消按鈕可見性**：

| 觸發情境 | canCancel | 取消按鈕 |
|----------|-----------|----------|
| ForcePasswordChange 強制 | `false` | ❌ 隱藏 |
| UserMenu 主動觸發 | `true` | ✅ 顯示 |

**公開 API**：

```csharp
public Task<ChangePasswordResult> ShowAsync(
    string title, int roleLevel, bool canCancel = true);

public void ShowError(string message);

public record ChangePasswordResult(
    bool IsCancelled,
    string OldPassword = "",
    string NewPassword = "");
```

---

### 2. ForcePasswordChange 強制變更流程

#### [MODIFY] Views/AppShell.xaml.cs

在 `OnLoginSucceeded()` 登入成功後插入 ForcePasswordChange 檢查：

```
登入成功
    │
    ▼ 檢查 user.ForcePasswordChange == 1
    │
    ├─ = 0 → 正常導航（原流程）
    │
    └─ = 1 → ChangePasswordOverlay(canCancel: false)
                  │
                  ├─ 失敗 → ShowError → 繼續等待
                  │
                  └─ 成功
                        → ForcePasswordChange = 0
                        → PasswordChangedAt = now
                        → ClearSession()
                        → NavigateTo("login")
                        → 提示「密碼已更新，請以新密碼重新登入」
                        → Log: LogAuth("ForcePasswordChanged", username, true)
```

> [!IMPORTANT]
> **強制重新登入**：改密成功後強制登出，要求以新密碼重新登入，確保舊 Session 失效。

---

### 3. UserMenu 主動觸發

「🔑 變更密碼」按鈕（Operator / Admin 可見，**Service 絕對不可見**）：
- `ShowAsync(canCancel: true)` → 使用者可取消
- 成功後：寫 Log、顯示成功訊息
- **不強制重新登入**（主動變更，Session 維持有效）

---

### 4. AuthService.ChangePasswordAsync

#### [MODIFY] Services/AuthService.cs

```csharp
/// <summary>
/// 變更使用者密碼
/// 1. 驗證舊密碼（BCrypt.Verify）
/// 2. PasswordPolicyService.Validate(newPassword, roleLevel)
/// 3. 更新 PasswordHash + PasswordChangedAt + ForcePasswordChange=0
/// </summary>
public async Task<(bool Success, string? Error)> ChangePasswordAsync(
    int userId, string oldPassword, string newPassword)
```

---

## 不在本次實作範圍

| 項目 | 說明 |
|------|------|
| 自訂觸控數字鍵盤 | 後續版本實作 |
| PasswordExpiryDays 效期檢查 | DB 欄位保留，後續版本實作 |
| 密碼歷史記錄 | 防止重複使用 |

---

## 受影響檔案

| 層級 | 檔案 | 操作 | 說明 |
|------|------|------|------|
| App/Controls | `ChangePasswordOverlay.xaml` | NEW | 密碼變更 Overlay UI |
| App/Controls | `ChangePasswordOverlay.xaml.cs` | NEW | Overlay 邏輯 |
| App/Services | `AuthService.cs` | MODIFY | 新增 ChangePasswordAsync |
| App/Views | `AppShell.xaml.cs` | MODIFY | OnLoginSucceeded 加入強制變更檢查 |
| App/Controls | `UserMenuControl.xaml` | MODIFY | 新增「🔑 變更密碼」（併入帳號管理計畫） |
| App/Controls | `UserMenuControl.xaml.cs` | MODIFY | 事件處理（併入帳號管理計畫） |

---

## Open Questions — 全部已決議

| # | 問題 | 決議 |
|---|------|------|
| PW-Q1 | ForcePasswordChange 可否跳過 | ✅ **不可跳過**，必須完成變更才能繼續 |
| PW-Q2 | 觸控自訂鍵盤 | ✅ **本次不實作**，後續版本再做 |
| PW-Q3 | 顯示明碼切換 | ✅ **加入 👁 按鈕**，各輸入框獨立切換 |
| PW-Q4 | 密碼原則提示方式 | ✅ **選項 B**：即時逐條 ✅/❌ 驗證 |
| PW-Q5 | 觸發點（UI 呈現） | ✅ **兩者皆有**：UserMenu 主動 + ForcePasswordChange 強制 |
| PW-Q6 | PasswordExpiryDays | ✅ **本次不實作** |
| PW-Q7 | 取消按鈕可見情境 | ✅ **依觸發情境**：強制時隱藏；主動時顯示 |
