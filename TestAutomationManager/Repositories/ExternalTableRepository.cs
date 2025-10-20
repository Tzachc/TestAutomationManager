using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using TestAutomationManager.Data;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for querying ACTUAL ExtTable tables (ext.ExtTable1, ext.ExtTable2, etc.)
    /// </summary>
    public class ExternalTableRepository
    {
        private readonly string _connectionString;

        public ExternalTableRepository()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
        }

        // ================================================
        // DISCOVER EXTERNAL TABLES
        // ================================================

        /// <summary>
        /// Get list of all ExtTable tables that exist in database
        /// Scans ext schema for tables matching ExtTable pattern
        /// </summary>
        public async Task<List<ExternalTableInfo>> GetAllExternalTablesAsync()
        {
            var tables = new List<ExternalTableInfo>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Query to find all ExtTable tables in ext schema
                    string query = @"
                        SELECT 
                            t.name AS TableName,
                            -- Extract test ID from table name (ExtTable1 -> 1)
                            CAST(REPLACE(t.name, 'ExtTable', '') AS INT) AS TestId
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE s.name = 'ext'
                          AND t.name LIKE 'ExtTable%'
                        ORDER BY TestId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string tableName = reader.GetString(0);
                                int testId = reader.GetInt32(1);

                                // Get row count for this table
                                int rowCount = await GetRowCountAsync(tableName);

                                tables.Add(new ExternalTableInfo
                                {
                                    TestId = testId,
                                    TableName = tableName,
                                    RowCount = rowCount,
                                    Category = "Test Data"
                                });
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Discovered {tables.Count} external tables");
                return tables;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error discovering external tables: {ex.Message}");
                throw new Exception("Failed to discover external tables", ex);
            }
        }

        /// <summary>
        /// Get row count for a specific ExtTable
        /// </summary>
        private async Task<int> GetRowCountAsync(string tableName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = $"SELECT COUNT(*) FROM ext.{tableName}";

                    using (var command = new SqlCommand(query, connection))
                    {
                        return (int)await command.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting row count for {tableName}: {ex.Message}");
                return 0;
            }
        }

        // ================================================
        // READ DATA FROM EXTERNAL TABLES
        // ================================================

        /// <summary>
        /// Get all rows from a specific ExtTable
        /// </summary>
        public async Task<List<ExternalTableRow>> GetTableDataAsync(string tableName)
        {
            var rows = new List<ExternalTableRow>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = $@"
                        SELECT 
                            Id, IterationName, Run, ExcludeProcess, LastTimePassed,
                            Exception, Image, RegistrationURL2, Username3, Password4,
                            ConnStr75, SqlQueryResult
                        FROM ext.{tableName}
                        ORDER BY Id";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                rows.Add(new ExternalTableRow
                                {
                                    Id = reader.GetInt32(0),
                                    IterationName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    Run = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    ExcludeProcess = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    LastTimePassed = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                    Exception = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Image = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    RegistrationURL2 = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Username3 = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    Password4 = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    ConnStr75 = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    SqlQueryResult = reader.IsDBNull(11) ? null : reader.GetString(11)
                                });
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {rows.Count} rows from {tableName}");
                return rows;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading data from {tableName}: {ex.Message}");
                throw new Exception($"Failed to load data from {tableName}", ex);
            }
        }

        // ================================================
        // SEARCH
        // ================================================

        /// <summary>
        /// Search external tables by test name or table name
        /// </summary>
        public async Task<List<ExternalTableInfo>> SearchTablesAsync(string searchQuery, List<Test> allTests)
        {
            var allTables = await GetAllExternalTablesAsync();

            // Apply search filter
            searchQuery = searchQuery.ToLowerInvariant();

            return allTables.Where(table =>
            {
                // Search by table name
                if (table.TableName.ToLowerInvariant().Contains(searchQuery))
                    return true;

                // Search by test name
                var test = allTests.FirstOrDefault(t => t.Id == table.TestId);
                if (test != null && test.Name.ToLowerInvariant().Contains(searchQuery))
                    return true;

                return false;
            }).ToList();
        }
        /// <summary>
        /// Create new ExtTable by copying structure from existing ExtTable
        /// </summary>
        public async Task CreateExtTableFromTemplateAsync(int newTestId, string sourceTableName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string newTableName = $"ExtTable{newTestId}";

                    // Check if table already exists
                    string checkQuery = @"
                SELECT COUNT(*) 
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'ext' AND t.name = @TableName";

                    using (var checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@TableName", newTableName);
                        int exists = (int)await checkCmd.ExecuteScalarAsync();

                        if (exists > 0)
                        {
                            throw new InvalidOperationException($"Table ext.{newTableName} already exists");
                        }
                    }

                    // Create new table by copying structure from source table
                    string createTableQuery = $@"
                -- Create new ExtTable with same structure as source
                SELECT TOP 0 *
                INTO ext.{newTableName}
                FROM ext.{sourceTableName}

                -- Add primary key constraint
                ALTER TABLE ext.{newTableName}
                ADD CONSTRAINT PK_{newTableName} PRIMARY KEY CLUSTERED (Id)

                -- Add identity to Id column
                -- Note: Cannot add identity to existing column, so we need to recreate it
                
                -- Step 1: Create temp table with identity
                CREATE TABLE ext.{newTableName}_temp (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    IterationName NVARCHAR(200),
                    Run NVARCHAR(50),
                    ExcludeProcess NVARCHAR(200),
                    LastTimePassed DATETIME,
                    Exception NVARCHAR(MAX),
                    Image NVARCHAR(500),
                    RegistrationURL2 NVARCHAR(500),
                    Username3 NVARCHAR(200),
                    Password4 NVARCHAR(200),
                    ConnStr75 NVARCHAR(500),
                    SqlQueryResult NVARCHAR(MAX)
                )

                -- Step 2: Drop the temp table we just created (structure copied)
                DROP TABLE ext.{newTableName}

                -- Step 3: Rename temp to actual name
                EXEC sp_rename 'ext.{newTableName}_temp', '{newTableName}'

                -- Add default constraint for Run column
                ALTER TABLE ext.{newTableName}
                ADD CONSTRAINT DF_{newTableName}_Run DEFAULT ('No') FOR Run
            ";

                    using (var createCmd = new SqlCommand(createTableQuery, connection))
                    {
                        createCmd.CommandTimeout = 60; // 60 seconds timeout
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Created ext.{newTableName} from {sourceTableName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error creating ExtTable: {ex.Message}");
                throw new Exception($"Failed to create ExtTable{newTestId}", ex);
            }
        }

        /// <summary>
        /// Drop/Delete an ExtTable
        /// </summary>
        public async Task DropExtTableAsync(int testId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string tableName = $"ExtTable{testId}";

                    // Check if table exists
                    string checkQuery = @"
                SELECT COUNT(*) 
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'ext' AND t.name = @TableName";

                    using (var checkCmd = new SqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@TableName", tableName);
                        int exists = (int)await checkCmd.ExecuteScalarAsync();

                        if (exists == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Table ext.{tableName} does not exist");
                            return; // Table doesn't exist, nothing to do
                        }
                    }

                    // Drop the table
                    string dropQuery = $"DROP TABLE ext.{tableName}";

                    using (var dropCmd = new SqlCommand(dropQuery, connection))
                    {
                        await dropCmd.ExecuteNonQueryAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Dropped ext.{tableName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error dropping ExtTable: {ex.Message}");
                throw new Exception($"Failed to drop ExtTable{testId}", ex);
            }
        }
    }
}