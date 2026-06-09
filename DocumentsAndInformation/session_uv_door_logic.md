# Session Timeout × UV 照射 × 門板 × IdleTimer 邏輯關係

> 製作者：Office of William | 更新日期：2026-06-02

---

## 一、系統架構總覽

```mermaid
graph TD
    subgraph "Hardware Layer"
        SIM["Hardware Simulator<br/>TCP Server :5020"]
        DOOR["🚪 門板 Sensor"]
        UV_HW["💡 UV 燈管"]
    end

    subgraph "Service Layer"
        MOCK["MockUvHardwareService<br/>TCP Client"]
        IDLE["IdleTimerService<br/>全域閒置監聽"]
        SESSION["SessionService<br/>登入/鎖定管理"]
    end

    subgraph "ViewModel Layer"
        UV_VM["UvDecontaminationViewModel<br/>UV 倒數 + 門板互鎖"]
    end

    subgraph "View Layer"
        UV_PAGE["UvDecontaminationPage"]
        LOCK["LockScreenOverlay"]
        SHELL["AppShell"]
    end

    SIM -->|DoorOpened/DoorClosed| MOCK
    MOCK -->|Event| UV_VM
    UV_VM -->|StartUV/StopUV| MOCK
    MOCK -->|TCP Command| SIM

    IDLE -->|TimeoutTriggered| SHELL
    SHELL -->|ShowLockScreen| LOCK
    UV_VM -->|DoorInterrupted/DoorResumed| UV_PAGE
    UV_PAGE -->|Passthrough Message| LOCK
```

---

## 二、核心狀態機

### 2.1 IdleTimer 狀態

```mermaid
stateDiagram-v2
    [*] --> Stopped : 初始 / Guest 帳號
    Stopped --> Running : Start(timeoutMinutes)
    Running --> Stopped : Stop() / 超時登出
    Running --> Running : 使用者輸入 → Reset

    note right of Running
        每秒 Tick
        監聽: MouseButton, MouseWheel, Touch, Keyboard
        排除: MouseMove (防止 UI 更新誤觸)
    end note
```

> [!IMPORTANT]
> **UV 執行中不暫停 IdleTimer** — 安全考量：防止使用者離開後被未授權人員接管操作。

### 2.2 UV 照射狀態

```mermaid
stateDiagram-v2
    [*] --> Idle : 初始
    Idle --> Running : 使用者按 Start + 門板關閉
    Idle --> Idle : 使用者按 Start + 門板開啟 → 阻擋警告

    Running --> Paused : 門板開啟 (DoorInterrupted)
    Running --> Idle : 倒數歸零 (Complete)
    Running --> Idle : 使用者確認停止 (ConfirmStop)

    Paused --> Running : 門板關閉 (DoorResumed)
    Paused --> Idle : 使用者確認停止 (ConfirmStop)
```

---

## 三、事件流程圖

### 3.1 UV 啟動流程

```mermaid
sequenceDiagram
    participant User as 👤 使用者
    participant Page as UvPage
    participant VM as UvViewModel
    participant HW as MockUvHardware
    participant SIM as Simulator
    participant Timer as IdleTimer

    User->>Page: 按下 Start
    Page->>VM: StartCommand
    VM->>VM: 檢查 IsDoorOpen
    alt 門板開啟
        VM-->>Page: StartBlockedByDoor
        Page-->>User: ⚠ 警告對話框
    else 門板關閉
        VM->>HW: StartUvLampAsync(durationSeconds)
        HW->>SIM: {"Command":"StartUV","Duration":900}
        SIM->>SIM: 啟動倒數計時
        VM->>VM: IsRunning = true, 啟動 Timer
        Note over Timer: IdleTimer 繼續運行<br/>(不暫停)
    end
```

### 3.2 門板開啟中斷流程

```mermaid
sequenceDiagram
    participant SIM as Simulator
    participant HW as MockUvHardware
    participant VM as UvViewModel
    participant Page as UvPage
    participant Lock as LockScreen
    participant Timer as IdleTimer

    SIM->>SIM: 使用者按「開啟門板」
    SIM->>SIM: 暫停倒數計時 ⏸
    SIM->>HW: {"Event":"DoorOpened"}
    HW->>VM: DoorOpened event
    VM->>VM: IsDoorOpen = true
    VM->>VM: _timer.Stop() 暫停倒數

    alt 畫面未鎖定
        VM-->>Page: DoorInterrupted
        Page->>Page: 顯示 DoorErrorOverlay
    else 畫面已鎖定
        VM-->>Page: DoorInterrupted
        Page->>Lock: ShowPassthroughMessage("門板開啟警告")
    end

    Note over Timer: IdleTimer 不受影響<br/>持續計時
```

### 3.3 門板關閉恢復流程

```mermaid
sequenceDiagram
    participant SIM as Simulator
    participant HW as MockUvHardware
    participant VM as UvViewModel
    participant Page as UvPage
    participant Lock as LockScreen

    SIM->>SIM: 使用者按「關閉門板」
    SIM->>SIM: 恢復倒數計時 ▶
    SIM->>HW: {"Event":"DoorClosed"}
    HW->>VM: DoorClosed event
    VM->>VM: IsDoorOpen = false
    VM->>VM: _timer.Start() 恢復倒數

    alt 畫面未鎖定
        VM-->>Page: DoorResumed
        Page->>Page: 隱藏 DoorErrorOverlay
    else 畫面已鎖定
        VM-->>Page: DoorResumed
        Page->>Lock: HidePassthroughMessage()
    end
```

