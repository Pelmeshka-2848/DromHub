using DromHub.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class BrandMarkupBadgeConverter : IValueConverter
    {
        // Для отображения в правой части строки бренда: "15%" / "выкл" / "—"
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var b = value as Brand;
            if (b == null) return "—";

            // Нет записи о наценке
            if (b.MarkupEnabled == null) return "—";

            // Запись есть, но выключена
            if (b.MarkupEnabled == false) return "выкл";

            // Включена — показываем процент (если null, считаем 0)
            var pct = b.MarkupPercent ?? 0m;
            return $"{pct:0.#}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}