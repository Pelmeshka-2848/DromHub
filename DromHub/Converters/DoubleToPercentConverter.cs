using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class DoubleToPercentConverter : IValueConverter
    {
        // "F0" => без десятых; можно поставить "F1" и т.п.
        public string Format { get; set; } = "F0";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d) return $"{d.ToString(Format)}%";
            if (value is float f) return $"{f.ToString(Format)}%";
            if (value is decimal m) return $"{m.ToString(Format)}%";
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
