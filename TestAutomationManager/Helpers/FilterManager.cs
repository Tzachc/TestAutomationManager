using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using TestAutomationManager.Models;

namespace TestAutomationManager.Helpers
{
    /// <summary>
    /// Manages filtering and sorting for data collections
    /// </summary>
    public class FilterManager<T> where T : class
    {
        private readonly ObservableCollection<T> _sourceCollection;
        private readonly ObservableCollection<T> _filteredCollection;
        private readonly Dictionary<string, ColumnFilter> _columnFilters;

        public FilterManager(ObservableCollection<T> sourceCollection, ObservableCollection<T> filteredCollection)
        {
            _sourceCollection = sourceCollection;
            _filteredCollection = filteredCollection;
            _columnFilters = new Dictionary<string, ColumnFilter>();
        }

        /// <summary>
        /// Get or create a column filter
        /// </summary>
        public ColumnFilter GetColumnFilter(string columnName, string propertyName)
        {
            if (!_columnFilters.ContainsKey(propertyName))
            {
                _columnFilters[propertyName] = new ColumnFilter
                {
                    ColumnName = columnName,
                    PropertyName = propertyName
                };
            }
            return _columnFilters[propertyName];
        }

        /// <summary>
        /// Apply all active filters
        /// </summary>
        public void ApplyFilters()
        {
            _filteredCollection.Clear();

            var activeFilters = _columnFilters.Values.Where(f => f.IsActive).ToList();

            // Start with all items
            IEnumerable<T> filteredItems = _sourceCollection;

            // Apply text filters
            foreach (var filter in activeFilters.Where(f => f.TextFilterType != TextFilterType.None))
            {
                filteredItems = ApplyTextFilter(filteredItems, filter);
            }

            // Apply sorting (only one sort at a time - last sort wins)
            var sortFilter = activeFilters.FirstOrDefault(f => f.SortDirection != SortDirection.None);
            if (sortFilter != null)
            {
                filteredItems = ApplySorting(filteredItems, sortFilter);
            }

            // Add filtered items to collection
            foreach (var item in filteredItems)
            {
                _filteredCollection.Add(item);
            }
        }

        /// <summary>
        /// Apply text filter to items
        /// </summary>
        private IEnumerable<T> ApplyTextFilter(IEnumerable<T> items, ColumnFilter filter)
        {
            return items.Where(item =>
            {
                var value = GetPropertyValue(item, filter.PropertyName);
                return filter.Matches(value);
            });
        }

        /// <summary>
        /// Apply sorting to items
        /// </summary>
        private IEnumerable<T> ApplySorting(IEnumerable<T> items, ColumnFilter filter)
        {
            var propertyInfo = typeof(T).GetProperty(filter.PropertyName);
            if (propertyInfo == null)
                return items;

            // Create a comparer that handles nulls and different types
            var comparer = Comparer<object>.Create((x, y) =>
            {
                // Handle nulls
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                // Try to compare as IComparable
                if (x is IComparable comparableX)
                {
                    try
                    {
                        return comparableX.CompareTo(y);
                    }
                    catch
                    {
                        // If comparison fails, fall back to string comparison
                        return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                // Fall back to string comparison
                return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
            });

            if (filter.SortDirection == SortDirection.Ascending)
            {
                return items.OrderBy(item => GetPropertyValue(item, filter.PropertyName), comparer);
            }
            else if (filter.SortDirection == SortDirection.Descending)
            {
                return items.OrderByDescending(item => GetPropertyValue(item, filter.PropertyName), comparer);
            }

            return items;
        }

        /// <summary>
        /// Get property value from an object using reflection
        /// </summary>
        private object GetPropertyValue(T item, string propertyName)
        {
            try
            {
                var propertyInfo = typeof(T).GetProperty(propertyName);
                if (propertyInfo == null)
                    return null;

                var value = propertyInfo.GetValue(item);

                // Handle nullable types
                if (value != null && propertyInfo.PropertyType.IsGenericType &&
                    propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return value;
                }

                return value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clear all filters
        /// </summary>
        public void ClearAllFilters()
        {
            foreach (var filter in _columnFilters.Values)
            {
                filter.Clear();
            }
            ApplyFilters();
        }

        /// <summary>
        /// Check if any filters are active
        /// </summary>
        public bool HasActiveFilters()
        {
            return _columnFilters.Values.Any(f => f.IsActive);
        }
    }
}
