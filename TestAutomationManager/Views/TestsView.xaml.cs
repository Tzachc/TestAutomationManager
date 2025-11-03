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

        /// <summary>
        /// Flag to prevent incremental updates during initial load
        /// </summary>
        private bool _isInitialLoad = true;

        private readonly List<ProcessHeaderRegistration> _processHeaders = new();

        private const string ProcessContainerTag = "ProcessContainer";

        /// <summary>
        /// Event fired when data is loaded
        /// </summary>
        public event EventHandler DataLoaded;

        // ----- ScrollViewer reference for panning (from ListBox's internal template) -----
        private ScrollViewer _mainScrollViewer;
        private ScrollViewer MainScrollViewer => _mainScrollViewer ??= GetScrollViewer(TestsItemsControl);

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

            // Reduce panning sensitivity for smoother control (0.5x speed)
            const double panSensitivity = 0.5;
            dx *= panSensitivity;
            dy *= panSensitivity;

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
            // Subscribe to database change events (incremental updates)
            DatabaseWatcherService.Instance.DatabaseChanged += OnDatabaseChanged;

            // Start watching (polls every 3 seconds by default)
            DatabaseWatcherService.Instance.StartWatching();

            System.Diagnostics.Debug.WriteLine("✓ Live database updates enabled");
        }

        /// <summary>
        /// Handle INCREMENTAL database updates (multi-user collaboration)
        /// Only updates the SPECIFIC tests that changed - NOT a full reload!
        /// FAST - no UI freeze!
        /// </summary>
        private void OnDatabaseChanged(object sender, Services.DatabaseChangeEventArgs e)
        {
            // ⭐ Skip incremental updates during initial load
            if (_isInitialLoad)
            {
                System.Diagnostics.Debug.WriteLine("⏭ Skipping incremental update during initial load");
                return;
            }

            if (!e.HasChanges)
                return;

            System.Diagnostics.Debug.WriteLine($"⚡ Applying INCREMENTAL updates: {e.ChangedTests.Count} tests");

            // ⭐ STEP 1: Handle deleted tests
            int deletedCount = 0;
            foreach (var deletedId in e.DeletedTestIds)
            {
                var testToRemove = _allTests.FirstOrDefault(t => t.Id == deletedId);
                if (testToRemove != null)
                {
                    _allTests.Remove(testToRemove);
                    Tests.Remove(testToRemove);
                    deletedCount++;
                }
            }

            // ⭐ STEP 2: Handle new and changed tests
            int updatedCount = 0;
            int addedCount = 0;
            foreach (var freshTest in e.ChangedTests)
            {
                var existingTest = _allTests.FirstOrDefault(t => t.Id == freshTest.Id);

                if (existingTest != null)
                {
                    // ⭐ UPDATE existing test IN-PLACE (preserve pre-loaded data!)
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

                    // Keep existing Processes and AreProcessesLoaded (pre-loaded data!)
                    // INotifyPropertyChanged will auto-update the UI!

                    updatedCount++;
                }
                else
                {
                    // ⭐ NEW test - add it
                    freshTest.PropertyChanged += Test_PropertyChanged;
                    _allTests.Add(freshTest);

                    // Add to filtered list if it matches current filter
                    if (string.IsNullOrEmpty(_currentSearchQuery) ||
                        freshTest.Name.Contains(_currentSearchQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        Tests.Add(freshTest);
                    }

                    addedCount++;
                }
            }

            // ⭐ STEP 3: Update statistics (minimal impact)
            UpdateStatistics();

            System.Diagnostics.Debug.WriteLine($"✅ Incremental update complete: {updatedCount} updated, {addedCount} added, {deletedCount} deleted");
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

                // ⭐ STEP 1: Get all tests from database - FAST with stored procedure!
                var testsFromDb = await _repository.GetAllTestsAsync();
                int totalTests = testsFromDb.Count;
                UpdateLoadingProgress($"Loaded {totalTests} tests...", 50);

                // Clear existing data
                Tests.Clear();
                _allTests.Clear();

                // ⭐ STEP 2: Add tests to UI - FAST! (no processes yet)
                foreach (var test in testsFromDb)
                {
                    Tests.Add(test);
                    _allTests.Add(test);
                    test.PropertyChanged += Test_PropertyChanged;
                    // Processes will be loaded on-demand from cache (instant) or database (fallback)
                }

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {testsFromDb.Count} tests - UI ready!");

                // Update statistics
                UpdateLoadingProgress("Ready!", 100);
                UpdateStatistics();

                // Update progress
                UpdateLoadingProgress($"Loaded {Tests.Count} tests successfully!", 100);

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {Tests.Count} tests from database successfully!");

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Hide loading screen after a short delay
                await System.Threading.Tasks.Task.Delay(300);
                HideLoadingScreen();

                // ⭐ Mark initial load as complete to allow incremental updates
                _isInitialLoad = false;
                System.Diagnostics.Debug.WriteLine("✓ Initial load complete - incremental updates now enabled");

                // ⭐ STEP 3: Start background job to preload ALL processes into cache (non-blocking!)
                System.Diagnostics.Debug.WriteLine("🚀 Starting background process preload into cache...");
                _ = PreloadAllProcessesInBackgroundAsync();

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
        // BACKGROUND PRELOAD
        // ================================================

        /// <summary>
        /// Background job to preload ALL processes into cache (non-blocking!)
        /// This runs AFTER the UI is visible, so users see tests immediately
        /// </summary>
        private async System.Threading.Tasks.Task PreloadAllProcessesInBackgroundAsync()
        {
            try
            {
                // Run on background thread to avoid blocking UI
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("📊 [Background] Loading all processes from database...");

                    var processRepository = new ProcessRepository();
                    var allProcesses = await processRepository.GetAllProcessesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ [Background] Loaded {allProcesses.Count} processes from database");

                    // Group processes by TestID for quick lookup
                    var processesByTestId = allProcesses
                        .Where(p => p.TestID.HasValue)
                        .GroupBy(p => (int)p.TestID.Value)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    System.Diagnostics.Debug.WriteLine($"✓ [Background] Grouped {allProcesses.Count} processes by {processesByTestId.Count} tests");

                    // Add ALL processes to cache (no UI updates, super fast!)
                    int totalProcesses = 0;
                    foreach (var kvp in processesByTestId)
                    {
                        Services.ProcessCacheService.Instance.AddProcessesByTestId(kvp.Key, kvp.Value);
                        totalProcesses += kvp.Value.Count;
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ [Background] Preloaded {totalProcesses} processes into cache - expansion will be instant!");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [Background] Error preloading processes: {ex.Message}");
                // Don't crash - just log the error. Users can still expand tests (will lazy load from database)
            }
        }

        // ================================================
        // LAZY LOADING EVENT HANDLERS (with cache-first strategy!)
        // ================================================

        /// <summary>
        /// Handle Test property changes to detect expansion
        /// Processes are loaded from CACHE (instant) or database (fallback)
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
        /// Functions are still lazy loaded on-demand (too many to preload all at once)
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
        /// Load processes for a specific test (CACHE-FIRST strategy!)
        /// 1. Check cache first (INSTANT - from background preload)
        /// 2. Fallback to database if not in cache (lazy load)
        /// </summary>
        private async System.Threading.Tasks.Task LoadProcessesForTestAsync(Test test)
        {
            if (!test.TestID.HasValue)
                return;

            try
            {
                var testId = (int)test.TestID.Value;
                List<Process> processes;

                // ⭐ STEP 1: Try cache first (INSTANT!)
                var cachedProcesses = Services.ProcessCacheService.Instance.GetProcessesByTestId(testId);
                if (cachedProcesses != null && cachedProcesses.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"⚡ Loading processes for Test #{testId} from CACHE (instant!)");
                    processes = cachedProcesses;
                }
                else
                {
                    // ⭐ STEP 2: Fallback to database (background preload hasn't finished yet)
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading processes for Test #{testId} from DATABASE (cache miss)...");
                    processes = await _repository.GetProcessesForTestAsync(testId);

                    // Add to cache for next time
                    Services.ProcessCacheService.Instance.AddProcessesByTestId(testId, processes);
                }

                // ⭐ STEP 2.5: Sort processes by ProcessPosition (low to high)
                processes = processes.OrderBy(p => p.ProcessPosition).ToList();

                // ⭐ STEP 3: Update UI on UI thread
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
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes for Test #{testId}");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading processes: {ex.Message}");
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

                // ⭐ Add functions to shared cache for ProcessView to use
                Services.ProcessCacheService.Instance.AddFunctions(process.ProcessID.Value, functions);

                // Update UI on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    process.Functions.Clear();
                    foreach (var function in functions)
                    {
                        process.Functions.Add(function);
                    }

                    process.AreFunctionsLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"✓ Lazy loaded {functions.Count} functions for Process #{process.ProcessID} (added to cache)");
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
            // Make sure we subscribe exactly once to the NEW incremental update event
            DatabaseWatcherService.Instance.DatabaseChanged -= OnDatabaseChanged;
            DatabaseWatcherService.Instance.DatabaseChanged += OnDatabaseChanged;

            // Ensure watcher is running (idempotent)
            if (!DatabaseWatcherService.Instance.IsWatching)
                DatabaseWatcherService.Instance.StartWatching();

            // Ensure header/body sync is correct at load
            SyncHeaderToBody();
            UpdateProcessHeaderPositions();

            System.Diagnostics.Debug.WriteLine("✓ TestsView Loaded: subscribed to incremental updates");
        }

        private void TestsView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Only unsubscribe this view's handler; DO NOT stop the global watcher here
            DatabaseWatcherService.Instance.DatabaseChanged -= OnDatabaseChanged;
            System.Diagnostics.Debug.WriteLine("✓ TestsView Unloaded: unsubscribed from incremental updates (watcher left running)");

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
            if (sender is not ScrollViewer scrollViewer)
                return;

            // Check if mouse is actually over this ScrollViewer
            var mousePosition = e.GetPosition(scrollViewer);
            bool isMouseOver = mousePosition.X >= 0 && mousePosition.X <= scrollViewer.ActualWidth &&
                               mousePosition.Y >= 0 && mousePosition.Y <= scrollViewer.ActualHeight;

            if (!isMouseOver)
                return;

            // Only handle if this ScrollViewer has scrollable content
            if (scrollViewer.ScrollableHeight <= 0)
                return;

            var delta = e.Delta;
            bool scrollingDown = delta < 0;
            bool scrollingUp = delta > 0;

            // Check if we can scroll in the requested direction
            bool canScrollDown = scrollingDown && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
            bool canScrollUp = scrollingUp && scrollViewer.VerticalOffset > 0;

            // Only handle if we can actually scroll in the requested direction
            if (canScrollDown || canScrollUp)
            {
                // Moderate scrolling speed: 32 pixels per wheel notch (2 lines of 16px each)
                double scrollAmount = -delta / 120.0 * 32.0;
                double newOffset = scrollViewer.VerticalOffset + scrollAmount;

                // Clamp to valid range
                newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ScrollableHeight));

                scrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
            // If we can't scroll, let the event bubble to parent (main scroll)
        }

        /// <summary>
        /// Prevent automatic scrolling when expanding items inside ProcRowsScrollViewer
        /// This fixes the UI "jump" bug when clicking expand buttons
        /// </summary>
        private void ProcRowsScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Cancel the automatic scroll-to-focused-element behavior
            // This prevents the annoying jump when clicking expand buttons
            e.Handled = true;
        }

        /// <summary>
        /// Prevent automatic scrolling in main ListBox when collapsing/expanding tests
        /// This fixes the UI "jump" bug when collapsing tests that have expanded processes
        /// </summary>
        private void TestsItemsControl_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Cancel the automatic scroll-to-focused-element behavior
            // This prevents the annoying jump when collapsing expanded tests
            e.Handled = true;
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
                LoadingPercentageText.Text = $"{progress:F0}%";
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
                    Duration = TimeSpan.FromMilliseconds(100), // Fast 100ms animation
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                LoadingProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
                LoadingProgressText.Text = message;

                // ⭐ Update percentage display
                LoadingPercentageText.Text = $"{progress:F0}%";
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

        /// <summary>
        /// Get ScrollViewer from ListBox's visual tree (for virtualization support)
        /// </summary>
        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

    }
}
