using DromHub.Models;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    // Подсказка: "Наценка бренда: N%"
    public sealed class BrandMarkupBadgeTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Brand b)
            {
                decimal pct = 0m;

                if (b.Markup != null) pct = b.Markup.MarkupPct;

                var pi = typeof(Brand).GetProperty("MarkupPercent");
                if (pi != null && pi.PropertyType == typeof(decimal?))
                {
                    var v = (decimal?)pi.GetValue(b);
                    if (v.HasValue) pct = v.Value;
                }

                return $"Наценка бренда: {pct:0.#}%";
            }
            return "Наценка бренда: 0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}