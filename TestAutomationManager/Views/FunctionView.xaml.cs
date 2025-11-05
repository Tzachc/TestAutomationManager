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

namespace TestAutomationManager.Views
{
    public partial class FunctionView : UserControl
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
        public ObservableCollection<Function> Functions { get; set; }

        /// <summary>
        /// Keep reference to all functions for filtering
        /// </summary>
        private ObservableCollection<Function> _allFunctions;

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

        public FunctionView()
        {
            InitializeComponent();

            // Initialize repository
            _repository = new ProcessRepository();

            // Initialize collections
            Functions = new ObservableCollection<Function>();
            _allFunctions = new ObservableCollection<Function>();

            // Set data context
            FunctionsItemsControl.ItemsSource = Functions;

            // Load initial data from database
            LoadFunctionsFromDatabase();
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
            if (FunctionsItemsControl != null)
            {
                _listBoxScrollViewer = FindVisualChild<ScrollViewer>(FunctionsItemsControl);

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

                FunctionsItemsControl.CaptureMouse();
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
                FunctionsItemsControl.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void MainScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                FunctionsItemsControl.ReleaseMouseCapture();
            }
        }

        // ================================================
        // DATA LOADING
        // ================================================

        /// <summary>
        /// Load functions from SQL database with loading screen
        /// OPTIMIZED FOR LARGE DATASETS (potentially more records than processes)
        /// Uses bulk loading to avoid UI freeze
        /// </summary>
        private async void LoadFunctionsFromDatabase()
        {
            try
            {
                // Show loading overlay
                ShowLoadingScreen("Loading functions...", 0);

                // ⭐ CRITICAL: Let UI render the loading screen before blocking
                await System.Threading.Tasks.Task.Delay(50);

                System.Diagnostics.Debug.WriteLine("📊 Loading functions...");

                // Get all functions from database (async)
                UpdateLoadingProgress("Fetching functions from database...", 25);
                var functionsFromDb = await _repository.GetAllFunctionsAsync();

                int totalFunctions = functionsFromDb.Count;
                UpdateLoadingProgress($"Preparing {totalFunctions} functions...", 75);

                // ⭐ CRITICAL FIX: Build collections OFF the UI thread, then update UI once
                // This prevents thousands of individual UI updates that freeze the app
                System.Diagnostics.Debug.WriteLine($"⚡ BULK LOAD: Preparing {totalFunctions} functions for single UI update");

                UpdateLoadingProgress($"Displaying {totalFunctions} functions...", 90);

                // ⭐ SORT: Sort functions by ProcessID and FunctionPosition
                var sortedFunctions = functionsFromDb
                    .OrderBy(f => f.ProcessID)
                    .ThenBy(f => f.FunctionPosition)
                    .ToList();

                // ⭐ SINGLE UI UPDATE: Replace entire collection in one operation
                // This triggers only ONE UI update instead of thousands of individual updates!
                System.Diagnostics.Debug.WriteLine($"📊 Replacing collections with {totalFunctions} functions in single operation...");

                // Update UI on UI thread - single operation
                await Dispatcher.InvokeAsync(() =>
                {
                    // Create NEW ObservableCollections from the sorted list (single operation)
                    Functions = new ObservableCollection<Function>(sortedFunctions);
                    _allFunctions = new ObservableCollection<Function>(sortedFunctions);

                    // Update ItemsControl to use new collection
                    FunctionsItemsControl.ItemsSource = Functions;
                });

                UpdateLoadingProgress($"Loaded {Functions.Count} functions!", 100);
                System.Diagnostics.Debug.WriteLine($"✓ Bulk load complete: {Functions.Count} functions loaded instantly");

                // Update record count footer
                UpdateRecordCount();

                // Fire data loaded event
                DataLoaded?.Invoke(this, EventArgs.Empty);

                // Hide loading screen
                await System.Threading.Tasks.Task.Delay(100);
                HideLoadingScreen();

                // Show message if no data
                if (Functions.Count == 0)
                {
                    MessageBox.Show("No functions found in database.\n\nMake sure the Function_WEB3 table has data.",
                        "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                HideLoadingScreen();
                System.Diagnostics.Debug.WriteLine($"✗ Error loading functions: {ex.Message}");
                MessageBox.Show($"Failed to load functions from database.\n\nError: {ex.Message}\n\nCheck:\n1. Database connection\n2. Function_WEB3 table exists\n3. usp_GetAllFunctions stored procedure exists\n4. DbConnectionConfig settings",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // SEARCH & FILTER
        // ================================================

        /// <summary>
        /// Filter functions based on search query
        /// </summary>
        public void FilterFunctions(string searchQuery)
        {
            // Save current search query for re-filtering after updates
            _currentSearchQuery = searchQuery ?? "";

            Functions.Clear();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                // Show all functions when search is empty
                foreach (var function in _allFunctions)
                {
                    Functions.Add(function);
                }
            }
            else
            {
                // Filter by function name, process ID, operator, etc.
                var filtered = _allFunctions.Where(f =>
                    (f.FunctionName?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (f.FunctionDescription?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (f.ProcessID?.ToString().Contains(searchQuery) ?? false) ||
                    (f.WEB3Operator?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (f.Comments?.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
                );

                foreach (var function in filtered)
                {
                    Functions.Add(function);
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
            LoadFunctionsFromDatabase();
        }

        public int GetFunctionCount() => _allFunctions.Count;

        // ================================================
        // CLEANUP
        // ================================================

        private void FunctionView_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize the ListBox's internal ScrollViewer
            GetListBoxScrollViewer();

            // Ensure header/body sync is correct at load
            SyncHeaderToBody();

            System.Diagnostics.Debug.WriteLine("✓ FunctionView Loaded with virtualization enabled");
        }

        private void FunctionView_Unloaded(object sender, RoutedEventArgs e)
        {
            // Safety: release capture if leaving while panning
            if (_isPanning && FunctionsItemsControl != null)
            {
                _isPanning = false;
                FunctionsItemsControl.ReleaseMouseCapture();
            }

            System.Diagnostics.Debug.WriteLine("✓ FunctionView Unloaded");
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
                int count = Functions?.Count ?? 0;
                RecordCountText.Text = count == 1 ? "1 function" : $"{count:N0} functions";
            });
        }
    }
}
