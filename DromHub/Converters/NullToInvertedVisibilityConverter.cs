using DromHub.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    /// <summary>
    /// null (и, опционально, пустая строка) -> Visible, иначе Collapsed.
    /// </summary>
    public sealed class NullToInvertedVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Свойство TreatEmptyStringAsNull предоставляет доступ к данным TreatEmptyStringAsNull.
        /// </summary>
        public bool TreatEmptyStringAsNull { get; set; } = true;
        /// <summary>
        /// Свойство NullVisibility предоставляет доступ к данным NullVisibility.
        /// </summary>
        public Visibility NullVisibility { get; set; } = Visibility.Visible;
        /// <summary>
        /// Свойство NotNullVisibility предоставляет доступ к данным NotNullVisibility.
        /// </summary>
        public Visibility NotNullVisibility { get; set; } = Visibility.Collapsed;
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isNull = value is null || (TreatEmptyStringAsNull && value is string s && string.IsNullOrWhiteSpace(s));
            return isNull ? NullVisibility : NotNullVisibility;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}