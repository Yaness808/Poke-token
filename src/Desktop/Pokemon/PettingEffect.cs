using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class PettingEffect : FrameworkElement
    {
        private static readonly Geometry Hand = FrozenGeometry(
            "M 7,19 C 5,18 5,15 7,14 L 17,9 C 18,8 19,7 19,6 C 20,4 23,5 24,7 C 25,9 23,11 21,13 L 27,13 C 30,13 31,12 34,11 L 38,18 C 34,20 33,23 30,24 C 24,26 16,26 12,25 C 10,25 9,24 9,22 C 6,23 4,21 7,19 Z");
        private static readonly Geometry Seams = FrozenGeometry("M 7,19 L 15,16 M 9,22 L 17,19 M 12,25 L 19,22 M 29,14 L 34,21");
        private static readonly Geometry Heart = FrozenGeometry(
            "M 7,11 C 5,9 1,6 1,3 C 1,0 5,-1 7,3 C 9,-1 13,0 13,3 C 13,6 9,9 7,11 Z");
        private static readonly Brush Fill = FrozenBrush(255, 236, 195);
        private static readonly Brush HeartFill = FrozenBrush(245, 133, 125);
        private static readonly Pen Ink = FrozenPen(108, 65, 47, 1.5);
        private static readonly Pen Detail = FrozenPen(165, 115, 79, .9);
        private readonly TranslateTransform pat = new TranslateTransform();

        internal PettingEffect()
        {
            Width = 43;
            Height = 29;
            IsHitTestVisible = false;
            Opacity = 0;
            RenderTransform = pat;
        }

        protected override void OnRender(DrawingContext drawing)
        {
            drawing.DrawGeometry(Fill, Ink, Hand);
            drawing.DrawGeometry(null, Detail, Seams);
            drawing.PushTransform(new ScaleTransform(.62, .62));
            drawing.DrawGeometry(HeartFill, null, Heart);
            drawing.Pop();
        }

        internal void Play()
        {
            var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, At(0)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, At(90)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, At(610)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, At(820)));
            BeginAnimation(OpacityProperty, fade);

            if (!SystemParameters.ClientAreaAnimation) return;
            pat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, 4, TimeSpan.FromMilliseconds(140)) {
                AutoReverse = true, RepeatBehavior = new RepeatBehavior(2), FillBehavior = FillBehavior.Stop,
                EasingFunction = new SineEase()
            });
        }

        private static KeyTime At(int milliseconds) { return KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(milliseconds)); }

        private static Geometry FrozenGeometry(string path)
        {
            var geometry = Geometry.Parse(path);
            geometry.Freeze();
            return geometry;
        }

        private static Brush FrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen FrozenPen(byte red, byte green, byte blue, double width)
        {
            var pen = new Pen(FrozenBrush(red, green, blue), width) {
                StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }
    }
}
