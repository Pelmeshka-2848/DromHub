using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public class SectionEqualsToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null) return FontWeights.Normal;
            var sectionName = parameter.ToString();
            var current = value.ToString();
            return string.Equals(current, sectionName, StringComparison.OrdinalIgnoreCase)
                ? FontWeights.SemiBold : FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}