using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenPet.Core;
using TokenPet.Infrastructure.Codex;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Windows;

namespace TokenPet.Desktop.Views
{
    internal sealed class OverlayWindow : Window
    {
        private readonly SessionUsageSource usage;
        private readonly string preferencesPath;
        private readonly Preferences preferences;
        private readonly Border card;
        private readonly Grid surface = new Grid();
        private readonly ScaleTransform scale = new ScaleTransform(1, 1);
        private readonly ContextMenu menu = new ContextMenu();
        private readonly DispatcherTimer follow = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer pointer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly WindowInteropHelper interop;
        private IntPtr attached;
        private DateTime hiddenUntil;
        private DateTime displayedDay = DateTime.Today;
        private bool scanning, dialogOpen;
        private WindowGesture gesture;

        internal OverlayWindow(SessionUsageSource usage, string preferencesPath)
        {
            this.usage = usage;
            this.preferencesPath = preferencesPath;
            preferences = Preferences.Load(preferencesPath);
            Title = "Codex Token Pet";
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            Background = Brushes.Transparent;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            interop = new WindowInteropHelper(this);

            card = CardView.Create(preferences.MascotPath);
            surface.Children.Add(card);
            surface.Children.Add(CreateSizeGrip());
            surface.LayoutTransform = scale;
            Content = surface;

            System.Windows.Automation.AutomationProperties.SetName(card, "Codex 今日 Token 用量");
            var header = Find<Grid>("Header");
            header.ToolTip = "拖动移动 · 单击查看明细 · 右键打开菜单";
            header.MouseLeftButtonDown += (s, e) => BeginGesture(e, false);

            AddMenu("展开 / 收起明细", Toggle);
            AddMenu("调整大小…", ChangeSize);
            AddMenu("恢复默认位置和大小", ResetPlacement);
            menu.Items.Add(new Separator());
            AddMenu("更换头像…", ChooseMascot);
            AddMenu("恢复杰尼龟头像", ResetMascot);
            menu.Items.Add(new Separator());
            AddMenu("隐藏 15 分钟", () => { hiddenUntil = DateTime.UtcNow.AddMinutes(15); Hide(); });
            AddMenu("退出 Token 精灵", () => Application.Current.Shutdown());
            surface.ContextMenu = menu;

            follow.Tick += (s, e) => Follow();
            refresh.Tick += (s, e) => Refresh();
            pointer.Tick += (s, e) => UpdateGesture();
        }

        internal void Start()
        {
            follow.Start();
            refresh.Start();
            Follow();
        }

        private Border CreateSizeGrip()
        {
            var grip = new Border {
                Width = 18, Height = 18, Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeNWSE, ToolTip = "拖动调节大小"
            };
            grip.Child = new TextBlock {
                Text = "◢", FontSize = 9, Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 5, 3), IsHitTestVisible = false
            };
            grip.MouseLeftButtonDown += (s, e) => BeginGesture(e, true);
            return grip;
        }

        private T Find<T>(string name) where T : class { return (T)card.FindName(name); }

