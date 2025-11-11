using System;
using System.Text.RegularExpressions;

namespace TestAutomationManager.Helpers
{
    /// <summary>
    /// Helper class for detecting and parsing parameter navigation patterns
    /// Supports:
    /// - From_ExtTest_columnName: Navigate to ExtTest table column
    /// - From_Process_paramName: Navigate to Process parameter
    /// </summary>
    public static class ParameterNavigationHelper
    {
        private const string ExtTestPattern = @"^From_ExtTest_(.+)$";
        private const string ProcessPattern = @"^From_Process_(.+)$";

        /// <summary>
        /// Represents a navigation target parsed from a parameter value
        /// </summary>
        public class NavigationTarget
        {
            public NavigationType Type { get; set; }
            public string TargetName { get; set; }

            public NavigationTarget(NavigationType type, string targetName)
            {
                Type = type;
                TargetName = targetName;
            }
        }

        /// <summary>
        /// Types of navigation supported
        /// </summary>
        public enum NavigationType
        {
            None,
            ExtTest,
            Process
        }

        /// <summary>
        /// Detects if a parameter value contains a navigation pattern
        /// </summary>
        /// <param name="parameterValue">The parameter value to check</param>
        /// <returns>True if a navigation pattern is detected</returns>
        public static bool IsNavigationPattern(string parameterValue)
        {
            if (string.IsNullOrWhiteSpace(parameterValue))
                return false;

            return Regex.IsMatch(parameterValue, ExtTestPattern) ||
                   Regex.IsMatch(parameterValue, ProcessPattern);
        }

        /// <summary>
        /// Parses a parameter value to extract navigation target information
        /// </summary>
        /// <param name="parameterValue">The parameter value to parse</param>
        /// <returns>NavigationTarget if pattern is found, null otherwise</returns>
        public static NavigationTarget? ParseNavigationTarget(string parameterValue)
        {
            if (string.IsNullOrWhiteSpace(parameterValue))
                return null;

            // Check for From_ExtTest_ pattern
            var extTestMatch = Regex.Match(parameterValue, ExtTestPattern);
            if (extTestMatch.Success)
            {
                var columnName = extTestMatch.Groups[1].Value;
                return new NavigationTarget(NavigationType.ExtTest, columnName);
            }

            // Check for From_Process_ pattern
            var processMatch = Regex.Match(parameterValue, ProcessPattern);
            if (processMatch.Success)
            {
                var paramName = processMatch.Groups[1].Value;
                return new NavigationTarget(NavigationType.Process, paramName);
            }

            return null;
        }

        /// <summary>
        /// Gets a user-friendly description for the navigation action
        /// </summary>
        /// <param name="target">The navigation target</param>
        /// <returns>Description string for the context menu</returns>
        public static string GetNavigationDescription(NavigationTarget target)
        {
            switch (target.Type)
            {
                case NavigationType.ExtTest:
                    return $"Go to ExtTest → {target.TargetName}";
                case NavigationType.Process:
                    return $"Go to Process → {target.TargetName}";
                default:
                    return "Go to";
            }
        }
    }
}
