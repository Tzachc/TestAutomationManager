using System;
using System.Collections.Generic;

namespace TestAutomationManager.Data.Schema
{
    /// <summary>
    /// Represents the contents of the schema configuration file (schema-config.json).
    /// </summary>
    public class SchemaConfiguration
    {
        /// <summary>
        /// Name of the schema that should be used when the application starts.
        /// </summary>
        public string DefaultSchema { get; set; } = "Default";

        /// <summary>
        /// Collection of available schema definitions.
        /// </summary>
        public List<SchemaDefinition> Schemas { get; set; } = new();
    }

    /// <summary>
    /// Definition of a logical schema – includes table names, column mappings and metadata.
    /// </summary>
    public class SchemaDefinition
    {
        /// <summary>
        /// Friendly name for the schema (e.g., "PRODUCTION_Selenium").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Actual SQL schema name that hosts the core tables.
        /// </summary>
        public string DatabaseSchema { get; set; } = "dbo";

        /// <summary>
        /// Table names used by this schema.
        /// </summary>
        public TableNames Tables { get; set; } = new();

        /// <summary>
        /// Settings that describe where external data tables live.
        /// </summary>
        public ExternalTablesDefinition ExternalTables { get; set; } = new();

        /// <summary>
        /// Column mapping candidates organised by logical table (tests / processes / functions).
        /// Key = logical table, value = mapping of property name to candidate column names.
        /// </summary>
        public Dictionary<string, Dictionary<string, List<string>>> Columns { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Try to retrieve the physical table name for a logical table.
        /// </summary>
        public string GetTableName(string logicalTable)
        {
            if (string.IsNullOrWhiteSpace(logicalTable))
            {
                throw new ArgumentException("Logical table name cannot be empty", nameof(logicalTable));
            }

            return logicalTable.ToLowerInvariant() switch
            {
                "tests" => string.IsNullOrWhiteSpace(Tables.Tests) ? "Tests" : Tables.Tests,
                "processes" => string.IsNullOrWhiteSpace(Tables.Processes) ? "Processes" : Tables.Processes,
                "functions" => string.IsNullOrWhiteSpace(Tables.Functions) ? "Functions" : Tables.Functions,
                _ => throw new ArgumentOutOfRangeException(nameof(logicalTable),
                    $"Unknown logical table '{logicalTable}'.")
            };
        }

        /// <summary>
        /// Retrieve the candidate column mapping for a logical table, if available.
        /// </summary>
        public Dictionary<string, List<string>> GetColumnCandidates(string logicalTable)
        {
            if (Columns.TryGetValue(logicalTable, out var value))
            {
                return value;
            }

            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Container for table names within a schema definition.
    /// </summary>
    public class TableNames
    {
        public string Tests { get; set; } = "Tests";
        public string Processes { get; set; } = "Processes";
        public string Functions { get; set; } = "Functions";
    }

    /// <summary>
    /// Metadata describing the location and pattern of external data tables.
    /// </summary>
    public class ExternalTablesDefinition
    {
        /// <summary>
        /// SQL schema where the external tables reside.
        /// </summary>
        public string Schema { get; set; } = "ext";

        /// <summary>
        /// List of table name prefixes that should be treated as external data tables
        /// (e.g., "ExtTable" or "ExtTest").
        /// </summary>
        public List<string> Prefixes { get; set; } = new();
    }
}
