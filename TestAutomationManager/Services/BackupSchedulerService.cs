using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TestAutomationManager.Data;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Service for scheduling automatic database backups at configurable intervals
    /// </summary>
    public class BackupSchedulerService
    {
        private static BackupSchedulerService _instance;
        private static readonly object _lock = new object();
        private Timer _backupTimer;
        private readonly int _intervalMinutes;
        private readonly DatabaseBackupService _backupService;
        private bool _isRunning;
        private DateTime? _lastBackupTime;
        private DateTime? _nextBackupTime;

        public event EventHandler<BackupSchedulerEventArgs> BackupCompleted;
        public event EventHandler<BackupSchedulerEventArgs> BackupFailed;
        public event EventHandler<string> BackupProgress;

        public bool IsRunning => _isRunning;
        public DateTime? LastBackupTime => _lastBackupTime;
        public DateTime? NextBackupTime => _nextBackupTime;

        public static BackupSchedulerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new BackupSchedulerService();
                        }
                    }
                }
                return _instance;
            }
        }

        private BackupSchedulerService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            _intervalMinutes = int.TryParse(config["BackupSettings:IntervalMinutes"], out var minutes) ? minutes : 60;
            _backupService = DatabaseBackupService.Instance;
        }

        /// <summary>
        /// Starts the automatic backup scheduler
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            var intervalMs = _intervalMinutes * 60 * 1000;

            // Start timer - first backup after 1 minute, then at configured intervals
            _backupTimer = new Timer(
                async _ => await PerformScheduledBackupAsync(),
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMilliseconds(intervalMs)
            );

            _nextBackupTime = DateTime.Now.AddMinutes(1);
            System.Diagnostics.Debug.WriteLine($"BackupSchedulerService started. Backups will run every {_intervalMinutes} minutes.");
        }

        /// <summary>
        /// Stops the automatic backup scheduler
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _backupTimer?.Dispose();
            _backupTimer = null;
            _isRunning = false;
            _nextBackupTime = null;

            System.Diagnostics.Debug.WriteLine("BackupSchedulerService stopped.");
        }

        /// <summary>
        /// Manually trigger a backup immediately
        /// </summary>
        public async Task TriggerManualBackupAsync()
        {
            await PerformScheduledBackupAsync();
        }

        /// <summary>
        /// Performs the scheduled backup operation
        /// </summary>
        private async Task PerformScheduledBackupAsync()
        {
            try
            {
                // Get all schemas that need backing up
                var schemas = GetActiveSchemas();

                if (!schemas.Any())
                {
                    System.Diagnostics.Debug.WriteLine("No schemas found for backup");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Starting scheduled backup for {schemas.Count} schema(s)...");
                BackupProgress?.Invoke(this, $"Starting backup for {schemas.Count} schema(s)");

                foreach (var schema in schemas)
                {
                    try
                    {
                        var connectionString = GetConnectionStringForSchema(schema);

                        var progress = new Progress<string>(msg =>
                        {
                            System.Diagnostics.Debug.WriteLine($"[{schema}] {msg}");
                            BackupProgress?.Invoke(this, $"[{schema}] {msg}");
                        });

                        var result = await _backupService.CreateBackupAsync(schema, connectionString, progress);

                        if (result.Success)
                        {
                            _lastBackupTime = DateTime.Now;
                            _nextBackupTime = DateTime.Now.AddMinutes(_intervalMinutes);

                            BackupCompleted?.Invoke(this, new BackupSchedulerEventArgs
                            {
                                Schema = schema,
                                Success = true,
                                BackupFilePath = result.FilePath,
                                Timestamp = result.Timestamp
                            });

                            System.Diagnostics.Debug.WriteLine($"Backup completed for {schema}: {result.FileName}");
                        }
                        else
                        {
                            BackupFailed?.Invoke(this, new BackupSchedulerEventArgs
                            {
                                Schema = schema,
                                Success = false,
                                ErrorMessage = result.ErrorMessage
                            });

                            System.Diagnostics.Debug.WriteLine($"Backup failed for {schema}: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error backing up {schema}: {ex.Message}");
                        BackupFailed?.Invoke(this, new BackupSchedulerEventArgs
                        {
                            Schema = schema,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine("Scheduled backup completed for all schemas");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PerformScheduledBackupAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets list of active schemas that need backing up
        /// </summary>
        private List<string> GetActiveSchemas()
        {
            var schemas = new List<string>();

            try
            {
                // Get configured schemas from SchemaConfigService
                var currentSchema = SchemaConfigService.Instance.CurrentSchemaName;

                // Always add the current schema
                if (!string.IsNullOrEmpty(currentSchema))
                {
                    schemas.Add(currentSchema);
                }

                // Add other known schemas (you can make this more dynamic later)
                var knownSchemas = new[] { "SeleniumDB", "PRODUCTION_Selenium" };
                foreach (var schema in knownSchemas)
                {
                    if (!schemas.Contains(schema, StringComparer.OrdinalIgnoreCase))
                    {
                        schemas.Add(schema);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting schemas: {ex.Message}");
            }

            return schemas;
        }

        /// <summary>
        /// Gets connection string for a specific schema
        /// </summary>
        private string GetConnectionStringForSchema(string schema)
        {
            try
            {
                // Use DbConnectionConfig to build connection string
                var config = DbConnectionConfig.Instance;

                // Temporarily switch to target schema to get its connection string
                var originalSchema = SchemaConfigService.Instance.CurrentSchemaName;
                SchemaConfigService.Instance.CurrentSchemaName = schema;

                var connectionString = config.GetConnectionString();

                // Restore original schema
                SchemaConfigService.Instance.CurrentSchemaName = originalSchema;

                return connectionString;
            }
            catch
            {
                // Fallback to default connection string
                return DbConnectionConfig.Instance.GetConnectionString();
            }
        }

        /// <summary>
        /// Gets a formatted status message
        /// </summary>
        public string GetStatusMessage()
        {
            if (!_isRunning)
                return "Backup scheduler is stopped";

            if (_lastBackupTime.HasValue && _nextBackupTime.HasValue)
            {
                return $"Last backup: {_lastBackupTime:yyyy-MM-dd HH:mm:ss} | Next backup: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}";
            }

            if (_nextBackupTime.HasValue)
            {
                return $"Next backup scheduled for: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}";
            }

            return "Backup scheduler is running";
        }
    }

    /// <summary>
    /// Event arguments for backup scheduler events
    /// </summary>
    public class BackupSchedulerEventArgs : EventArgs
    {
        public string Schema { get; set; }
        public bool Success { get; set; }
        public string BackupFilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public string ErrorMessage { get; set; }
    }
}
