using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TokenPet.Infrastructure.Windows
{
    internal static class DesktopWindows
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ScreenPoint { public int X, Y; }

        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out ScreenPoint point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder buffer, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLong(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        private static IntPtr checkedWindow;
        private static int checkedPid;
        private static bool isCodex;

        internal static bool TryGetTarget(IntPtr window, out Rect area)
        {
            area = new Rect();
            if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
            int pid;
            GetWindowThreadProcessId(window, out pid);
            if (window != checkedWindow || pid != checkedPid)
            {
                checkedWindow = window;
                checkedPid = pid;
                isCodex = IsCodexWindow(window, pid);
            }
            if (!isCodex) return false;
            // DWM returns the visible frame without the invisible resize border.
            if (DwmGetWindowAttribute(window, 9, out area, Marshal.SizeOf(typeof(Rect))) != 0 &&
                !GetWindowRect(window, out area)) return false;
            double scale = DpiScale(window);
            return area.Right - area.Left >= 360 * scale && area.Bottom - area.Top >= 280 * scale;
        }

        internal static object Report()
        {
            Rect frame;
            Rect petFrame;
            IntPtr foreground = GetForegroundWindow();
            IntPtr pet = FindWindow(null, "Codex Token Pet");
            GetWindowRect(pet, out petFrame);
            int pid;
            GetWindowThreadProcessId(foreground, out pid);
            return new {
                foregroundPid = pid,
                foregroundClass = ClassName(foreground),
                attachedTarget = TryGetTarget(foreground, out frame),
                targetBounds = frame,
                overlayVisible = pet != IntPtr.Zero && IsWindowVisible(pet),
                overlayBounds = petFrame,
                desktopWindows = Process.GetProcessesByName("ChatGPT").Select(process => {
                    using (process)
                    {
                        IntPtr window = process.MainWindowHandle;
                        Rect area;
                        return new { pid = process.Id, visible = window != IntPtr.Zero,
                            windowClass = ClassName(window), eligible = TryGetTarget(window, out area) };
                    }
                }).Where(window => window.visible).ToArray()
            };
        }

        private static string ClassName(IntPtr window)
        {
            var name = new StringBuilder(128);
            GetClassName(window, name, name.Capacity);
            return name.ToString();
        }

        private static bool IsCodexWindow(IntPtr window, int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    if (!process.ProcessName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) &&
                        !process.ProcessName.Equals("Codex", StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            return ClassName(window) == "Chrome_WidgetWin_1";
        }

        internal static void MakePassive(IntPtr window)
        {
            const int extendedStyle = -20;
            const long noActivateToolWindow = 0x08000080;
            SetWindowLong(window, extendedStyle, new IntPtr(GetWindowLong(window, extendedStyle).ToInt64() | noActivateToolWindow));
        }

        internal static double DpiScale(IntPtr window)
        {
            uint dpi = GetDpiForWindow(window);
            return dpi == 0 ? 1 : dpi / 96.0;
        }

        internal static bool IsPrimaryMouseDown()
        {
            return GetAsyncKeyState(System.Windows.SystemParameters.SwapButtons ? 2 : 1) < 0;
        }

        internal static void Dock(IntPtr overlay, Rect area, double dpi, double width, double height,
            double rightInset, double bottomInset, bool show)
        {
            int x = (int)Math.Max(area.Left, area.Right - Math.Round((width + rightInset) * dpi));
            int y = (int)Math.Max(area.Top, area.Bottom - Math.Round((height + bottomInset) * dpi));
            Rect bounds;
            if (show || !GetWindowRect(overlay, out bounds) || bounds.Left != x || bounds.Top != y)
                SetWindowPos(overlay, new IntPtr(-1), x, y, 0, 0, show ? 0x0051u : 0x0011u);
        }
    }
}
