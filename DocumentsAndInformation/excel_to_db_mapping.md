# Excel 報告 → DB Schema 欄位映射（含資料來源）

> 製作者: Office of William
> 最後更新: 2026-06-12

## 用途

從一代機 Excel 報告**逆向工程**，產生測試資料至 `test_data.db`，供二代機報表引擎開發使用。

> [!IMPORTANT]
> **Excel → DB 不是正常的生產流程。**
> 正常流程是：運行中即時存入 DB → 事後從 DB 讀出生成報告。
> 這裡是反向操作，從舊報告推回 DB 內容作為開發測試資料。

---

## 資料來源分類

| 標記 | 來源 | 說明 |
|------|------|------|
| `[QR]` | QR Code 掃描 | 試劑條碼掃描自動帶入 |
| `[MANUAL]` | 操作員手動輸入 | 觸控螢幕或鍵盤輸入 |
| `[MACHINE]` | 機器運行產生 | 硬體量測/韌體回傳 |
| `[CALC]` | 軟體計算 | 從原始數據計算/轉換 |
| `[SYSTEM]` | 系統自動 | 時間戳、版本、UUID 等 |

---

## IntelliPlex Report（mode=1）

### Header 區域 → `TestRecord`

| Excel 位置 | 欄位名稱 | 來源 | DB Column | 範例值 |
|-----------|---------|------|-----------|--------|
| A1 | 報告標題 | — | `ReportType = "IntelliPlex"` | 固定 |
| B3 | Experiment Date | `[MACHINE]` | `ExperimentDate` | 2026/03/17 |
| B4 | Extraction Program | `[QR]` | `ExtractionProgram` | QC Dilution Test_0.5ng/ul |
| B5 | Extraction Kit Lot. No. | `[QR]` | `ExtractionKitLotNo` | 26275878 |
| B6 | Extraction Sample Volume | `[QR]` | `ExtractionSampleVolume` | N/A |
| B7 | Elution Volume | `[QR]` | `ElutionVolume` | N/A |
| B8 | PCR Total Nucleic Acid Input | `[QR]` | `PcrTotalNucleicAcidInput` | 10.00 ng |
| B9 | IntelliPlex Kit 1 Product Name | `[QR]` | `IntelliPlexKit1Name` | QC Dilution Test... |
| B10 | IntelliPlex Kit 1 Lot No. | `[QR]` | `IntelliPlexKit1LotNo` | 26275878 |
| B11 | IntelliPlex Kit 2 Product Name | `[QR]` | `IntelliPlexKit2Name` | QC Dilution Test... |
| B12 | IntelliPlex Kit 2 Lot No. | `[QR]` | `IntelliPlexKit2LotNo` | 26275878 |
| B13 | PCR Plate ID | `[MANUAL]` | `PcrPlateId` | N/A |
| B14 | S1 A/D Value | `[MACHINE]` | `RawMeasurement.S1AdValue` | 1190 |
| B15 | S2 A/D Value | `[MACHINE]` | `RawMeasurement.S2AdValue` | 16110 |

### Data 區域 → `SampleResult`（Row 22~45）

| Excel 欄 | 欄位名稱 | 來源 | DB Column |
|----------|---------|------|-----------|
| A 欄 | Sample Position | `[MACHINE]` | `SamplePosition` (int) |
| A 欄 | NC / PC | `[QR]` | `SampleType` = "NC" or "PC" |
| B 欄 | Concentration (ng/μL) | `[MACHINE]` | `Concentration` + `ConcentrationDisplay` |
| C 欄 | Utilized Eluted Sample (μL) | `[CALC]` | `UtilizedElutedVolume` |
| D 欄 | PCR Kit 1 Well | `[CALC]` | `PcrWellKit1` |
| E 欄 | PCR Kit 2 Well | `[CALC]` | `PcrWellKit2` |
| F 欄 | Sample ID | `[MANUAL]` | `SampleId` |
| G 欄 | Elution Tube ID | `[MANUAL]` | `ElutionTubeId` |

---

## Custom Program Report（mode=2）

### Header 區域 → `TestRecord`

