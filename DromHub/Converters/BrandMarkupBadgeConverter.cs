using DromHub.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class BrandMarkupBadgeConverter : IValueConverter
    {
        // value ожидается = Brand
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Brand b)
            {
                // Нет записи о наценке
                if (b.MarkupEnabled == null)
                    return "—";

                // Есть запись, но выключено применение
                if (b.MarkupEnabled == false)
                    return "выкл";

                // Включено — показываем процент
                if (b.MarkupPercent.HasValue)
                    return $"{b.MarkupPercent.Value:0.#}%";

                return "0%";
            }

            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}