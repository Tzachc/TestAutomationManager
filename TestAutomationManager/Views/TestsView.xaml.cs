using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TestAutomationManager.Dialogs;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;
using TestAutomationManager.Services;
using TestAutomationManager.Services.Statistics;

namespace TestAutomationManager.Views
{
    public partial class TestsView : UserControl
    {
        // ================================================
        // FIELDS
        // ================================================

        /// <summary>
        /// Repository for database operations
        /// </summary>
        private readonly ITestRepository _repository;

        /// <summary>
        /// Observable collection for UI binding
        /// </summary>
        public ObservableCollection<Test> Tests { get; set; }

        /// <summary>
        /// Keep reference to all tests for filtering
        /// </summary>
        private ObservableCollection<Test> _allTests;

        /// <summary>
        /// Current search query for re-filtering after updates
        /// </summary>
        private string _currentSearchQuery = "";

        /// <summary>
        /// Event fired when data is loaded
        /// </summary>
        public event EventHandler DataLoaded;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public TestsView()
        {
            InitializeComponent();

            // Initialize repository
            _repository = new TestRepository();

            // Initialize collections
            Tests = new ObservableCollection<Test>();
            _allTests = new ObservableCollection<Test>();

            // Set data context
            TestsItemsControl.ItemsSource = Tests;

            // Load initial data from database
            LoadTestsFromDatabase();

            // ⭐ START DATABASE WATCHER for live updates
            StartLiveUpdates();
        }

        // ================================================
        // LIVE UPDATES
        // ================================================

        /// <summary>
        /// Start watching database for external changes
        /// </summary>
        private void StartLiveUpdates()
        {
            // Subscribe to database change events
            DatabaseWatcherService.Instance.TestsUpdated += OnDatabaseTestsUpdated;

            // Start watching (polls every 3 seconds by default)
            DatabaseWatcherService.Instance.StartWatching();

            System.Diagnostics.Debug.WriteLine("✓ Live database updates enabled");
        }

        /// <summary>
        /// Handle database updates (called when external changes detected)
        /// Preserves UI state like IsExpanded to avoid disrupting user experience
        /// </summary>
        private void OnDatabaseTestsUpdated(object sender, List<Test> updatedTests)
        {
            System.Diagnostics.Debug.WriteLine("🔄 Applying database updates to UI...");

            // ⭐ STEP 1: Save current UI state (which items are expanded)
            var expandedTestIds = new HashSet<int>(
                _allTests.Where(t => t.IsExpanded).Select(t => t.Id)
            );

            var expandedProcessIds = new HashSet<int>();
            foreach (var test in _allTests)
            {
                foreach (var process in test.Processes.Where(p => p.IsExpanded))
                {
                    expandedProcessIds.Add(process.Id);
                }
            }

            // ⭐ STEP 2: Update data from database
            _allTests.Clear();
            foreach (var test in updatedTests)
            {
                // Restore IsExpanded state for tests
                if (expandedTestIds.Contains(test.Id))
                {
                    test.IsExpanded = true;
                }

                // Restore IsExpanded state for processes
                foreach (var process in test.Processes)
                {
                    if (expandedProcessIds.Contains(process.Id))
                    {
                        process.IsExpanded = true;
                    }
                }

                _allTests.Add(test);
            }

            // ⭐ STEP 3: Re-apply current filter
            FilterTests(_currentSearchQuery);

            // ⭐ STEP 4: Update statistics
            UpdateStatistics();

            System.Diagnostics.Debug.WriteLine("✓ UI synchronized with database (expanded items preserved)");
        }

        // ================================================
        // DATA LOADING
        // ================================================

        /// <summary>
        /// Load tests from SQL database
        /// </summary>
        private async void LoadTestsFromDatabase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📊 Loading tests from database...");

                // Get all tests from database (async)
                var testsFromDb = await _repository.GetAllTestsAsync();

                // Clear existing data
                Tests.Clear();
                _allTests.Clear();

