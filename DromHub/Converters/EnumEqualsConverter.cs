using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс EnumEqualsConverter отвечает за логику компонента EnumEqualsConverter.
    /// </summary>
    public sealed class EnumEqualsConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is null || parameter is null) return false;
            // сравниваем строковые представления без привязки к конкретному enum
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // не используется
            return null;
        }
    }
}