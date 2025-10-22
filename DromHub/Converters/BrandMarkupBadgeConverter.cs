using DromHub.Models;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    // Печатает "N%"
    /// <summary>
    /// Класс BrandMarkupBadgeConverter отвечает за логику компонента BrandMarkupBadgeConverter.
    /// </summary>
    public sealed class BrandMarkupBadgeConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Brand b)
            {
                decimal pct = 0m;

                // 1) Навигационное свойство Brand.Markup?.MarkupPct
                if (b.Markup != null) pct = b.Markup.MarkupPct;

                // 2) Fallback: NotMapped Brand.MarkupPercent (если где-то заполняется)
                var pi = typeof(Brand).GetProperty("MarkupPercent");
                if (pi != null && pi.PropertyType == typeof(decimal?))
                {
                    var v = (decimal?)pi.GetValue(b);
                    if (v.HasValue) pct = v.Value;
                }

                return $"{pct:0.#}%";
            }
            return "0%";
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}