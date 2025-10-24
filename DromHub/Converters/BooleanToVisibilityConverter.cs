using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс BooleanToVisibilityConverter отвечает за логику компонента BooleanToVisibilityConverter.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                bool shouldInvert = parameter?.ToString() == "invert";
                if (shouldInvert)
                    boolValue = !boolValue;

                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}