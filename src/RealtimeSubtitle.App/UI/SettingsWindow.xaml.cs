using System.Windows;
using RealtimeSubtitle.App.Core;

namespace RealtimeSubtitle.App.UI;

public partial class SettingsWindow : Window
{
    public AppConfig Config { get; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        Config = config;
        ModelText.Text = config.Asr.Model;
        ApiKeyBox.Password = config.Translate.ApiKey;
        ContextText.Text = config.Translate.ContextSentences.ToString();
        AutoHideText.Text = config.Subtitle.AutoHideSeconds.ToString();
        OpacityText.Text = config.Subtitle.BackgroundOpacity.ToString("0.##");
        JaSizeText.Text = config.Subtitle.FontSizeJa.ToString("0");
        ZhSizeText.Text = config.Subtitle.FontSizeZh.ToString("0");
        FontColorText.Text = config.Subtitle.FontColor;
        DebugLogCheck.IsChecked = config.App.DebugLog;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        Config.Asr.Model = ModelText.Text.Trim();
        Config.Translate.ApiKey = ApiKeyBox.Password.Trim();
        Config.Translate.ContextSentences = ReadInt(ContextText.Text, Config.Translate.ContextSentences);
        Config.Subtitle.AutoHideSeconds = ReadInt(AutoHideText.Text, Config.Subtitle.AutoHideSeconds);
        Config.Subtitle.BackgroundOpacity = ReadDouble(OpacityText.Text, Config.Subtitle.BackgroundOpacity);
        Config.Subtitle.FontSizeJa = ReadDouble(JaSizeText.Text, Config.Subtitle.FontSizeJa);
        Config.Subtitle.FontSizeZh = ReadDouble(ZhSizeText.Text, Config.Subtitle.FontSizeZh);
        Config.Subtitle.FontColor = string.IsNullOrWhiteSpace(FontColorText.Text) ? "#FFFFFF" : FontColorText.Text.Trim();
        Config.App.DebugLog = DebugLogCheck.IsChecked == true;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static int ReadInt(string text, int fallback)
    {
        return int.TryParse(text, out var value) ? value : fallback;
    }

    private static double ReadDouble(string text, double fallback)
    {
        return double.TryParse(text, out var value) ? value : fallback;
    }
}
