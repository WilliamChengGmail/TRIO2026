# TRIO2026 軟體架構評估：HAL 層定位與 Sniper 介面規劃

---

## 一、整體系統分層

```mermaid
graph TB
    subgraph SNIPER["Sniper 公司負責"]
        HW["⚙️ Hardware\n儀器本體、感測器、反應槽"]
        FW["💾 Firmware\n底層設備控制程式"]
        DRV["🔌 Driver\nWindows 設備驅動程式"]
    end

    subgraph OS["🟠 Windows IoT Enterprise LTSC（博麗設定）"]
        KIOSK["Kiosk / Assigned Access"]
        COMPORT["COM Port / TCP 網路堆疊"]
        AUTOLOGIN["自動登入 Windows聩號"]
        UPDATES["Windows Update 管控"]
    end

    subgraph BOLEX["🟡 博麗負責"]
        HAL["🟡 HAL\nTRIO2026.Hardware\nSniperHardwareService"]
        FLOW["Flow Engine\nTRIO2026.FlowEngine"]
        APP["GUI\nTRIO2026.App (WPF)"]
        DATA["Data Layer\nSQLite / Repository"]
        SVC["PrivilegedService\nWindows Service"]
    end

    HW --> FW --> DRV
    DRV <-->|"Modbus RTU/TCP"| COMPORT
    COMPORT --> HAL
    KIOSK --> APP
    AUTOLOGIN --> APP
    HAL --> FLOW
    FLOW --> APP
    FLOW --> DATA
    SVC --> COMPORT

    style HAL fill:#ffd700,color:#000
    style OS fill:#1a3a5c,color:#fff
    style SNIPER fill:#4a1a1a,color:#fff
    style BOLEX fill:#1a3a1a,color:#fff
```

---

## 二、責任邊界明確化

```mermaid
graph TB
    subgraph SNIPER["Sniper 提供"]
        HW[硬體設備]
        FW[Firmware]
        DRV[Driver / SDK]
    end

    subgraph BOUNDARY["🟡 博錸 ↔ Sniper 邊界"]
        SDK_API["Sniper 提供的 API / Protocol\n(可能是 USB HID / COM Port / SDK DLL)"]
    end

    subgraph BOLEX["博錸開發"]
        subgraph HAL["HAL 層"]
            IHW["IHardwareService 介面"]
            IMPL["SniperHardwareService 實作\n（唯一知道 Sniper 細節的類別）"]
        end

        subgraph CORE["Core / Flow 層"]
            FC["Flow Controller\n實驗排程、步驟管理"]
            CMD["Command Queue\n非同步指令佇列"]
        end

        subgraph APP["App 層（現有 TRIO2026.App）"]
            GUI["WPF GUI"]
            DATA["Data Records\nDataListPage / DataDetailPage"]
            CFG["System Settings"]
        end

        subgraph INFRA["Infrastructure 層"]
            DB["SQLite DB\n(data.db / system_config.db)"]
            LOG["EventLog Service"]
            USB["USB Security Service"]
        end
    end

    DRV --> SDK_API
    SDK_API --> IMPL
    IMPL --> IHW
    IHW --> FC
    FC --> CMD
    CMD --> GUI
    FC --> DATA
    FC --> DB
    GUI --> CFG
```

---

## 三、HAL 的定位分析

### HAL 應該放在哪裡？

```
┌─────────────────────────────────────────────────────────┐
│                    TRIO2026 Solution                     │
│                                                          │
│  TRIO2026.App          (GUI, 現有)                       │
│  TRIO2026.Core         (Enums, ErrorCodes, 現有)         │
│  TRIO2026.Data         (DB, Repository, 現有)            │
│  TRIO2026.PrivilegedService  (USB 特權, 現有)            │
│                                                          │
│  ──── 建議新增 ────                                      │
│  TRIO2026.Hardware     ← HAL 層主體                      │
│  TRIO2026.FlowEngine   ← 流程控制層                      │
└─────────────────────────────────────────────────────────┘
```

### HAL 層的職責

| 職責 | 說明 |
|------|------|
| **封裝 Sniper 通訊細節** | 所有 SDK/Protocol 呼叫集中在此 |
| **提供穩定介面** | 上層只依賴 `IHardwareService`，不接觸 Sniper |
| **硬體狀態管理** | 連線、斷線、錯誤恢復 |
| **事件發佈** | 溫度、進度、完成等事件推送給 Flow 層 |
| **Mock 支援** | 提供 `MockHardwareService` 供無硬體開發/測試 |

