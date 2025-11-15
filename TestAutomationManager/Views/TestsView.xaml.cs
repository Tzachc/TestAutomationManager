using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Controls;
using TestAutomationManager.Dialogs;
using TestAutomationManager.Helpers;
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
        /// Process repository for database operations
        /// </summary>
        private readonly ProcessRepository _processRepository;

        /// <summary>
        /// Edit service for inline editing
        /// </summary>
        private readonly TestEditService _editService;

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

        // ----- Copy/Paste selection state -----
        private bool _isSelecting = false;
        private Process? _selectionStartProcess = null;
        private Function? _selectionStartFunction = null;
        private List<Process> _copiedProcesses = new();
        private List<Function> _copiedFunctions = new();
        private double _startH;
        private double _startV;
        private DateTime _lastPanTime;
        private double _panVelocity;

        // ----- Scroll area focus tracking -----
        private ScrollViewer _focusedScrollViewer = null;
        private Border _focusedScrollBorder = null;

        // ----- Filter management -----
        private FilterManager<Test> _filterManager;
        private Button _currentFilterButton;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public TestsView()
        {
            InitializeComponent();

            // Initialize repositories
            _repository = new TestRepository();
            _processRepository = new ProcessRepository();

            // Initialize edit service
            _editService = new TestEditService(_repository, _processRepository);

            // Register global inline edit handler
            InlineEditHelper.SetEditConfirmedHandler(this, OnInlineEditConfirmed);

            // Initialize collections
            Tests = new ObservableCollection<Test>();
            _allTests = new ObservableCollection<Test>();

            // Initialize filter manager
            _filterManager = new FilterManager<Test>(_allTests, Tests);

            // Set data context
            TestsItemsControl.ItemsSource = Tests;

            // Load initial data from database
            LoadTestsFromDatabase();

            // ⭐ START DATABASE WATCHER for live updates
            StartLiveUpdates();

            // Register keyboard shortcuts for copy/paste
            this.KeyDown += TestsView_KeyDown;
            this.Focusable = true;
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
                _lastPanTime = DateTime.Now;
                _panVelocity = 0;

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

            // ⚡ Calculate velocity (pixels per millisecond)
            var currentTime = DateTime.Now;
            var elapsedMs = (currentTime - _lastPanTime).TotalMilliseconds;
            if (elapsedMs > 0)
            {
                var distance = Math.Sqrt(dx * dx + dy * dy);
                _panVelocity = distance / elapsedMs;
                _lastPanTime = currentTime;
            }

            // 🎯 Smart adaptive damping based on velocity
            // Slow movements (< 0.5 px/ms): 0.2x speed (precise control)
            // Medium movements (0.5-2 px/ms): 0.4-1.0x speed (smooth ramping)
            // Fast movements (> 2 px/ms): 1.0-2.0x speed (responsive)
            double dampingFactor;
            if (_panVelocity < 0.5)
            {
                // Very slow = very precise (20% speed)
                dampingFactor = 0.2;
            }
            else if (_panVelocity < 2.0)
            {
                // Medium speed = linear ramp from 0.4 to 1.0
                dampingFactor = 0.4 + (_panVelocity - 0.5) * 0.4;
            }
            else
            {
                // Fast speed = accelerated (up to 2x for very fast movements)
                dampingFactor = Math.Min(2.0, 1.0 + (_panVelocity - 2.0) * 0.3);
            }

            dx *= dampingFactor;
            dy *= dampingFactor;

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

        /// <summary>
        /// Handle mouse wheel scrolling for the main view with reduced speed
        /// </summary>
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainScrollViewer == null)
                return;

            // Only handle if there's no focused inner ScrollViewer
            if (_focusedScrollViewer != null)
                return;

            // Only handle if this ScrollViewer actually has scrollable content
            if (MainScrollViewer.ScrollableHeight <= 0)
                return;

            var delta = e.Delta;

            // Check if we can scroll in the requested direction
            bool canScrollDown = delta < 0 && MainScrollViewer.VerticalOffset < MainScrollViewer.ScrollableHeight;
            bool canScrollUp = delta > 0 && MainScrollViewer.VerticalOffset > 0;

            if (canScrollDown || canScrollUp)
            {
                const double lineHeightPx = 16.0;
                const double linesPerNotch = 2.5;         // Smooth scrolling - 2.5 lines per notch (40px)
                double scrollAmount = -delta / 120.0 * (linesPerNotch * lineHeightPx);
                double newOffset = MainScrollViewer.VerticalOffset + scrollAmount;

                // Clamp to valid range
                newOffset = Math.Max(0, Math.Min(newOffset, MainScrollViewer.ScrollableHeight));

                MainScrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
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
        /// Also handles ProcessID changes to reload template data
        /// </summary>
        private async void Process_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not Process process)
                return;

            // Handle expansion - lazy load functions
            if (e.PropertyName == nameof(Process.IsExpanded))
            {
                // Only load if expanded and not already loaded
                if (process.IsExpanded && !process.AreFunctionsLoaded)
                {
                    await LoadFunctionsForProcessAsync(process);
                }
            }

            // Handle ProcessID change on existing process - reload template data
            if (e.PropertyName == nameof(Process.ProcessID) && !process.IsPlaceholder && process.ProcessID.HasValue)
            {
                await HandleProcessIdChange(process, process.ProcessID.Value);
            }
        }

        /// <summary>
        /// Handle ProcessID change on an existing process
        /// Reloads template data and functions from the new ProcessID
        /// </summary>
        private async System.Threading.Tasks.Task HandleProcessIdChange(Process process, double newProcessId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 ProcessID changed to {newProcessId} on existing process (Index: {process.Index})");

                // Check if new ProcessID exists in database
                bool exists = await _processRepository.ProcessIdExistsAsync(newProcessId);

                if (exists)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ ProcessID {newProcessId} exists - loading template and functions...");

                    // Get the template process (for copying parameters, etc.)
                    var templateProcess = await _processRepository.GetProcessTemplateByIdAsync(newProcessId);

                    if (templateProcess == null)
                    {
                        MessageBox.Show($"ProcessID {newProcessId} not found in database.",
                            "Process Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Load all functions for this ProcessID
                    var functions = await _processRepository.GetFunctionsForProcessAsync(newProcessId);

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded template process and {functions.Count} functions for ProcessID {newProcessId}");

                    // Update process with template data (keep TestID and Index, but update everything else)
                    process.WEB3Operator = templateProcess.WEB3Operator;
                    process.Pass_Fail_WEB3Operator = templateProcess.Pass_Fail_WEB3Operator;
                    process.Comments = templateProcess.Comments;
                    process.Module = templateProcess.Module;
                    process.Repeat = templateProcess.Repeat;

                    // NOTE: Params 1-46 are intentionally left as-is (not copied from template)
                    // The C# automation framework will load these values

                    // Update database
                    await _processRepository.UpdateProcessAsync(process);

                    // Update functions in UI
                    await Dispatcher.InvokeAsync(() =>
                    {
                        process.Functions.Clear();
                        foreach (var function in functions)
                        {
                            function.ParentProcess = process;
                            process.Functions.Add(function);
                        }
                        process.AreFunctionsLoaded = true;

                        System.Diagnostics.Debug.WriteLine($"✅ Updated process with ProcessID {newProcessId} and loaded {functions.Count} functions");

                        // Expand to show the new functions
                        process.IsExpanded = true;
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ℹ ProcessID {newProcessId} is new - updating database with new ID (keeping existing data)");

                    // Just update the ProcessID in database, keep all other data
                    await _processRepository.UpdateProcessAsync(process);

                    // Clear functions since this is a new ProcessID with no functions
                    await Dispatcher.InvokeAsync(() =>
                    {
                        process.Functions.Clear();
                        process.AreFunctionsLoaded = true;

                        System.Diagnostics.Debug.WriteLine($"✅ Updated process to new ProcessID {newProcessId}");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling ProcessID change: {ex.Message}");
                MessageBox.Show($"Failed to update process with new ProcessID.\n\nError: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle placeholder process property changes to detect when user enters ProcessID
        /// This implements the "New" process adding functionality
        /// </summary>
        private async void PlaceholderProcess_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Process.ProcessID) && sender is Process placeholder)
            {
                // Only handle if this is still a placeholder and ProcessID is not null
                if (!placeholder.IsPlaceholder || !placeholder.ProcessID.HasValue)
                    return;

                try
                {
                    var enteredProcessId = placeholder.ProcessID.Value;
                    System.Diagnostics.Debug.WriteLine($"🆕 User entered ProcessID {enteredProcessId} in placeholder row");

                    // Check if ProcessID exists in database
                    bool exists = await _processRepository.ProcessIdExistsAsync(enteredProcessId);

                    if (exists)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ ProcessID {enteredProcessId} exists - loading template and functions...");
                        await HandleExistingProcessId(placeholder, enteredProcessId);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ℹ ProcessID {enteredProcessId} is new - creating empty process...");
                        await HandleNewProcessId(placeholder, enteredProcessId);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error handling placeholder ProcessID: {ex.Message}");
                    MessageBox.Show($"Failed to create process.\n\nError: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Reset placeholder
                    if (sender is Process p)
                    {
                        p.ProcessID = null;
                    }
                }
            }
        }

        /// <summary>
        /// Handle when user enters an EXISTING ProcessID in the placeholder
        /// Load all functions from that ProcessID and create a new process record
        /// </summary>
        private async System.Threading.Tasks.Task HandleExistingProcessId(Process placeholder, double processId)
        {
            try
            {
                // Get the template process (for copying parameters, etc.)
                var templateProcess = await _processRepository.GetProcessTemplateByIdAsync(processId);

                if (templateProcess == null)
                {
                    MessageBox.Show($"ProcessID {processId} not found in database.",
                        "Process Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    placeholder.ProcessID = null;
                    return;
                }

                // Load all functions for this ProcessID
                var functions = await _processRepository.GetFunctionsForProcessAsync(processId);

                System.Diagnostics.Debug.WriteLine($"✓ Loaded template process and {functions.Count} functions for ProcessID {processId}");

                // Create new process record with the same ProcessID but linked to current test
                var newProcess = new Process
                {
                    TestID = placeholder.TestID,
                    ProcessID = processId,
                    ProcessPosition = placeholder.ProcessPosition,
                    // Leave ProcessName and all Params empty - the automation framework will load them
                    ProcessName = null,
                    WEB3Operator = templateProcess.WEB3Operator,
                    Pass_Fail_WEB3Operator = templateProcess.Pass_Fail_WEB3Operator,
                    Comments = templateProcess.Comments,
                    Module = templateProcess.Module,
                    Repeat = templateProcess.Repeat,
                    IsPlaceholder = false,
                    ParentTest = placeholder.ParentTest,
                    Functions = new ObservableCollection<Function>(),
                    AreFunctionsLoaded = true
                };

                // NOTE: Params 1-46 are intentionally left empty (null)
                // The C# automation framework will load these values

                // Insert into database
                var insertedProcess = await _processRepository.InsertProcessAsync(newProcess);

                // Add functions to UI (these are loaded from existing ProcessID, not newly created)
                foreach (var function in functions)
                {
                    function.ParentProcess = insertedProcess;
                    insertedProcess.Functions.Add(function);
                }

                // Update UI - use Remove/Insert instead of array indexer to force WPF to re-render
                await Dispatcher.InvokeAsync(() =>
                {
                    var test = placeholder.ParentTest;
                    if (test != null)
                    {
                        // Find placeholder index
                        int placeholderIndex = test.Processes.IndexOf(placeholder);

                        if (placeholderIndex >= 0)
                        {
                            // Unsubscribe from placeholder events
                            placeholder.PropertyChanged -= PlaceholderProcess_PropertyChanged;

                            // Remove placeholder and insert real process at same position
                            // This forces WPF to re-render the row and attach InlineEditHelper
                            test.Processes.RemoveAt(placeholderIndex);
                            test.Processes.Insert(placeholderIndex, insertedProcess);

                            // Subscribe to real process events
                            insertedProcess.PropertyChanged += Process_PropertyChanged;

                            // Add new placeholder at the end
                            var newPlaceholder = new Process
                            {
                                IsPlaceholder = true,
                                ParentTest = test,
                                TestID = test.TestID,
                                ProcessPosition = insertedProcess.ProcessPosition + 1,
                                Functions = new ObservableCollection<Function>(),
                                AreFunctionsLoaded = true
                            };

                            newPlaceholder.PropertyChanged += PlaceholderProcess_PropertyChanged;
                            test.Processes.Add(newPlaceholder);

                            System.Diagnostics.Debug.WriteLine($"✅ Added process with existing ProcessID {processId} and {functions.Count} functions to Test #{test.TestID}");

                            // Expand the new process to show functions
                            insertedProcess.IsExpanded = true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling existing ProcessID: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handle when user enters a NEW ProcessID in the placeholder
        /// Create an empty process record
        /// </summary>
        private async System.Threading.Tasks.Task HandleNewProcessId(Process placeholder, double processId)
        {
            try
            {
                // Create new empty process
                var newProcess = new Process
                {
                    TestID = placeholder.TestID,
                    ProcessID = processId,
                    ProcessPosition = placeholder.ProcessPosition,
                    ProcessName = null,  // Empty as per requirements
                    IsPlaceholder = false,
                    ParentTest = placeholder.ParentTest,
                    Functions = new ObservableCollection<Function>(),
                    AreFunctionsLoaded = true  // No functions to load for new process
                };

                // Insert into database
                var insertedProcess = await _processRepository.InsertProcessAsync(newProcess);

                // Update UI
                await Dispatcher.InvokeAsync(() =>
                {
                    var test = placeholder.ParentTest;
                    if (test != null)
                    {
                        // Find placeholder index
                        int placeholderIndex = test.Processes.IndexOf(placeholder);

                        if (placeholderIndex >= 0)
                        {
                            // Unsubscribe from placeholder events
                            placeholder.PropertyChanged -= PlaceholderProcess_PropertyChanged;

                            // Remove placeholder and insert real process at same position
                            // This forces WPF to re-render the row and attach InlineEditHelper
                            test.Processes.RemoveAt(placeholderIndex);
                            test.Processes.Insert(placeholderIndex, insertedProcess);

                            // Subscribe to real process events
                            insertedProcess.PropertyChanged += Process_PropertyChanged;

                            // Add new placeholder at the end
                            var newPlaceholder = new Process
                            {
                                IsPlaceholder = true,
                                ParentTest = test,
                                TestID = test.TestID,
                                ProcessPosition = insertedProcess.ProcessPosition + 1,
                                Functions = new ObservableCollection<Function>(),
                                AreFunctionsLoaded = true
                            };

                            newPlaceholder.PropertyChanged += PlaceholderProcess_PropertyChanged;
                            test.Processes.Add(newPlaceholder);

                            System.Diagnostics.Debug.WriteLine($"✅ Added new empty process with ProcessID {processId} to Test #{test.TestID}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling new ProcessID: {ex.Message}");
                throw;
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
                        // Set parent reference for navigation
                        process.ParentTest = test;

                        test.Processes.Add(process);

                        // Subscribe to process expansion events for lazy loading functions
                        process.PropertyChanged += Process_PropertyChanged;
                    }

                    // ⭐ STEP 4: Add placeholder "(New)" row at the bottom
                    var placeholderProcess = new Process
                    {
                        IsPlaceholder = true,
                        ParentTest = test,
                        TestID = test.TestID,
                        ProcessPosition = (processes.Any() ? processes.Max(p => p.ProcessPosition ?? 0) + 1 : 1),
                        Functions = new ObservableCollection<Function>(),
                        AreFunctionsLoaded = true
                    };

                    // Subscribe to property changes to detect when user enters ProcessID
                    placeholderProcess.PropertyChanged += PlaceholderProcess_PropertyChanged;

                    test.Processes.Add(placeholderProcess);

                    test.AreProcessesLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes for Test #{testId} + 1 placeholder row");
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
                        // Set parent reference for navigation
                        function.ParentProcess = process;

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

            // Only handle if this ScrollViewer actually has scrollable content
            if (scrollViewer.ScrollableHeight <= 0)
                return;

            // ⚡ NEW LOGIC: Check if mouse is directly over THIS ScrollViewer (not a child ScrollViewer)
            // This ensures nested scroll areas work independently
            var mousePos = e.GetPosition(scrollViewer);
            var isMouseOver = mousePos.X >= 0 && mousePos.X <= scrollViewer.ActualWidth &&
                              mousePos.Y >= 0 && mousePos.Y <= scrollViewer.ActualHeight;

            if (!isMouseOver)
                return;

            // Check if there's a child ScrollViewer under the mouse that should handle this instead
            var elementUnderMouse = scrollViewer.InputHitTest(mousePos) as DependencyObject;
            if (elementUnderMouse != null)
            {
                // Walk up the visual tree to see if there's a ScrollViewer between the element and this one
                var current = elementUnderMouse;
                while (current != null && current != scrollViewer)
                {
                    if (current is ScrollViewer childScrollViewer &&
                        childScrollViewer != scrollViewer &&
                        childScrollViewer.ScrollableHeight > 0)
                    {
                        // There's a child ScrollViewer with scrollable content - let it handle this
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            var delta = e.Delta;

            // Check if we can scroll in the requested direction
            bool canScrollDown = delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
            bool canScrollUp = delta > 0 && scrollViewer.VerticalOffset > 0;

            if (canScrollDown || canScrollUp)
            {
                const double lineHeightPx = 16.0;
                const double linesPerNotch = 2.0;         // Smooth but responsive scrolling
                double scrollAmount = -delta / 120.0 * (linesPerNotch * lineHeightPx);
                double newOffset = scrollViewer.VerticalOffset + scrollAmount;


                // Clamp to valid range
                newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ScrollableHeight));

                scrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
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
        // SCROLL AREA FOCUS HANDLERS
        // ================================================

        /// <summary>
        /// Show hover indication when mouse enters a scroll area
        /// </summary>
        private void ScrollArea_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // Find the parent Border for this ScrollViewer
            var border = FindScrollBorder(scrollViewer);
            if (border == null)
                return;

            // Show hover indication (only if not already focused)
            if (_focusedScrollViewer != scrollViewer)
            {
                // Subtle hover with light background tint
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x3B, 0x9F, 0xF3)); // Subtle blue border
                border.Background = new SolidColorBrush(Color.FromArgb(0x08, 0x3B, 0x9F, 0xF3)); // Very light blue tint
            }
        }

        /// <summary>
        /// Remove hover indication when mouse leaves a scroll area
        /// </summary>
        private void ScrollArea_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // Find the parent Border for this ScrollViewer
            var border = FindScrollBorder(scrollViewer);
            if (border == null)
                return;

            // Remove hover indication (only if not focused)
            if (_focusedScrollViewer != scrollViewer)
            {
                border.BorderBrush = Brushes.Transparent;
                border.Background = Brushes.Transparent;
            }
        }

        /// <summary>
        /// Set focus on a scroll area when clicked
        /// </summary>
        private void ScrollArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // Find the parent Border for this ScrollViewer
            var border = FindScrollBorder(scrollViewer);
            if (border == null)
                return;

            // Clear previous focus
            if (_focusedScrollBorder != null && _focusedScrollBorder != border)
            {
                _focusedScrollBorder.BorderBrush = Brushes.Transparent;
                _focusedScrollBorder.Background = Brushes.Transparent;
            }

            // Set new focus with clean modern styling
            _focusedScrollViewer = scrollViewer;
            _focusedScrollBorder = border;
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xB0, 0x3B, 0x9F, 0xF3)); // Modern blue with good opacity
            border.Background = new SolidColorBrush(Color.FromArgb(0x12, 0x3B, 0x9F, 0xF3)); // Light blue tint background
        }

        /// <summary>
        /// Find the parent Border for a ScrollViewer (identified by Tag="FocusBorder")
        /// </summary>
        private Border FindScrollBorder(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                return null;

            var parent = VisualTreeHelper.GetParent(scrollViewer);
            if (parent is Border border && border.Tag?.ToString() == "FocusBorder")
            {
                return border;
            }

            return null;
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

        // ================================================
        // FILTER METHODS
        // ================================================

        /// <summary>
        /// Show filter popup for a column
        /// </summary>
        private void ShowFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag == null)
                return;

            // Parse the Tag: "ColumnName|PropertyName"
            var tag = button.Tag.ToString();
            var parts = tag.Split('|');
            if (parts.Length != 2)
                return;

            var columnName = parts[0];
            var propertyName = parts[1];

            // Get or create filter for this column
            var filter = _filterManager.GetColumnFilter(columnName, propertyName);

            // Initialize the filter control
            FilterControl.Initialize(filter);

            // Position the popup relative to the button
            FilterPopup.PlacementTarget = button;
            FilterPopup.IsOpen = true;

            _currentFilterButton = button;
        }

        /// <summary>
        /// Apply filter and close popup
        /// </summary>
        private void FilterControl_FilterApplied(object sender, ColumnFilter filter)
        {
            FilterPopup.IsOpen = false;

            // Apply all filters
            _filterManager.ApplyFilters();

            // Update statistics
            UpdateStatistics();

            System.Diagnostics.Debug.WriteLine($"✓ Filter applied: {filter.ColumnName}");
        }

        /// <summary>
        /// Clear filter and close popup
        /// </summary>
        private void FilterControl_FilterCleared(object sender, EventArgs e)
        {
            FilterPopup.IsOpen = false;

            // Apply all filters (which will show all items if no filters are active)
            _filterManager.ApplyFilters();

            // Update statistics
            UpdateStatistics();

            System.Diagnostics.Debug.WriteLine("✓ Filter cleared");
        }

        // ================================================
        // INLINE EDITING
        // ================================================

        /// <summary>
        /// Global handler for inline edit confirmations (used by InlineEditHelper)
        /// </summary>
        private async void OnInlineEditConfirmed(object sender, Helpers.EditConfirmedEventArgs e)
        {
            // Handle Test editing
            if (e.DataContext is Test test)
            {
                var result = await _editService.EditTestFieldAsync(test, e.FieldName, e.OldValue, e.NewValue, Window.GetWindow(this));

                if (!result.IsSuccess)
                {
                    e.Cancel = true;
                    e.CancelReason = result.Message;

                    if (!result.IsCancelled)
                    {
                        MessageBox.Show(result.Message, "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            // Handle Process editing
            else if (e.DataContext is Process process)
            {
                var result = await _editService.EditProcessFieldAsync(process, e.FieldName, e.OldValue, e.NewValue, Window.GetWindow(this));

                if (!result.IsSuccess)
                {
                    e.Cancel = true;
                    e.CancelReason = result.Message;

                    if (!result.IsCancelled)
                    {
                        MessageBox.Show(result.Message, "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            // Handle Function editing
            else if (e.DataContext is Function function)
            {
                var result = await _editService.EditFunctionFieldAsync(function, e.FieldName, e.OldValue, e.NewValue, Window.GetWindow(this));

                if (!result.IsSuccess)
                {
                    e.Cancel = true;
                    e.CancelReason = result.Message;

                    if (!result.IsCancelled)
                    {
                        MessageBox.Show(result.Message, "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ================================================
        // PARAMETER NAVIGATION
        // ================================================

        /// <summary>
        /// Scrolls to a specific parameter in a process
        /// Called when navigating from From_Process_ pattern
        /// </summary>
        public void ScrollToProcessParameter(Test test, Process process, string paramName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Navigating to Process Parameter: {paramName} in Process #{process.ProcessID}");

                // Ensure the test is expanded
                if (!test.IsExpanded)
                {
                    test.IsExpanded = true;
                }

                // Wait for processes to load if needed
                if (!test.AreProcessesLoaded)
                {
                    // Subscribe to property changed to continue after processes load
                    PropertyChangedEventHandler? handler = null;
                    handler = (s, e) =>
                    {
                        if (e.PropertyName == nameof(Test.AreProcessesLoaded) && test.AreProcessesLoaded)
                        {
                            test.PropertyChanged -= handler;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                PerformProcessParameterScroll(test, process, paramName);
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    };
                    test.PropertyChanged += handler;
                    return;
                }

                PerformProcessParameterScroll(test, process, paramName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to process parameter: {ex.Message}");
                MessageBox.Show($"Failed to navigate to parameter.\n\nError: {ex.Message}",
                    "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Performs the actual scrolling to the process parameter
        /// </summary>
        private void PerformProcessParameterScroll(Test test, Process process, string paramName)
        {
            try
            {
                // Scroll to test first
                TestsItemsControl.ScrollIntoView(test);

                // Give UI time to render the test container
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Find the test container (for highlighting later)
                    var testContainer = TestsItemsControl.ItemContainerGenerator.ContainerFromItem(test) as FrameworkElement;
                    if (testContainer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Could not find test container");
                        return;
                    }

                    // Find the MAIN horizontal ScrollViewer - it's the internal ScrollViewer of the TestsItemsControl ListBox
                    // This ScrollViewer handles horizontal scrolling for ALL the wide grids (Tests, Processes, Functions)
                    var mainScrollViewer = FindVisualChild<ScrollViewer>(TestsItemsControl);

                    if (mainScrollViewer == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Could not find main horizontal ScrollViewer in TestsItemsControl");
                        return;
                    }

                    // Calculate column position based on parameter name
                    int paramNumber = ExtractParamNumber(paramName);
                    if (paramNumber <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Invalid param number for {paramName}");
                        return;
                    }

                    // Calculate horizontal scroll offset
                    double offset = CalculateParamColumnOffset(paramNumber);

                    System.Diagnostics.Debug.WriteLine($"Scrolling to {paramName} (offset: {offset}, current: {mainScrollViewer.HorizontalOffset})");

                    // Perform the scroll on the main horizontal ScrollViewer
                    mainScrollViewer.ScrollToHorizontalOffset(offset);

                    // Force update the layout
                    mainScrollViewer.UpdateLayout();

                    System.Diagnostics.Debug.WriteLine($"✓ After scroll: HorizontalOffset = {mainScrollViewer.HorizontalOffset}");

                    // Wait for scroll to complete, then highlight
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    timer.Tick += (s, e) =>
                    {
                        HighlightProcessParameter(testContainer, process, paramName);
                        timer.Stop();
                    };
                    timer.Start();

                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scrolling to parameter: {ex.Message}");
            }
        }

        private void HighlightProcessParameter(FrameworkElement testContainer, Process process, string paramName)
        {
            try
            {
                // Find all TextBlocks in the test container
                var textBlocks = FindVisualChildren<TextBlock>(testContainer);

                // Find the TextBlock that matches the process and parameter
                foreach (var textBlock in textBlocks)
                {
                    if (textBlock.DataContext == process)
                    {
                        // Check if this TextBlock has the InlineEditHelper.FieldName set to our param
                        var fieldName = Helpers.InlineEditHelper.GetFieldName(textBlock);
                        if (fieldName == paramName)
                        {
                            // Store original styling
                            var originalBackground = textBlock.Background;
                            var originalBorderBrush = textBlock.Tag as Brush;

                            // Apply highlight
                            textBlock.Background = new SolidColorBrush(Color.FromRgb(255, 255, 153)); // Light yellow
                            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
                            {
                                Color = Color.FromRgb(255, 165, 0), // Orange glow
                                BlurRadius = 10,
                                ShadowDepth = 0,
                                Opacity = 0.8
                            };

                            System.Diagnostics.Debug.WriteLine($"✓ Applied highlight to {paramName}");

                            // Remove highlight after 3 seconds
                            var timer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromSeconds(3)
                            };
                            timer.Tick += (s, e) =>
                            {
                                textBlock.Background = originalBackground;
                                textBlock.Effect = null;
                                timer.Stop();
                            };
                            timer.Start();

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error highlighting parameter: {ex.Message}");
            }
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private int ExtractParamNumber(string paramName)
        {
            // Extract number from "Param1", "Param2", etc.
            var match = System.Text.RegularExpressions.Regex.Match(paramName, @"Param(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            {
                return number;
            }
            return 0;
        }

        private double CalculateParamColumnOffset(int paramNumber)
        {
            // Column widths from XAML:
            // Columns 0-6: Various fixed widths (Position, Web3 Operator, ProcessName, etc.)
            // Param1-23: 300px each
            // Param24-46: 120px each

            // Base offset (skip first 7 columns before params)
            double baseOffset = 60 + 150 + 200 + 80 + 150 + 120 + 120; // Approximate total of first 7 columns

            if (paramNumber <= 23)
            {
                // Params 1-23 are 300px wide
                return baseOffset + ((paramNumber - 1) * 300);
            }
            else
            {
                // Params 24-46 are 120px wide
                double offset23 = baseOffset + (23 * 300);
                return offset23 + ((paramNumber - 24) * 120);
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }

                var result = FindVisualChild(child, predicate);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Navigates to the ExtTest table for a specific test and column
        /// Called when navigating from From_ExtTest_ pattern
        /// </summary>
        public void NavigateToExtTest(int testId, string columnName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Navigating to ExtTest{testId}, column: {columnName}");

                // Find the MainWindow
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    // Open the ExtTest table with column navigation
                    mainWindow.OpenExtTableTabWithColumn($"ExtTest{testId}", columnName);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Could not find MainWindow");
                    MessageBox.Show($"Navigate to ExtTest{testId} → {columnName}",
                        "ExtTest Navigation", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to ExtTest: {ex.Message}");
                MessageBox.Show($"Failed to navigate to ExtTest.\n\nError: {ex.Message}",
                    "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ================================================
        // COPY/PASTE SELECTION HANDLERS
        // ================================================

        /// <summary>
        /// Handle mouse click on process selection border
        /// </summary>
        private void ProcessSelectionBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Process process)
            {
                // Toggle selection on click
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // CTRL+Click: toggle individual selection
                    process.IsSelected = !process.IsSelected;
                }
                else
                {
                    // Regular click: clear all and select this one
                    ClearAllSelections();
                    process.IsSelected = true;
                }

                _isSelecting = true;
                _selectionStartProcess = process;
                _selectionStartFunction = null;

                System.Diagnostics.Debug.WriteLine($"Process {process.ProcessID} selection: {process.IsSelected}");
            }
        }

        /// <summary>
        /// Handle mouse move on process selection border (for drag multi-select)
        /// </summary>
        private void ProcessSelectionBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed && _selectionStartProcess != null)
            {
                if (sender is Border border && border.DataContext is Process currentProcess)
                {
                    // Find the test that contains both processes
                    var test = _allTests.FirstOrDefault(t => t.Processes.Contains(_selectionStartProcess) && t.Processes.Contains(currentProcess));
                    if (test != null)
                    {
                        // Get indices
                        int startIndex = test.Processes.IndexOf(_selectionStartProcess);
                        int currentIndex = test.Processes.IndexOf(currentProcess);

                        // Clear current selections in this test
                        foreach (var p in test.Processes)
                        {
                            p.IsSelected = false;
                        }

                        // Select range
                        int min = Math.Min(startIndex, currentIndex);
                        int max = Math.Max(startIndex, currentIndex);
                        for (int i = min; i <= max; i++)
                        {
                            if (!test.Processes[i].IsPlaceholder)
                            {
                                test.Processes[i].IsSelected = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle mouse click on function selection border
        /// </summary>
        private void FunctionSelectionBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Function function)
            {
                // Toggle selection on click
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // CTRL+Click: toggle individual selection
                    function.IsSelected = !function.IsSelected;
                }
                else
                {
                    // Regular click: clear all and select this one
                    ClearAllSelections();
                    function.IsSelected = true;
                }

                _isSelecting = true;
                _selectionStartFunction = function;
                _selectionStartProcess = null;

                System.Diagnostics.Debug.WriteLine($"Function {function.FunctionName} selection: {function.IsSelected}");
            }
        }

        /// <summary>
        /// Handle right-click on process selection border
        /// </summary>
        private void ProcessSelectionBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Process process)
            {
                // If right-clicking on an unselected row, select it first
                if (!process.IsSelected)
                {
                    ClearAllSelections();
                    process.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// Handle right-click on function selection border
        /// </summary>
        private void FunctionSelectionBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Function function)
            {
                // If right-clicking on an unselected row, select it first
                if (!function.IsSelected)
                {
                    ClearAllSelections();
                    function.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// Handle mouse move on function selection border (for drag multi-select)
        /// </summary>
        private void FunctionSelectionBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed && _selectionStartFunction != null)
            {
                if (sender is Border border && border.DataContext is Function currentFunction)
                {
                    // Find the process that contains both functions
                    var process = _allTests
                        .SelectMany(t => t.Processes)
                        .FirstOrDefault(p => p.Functions.Contains(_selectionStartFunction) && p.Functions.Contains(currentFunction));

                    if (process != null)
                    {
                        // Get indices
                        int startIndex = process.Functions.IndexOf(_selectionStartFunction);
                        int currentIndex = process.Functions.IndexOf(currentFunction);

                        // Clear current selections in this process
                        foreach (var f in process.Functions)
                        {
                            f.IsSelected = false;
                        }

                        // Select range
                        int min = Math.Min(startIndex, currentIndex);
                        int max = Math.Max(startIndex, currentIndex);
                        for (int i = min; i <= max; i++)
                        {
                            process.Functions[i].IsSelected = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts (CTRL+C, CTRL+V, Delete)
        /// </summary>
        private async void TestsView_KeyDown(object sender, KeyEventArgs e)
        {
            // CTRL+C: Copy selected processes/functions
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await CopySelectedItems();
                e.Handled = true;
            }
            // CTRL+V: Paste copied processes/functions
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await PasteItems();
                e.Handled = true;
            }
            // Delete: Delete selected items
            else if (e.Key == Key.Delete)
            {
                await DeleteSelectedItems();
                e.Handled = true;
            }
            // ESC: Clear selections
            else if (e.Key == Key.Escape)
            {
                ClearAllSelections();
                _isSelecting = false;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Copy selected processes and functions to clipboard
        /// </summary>
        private async Task CopySelectedItems()
        {
            // Get selected processes
            _copiedProcesses = _allTests
                .SelectMany(t => t.Processes)
                .Where(p => p.IsSelected && !p.IsPlaceholder)
                .ToList();

            // Get selected functions
            _copiedFunctions = _allTests
                .SelectMany(t => t.Processes)
                .SelectMany(p => p.Functions)
                .Where(f => f.IsSelected)
                .ToList();

            if (_copiedProcesses.Any())
            {
                System.Diagnostics.Debug.WriteLine($"✓ Copied {_copiedProcesses.Count} process(es)");
                MessageBox.Show($"Copied {_copiedProcesses.Count} process(es)",
                    "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (_copiedFunctions.Any())
            {
                System.Diagnostics.Debug.WriteLine($"✓ Copied {_copiedFunctions.Count} function(s)");
                MessageBox.Show($"Copied {_copiedFunctions.Count} function(s)",
                    "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Paste copied processes/functions
        /// </summary>
        private async Task PasteItems()
        {
            try
            {
                if (_copiedProcesses.Any())
                {
                    await PasteProcesses();
                }
                else if (_copiedFunctions.Any())
                {
                    await PasteFunctions();
                }
                else
                {
                    MessageBox.Show("Nothing to paste. Please copy some processes or functions first.",
                        "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error pasting: {ex.Message}");
                MessageBox.Show($"Failed to paste.\n\nError: {ex.Message}",
                    "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Paste processes to the currently selected test
        /// </summary>
        private async Task PasteProcesses()
        {
            // Find which test to paste into (use the test of the currently selected process, or first test)
            var targetTest = _allTests
                .FirstOrDefault(t => t.Processes.Any(p => p.IsSelected));

            if (targetTest == null)
            {
                MessageBox.Show("Please select a process row in the test where you want to paste.",
                    "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int pastedCount = 0;
            foreach (var copiedProcess in _copiedProcesses)
            {
                // Create a deep copy of the process
                var newProcess = new Process
                {
                    TestID = targetTest.TestID,
                    ProcessID = copiedProcess.ProcessID,
                    ProcessName = copiedProcess.ProcessName,
                    WEB3Operator = copiedProcess.WEB3Operator,
                    Pass_Fail_WEB3Operator = copiedProcess.Pass_Fail_WEB3Operator,
                    Comments = copiedProcess.Comments,
                    Module = copiedProcess.Module,
                    Repeat = copiedProcess.Repeat,
                    ProcessPosition = targetTest.Processes.Count(p => !p.IsPlaceholder) + 1,
                    ParentTest = targetTest,
                    Functions = new ObservableCollection<Function>(),
                    AreFunctionsLoaded = true
                };

                // Copy all parameters
                for (int i = 1; i <= 46; i++)
                {
                    var paramProp = typeof(Process).GetProperty($"Param{i}");
                    if (paramProp != null)
                    {
                        var paramValue = paramProp.GetValue(copiedProcess);
                        paramProp.SetValue(newProcess, paramValue);
                    }
                }

                // Insert into database
                var insertedProcess = await _processRepository.InsertProcessAsync(newProcess);

                if (insertedProcess != null)
                {
                    // Load functions if the original process had any
                    if (copiedProcess.ProcessID.HasValue)
                    {
                        var functions = await _processRepository.GetFunctionsForProcessAsync(copiedProcess.ProcessID.Value);
                        foreach (var function in functions)
                        {
                            function.ParentProcess = insertedProcess;
                            insertedProcess.Functions.Add(function);
                        }
                    }

                    // Add to UI (insert before placeholder)
                    var placeholderIndex = targetTest.Processes.ToList().FindIndex(p => p.IsPlaceholder);
                    if (placeholderIndex >= 0)
                    {
                        targetTest.Processes.Insert(placeholderIndex, insertedProcess);
                    }
                    else
                    {
                        targetTest.Processes.Add(insertedProcess);
                    }

                    pastedCount++;
                    System.Diagnostics.Debug.WriteLine($"✓ Pasted process {insertedProcess.ProcessID}");
                }
            }

            MessageBox.Show($"Successfully pasted {pastedCount} process(es) to Test #{targetTest.TestID}",
                "Paste", MessageBoxButton.OK, MessageBoxImage.Information);

            // Clear selections
            ClearAllSelections();
        }

        /// <summary>
        /// Paste functions to the currently selected process
        /// </summary>
        private async Task PasteFunctions()
        {
            // Find which process to paste into
            var targetProcess = _allTests
                .SelectMany(t => t.Processes)
                .FirstOrDefault(p => p.IsSelected && !p.IsPlaceholder);

            if (targetProcess == null || !targetProcess.ProcessID.HasValue)
            {
                MessageBox.Show("Please select a process row where you want to paste the functions.",
                    "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int pastedCount = 0;
            foreach (var copiedFunction in _copiedFunctions)
            {
                // Create a deep copy of the function
                var newFunction = new Function
                {
                    ProcessID = targetProcess.ProcessID.Value,
                    FunctionName = copiedFunction.FunctionName,
                    FunctionDescription = copiedFunction.FunctionDescription,
                    WEB3Operator = copiedFunction.WEB3Operator,
                    Pass_Fail_WEB3Operator = copiedFunction.Pass_Fail_WEB3Operator,
                    ActualValue = copiedFunction.ActualValue,
                    BreakPoint = copiedFunction.BreakPoint,
                    Comments = copiedFunction.Comments,
                    FunctionPosition = targetProcess.Functions.Count + 1,
                    ParentProcess = targetProcess
                };

                // Copy all parameters
                for (int i = 1; i <= 30; i++)
                {
                    var paramProp = typeof(Function).GetProperty($"Param{i}");
                    if (paramProp != null)
                    {
                        var paramValue = paramProp.GetValue(copiedFunction);
                        paramProp.SetValue(newFunction, paramValue);
                    }
                }

                // Insert into database
                var insertedFunction = await _processRepository.InsertFunctionAsync(newFunction);

                if (insertedFunction != null)
                {
                    targetProcess.Functions.Add(insertedFunction);
                    pastedCount++;
                    System.Diagnostics.Debug.WriteLine($"✓ Pasted function {insertedFunction.FunctionName}");
                }
            }

            MessageBox.Show($"Successfully pasted {pastedCount} function(s) to Process #{targetProcess.ProcessID}",
                "Paste", MessageBoxButton.OK, MessageBoxImage.Information);

            // Clear selections
            ClearAllSelections();
        }

        // ================================================
        // CONTEXT MENU HANDLERS
        // ================================================

        /// <summary>
        /// Handle Copy from context menu
        /// </summary>
        private async void ContextMenu_Copy(object sender, RoutedEventArgs e)
        {
            await CopySelectedItems();
        }

        /// <summary>
        /// Handle Paste from context menu
        /// </summary>
        private async void ContextMenu_Paste(object sender, RoutedEventArgs e)
        {
            await PasteItems();
        }

        /// <summary>
        /// Handle Delete from context menu
        /// </summary>
        private async void ContextMenu_Delete(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedItems();
        }

        /// <summary>
        /// Delete selected processes and functions
        /// </summary>
        private async Task DeleteSelectedItems()
        {
            try
            {
                // Get selected processes and functions
                var selectedProcesses = _allTests
                    .SelectMany(t => t.Processes)
                    .Where(p => p.IsSelected && !p.IsPlaceholder)
                    .ToList();

                var selectedFunctions = _allTests
                    .SelectMany(t => t.Processes)
                    .SelectMany(p => p.Functions)
                    .Where(f => f.IsSelected)
                    .ToList();

                if (!selectedProcesses.Any() && !selectedFunctions.Any())
                {
                    MessageBox.Show("No items selected to delete.",
                        "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Confirm deletion
                string message;
                if (selectedProcesses.Any() && selectedFunctions.Any())
                {
                    message = $"Are you sure you want to delete {selectedProcesses.Count} process(es) and {selectedFunctions.Count} function(s)?";
                }
                else if (selectedProcesses.Any())
                {
                    message = $"Are you sure you want to delete {selectedProcesses.Count} process(es)?";
                }
                else
                {
                    message = $"Are you sure you want to delete {selectedFunctions.Count} function(s)?";
                }

                var result = MessageBox.Show(message, "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                int deletedCount = 0;

                // Delete processes
                foreach (var process in selectedProcesses)
                {
                    await _processRepository.DeleteProcessAsync(process.Index.Value);

                    // Remove from UI
                    var test = _allTests.FirstOrDefault(t => t.Processes.Contains(process));
                    if (test != null)
                    {
                        test.Processes.Remove(process);
                        deletedCount++;
                        System.Diagnostics.Debug.WriteLine($"✓ Deleted process Index #{process.Index}");
                    }
                }

                // Delete functions
                foreach (var function in selectedFunctions)
                {
                    await _processRepository.DeleteFunctionAsync(function.Index.Value);

                    // Remove from UI
                    var process = _allTests
                        .SelectMany(t => t.Processes)
                        .FirstOrDefault(p => p.Functions.Contains(function));

                    if (process != null)
                    {
                        process.Functions.Remove(function);
                        deletedCount++;
                        System.Diagnostics.Debug.WriteLine($"✓ Deleted function Index #{function.Index}");
                    }
                }

                MessageBox.Show($"Successfully deleted {deletedCount} item(s).",
                    "Delete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear selections
                ClearAllSelections();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting: {ex.Message}");
                MessageBox.Show($"Failed to delete items.\n\nError: {ex.Message}",
                    "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Clear all selections
        /// </summary>
        private void ClearAllSelections()
        {
            foreach (var test in _allTests)
            {
                foreach (var process in test.Processes)
                {
                    process.IsSelected = false;
                    foreach (var function in process.Functions)
                    {
                        function.IsSelected = false;
                    }
                }
            }
        }


    }
}