        private void AddMenu(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        private void Toggle()
        {
            var details = Find<StackPanel>("Details");
            details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            Follow();
        }

        private async void Refresh()
        {
            if (attached == IntPtr.Zero || !IsVisible) return;
            var now = DateTimeOffset.Now;
            if (now.Date != displayedDay)
            {
                displayedDay = now.Date;
                CardPresenter.Paint(card, new Summary());
            }
            if (scanning) return;
            scanning = true;
            try
            {
                var summary = await Task.Run(() => usage.Scan(now.Date, now));
                if (now.Date == DateTime.Today) CardPresenter.Paint(card, summary);
            }
            catch (Exception ex) { Find<TextBlock>("Cost").Text = "读取用量失败 · " + ex.GetType().Name; }
            finally { scanning = false; }
        }

        private void Follow()
        {
            if (menu.IsOpen || dialogOpen || gesture != null) return;
            IntPtr foreground = DesktopWindows.GetForegroundWindow();
            var target = foreground == interop.Handle && foreground != IntPtr.Zero ? attached : foreground;
            DesktopWindows.Rect area;
            if (DateTime.UtcNow < hiddenUntil || !DesktopWindows.TryGetTarget(target, out area))
            {
                attached = IntPtr.Zero;
                if (IsVisible) Hide();
                return;
            }

            attached = target;
            bool opening = !IsVisible;
            if (opening)
            {
                interop.EnsureHandle();
                DesktopWindows.MakePassive(interop.Handle);
            }
            double dpi = DesktopWindows.DpiScale(target);
            var size = Measure(area, dpi);
            Dock(area, dpi, size, false);
            if (opening)
            {
                Show();
                Dock(area, dpi, size, true);
                Refresh();
            }
        }

        private Size Measure(DesktopWindows.Rect area, double dpi)
        {
            var unconstrained = new Size(double.PositiveInfinity, double.PositiveInfinity);
            surface.Measure(unconstrained);
            var size = surface.DesiredSize;
            double fit = Math.Min((area.Right - area.Left) / (dpi * size.Width),
                (area.Bottom - area.Top) / (dpi * size.Height)) * scale.ScaleX;
            double desiredScale = Math.Min(preferences.Scale, fit);
            if (Math.Abs(desiredScale - scale.ScaleX) > 0.001)
            {
                scale.ScaleX = scale.ScaleY = desiredScale;
                surface.Measure(unconstrained);
            }
            return surface.DesiredSize;
        }

        private void Dock(DesktopWindows.Rect area, double dpi, Size size, bool show)
        {
            DesktopWindows.Dock(interop.Handle, area, dpi, size.Width, size.Height,
                preferences.RightInset, preferences.BottomInset, show);
        }

        private void BeginGesture(MouseButtonEventArgs e, bool resize)
        {
            DesktopWindows.ScreenPoint cursor;
            DesktopWindows.Rect bounds;
            if (gesture != null || attached == IntPtr.Zero ||
                !DesktopWindows.GetCursorPos(out cursor) || !DesktopWindows.GetWindowRect(interop.Handle, out bounds))
                return;
            gesture = new WindowGesture(cursor, bounds, DesktopWindows.DpiScale(attached), scale.ScaleX, resize);
            pointer.Start();
            e.Handled = true;
        }

        private void UpdateGesture()
        {
            var active = gesture;
            DesktopWindows.ScreenPoint cursor;
            DesktopWindows.Rect area;
            if (!DesktopWindows.GetCursorPos(out cursor) || !DesktopWindows.TryGetTarget(attached, out area))
            {
                EndGesture(false);
                return;
            }

            bool down = DesktopWindows.IsPrimaryMouseDown();
            if (active.HasMoved(cursor))
            {
                active.Moved = true;
                if (active.Resize) preferences.Scale = active.ResizeTo(cursor);
                var size = Measure(area, active.Dpi);
                active.Place(cursor, area, size, preferences);
                Dock(area, active.Dpi, size, false);
            }
            if (!down) EndGesture(true);
        }

        private void EndGesture(bool released)
        {
            var completed = gesture;
            gesture = null;
            pointer.Stop();
            if (completed.Moved) SavePreferences();
            else if (released && !completed.Resize) Toggle();
            Follow();
        }

        private void ChangeSize()
        {
            dialogOpen = true;
            try
            {
                var dialog = new SizeDialog(this, preferences.Scale);
                if (dialog.ShowDialog() != true) return;
                preferences.Scale = dialog.SelectedScale;
                SavePreferences();
            }
            finally { dialogOpen = false; }
            Follow();
        }

        private void ResetPlacement()
        {
            preferences.ResetPlacement();
            SavePreferences();
            Follow();
        }

        private void ChooseMascot()
        {
            dialogOpen = true;
            try
            {
                var dialog = new OpenFileDialog { Title = "选择头像", Filter = "图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
                if (dialog.ShowDialog(this) != true) return;
                Find<ContentControl>("Mascot").Content = MascotView.LoadCustom(dialog.FileName);
                preferences.MascotPath = dialog.FileName;
                SavePreferences();
            }
            catch (IOException) { ShowImageError(); }
            catch (UnauthorizedAccessException) { ShowImageError(); }
            catch (FormatException) { ShowImageError(); }
            catch (NotSupportedException) { ShowImageError(); }
            finally { dialogOpen = false; }
        }

        private void ShowImageError()
        {
            MessageBox.Show(this, "无法读取图片，请选择有效的 PNG 或 JPEG 文件。", "更换头像");
        }

        private void ResetMascot()
        {
            Find<ContentControl>("Mascot").Content = MascotView.LoadDefault();
            preferences.MascotPath = null;
            SavePreferences();
        }

        private void SavePreferences()
        {
            try { preferences.Save(preferencesPath); }
            catch (IOException) { Find<Grid>("Header").ToolTip = "无法保存设置 · 本次运行已应用"; }
            catch (UnauthorizedAccessException) { Find<Grid>("Header").ToolTip = "无法保存设置 · 本次运行已应用"; }
        }
    }
}