---

## 四、與 Sniper 通訊窗口設計

```mermaid
graph LR
    subgraph HAL["TRIO2026.Hardware (HAL)"]
        IFACE["IHardwareService"]
        REAL["SniperHardwareService\n(Real)"]
        MOCK["MockHardwareService\n(開發/測試用)"]
        IFACE --> REAL
        IFACE --> MOCK
    end

    subgraph COMM["通訊協議：Modbus"]  
        OPT1["Modbus RTU\n(RS-232 / RS-485 串列)"]  
        OPT2["Modbus TCP\n(乙太網路)"]  
    end

    REAL --> OPT1
    REAL --> OPT2
```

> ✅ **協議確認**：使用 **Modbus** 通訊（RTU 或 TCP，待 Sniper 確認物理層）。  
> HAL 內部的 `SniperHardwareService` 封裝所有 Modbus 讀寫細節，對外介面 `IHardwareService` 不變。

---

## 三-B、Modbus 通訊架構詳解

### Modbus RTU vs TCP 選擇

| 項目 | Modbus RTU | Modbus TCP |
|------|-----------|------------|
| **物理層** | RS-232 / RS-485 (COM Port) | 乙太網路 (RJ45) |
| **速度** | 9600~115200 baud | 快（局域網速度） |
| **佈線** | 需串列線，距離遠 | 標準網線，靈活 |
| **工業場景** | 傳統工控設備常見 | 較新設備趨勢 |
| **Windows 驅動** | 需 COM Port（USB 轉接常見） | 標準 TCP/IP，免驅動 |
| **推薦場景** | Sniper 舊設計延續 | 未來擴充性更好 |

### HAL 內部 Modbus 通訊流程

```mermaid
sequenceDiagram
    participant FC as Flow Controller
    participant HAL as HAL (SniperHardwareService)
    participant MB as Modbus Client
    participant DEV as Sniper 設備

    FC->>HAL: StartExperiment(params)
    HAL->>MB: WriteMultipleRegisters(addr, data)
    MB->>DEV: [Modbus Frame]
    DEV-->>MB: ACK
    MB-->>HAL: Success

    loop 輪詢狀態（每 500ms）
        HAL->>MB: ReadHoldingRegisters(statusAddr)
        MB->>DEV: [Modbus Frame]
        DEV-->>MB: [Status Register]
        MB-->>HAL: status=Running / Done / Error
        HAL-->>FC: OnStatusChanged(event)
    end

    DEV-->>MB: status=Done
    MB-->>HAL: 實驗完成
    HAL-->>FC: OnExperimentCompleted(result)
    FC->>HAL: ReadResults()
    HAL->>MB: ReadHoldingRegisters(resultAddr, count)
    DEV-->>MB: [結果 Registers]
    MB-->>HAL: raw data
    HAL-->>FC: ExperimentResult
```

### Register Map 設計原則

```
建議與 Sniper 協議定義的 Register 區塊結構：

┌─────────────────────────────────────────────────┐
│  Address Range   │ 用途                          │
├─────────────────────────────────────────────────┤
│  0x0000 ~ 0x00FF │ 設備狀態（唯讀）              │
│    0x0000        │   Device Status               │
│    0x0001        │   Error Code                  │
│    0x0002        │   Current Step                │
│    0x0003        │   Progress (0~100)            │
├─────────────────────────────────────────────────┤
│  0x0100 ~ 0x01FF │ 控制指令（讀寫）              │
│    0x0100        │   Command Register            │
│    0x0101~0x01FF │   Parameters                  │
├─────────────────────────────────────────────────┤
│  0x0200 ~ 0x02FF │ 實驗設定（讀寫）              │
├─────────────────────────────────────────────────┤
│  0x0300 ~ 0x03FF │ 結果資料（唯讀）              │
└─────────────────────────────────────────────────┘

⚠️ 實際 Register Map 以 Sniper 提供的文件為準
```

### 建議 NuGet 套件

| 套件 | 說明 |
|------|------|
| **NModbus4** | .NET Modbus 客戶端，支援 RTU + TCP，穩定老牌 |
| **EasyModbus** | 輕量，同時支援 RTU/TCP，適合快速整合 |
| **FluentModbus** | 現代非同步 API，支援 .NET 8，推薦 |

---

## 五、對現有 TRIO2026 專案的影響評估

### 現狀（已建立）

```
TRIO2026.App ─── 純 GUI，無硬體邏輯
TRIO2026.Core ── Enums, ErrorCodes
TRIO2026.Data ── SQLite, Repository
PrivilegedService ─ USB 格式化
```

