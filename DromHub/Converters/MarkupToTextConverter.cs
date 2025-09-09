using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class MarkupToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        => value is decimal d ? $"{d:0.##}%" : "—";
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