                // Add tests to collections
                foreach (var test in testsFromDb)
                {
                    Tests.Add(test);
                    _allTests.Add(test);
                }

                // Update statistics
                UpdateStatistics();

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {Tests.Count} tests from database successfully!");

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Show message if no data
                if (Tests.Count == 0)
                {
                    MessageBox.Show("No tests found in database.\n\nMake sure you ran the SQL scripts to create sample data.",
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading tests: {ex.Message}");
                MessageBox.Show($"Failed to load tests from database.\n\nError: {ex.Message}\n\nCheck:\n1. Database connection\n2. SQL scripts ran\n3. DbConnectionConfig settings",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // SEARCH & FILTER
        // ================================================

        /// <summary>
        /// Filter tests based on search query
        /// </summary>
        public void FilterTests(string searchQuery)
        {
            // Save current search query for re-filtering after updates
            _currentSearchQuery = searchQuery ?? "";

            Tests.Clear();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                // Show all tests when search is empty
                foreach (var test in _allTests)
                {
                    Tests.Add(test);
                }
                UpdateStatistics();
                return;
            }

            // Filter tests using fuzzy matching (including ID search)
            var filtered = _allTests.Where(test =>
                SearchHelper.MatchesId(test.Id, searchQuery) ||
                SearchHelper.Matches(test.Name, searchQuery) ||
                SearchHelper.Matches(test.Description, searchQuery) ||
                SearchHelper.Matches(test.Category, searchQuery) ||
                SearchHelper.Matches(test.Status, searchQuery)
            ).ToList();

            foreach (var test in filtered)
            {
                Tests.Add(test);
            }

            UpdateStatistics();
        }

        /// <summary>
        /// Get count of filtered tests
        /// </summary>
        public int GetTestCount()
        {
            return Tests.Count;
        }

        /// <summary>
        /// Get total count of processes across all tests
        /// </summary>
        public int GetProcessCount()
        {
            return _allTests.Sum(t => t.Processes.Count);
        }

        /// <summary>
        /// Get total count of functions across all processes
        /// </summary>
        public int GetFunctionCount()
        {
            return _allTests.Sum(t => t.Processes.Sum(p => p.Functions.Count));
        }

        // ================================================
        // STATISTICS
        // ================================================

        /// <summary>
        /// Update statistics whenever tests change
        /// </summary>
        private void UpdateStatistics()
        {
            TestStatisticsService.Instance.UpdateStatistics(_allTests);
        }

        /// <summary>
        /// ⭐ Force refresh statistics (called when tab becomes active)
        /// This ensures the title bar statistics are updated when switching tabs
        /// </summary>
        public void ForceRefreshStatistics()
        {
            UpdateStatistics();
            System.Diagnostics.Debug.WriteLine("✓ Force refreshed statistics for active tab");
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        /// <summary>
        /// Refresh data from database manually
        /// </summary>
        public void RefreshData()
        {
            LoadTestsFromDatabase();
        }

        // ================================================
        // CLEANUP
        // ================================================

        // Called when the view is shown (navigated to)
        private void TestsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure we subscribe exactly once
            TestAutomationManager.Services.DatabaseWatcherService.Instance.TestsUpdated -= OnDatabaseTestsUpdated;
            TestAutomationManager.Services.DatabaseWatcherService.Instance.TestsUpdated += OnDatabaseTestsUpdated;

            // Ensure watcher is running (idempotent)
            if (!TestAutomationManager.Services.DatabaseWatcherService.Instance.IsWatching)
                TestAutomationManager.Services.DatabaseWatcherService.Instance.StartWatching();

            System.Diagnostics.Debug.WriteLine("✓ TestsView Loaded: subscribed & watcher ensured");
        }

        // Called when the view is hidden (navigated away) or removed
        private void TestsView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Only unsubscribe this view’s handler; DO NOT stop the global watcher here
            TestAutomationManager.Services.DatabaseWatcherService.Instance.TestsUpdated -= OnDatabaseTestsUpdated;
            System.Diagnostics.Debug.WriteLine("✓ TestsView Unloaded: unsubscribed (watcher left running)");
        }

        /// <summary>
        /// Handle Edit button click
        /// </summary>
        private void EditTest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Test test)
            {
                MessageBox.Show(
                    $"Edit functionality for Test #{test.Id} '{test.Name}' is coming soon!",
                    "Coming Soon",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        /// <summary>
        /// Handle Run button click
        /// </summary>
        private void RunTest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Test test)
            {
                MessageBox.Show(
                    $"Run functionality for Test #{test.Id} '{test.Name}' is coming soon!\n\nThis will execute the test automation.",
                    "Coming Soon",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        /// <summary>
        /// Handle Delete button click
        /// </summary>
        private async void DeleteTest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Test test)
            {
                try
                {
                    // Count associated processes and functions
                    int processCount = test.Processes?.Count ?? 0;
                    int functionCount = test.Processes?.Sum(p => p.Functions?.Count ?? 0) ?? 0;

                    // Build warning message
                    string warningMessage = $"Are you sure you want to delete this test?\n\n" +
                                          $"Test: {test.Name} (ID #{test.Id})\n" +
                                          $"Category: {test.Category}\n\n";

                    if (processCount > 0)
                    {
                        warningMessage += $"⚠️ This will also delete:\n" +
                                        $"  • {processCount} process{(processCount != 1 ? "es" : "")}\n" +
                                        $"  • {functionCount} function{(functionCount != 1 ? "s" : "")}\n\n";
                    }

                    warningMessage += "This action cannot be undone!";

                    // Show confirmation dialog
                    var result = ModernMessageDialog.ShowConfirmation(
                        warningMessage,
                        "Confirm Deletion",
                        Window.GetWindow(this));

                    if (result == MessageBoxResult.Yes)
                    {
                        // Disable button to prevent double-click
                        button.IsEnabled = false;

                        System.Diagnostics.Debug.WriteLine($"🗑️ Deleting test #{test.Id}...");

                        // Delete from database
                        await _repository.DeleteTestAsync(test.Id);

                        System.Diagnostics.Debug.WriteLine($"✓ Test #{test.Id} deleted successfully!");

                        // Show success message
                        ModernMessageDialog.ShowSuccess(
                            $"Test '{test.Name}' has been deleted successfully!",
                            "Test Deleted",
                            Window.GetWindow(this));

                        // Refresh data from database
                        // The DatabaseWatcherService will pick it up, but force immediate refresh
                        RefreshData();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deleting test: {ex.Message}");
                    ModernMessageDialog.ShowError(
                        $"Failed to delete test.\n\nError: {ex.Message}",
                        "Delete Error",
                        Window.GetWindow(this));

                    // Re-enable button
                    if (sender is Button btn)
                    {
                        btn.IsEnabled = true;
                    }
                }
            }
        }
        public void FocusTest(int testId)
        {
            // Clear current filter so full list remains
            _currentSearchQuery = "";
            FilterTests("");   // repopulates 'Tests' from '_allTests'

            // Find the test in the full dataset
            var test = _allTests.FirstOrDefault(t => t.Id == testId);
            if (test == null) return;

            test.IsExpanded = true;

            // Ensure containers are generated before BringIntoView
            Dispatcher.InvokeAsync(() =>
            {
                // Force layout so the container can be generated
                TestsItemsControl.UpdateLayout();

                var container = TestsItemsControl.ItemContainerGenerator.ContainerFromItem(test) as FrameworkElement;
                if (container != null)
                {
                    container.BringIntoView();
                }
                else
                {
                    // In rare cases, generate again on next idle
                    Dispatcher.InvokeAsync(() =>
                    {
                        TestsItemsControl.UpdateLayout();
                        var c2 = TestsItemsControl.ItemContainerGenerator.ContainerFromItem(test) as FrameworkElement;
                        c2?.BringIntoView();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }


    }
}