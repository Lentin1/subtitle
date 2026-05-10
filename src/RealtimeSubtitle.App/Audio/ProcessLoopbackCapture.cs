namespace RealtimeSubtitle.App.Audio;

public sealed class ProcessLoopbackCapture : IAudioCapture
{
    private readonly DeviceLoopbackCapture _deviceCapture = new();
    private readonly AudioSessionWatcher _sessionWatcher;
    private readonly Func<string> _targetAppProvider;
    private DateTimeOffset _lastAudibleAt = DateTimeOffset.MinValue;

    public event EventHandler<AudioChunk>? AudioAvailable;

    public ProcessLoopbackCapture(AudioSessionWatcher sessionWatcher, Func<string> targetAppProvider)
    {
        _sessionWatcher = sessionWatcher;
        _targetAppProvider = targetAppProvider;
        _deviceCapture.AudioAvailable += OnAudioAvailable;
    }

    public void Start()
    {
        _sessionWatcher.Start();
        _deviceCapture.Start();
    }

    public void Stop()
    {
        _deviceCapture.Stop();
    }

    private void OnAudioAvailable(object? sender, AudioChunk chunk)
    {
        var targetApp = _targetAppProvider();
        if (string.IsNullOrWhiteSpace(targetApp))
        {
            AudioAvailable?.Invoke(this, chunk);
            return;
        }

        if (_sessionWatcher.IsAppAudible(targetApp))
        {
            _lastAudibleAt = DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.UtcNow - _lastAudibleAt < TimeSpan.FromMilliseconds(800))
        {
            AudioAvailable?.Invoke(this, chunk);
        }
    }

    public void Dispose()
    {
        _deviceCapture.AudioAvailable -= OnAudioAvailable;
        _deviceCapture.Dispose();
    }
}
