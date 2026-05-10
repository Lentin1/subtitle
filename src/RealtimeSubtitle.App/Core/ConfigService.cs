using System.IO;
using System.Text.Json;

namespace RealtimeSubtitle.App.Core;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ConfigPath { get; }
    public string RepositoryRoot { get; }

    public ConfigService()
    {
        RepositoryRoot = FindRepositoryRoot();
        ConfigPath = Path.Combine(RepositoryRoot, "config", "config.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var config = new AppConfig();
            Save(config);
            return config;
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(RepositoryRoot, path));
    }

    private static string FindRepositoryRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDirectory, "config", "config.json")) ||
            Directory.Exists(Path.Combine(baseDirectory, "worker")))
        {
            return baseDirectory;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "realtime-subtitle-technical-design.md")) ||
                Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
