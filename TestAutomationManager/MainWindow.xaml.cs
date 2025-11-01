using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TestAutomationManager.Data;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;
using TestAutomationManager.Services.Statistics;
using TestAutomationManager.Services; // Import Services
using TestAutomationManager.Dialogs; // Import Dialogs
using System.Diagnostics; // For Process
// Note: Removed unused System.Windows.Data and System.Globalization

namespace TestAutomationManager
{
    // ⭐ FIX: Removed the SchemaToBoolConverter class from here. It is now in its own file.

    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        // Tab tracking
        private class TabInfo
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Icon { get; set; }
            public TabItem TabItem { get; set; }
            public object Content { get; set; }
        }

        private List<TabInfo> _openTabs = new List<TabInfo>();
        private string _recordCountText;
        private string _systemStatusText;
        private System.Windows.Media.Brush _systemStatusColor;
        private bool _isExtTablesExpanded = false;
        private readonly ITestRepository _repository;
        public ObservableCollection<ExternalTableInfo> ExtTablesList { get; set; }
        private ObservableCollection<ExternalTableInfo> _allExtTables;
        private Dialogs.SearchOverlay _currentSearchDialog = null;

        // Tab drag and drop
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private TabItem _draggedTab = null;

        // ⭐ NEW: Schema navigation fields
        private bool _isSchemaExpanded = false;
        public List<string> AvailableSchemas { get; set; }

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

            _repository = new TestRepository();
            ExtTablesList = new ObservableCollection<ExternalTableInfo>();
            _allExtTables = new ObservableCollection<ExternalTableInfo>();
            ExtTablesItemsControl.ItemsSource = ExtTablesList;

            // ⭐ NEW: Bind available schemas to the ItemsControl
            AvailableSchemas = SchemaConfigService.AvailableSchemas;
            SchemaItemsControl.ItemsSource = AvailableSchemas;

            // Subscribe to tab selection changed
            ContentTabControl.SelectionChanged += ContentTabControl_SelectionChanged;

            // Initialize system status
            CheckDatabaseConnection();

            // Subscribe to statistics changes
            TestStatisticsService.Instance.PropertyChanged += OnStatisticsChanged;

            // Load initial view as first tab
            OpenTestsTab();

            // Pre-load external tables
            LoadExtTablesForNavigation();

            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            // ⭐ FIX: Removed the redundant PropertyChanged handler for CurrentSchema.
            // The binding in the XAML title bar is to the static instance and will update automatically.
        }

        // ================================================
        // TAB MANAGEMENT
        // ================================================

        /// <summary>
        /// Open or switch to a tab
        /// </summary>
        private void OpenOrSwitchToTab(string id, string title, string icon, Func<object> contentFactory)
        {
            // Check if tab already exists
            var existingTab = _openTabs.FirstOrDefault(t => t.Id == id);
            if (existingTab != null)
            {
                // Switch to existing tab
                ContentTabControl.SelectedItem = existingTab.TabItem;
                return;
            }

            // Create new tab
            var content = contentFactory();
            var tabItem = new TabItem
            {
                Header = title,
                Content = content,
                Tag = new { Icon = icon }
            };

            var tabInfo = new TabInfo
            {
                Id = id,
                Title = title,
                Icon = icon,
                TabItem = tabItem,
                Content = content
            };

            _openTabs.Add(tabInfo);
            ContentTabControl.Items.Add(tabItem);
            ContentTabControl.SelectedItem = tabItem;

            UpdatePageTitle();
        }

        /// <summary>
        /// Handle tab close button click
        /// </summary>
        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Find the TabItem
                var tabItem = FindParent<TabItem>(button);
                if (tabItem != null)
                {
                    CloseTab(tabItem);
                }
            }
        }

        /// <summary>
        /// Close a tab
        /// </summary>
        private void CloseTab(TabItem tabItem)
        {
            var tabInfo = _openTabs.FirstOrDefault(t => t.TabItem == tabItem);
            if (tabInfo != null)
            {
                // Clean up content if it's a view
                if (tabInfo.Content is Views.TestsView testsView)
                {
                    // Manually call Unloaded logic if needed, though WPF should handle it
                }

                _openTabs.Remove(tabInfo);
                ContentTabControl.Items.Remove(tabItem);
                tabInfo.Content = null; // Help GC

                // If no tabs left, open Tests by default
                if (_openTabs.Count == 0)
                {
                    OpenTestsTab();
                }
            }
        }

        /// <summary>
        /// ⭐ NEW: Close all open tabs (used for schema reload)
        /// </summary>
        private void CloseAllTabs()
        {
            // Create a copy of the list to iterate over, as we'll be modifying the original
            var tabsToClose = new List<TabItem>(_openTabs.Select(t => t.TabItem));
            foreach (var tabItem in tabsToClose)
            {
                CloseTab(tabItem);
            }
            _openTabs.Clear();
            ContentTabControl.Items.Clear();
        }


        /// <summary>
        /// Handle tab selection changed
        /// </summary>
        private void ContentTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePageTitle();
            UpdateRecordCount();
        }

        /// <summary>
        /// Update page title based on active tab
        /// </summary>
        private void UpdatePageTitle()
        {
            if (ContentTabControl.SelectedItem is TabItem selectedTab)
            {
                var tabInfo = _openTabs.FirstOrDefault(t => t.TabItem == selectedTab);
                if (tabInfo != null)
                {
                    PageTitle.Text = tabInfo.Title;
                }
            }
        }

        /// <summary>
        /// Find parent of specific type
        /// </summary>
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
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
        // ⭐ NEW: SCHEMA NAVIGATION
        // ================================================

        /// <summary>
        /// Handle Schema navigation click (expand/collapse)
        /// </summary>
        private void SchemaNav_Click(object sender, MouseButtonEventArgs e)
        {
            _isSchemaExpanded = !_isSchemaExpanded;

            if (_isSchemaExpanded)
            {
                SchemaChildren.Visibility = Visibility.Visible;
                AnimateArrow(SchemaArrowRotation, 0, 90);
                SchemaNavItem.Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackgroundBrush"];
                SchemaText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextPrimaryBrush"];
            }
            else
            {
                SchemaChildren.Visibility = Visibility.Collapsed;
                AnimateArrow(SchemaArrowRotation, 90, 0);
                SchemaNavItem.Background = System.Windows.Media.Brushes.Transparent;
                SchemaText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
            }
        }

        /// <summary>
        /// Handle selection of a new schema from the list
        /// </summary>
        private void SchemaItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string newSchema)
            {
                string currentSchema = SchemaConfigService.Instance.CurrentSchema;

                if (newSchema == currentSchema)
                {
                    return; // Already on this schema, do nothing
                }

                // Show the dialog to ask the user what to do
                var dialog = new SwitchSchemaDialog(currentSchema, newSchema);
                dialog.Owner = this;
                dialog.ShowDialog();

                if (dialog.Result == SchemaSwitchAction.Reload)
                {
                    // Reload the current instance
                    ReloadApplication(newSchema);
                }
                else if (dialog.Result == SchemaSwitchAction.OpenNew)
                {
                    // Launch a new instance with the new schema as an argument
                    LaunchNewInstance(newSchema);

                    // Reset the radio button check
                    rb.IsChecked = false;
                    var oldRb = FindVisualChild<RadioButton>(SchemaItemsControl,
                        (r) => r.Tag is string && (string)r.Tag == currentSchema);
                    if (oldRb != null)
                    {
                        oldRb.IsChecked = true;
                    }
                }
                else
                {
                    // User cancelled. We need to reset the RadioButton selection.
                    rb.IsChecked = false;
                    var oldRb = FindVisualChild<RadioButton>(SchemaItemsControl,
                        (r) => r.Tag is string && (string)r.Tag == currentSchema);
                    if (oldRb != null)
                    {
                        oldRb.IsChecked = true;
                    }
                }
            }
        }

        /// <summary>
        /// Reloads the entire application content to reflect a new schema
        /// This restarts the application process with the new schema to ensure
        /// Entity Framework Core rebuilds its model with the correct schema
        /// </summary>
        private void ReloadApplication(string newSchema)
        {
            System.Diagnostics.Debug.WriteLine($"🔄 Reloading application with new schema: {newSchema}");

            try
            {
                // Launch a new instance with the new schema
                string currentExecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string arguments = $"/schema:{newSchema}";

                ProcessStartInfo startInfo = new ProcessStartInfo(currentExecutable, arguments)
                {
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
                System.Diagnostics.Debug.WriteLine($"✅ New instance launched with schema: {newSchema}");

                // Close the current application window
                // This effectively "reloads" by replacing the old instance with a new one
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to reload application: {ex.Message}");
                ModernMessageDialog.ShowError(
                    $"Failed to reload the application with the new schema.\n\nError: {ex.Message}",
                    "Reload Error",
                    this);
            }
        }

        /// <summary>
        /// Launches a new instance of the application with a schema argument
        /// </summary>
        private void LaunchNewInstance(string newSchema)
        {
            try
            {
                // ⭐ FIX: Fully qualify 'Process' to avoid ambiguity
                string currentExecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string arguments = $"/schema:{newSchema}";

                ProcessStartInfo startInfo = new ProcessStartInfo(currentExecutable, arguments)
                {
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
                System.Diagnostics.Debug.WriteLine($"🚀 Launched new instance with schema: {newSchema}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to launch new instance: {ex.Message}");
                ModernMessageDialog.ShowError(
                    $"Failed to open a new application window.\n\nError: {ex.Message}",
                    "Error Launching",
                    this);
            }
        }

        /// <summary>
        /// Helper to find a child element in an ItemsControl
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T childAsT && predicate(childAsT))
                {
                    return childAsT;
                }

                // Check inside containers like ContentPresenter
                if (child is ContentPresenter contentPresenter)
                {
                    if (VisualTreeHelper.GetChildrenCount(contentPresenter) > 0)
                    {
                        var contentChild = VisualTreeHelper.GetChild(contentPresenter, 0);
                        if (contentChild is T contentChildAsT && predicate(contentChildAsT))
                        {
                            return contentChildAsT;
                        }
                        var resultInContent = FindVisualChild(contentChild, predicate);
                        if (resultInContent != null) return resultInContent;
                    }
                }

                var result = FindVisualChild(child, predicate);
                if (result != null) return result;
            }
            return null;
        }


        // ================================================
        // EXTTABLES NAVIGATION
        // ================================================

        /// <summary>
        /// Load external tables for the navigation sidebar
        /// </summary>
        private async void LoadExtTablesForNavigation()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📊 Loading external tables for navigation...");

                // Clear existing lists
                _allExtTables.Clear();
                ExtTablesList.Clear();
                UpdateExtTablesCount(); // Show 0

                var tables = await _repository.GetAllExternalTablesAsync();

                foreach (var table in tables)
                {
                    _allExtTables.Add(table);
                    ExtTablesList.Add(table);
                }

                UpdateExtTablesCount();

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {ExtTablesList.Count} external tables for navigation");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading external tables for navigation: {ex.Message}");
                ExtTablesEmptyState.Text = "Error loading tables";
                ExtTablesEmptyState.Visibility = Visibility.Visible;
                ExtTablesItemsControl.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle ExtTables navigation click (expand/collapse)
        /// </summary>
        private void ExtTablesNav_Click(object sender, MouseButtonEventArgs e)
        {
            _isExtTablesExpanded = !_isExtTablesExpanded;

            if (_isExtTablesExpanded)
            {
                ExtTablesChildren.Visibility = Visibility.Visible;
                AnimateArrow(ExtTablesArrowRotation, 0, 90);
                ExtTablesNavItem.Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackgroundBrush"];
                ExtTablesText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextPrimaryBrush"];
                LoadExtTablesForNavigation();
                ExtTablesSearchBox.Focus();
            }
            else
            {
                ExtTablesChildren.Visibility = Visibility.Collapsed;
                AnimateArrow(ExtTablesArrowRotation, 90, 0);
                ExtTablesNavItem.Background = System.Windows.Media.Brushes.Transparent;
                ExtTablesText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
                ExtTablesSearchBox.Text = "";
            }
        }

        /// <summary>
        /// Handle search within ExtTables navigation
        /// </summary>
        private void ExtTablesSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchQuery = ExtTablesSearchBox.Text?.Trim() ?? "";
            FilterExtTablesInNavigation(searchQuery);
            UpdateSearchPlaceholder();
        }

        /// <summary>
        /// Handle search box focus
        /// </summary>
        private void ExtTablesSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ExtTablesSearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Handle search box lost focus
        /// </summary>
        private void ExtTablesSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateSearchPlaceholder();
        }

        /// <summary>
        /// Update placeholder visibility based on text content
        /// </summary>
        private void UpdateSearchPlaceholder()
        {
            if (string.IsNullOrEmpty(ExtTablesSearchBox.Text))
            {
                ExtTablesSearchPlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                ExtTablesSearchPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Filter ExtTables in the navigation list
        /// </summary>
        private void FilterExtTablesInNavigation(string searchQuery)
        {
            ExtTablesList.Clear();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                foreach (var table in _allExtTables)
                {
                    ExtTablesList.Add(table);
                }
            }
            else
            {
                var filtered = _allExtTables.Where(t =>
                    t.TableName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.TestName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.TestId.ToString().Contains(searchQuery) ||
                    (t.Category?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();

                foreach (var table in filtered)
                {
                    ExtTablesList.Add(table);
                }
            }

            UpdateExtTablesCount();
        }

        /// <summary>
        /// Update the count display in ExtTables navigation
        /// </summary>
        private void UpdateExtTablesCount()
        {
            ExtTablesCountRun.Text = ExtTablesList.Count.ToString();
            ExtTablesEmptyState.Text = "No tables found"; // Reset error message

            if (ExtTablesList.Count == 0)
            {
                ExtTablesEmptyState.Visibility = Visibility.Visible;
                ExtTablesItemsControl.Visibility = Visibility.Collapsed;
            }
            else
            {
                ExtTablesEmptyState.Visibility = Visibility.Collapsed;
                ExtTablesItemsControl.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Animate the expand/collapse arrow
        /// </summary>
        private void AnimateArrow(RotateTransform transform, double from, double to)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        /// <summary>
        /// Handle individual ExtTable item click
        /// </summary>
        private void ExtTableItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tableName)
            {
                OpenExtTableTab(tableName);
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
            if (ContentTabControl.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Content is Views.TestsView testsView)
                {
                    int testCount = testsView.GetTestCount();
                    RecordCountText = $"({testCount} {(testCount == 1 ? "test" : "tests")})";
                }
                else if (selectedTab.Content is Views.ExtTableDetailView extTableView)
                {
                    int rowCount = extTableView.GetRowCount();
                    RecordCountText = $"({rowCount} {(rowCount == 1 ? "row" : "Iterations")})";
                }
                else
                {
                    RecordCountText = "";
                }
            }
            else
            {
                // Default to 0 if no tab is selected (e.g., during reload)
                RecordCountText = "(0 tests)";
            }
        }

        // ================================================
        // NAVIGATION - OPEN TABS
        // ================================================

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                string tag = radioButton.Tag?.ToString() ?? "";

                switch (tag)
                {
                    case "Tests":
                        OpenTestsTab();
                        break;
                    case "Processes":
                        OpenProcessesTab();
                        break;
                    case "Functions":
                        OpenFunctionsTab();
                        break;
                    case "Reports":
                        OpenReportsTab();
                        break;
                    case "Settings":
                        OpenSettingsTab();
                        break;
                }
            }
        }

        private void OpenTestsTab()
        {
            OpenOrSwitchToTab(
                "tests",
                "Tests",
                "📝",
                () =>
                {
                    var view = new Views.TestsView();
                    view.DataLoaded += (s, e) => UpdateRecordCount();
                    return view;
                }
            );
        }

        private void OpenExtTableTab(string tableName)
        {
            OpenOrSwitchToTab(
                $"exttable_{tableName}",
                tableName,
                "📊",
                () =>
                {
                    var view = new Views.ExtTableDetailView(tableName);
                    view.DataLoaded += (s, e) => UpdateRecordCount();
                    return view;
                }
            );
        }

        private void OpenProcessesTab()
        {
            OpenOrSwitchToTab(
                "processes",
                "Processes",
                "⚙️",
                () =>
                {
                    var textBlock = new TextBlock
                    {
                        Text = "Processes View - Coming Soon",
                        FontSize = 24,
                        Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    return textBlock;
                }
            );
        }

        private void OpenFunctionsTab()
        {
            OpenOrSwitchToTab(
                "functions",
                "Functions",
                "⚡",
                () =>
                {
                    var textBlock = new TextBlock
                    {
                        Text = "Functions View - Coming Soon",
                        FontSize = 24,
                        Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    return textBlock;
                }
            );
        }

        private void OpenReportsTab()
        {
            OpenOrSwitchToTab(
                "reports",
                "Reports",
                "📈",
                () =>
                {
                    var textBlock = new TextBlock
                    {
                        Text = "Reports View - Coming Soon",
                        FontSize = 24,
                        Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    return textBlock;
                }
            );
        }

        private void OpenSettingsTab()
        {
            OpenOrSwitchToTab(
                "settings",
                "Settings",
                "⚙️",
                () =>
                {
                    return new Views.SettingsView();
                }
            );
        }

        // ================================================
        // ACTIONS
        // ================================================

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Refresh current tab
            if (ContentTabControl.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Content is Views.TestsView testsView)
                {
                    testsView.RefreshData();
                }
                else if (selectedTab.Content is Views.ExtTableDetailView extTableView)
                {
                    extTableView.RefreshData();
                }
            }

            LoadExtTablesForNavigation();
            CheckDatabaseConnection();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsTab();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only search in Tests view
            if (ContentTabControl.SelectedItem is TabItem selectedTab)
            {
                if (selectedTab.Content is Views.TestsView testsView)
                {
                    string searchQuery = SearchBox.Text;
                    testsView.FilterTests(searchQuery);
                    UpdateRecordCount();
                }
            }
        }

        private void SystemStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CheckDatabaseConnection();
            LoadExtTablesForNavigation();
        }

        // ================================================
        // TAB DRAG AND DROP
        // ================================================

        /// <summary>
        /// Handle middle mouse button click - close tab (like browsers)
        /// </summary>
        private void TabItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                if (sender is Border border)
                {
                    TabItem tabItem = FindParent<TabItem>(border);
                    if (tabItem != null)
                    {
                        CloseTab(tabItem);
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Handle mouse down on tab - start tracking for drag
        /// </summary>
        private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedTab = FindParent<TabItem>(border);
            }
        }

        /// <summary>
        /// Handle mouse move - initiate drag if moved far enough
        /// </summary>
        private void TabItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && _draggedTab != null)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                // Check if moved far enough to start drag (threshold = 5 pixels)
                if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                {
                    _isDragging = true;

                    // Start drag operation
                    DataObject dragData = new DataObject("TabItem", _draggedTab);
                    DragDrop.DoDragDrop(_draggedTab, dragData, DragDropEffects.Move);

                    _isDragging = false;
                    _draggedTab = null;
                }
            }
        }

        /// <summary>
        /// Handle mouse up - cancel drag
        /// </summary>
        private void TabItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _draggedTab = null;
        }

        /// <summary>
        /// Handle drag over - allow drop
        /// </summary>
        private void TabItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle drop - reorder tabs
        /// </summary>
        private void TabItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                TabItem droppedTab = e.Data.GetData("TabItem") as TabItem;
                Border targetBorder = sender as Border;
                TabItem targetTab = FindParent<TabItem>(targetBorder);

                if (droppedTab != null && targetTab != null && droppedTab != targetTab)
                {
                    // Get indices
                    int droppedIndex = ContentTabControl.Items.IndexOf(droppedTab);
                    int targetIndex = ContentTabControl.Items.IndexOf(targetTab);

                    if (droppedIndex >= 0 && targetIndex >= 0)
                    {
                        // Reorder in TabControl
                        ContentTabControl.Items.Remove(droppedTab);
                        ContentTabControl.Items.Insert(targetIndex, droppedTab);

                        // Reorder in tracking list
                        var droppedTabInfo = _openTabs.FirstOrDefault(t => t.TabItem == droppedTab);
                        if (droppedTabInfo != null)
                        {
                            _openTabs.Remove(droppedTabInfo);

                            // Recalculate target index in tracking list
                            var targetTabInfo = _openTabs.FirstOrDefault(t => t.TabItem == targetTab);
                            int newIndex = _openTabs.IndexOf(targetTabInfo);

                            if (newIndex >= 0)
                            {
                                _openTabs.Insert(newIndex, droppedTabInfo);
                            }
                            else
                            {
                                _openTabs.Add(droppedTabInfo);
                            }
                        }

                        // Keep the dragged tab selected
                        ContentTabControl.SelectedItem = droppedTab;

                        System.Diagnostics.Debug.WriteLine($"✓ Moved tab from position {droppedIndex} to {targetIndex}");
                    }
                }
            }
        }

        // ================================================
        // INotifyPropertyChanged
        // ================================================

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handle Add New Test button click
        /// </summary>
        private async void AddNewTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("➕ Opening Add New Test dialog...");

                // Create and show the dialog
                var dialog = new TestAutomationManager.Dialogs.AddTestDialog();
                dialog.Owner = this; // Set owner for proper modal behavior

                bool? result = dialog.ShowDialog();

                if (result == true && dialog.IsSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ New test created: #{dialog.CreatedTest.Id} - {dialog.CreatedTest.Name}");

                    // Ensure no active filter is applied
                    SearchBox.Text = "";

                    // If Tests tab is already open/selected, refresh and focus
                    if (ContentTabControl.SelectedItem is TabItem selTab1 &&
                        selTab1.Content is TestAutomationManager.Views.TestsView tv1)
                    {
                        tv1.RefreshData();
                        tv1.FocusTest(dialog.CreatedTest.Id);
                    }
                    else
                    {
                        // Open Tests tab, wait for it to render, then focus the test
                        OpenTestsTab();
                        await System.Threading.Tasks.Task.Delay(400);

                        if (ContentTabControl.SelectedItem is TabItem selTab2 &&
                            selTab2.Content is TestAutomationManager.Views.TestsView tv2)
                        {
                            tv2.RefreshData();
                            tv2.FocusTest(dialog.CreatedTest.Id);
                        }
                    }

                    // Refresh ExtTables navigation and stats
                    LoadExtTablesForNavigation();
                    TestStatisticsService.Instance.ForceRefresh();

                    System.Diagnostics.Debug.WriteLine("✓ UI refreshed to show new test");
                }

                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ Add New Test dialog cancelled");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in Add New Test: {ex.Message}");
                MessageBox.Show(
                    $"An error occurred while opening the Add New Test dialog.\n\nError: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        //Handlers for the ctrl + F functionality
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+F for search
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                e.Handled = true; // Mark as handled immediately to prevent propagation

                // If search dialog is already open, bring it to focus instead of opening a new one
                if (_currentSearchDialog != null)
                {
                    try
                    {
                        _currentSearchDialog.Activate();
                        _currentSearchDialog.Focus();
                        return;
                    }
                    catch
                    {
                        // Dialog might have been closed, continue to open a new one
                        _currentSearchDialog = null;
                    }
                }

                // Open search dialog
                try
                {
                    var searchDialog = new Dialogs.SearchOverlay(this);
                    _currentSearchDialog = searchDialog;

                    // Subscribe to closing event to clear the reference
                    searchDialog.Closed += (s, args) =>
                    {
                        _currentSearchDialog = null;
                    };

                    bool? result = searchDialog.ShowDialog();

                    if (result == true)
                    {
                        string query = searchDialog.SearchQuery;
                        bool exact = searchDialog.ExactMatch;

                        // Ensure we have a valid query
                        if (string.IsNullOrWhiteSpace(query))
                        {
                            return;
                        }

                        // Get the current tab content
                        if (ContentTabControl.SelectedItem is TabItem selectedTab &&
                            selectedTab.Content is FrameworkElement content)
                        {
                            // Clear previous highlights
                            Services.VisualSearchHighlighter.ClearHighlights(content);

                            // Perform the search and highlight matches
                            var (matches, firstMatch) = Services.VisualSearchHighlighter.HighlightText(content, query, exact);

                            if (matches == 0)
                            {
                                // No matches found - show info dialog
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    Dialogs.ModernMessageDialog.ShowInfo(
                                        $"No matches found for \"{query}\"",
                                        "Find Results",
                                        this);
                                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                            }
                            else
                            {
                                // Scroll to the first match
                                if (firstMatch != null)
                                {
                                    // Use BeginInvoke to ensure the UI has updated before scrolling
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        Services.ViewportScroller.ScrollToElement(firstMatch);
                                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                                }

                                // Auto-clear highlights after 3 seconds
                                var timer = new System.Windows.Threading.DispatcherTimer
                                {
                                    Interval = TimeSpan.FromSeconds(3)
                                };
                                timer.Tick += (s, args) =>
                                {
                                    timer.Stop();
                                    Services.VisualSearchHighlighter.ClearHighlights(content);
                                };
                                timer.Start();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening search dialog: {ex.Message}");
                    _currentSearchDialog = null;

                    // Show error to user
                    MessageBox.Show(
                        $"An error occurred while opening the search dialog.\n\nError: {ex.Message}",
                        "Search Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}