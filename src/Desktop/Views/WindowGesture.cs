using System;
using System.Windows;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Windows;

namespace TokenPet.Desktop.Views
{
    internal sealed class WindowGesture
    {
        private readonly DesktopWindows.ScreenPoint origin;
        private readonly DesktopWindows.Rect bounds;
        internal readonly double Dpi;
        internal bool Moved;

        internal WindowGesture(DesktopWindows.ScreenPoint origin, DesktopWindows.Rect bounds, double dpi)
        {
            this.origin = origin;
            this.bounds = bounds;
            Dpi = dpi;
        }

        internal bool HasMoved(DesktopWindows.ScreenPoint point)
        {
            return Moved || Math.Abs(point.X - origin.X) >= 4 * Dpi || Math.Abs(point.Y - origin.Y) >= 4 * Dpi;
        }

        internal void Place(DesktopWindows.ScreenPoint cursor, DesktopWindows.Rect area, Size size, Preferences settings)
        {
            double left = bounds.Left + cursor.X - origin.X;
            double top = bounds.Top + cursor.Y - origin.Y;
            left = Math.Max(area.Left, left);
            top = Math.Max(area.Top, top);
            settings.RightInset = Math.Max(0, (area.Right - left) / Dpi - size.Width);
            settings.BottomInset = Math.Max(0, (area.Bottom - top) / Dpi - size.Height);
        }
    }
}
