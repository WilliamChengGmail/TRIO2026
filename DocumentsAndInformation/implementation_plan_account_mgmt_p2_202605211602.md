# 帳號管理完整實作計畫 — Part 2：帳號管理頁面 UI 設計

> 製作者: Office of William
> 接續 Part 1，本部分定義 AccountManagementPage 的完整 UI 版面與各功能操作流程。

---

## AccountManagementPage — 主版面設計

### 整體佈局（二段式）

```
┌──────────────────────────────────────────────────────────┐
│  [←]  帳號管理           [使用者選單]                    │  ← 頂部列 H=80
├────────────────────────┬─────────────────────────────────┤
│                        │                                  │
│   帳號列表             │   操作面板                       │  ← 主區域
│   (左側 55%)           │   (右側 45%)                     │
│                        │                                  │
│  ┌──────────────────┐  │  [選擇帳號後才顯示]               │
│  │ 🟢 admin   Admin │  │                                  │
│  │ 🟢 operator Op.. │  │  ┌───────────────────────────┐  │
│  │ 🔴 user02  [停用]│  │  │  帳號：operator            │  │
│  │ 🔒 user03  [鎖定]│  │  │  角色：Operator             │  │
│  └──────────────────┘  │  │  狀態：啟用                 │  │
│                        │  └───────────────────────────┘  │
│  [ + 新增帳號 ]        │                                  │
│                        │  [停用帳號] [刪除帳號]            │
│                        │  [鎖定帳號] [解鎖帳號]            │
│                        │  [重設密碼] [變更密碼]            │
│                        │  [       檢視詳細資料       ]    │
├────────────────────────┴─────────────────────────────────┤
│  版本資訊                                    狀態訊息     │  ← 底部列 H=40
└──────────────────────────────────────────────────────────┘
```

> [!NOTE]
> 七吋螢幕（600×960 Viewbox）考量：帳號列表每列高度 56px，操作按鈕最小高度 52px。
> 二段式設計（左列表 + 右操作）避免單欄式需要過多捲動。

---

## 帳號列表設計

### 列表項目格式（每列 56px）

```
┌──────────────────────────────────────────────────────┐
│  [●]  顯示名稱              角色標籤   [狀態標籤]    │
│       username                                        │
└──────────────────────────────────────────────────────┘
```

| 元素 | 說明 |
|------|------|
| 狀態圓點 [●] | 🟢 啟用 / 🔴 停用 / 🔒 鎖定 |
| 顯示名稱 | `DisplayName ?? Username`，FontSize=18 Bold |
| Username | FontSize=14，次要顏色 |
| 角色標籤 | `Operator` / `Service` / `Admin`，帶色框 |
| 狀態標籤 | 僅停用或鎖定時顯示（`[已停用]` / `[已鎖定]`）|

### 列表排序規則

1. Admin（RoleLevel=3）優先
2. Service（RoleLevel=2）次之
3. Operator（RoleLevel=1）最後
4. 同角色內按 Username 字母排序
5. **停用帳號（IsActive=0）顯示在各組最後**（不隱藏，管理頁需要看到）
6. **`local_operator` 帳號不顯示**（免登入專用帳號，非管理對象）

### 選中狀態

- 點擊列表項目 → 高亮（`Background="#2A4570"`）
- 右側操作面板隨之更新
- 再次點擊同一項目不取消選取（避免誤觸）

---

## 八大功能操作流程

### 1. 新增帳號

**觸發**：點擊「+ 新增帳號」按鈕（位於列表底部）

**UI 行為**：彈出 `CreateAccountOverlay`（新建 Overlay）

```
┌─────────────────────────────────────┐
│         ➕ 新增帳號                  │
├─────────────────────────────────────┤
│  帳號名稱   [__________________]    │
│  顯示名稱   [__________________]    │
│  角色       [  Operator       ▼]    │
│  （Operator / Admin）               │
│                                     │
│  ⚠️ Service 帳號由 DB 直接管理      │
│  初始密碼由系統隨機產生並顯示一次   │
│                                     │
│  [ 建立 ]  [ 取消 ]                 │
└─────────────────────────────────────┘
```

**流程**：
1. 驗證帳號名稱：英數字 + 底線，長度 3~20，不可重複
2. 角色選擇：僅限 Operator / Admin（Service 不在 UI 建立）
3. 系統隨機產生臨時密碼（12碼，英數混合）
4. 呼叫 `AccountManagementService.CreateUserAsync()`
5. 設定 `ForcePasswordChange=1`，新使用者首次登入強制改密碼
6. 顯示成功對話框，**明碼顯示臨時密碼**（一次性，需抄錄）
7. 寫入 EventLog：`AccountCreated`

> [!IMPORTANT]
> **角色限制**：UI 介面不允許建立 Service 帳號，Service 帳號由工廠/IT 部門透過 DB 直接建立。

---

### 2. 刪除帳號

**觸發**：選取帳號 → 點擊「刪除帳號」

**流程**：
1. 安全守衛：**不可刪除自己**（當前登入 Admin）
2. 安全守衛：**不可刪除唯一的 Admin 帳號**（系統至少保留一個）
3. 安全守衛：**不可刪除 local_operator 帳號**
4. 顯示 OverlayDialog 二次確認（`OverlayDialogIcon.Warning`）
5. 確認後呼叫 `AccountManagementService.DeleteUserAsync()`
6. 寫入 EventLog：`AccountDeleted`

---

### 3. 停用 / 啟用帳號

**觸發**：選取帳號 → 點擊「停用帳號」（帳號啟用時顯示）或「啟用帳號」（帳號停用時顯示）

