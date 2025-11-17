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
                // Get current schema for folder organization
                var currentSchema = SchemaConfigService.Instance.CurrentSchema;

                System.Diagnostics.Debug.WriteLine($"Starting scheduled backup for database (current schema: {currentSchema})...");
                BackupProgress?.Invoke(this, $"Starting database backup");

                var connectionString = DbConnectionConfig.GetConnectionString();

                var progress = new Progress<string>(msg =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Backup] {msg}");
                    BackupProgress?.Invoke(this, $"[Backup] {msg}");
                });

                // Backup the database once, organized by current schema folder
                var result = await _backupService.CreateBackupAsync(currentSchema, connectionString, progress);

                if (result.Success)
                {
                    _lastBackupTime = DateTime.Now;
                    _nextBackupTime = DateTime.Now.AddMinutes(_intervalMinutes);

                    BackupCompleted?.Invoke(this, new BackupSchedulerEventArgs
                    {
                        Schema = currentSchema,
                        Success = true,
                        BackupFilePath = result.FilePath,
                        Timestamp = result.Timestamp
                    });

                    System.Diagnostics.Debug.WriteLine($"Backup completed: {result.FileName}");
                }
                else
                {
                    BackupFailed?.Invoke(this, new BackupSchedulerEventArgs
                    {
                        Schema = currentSchema,
                        Success = false,
                        ErrorMessage = result.ErrorMessage
                    });

                    System.Diagnostics.Debug.WriteLine($"Backup failed: {result.ErrorMessage}");
                }

                System.Diagnostics.Debug.WriteLine("Scheduled backup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PerformScheduledBackupAsync: {ex.Message}");
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
