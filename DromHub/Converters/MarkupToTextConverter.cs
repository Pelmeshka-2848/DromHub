using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class MarkupToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "—";
            if (value is decimal d) return $"{d:0.##}%";
            if (value is double f) return $"{f:0.##}%";
            if (value is float s) return $"{s:0.##}%";
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var s = (value as string)?.Replace("%", "").Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (decimal.TryParse(s, out var d)) return d;
            return null;
        }
    }
}
