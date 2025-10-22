using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Globalization;

namespace DromHub.Converters
{
    // Стойкий «магический» градиент из названия бренда
    /// <summary>
    /// Класс BrandNameToGradientBrushConverter отвечает за логику компонента BrandNameToGradientBrushConverter.
    /// </summary>
    public sealed class BrandNameToGradientBrushConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var s = (value as string ?? "").Trim();
            if (s.Length == 0)
            {
                return new SolidColorBrush(Windows.UI.Color.FromArgb(20, 128, 128, 128));
            }

            // детерминированный хэш 2-х цветов
            int h = s.GetHashCode();
            byte a = 255;
            byte r1 = (byte)(50 + (h & 0x7F));
            byte g1 = (byte)(50 + ((h >> 7) & 0x7F));
            byte b1 = (byte)(50 + ((h >> 14) & 0x7F));

            byte r2 = (byte)(50 + ((h >> 3) & 0x7F));
            byte g2 = (byte)(50 + ((h >> 10) & 0x7F));
            byte b2 = (byte)(50 + ((h >> 17) & 0x7F));

            var brush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(a, r1, g1, b1), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(a, r2, g2, b2), Offset = 1 });
            return brush;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}