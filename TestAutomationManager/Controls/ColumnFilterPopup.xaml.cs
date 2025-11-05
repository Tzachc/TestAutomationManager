using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TestAutomationManager.Models;

namespace TestAutomationManager.Controls
{
    public partial class ColumnFilterPopup : UserControl
    {
        private ColumnFilter _filter;

        public event EventHandler<ColumnFilter> FilterApplied;
        public event EventHandler FilterCleared;

        public ColumnFilterPopup()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the popup with a column filter
        /// </summary>
        public void Initialize(ColumnFilter filter)
        {
            _filter = filter;

            if (ColumnNameText != null)
                ColumnNameText.Text = $"Filter: {filter.ColumnName}";

            // Restore previous filter state if exists
            if (filter.TextFilterType != TextFilterType.None)
            {
                if (FilterTypeComboBox != null)
                    FilterTypeComboBox.SelectedIndex = (int)filter.TextFilterType - 1;

                if (FilterValueTextBox != null)
                    FilterValueTextBox.Text = filter.FilterValue ?? "";
            }
            else
            {
                // Reset to default state
                if (FilterTypeComboBox != null)
                    FilterTypeComboBox.SelectedIndex = 0;

                if (FilterValueTextBox != null)
                    FilterValueTextBox.Text = "";
            }
        }

        private void SortAscending_Click(object sender, RoutedEventArgs e)
        {
            if (_filter == null) return;

            _filter.SortDirection = SortDirection.Ascending;
            _filter.TextFilterType = TextFilterType.None; // Clear text filter when sorting
            _filter.FilterValue = "";

            FilterApplied?.Invoke(this, _filter);
        }

        private void SortDescending_Click(object sender, RoutedEventArgs e)
        {
            if (_filter == null) return;

            _filter.SortDirection = SortDirection.Descending;
            _filter.TextFilterType = TextFilterType.None; // Clear text filter when sorting
            _filter.FilterValue = "";

            FilterApplied?.Invoke(this, _filter);
        }

        private void FilterTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Null check - controls may not be loaded yet
            if (FilterValueTextBox == null || ApplyButton == null || FilterTypeComboBox == null)
                return;

            // Enable the text box and apply button when a filter type is selected
            FilterValueTextBox.IsEnabled = FilterTypeComboBox.SelectedIndex >= 0;
            ApplyButton.IsEnabled = FilterTypeComboBox.SelectedIndex >= 0;
        }

        private void FilterValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Apply filter on Enter key
            if (e.Key == Key.Enter)
            {
                Apply_Click(sender, e);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_filter == null || FilterTypeComboBox.SelectedIndex < 0) return;

            var filterValue = FilterValueTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(filterValue))
            {
                MessageBox.Show("Please enter a filter value.", "Filter Value Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _filter.SortDirection = SortDirection.None; // Clear sort when filtering
            _filter.TextFilterType = (TextFilterType)(FilterTypeComboBox.SelectedIndex + 1);
            _filter.FilterValue = filterValue;

            FilterApplied?.Invoke(this, _filter);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (_filter == null) return;

            _filter.Clear();

            if (FilterValueTextBox != null)
                FilterValueTextBox.Text = "";

            if (FilterTypeComboBox != null)
                FilterTypeComboBox.SelectedIndex = 0;

            FilterCleared?.Invoke(this, EventArgs.Empty);
        }
    }
}