### 加入 HAL 後的依賴方向

```mermaid
graph BT
    DB["TRIO2026.Data"]
    CORE["TRIO2026.Core"]
    HW["TRIO2026.Hardware (HAL)"]
    FLOW["TRIO2026.FlowEngine"]
    APP["TRIO2026.App (GUI)"]
    SVC["TRIO2026.PrivilegedService"]

    CORE --> DB
    CORE --> HW
    HW --> FLOW
    DB --> FLOW
    FLOW --> APP
    CORE --> APP
    CORE --> SVC
```

**原則：依賴方向向下，GUI 不直接操作硬體。**

---

## 六、漸進開發路徑

```mermaid
gantt
    title TRIO2026 架構演進路徑
    dateFormat  YYYY-MM
    section 現階段（已完成）
    GUI 基礎框架        :done, 2025-05, 2025-09
    DB / Data Layer     :done, 2025-06, 2025-09
    USB 安全服務        :done, 2025-08, 2025-10

    section Phase 2（HAL 準備）
    確認 Sniper 通訊協議 :milestone, 2026-06, 1d
    設計 IHardwareService 介面 :2026-07, 1M
    實作 MockHardwareService   :2026-07, 1M

    section Phase 3（Flow Engine）
    建立 FlowEngine 專案        :2026-08, 2M
    整合 HAL + GUI              :2026-09, 2M

    section Phase 4（整合測試）
    接入 Sniper 真實硬體        :2026-10, 2M
```

---

## 七、開放問題（需要決策）

| # | 問題 | 影響範圍 |
|---|------|----------|
| 1 | Sniper 使用 **Modbus RTU**（串列）還是 **Modbus TCP**（網路）？ | 決定 HAL 物理層連線方式 |
| 2 | Sniper 能否提供完整的 **Register Map 文件**？ | HAL 實作的核心依賴 |
| 3 | 硬體狀態是**輪詢（Polling）**還是 Modbus **Exception Coil** 通知？ | 決定輪詢頻率與 CPU 佔用 |
| 4 | 是否需要支援**同時連接多台設備**？ | 影響 HAL 設計複雜度 |
| 5 | GUI 是否需要顯示**即時硬體狀態**（溫度/進度/步驟）？ | 影響 App 層的事件推送設計 |
| 6 | FlowEngine 是否需要**獨立程序**（類似 PrivilegedService）？ | 影響程序間通訊設計 |


---

## 八、HAL 的本質定義

### HAL 是軟體還是硬體？

```
HAL = Hardware Abstraction Layer
    = 純軟體程式，運行在 Windows 上
    = 位於「應用程式」和「硬體/Firmware」之間
    不屬於硬體，也不屬於 Firmware
```

```mermaid
graph TB
    subgraph PC["Windows 工業電腦（博錸負責）"]
        GUI["GUI\nTRIO2026.App"]
        FLOW["Flow Engine"]
        HAL["HAL\nTRIO2026.Hardware\n🟡 純軟體"]
        DRV["OS Driver / COM Port\n🟠 OS 提供"]
    end

    subgraph DEVICE["Sniper 設備（實體）"]
        FW["Firmware\n🔴 嵌入式軟體"]
        HW["Hardware\n🔴 電路/機構"]
    end

    GUI --> FLOW
    FLOW --> HAL
    HAL --> DRV
    DRV <-->|"Modbus 訊息幀"| FW
    FW --> HW

    style HAL fill:#ffd700,color:#000
    style FW fill:#ff6b6b,color:#fff
    style HW fill:#ff6b6b,color:#fff
    style GUI fill:#4a9eff,color:#fff
    style FLOW fill:#4a9eff,color:#fff
```

### 各層責任對照

| 層次 | 本質 | 責任歸屬 | 語言 |
|------|------|----------|------|
| **Hardware** | 電路板、馬達、試管槽 | Sniper | 磁鐵 |
| **Firmware** | 設備內嵌入的控制程式 | Sniper | C / 組合語言 |
| **Driver** | Windows 認識設備的驅動程式 | OS 提供 (COM Port) | C |
| **HAL** 🟡 | 封裝硬體細節的軟體層 | **博錸** | **C#** |
| **Flow Engine** | 實驗流程控制 | 博錸 | C# |
| **GUI** | 使用者介面 | 博錸 | C# / WPF |

---

## 九、HAL 與 Firmware 的關係

