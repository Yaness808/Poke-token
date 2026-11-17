using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using TokenPet.Infrastructure.Codex;
using TokenPet.Infrastructure.Configuration;
using TokenPet.Infrastructure.Windows;

namespace TokenPet.Desktop.Views
{
    internal sealed class SkillsOverviewWindow : Window
    {
        private const int MinZoom = 80;
        private const int MaxZoom = 160;
        private const int ZoomStep = 10;
        private readonly ExtensionInventory inventory;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        private readonly Grid view;
        private readonly TextBox search;
        private readonly ComboBox category;
        private readonly ComboBox purpose;
        private readonly ListBox entries;
        private readonly ScaleTransform scale = new ScaleTransform();
        private bool refreshing;
        private ExtensionSnapshot latest;
        private ExtensionPresentation[] rows = new ExtensionPresentation[0];
        private Rect normalBoundsBeforeClose = Rect.Empty;
        private int zoom;

        internal SkillsOverviewWindow(Window owner, ExtensionInventory inventory, Preferences preferences)
        {
            this.inventory = inventory;
            Owner = owner;
            Title = "Skills 总览";
            MinWidth = 430;
            MinHeight = 330;
            var area = SystemParameters.WorkArea;
            Width = Clamp(preferences.SkillsOverviewWidth, MinWidth, area.Width, 660);
            Height = Clamp(preferences.SkillsOverviewHeight, MinHeight, area.Height, 560);
            ResizeMode = ResizeMode.CanResize;
            using (var stream = typeof(SkillsOverviewWindow).Assembly.GetManifestResourceStream("TokenPet.SkillsOverview.xaml"))
                view = (Grid)XamlReader.Load(stream);
            ComicDialog.Apply(this, view);
            // Layered windows ignore fully transparent pixels. Keep the rounded corners reachable for resizing.
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            ComicDialog.EnableTitleDrag(this, Find<TextBlock>("Heading"), ToggleMaximized);
            Find<Button>("MaximizeRestore").Click += (s, e) => ToggleMaximized();
            StateChanged += (s, e) => UpdateWindowControls();
            SourceInitialized += (s, e) =>
                ((HwndSource)PresentationSource.FromVisual(this)).AddHook(HandleWindowMessage);
            UpdateWindowControls();
            search = Find<TextBox>("Search");
            entries = Find<ListBox>("Entries");
            category = Find<ComboBox>("Category");
            purpose = Find<ComboBox>("Purpose");
            foreach (var label in new[] { "全部用途" }.Concat(ExtensionPresentation.Categories))
                purpose.Items.Add(new ComboBoxItem { Content = label });
            purpose.SelectedIndex = 0;
            search.TextChanged += (s, e) =>
            {
                foreach (var row in rows) row.IsExpanded = false;
                Filter();
            };
            category.SelectionChanged += (s, e) => Filter();
            purpose.SelectionChanged += (s, e) => Filter();
            Find<Button>("ShowAllMatches").Click += (s, e) =>
            {
                purpose.SelectedIndex = 0;
                category.SelectedIndex = 0;
            };
            entries.LayoutTransform = scale;
            SetZoom(preferences.SkillsOverviewZoom);
            Find<Button>("ZoomOut").Click += (s, e) => SetZoom(zoom - ZoomStep);
            Find<Button>("ZoomIn").Click += (s, e) => SetZoom(zoom + ZoomStep);
            Find<Button>("ZoomReset").Click += (s, e) => SetZoom(100);
            entries.PreviewMouseWheel += (s, e) =>
            {
                if (Keyboard.Modifiers != ModifierKeys.Control) return;
                SetZoom(zoom + Math.Sign(e.Delta) * ZoomStep);
                e.Handled = true;
            };
            Find<Thumb>("ResizeHandle").DragDelta += (s, e) =>
            {
                if (WindowState != WindowState.Normal) return;
                Width = Clamp(Width + e.HorizontalChange, MinWidth, SystemParameters.VirtualScreenWidth, Width);
                Height = Clamp(Height + e.VerticalChange, MinHeight, SystemParameters.VirtualScreenHeight, Height);
            };
            timer.Tick += async (s, e) => await Refresh();
            Loaded += async (s, e) =>
            {
                if (preferences.SkillsOverviewMaximized) WindowState = WindowState.Maximized;
                search.Focus();
                timer.Start();
                await Refresh();
            };
            // The window handle is unavailable by the time ShowDialog returns.
            Closing += (s, e) => normalBoundsBeforeClose = RestoreBounds;
            Closed += (s, e) =>
            {
                timer.Stop();
                lifetime.Cancel();
                if (!refreshing) lifetime.Dispose();
            };
        }

