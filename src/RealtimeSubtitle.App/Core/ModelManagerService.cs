using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RealtimeSubtitle.App.Core;

public sealed class ModelManagerService
{
    private readonly ConfigService _configService;
    private readonly AppLogger _logger;

    public ModelManagerService(ConfigService configService, AppLogger logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<string> DownloadOrVerifyAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"realtime-subtitle-model-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            tempConfigPath,
            JsonSerializer.Serialize(config, ConfigService.JsonOptions),
            cancellationToken);

        try
        {
            var executablePath = _configService.ResolveOptionalPath(config.Worker.ExecutablePath);
            var useExecutableWorker = !string.IsNullOrWhiteSpace(executablePath);
            var fileName = useExecutableWorker ? executablePath : _configService.ResolveOptionalPath(config.Worker.PythonPath);
            var scriptPath = _configService.ResolveOptionalPath(config.Worker.ScriptPath);
            var arguments = useExecutableWorker
                ? $"--download-model {Quote(tempConfigPath)}"
                : $"{Quote(scriptPath)} --download-model {Quote(tempConfigPath)}";

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = _configService.RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _logger.Info($"Prewarming model: {startInfo.FileName} {startInfo.Arguments}");
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动模型下载进程。");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
            }

            return string.IsNullOrWhiteSpace(stdout) ? "模型已可用" : stdout.Trim();
        }
        finally
        {
            try
            {
                File.Delete(tempConfigPath);
            }
            catch
            {
            }
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