```mermaid
sequenceDiagram
    participant GUI as GUI
    participant FLOW as Flow Engine
    participant HAL as HAL（軟體）
    participant FW as Firmware（硬體內）
    participant HW as Hardware

    Note over GUI,HW: 「啟動實驗」跨層流程

    GUI->>FLOW: 使用者按下「開始」
    FLOW->>HAL: StartExperiment(params)
    HAL->>FW: Modbus Write Register<br/>[Command=START, Param=...]
    Note right of FW: Firmware 解析指令
    FW->>HW: 驅動馬達/加熱器...
    HW-->>FW: 硬體即行回饋
    FW-->>HAL: Modbus Read Register<br/>[Status=RUNNING, Progress=30]
    HAL-->>FLOW: 事件 OnProgressChanged(30)
    FLOW-->>GUI: 更新進度條
```

### 關鍵關係：HAL 與 Firmware 的「契約」

```
HAL 和 Firmware 之間只共享一件事：
        Register Map 協議文件

博錸定義「我要做什麼」 → IHardwareService 介面
Firmware 對應實作「怎麼做」 → Register 讀寫
HAL 對應對接「如何說」 → Modbus 指令結構

只要 Register Map 不變，就童叟無欺——兩邊不影響對方。
```

| 一方變更 | 另一方影響 |
|---------|-----------|
| Firmware 內部演算法更新 | HAL **不需改動**（Register 介面不變） |
| HAL 程式碼重構 | Firmware **不需改動** |
| Register Map 變更 | **兩邊都要改** — 這是唯一的耦合點 |

---

## 十、GUI 與 HAL 需要溝通的項目

> GUI 不直接跟 HAL 說話，都透過 **Flow Engine** 中轉。
> 以下清單為「業務語言」的需求，實際拆複由 Flow 和 HAL 分擔。

```mermaid
graph LR
    GUI["GUI"]
    FLOW["Flow Engine"]
    HAL["HAL"]

    subgraph CMDS["指令類（GUI 發起）"]
        C1["開始 / 暫停 / 中止實驗"]
        C2["加載實驗參數"]
        C3["緊急停機"]
    end

    subgraph STATUS["狀態讀取（HAL 提供）"]
        S1["設備連線 / 斷線"]
        S2["實驗進度 (0~100%)「目前步驟」"]
        S3["即時溫度 / 壓力等磁鐵變數"]
        S4["錯誤代碼 / 警告"]
        S5["實驗完成通知"]
    end

    subgraph DATA["資料讀取（實驗後）"]
        D1["樣本結果原始數據"]
        D2["實驗日誌 / 稽核記錄"]
    end

    GUI --> FLOW
    FLOW --> HAL
    CMDS --> FLOW
    HAL --> STATUS
    HAL --> DATA
    STATUS --> FLOW --> GUI
    DATA --> FLOW --> GUI
```

### 具體項目對照表

| 類別 | GUI 的需求 | 對應 HAL 操作 | Modbus 方式 |
|------|------------|---------------|-------------|
| **指令** | 啟動實驗 | StartExperiment() | Write Register: CMD=START |
| **指令** | 暫停實驗 | PauseExperiment() | Write Register: CMD=PAUSE |
| **指令** | 中止實驗 | AbortExperiment() | Write Register: CMD=ABORT |
| **指令** | 緊急停機 | EmergencyStop() | Write Coil: ESTOP=1 |
| **狀態** | 設備是否連線 | IsConnected | Ping / Read Status Reg |
| **狀態** | 實驗進度 | Progress (0~100) | Read Register: 0x0003 |
| **狀態** | 目前步驟名稱 | CurrentStep | Read Register: 0x0002 |
| **狀態** | 錯誤資訊 | LastError | Read Register: 0x0001 |
| **磁鐵變數** | 溫度（若有） | GetTemperature() | Read Register: TBD |
| **結果** | 樣本網格變光度 | ReadSampleResults() | Read Registers: 0x0300~ |
| **設定** | 載入實驗參數 | LoadProtocol(params) | Write Registers: 0x0200~ |

### GUI 跟 HAL 說話的正確方式

```
✘ 錯誤：GUI 直接呼叫 Modbus
   DataListPage.xaml.cs 裡面擺 ModbusClient.WriteRegister()

✔ 正確：GUI 只跟業務層說話
   GUI 呼叫 flowEngine.StartExperiment(protocol)
       ↓
   Flow Engine 處理業務邏輯（驗證/排程/日誌）
       ↓
   HAL 處理協議細節 (Modbus 指令結構)
       ↓
   Firmware 執行硬體動作
```

