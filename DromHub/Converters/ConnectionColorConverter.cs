using Windows.UI.Xaml.Data;
using Windows.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс ConnectionColorConverter отвечает за логику компонента ConnectionColorConverter.
    /// </summary>
    public class ConnectionColorConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ?
                new SolidColorBrush(Colors.Green) :
                new SolidColorBrush(Colors.Red);
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