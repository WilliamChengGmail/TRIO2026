# 建立 DB 主題切換與暖色淺色系實作計畫

這個計畫旨在解決目前介面中大量寫死的色碼（Hardcoded Hex Colors），建立正規的佈景主題機制，並加入使用者期望的「暖色淺色系」，同時可透過 DB 設定後在重啟時套用。

## User Review Required

> [!WARNING]
> **全面重構 XAML 色碼：** 
> 由於目前的深色色系（如 `#0A1628` 背景、`#F0F4F8` 文字、`#42A5F5` 主色）散佈在所有的 `Page` 和 `Control`（超過 360 個地方），我們需要一次性把所有的 XAML 寫死的 `#HEX` 色碼重構成 `DynamicResource` 綁定。這將會修改幾乎所有的 `.xaml` 檔案。

> [!IMPORTANT]
> **暖色淺色系配色確認：**
> 為了達成「偏向暖色基底」，我提議以下的基礎配色（可依您喜好微調）：
> - **主背景 (AppBackground)**: `#FDFBF7` (極淺的暖米白色)
> - **卡片/面板背景 (CardBg)**: `#FFFFFF` (純白) 或 `#F5F2EB` (淺暖灰)
> - **主要文字 (TextPrimary)**: `#3E3A39` (深暖灰黑，避免純黑以降低對比刺眼感)
> - **次要文字 (TextSecondary)**: `#7A7571` (中等暖灰)
> - **主色調 (PrimaryBlue)**: `#1976D2` (保留藍色系但適合淺色底的深邃藍) 或 您可指定其他暖色主色(如橘紅/暖黃)。
> 若您對上述配色方向滿意，請點擊 Proceed 進行重構作業。

## Proposed Changes

### Theme 資源目錄與字典
#### [NEW] [DarkTheme.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Themes/DarkTheme.xaml)
- 將目前所有的深色系（`#0A1628`, `#2A3A5C`, `#F0F4F8` 等）集中定義為 `SolidColorBrush` 和 `Color` 資源。

#### [NEW] [LightTheme.xaml](file:///d:/TRIO2026/src/TRIO2026.App/Themes/LightTheme.xaml)
- 建立對應的暖色淺色系資源定義。

### 服務層與啟動流程
#### [MODIFY] [SystemSettingService.cs](file:///d:/TRIO2026/src/TRIO2026.App/Services/SystemSettingService.cs)
- 加入 `AppTheme` 屬性（讀寫 `Category="UI"`, `Key="Theme"`），預設值為 `Dark`。

#### [MODIFY] [App.xaml.cs](file:///d:/TRIO2026/src/TRIO2026.App/App.xaml.cs)
- 在 `OnStartup` 階段（DB 初始化完成後），讀取 Theme 設定。
- 將對應的 `DarkTheme.xaml` 或 `LightTheme.xaml` 動態載入到 `Application.Current.Resources.MergedDictionaries` 中。

### UI 檔案全面重構
#### [MODIFY] 所有 `.xaml` 檔案 (Pages & Controls)
- 刪除所有 `UserControl.Resources` 或 `Window.Resources` 中重複定義的 `Color x:Key="TextPrimary"` 等項目。
- 將 `Background="#0A1628"` 替換為 `Background="{DynamicResource AppBackgroundBrush}"` 等。
- 透過程式化批量替換，確保主題切換能夠完整套用至每個角落。

## Verification Plan

### Manual Verification
1. 啟動應用程式，預設仍為深色模式，檢查是否有遺漏的破圖或文字顏色錯誤。
2. 進入 DB 將 `SystemSetting` 中的 `UI.Theme` 設為 `Light`。
3. 重新啟動 App。
4. 檢查所有的視窗、首頁、選單、燈管控制頁面是否正確呈現「暖色淺色系」，且文字對比度足夠清晰。
