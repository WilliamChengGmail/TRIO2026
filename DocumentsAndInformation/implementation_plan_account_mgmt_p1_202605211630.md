# 帳號管理完整實作計畫 — Part 1：整體架構 & UserMenu 角色差異（修訂版）

> 製作者: Office of William
> 此計畫與「密碼複雜度驗證」及「密碼管理 UI」為平行計畫，待審核通過後一起或分批執行。

---

## 全域設計規範（適用三份計畫所有實作）

> [!IMPORTANT]
> 1. **觸控優先**：所有按鈕最小高度 56px，無 Hover 效果（僅 IsPressed），間距充裕
> 2. **七吋螢幕**：介面簡潔，列表每列高度 ≥ 56px，字型 ≥ 16px
> 3. **多語系（強制）**：畫面上所有可見文字（標題、按鈕、提示、錯誤訊息）一律透過 `LocalizationService` 鍵值顯示，**絕不可寫死中文或英文字串**
> 4. **Log 埋點（強制）**：所有使用者行為（頁面導航、按鈕點擊、帳號異動、驗證結果）均須呼叫 `EventLogService` 記錄，確保完整稽核軌跡
>    - 頁面導航：`LogNavigation(from, to)`
>    - 按鈕操作：`LogButtonClick(page, button, detail?)`
>    - 帳號異動：`LogAuth(action, username, success, detail?)`
>    - 其他事件：`LogInfo(category, source, errorCode, message)`
> 5. **角色守衛**：所有帳號管理操作在 UI 層 + Service 層雙重驗證 Admin 角色
> 6. **假刪除原則**：所有「刪除」操作均為軟刪除（Soft Delete），資料庫保留原始紀錄

---

## 現狀盤點

| 元件 | 現狀 |
|------|------|
| UserMenuControl | 有：Home / ServiceMode / 語系 / 登出 四個節點，所有角色共用 |
| ServiceModePage | 僅 Placeholder，無任何功能區塊 |
| AuthService | 有：LoginAsync / HashPassword / GetAllUsersAsync / UpdateLanguagePreferenceAsync |
| User 實體 | 完整，含 ForcePasswordChange / LockedUntil / IsActive / RoleLevel |
| 帳號管理 UI | ❌ 不存在 |
| ChangePasswordAsync | ❌ 不存在 |

---

## Part 1 範圍：UserMenu 角色差異重構

### 各角色 UserMenu 節點對照表（修訂）

| 節點 | Guest | Operator | Service | Admin |
|------|-------|----------|---------|-------|
| 🏠 返回主畫面 | ✅（非 MenuPage 時） | ✅（非 MenuPage 時） | ✅（非 Service 主畫面時）| ✅（非 MenuPage 時） |
| 🔧 Service Mode | ✅（Guest 專用） | ❌ | ❌ | ❌ |
| 🌐 切換語系 | ✅ | ✅ | ✅ | ✅ |
| 🔑 變更密碼 | ❌ | ✅（新增） | ❌（**不可出現**） | ✅（管理者密碼） |
| 👤 帳號管理 | ❌ | ❌ | ❌ | ✅（新增） |
| 🚪 登出/關閉 | ✅ | ✅ | ✅ | ✅ |

> [!IMPORTANT]
> **「返回主畫面」顯示規則**：
> - **MenuPage**（`ShowHomeButton=False`）：**不顯示**（既有行為不變）
> - **其他頁面**（UV、AccountMgmt 等）：依角色顯示
>   - Operator / Admin / Guest → 返回 MenuPage
>   - Service → 返回 **ServiceModePage 主畫面**（下方說明）
>
> **Service 角色限制**：Service 的密碼只能由 DB 直接變更，UI 中**絕對不可出現**「變更密碼」節點。
>
> **Admin 角色**：「變更密碼」節點用來變更管理者自己的密碼；「帳號管理」節點開啟帳號管理頁面。

---

### Service 角色的「返回主畫面」導向

ServiceModePage 內部可能有功能層次（子頁面），當 Service 使用者進入子頁面後，UserMenu 的「返回主畫面」應導回 **ServiceModePage**（而非 MenuPage）。

實作方式：`UserMenuControl.OnHomeClick()` 中，判斷當前使用者角色：

