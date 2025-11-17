using System.Linq;
using System.Windows;
using TestAutomationManager.Services;

namespace TestAutomationManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for command-line arguments
            if (e.Args.Length > 0)
            {
                // Check for schema argument
                string schemaArg = e.Args.FirstOrDefault(arg => arg.StartsWith("/schema:", System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(schemaArg))
                {
                    string schemaName = schemaArg.Substring(8); // Get text after "/schema:"
                    if (SchemaConfigService.AvailableSchemas.Contains(schemaName))
                    {
                        SchemaConfigService.Instance.CurrentSchema = schemaName;
                        System.Diagnostics.Debug.WriteLine($"🚀 App started with schema from args: {schemaName}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Schema from args '{schemaName}' not found. Using default.");
                    }
                }

                // Check for preview database argument
                string databaseArg = e.Args.FirstOrDefault(arg => arg.StartsWith("/database:", System.StringComparison.OrdinalIgnoreCase));
                string previewArg = e.Args.FirstOrDefault(arg => arg.StartsWith("/preview:", System.StringComparison.OrdinalIgnoreCase));
                string backupDateArg = e.Args.FirstOrDefault(arg => arg.StartsWith("/backupdate:", System.StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(databaseArg) && !string.IsNullOrEmpty(previewArg))
                {
                    string databaseName = databaseArg.Substring(10); // Get text after "/database:"
                    string backupDate = backupDateArg?.Substring(12).Trim('"') ?? "Unknown";

                    // Set up preview mode
                    if (System.DateTime.TryParse(backupDate, out var parsedDate))
                    {
                        DatabaseConnectionService.Instance.SwitchToPreviewDatabase(databaseName, parsedDate, "Preview");
                    }
                    else
                    {
                        DatabaseConnectionService.Instance.SwitchToPreviewDatabase(databaseName, System.DateTime.Now, "Preview");
                    }

                    System.Diagnostics.Debug.WriteLine($"🔵 App started in PREVIEW MODE - Database: {databaseName}, Backup Date: {backupDate}");
                }
            }

            ThemeService.ApplyTheme(AppTheme.Dark);

            // DISABLED: Automatic backup scheduler (to be moved to Jenkins)
            // BackupSchedulerService.Instance.Start();
            // System.Diagnostics.Debug.WriteLine("🗄️ Backup scheduler service started");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // DISABLED: Automatic backup scheduler
            // BackupSchedulerService.Instance.Stop();
            // System.Diagnostics.Debug.WriteLine("🗄️ Backup scheduler service stopped");

            base.OnExit(e);
        }
    }
}