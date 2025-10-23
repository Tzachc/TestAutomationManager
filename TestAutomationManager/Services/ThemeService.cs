using ControlzEx.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TestAutomationManager.Services
{
    public enum AppTheme
    {
        Dark,
        Light,
        Red
    }

    public static class ThemeService
    {
        private static readonly Dictionary<AppTheme, Uri> ThemeUris = new()
        {
            { AppTheme.Dark, new Uri("Themes/DarkTheme.xaml", UriKind.Relative) },
            { AppTheme.Light, new Uri("Themes/LightTheme.xaml", UriKind.Relative) },
            { AppTheme.Red, new Uri("Themes/RedTheme.xaml", UriKind.Relative) }
        };

        private static readonly Dictionary<AppTheme, string> MahAppsThemes = new()
        {
            { AppTheme.Dark, "Dark.Blue" },
            { AppTheme.Light, "Light.Blue" },
            { AppTheme.Red, "Dark.Red" }
        };

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

        public static void ApplyTheme(AppTheme theme)
        {
            if (!ThemeUris.TryGetValue(theme, out var resourceUri))
            {
                return;
            }

            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            var dictionaries = app.Resources.MergedDictionaries;
            var currentThemeDictionary = dictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase));

            if (currentThemeDictionary != null)
            {
                dictionaries.Remove(currentThemeDictionary);
            }

            dictionaries.Add(new ResourceDictionary { Source = resourceUri });

            if (MahAppsThemes.TryGetValue(theme, out var mahAppsTheme))
            {
                ThemeManager.Current.ChangeTheme(app, mahAppsTheme);
            }

            CurrentTheme = theme;
        }
    }
}
