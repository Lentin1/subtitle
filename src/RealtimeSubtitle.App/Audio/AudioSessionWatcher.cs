using System.Diagnostics;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace RealtimeSubtitle.App.Audio;

public sealed class AudioSessionWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private readonly MMDeviceEnumerator _enumerator = new();
    private IReadOnlyList<AudioAppInfo> _apps = Array.Empty<AudioAppInfo>();

    public event Action<IReadOnlyList<AudioAppInfo>>? AppsChanged;

    public IReadOnlyList<AudioAppInfo> Apps => _apps;

    public AudioSessionWatcher()
    {
        _timer.Interval = TimeSpan.FromSeconds(2);
        _timer.Tick += (_, _) => Refresh();
    }

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public bool IsAppAudible(string targetApp)
    {
        if (string.IsNullOrWhiteSpace(targetApp))
        {
            return true;
        }

        Refresh();
        return _apps.Any(app =>
            app.IsActive &&
            (string.Equals(app.ProcessName, targetApp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(app.DisplayName, targetApp, StringComparison.OrdinalIgnoreCase)));
    }

    public void Refresh()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            var apps = new List<AudioAppInfo>();
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var processId = (int)session.GetProcessID;
                if (processId <= 0)
                {
                    continue;
                }

                var processName = GetProcessName(processId);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(session.DisplayName) ? processName : session.DisplayName;
                var isActive = session.AudioMeterInformation.MasterPeakValue > 0.001f;
                apps.Add(new AudioAppInfo(processId, processName, displayName, isActive));
            }

            _apps = apps
                .GroupBy(app => app.ProcessId)
                .Select(group => group.OrderByDescending(app => app.IsActive).ThenBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase).First())
                .OrderByDescending(app => app.IsActive)
                .ThenBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            AppsChanged?.Invoke(_apps);
        }
        catch
        {
            _apps = Array.Empty<AudioAppInfo>();
            AppsChanged?.Invoke(_apps);
        }
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return "";
        }
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
