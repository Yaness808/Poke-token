using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TokenPet.Desktop.Views
{
    internal static class MascotView
    {
        internal static FrameworkElement LoadDefault()
        {
            var assembly = typeof(MascotView).Assembly;
            Viewbox view;
            using (var stream = assembly.GetManifestResourceStream("TokenPet.DefaultMascot.xaml"))
                view = (Viewbox)XamlReader.Load(stream);
            using (var stream = assembly.GetManifestResourceStream("TokenPet.SquirtleReference.jpg"))
                ((Image)view.FindName("DefaultMascot")).Source = BitmapFrame.Create(
                    stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return view;
        }

        internal static Image LoadCustom(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 384;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return new Image { Source = bitmap, Stretch = Stretch.Uniform };
            }
        }

        internal static FrameworkElement Load(string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                try { return LoadCustom(customPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (FormatException) { }
                catch (NotSupportedException) { }
            }
            return LoadDefault();
        }
    }
}
