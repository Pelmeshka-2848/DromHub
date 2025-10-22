using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс InverseBooleanConverter отвечает за логику компонента InverseBooleanConverter.
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b ? !b : value;
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => value is bool b ? !b : value;
    }
}
