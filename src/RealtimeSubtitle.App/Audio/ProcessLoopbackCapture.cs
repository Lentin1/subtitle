using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using NAudio.Wasapi.CoreAudioApi.Interfaces;

namespace RealtimeSubtitle.App.Audio;

public sealed class ProcessLoopbackCapture : IAudioCapture
{
    private const string VirtualAudioDeviceProcessLoopback = "VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK";

    private readonly object _gate = new();
    private readonly uint _targetProcessId;
    private readonly ManualResetEvent _dataEvent = new(false);
    private readonly ManualResetEventSlim _activationEvent = new(false);

    private Thread? _captureThread;
    private AudioClient? _audioClient;
    private AudioCaptureClient? _captureClient;
    private WaveFormat? _waveFormat;
    private ActivationHandler? _activationHandler;
    private IActivateAudioInterfaceAsyncOperation? _activationOperation;
    private IntPtr _activationParamsBlobPtr = IntPtr.Zero;
    private IntPtr _propVariantPtr = IntPtr.Zero;
    private bool _started;

    public event EventHandler<AudioChunk>? AudioAvailable;

    public ProcessLoopbackCapture(int targetProcessId)
    {
        _targetProcessId = targetProcessId > 0 ? (uint)targetProcessId : 0;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            if (_targetProcessId == 0)
            {
                throw new InvalidOperationException("未选择目标进程。");
            }

            _activationHandler = new ActivationHandler(this);
            var activationParams = CreateActivationParams(_targetProcessId);
            _activationParamsBlobPtr = AllocateAndCopy(activationParams);
            _propVariantPtr = AllocatePropVariantBlob(_activationParamsBlobPtr, Marshal.SizeOf<NativeAudioClientActivationParams>());

            try
            {
                var audioClientGuid = typeof(IAudioClient).GUID;
                AudioNativeMethods.ActivateAudioInterfaceAsync(
                    VirtualAudioDeviceProcessLoopback,
                    ref audioClientGuid,
                    _propVariantPtr,
                    _activationHandler,
                    out _activationOperation);

                var activatedClient = _activationHandler.WaitForClient(TimeSpan.FromSeconds(10));
                _audioClient = new AudioClient(activatedClient);
                _waveFormat = ResolveFormat(_audioClient);
                InitializeClient(_audioClient, _waveFormat);
                _captureClient = _audioClient.AudioCaptureClient;
                _audioClient.SetEventHandle(_dataEvent.SafeWaitHandle.DangerousGetHandle());
                _audioClient.Start();

                _captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "ProcessLoopbackCapture"
                };
                _started = true;
                _captureThread.Start();
            }
            catch
            {
                CleanupStartFailure();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                FreeNative();
                return;
            }

