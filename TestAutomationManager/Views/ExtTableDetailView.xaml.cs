using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TestAutomationManager.Data;
using TestAutomationManager.Dialogs;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Views
{
    public partial class ExtTableDetailView : UserControl
    {
        private readonly ITestRepository _repository;
        private readonly ExtTableDataRepository _dataRepository;
        public string TableName { get; private set; }
        private ExternalTableInfo _tableInfo;
        private int _rowCount;
        private DataTable _currentDataTable;

        public event EventHandler DataLoaded;

        public ExtTableDetailView(string tableName)
        {
            InitializeComponent();

            _repository = new TestRepository();
            _dataRepository = new ExtTableDataRepository();
            TableName = tableName;
            _rowCount = 0;

            LoadTableData();
        }

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

                    _rowCount = _currentDataTable.Rows.Count;
                    RowCountText.Text = _rowCount.ToString();

                    LoadingPanel.Visibility = Visibility.Collapsed;
                    TableDataGrid.Visibility = Visibility.Visible;

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {_rowCount} rows from {TableName}");
                    StatusText.Text = "Ready - Double-click any cell to edit";
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

        /// <summary>
        /// Configure columns for dynamic sizing based on content
        /// ⭐ FIXED: Ensure column width accommodates both header text AND cell content
        /// </summary>
        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string columnName = e.PropertyName;

            // Make ID column read-only
            if (columnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                e.Column.IsReadOnly = true;
            }

            // ⭐ FIXED: Use Auto instead of SizeToCells to include header width
            e.Column.Width = DataGridLength.Auto;

            // Calculate minimum width based on header text length
            // This ensures the header is never cut off
            int headerLength = columnName.Length;
            double calculatedMinWidth = Math.Max(80, headerLength * 8 + 40); // 8px per char + 40px padding

            e.Column.MinWidth = calculatedMinWidth;
            e.Column.MaxWidth = 500;
            e.Column.CanUserResize = true;

            // Center alignment for all columns
            if (e.Column is DataGridTextColumn textColumn)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));

                textColumn.ElementStyle = style;
            }
        }

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
        // CELL EDITING
        // ================================================

        private void TableDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            StatusText.Text = $"Editing {e.Column.Header}...";
            StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];
        }

        private async void TableDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                try
                {
                    if (e.EditingElement is TextBox textBox)
                    {
                        string newValue = textBox.Text;
                        string columnName = e.Column.Header.ToString();

                        var rowView = e.Row.Item as DataRowView;
                        if (rowView != null)
                        {
                            int rowId = Convert.ToInt32(rowView["Id"]);
                            object oldValue = rowView[columnName];

                            if (newValue != oldValue?.ToString())
                            {
                                StatusText.Text = $"Saving changes to {columnName}...";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["WarningBrush"];

                                await _dataRepository.UpdateCellValueAsync(TableName, rowId, columnName, newValue);

                                StatusText.Text = $"✓ Saved: {columnName} updated successfully";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SuccessBrush"];

                                System.Diagnostics.Debug.WriteLine($"✓ Updated {TableName}.{columnName} for row {rowId}");

                                await System.Threading.Tasks.Task.Delay(3000);
                                StatusText.Text = "Ready - Double-click any cell to edit";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
                            }
                            else
                            {
                                StatusText.Text = "No changes made";
                                StatusText.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error saving cell: {ex.Message}");

                    StatusText.Text = $"✗ Error: {ex.Message}";
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
                // Show custom modern dialog
                var dialog = new TestAutomationManager.Dialogs.RenameColumnDialog(currentColumnName);
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