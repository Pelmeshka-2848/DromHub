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
        public bool TreatEmptyStringAsNull { get; set; } = true;
        public Visibility NullVisibility { get; set; } = Visibility.Visible;
        public Visibility NotNullVisibility { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isNull = value is null || (TreatEmptyStringAsNull && value is string s && string.IsNullOrWhiteSpace(s));
            return isNull ? NullVisibility : NotNullVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}