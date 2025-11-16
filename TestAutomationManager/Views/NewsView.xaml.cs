using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TestAutomationManager.Models;
using TestAutomationManager.Services;

namespace TestAutomationManager.Views
{
    public partial class NewsView : UserControl
    {
        private readonly NewsService _newsService;
        private List<NewsArticle> _allArticles;
        private string _currentFilter = "All";

        public event EventHandler DataLoaded;

        public NewsView()
        {
            InitializeComponent();
            _newsService = new NewsService();
            _allArticles = new List<NewsArticle>();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadNewsAsync();
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }

        private async System.Threading.Tasks.Task LoadNewsAsync()
        {
            try
            {
                // Show loading state
                LoadingPanel.Visibility = Visibility.Visible;
                NewsItemsControl.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Collapsed;
                RefreshButton.IsEnabled = false;

                // Fetch news
                _allArticles = await _newsService.GetTechNewsAsync();

                // Apply current filter
                ApplyFilter(_currentFilter);

                // Hide loading state
                LoadingPanel.Visibility = Visibility.Collapsed;
                RefreshButton.IsEnabled = true;

                if (_allArticles.Count == 0)
                {
                    EmptyPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    NewsItemsControl.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading news: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
                RefreshButton.IsEnabled = true;
            }
        }

        private void ApplyFilter(string filter)
        {
            _currentFilter = filter;

            IEnumerable<NewsArticle> filteredArticles = _allArticles;

            if (filter != "All")
            {
                filteredArticles = _allArticles.Where(a => a.Category == filter);
            }

            NewsItemsControl.ItemsSource = filteredArticles.ToList();

            // Update button styles
            UpdateFilterButtonStyles(filter);
        }

        private void UpdateFilterButtonStyles(string activeFilter)
        {
            // Reset all buttons
            FilterAll.Tag = null;
            FilterAutomation.Tag = null;
            FilterAI.Tag = null;
            FilterDevOps.Tag = null;

            // Set active button
            switch (activeFilter)
            {
                case "All":
                    FilterAll.Tag = "Active";
                    break;
                case "Test Automation":
                    FilterAutomation.Tag = "Active";
                    break;
                case "AI & ML":
                    FilterAI.Tag = "Active";
                    break;
                case "DevOps":
                    FilterDevOps.Tag = "Active";
                    break;
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string filter = button.Name switch
                {
                    "FilterAll" => "All",
                    "FilterAutomation" => "Test Automation",
                    "FilterAI" => "AI & ML",
                    "FilterDevOps" => "DevOps",
                    _ => "All"
                };

                ApplyFilter(filter);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNewsAsync();
        }

        private void NewsCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string url)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        // Open URL in default browser
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open link: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}
