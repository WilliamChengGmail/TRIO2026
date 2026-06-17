using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TRIO2026.App.Services;

namespace TRIO2026.App.Controls;

/// <summary>
/// 觸控全鍵盤覆蓋層 — LoginPage 帳號/密碼輸入用
///
/// 功能：
///   - QWERTY 固定排列鍵盤
///   - 左下角 ⯪ 按鈕 = CapsLock（開啟時大寫，與實體 CapsLock 雙向連動）
///   - 符號模式切換（!@# / ABC）
///   - 密碼模式：遮罩顯示 + 眼睛切換
///   - 帳號模式：明碼顯示
///   - 觸控環境：52px 按鈕、無 Hover、底部對齊
///   - 開啟時停用 IME，關閉時恢復
///   - 開啟時安裝低層鍵盤 Hook（攔截 Windows 鍵）
///
/// 製作者: Office of William
/// </summary>
public partial class TouchKeyboardOverlay : UserControl
{
    // QWERTY 鍵盤佈局
    private static readonly string[][] LetterRows =
    [
        ["1","2","3","4","5","6","7","8","9","0"],
        ["q","w","e","r","t","y","u","i","o","p"],
        ["a","s","d","f","g","h","j","k","l"],
        ["z","x","c","v","b","n","m"]
    ];

    // 符號鍵盤佈局
    private static readonly string[][] SymbolRows =
    [
        ["!","@","#","$","%","^","&","*"],
        ["(",")", "_","-","=","+","[","]"],
        ["{","}","\\","|",";",":",  "'","\""],
        [",",".","/","?","~","`","<",">"]
    ];

    private string _inputText = "";
    private bool _isPasswordMode;
    private bool _showPlainText;
    private bool _isShifted;             // 虛擬 CapsLock 狀態（與實體 CapsLock 雙向連動）
    private bool _isSymbolMode;
    private bool _physicalShiftHeld = false;     // 實體 Shift 按住中
    private bool _shiftUsedAsCombo = false;      // Shift 是否用於組合鍵
    private InputMethodState _savedImeState = InputMethodState.DoNotCare; // IME 備份
    private bool _virtualCapsLockToggling = false; // 虛擬按鈕觸發旧實體按鍵旗標
    private Window? _parentWindow;       // 父視窗參照（訂閱 Activated 事件）

    // 持續守衛計時器：鍵盤開啟期間每 200ms 自動確保 FocusCatcher 持有鍵盤焦點
    private System.Windows.Threading.DispatcherTimer? _focusGuardTimer;

    private Action<string>? _onConfirm;
    private Action? _onCancel;

    // ═══════════════════════════════════════
    // Win32 P/Invoke
    // ═══════════════════════════════════════

    // ――― CapsLock 模擬 ―――
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    /// <summary>模擬 CapsLock 鍵按下 + 放開，切換實體 CapsLock 狀態</summary>
    private void SimulateCapsLockToggle()
    {
        const byte VK_CAPITAL = 0x14;
        keybd_event(VK_CAPITAL, 0x45, 0x0000, UIntPtr.Zero); // keydown
        keybd_event(VK_CAPITAL, 0x45, 0x0002, UIntPtr.Zero); // keyup
    }

    // ――― IME 層級解除（imm32）―――
    // 內建的 WPF InputMethodState.Off 僅操作邏輯層，
    // ImmAssociateContext(…, Zero) 則在 Win32 HWND 層很後切斷 IME 綁定，
    // 確保任何輸入字元均不經過 IME 中轉直接送到 WPF。
    private IntPtr _originalImc = IntPtr.Zero; // 備份原始 IME context，隔藏時恢復

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

    /// <summary>HWND 層剩断 IME，從根本防止 IME 攔截鍵盤輸入</summary>
    private void DisableImeAtWin32Level()
    {
        if (PresentationSource.FromVisual(this) is not
            System.Windows.Interop.HwndSource src) return;
        if (_originalImc == IntPtr.Zero)
            _originalImc = ImmGetContext(src.Handle); // 備份一次即可
        ImmAssociateContext(src.Handle, IntPtr.Zero);  // 解除 IME
    }

