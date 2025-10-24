using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestAutomationManager.Data;
using TestAutomationManager.Data.Schema;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for Test CRUD operations backed by dynamic schema metadata.
    /// </summary>
    public class TestRepository : ITestRepository, IDisposable
    {
        private readonly string _connectionString;
        private readonly SchemaMetadataResolver _metadataResolver;
        private bool _disposed;

        public TestRepository()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
            _metadataResolver = new SchemaMetadataResolver();
        }

        #region Read Operations

        public async Task<List<Test>> GetAllTestsAsync()
        {
            try
            {
                var schema = SchemaManager.Current;
                var testColumns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                var processColumns = await _metadataResolver.GetResolvedColumnsAsync("processes").ConfigureAwait(false);
                var functionColumns = await _metadataResolver.GetResolvedColumnsAsync("functions").ConfigureAwait(false);

                EnsureColumnExists(testColumns, "Id", "tests");

                var tests = await LoadTestsAsync(schema, testColumns, null).ConfigureAwait(false);
                var processes = await LoadProcessesAsync(schema, processColumns, null).ConfigureAwait(false);
                var functions = await LoadFunctionsAsync(schema, functionColumns, null).ConfigureAwait(false);

                var processById = processes.ToDictionary(p => p.Id);
                var processesByTestId = processes
                    .GroupBy(p => p.TestId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var function in functions)
                {
                    if (processById.TryGetValue(function.ProcessId, out var parentProcess))
                    {
                        parentProcess.Functions.Add(function);
                    }
                }

                foreach (var test in tests)
                {
                    if (processesByTestId.TryGetValue(test.Id, out var relatedProcesses))
                    {
                        foreach (var process in relatedProcesses.OrderBy(p => p.Sequence))
                        {
                            test.Processes.Add(process);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests from schema '{schema.Name}'");
                return tests.OrderBy(t => t.Id).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading tests: {ex.Message}");
                throw new Exception("Failed to load tests from database", ex);
            }
        }

        public async Task<Test?> GetTestByIdAsync(int testId)
        {
            try
            {
                var schema = SchemaManager.Current;
                var testColumns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                var processColumns = await _metadataResolver.GetResolvedColumnsAsync("processes").ConfigureAwait(false);
                var functionColumns = await _metadataResolver.GetResolvedColumnsAsync("functions").ConfigureAwait(false);

                EnsureColumnExists(testColumns, "Id", "tests");

                var tests = await LoadTestsAsync(schema, testColumns, new[] { testId }).ConfigureAwait(false);
                var test = tests.FirstOrDefault();
                if (test == null)
                    return null;

                var processes = await LoadProcessesAsync(schema, processColumns, new[] { test.Id }).ConfigureAwait(false);
                var processIds = processes.Select(p => p.Id).ToList();
                var functions = await LoadFunctionsAsync(schema, functionColumns, processIds).ConfigureAwait(false);

                var processLookup = processes.ToDictionary(p => p.Id);
                foreach (var function in functions)
                {
                    if (processLookup.TryGetValue(function.ProcessId, out var parent))
                    {
                        parent.Functions.Add(function);
                    }
                }

                foreach (var process in processes.OrderBy(p => p.Sequence))
                {
                    test.Processes.Add(process);
                }

                return test;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting test by ID: {ex.Message}");
                throw new Exception("Failed to get test", ex);
            }
        }

        public async Task<List<ExternalTableInfo>> GetAllExternalTablesAsync()
        {
            try
            {
                var schema = SchemaManager.Current;
                var externalDefinition = schema.ExternalTables ?? new ExternalTablesDefinition();

                if (externalDefinition.Prefixes == null || externalDefinition.Prefixes.Count == 0)
                {   
                    return new List<ExternalTableInfo>();
                }

                var tables = new List<ExternalTableInfo>();
                string extSchema = string.IsNullOrWhiteSpace(externalDefinition.Schema)
                    ? schema.DatabaseSchema
                    : externalDefinition.Schema;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync().ConfigureAwait(false);

                    var prefixConditions = new List<string>();
                    var command = new SqlCommand();
                    command.Connection = connection;

                    for (int i = 0; i < externalDefinition.Prefixes.Count; i++)
                    {
                        string parameterName = $"@prefix{i}";
                        prefixConditions.Add($"TABLE_NAME LIKE {parameterName}");
                        command.Parameters.AddWithValue(parameterName, externalDefinition.Prefixes[i] + "%");
                    }

                    command.CommandText = $@"
                        SELECT TABLE_NAME
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_SCHEMA = @schema
                          AND ({string.Join(" OR ", prefixConditions)})
                        ORDER BY TABLE_NAME";

                    command.Parameters.AddWithValue("@schema", extSchema);

                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            string tableName = reader.GetString(0);
                            int testId = ExtractTrailingNumber(tableName);

                            tables.Add(new ExternalTableInfo
                            {
                                TableName = tableName,
                                TestId = testId,
                                RowCount = 0,
                                Category = "Test Data"
                            });
                        }
                    }
                }

                foreach (var table in tables)
                {
                    table.RowCount = await GetExternalTableRowCountAsync(extSchema, table.TableName).ConfigureAwait(false);
                }

                // Load names/categories for discovered tests
                var testIds = tables.Select(t => t.TestId).Where(id => id > 0).Distinct().ToList();
                if (testIds.Count > 0)
                {
                    var testColumns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                    var existingTests = await LoadTestsAsync(schema, testColumns, testIds).ConfigureAwait(false);
                    var lookup = existingTests.ToDictionary(t => t.Id);

                    foreach (var table in tables)
                    {
                        if (lookup.TryGetValue(table.TestId, out var test))
                        {
                            table.TestName = test.Name;
                            table.Category = test.Category;
                        }
                        else if (table.TestId > 0)
                        {
                            table.TestName = $"Test #{table.TestId} (Not Found)";
                            table.Category = "Unknown";
                        }
                    }
                }

                return tables;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading external tables: {ex.Message}");
                throw new Exception("Failed to load external tables", ex);
            }
        }

        #endregion

        #region Create / Update / Delete

        public async Task InsertTestAsync(Test test)
        {
            if (test == null) throw new ArgumentNullException(nameof(test));

            try
            {
                var schema = SchemaManager.Current;
                var columns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                EnsureColumnExists(columns, "Id", "tests");

                var insertColumns = new List<(string Column, object? Value)>();

                insertColumns.Add((columns["Id"], test.Id));

                if (columns.TryGetValue("Name", out var nameColumn))
                    insertColumns.Add((nameColumn, test.Name));
                if (columns.TryGetValue("Description", out var descColumn))
                    insertColumns.Add((descColumn, test.Description));
                if (columns.TryGetValue("Category", out var categoryColumn))
                    insertColumns.Add((categoryColumn, test.Category));
                if (columns.TryGetValue("IsActive", out var activeColumn))
                    insertColumns.Add((activeColumn, test.IsActive));
                if (columns.TryGetValue("Status", out var statusColumn))
                    insertColumns.Add((statusColumn, test.Status));
                if (columns.TryGetValue("LastRun", out var lastRunColumn))
                    insertColumns.Add((lastRunColumn, NormalizeDate(test.LastRun)));

                string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));
                var sb = new StringBuilder();
                sb.Append($"INSERT INTO {table} (");
                sb.Append(string.Join(", ", insertColumns.Select(c => $"[{c.Column}]")));
                sb.Append(") VALUES (");

                var parameters = new List<SqlParameter>();
                for (int i = 0; i < insertColumns.Count; i++)
                {
                    string paramName = $"@p{i}";
                    sb.Append(paramName);
                    if (i < insertColumns.Count - 1)
                        sb.Append(", ");

                    parameters.Add(new SqlParameter(paramName, insertColumns[i].Value ?? DBNull.Value));
                }

                sb.Append(");");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(sb.ToString(), connection);
                command.Parameters.AddRange(parameters.ToArray());
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine($"✓ Test #{test.Id} inserted into schema '{schema.Name}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error inserting test: {ex.Message}");
                throw new Exception("Failed to insert test", ex);
            }
        }

        public async Task UpdateTestAsync(Test test)
        {
            if (test == null) throw new ArgumentNullException(nameof(test));

            try
            {
                var schema = SchemaManager.Current;
                var columns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                EnsureColumnExists(columns, "Id", "tests");

                var updates = new List<(string Column, object? Value)>();

                if (columns.TryGetValue("Name", out var nameColumn))
                    updates.Add((nameColumn, test.Name));
                if (columns.TryGetValue("Description", out var descColumn))
                    updates.Add((descColumn, test.Description));
                if (columns.TryGetValue("Category", out var categoryColumn))
                    updates.Add((categoryColumn, test.Category));
                if (columns.TryGetValue("IsActive", out var activeColumn))
                    updates.Add((activeColumn, test.IsActive));
                if (columns.TryGetValue("Status", out var statusColumn))
                    updates.Add((statusColumn, test.Status));
                if (columns.TryGetValue("LastRun", out var lastRunColumn))
                    updates.Add((lastRunColumn, NormalizeDate(test.LastRun)));

                if (updates.Count == 0)
                    return; // Nothing to update

                string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));

                var sb = new StringBuilder();
                sb.Append($"UPDATE {table} SET ");

                var parameters = new List<SqlParameter>();
                for (int i = 0; i < updates.Count; i++)
                {
                    string paramName = $"@p{i}";
                    sb.Append($"[{updates[i].Column}] = {paramName}");
                    if (i < updates.Count - 1)
                        sb.Append(", ");

                    parameters.Add(new SqlParameter(paramName, updates[i].Value ?? DBNull.Value));
                }

                string idParam = "@testId";
                sb.Append($" WHERE [{columns["Id"]}] = {idParam};");
                parameters.Add(new SqlParameter(idParam, test.Id));

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = new SqlCommand(sb.ToString(), connection);
                command.Parameters.AddRange(parameters.ToArray());
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine($"✓ Test #{test.Id} updated in schema '{schema.Name}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating test: {ex.Message}");
                throw new Exception("Failed to update test", ex);
            }
        }

        public async Task DeleteTestAsync(int testId)
        {
            try
            {
                var schema = SchemaManager.Current;
                var testColumns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                var processColumns = await _metadataResolver.GetResolvedColumnsAsync("processes").ConfigureAwait(false);
                var functionColumns = await _metadataResolver.GetResolvedColumnsAsync("functions").ConfigureAwait(false);

                EnsureColumnExists(testColumns, "Id", "tests");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                using var transaction = connection.BeginTransaction();

                try
                {
                    if (processColumns.TryGetValue("Id", out var processIdColumn) &&
                        processColumns.TryGetValue("TestId", out var processTestIdColumn) &&
                        functionColumns.TryGetValue("ProcessId", out var functionProcessIdColumn))
                    {
                        string functionsTable = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("functions"));
                        string processesTable = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("processes"));

                        string deleteFunctionsSql = $@"
                            DELETE F
                            FROM {functionsTable} AS F
                            WHERE F.[{functionProcessIdColumn}] IN (
                                SELECT P.[{processIdColumn}] FROM {processesTable} AS P
                                WHERE P.[{processTestIdColumn}] = @testId)";

                        using (var deleteFunctions = new SqlCommand(deleteFunctionsSql, connection, transaction))
                        {
                            deleteFunctions.Parameters.AddWithValue("@testId", testId);
                            await deleteFunctions.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }

                    if (processColumns.TryGetValue("TestId", out var processTestColumn))
                    {
                        string processesTable = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("processes"));
                        string deleteProcessesSql = $"DELETE FROM {processesTable} WHERE [{processTestColumn}] = @testId";

                        using (var deleteProcesses = new SqlCommand(deleteProcessesSql, connection, transaction))
                        {
                            deleteProcesses.Parameters.AddWithValue("@testId", testId);
                            await deleteProcesses.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }

                    string testsTable = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));
                    string deleteTestSql = $"DELETE FROM {testsTable} WHERE [{testColumns["Id"]}] = @testId";

                    using (var deleteTest = new SqlCommand(deleteTestSql, connection, transaction))
                    {
                        deleteTest.Parameters.AddWithValue("@testId", testId);
                        await deleteTest.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    await transaction.CommitAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"✓ Test #{testId} deleted from schema '{schema.Name}'");
                }
                catch
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting test: {ex.Message}");
                throw new Exception("Failed to delete test", ex);
            }
        }

        #endregion

        #region Utility Operations

        public async Task<int?> GetNextAvailableTestIdAsync()
        {
            try
            {
                var schema = SchemaManager.Current;
                var columns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                EnsureColumnExists(columns, "Id", "tests");

                string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));
                string sql = $"SELECT [{columns["Id"]}] FROM {table} ORDER BY [{columns["Id"]}]";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                using var command = new SqlCommand(sql, connection);

                var ids = new List<int>();
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        ids.Add(Convert.ToInt32(reader.GetValue(0)));
                    }
                }

                if (ids.Count == 0)
                    return 1;

                for (int i = 0; i < ids.Count; i++)
                {
                    int expected = i + 1;
                    if (ids[i] != expected)
                        return expected;
                }

                return ids.Max() + 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting next available test ID: {ex.Message}");
                throw new Exception("Failed to get next available test ID", ex);
            }
        }

        public async Task<bool> TestIdExistsAsync(int testId)
        {
            try
            {
                var schema = SchemaManager.Current;
                var columns = await _metadataResolver.GetResolvedColumnsAsync("tests").ConfigureAwait(false);
                EnsureColumnExists(columns, "Id", "tests");

                string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));
                string sql = $"SELECT COUNT(1) FROM {table} WHERE [{columns["Id"]}] = @testId";

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@testId", testId);

                int count = Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
                return count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking if test ID exists: {ex.Message}");
                throw new Exception("Failed to check test ID", ex);
            }
        }

        #endregion

        #region Helper Methods

        private async Task<List<Test>> LoadTestsAsync(SchemaDefinition schema, Dictionary<string, string> columns, IEnumerable<int>? filterIds)
        {
            EnsureColumnExists(columns, "Id", "tests");

            string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("tests"));
            var selectParts = BuildSelectParts(columns, new[] { "Id", "Name", "Description", "Category", "IsActive", "Status", "LastRun" });

            if (selectParts.Count == 0)
                throw new InvalidOperationException("No columns available to select for tests.");

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("SELECT ");
            sqlBuilder.Append(string.Join(", ", selectParts));
            sqlBuilder.Append($" FROM {table}");

            var parameters = new List<SqlParameter>();

            if (filterIds != null)
            {
                var filterList = filterIds.Distinct().ToList();
                if (filterList.Count > 0)
                {
                    var inClause = BuildInClause(columns["Id"], "tid", filterList, out var clauseParameters);
                    sqlBuilder.Append($" WHERE {inClause}");
                    parameters.AddRange(clauseParameters);
                }
            }

            sqlBuilder.Append($" ORDER BY [{columns["Id"]}]");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            using var command = new SqlCommand(sqlBuilder.ToString(), connection);
            if (parameters.Count > 0)
                command.Parameters.AddRange(parameters.ToArray());

            var results = new List<Test>();

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var ordinals = BuildAliasLookup(reader);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var test = new Test
                    {
                        Processes = new ObservableCollection<Process>()
                    };

                    if (TryGetValue(reader, ordinals, "Id", out int id))
                        test.Id = id;
                    if (columns.ContainsKey("Name") && TryGetValue(reader, ordinals, "Name", out string name))
                        test.Name = name;
                    if (columns.ContainsKey("Description") && TryGetValue(reader, ordinals, "Description", out string description))
                        test.Description = description;
                    if (columns.ContainsKey("Category") && TryGetValue(reader, ordinals, "Category", out string category))
                        test.Category = category;
                    if (columns.ContainsKey("IsActive") && TryGetValue(reader, ordinals, "IsActive", out bool isActive))
                        test.IsActive = isActive;
                    if (columns.ContainsKey("Status") && TryGetValue(reader, ordinals, "Status", out string status))
                        test.Status = status;
                    if (columns.ContainsKey("LastRun") && TryGetValue(reader, ordinals, "LastRun", out DateTime lastRun))
                        test.LastRun = lastRun;

                    results.Add(test);
                }
            }

            return results;
        }

        private async Task<List<Process>> LoadProcessesAsync(SchemaDefinition schema, Dictionary<string, string> columns, IEnumerable<int>? testIds)
        {
            if (columns.Count == 0)
                return new List<Process>();

            EnsureColumnExists(columns, "Id", "processes");
            EnsureColumnExists(columns, "TestId", "processes");

            string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("processes"));
            var selectParts = BuildSelectParts(columns, new[] { "Id", "TestId", "Name", "Description", "Sequence", "IsCritical", "Timeout" });

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("SELECT ");
            sqlBuilder.Append(string.Join(", ", selectParts));
            sqlBuilder.Append($" FROM {table}");

            var parameters = new List<SqlParameter>();

            if (testIds != null)
            {
                var filterList = testIds.Distinct().ToList();
                if (filterList.Count > 0)
                {
                    var inClause = BuildInClause(columns["TestId"], "pid", filterList, out var clauseParameters);
                    sqlBuilder.Append($" WHERE {inClause}");
                    parameters.AddRange(clauseParameters);
                }
            }

            sqlBuilder.Append($" ORDER BY [{columns["TestId"]}], [{columns.GetValueOrDefault("Sequence", columns["Id"])}]");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var command = new SqlCommand(sqlBuilder.ToString(), connection);
            if (parameters.Count > 0)
                command.Parameters.AddRange(parameters.ToArray());

            var results = new List<Process>();

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var ordinals = BuildAliasLookup(reader);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var process = new Process
                    {
                        Functions = new ObservableCollection<Function>()
                    };

                    if (TryGetValue(reader, ordinals, "Id", out int id))
                        process.Id = id;
                    if (TryGetValue(reader, ordinals, "TestId", out int testIdValue))
                        process.TestId = testIdValue;
                    if (columns.ContainsKey("Name") && TryGetValue(reader, ordinals, "Name", out string name))
                        process.Name = name;
                    if (columns.ContainsKey("Description") && TryGetValue(reader, ordinals, "Description", out string description))
                        process.Description = description;
                    if (columns.ContainsKey("Sequence") && TryGetValue(reader, ordinals, "Sequence", out int sequence))
                        process.Sequence = sequence;
                    if (columns.ContainsKey("IsCritical") && TryGetValue(reader, ordinals, "IsCritical", out bool isCritical))
                        process.IsCritical = isCritical;
                    if (columns.ContainsKey("Timeout") && TryGetValue(reader, ordinals, "Timeout", out double timeout))
                        process.Timeout = timeout;

                    results.Add(process);
                }
            }

            return results;
        }

        private async Task<List<Function>> LoadFunctionsAsync(SchemaDefinition schema, Dictionary<string, string> columns, IEnumerable<int>? processIds)
        {
            if (columns.Count == 0)
                return new List<Function>();

            EnsureColumnExists(columns, "Id", "functions");
            EnsureColumnExists(columns, "ProcessId", "functions");

            var filterList = processIds?.Distinct().ToList();
            if (filterList != null && filterList.Count == 0)
                return new List<Function>();

            string table = BuildQualifiedTableName(schema.DatabaseSchema, schema.GetTableName("functions"));
            var selectParts = BuildSelectParts(columns, new[] { "Id", "ProcessId", "Name", "MethodName", "Parameters", "ExpectedResult", "Sequence" });

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append("SELECT ");
            sqlBuilder.Append(string.Join(", ", selectParts));
            sqlBuilder.Append($" FROM {table}");

            var parameters = new List<SqlParameter>();
            if (filterList != null)
            {
                var inClause = BuildInClause(columns["ProcessId"], "fid", filterList, out var clauseParameters);
                sqlBuilder.Append($" WHERE {inClause}");
                parameters.AddRange(clauseParameters);
            }

            sqlBuilder.Append($" ORDER BY [{columns["ProcessId"]}], [{columns.GetValueOrDefault("Sequence", columns["Id"])}]");

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var command = new SqlCommand(sqlBuilder.ToString(), connection);
            if (parameters.Count > 0)
                command.Parameters.AddRange(parameters.ToArray());

            var results = new List<Function>();

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var ordinals = BuildAliasLookup(reader);

                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var function = new Function();

                    if (TryGetValue(reader, ordinals, "Id", out int id))
                        function.Id = id;
                    if (TryGetValue(reader, ordinals, "ProcessId", out int processId))
                        function.ProcessId = processId;
                    if (columns.ContainsKey("Name") && TryGetValue(reader, ordinals, "Name", out string name))
                        function.Name = name;
                    if (columns.ContainsKey("MethodName") && TryGetValue(reader, ordinals, "MethodName", out string methodName))
                        function.MethodName = methodName;
                    if (columns.ContainsKey("Parameters") && TryGetValue(reader, ordinals, "Parameters", out string parametersValue))
                        function.Parameters = parametersValue;
                    if (columns.ContainsKey("ExpectedResult") && TryGetValue(reader, ordinals, "ExpectedResult", out string expected))
                        function.ExpectedResult = expected;
                    if (columns.ContainsKey("Sequence") && TryGetValue(reader, ordinals, "Sequence", out int sequence))
                        function.Sequence = sequence;

                    results.Add(function);
                }
            }

            return results;
        }

        private async Task<int> GetExternalTableRowCountAsync(string schemaName, string tableName)
        {
            string qualified = BuildQualifiedTableName(schemaName, tableName);
            string sql = $"SELECT COUNT(*) FROM {qualified}";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            using var command = new SqlCommand(sql, connection);

            object? result = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt32(result ?? 0);
        }

        private static void EnsureColumnExists(Dictionary<string, string> mapping, string property, string logicalTable)
        {
            if (!mapping.ContainsKey(property))
            {
                throw new InvalidOperationException(
                    $"Active schema '{SchemaManager.CurrentSchemaName}' does not define a column mapping for '{logicalTable}.{property}'.");
            }
        }

        private static List<string> BuildSelectParts(Dictionary<string, string> mapping, IEnumerable<string> properties)
        {
            var parts = new List<string>();
            foreach (var property in properties)
            {
                if (mapping.TryGetValue(property, out var column))
                {
                    parts.Add($"[{column}] AS [{property}]");
                }
            }
            return parts;
        }

        private static string BuildQualifiedTableName(string schema, string table)
        {
            string safeSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
            return $"[{safeSchema}].[{table}]";
        }

        private static Dictionary<string, int> BuildAliasLookup(SqlDataReader reader)
        {
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string name = reader.GetName(i);
                if (!lookup.ContainsKey(name))
                {
                    lookup[name] = i;
                }
            }

            return lookup;
        }

        private static bool TryGetValue<T>(SqlDataReader reader, Dictionary<string, int> ordinals, string alias, out T value)
        {
            value = default!;

            if (!ordinals.TryGetValue(alias, out int ordinal))
            {
                return false;
            }

            if (reader.IsDBNull(ordinal))
            {
                return false;
            }

            object rawValue = reader.GetValue(ordinal);
            value = (T)ConvertValue(rawValue, typeof(T));
            return true;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType == typeof(string))
            {
                return value?.ToString() ?? string.Empty;
            }

            if (targetType == typeof(int))
            {
                if (value is int intValue)
                    return intValue;
                if (value is long longValue)
                    return Convert.ToInt32(longValue);
                if (value is double doubleValue)
                    return Convert.ToInt32(doubleValue);
                if (value is decimal decimalValue)
                    return Convert.ToInt32(decimalValue);

                var stringValue = value?.ToString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    if (int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                        return parsedInt;
                    if (double.TryParse(stringValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedDouble))
                        return Convert.ToInt32(parsedDouble);
                }

                return 0;
            }

            if (targetType == typeof(long))
            {
                if (value is long longValue)
                    return longValue;
                if (value is int intValue)
                    return (long)intValue;
                if (value is double doubleValue)
                    return Convert.ToInt64(doubleValue);
                if (value is decimal decimalValue)
                    return Convert.ToInt64(decimalValue);

                var stringValue = value?.ToString();
                if (!string.IsNullOrWhiteSpace(stringValue) &&
                    long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                {
                    return parsedLong;
                }

                return 0L;
            }

            if (targetType == typeof(bool))
            {
                switch (value)
                {
                    case bool boolValue:
                        return boolValue;
                    case int intValue:
                        return intValue != 0;
                    case long longValue:
                        return longValue != 0;
                    case double doubleValue:
                        return Math.Abs(doubleValue) > double.Epsilon;
                    case decimal decimalValue:
                        return decimalValue != 0m;
                    case string stringValue:
                        {
                            string trimmed = stringValue.Trim();
                            if (bool.TryParse(trimmed, out var parsedBool))
                                return parsedBool;
                            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                                return parsedInt != 0;

                            string lower = trimmed.ToLowerInvariant();
                            if (lower is "y" or "yes" or "on" or "true" or "1")
                                return true;
                            if (lower is "n" or "no" or "off" or "false" or "0")
                                return false;

                            return false;
                        }
                }

                if (value is IConvertible convertible)
                {
                    try
                    {
                        return convertible.ToInt32(CultureInfo.InvariantCulture) != 0;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return false;
            }

            if (targetType == typeof(double))
            {
                if (value is double doubleValue)
                    return doubleValue;
                if (value is float floatValue)
                    return (double)floatValue;
                if (value is decimal decimalValue)
                    return (double)decimalValue;
                if (value is int intValue)
                    return (double)intValue;
                if (value is long longValue)
                    return (double)longValue;

                var stringValue = value?.ToString();
                if (!string.IsNullOrWhiteSpace(stringValue) &&
                    double.TryParse(stringValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedDouble))
                {
                    return parsedDouble;
                }

                return 0d;
            }

            if (targetType == typeof(float))
            {
                if (value is float floatValue)
                    return floatValue;
                var doubleValue = (double)ConvertValue(value, typeof(double));
                return (float)doubleValue;
            }

            if (targetType == typeof(decimal))
            {
                if (value is decimal decimalValue)
                    return decimalValue;
                var doubleValue = (double)ConvertValue(value, typeof(double));
                return Convert.ToDecimal(doubleValue);
            }

            if (targetType == typeof(DateTime))
            {
                if (value is DateTime dateTimeValue)
                    return dateTimeValue;

                if (value is string stringValue &&
                    DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
                {
                    return parsedDate;
                }

                if (value is double oaDate)
                {
                    try
                    {
                        return DateTime.FromOADate(oaDate);
                    }
                    catch
                    {
                        return DateTime.MinValue;
                    }
                }

                return DateTime.MinValue;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static string BuildInClause(string columnName, string parameterPrefix, IList<int> values, out List<SqlParameter> parameters)
        {
            parameters = new List<SqlParameter>();
            var parameterNames = new List<string>();

            for (int i = 0; i < values.Count; i++)
            {
                string parameterName = $"@{parameterPrefix}{i}";
                parameterNames.Add(parameterName);
                parameters.Add(new SqlParameter(parameterName, values[i]));
            }

            return $"[{columnName}] IN ({string.Join(", ", parameterNames)})";
        }

        private static object? NormalizeDate(DateTime value)
        {
            if (value == default || value == DateTime.MinValue)
                return DBNull.Value;
            return value;
        }

        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            int number = 0;
            int multiplier = 1;

            for (int i = value.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(value[i]))
                    break;

                number += (value[i] - '0') * multiplier;
                multiplier *= 10;
            }

            return number;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _metadataResolver.Dispose();
        }
    }
}
