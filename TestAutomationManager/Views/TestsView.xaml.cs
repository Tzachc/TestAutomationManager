using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private readonly List<ProcessHeaderRegistration> _processHeaders = new();

        private const string ProcessContainerTag = "ProcessContainer";

        /// <summary>
        /// Event fired when data is loaded
        /// </summary>
        public event EventHandler DataLoaded;

        // ----- Middle-mouse panning state -----
        private bool _isPanning = false;
        private Point _lastPanPoint;
        private double _startH;
        private double _startV;

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
        // SCROLL SYNCHRONIZATION
        // ================================================

        /// <summary>
        /// Synchronize sticky header with body horizontal offset
        /// </summary>
        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Keep header aligned horizontally with body
            SyncHeaderToBody();
            UpdateProcessHeaderPositions();
        }

        /// <summary>
        /// (Legacy) Handle external horizontal scrollbar interaction (kept for compatibility)
        /// </summary>
        private void HorizontalScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            if (MainScrollViewer != null)
                MainScrollViewer.ScrollToHorizontalOffset(e.NewValue);
        }

        /// <summary>
        /// Apply the body horizontal offset to the header scrollviewer
        /// </summary>
        private void SyncHeaderToBody()
        {
            if (HeaderScrollViewer == null || MainScrollViewer == null) return;
            HeaderScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset);
        }

        private void MainScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateProcessHeaderPositions();
        }

        private void ProcessHeader_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Border header)
                return;

            var container = FindAncestor<FrameworkElement>(header, element => Equals(element.Tag, ProcessContainerTag));
            if (container == null)
                return;

            if (header.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                header.RenderTransform = transform;
            }

            if (_processHeaders.Any(registration => ReferenceEquals(registration.Header, header)))
                return;

            var registration = new ProcessHeaderRegistration(header, container, transform);
            _processHeaders.Add(registration);

            container.SizeChanged += ProcessContainer_SizeChanged;
            header.SizeChanged += ProcessHeader_SizeChanged;

            UpdateProcessHeaderPositions(registration);
        }

        private void ProcessHeader_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Border header)
                return;

            var index = _processHeaders.FindIndex(registration => ReferenceEquals(registration.Header, header));
            if (index < 0)
                return;

            var registration = _processHeaders[index];
            registration.Container.SizeChanged -= ProcessContainer_SizeChanged;
            registration.Header.SizeChanged -= ProcessHeader_SizeChanged;
            registration.Transform.Y = 0;

            _processHeaders.RemoveAt(index);
        }

        private void ProcessContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not FrameworkElement container)
                return;

            foreach (var registration in _processHeaders.Where(r => ReferenceEquals(r.Container, container)))
            {
                UpdateProcessHeaderPositions(registration);
            }
        }

        private void ProcessHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not Border header)
                return;

            var registration = _processHeaders.FirstOrDefault(r => ReferenceEquals(r.Header, header));
            if (registration != null)
            {
                UpdateProcessHeaderPositions(registration);
            }
        }

        private void UpdateProcessHeaderPositions()
        {
            if (_processHeaders.Count == 0)
                return;

            foreach (var registration in _processHeaders.ToList())
            {
                UpdateProcessHeaderPositions(registration);
            }
        }

        private void UpdateProcessHeaderPositions(ProcessHeaderRegistration registration)
        {
            if (MainScrollViewer == null)
                return;

            if (registration.Header == null || registration.Container == null)
                return;

            if (!registration.Header.IsLoaded || !registration.Container.IsLoaded)
                return;

            registration.Transform.Y = 0;

            registration.Header.UpdateLayout();

            GeneralTransform headerTransform;
            try
            {
                headerTransform = registration.Header.TransformToAncestor(MainScrollViewer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            GeneralTransform containerTransform;
            try
            {
                containerTransform = registration.Container.TransformToAncestor(MainScrollViewer);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var headerTopLeft = headerTransform.Transform(new Point(0, 0));
            var containerBottom = containerTransform.Transform(new Point(0, registration.Container.ActualHeight)).Y;
            var stickyTop = 0d;

            var desiredOffset = 0d;

            if (headerTopLeft.Y < stickyTop)
            {
                desiredOffset = stickyTop - headerTopLeft.Y;
                var maxOffset = containerBottom - stickyTop - registration.Header.ActualHeight;
                if (desiredOffset > maxOffset)
                {
                    desiredOffset = maxOffset;
                }
            }

            registration.Transform.Y = desiredOffset;
        }

        private static T FindAncestor<T>(DependencyObject current, Predicate<T> predicate = null)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target && (predicate == null || predicate(target)))
                    return target;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private sealed class ProcessHeaderRegistration
        {
            public ProcessHeaderRegistration(Border header, FrameworkElement container, TranslateTransform transform)
            {
                Header = header;
                Container = container;
                Transform = transform;
            }

            public Border Header { get; }
            public FrameworkElement Container { get; }
            public TranslateTransform Transform { get; }
        }

        // ================================================
        // MIDDLE-MOUSE DRAG (PANNING) — fixed direction
        // ================================================

        private void MainScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(MainScrollViewer);
                _startH = MainScrollViewer.HorizontalOffset;
                _startV = MainScrollViewer.VerticalOffset;

                MainScrollViewer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;

            var current = e.GetPosition(MainScrollViewer);
            var dx = current.X - _lastPanPoint.X;
            var dy = current.Y - _lastPanPoint.Y;

            // ✅ Natural panning: drag RIGHT -> scroll RIGHT, drag DOWN -> scroll DOWN
            var targetH = _startH + dx;
            var targetV = _startV + dy;

            // Clamp to bounds
            targetH = Math.Max(0, Math.Min(targetH, MainScrollViewer.ScrollableWidth));
            targetV = Math.Max(0, Math.Min(targetV, MainScrollViewer.ScrollableHeight));

            MainScrollViewer.ScrollToHorizontalOffset(targetH);
            MainScrollViewer.ScrollToVerticalOffset(targetV);

            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _isPanning)
            {
                _isPanning = false;
                MainScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                MainScrollViewer.ReleaseMouseCapture();
            }
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
            }
            else
            {
                // Filter by test name, ID, category, or status
                var filtered = _allTests.Where(t =>
                    (t.TestName?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.TestID?.ToString().Contains(searchQuery) ?? false) ||
                    (t.Category?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.RunStatus?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                );

                foreach (var test in filtered)
                {
                    Tests.Add(test);
                }
            }

            // Update statistics based on filtered results
            UpdateStatistics();
        }

        // ================================================
        // GETTERS (for dashboard statistics)
        // ================================================

        public int GetTestCount() => _allTests.Count;
        public int GetProcessCount() => _allTests.Sum(t => t.Processes.Count);
        public int GetFunctionCount() => _allTests.Sum(t => t.Processes.Sum(p => p.Functions.Count));

        // ================================================
        // STATISTICS
        // ================================================

        private void UpdateStatistics()
        {
            TestStatisticsService.Instance.UpdateStatistics(_allTests);
        }

        public void ForceRefreshStatistics()
        {
            UpdateStatistics();
            System.Diagnostics.Debug.WriteLine("✓ Force refreshed statistics for active tab");
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        public void RefreshData()
        {
            LoadTestsFromDatabase();
        }

        // ================================================
        // CLEANUP
        // ================================================

        private void TestsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure we subscribe exactly once
            DatabaseWatcherService.Instance.TestsUpdated -= OnDatabaseTestsUpdated;
            DatabaseWatcherService.Instance.TestsUpdated += OnDatabaseTestsUpdated;

            // Ensure watcher is running (idempotent)
            if (!DatabaseWatcherService.Instance.IsWatching)
                DatabaseWatcherService.Instance.StartWatching();

            // Ensure header/body sync is correct at load
            SyncHeaderToBody();
            UpdateProcessHeaderPositions();

            System.Diagnostics.Debug.WriteLine("✓ TestsView Loaded: subscribed & watcher ensured");
        }

        private void TestsView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Only unsubscribe this view's handler; DO NOT stop the global watcher here
            DatabaseWatcherService.Instance.TestsUpdated -= OnDatabaseTestsUpdated;
            System.Diagnostics.Debug.WriteLine("✓ TestsView Unloaded: unsubscribed (watcher left running)");

            // Safety: release capture if leaving while panning
            if (_isPanning && MainScrollViewer != null)
            {
                _isPanning = false;
                MainScrollViewer.ReleaseMouseCapture();
            }

            foreach (var registration in _processHeaders.ToList())
            {
                registration.Container.SizeChanged -= ProcessContainer_SizeChanged;
                registration.Header.SizeChanged -= ProcessHeader_SizeChanged;
                registration.Transform.Y = 0;
            }

            _processHeaders.Clear();
        }

        // ================================================
        // ROW ACTIONS
        // ================================================

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

        private async void DeleteTest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Test test)
            {
                try
                {
                    int processCount = test.Processes?.Count ?? 0;
                    int functionCount = test.Processes?.Sum(p => p.Functions?.Count ?? 0) ?? 0;

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

                    var result = ModernMessageDialog.ShowConfirmation(
                        warningMessage,
                        "Confirm Deletion",
                        Window.GetWindow(this));

                    if (result == MessageBoxResult.Yes)
                    {
                        button.IsEnabled = false;

                        System.Diagnostics.Debug.WriteLine($"🗑️ Deleting test #{test.Id}...");
                        await _repository.DeleteTestAsync(test.Id);
                        System.Diagnostics.Debug.WriteLine($"✓ Test #{test.Id} deleted successfully!");

                        ModernMessageDialog.ShowSuccess(
                            $"Test '{test.Name}' has been deleted successfully!",
                            "Test Deleted",
                            Window.GetWindow(this));

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

                    if (sender is Button btn) btn.IsEnabled = true;
                }
            }
        }

        // ================================================
        // UTILITIES
        // ================================================

        public void FocusTest(int testId)
        {
            _currentSearchQuery = "";
            FilterTests("");

            var test = _allTests.FirstOrDefault(t => t.Id == testId);
            if (test == null) return;

            test.IsExpanded = true;

            Dispatcher.InvokeAsync(() =>
            {
                TestsItemsControl.UpdateLayout();

                var container = TestsItemsControl.ItemContainerGenerator.ContainerFromItem(test) as FrameworkElement;
                if (container != null)
                {
                    container.BringIntoView();
                }
                else
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        TestsItemsControl.UpdateLayout();
                        var c2 = TestsItemsControl.ItemContainerGenerator.ContainerFromItem(test) as FrameworkElement;
                        c2?.BringIntoView();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ProcRowsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // allow inner ScrollViewer to consume the wheel if it can scroll
            if (sender is ScrollViewer sv)
            {
                var delta = e.Delta;
                if ((delta < 0 && sv.VerticalOffset < sv.ScrollableHeight) ||
                    (delta > 0 && sv.VerticalOffset > 0))
                {
                    e.Handled = true;
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - delta);
                }
            }
        }

    }
}
