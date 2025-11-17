using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TestAutomationManager.Converters
{
    /// <summary>
    /// Converts percentage (0-100) to width for progress bar
    /// Assumes parent container is 200px wide
    /// </summary>
    public class PercentageToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                // Scale to a reasonable max width (e.g., 200px container)
                return Math.Max(0, Math.Min(200, percentage * 2));
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts count to height for bar charts
    /// Scales count to pixel height (max 100px for 10+ items)
    /// </summary>
    public class CountToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Scale: 1 test = 10px, max 100px
                return Math.Max(2, Math.Min(100, count * 10));
            }
            return 2.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts hex color string to Color
    /// </summary>
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString && !string.IsNullOrWhiteSpace(colorString))
            {
                try
                {
                    return (Color)ColorConverter.ConvertFromString(colorString);
                }
                catch
                {
                    // Return gray as fallback
                    return Colors.Gray;
                }
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return color.ToString();
            }
            return "#9E9E9E";
        }
    }
}
