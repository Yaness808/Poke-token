using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TokenPet.Desktop.Pokemon
{
    internal static class GloveCursor
    {
        private const int Pixels = 32;
        internal static readonly Cursor Value = Create();

        private static Cursor Create()
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    WriteCursor(writer, RenderPixels());
                    stream.Position = 0;
                    return new Cursor(stream);
                }
            }
            catch (ArgumentException) { return Cursors.Hand; }
            catch (IOException) { return Cursors.Hand; }
            catch (Win32Exception) { return Cursors.Hand; }
        }

        private static byte[] RenderPixels()
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                var ink = new Pen(new SolidColorBrush(Color.FromRgb(83, 50, 41)), 1.65) { LineJoin = PenLineJoin.Round };
                var glove = Geometry.Parse(
                    "M 12,26 C 10,24 9,22 8,20 L 5,16 Q 4,14 5,13 C 7,12 9,13 11,16 L 10,6 C 10,4 11,3 13,3 C 15,3 16,5 16,7 L 16,11 C 17,9 20,9 21,12 C 22,10 25,11 25,14 C 27,13 29,15 29,17 L 28,22 Q 28,25 25,27 Z");
                var cuff = Geometry.Parse("M 12,25 Q 12,24 14,24 L 26,24 Q 28,24 28,26 L 27,29 L 13,29 Z");
                drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(255, 249, 233)), ink, glove);
                drawing.DrawGeometry(new SolidColorBrush(Color.FromRgb(238, 162, 103)), ink, cuff);
                drawing.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(181, 138, 105)), .9),
                    Geometry.Parse("M 16,11 L 16,15 M 21,12 L 21,15 M 25,14 L 25,16 M 13,20 Q 14,22 17,22"));
            }
            var bitmap = new RenderTargetBitmap(Pixels, Pixels, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var straightAlpha = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[Pixels * Pixels * 4];
            straightAlpha.CopyPixels(pixels, Pixels * 4, 0);
            return pixels;
        }

        private static void WriteCursor(BinaryWriter writer, byte[] pixels)
        {
            const int headerBytes = 40;
            const int pixelStride = Pixels * 4;
            const int maskStride = 4;
            // One 32-bit CUR entry with its hotspot at the raised fingertip.
            writer.Write((ushort)0);
            writer.Write((ushort)2);
            writer.Write((ushort)1);
            writer.Write((byte)Pixels);
            writer.Write((byte)Pixels);
            writer.Write((ushort)0);
            writer.Write((ushort)13);
            writer.Write((ushort)3);
            writer.Write(headerBytes + (pixelStride + maskStride) * Pixels);
            writer.Write(22);

            // The DIB contains bottom-up BGRA pixels followed by a one-bit transparency mask.
            writer.Write(headerBytes);
            writer.Write(Pixels);
            writer.Write(Pixels * 2);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(0);
            writer.Write((pixelStride + maskStride) * Pixels);
            writer.Write(new byte[16]);
            for (int row = Pixels - 1; row >= 0; row--)
                writer.Write(pixels, row * pixelStride, pixelStride);
            for (int row = Pixels - 1; row >= 0; row--)
            {
                var mask = new byte[maskStride];
                for (int column = 0; column < Pixels; column++)
                    if (pixels[row * pixelStride + column * 4 + 3] == 0)
                        mask[column / 8] |= (byte)(0x80 >> (column % 8));
                writer.Write(mask);
            }
        }
    }
}
