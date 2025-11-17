using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TestAutomationManager.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";

            if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                return invert ? Visibility.Visible : Visibility.Collapsed;
            }

            return invert ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
