using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using TestAutomationManager.Data;

namespace TestAutomationManager.Data.Schema
{
    /// <summary>
    /// Resolves actual database column names for logical model properties using schema configuration.
    /// </summary>
    public class SchemaMetadataResolver : IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<string, Lazy<Task<Dictionary<string, string>>>> _columnCache = new();
        private bool _disposed;

        public SchemaMetadataResolver()
        {
            _connectionString = DbConnectionConfig.GetConnectionString();
            SchemaManager.SchemaChanged += OnSchemaChanged;
        }

        private void OnSchemaChanged(object? sender, SchemaDefinition e)
        {
            _columnCache.Clear();
        }

        /// <summary>
        /// Resolve the mapping between model properties and physical columns for the specified logical table.
        /// </summary>
        /// <param name="logicalTable">One of "tests", "processes" or "functions".</param>
        public Task<Dictionary<string, string>> GetResolvedColumnsAsync(string logicalTable)
        {
            if (string.IsNullOrWhiteSpace(logicalTable))
                throw new ArgumentException("Logical table name cannot be empty", nameof(logicalTable));

            string cacheKey = $"{SchemaManager.CurrentSchemaName}:{logicalTable.ToLowerInvariant()}";

            var lazyTask = _columnCache.GetOrAdd(cacheKey,
                _ => new Lazy<Task<Dictionary<string, string>>>(() => ResolveColumnsAsync(logicalTable)));

            return lazyTask.Value;
        }

        private async Task<Dictionary<string, string>> ResolveColumnsAsync(string logicalTable)
        {
            var schema = SchemaManager.Current;
            var candidates = schema.GetColumnCandidates(logicalTable);
            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (candidates.Count == 0)
            {
                return resolved;
            }

            string physicalTableName = schema.GetTableName(logicalTable);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var command = new SqlCommand(@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema
                  AND TABLE_NAME = @table", connection);

            command.Parameters.AddWithValue("@schema", schema.DatabaseSchema);
            command.Parameters.AddWithValue("@table", physicalTableName);

            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }

            foreach (var mapping in candidates)
            {
                string propertyName = mapping.Key;
                var options = mapping.Value ?? new List<string>();

                string? match = options.FirstOrDefault(existingColumns.Contains);
                if (!string.IsNullOrWhiteSpace(match))
                {
                    resolved[propertyName] = match;
                }
            }

            return resolved;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            SchemaManager.SchemaChanged -= OnSchemaChanged;
        }
    }
}
