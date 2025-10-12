using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using DrawingSize = System.Drawing.Size;
using WindowsSize = System.Windows.Size;

namespace ServiceBench.App.Services;

public class ScreenshotService
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public bool SaveElementPng(FrameworkElement? element, string filePath)
    {
        if (element == null)
        {
            return false;
        }

        try
        {
            return element.Dispatcher.Invoke(() =>
            {
                element.Measure(new WindowsSize(element.ActualWidth, element.ActualHeight));
                element.Arrange(new Rect(new WindowsSize(element.ActualWidth, element.ActualHeight)));
                element.UpdateLayout();

                var width = (int)Math.Max(1, Math.Round(element.ActualWidth));
                var height = (int)Math.Max(1, Math.Round(element.ActualHeight));
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(element);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                encoder.Save(stream);
                return true;
            });
        }
        catch
        {
            return false;
        }
    }

    public string CaptureWindow(string windowTitleSubstring, string filePath)
    {
        var handle = FindWindowByTitle(windowTitleSubstring);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Окно с заголовком, содержащим '{windowTitleSubstring}', не найдено");
        }

        if (!GetWindowRect(handle, out var rect))
        {
            throw new InvalidOperationException("Не удалось получить размеры окна");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new DrawingSize(width, height));
        bitmap.Save(filePath, ImageFormat.Png);
        return filePath;
    }

    private static IntPtr FindWindowByTitle(string titleSubstring)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.ToString().Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