```csharp
private void OnHomeClick(object sender, RoutedEventArgs e)
{
    CloseAllOverlays();
    var shell = Window.GetWindow(this) as AppShell;
    if (shell == null) return;

    // Service 角色（非 Guest 模式）→ 返回 ServiceModePage 主畫面
    if (_sessionService != null && !_sessionService.IsGuestMode
        && _sessionService.CurrentRole == RoleLevel.Service)
    {
        shell.NavigateTo("service");   // 既有行為，保持不變
    }
    else
    {
        shell.NavigateTo("menu");       // Operator / Admin / Guest → MenuPage
    }
}
```

> [!NOTE]
> 此邏輯目前已在 `OnHomeClick()` 中存在（`case RoleLevel.Service → "service"`），
> 只需確保 ServiceModePage 子頁面也正確傳遞 `ShowHomeButton=True`，
> 且 Home 按鈕在 ServiceModePage 本身（主畫面）時不顯示（`ShowHomeButton=False`）。

---

### 受影響的 UserMenu 節點插入位置

```
[ 🏠 返回主畫面 ]  — 依頁面與角色控制可見性（既有，行為微調）
[ 分隔線 ]
[ 🔧 Service Mode ]  — 僅 Guest 可見（既有）
[ 分隔線 ]（Service Mode 分隔線）
[ 🌐 切換語系 ]  — 全角色（既有）
[ 分隔線 ]
─────────────── 新增節點（以下） ───────────────
[ 🔑 變更密碼 ]  — 僅 Operator / Admin 可見（新增）
[ 分隔線 ]（ChangePassword 分隔線）
[ 👤 帳號管理 ]  — 僅 Admin 可見（新增）
[ 分隔線 ]（AccountMgmt 分隔線）
─────────────────────────────────────────────
[ 🚪 登出/關閉 ]  — 全角色（既有）
```

---

### [MODIFY] UserMenuControl.xaml

新增兩個 Button 節點（在語系分隔線之後、登出按鈕之前）：

```xml
<!-- 變更密碼（Operator / Admin 可見，Service 不可見） -->
<Button x:Name="BtnChangePassword" Click="OnChangePasswordClick" Cursor="Hand"
        Visibility="Collapsed">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border x:Name="Bd" Background="Transparent"
                    CornerRadius="8" Padding="20,16">
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="🔑" FontSize="22" VerticalAlignment="Center"
                               Margin="0,0,12,0"/>
                    <TextBlock Text="{Binding [UserMenu.ChangePassword],
                               Source={x:Static svc:LocalizationService.Instance}}"
                               FontSize="17" Foreground="#F0F4F8" VerticalAlignment="Center"/>
                </StackPanel>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsPressed" Value="True">
                    <Setter TargetName="Bd" Property="Background" Value="#3A5A8A"/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
    </Button.Template>
</Button>
<Border x:Name="ChangePasswordSeparator" Height="1" Background="#2A3D5E" Margin="8,4"
        Visibility="Collapsed"/>

<!-- 帳號管理（Admin 可見） -->
<Button x:Name="BtnAccountMgmt" Click="OnAccountMgmtClick" Cursor="Hand"
        Visibility="Collapsed">
    <Button.Template>
        <!-- 同上樣式，文字 UserMenu.AccountMgmt，圖示 👤 -->
    </Button.Template>
</Button>
<Border x:Name="AccountMgmtSeparator" Height="1" Background="#2A3D5E" Margin="8,4"
        Visibility="Collapsed"/>
```

### [MODIFY] UserMenuControl.xaml.cs

在 `RefreshUserDisplay()` 中加入角色判斷：

```csharp
// 「變更密碼」：Operator(1) 和 Admin(3) 可見，Service(2) 絕對不可見
var showChangePassword = !isGuest
    && user.RoleLevel != (int)RoleLevel.Service;
BtnChangePassword.Visibility = showChangePassword
    ? Visibility.Visible : Visibility.Collapsed;
ChangePasswordSeparator.Visibility = showChangePassword
    ? Visibility.Visible : Visibility.Collapsed;

// 「帳號管理」：僅 Admin(3) 可見
var showAccountMgmt = !isGuest
    && user.RoleLevel == (int)RoleLevel.Admin;
BtnAccountMgmt.Visibility = showAccountMgmt
    ? Visibility.Visible : Visibility.Collapsed;
AccountMgmtSeparator.Visibility = showAccountMgmt
    ? Visibility.Visible : Visibility.Collapsed;
```

新增事件處理方法：

