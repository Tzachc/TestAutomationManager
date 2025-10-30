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

            // ⭐ STEP 1: Build dictionary of existing tests with their pre-loaded data
            var existingTestsDict = _allTests.ToDictionary(t => t.Id, t => t);

            // ⭐ STEP 2: Update tests while preserving pre-loaded processes/functions
            _allTests.Clear();
            foreach (var freshTest in updatedTests)
            {
                Test testToAdd;

                // Check if we have an existing test with pre-loaded data
                if (existingTestsDict.TryGetValue(freshTest.Id, out var existingTest))
                {
                    // ⭐ PRESERVE pre-loaded data from existing test
                    // Update database properties from fresh test
                    existingTest.TestName = freshTest.TestName;
                    existingTest.RunStatus = freshTest.RunStatus;
                    existingTest.LastRunning = freshTest.LastRunning;
                    existingTest.LastTimePass = freshTest.LastTimePass;
                    existingTest.Bugs = freshTest.Bugs;
                    existingTest.ExceptionMessage = freshTest.ExceptionMessage;
                    existingTest.RecipientsEmailsList = freshTest.RecipientsEmailsList;
                    existingTest.SendEmailReport = freshTest.SendEmailReport;
                    existingTest.EmailOnFailureOnly = freshTest.EmailOnFailureOnly;
                    existingTest.ExitTestOnFailure = freshTest.ExitTestOnFailure;
                    existingTest.TestRunAgainTimes = freshTest.TestRunAgainTimes;
                    existingTest.SnapshotMultipleFailure = freshTest.SnapshotMultipleFailure;
                    existingTest.DisableKillDriver = freshTest.DisableKillDriver;

                    // Keep existing IsActive, Category (UI-only properties)
                    // Keep existing Processes and AreProcessesLoaded flag (pre-loaded data!)

                    testToAdd = existingTest;
                }
                else
                {
                    // New test, subscribe to events
                    freshTest.PropertyChanged += Test_PropertyChanged;
                    testToAdd = freshTest;
                }

                _allTests.Add(testToAdd);
            }

            // ⭐ STEP 3: Re-apply current filter
            FilterTests(_currentSearchQuery);

            // ⭐ STEP 4: Update statistics
            UpdateStatistics();

            System.Diagnostics.Debug.WriteLine("✓ UI synchronized with database (pre-loaded data preserved)");
        }

        // ================================================
        // DATA LOADING
        // ================================================

        /// <summary>
        /// Load tests from SQL database with loading screen
        /// </summary>
        private async void LoadTestsFromDatabase()
        {
            try
            {
                // Show loading overlay
                ShowLoadingScreen("Loading tests...", 0);

                // ⭐ CRITICAL: Let UI render the loading screen before blocking
                await System.Threading.Tasks.Task.Delay(50);

                System.Diagnostics.Debug.WriteLine("📊 Loading tests from database...");

                // Get all tests from database (async)
                var testsFromDb = await _repository.GetAllTestsAsync();

                int totalTests = testsFromDb.Count;
                UpdateLoadingProgress($"Processing {totalTests} tests...", 10);
                await System.Threading.Tasks.Task.Delay(10);

                // Clear existing data
                Tests.Clear();
                _allTests.Clear();

                // ⭐ Process tests in SMALL BATCHES for smooth progress updates
                const int batchSize = 10; // Smaller batches = smoother animation
                int processed = 0;

                for (int i = 0; i < testsFromDb.Count; i += batchSize)
                {
                    // Process a batch
                    int batchEnd = Math.Min(i + batchSize, testsFromDb.Count);

                    for (int j = i; j < batchEnd; j++)
                    {
                        var test = testsFromDb[j];
                        Tests.Add(test);
                        _allTests.Add(test);

                        // ⭐ Subscribe to PropertyChanged for lazy loading
                        test.PropertyChanged += Test_PropertyChanged;
                        processed++;
                    }

                    // Update progress after each batch (with smooth animation)
                    double progress = 10 + (processed / (double)totalTests * 80); // 10-90%
                    UpdateLoadingProgress($"Loaded {processed}/{totalTests} tests...", progress);

                    // ⭐ CRITICAL: Yield to UI thread so progress bar can animate smoothly!
                    await System.Threading.Tasks.Task.Delay(1);
                }

                // Update statistics
                UpdateLoadingProgress("Finalizing...", 95);
                await System.Threading.Tasks.Task.Delay(10);

                UpdateStatistics();

                // Update progress
                UpdateLoadingProgress($"Loaded {Tests.Count} tests successfully!", 100);

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {Tests.Count} tests from database successfully!");

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Hide loading screen after a short delay
                await System.Threading.Tasks.Task.Delay(300);
                HideLoadingScreen();

                // ⭐ START BACKGROUND PRE-LOADING after UI is responsive
                StartBackgroundPreloading();

                // Show message if no data
                if (Tests.Count == 0)
                {
                    MessageBox.Show("No tests found in database.\n\nMake sure you ran the SQL scripts to create sample data.",
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                HideLoadingScreen();
                System.Diagnostics.Debug.WriteLine($"✗ Error loading tests: {ex.Message}");
                MessageBox.Show($"Failed to load tests from database.\n\nError: {ex.Message}\n\nCheck:\n1. Database connection\n2. SQL scripts ran\n3. DbConnectionConfig settings",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // LAZY LOADING EVENT HANDLERS
        // ================================================

        /// <summary>
        /// Handle Test property changes to detect expansion and lazy load processes
        /// </summary>
        private async void Test_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Test.IsExpanded) && sender is Test test)
            {
                // Only load if expanded and not already loaded
                if (test.IsExpanded && !test.AreProcessesLoaded)
                {
                    await LoadProcessesForTestAsync(test);
                }
            }
        }

        /// <summary>
        /// Handle Process property changes to detect expansion and lazy load functions
        /// </summary>
        private async void Process_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Process.IsExpanded) && sender is Process process)
            {
                // Only load if expanded and not already loaded
                if (process.IsExpanded && !process.AreFunctionsLoaded)
                {
                    await LoadFunctionsForProcessAsync(process);
                }
            }
        }

        /// <summary>
        /// Lazy load processes for a specific test
        /// </summary>
        private async System.Threading.Tasks.Task LoadProcessesForTestAsync(Test test)
        {
            if (!test.TestID.HasValue)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"⏳ Lazy loading processes for Test #{test.TestID}...");

                var processes = await _repository.GetProcessesForTestAsync((int)test.TestID.Value);

                // Update UI on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    test.Processes.Clear();
                    foreach (var process in processes)
                    {
                        test.Processes.Add(process);

                        // Subscribe to process expansion events for lazy loading functions
                        process.PropertyChanged += Process_PropertyChanged;
                    }

                    test.AreProcessesLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"✓ Lazy loaded {processes.Count} processes for Test #{test.TestID}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error lazy loading processes: {ex.Message}");
                MessageBox.Show($"Failed to load processes for test.\n\nError: {ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Lazy load functions for a specific process
        /// </summary>
        private async System.Threading.Tasks.Task LoadFunctionsForProcessAsync(Process process)
        {
            if (!process.ProcessID.HasValue)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"⏳ Lazy loading functions for Process #{process.ProcessID}...");

                var functions = await _repository.GetFunctionsForProcessAsync(process.ProcessID.Value);

                // Update UI on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    process.Functions.Clear();
                    foreach (var function in functions)
                    {
                        process.Functions.Add(function);
                    }

                    process.AreFunctionsLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"✓ Lazy loaded {functions.Count} functions for Process #{process.ProcessID}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error lazy loading functions: {ex.Message}");
                MessageBox.Show($"Failed to load functions for process.\n\nError: {ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        /// <summary>
        /// Get total process count using efficient database query
        /// (Lazy loading means we can't rely on in-memory counts)
        /// </summary>
        public async System.Threading.Tasks.Task<int> GetProcessCountAsync()
        {
            try
            {
                return await _repository.GetTotalProcessCountAsync();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get total function count using efficient database query
        /// (Lazy loading means we can't rely on in-memory counts)
        /// </summary>
        public async System.Threading.Tasks.Task<int> GetFunctionCountAsync()
        {
            try
            {
                return await _repository.GetTotalFunctionCountAsync();
            }
            catch
            {
                return 0;
            }
        }

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

        // ================================================
        // LOADING SCREEN HELPERS
        // ================================================

        /// <summary>
        /// Show loading overlay with progress
        /// </summary>
        private void ShowLoadingScreen(string message, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingProgressBar.Value = progress;
                LoadingProgressText.Text = message;
            });
        }

        /// <summary>
        /// Update loading progress with smooth animation
        /// </summary>
        private void UpdateLoadingProgress(string message, double progress)
        {
            Dispatcher.Invoke(() =>
            {
                // ⭐ Animate progress bar smoothly instead of jumping
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = LoadingProgressBar.Value,
                    To = progress,
                    Duration = TimeSpan.FromMilliseconds(200), // Smooth 200ms transition
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                LoadingProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
                LoadingProgressText.Text = message;
            });
        }

        /// <summary>
        /// Hide loading overlay
        /// </summary>
        private void HideLoadingScreen()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }

        // ================================================
        // BACKGROUND PRE-LOADING
        // ================================================

        private bool _isBackgroundLoadingRunning = false;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Test> _preloadQueue = new();

        /// <summary>
        /// Start background pre-loading of processes/functions after initial UI load
        /// Loads data in the background so subsequent expansions are instant
        /// </summary>
        private async void StartBackgroundPreloading()
        {
            if (_isBackgroundLoadingRunning)
                return;

            _isBackgroundLoadingRunning = true;
            System.Diagnostics.Debug.WriteLine("🚀 Starting background pre-loading...");

            // Build priority queue: Active tests first, then others
            var activeTests = _allTests.Where(t => t.IsActive).ToList();
            var inactiveTests = _allTests.Where(t => !t.IsActive).ToList();

            // Add active tests first (user is more likely to use these)
            foreach (var test in activeTests)
                _preloadQueue.Enqueue(test);

            // Then add inactive tests
            foreach (var test in inactiveTests)
                _preloadQueue.Enqueue(test);

            // Start background loading task
            await System.Threading.Tasks.Task.Run(async () => await BackgroundPreloadWorker());
        }

        /// <summary>
        /// Background worker that pre-loads data with throttling
        /// </summary>
        private async System.Threading.Tasks.Task BackgroundPreloadWorker()
        {
            int testsLoaded = 0;
            int totalTests = _preloadQueue.Count;

            while (_preloadQueue.TryDequeue(out Test test))
            {
                try
                {
                    // Only load if not already loaded
                    if (!test.AreProcessesLoaded)
                    {
                        // Load processes for this test
                        var processes = await _repository.GetProcessesForTestAsync((int)test.TestID.Value);

                        // Update UI on UI thread
                        await Dispatcher.InvokeAsync(() =>
                        {
                            test.Processes.Clear();
                            foreach (var process in processes)
                            {
                                test.Processes.Add(process);
                                process.PropertyChanged += Process_PropertyChanged;
                            }
                            test.AreProcessesLoaded = true;
                        });

                        testsLoaded++;

                        // Log progress every 50 tests
                        if (testsLoaded % 50 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"📦 Background pre-loaded {testsLoaded}/{totalTests} tests");
                        }

                        // Throttle to avoid overwhelming database/UI (load 5 tests, pause 100ms)
                        if (testsLoaded % 5 == 0)
                        {
                            await System.Threading.Tasks.Task.Delay(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Background preload error for test #{test.TestID}: {ex.Message}");
                    // Continue with next test
                }
            }

            _isBackgroundLoadingRunning = false;
            System.Diagnostics.Debug.WriteLine($"✓ Background pre-loading completed! Loaded {testsLoaded}/{totalTests} tests");
        }

    }
}
