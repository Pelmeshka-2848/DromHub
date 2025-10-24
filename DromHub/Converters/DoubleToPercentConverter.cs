using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс DoubleToPercentConverter отвечает за логику компонента DoubleToPercentConverter.
    /// </summary>
    public sealed class DoubleToPercentConverter : IValueConverter
    {
        // "F0" => без десятых; можно поставить "F1" и т.п.
        /// <summary>
        /// Свойство Format предоставляет доступ к данным Format.
        /// </summary>
        public string Format { get; set; } = "F0";
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d) return $"{d.ToString(Format)}%";
            if (value is float f) return $"{f.ToString(Format)}%";
            if (value is decimal m) return $"{m.ToString(Format)}%";
            return "0%";
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
