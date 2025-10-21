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

                    // Use transaction for atomicity
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Step 1: Create the table structure (copy schema)
                            string createTableQuery = $@"
                                -- Create new table with same structure as source
                                SELECT TOP 0 * 
                                INTO [ext].[{newTableName}]
                                FROM [ext].[{sourceTableName}]";

                            using (var command = new SqlCommand(createTableQuery, connection, transaction))
                            {
                                await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"✓ Created table structure for {newTableName}");
                            }

                            // Step 2: Add identity column if needed (for Id column)
                            string addIdentityQuery = $@"
                                -- Drop existing Id column if it exists
                                IF EXISTS (SELECT * FROM sys.columns 
                                          WHERE object_id = OBJECT_ID('[ext].[{newTableName}]') 
                                          AND name = 'Id')
                                BEGIN
                                    ALTER TABLE [ext].[{newTableName}] DROP COLUMN [Id]
                                END

                                -- Add Id column with IDENTITY
                                ALTER TABLE [ext].[{newTableName}] 
                                ADD [Id] INT IDENTITY(1,1) PRIMARY KEY";

                            using (var command = new SqlCommand(addIdentityQuery, connection, transaction))
                            {
                                await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"✓ Added identity column to {newTableName}");
                            }

                            // Step 3: Copy data from source table (excluding Id column)
                            string copyDataQuery = $@"
                                INSERT INTO [ext].[{newTableName}] 
                                (IterationName, Run, ExcludeProcess, LastTimePassed, 
                                 Exception, Image, RegistrationURL2, Username3, 
                                 Password4, ConnStr75, SqlQueryResult)
                                SELECT 
                                    IterationName, Run, ExcludeProcess, LastTimePassed, 
                                    Exception, Image, RegistrationURL2, Username3, 
                                    Password4, ConnStr75, SqlQueryResult
                                FROM [ext].[{sourceTableName}]";

                            using (var command = new SqlCommand(copyDataQuery, connection, transaction))
                            {
                                int rowsCopied = await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"✓ Copied {rowsCopied} rows from {sourceTableName} to {newTableName}");
                            }

                            // Commit transaction
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"✅ Successfully created {newTableName} from {sourceTableName}");
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

                    string dropQuery = $"DROP TABLE [ext].[{tableName}]";

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

                    string query = $"SELECT COUNT(*) FROM [ext].[{tableName}]";

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