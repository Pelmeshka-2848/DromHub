using Microsoft.UI.Xaml.Data;
using System;

namespace DromHub.Converters
{
    public class ConnectionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool connected)
            {
                return connected ? "Подключено" : "Отключено";
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
