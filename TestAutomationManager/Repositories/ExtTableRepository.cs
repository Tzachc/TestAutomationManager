using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using TestAutomationManager.Data;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository interface for ExtTable operations
    /// </summary>
    public interface IExtTableRepository
    {
        /// <summary>
        /// Check if an ExtTable exists
        /// </summary>
        Task<bool> ExtTableExistsAsync(string tableName);

        /// <summary>
        /// Create new ExtTable by copying structure and data from template
        /// </summary>
        Task CreateExtTableFromTemplateAsync(string newTableName, string sourceTableName);

        /// <summary>
        /// Delete an ExtTable
        /// </summary>
        Task DeleteExtTableAsync(string tableName);

        /// <summary>
        /// Get row count for an ExtTable
        /// </summary>
        Task<int> GetExtTableRowCountAsync(string tableName);
    }

    /// <summary>
    /// Implementation of ExtTable repository for SQL operations
    /// </summary>
    public class ExtTableRepository : IExtTableRepository
    {
        // ================================================
        // FIELDS
        // ================================================

        private readonly string _connectionString;

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// Get the current schema name from SchemaConfigService
        /// </summary>
        private string CurrentSchema => TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public ExtTableRepository()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
        }

        // ================================================
        // TABLE EXISTENCE CHECK
        // ================================================

        /// <summary>
        /// Check if an ExtTable exists in the database
        /// </summary>
        public async Task<bool> ExtTableExistsAsync(string tableName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_SCHEMA = 'ext'
                          AND TABLE_NAME = @tableName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        int count = (int)await command.ExecuteScalarAsync();

                        System.Diagnostics.Debug.WriteLine($"✓ Table '{tableName}' exists: {count > 0}");
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking if table exists: {ex.Message}");
                throw new Exception($"Failed to check if table {tableName} exists", ex);
            }
        }

        // ================================================
        // CREATE TABLE FROM TEMPLATE
        // ================================================

        /// <summary>
        /// Create new ExtTable by copying structure and data from template
        /// ⭐ FIXED: ID column is always first
        /// </summary>
        public async Task CreateExtTableFromTemplateAsync(string newTableName, string sourceTableName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📊 Creating {newTableName} from template {sourceTableName}...");

                // Validate input
                if (string.IsNullOrWhiteSpace(newTableName))
                    throw new ArgumentException("New table name cannot be empty");

                if (string.IsNullOrWhiteSpace(sourceTableName))
                    throw new ArgumentException("Source table name cannot be empty");

                // Check if new table already exists
                if (await ExtTableExistsAsync(newTableName))
                {
                    throw new InvalidOperationException($"Table {newTableName} already exists");
                }

                // Check if source table exists
                if (!await ExtTableExistsAsync(sourceTableName))
                {
                    throw new InvalidOperationException($"Source table {sourceTableName} does not exist");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // ⭐ STEP 1: Get column definitions from source table
                    var columnDefinitions = await GetTableColumnDefinitionsAsync(connection, sourceTableName);

                    if (columnDefinitions.Count == 0)
                    {
                        throw new InvalidOperationException($"Source table {sourceTableName} has no columns to copy");
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Found {columnDefinitions.Count} columns to copy");

                    // Use transaction for atomicity
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // ⭐ STEP 2: Create new table with proper column order (ID first)
                            string createTableQuery = BuildCreateTableQuery(newTableName, columnDefinitions);

                            using (var command = new SqlCommand(createTableQuery, connection, transaction))
                            {
                                await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"✓ Created table structure for {newTableName} with ID as first column");
                            }

                            // ⭐ STEP 3: Copy data (excluding Id column since it's auto-generated)
                            var dataColumns = columnDefinitions
                                .Where(c => !c.ColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase))
                                .Select(c => c.ColumnName)
                                .ToList();

                            if (dataColumns.Count > 0)
                            {
                                string columnList = string.Join(", ", dataColumns.Select(c => $"[{c}]"));

                                string copyDataQuery = $@"
                            INSERT INTO [{CurrentSchema}].[{newTableName}] 
                            ({columnList})
                            SELECT {columnList}
                            FROM [{CurrentSchema}].[{sourceTableName}]";

                                using (var command = new SqlCommand(copyDataQuery, connection, transaction))
                                {
                                    int rowsCopied = await command.ExecuteNonQueryAsync();
                                    System.Diagnostics.Debug.WriteLine($"✓ Copied {rowsCopied} rows from {sourceTableName} to {newTableName}");
                                }
                            }

                            // Commit transaction
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"✅ Successfully created {newTableName} from {sourceTableName} with ID as first column");
                        }
                        catch (Exception)
                        {
                            // Rollback on error
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error creating table from template: {ex.Message}");
                throw new Exception($"Failed to create {newTableName} from {sourceTableName}", ex);
            }
        }

        /// <summary>
        /// ⭐ NEW: Get detailed column definitions from a table
        /// </summary>
        private async Task<List<ColumnDefinition>> GetTableColumnDefinitionsAsync(SqlConnection connection, string tableName)
        {
            var columns = new List<ColumnDefinition>();

            try
            {
                string query = @"
            SELECT 
                COLUMN_NAME,
                DATA_TYPE,
                CHARACTER_MAXIMUM_LENGTH,
                IS_NULLABLE,
                ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'ext'
              AND TABLE_NAME = @tableName
            ORDER BY ORDINAL_POSITION";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@tableName", tableName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columns.Add(new ColumnDefinition
                            {
                                ColumnName = reader.GetString(0),
                                DataType = reader.GetString(1),
                                MaxLength = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                IsNullable = reader.GetString(3) == "YES"
                            });
                        }
                    }
                }

                return columns;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting column definitions for {tableName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ⭐ NEW: Build CREATE TABLE query with ID as first column
        /// </summary>
        private string BuildCreateTableQuery(string tableName, List<ColumnDefinition> columns)
        {
            var columnDefinitions = new List<string>();

            // ⭐ IMPORTANT: Add ID column FIRST with IDENTITY
            columnDefinitions.Add("[Id] INT IDENTITY(1,1) PRIMARY KEY");

            // Add all other columns (excluding Id if it exists in source)
            foreach (var col in columns.Where(c => !c.ColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase)))
            {
                string columnDef = $"[{col.ColumnName}] {GetSqlDataType(col)}";

                if (!col.IsNullable)
                {
                    columnDef += " NOT NULL";
                }
                else
                {
                    columnDef += " NULL";
                }

                columnDefinitions.Add(columnDef);
            }

            string createTableQuery = $@"
        CREATE TABLE [{CurrentSchema}].[{tableName}] (
            {string.Join(",\n            ", columnDefinitions)}
        )";

            return createTableQuery;
        }

        /// <summary>
        /// ⭐ NEW: Convert column definition to SQL data type string
        /// </summary>
        private string GetSqlDataType(ColumnDefinition column)
        {
            string dataType = column.DataType.ToUpperInvariant();

            // Handle types with length
            if (column.MaxLength.HasValue && (dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR"))
            {
                if (column.MaxLength.Value == -1)
                {
                    return $"{dataType}(MAX)";
                }
                return $"{dataType}({column.MaxLength.Value})";
            }

            // Return type as-is for other data types
            return dataType;
        }

        /// <summary>
        /// Helper class to store column definitions
        /// </summary>
        private class ColumnDefinition
        {
            public string ColumnName { get; set; }
            public string DataType { get; set; }
            public int? MaxLength { get; set; }
            public bool IsNullable { get; set; }
        }


        // ================================================
        // DELETE TABLE
        // ================================================

        /// <summary>
        /// Delete an ExtTable from the database
        /// </summary>
        public async Task DeleteExtTableAsync(string tableName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🗑️ Deleting table {tableName}...");

                // Check if table exists
                if (!await ExtTableExistsAsync(tableName))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Table {tableName} does not exist");
                    return;
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string dropQuery = $"DROP TABLE [{CurrentSchema}].[{tableName}]";

                    using (var command = new SqlCommand(dropQuery, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✓ Deleted table {tableName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting table: {ex.Message}");
                throw new Exception($"Failed to delete table {tableName}", ex);
            }
        }

        // ================================================
        // GET ROW COUNT
        // ================================================

        /// <summary>
        /// Get the number of rows in an ExtTable
        /// </summary>
        public async Task<int> GetExtTableRowCountAsync(string tableName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = $"SELECT COUNT(*) FROM [{CurrentSchema}].[{tableName}]";

                    using (var command = new SqlCommand(query, connection))
                    {
                        return (int)await command.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting row count: {ex.Message}");
                return 0;
            }
        }

    }
}