---

## 十一、OS 層規劃

### OS 在整個架構中的位置

```
Sniper 設備 ←→ COM Port/TCP (由 OS 提供)
                              ↓
              Windows IoT Enterprise LTSC ←—「博麗設定」
                    │
          ┌──────────│──────────┐
          │         Kiosk 模式      │
          │   ↓                    │
          │   TRIO2026.App (WPF)   │
          │   PrivilegedService     │
          │   HAL / FlowEngine      │
          └────────────────────┘
```

### 已完成的 OS 層工作

| 已完成 | 對應 OS 功能 |
|---------|-------------|
| `PrivilegedService.exe` | **Windows Service** — 以 SYSTEM 身份執行，解決 USB 格式化提權 |
| Named Pipe IPC | App ↔ Service 跟程序間通訊 |
| USB Security Service | 呼叫 OS 的 `DriveInfo` / `Directory` API |
| Login Required 設定 | 模擬 Kiosk 行為（免登入 Guest 模式）|
| `部署到隨身碟.bat` | 部署打包手動腳本 |

### 尚未規劃的 OS 層項目

| 項目 | 建議方案 | 優先度 |
|------|----------|--------|
| **Kiosk 模式** | Windows Assigned Access 單 App 模式 | 🔴 高 |
| **開機自動啟動 App** | 工作排程器 Task Scheduler 觸發 | 🔴 高 |
| **COM Port 號碼穩定** | 設備管理員固定號碼，或 `system_settings` 可設定 | 🔴 高（Modbus 需要） |
| **Windows Update 管理** | 停用自動重開機 | 🟡 中 |
| **自動登入 Windows 帳號** | Autologin 設定（搭配 Kiosk） | 🟡 中 |
| **部署自動化** | 改善 `.bat` 加入 Task Scheduler 設定 | 🟢 低 |

### OS 規格建議

| 項目 | 建議 | 原因 |
|------|------|------|
| **版本** | Windows 10 IoT Enterprise LTSC 2021 | 長期支援，無強制更新，Kiosk 功能完整 |
| **架構** | x64 | .NET 8 WPF 需求 |
| **Kiosk** | Assigned Access 單 App 模式 | 防止使用者誤操作 |
| **Update** | 停用自動重開機 | 避免實驗中斷 |
| **登入** | 自動登入 Windows 帳號 | 搭配 Kiosk 模式，不顯示 Windows 登入畫面 |
| **使用者管理** | TRIO2026 App 內部管理 | 不依賴 Windows 帳號切換 |

### Kiosk 展開後的開機流程

```mermaid
sequenceDiagram
    participant HW as 工業電腦開機
    participant OS as Windows IoT
    participant SVC as PrivilegedService
    participant APP as TRIO2026.App

    HW->>OS: 電源開機
    OS->>OS: 自動登入 Windows 帳號
    OS->>SVC: 啟動 Windows Service（SYSTEM）
    OS->>APP: Kiosk 自動啟動 TRIO2026.App
    APP->>SVC: Named Pipe 建立連線
    APP->>APP: 顯示登入 / Guest 畫面
    Note over APP: 不顯示 Windows 桌面
```

---

## 十二、博錸軟體內部資料流動

### 整體資料流全景

```mermaid
graph LR
    subgraph SOURCE["資料來源"]
        HW["Sniper 儀器\n(Modbus Registers)"]
        USER["使用者操作\n(GUI 觸控)"]
    end

    subgraph BOLEX["博錸軟體"]
        HAL["HAL\n翻譯 Modbus\n→ 業務物件"]
        FLOW["Flow Engine\n流程控制\n資料整合"]

        subgraph DB["資料庫層（SQLite）"]
            MAINDB["data.db\n實驗紀錄\n樣本結果"]
            CFGDB["system_config.db\n系統設定\n帳號/翻譯"]
            LOGDB["system_event.db\n稽核日誌\n操作記錄"]
        end

        GUI["GUI\n資料顯示\n使用者互動"]
    end

    subgraph EXPORT["匯出"]
        USB["USB 隨身碟\nExcel / CSV"]
    end

    HW -->|"原始數值"| HAL
    HAL -->|"結構化物件"| FLOW
    USER -->|"操作指令"| GUI
    GUI -->|"業務指令"| FLOW
    FLOW -->|"寫入實驗結果"| MAINDB
    FLOW -->|"寫入操作紀錄"| LOGDB
    CFGDB -->|"讀取設定/翻譯/帳號"| GUI
    MAINDB -->|"讀取歷史紀錄"| GUI
    LOGDB -->|"讀取稽核日誌"| GUI
    MAINDB -->|"匯出"| USB
```