| Excel 位置 | 欄位名稱 | 來源 | DB Column |
|-----------|---------|------|-----------|
| A1 | 報告標題 | — | `ReportType = "Custom"` |
| B3 | Experiment Date | `[MACHINE]` | `ExperimentDate` |
| B4 | Function Modules Selected | `[SYSTEM]` | `FunctionModulesSelected` |
| B5 | Extraction Program | `[QR]` | `ExtractionProgram` |
| B6 | Extraction Kit Lot. No. | `[QR]` | `ExtractionKitLotNo` |
| B7 | Extraction Sample Volume | `[QR]` | `ExtractionSampleVolume` |
| B8 | Elution Volume | `[QR]` | `ElutionVolume` |
| B9 | PCR Plate ID | `[MANUAL]` | `PcrPlateId` |
| B10~E15 | Custom PCR Setup (4 Rxn) | `[QR+MANUAL]` | `CustomPcrSetupJson` (JSON) |
| B16 | S1 A/D Value | `[MACHINE]` | `RawMeasurement.S1AdValue` |
| B17 | S2 A/D Value | `[MACHINE]` | `RawMeasurement.S2AdValue` |

### Data 區域 → `SampleResult`（Row 24~47）

| Excel 欄 | 欄位名稱 | 來源 | DB Column |
|----------|---------|------|-----------|
| A 欄 | Sample Position | `[MACHINE]` | `SamplePosition` |
| A 欄 | Ctrl1 / Ctrl2 | `[QR]` | `SampleType` = "Ctrl1" or "Ctrl2" |
| B 欄 | Concentration | `[MACHINE]` | `Concentration` + `ConcentrationDisplay` |
| C 欄 | Utilized Eluted Sample | `[CALC]` | `UtilizedElutedVolume` |
| D 欄 | Rxn 1 Well | `[CALC]` | `PcrWellKit1` |
| E 欄 | Rxn 2 Well | `[CALC]` | `PcrWellKit2` |
| F 欄 | Rxn 3 Well | `[CALC]` | `PcrWellRxn3` |
| G 欄 | Rxn 4 Well | `[CALC]` | `PcrWellRxn4` |
| H 欄 | Sample ID | `[MANUAL]` | `SampleId` |
| I 欄 | Elution Tube ID | `[MANUAL]` | `ElutionTubeId` |

---

## 測試資料產生結果

已從 38 份 Excel 報告成功產生 `test_data.db`：

| Table | 筆數 | 說明 |
|-------|------|------|
| TestRecord | 38 | 12 IntelliPlex + 26 Custom |
| SampleResult | 231 | 197 Sample + 12 NC + 12 PC + 5 Ctrl1 + 5 Ctrl2 |
| RawMeasurement | 38 | S1/S2 A/D 值（部分為 null） |
| ReportSnapshot | 38 | 每份含完整 ContentJson 快照 |

工具位於: [seed_from_excel.py](file:///d:/TRIO2026/tools/TestDataSeeder/seed_from_excel.py)
資料庫: [test_data.db](file:///d:/TRIO2026/tools/TestDataSeeder/test_data.db)

---

## 無法從 Excel 逆向工程的欄位

以下欄位**在 Excel 中不存在**，需要在二代機實際運行時才會產生：

| Table | Column | 說明 |
|-------|--------|------|
| TestRecord | OperatorUserId/Username | 操作員身份 |
| TestRecord | DeviceSerialNo | 設備序號 |
| TestRecord | InstallationUuid | 設備 UUID |
| TestRecord | StartTime/EndTime | 精確運行時間（Excel 只有日期） |
| TestRecord | SampleBitmap | 樣本位圖 |
| TestRecord | ReagentInfoJson | 試劑 QR 原始字串 |
| RawMeasurement | arg0~arg6 原始值 | 硬體回傳原始數據 |
| RawMeasurement | 校正參數 | 來自 opticsinfo.ini |
| ProcessLog | 全部 | 硬體狀態快照（2000筆/次） |
| RunTimePhase | 全部 | 各階段耗時 |
| ConsumableUsage | 全部 | 耗材追蹤 |
| CameraCheckResult | 全部 | 光學偵測結果 |
