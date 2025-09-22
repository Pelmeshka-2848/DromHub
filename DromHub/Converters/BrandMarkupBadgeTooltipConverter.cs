using DromHub.Models;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Converters
{
    // Для ToolTip: "Вкл 20%" / "Выкл" / "Не задана"
    public sealed class BrandMarkupBadgeTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var b = value as Brand;
            if (b == null) return "Не задана";

            if (b.MarkupEnabled == null) return "Не задана";
            if (b.MarkupEnabled == false) return "Выкл";

            var pct = b.MarkupPercent ?? 0m;
            return $"Вкл {pct:0.#}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
