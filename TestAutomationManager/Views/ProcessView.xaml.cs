using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;
using TestAutomationManager.Services;

namespace TestAutomationManager.Views
{
    public partial class ProcessView : UserControl
    {
        // ================================================
        // FIELDS
        // ================================================

        /// <summary>
        /// Repository for database operations
        /// </summary>
        private readonly ProcessRepository _repository;

        /// <summary>
        /// Observable collection for UI binding
        /// </summary>
        public ObservableCollection<Process> Processes { get; set; }

        /// <summary>
        /// Keep reference to all processes for filtering
        /// </summary>
        private ObservableCollection<Process> _allProcesses;

        /// <summary>
        /// Current search query for re-filtering after updates
        /// </summary>
        private string _currentSearchQuery = "";

        /// <summary>
        /// Event fired when data is loaded
        /// </summary>
        public event EventHandler DataLoaded;

        // ----- Middle-mouse panning state -----
        private bool _isPanning = false;
        private Point _lastPanPoint;
        private double _startH;
        private double _startV;

        // ----- Internal ScrollViewer from ListBox -----
        private ScrollViewer _listBoxScrollViewer;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public ProcessView()
        {
            InitializeComponent();

            // Initialize repository
            _repository = new ProcessRepository();

            // Initialize collections
            Processes = new ObservableCollection<Process>();
            _allProcesses = new ObservableCollection<Process>();

            // Set data context
            ProcessesItemsControl.ItemsSource = Processes;

            // Load initial data from database
            LoadProcessesFromDatabase();
        }

        // ================================================
        // SCROLL SYNCHRONIZATION
        // ================================================

        /// <summary>
        /// Get the internal ScrollViewer from the ListBox
        /// </summary>
        private ScrollViewer GetListBoxScrollViewer()
        {
            if (_listBoxScrollViewer != null)
                return _listBoxScrollViewer;

            // Find the ScrollViewer inside the ListBox
            if (ProcessesItemsControl != null)
            {
                _listBoxScrollViewer = FindVisualChild<ScrollViewer>(ProcessesItemsControl);

                // Hook up scroll changed event
                if (_listBoxScrollViewer != null)
                {
                    _listBoxScrollViewer.ScrollChanged += ListBoxScrollViewer_ScrollChanged;
                }
            }

            return _listBoxScrollViewer;
        }

        /// <summary>
        /// Find a visual child of a specific type
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Synchronize sticky header with body horizontal offset
        /// </summary>
        private void ListBoxScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Keep header aligned horizontally with body
            SyncHeaderToBody();
        }

