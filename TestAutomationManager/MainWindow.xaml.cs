using MahApps.Metro.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using TestAutomationManager.Data;
using TestAutomationManager.Services.Statistics;

namespace TestAutomationManager
{
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private Views.TestsView _currentTestsView;
        private string _recordCountText;
        private string _systemStatusText;
        private System.Windows.Media.Brush _systemStatusColor;

        // ================================================
        // PROPERTIES FOR BINDING
        // ================================================

        public string RecordCountText
        {
            get => _recordCountText;
            set
            {
                _recordCountText = value;
                OnPropertyChanged();
            }
        }

        public string SystemStatusText
        {
            get => _systemStatusText;
            set
            {
                _systemStatusText = value;
                OnPropertyChanged();
            }
        }

        public System.Windows.Media.Brush SystemStatusColor
        {
            get => _systemStatusColor;
            set
            {
                _systemStatusColor = value;
                OnPropertyChanged();
            }
        }

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize system status
            CheckDatabaseConnection();

            // Subscribe to statistics changes
            TestStatisticsService.Instance.PropertyChanged += OnStatisticsChanged;

            // Load initial view
            LoadTestsView();
        }

        // ================================================
        // DATABASE CONNECTION CHECK
        // ================================================

        /// <summary>
        /// Check database connection and update system status
        /// </summary>
        private async void CheckDatabaseConnection()
        {
            try
            {
                SystemStatusText = "Checking connection...";
                SystemStatusColor = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                // Test connection
                bool isConnected = await System.Threading.Tasks.Task.Run(() =>
                    DbConnectionConfig.TestConnection()
                );

                if (isConnected)
                {
                    SystemStatusText = "All systems online";
                    SystemStatusColor = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                    System.Diagnostics.Debug.WriteLine("✓ Database connection successful");
                }
                else
                {
                    SystemStatusText = "Database offline";
                    SystemStatusColor = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];
                    System.Diagnostics.Debug.WriteLine("✗ Database connection failed");
                }
            }
            catch (Exception ex)
            {
                SystemStatusText = "Connection error";
                SystemStatusColor = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];
                System.Diagnostics.Debug.WriteLine($"✗ Error checking connection: {ex.Message}");
            }
        }

        // ================================================
        // STATISTICS UPDATE HANDLER
        // ================================================

        /// <summary>
        /// Update record count when statistics change
        /// </summary>
        private void OnStatisticsChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateRecordCount();
        }

        /// <summary>
        /// Update record count display based on current view
        /// </summary>
        private void UpdateRecordCount()
        {
            if (_currentTestsView != null)
            {
                // Tests View - show only test count
                int testCount = _currentTestsView.GetTestCount();
                RecordCountText = $"({testCount} {(testCount == 1 ? "test" : "tests")})";
            }
        }

        // ================================================
        // NAVIGATION - Updated
        // ================================================

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                string tag = radioButton.Tag?.ToString() ?? "";
                PageTitle.Text = tag;

                // Clear search when changing views
                SearchBox.Text = "";

                switch (tag)
                {
                    case "Tests":
                        LoadTestsView();
                        break;
                    case "Processes":
                        LoadProcessesView();
                        break;
                    case "Functions":
                        LoadFunctionsView();
                        break;
                    case "Reports":
                        LoadReportsView();
                        break;
                    case "Settings":
                        LoadSettingsView();
                        break;
                }
            }
        }

        private void LoadTestsView()
        {
            _currentTestsView = new Views.TestsView();
            _currentTestsView.DataLoaded += OnTestsViewDataLoaded;
            ContentFrame.Content = _currentTestsView;
        }

        private void OnTestsViewDataLoaded(object sender, EventArgs e)
        {
            UpdateRecordCount();
        }

        private void LoadProcessesView()
        {
            _currentTestsView = null;
            var textBlock = new TextBlock
            {
                Text = "Processes View - Coming Soon",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentFrame.Content = textBlock;
            RecordCountText = "(45 processes)";
        }

        private void LoadFunctionsView()
        {
            _currentTestsView = null;
            var textBlock = new TextBlock
            {
                Text = "Functions View - Coming Soon",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentFrame.Content = textBlock;
            RecordCountText = "(128 functions)";
        }

        private void LoadReportsView()
        {
            _currentTestsView = null;
            var textBlock = new TextBlock
            {
                Text = "Reports View - Coming Soon",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentFrame.Content = textBlock;
            RecordCountText = "";
        }

        private void LoadSettingsView()
        {
            _currentTestsView = null;
            var textBlock = new TextBlock
            {
                Text = "Settings View - Coming Soon",
                FontSize = 24,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentFrame.Content = textBlock;
            RecordCountText = "";
        }

        // ================================================
        // ACTIONS
        // ================================================

        private void AddNew_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add new item functionality will be implemented soon!",
                           "Add New", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Clear search and refresh
            SearchBox.Text = "";

            if (TestsRadio.IsChecked == true)
            {
                LoadTestsView();
            }

            // Re-check database connection
            CheckDatabaseConnection();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only search in Tests view
            if (_currentTestsView != null)
            {
                string searchQuery = SearchBox.Text;
                _currentTestsView.FilterTests(searchQuery);
                UpdateRecordCount();
            }
        }

        private void SystemStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CheckDatabaseConnection();
        }

        // ================================================
        // INotifyPropertyChanged
        // ================================================

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}