        private async Task Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                var next = await Task.Run(() => inventory.RefreshAsync(lifetime.Token), lifetime.Token);
                if (!lifetime.IsCancellationRequested) ApplySnapshot(next);
            }
            catch (OperationCanceledException) { }
            catch (System.IO.IOException)
            {
                Find<TextBlock>("UpdateStatus").Text = "读取暂未完成，将自动重试。";
            }
            catch (UnauthorizedAccessException)
            {
                Find<TextBlock>("UpdateStatus").Text = "部分目录暂不可读，将自动重试。";
            }
            finally
            {
                refreshing = false;
                if (lifetime.IsCancellationRequested) lifetime.Dispose();
            }
        }

        internal void ApplySnapshot(ExtensionSnapshot next)
        {
            var prior = latest;
            latest = next;
            if (prior == null || prior.Signature != next.Signature || prior.Complete != next.Complete)
            {
                var expanded = new HashSet<string>(
                    rows.Where(row => row.IsExpanded).Select(row => row.Key), StringComparer.OrdinalIgnoreCase);
                rows = next.Entries.Select(entry => new ExtensionPresentation(entry))
                    .OrderBy(row => row.CategoryOrder).ToArray();
                var bundled = rows.Where(row => row.IsBundledSkill)
                    .ToLookup(row => row.PluginKey, StringComparer.OrdinalIgnoreCase);
                foreach (var plugin in rows.Where(row => row.Kind == ExtensionEntry.Plugin))
                {
                    plugin.BundledSkills = bundled[plugin.Key].ToArray();
                    plugin.IsExpanded = expanded.Contains(plugin.Key);
                }
                Filter();
            }
            if (!next.Complete)
                Find<TextBlock>("UpdateStatus").Text = "部分状态暂未确认 · 自动重试中";
            else
                Find<TextBlock>("UpdateStatus").Text = "自动刷新 · " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void Filter()
        {
            if (latest == null) return;
            string term = search.Text.Trim();
            int typeFilter = category.SelectedIndex;
            var purposeItem = purpose.SelectedItem as ComboBoxItem;
            string function = purposeItem == null ? "" : (string)purposeItem.Content;
            var allMatches = rows.Where(entry => term.Length == 0 ? !entry.IsBundledSkill :
                (entry.Name ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(entry => string.Equals(entry.Name, term, StringComparison.OrdinalIgnoreCase)).ToArray();
            var matches = allMatches.Where(entry =>
                (typeFilter <= 0 || (typeFilter == 1 ? entry.Kind == ExtensionEntry.Plugin :
                    entry.Kind == ExtensionEntry.Skill && !entry.IsBundledSkill)) &&
                (purpose.SelectedIndex <= 0 || entry.Category == function)).ToArray();
            var showAll = Find<Button>("ShowAllMatches");
            bool hasHiddenMatches = term.Length > 0 && matches.Length < allMatches.Length;
            showAll.Visibility = hasHiddenMatches ? Visibility.Visible : Visibility.Collapsed;
            if (hasHiddenMatches) showAll.Content = "查看全部 " + allMatches.Length + " 项匹配";
            var visiblePlugins = new HashSet<string>(
                matches.Where(row => row.Kind == ExtensionEntry.Plugin).Select(row => row.Key),
                StringComparer.OrdinalIgnoreCase);
            // A search hit can appear on its own when its plugin isn't in the results.
            var visible = matches.Where(row => !row.IsBundledSkill || !visiblePlugins.Contains(row.PluginKey)).ToArray();
            var selected = entries.SelectedItem as ExtensionPresentation;
            var grouped = new ListCollectionView(visible);
            grouped.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            entries.ItemsSource = grouped;
            if (selected != null) entries.SelectedItem = visible.FirstOrDefault(entry => entry.Key == selected.Key);
            var empty = Find<TextBlock>("Empty");
            empty.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            empty.Text = !latest.Complete && latest.Entries.Length == 0
                ? "暂时无法读取 Codex 清单，窗口会自动重试。"
                : latest.Entries.Length == 0 ? "暂无已安装的插件或技能。"
                : hasHiddenMatches ? "当前筛选隐藏了匹配项。" : "没有符合搜索条件的条目。";
        }

        private void SetZoom(int value)
        {
            zoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
            scale.ScaleX = scale.ScaleY = zoom / 100d;
            Find<Button>("ZoomReset").Content = zoom + "%";
            Find<Button>("ZoomOut").IsEnabled = zoom > MinZoom;
            Find<Button>("ZoomIn").IsEnabled = zoom < MaxZoom;
        }

        private void ToggleMaximized()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private IntPtr HandleWindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int getMinMaxInfo = 0x0024;
            if (message == getMinMaxInfo) DesktopWindows.SetMaximizedWorkArea(window, lParam);

            const int hitTest = 0x0084;
            if (message != hitTest || WindowState != WindowState.Normal) return IntPtr.Zero;

            var hit = DesktopWindows.HitTestResizeBorder(window, lParam);
            if (hit == DesktopWindows.ResizeHit.None) return IntPtr.Zero;
            handled = true;
            return new IntPtr((int)hit);
        }

        private void UpdateWindowControls()
        {
            bool maximized = WindowState == WindowState.Maximized;
            var button = Find<Button>("MaximizeRestore");
            button.Content = maximized ? "还原" : "最大化";
            AutomationProperties.SetName(button, maximized ? "还原窗口" : "最大化窗口");
            Find<Thumb>("ResizeHandle").Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        }

        internal bool StoreLayout(Preferences preferences)
        {
            bool maximized = WindowState == WindowState.Maximized;
            var bounds = normalBoundsBeforeClose;
            if (bounds.IsEmpty && maximized) bounds = RestoreBounds;
            double width = bounds.IsEmpty ? Width : bounds.Width;
            double height = bounds.IsEmpty ? Height : bounds.Height;
            if (preferences.SkillsOverviewWidth == width && preferences.SkillsOverviewHeight == height &&
                preferences.SkillsOverviewZoom == zoom && preferences.SkillsOverviewMaximized == maximized) return false;
            preferences.SkillsOverviewWidth = width;
            preferences.SkillsOverviewHeight = height;
            preferences.SkillsOverviewZoom = zoom;
            preferences.SkillsOverviewMaximized = maximized;
            return true;
        }

        private static double Clamp(double value, double minimum, double maximum, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) value = fallback;
            return Math.Max(Math.Min(minimum, maximum), Math.Min(maximum, value));
        }

        private T Find<T>(string name) where T : class { return (T)view.FindName(name); }
    }
}
