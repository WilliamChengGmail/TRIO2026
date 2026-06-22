# USB 讀取背景檢查（usb_read_background_check）策略重構

**分析者 / 製作者：Office of William**

## 背景

`usb_read_background_check` 的定位是：**當 GUI 需要從 USB 讀取內容時（如軟體版本更新、韌體匯入等操作場景），在背景執行檔案安全掃描**。此機制的目的是防止操作人員不慎將帶有惡意執行檔的隨身碟插入儀器。

**不適用的場景**：Data Detail Page 的「下載/匯出」流程是系統**寫入**至 USB，而非從 USB 讀取資料，因此無需此檢查。

## 設定值重新定義

```
usb_read_background_check（int）
├── 0 = 不執行檢查（完全關閉）
├── 1 = 是，檢查非法格式檔案 → 發現威脅時阻擋後續使用
└── 2 = 是，僅跳出提示訊息 → 由使用者點選按鈕確認有收到檢查提醒
```

## 觸發時機

1. **USB 實體插入時** — 在 `UsbSecurityService.ProcessQueueAsync` 的流程中，於 Auto Format 判斷之後、Content Scan 之前，執行 read background check。
2. **不再在 DataListPage/DataDetailPage 的匯出流程中執行**。

---

## Proposed Changes

### SystemSettingService

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

- 將 `UsbReadBackgroundCheck` 屬性從 `bool` 改為 `int`（0/1/2）。

---

### SystemSettingSeed

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

- 更新 Id=48 的 Description 與 Remark，反映三段值（0/1/2）的語義。

---

### ErrorCodes

#### [MODIFY] [ErrorCodes.cs](file:///d:/TRIO2026/src/TRIO2026.Core/ErrorCodes.cs)

新增以下代碼（4025 ~ 4028）：

| 代碼 | 名稱 | 用途 |
|------|------|------|
| INF-4025 | `UsbReadCheckStarted` | 背景讀取檢查開始（記錄檢查模式 1/2） |
| INF-4026 | `UsbReadCheckPassed` | 背景讀取檢查通過（無威脅） |
| WRN-4027 | `UsbReadCheckBlocked` | 模式 1：偵測到威脅 → 阻擋使用 |
| INF-4028 | `UsbReadCheckUserAcknowledged` | 模式 2：使用者已確認收到安全提醒 |

---

### UsbSecurityService

#### [MODIFY] [UsbSecurityService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/UsbSecurityService.cs)

**ProcessQueueAsync 新增 Read Background Check 階段**（在 Auto Format 之後、Content Scan 之前）：

```
流程：
1. [既有] Device Insert 偵測 → 記錄
2. [既有] CybersecurityEnabled 總開關判斷
3. [既有] 認證/鎖定/Guest 檢查
4. [既有] Auto Format 判斷
5. [新增] Read Background Check（usb_read_background_check）
   ├── 值=0 → 跳過，記錄 log
   ├── 值=1 → 執行 ScanDeviceContentAsync
   │   ├── 通過 → 記錄 log，繼續流程
   │   └── 未通過 → 記錄 log + 觸發 ReadCheckBlocked 事件 → 通知 UI 顯示阻擋提示
   └── 值=2 → 執行 ScanDeviceContentAsync
       ├── 通過 → 記錄 log（靜默繼續）
       └── 未通過 → 記錄 log + 觸發 ReadCheckWarning 事件 → 通知 UI 顯示提示，等使用者確認後記錄 Acknowledged
6. [既有] Content Scan
```

**新增事件**：
- `event EventHandler<(UsbDeviceInfo Info, int Mode, bool HasThreat)> ReadCheckCompleted`

**新增方法**：
- `Task ReportReadCheckAcknowledgedAsync(UsbDeviceInfo info)` — 供 UI 在模式 2 時回報使用者已確認

---

### IUsbSecurityService

#### [MODIFY] [IUsbSecurityService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/IUsbSecurityService.cs)

新增：
- `event EventHandler<(UsbDeviceInfo Info, int Mode, bool HasThreat)> ReadCheckCompleted`
- `Task ReportReadCheckAcknowledgedAsync(UsbDeviceInfo info)`

