# 密碼複雜度與長度驗證系統

## 目標

在 TRIO2026 帳號系統中實作**依角色分級**的密碼複雜度驗證，所有規則由 `system_config.db` (SystemSetting) 控制啟停，可不重啟即時生效。

---

## 設計原則

> [!IMPORTANT]
> **臨床友善**：Operator 在 7 吋觸控螢幕、可能戴手套操作，過於複雜的密碼反而導致「密碼貼紙」問題。
> **BCrypt 硬限**：密碼有效長度上限 72 bytes（純 ASCII = 72 字元），超出部分被靜默截斷。

---

## 現有密碼驗證機制盤點

| 層級 | 元件 | 現狀 |
|------|------|------|
| **UI — LoginPage** | `LoginViewModel.CanLogin` | 僅檢查 `!IsNullOrWhiteSpace(Password)` |
| **UI — LoginOverlay** | `ConfirmButton_Click` | 僅檢查 `!IsNullOrEmpty(password)` |
| **Service — AuthService** | `LoginAsync()` | 無長度/複雜度驗證，直接 BCrypt.Verify |
| **Service — AuthService** | `HashPassword()` | 無驗證，直接 BCrypt.HashPassword |
| **DB — User 實體** | `PasswordHash` | `IsRequired()` 但允許空字串 |
| **DB — SystemSetting** | LoginUI 分類 | 無密碼複雜度相關設定 |

**結論**：目前系統在任何層級都**沒有**密碼長度或複雜度驗證。

---

## Proposed Changes

### 1. 新增 DB 設定（SystemSettingSeed）

新增 `PasswordPolicy` 分類，所有規則可透過 DB 即時控制：

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

新增以下 Seed 項目（Id=22~30）：

| Id | Key | 預設值 | 說明 |
|----|-----|--------|------|
| 22 | `enabled` | `1` | 密碼原則是否啟用（0=不檢查，任何密碼都放行） |
| 23 | `operator_min_length` | `6` | Operator 最短密碼長度 |
| 24 | `operator_max_length` | `20` | Operator 最大密碼長度 |
| 25 | `operator_require_mixed` | `0` | Operator 是否要求英數混合（0=允許純數字 PIN） |
| 26 | `admin_min_length` | `10` | Admin/Service 最短密碼長度 |
| 27 | `admin_max_length` | `64` | Admin/Service 最大密碼長度（BCrypt 72B 安全範圍內） |
| 28 | `admin_require_upper` | `1` | Admin/Service 是否要求含大寫字母 |
| 29 | `admin_require_lower` | `1` | Admin/Service 是否要求含小寫字母 |
| 30 | `admin_require_digit` | `1` | Admin/Service 是否要求含數字 |

> [!NOTE]
> `operator_require_mixed=0` 讓 Operator 可以使用純數字 PIN（如 `123456`），適合 7 吋觸控螢幕環境。
> 設為 `1` 時要求至少包含英文字母和數字。

---

### 2. SystemSettingService 便利屬性

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

新增 `PasswordPolicy` 區塊的便利屬性，全部為即時 DB 讀取：

```csharp
// ═══════════════════════════════════════
// Password Policy 設定
// ═══════════════════════════════════════

bool PasswordPolicyEnabled          // enabled
int  OperatorMinLength              // operator_min_length
int  OperatorMaxLength              // operator_max_length
bool OperatorRequireMixed           // operator_require_mixed
int  AdminMinLength                 // admin_min_length
int  AdminMaxLength                 // admin_max_length
bool AdminRequireUpper              // admin_require_upper
bool AdminRequireLower              // admin_require_lower
bool AdminRequireDigit              // admin_require_digit
```

---

### 3. 密碼驗證服務

#### [NEW] [PasswordPolicyService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/PasswordPolicyService.cs)

獨立的密碼原則驗證服務，不耦合 AuthService：

```csharp
public class PasswordPolicyService
{
    /// <summary>
    /// 驗證密碼是否符合指定角色的原則
    /// </summary>
    /// <returns>null = 通過, string = 錯誤訊息</returns>
    public string? Validate(string password, int roleLevel);

    /// <summary>
    /// 取得指定角色的密碼提示文字（用於 UI 顯示）
    /// </summary>
    public string GetPolicyHint(int roleLevel);
}
```

**驗證邏輯**（依序檢查）：
1. `PasswordPolicyEnabled=0` → 直接通過
2. 檢查空值/空白
3. 根據 `roleLevel` 選擇規則集：
   - `roleLevel=1` (Operator) → Operator 規則
   - `roleLevel>=2` (Service/Admin) → Admin 規則
4. 檢查最短長度
5. 檢查最大長度（含 BCrypt 72B 上限保護）
6. 檢查字元組成要求

**DI 註冊**：`Transient`（依賴 SystemSettingService 的即時讀取）

---

### 4. 整合至密碼變更流程

> [!IMPORTANT]
> **密碼原則僅在「設定/變更密碼」時驗證，不在「登入」時驗證。**
> 
> 原因：如果管理員事後調嚴規則，已存在的密碼不應阻擋使用者登入。
> 登入時走 BCrypt 驗證即可。可搭配 `ForcePasswordChange=1` 強制使用者下次登入時更新密碼。

#### [MODIFY] [AuthService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/AuthService.cs)

- 新增 `ChangePasswordAsync(userId, oldPassword, newPassword)` 方法
- 在此方法中呼叫 `PasswordPolicyService.Validate(newPassword, roleLevel)`
- 驗證通過後更新 `PasswordHash` + `PasswordChangedAt` + 重設 `ForcePasswordChange=0`

```csharp
public async Task<(bool Success, string? Error)> ChangePasswordAsync(
    int userId, string oldPassword, string newPassword)
```

---

### 5. SystemSetting 實體 docstring 更新

#### [MODIFY] [SystemSetting.cs](file:///d:/TRIO2026/src/TRIO2026.Core/Entities/SystemSetting.cs)

在分類列表中加入 `PasswordPolicy` 分類。

---

## 未涉及範圍（後續可擴充）

以下項目**不在本次實作範圍**，但設計上已預留擴充空間：

| 項目 | 說明 |
|------|------|
| 密碼變更 UI 頁面 | 需要專門的 ChangePasswordOverlay（類似 LoginOverlay） |
| ForcePasswordChange 流程 | 登入成功後檢查並強制跳轉到密碼變更頁面 |
| PasswordExpiryDays 效期檢查 | 密碼到期後強制變更 |
| 密碼歷史記錄 | 防止重複使用最近 N 組密碼 |

---

## Open Questions

> [!IMPORTANT]
> **Q1**：是否需要 `operator_require_special`（Operator 要求特殊符號）？根據需求描述，Operator 應該允許純數字 PIN，所以此設定可能不需要。
> 
> **Q2**：Admin/Service 是否需要 `admin_require_special`（要求特殊符號如 `!@#$%`）？需求中僅提到「大小寫英文 + 數字」，未要求特殊符號。
>
> **Q3**：密碼最大長度 Admin 預設值建議 `64`（BCrypt 72B 安全範圍內，保留 8B 餘裕給 UTF-8 字元），是否合適？

---

## Verification Plan

### Automated Tests
- `PasswordPolicyService` 單元測試：各角色規則的通過/拒絕邊界測試
- Seed 資料完整性：新增的 9 筆設定均可正確寫入 DB

### Manual Verification
- 建置成功，0 error / 0 warning
- 修改 DB 中 `PasswordPolicy` 設定值後，不重啟即可生效
