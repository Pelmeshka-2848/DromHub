using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace DromHub.Converters
{
    // 0..100 -> дуга круга (PathGeometry) для тонкого кольца
    public sealed class PercentToArcGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double val = 0;
            if (value is double d) val = d;
            else if (value is float f) val = f;
            else if (value is int i) val = i;

            val = Math.Max(0, Math.Min(100, val));

            double radius = 16; // px
            double cx = 16, cy = 16;
            double angle = val / 100.0 * 360.0;
            double radians = (Math.PI / 180.0) * (angle - 90);
            double x = cx + radius * Math.Cos(radians);
            double y = cy + radius * Math.Sin(radians);

            bool largeArc = angle > 180;

            var fig = new PathFigure
            {
                StartPoint = new Windows.Foundation.Point(cx, cy - radius),
                IsClosed = false
            };
            var arc = new ArcSegment
            {
                IsLargeArc = largeArc,
                Point = new Windows.Foundation.Point(x, y),
                Size = new Windows.Foundation.Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise
            };
            fig.Segments.Add(arc);
            var geom = new PathGeometry();
            geom.Figures.Add(fig);
            return geom;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}