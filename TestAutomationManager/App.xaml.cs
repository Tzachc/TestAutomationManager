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

            // NEW: Check for command-line arguments to set schema
            if (e.Args.Length > 0)
            {
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
            }

            ThemeService.ApplyTheme(AppTheme.Dark);

            // Start the backup scheduler service
            BackupSchedulerService.Instance.Start();
            System.Diagnostics.Debug.WriteLine("🗄️ Backup scheduler service started");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop the backup scheduler service when app exits
            BackupSchedulerService.Instance.Stop();
            System.Diagnostics.Debug.WriteLine("🗄️ Backup scheduler service stopped");

            base.OnExit(e);
        }
    }
}