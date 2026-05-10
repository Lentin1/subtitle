using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RealtimeSubtitle.App.Core;

namespace RealtimeSubtitle.App.UI;

public partial class SubtitleWindow : Window
{
    private readonly DispatcherTimer _hideTimer = new();
    private SubtitleOptions _options = new();

    public SubtitleWindow()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    public void ApplyConfig(SubtitleOptions options)
    {
        _options = options;
        Width = options.Width;
        Height = options.Height;
        JapaneseText.FontSize = options.FontSizeJa;
        ChineseText.FontSize = options.FontSizeZh;
        var color = TryParseColor(options.FontColor);
        JapaneseText.Foreground = new SolidColorBrush(color);
        ChineseText.Foreground = new SolidColorBrush(color);

        if (options.BackgroundEnabled)
        {
            var opacity = Math.Clamp(options.BackgroundOpacity, 0, 1);
            Container.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(opacity * 255), 0, 0, 0));
        }
        else
        {
            Container.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    public void ShowIdle()
    {
        ShowStatus("等待开始");
    }

    public void ShowStatus(string status)
    {
        JapaneseText.Text = status;
        ChineseText.Text = "";
        Show();
    }

    public void UpdateSubtitle(string japanese, string chinese)
    {
        JapaneseText.Text = string.IsNullOrWhiteSpace(japanese) ? JapaneseText.Text : japanese;
        ChineseText.Text = chinese;
        Show();
        ResetAutoHideTimer();
    }

    private void ResetAutoHideTimer()
    {
        _hideTimer.Stop();
        if (_options.AutoHideSeconds > 0)
        {
            _hideTimer.Interval = TimeSpan.FromSeconds(_options.AutoHideSeconds);
            _hideTimer.Start();
        }
    }

    private void Container_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Hide();
            return;
        }

        DragMove();
    }

    private static System.Windows.Media.Color TryParseColor(string value)
    {
        try
        {
            return (System.Windows.Media.Color)(System.Windows.Media.ColorConverter.ConvertFromString(value) ?? System.Windows.Media.Colors.White);
        }
        catch (FormatException)
        {
            return System.Windows.Media.Colors.White;
        }
    }
}
