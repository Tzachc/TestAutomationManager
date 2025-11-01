using System;
using System.Globalization;
using System.Windows.Data;
using TestAutomationManager.Services;

namespace TestAutomationManager.Converters
{
    /// <summary>
    /// Converts a schema name string to a boolean if it matches the current active schema.
    /// Used to set the IsChecked property on the schema selection RadioButtons.
    /// </summary>
    public class SchemaToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string schemaName = value as string;
            if (string.IsNullOrEmpty(schemaName))
            {
                return false;
            }
            return schemaName.Equals(SchemaConfigService.Instance.CurrentSchema, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotImplementedException();
        }
    }
}