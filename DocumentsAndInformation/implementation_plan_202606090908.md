# USB 模擬插拔功能 — 實作計畫書

為了解決實體 USB 隨身碟拔插測試不便的問題，我們將在現有的 `TRIO2026 Hardware Simulator` 中整合「模擬 USB 拔插」的功能。此設計允許開發與測試人員從目前的電腦中挑選真實插著的 USB 隨身碟，並在不動手拔插實體硬體的狀況下，透過 TCP 通知主應用程式觸發 USB 相關事件。

## Proposed Changes

---
### 1. 模擬器介面與底層查詢 (`tools/Simulator`)

#### [MODIFY] [Simulator.csproj](file:///d:/TRIO2026/tools/Simulator/Simulator.csproj)
* 引入 `System.Management` 套件，使模擬器具備查詢 WMI 硬體資訊的能力。

#### [MODIFY] [SimulatorWindow.xaml](file:///d:/TRIO2026/tools/Simulator/SimulatorWindow.xaml)
* 在畫面右側或適當區塊新增 **USB 模擬測試** 區塊。
* 加入 `ComboBox` 下拉選單供使用者挑選。
* 加入「更新清單」與「模擬拔除後重新插入」兩個按鈕。

#### [MODIFY] [SimulatorWindow.xaml.cs](file:///d:/TRIO2026/tools/Simulator/SimulatorWindow.xaml.cs)
* 實作 `DriveInfo` 搭配 WMI 的查詢邏輯，讓下拉選單清楚呈現：`[磁碟代號] [廠牌型號] [容量] (標籤名稱)`，例如：`[E:] Transcend 32GB (DATA_DISK)`，滿足**清楚辨識要模擬哪支隨身碟**的需求。
* 實作按鈕邏輯：透過現有的 TCP 連線，發送 `{"Event": "UsbRemoved", "Drive": "E:"}`，等待 1 秒後，再發送 `{"Event": "UsbInserted", "Drive": "E:"}`。

---
### 2. 應用程式事件承接 (`src/TRIO2026.App`)

#### [MODIFY] [MockUvHardwareService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/MockUvHardwareService.cs)
* 擴充 JSON 訊息解析邏輯，攔截 `UsbRemoved` 與 `UsbInserted`。
* 宣告並觸發 `public event Action<string, string>? SimulatedUsbEvent` 委派。

#### [MODIFY] [IUsbSecurityService.cs](file:///d:/TRIO2026/src/TRIO2026.Core/Interfaces/IUsbSecurityService.cs)
* 新增介面方法 `void TriggerMockEvent(string eventType, string driveLetter);`

#### [MODIFY] [UsbSecurityService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/UsbSecurityService.cs)
* 實作 `TriggerMockEvent`，並將原本綁死在 WMI `EventArrivedEventArgs` 的邏輯重構出 `ProcessDriveInserted(string driveLetter)` 與 `ProcessDriveRemoved(string driveLetter)`，讓實體 WMI 與模擬 TCP 訊息可以共用同一套佇列與防呆邏輯。

#### [MODIFY] [App.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/App.xaml.cs)
* 於啟動階段將 `MockUvHardwareService.SimulatedUsbEvent` 註冊，當收到事件時調用 `IUsbSecurityService.TriggerMockEvent`。

## Verification Plan

### Manual Verification
1. 啟動 Simulator 與 主程式。
2. 插入一支實體隨身碟，確認 Simulator 的下拉選單能清楚顯示該隨身碟的容量與型號。
3. 點擊「模擬拔除與插入」按鈕。
4. 觀察主程式是否會彈出 `UsbFormatConfirmOverlay` 面板，藉此驗證 USB 專碟專用的防呆與掃描機制是否被正確觸發。
