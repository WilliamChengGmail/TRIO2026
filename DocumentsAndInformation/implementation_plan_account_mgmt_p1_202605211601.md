# 帳號管理完整實作計畫 — Part 1：整體架構 & UserMenu 角色差異

> 製作者: Office of William
> 此計畫與「密碼複雜度驗證」及「密碼管理 UI」為平行計畫，待審核通過後一起或分批執行。

---

## 設計原則

> [!IMPORTANT]
> 1. **觸控優先**：所有按鈕最小高度 56px，無 Hover 效果（僅 IsPressed），間距充裕
> 2. **七吋螢幕**：介面簡潔，列表每列高度 ≥ 56px，字型 ≥ 16px
> 3. **多語系**：所有文字透過 `LocalizationService` 的 DB 鍵值，Module = `AccountMgmt`
> 4. **角色守衛**：所有帳號管理操作在 UI 層 + Service 層雙重驗證 Admin 角色
> 5. **事件日誌**：所有帳號異動呼叫 `EventLogService`，保留稽核軌跡

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

### 各角色 UserMenu 節點對照表

| 節點 | Guest | Operator | Service | Admin |
|------|-------|----------|---------|-------|
| 🏠 返回主畫面 | ✅ | ✅ | ❌ | ✅ |
| 🔧 Service Mode | ✅（Guest專用） | ❌ | ❌ | ❌ |
| 🌐 切換語系 | ✅ | ✅ | ✅ | ✅ |
| 🔑 變更密碼 | ❌ | ✅（新增） | ❌（不可有此節點） | ✅（管理者密碼） |
| 👤 帳號管理 | ❌ | ❌ | ❌ | ✅（新增） |
| 🚪 登出/關閉 | ✅ | ✅ | ✅ | ✅ |

> [!IMPORTANT]
> **Service 角色限制**：Service 的密碼只能由 DB 直接變更，UI 中**不可出現**「變更密碼」節點。
> **Admin 角色**：「變更密碼」節點用來變更管理者自己的密碼；「帳號管理」節點開啟帳號管理頁面。

### 受影響的 UserMenu 節點插入位置

```
[ 🏠 返回主畫面 ]  — 依角色控制可見性（既有）
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
                    <TextBlock Text="{Binding [UserMenu.ChangePassword], ...}"
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
    <!-- 同上樣式，文字 UserMenu.AccountMgmt，圖示 👤 -->
</Button>
<Border x:Name="AccountMgmtSeparator" Height="1" Background="#2A3D5E" Margin="8,4"
        Visibility="Collapsed"/>
```

### [MODIFY] UserMenuControl.xaml.cs

在 `RefreshUserDisplay()` 中加入角色判斷：

```csharp
// 「變更密碼」：Operator(1) 和 Admin(3) 可見，Service(2) 不可見
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

新增事件處理方法（本計畫 Part 1 定義，Part 2 填充邏輯）：

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
    // 導航至帳號管理頁面
    var shell = Window.GetWindow(this) as AppShell;
    shell?.NavigateTo("accountMgmt");
}
```

### Initialize() 方法簽章不變

`UserMenuControl.Initialize()` 方法簽章保持不變，不需新增參數。
帳號管理頁面所需的 `AuthService` 已在 Initialize 時傳入並可轉發。

---

## ServiceModePage 功能區塊規劃

> [!NOTE]
> Service 登入後的 ServiceModePage 目前是 Placeholder，需要規劃功能區塊。
> 這部分獨立於帳號管理，但在此一併列出，供審核。

### ServiceModePage 功能區塊（一覽）

```
┌─────────────────────────────────────────┐
│  🔧 Service Mode      [使用者選單]       │
├─────────────────────────────────────────┤
│  ┌────────────────┐  ┌────────────────┐ │
│  │ ⚙️ Machine     │  │ 🔄 Flow        │ │
│  │    Setting     │  │    Setting     │ │
│  │ • Flow List    │  │ • Sub-Function │ │
│  │ • Other...     │  │ • Block        │ │
│  └────────────────┘  └────────────────┘ │
│  ┌────────────────┐  ┌────────────────┐ │
│  │ 📡 Communi-   │  │ 🔐 重設        │ │
│  │    cation     │  │   管理者密碼   │ │
│  └────────────────┘  └────────────────┘ │
└─────────────────────────────────────────┘
```

| 功能區塊 | 圖示 | 說明 | 目前狀態 |
|----------|------|------|----------|
| Machine Setting | ⚙️ | 含 Flow List / Other | ❌ Placeholder |
| Flow Setting | 🔄 | 含 Sub-Function/Block | ❌ Placeholder |
| Communication | 📡 | 通訊設定 | ❌ Placeholder |
| 重設管理者密碼 | 🔐 | Admin level 使用者密碼重設 | ❌ 新增（見 Part 3） |

「重設管理者密碼」功能說明：
- Service 工程師可針對 **Admin level (RoleLevel=3)** 的帳號進行密碼重設
- 流程：選擇目標 Admin 帳號 → 系統產生臨時密碼 → 顯示給 Service → 設定 ForcePasswordChange=1

---

## Open Questions（Part 1）

> [!IMPORTANT]
> **Q-P1-1：帳號管理頁面的開啟方式？**
> - 選項 A：在 AppShell 中新增 `accountMgmt` 頁面路由，整頁切換（與 MenuPage 同層）
> - 選項 B：以 Overlay 方式蓋在現有頁面上（類似 LoginOverlay）
>
> **建議**：選項 A（整頁切換），帳號管理功能複雜，整頁空間更合適。
> 使用者點擊後切換頁面，管理完成後返回 MenuPage。

> [!IMPORTANT]
> **Q-P1-2：ServiceModePage 的功能按鈕是否本次一起實作？**
> - Machine Setting / Flow Setting / Communication 都是 Placeholder
> - 建議：本次計畫只完成「重設管理者密碼」功能，其他按鈕仍保持 Placeholder

---

*Part 2 將涵蓋：帳號管理頁面 UI 設計（AccountManagementPage）+ 帳號列表 + 功能流程*
