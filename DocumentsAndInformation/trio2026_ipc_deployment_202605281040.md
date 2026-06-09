# TRIO2026 工業電腦部署規劃

> 撰寫者: Office of William
> 日期: 2026-05-28

## 1. 系統概述

| 項目 | 規格 |
|------|------|
| 應用框架 | .NET 8.0 + WPF (Self-contained 部署) |
| 資料庫 | SQLite × 4 (config / main / data / event) |
| 部署大小 | ~168 MB (含 runtime) |
| 程式碼規模 | ~33,000 行 (C# + XAML) |
| 目標面板 | 7 吋觸控螢幕 (1024×600 典型) |
| 硬體通訊 | USB HID / Serial (規劃中) |
| 網路需求 | 僅內部 (無雲端依賴) |

---

## 2. 硬體規格建議

### 2.1 最低規格 (Minimum)

| 元件 | 規格 | 說明 |
|------|------|------|
| **CPU** | Intel Celeron N5105 / AMD Ryzen Embedded R1305G | 4 核心 ≥ 2.0 GHz，x64 架構 |
| **RAM** | 4 GB DDR4 | Win11 IoT (2 GB) + WPF (~200 MB) + SQLite + 餘裕 |
| **儲存** | 64 GB eMMC / SSD | OS (~20 GB) + App (~200 MB) + DB + Log 成長 |
| **顯示** | 7" 電容式觸控 (1024×600) | 需支援 Windows Touch HID 標準 |
| **I/O** | USB 2.0 × 2, COM Port × 1 | 設備通訊 + 維護用 USB |
| **網路** | 100M Ethernet | 韌體更新 / 遠端維護 (可選) |
| **電源** | 12V/24V DC-in (寬壓) | 工業環境標準 |
| **TPM** | 選用 (Win11 IoT LTSC 不強制) | 若需 BitLocker 加密則必要 |

### 2.2 推薦規格 (Recommended)

| 元件 | 規格 | 說明 |
|------|------|------|
| **CPU** | Intel Core i3-1215U / AMD Ryzen 5 Embedded | 6+ 核心，≥ 2.5 GHz，未來擴充餘裕 |
| **RAM** | **8 GB** DDR4/DDR5 | 長時間運行不依賴 Page File |
| **儲存** | **128 GB** 工業級 SSD (MLC/pSLC) | 壽命保證 + Log/DB 成長空間 |
| **觸控** | PCAP 多點觸控，戴手套支援 | 醫療/實驗室手套操作 |
| **I/O** | USB 3.0 × 2, RS-232 × 2, GPIO | 設備擴充性 |
| **散熱** | 無風扇 (Fanless) | 醫療/潔淨室環境 |
| **工作溫度** | 0°C ~ 50°C | 工業級溫度範圍 |
| **防護等級** | IP65 前面板 | 防塵防水 |

> [!IMPORTANT]
> **儲存選型關鍵**：SQLite 的 WAL 模式會頻繁小寫入。使用 TLC/QLC SSD 會加速磨損。
> 強烈建議使用 **MLC 或 pSLC** 等級工業 SSD，或搭配 DRAM 快取的 SSD。
> 目前 `system_event.db` 已達 28 MB，長期運行需規劃定期歸檔。

### 2.3 觸控面板詳細規格

#### 軟體端設計基準

| 項目 | 目前設定 | 來源 |
|------|----------|------|
| UI 設計基準解析度 | **600 × 960 px** (直式) | `AppShell.xaml` → Viewbox Border |
| 縮放基準 | 1920 × 1200 | `ScreenDetector.ScaleFactor` |
| 縮放模式 | Viewbox `Stretch="Uniform"` | 等比縮放，自動適配任何解析度 |
| 視窗模式 | `WindowState="Maximized"` + `WindowStyle="None"` | 全螢幕無邊框 |
| 觸控偵測 | `Tablet.TabletDevices.Count > 0` | 自動判斷觸控支援 |

> [!NOTE]
> 軟體使用 **Viewbox 等比縮放**，UI 會自動適配不同解析度面板。
> 設計基準 600×960 為直式 (Portrait)，若面板為橫式需在 OS 中設定螢幕旋轉。

#### 面板硬體參數需求

| 參數 | 最低要求 | 推薦規格 | 說明 |
|------|----------|----------|------|
| **尺寸** | 7 吋 | 7 吋 | 搭配 600×960 設計基準 |
| **方向** | Portrait (直式) | Portrait (直式) | 可透過 OS 旋轉橫式面板 |
| **解析度** | 800 × 480 (WVGA) | **1024 × 600** (WSVGA) | Viewbox 會自動縮放 |
| **像素密度** | ~133 PPI | ~170 PPI | 文字清晰度差異明顯 |
| **觸控類型** | 電阻式 (Resistive) | **PCAP 電容式** | 支援多點觸控 + 手套操作 |
| **觸控點數** | 單點 | ≥ 5 點 | 預留手勢擴充 |
| **手套支援** | — | ✅ 必要 | 醫療/實驗室操作 |
| **亮度** | 300 cd/m² | ≥ 400 cd/m² | 明亮實驗室環境 |
| **對比度** | 500:1 | ≥ 800:1 | 改善文字/圖示辨識度 |
| **視角** | TN (±60°) | **IPS (±85°)** | 側視時畫面不變色 |
| **背光壽命** | 30,000 hr | ≥ 50,000 hr | 24/7 運行約 5.7 年 |
| **表面處理** | 亮面 | **霧面 (AG)** | 減少反光 + 抗指紋 |
| **光學貼合** | Air Gap | **Optical Bonding** | 減少視差 + 提升觸控精準度 |
| **介面** | LVDS | LVDS / eDP | 工業主板常用介面 |
| **前面板防護** | — | **IP65** | 防塵防水濺 |

#### 解析度適配對照表

由於 Viewbox 等比縮放，不同解析度面板會有以下呈現效果：

| 面板解析度 | 等效顯示 | 黑邊 | 清晰度 | 適用性 |
|-----------|----------|------|--------|--------|
| 800 × 480 (橫→直旋轉) | 480 × 800 | 上下有黑邊 | ★★☆ | 勉強可用 |
| **1024 × 600** (橫→直旋轉) | **600 × 1024** | 上下極小黑邊 | ★★★ | **最佳匹配** |
| 1280 × 800 (橫→直旋轉) | 800 × 1280 | 無黑邊 | ★★★★ | 優秀 |
| 1920 × 1080 (FHD 開發用) | 自動縮放 | 兩側黑邊 | ★★★★★ | 開發/測試 |

> [!TIP]
> **最佳選擇**：1024×600 橫式面板 + OS 設定旋轉 90° 為直式。
> 設計基準 600×960 與 600×1024 面板幾乎完美匹配，僅 64px 差異由 Viewbox 自動處理。

#### 硬體通訊介面 (I/O)

| 介面 | 數量 | 用途 |
|------|------|------|
| **USB 2.0** | ≥ 2 | 設備通訊 (HID) + 維護用隨身碟 |
| **USB 3.0** | ≥ 1 | 高速資料匯出 (選配) |
| **RS-232 (COM)** | ≥ 1 | 儀器序列通訊 (規劃中) |
| **RS-485** | 1 (選配) | 多設備串接 |
| **RJ-45 Ethernet** | 1 | 遠端維護 / 韌體更新 |
| **GPIO** | 4-8 pin (選配) | 外部感測器 / 門禁整合 |
| **DC Power Jack** | 1 | 12~24V 寬壓 DC-in |
| **音訊輸出** | 1 (選配) | 警報蜂鳴器 |

#### 環境與認證需求

| 項目 | 規格 |
|------|------|
| **工作溫度** | 0°C ~ 50°C (工業級) |
| **儲存溫度** | -20°C ~ 60°C |
| **工作濕度** | 10% ~ 95% RH (無凝結) |
| **抗震動** | IEC 60068-2-64，2 Grms (5~500 Hz) |
| **抗衝擊** | IEC 60068-2-27，20 G (11 ms) |
| **EMC** | CE / FCC Class B |
| **安規** | UL / CB (若需醫療認證另計) |
| **MTBF** | ≥ 50,000 hr |

### 2.4 推薦工業電腦品牌

| 品牌 | 系列 | 特點 |
|------|------|------|
| **Advantech (研華)** | TPC-71W / PPC 系列 | 7" 觸控面板電腦，台灣品牌，支援完善 |
| **Cincoze (德承)** | P1201 | 嵌入式無風扇，寬溫寬壓 |
| **Axiomtek (艾訊)** | GOT 系列 | 觸控面板電腦，醫療認證版本 |
| **Kontron** | KBox 系列 | 歐洲品牌，高可靠度 |
| **AAEON (研揚)** | AFOLUX 系列 | 7" 面板電腦，價格合理 |

---

## 3. 軟體規劃

### 3.1 作業系統選型

| 項目 | 建議 |
|------|------|
| **版本** | **Windows 11 IoT Enterprise LTSC 2024** |
| **優勢** | 10 年支援 (至 2034)、無強制功能更新、TPM 可選 |
| **授權** | OEM 嵌入式授權 (透過 IPC 供應商取得) |
| **語系** | 安裝 en-US + zh-TW 語言包 |
| **更新策略** | 僅安全更新，WSUS / 手動控制 |

> [!WARNING]
> **不建議使用 Windows 11 Home/Pro**：
> - 強制功能更新可能中斷生產
> - 無 Shell Launcher / Unified Write Filter 等 Kiosk 功能
> - 無長期支援保證

### 3.2 OS 精簡化 (Lockdown)

```powershell
# ═══ 啟用 Kiosk 必要功能 ═══

# 1. Shell Launcher — 取代 Explorer，直接啟動 TRIO2026
Dism /online /Enable-Feature /all /FeatureName:Client-EmbeddedShellLauncher

# 2. Unified Write Filter (UWF) — 保護系統磁碟防止意外寫入
Dism /online /Enable-Feature /all /FeatureName:Client-UnifiedWriteFilter

# 3. Keyboard Filter — 遮蔽 Ctrl+Alt+Del、Win 鍵等
Dism /online /Enable-Feature /all /FeatureName:Client-KeyboardFilter
```

#### 移除/停用不需要的服務

| 服務 | 動作 | 原因 |
|------|------|------|
| Windows Update | 改手動 | 避免自動重啟 |
| Cortana | 移除 | 不需要語音助理 |
| OneDrive | 移除 | 無雲端需求 |
| Windows Search | 停用 | 減少磁碟 I/O |
| Windows Defender | 保留但排除 App 目錄 | 避免掃描影響效能 |
| Print Spooler | 停用 | 無列印需求 |
| Bluetooth | 視需求 | 若無藍牙設備則停用 |

### 3.3 Kiosk 模式 — Shell Launcher 設定

```powershell
# ═══ 設定 TRIO2026 為預設 Shell ═══

# 取得 WMI 類別
$ShellLauncherClass = [wmiclass]"\\localhost\root\standardcimv2\embedded:WESL_UserSetting"

# 設定預設 Shell（所有使用者）
# 參數: 應用程式路徑, 退出行為 (1=自動重啟)
$ShellLauncherClass.SetDefaultShell(
    "C:\TRIO2026\App\TRIO2026.App.exe", 1)

# 為維護帳號保留 Explorer
$AdminSID = (New-Object System.Security.Principal.NTAccount("Administrator")).Translate(
    [System.Security.Principal.SecurityIdentifier]).Value
$ShellLauncherClass.SetCustomShell($AdminSID, "explorer.exe", $null, $null, 0)

# 啟用 Shell Launcher
$ShellLauncherClass.SetEnabled($true)
```

### 3.4 自動啟動與看門狗

#### 方案 A：Shell Launcher (推薦)
- 應用退出時自動重啟 (退出行為=1)
- 無需額外看門狗

#### 方案 B：Windows Task Scheduler + 自訂看門狗

```xml
<!-- WatchdogTask.xml -->
<Task>
  <Triggers>
    <BootTrigger><Delay>PT30S</Delay></BootTrigger>
    <EventTrigger>
      <!-- 偵測 TRIO2026.App.exe 程序結束 -->
      <Subscription>
        <![CDATA[
        <QueryList>
          <Query>
            <Select Path="System">
              *[System[EventID=1]] and 
              *[EventData[Data='TRIO2026.App.exe']]
            </Select>
          </Query>
        </QueryList>
        ]]>
      </Subscription>
    </EventTrigger>
  </Triggers>
  <Actions>
    <Exec>
      <Command>C:\TRIO2026\App\TRIO2026.App.exe</Command>
      <WorkingDirectory>C:\TRIO2026\App</WorkingDirectory>
    </Exec>
  </Actions>
</Task>
```

### 3.5 資料庫儲存策略

```
C:\TRIO2026\
├── App\                        # 應用程式 (Shell Launcher 目標)
│   ├── TRIO2026.App.exe
│   ├── TRIO2026.Core.dll
│   ├── TRIO2026.Data.dll
│   └── ...
├── Database\                   # 資料庫 (UWF 排除寫入保護)
│   ├── system_config.db        # 系統設定 (~0.2 MB)
│   ├── main.db                 # 帳號/授權 (~0.04 MB)
│   ├── data.db                 # 實驗資料 (成長中)
│   └── system_event.db         # 事件日誌 (~28 MB，持續成長)
├── Backup\                     # 自動備份目錄
│   └── EventLog\               # 歸檔的事件日誌
├── Tools\                      # 維護工具
│   ├── 啟動模擬器.bat
│   ├── DevLauncher.exe
│   └── import_excel_to_db.py
└── Logs\                       # 應用程式文字日誌
```

> [!TIP]
> **UWF 設定**：啟用 Unified Write Filter 時，需將 `Database\` 和 `Logs\` 目錄加入排除清單，
> 否則重開機後資料會遺失。
> ```powershell
> uwfmgr.exe file add-exclusion "C:\TRIO2026\Database"
> uwfmgr.exe file add-exclusion "C:\TRIO2026\Logs"
> uwfmgr.exe file add-exclusion "C:\TRIO2026\Backup"
> ```

### 3.6 事件日誌歸檔策略

| 項目 | 建議值 |
|------|--------|
| 歸檔週期 | 每 7 天或 50 MB |
| 保留份數 | 最近 12 份 (≈3 個月) |
| 歸檔位置 | `C:\TRIO2026\Backup\EventLog\` |
| 匯出格式 | `.db` + 選配 `.csv` |
| 清理機制 | `EventLogArchiveService` (已實作) |

---

## 4. 安全強化

### 4.1 OS 層級

| 項目 | 設定 |
|------|------|
| **本機帳號** | 僅建立 `TrioOperator` (標準使用者) + `Administrator` (維護用) |
| **UAC** | 維持啟用（App 以標準權限執行） |
| **BitLocker** | 建議啟用（需 TPM 2.0） |
| **防火牆** | 啟用，僅開放維護用 RDP port (需時開啟) |
| **USB Policy** | 透過群組原則限制可安裝裝置 |
| **螢幕保護** | 停用（Kiosk 永遠顯示） |
| **電源管理** | 停用休眠/睡眠（永遠開啟） |

### 4.2 應用層級 (已實作)

| 功能 | 狀態 |
|------|------|
| BCrypt 密碼雜湊 | ✅ |
| 帳號鎖定機制 | ✅ |
| 強制密碼變更 | ✅ |
| RBAC 角色控制 | ✅ |
| 事件日誌追蹤 | ✅ |
| 觸控鍵盤取代實體 | ✅ |

---

## 5. 維運工具需求

| 工具 | 用途 | 部署方式 |
|------|------|----------|
| **DevLauncher** | 開發/維護用啟動器 | 已有 (`Tools\`) |
| **遠端桌面 (RDP)** | 遠端維護 | OS 內建 (需時開啟) |
| **TeamViewer / AnyDesk** | 跨網段遠端維護 | 可選安裝 |
| **dotnet-counters** | 記憶體/效能監控 | `dotnet tool install` |
| **DB Browser for SQLite** | 資料庫直接查看 | Portable 版放 `Tools\` |
| **Windows Event Viewer** | OS 層級異常追蹤 | OS 內建 |

---

## 6. 部署檢查清單

### 6.1 首次安裝

- [ ] 安裝 Win11 IoT Enterprise LTSC 2024
- [ ] 啟用 Shell Launcher + Keyboard Filter
- [ ] 建立 `TrioOperator` 標準使用者帳號
- [ ] 複製 `C:\TRIO2026\App\` 應用程式目錄
- [ ] 複製 `C:\TRIO2026\Database\` 初始資料庫
- [ ] 設定 Shell Launcher 指向 `TRIO2026.App.exe`
- [ ] 設定 UWF 並排除 Database/Logs/Backup 目錄
- [ ] 停用不必要的 Windows 服務
- [ ] 設定電源管理（停用休眠/睡眠）
- [ ] 設定 Windows Update 為手動
- [ ] 測試觸控功能（含手套操作）
- [ ] 驗證 Shell Launcher 退出自動重啟
- [ ] 執行 72 小時穩定性測試

### 6.2 版本更新 SOP

1. 透過 RDP 或 USB 登入 Administrator 帳號
2. 停止 TRIO2026 應用程式
3. 備份 `Database\` 目錄
4. 替換 `App\` 目錄下的檔案
5. 重新啟動系統
6. 驗證應用程式正常啟動
7. 登出 Administrator（自動回到 Kiosk 模式）

---

## 7. 成本估算 (參考)

| 項目 | 估算成本 (USD) |
|------|----------------|
| 7" 觸控工業面板電腦 (i3 / 8GB / 128GB) | $800 ~ $1,500 |
| Win11 IoT Enterprise LTSC OEM 授權 | $150 ~ $250 |
| 工業級 SSD 128GB (MLC/pSLC) | $50 ~ $100 |
| 額外 RAM (若需升級) | $30 ~ $60 |
| **合計 (單台)** | **$1,030 ~ $1,910** |

> [!NOTE]
> 價格為 2026 年市場參考，實際依供應商報價與數量折扣而定。
> OEM 授權通常透過 IPC 廠商購機時一併取得，單獨購買價格較高。
