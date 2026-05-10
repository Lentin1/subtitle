using System.Drawing;
using Forms = System.Windows.Forms;

namespace RealtimeSubtitle.App.UI;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Func<Task> _start;
    private readonly Func<Task> _stop;
    private bool _running;

    public TrayController(
        Func<Task> start,
        Func<Task> stop,
        Action showSubtitle,
        Action hideSubtitle,
        Action showSettings,
        Func<Task> exit)
    {
        _start = start;
        _stop = stop;
        _toggleItem = new Forms.ToolStripMenuItem("开始识别", null, async (_, _) => await ToggleAsync());

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add("显示字幕", null, (_, _) => showSubtitle());
        menu.Items.Add("隐藏字幕", null, (_, _) => hideSubtitle());
        menu.Items.Add("设置", null, (_, _) => showSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, async (_, _) => await exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Realtime Subtitle",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => showSubtitle();
    }

    public void SetRunning(bool running)
    {
        _running = running;
        _toggleItem.Text = running ? "暂停识别" : "开始识别";
    }

    private async Task ToggleAsync()
    {
        if (_running)
        {
            await _stop();
        }
        else
        {
            await _start();
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
