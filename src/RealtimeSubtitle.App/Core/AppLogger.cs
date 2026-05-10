using System.IO;

namespace RealtimeSubtitle.App.Core;

public sealed class AppLogger
{
    private readonly string _logPath;
    private readonly object _gate = new();

    public AppLogger(string repositoryRoot)
    {
        _logPath = Path.Combine(repositoryRoot, "logs", "app.log");
    }

    public bool Enabled { get; set; }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message}: {exception}");
    }

    private void Write(string level, string message)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(_logPath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
    }
}
