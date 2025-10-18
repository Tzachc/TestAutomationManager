using System;
using System.Linq;

namespace TestAutomationManager
{
    /// <summary>
    /// Simple search helper with fuzzy matching
    /// </summary>
    public static class SearchHelper
    {
        /// <summary>
        /// Check if text matches search query (fuzzy matching)
        /// </summary>
        public static bool Matches(string text, string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return true;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.ToLowerInvariant();
            searchQuery = searchQuery.ToLowerInvariant();

            // Exact match
            if (text.Contains(searchQuery))
                return true;

            // Multi-word search (all words must match)
            var searchWords = searchQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchWords.Length > 1)
            {
                return searchWords.All(word => text.Contains(word));
            }

            // Fuzzy match (allow 1-2 character differences)
            if (searchQuery.Length >= 4)
            {
                int distance = LevenshteinDistance(searchQuery, text);
                int maxDistance = searchQuery.Length <= 6 ? 1 : 2;

                // Check if similar enough
                if (distance <= maxDistance)
                    return true;

                // Check if any word in text is similar
                var textWords = text.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in textWords)
                {
                    if (LevenshteinDistance(searchQuery, word) <= maxDistance)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if ID matches search query (supports #1, #123, or just 1, 123)
        /// </summary>
        public static bool MatchesId(int id, string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return true;

            searchQuery = searchQuery.Trim();

            // Handle "#1" format
            if (searchQuery.StartsWith("#"))
            {
                string numberPart = searchQuery.Substring(1);
                if (int.TryParse(numberPart, out int searchId))
                {
                    return id == searchId;
                }
            }

            // Handle just number "1" format
            if (int.TryParse(searchQuery, out int directId))
            {
                return id == directId;
            }

            // Also match if ID contains the search as string (for partial matches like "1" matching 10, 11, etc.)
            return id.ToString().Contains(searchQuery);
        }

        /// <summary>
        /// Calculate similarity between two strings (Levenshtein distance)
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            // Only compare up to reasonable length for performance
            int maxLength = Math.Min(Math.Min(source.Length, target.Length), 50);
            source = source.Substring(0, Math.Min(source.Length, maxLength));
            target = target.Substring(0, Math.Min(target.Length, maxLength));

            int sourceLength = source.Length;
            int targetLength = target.Length;

            var matrix = new int[sourceLength + 1, targetLength + 1];

            for (int i = 0; i <= sourceLength; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= targetLength; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost
                    );
                }
            }

            return matrix[sourceLength, targetLength];
        }
    }
}