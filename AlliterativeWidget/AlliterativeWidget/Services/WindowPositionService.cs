using System.Runtime.InteropServices;

namespace AlliterativeWidget.Services;

public class WindowPositionService : IWindowPositionService
{
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public (int X, int Y) CalculateTopRightPosition(IntPtr hwnd, int windowWidth, int windowHeight, int padding)
    {
        var dpi = GetDpiForWindow(hwnd);
        var scaleFactor = dpi / 96.0;

        var scaledWidth = (int)(windowWidth * scaleFactor);
        var scaledHeight = (int)(windowHeight * scaleFactor);
        var scaledPadding = (int)(padding * scaleFactor);

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (GetMonitorInfo(monitor, ref monitorInfo))
        {
            var workArea = monitorInfo.rcWork;
            var x = workArea.Right - scaledWidth - scaledPadding;
            var y = workArea.Top + scaledPadding;
            return (x, y);
        }

        // Fallback to reasonable defaults
        return (1500, 20);
    }
}
