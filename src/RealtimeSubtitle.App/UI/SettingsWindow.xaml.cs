using System.Windows;
using RealtimeSubtitle.App.Audio;
using RealtimeSubtitle.App.Core;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace RealtimeSubtitle.App.UI;

public partial class SettingsWindow : Window
{
    public AppConfig Config { get; }

    public SettingsWindow(AppConfig config, IReadOnlyList<AudioAppInfo> apps)
    {
        InitializeComponent();
        Config = config;
        LoadAudioApps(apps);
        LoadConfig(config);
    }

    private void LoadAudioApps(IReadOnlyList<AudioAppInfo> apps)
    {
        TargetAppBox.Items.Clear();
        foreach (var app in apps)
        {
            var label = app.IsActive ? $"{app.DisplayName} ({app.ProcessName}, 活动)" : $"{app.DisplayName} ({app.ProcessName})";
            TargetAppBox.Items.Add(new WpfComboBoxItem { Content = label, Tag = app.ProcessName });
        }
    }

    private void LoadConfig(AppConfig config)
    {
        SelectTag(AudioModeBox, config.Audio.Mode);
        TargetAppBox.Text = config.Audio.TargetApp;

        ModelText.Text = config.Asr.Model;
        SelectContent(DeviceBox, config.Asr.Device);
        ComputeTypeText.Text = config.Asr.ComputeType;
        MaxSegmentText.Text = config.Asr.MaxSegmentSeconds.ToString();
        PartialIntervalText.Text = config.Asr.PartialIntervalSeconds.ToString("0.##");
        StableSilenceText.Text = config.Asr.StableSilenceMs.ToString();

        ApiKeyBox.Password = config.Translate.ApiKey;
        ContextText.Text = config.Translate.ContextSentences.ToString();
        TimeoutText.Text = config.Translate.TimeoutSeconds.ToString();
        PartialTranslateCheck.IsChecked = config.Translate.PartialTranslateEnabled;
        PartialTranslateMinText.Text = config.Translate.PartialTranslateMinChars.ToString();

        AutoHideText.Text = config.Subtitle.AutoHideSeconds.ToString();
        OpacityText.Text = config.Subtitle.BackgroundOpacity.ToString("0.##");
        JaSizeText.Text = config.Subtitle.FontSizeJa.ToString("0");
        ZhSizeText.Text = config.Subtitle.FontSizeZh.ToString("0");
        FontColorText.Text = config.Subtitle.FontColor;
        BackgroundCheck.IsChecked = config.Subtitle.BackgroundEnabled;

        HotkeyCheck.IsChecked = config.Hotkey.Enabled;
        HotkeyText.Text = config.Hotkey.ToggleRecognition;
        DebugLogCheck.IsChecked = config.App.DebugLog;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        Config.Audio.Mode = ReadSelectedTag(AudioModeBox, "device_loopback");
        Config.Audio.TargetApp = ReadTargetApp();

        Config.Asr.Model = ModelText.Text.Trim();
        Config.Asr.Device = ReadSelectedContent(DeviceBox, Config.Asr.Device);
        Config.Asr.ComputeType = ComputeTypeText.Text.Trim();
        Config.Asr.MaxSegmentSeconds = ReadInt(MaxSegmentText.Text, Config.Asr.MaxSegmentSeconds);
        Config.Asr.PartialIntervalSeconds = ReadDouble(PartialIntervalText.Text, Config.Asr.PartialIntervalSeconds);
        Config.Asr.StableSilenceMs = ReadInt(StableSilenceText.Text, Config.Asr.StableSilenceMs);

        Config.Translate.ApiKey = ApiKeyBox.Password.Trim();
        Config.Translate.ContextSentences = ReadInt(ContextText.Text, Config.Translate.ContextSentences);
        Config.Translate.TimeoutSeconds = ReadInt(TimeoutText.Text, Config.Translate.TimeoutSeconds);
        Config.Translate.PartialTranslateEnabled = PartialTranslateCheck.IsChecked == true;
        Config.Translate.PartialTranslateMinChars = ReadInt(PartialTranslateMinText.Text, Config.Translate.PartialTranslateMinChars);

        Config.Subtitle.AutoHideSeconds = ReadInt(AutoHideText.Text, Config.Subtitle.AutoHideSeconds);
        Config.Subtitle.BackgroundOpacity = ReadDouble(OpacityText.Text, Config.Subtitle.BackgroundOpacity);
        Config.Subtitle.FontSizeJa = ReadDouble(JaSizeText.Text, Config.Subtitle.FontSizeJa);
        Config.Subtitle.FontSizeZh = ReadDouble(ZhSizeText.Text, Config.Subtitle.FontSizeZh);
        Config.Subtitle.FontColor = string.IsNullOrWhiteSpace(FontColorText.Text) ? "#FFFFFF" : FontColorText.Text.Trim();
        Config.Subtitle.BackgroundEnabled = BackgroundCheck.IsChecked == true;

        Config.Hotkey.Enabled = HotkeyCheck.IsChecked == true;
        Config.Hotkey.ToggleRecognition = string.IsNullOrWhiteSpace(HotkeyText.Text) ? "F8" : HotkeyText.Text.Trim();
        Config.App.DebugLog = DebugLogCheck.IsChecked == true;
        DialogResult = true;
    }

    private string ReadTargetApp()
    {
        if (TargetAppBox.SelectedItem is WpfComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }

        return TargetAppBox.Text.Trim();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void SelectTag(WpfComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<WpfComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static void SelectContent(WpfComboBox comboBox, string content)
    {
        foreach (var item in comboBox.Items.OfType<WpfComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private static string ReadSelectedTag(WpfComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is WpfComboBoxItem item && item.Tag is string tag ? tag : fallback;
    }

    private static string ReadSelectedContent(WpfComboBox comboBox, string fallback)
    {
        return comboBox.SelectedItem is WpfComboBoxItem item ? item.Content?.ToString() ?? fallback : fallback;
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
