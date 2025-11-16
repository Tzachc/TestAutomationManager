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
        /// Handle placeholder function property changes to detect when user enters FunctionName
        /// This implements the "New" function adding functionality
        /// </summary>
        private async void PlaceholderFunction_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Function.FunctionName) && sender is Function placeholder)
            {
                // Only handle if this is still a placeholder and FunctionName is not null/empty
                if (!placeholder.IsPlaceholder || string.IsNullOrWhiteSpace(placeholder.FunctionName))
                    return;

                try
                {
                    System.Diagnostics.Debug.WriteLine($"🆕 User entered FunctionName '{placeholder.FunctionName}' in placeholder row");

                    // Unsubscribe from placeholder events to prevent re-triggering
                    placeholder.PropertyChanged -= PlaceholderFunction_PropertyChanged;

                    // Create new function record with the entered FunctionName
                    var newFunction = new Function
                    {
                        ProcessID = placeholder.ProcessID,
                        FunctionName = placeholder.FunctionName,
                        FunctionPosition = placeholder.FunctionPosition,
                        IsPlaceholder = false,
                        ParentProcess = placeholder.ParentProcess
                    };

                    // Insert into database
                    var insertedFunction = await _repository.InsertFunctionAsync(newFunction);

                    // Ensure properties are set correctly (in case repository returns a new object)
                    insertedFunction.IsPlaceholder = false;
                    insertedFunction.ParentProcess = placeholder.ParentProcess;

                    System.Diagnostics.Debug.WriteLine($"✓ Inserted function '{insertedFunction.FunctionName}' with Index #{insertedFunction.Index}, IsPlaceholder={insertedFunction.IsPlaceholder}");

                    // Update UI
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var parentProcess = placeholder.ParentProcess;
                        if (parentProcess != null)
                        {
                            // Find placeholder index
                            int placeholderIndex = parentProcess.Functions.IndexOf(placeholder);

                            if (placeholderIndex >= 0)
                            {
                                // Remove placeholder and insert real function at same position
                                parentProcess.Functions.RemoveAt(placeholderIndex);
                                parentProcess.Functions.Insert(placeholderIndex, insertedFunction);

                                // Add new placeholder at the end
                                var newPlaceholder = new Function
                                {
                                    IsPlaceholder = true,
                                    ParentProcess = parentProcess,
                                    ProcessID = parentProcess.ProcessID,
                                    FunctionPosition = insertedFunction.FunctionPosition + 1
                                };

                                newPlaceholder.PropertyChanged += PlaceholderFunction_PropertyChanged;
                                parentProcess.Functions.Add(newPlaceholder);

                                System.Diagnostics.Debug.WriteLine($"✅ Added new function '{insertedFunction.FunctionName}' to Process #{parentProcess.ProcessID}");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error creating new function: {ex.Message}");
                    MessageBox.Show($"Failed to create function.\n\nError: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Revert the placeholder
                    placeholder.FunctionName = null;
                    placeholder.PropertyChanged += PlaceholderFunction_PropertyChanged;
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

                    // ⭐ Add placeholder "(New)" row for creating new functions
                    var placeholderFunction = new Function
                    {
                        IsPlaceholder = true,
                        ParentProcess = process,
                        ProcessID = process.ProcessID,
                        FunctionPosition = functions.Any() ? functions.Max(f => f.FunctionPosition ?? 0) + 1 : 1
                    };

                    // Subscribe to PropertyChanged to detect when user enters FunctionName
                    placeholderFunction.PropertyChanged += PlaceholderFunction_PropertyChanged;
                    process.Functions.Add(placeholderFunction);

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
    }
}
