using System.Windows;
using System.Windows.Controls;

namespace TRIO2026.App.Controls;

/// <summary>
/// 動態數字鍵盤覆蓋層 — 僅供 LoginPage 使用
/// 
/// 功能：
///   - 每次開啟時 0-9 數字隨機排列（防窺視）
///   - 密碼以星號（●）顯示，支援眼睛切換明碼
///   - 支援退格（⌫）和清除（C）
///   - 確認後透過 callback 回傳密碼
///   - 觸控環境：大按鈕、無 Hover、不易誤觸
/// 
/// 製作者: Office of William
/// </summary>
public partial class NumericKeypadOverlay : UserControl
{
    private string _inputPassword = "";
    private bool _showPlainText = false;
    private Action<string>? _onConfirm;
    private Action? _onCancel;
    private readonly Random _random = new();

    public NumericKeypadOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 顯示數字鍵盤
    /// </summary>
    /// <param name="onConfirm">確認按鈕 callback，回傳輸入的密碼</param>
    /// <param name="onCancel">取消 callback（可選）</param>
    public void Show(Action<string> onConfirm, Action? onCancel = null)
    {
        _inputPassword = "";
        _showPlainText = false;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        UpdateDisplay();
        ShuffleKeypad();

        Visibility = Visibility.Visible;
    }

    /// <summary>隱藏數字鍵盤</summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        _inputPassword = "";
        _onConfirm = null;
        _onCancel = null;
    }

    /// <summary>鍵盤顯示時攔截 Tab 鍵，防止焦點跳離</summary>
    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Tab)
            e.Handled = true;
    }

    /// <summary>
    /// 隨機排列數字按鈕 0-9 + 退格鍵
    /// </summary>
    private void ShuffleKeypad()
    {
        KeypadGrid.Children.Clear();

        // 0-9 隨機排列
        var digits = Enumerable.Range(0, 10).ToList();
        for (int i = digits.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (digits[i], digits[j]) = (digits[j], digits[i]);
        }

        // 前 9 個數字放到 3x3 格子
        for (int i = 0; i < 9; i++)
        {
            var btn = CreateDigitButton(digits[i].ToString());
            KeypadGrid.Children.Add(btn);
        }

        // 第四行：空白 | 最後一個數字 | 退格
        var placeholder = new Border { Width = 100, Height = 80, Margin = new Thickness(6) };
        KeypadGrid.Children.Add(placeholder);

        var lastBtn = CreateDigitButton(digits[9].ToString());
        KeypadGrid.Children.Add(lastBtn);

        var backspaceBtn = CreateFuncButton("⌫", "#5D4037");
        backspaceBtn.Click += BackspaceButton_Click;
        KeypadGrid.Children.Add(backspaceBtn);
    }

    private Button CreateDigitButton(string digit)
    {
        var btn = new Button
        {
            Content = digit,
            Style = (Style)FindResource("KeypadButton")
        };
        btn.Click += DigitButton_Click;
        return btn;
    }

    private Button CreateFuncButton(string text, string colorHex)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));

        var btn = new Button
        {
            Content = text,
            Style = (Style)FindResource("FuncButton"),
            Tag = brush
        };
        return btn;
    }

    // ═══════════════════════════════════════
    // 按鈕事件
    // ═══════════════════════════════════════

    private void DigitButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string digit)
        {
            _inputPassword += digit;
            UpdateDisplay();
        }
    }

    private void BackspaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_inputPassword.Length > 0)
        {
            _inputPassword = _inputPassword[..^1];
            UpdateDisplay();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _inputPassword = "";
        UpdateDisplay();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var password = _inputPassword;
        var callback = _onConfirm;
        Hide();
        callback?.Invoke(password);
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
    }

    // ═══════════════════════════════════════
    // 顯示更新
    // ═══════════════════════════════════════

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(_inputPassword))
        {
            PasswordDisplay.Text = "";
            return;
        }

        // 密碼欄位可顯示的最大字元數（依欄位寬度估算）
        const int maxVisible = 10;

        if (_showPlainText)
        {
            // 明碼模式：過長時只顯示末尾並加前綴 …
            if (_inputPassword.Length > maxVisible)
                PasswordDisplay.Text = "…" + _inputPassword[^maxVisible..];
            else
                PasswordDisplay.Text = _inputPassword;
        }
        else
        {
            // 遮罩模式：以 ● 表示，過長時同樣截斷
            if (_inputPassword.Length > maxVisible)
                PasswordDisplay.Text = "…" + new string('●', maxVisible);
            else
                PasswordDisplay.Text = new string('●', _inputPassword.Length);
        }
    }
}
