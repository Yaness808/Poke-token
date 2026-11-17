using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenPet.Core;
using TokenPet.Desktop.Pokemon;
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
        private readonly PetView surface = new PetView();
        private readonly ScaleTransform scale = new ScaleTransform(1, 1);
        private readonly ContextMenu menu;
        private readonly MascotCompanion companion;
        private readonly DispatcherTimer follow = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer pointer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        private readonly WindowInteropHelper interop;
        private IntPtr attached;
        private DateTime hiddenUntil;
        private DateTime displayedDay = DateTime.Today;
        private bool scanning, dialogOpen;
        private WindowGesture gesture;
        private Summary latestSummary = new Summary();

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

            var mascot = surface.Mascot;
            companion = new MascotCompanion(preferences, mascot,
                Path.Combine(Path.GetDirectoryName(preferencesPath), "kanto-artwork"));
            Closed += (s, e) => companion.CancelArtwork();
            surface.LayoutTransform = scale;
            Content = surface;

            mascot.MouseLeftButtonDown += (s, e) => BeginGesture(e);

            using (var stream = typeof(OverlayWindow).Assembly.GetManifestResourceStream("TokenPet.MascotMenu.xaml"))
                menu = (ContextMenu)XamlReader.Load(stream);
            menu.Resources.MergedDictionaries.Add(ComicDialog.LoadTheme());
            AddMenu("用量详情…", ShowUsageDetails);
            AddMenu("调整大小…", ChangeSize);
            AddMenu("恢复默认位置和大小", ResetPlacement);
            menu.Items.Add(new Separator());
            AddMenu("切换宝可梦…", ChoosePokemon);
            AddMenu("使用本地图片…", ChooseMascot);
            menu.Items.Add(new Separator());
            AddMenu("隐藏 15 分钟", () => { hiddenUntil = DateTime.UtcNow.AddMinutes(15); surface.HideDetails(); Hide(); });
            AddMenu("退出 Token 精灵", () => Application.Current.Shutdown());
            mascot.ContextMenu = menu;

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

        private void AddMenu(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        private void ShowUsageDetails()
        {
            surface.ToggleDetails(latestSummary);
            Follow();
        }

        private async void Refresh()
        {
            if (attached == IntPtr.Zero || !IsVisible) return;
            companion.RefreshArtwork();
            var now = DateTimeOffset.Now;
            if (now.Date != displayedDay)
            {
                displayedDay = now.Date;
                UpdateUsage(new Summary());
            }
            if (scanning) return;
            scanning = true;
            try
            {
                var summary = await Task.Run(() => usage.Scan(now.Date, now));
                if (now.Date == DateTime.Today) UpdateUsage(summary);
            }
            catch (Exception)
            {
                latestSummary.Unavailable = true;
                surface.UpdateDetails(latestSummary);
            }
            finally { scanning = false; }
        }

        private void UpdateUsage(Summary summary)
        {
            latestSummary = summary;
            surface.UpdateDetails(summary);
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
                if (IsVisible)
                {
                    surface.HideDetails();
                    Hide();
                }
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
            if (DesktopWindows.Dock(interop.Handle, area, dpi, size.Width, size.Height,
                preferences.RightInset, preferences.BottomInset, show))
                surface.HideDetails();
        }

        private void BeginGesture(MouseButtonEventArgs e)
        {
            DesktopWindows.ScreenPoint cursor;
            DesktopWindows.Rect bounds;
            if (gesture != null || attached == IntPtr.Zero ||
                !DesktopWindows.GetCursorPos(out cursor) || !DesktopWindows.GetWindowRect(interop.Handle, out bounds))
                return;
            gesture = new WindowGesture(cursor, bounds, DesktopWindows.DpiScale(attached));
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
            else if (released)
            {
                surface.Mascot.PatHead();
                ShowUsageDetails();
                return;
            }
            Follow();
        }

        private void ChangeSize()
        {
            surface.HideDetails();
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
            surface.HideDetails();
            dialogOpen = true;
            try
            {
                var dialog = new OpenFileDialog { Title = "选择头像", Filter = "图片 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
                if (dialog.ShowDialog(this) != true) return;
                companion.SelectCustom(dialog.FileName);
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

        private void ChoosePokemon()
        {
            surface.HideDetails();
            dialogOpen = true;
            try
            {
                var picker = new PickerWindow(this, companion.Catalog, companion.Artwork, companion.SelectedId,
                    preferences.FavoritePokemonIds);
                bool selected = picker.ShowDialog() == true;
                var favorites = picker.FavoriteIds;
                bool favoritesChanged = !favorites.SequenceEqual(preferences.FavoritePokemonIds);
                preferences.FavoritePokemonIds = favorites;
                if (selected) companion.Select(picker.Selected, picker.SelectedArtwork);
                if (selected || favoritesChanged) SavePreferences();
            }
            finally { dialogOpen = false; Follow(); }
        }

        private void SavePreferences()
        {
            try { preferences.Save(preferencesPath); }
            catch (IOException) { surface.Mascot.ToolTip = "无法保存设置 · 本次运行已应用"; }
            catch (UnauthorizedAccessException) { surface.Mascot.ToolTip = "无法保存设置 · 本次运行已应用"; }
        }
    }
}
