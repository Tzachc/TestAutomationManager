using System;
using System.ComponentModel;

namespace TestAutomationManager.Models
{
    /// <summary>
    /// Represents the filter type for text filtering
    /// </summary>
    public enum TextFilterType
    {
        None,
        Equal,
        BeginsWith,
        Contains
    }

    /// <summary>
    /// Represents the sort direction
    /// </summary>
    public enum SortDirection
    {
        None,
        Ascending,  // Smallest to Highest
        Descending  // Highest to Smallest
    }

    /// <summary>
    /// Represents a filter configuration for a column
    /// </summary>
    public class ColumnFilter : INotifyPropertyChanged
    {
        private string _columnName;
        private SortDirection _sortDirection;
        private TextFilterType _textFilterType;
        private string _filterValue;

        public string ColumnName
        {
            get => _columnName;
            set
            {
                _columnName = value;
                OnPropertyChanged(nameof(ColumnName));
            }
        }

        public SortDirection SortDirection
        {
            get => _sortDirection;
            set
            {
                _sortDirection = value;
                OnPropertyChanged(nameof(SortDirection));
            }
        }

        public TextFilterType TextFilterType
        {
            get => _textFilterType;
            set
            {
                _textFilterType = value;
                OnPropertyChanged(nameof(TextFilterType));
            }
        }

        public string FilterValue
        {
            get => _filterValue;
            set
            {
                _filterValue = value;
                OnPropertyChanged(nameof(FilterValue));
            }
        }

        /// <summary>
        /// Property name used for binding (e.g., "TestID", "ProcessID")
        /// </summary>
        public string PropertyName { get; set; }

        public bool IsActive => SortDirection != SortDirection.None || TextFilterType != TextFilterType.None;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Test if a value matches the filter criteria
        /// </summary>
        public bool Matches(object value)
        {
            // If no text filter is active, pass
            if (TextFilterType == TextFilterType.None || string.IsNullOrEmpty(FilterValue))
                return true;

            var stringValue = value?.ToString() ?? "";
            var filterValue = FilterValue;

            switch (TextFilterType)
            {
                case TextFilterType.Equal:
                    return stringValue.Equals(filterValue, StringComparison.OrdinalIgnoreCase);

                case TextFilterType.BeginsWith:
                    return stringValue.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase);

                case TextFilterType.Contains:
                    return stringValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase);

                default:
                    return true;
            }
        }

        /// <summary>
        /// Clear all filters for this column
        /// </summary>
        public void Clear()
        {
            SortDirection = SortDirection.None;
            TextFilterType = TextFilterType.None;
            FilterValue = "";
        }
    }
}
