# 硬體狀態主動查詢與雙重防護計畫

為了解決「訊號時間差（Race Condition）」導致的狀態不一致，以及實現進入頁面與啟動前的最高安全防護，我們需要讓軟體具備「**主動詢問硬體狀態**」的能力。

## Proposed Changes

### 1. `TRIO2026.Core` (核心介面)

#### [MODIFY] IUvHardwareService.cs
- 新增屬性：`bool IsDoorOpen { get; }`
  - 目的：讓 UI 層能夠隨時「主動讀取」當前的硬體門板狀態，而不只是被動等待事件通知。

### 2. `TRIO2026.App` (WPF 應用程式)

#### [MODIFY] MockUvHardwareService.cs
- 實作 `IsDoorOpen` 屬性。
- 在內部處理 Simulator 訊息 (`ProcessSimulatorMessage`) 時，同步更新 `IsDoorOpen` 的值：
  - 收到 `DoorOpened` 時設為 `true`。
  - 收到 `DoorClosed` 時設為 `false`。

#### [MODIFY] UvDecontaminationViewModel.cs
- **第一道防護 (導覽時主動同步)**：
  - 在 `InitializeAsync()` 方法內（也就是剛進入 UV Page 時），加入 `IsDoorOpen = _hardwareService.IsDoorOpen;`。這能確保就算漏掉了啟動瞬間的 Event，UI 依然能讀到最新的硬體狀態並立即 Disable 相關按鈕。
- **第二道防護 (啟動時主動詢問)**：
  - 在 `ExecuteStartStopAsync()` 準備啟動 UV 之前，除了檢查 ViewModel 自己的 `IsDoorOpen`，再多加一層 `if (_hardwareService.IsDoorOpen)` 的底層硬體狀態雙重驗證。
  - 若底層回報門板為開啟，立刻觸發警告視窗 (`StartBlockedByDoor`) 並終止啟動。

## Verification Plan

### Manual Verification
1. **情境一：啟動前狀態同步**
   - 先開啟 Simulator 並將狀態切換為 `Door Opened`。
   - 啟動 App，點擊進入 UV 頁面。
   - 驗證：UV 頁面載入的瞬間，是否已經呈現 Disable 狀態（箭頭反灰、顯示 `--:--`），並且下方顯示紅字警告。
2. **情境二：最後一道防線測試**
   - 如果透過某種方式（例如使用 API 或直接呼叫 Command）繞過 UI 的 Disable 狀態去觸發 Start。
   - 驗證：系統會主動詢問底層，發現門板開啟後，會成功彈出「門板已開啟，請關閉門板以繼續。」的警告視窗，且不會發送 StartUV 訊號給模擬器。
