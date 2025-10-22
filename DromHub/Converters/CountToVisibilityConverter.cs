using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс CountToVisibilityConverter отвечает за логику компонента CountToVisibilityConverter.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isInverted = parameter?.ToString() == "invert";

            if (value is ICollection collection)
            {
                bool hasItems = collection.Count > 0;
                return (hasItems && !isInverted) || (!hasItems && isInverted)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (value is int count)
            {
                bool hasItems = count > 0;
                return (hasItems && !isInverted) || (!hasItems && isInverted)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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