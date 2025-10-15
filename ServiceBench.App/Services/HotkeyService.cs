using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace ServiceBench.App.Services;

public class HotkeyService
{
    private const int HotkeyId = 0x0001;
    private const int WmHotkey = 0x0312;
    private Window? _window;

    public event EventHandler? EscPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Register(Window window)
    {
        _window = window;
        var helper = new System.Windows.Interop.WindowInteropHelper(window);
        var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        if (source != null)
        {
            source.AddHook(HwndHook);
            RegisterHotKey(helper.Handle, HotkeyId, 0, 0x1B);
        }
    }

    public void Unregister()
    {
        if (_window == null)
        {
            return;
        }

        var helper = new System.Windows.Interop.WindowInteropHelper(_window);
        UnregisterHotKey(helper.Handle, HotkeyId);
        var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
        source?.RemoveHook(HwndHook);
        _window = null;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            EscPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
