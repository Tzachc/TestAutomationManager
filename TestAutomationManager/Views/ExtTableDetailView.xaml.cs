using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using TestAutomationManager.Data;
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
        public string TableName { get; private set; }
        private ExternalTableInfo _tableInfo;
        private int _rowCount;

        /// <summary>
        /// Event fired when data is loaded
        /// </summary>
        public event EventHandler DataLoaded;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public ExtTableDetailView(string tableName)
        {
            InitializeComponent();

            _repository = new TestRepository();
            TableName = tableName;
            _rowCount = 0;

            LoadTableData();
        }

        // ================================================
        // DATA LOADING
        // ================================================

        /// <summary>
        /// Load table data and metadata
        /// </summary>
        private async void LoadTableData()
        {
            try
            {
                // Show loading indicator
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
                ErrorState.Visibility = Visibility.Collapsed;
                TableDataGrid.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine($"📊 Loading data for {TableName}...");

                // Load table metadata
                var allTables = await _repository.GetAllExternalTablesAsync();
                _tableInfo = allTables.FirstOrDefault(t => t.TableName == TableName);

                if (_tableInfo != null)
                {
                    // Update header info
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

                // Load actual table data
                DataTable dataTable = await LoadTableDataFromDatabase(TableName);

                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    // Bind data to DataGrid
                    TableDataGrid.ItemsSource = dataTable.DefaultView;
                    _rowCount = dataTable.Rows.Count;
                    RowCountText.Text = _rowCount.ToString();

                    // Show DataGrid
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    TableDataGrid.Visibility = Visibility.Visible;

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {_rowCount} rows from {TableName}");
                }
                else
                {
                    // Show empty state
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

                // Show error state
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorState.Visibility = Visibility.Visible;
                ErrorMessageText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Load table data from database into a DataTable
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

                        // Query to get all data from the table
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
        // PUBLIC METHODS
        // ================================================

        /// <summary>
        /// Get row count for display
        /// </summary>
        public int GetRowCount()
        {
            return _rowCount;
        }

        /// <summary>
        /// Refresh data from database
        /// </summary>
        public void RefreshData()
        {
            LoadTableData();
        }
    }
}