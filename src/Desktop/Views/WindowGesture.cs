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
        private readonly double initialScale;
        internal readonly double Dpi;
        internal readonly bool Resize;
        internal bool Moved;

        internal WindowGesture(DesktopWindows.ScreenPoint origin, DesktopWindows.Rect bounds,
            double dpi, double scale, bool resize)
        {
            this.origin = origin;
            this.bounds = bounds;
            initialScale = scale;
            Dpi = dpi;
            Resize = resize;
        }

        internal bool HasMoved(DesktopWindows.ScreenPoint point)
        {
            return Moved || Math.Abs(point.X - origin.X) >= 4 * Dpi || Math.Abs(point.Y - origin.Y) >= 4 * Dpi;
        }

        internal double ResizeTo(DesktopWindows.ScreenPoint point)
        {
            double horizontal = (double)(point.X - origin.X) / Math.Max(1, bounds.Right - bounds.Left);
            double vertical = (double)(point.Y - origin.Y) / Math.Max(1, bounds.Bottom - bounds.Top);
            double delta = Math.Abs(horizontal) >= Math.Abs(vertical) ? horizontal : vertical;
            return Preferences.ClampScale(initialScale * (1 + delta));
        }

        internal void Place(DesktopWindows.ScreenPoint cursor, DesktopWindows.Rect area, Size size, Preferences settings)
        {
            double left = Resize ? bounds.Left : bounds.Left + cursor.X - origin.X;
            double top = Resize ? bounds.Top : bounds.Top + cursor.Y - origin.Y;
            left = Math.Max(area.Left, left);
            top = Math.Max(area.Top, top);
            settings.RightInset = Math.Max(0, (area.Right - left) / Dpi - size.Width);
            settings.BottomInset = Math.Max(0, (area.Bottom - top) / Dpi - size.Height);
        }
    }
}
