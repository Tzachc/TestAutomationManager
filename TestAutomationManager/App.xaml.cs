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
            ThemeService.ApplyTheme(AppTheme.Dark);
        }
    }
}
