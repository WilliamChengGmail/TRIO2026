# 帳號管理完整實作計畫 — Part 2：帳號管理頁面 UI 設計（修訂版）

> 製作者: Office of William
> 接續 Part 1，本部分定義 AccountManagementPage 的完整 UI 版面與各功能操作流程。

---

## AccountManagementPage — 主版面設計

### 整體佈局（二段式）

```
┌──────────────────────────────────────────────────────────┐
│  [←]  帳號管理                           [使用者選單]   │  ← 頂部列 H=80
├────────────────────────┬─────────────────────────────────┤
│                        │                                  │
│   帳號列表（左 55%）   │   操作面板（右 45%）             │
│                        │                                  │
│  ┌──────────────────┐  │  （未選取帳號時：灰色提示文字）  │
│  │ [A] Admin        │  │  「請從左側選擇一個帳號」        │
│  │     administrator│  │                                  │
│  ├──────────────────┤  │  （選取後顯示操作按鈕區）        │
│  │ [O] Operator     │  │                                  │
│  │     operator     │  │                                  │
│  ├──────────────────┤  │                                  │
│  │ [O] 🔴 user02   │  │                                  │
│  │     [已停用]     │  │                                  │
│  └──────────────────┘  │                                  │
│                        │                                  │
│  [ + 新增帳號 ]        │                                  │
├────────────────────────┴─────────────────────────────────┤
│  版本資訊                                    狀態訊息     │  ← 底部列 H=40
└──────────────────────────────────────────────────────────┘
```

---

## 帳號列表視覺設計（七吋螢幕優化）

> [!IMPORTANT]
> 七吋觸控面板需確保**易閱讀性優先**：
> - 每列高度 **64px**（比原 56px 再高，讓手套觸控更容易點到正確列）
> - 角色以**色塊標籤**區分，不依賴文字大小
> - 帳號名稱（DisplayName）為主要資訊，`Username` 為次要輔助
> - 狀態（停用/鎖定）以**顏色 + 圖示**雙重表達，色盲友善

### 列表項目設計（每列 64px）

```
┌──────────────────────────────────────────────────────────┐
│  ┌────┐  顯示名稱（DisplayName）     ┌──────────┐        │  ← 主資訊 20px Bold
│  │ A  │  username（次要）            │  Admin   │  🔴    │  ← 角色色塊 + 狀態圖示
│  └────┘                              └──────────┘        │
└──────────────────────────────────────────────────────────┘
```

| 元素 | 規格 | 說明 |
|------|------|------|
| 角色縮寫圓框 | 48×48px，CornerRadius=24 | A=Admin(藍)、S=Service(橘)、O=Operator(青) |
| 顯示名稱 | FontSize=**20** Bold，Foreground=#F0F4F8 | 可能較長，超出用省略號 |
| Username | FontSize=**14**，Foreground=#7B8FA8 | 輔助識別 |
| 角色標籤 | FontSize=**14**，Padding=6,3，CornerRadius=4 | 帶背景色框 |
| 狀態圖示 | FontSize=**20** | 🟢啟用(隱藏)、🔴停用、🔒鎖定 |

### 角色色彩規格

| 角色 | 縮寫 | 圓框色 | 標籤背景 | 標籤文字 |
|------|------|--------|----------|----------|
| Admin (3) | A | `#1565C0` (深藍) | `#1A3A6A` | `#64B5F6` |
| Service (2) | S | `#E65100` (深橘) | `#4A2000` | `#FFB74D` |
| Operator (1) | O | `#00695C` (深青) | `#003C36` | `#4DB6AC` |

### 停用 / 鎖定 的視覺降階

- **停用帳號**：整列 `Opacity=0.55`，角色縮寫圓框灰化（`#455A64`），圖示 🔴
- **鎖定帳號**：整列 `Opacity=0.75`，標籤右側加 🔒 圖示，角色色彩保留

### 列表排序規則

