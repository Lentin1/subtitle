using System.Windows;
using RealtimeSubtitle.App.Audio;
using RealtimeSubtitle.App.Core;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace RealtimeSubtitle.App.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppLogger _logger;
    public AppConfig Config { get; }

    public SettingsWindow(AppConfig config, IReadOnlyList<AudioAppInfo> apps, ConfigService configService, AppLogger logger)
    {
        InitializeComponent();
        _configService = configService;
        _logger = logger;
        Config = config;
        LoadAudioApps(apps);
        LoadConfig(config);
    }

    private void LoadAudioApps(IReadOnlyList<AudioAppInfo> apps)
    {
        TargetAppBox.Items.Clear();
        foreach (var app in apps)
        {
            var label = app.IsActive
                ? $"{app.DisplayName} ({app.ProcessName}, PID {app.ProcessId}, 活动)"
                : $"{app.DisplayName} ({app.ProcessName}, PID {app.ProcessId})";
            TargetAppBox.Items.Add(new WpfComboBoxItem { Content = label, Tag = app.ProcessId });
        }

        if (Config.Audio.TargetProcessId > 0)
        {
            SelectTag(TargetAppBox, Config.Audio.TargetProcessId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Config.Audio.TargetApp))
        {
            var match = apps.FirstOrDefault(app =>
                string.Equals(app.ProcessName, Config.Audio.TargetApp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(app.DisplayName, Config.Audio.TargetApp, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                SelectTag(TargetAppBox, match.ProcessId);
            }
        }
    }

    private void LoadConfig(AppConfig config)
    {
        SelectTag(AudioModeBox, config.Audio.Mode);
        TargetAppBox.Text = config.Audio.TargetApp;
        if (config.Audio.TargetProcessId > 0)
        {
            SelectTag(TargetAppBox, config.Audio.TargetProcessId);
        }

        ModelText.Text = config.Asr.Model;
        ModelCacheText.Text = config.Asr.ModelCacheDir;
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
        FontColorText.Text = string.IsNullOrWhiteSpace(config.Subtitle.FontColorJa) ? config.Subtitle.FontColor : config.Subtitle.FontColorJa;
        ZhFontColorText.Text = string.IsNullOrWhiteSpace(config.Subtitle.FontColorZh) ? config.Subtitle.FontColor : config.Subtitle.FontColorZh;
        BackgroundColorText.Text = config.Subtitle.BackgroundColor;
        FontFamilyText.Text = config.Subtitle.FontFamily;
        CornerRadiusText.Text = config.Subtitle.CornerRadius.ToString("0");
        PaddingText.Text = config.Subtitle.Padding.ToString("0");
        LineSpacingText.Text = config.Subtitle.LineSpacing.ToString("0");
        ShadowBlurText.Text = config.Subtitle.ShadowBlurRadius.ToString("0");
        ShadowOpacityText.Text = config.Subtitle.ShadowOpacity.ToString("0.##");
        SelectContent(TextAlignmentBox, config.Subtitle.TextAlignment);
        BackgroundCheck.IsChecked = config.Subtitle.BackgroundEnabled;

        HotkeyCheck.IsChecked = config.Hotkey.Enabled;
        HotkeyText.Text = config.Hotkey.ToggleRecognition;
        DebugLogCheck.IsChecked = config.App.DebugLog;
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        Config.Audio.Mode = ReadSelectedTag(AudioModeBox, "device_loopback");
        Config.Audio.TargetProcessId = ReadTargetProcessId();
        Config.Audio.TargetApp = ReadTargetApp();

        Config.Asr.Model = ModelText.Text.Trim();
        Config.Asr.ModelCacheDir = string.IsNullOrWhiteSpace(ModelCacheText.Text) ? "models" : ModelCacheText.Text.Trim();
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
        Config.Subtitle.FontColorJa = string.IsNullOrWhiteSpace(FontColorText.Text) ? "#FFFFFF" : FontColorText.Text.Trim();
        Config.Subtitle.FontColorZh = string.IsNullOrWhiteSpace(ZhFontColorText.Text) ? Config.Subtitle.FontColorJa : ZhFontColorText.Text.Trim();
        Config.Subtitle.FontColor = Config.Subtitle.FontColorJa;
        Config.Subtitle.BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColorText.Text) ? "#000000" : BackgroundColorText.Text.Trim();
        Config.Subtitle.FontFamily = string.IsNullOrWhiteSpace(FontFamilyText.Text) ? "Microsoft YaHei UI" : FontFamilyText.Text.Trim();
        Config.Subtitle.CornerRadius = ReadDouble(CornerRadiusText.Text, Config.Subtitle.CornerRadius);
        Config.Subtitle.Padding = ReadDouble(PaddingText.Text, Config.Subtitle.Padding);
        Config.Subtitle.LineSpacing = ReadDouble(LineSpacingText.Text, Config.Subtitle.LineSpacing);
        Config.Subtitle.ShadowBlurRadius = ReadDouble(ShadowBlurText.Text, Config.Subtitle.ShadowBlurRadius);
        Config.Subtitle.ShadowOpacity = ReadDouble(ShadowOpacityText.Text, Config.Subtitle.ShadowOpacity);
        Config.Subtitle.TextAlignment = ReadSelectedContent(TextAlignmentBox, Config.Subtitle.TextAlignment);
        Config.Subtitle.BackgroundEnabled = BackgroundCheck.IsChecked == true;

        Config.Hotkey.Enabled = HotkeyCheck.IsChecked == true;
        Config.Hotkey.ToggleRecognition = string.IsNullOrWhiteSpace(HotkeyText.Text) ? "F8" : HotkeyText.Text.Trim();
        Config.App.DebugLog = DebugLogCheck.IsChecked == true;
        DialogResult = true;
    }

    private async void DownloadModel_OnClick(object sender, RoutedEventArgs e)
    {
        SaveCurrentFields();
        DownloadModelButton.IsEnabled = false;
        DownloadModelButton.Content = "下载中...";
        try
        {
            var service = new ModelManagerService(_configService, _logger);
            var message = await service.DownloadOrVerifyAsync(Config);
            System.Windows.MessageBox.Show(this, message, "模型", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exc)
        {
            System.Windows.MessageBox.Show(this, exc.Message, "模型下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DownloadModelButton.Content = "下载/验证模型";
            DownloadModelButton.IsEnabled = true;
        }
    }

    private void SaveCurrentFields()
    {
        Config.Audio.Mode = ReadSelectedTag(AudioModeBox, "device_loopback");
        Config.Audio.TargetProcessId = ReadTargetProcessId();
        Config.Audio.TargetApp = ReadTargetApp();
        Config.Asr.Model = ModelText.Text.Trim();
        Config.Asr.ModelCacheDir = string.IsNullOrWhiteSpace(ModelCacheText.Text) ? "models" : ModelCacheText.Text.Trim();
        Config.Asr.Device = ReadSelectedContent(DeviceBox, Config.Asr.Device);
        Config.Asr.ComputeType = ComputeTypeText.Text.Trim();
        Config.App.DebugLog = DebugLogCheck.IsChecked == true;
    }

    private string ReadTargetApp()
    {
        if (TargetAppBox.SelectedItem is WpfComboBoxItem item)
        {
            return item.Content?.ToString() ?? "";
        }

        return TargetAppBox.Text.Trim();
    }

    private int ReadTargetProcessId()
    {
        if (TargetAppBox.SelectedItem is WpfComboBoxItem item)
        {
            if (item.Tag is int intTag)
            {
                return intTag;
            }

            if (item.Tag is string stringTag && int.TryParse(stringTag, out var parsedTag))
            {
                return parsedTag;
            }
        }

        return int.TryParse(TargetAppBox.Text.Trim(), out var value) ? value : 0;
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

    private static void SelectTag(WpfComboBox comboBox, int tag)
    {
        foreach (var item in comboBox.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int intTag && intTag == tag)
            {
                comboBox.SelectedItem = item;
                return;
            }

            if (int.TryParse(item.Tag?.ToString(), out var parsed) && parsed == tag)
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
