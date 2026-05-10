using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace RealtimeSubtitle.App.Core;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4458;
    private const int WmHotkey = 0x0312;
    private readonly Action _onHotkey;
    private bool _registered;

    public HotkeyService(Action onHotkey)
    {
        _onHotkey = onHotkey;
    }

    public void Register(string keyName)
    {
        Unregister();
        var virtualKey = ParseVirtualKey(keyName);
        if (virtualKey == 0)
        {
            return;
        }

        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
        _registered = RegisterHotKey(IntPtr.Zero, HotkeyId, 0, virtualKey);
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(IntPtr.Zero, HotkeyId);
        ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
        _registered = false;
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WmHotkey || msg.wParam.ToInt32() != HotkeyId)
        {
            return;
        }

        handled = true;
        _onHotkey();
    }

    private static uint ParseVirtualKey(string keyName)
    {
        return keyName.Trim().ToUpperInvariant() switch
        {
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0
        };
    }

    public void Dispose()
    {
        Unregister();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
