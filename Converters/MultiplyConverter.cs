using System;
using System.Globalization;
using System.Windows.Data;

namespace Winton.Converters
{
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double number = System.Convert.ToDouble(value);
            double factor = System.Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
            return number * factor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
