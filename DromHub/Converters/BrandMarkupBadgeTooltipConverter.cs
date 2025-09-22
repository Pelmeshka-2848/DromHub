using DromHub.Models;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Converters
{
    // Тултип: "30%" или "0% (не применяется)"
    public sealed class BrandMarkupBadgeTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Brand b)
            {
                var pct = b.MarkupPercent ?? 0m;
                return pct == 0m ? "0% (не применяется)" : $"{pct:0.#}%";
            }
            return "0% (не применяется)";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
    }
}