```csharp
private async void OnChangePasswordClick(object sender, RoutedEventArgs e)
{
    CloseAllOverlays();
    // 呼叫 ChangePasswordOverlay（見 implementation_plan_password_ui）
    // TODO: 待 ChangePasswordOverlay 實作完成後對接
}

private void OnAccountMgmtClick(object sender, RoutedEventArgs e)
{
    CloseAllOverlays();
    // 導航至帳號管理頁面（獨立 Page，整頁切換）
    var shell = Window.GetWindow(this) as AppShell;
    shell?.NavigateTo("accountMgmt");
}
```

### Initialize() 方法簽章不變

`UserMenuControl.Initialize()` 簽章保持不變，不需新增參數。
帳號管理頁面所需的 `AuthService` 已在 Initialize 時傳入並可轉發。

---

## 帳號管理頁面開啟方式（Q-P1-1 已確認）

> [!NOTE]
> 依照您的決議：**另外規劃新的 Page，整頁切換，不以 Overlay 方式呈現。**

- 在 `AppShell` 中新增 `"accountMgmt"` 路由
- 對應新建 `AccountManagementPage.xaml / .xaml.cs`
- 完成操作後，點擊「← 返回」或 UserMenu Home 按鈕返回 MenuPage

---

## ServiceModePage 功能區塊規劃（修訂）

> [!NOTE]
> 依照您的決議：**先建立功能殼**，各區塊點擊後顯示「功能待開發」提示訊息。
> 「重設管理者密碼」為本次唯一實作完整功能的區塊。

### ServiceModePage 功能區塊版面

```
┌─────────────────────────────────────────┐
│  🔧 Service Mode          [使用者選單]  │   ← 頂部列（無 HOME，本頁為 Service 主畫面）
├─────────────────────────────────────────┤
│  ┌────────────────┐  ┌────────────────┐ │
│  │ ⚙️ Machine     │  │ 🔄 Flow        │ │
│  │    Setting     │  │    Setting     │ │
│  │                │  │                │ │
│  │  [開發中...]   │  │  [開發中...]   │ │
│  └────────────────┘  └────────────────┘ │
│  ┌────────────────┐  ┌────────────────┐ │
│  │ 📡 Communi-   │  │ 🔐 重設        │ │
│  │    cation     │  │   管理者密碼   │ │
│  │               │  │                │ │
│  │  [開發中...]   │  │  ✅ 已實作     │ │
│  └────────────────┘  └────────────────┘ │
└─────────────────────────────────────────┘
```

### 各功能區塊實作狀態

| 功能區塊 | 圖示 | 說明 | 本次狀態 |
|----------|------|------|----------|
| Machine Setting | ⚙️ | 含 Flow List / Other（子頁面） | 🔲 殼（點擊→顯示「功能待開發」） |
| Flow Setting | 🔄 | 含 Sub-Function / Block（子頁面） | 🔲 殼（點擊→顯示「功能待開發」） |
| Communication | 📡 | 通訊設定 | 🔲 殼（點擊→顯示「功能待開發」） |
| 重設管理者密碼 | 🔐 | Admin level 帳號密碼重設 | ✅ 完整實作（見 Part 3） |

### 功能殼按鈕行為

點擊「開發中」區塊後，以 `OverlayDialog.ShowAsync()` 顯示：

```csharp
await _dialogOverlay.ShowAsync(
    loc["ServiceMode.ComingSoonTitle"],      // "功能開發中"
    loc["ServiceMode.ComingSoonMessage"],    // "此功能正在開發中，敬請期待。"
    loc["Common.OK"],
    OverlayDialogIcon.Info);
```

i18n 新增鍵值（Module = `ServiceMode`）：

| Key | en | zh-TW |
|-----|----|-------|
| `ServiceMode.ComingSoonTitle` | Under Development | 功能開發中 |
| `ServiceMode.ComingSoonMessage` | This feature is under development. Stay tuned. | 此功能正在開發中，敬請期待。 |

---

## Open Questions（Part 1 — 全部已解決）

| # | 問題 | 決議 |
|---|------|------|
| Q-P1-1 | 帳號管理頁面開啟方式 | ✅ 選項 A：獨立新 Page，AppShell 整頁切換 |
| Q-P1-2 | ServiceModePage 功能按鈕是否本次實作 | ✅ 建立功能殼，點擊顯示「功能開發中」，重設管理者密碼完整實作 |
| Q-P1-3 | ServiceModePage 子頁面導覽方式 | ✅ **選項 B**：ServiceModePage 內部自管子頁面切換（內嵌 Frame 或 ContentControl），不擴充 AppShell 路由表 |

---

*Part 2 將涵蓋：帳號管理頁面 UI 設計（AccountManagementPage）+ 帳號列表 + 功能流程*
