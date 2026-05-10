using RealtimeSubtitle.App.Core;
using RealtimeSubtitle.App.UI;
using WpfApplication = System.Windows.Application;
using WpfExitEventArgs = System.Windows.ExitEventArgs;
using WpfShutdownMode = System.Windows.ShutdownMode;
using WpfStartupEventArgs = System.Windows.StartupEventArgs;

namespace RealtimeSubtitle.App;

public partial class App : WpfApplication
{
    private RecognitionController? _controller;

    protected override void OnStartup(WpfStartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = WpfShutdownMode.OnExplicitShutdown;

        var configService = new ConfigService();
        var config = configService.Load();
        var subtitleWindow = new SubtitleWindow();
        subtitleWindow.ApplyConfig(config.Subtitle);
        subtitleWindow.ShowIdle();

        _controller = new RecognitionController(configService, config, subtitleWindow);
        _controller.Initialize();
    }

    protected override async void OnExit(WpfExitEventArgs e)
    {
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }

        base.OnExit(e);
    }
}
