using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Controls;
using TestAutomationManager.Helpers;
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
        /// Test repository for edit service
        /// </summary>
        private readonly ITestRepository _testRepository;

        /// <summary>
        /// Edit service for inline editing
        /// </summary>
        private readonly TestEditService _editService;

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

        // ----- Copy/Paste selection state -----
        private bool _isSelecting = false;
        private Process? _selectionStartProcess = null;
        private List<Process> _copiedProcesses = new();

        // ----- Internal ScrollViewer from ListBox -----
        private ScrollViewer _listBoxScrollViewer;

        // ----- Track which ProcessIDs are currently loading to prevent duplicates -----
        private readonly System.Collections.Concurrent.ConcurrentDictionary<double, bool> _loadingProcessIds = new();

        // ----- Filter management -----
        private FilterManager<Process> _filterManager;
        private Button _currentFilterButton;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public ProcessView()
        {
            InitializeComponent();

            // Initialize repositories
            _repository = new ProcessRepository();
            _testRepository = new TestRepository();

            // Initialize edit service
            _editService = new TestEditService(_testRepository, _repository);

            // Register global inline edit handler
            InlineEditHelper.SetEditConfirmedHandler(this, OnInlineEditConfirmed);

            // Initialize collections
            Processes = new ObservableCollection<Process>();
            _allProcesses = new ObservableCollection<Process>();

            // Initialize filter manager
            _filterManager = new FilterManager<Process>(_allProcesses, Processes);

            // Set data context
            ProcessesItemsControl.ItemsSource = Processes;

            // Register keyboard shortcuts for copy/paste
            this.KeyDown += ProcessView_KeyDown;
            this.Focusable = true;

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

                // ⭐ SORT: Sort processes by ProcessID (low to high)
                var sortedProcesses = processesFromDb.OrderBy(p => p.ProcessID).ToList();

                // ⭐ CRITICAL FIX: Clear and reuse existing collections instead of creating new ones
                // This ensures FilterManager maintains valid references to the collections
                System.Diagnostics.Debug.WriteLine($"📊 Updating collections with {totalProcesses} processes...");

                // Update UI on UI thread - clear and add to existing collections
                await Dispatcher.InvokeAsync(() =>
                {
                    // Clear existing collections
                    Processes.Clear();
                    _allProcesses.Clear();

                    // Add sorted processes to existing collections
                    foreach (var process in sortedProcesses)
                    {
                        Processes.Add(process);
                        _allProcesses.Add(process);
                    }

                    // Add placeholder "(New)" process at the end
                    var placeholderProcess = new Process
                    {
                        IsPlaceholder = true,
                        ProcessPosition = null,
                        Functions = new ObservableCollection<Function>(),
                        AreFunctionsLoaded = true
                    };
                    placeholderProcess.PropertyChanged += PlaceholderProcess_PropertyChanged;
                    Processes.Add(placeholderProcess);
                    _allProcesses.Add(placeholderProcess);
                });

                UpdateLoadingProgress($"Loaded {Processes.Count} processes!", 100);
                System.Diagnostics.Debug.WriteLine($"✓ Bulk load complete: {Processes.Count} processes loaded instantly");

                // Update record count footer
                UpdateRecordCount();

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Hide loading screen
                await System.Threading.Tasks.Task.Delay(100);
                HideLoadingScreen();

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
        /// PREVENTS DUPLICATE LOADS for same ProcessID (handles duplicates + virtualization)
        /// </summary>
        private async System.Threading.Tasks.Task LoadFunctionsForProcessAsync(Process process)
        {
            if (!process.ProcessID.HasValue)
                return;

            var processId = process.ProcessID.Value;

            // ⭐ CRITICAL: Prevent duplicate loads for same ProcessID
            // With virtualization recycling + duplicate ProcessIDs, this can happen!
            if (_loadingProcessIds.ContainsKey(processId))
            {
                // Already loading this ProcessID, skip
                return;
            }

            try
            {
                // Mark as loading
                _loadingProcessIds[processId] = true;

                var cache = ProcessCacheService.Instance;
                List<Function> functions;

                // ⭐ Check cache first
                if (cache.AreFunctionsLoaded(processId))
                {
                    functions = cache.GetFunctions(processId);
                    // Removed excessive logging for cache hits
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading functions for Process #{processId} from database...");
                    functions = await _repository.GetFunctionsForProcessAsync(processId);

                    // Add to cache
                    cache.AddFunctions(processId, functions);
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
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error lazy loading functions: {ex.Message}");
                MessageBox.Show($"Failed to load functions for process.\n\nError: {ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                // Remove from loading tracker
                _loadingProcessIds.TryRemove(processId, out _);
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

            // Update record count after filtering
            UpdateRecordCount();
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

        /// <summary>
        /// Update the record count footer
        /// </summary>
        private void UpdateRecordCount()
        {
            Dispatcher.Invoke(() =>
            {
                int count = Processes?.Count ?? 0;
                RecordCountText.Text = count == 1 ? "1 process" : $"{count:N0} processes";
            });
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

            // Update record count
            UpdateRecordCount();

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

            // Update record count
            UpdateRecordCount();

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
            // Handle Process editing
            if (e.DataContext is Process process)
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
        // PLACEHOLDER PROCESS HANDLERS
        // ================================================

        /// <summary>
        /// Handle property changes on placeholder process
        /// </summary>
        private async void PlaceholderProcess_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is Process placeholder && placeholder.IsPlaceholder)
            {
                // When ProcessID is entered on placeholder, create new process
                if (e.PropertyName == nameof(Process.ProcessID) && placeholder.ProcessID.HasValue)
                {
                    await HandlePlaceholderProcessIdEntered(placeholder);
                }
                // When TestID is entered, just update the placeholder
                else if (e.PropertyName == nameof(Process.TestID))
                {
                    System.Diagnostics.Debug.WriteLine($"Placeholder TestID set to: {placeholder.TestID}");
                }
            }
        }

        /// <summary>
        /// Handle when user enters ProcessID on placeholder row
        /// </summary>
        private async Task HandlePlaceholderProcessIdEntered(Process placeholder)
        {
            try
            {
                var processId = placeholder.ProcessID.Value;
                System.Diagnostics.Debug.WriteLine($"User entered ProcessID {processId} on placeholder");

                // Check if user also entered TestID
                if (!placeholder.TestID.HasValue || placeholder.TestID.Value == 0)
                {
                    MessageBox.Show("Please enter a TestID first before entering ProcessID.",
                        "TestID Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    placeholder.ProcessID = null;
                    return;
                }

                var testId = placeholder.TestID.Value;

                // Check if ProcessID exists in database
                bool processIdExists = await _repository.ProcessIdExistsAsync(processId);

                if (processIdExists)
                {
                    await HandleExistingProcessId(placeholder, testId, processId);
                }
                else
                {
                    await HandleNewProcessId(placeholder, testId, processId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling placeholder: {ex.Message}");
                MessageBox.Show($"Failed to create process.\n\nError: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle existing ProcessID - load template and create new record
        /// </summary>
        private async Task HandleExistingProcessId(Process placeholder, int testId, double processId)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessID {processId} exists - loading template");

            // Load template and functions
            var templateProcess = await _repository.GetProcessTemplateByIdAsync(processId);
            var functions = await _repository.GetFunctionsForProcessAsync(processId);

            // Create new process with template data
            var newProcess = new Process
            {
                TestID = testId,
                ProcessID = processId,
                ProcessPosition = null,
                ProcessName = null,
                WEB3Operator = templateProcess?.WEB3Operator,
                Pass_Fail_WEB3Operator = templateProcess?.Pass_Fail_WEB3Operator,
                Comments = templateProcess?.Comments,
                Module = templateProcess?.Module,
                Repeat = templateProcess?.Repeat,
                IsPlaceholder = false,
                Functions = new ObservableCollection<Function>(),
                AreFunctionsLoaded = true
            };

            // Add functions
            foreach (var function in functions)
            {
                function.ParentProcess = newProcess;
                newProcess.Functions.Add(function);
            }

            // Insert into database
            var insertedProcess = await _repository.InsertProcessAsync(newProcess);

            if (insertedProcess != null)
            {
                // Add to UI
                var placeholderIndex = _allProcesses.IndexOf(placeholder);
                if (placeholderIndex >= 0)
                {
                    placeholder.PropertyChanged -= PlaceholderProcess_PropertyChanged;
                    _allProcesses.RemoveAt(placeholderIndex);
                    _allProcesses.Insert(placeholderIndex, insertedProcess);
                    Processes.RemoveAt(placeholderIndex);
                    Processes.Insert(placeholderIndex, insertedProcess);
                    insertedProcess.PropertyChanged += Process_PropertyChanged;
                }

                System.Diagnostics.Debug.WriteLine($"✓ Created new process from template ProcessID {processId}");
            }
        }

        /// <summary>
        /// Handle new ProcessID - create empty process
        /// </summary>
        private async Task HandleNewProcessId(Process placeholder, int testId, double processId)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessID {processId} is new - creating empty process");

            var newProcess = new Process
            {
                TestID = testId,
                ProcessID = processId,
                ProcessPosition = null,
                IsPlaceholder = false,
                Functions = new ObservableCollection<Function>(),
                AreFunctionsLoaded = true
            };

            // Insert into database
            var insertedProcess = await _repository.InsertProcessAsync(newProcess);

            if (insertedProcess != null)
            {
                // Add to UI
                var placeholderIndex = _allProcesses.IndexOf(placeholder);
                if (placeholderIndex >= 0)
                {
                    placeholder.PropertyChanged -= PlaceholderProcess_PropertyChanged;
                    _allProcesses.RemoveAt(placeholderIndex);
                    _allProcesses.Insert(placeholderIndex, insertedProcess);
                    Processes.RemoveAt(placeholderIndex);
                    Processes.Insert(placeholderIndex, insertedProcess);
                    insertedProcess.PropertyChanged += Process_PropertyChanged;
                }

                System.Diagnostics.Debug.WriteLine($"✓ Created new empty process with ProcessID {processId}");
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
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    process.IsSelected = !process.IsSelected;
                }
                else
                {
                    ClearAllSelections();
                    process.IsSelected = true;
                }

                _isSelecting = true;
                _selectionStartProcess = process;
            }
        }

        /// <summary>
        /// Handle mouse move on process selection border
        /// </summary>
        private void ProcessSelectionBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed && _selectionStartProcess != null)
            {
                if (sender is Border border && border.DataContext is Process currentProcess)
                {
                    int startIndex = _allProcesses.IndexOf(_selectionStartProcess);
                    int currentIndex = _allProcesses.IndexOf(currentProcess);

                    foreach (var p in _allProcesses)
                    {
                        p.IsSelected = false;
                    }

                    int min = Math.Min(startIndex, currentIndex);
                    int max = Math.Max(startIndex, currentIndex);
                    for (int i = min; i <= max; i++)
                    {
                        if (!_allProcesses[i].IsPlaceholder)
                        {
                            _allProcesses[i].IsSelected = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle right-click on process selection border
        /// </summary>
        private void ProcessSelectionBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is Process process)
            {
                if (!process.IsSelected)
                {
                    ClearAllSelections();
                    process.IsSelected = true;
                }
            }
        }

        /// <summary>
        /// Handle keyboard shortcuts
        /// </summary>
        private async void ProcessView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await CopySelectedItems();
                e.Handled = true;
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                await PasteItems();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                await DeleteSelectedItems();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ClearAllSelections();
                _isSelecting = false;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Copy selected processes
        /// </summary>
        private async Task CopySelectedItems()
        {
            _copiedProcesses = _allProcesses.Where(p => p.IsSelected && !p.IsPlaceholder).ToList();

            if (_copiedProcesses.Any())
            {
                System.Diagnostics.Debug.WriteLine($"✓ Copied {_copiedProcesses.Count} process(es)");
                MessageBox.Show($"Copied {_copiedProcesses.Count} process(es)",
                    "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Paste copied processes
        /// </summary>
        private async Task PasteItems()
        {
            try
            {
                if (!_copiedProcesses.Any())
                {
                    MessageBox.Show("Nothing to paste. Please copy some processes first.",
                        "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Find target test (from first selected process, or ask user)
                var targetProcess = _allProcesses.FirstOrDefault(p => p.IsSelected);
                if (targetProcess == null || !targetProcess.TestID.HasValue)
                {
                    MessageBox.Show("Please select a process row to determine which test to paste into.",
                        "Paste", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var targetTestId = targetProcess.TestID.Value;
                int pastedCount = 0;

                foreach (var copiedProcess in _copiedProcesses)
                {
                    var newProcess = new Process
                    {
                        TestID = targetTestId,
                        ProcessID = copiedProcess.ProcessID,
                        ProcessName = copiedProcess.ProcessName,
                        WEB3Operator = copiedProcess.WEB3Operator,
                        Pass_Fail_WEB3Operator = copiedProcess.Pass_Fail_WEB3Operator,
                        Comments = copiedProcess.Comments,
                        Module = copiedProcess.Module,
                        Repeat = copiedProcess.Repeat,
                        ProcessPosition = copiedProcess.ProcessPosition,
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

                    var insertedProcess = await _repository.InsertProcessAsync(newProcess);

                    if (insertedProcess != null)
                    {
                        if (copiedProcess.ProcessID.HasValue)
                        {
                            var functions = await _repository.GetFunctionsForProcessAsync(copiedProcess.ProcessID.Value);
                            foreach (var function in functions)
                            {
                                function.ParentProcess = insertedProcess;
                                insertedProcess.Functions.Add(function);
                            }
                        }

                        var placeholderIndex = _allProcesses.ToList().FindIndex(p => p.IsPlaceholder);
                        if (placeholderIndex >= 0)
                        {
                            _allProcesses.Insert(placeholderIndex, insertedProcess);
                            Processes.Insert(placeholderIndex, insertedProcess);
                        }
                        else
                        {
                            _allProcesses.Add(insertedProcess);
                            Processes.Add(insertedProcess);
                        }

                        pastedCount++;
                    }
                }

                MessageBox.Show($"Successfully pasted {pastedCount} process(es) to Test #{targetTestId}",
                    "Paste", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearAllSelections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to paste.\n\nError: {ex.Message}",
                    "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Delete selected processes
        /// </summary>
        private async Task DeleteSelectedItems()
        {
            try
            {
                var selectedProcesses = _allProcesses.Where(p => p.IsSelected && !p.IsPlaceholder).ToList();

                if (!selectedProcesses.Any())
                {
                    MessageBox.Show("No items selected to delete.",
                        "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"Are you sure you want to delete {selectedProcesses.Count} process(es)?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                int deletedCount = 0;

                foreach (var process in selectedProcesses)
                {
                    await _repository.DeleteProcessAsync(process.Index.Value);

                    _allProcesses.Remove(process);
                    Processes.Remove(process);
                    deletedCount++;
                }

                MessageBox.Show($"Successfully deleted {deletedCount} process(es).",
                    "Delete", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearAllSelections();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete.\n\nError: {ex.Message}",
                    "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Context menu handlers
        /// </summary>
        private async void ContextMenu_Copy(object sender, RoutedEventArgs e) => await CopySelectedItems();
        private async void ContextMenu_Paste(object sender, RoutedEventArgs e) => await PasteItems();
        private async void ContextMenu_Delete(object sender, RoutedEventArgs e) => await DeleteSelectedItems();

        /// <summary>
        /// Clear all selections
        /// </summary>
        private void ClearAllSelections()
        {
            foreach (var process in _allProcesses)
            {
                process.IsSelected = false;
            }
        }
    }
}
