# USB 模擬插拔功能 — 實作計畫書 (Simulator GUI 版)

為滿足在 `TRIO2026 Hardware Simulator` 圖形化介面中進行「具備一致行為與機制」的 OS 層級 USB 拔插測試，我們將在模擬器中擴充 USB 專用測試區塊，並透過 C# 背景調用 PowerShell 完成設備重啟。

## Proposed Changes

---
### 1. 模擬器介面更新 (`tools/Simulator/SimulatorWindow.xaml`)

#### [MODIFY] [SimulatorWindow.xaml](file:///d:/TRIO2026/tools/Simulator/SimulatorWindow.xaml)
* 在「門板控制」區塊下方，新增一個 **USB 模擬控制** 的專屬區塊。
* 新增 `ComboBox`：用於顯示目前電腦上偵測到的 USB 隨身碟清單。
* 新增「重新整理」按鈕：允許在模擬器運行途中更新隨身碟清單。
* 新增「模擬實體拔插」按鈕：點擊後執行底層的 PnP 設備重啟。

---
### 2. 模擬器底層邏輯 (`tools/Simulator/SimulatorWindow.xaml.cs`)

#### [MODIFY] [SimulatorWindow.xaml.cs](file:///d:/TRIO2026/tools/Simulator/SimulatorWindow.xaml.cs)
* **隨身碟清單查詢邏輯**：
  * 使用 `Process.Start` 隱藏執行 PowerShell `Get-Disk`，過濾出 `BusType = 'USB'` 的磁碟，並擷取其 `Number`, `FriendlyName` 與 PnP `Path`。
  * 這個讀取動作**不需要**管理員權限，可直接無縫顯示於 ComboBox 供使用者挑選。
* **PnP 設備重啟邏輯**：
  * 當點擊「模擬實體拔插」時，擷取所選隨身碟的 `Path`。
  * 使用 `ProcessStartInfo` 設定 `Verb = "runas"` (要求提權)，並設定 `WindowStyle = Hidden`。
  * 執行 PowerShell 指令：`Disable-PnpDevice` → 等待 2~3 秒 → `Enable-PnpDevice`。
  * > [!IMPORTANT]
    > 由於牽涉到真實的硬體重啟，點擊按鈕時系統可能會彈出一次 **UAC (使用者帳戶控制)** 視窗要求允許。按下允許後，背後的黑畫面會被隱藏，直接安靜地完成重啟。

## Verification Plan

### Manual Verification
1. 使用 `啟動硬體模擬器.bat` 開啟 Simulator。
2. 確認介面上出現新的「USB 模擬控制」區塊。
3. 插入隨身碟，點擊「重新整理」，確認 ComboBox 有顯示該隨身碟的正確廠牌與型號。
4. 點選該隨身碟後按下「模擬實體拔插」，允許 UAC 提權，觀察主程式是否真實捕捉到 WMI 事件並彈出格式化面板。
