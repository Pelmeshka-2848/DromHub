using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс DateTimeToShortConverter отвечает за логику компонента DateTimeToShortConverter.
    /// </summary>
    public sealed class DateTimeToShortConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
                return dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            return "—";
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}