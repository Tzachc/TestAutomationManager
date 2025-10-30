using System;
using System.Configuration;

namespace TestAutomationManager.Data
{
    /// <summary>
    /// Dynamic schema configuration for handling different database schemas
    /// Allows easy switching between schemas (PRODUCTION_Selenium, QA_Selenium, etc.)
    /// </summary>
    public static class SchemaConfig
    {
        // ================================================
        // CURRENT SCHEMA CONFIGURATION
        // ================================================

        /// <summary>
        /// Current active schema name
        /// Change this to switch to a different schema (e.g., "QA_Selenium", "DEV_Selenium")
        /// </summary>
        private static string _currentSchema = "SeleniumDB";
        //private static string _currentSchema = "SeleniumDB";
        /// <summary>
        /// Get or set the current schema name
        /// </summary>
        public static string CurrentSchema
        {
            get
            {
                // Try to read from App.config first
                try
                {
                    string configSchema = ConfigurationManager.AppSettings["DatabaseSchema"];
                    if (!string.IsNullOrEmpty(configSchema))
                    {
                        return configSchema;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Could not read schema from config: {ex.Message}");
                }

                return _currentSchema;
            }
            set
            {
                _currentSchema = value;
                System.Diagnostics.Debug.WriteLine($"✓ Schema changed to: {_currentSchema}");
            }
        }

        // ================================================
        // TABLE NAME MAPPINGS
        // ================================================

        /// <summary>
        /// Get the Tests table name for the current schema
        /// Can be overridden via App.config with key "TestsTableName"
        /// </summary>
        public static string TestsTable
        {
            get
            {
                try
                {
                    string configName = ConfigurationManager.AppSettings["TestsTableName"];
                    if (!string.IsNullOrEmpty(configName))
                        return configName;
                }
                catch { }

                return "Test_WEB3";
            }
        }

        /// <summary>
        /// Get the Processes table name for the current schema
        /// Can be overridden via App.config with key "ProcessesTableName"
        /// </summary>
        public static string ProcessesTable
        {
            get
            {
                try
                {
                    string configName = ConfigurationManager.AppSettings["ProcessesTableName"];
                    if (!string.IsNullOrEmpty(configName))
                        return configName;
                }
                catch { }

                return "Process_WEB3";
            }
        }

        /// <summary>
        /// Get the Functions table name for the current schema
        /// Can be overridden via App.config with key "FunctionsTableName"
        /// </summary>
        public static string FunctionsTable
        {
            get
            {
                try
                {
                    string configName = ConfigurationManager.AppSettings["FunctionsTableName"];
                    if (!string.IsNullOrEmpty(configName))
                        return configName;
                }
                catch { }

                return "Function_WEB3";
            }
        }

        /// <summary>
        /// Get the prefix for ExtTable tables (e.g., "ExtTest" or "ExtTable")
        /// Can be overridden via App.config with key "ExtTablePrefix"
        /// </summary>
        public static string ExtTablePrefix
        {
            get
            {
                try
                {
                    string configPrefix = ConfigurationManager.AppSettings["ExtTablePrefix"];
                    if (!string.IsNullOrEmpty(configPrefix))
                        return configPrefix;
                }
                catch { }

                return "ExtTest";
            }
        }

        // ================================================
        // FULLY QUALIFIED TABLE NAMES
        // ================================================

        /// <summary>
        /// Get fully qualified Tests table name: [PRODUCTION_Selenium].[Test_WEB3]
        /// </summary>
        public static string GetTestsTableFullName()
        {
            return $"[{CurrentSchema}].[{TestsTable}]";
        }

        /// <summary>
        /// Get fully qualified Processes table name: [PRODUCTION_Selenium].[Process_WEB3]
        /// </summary>
        public static string GetProcessesTableFullName()
        {
            return $"[{CurrentSchema}].[{ProcessesTable}]";
        }

        /// <summary>
        /// Get fully qualified Functions table name: [PRODUCTION_Selenium].[Function_WEB3]
        /// </summary>
        public static string GetFunctionsTableFullName()
        {
            return $"[{CurrentSchema}].[{FunctionsTable}]";
        }

        /// <summary>
        /// Get fully qualified ExtTable name: [PRODUCTION_Selenium].[ExtTest1]
        /// </summary>
        /// <param name="tableNumber">Table number (e.g., 1 for ExtTest1)</param>
        public static string GetExtTableFullName(int tableNumber)
        {
            return $"[{CurrentSchema}].[{ExtTablePrefix}{tableNumber}]";
        }

        /// <summary>
        /// Get fully qualified ExtTable name: [PRODUCTION_Selenium].[ExtTest1]
        /// </summary>
        /// <param name="tableName">Table name without schema (e.g., "ExtTest1")</param>
        public static string GetExtTableFullName(string tableName)
        {
            return $"[{CurrentSchema}].[{tableName}]";
        }

        // ================================================
        // HELPER METHODS
        // ================================================

        /// <summary>
        /// Extract test ID from ExtTable name
        /// Examples: "ExtTest1" -> 1, "ExtTable42" -> 42
        /// </summary>
        public static int? ExtractTestIdFromTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return null;

            // Try to extract number from table name
            // Handle both "ExtTest" and "ExtTable" prefixes
            string numberPart = tableName.Replace("ExtTest", "").Replace("ExtTable", "");

            if (int.TryParse(numberPart, out int testId))
            {
                return testId;
            }

            return null;
        }

        /// <summary>
        /// Build ExtTable name from test ID
        /// Example: 1 -> "ExtTest1"
        /// </summary>
        public static string BuildExtTableName(int testId)
        {
            return $"{ExtTablePrefix}{testId}";
        }

        /// <summary>
        /// Check if a table name is an ExtTable
        /// </summary>
        public static bool IsExtTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return false;

            return tableName.StartsWith(ExtTablePrefix, StringComparison.OrdinalIgnoreCase) ||
                   tableName.StartsWith("ExtTable", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Log current configuration (useful for debugging)
        /// </summary>
        public static void LogConfiguration()
        {
            System.Diagnostics.Debug.WriteLine("=== Schema Configuration ===");
            System.Diagnostics.Debug.WriteLine($"Schema: {CurrentSchema}");
            System.Diagnostics.Debug.WriteLine($"Tests Table: {TestsTable}");
            System.Diagnostics.Debug.WriteLine($"Processes Table: {ProcessesTable}");
            System.Diagnostics.Debug.WriteLine($"Functions Table: {FunctionsTable}");
            System.Diagnostics.Debug.WriteLine($"ExtTable Prefix: {ExtTablePrefix}");
            System.Diagnostics.Debug.WriteLine($"Full Tests Path: {GetTestsTableFullName()}");
            System.Diagnostics.Debug.WriteLine($"Full Processes Path: {GetProcessesTableFullName()}");
            System.Diagnostics.Debug.WriteLine($"Full Functions Path: {GetFunctionsTableFullName()}");
            System.Diagnostics.Debug.WriteLine($"Sample ExtTable: {GetExtTableFullName(1)}");
            System.Diagnostics.Debug.WriteLine("============================");
        }
    }
}