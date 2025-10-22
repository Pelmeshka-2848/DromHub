using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс BoolToFontWeightConverter отвечает за логику компонента BoolToFontWeightConverter.
    /// </summary>
    public sealed class BoolToFontWeightConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, System.Type targetType, object parameter, string language)
            => (value is bool b && b) ? FontWeights.Bold : FontWeights.Normal;
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>
        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
            => FontWeights.Normal;
    }
}