        /// <summary>
        /// Apply the body horizontal offset to the header scrollviewer
        /// </summary>
        private void SyncHeaderToBody()
        {
            var scrollViewer = GetListBoxScrollViewer();
            if (HeaderScrollViewer == null || scrollViewer == null) return;
            HeaderScrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset);
        }

        // ================================================
        // MIDDLE-MOUSE DRAG (PANNING)
        // ================================================

        private void MainScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                var scrollViewer = GetListBoxScrollViewer();
                if (scrollViewer == null) return;

                _isPanning = true;
                _lastPanPoint = e.GetPosition(scrollViewer);
                _startH = scrollViewer.HorizontalOffset;
                _startV = scrollViewer.VerticalOffset;

                ProcessesItemsControl.CaptureMouse();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;

            var scrollViewer = GetListBoxScrollViewer();
            if (scrollViewer == null) return;

            var current = e.GetPosition(scrollViewer);
            var dx = current.X - _lastPanPoint.X;
            var dy = current.Y - _lastPanPoint.Y;

            // Natural panning
            var targetH = _startH + dx;
            var targetV = _startV + dy;

            // Clamp to bounds
            targetH = Math.Max(0, Math.Min(targetH, scrollViewer.ScrollableWidth));
            targetV = Math.Max(0, Math.Min(targetV, scrollViewer.ScrollableHeight));

            scrollViewer.ScrollToHorizontalOffset(targetH);
            scrollViewer.ScrollToVerticalOffset(targetV);

            e.Handled = true;
        }

        private void MainScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _isPanning)
            {
                _isPanning = false;
                ProcessesItemsControl.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ProcessesItemsControl.ReleaseMouseCapture();
            }
        }

        // ================================================
        // DATA LOADING
        // ================================================

        /// <summary>
        /// Load processes from SQL database with loading screen
        /// OPTIMIZED FOR LARGE DATASETS (20000+ records)
        /// Uses bulk loading to avoid UI freeze
        /// </summary>
        private async void LoadProcessesFromDatabase()
        {
            try
            {
                // Show loading overlay
                ShowLoadingScreen("Loading processes...", 0);

                // ⭐ CRITICAL: Let UI render the loading screen before blocking
                await System.Threading.Tasks.Task.Delay(50);

                System.Diagnostics.Debug.WriteLine("📊 Loading processes...");

                List<Process> processesFromDb;
                var cache = ProcessCacheService.Instance;
                bool usedCache = false;

                // ⭐ SMART CACHING: Check if we already have processes loaded from TestsView
                if (cache.HasSignificantCachedData())
                {
                    System.Diagnostics.Debug.WriteLine($"🚀 CACHE HIT! Using {cache.GetCachedProcessCount()} cached processes from TestsView");
                    processesFromDb = cache.GetAllCachedProcesses();
                    UpdateLoadingProgress($"Loading {processesFromDb.Count} cached processes (instant!)...", 50);
                    usedCache = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("📊 Cache miss - loading from database...");
                    // Get all processes from database (async)
                    processesFromDb = await _repository.GetAllProcessesAsync();

                    // Add to cache for future use
                    cache.AddProcesses(processesFromDb);
                    System.Diagnostics.Debug.WriteLine($"✓ Added {processesFromDb.Count} processes to cache");
                }

                int totalProcesses = processesFromDb.Count;
                UpdateLoadingProgress($"Preparing {totalProcesses} processes...", 75);

                // ⭐ CRITICAL FIX: Build collections OFF the UI thread, then update UI once
                // This prevents 21k+ individual UI updates that freeze the app
                System.Diagnostics.Debug.WriteLine($"⚡ BULK LOAD: Preparing {totalProcesses} processes for single UI update");

                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Attach PropertyChanged handlers off UI thread
                    foreach (var process in processesFromDb)
                    {
                        process.PropertyChanged += Process_PropertyChanged;
                    }
                });

                UpdateLoadingProgress($"Displaying {totalProcesses} processes...", 90);

                // ⭐ SINGLE UI UPDATE: Replace entire collection in one operation
                // This triggers only ONE UI update instead of 21k+ individual updates!
                System.Diagnostics.Debug.WriteLine($"📊 Replacing collections with {totalProcesses} processes in single operation...");

                // Update UI on UI thread - single operation
                await Dispatcher.InvokeAsync(() =>
                {
                    // Create NEW ObservableCollections from the list (single operation)
                    Processes = new ObservableCollection<Process>(processesFromDb);
                    _allProcesses = new ObservableCollection<Process>(processesFromDb);

                    // Update ItemsControl to use new collection
                    ProcessesItemsControl.ItemsSource = Processes;
                });

                UpdateLoadingProgress($"Loaded {Processes.Count} processes!", 100);
                System.Diagnostics.Debug.WriteLine($"✓ Bulk load complete: {Processes.Count} processes loaded instantly");

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Hide loading screen
                await System.Threading.Tasks.Task.Delay(100);
                HideLoadingScreen();

                // ⭐ START BACKGROUND PRE-LOADING after UI is responsive
                // But SKIP if we used cache - functions are likely already cached too!
                if (!usedCache)
                {
                    System.Diagnostics.Debug.WriteLine("🔄 Starting background function preload (database load)");
                    StartBackgroundPreloading();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚡ Skipping background preload (cache already has data)");
                    // Log cache statistics to see what we have
                    ProcessCacheService.Instance.LogStatistics();
                }

                // Show message if no data
                if (Processes.Count == 0)
                {
                    MessageBox.Show("No processes found in database.\n\nMake sure the Process_WEB3 table has data.",
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                HideLoadingScreen();
                System.Diagnostics.Debug.WriteLine($"✗ Error loading processes: {ex.Message}");
                MessageBox.Show($"Failed to load processes from database.\n\nError: {ex.Message}\n\nCheck:\n1. Database connection\n2. Process_WEB3 table exists\n3. DbConnectionConfig settings",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // LAZY LOADING EVENT HANDLERS
        // ================================================

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
        /// Lazy load functions for a specific process
        /// </summary>
        private async System.Threading.Tasks.Task LoadFunctionsForProcessAsync(Process process)
        {
            if (!process.ProcessID.HasValue)
                return;

            try
            {
                var cache = ProcessCacheService.Instance;
                List<Function> functions;

                // ⭐ Check cache first
                if (cache.AreFunctionsLoaded(process.ProcessID.Value))
                {
                    functions = cache.GetFunctions(process.ProcessID.Value);
                    System.Diagnostics.Debug.WriteLine($"🚀 CACHE HIT! Using cached functions for Process #{process.ProcessID}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading functions for Process #{process.ProcessID} from database...");
                    functions = await _repository.GetFunctionsForProcessAsync(process.ProcessID.Value);

                    // Add to cache
                    cache.AddFunctions(process.ProcessID.Value, functions);
                }

                // Update UI on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    process.Functions.Clear();
                    foreach (var function in functions)
                    {
                        process.Functions.Add(function);
                    }

                    process.AreFunctionsLoaded = true;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {functions.Count} functions for Process #{process.ProcessID}");
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
        /// Filter processes based on search query
        /// </summary>
        public void FilterProcesses(string searchQuery)
        {
            // Save current search query for re-filtering after updates
            _currentSearchQuery = searchQuery ?? "";

            Processes.Clear();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                // Show all processes when search is empty
                foreach (var process in _allProcesses)
                {
                    Processes.Add(process);
                }
            }
            else
            {
                // Filter by process name, ID, operator, etc.
                var filtered = _allProcesses.Where(p =>
                    (p.ProcessName?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.ProcessID?.ToString().Contains(searchQuery) ?? false) ||
                    (p.TestID?.ToString().Contains(searchQuery) ?? false) ||
                    (p.WEB3Operator?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                );

                foreach (var process in filtered)
                {
                    Processes.Add(process);
                }
            }
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        public void RefreshData()
        {
            LoadProcessesFromDatabase();
        }

        public int GetProcessCount() => _allProcesses.Count;

        // ================================================
        // CLEANUP
        // ================================================

        private void ProcessView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize the ListBox's internal ScrollViewer
            GetListBoxScrollViewer();

            // Ensure header/body sync is correct at load
            SyncHeaderToBody();

            System.Diagnostics.Debug.WriteLine("✓ ProcessView Loaded with virtualization enabled");
        }

        private void ProcessView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Safety: release capture if leaving while panning
            if (_isPanning && ProcessesItemsControl != null)
            {
                _isPanning = false;
                ProcessesItemsControl.ReleaseMouseCapture();
            }

            System.Diagnostics.Debug.WriteLine("✓ ProcessView Unloaded");
        }

        // ================================================
        // ROW ACTIONS
        // ================================================

        private void EditProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Process process)
            {
                MessageBox.Show(
                    $"Edit functionality for Process #{process.ProcessID} '{process.ProcessName}' is coming soon!",
                    "Coming Soon",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private void RunProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Process process)
            {
                MessageBox.Show(
                    $"Run functionality for Process #{process.ProcessID} '{process.ProcessName}' is coming soon!",
                    "Coming Soon",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private async void DeleteProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Process process)
            {
                try
                {
                    int functionCount = process.Functions?.Count ?? 0;

                    string warningMessage = $"Are you sure you want to delete this process?\n\n" +
                                            $"Process: {process.ProcessName} (ID #{process.ProcessID})\n" +
                                            $"Test ID: {process.TestID}\n\n";

                    if (functionCount > 0)
                    {
                        warningMessage += $"⚠️ This will also delete:\n" +
                                          $"  • {functionCount} function{(functionCount != 1 ? "s" : "")}\n\n";
                    }

                    warningMessage += "This action cannot be undone!";

                    var result = MessageBox.Show(
                        warningMessage,
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        button.IsEnabled = false;

                        System.Diagnostics.Debug.WriteLine($"🗑️ Deleting process #{process.ProcessID}...");
                        await _repository.DeleteProcessAsync(process.ProcessID.Value);
                        System.Diagnostics.Debug.WriteLine($"✓ Process #{process.ProcessID} deleted successfully!");

                        MessageBox.Show(
                            $"Process '{process.ProcessName}' has been deleted successfully!",
                            "Process Deleted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        RefreshData();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error deleting process: {ex.Message}");
                    MessageBox.Show(
                        $"Failed to delete process.\n\nError: {ex.Message}",
                        "Delete Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    if (sender is Button btn) btn.IsEnabled = true;
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
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = LoadingProgressBar.Value,
                    To = progress,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };

                LoadingProgressBar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, animation);
                LoadingProgressText.Text = message;
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

        // ================================================
        // BACKGROUND PRE-LOADING
        // ================================================

        private bool _isBackgroundLoadingRunning = false;
        private readonly System.Collections.Concurrent.ConcurrentQueue<Process> _preloadQueue = new();

        /// <summary>
        /// Start background pre-loading of functions after initial UI load
        /// Loads data in the background so subsequent expansions are instant
        /// </summary>
        private async void StartBackgroundPreloading()
        {
            if (_isBackgroundLoadingRunning)
                return;

            _isBackgroundLoadingRunning = true;
            System.Diagnostics.Debug.WriteLine("🚀 Starting background pre-loading for processes...");

            // Add all processes to the queue
            foreach (var process in _allProcesses)
                _preloadQueue.Enqueue(process);

            // Start background loading task
            await System.Threading.Tasks.Task.Run(async () => await BackgroundPreloadWorker());
        }

        /// <summary>
        /// Background worker that pre-loads data with throttling
        /// </summary>
        private async System.Threading.Tasks.Task BackgroundPreloadWorker()
        {
            int processesLoaded = 0;
            int totalProcesses = _preloadQueue.Count;

            while (_preloadQueue.TryDequeue(out Process process))
            {
                try
                {
                    // Only load if not already loaded
                    if (!process.AreFunctionsLoaded && process.ProcessID.HasValue)
                    {
                        // Load functions for this process
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
                        });

                        processesLoaded++;

                        // Log progress every 100 processes
                        if (processesLoaded % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"📦 Background pre-loaded {processesLoaded}/{totalProcesses} processes");
                        }

                        // Throttle to avoid overwhelming database/UI (load 10 processes, pause 100ms)
                        if (processesLoaded % 10 == 0)
                        {
                            await System.Threading.Tasks.Task.Delay(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Background preload error for process #{process.ProcessID}: {ex.Message}");
                    // Continue with next process
                }
            }

            _isBackgroundLoadingRunning = false;
            System.Diagnostics.Debug.WriteLine($"✓ Background pre-loading completed! Loaded {processesLoaded}/{totalProcesses} processes");
        }

        private void FuncRowsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
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
