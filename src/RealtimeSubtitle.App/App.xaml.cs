using System.Windows;
using RealtimeSubtitle.App.Core;
using RealtimeSubtitle.App.UI;

namespace RealtimeSubtitle.App;

public partial class App : Application
{
    private RecognitionController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var configService = new ConfigService();
        var config = configService.Load();
        var subtitleWindow = new SubtitleWindow();
        subtitleWindow.ApplyConfig(config.Subtitle);
        subtitleWindow.ShowIdle();

        _controller = new RecognitionController(configService, config, subtitleWindow);
        _controller.Initialize();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }

        base.OnExit(e);
    }
}
