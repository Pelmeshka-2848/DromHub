using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;

namespace DromHub.Converters
{
    public class CountToVisibilityConverter : IValueConverter
    {
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}