**流程**：
1. 安全守衛：**不可停用自己**
2. 安全守衛：**不可停用唯一啟用的 Admin 帳號**
3. 確認對話框
4. `IsActive` 切換 0/1
5. 寫入 EventLog：`AccountDisabled` / `AccountEnabled`

> [!NOTE]
> 停用的帳號不會出現在登入頁面的下拉選單中（`GetAllUsersAsync()` 已過濾 `IsActive=1`）

---

### 4. 鎖定帳號

**觸發**：選取帳號 → 點擊「鎖定帳號」

**前提**：`AccountLockEnabled`（SystemSetting 新增設定）= 1 才顯示此按鈕

**流程**：
1. 安全守衛：**不可鎖定自己**
2. 確認對話框：「鎖定後該使用者將無法登入，直到解鎖為止。」
3. 設定 `LockedUntil = DateTime.MaxValue.ToString("O")`（永久鎖定，直到手動解鎖）
4. 寫入 EventLog：`AccountLocked`

---

### 5. 解鎖帳號

**觸發**：選取已鎖定帳號 → 點擊「解鎖帳號」

**前提**：`AccountLockEnabled` = 1 才顯示此按鈕

**流程**：
1. 設定 `LockedUntil = null`，`FailedLoginCount = 0`
2. 寫入 EventLog：`AccountUnlocked`

---

### 6. 忘記密碼（Admin 觸發重設）

**觸發**：選取帳號 → 點擊「重設密碼」

**流程**：
```
Admin 點擊「重設密碼」
     │
     ▼
確認對話框：「將為 [DisplayName] 重設密碼，新密碼將顯示一次，請務必記錄。」
     │
     ▼
系統產生臨時密碼（12碼隨機英數混合）
     │
     ▼
呼叫 AccountManagementService.ResetPasswordAsync()
  → 更新 PasswordHash（BCrypt）
  → 設定 ForcePasswordChange = 1
  → 清除 LockedUntil / FailedLoginCount
     │
     ▼
顯示結果對話框（一次性明碼顯示）：
┌──────────────────────────────────┐
│  ✅ 密碼已重設                   │
│                                  │
│  臨時密碼：                      │
│  ┌────────────────────────────┐  │
│  │   Xk9mP2qL8nRw            │  │
│  └────────────────────────────┘  │
│  請將此密碼提供給使用者           │
│  使用者登入後系統將強制要求       │
│  變更密碼，並以新密碼重新登入     │
│                                  │
│            [ 我已記錄 ]          │
└──────────────────────────────────┘
     │
     ▼
寫入 EventLog：AccountPasswordReset
```

> [!IMPORTANT]
> **強制重新登入流程**：使用者以臨時密碼登入 → 系統偵測 `ForcePasswordChange=1` → 強制跳出 `ChangePasswordOverlay`（不可取消）→ 密碼變更成功後 → **強制登出，要求以新密碼重新登入**（而非直接進入系統）

---

### 7. 變更密碼（Admin 變更自己的密碼）

**觸發**：UserMenu → 「🔑 變更密碼」（Admin 自己觸發）

**UI**：呼叫 `ChangePasswordOverlay`（由 `implementation_plan_password_ui` 定義）

**流程**：
1. 輸入舊密碼驗證身份
2. 輸入新密碼（符合 `PasswordPolicyService`，Admin 規則）
3. 呼叫 `AuthService.ChangePasswordAsync()`
4. 成功後寫入 EventLog：`PasswordChanged`
5. 清除 `ForcePasswordChange`

---

### 8. 檢視使用者

**觸發**：選取帳號 → 點擊「檢視詳細資料」

**UI 行為**：操作面板區域展開詳細資訊（不另開 Overlay，在右側面板就地顯示）

**顯示欄位**：

| 欄位 | 顯示 |
|------|------|
| Username | 唯讀文字 |
| DisplayName | 唯讀文字 |
| RoleLevel | 唯讀（Operator / Service / Admin） |
| IsActive | 唯讀（啟用 / 停用） |
| EmployeeId | 唯讀 |
| Department | 唯讀 |
| Email | 唯讀 |
| LastLoginAt | 唯讀（格式化時間） |
| PasswordChangedAt | 唯讀 |
| ForcePasswordChange | 唯讀（是 / 否） |
| LockedUntil | 唯讀（鎖定中 / 未鎖定） |
| FailedLoginCount | 唯讀 |
| CreatedAt / CreatedBy | 唯讀 |
| Notes | 唯讀 |

---

## Open Questions（Part 2）

> [!IMPORTANT]
> **Q-P2-1：ForcePasswordChange=1 後的強制重新登入是否必要？**
> - 當前計畫：臨時密碼登入 → 強制改密碼 → 強制登出 → 要求以新密碼重新登入
> - 替代方案：改密碼成功後直接進入系統（不強制重新登入）
> - **建議**：強制重新登入（確保舊的 Session Token 失效，醫療級系統標準做法）

> [!IMPORTANT]
> **Q-P2-2：帳號列表是否顯示 Service 帳號？**
> - Service 帳號（RoleLevel=2）由 DB 直接管理，UI 不建立
> - 但 Admin 是否可以在列表中看到 Service 帳號（做停用/鎖定操作）？
> - **建議**：顯示 Service 帳號，但「刪除」與「新增」操作受限

> [!IMPORTANT]
> **Q-P2-3：帳號是否支援編輯資料（DisplayName / Department / Email）？**
> - 目前計畫「檢視使用者」僅唯讀顯示
> - 是否需要在本次加入「編輯帳號資料」功能（DisplayName / Notes 可編輯）？

---

*Part 3 將涵蓋：服務層設計（AccountManagementService）+ SystemSetting 新增 + i18n 鍵值清單 + AppShell 路由*
