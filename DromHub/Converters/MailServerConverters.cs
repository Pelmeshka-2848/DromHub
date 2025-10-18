using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using DromHub.ViewModels;

namespace DromHub.Converters
{
    public class MailServerTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MailParserViewModel.MailServerType serverType)
            {
                return serverType switch
                {
                    MailParserViewModel.MailServerType.Gmail => "Gmail",
                    MailParserViewModel.MailServerType.MailRu => "Mail.ru",
                    MailParserViewModel.MailServerType.Yandex => "Yandex Mail",
                    MailParserViewModel.MailServerType.Custom => "Другой почтовый сервер",
                    _ => value.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class MailServerTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MailParserViewModel.MailServerType serverType)
            {
                return serverType == MailParserViewModel.MailServerType.Custom ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class GmailInfoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MailParserViewModel.MailServerType serverType)
            {
                return serverType == MailParserViewModel.MailServerType.Gmail ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}