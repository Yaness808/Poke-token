using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace TokenPet.Desktop.Views
{
    internal static class ComicDialog
    {
        internal static ResourceDictionary LoadTheme()
        {
            using (var stream = typeof(ComicDialog).Assembly.GetManifestResourceStream("TokenPet.ComicTheme.xaml"))
                return (ResourceDictionary)XamlReader.Load(stream);
        }

        internal static void Apply(Window window, FrameworkElement content)
        {
            var theme = LoadTheme();
            window.Resources.MergedDictionaries.Add(theme);
            window.WindowStyle = WindowStyle.None;
            window.AllowsTransparency = true;
            window.Background = Brushes.Transparent;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
            window.FontSize = 13;
            window.Foreground = (Brush)theme["ComicInk"];
            window.Content = new ContentControl {
                Style = (Style)theme["ComicFrame"],
                Padding = new Thickness(16, 14, 16, 14),
                Content = content
            };
        }

        internal static void EnableTitleDrag(Window window, TextBlock title)
        {
            title.Background = Brushes.Transparent;
            title.MouseLeftButtonDown += (s, e) => window.DragMove();
        }
    }
}
