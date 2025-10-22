using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TestAutomationManager.Data;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for saving and loading ExtTable layout configurations (column widths, row heights)
    /// </summary>
    public class ExtTableLayoutRepository
    {
        private readonly string _connectionString;

        public ExtTableLayoutRepository()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
        }

        // ================================================
        // SAVE LAYOUT
        // ================================================

        /// <summary>
        /// Save layout configuration for a specific ExtTable
        /// </summary>
        public async Task SaveLayoutAsync(ExtTableLayout layout)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💾 Saving layout for {layout.TableName}...");

                // Serialize layout data to JSON
                string columnWidthsJson = JsonSerializer.Serialize(layout.ColumnWidths);
                string rowHeightsJson = JsonSerializer.Serialize(layout.RowHeights);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if layout exists
                    bool exists = await LayoutExistsAsync(connection, layout.TableName);

                    string query;
                    if (exists)
                    {
                        // Update existing layout
                        query = @"
                            UPDATE [dbo].[ExtTableLayouts]
                            SET 
                                ColumnWidthsJson = @columnWidthsJson,
                                RowHeightsJson = @rowHeightsJson,
                                LastModified = GETDATE(),
                                ModifiedBy = SYSTEM_USER
                            WHERE TableName = @tableName";
                    }
                    else
                    {
                        // Insert new layout
                        query = @"
                            INSERT INTO [dbo].[ExtTableLayouts]
                            (TableName, ColumnWidthsJson, RowHeightsJson, LastModified, ModifiedBy)
                            VALUES
                            (@tableName, @columnWidthsJson, @rowHeightsJson, GETDATE(), SYSTEM_USER)";
                    }

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", layout.TableName);
                        command.Parameters.AddWithValue("@columnWidthsJson", columnWidthsJson);
                        command.Parameters.AddWithValue("@rowHeightsJson", rowHeightsJson);

                        await command.ExecuteNonQueryAsync();

                        System.Diagnostics.Debug.WriteLine($"✓ Layout saved for {layout.TableName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving layout: {ex.Message}");
                throw new Exception($"Failed to save layout for {layout.TableName}", ex);
            }
        }

        // ================================================
        // LOAD LAYOUT
        // ================================================

        /// <summary>
        /// Get layout configuration for a specific ExtTable
        /// </summary>
        public async Task<ExtTableLayout> GetLayoutAsync(string tableName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📂 Loading layout for {tableName}...");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT ColumnWidthsJson, RowHeightsJson, LastModified, ModifiedBy
                        FROM [dbo].[ExtTableLayouts]
                        WHERE TableName = @tableName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string columnWidthsJson = reader.GetString(0);
                                string rowHeightsJson = reader.GetString(1);
                                DateTime lastModified = reader.GetDateTime(2);
                                string modifiedBy = reader.GetString(3);

                                // Deserialize JSON
                                var columnWidths = JsonSerializer.Deserialize<Dictionary<string, double>>(columnWidthsJson);
                                var rowHeights = JsonSerializer.Deserialize<Dictionary<int, double>>(rowHeightsJson);

                                System.Diagnostics.Debug.WriteLine($"✓ Loaded layout for {tableName} (modified by {modifiedBy} on {lastModified})");

                                return new ExtTableLayout
                                {
                                    TableName = tableName,
                                    ColumnWidths = columnWidths ?? new Dictionary<string, double>(),
                                    RowHeights = rowHeights ?? new Dictionary<int, double>(),
                                    LastModified = lastModified,
                                    ModifiedBy = modifiedBy
                                };
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"ℹ No layout found for {tableName}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading layout: {ex.Message}");
                throw new Exception($"Failed to load layout for {tableName}", ex);
            }
        }

        // ================================================
        // DELETE LAYOUT
        // ================================================

        /// <summary>
        /// Delete layout configuration for a specific ExtTable
        /// </summary>
        public async Task DeleteLayoutAsync(string tableName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🗑️ Deleting layout for {tableName}...");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "DELETE FROM [dbo].[ExtTableLayouts] WHERE TableName = @tableName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Layout deleted for {tableName}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"ℹ No layout found to delete for {tableName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting layout: {ex.Message}");
                throw new Exception($"Failed to delete layout for {tableName}", ex);
            }
        }

        // ================================================
        // HELPER METHODS
        // ================================================

        /// <summary>
        /// Check if layout exists for a table
        /// </summary>
        private async Task<bool> LayoutExistsAsync(SqlConnection connection, string tableName)
        {
            string query = "SELECT COUNT(*) FROM [dbo].[ExtTableLayouts] WHERE TableName = @tableName";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);
                int count = (int)await command.ExecuteScalarAsync();
                return count > 0;
            }
        }

        /// <summary>
        /// Get all saved layouts
        /// </summary>
        public async Task<List<string>> GetAllLayoutTablesAsync()
        {
            try
            {
                var tables = new List<string>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT TableName FROM [dbo].[ExtTableLayouts] ORDER BY TableName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tables.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return tables;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting layout tables: {ex.Message}");
                return new List<string>();
            }
        }
    }
}