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
        /// Get list of all ExtTest tables that exist in database
        /// Scans current schema for tables matching ExtTest pattern
        /// </summary>
        public async Task<List<ExternalTableInfo>> GetAllExternalTablesAsync()
        {
            var tables = new List<ExternalTableInfo>();

            try
            {
                var schemaConfig = TestAutomationManager.Services.SchemaConfigService.Instance;
                string currentSchema = schemaConfig.CurrentSchema;
                string extTablePrefix = schemaConfig.ExtTestTablePrefix;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Query to find all ExtTest tables in current schema
                    string query = $@"
                        SELECT
                            t.name AS TableName,
                            -- Extract test ID from table name (ExtTest1 -> 1)
                            CAST(REPLACE(t.name, '{extTablePrefix}', '') AS INT) AS TestId
                        FROM sys.tables t
                        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                        WHERE s.name = @SchemaName
                          AND t.name LIKE @TablePattern
                        ORDER BY TestId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@SchemaName", currentSchema);
                        command.Parameters.AddWithValue("@TablePattern", $"{extTablePrefix}%");

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string tableName = reader.GetString(0);
                                int testId = reader.GetInt32(1);

                                // Get row count for this table
                                int rowCount = await GetRowCountAsync(tableName, currentSchema);

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

                System.Diagnostics.Debug.WriteLine($"✓ Discovered {tables.Count} external tables in schema '{currentSchema}'");
                return tables;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error discovering external tables: {ex.Message}");
                throw new Exception("Failed to discover external tables", ex);
            }
        }

        /// <summary>
        /// Get row count for a specific ExtTable in the given schema
        /// </summary>
        private async Task<int> GetRowCountAsync(string tableName, string schemaName)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = $"SELECT COUNT(*) FROM [{schemaName}].[{tableName}]";

                    using (var command = new SqlCommand(query, connection))
                    {
                        return (int)await command.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting row count for {schemaName}.{tableName}: {ex.Message}");
                return 0;
            }
        }
    }
}