1. Admin（RoleLevel=3）優先
2. Service（RoleLevel=2）次之
3. Operator（RoleLevel=1）最後
4. 同角色內按 DisplayName 字母排序
5. **停用帳號排在各組最後**（管理頁需要看到，但視覺降階）
6. **`local_operator` 及 `IsDeleted=1` 帳號不顯示**
7. **Service 帳號（RoleLevel=2）顯示於列表**，但操作受限：
   - ✅ 可執行：停用 / 鎖定 / 解鎖 / 重設密碼 / 檢視詳細資料
   - ❌ 不可執行：UI 新增（Service 帳號由 IT/DB 直接建立） / UI 刪除（由工廠端管理）

### 選中狀態

- 點擊列表項目 → 左側邊框高亮（`BorderLeft 3px #42A5F5`） + 背景 `#1A3050`
- 右側操作面板隨之刷新
- 再次點擊同一項目不取消選取（避免誤觸）

---

## 假刪除（Soft Delete）設計

> [!IMPORTANT]
> 「刪除」帳號不會真正從資料庫移除，而是標記為已刪除狀態，保留完整稽核軌跡。

### User 實體新增欄位

```csharp
// ── 假刪除 ──

/// <summary>是否已刪除（軟刪除）：0=正常, 1=已刪除</summary>
public int IsDeleted { get; set; } = 0;

/// <summary>刪除時間（ISO8601），null 表示未刪除</summary>
public string? DeletedAt { get; set; }

/// <summary>刪除操作者帳號</summary>
public string? DeletedBy { get; set; }
```

### 假刪除邏輯規則

| 場景 | 行為 |
|------|------|
| 帳號管理列表 | 過濾 `IsDeleted=0`（已刪除不顯示） |
| 登入下拉選單 | 過濾 `IsActive=1 AND IsDeleted=0` |
| AuthService.LoginAsync() | 新增 `IsDeleted=0` 過濾條件 |
| 新增帳號時 Username 唯一性檢查 | 含已刪除帳號一起比對（防止 Username 重複）|
| EventLog | 記錄刪除者、刪除時間、被刪帳號資訊 |

### AppMainDbContext 新增全域過濾（選配）

若使用 EF Core 全域查詢篩選器（Global Query Filter），可在 `AppMainDbContext` 加入：
```csharp
modelBuilder.Entity<User>().HasQueryFilter(u => u.IsDeleted == 0);
```
> [!NOTE]
> 若使用全域過濾，帳號管理的「真正查詢含已刪除」需用 `.IgnoreQueryFilters()`。
> 建議：**本次不使用全域過濾**，由各查詢點手動加入 `IsDeleted=0` 條件，較為直觀。

---

## 八大功能操作流程

### 操作面板佈局（選取帳號後右側顯示）

