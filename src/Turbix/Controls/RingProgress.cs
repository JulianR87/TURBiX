using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Turbix.Controls
{
    /// <summary>Anillo de progreso (0–100) con animación suave.</summary>
    public class RingProgress : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(RingProgress), new PropertyMetadata(0.0, OnValueChanged));

        private static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
            "DisplayValue", typeof(double), typeof(RingProgress),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
            nameof(Thickness), typeof(double), typeof(RingProgress),
            new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
            nameof(TrackBrush), typeof(Brush), typeof(RingProgress),
            new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
            nameof(Fill), typeof(Brush), typeof(RingProgress),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
        public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ring = (RingProgress)d;
            var anim = new DoubleAnimation((double)e.NewValue, TimeSpan.FromMilliseconds(900))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ring.BeginAnimation(DisplayValueProperty, anim);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return;
            double t = Thickness;
            double r = (size - t) / 2;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);

            var track = new Pen(TrackBrush, t);
            dc.DrawEllipse(null, track, center, r, r);

            double value = Math.Max(0, Math.Min(100, (double)GetValue(DisplayValueProperty)));
            if (value <= 0) return;

            var pen = new Pen(Fill, t) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (value >= 99.99)
            {
                dc.DrawEllipse(null, pen, center, r, r);
                return;
            }

            double angle = value / 100 * 360;
            double rad = (angle - 90) * Math.PI / 180;
            var start = new Point(center.X, center.Y - r);
            var end = new Point(center.X + r * Math.Cos(rad), center.Y + r * Math.Sin(rad));
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(r, r), 0, angle > 180, SweepDirection.Clockwise, true, true);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
    }
}
