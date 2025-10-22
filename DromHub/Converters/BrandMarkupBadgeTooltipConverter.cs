using DromHub.Models;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    // Подсказка: "Наценка бренда: N%"
    /// <summary>
    /// Класс BrandMarkupBadgeTooltipConverter отвечает за логику компонента BrandMarkupBadgeTooltipConverter.
    /// </summary>
    public sealed class BrandMarkupBadgeTooltipConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
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
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}