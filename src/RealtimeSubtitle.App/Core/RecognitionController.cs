using System.Windows;
using RealtimeSubtitle.App.Audio;
using RealtimeSubtitle.App.Ipc;
using RealtimeSubtitle.App.UI;

namespace RealtimeSubtitle.App.Core;

public sealed class RecognitionController : IAsyncDisposable
{
    private readonly ConfigService _configService;
    private readonly SubtitleWindow _subtitleWindow;
    private readonly DeviceLoopbackCapture _capture = new();
    private PythonWorkerClient? _worker;
    private TrayController? _tray;
    private AppConfig _config;
    private bool _isRunning;

    public RecognitionController(ConfigService configService, AppConfig config, SubtitleWindow subtitleWindow)
    {
        _configService = configService;
        _config = config;
        _subtitleWindow = subtitleWindow;
        _capture.AudioAvailable += OnAudioAvailable;
    }

    public void Initialize()
    {
        _tray = new TrayController(StartAsync, StopAsync, ShowSubtitle, HideSubtitle, ShowSettings, ExitAsync);
        _tray.SetRunning(false);

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
            _subtitleWindow.ApplyConfig(_config.Subtitle);
            _subtitleWindow.ShowStatus("正在启动识别...");

            _worker = new PythonWorkerClient(_configService.ResolvePath(_config.Worker.ScriptPath), _config.Worker.PythonPath);
            _worker.SubtitleReceived += OnSubtitleReceived;
            _worker.StatusReceived += status => Application.Current.Dispatcher.Invoke(() => _subtitleWindow.ShowStatus(status));
            _worker.ErrorReceived += error => Application.Current.Dispatcher.Invoke(() => _subtitleWindow.ShowStatus(error));

            await _worker.StartAsync(_config);
            _capture.Start();
            _isRunning = true;
            _tray?.SetRunning(true);
            _subtitleWindow.ShowStatus("正在监听系统声音");
        }
        catch (Exception exc)
        {
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

        _capture.Stop();
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
        var window = new SettingsWindow(_configService.Load());
        if (window.ShowDialog() == true)
        {
            _configService.Save(window.Config);
            _config = window.Config;
            _subtitleWindow.ApplyConfig(_config.Subtitle);
        }
    }

    private async Task ExitAsync()
    {
        await DisposeAsync();
        Application.Current.Shutdown();
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
        Application.Current.Dispatcher.Invoke(() => _subtitleWindow.UpdateSubtitle(subtitle.Japanese, subtitle.Chinese));
    }

    public async ValueTask DisposeAsync()
    {
        _capture.AudioAvailable -= OnAudioAvailable;
        _capture.Dispose();
        _tray?.Dispose();
        if (_worker is not null)
        {
            await _worker.DisposeAsync();
        }
    }
}