```
┌─────────────────────────────────────────┐
│  顯示名稱（大字 22px Bold）              │
│  username • 角色標籤 • 狀態             │
├─────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  │
│  │  🚫 停用帳號  │  │  🗑️ 刪除帳號  │  │  ← 52px
│  └───────────────┘  └───────────────┘  │
│  ┌───────────────┐  ┌───────────────┐  │
│  │  🔒 鎖定帳號  │  │  🔓 解鎖帳號  │  │  ← 52px（lock_enabled=1 才顯示）
│  └───────────────┘  └───────────────┘  │
│  ┌───────────────┐  ┌───────────────┐  │
│  │  🔄 重設密碼  │  │  🔑 變更密碼  │  │  ← 52px
│  └───────────────┘  └───────────────┘  │
│  ┌─────────────────────────────────┐   │
│  │      👁️ 檢視詳細資料            │   │  ← 52px
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

> [!NOTE]
> 「停用帳號」/ 「啟用帳號」依當前帳號狀態互相切換（同一按鈕位置，文字/圖示切換）。
> 「變更密碼」：Admin 幫自己改密碼（選自己時顯示），或未來擴充為替其他帳號設密碼。

---

### 1. 新增帳號

**觸發**：點擊「+ 新增帳號」按鈕（位於列表底部）
**Log**：`LogButtonClick("AccountMgmt", "AddAccount")`

**UI 行為**：彈出 `CreateAccountOverlay`（新建 Overlay，非全頁切換）

```
┌─────────────────────────────────────┐
│         ➕ 新增帳號                  │
├─────────────────────────────────────┤
│  帳號名稱   [__________________]    │
│  顯示名稱   [__________________]    │
│  角色       [  Operator       ▼]    │
│  （選項：Operator / Admin）          │
│                                     │
│  ℹ️ Service 帳號由 IT/DB 直接管理   │
│  ℹ️ 初始密碼由系統隨機產生並顯示一次│
│                                     │
│  [ 建立 ]         [ 取消 ]          │
└─────────────────────────────────────┘
```

**流程**：
1. 驗證帳號名稱：英數字 + 底線，長度 3~20
2. 檢查 Username 唯一性（含已刪除帳號）
3. 角色選擇：僅限 Operator(1) / Admin(3)
4. 系統隨機產生 12 碼臨時密碼（大小寫英數混合）
5. 呼叫 `AccountManagementService.CreateUserAsync()`
6. 設定 `ForcePasswordChange=1`，`IsDeleted=0`
7. 顯示臨時密碼（一次性，需抄錄）
8. **Log**：`LogAuth("AccountCreated", newUsername, true, $"RoleLevel={roleLevel}, CreatedBy={adminUser}")`

---

### 2. 刪除帳號（假刪除）

**觸發**：選取帳號 → 點擊「🗑️ 刪除帳號」
**Log**：`LogButtonClick("AccountMgmt", "DeleteAccount", $"Target={username}")`

**安全守衛**（依序檢查）：
1. 不可刪除自己（操作者 = 目標）
2. 不可刪除唯一啟用且未刪除的 Admin 帳號
3. 不可刪除 `local_operator`

**流程**：
1. 通過守衛後顯示確認對話框
2. 呼叫 `AccountManagementService.DeleteUserAsync()`
   - 設定 `IsDeleted = 1`
   - 設定 `IsActive = 0`
   - 設定 `DeletedAt = DateTime.UtcNow.ToString("O")`
   - 設定 `DeletedBy = operatorUsername`
3. 列表移除該帳號（不再顯示）
4. **Log**：`LogAuth("AccountDeleted", targetUsername, true, $"SoftDelete, DeletedBy={adminUser}")`

---

### 3. 停用 / 啟用帳號

**觸發**：選取帳號 → 點擊「🚫 停用帳號」或「✅ 啟用帳號」
**Log**：`LogButtonClick("AccountMgmt", "ToggleActive", $"Target={username}, Action={disable|enable}")`

**安全守衛**：
1. 不可停用自己
2. 不可停用唯一啟用的 Admin 帳號

**流程**：確認對話框 → 切換 `IsActive` → 刷新列表
**Log**：`LogAuth("AccountDisabled|AccountEnabled", targetUsername, true)`

---

### 4. 鎖定帳號

**前提**：`SystemSettingService.AccountLockEnabled = true`（按鈕才顯示）
**觸發**：選取帳號 → 點擊「🔒 鎖定帳號」
**Log**：`LogButtonClick("AccountMgmt", "LockAccount", $"Target={username}")`

**安全守衛**：不可鎖定自己

**流程**：
1. 確認對話框
2. `LockedUntil = "9999-12-31T23:59:59.0000000+00:00"` （永久鎖定標記）
3. **Log**：`LogAuth("AccountLocked", targetUsername, true, "ManualLock")`

---

### 5. 解鎖帳號

**前提**：`AccountLockEnabled = true`（按鈕才顯示）
**觸發**：選取已鎖定帳號 → 點擊「🔓 解鎖帳號」
**Log**：`LogButtonClick("AccountMgmt", "UnlockAccount", $"Target={username}")`

**流程**：
1. `LockedUntil = null`，`FailedLoginCount = 0`
2. **Log**：`LogAuth("AccountUnlocked", targetUsername, true)`

---

### 6. 忘記密碼（Admin 觸發重設）

**觸發**：選取帳號 → 點擊「🔄 重設密碼」
**Log**：`LogButtonClick("AccountMgmt", "ResetPassword", $"Target={username}")`

**流程**：
```
Admin 點擊「重設密碼」
     │
     ▼
確認對話框
     │
     ▼
產生 12 碼隨機臨時密碼
     │
     ▼
AccountManagementService.ResetPasswordAsync()
  → PasswordHash = BCrypt(tempPassword)
  → ForcePasswordChange = 1
  → LockedUntil = null（順帶解鎖）
  → FailedLoginCount = 0
  → PasswordChangedAt = null（等使用者自行更新）
     │
     ▼
