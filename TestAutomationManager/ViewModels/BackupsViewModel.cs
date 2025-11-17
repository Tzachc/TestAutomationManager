using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TestAutomationManager.Data;
using TestAutomationManager.Services;

namespace TestAutomationManager.ViewModels
{
    public class BackupsViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseBackupService _backupService;
        private readonly BackupSchedulerService _schedulerService;
        private ObservableCollection<BackupGroupViewModel> _backupGroups;
        private BackupItemViewModel _selectedBackup;
        private string _statusMessage;
        private string _progressMessage;
        private bool _isLoading;
        private bool _isRestoring;
        private string _schedulerStatus;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<BackupGroupViewModel> BackupGroups
        {
            get => _backupGroups;
            set { _backupGroups = value; OnPropertyChanged(); }
        }

        public BackupItemViewModel SelectedBackup
        {
            get => _selectedBackup;
            set { _selectedBackup = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set { _progressMessage = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool IsRestoring
        {
            get => _isRestoring;
            set { _isRestoring = value; OnPropertyChanged(); }
        }

        public string SchedulerStatus
        {
            get => _schedulerStatus;
            set { _schedulerStatus = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestorePreviewCommand { get; }
        public ICommand ApplyToRealDbCommand { get; }
        public ICommand DeleteBackupCommand { get; }
        public ICommand TriggerManualBackupCommand { get; }
        public ICommand ReturnToLiveCommand { get; }

        public event EventHandler<DatabaseConnectionChangedEventArgs> PreviewModeActivated;

        public BackupsViewModel()
        {
            _backupService = DatabaseBackupService.Instance;
            _schedulerService = BackupSchedulerService.Instance;

            BackupGroups = new ObservableCollection<BackupGroupViewModel>();

            RefreshCommand = new RelayCommand(async () => await LoadBackupsAsync());
            CreateBackupCommand = new RelayCommand(async () => await CreateManualBackupAsync());
            RestorePreviewCommand = new RelayCommand(async () => await RestoreBackupAsync(false), () => SelectedBackup != null);
            ApplyToRealDbCommand = new RelayCommand(async () => await RestoreBackupAsync(true), () => SelectedBackup != null);
            DeleteBackupCommand = new RelayCommand(DeleteSelectedBackup, () => SelectedBackup != null);
            TriggerManualBackupCommand = new RelayCommand(async () => await TriggerManualBackupAsync());
            ReturnToLiveCommand = new RelayCommand(ReturnToLive, () => DatabaseConnectionService.Instance.IsPreviewMode);

            // Subscribe to scheduler events
            _schedulerService.BackupCompleted += OnBackupCompleted;
            _schedulerService.BackupFailed += OnBackupFailed;
            _schedulerService.BackupProgress += OnBackupProgress;

            UpdateSchedulerStatus();
        }

        public async Task LoadBackupsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading backups...";

            try
            {
                await Task.Run(() =>
                {
                    var backups = _backupService.GetAvailableBackups();

                    var groups = backups
                        .GroupBy(b => b.Schema)
                        .Select(g => new BackupGroupViewModel
                        {
                            SchemaName = g.Key,
                            Backups = new ObservableCollection<BackupItemViewModel>(
                                g.Select(b => new BackupItemViewModel
                                {
                                    Schema = b.Schema,
                                    FileName = b.FileName,
                                    FilePath = b.FilePath,
                                    CreatedDate = b.CreatedDate,
                                    SizeFormatted = b.SizeFormatted,
                                    SizeInBytes = b.SizeInBytes
                                }).OrderByDescending(b => b.CreatedDate)
                            )
                        })
                        .OrderBy(g => g.SchemaName)
                        .ToList();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        BackupGroups.Clear();
                        foreach (var group in groups)
                        {
                            BackupGroups.Add(group);
                        }
                    });
                });

                StatusMessage = $"Found {BackupGroups.Sum(g => g.Backups.Count)} backup(s) across {BackupGroups.Count} schema(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading backups: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateManualBackupAsync()
        {
            IsLoading = true;
            StatusMessage = "Creating backup...";

            try
            {
                var currentSchema = SchemaConfigService.Instance.CurrentSchema;
                var connectionString = DbConnectionConfig.GetConnectionString();

                var progress = new Progress<string>(msg =>
                {
                    ProgressMessage = msg;
                });

                var result = await _backupService.CreateBackupAsync(currentSchema, connectionString, progress);

                if (result.Success)
                {
                    StatusMessage = $"Backup created successfully: {result.FileName}";
                    await LoadBackupsAsync();
                }
                else
                {
                    StatusMessage = $"Backup failed: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating backup: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                ProgressMessage = string.Empty;
            }
        }

        private async Task RestoreBackupAsync(bool applyToRealDb)
        {
            if (SelectedBackup == null)
                return;

            var action = applyToRealDb ? "restore to live database" : "preview";
            var confirmMessage = applyToRealDb
                ? $"Are you sure you want to REPLACE the live database with this backup?\n\nBackup: {SelectedBackup.FileName}\nCreated: {SelectedBackup.CreatedDate:yyyy-MM-dd HH:mm:ss}\n\nTHIS WILL OVERWRITE ALL CURRENT DATA!"
                : $"This will create a preview database with data from:\n\nBackup: {SelectedBackup.FileName}\nCreated: {SelectedBackup.CreatedDate:yyyy-MM-dd HH:mm:ss}\n\nYour live database will not be affected.";

            var result = MessageBox.Show(
                confirmMessage,
                $"Confirm {(applyToRealDb ? "Restore" : "Preview")}",
                MessageBoxButton.YesNo,
                applyToRealDb ? MessageBoxImage.Warning : MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            IsRestoring = true;
            StatusMessage = $"Restoring backup ({action})...";

            try
            {
                var connectionString = DbConnectionConfig.GetConnectionString();

                var progress = new Progress<string>(msg =>
                {
                    ProgressMessage = msg;
                });

                var restoreResult = await _backupService.RestoreBackupAsync(
                    SelectedBackup.FilePath,
                    connectionString,
                    applyToRealDb,
                    progress
                );

                if (restoreResult.Success)
                {
                    if (applyToRealDb)
                    {
                        StatusMessage = "Database restored successfully! Application will reload...";

                        // Switch back to live database and reload
                        DatabaseConnectionService.Instance.SwitchToLiveDatabase();

                        MessageBox.Show(
                            "Database has been restored successfully!\n\nThe application will now reload with the restored data.",
                            "Restore Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );

                        // Notify to reload app
                        PreviewModeActivated?.Invoke(this, new DatabaseConnectionChangedEventArgs
                        {
                            NewDatabase = restoreResult.RestoredDatabase,
                            IsPreviewMode = false
                        });
                    }
                    else
                    {
                        StatusMessage = $"Preview window opening...";

                        // Launch a new app window with the preview database
                        try
                        {
                            var exePath = Process.GetCurrentProcess().MainModule.FileName;
                            var previewArgs = $"/database:{restoreResult.RestoredDatabase} /preview:true /backupdate:\"{SelectedBackup.CreatedDate:yyyy-MM-dd HH:mm:ss}\"";

                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                Arguments = previewArgs,
                                UseShellExecute = true
                            });

                            StatusMessage = "Preview window launched successfully";

                            MessageBox.Show(
                                $"Preview window opened!\n\nA new window has been launched with backup data from:\n{SelectedBackup.CreatedDate:yyyy-MM-dd HH:mm:ss}\n\nYour current window shows live data.\nThe new window shows the backup preview.",
                                "Preview Window Launched",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                        catch (Exception launchEx)
                        {
                            StatusMessage = $"Failed to launch preview window: {launchEx.Message}";
                            MessageBox.Show(
                                $"Failed to launch preview window:\n\n{launchEx.Message}",
                                "Launch Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        }
                    }
                }
                else
                {
                    StatusMessage = $"Restore failed: {restoreResult.ErrorMessage}";
                    MessageBox.Show(
                        $"Failed to restore backup:\n\n{restoreResult.ErrorMessage}",
                        "Restore Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restoring backup: {ex.Message}";
                MessageBox.Show(
                    $"An error occurred while restoring the backup:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                IsRestoring = false;
                ProgressMessage = string.Empty;
            }
        }

        private void DeleteSelectedBackup()
        {
            if (SelectedBackup == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete this backup?\n\n{SelectedBackup.FileName}\nCreated: {SelectedBackup.CreatedDate:yyyy-MM-dd HH:mm:ss}",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (_backupService.DeleteBackup(SelectedBackup.FilePath))
                {
                    StatusMessage = $"Backup deleted: {SelectedBackup.FileName}";
                    _ = LoadBackupsAsync();
                }
                else
                {
                    StatusMessage = "Failed to delete backup";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting backup: {ex.Message}";
            }
        }

        private async Task TriggerManualBackupAsync()
        {
            IsLoading = true;
            StatusMessage = "Triggering manual backup for all schemas...";

            try
            {
                await _schedulerService.TriggerManualBackupAsync();
                StatusMessage = "Manual backup triggered successfully";
                await Task.Delay(2000); // Give it time to complete
                await LoadBackupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error triggering backup: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ReturnToLive()
        {
            var result = MessageBox.Show(
                "Return to live database?\n\nThis will close preview mode and switch back to the live database.",
                "Return to Live Database",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            // Switch back to live database
            DatabaseConnectionService.Instance.SwitchToLiveDatabase();

            StatusMessage = "Returned to live database. Application will reload...";

            // Notify to reload app
            PreviewModeActivated?.Invoke(this, new DatabaseConnectionChangedEventArgs
            {
                NewDatabase = DatabaseConnectionService.Instance.ActiveDatabaseName,
                IsPreviewMode = false
            });
        }

        private void OnBackupCompleted(object sender, BackupSchedulerEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"Automatic backup completed for {e.Schema}";
                _ = LoadBackupsAsync();
                UpdateSchedulerStatus();
            });
        }

        private void OnBackupFailed(object sender, BackupSchedulerEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"Automatic backup failed for {e.Schema}: {e.ErrorMessage}";
                UpdateSchedulerStatus();
            });
        }

        private void OnBackupProgress(object sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressMessage = message;
            });
        }

        private void UpdateSchedulerStatus()
        {
            SchedulerStatus = _schedulerService.GetStatusMessage();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #region View Models for UI

    public class BackupGroupViewModel : INotifyPropertyChanged
    {
        private string _schemaName;
        private ObservableCollection<BackupItemViewModel> _backups;
        private bool _isExpanded = true;

        public string SchemaName
        {
            get => _schemaName;
            set { _schemaName = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BackupItemViewModel> Backups
        {
            get => _backups;
            set { _backups = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public int BackupCount => Backups?.Count ?? 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BackupItemViewModel : INotifyPropertyChanged
    {
        private string _schema;
        private string _fileName;
        private string _filePath;
        private DateTime _createdDate;
        private string _sizeFormatted;
        private long _sizeInBytes;

        public string Schema
        {
            get => _schema;
            set { _schema = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(); }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set { _createdDate = value; OnPropertyChanged(); }
        }

        public string SizeFormatted
        {
            get => _sizeFormatted;
            set { _sizeFormatted = value; OnPropertyChanged(); }
        }

        public long SizeInBytes
        {
            get => _sizeInBytes;
            set { _sizeInBytes = value; OnPropertyChanged(); }
        }

        public string DisplayText => $"{FileName} ({SizeFormatted})";
        public string DateTimeDisplay => CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion

    #region RelayCommand Helper

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }

    #endregion
}
