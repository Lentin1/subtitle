namespace RealtimeSubtitle.App.Audio;

public sealed record AudioChunk(byte[] Buffer, int SampleRate, int Channels, int BitsPerSample);
