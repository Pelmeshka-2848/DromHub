using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public sealed class DateTimeToShortConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
                return dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            return "—";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}