---

### AppShell

#### [MODIFY] [AppShell.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/AppShell.xaml.cs)

訂閱 `ReadCheckCompleted` 事件：
- **Mode=1, HasThreat=true** → 用 `_dialogOverlay.ShowAsync()` 顯示**阻擋警告**（紅色 Error 圖示），告知使用者此 USB 含危險檔案，已阻擋後續操作。
- **Mode=2, HasThreat=true** → 用 `_dialogOverlay.ShowConfirmAsync()` 顯示**警告提示**（黃色 Warning 圖示），含一個「我已了解」按鈕，使用者按下後呼叫 `ReportReadCheckAcknowledgedAsync`。
- **HasThreat=false** → 不彈窗（靜默通過，僅記錄日誌）。

---

### DataListPage / DataDetailPage

#### [MODIFY] [DataListPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataListPage.xaml.cs)

- **移除** Step 3「Cybersecurity 讀取背景掃描」整段邏輯（第 1192~1217 行）。
- 重新編號後續步驟（Step 3 → 格式化判斷）。

#### [MODIFY] [DataDetailPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/DataDetailPage.xaml.cs)

- **移除** Step 2「Cybersecurity 讀取背景掃描」整段邏輯（第 415~435 行）。
- 重新編號後續步驟（Step 2 → 格式化判斷）。

---

### LocalizedStringSeed

#### [MODIFY] [LocalizedStringSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/LocalizedStringSeed.cs)

新增翻譯字串：

| Key | EN | 繁中 |
|-----|----|----|
| `UsbSecurity.ReadCheckBlocked` | USB contains potentially dangerous files. Access has been blocked. | 此 USB 包含潛在危險檔案，已阻擋後續操作。 |
| `UsbSecurity.ReadCheckWarning` | USB contains files that may pose a security risk. Please confirm you have been notified. | 此 USB 可能包含有安全風險的檔案，請確認已收到此提醒。 |
| `UsbSecurity.ReadCheckAcknowledged` | I Understand | 我已了解 |

---

## 完整日誌埋點表

| 時間點 | ErrorCode | Level | 訊息摘要 | 記錄內容 |
|--------|-----------|-------|----------|----------|
| 檢查開始 | INF-4025 | Info | USB Read Check Started | DeviceInfo, Mode=1\|2, User |
| 掃描通過 | INF-4026 | Info | USB Read Check Passed | DeviceInfo, Mode, FileCount, User |
| 模式1 威脅偵測 | WRN-4027 | Warning | USB Read Check Blocked | DeviceInfo, Mode=1, ThreatFiles=[...], Action=Blocked, User |
| 模式2 威脅偵測 | WRN-4027 | Warning | USB Read Check Threat Found | DeviceInfo, Mode=2, ThreatFiles=[...], Action=WaitingAcknowledgement, User |
| 模式2 使用者確認 | INF-4028 | Info | USB Read Check User Acknowledged | DeviceInfo, Mode=2, User, AckTimestamp |

> [!IMPORTANT]
> 上述日誌配合既有的 `UsbScanThreatDetected`（WRN-4016）/ `UsbScanSuspiciousFile`（WRN-4017）/ `UsbScanClean`（INF-4015），可完整還原「什麼時候插入 → 掃到什麼 → 系統做了什麼 → 使用者如何回應」的完整鏈條。

---

## Verification Plan

### Automated Tests
1. `dotnet build` 確認 0 錯誤
2. 在模擬器中分別測試：
   - `usb_read_background_check=0`：插入 USB，確認不觸發任何掃描或彈窗
   - `usb_read_background_check=1`：插入含 `.exe` 的 USB，確認跳出阻擋警告（Error 圖示），按確定後不執行任何後續
   - `usb_read_background_check=2`：插入含 `.exe` 的 USB，確認跳出提示（Warning 圖示），按「我已了解」後記錄 Acknowledged
   - 在 DataListPage 和 DataDetailPage 下載流程中，確認不再有 read background check 步驟

### Manual Verification
- 檢查 `system_event.db` 事件日誌，確認 INF-4025 ~ INF-4028 的完整鏈條
