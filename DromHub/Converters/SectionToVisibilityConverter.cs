using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс SectionToVisibilityConverter отвечает за логику компонента SectionToVisibilityConverter.
    /// </summary>
    public class SectionToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            var sectionName = parameter.ToString();
            var current = value.ToString(); // BrandDetailsSection enum -> string
            return string.Equals(current, sectionName, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}