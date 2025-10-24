using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс MarkupToTextConverter отвечает за логику компонента MarkupToTextConverter.
    /// </summary>
    public sealed class MarkupToTextConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        => value is decimal d ? $"{d:0.##}%" : "—";
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
