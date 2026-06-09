# UV 功能 EventLog 埋點審查報告

> 審查者：Office of William | 審查日期：2026-06-02

---

## 一、現有埋點清單

| # | 動作 | ErrorCode | Level | 位置 | Detail |
|---|------|-----------|-------|------|--------|
| 1 | **UV Start** | `INF-3001` | ℹ Info | [StartAsync:L310](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L310) | `Duration={s}s` |
| 2 | **UV Stop** (手動) | `WRN-3002` | ⚠ Warning | [StopAsync:L320](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L320) | `RemainingSeconds={n}` |
| 3 | **UV Complete** | `INF-3003` | ℹ Info | [OnTimerTick:L353](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L353) | _(無)_ |
| 4 | **門板中斷** | `ERR-3004` | 🔴 Error | [OnDoorOpened:L369](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L369) | `RemainingSeconds={n}` |
| 5 | **燈管啟動失敗** | `ERR-3005` | 🔴 Error | [StartAsync:L303](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L303) | _(無)_ |
| 6 | **門板恢復** | _(無 code)_ | ℹ Info | [OnDoorClosed:L384](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L384) | `RemainingSeconds={n}` |
| 7 | **時長切換** | _(無 code)_ | ℹ Info | [SelectPrevious:L251](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L251) / [SelectNext:L262](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs#L262) | `Duration={s}s, Label={label}` |

---

## 二、缺失埋點分析

### 🔴 高優先 — 合規性/安全性必須

| # | 缺失場景 | 建議 ErrorCode | Level | 建議位置 | 理由 |
|---|---------|---------------|-------|---------|------|
| A | **門板開啟阻擋啟動** | `WRN-3007` | ⚠ Warning | `ExecuteStartStopAsync` L288 | 使用者嘗試在門板開啟時啟動 UV，應記錄此安全阻擋事件 |
| B | **UV Complete 缺少 detail** | `INF-3003` | ℹ Info | `OnTimerTick` L353 | 完成時應記錄實際照射的總時長或選用的 DurationOption |

### 🟡 中優先 — 追溯與除錯

| # | 缺失場景 | 建議 ErrorCode | Level | 建議位置 | 理由 |
|---|---------|---------------|-------|---------|------|
| C | **門板恢復缺少 ErrorCode** | `INF-3008` | ℹ Info | `OnDoorClosed` L384 | 目前 `DoorResumed` 未帶 ErrorCode，無法在 DB 中以 Code 篩選 |
| D | **時長切換缺少 ErrorCode** | `INF-3009` | ℹ Info | `SelectPrevious/Next` | 同上，無 Code 不利篩選 |
| E | **門板開啟（非 UV 運行中）** | _(不記錄)_ | — | — | UV 未運行時門板開關不需記錄（正常操作） ✅ 正確 |

### 🟢 低優先 — 增強追蹤

| # | 缺失場景 | 建議 | 理由 |
|---|---------|------|------|
| F | **進入 UV 頁面** | `INF-3010` Page enter | 追蹤使用者何時進入 UV 功能頁 |
| G | **離開 UV 頁面** | `INF-3011` Page leave | 配合 F 可計算頁面停留時間 |
| H | **鎖定中 UV 完成** | 已記錄 `INF-3003`，但應在 detail 加註 `LockedScreen=true` | 區分鎖定中完成與正常完成 |

---

## 三、ErrorCode 缺失清單

需要在 [ErrorCodes.cs](file:///d:/TRIO2026/src/TRIO2026.Core/ErrorCodes.cs#L36-L42) 補充：

```diff
 // ── 3xxx UV ──
 public const string UvStart = "INF-3001";
 public const string UvStop = "WRN-3002";
 public const string UvComplete = "INF-3003";
 public const string UvDoorInterrupted = "ERR-3004";
 public const string UvLampFailure = "ERR-3005";
 public const string UvConfigLoadFailure = "ERR-3006";
+public const string UvStartBlockedByDoor = "WRN-3007";
+public const string UvDoorResumed = "INF-3008";
+public const string UvDurationChanged = "INF-3009";
+public const string UvPageEnter = "INF-3010";
+public const string UvPageLeave = "INF-3011";
```

---

## 四、現有埋點品質評估

### ✅ 做得好的部分

| 項目 | 說明 |
|------|------|
| **關鍵路徑完整** | Start / Stop / Complete / DoorInterrupted 四大核心事件皆有記錄 |
| **ErrorCode 分級正確** | Start=Info, Stop=Warning(提前終止), DoorInterrupted=Error |
| **Detail 包含上下文** | RemainingSeconds 在 Stop/DoorInterrupted/DoorResumed 中都有記錄 |
| **LogUvAction 封裝** | 統一透過 Extension Method，自動根據 ErrorCode 前綴選擇 Level |

### ⚠ 需要改進的部分

| 項目 | 問題 | 影響 |
|------|------|------|
| **DoorResumed 無 ErrorCode** | 無法在 DB 用 event_code 欄位篩選 | 查詢困難 |
| **DurationChanged 無 ErrorCode** | 同上 | 審計追蹤不完整 |
| **StartBlockedByDoor 無記錄** | 使用者嘗試不安全操作完全沒有日誌 | **合規風險** |
| **UV Complete 無 Detail** | 不知道最終照射了多少秒 | 無法驗證實際照射時長 |
| **燈管啟動失敗無 Detail** | 不知道失敗原因 | 除錯困難 |

---

## 五、建議修正方案

> [!IMPORTANT]
> **項目 A（啟動阻擋）為最高優先**：任何安全阻擋動作都必須留下審計軌跡，這是 FDA 21 CFR Part 11 的基本要求。

### 需要您確認是否執行以上修正？

修正範圍：
1. 在 `ErrorCodes.cs` 新增 5 個 ErrorCode（WRN-3007 ~ INF-3011）
2. 在 `UvDecontaminationViewModel.cs` 補 3 個埋點（A, B, C/D）
3. 在 `UvDecontaminationPage.xaml.cs` 補 2 個埋點（F, G）
4. 在 `OnCountdownCompleted` 補充 LockedScreen detail（H）
