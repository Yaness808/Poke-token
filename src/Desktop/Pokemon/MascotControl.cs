using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TokenPet.Core;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class MascotControl : Grid
    {
        private readonly Image portrait = new Image { Stretch = Stretch.Uniform };
        private readonly TextBlock fallback = new TextBlock {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.PaleTurquoise, FontWeight = FontWeights.SemiBold
        };
        private readonly Ellipse shadow = new Ellipse {
            Height = 10, VerticalAlignment = VerticalAlignment.Bottom,
            Fill = new RadialGradientBrush(Color.FromArgb(120, 24, 17, 14), Color.FromArgb(0, 24, 17, 14)),
            IsHitTestVisible = false
        };
        private readonly PettingEffect petting = new PettingEffect {
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };

        internal MascotControl()
        {
            RenderOptions.SetBitmapScalingMode(portrait, BitmapScalingMode.HighQuality);
            Cursor = GloveCursor.Value;
            Children.Add(shadow);
            Children.Add(portrait);
            Children.Add(fallback);
            Children.Add(petting);
        }

        internal static double PortraitSize(PokemonSpecies species)
        {
            if (species == null) return 118;
            double stature = Math.Max(0, Math.Min(8, (species.HeightDecimeters - 5) * .65));
            return 118 + (species.EvolutionStage - 1) * 16 + stature;
        }

        internal void Show(ImageSource image, string label, PokemonSpecies species = null)
        {
            Width = Height = PortraitSize(species);
            shadow.Width = Width * .63;
            portrait.Source = image;
            petting.Margin = new Thickness(Width * (species != null && species.Id == 4 ? .28 : .39), 0, 0, 0);
            fallback.Text = label;
            UpdateArtworkVisibility();
            System.Windows.Automation.AutomationProperties.SetName(this, label);
        }

        internal void PatHead() { if (portrait.Source != null) petting.Play(); }

        private void UpdateArtworkVisibility()
        {
            shadow.Visibility = portrait.Source == null ? Visibility.Collapsed : Visibility.Visible;
            fallback.Visibility = portrait.Source == null ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
