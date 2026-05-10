using NAudio.Wave;

namespace RealtimeSubtitle.App.Audio;

public sealed class DeviceLoopbackCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;

    public event EventHandler<AudioChunk>? AudioAvailable;

    public void Start()
    {
        if (_capture is not null)
        {
            return;
        }

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
    }

    public void Stop()
    {
        var capture = _capture;
        if (capture is null)
        {
            return;
        }

        _capture = null;
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        capture.StopRecording();
        capture.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture || e.BytesRecorded <= 0)
        {
            return;
        }

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        AudioAvailable?.Invoke(this, new AudioChunk(
            buffer,
            capture.WaveFormat.SampleRate,
            capture.WaveFormat.Channels,
            capture.WaveFormat.BitsPerSample));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture)
        {
            return;
        }

        _capture = null;
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        capture.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}
