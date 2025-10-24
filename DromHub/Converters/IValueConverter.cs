using Microsoft.UI.Xaml.Data;
using System;
using System.Globalization;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс DoubleToStringConverter отвечает за логику компонента DoubleToStringConverter.
    /// </summary>
    public class DoubleToStringConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
                return d.ToString("F2", CultureInfo.InvariantCulture); // формат с 2 знаками после запятой
            return value?.ToString() ?? "";
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;
            return 0.0;
        }
    }
}
