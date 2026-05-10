using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        Container.CornerRadius = new CornerRadius(Math.Max(0, options.CornerRadius));
        Container.Padding = new Thickness(Math.Max(0, options.Padding));
        if (Container.Child is Grid grid && grid.RowDefinitions.Count > 1)
        {
            grid.RowDefinitions[1].Height = new GridLength(Math.Max(0, options.LineSpacing));
        }

        var fontFamily = string.IsNullOrWhiteSpace(options.FontFamily) ? "Microsoft YaHei UI" : options.FontFamily;
        JapaneseText.FontFamily = new System.Windows.Media.FontFamily(fontFamily);
        ChineseText.FontFamily = new System.Windows.Media.FontFamily(fontFamily);
        JapaneseText.FontSize = options.FontSizeJa;
        ChineseText.FontSize = options.FontSizeZh;
        JapaneseText.Foreground = new SolidColorBrush(TryParseColor(Coalesce(options.FontColorJa, options.FontColor, "#FFFFFF")));
        ChineseText.Foreground = new SolidColorBrush(TryParseColor(Coalesce(options.FontColorZh, options.FontColor, "#FFFFFF")));
        JapaneseText.TextAlignment = ParseTextAlignment(options.TextAlignment);
        ChineseText.TextAlignment = ParseTextAlignment(options.TextAlignment);
        ApplyShadow(options);

        if (options.BackgroundEnabled)
        {
            var opacity = Math.Clamp(options.BackgroundOpacity, 0, 1);
            var background = TryParseColor(options.BackgroundColor);
            Container.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                (byte)(opacity * 255),
                background.R,
                background.G,
                background.B));
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
        if (!string.IsNullOrWhiteSpace(chinese))
        {
            ChineseText.Text = chinese;
        }

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

    private void ApplyShadow(SubtitleOptions options)
    {
        var opacity = Math.Clamp(options.ShadowOpacity, 0, 1);
        var blurRadius = Math.Max(0, options.ShadowBlurRadius);
        Effect? effect = opacity <= 0 || blurRadius <= 0
            ? null
            : new DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = blurRadius,
                ShadowDepth = 1,
                Opacity = opacity
            };

        JapaneseText.Effect = effect;
        ChineseText.Effect = effect is null ? null : effect.Clone();
    }

    private static TextAlignment ParseTextAlignment(string value)
    {
        return Enum.TryParse<TextAlignment>(value, ignoreCase: true, out var alignment)
            ? alignment
            : TextAlignment.Center;
    }

    private static string Coalesce(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "#FFFFFF";
    }
}
