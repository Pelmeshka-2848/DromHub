using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс SectionEqualsToFontWeightConverter отвечает за логику компонента SectionEqualsToFontWeightConverter.
    /// </summary>
    public class SectionEqualsToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null) return FontWeights.Normal;
            var sectionName = parameter.ToString();
            var current = value.ToString();
            return string.Equals(current, sectionName, StringComparison.OrdinalIgnoreCase)
                ? FontWeights.SemiBold : FontWeights.Normal;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}