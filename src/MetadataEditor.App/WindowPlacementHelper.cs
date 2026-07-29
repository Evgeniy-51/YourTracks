using System.Windows;
using MetadataEditor.App.Localization;

namespace MetadataEditor.App;

internal static class WindowPlacementHelper
{
    public static WindowGeometrySettings Capture(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        return new WindowGeometrySettings
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = window.WindowState == WindowState.Maximized
        };
    }

    public static void Apply(Window window, WindowGeometrySettings? geometry)
    {
        if (geometry is null ||
            geometry.Width is not double savedWidth ||
            geometry.Height is not double savedHeight ||
            savedWidth <= 0 ||
            savedHeight <= 0)
        {
            return;
        }

        var width = Math.Max(window.MinWidth, savedWidth);
        var height = Math.Max(window.MinHeight, savedHeight);

        var left = geometry.Left ?? double.NaN;
        var top = geometry.Top ?? double.NaN;
        if (double.IsNaN(left) || double.IsNaN(top))
        {
            return;
        }

        var bounds = ClampToVirtualScreen(new Rect(left, top, width, height));
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;

        if (geometry.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static Rect ClampToVirtualScreen(Rect bounds)
    {
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        if (!virtualScreen.IntersectsWith(bounds))
        {
            bounds = new Rect(
                virtualScreen.Left + (virtualScreen.Width - bounds.Width) / 2,
                virtualScreen.Top + (virtualScreen.Height - bounds.Height) / 2,
                bounds.Width,
                bounds.Height);
        }

        var width = Math.Min(bounds.Width, virtualScreen.Width);
        var height = Math.Min(bounds.Height, virtualScreen.Height);
        var left = Math.Max(
            virtualScreen.Left,
            Math.Min(bounds.Left, virtualScreen.Right - width));
        var top = Math.Max(
            virtualScreen.Top,
            Math.Min(bounds.Top, virtualScreen.Bottom - height));

        return new Rect(left, top, width, height);
    }
}
