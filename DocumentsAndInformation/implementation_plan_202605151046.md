# 全面 UI 操作追蹤埋點

## 目標

在 air-gapped 醫療設備中，確保所有使用者操作都被紀錄到 SystemEvent，
後續維護團隊能根據模糊資訊（時間、使用者、功能區域）復現操作情境。

## 現狀差距分析

| 追蹤項目 | 目前狀態 | 目標 |
|----------|---------|------|
| 頁面導航 | ❌ 未追蹤 | ✅ 記錄進入/離開哪個頁面 |
| 按鈕點擊 | ❌ 未追蹤 | ✅ 記錄點了哪個按鈕、在哪個頁面 |
| UV 操作 | ❌ 未追蹤 | ✅ Start/Stop/完成/時間選擇 |
| 登入/登出 | ❌ 未追蹤 | ✅ 成功/失敗/帳號 |
| 使用者選單 | ❌ 未追蹤 | ✅ 開關選單、選項點擊 |
| 輸入欄位 | ❌ 未追蹤 | ✅ 哪個欄位、輸入了什麼（密碼遮蔽） |
| 系統啟動/關閉 | ✅ 已有 | ✅ 維持 |
| 未處理例外 | ✅ 已有 | ✅ 維持 |

## 每筆日誌記錄的資訊

```
時間 | 使用者ID | 使用者名稱 | 等級 | 分類 | 來源(頁面) | ErrorId | 訊息 | 細節
```

範例：
```
2026-05-15 10:30:15 | UserId=1 | admin | Info | Navigation | AppShell | ERR-6001 | 頁面導航 | From=MenuPage, To=UvDecontaminationPage
2026-05-15 10:30:18 | UserId=1 | admin | Info | UV | UvViewModel | ERR-3001 | UV Start | Duration=90s
2026-05-15 10:30:20 | UserId=1 | admin | Info | UI | UvPage | null | Button Click | Element=StopButton, Page=UvDecontaminationPage
```

## Proposed Changes

### 1. 便利擴充方法（減少重複程式碼）

#### [NEW] [EventLogExtensions.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/EventLogExtensions.cs)

提供語意化的快捷方法：
- `LogNavigation(from, to)` — 頁面導航
- `LogButtonClick(page, element)` — 按鈕點擊
- `LogInput(page, field, value, isSensitive)` — 輸入欄位（密碼自動遮蔽）
- `LogUvAction(action, detail)` — UV 操作
- `LogAuth(action, username, success)` — 認證操作

---

### 2. 頁面導航追蹤

#### [MODIFY] [AppShell.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/AppShell.xaml.cs)
- 在 `NavigateTo()` 方法加入 `LogNavigation()`

---

### 3. UV 操作追蹤

#### [MODIFY] [UvDecontaminationViewModel.cs](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs)
- Start → `LogUvAction("Start", duration)`
- Stop → `LogUvAction("Stop")`
- Complete → `LogUvAction("Complete")`
- 時間選擇變更 → `LogInput("UV", "Duration", newValue)`

#### [MODIFY] [UvDecontaminationPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/UvDecontaminationPage.xaml.cs)
- Home 按鈕 → `LogButtonClick()`

---

### 4. 登入/登出追蹤

#### [MODIFY] [LoginViewModel.cs](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/LoginViewModel.cs)
- 登入成功 → `LogAuth("Login", username, true)`
- 登入失敗 → `LogAuth("Login", username, false)`

#### [MODIFY] [UserMenuControl.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Controls/UserMenuControl.xaml.cs)
- 登出 → `LogAuth("Logout", username, true)`
- 選單開啟/關閉

---

### 5. 選單操作追蹤

#### [MODIFY] [MenuPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/MenuPage.xaml.cs)
- 每個功能按鈕 → `LogButtonClick()`

---

### 6. 密碼欄位安全處理

> [!IMPORTANT]
> 密碼欄位的輸入**不記錄內容**，僅記錄「密碼欄位有輸入動作」。
> `LogInput()` 的 `isSensitive` 參數為 `true` 時，Value 記錄為 `"***"`。

## Open Questions

> [!IMPORTANT]
> 1. 是否需要追蹤**滑鼠移動/hover** 事件？（通常不需要，資料量太大）
> 2. 鍵盤輸入追蹤的粒度：每次按鍵 vs 欄位失焦時整體記錄？（建議後者）

## Verification Plan

### Automated Tests
- `dotnet build` 編譯通過
- App 啟動 → Console 觀察操作日誌輸出

### Manual Verification
- 完整操作流程：啟動 → 登入 → 導航 → UV 操作 → 登出
- 檢查 system_event.db 是否完整記錄所有操作
