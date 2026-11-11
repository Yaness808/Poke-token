using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenPet.Core;

namespace TokenPet.Desktop.Views
{
    internal static class CardView
    {
        internal static Border Create(string mascotPath)
        {
            Border card;
            using (var stream = typeof(CardView).Assembly.GetManifestResourceStream("TokenPet.Card.xaml"))
                card = (Border)XamlReader.Load(stream);
            ((ContentControl)card.FindName("Mascot")).Content = MascotView.Load(mascotPath);
            return card;
        }

        internal static void Preview(string path)
        {
            var card = Create(null);
            var date = new DateTimeOffset(2026, 11, 11, 15, 8, 0, TimeSpan.FromHours(8));
            CardPresenter.Paint(card, new Summary {
                Tokens = new Tokens { Input = 1476020, Cached = 1330080, Output = 18780 },
                Dollars = 0.61m,
                Credits = 15.32m,
                Weekly = new WeeklyQuota(date, date.AddDays(5), 30),
                Latest = new Sample { Model = "gpt-6.1-sol", Time = date, Tokens = new Tokens { Input = 54321, Output = 892 } }
            });
            ((StackPanel)card.FindName("Details")).Visibility = Visibility.Visible;
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = card.DesiredSize;
            card.Arrange(new Rect(size));
            card.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * 2),
                (int)Math.Ceiling(size.Height * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(card);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(path)) png.Save(output);
        }
    }
}
