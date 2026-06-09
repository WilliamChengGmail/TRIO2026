# 動態數字鍵盤密碼輸入模式

## 背景
新增「僅限動態鍵盤輸入」模式，當啟用此設定後：
1. 密碼規則僅保留 **最短/最長密碼長度**（operator 和 admin 分別保留），其他複雜度規則自動忽略
2. 密碼欄位**禁止實體鍵盤輸入**，點擊後彈出動態數字鍵盤
3. 數字鍵盤上的數字位置每次開啟時**隨機打亂**（防窺視）

## 設計決策

### 動態鍵盤行為
- 每次開啟時 0-9 數字**隨機排列**
- 支援退格（⌫）和清除（C）
- 確認後自動關閉鍵盤，密碼傳回輸入框
- 鍵盤面板大小適配 7 吋觸控面板

### 密碼規則調整
| 設定 | `numeric_keypad_only=0` | `numeric_keypad_only=1` |
|------|-------------------------|-------------------------|
| min_length | ✅ 生效 | ✅ 生效 |
| max_length | ✅ 生效 | ✅ 生效 |
| require_mixed | ✅ 生效 | ❌ 忽略 |
| require_special | ✅ 生效 | ❌ 忽略 |
| require_upper | ✅ 生效 | ❌ 忽略 |
| require_lower | ✅ 生效 | ❌ 忽略 |
| require_digit | ✅ 生效 | ❌ 忽略 |

> [!IMPORTANT]
> 請確認：動態鍵盤是否也適用於 **ChangePasswordOverlay** 和 **CreateAccountOverlay**，還是僅限 LoginPage？

## Proposed Changes

### 1. SystemSettingSeed — 新增設定

#### [MODIFY] [SystemSettingSeed.cs](file:///d:/TRIO2026/src/TRIO2026.Data/Seeding/SystemSettingSeed.cs)
- PasswordPolicy 區塊新增 `numeric_keypad_only` 設定（預設 `0`）

---

### 2. SystemSettingService — 便利屬性

#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)
- 新增 `NumericKeypadOnly` bool 屬性

---

### 3. PasswordPolicyService — 規則忽略邏輯

#### [MODIFY] [PasswordPolicyService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/PasswordPolicyService.cs)
- `Validate()` / `GetPolicyRules()` / `GetPolicyHint()` 中判斷 `NumericKeypadOnly`
- 啟用時僅保留 min/max length 規則

---

### 4. NumericKeypadOverlay — 新增動態數字鍵盤 UI

#### [NEW] NumericKeypadOverlay.xaml
- 半透明遮罩 + 置中鍵盤面板
- 3x4 格 + 退格/清除按鈕
- 每次顯示時數字順序隨機化

#### [NEW] NumericKeypadOverlay.xaml.cs
- `Show(Action<string> callback)` — 開啟鍵盤並在確認後回傳輸入值
- 數字隨機排列邏輯
- 密碼遮罩顯示（●●●●）

---

### 5. LoginPage 整合

#### [MODIFY] [LoginPage.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/LoginPage.xaml)
- 加入 NumericKeypadOverlay 控制項

#### [MODIFY] [LoginPage.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/Views/Pages/LoginPage.xaml.cs)
- 密碼欄位 GotFocus 時偵測設定，啟用時攔截 focus 並開啟數字鍵盤
- 禁用 PasswordBox 的實體鍵盤輸入（PreviewKeyDown 攔截）

---

## Verification Plan
- `dotnet build` 編譯驗證
- 手動測試：開啟 `numeric_keypad_only=1`，確認登入頁密碼欄位彈出數字鍵盤
- 確認數字每次開啟時順序不同
- 確認實體鍵盤輸入被阻擋
