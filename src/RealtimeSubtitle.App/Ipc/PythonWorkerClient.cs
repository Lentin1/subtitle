using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RealtimeSubtitle.App.Audio;
using RealtimeSubtitle.App.Core;

namespace RealtimeSubtitle.App.Ipc;

public sealed class PythonWorkerClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _scriptPath;
    private readonly string _pythonPath;
    private Process? _process;
    private StreamWriter? _stdin;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event Action<SubtitleEvent>? SubtitleReceived;
    public event Action<string>? StatusReceived;
    public event Action<string>? ErrorReceived;

    public PythonWorkerClient(string scriptPath, string pythonPath)
    {
        _scriptPath = scriptPath;
        _pythonPath = pythonPath;
    }

    public async Task StartAsync(AppConfig config)
    {
        if (!File.Exists(_scriptPath))
        {
            throw new FileNotFoundException("Worker script not found.", _scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = Quote(_scriptPath),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Python worker.");
        _stdin = _process.StandardInput;
        _ = Task.Run(ReadStdoutAsync);
        _ = Task.Run(ReadStderrAsync);

        await SendAsync(new { type = "configure", config });
        await SendControlAsync("start");
    }

    public Task SendControlAsync(string command)
    {
        return SendAsync(new { type = "control", command });
    }

    public Task SendAudioAsync(AudioChunk chunk)
    {
        return SendAsync(new
        {
            type = "audio",
            sampleRate = chunk.SampleRate,
            channels = chunk.Channels,
            bitsPerSample = chunk.BitsPerSample,
            data = Convert.ToBase64String(chunk.Buffer)
        });
    }

    private async Task SendAsync(object message)
    {
        if (_stdin is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(message, JsonOptions);
        await _writeLock.WaitAsync();
        try
        {
            await _stdin.WriteLineAsync(json);
            await _stdin.FlushAsync();
        }
        catch (IOException)
        {
            ErrorReceived?.Invoke("Worker IPC 已断开");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadStdoutAsync()
    {
        if (_process is null)
        {
            return;
        }

        while (!_process.HasExited)
        {
            var line = await _process.StandardOutput.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            HandleWorkerLine(line);
        }
    }

    private async Task ReadStderrAsync()
    {
        if (_process is null)
        {
            return;
        }

        while (!_process.HasExited)
        {
            var line = await _process.StandardError.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                ErrorReceived?.Invoke(line);
            }
        }
    }

    private void HandleWorkerLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type == "subtitle")
            {
                SubtitleReceived?.Invoke(new SubtitleEvent
                {
                    Japanese = root.TryGetProperty("japanese", out var ja) ? ja.GetString() ?? "" : "",
                    Chinese = root.TryGetProperty("chinese", out var zh) ? zh.GetString() ?? "" : ""
                });
            }
            else if (type == "status")
            {
                StatusReceived?.Invoke(root.GetProperty("message").GetString() ?? "");
            }
            else if (type == "error")
            {
                ErrorReceived?.Invoke(root.GetProperty("message").GetString() ?? "");
            }
        }
        catch (JsonException)
        {
            ErrorReceived?.Invoke(line);
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await SendControlAsync("stop");
        }
        catch
        {
            // Process shutdown is best-effort.
        }

        _stdin?.Dispose();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
        }

        _process?.Dispose();
        _writeLock.Dispose();
    }
}