    /// <summary>復原 HWND 層的 IME 綁定，隱藏鍵盤時呼叫</summary>
    private void RestoreImeAtWin32Level()
    {
        if (_originalImc == IntPtr.Zero) return;
        if (PresentationSource.FromVisual(this) is not
            System.Windows.Interop.HwndSource src) return;
        ImmAssociateContext(src.Handle, _originalImc);
        _originalImc = IntPtr.Zero;
    }

    // ――― 低層鍵盤 Hook（攔截 Windows 鍵）―――
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int VK_LWIN        = 0x5B;
    private const int VK_RWIN        = 0x5C;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static LowLevelKeyboardProc? _lowLevelKeyboardProc;
    private static IntPtr _keyboardHookHandle = IntPtr.Zero;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>
    /// 低層鍵盤 Hook Callback。
    /// 攔截 Windows 左/右鍵（VK_LWIN/VK_RWIN）並封鎖輸出。
    /// Fn 鍵由鍵盤韓體在硬體層處理，不發送標準號碼，無法攔截。
    /// </summary>
    private static IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                    return (IntPtr)1; // 封鎖，不傳遞給系統
            }
        }
        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    /// <summary>安裝低層鍵盤 Hook（虛擬鍵盤顯示時呼叫）</summary>
    private static void InstallKeyboardHook()
    {
        if (_keyboardHookHandle != IntPtr.Zero) return;
        _lowLevelKeyboardProc = LowLevelKeyboardCallback;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module  = process.MainModule!;
        _keyboardHookHandle = SetWindowsHookEx(
            WH_KEYBOARD_LL, _lowLevelKeyboardProc,
            GetModuleHandle(module.ModuleName), 0);
    }

    /// <summary>卸載低層鍵盤 Hook（虛擬鍵盤隱藏時呼叫）</summary>
    private static void UninstallKeyboardHook()
    {
        if (_keyboardHookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_keyboardHookHandle);
        _keyboardHookHandle = IntPtr.Zero;
        _lowLevelKeyboardProc = null;
    }

    public TouchKeyboardOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 顯示鍵盤
    /// </summary>
    /// <param name="isPassword">true=密碼模式（遮罩+眼睛），false=帳號模式（明碼）</param>
    /// <param name="initialText">初始文字</param>
    /// <param name="onConfirm">確認 callback</param>
    /// <param name="onCancel">取消 callback</param>
    /// <param name="customTitle">自訂標題（null 時使用預設帳號/密碼標題）</param>
    public void Show(bool isPassword, string initialText, Action<string> onConfirm, Action? onCancel = null, string? customTitle = null)
    {
        _isPasswordMode = isPassword;
        _inputText = initialText ?? "";
        _showPlainText = !isPassword; // 帳號模式預設明碼
        // 同步實體鍵盤 CapsLock 狀態（開啟時左下角 ⯪ 圖示自動對應）
        _isShifted = Keyboard.IsKeyToggled(Key.CapsLock);
        _isSymbolMode = false;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        if (!string.IsNullOrEmpty(customTitle))
        {
            TitleText.Text = customTitle;
        }
        else
        {
            var loc = LocalizationService.Instance;
            TitleText.Text = isPassword
                ? loc["TouchKeyboard.TitlePassword"]
                : loc["TouchKeyboard.TitleAccount"];
        }

        EyeToggle.Visibility = isPassword ? Visibility.Visible : Visibility.Collapsed;
        EyeToggle.Content = _showPlainText ? "🙈" : "👁";

        UpdateDisplay();
        BuildKeyboard();

        Visibility = Visibility.Visible;

        // 開啟鍵盤時主動關閉 IME（防止中文輸入法攔截 Shift 鍵）
        _savedImeState = InputMethod.Current.ImeState;
        InputMethod.Current.ImeState = InputMethodState.Off;
        // Win32 層剩断 IME context（最可靠）
        DisableImeAtWin32Level();

        // 聚焦隱藏 TextBox 以接收實體鍵盤輸入
        FocusCatcher.Text = "";
        // 使用 Loaded 優先級（比 Input 更晚），確保 layout 完全結束後再搞焦點
        Dispatcher.BeginInvoke(() =>
        {
            FocusCatcher.Focus();
            Keyboard.Focus(FocusCatcher);
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!FocusCatcher.IsKeyboardFocused && Visibility == Visibility.Visible)
            {
                FocusCatcher.Focus();
                Keyboard.Focus(FocusCatcher);
            }
        };
        timer.Start();

        // 訂閱父視窗 Activated 事件：
        // 當使用者從其他 App 切回時，自動補回焦點並同步 CapsLock 狀態
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
            _parentWindow.Activated += OnWindowActivated;

        // 啟動持續守衛計時器：
        // 鍵盤開啟期間每 200ms 檢查一次焦點，若已失則自動補回
        // 覆蓋平時 50ms/100ms 單次試不广幾的問題
        _focusGuardTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _focusGuardTimer.Tick += (_, _) =>
        {
            if (Visibility != Visibility.Visible)
            {
                _focusGuardTimer?.Stop();
                return;
            }
            // 無條件重新解除 IME + 強制歸還焦點
            // （WPF 的 IsKeyboardFocused 可能為 true 但 OS/IME 仍輸入被攔）
            InputMethod.Current.ImeState = InputMethodState.Off;
            DisableImeAtWin32Level();
            FocusCatcher.Focus();
            Keyboard.Focus(FocusCatcher);

            // Focus 建立後再讀取 CapsLock 系統狀態（此時 GetKeyState 回傳正確值）
            // 若與虛擬鍵盤狀態不同 → 同步並重繪左下角 ⯪ 圖示
            bool physicalCapsLock = Keyboard.IsKeyToggled(Key.CapsLock);
            if (_isShifted != physicalCapsLock)
            {
                _isShifted = physicalCapsLock;
                BuildKeyboard();
            }
        };
        _focusGuardTimer.Start();

        InstallKeyboardHook();
    }

    /// <summary>隱藏鍵盤並恢復 IME</summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _inputText = "";
        _onConfirm = null;
        _onCancel  = null;

        // 關閉鍵盤時恢復先前的 IME 狀態
        InputMethod.Current.ImeState = _savedImeState;
        // 復原 Win32 層的 IME 綁定
        RestoreImeAtWin32Level();

        // 停止持續守衛計時器
        _focusGuardTimer?.Stop();
        _focusGuardTimer = null;

        // 取消訂閱 Activated 事件，防止記憶體泄漏
        if (_parentWindow != null)
        {
            _parentWindow.Activated -= OnWindowActivated;
            _parentWindow = null;
        }

        // 卸載低層鍵盤 Hook
        UninstallKeyboardHook();
    }

    /// <summary>
    /// 父視窗重新被激活（使用者從其他 App 切回）時：
    ///   1. 立即重新關閉 IME（系統在激活時可能重設）
    ///   2. 同步可能在外部改變的 CapsLock 狀態
    ///   3. 延遲 150ms 後強制補回焦點（持續守衛 Timer 也會輔助）
    /// </summary>
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (Visibility != Visibility.Visible) return;

        // 立即在 Win32 HWND 層剩断 IME（最有效）
        DisableImeAtWin32Level();
        InputMethod.Current.ImeState = InputMethodState.Off;

        // 150ms 延遲：等待 WPF 內部焦點調度完成再補回
        var activationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        activationTimer.Tick += (_, _) =>
        {
            activationTimer.Stop();
            if (Visibility != Visibility.Visible) return;

            // 同步可能在外部 App 改變的實體 CapsLock 狀態
            bool physicalCapsLock = Keyboard.IsKeyToggled(Key.CapsLock);
            if (_isShifted != physicalCapsLock)
            {
                _isShifted = physicalCapsLock;
                BuildKeyboard();
            }

            // 強制補回焦點（不判斷當前狀態，直接設定）
            InputMethod.Current.ImeState = InputMethodState.Off;
            DisableImeAtWin32Level();
            FocusCatcher.Focus();
            Keyboard.Focus(FocusCatcher);
        };
        activationTimer.Start();
    }

    /// <summary>鍵盤顯示時攔截 Tab 鍵，防止焦點跳離</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
            e.Handled = true;
    }

    // ═══════════════════════════════════════
    // 鍵盤建構
    // ═══════════════════════════════════════

    private void BuildKeyboard()
    {
        KeyboardRows.Children.Clear();
        var rows = _isSymbolMode ? SymbolRows : LetterRows;

        for (int r = 0; r < rows.Length; r++)
        {
            var rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1)
            };

            bool isLastRow = r == rows.Length - 1;

            if (isLastRow && !_isSymbolMode)
            {
                var capsLabel = _isShifted ? "⯪ ON" : "⯪";
                var capsBg    = _isShifted ? "#4A6A2A" : "#5D4037";
                rowPanel.Children.Add(CreateFuncButton(capsLabel, capsBg, 72, OnShiftClick));
            }

            foreach (var key in rows[r])
            {
                var display = (!_isSymbolMode && _isShifted) ? key.ToUpper() : key;
                var btn = new Button
                {
                    Content = display,
                    Style   = (Style)FindResource("CharKey"),
                    Tag     = key
                };
                btn.Click += OnCharClick;
                rowPanel.Children.Add(btn);
            }

            if (isLastRow)
            {
                rowPanel.Children.Add(CreateFuncButton("⌫", "#5D4037", 72, OnBackspaceClick));
            }

            KeyboardRows.Children.Add(rowPanel);
        }

        var bottomRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };

        if (_isSymbolMode)
            bottomRow.Children.Add(CreateFuncButton("ABC", "#5D4037", 80, OnAbcClick));
        else
            bottomRow.Children.Add(CreateFuncButton("!@#", "#5D4037", 80, OnSymbolClick));

        var spaceBtn = CreateFuncButton(
            LocalizationService.Instance["TouchKeyboard.Space"],
            "#3A5278", 240, OnSpaceClick);
        spaceBtn.FontSize = 16;
        bottomRow.Children.Add(spaceBtn);

        bottomRow.Children.Add(CreateFuncButton(
            LocalizationService.Instance["TouchKeyboard.Confirm"],
            "#2E7D32", 100, OnConfirmClick));

        bottomRow.Children.Add(CreateFuncButton(
            LocalizationService.Instance["TouchKeyboard.Clear"],
            "#B71C1C", 80, OnClearClick));

        KeyboardRows.Children.Add(bottomRow);
    }

    private Button CreateFuncButton(string text, string bgColor, double width, RoutedEventHandler handler)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(bgColor));
        var btn = new Button
        {
            Content  = text,
            Style    = (Style)FindResource("FuncKey"),
            Tag      = brush,
            MinWidth = width
        };
        btn.Click += handler;
        return btn;
    }

    // ═══════════════════════════════════════
    // 事件處理
    // ═══════════════════════════════════════

    private void OnCharClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string key)
        {
            var ch = (!_isSymbolMode && _isShifted) ? key.ToUpper() : key;
            _inputText += ch;
            UpdateDisplay();
        }
        RestoreFocus();
    }

    private void OnShiftClick(object sender, RoutedEventArgs e)
    {
        _isShifted = !_isShifted;
        BuildKeyboard();

        // 連動實體 CapsLock：若實體狀態與虛擬不同，則模擬按鍵將實體同步
        if (Keyboard.IsKeyToggled(Key.CapsLock) != _isShifted)
        {
            _virtualCapsLockToggling = true; // 標記：下個 CapsLock 事件是我們自己觸發的
            SimulateCapsLockToggle();
        }

        RestoreFocus();
    }

    private void OnSymbolClick(object sender, RoutedEventArgs e)
    {
        _isSymbolMode = true;
        _isShifted    = false;
        BuildKeyboard();
        RestoreFocus();
    }

    private void OnAbcClick(object sender, RoutedEventArgs e)
    {
        _isSymbolMode = false;
        BuildKeyboard();
        RestoreFocus();
    }

    private void OnBackspaceClick(object sender, RoutedEventArgs e)
    {
        if (_inputText.Length > 0)
        {
            _inputText = _inputText[..^1];
            UpdateDisplay();
        }
        RestoreFocus();
    }

    private void OnSpaceClick(object sender, RoutedEventArgs e)
    {
        _inputText += " ";
        UpdateDisplay();
        RestoreFocus();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var text     = _inputText;
        var callback = _onConfirm;
        Hide();
        callback?.Invoke(text);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _inputText = "";
        UpdateDisplay();
        RestoreFocus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var cancelCallback = _onCancel;
        Hide();
        cancelCallback?.Invoke();
    }

    private void EyeToggle_Click(object sender, RoutedEventArgs e)
    {
        _showPlainText = !_showPlainText;
        EyeToggle.Content = _showPlainText ? "🙈" : "👁";
        UpdateDisplay();
        RestoreFocus();
    }

    /// <summary>所有虛擬按鈕操作後統一恢復 FocusCatcher 鍵盤焦點</summary>
    private void RestoreFocus()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (Visibility == Visibility.Visible && !FocusCatcher.IsKeyboardFocused)
            {
                FocusCatcher.Focus();
                Keyboard.Focus(FocusCatcher);
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    // ═══════════════════════════════════════
    // 顯示更新
    // ═══════════════════════════════════════

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(_inputText))
        {
            InputDisplay.Text = "";
            return;
        }

        const int maxVisible = 24;

        if (_showPlainText)
        {
            InputDisplay.Text = _inputText.Length > maxVisible
                ? "…" + _inputText[^maxVisible..]
                : _inputText;
        }
        else
        {
            // 密碼遮罩
            InputDisplay.Text = _inputText.Length > maxVisible
                ? "…" + new string('●', maxVisible)
                : new string('●', _inputText.Length);
        }
    }

    // ═══════════════════════════════════════
    // 實體鍵盤輸入（透過隱藏 FocusCatcher 接收）
    // ═══════════════════════════════════════

    private void FocusCatcher_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Tab:
                e.Handled = true;
                break;
            case Key.Back:
                OnBackspaceClick(this, e);
                e.Handled = true;
                break;
            case Key.Enter:
                OnConfirmClick(this, e);
                e.Handled = true;
                break;
            case Key.Escape:
                CancelButton_Click(this, e);
                e.Handled = true;
                break;
            case Key.Space:
                OnSpaceClick(this, e);
                e.Handled = true;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                _physicalShiftHeld = true;
                _shiftUsedAsCombo  = false;
                e.Handled = true;
                break;
            case Key.CapsLock:
                if (_virtualCapsLockToggling)
                {
                    _virtualCapsLockToggling = false;
                    e.Handled = true;
                    break;
                }
                _isShifted = !_isShifted;
                Dispatcher.BeginInvoke(() =>
                {
                    BuildKeyboard();
                    if (Visibility == Visibility.Visible)
                    {
                        FocusCatcher.Focus();
                        Keyboard.Focus(FocusCatcher);
                    }
                }, System.Windows.Threading.DispatcherPriority.Input);
                e.Handled = true;
                break;
            default:
                if (_physicalShiftHeld)
                    _shiftUsedAsCombo = true;
                break;
        }
    }

    /// <summary>
    /// 實體 Shift 鍵放開：
    ///   Shift 鍵僅負責臨時大寫（配合 PreviewTextInput 處理），
    ///   不切換虛擬 CapsLock（左下角 ⯪ 按鈕不受 Shift 影響）。
    ///   將確保放開後焦點回到 FocusCatcher。
    /// </summary>
    private void FocusCatcher_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
        {
            _physicalShiftHeld = false;
            e.Handled = true;
            // 確保 Shift 放開後焦點回到 FocusCatcher
            RestoreFocus();
        }
    }

    private void FocusCatcher_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 符號模式下阻擋實體鍵盤字元輸入（僅允許觸控鍵盤點擊）
        if (_isSymbolMode)
        {
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrEmpty(e.Text))
        {
            string ch = e.Text;

            // 套用虛擬 CapsLock 邏輯（僅對英文字母）
            if (_isShifted && ch.Length == 1 && char.IsLetter(ch[0]))
            {
                bool physicalShiftHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                // 虛擬 CapsLock ON + 實體 Shift 按住 = 小寫（抵消，同實體 CapsLock 行為）
                // 虛擬 CapsLock ON + 無實體 Shift   = 大寫
                ch = physicalShiftHeld ? ch.ToLower() : ch.ToUpper();
            }

            _inputText += ch;
            UpdateDisplay();
        }
        e.Handled = true; // 阻止實際寫入 FocusCatcher
    }
}