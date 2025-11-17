using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TestAutomationManager.Data;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Manages the current database connection for the application
    /// Allows switching between live database and preview/backup databases
    /// </summary>
    public class DatabaseConnectionService : INotifyPropertyChanged
    {
        private static DatabaseConnectionService _instance;
        private static readonly object _lock = new object();

        private string _activeConnectionString;
        private string _activeDatabaseName;
        private bool _isPreviewMode;
        private DateTime? _previewBackupDate;
        private string _previewBackupFile;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DatabaseConnectionChangedEventArgs> ConnectionChanged;

        public static DatabaseConnectionService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseConnectionService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DatabaseConnectionService()
        {
            // Initialize with live database connection
            _activeConnectionString = DbConnectionConfig.GetConnectionString();
            _activeDatabaseName = GetDatabaseNameFromConnectionString(_activeConnectionString);
            _isPreviewMode = false;

            System.Diagnostics.Debug.WriteLine($"✓ DatabaseConnectionService initialized with: {_activeDatabaseName}");
        }

        /// <summary>
        /// Gets the currently active connection string
        /// </summary>
        public string ActiveConnectionString => _activeConnectionString;

        /// <summary>
        /// Gets the currently active database name
        /// </summary>
        public string ActiveDatabaseName => _activeDatabaseName;

        /// <summary>
        /// Indicates if currently in preview mode
        /// </summary>
        public bool IsPreviewMode
        {
            get => _isPreviewMode;
            private set
            {
                if (_isPreviewMode != value)
                {
                    _isPreviewMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the backup date/time when in preview mode
        /// </summary>
        public DateTime? PreviewBackupDate => _previewBackupDate;

        /// <summary>
        /// Gets the backup file name when in preview mode
        /// </summary>
        public string PreviewBackupFile => _previewBackupFile;

        /// <summary>
        /// Gets the preview mode display text
        /// </summary>
        public string PreviewModeText
        {
            get
            {
                if (!IsPreviewMode || !_previewBackupDate.HasValue)
                    return string.Empty;

                return $"🔵 PREVIEW MODE - Backup from {_previewBackupDate:yyyy-MM-dd HH:mm:ss}";
            }
        }

        /// <summary>
        /// Switch to preview database
        /// </summary>
        public void SwitchToPreviewDatabase(string previewDatabaseName, DateTime backupDate, string backupFileName)
        {
            var previousConnection = _activeConnectionString;
            var previousDatabase = _activeDatabaseName;

            // Build connection string for preview database
            _activeConnectionString = BuildConnectionStringForDatabase(previewDatabaseName);
            _activeDatabaseName = previewDatabaseName;
            _isPreviewMode = true;
            _previewBackupDate = backupDate;
            _previewBackupFile = backupFileName;

            OnPropertyChanged(nameof(ActiveConnectionString));
            OnPropertyChanged(nameof(ActiveDatabaseName));
            OnPropertyChanged(nameof(PreviewBackupDate));
            OnPropertyChanged(nameof(PreviewBackupFile));
            OnPropertyChanged(nameof(PreviewModeText));

            ConnectionChanged?.Invoke(this, new DatabaseConnectionChangedEventArgs
            {
                PreviousConnection = previousConnection,
                PreviousDatabase = previousDatabase,
                NewConnection = _activeConnectionString,
                NewDatabase = _activeDatabaseName,
                IsPreviewMode = true
            });

            System.Diagnostics.Debug.WriteLine($"✓ Switched to preview database: {_activeDatabaseName}");
        }

        /// <summary>
        /// Switch back to live database
        /// </summary>
        public void SwitchToLiveDatabase()
        {
            if (!_isPreviewMode)
                return; // Already on live

            var previousConnection = _activeConnectionString;
            var previousDatabase = _activeDatabaseName;

            // Reset to live database connection
            _activeConnectionString = DbConnectionConfig.GetConnectionString();
            _activeDatabaseName = GetDatabaseNameFromConnectionString(_activeConnectionString);
            _isPreviewMode = false;
            _previewBackupDate = null;
            _previewBackupFile = null;

            OnPropertyChanged(nameof(ActiveConnectionString));
            OnPropertyChanged(nameof(ActiveDatabaseName));
            OnPropertyChanged(nameof(IsPreviewMode));
            OnPropertyChanged(nameof(PreviewBackupDate));
            OnPropertyChanged(nameof(PreviewBackupFile));
            OnPropertyChanged(nameof(PreviewModeText));

            ConnectionChanged?.Invoke(this, new DatabaseConnectionChangedEventArgs
            {
                PreviousConnection = previousConnection,
                PreviousDatabase = previousDatabase,
                NewConnection = _activeConnectionString,
                NewDatabase = _activeDatabaseName,
                IsPreviewMode = false
            });

            System.Diagnostics.Debug.WriteLine($"✓ Switched back to live database: {_activeDatabaseName}");
        }

        /// <summary>
        /// Get database name from connection string
        /// </summary>
        private string GetDatabaseNameFromConnectionString(string connectionString)
        {
            try
            {
                var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                return builder.InitialCatalog;
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Build connection string for a specific database
        /// </summary>
        private string BuildConnectionStringForDatabase(string databaseName)
        {
            var liveConnectionString = DbConnectionConfig.GetConnectionString();
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(liveConnectionString);
            builder.InitialCatalog = databaseName;
            return builder.ConnectionString;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Event args for database connection changes
    /// </summary>
    public class DatabaseConnectionChangedEventArgs : EventArgs
    {
        public string PreviousConnection { get; set; }
        public string PreviousDatabase { get; set; }
        public string NewConnection { get; set; }
        public string NewDatabase { get; set; }
        public bool IsPreviewMode { get; set; }
    }
}
