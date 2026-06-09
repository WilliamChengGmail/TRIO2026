# 模擬器專案建置與 IPC 整合計畫

此計畫旨在於 `Tools` 目錄下建立一個 WinForms 專案 (`TRIO2026.Simulator`)，並透過 TCP Socket 實作與主程式 (`TRIO2026.App`) 間的 IPC (進程間通訊)，以模擬底層韌體/硬體的行為與狀態。

## User Review Required

> [!IMPORTANT]  
> 本計畫預設使用 **TCP Socket (JSON)** 進行雙向通訊。這是一個輕量、容易擴充且不需依賴額外套件的方式。未來若要正式對接 Modbus，只需在 `TRIO2026.App` 中將 `MockUvHardwareService` 替換為真實的 `ModbusUvHardwareService` 即可，不會影響任何 UI 邏輯。請問是否同意使用 TCP Socket (JSON) 作為模擬器的通訊協定？

## Proposed Changes

### TRIO2026.Simulator (新 WinForms 專案)

建立位於 `d:\TRIO2026\src\Tools\TRIO2026.Simulator` 的 .NET 8 WinForms 專案。

#### [NEW] `MainForm.cs`
- **UI 介面**：
  - **Connection Status**：顯示是否有 App 連線進來。
  - **UV Status**：顯示 UV 燈當前狀態（由 App 發送命令改變）。
  - **Door Control**：提供兩個按鈕「模擬門板開啟」、「模擬門板關閉」。
  - **Log Output**：一個 TextBox 用來顯示收發的命令紀錄。
- **TCP Server 邏輯**：
  - 監聽本機連接埠 `127.0.0.1:5020`。
  - 當收到 `{"Command": "StartUV"}` 時，更新畫面顯示為「UV 運作中」。
  - 當點擊「模擬門板開啟」時，主動發送 `{"Event": "DoorOpened"}` 給 App。

### TRIO2026.App

#### [MODIFY] [MockUvHardwareService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/MockUvHardwareService.cs)
- 加入 TCP Client 邏輯，在背景嘗試連線至 `127.0.0.1:5020`。
- **發送端**：`StartUvLampAsync` 與 `StopUvLampAsync` 改為將對應的 JSON 字串發送給 Simulator，並非只是單純的寫 Log。
- **接收端**：背景迴圈非同步讀取 Simulator 傳來的訊息。如果收到 `DoorOpened`，則觸發 `DoorOpened` 事件；收到 `DoorClosed`，則觸發 `DoorClosed` 事件。
- 若 Simulator 未開啟，Service 將退回原本的本機模擬模式（或直接忽略，避免 App 當機），確保開發體驗。

## Verification Plan

### Manual Verification
1. 使用指令建立並啟動 `TRIO2026.Simulator`。
2. 啟動 `TRIO2026.App`，進入 `UV Decontamination` 頁面。
3. **驗證 UV 控制**：在 App 點擊開始，觀察 Simulator 的 Log 與狀態是否變成「UV 運作中」。
4. **驗證門板中斷**：在 Simulator 中點擊「模擬門板開啟」，觀察 App 是否立即跳出門板異常的中斷畫面，並且倒數暫停。
5. **驗證門板恢復**：在 Simulator 中點擊「模擬門板關閉」，觀察 App 是否自動關閉異常畫面並繼續。
