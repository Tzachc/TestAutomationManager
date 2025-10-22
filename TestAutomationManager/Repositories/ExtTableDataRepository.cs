using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using TestAutomationManager.Data;
using TestAutomationManager.Exceptions;
using TestAutomationManager.Exceptions;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for managing data within ExtTable tables (CRUD operations on rows and columns)
    /// </summary>
    public class ExtTableDataRepository
    {
        private readonly string _connectionString;

        public ExtTableDataRepository()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
        }

        // ================================================
        // UPDATE CELL VALUE
        // ================================================

        /// <summary>
        /// Update a single cell value in an ExtTable
        /// ⭐ FIXED: Better handling of text length limits
        /// </summary>

public async Task UpdateCellValueAsync(string tableName, int rowId, string columnName, object newValue)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"💾 Updating {tableName}.{columnName} for row {rowId}...");

            // Get column info to check constraints
            var columnInfo = await GetColumnInfoAsync(tableName, columnName);

            // ⭐ Check if value exceeds column length
            if (newValue != null && columnInfo != null)
            {
                string valueStr = newValue.ToString();

                if (columnInfo.MaxLength.HasValue &&
                    columnInfo.MaxLength.Value > 0 &&
                    valueStr.Length > columnInfo.MaxLength.Value)
                {
                    // ⭐ Throw custom exception with all the details
                    throw new ColumnLengthExceededException(
                        columnName,
                        columnInfo.MaxLength.Value,
                        valueStr.Length);
                }
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                if (!IsValidColumnName(columnName))
                {
                    throw new ArgumentException($"Invalid column name: {columnName}");
                }

                string query = $@"
                UPDATE [ext].[{tableName}]
                SET [{columnName}] = @newValue
                WHERE [Id] = @rowId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@newValue", newValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@rowId", rowId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Cell updated successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ No rows updated (row ID {rowId} not found)");
                    }
                }
            }
        }
        catch (SqlException ex) when (ex.Message.Contains("would be truncated"))
        {
            // ⭐ Catch SQL truncation errors that slip through
            System.Diagnostics.Debug.WriteLine($"✗ SQL truncation error: {ex.Message}");

            // Try to extract column info and throw our custom exception
            var columnInfo = await GetColumnInfoAsync(tableName, columnName);
            if (columnInfo?.MaxLength != null)
            {
                throw new ColumnLengthExceededException(
                    columnName,
                    columnInfo.MaxLength.Value,
                    newValue?.ToString()?.Length ?? 0);
            }
            else
            {
                throw new InvalidOperationException(
                    "The text you entered is too long for this column.\n\n" +
                    "Please shorten your text or contact your administrator.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"✗ Error updating cell: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// ⭐ NEW: Get column information including max length
    /// </summary>
    public async Task<ColumnInfo> GetColumnInfoAsync(string tableName, string columnName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    DATA_TYPE,
                    CHARACTER_MAXIMUM_LENGTH,
                    IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'ext'
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        command.Parameters.AddWithValue("@columnName", columnName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ColumnInfo
                                {
                                    DataType = reader.GetString(0),
                                    MaxLength = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                    IsNullable = reader.GetString(2) == "YES"
                                };
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting column info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper class to store column information
        /// </summary>
        public class ColumnInfo
        {
            public string DataType { get; set; }
            public int? MaxLength { get; set; }
            public bool IsNullable { get; set; }
        }

        // ================================================
        // RENAME COLUMN
        // ================================================

        /// <summary>
        /// Rename a column in an ExtTable
        /// </summary>
        /// <param name="tableName">Name of the ExtTable (e.g., "ExtTable1")</param>
        /// <param name="oldColumnName">Current column name</param>
        /// <param name="newColumnName">New column name</param>
        public async Task RenameColumnAsync(string tableName, string oldColumnName, string newColumnName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"✏️ Renaming column {oldColumnName} to {newColumnName} in {tableName}...");

                // Validate column names
                if (!IsValidColumnName(oldColumnName) || !IsValidColumnName(newColumnName))
                {
                    throw new ArgumentException("Invalid column name");
                }

                // Check if new column name already exists
                if (await ColumnExistsAsync(tableName, newColumnName))
                {
                    throw new InvalidOperationException($"Column '{newColumnName}' already exists");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Use sp_rename to rename the column
                    string query = $"EXEC sp_rename '[ext].[{tableName}].[{oldColumnName}]', '{newColumnName}', 'COLUMN'";

                    using (var command = new SqlCommand(query, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✓ Column renamed successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error renaming column: {ex.Message}");
                throw new Exception($"Failed to rename column in {tableName}", ex);
            }
        }

        // ================================================
        // VALIDATION & HELPER METHODS
        // ================================================

        /// <summary>
        /// Check if a column exists in the table
        /// </summary>
        private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'ext'
                          AND TABLE_NAME = @tableName
                          AND COLUMN_NAME = @columnName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        command.Parameters.AddWithValue("@columnName", columnName);

                        int count = (int)await command.ExecuteScalarAsync();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking column existence: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate column name to prevent SQL injection
        /// Only allows alphanumeric characters, underscores, and must not start with a number
        /// </summary>
        private bool IsValidColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            // Must start with a letter or underscore
            if (!char.IsLetter(columnName[0]) && columnName[0] != '_')
                return false;

            // Only allow letters, numbers, and underscores
            foreach (char c in columnName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            // Prevent SQL reserved keywords (add more as needed)
            string[] reservedWords = { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE" };
            string upperName = columnName.ToUpperInvariant();
            foreach (string reserved in reservedWords)
            {
                if (upperName == reserved)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get column data type
        /// </summary>
        public async Task<string> GetColumnDataTypeAsync(string tableName, string columnName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT DATA_TYPE
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'ext'
                          AND TABLE_NAME = @tableName
                          AND COLUMN_NAME = @columnName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        command.Parameters.AddWithValue("@columnName", columnName);

                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString() ?? "unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting column data type: {ex.Message}");
                return "unknown";
            }
        }

        /// <summary>
        /// Expand column size to accommodate longer text
        /// </summary>
        public async Task ExpandColumnSizeAsync(string tableName, string columnName, int newMaxLength)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📏 Expanding {tableName}.{columnName} to {newMaxLength} characters...");

                if (!IsValidColumnName(columnName))
                {
                    throw new ArgumentException($"Invalid column name: {columnName}");
                }

                // Get current column info
                var columnInfo = await GetColumnInfoAsync(tableName, columnName);
                if (columnInfo == null)
                {
                    throw new InvalidOperationException($"Column '{columnName}' not found");
                }

                string dataType = columnInfo.DataType.ToUpperInvariant();

                // Only works for VARCHAR/NVARCHAR types
                if (!dataType.Contains("VARCHAR"))
                {
                    throw new InvalidOperationException($"Cannot resize column of type {dataType}. Only VARCHAR/NVARCHAR columns can be resized.");
                }

                // Prevent shrinking
                if (newMaxLength > 0 && columnInfo.MaxLength.HasValue && newMaxLength <= columnInfo.MaxLength.Value)
                {
                    throw new InvalidOperationException($"New length ({newMaxLength}) must be greater than current length ({columnInfo.MaxLength.Value})");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string newSize = newMaxLength == -1 ? "MAX" : newMaxLength.ToString();

                    string alterQuery = $@"
                ALTER TABLE [ext].[{tableName}]
                ALTER COLUMN [{columnName}] {dataType}({newSize})";

                    using (var command = new SqlCommand(alterQuery, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✓ Column expanded successfully to {newSize} characters");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error expanding column: {ex.Message}");
                throw new Exception($"Failed to expand column {columnName} in {tableName}", ex);
            }
        }

    }
}