using System;
using System.Globalization;
using System.Windows.Data;

namespace TestAutomationManager.Converters
{
    /// <summary>
    /// Converts empty image URLs to null so WPF doesn't throw binding errors
    /// </summary>
    public class ImageUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
            return null; // Return null for empty/null strings, which WPF can handle
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
