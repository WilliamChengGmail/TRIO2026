# USB Cybersecurity 專碟專用模組 — 設定架構

## 設計目標

建立 USB 儲存裝置的資安管控機制，確保僅允許經過格式化的專用隨身碟用於系統資料交換。

## 設定值規劃

新增 Category = `UsbSecurity`，共 8 組設定值（Id 42~49）：

### 總開關

| Id | Key | 預設 | 說明 |
|----|-----|:----:|------|
| 42 | `usb_cybersecurity_enabled` | `0` | 總開關（0=停用, 1=啟用）。停用時所有子功能一律不執行 |

### 功能 1 — 插入即格式化（專碟專用）

| Id | Key | 預設 | 說明 |
|----|-----|:----:|------|
| 43 | `usb_auto_format_on_insert` | `0` | 偵測到 USB 隨身碟插入時是否觸發快速格式化提示（0=否, 1=是） |
| 44 | `usb_format_confirm_delay_seconds` | `2` | 格式化確認對話框中「執行」按鈕的延遲出現秒數（防止誤觸） |

> [!IMPORTANT]
> **嚴格限制**：僅對 USB 可卸除式磁碟（Removable Disk）執行，禁止對固定磁碟、網路磁碟、CD-ROM 執行。格式化類型僅限**快速格式化**（Quick Format），嚴禁完整格式化。

#### 功能 1 的執行流程

```
USB 隨身碟插入
  ↓
① 檢查 usb_cybersecurity_enabled == "1" && usb_auto_format_on_insert == "1"
  ↓
② 彈出提示面板（多語系）
   ┌──────────────────────────────────────────┐
   │  ⚠️ USB 專碟專用                         │
   │                                          │
   │  偵測到 USB 隨身碟 (E:)                   │
   │  為確保資訊安全，即將執行快速格式化。       │
   │  此操作將清除隨身碟上所有資料。             │
   │                                          │
   │  [取消]              [執行格式化] (N秒後)  │
   └──────────────────────────────────────────┘
  ↓ 使用者點選「執行格式化」
③ 執行快速格式化（僅限 Removable Disk）
  ↓
④ 寫入 EventLog（WRN 等級，含 DriveLetter、DriveType、SerialNumber）
```

> [!WARNING]
> - 「執行格式化」按鈕預設停用，等待 `usb_format_confirm_delay_seconds` 秒後才啟用
> - 預設行為是**不執行**（使用者必須主動點擊）
> - 所有格式化動作必須記錄完整審計日誌（成功/失敗/取消）

### 功能 2 — USB 內容安全掃描

| Id | Key | 預設 | 說明 |
|----|-----|:----:|------|
| 45 | `usb_content_scan_enabled` | `0` | 是否掃描 USB 內容中已知有風險的檔案（0=否, 1=是） |
| 46 | `usb_scan_safe_extensions` | `.pdf,.csv,.xlsx,.docx,.txt,.png,.jpg,.xml,.json` | 安全檔案副檔名白名單（逗號分隔） |
| 47 | `usb_scan_block_extensions` | `.exe,.bat,.cmd,.ps1,.vbs,.js,.msi,.scr,.dll,.sys,.com,.inf,.reg` | 封鎖檔案副檔名黑名單（逗號分隔），偵測到即報警 |

> [!NOTE]
> **掃描策略**：雙名單模式
> - `block_extensions`：偵測到即阻擋 + 告警（高優先）
> - `safe_extensions`：允許通過
> - 不在兩個名單中的：視為可疑，記錄 WRN 日誌但不阻擋

### 功能 3 — GUI 讀取時背景檔案檢查（設定預留）

| Id | Key | 預設 | 說明 |
|----|-----|:----:|------|
| 48 | `usb_read_background_check` | `0` | GUI 讀取隨身碟時，背景檢查是否有非法格式檔案（0=否, 1=是） |

> [!NOTE]
> 此功能為設定預留，實際掃描邏輯在後續開發時實作。

