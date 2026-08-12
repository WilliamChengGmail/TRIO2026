# 動態 App 啟動畫面（SplashWindow）主題色彩支援實作計劃

本計劃旨在解決 TRIO2026 應用程式在啟動時，`SplashWindow`（啟動畫面）的 Logo、背景及動畫控制項色彩無法隨著使用者設定的 Light/Dark 主題動態切換的問題。

## 1. 變更原因與背景
目前 `SplashWindow.xaml` 的背景、文字、Logo、動畫 Spinner 顏色均為硬編碼（Hardcoded）的深色系色彩（例如背景 `#0F1B2D`、Logo `#4FC3F7`）。
當使用者在系統中切換為 `Light`（淺色）主題時，啟動畫面依然是深色系，導致風格不一致。
此外，由於 `SplashWindow` 必須在 App 剛啟動、DI 容器與資料庫設定（SystemSettingService）尚未載入前立即顯示，因此在一開始載入 DynamicResource 時，需要有安全的「預載主題」機制。

## 2. 開發者 / 撰寫者資訊
- **分析與撰寫**: Office of William

## 3. 開發規範與安全說明
- **變更溯源**: 本變更關聯之主要 C# 類別為 `TRIO2026.App.App`（於 `App.xaml.cs`）以及 `TRIO2026.App.Views.SplashWindow`。
- **秘密資訊防護**: 本檔案及修改後的程式碼不包含任何真實金鑰或敏感憑證。

---

## 4. 預期變更內容

本變更將分為三個主要部分：

### A. 主題資源定義（Themes）

我們將在兩個主題字典中，定義一組完全對稱的 Splash 專屬色票與筆刷。

#### [MODIFY] [LightTheme.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Themes/LightTheme.xaml)
在 `LightTheme.xaml` 中新增 Splash 專屬的淺色系筆刷與陰影色票：
```xml
    <!-- Splash 專用色彩與筆刷 (淺色系) -->
    <Color x:Key="SplashLogoColor">#1976D2</Color>
    <SolidColorBrush x:Key="SplashLogoBrush" Color="{StaticResource SplashLogoColor}" />
    <SolidColorBrush x:Key="SplashTextBrush" Color="#7A7571" />
    <SolidColorBrush x:Key="SplashLineBrush" Color="#00838F" />
    <SolidColorBrush x:Key="SplashSpinnerBrush" Color="#1976D2" />
    <SolidColorBrush x:Key="SplashStatusBrush" Color="#7A7571" />
    <SolidColorBrush x:Key="SplashCopyrightBrush" Color="#A09C98" />
```

#### [MODIFY] [DarkTheme.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Themes/DarkTheme.xaml)
在 `DarkTheme.xaml` 中新增 Splash 專屬的深色系筆刷與陰影色票：
```xml
    <!-- Splash 專用色彩與筆刷 (深色系) -->
    <Color x:Key="SplashLogoColor">#4FC3F7</Color>
    <SolidColorBrush x:Key="SplashLogoBrush" Color="{StaticResource SplashLogoColor}" />
    <SolidColorBrush x:Key="SplashTextBrush" Color="#78909C" />
    <SolidColorBrush x:Key="SplashLineBrush" Color="#2196F3" />
    <SolidColorBrush x:Key="SplashSpinnerBrush" Color="#4FC3F7" />
    <SolidColorBrush x:Key="SplashStatusBrush" Color="#607D8B" />
    <SolidColorBrush x:Key="SplashCopyrightBrush" Color="#37474F" />
```

---

### B. 啟動畫面 UI 資源綁定（SplashWindow）

#### [MODIFY] [SplashWindow.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Views/SplashWindow.xaml)
將所有硬編碼色彩改為 `DynamicResource` 綁定，使其可以動態響應主題載入：
- **視窗背景**: `Background="{DynamicResource AppBackgroundBrush}"`
- **漸層背景**: 
  - `LinearGradientBrush` 的 `GradientStop` 顏色分別綁定為 `{DynamicResource SplashGradient1}`、`{DynamicResource SplashGradient2}`、`{DynamicResource SplashGradient3}`。
- **Logo "TRIO"**: 
  - `Foreground="{DynamicResource SplashLogoBrush}"`
  - `DropShadowEffect.Color="{DynamicResource SplashLogoColor}"`
- **版本號 "2 0 2 6"**: `Foreground="{DynamicResource SplashTextBrush}"`
- **分隔線**: `Background="{DynamicResource SplashLineBrush}"`
- **動畫 Spinner Path**: `Stroke="{DynamicResource SplashSpinnerBrush}"`
- **狀態文字 StatusText**: `Foreground="{DynamicResource SplashStatusBrush}"`
- **版權資訊**: `Foreground="{DynamicResource SplashCopyrightBrush}"`

---

### C. 啟動生命週期與主題載入最佳化（App.xaml.cs）

#### [MODIFY] [App.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/App.xaml.cs)
為了解決在讀取資料庫設定前 `DynamicResource` 無法解析的問題，我們將實作以下生命週期順序：
1. **App 啟動最一開始 (OnStartup)**:
   - 先預載預設主題（`DarkTheme.xaml`）至 `Application.Current.Resources.MergedDictionaries` 中。
   - 接著實例化並顯示 `SplashWindow`。此時啟動畫面將完美呈現 Dark 樣式。
2. **資料庫載入完成、解析出 `sysSettings.UITheme` 時**:
   - 安全地呼叫 `Application.Current.Resources.MergedDictionaries.Clear()` 清除預載主題。
   - 載入使用者真正設定的主題（`LightTheme.xaml` 或是 `DarkTheme.xaml`）並加入 `MergedDictionaries`。
   - `SplashWindow` 的 `DynamicResource` 將無縫且平滑地自動轉化為對應的主題色彩（若使用者設定為 Light，將動態淡入為極具質感的暖白系色調）。

---

## 5. 驗證計劃

### 自動化建置測試
我們將在套用變更後，嘗試建置並編譯專案以確保無任何編譯期錯誤：
- 執行建置，確保 XAML 解析無誤、C# 語法正確。

### 手動驗證流程
1. **啟動測試**: 啟動 App，確認啟動時是否不會產生黑畫面或白閃。
2. **主題變更測試**:
   - 將資料庫中的主題設定設為 `Dark`，重啟應用程式，驗證啟動畫面是否為深藍色。
   - 將資料庫中的主題設定設為 `Light`，重啟應用程式，驗證啟動畫面是否在初始化階段平滑地切換為淺色系暖白風格，且 Logo 與進度 Spinner 均顯示為精美的深藍色。
