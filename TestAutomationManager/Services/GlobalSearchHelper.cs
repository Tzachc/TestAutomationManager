using System.Windows;
using TestAutomationManager.Views;

namespace TestAutomationManager.Services
{
    public static class GlobalSearchHelper
    {
        public static void ExecuteGlobalSearch(string query, Window owner)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            // Search inside currently visible view
            if (owner is MainWindow mainWindow &&
                mainWindow.ContentTabControl.SelectedItem is System.Windows.Controls.TabItem selectedTab)
            {
                if (selectedTab.Content is TestsView testsView)
                {
                    testsView.FilterTests(query);
                    return;
                }

                if (selectedTab.Content is ExtTableDetailView extTableView)
                {
                    // Filter rows directly in DataGrid
                    var view = extTableView.FindName("TableDataGrid") as System.Windows.Controls.DataGrid;
                    if (view?.ItemsSource is System.Data.DataView dataView)
                    {
                        try
                        {
                            string filter = string.Join(" OR ",
                                dataView.Table.Columns.Cast<System.Data.DataColumn>()
                                    .Select(c => $"CONVERT([{c.ColumnName}], System.String) LIKE '%{query.Replace("'", "''")}%'"));
                            dataView.RowFilter = filter;
                        }
                        catch
                        {
                            // fallback: ignore errors
                        }
                    }
                }
            }
        }
    }
}
