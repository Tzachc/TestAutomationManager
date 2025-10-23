using System;

namespace TestAutomationManager.Views
{
    /// <summary>
    /// Helper class for search and filtering functionality
    /// </summary>
    public static class SearchHelper
    {
        /// <summary>
        /// Check if a string matches the search query (case-insensitive)
        /// </summary>
        public static bool Matches(string value, string searchQuery)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(searchQuery))
                return false;

            return value.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if an ID matches the search query
        /// Supports searching by "#123" or just "123"
        /// </summary>
        public static bool MatchesId(int id, string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
                return false;

            // Remove # if present
            string cleanQuery = searchQuery.TrimStart('#');

            // Check if query is numeric and matches ID
            if (int.TryParse(cleanQuery, out int searchId))
            {
                return id == searchId;
            }

            // Also check if ID contains the search digits
            return id.ToString().Contains(cleanQuery);
        }
    }
}