            _started = false;
            _dataEvent.Set();
        }

        var thread = _captureThread;
        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
        {
            thread.Join(TimeSpan.FromSeconds(2));
        }

        lock (_gate)
        {
            try
            {
                _audioClient?.Stop();
            }
            catch
            {
            }

            _captureClient?.Dispose();
            _audioClient?.Dispose();
            _captureClient = null;
            _audioClient = null;
            _waveFormat = null;
            _captureThread = null;
            _activationOperation = null;
            _activationHandler = null;
            _dataEvent.Reset();
            _activationEvent.Reset();
            FreeNative();
        }
    }

    private void CaptureLoop()
    {
        while (true)
        {
            if (!_dataEvent.WaitOne(1000))
            {
                if (!_started)
                {
                    return;
                }

                continue;
            }

            if (!_started)
            {
                return;
            }

            var captureClient = _captureClient;
            var waveFormat = _waveFormat;
            if (captureClient is null || waveFormat is null)
            {
                return;
            }

            while (true)
            {
                int packetSize;
                try
                {
                    packetSize = captureClient.GetNextPacketSize();
                }
                catch
                {
                    return;
                }

                if (packetSize <= 0)
                {
                    break;
                }

                IntPtr dataPtr;
                int frames;
                AudioClientBufferFlags bufferFlags;

                try
                {
                    dataPtr = captureClient.GetBuffer(out frames, out bufferFlags);
                }
                catch
                {
                    return;
                }

                var releaseFailed = false;
                try
                {
                    if (frames > 0)
                    {
                        EmitChunk(dataPtr, frames, bufferFlags, waveFormat);
                    }
                }
                finally
                {
                    try
                    {
                        captureClient.ReleaseBuffer(frames);
                    }
                    catch
                    {
                        releaseFailed = true;
                    }
                }

                if (releaseFailed)
                {
                    return;
                }
            }
        }
    }

    private void EmitChunk(IntPtr dataPtr, int frames, AudioClientBufferFlags bufferFlags, WaveFormat waveFormat)
    {
        var bytesToCopy = frames * waveFormat.BlockAlign;
        if (bytesToCopy <= 0)
        {
            return;
        }

        var buffer = new byte[bytesToCopy];
        if (bufferFlags.HasFlag(AudioClientBufferFlags.Silent) || dataPtr == IntPtr.Zero)
        {
            Array.Clear(buffer, 0, buffer.Length);
        }
        else
        {
            Marshal.Copy(dataPtr, buffer, 0, buffer.Length);
        }

        AudioAvailable?.Invoke(this, new AudioChunk(
            buffer,
            waveFormat.SampleRate,
            waveFormat.Channels,
            waveFormat.BitsPerSample));
    }

    private static NativeAudioClientActivationParams CreateActivationParams(uint processId)
    {
        return new NativeAudioClientActivationParams
        {
            ActivationType = NativeAudioClientActivationType.ProcessLoopback,
            ProcessLoopbackParams = new NativeAudioClientProcessLoopbackParams
            {
                TargetProcessId = processId,
                ProcessLoopbackMode = NativeProcessLoopbackMode.IncludeTargetProcessTree
            }
        };
    }

    private static IntPtr AllocateAndCopy<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(value, ptr, false);
        return ptr;
    }

    private static IntPtr AllocatePropVariantBlob(IntPtr blobDataPtr, int blobLength)
    {
        var propVariant = new PropVariant
        {
            vt = (short)VarEnum.VT_BLOB,
            blobVal = new Blob
            {
                Length = blobLength,
                Data = blobDataPtr
            }
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariant>());
        Marshal.StructureToPtr(propVariant, ptr, false);
        return ptr;
    }

    private static WaveFormat ResolveFormat(AudioClient audioClient)
    {
        var candidates = new List<WaveFormat>();

        try
        {
            var mixFormat = audioClient.MixFormat;
            if (mixFormat is not null)
            {
                candidates.Add(mixFormat);
            }
        }
        catch
        {
        }

        candidates.Add(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        candidates.Add(new WaveFormat(48000, 16, 2));
        candidates.Add(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
        candidates.Add(new WaveFormat(44100, 16, 2));

        foreach (var candidate in candidates)
        {
            try
            {
                if (audioClient.IsFormatSupported(AudioClientShareMode.Shared, candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return candidates.First();
    }

    private static void InitializeClient(AudioClient audioClient, WaveFormat waveFormat)
    {
        var bufferDuration = Math.Max(audioClient.DefaultDevicePeriod, TimeSpan.FromMilliseconds(20).Ticks);
        Exception? lastError = null;

        var formats = new[]
        {
            waveFormat,
            WaveFormat.CreateIeeeFloatWaveFormat(48000, 2),
            new WaveFormat(48000, 16, 2),
            WaveFormat.CreateIeeeFloatWaveFormat(44100, 2),
            new WaveFormat(44100, 16, 2)
        };

        foreach (var format in formats.DistinctBy(item => $"{item.SampleRate}:{item.Channels}:{item.BitsPerSample}:{item.Encoding}"))
        {
            try
            {
                audioClient.Initialize(
                    AudioClientShareMode.Shared,
                    AudioClientStreamFlags.EventCallback,
                    bufferDuration,
                    0,
                    format,
                    Guid.Empty);
                return;
            }
            catch (Exception exc)
            {
                lastError = exc;
            }
        }

        throw new InvalidOperationException("无法初始化进程环回音频客户端。", lastError);
    }

    private void FreeNative()
    {
        if (_propVariantPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_propVariantPtr);
            _propVariantPtr = IntPtr.Zero;
        }

        if (_activationParamsBlobPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_activationParamsBlobPtr);
            _activationParamsBlobPtr = IntPtr.Zero;
        }
    }

    private void CleanupStartFailure()
    {
        try
        {
            _audioClient?.Stop();
        }
        catch
        {
        }

        _captureClient?.Dispose();
        _audioClient?.Dispose();
        _captureClient = null;
        _audioClient = null;
        _waveFormat = null;
        _captureThread = null;
        _activationOperation = null;
        _activationHandler = null;
        _started = false;
        _dataEvent.Reset();
        _activationEvent.Reset();
        FreeNative();
    }

    public void Dispose()
    {
        Stop();
        _dataEvent.Dispose();
        _activationEvent.Dispose();
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ActivationHandler : IActivateAudioInterfaceCompletionHandler
    {
        private readonly ProcessLoopbackCapture _owner;
        private readonly TaskCompletionSource<IAudioClient> _clientTask =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ActivationHandler(ProcessLoopbackCapture owner)
        {
            _owner = owner;
        }

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        {
            try
            {
                activateOperation.GetActivateResult(out var activateResult, out var activatedInterface);
                if (activateResult < 0 || activatedInterface is not IAudioClient client)
                {
                    _clientTask.TrySetException(new COMException("进程环回激活失败。", activateResult));
                    return;
                }

                _clientTask.TrySetResult(client);
            }
            catch (Exception exc)
            {
                _clientTask.TrySetException(exc);
            }
            finally
            {
                _owner._activationEvent.Set();
            }
        }

        public IAudioClient WaitForClient(TimeSpan timeout)
        {
            if (!_clientTask.Task.Wait(timeout))
            {
                throw new TimeoutException("等待进程环回激活超时。");
            }

            return _clientTask.Task.GetAwaiter().GetResult();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeAudioClientActivationParams
    {
        public NativeAudioClientActivationType ActivationType;
        public NativeAudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeAudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public NativeProcessLoopbackMode ProcessLoopbackMode;
    }

    private enum NativeAudioClientActivationType : int
    {
        Default = 0,
        ProcessLoopback = 1
    }

    private enum NativeProcessLoopbackMode : int
    {
        IncludeTargetProcessTree = 0,
        ExcludeTargetProcessTree = 1
    }

    private static class AudioNativeMethods
    {
        [DllImport("Mmdevapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int ActivateAudioInterfaceAsync(
            string deviceInterfacePath,
            ref Guid riid,
            IntPtr activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);
    }
}
