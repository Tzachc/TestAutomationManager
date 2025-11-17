using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Service for managing SQL Server database backups with automatic retention and organization
    /// </summary>
    public class DatabaseBackupService
    {
        private static DatabaseBackupService _instance;
        private static readonly object _lock = new object();
        private readonly string _backupRootPath;
        private readonly int _retentionDays;

        public static DatabaseBackupService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseBackupService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DatabaseBackupService()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            _backupRootPath = config["BackupSettings:RootPath"] ?? @"C:\SqlBackups";
            _retentionDays = int.TryParse(config["BackupSettings:RetentionDays"], out var days) ? days : 7;

            // Ensure backup root directory exists
            if (!Directory.Exists(_backupRootPath))
            {
                Directory.CreateDirectory(_backupRootPath);
            }
        }

        /// <summary>
        /// Creates a backup for the specified schema
        /// </summary>
        public async Task<BackupResult> CreateBackupAsync(string schema, string connectionString, IProgress<string> progress = null)
        {
            try
            {
                progress?.Report($"Starting backup for schema: {schema}");

                // Create schema-specific folder
                var schemaFolder = Path.Combine(_backupRootPath, schema);
                if (!Directory.Exists(schemaFolder))
                {
                    Directory.CreateDirectory(schemaFolder);
                }

                // Generate backup file name with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{schema}_Backup_{timestamp}.bak";
                var fullPath = Path.Combine(schemaFolder, fileName);

                progress?.Report($"Creating backup file: {fileName}");

                // Get database name from connection string
                var builder = new SqlConnectionStringBuilder(connectionString);
                var databaseName = builder.InitialCatalog;

                // Execute SQL Server backup command
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var backupCommand = $@"
                        BACKUP DATABASE [{databaseName}]
                        TO DISK = @BackupPath
                        WITH FORMAT,
                        MEDIANAME = '{schema}_Backup',
                        NAME = '{schema} Full Backup - {timestamp}',
                        COMPRESSION,
                        STATS = 10;";

                    using (var command = new SqlCommand(backupCommand, connection))
                    {
                        command.CommandTimeout = 300; // 5 minutes timeout
                        command.Parameters.AddWithValue("@BackupPath", fullPath);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                progress?.Report($"Backup completed successfully");

                // Get file info
                var fileInfo = new FileInfo(fullPath);

                // Clean up old backups
                await CleanupOldBackupsAsync(schema);

                return new BackupResult
                {
                    Success = true,
                    FilePath = fullPath,
                    FileName = fileName,
                    Schema = schema,
                    Timestamp = DateTime.Now,
                    SizeInBytes = fileInfo.Length
                };
            }
            catch (Exception ex)
            {
                progress?.Report($"Error: {ex.Message}");
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Schema = schema
                };
            }
        }

        /// <summary>
        /// Restores a database from a backup file
        /// </summary>
        public async Task<RestoreResult> RestoreBackupAsync(string backupFilePath, string connectionString, bool applyToRealDb, IProgress<string> progress = null)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                var originalDatabase = builder.InitialCatalog;
                var targetDatabase = applyToRealDb ? originalDatabase : $"{originalDatabase}_Preview_{DateTime.Now:yyyyMMddHHmmss}";

                progress?.Report($"Starting restore to: {targetDatabase}");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // If restoring to a new preview database, create it first
                    if (!applyToRealDb)
                    {
                        progress?.Report("Creating preview database...");

                        var createDbCommand = $@"
                            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{targetDatabase}')
                            BEGIN
                                CREATE DATABASE [{targetDatabase}]
                            END";

                        using (var cmd = new SqlCommand(createDbCommand, connection))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Get logical file names from backup
                    var logicalFiles = await GetBackupFileListAsync(backupFilePath, connectionString);

                    progress?.Report("Restoring database...");

                    // Build RESTORE command with MOVE clauses
                    var restoreCommand = $@"
                        RESTORE DATABASE [{targetDatabase}]
                        FROM DISK = @BackupPath
                        WITH REPLACE,
                        STATS = 10";

                    // Add MOVE clauses for each logical file
                    foreach (var file in logicalFiles)
                    {
                        var newPhysicalPath = Path.Combine(
                            Path.GetDirectoryName(file.PhysicalName),
                            $"{targetDatabase}_{Path.GetFileName(file.PhysicalName)}"
                        );
                        restoreCommand += $",\n MOVE '{file.LogicalName}' TO '{newPhysicalPath}'";
                    }

                    using (var command = new SqlCommand(restoreCommand, connection))
                    {
                        command.CommandTimeout = 600; // 10 minutes timeout
                        command.Parameters.AddWithValue("@BackupPath", backupFilePath);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                progress?.Report("Restore completed successfully");

                return new RestoreResult
                {
                    Success = true,
                    RestoredDatabase = targetDatabase,
                    IsPreview = !applyToRealDb
                };
            }
            catch (Exception ex)
            {
                progress?.Report($"Error: {ex.Message}");
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Gets list of logical files in a backup
        /// </summary>
        private async Task<List<BackupFileInfo>> GetBackupFileListAsync(string backupPath, string connectionString)
        {
            var files = new List<BackupFileInfo>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var command = $"RESTORE FILELISTONLY FROM DISK = @BackupPath";

                using (var cmd = new SqlCommand(command, connection))
                {
                    cmd.Parameters.AddWithValue("@BackupPath", backupPath);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            files.Add(new BackupFileInfo
                            {
                                LogicalName = reader["LogicalName"].ToString(),
                                PhysicalName = reader["PhysicalName"].ToString()
                            });
                        }
                    }
                }
            }

            return files;
        }

        /// <summary>
        /// Gets all available backups organized by schema
        /// </summary>
        public List<BackupInfo> GetAvailableBackups()
        {
            var backups = new List<BackupInfo>();

            if (!Directory.Exists(_backupRootPath))
                return backups;

            foreach (var schemaDir in Directory.GetDirectories(_backupRootPath))
            {
                var schemaName = Path.GetFileName(schemaDir);

                foreach (var backupFile in Directory.GetFiles(schemaDir, "*.bak").OrderByDescending(f => File.GetCreationTime(f)))
                {
                    var fileInfo = new FileInfo(backupFile);

                    backups.Add(new BackupInfo
                    {
                        Schema = schemaName,
                        FileName = fileInfo.Name,
                        FilePath = fileInfo.FullName,
                        CreatedDate = fileInfo.CreationTime,
                        SizeInBytes = fileInfo.Length,
                        SizeFormatted = FormatFileSize(fileInfo.Length)
                    });
                }
            }

            return backups.OrderByDescending(b => b.CreatedDate).ToList();
        }

        /// <summary>
        /// Removes backups older than retention period
        /// </summary>
        private async Task CleanupOldBackupsAsync(string schema)
        {
            await Task.Run(() =>
            {
                var schemaFolder = Path.Combine(_backupRootPath, schema);
                if (!Directory.Exists(schemaFolder))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
                var oldBackups = Directory.GetFiles(schemaFolder, "*.bak")
                    .Where(f => File.GetCreationTime(f) < cutoffDate)
                    .ToList();

                foreach (var oldBackup in oldBackups)
                {
                    try
                    {
                        File.Delete(oldBackup);
                    }
                    catch
                    {
                        // Ignore errors when deleting old backups
                    }
                }
            });
        }

        /// <summary>
        /// Manually trigger cleanup for all schemas
        /// </summary>
        public async Task CleanupAllOldBackupsAsync()
        {
            if (!Directory.Exists(_backupRootPath))
                return;

            foreach (var schemaDir in Directory.GetDirectories(_backupRootPath))
            {
                var schemaName = Path.GetFileName(schemaDir);
                await CleanupOldBackupsAsync(schemaName);
            }
        }

        /// <summary>
        /// Deletes a specific backup file
        /// </summary>
        public bool DeleteBackup(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    #region Result Classes

    public class BackupResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Schema { get; set; }
        public DateTime Timestamp { get; set; }
        public long SizeInBytes { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string RestoredDatabase { get; set; }
        public bool IsPreview { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BackupInfo
    {
        public string Schema { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime CreatedDate { get; set; }
        public long SizeInBytes { get; set; }
        public string SizeFormatted { get; set; }
    }

    public class BackupFileInfo
    {
        public string LogicalName { get; set; }
        public string PhysicalName { get; set; }
    }

    #endregion
}
