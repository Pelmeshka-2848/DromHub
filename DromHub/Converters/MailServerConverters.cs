using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using DromHub.ViewModels;

namespace DromHub.Converters
{
    /// <summary>
    /// Класс MailServerTypeConverter отвечает за логику компонента MailServerTypeConverter.
    /// </summary>
    public class MailServerTypeConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
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
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Класс MailServerTypeToVisibilityConverter отвечает за логику компонента MailServerTypeToVisibilityConverter.
    /// </summary>

    public class MailServerTypeToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MailParserViewModel.MailServerType serverType)
            {
                return serverType == MailParserViewModel.MailServerType.Custom ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Класс GmailInfoVisibilityConverter отвечает за логику компонента GmailInfoVisibilityConverter.
    /// </summary>

    public class GmailInfoVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Метод Convert выполняет основную операцию класса.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is MailParserViewModel.MailServerType serverType)
            {
                return serverType == MailParserViewModel.MailServerType.Gmail ?
                    Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        /// <summary>
        /// Метод ConvertBack выполняет основную операцию класса.
        /// </summary>

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}