---

### 五條主要資料流

#### 流 1：實驗結果流（最核心）

```mermaid
sequenceDiagram
    participant HW as Sniper 儀器
    participant HAL as HAL
    participant FLOW as Flow Engine
    participant DB as data.db
    participant GUI as GUI

    HW-->>HAL: Modbus Registers 原始數值<br/>(光度值、步驟、完成旗標)
    HAL-->>FLOW: ExperimentResult 物件<br/>(樣本陣列、時間戳記)
    FLOW->>DB: INSERT experiments<br/>INSERT sample_results
    FLOW-->>GUI: 通知實驗完成
    GUI->>DB: SELECT 最新紀錄
    DB-->>GUI: DataRecordItem 清單
    Note over GUI: DataListPage 顯示
```

#### 流 2：即時狀態流（實驗進行中）

```
Sniper 儀器                              GUI
  │                                       │
  │  每 500ms Modbus Poll                 │
  │ ←────────── HAL ──────────────        │
  │             │ OnProgressChanged(45)   │
  │             └────── Flow Engine ─────→│
  │                                       │ 更新進度條
  │                                       │ 顯示當前步驟
  │                                       │ 顯示即時溫度
```

#### 流 3：設定流（App 啟動 & 運行中）

```mermaid
graph LR
    CFGDB["system_config.db"]

    subgraph 啟動時讀取
        S1["SystemSettings\n(版面/語系/功能開關)"]
        S2["LocalizedStrings\n(所有 UI 文字翻譯)"]
        S3["Accounts\n(帳號/密碼/角色)"]
    end

    subgraph 運行中
        S4["語系切換\n→ 重新讀取翻譯"]
        S5["系統設定變更\n→ 即時生效"]
    end

    CFGDB --> S1 & S2 & S3
    CFGDB --> S4 & S5
```

#### 流 4：稽核日誌流（持續寫入）

```
所有層都可寫入 EventLog
─────────────────────────────────────────
GUI 層：  使用者點擊按鈕 → LogButtonClick()
          登入/登出      → LogAuth()
Flow 層： 實驗開始/結束  → LogInfo()
          實驗中止       → LogWarning()
HAL 層：  設備連線/斷線  → LogInfo()
          硬體錯誤       → LogError()
OS/USB：  USB 掃描結果  → LogWarning()
          格式化操作     → LogInfo()
─────────────────────────────────────────
全部寫入 → system_event.db
```

#### 流 5：匯出流（USB 下載）

```mermaid
sequenceDiagram
    participant GUI as GUI
    participant SVC as PrivilegedService
    participant DB as data.db
    participant USB as USB 隨身碟

    GUI->>GUI: 使用者選取要匯出的紀錄
    GUI->>SVC: Named Pipe: FormatDrive 請求
    SVC->>USB: 格式化 (FAT32)
    SVC-->>GUI: 格式化完成
    GUI->>DB: SELECT experiments + sample_results
    DB-->>GUI: 原始資料
    GUI->>GUI: 轉換為 Excel/CSV 格式
    GUI->>USB: 寫入檔案
    Note over USB: 報告檔案輸出完成
```

---

### 資料庫職責分工

| 資料庫 | 存放內容 | 讀取者 | 寫入者 |
|--------|----------|--------|--------|
| **data.db** | 實驗紀錄、樣本結果 | GUI (DataListPage/DetailPage) | Flow Engine（實驗完成後） |
| **system_config.db** | 系統設定、帳號、翻譯字串 | GUI (全部頁面) | 管理員透過 GUI 設定 |
| **system_event.db** | 稽核日誌、操作記錄 | GUI (稽核頁面) | 所有層（EventLogService） |

### 資料流中各層的角色

| 層次 | 對資料的職責 |
|------|-------------|
| **HAL** | 把 Modbus 原始數值（整數）翻譯成業務物件（`ExperimentResult`） |
| **Flow Engine** | 決定資料什麼時候、用什麼格式寫入 DB；協調多個資料流的時序 |
| **GUI** | 只讀取，不直接修改業務資料；透過 Flow Engine 間接觸發寫入 |
| **EventLogService** | 跨越所有層的橫切關注點，每個重要操作都寫入 event log |
| **Repository** | DB 存取的唯一入口，Flow Engine 和 GUI 都透過 Repository 操作 DB |

---

*製作者: Office of William*