顯示結果對話框（臨時密碼，一次性明碼）
┌──────────────────────────────────┐
│  ✅ 密碼已重設                   │
│                                  │
│  臨時密碼：                      │
│  ┌────────────────────────────┐  │
│  │   Xk9mP2qL8nRw            │  │  ← 大字 FontSize=22 Monospace
│  └────────────────────────────┘  │
│  請將此密碼提供給使用者           │
│  使用者登入後系統將強制要求       │
│  變更密碼，並以新密碼重新登入     │
│                                  │
│         [ 我已記錄，關閉 ]        │
└──────────────────────────────────┘
     │
     ▼
Log：LogAuth("AccountPasswordReset", targetUsername, true, $"ResetBy={adminUser}")
```

**強制重新登入流程**（密碼重設後使用者登入時觸發）：
```
使用者以臨時密碼登入成功
     │ AppShell.OnLoginSucceeded 偵測 ForcePasswordChange=1
     ▼
顯示 ChangePasswordOverlay（不可取消）
     │ 變更成功
     ▼
ForcePasswordChange=0，PasswordChangedAt=now
     │
     ▼
強制登出 ClearSession() → NavigateTo("login")
+ OverlayDialog 顯示：「密碼已更新，請以新密碼重新登入」
     │
Log：LogAuth("ForcePasswordChanged", username, true)
```

---

### 7. 變更密碼（Admin 變更自己的密碼）

**觸發**：UserMenu → 「🔑 變更密碼」
**Log**：`LogButtonClick("UserMenu", "ChangePassword")`

**UI**：呼叫 `ChangePasswordOverlay`（見 `implementation_plan_password_ui`）
**Log**：`LogAuth("PasswordChanged", username, true)`

---

### 8. 檢視使用者

**觸發**：選取帳號 → 點擊「👁️ 檢視詳細資料」
**Log**：`LogButtonClick("AccountMgmt", "ViewDetails", $"Target={username}")`

**UI 行為**：右側操作面板切換至詳細資料檢視模式（就地替換，不另開 Overlay）

**顯示欄位**（全部唯讀）：

| 欄位 | FontSize | 說明 |
|------|----------|------|
| Username | 16 | 帳號名稱 |
| DisplayName | 16 | 顯示名稱 |
| RoleLevel | 16 | 角色標籤（色塊） |
| IsActive | 16 | 啟用 / 停用 |
| EmployeeId | 14 | 員工編號 |
| Department | 14 | 部門 |
| Email | 14 | 電子郵件 |
| LastLoginAt | 14 | 最後登入時間 |
| PasswordChangedAt | 14 | 密碼最後變更時間 |
| ForcePasswordChange | 14 | 是 / 否 |
| LockedUntil | 14 | 鎖定中 / 未鎖定 |
| FailedLoginCount | 14 | 連續失敗次數 |
| CreatedAt / CreatedBy | 14 | 建立時間與建立者 |
| Notes | 14 | 備註 |

底部加一個「← 返回操作」按鈕，切換回操作面板。

> [!NOTE]
> **彈性設計預留**（Q-P2-3 決議）：
> 本次「檢視詳細資料」為純唯讀模式。
> 未來若需新增編輯功能（DisplayName / Department / Email / Notes），
> 建議在操作面板右側加入「✏️ 編輯資料」按鈕，點擊後將唯讀欄位切換為可編輯 TextBox。
> 架構上，`AccountManagementService` 應預留 `UpdateUserProfileAsync()` 方法簽章（暫不實作）。

---

## Open Questions（Part 2 — 全部已決議）

| # | 問題 | 決議 |
|---|------|------|
| Q-P2-1 | ForcePasswordChange=1 後是否強制重新登入？ | ✅ **強制重新登入**：改密成功後強制登出，要求以新密碼重新登入 |
| Q-P2-2 | 帳號列表是否顯示 Service 帳號？ | ✅ **顯示**，可做停用 / 鎖定 / 解鎖 / 重設密碼；**新增與刪除受限** |
| Q-P2-3 | 是否支援編輯帳號資料？ | ✅ **本次唯讀**；架構預留 `UpdateUserProfileAsync()` 擴充點，後續版本增加編輯功能 |