### 功能 4 — GUI 寫入前格式化（設定預留）

| Id | Key | 預設 | 說明 |
|----|-----|:----:|------|
| 49 | `usb_format_before_write` | `0` | GUI 寫入隨身碟前是否執行快速格式化（0=否, 1=是）。若功能 1 已執行過則自動跳過 |

> [!NOTE]
> 此功能為設定預留，與功能 1 互斥邏輯在後續開發時實作。

---

## Proposed Changes

### SystemSettingSeed

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)

- 類別註解新增 `UsbSecurity` 分類
- 新增 8 組設定值（Id 42~49），Category = `UsbSecurity`

---

### SystemSetting Entity

#### [MODIFY] [SystemSetting.cs](file:///d:/TRIO2026/src/TRIO2026.Core/Entities/SystemSetting.cs)

- 類別註解新增 `UsbSecurity` 分類說明

---

### SystemSettingService

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)

新增 6 個屬性：

```csharp
// ── USB Cybersecurity ──
bool UsbCybersecurityEnabled          // 總開關
bool UsbAutoFormatOnInsert            // 功能 1
int  UsbFormatConfirmDelaySeconds     // 格式化確認延遲
bool UsbContentScanEnabled            // 功能 2
string UsbScanSafeExtensions          // 安全副檔名白名單
string UsbScanBlockExtensions         // 封鎖副檔名黑名單
bool UsbReadBackgroundCheck           // 功能 3（預留）
bool UsbFormatBeforeWrite             // 功能 4（預留）
```

---

### ErrorCodes + EventCodeDefinitionSeed

#### [MODIFY] [ErrorCodes.cs](file:///d:/TRIO2026/src/TRIO2026.Core/ErrorCodes.cs)

新增 USB 資安相關事件代碼（建議放在 4xxx 區段，Hardware 分類）：

| Code | 常數名 | 等級 | 說明 |
|------|--------|:----:|------|
| `INF-4010` | `UsbDeviceInserted` | INFO | USB 儲存裝置插入偵測 |
| `INF-4011` | `UsbFormatSuccess` | INFO | USB 快速格式化成功 |
| `WRN-4012` | `UsbFormatFailed` | WRN | USB 快速格式化失敗 |
| `INF-4013` | `UsbFormatCancelled` | INFO | 使用者取消格式化 |
| `WRN-4014` | `UsbFormatBlockedNonRemovable` | WRN | 非可卸除式裝置被阻擋格式化 |
| `INF-4015` | `UsbScanClean` | INFO | USB 掃描通過（無威脅） |
| `WRN-4016` | `UsbScanThreatDetected` | WRN | USB 掃描偵測到風險檔案 |
| `WRN-4017` | `UsbScanSuspiciousFile` | WRN | USB 掃描偵測到可疑檔案（不在白名單） |
| `INF-4018` | `UsbDeviceRemoved` | INFO | USB 儲存裝置拔除 |

#### [MODIFY] [EventCodeDefinitionSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/EventCodeDefinitionSeed.cs)

- 新增 9 筆對應事件定義

---

## Open Questions

> [!IMPORTANT]
> **功能 2 的 CVE 資料庫來源**：
> USB 內容掃描的「已知有風險的檔案」判斷，目前規劃以副檔名黑白名單為基礎機制。若需對接外部 CVE 資料庫（如 NIST NVD、ClamAV 病毒碼），是否在後續階段再擴充？

> [!IMPORTANT]
> **功能 1 的檔案系統格式**：
> 快速格式化時應使用哪種檔案系統？建議 `exFAT`（支援大檔案、跨平台相容），或是需要 `NTFS`？

## Verification Plan

### Automated Tests
- `dotnet build` 確認編譯無錯誤
- 執行 DbInitializer 確認 seed 正確植入
- 檢查 `db-init-logs/` 確認 8 筆新設定已補入

### Manual Verification
- 透過 DB Browser 確認 SystemSetting 表新增 8 筆記錄
- 確認 SystemSettingService 屬性讀取正確
