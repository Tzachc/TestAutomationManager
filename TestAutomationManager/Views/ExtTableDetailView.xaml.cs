using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Data;
using TestAutomationManager.Dialogs;
using TestAutomationManager.Exceptions;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Views
{
    public partial class ExtTableDetailView : UserControl
    {
        // ================================================
        // FIELDS
        // ================================================

        private readonly ITestRepository _repository;
        private readonly ExtTableDataRepository _dataRepository;
        private readonly ExtTableLayoutRepository _layoutRepository;
        public string TableName { get; private set; }
        private ExternalTableInfo _tableInfo;
        private int _rowCount;
        private DataTable _currentDataTable;

        // ⭐ NEW: Track layout changes
        private Dictionary<string, double> _defaultColumnWidths;
        private Dictionary<string, double> _currentColumnWidths;
        private Dictionary<int, double> _currentRowHeights; // Row index -> height
        private bool _hasUnsavedChanges = false;
        private bool _isLoadingLayout = false; // Prevent change tracking during layout load

        public event EventHandler DataLoaded;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public ExtTableDetailView(string tableName)
        {
            InitializeComponent();

            _repository = new TestRepository();
            _dataRepository = new ExtTableDataRepository();
            _layoutRepository = new ExtTableLayoutRepository();
            TableName = tableName;
            _rowCount = 0;
            _defaultColumnWidths = new Dictionary<string, double>();
            _currentColumnWidths = new Dictionary<string, double>();
            _currentRowHeights = new Dictionary<int, double>();

            LoadTableData();
        }

        // ================================================
        // DATA LOADING
        // ================================================

        private async void LoadTableData()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
                ErrorState.Visibility = Visibility.Collapsed;
                TableDataGrid.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine($"📊 Loading data for {TableName}...");

                var allTables = await _repository.GetAllExternalTablesAsync();
                _tableInfo = allTables.FirstOrDefault(t => t.TableName == TableName);

                if (_tableInfo != null)
                {
                    TableNameText.Text = _tableInfo.TableName;
                    TestNameText.Text = _tableInfo.TestName;
                    CategoryText.Text = _tableInfo.Category;
                    TestIdText.Text = _tableInfo.TestId.ToString();
                    RowCountText.Text = _tableInfo.RowCount.ToString();
                    _rowCount = _tableInfo.RowCount;
                }
                else
                {
                    TableNameText.Text = TableName;
                    TestNameText.Text = "Unknown Test";
                    CategoryText.Text = "Unknown";
                    TestIdText.Text = "0";
                }

                _currentDataTable = await LoadTableDataFromDatabase(TableName);

                if (_currentDataTable != null && _currentDataTable.Rows.Count > 0)
                {
                    TableDataGrid.ItemsSource = _currentDataTable.DefaultView;
                    TableDataGrid.AutoGeneratingColumn += OnAutoGeneratingColumn;

                    // Subscribe to MouseRightButtonUp for context menu
                    TableDataGrid.PreviewMouseRightButtonUp += DataGrid_PreviewMouseRightButtonUp;

                    _rowCount = _currentDataTable.Rows.Count;
                    RowCountText.Text = _rowCount.ToString();

                    LoadingPanel.Visibility = Visibility.Collapsed;
                    TableDataGrid.Visibility = Visibility.Visible;

                    // ⭐ Load saved layout after data is displayed
                    await LoadSavedLayoutAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {_rowCount} rows from {TableName}");
                    StatusText.Text = "Ready - Double-click to edit • Drag borders to resize • Changes tracked automatically";
                }
                else
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    EmptyState.Visibility = Visibility.Visible;
                    _rowCount = 0;
                    RowCountText.Text = "0";

                    System.Diagnostics.Debug.WriteLine($"⚠ No data found in {TableName}");
                }

                DataLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading table data: {ex.Message}");

                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorState.Visibility = Visibility.Visible;
                ErrorMessageText.Text = ex.Message;
            }
        }

        // ================================================
        // DATABASE OPERATIONS
        // ================================================

        /// <summary>
        /// Load table data from database into DataTable
        /// </summary>
        private async System.Threading.Tasks.Task<DataTable> LoadTableDataFromDatabase(string tableName)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                DataTable dataTable = new DataTable();

                try
                {
                    string connectionString = DbConnectionConfig.GetConnectionString();

                    using (var connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = $"SELECT * FROM [ext].[{tableName}]";

                        using (var command = new SqlCommand(query, connection))
                        {
                            using (var adapter = new SqlDataAdapter(command))
                            {
                                adapter.Fill(dataTable);
                            }
                        }
                    }

                    return dataTable;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error loading data from database: {ex.Message}");
                    throw;
                }
            });
        }

        // ================================================
        // COLUMN GENERATION
        // ================================================

        /// <summary>
        /// ⭐ ENHANCED: Configure columns with resizing capabilities
        /// </summary>
        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string columnName = e.PropertyName;

            // ID column is read-only
            if (columnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                e.Column.IsReadOnly = true;
            }

            // Set initial width to auto
            e.Column.Width = DataGridLength.Auto;

            // Calculate minimum width based on header length
            int headerLength = columnName.Length;
            double minWidth = Math.Max(80, headerLength * 8 + 40);

            e.Column.MinWidth = minWidth;
            e.Column.MaxWidth = 800;
            e.Column.CanUserResize = true;

            // Store default width for reset functionality
            _defaultColumnWidths[columnName] = minWidth;
            _currentColumnWidths[columnName] = minWidth;

            // Configure text column styling
            if (e.Column is DataGridTextColumn textColumn)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));

                textColumn.ElementStyle = style;
            }
        }

        // ================================================
        // ⭐ NEW: ROW LOADING - ADD RESIZE GRIP TO ALL CELLS
        // ================================================

        /// <summary>
        /// Add row resize grip to all cells when rows are loaded
        /// </summary>
        private void TableDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                var row = e.Row;
                
                // Use Dispatcher to ensure visual tree is ready
                row.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // ⭐ Add resize grip to ALL cells, not just ID column
                        for (int colIndex = 0; colIndex < TableDataGrid.Columns.Count; colIndex++)
                        {
                            var cell = GetCell(TableDataGrid, row, colIndex);
                            
                            if (cell != null)
                            {
                                AddRowResizeGripToCell(cell, row, colIndex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Error adding resize grip: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in LoadingRow: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ ENHANCED: Track column width changes for save functionality
        /// </summary>
        private void TableDataGrid_ColumnWidthChanged(object sender, DataGridColumnEventArgs e)
        {
            try
            {
                if (_isLoadingLayout) return;

                string columnName = e.Column.Header?.ToString();
                if (!string.IsNullOrEmpty(columnName))
                {
                    double newWidth = e.Column.ActualWidth;
                    
                    // Only mark as changed if width actually changed
                    if (_currentColumnWidths.ContainsKey(columnName))
                    {
                        if (Math.Abs(_currentColumnWidths[columnName] - newWidth) > 0.1)
                        {
                            _currentColumnWidths[columnName] = newWidth;
                            MarkLayoutChanged();
                            
                            System.Diagnostics.Debug.WriteLine($"📏 Column '{columnName}' width changed to {newWidth:F1}px");
                        }
                    }
                    else
                    {
                        _currentColumnWidths[columnName] = newWidth;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error tracking column width change: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a specific cell from a DataGrid row
        /// </summary>
        private DataGridCell GetCell(DataGrid dataGrid, DataGridRow row, int columnIndex)
        {
            if (row == null) return null;

            var presenter = GetVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null) return null;

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            if (cell != null) return cell;

            // Cell may be virtualized, force generation
            dataGrid.ScrollIntoView(row, dataGrid.Columns[columnIndex]);
            cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;

            return cell;
        }

        /// <summary>
        /// Get visual child of specific type from parent
        /// </summary>
        private T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            
            for (int i = 0; i < numVisuals; i++)
            {
                var visual = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = visual as T;
                
                if (child == null)
                {
                    child = GetVisualChild<T>(visual);
                }
                
                if (child != null)
                {
                    break;
                }
            }
            
            return child;
        }

        /// <summary>
        /// ⭐ ENHANCED: Add row resize grip to any cell with better visibility
        /// </summary>
        private void AddRowResizeGripToCell(DataGridCell cell, DataGridRow row, int columnIndex)
        {
            try
            {
                // Find the Border in the cell template
                var border = GetVisualChild<Border>(cell);
                if (border == null) return;

                // Find the Grid inside the border
                var grid = GetVisualChild<Grid>(border);
                if (grid == null)
                {
                    // Create grid if it doesn't exist
                    grid = new Grid();
                    var content = border.Child;
                    border.Child = grid;
                    if (content != null)
                    {
                        grid.Children.Add(content);
                    }
                }

                // Check if resize grip already exists
                var existingGrip = grid.Children.OfType<Border>().FirstOrDefault(b => b.Name == "ResizeGripBorder");
                if (existingGrip != null) return;

                // ⭐ Create visible resize grip border container
                var gripBorder = new Border
                {
                    Name = "ResizeGripBorder",
                    Height = 8,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 0, -4),
                    Cursor = Cursors.SizeNS,
                    ToolTip = "Drag to resize row height"
                };

                // ⭐ Create visual indicator (visible on hover)
                var gripVisual = new Border
                {
                    Height = 3,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new System.Windows.Media.SolidColorBrush(Color.FromArgb(100, 127, 161, 255)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(Color.FromArgb(180, 127, 161, 255)),
                    BorderThickness = new Thickness(0, 1, 0, 1),
                    Opacity = 0,
                    CornerRadius = new CornerRadius(1)
                };

                gripBorder.Child = gripVisual;

                // ⭐ Show grip on mouse over
                gripBorder.MouseEnter += (s, e) =>
                {
                    gripVisual.Opacity = 1;
                };

                gripBorder.MouseLeave += (s, e) =>
                {
                    gripVisual.Opacity = 0;
                };

                // Create resize thumb
                var resizeThumb = new Thumb
                {
                    Height = 8,
                    Cursor = Cursors.SizeNS,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    Opacity = 0
                };

                gripBorder.Child = new Grid
                {
                    Children = { gripVisual, resizeThumb }
                };

                // Handle drag events
                double startHeight = 0;

                resizeThumb.DragStarted += (s, args) =>
                {
                    startHeight = row.ActualHeight;
                    gripVisual.Opacity = 1;
                    gripVisual.Background = new System.Windows.Media.SolidColorBrush(Color.FromArgb(200, 127, 161, 255));
                    System.Diagnostics.Debug.WriteLine($"🔧 Starting row resize from height: {startHeight:F1}px");
                };

                resizeThumb.DragDelta += (s, args) =>
                {
                    double newHeight = Math.Max(38, startHeight + args.VerticalChange);
                    row.Height = newHeight;
                    
                    // Track the change
                    int rowIndex = TableDataGrid.Items.IndexOf(row.Item);
                    _currentRowHeights[rowIndex] = newHeight;
                    MarkLayoutChanged();
                };

                resizeThumb.DragCompleted += (s, args) =>
                {
                    gripVisual.Opacity = 0;
                    gripVisual.Background = new System.Windows.Media.SolidColorBrush(Color.FromArgb(100, 127, 161, 255));
                    System.Diagnostics.Debug.WriteLine($"✓ Row resized to: {row.Height:F1}px");
                    
                    StatusText.Text = $"Row resized to {row.Height:F1}px - Don't forget to save";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];
                };

                // Add to grid
                grid.Children.Add(gripBorder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error adding resize grip: {ex.Message}");
            }
        }

        // ================================================
        // ⭐ NEW: LAYOUT SAVE/LOAD FUNCTIONALITY
        // ================================================

        /// <summary>
        /// Load saved layout from database
        /// </summary>
        private async System.Threading.Tasks.Task LoadSavedLayoutAsync()
        {
            try
            {
                _isLoadingLayout = true;
                System.Diagnostics.Debug.WriteLine($"📂 Loading saved layout for {TableName}...");

                var layout = await _layoutRepository.GetLayoutAsync(TableName);
                
                if (layout != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Found saved layout with {layout.ColumnWidths.Count} columns and {layout.RowHeights.Count} rows");

                    // Apply column widths
                    foreach (var col in TableDataGrid.Columns)
                    {
                        string columnName = col.Header?.ToString();
                        if (!string.IsNullOrEmpty(columnName) && layout.ColumnWidths.ContainsKey(columnName))
                        {
                            col.Width = new DataGridLength(layout.ColumnWidths[columnName]);
                            _currentColumnWidths[columnName] = layout.ColumnWidths[columnName];
                        }
                    }

                    // Apply row heights
                    _currentRowHeights = new Dictionary<int, double>(layout.RowHeights);
                    
                    // Apply heights to visible rows
                    foreach (var item in TableDataGrid.Items)
                    {
                        int rowIndex = TableDataGrid.Items.IndexOf(item);
                        if (layout.RowHeights.ContainsKey(rowIndex))
                        {
                            var row = TableDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
                            if (row != null)
                            {
                                row.Height = layout.RowHeights[rowIndex];
                            }
                        }
                    }

                    StatusText.Text = "✓ Loaded saved layout";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ No saved layout found, using defaults");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading layout: {ex.Message}");
            }
            finally
            {
                _isLoadingLayout = false;
                _hasUnsavedChanges = false;
                UpdateSaveButtonVisibility();
            }
        }

        /// <summary>
        /// Save current layout to database
        /// </summary>
        private async void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                var result = ModernMessageDialog.ShowQuestion(
                    $"⚠️ You are about to save layout changes for '{TableName}'.\n\n" +
                    $"This will affect ALL USERS viewing this table.\n\n" +
                    $"Column widths: {_currentColumnWidths.Count}\n" +
                    $"Row heights: {_currentRowHeights.Count}\n\n" +
                    $"Are you sure you want to save these changes?",
                    "Save Layout Changes?",
                    Window.GetWindow(this));

                if (result != MessageBoxResult.Yes && result != MessageBoxResult.OK)
                {
                    StatusText.Text = "Layout save cancelled";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
                    return;
                }

                StatusText.Text = "Saving layout...";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                // Collect current column widths
                foreach (var col in TableDataGrid.Columns)
                {
                    string columnName = col.Header?.ToString();
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        _currentColumnWidths[columnName] = col.ActualWidth;
                    }
                }

                // Create layout object
                var layout = new ExtTableLayout
                {
                    TableName = TableName,
                    ColumnWidths = new Dictionary<string, double>(_currentColumnWidths),
                    RowHeights = new Dictionary<int, double>(_currentRowHeights)
                };

                // Save to database
                await _layoutRepository.SaveLayoutAsync(layout);

                _hasUnsavedChanges = false;
                UpdateSaveButtonVisibility();

                StatusText.Text = $"✓ Layout saved successfully! (Columns: {layout.ColumnWidths.Count}, Rows: {layout.RowHeights.Count})";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                System.Diagnostics.Debug.WriteLine($"✓ Layout saved for {TableName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving layout: {ex.Message}");

                StatusText.Text = "✗ Failed to save layout";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];

                ModernMessageDialog.ShowError(
                    $"Failed to save layout.\n\nError: {ex.Message}",
                    "Save Failed",
                    Window.GetWindow(this));
            }
        }

        /// <summary>
        /// Mark layout as changed and show save button
        /// </summary>
        private void MarkLayoutChanged()
        {
            if (_isLoadingLayout) return;

            if (!_hasUnsavedChanges)
            {
                _hasUnsavedChanges = true;
                UpdateSaveButtonVisibility();
                System.Diagnostics.Debug.WriteLine("📝 Layout marked as changed");
            }
        }

        /// <summary>
        /// Update save button and indicator visibility
        /// </summary>
        private void UpdateSaveButtonVisibility()
        {
            SaveLayoutButton.Visibility = _hasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
            UnsavedChangesIndicator.Visibility = _hasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================================================
        // ⭐ NEW: SIZE RESET FUNCTIONALITY
        // ================================================

        /// <summary>
        /// Reset all column widths and row heights to defaults
        /// </summary>
        private void ResetSizesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Resetting column widths and row heights...");

                _isLoadingLayout = true;

                // Reset all column widths to default
                foreach (var column in TableDataGrid.Columns)
                {
                    string columnName = column.Header?.ToString() ?? "";
                    
                    if (_defaultColumnWidths.ContainsKey(columnName))
                    {
                        column.Width = new DataGridLength(_defaultColumnWidths[columnName]);
                        _currentColumnWidths[columnName] = _defaultColumnWidths[columnName];
                    }
                    else
                    {
                        column.Width = DataGridLength.Auto;
                    }
                }

                // Reset all row heights
                foreach (var item in TableDataGrid.Items)
                {
                    var row = TableDataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null)
                    {
                        row.Height = double.NaN; // Auto height
                    }
                }
                _currentRowHeights.Clear();

                _isLoadingLayout = false;

                MarkLayoutChanged();

                StatusText.Text = "✓ Sizes reset to defaults - Don't forget to save if you want to keep these changes";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                System.Diagnostics.Debug.WriteLine("✓ Sizes reset successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error resetting sizes: {ex.Message}");
                StatusText.Text = "✗ Failed to reset sizes";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];
            }
        }

        // ================================================
        // CONTEXT MENU
        // ================================================

        /// <summary>
        /// Handle right-click on DataGrid to show context menu for column headers
        /// </summary>
        private void DataGrid_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Find what was clicked
                DependencyObject dep = (DependencyObject)e.OriginalSource;

                // Navigate up the visual tree to find if we clicked on a column header
                while (dep != null && !(dep is DataGridColumnHeader))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }

                if (dep is DataGridColumnHeader header && header.Content != null)
                {
                    string columnName = header.Content.ToString();

                    System.Diagnostics.Debug.WriteLine($"🖱️ Right-clicked on column: {columnName}");

                    // ⭐ Special context menu for ID column
                    if (columnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowIdColumnContextMenu(header);
                        e.Handled = true;
                        return;
                    }

                    // Create and show context menu for other columns
                    ShowColumnContextMenu(header, columnName);

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error handling right-click: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ NEW: Show special context menu for ID column header
        /// </summary>
        private void ShowIdColumnContextMenu(FrameworkElement target)
        {
            try
            {
                // Create context menu
                var contextMenu = new ContextMenu
                {
                    PlacementTarget = target,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],
                    BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Padding = new Thickness(4)
                };

                // ⭐ Menu Item: Select All Rows
                var selectAllItem = new MenuItem
                {
                    Header = "✓ Select All Rows",
                    FontWeight = FontWeights.SemiBold
                };
                selectAllItem.Click += (s, args) =>
                {
                    TableDataGrid.SelectAll();
                    StatusText.Text = $"✓ Selected all {TableDataGrid.Items.Count} rows";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                };
                contextMenu.Items.Add(selectAllItem);

                contextMenu.Items.Add(new Separator());

                // ⭐ Menu Item: Resize All Rows to Same Height
                var resizeAllItem = new MenuItem
                {
                    Header = "↕️ Resize All Rows to Same Height...",
                    FontWeight = FontWeights.SemiBold
                };
                resizeAllItem.Click += (s, args) =>
                {
                    ShowResizeAllRowsDialog();
                };
                contextMenu.Items.Add(resizeAllItem);

                // ⭐ Menu Item: Reset All Row Heights
                var resetRowsItem = new MenuItem
                {
                    Header = "🔄 Reset All Row Heights"
                };
                resetRowsItem.Click += (s, args) =>
                {
                    ResetAllRowHeights();
                };
                contextMenu.Items.Add(resetRowsItem);

                // Show the context menu
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing ID column context menu: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ NEW: Show dialog to resize all rows to same height
        /// </summary>
        private void ShowResizeAllRowsDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "Resize All Rows",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],
                    ResizeMode = ResizeMode.NoResize
                };

                var stackPanel = new StackPanel { Margin = new Thickness(20) };

                var label = new TextBlock
                {
                    Text = "Enter row height (pixels):",
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextPrimaryBrush"],
                    Margin = new Thickness(0, 0, 0, 10),
                    FontSize = 14
                };
                stackPanel.Children.Add(label);

                var textBox = new TextBox
                {
                    Text = "38",
                    FontSize = 16,
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                stackPanel.Children.Add(textBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var okButton = new Button
                {
                    Content = "Apply",
                    Width = 80,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                okButton.Click += (s, e) =>
                {
                    if (double.TryParse(textBox.Text, out double height) && height >= 38 && height <= 500)
                    {
                        dialog.DialogResult = true;
                        dialog.Tag = height;
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("Please enter a valid height between 38 and 500 pixels.", "Invalid Input",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                buttonPanel.Children.Add(okButton);

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 32
                };
                cancelButton.Click += (s, e) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(buttonPanel);
                dialog.Content = stackPanel;

                textBox.Focus();
                textBox.SelectAll();

                if (dialog.ShowDialog() == true && dialog.Tag is double newHeight)
                {
                    ResizeAllRowsToHeight(newHeight);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing resize all rows dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ NEW: Resize all rows to specified height
        /// </summary>
        private void ResizeAllRowsToHeight(double height)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📏 Resizing all rows to {height}px...");

                int count = 0;
                for (int i = 0; i < TableDataGrid.Items.Count; i++)
                {
                    var row = TableDataGrid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                    if (row != null)
                    {
                        row.Height = height;
                        _currentRowHeights[i] = height;
                        count++;
                    }
                }

                // For rows that aren't loaded yet, we'll set them when they load
                // Store the desired height for all rows
                for (int i = 0; i < TableDataGrid.Items.Count; i++)
                {
                    _currentRowHeights[i] = height;
                }

                MarkLayoutChanged();

                StatusText.Text = $"✓ Resized {count} visible rows to {height}px - Don't forget to save";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                System.Diagnostics.Debug.WriteLine($"✓ Resized {count} rows");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error resizing all rows: {ex.Message}");
                StatusText.Text = "✗ Failed to resize rows";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];
            }
        }

        /// <summary>
        /// ⭐ NEW: Reset all row heights to default
        /// </summary>
        private void ResetAllRowHeights()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Resetting all row heights...");

                int count = 0;
                foreach (var item in TableDataGrid.Items)
                {
                    var row = TableDataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null)
                    {
                        row.Height = double.NaN; // Auto height
                        count++;
                    }
                }
                
                _currentRowHeights.Clear();
                MarkLayoutChanged();

                StatusText.Text = $"✓ Reset {count} row heights to default - Don't forget to save";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                System.Diagnostics.Debug.WriteLine($"✓ Reset {count} rows");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error resetting row heights: {ex.Message}");
            }
        }

        /// <summary>
        /// Create and show context menu for column
        /// </summary>
        private void ShowColumnContextMenu(FrameworkElement target, string columnName)
        {
            try
            {
                // Create context menu
                var contextMenu = new ContextMenu
                {
                    PlacementTarget = target,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                    Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],
                    BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Padding = new Thickness(4)
                };

                // Menu Item: Rename Column
                var renameItem = new MenuItem
                {
                    Header = $"✏️ Rename '{columnName}'",
                    Tag = columnName
                };
                renameItem.Click += async (s, args) =>
                {
                    await ShowRenameColumnDialog(columnName);
                };
                contextMenu.Items.Add(renameItem);

                // Menu Item: Edit Column Length
                var editLengthItem = new MenuItem
                {
                    Header = $"📏 Edit Column Length",
                    Tag = columnName
                };
                editLengthItem.Click += (s, args) =>
                {
                    EditColumnLength_Click(columnName);
                };
                contextMenu.Items.Add(editLengthItem);

                // Menu Item: Auto-size this column
                contextMenu.Items.Add(new Separator());
                var autoSizeItem = new MenuItem
                {
                    Header = "↔️ Auto-size This Column",
                    Tag = columnName
                };
                autoSizeItem.Click += (s, args) =>
                {
                    AutoSizeColumn(columnName);
                };
                contextMenu.Items.Add(autoSizeItem);

                // Menu Item: Auto-size all columns
                var autoSizeAllItem = new MenuItem
                {
                    Header = "↔️ Auto-size All Columns"
                };
                autoSizeAllItem.Click += (s, args) =>
                {
                    AutoSizeAllColumns();
                };
                contextMenu.Items.Add(autoSizeAllItem);

                // Show the context menu
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error showing context menu: {ex.Message}");
            }
        }

        // ================================================
        // AUTO-SIZE FUNCTIONALITY
        // ================================================

        /// <summary>
        /// Auto-size a specific column to fit content
        /// </summary>
        private void AutoSizeColumn(string columnName)
        {
            try
            {
                var column = TableDataGrid.Columns.FirstOrDefault(c => 
                    c.Header?.ToString()?.Equals(columnName, StringComparison.OrdinalIgnoreCase) == true);

                if (column != null)
                {
                    column.Width = DataGridLength.Auto;
                    _currentColumnWidths[columnName] = column.ActualWidth;
                    MarkLayoutChanged();
                    
                    StatusText.Text = $"✓ Auto-sized column '{columnName}' - Don't forget to save";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                    
                    System.Diagnostics.Debug.WriteLine($"✓ Auto-sized column: {columnName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error auto-sizing column: {ex.Message}");
            }
        }

        /// <summary>
        /// Auto-size all columns to fit content
        /// </summary>
        private void AutoSizeAllColumns()
        {
            try
            {
                foreach (var column in TableDataGrid.Columns)
                {
                    column.Width = DataGridLength.Auto;
                    string columnName = column.Header?.ToString();
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        _currentColumnWidths[columnName] = column.ActualWidth;
                    }
                }

                MarkLayoutChanged();

                StatusText.Text = "✓ Auto-sized all columns - Don't forget to save";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];
                
                System.Diagnostics.Debug.WriteLine("✓ Auto-sized all columns");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error auto-sizing columns: {ex.Message}");
            }
        }

        // ================================================
        // CELL EDITING
        // ================================================

        private void TableDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                string columnName = e.Column.Header?.ToString() ?? "Unknown";
                int rowIndex = e.Row.GetIndex();

                System.Diagnostics.Debug.WriteLine($"✏️ Starting edit: Column='{columnName}', Row={rowIndex}");

                StatusText.Text = $"Editing {columnName} (Row {rowIndex + 1})";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error in BeginningEdit: {ex.Message}");
            }
        }

        private async void TableDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                try
                {
                    string columnName = e.Column.Header?.ToString();
                    var editingElement = e.EditingElement as TextBox;

                    if (editingElement != null && !string.IsNullOrEmpty(columnName))
                    {
                        string newValue = editingElement.Text;
                        var rowView = e.Row.Item as DataRowView;

                        if (rowView != null)
                        {
                            int rowId = Convert.ToInt32(rowView["Id"]);
                            string oldValue = rowView[columnName]?.ToString() ?? "";

                            if (newValue != oldValue)
                            {
                                System.Diagnostics.Debug.WriteLine($"💾 Saving change: Row {rowId}, Column '{columnName}'");
                                System.Diagnostics.Debug.WriteLine($"   Old: '{oldValue}' → New: '{newValue}'");

                                StatusText.Text = "Saving changes...";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                                await _dataRepository.UpdateCellValueAsync(TableName, rowId, columnName, newValue);

                                StatusText.Text = $"✓ Saved: {columnName} updated";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                                System.Diagnostics.Debug.WriteLine("✓ Cell updated successfully");
                            }
                        }
                    }
                }
                catch (ColumnLengthExceededException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Column length exceeded: {ex.Message}");

                    StatusText.Text = $"✗ Text too long for column '{ex.ColumnName}'";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];

                    ModernMessageDialog.ShowWarning(
                        ex.GetDetailedMessage(),
                        "Text Too Long",
                        Window.GetWindow(this));

                    RefreshData();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error saving cell: {ex.Message}");

                    StatusText.Text = $"✗ Error saving changes";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];

                    ModernMessageDialog.ShowError(
                        $"Failed to save changes.\n\nError: {ex.Message}",
                        "Save Error",
                        Window.GetWindow(this));

                    RefreshData();
                }
            }
            else
            {
                StatusText.Text = "Edit cancelled";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
            }
        }

        // ================================================
        // COLUMN RENAMING
        // ================================================

        private async void EditColumnName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                string currentColumnName = button.Tag.ToString();
                await ShowRenameColumnDialog(currentColumnName);
            }
        }

        private async System.Threading.Tasks.Task ShowRenameColumnDialog(string currentColumnName)
        {
            if (currentColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                ModernMessageDialog.ShowWarning(
                    "The 'Id' column cannot be renamed as it is a primary key.",
                    "Cannot Rename",
                    Window.GetWindow(this));
                return;
            }

            try
            {
                var dialog = new RenameColumnDialog(currentColumnName);
                dialog.Owner = Window.GetWindow(this);

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    string newName = dialog.NewColumnName;

                    StatusText.Text = $"Renaming column '{currentColumnName}' to '{newName}'...";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                    await _dataRepository.RenameColumnAsync(TableName, currentColumnName, newName);

                    StatusText.Text = $"✓ Column renamed successfully!";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                    ModernMessageDialog.ShowSuccess(
                        $"Column renamed successfully!\n\n'{currentColumnName}' → '{newName}'",
                        "Success",
                        Window.GetWindow(this));

                    RefreshData();
                }
                else
                {
                    StatusText.Text = "Column rename cancelled";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"✗ Failed to rename column";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];

                ModernMessageDialog.ShowError(
                    $"Failed to rename column.\n\nError: {ex.Message}",
                    "Rename Failed",
                    Window.GetWindow(this));
            }
        }

        // ================================================
        // COLUMN LENGTH EDITING
        // ================================================

        private async void EditColumnLength_Click(string columnName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📏 Opening edit length dialog for: {columnName}");

                var columnInfo = await _dataRepository.GetColumnInfoAsync(TableName, columnName);

                if (columnInfo == null || !columnInfo.MaxLength.HasValue)
                {
                    ModernMessageDialog.ShowWarning(
                        $"Column '{columnName}' does not have a length limit, or is not a text column.",
                        "Cannot Edit Length",
                        Window.GetWindow(this));
                    return;
                }

                var dialog = new EditColumnLengthDialog(columnName, columnInfo.MaxLength.Value);
                dialog.Owner = Window.GetWindow(this);

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    int newLength = dialog.NewLength;
                    string lengthText = newLength == -1 ? "MAX" : $"{newLength} characters";

                    StatusText.Text = $"Updating column '{columnName}' length to {lengthText}...";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                    await _dataRepository.ExpandColumnSizeAsync(TableName, columnName, newLength);

                    StatusText.Text = $"✓ Column length updated successfully!";
                    StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                    ModernMessageDialog.ShowSuccess(
                        $"Column '{columnName}' has been expanded to {lengthText}.\n\nYou can now enter longer text in this column.",
                        "Length Updated",
                        Window.GetWindow(this));

                    RefreshData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error editing column length: {ex.Message}");

                StatusText.Text = $"✗ Failed to update column length";
                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ErrorBrush"];

                ModernMessageDialog.ShowError(
                    $"Failed to update column length.\n\nError: {ex.Message}",
                    "Update Failed",
                    Window.GetWindow(this));
            }
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        public int GetRowCount()
        {
            return _rowCount;
        }

        public void RefreshData()
        {
            LoadTableData();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hasUnsavedChanges)
                {
                    var result = ModernMessageDialog.ShowQuestion(
                        "You have unsaved layout changes.\n\nRefreshing will discard these changes. Continue?",
                        "Unsaved Changes",
                        Window.GetWindow(this));

                    if (result != MessageBoxResult.Yes && result != MessageBoxResult.OK)
                    {
                        return;
                    }
                }

                StatusText.Text = "Refreshing…";
                RefreshData();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Refresh failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex}");
            }
        }
    }
}