### 3.4 Session Timeout 鎖定流程（UV 執行中）

```mermaid
sequenceDiagram
    participant Timer as IdleTimer
    participant Shell as AppShell
    participant Lock as LockScreen
    participant VM as UvViewModel

    Timer->>Timer: _elapsedSeconds >= _timeoutSeconds
    Timer->>Shell: TimeoutTriggered
    Shell->>Lock: Show() 鎖定畫面

    Note over VM: UV 繼續照射<br/>倒數繼續
    Note over Lock: 顯示工作狀態:<br/>"UV 照射中 — 剩餘 xx:xx"

    alt UV 完成（鎖定中）
        VM-->>Shell: CountdownCompleted
        Shell->>Lock: EnqueueMessage("UV完成")
        Shell->>Lock: UpdateWorkStatus(null)
        Note over Lock: 解鎖後彈出完成訊息
    end
```

---

## 四、三層同步對照表

### 4.1 UV 照射狀態同步

| 事件 | App ViewModel | App UI (Page) | Simulator |
|------|--------------|---------------|-----------|
| **啟動 UV** | `IsRunning=true`, `_timer.Start()` | 顯示倒數 UI | `🟣 照射中 — 剩餘 14:59` |
| **門板開啟** | `_timer.Stop()`, `DoorInterrupted` | 顯示 DoorErrorOverlay | `⏸ 暫停（門板開啟）— 剩餘 xx:xx` |
| **門板關閉** | `_timer.Start()`, `DoorResumed` | 隱藏 DoorErrorOverlay | `🟣 照射中 — 剩餘 xx:xx` |
| **倒數歸零** | `IsRunning=false`, `StopUvLampAsync()` | 顯示完成對話框 | `⚫ 關閉` |
| **手動停止** | `IsRunning=false`, `StopUvLampAsync()` | 隱藏倒數 UI | `⚫ 關閉` |

### 4.2 IdleTimer 在各情境下的行為

| 情境 | IdleTimer 狀態 | 說明 |
|------|---------------|------|
| 一般操作 | ▶ 運行中 | 使用者互動重置計時器 |
| UV 照射中（使用者不操作） | ▶ **繼續運行** | 超時仍會鎖定畫面 |
| UV 照射中 + 門板開啟 | ▶ 繼續運行 | 不受門板事件影響 |
| 畫面已鎖定 | ⏹ 已停止 | 鎖定後 Stop()，解鎖後重新 Start() |
| Guest 帳號 | ⏹ 不啟動 | Guest 不受 session timeout 限制 |

### 4.3 鎖定畫面穿透機制

| 情境 | 穿透行為 |
|------|---------|
| UV 照射中鎖定 | 鎖定畫面顯示工作狀態 `"UV 照射中"` |
| 鎖定中門板開啟 | `ShowPassthroughMessage()` 在鎖定畫面顯示警告 |
| 鎖定中門板關閉 | `HidePassthroughMessage()` 自動清除警告 |
| 鎖定中 UV 完成 | `EnqueueMessage()` 排隊，解鎖後彈出完成訊息 |

---

## 五、輸入事件過濾規則

[IdleTimerService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/IdleTimerService.cs#L138-L162) `OnPreProcessInput`：

| 事件類型 | 是否重置計時器 | 說明 |
|---------|-------------|------|
| `MouseButtonEventArgs` | ✅ 重置 | 滑鼠點擊 |
| `MouseWheelEventArgs` | ✅ 重置 | 滑鼠滾輪 |
| `TouchEventArgs` | ✅ 重置 | 觸控操作 |
| `KeyEventArgs` | ✅ 重置 | 鍵盤輸入 |
| `MouseMoveEventArgs` | ❌ **排除** | UI 動畫/Binding 更新產生的內部事件 |
| 其他 `InputEventArgs` | ❌ 忽略 | Stylus 等非常見事件 |

> [!WARNING]
> `MouseMoveEventArgs` 被排除的原因：UV 倒數計時每秒更新 `RemainingSeconds` → WPF Binding → Layout 更新 → 內部產生 `MouseMove` 事件 → 導致計時器永遠被重置。

---

## 六、關鍵檔案參考

| 檔案 | 職責 |
|------|------|
| [IdleTimerService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/IdleTimerService.cs) | 全域閒置計時器，監聽輸入事件 |
| [UvDecontaminationViewModel.cs](file:///d:/TRIO2026/src/TRIO2026.App/ViewModels/UvDecontaminationViewModel.cs) | UV 倒數邏輯 + 門板互鎖 |
| [UvDecontaminationPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/UvDecontaminationPage.xaml.cs) | UV 頁面 UI 事件 + 鎖定穿透 |
| [LockScreenOverlay.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Controls/LockScreenOverlay.xaml.cs) | 鎖定畫面 + 穿透訊息 + 工作狀態 |
| [MockUvHardwareService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/MockUvHardwareService.cs) | TCP Client → Simulator 通訊 |
| [SimulatorWindow.xaml.cs](file:///d:/TRIO2026/tools/Simulator/SimulatorWindow.xaml.cs) | 硬體模擬器：門板/UV 狀態模擬 |
| [IUvHardwareService.cs](file:///d:/TRIO2026/src/TRIO2026.Core/Interfaces/IUvHardwareService.cs) | 硬體抽象介面 |
