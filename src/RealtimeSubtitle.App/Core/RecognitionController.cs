using RealtimeSubtitle.App.Audio;
using RealtimeSubtitle.App.Ipc;
using RealtimeSubtitle.App.UI;
using WpfApplication = System.Windows.Application;

namespace RealtimeSubtitle.App.Core;

public sealed class RecognitionController : IAsyncDisposable
{
    private readonly ConfigService _configService;
    private readonly SubtitleWindow _subtitleWindow;
    private readonly AudioSessionWatcher _sessionWatcher = new();
    private readonly HotkeyService _hotkey;
    private readonly AppLogger _logger;
    private IAudioCapture? _capture;
    private PythonWorkerClient? _worker;
    private TrayController? _tray;
    private AppConfig _config;
    private bool _isRunning;

    public RecognitionController(ConfigService configService, AppConfig config, SubtitleWindow subtitleWindow)
    {
        _configService = configService;
        _config = config;
        _subtitleWindow = subtitleWindow;
        _hotkey = new HotkeyService(() => _ = ToggleAsync());
        _logger = new AppLogger(configService.RepositoryRoot) { Enabled = config.App.DebugLog };
    }

    public void Initialize()
    {
        _tray = new TrayController(StartAsync, StopAsync, ShowSubtitle, HideSubtitle, ShowSettings, ExitAsync);
        _tray.SetRunning(false);
        _sessionWatcher.Start();
        RegisterHotkey();

        if (_config.App.AutoStart)
        {
            _ = StartAsync();
        }
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _config = _configService.Load();
            _logger.Enabled = _config.App.DebugLog;
            _logger.Info("Starting recognition");
            _subtitleWindow.ApplyConfig(_config.Subtitle);
            _subtitleWindow.ShowStatus("正在启动识别...");

            _worker = new PythonWorkerClient(
                _configService.ResolveOptionalPath(_config.Worker.ScriptPath),
                _configService.ResolveOptionalPath(_config.Worker.PythonPath),
                _configService.ResolveOptionalPath(_config.Worker.ExecutablePath),
                _configService.RepositoryRoot,
                _logger);
            _worker.SubtitleReceived += OnSubtitleReceived;
            _worker.StatusReceived += status => WpfApplication.Current.Dispatcher.Invoke(() => _subtitleWindow.ShowStatus(status));
            _worker.ErrorReceived += error => WpfApplication.Current.Dispatcher.Invoke(() => _subtitleWindow.ShowStatus(error));

            await _worker.StartAsync(_config);
            _capture = CreateCapture();
            _capture.AudioAvailable += OnAudioAvailable;
            await Task.Run(() => _capture.Start());
            _isRunning = true;
            _tray?.SetRunning(true);
            _subtitleWindow.ShowStatus(GetListeningStatus());
        }
        catch (Exception exc)
        {
            _logger.Error("Failed to start recognition", exc);
            _subtitleWindow.ShowStatus($"启动失败: {exc.Message}");
            if (_worker is not null)
            {
                await _worker.DisposeAsync();
                _worker = null;
            }
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.Info("Stopping recognition");
        if (_capture is not null)
        {
            _capture.AudioAvailable -= OnAudioAvailable;
            _capture.Stop();
            _capture.Dispose();
            _capture = null;
        }
        if (_worker is not null)
        {
            await _worker.SendControlAsync("stop");
            await _worker.DisposeAsync();
            _worker = null;
        }

        _isRunning = false;
        _tray?.SetRunning(false);
        _subtitleWindow.ShowStatus("已暂停");
    }

    private void ShowSubtitle()
    {
        _subtitleWindow.Show();
        _subtitleWindow.Activate();
    }

    private void HideSubtitle()
    {
        _subtitleWindow.Hide();
    }

    private void ShowSettings()
    {
        _sessionWatcher.Refresh();
        var window = new SettingsWindow(_configService.Load(), _sessionWatcher.Apps, _configService, _logger);
        if (window.ShowDialog() == true)
        {
            _configService.Save(window.Config);
            _config = window.Config;
            _logger.Enabled = _config.App.DebugLog;
            _subtitleWindow.ApplyConfig(_config.Subtitle);
            RegisterHotkey();
        }
    }

    private async Task ExitAsync()
    {
        await DisposeAsync();
        WpfApplication.Current.Shutdown();
    }

    private void OnAudioAvailable(object? sender, AudioChunk chunk)
    {
        var worker = _worker;
        if (!_isRunning || worker is null)
        {
            return;
        }

        _ = worker.SendAudioAsync(chunk);
    }

    private void OnSubtitleReceived(SubtitleEvent subtitle)
    {
        WpfApplication.Current.Dispatcher.Invoke(() => _subtitleWindow.UpdateSubtitle(subtitle.Japanese, subtitle.Chinese));
    }

    private IAudioCapture CreateCapture()
    {
        if (string.Equals(_config.Audio.Mode, "process_loopback", StringComparison.OrdinalIgnoreCase))
        {
            if (_config.Audio.TargetProcessId <= 0)
            {
                _logger.Info("Process loopback selected without a target PID");
                throw new InvalidOperationException("请先在设置里选择目标进程。");
            }

            _logger.Info($"Using process loopback for PID {_config.Audio.TargetProcessId}");
            return new ProcessLoopbackCapture(_config.Audio.TargetProcessId);
        }

        _logger.Info("Using default device loopback");
        return new DeviceLoopbackCapture();
    }

    private string GetListeningStatus()
    {
        if (string.Equals(_config.Audio.Mode, "process_loopback", StringComparison.OrdinalIgnoreCase) &&
            _config.Audio.TargetProcessId > 0)
        {
            var app = _sessionWatcher.Apps.FirstOrDefault(item => item.ProcessId == _config.Audio.TargetProcessId);
            if (app is not null)
            {
                return $"正在监听进程声音: {app.DisplayName} (PID {app.ProcessId})";
            }

            return $"正在监听进程声音: PID {_config.Audio.TargetProcessId}";
        }

        return "正在监听系统声音";
    }

    private async Task ToggleAsync()
    {
        if (_isRunning)
        {
            await StopAsync();
        }
        else
        {
            await StartAsync();
        }
    }

    private void RegisterHotkey()
    {
        if (_config.Hotkey.Enabled)
        {
            _hotkey.Register(_config.Hotkey.ToggleRecognition);
        }
        else
        {
            _hotkey.Unregister();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_capture is not null)
        {
            _capture.AudioAvailable -= OnAudioAvailable;
            _capture.Dispose();
        }

        _hotkey.Dispose();
        _sessionWatcher.Dispose();
        _tray?.Dispose();
        if (_worker is not null)
        {
            await _worker.DisposeAsync();
        }
    }
}
