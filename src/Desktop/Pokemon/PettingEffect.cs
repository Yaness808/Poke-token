using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TokenPet.Desktop.Pokemon
{
    internal sealed class PettingEffect : FrameworkElement
    {
        private static readonly Geometry Hand = FrozenGeometry(
            "M 32,7 C 28,6 25,6 22,7 L 10,11 C 7,12 6,14 7,16 C 5,17 4,19 6,21 C 7,22 9,22 11,21 C 11,24 13,25 16,24 L 21,22 C 21,24 23,25 26,23 L 33,20 C 37,18 38,14 37,11 C 36,9 34,8 32,7 Z");
        private static readonly Geometry Cuff = FrozenGeometry(
            "M 32,7 L 37,4 Q 39,3 40,5 L 43,12 Q 44,14 42,15 L 37,18 Q 34,16 32,7 Z");
        private static readonly Geometry Seams = FrozenGeometry(
            "M 7,16 Q 10,16 15,13 M 11,21 Q 14,20 18,17 M 21,22 Q 23,20 26,19 M 20,10 Q 23,10 25,11");
        private static readonly Geometry Heart = FrozenGeometry(
            "M 7,11 C 5,9 1,6 1,3 C 1,0 5,-1 7,3 C 9,-1 13,0 13,3 C 13,6 9,9 7,11 Z");
        private static readonly Brush Fill = FrozenBrush(255, 249, 233);
        private static readonly Brush CuffFill = FrozenBrush(238, 162, 103);
        private static readonly Brush HeartFill = FrozenBrush(245, 133, 125);
        private static readonly Pen Ink = FrozenPen(108, 65, 47, 1.5);
        private static readonly Pen Detail = FrozenPen(165, 115, 79, .9);
        private readonly TranslateTransform pat = new TranslateTransform();

        internal PettingEffect()
        {
            Width = 46;
            Height = 28;
            IsHitTestVisible = false;
            Opacity = 0;
            RenderTransform = pat;
        }

        protected override void OnRender(DrawingContext drawing)
        {
            drawing.DrawGeometry(Fill, Ink, Hand);
            drawing.DrawGeometry(null, Detail, Seams);
            drawing.DrawGeometry(CuffFill, Ink, Cuff);
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
            pat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, 3, TimeSpan.FromMilliseconds(140)) {
                AutoReverse = true, RepeatBehavior = new RepeatBehavior(2), FillBehavior = FillBehavior.Stop,
                EasingFunction = new SineEase()
            });
            pat.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(2, -1.5, TimeSpan.FromMilliseconds(140)) {
                AutoReverse = true, RepeatBehavior = new RepeatBehavior(2), FillBehavior = FillBehavior.Stop
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
