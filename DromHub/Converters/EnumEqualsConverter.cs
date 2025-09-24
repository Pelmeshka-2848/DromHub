// Converters/EnumEqualsConverter.cs
using System;
using DromHub.ViewModels;                 // для BrandDetailsSection
using Microsoft.UI.Xaml;                  // DependencyProperty.UnsetValue
using Microsoft.UI.Xaml.Data;

namespace DromHub.Converters
{
    public sealed class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is null || parameter is null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        // Если кнопку включили → пишем соответствующий enum.
        // Если пытаются выключить активную кнопку → не трогаем источник.
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isChecked && isChecked &&
                parameter is string s &&
                Enum.TryParse(typeof(BrandDetailsSection), s, out var boxed))
            {
                return boxed;
            }

            // ничего не менять в источнике
            return DependencyProperty.UnsetValue;
        }
    }
}