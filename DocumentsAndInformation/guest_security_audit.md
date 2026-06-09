# Guest 免密碼帳號 — 資安審計報告

> 審計者: Office of William | 日期: 2026-05-28

## 架構概述

Guest 帳號 (`id:guest`) 在 `login_required=1` 模式下允許免密碼登入。
Session 僅存在於記憶體 (`SessionService`)，無持久化 Token。

```
攻擊面分析：

  [外部] ──┬── 物理觸控螢幕
            ├── 實體鍵盤輸入
            └── USB / 網路（取決於 IPC lockdown）

  [內部] ──┬── DB 直接修改 (system_config.db / main.db)
            ├── AppShell.NavigateTo() 導航
            └── Service 層 API 呼叫
```

---

## ✅ 已修正項目

| # | 風險 | 層級 | 修正內容 |
|---|------|------|----------|
| 1 | Guest 登入繞過 SystemSetting 開關 | 🔴 High | `LoginViewModel.ExecuteLoginAsync` 新增 `GuestLoginEnabled` 檢查 |
| 2 | Guest 帳號可被刪除 | 🟡 Medium | `AccountManagementService.DeleteUserAsync` 新增 `"guest"` 守衛 |
| 3 | Guest 帳號可被停用/鎖定/改密碼 | 🟡 Medium | `BuildActionButtons` 對 guest/local_operator 僅顯示「檢視」 |
| 4 | 帳號名稱衝突 | 🟢 Low | DB 已有 guest (Id=101)，`USERNAME_EXISTS` 自動阻擋 |

---

## 🔧 待修正項目（本次發現）

### A. Service 層缺少 Guest 保護守衛 🟡 Medium

**問題**：`BuildActionButtons` 在 UI 層阻擋了操作按鈕，但 **Service 層方法未檢查**：
- `SetActiveAsync` — 可停用 guest
- `LockUserAsync` — 可鎖定 guest  
- `ResetPasswordAsync` — 可重設 guest 密碼（guest 不需密碼，給他設密碼反而破壞功能）

**縱深防禦原則**：即使 UI 阻擋了，Service 層也應有守衛。

### B. CreateAccountOverlay 保留名稱提示不明確 🟢 Low

**問題**：當使用者輸入 `guest` 建立帳號時，DB 返回 `USERNAME_EXISTS`。
但錯誤訊息是通用的「帳號已存在」，未說明這是保留名稱。
**建議**：在 `CreateUserAsync` 新增保留名稱檢查，返回 `RESERVED_USERNAME`。

### C. NavigateTo 無權限檢查 🟡 Medium

**問題**：`AppShell.NavigateTo("uv")` 和 `NavigateTo("service")` 無 session 權限檢查。
雖然 MenuPage 的 UV 按鈕已 disabled，但若程式碼有其他觸發路徑，guest 仍可進入。
**建議**：在 `NavigateTo` 的 `"uv"` 和 `"service"` case 加入 `IsGuestLogin` 檢查。

### D. CanLogin 在 GuestLoginEnabled=0 時仍亮起 🟢 Low

**問題**：`IsGuestUser` 判斷不考慮 `GuestLoginEnabled`，所以當功能停用時，
輸入 "guest" → 登入按鈕亮起（但點了會失敗）。不是安全問題但影響 UX。
**建議**：`CanLogin` 中 IsGuestUser 加入 `_systemSettings.GuestLoginEnabled` 條件。

---

## ⚪ 不需處理的項目（設計合理）

| # | 議題 | 說明 |
|---|------|------|
| 1 | Guest 帳號無 session token | ✅ 設計如此。Session 僅在記憶體，App 關閉即消失 |
| 2 | Guest 密碼為空字串 | ✅ 設計如此。`PasswordHash=""` 確保一般登入流程無法匹配 |
| 3 | Guest 繞過 lockout 計數 | ✅ 無密碼=無暴力破解風險，lockout 不適用 |
| 4 | Guest 帳號名稱可被探測 | ✅ Kiosk 環境為封閉系統，非公開網路服務 |
| 5 | Guest ForcePasswordChange 跳過 | ✅ Guest 無密碼，不適用密碼變更流程 |
