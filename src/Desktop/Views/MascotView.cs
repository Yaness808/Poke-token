using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TokenPet.Desktop.Views
{
    internal static class MascotView
    {
        internal static BitmapSource LoadCustom(string path)
        {
            using (var stream = File.OpenRead(path))
                return Decode(stream);
        }

        internal static BitmapSource DecodeArtwork(byte[] data)
        {
            if (data == null) return null;
            try { using (var stream = new MemoryStream(data)) return Decode(stream); }
            catch (IOException) { return null; }
            catch (FormatException) { return null; }
            catch (NotSupportedException) { return null; }
        }

        private static BitmapSource Decode(Stream stream)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 384;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
