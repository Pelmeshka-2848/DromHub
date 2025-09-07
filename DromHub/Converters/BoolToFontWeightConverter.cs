using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;

namespace DromHub.Converters
{
    public sealed class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
            => (value is bool b && b) ? FontWeights.Bold : FontWeights.Normal;
        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
            => FontWeights.Normal;
    }
}
