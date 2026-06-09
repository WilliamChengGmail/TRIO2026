# 密碼管理 UI 實作計畫（草案）

> 此計畫與「密碼複雜度與長度驗證系統」為平行計畫，待兩份計畫同時審核通過後一起執行。

---

## 目標

為 TRIO2026 提供完整的密碼管理 UI，讓使用者能在觸控螢幕環境下進行密碼變更操作。

---

## 現狀分析

| 項目 | 狀態 |
|------|------|
| `ForcePasswordChange` 欄位 | ✅ DB 存在，種子帳號初始值=1，但**無任何程式碼讀取** |
| `PasswordExpiryDays` 欄位 | ✅ DB 存在，預設=0，但**無任何程式碼讀取** |
| `PasswordChangedAt` 欄位 | ✅ DB 存在，但**無任何程式碼寫入** |
| 密碼變更 UI | ❌ 不存在 |
| 密碼變更 API | ❌ `AuthService` 無 `ChangePasswordAsync` 方法 |

---

## Proposed Changes

### 1. 密碼變更 Overlay

#### [NEW] Controls/ChangePasswordOverlay.xaml + .xaml.cs

類似現有 `LoginOverlay` 的 Modal 彈窗，但專為密碼變更設計。

**UI 佈局**：
```
┌─────────────────────────────┐
│         🔑 變更密碼          │
│    請輸入新密碼以完成變更     │
│                             │
│  當前密碼  [____________]   │
│  新密碼    [____________]   │
│  確認密碼  [____________]   │
│                             │
│  💡 密碼提示:               │
│  至少 6 碼，允許純數字       │
│                             │
│  ⚠️ 錯誤訊息區              │
│                             │
│     [ 確認變更 ]            │
│      [ 取消 ]               │
└─────────────────────────────┘
```

**功能**：
- 輸入當前密碼（驗證身分）
- 輸入新密碼 + 確認新密碼
- 即時顯示密碼原則提示（從 `PasswordPolicyService.GetPolicyHint()` 取得）
- 新舊密碼不得相同
- 確認密碼不一致時即時提示
- 使用 `TaskCompletionSource` 模式（同 LoginOverlay）

---

### 2. ForcePasswordChange 強制變更流程

#### [MODIFY] Views/AppShell.xaml + .xaml.cs

在登入成功後、導航到目標頁面之前，檢查 `ForcePasswordChange`：

```
登入成功 → 檢查 ForcePasswordChange
           ├── =0 → 正常導航
           └── =1 → 顯示 ChangePasswordOverlay（不可取消）
                     ├── 變更成功 → ForcePasswordChange=0 → 正常導航
                     └── 取消 → ???（見 Open Question Q1）
```

---

### 3. 密碼效期檢查（選配）

#### [MODIFY] AuthService.cs 或 AppShell.cs

在登入成功後檢查 `PasswordExpiryDays` + `PasswordChangedAt`：
- 如果密碼已過期 → 設定 `ForcePasswordChange=1` → 走強制變更流程
- 如果即將過期（例如 7 天內）→ 顯示提醒但不強制

---

## Open Questions（需討論）

> [!IMPORTANT]
> **Q1：ForcePasswordChange 是否可以取消/跳過？**
> - 選項 A：不可跳過，必須變更密碼才能進入系統（更安全）
> - 選項 B：可跳過，但每次登入都會提醒直到變更為止
> - 選項 C：可跳過 N 次，超過次數後強制（折衷）
> 
> **建議**：選項 A（不可跳過），這是醫療級系統的常見做法。

> [!IMPORTANT]
> **Q2：觸控環境的密碼輸入鍵盤**
> - 選項 A：直接使用系統觸控鍵盤（Windows OSK）
> - 選項 B：在 App 內建自訂數字鍵盤（適合 Operator 純數字 PIN）
> - 選項 C：不特別處理，使用者用外接鍵盤或系統鍵盤
> 
> 如果選 B，Operator 的 PIN 輸入可以設計成大按鈕數字鍵盤，類似 ATM。

> [!IMPORTANT]
> **Q3：密碼是否提供「顯示明碼」切換？**
> - 在觸控螢幕上，戴手套輸入容易按錯，「顯示明碼」按鈕可降低輸入錯誤率。
> - 是否加入眼睛圖示 👁 切換按鈕？

> [!IMPORTANT]
> **Q4：密碼原則提示的顯示方式？**
> - 選項 A：靜態文字顯示在輸入框下方（簡單）
> - 選項 B：即時驗證，逐條打勾/打叉（如 ✅ 至少 6 碼 / ❌ 需包含數字）
> 
> **建議**：選項 B（即時驗證），UX 更友善。

> [!IMPORTANT]
> **Q5：密碼變更功能的觸發點？**
> - 僅限 ForcePasswordChange 強制彈出？
> - 還是也在 UserMenu 中提供「變更密碼」按鈕讓使用者主動變更？
> 
> **建議**：兩者都有。UserMenu 加入「變更密碼」選項。

> [!IMPORTANT]
> **Q6：PasswordExpiryDays 密碼效期功能是否在本次實作？**
> - 此功能依賴 `PasswordChangedAt` 的正確記錄。
> - 如果本次實作，需要決定「即將過期」的提醒天數（例如 7 天前開始提醒）。

> [!IMPORTANT]
> **Q7：密碼變更 Overlay 的取消按鈕在何種情境下可見？**
> - ForcePasswordChange 觸發時 → 不顯示取消（若 Q1 選 A）
> - 使用者主動從 UserMenu 觸發 → 顯示取消
> - Service Mode 提權時 → 顯示取消

---

## 檔案影響範圍預估

| 檔案 | 操作 | 說明 |
|------|------|------|
| `Controls/ChangePasswordOverlay.xaml` | NEW | 密碼變更 Overlay UI |
| `Controls/ChangePasswordOverlay.xaml.cs` | NEW | Overlay 邏輯 |
| `Services/AuthService.cs` | MODIFY | 新增 ChangePasswordAsync |
| `Views/AppShell.xaml` | MODIFY | 加入 ChangePasswordOverlay 元件 |
| `Views/AppShell.xaml.cs` | MODIFY | ForcePasswordChange 檢查流程 |
| `Controls/UserMenuControl.xaml` | MODIFY | 加入「變更密碼」按鈕（若 Q5 決議） |
| `Controls/UserMenuControl.xaml.cs` | MODIFY | 變更密碼按鈕事件 |
