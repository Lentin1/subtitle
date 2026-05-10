namespace RealtimeSubtitle.App.Core;

public sealed class AppConfig
{
    public AppOptions App { get; set; } = new();
    public AudioOptions Audio { get; set; } = new();
    public AsrOptions Asr { get; set; } = new();
    public TranslateOptions Translate { get; set; } = new();
    public SubtitleOptions Subtitle { get; set; } = new();
    public HotkeyOptions Hotkey { get; set; } = new();
    public WorkerOptions Worker { get; set; } = new();
}

public sealed class AppOptions
{
    public bool AutoStart { get; set; }
    public bool DebugLog { get; set; }
}

public sealed class AudioOptions
{
    public string Mode { get; set; } = "device_loopback";
    public string TargetApp { get; set; } = "";
    public int TargetProcessId { get; set; }
    public bool AutoRefreshApps { get; set; } = true;
}

public sealed class AsrOptions
{
    public string Mode { get; set; } = "high_accuracy";
    public string Model { get; set; } = "large-v3-turbo";
    public string Language { get; set; } = "ja";
    public string Device { get; set; } = "cuda";
    public string ComputeType { get; set; } = "float16";
    public int StableSilenceMs { get; set; } = 900;
    public int MaxSegmentSeconds { get; set; } = 4;
    public double PartialIntervalSeconds { get; set; } = 0.8;
    public double PartialMinSeconds { get; set; } = 1.2;
    public double PartialMaxSeconds { get; set; } = 2.8;
}

public sealed class TranslateOptions
{
    public string Provider { get; set; } = "deepseek";
    public string Model { get; set; } = "deepseek-chat";
    public string ApiKey { get; set; } = "";
    public int ContextSentences { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 5;
    public bool PartialTranslateEnabled { get; set; } = true;
    public int PartialTranslateMinChars { get; set; } = 8;
}

public sealed class SubtitleOptions
{
    public double FontSizeJa { get; set; } = 30;
    public double FontSizeZh { get; set; } = 32;
    public string FontColor { get; set; } = "#FFFFFF";
    public bool BackgroundEnabled { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.45;
    public double Width { get; set; } = 960;
    public double Height { get; set; } = 170;
    public int AutoHideSeconds { get; set; } = 5;
}

public sealed class WorkerOptions
{
    public string PythonPath { get; set; } = "python";
    public string ScriptPath { get; set; } = "src/RealtimeSubtitle.Worker/main.py";
}

public sealed class HotkeyOptions
{
    public bool Enabled { get; set; } = true;
    public string ToggleRecognition { get; set; } = "F8";
}
