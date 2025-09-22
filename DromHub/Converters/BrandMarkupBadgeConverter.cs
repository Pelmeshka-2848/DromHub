using DromHub.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    // Печатает только "N%" (включая "0%")
    public sealed class BrandMarkupBadgeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Brand b)
            {
                var pct = b.MarkupPercent ?? 0m;
                return $"{pct:0.#}%";
            }
            return "0%";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}