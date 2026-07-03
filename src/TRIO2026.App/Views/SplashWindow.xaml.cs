using System.Windows;
using System.Windows.Media.Animation;

namespace TRIO2026.App.Views;

/// <summary>
/// 啟動畫面 — 在應用程式初始化期間顯示 Logo + 載入動畫
/// 避免使用者在 DB 初始化、設定載入等耗時操作時面對黑畫面
///
/// 使用方式：
///   var splash = new SplashWindow();
///   splash.Show();
///   splash.UpdateStatus("正在初始化資料庫...");
///   // ... 初始化 ...
///   splash.FadeOutAndClose();
///
/// 製作者: Office of William
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 啟動旋轉動畫
        if (TryFindResource("SpinnerAnimation") is Storyboard spinner)
            spinner.Begin(this);

        // 啟動文字脈搏動畫
        if (TryFindResource("PulseAnimation") is Storyboard pulse)
            pulse.Begin(this);
    }

    /// <summary>更新載入狀態文字</summary>
    public void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
        });
    }

    /// <summary>淡出並關閉</summary>
    public void FadeOutAndClose(Action? onClosed = null)
    {
        Dispatcher.Invoke(() =>
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fadeOut.Completed += (s, e) =>
            {
                Close();
                onClosed?.Invoke();
            };
            BeginAnimation(OpacityProperty, fadeOut);
        });
    }
}
