namespace RealtimeSubtitle.App.Audio;

public interface IAudioCapture : IDisposable
{
    event EventHandler<AudioChunk>? AudioAvailable;

    void Start();

    void Stop();
}
