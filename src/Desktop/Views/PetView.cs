using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenPet.Core;
using TokenPet.Desktop.Pokemon;
using TokenPet.Infrastructure.Pokemon;

namespace TokenPet.Desktop.Views
{
    internal sealed class PetView : StackPanel
    {
        internal MascotControl Mascot { get; private set; }
        private readonly StackPanel bubble;
        private readonly Border details;
        private readonly Popup detailsPopup;

        internal PetView()
        {
            using (var stream = typeof(PetView).Assembly.GetManifestResourceStream("TokenPet.UsageDetails.xaml"))
                details = (Border)XamlReader.Load(stream);
            bubble = new StackPanel();
            bubble.Children.Add(details);
            bubble.Children.Add(new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 0,1 C 9,4 14,12 18,14 Q 18,6 26,1"),
                Fill = details.Background, Stroke = details.BorderBrush, StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, -3, 72, 0),
                IsHitTestVisible = false
            });
            Mascot = new MascotControl { HorizontalAlignment = HorizontalAlignment.Right };
            Children.Add(Mascot);
            detailsPopup = new Popup {
                AllowsTransparency = true,
                StaysOpen = true,
                PopupAnimation = PopupAnimation.None,
                PlacementTarget = Mascot,
                Placement = PlacementMode.Custom,
                CustomPopupPlacementCallback = PlaceDetails,
                Child = bubble
            };
            Children.Add(detailsPopup);
        }

        internal bool DetailsVisible { get { return detailsPopup.IsOpen; } }

        internal void ToggleDetails(Summary summary)
        {
            if (DetailsVisible) HideDetails();
            else
            {
                CardPresenter.Paint(details, summary);
                detailsPopup.IsOpen = true;
            }
        }

        internal void HideDetails() { detailsPopup.IsOpen = false; }

        private static CustomPopupPlacement[] PlaceDetails(Size bubbleSize, Size petSize, Point offset)
        {
            double left = petSize.Width * .44 - bubbleSize.Width + 82;
            return new[] {
                new CustomPopupPlacement(new Point(left, -bubbleSize.Height - 3), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(left, petSize.Height + 3), PopupPrimaryAxis.Horizontal)
            };
        }

        internal void UpdateDetails(Summary summary)
        {
            if (DetailsVisible) CardPresenter.Paint(details, summary);
        }

        internal static void Preview(string path, int speciesId, bool showDetails)
        {
            var pet = new PetView();
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JieniToken");
            var artwork = new PokemonArtwork(Path.Combine(local, "kanto-artwork"));
            var species = new PokemonCatalog().Get(speciesId);
            pet.Mascot.Show(MascotView.DecodeArtwork(artwork.GetCached(species.Id)), species.Name, species);
            FrameworkElement preview = pet;
            if (showDetails)
            {
                var date = new DateTimeOffset(2026, 11, 15, 15, 8, 0, TimeSpan.FromHours(8));
                CardPresenter.Paint(pet.details, new Summary {
                    Tokens = new Tokens { Input = 10971507, Cached = 10212400, Output = 78523 },
                    Dollars = 2.84m, Credits = 51.65m, Weekly = new WeeklyQuota(date, date.AddDays(4), 57),
                    Latest = new Sample { Model = "gpt-6.1-sol", Time = date, Tokens = new Tokens { Input = 62791, Output = 822 } }
                });
                pet.detailsPopup.Child = null;
                var panel = new StackPanel();
                panel.Children.Add(pet.bubble);
                panel.Children.Add(pet);
                preview = panel;
            }
            preview.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = preview.DesiredSize;
            preview.Arrange(new Rect(size));
            preview.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * 2),
                (int)Math.Ceiling(size.Height * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(preview);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = File.Create(path)) png.Save(output);
        }
    }
}
