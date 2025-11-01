using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Manages the current schema configuration for the application.
    /// Allows users to switch between different schemas (e.g., PRODUCTION_Selenium, TEST_Selenium, etc.)
    /// </summary>
    public class SchemaConfigService : INotifyPropertyChanged
    {
        private static SchemaConfigService _instance;
        private static readonly object _lock = new object();

        private string _currentSchema;

        // ================================================
        // NEW: AVAILABLE SCHEMAS
        // ================================================

        /// <summary>
        /// Gets the list of available schemas.
        /// This can be expanded in the future.
        /// </summary>
        public static List<string> AvailableSchemas { get; } = new List<string>
        {
            "SeleniumDB",
            "PRODUCTION_Selenium"
            // Add more schemas here in the future
        };

        // ================================================
        // SINGLETON INSTANCE
        // ================================================

        /// <summary>
        /// Get singleton instance of SchemaConfigService
        /// </summary>
        public static SchemaConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SchemaConfigService();
                        }
                    }
                }
                return _instance;
            }
        }

        // ================================================
        // CONSTRUCTOR
        // ================================================

        private SchemaConfigService()
        {
            // Default schema
            _currentSchema = "PRODUCTION_Selenium";
            System.Diagnostics.Debug.WriteLine($"✓ SchemaConfigService initialized with schema: {_currentSchema}");
        }

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// Current schema name (e.g., "PRODUCTION_Selenium")
        /// </summary>
        public string CurrentSchema
        {
            get => _currentSchema;
            set
            {
                if (_currentSchema != value && AvailableSchemas.Contains(value))
                {
                    string oldSchema = _currentSchema;
                    _currentSchema = value;
                    OnPropertyChanged();
                    OnSchemaChanged(oldSchema, value);
                    System.Diagnostics.Debug.WriteLine($"✓ Schema changed from '{oldSchema}' to '{value}'");
                }
            }
        }

        // ================================================
        // TABLE NAMES
        // ================================================

        /// <summary>
        /// Get the Test table name for current schema
        /// </summary>
        public string TestTableName => "Test_WEB3";

        /// <summary>
        /// Get the Process table name for current schema
        /// </summary>
        public string ProcessTableName => "Process_WEB3";

        /// <summary>
        /// Get the Function table name for current schema
        /// </summary>
        public string FunctionTableName => "Function_WEB3";

        /// <summary>
        /// Get the ExtTest table name prefix for current schema
        /// </summary>
        public string ExtTestTablePrefix => "ExtTest";

        /// <summary>
        /// Get fully qualified table name
        /// </summary>
        public string GetFullTableName(string tableName)
        {
            return $"[{CurrentSchema}].[{tableName}]";
        }

        /// <summary>
        /// Get ExtTest table name for a specific test ID
        /// </summary>
        public string GetExtTestTableName(int testId)
        {
            return $"{ExtTestTablePrefix}{testId}";
        }

        /// <summary>
        /// Get fully qualified ExtTest table name
        /// </summary>
        public string GetFullExtTestTableName(int testId)
        {
            return $"[{CurrentSchema}].[{GetExtTestTableName(testId)}]";
        }

        // ================================================
        // EVENTS
        // ================================================

        /// <summary>
        /// Event raised when schema changes
        /// </summary>
        public event EventHandler<SchemaChangedEventArgs> SchemaChanged;

        /// <summary>
        /// Raise schema changed event
        /// </summary>
        private void OnSchemaChanged(string oldSchema, string newSchema)
        {
            SchemaChanged?.Invoke(this, new SchemaChangedEventArgs(oldSchema, newSchema));
        }

        // ================================================
        // INOTIFYPROPERTYCHANGED
        // ================================================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Event args for schema change event
    /// </summary>
    public class SchemaChangedEventArgs : EventArgs
    {
        public string OldSchema { get; }
        public string NewSchema { get; }

        public SchemaChangedEventArgs(string oldSchema, string newSchema)
        {
            OldSchema = oldSchema;
            NewSchema = newSchema;
        }
    }
}