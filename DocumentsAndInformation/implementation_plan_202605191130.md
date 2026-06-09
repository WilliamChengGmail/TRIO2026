# 實作門板未關閉時的 UV 頁面防護與模擬器狀態顯示

這個計畫旨在完善「UV 尚未啟動，但門板被開啟」時的防呆機制，並同步更新模擬器介面，讓開發與測試更直覺。

## Proposed Changes

### 1. `TRIO2026.App` (WPF 應用程式)

#### [MODIFY] UvDecontaminationViewModel.cs
- **屬性變更**：
  - 更新 `IsDoorOpen` 屬性，在 set 時觸發 `SelectedDisplayLabel`、`ShowDoorWarning` 與 Command 重新驗證。
  - 新增 `ShowDoorWarning` 屬性（`IsDoorOpen && !IsRunning`），用來控制紅色警告文字的顯示。
  - 修改 `SelectedDisplayLabel` 的 Getter：若 `IsDoorOpen` 且非啟動中，回傳 `"--:--"`。
- **事件處理修改**：
  - `OnDoorOpened()`：移除原本 `if (!IsRunning) return;` 的限制，確保即使未啟動也能紀錄門板狀態；將計時器暫停與 `DoorInterrupted` (覆蓋層) 的觸發包在 `if (IsRunning)` 條件內。
  - `OnDoorClosed()`：同上，確保門板狀態能正確更新。
- **Commands 條件修改 (CanExecute)**：
  - `StartStopCommand`：若未啟動，必須門板關閉 (`!IsDoorOpen`) 才允許點擊 Start；若運行中則允許點擊 Stop。
  - `PreviousCommand` / `NextCommand`：增加 `!IsDoorOpen` 條件，確保門板開啟時禁用左右箭頭。

#### [MODIFY] UvDecontaminationPage.xaml
- 在 Start 按鈕 (`StartStopButton`) 下方新增一段紅色醒目文字：`門板尚未關閉`。
- 透過綁定 `ShowDoorWarning` 搭配 `BoolToVis` 轉換器，確保該文字只在「UV 未啟動且門板開啟」時出現。
- 箭頭與 Start 按鈕的 `IsEnabled` 狀態會由 ViewModel 內 Command 的 `CanExecute` 自動接管，無需在 XAML 中手動綁定 `IsEnabled`。

---

### 2. `TRIO2026.Simulator` (WinForms 模擬器工具)

#### [MODIFY] MainForm.cs
- **UI 更新**：
  - 新增 `lblDoorStatus` 標籤，用來顯示當前門板狀態（例如："Door Status: Closed" 或 "Door Status: Open"）。
- **邏輯更新**：
  - 將原本單純發送字串的按鈕事件，改為同步更新 `lblDoorStatus` 的文字與顏色（Open 時顯示紅色，Closed 時顯示黑色）。

## Verification Plan

### Manual Verification
1. **啟動模擬器與 App**：確保兩者連線成功。
2. **UV 未啟動時測試門板**：
   - 點擊模擬器的「Simulate Door Open」，模擬器顯示門板開啟。
   - App 端觀察：左右切換箭頭被反白禁用、時間顯示為 `--:--`、Start 按鈕被禁用、下方出現紅字「門板尚未關閉」。
   - 點擊模擬器的「Simulate Door Close」，App 端觀察：所有按鈕恢復可用，時間顯示恢復（例如 15:00），紅字消失。
3. **UV 啟動中測試門板**：
   - 按下 Start 啟動 UV，倒數計時開始。
   - 點擊模擬器的「Simulate Door Open」，App 端觀察：直接彈出 Error 覆蓋層（黑底紅字警告），背景不會出現上述的「門板尚未關閉」紅字（因為 IsRunning 仍為 true）。
   - 點擊模擬器的「Simulate Door Close」，覆蓋層消失，倒數繼續。
