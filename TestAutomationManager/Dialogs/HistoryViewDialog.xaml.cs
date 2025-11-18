using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TestAutomationManager.Models;
using TestAutomationManager.Services;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog to view history of changes for Tests, Processes, and Functions
    /// Shows chronological list of changes with operation type, field changes, and timestamps
    /// </summary>
    public partial class HistoryViewDialog : Window
    {
        public HistoryViewDialog(string title, string subtitle, List<HistoryEntry> historyEntries)
        {
            InitializeComponent();

            // Set title and subtitle
            TitleTextBlock.Text = title;
            SubtitleTextBlock.Text = subtitle;

            // Show history or empty state
            if (historyEntries == null || historyEntries.Count == 0)
            {
                HistoryItemsControl.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
            else
            {
                HistoryItemsControl.ItemsSource = historyEntries;
                HistoryItemsControl.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }

            // Focus on Close button by default
            Loaded += (s, e) => CloseButton.Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ================================================
        // STATIC SHOW METHODS
        // ================================================

        /// <summary>
        /// Show history for a Test
        /// </summary>
        public static async Task ShowTestHistoryAsync(int testId, string testName, Window owner = null)
        {
            try
            {
                var history = await HistoryService.Instance.GetTestHistoryAsync(testId);

                var dialog = new HistoryViewDialog(
                    title: $"📝 History: {testName}",
                    subtitle: $"Recent changes to Test #{testId}",
                    historyEntries: history
                )
                {
                    Owner = owner
                };

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing test history: {ex.Message}");
                MessageBox.Show(
                    $"Failed to load history: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// Show history for a Process (including its Functions)
        /// </summary>
        public static async Task ShowProcessHistoryAsync(int processIndex, double processId, string processName, Window owner = null)
        {
            try
            {
                var history = await HistoryService.Instance.GetProcessHistoryAsync(processIndex, processId);

                var dialog = new HistoryViewDialog(
                    title: $"📝 History: {processName}",
                    subtitle: $"Recent changes to Process (Index: {processIndex}, ID: {processId}) and its Functions",
                    historyEntries: history
                )
                {
                    Owner = owner
                };

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing process history: {ex.Message}");
                MessageBox.Show(
                    $"Failed to load history: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// Show history for a Function
        /// </summary>
        public static async Task ShowFunctionHistoryAsync(int functionIndex, string functionName, Window owner = null)
        {
            try
            {
                var history = await HistoryService.Instance.GetFunctionHistoryAsync(functionIndex);

                var dialog = new HistoryViewDialog(
                    title: $"📝 History: {functionName}",
                    subtitle: $"Recent changes to Function #{functionIndex}",
                    historyEntries: history
                )
                {
                    Owner = owner
                };

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing function history: {ex.Message}");
                MessageBox.Show(
                    $"Failed to load history: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    // ================================================
    // CONVERTERS FOR XAML BINDINGS
    // ================================================

    /// <summary>
    /// Converts null to Collapsed, non-null to Visible
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && !string.IsNullOrEmpty(value.ToString())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null to Visible, non-null to Collapsed (inverse of above)
    /// </summary>
    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null || string.IsNullOrEmpty(value.ToString())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Shows EntityType only for Process history (to differentiate Process vs Function changes)
    /// </summary>
    public class EntityTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var entityType = value as string;
            // Show entity type label only for Function entries in a Process history view
            return entityType == "Function" ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
