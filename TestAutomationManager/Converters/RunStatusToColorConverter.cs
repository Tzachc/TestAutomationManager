using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TestAutomationManager.Converters
{
    /// <summary>
    /// Converts RunStatus string values to colors for status indicator
    /// Pass = Green, Fail = Red, Not Run or anything else = Grey
    /// </summary>
    public class RunStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.Trim();

            if (string.IsNullOrEmpty(status))
            {
                // No status = Grey
                return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray-400
            }

            // Normalize the status string (case-insensitive comparison)
            status = status.ToLowerInvariant();

            switch (status)
            {
                case "pass":
                    // Green for passed tests
                    return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green-500

                case "fail":
                    // Red for failed tests
                    return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red-500

                case "not run":
                    // Grey for not run
                    return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray-400

                default:
                    // Everything else = Grey (treat as not run)
                    return new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray-400
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}