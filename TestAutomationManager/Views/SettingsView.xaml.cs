using System;
using System.Windows;
using System.Windows.Controls;
using TestAutomationManager.Services;

namespace TestAutomationManager.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectedTheme();
        }

        private void ThemeOption_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton &&
                Enum.TryParse<AppTheme>(radioButton.Tag?.ToString(), out var theme))
            {
                if (theme != ThemeService.CurrentTheme)
                {
                    ThemeService.ApplyTheme(theme);
                }
                UpdateSelectedTheme();
            }
        }

        private void UpdateSelectedTheme()
        {
            var current = ThemeService.CurrentTheme;
            DarkThemeOption.IsChecked = current == AppTheme.Dark;
            LightThemeOption.IsChecked = current == AppTheme.Light;
            RedThemeOption.IsChecked = current == AppTheme.Red;
        }
    }
}
