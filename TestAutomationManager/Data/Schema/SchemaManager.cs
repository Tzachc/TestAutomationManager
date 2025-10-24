using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TestAutomationManager.Data.Schema
{
    /// <summary>
    /// Provides access to schema configuration and manages the active schema.
    /// </summary>
    public static class SchemaManager
    {
        private const string ConfigFileName = "schema-config.json";

        private static readonly object _lock = new();
        private static SchemaConfiguration? _configuration;
        private static SchemaDefinition? _currentSchema;
        private static string? _currentSchemaName;

        /// <summary>
        /// Event raised whenever the active schema changes.
        /// </summary>
        public static event EventHandler<SchemaDefinition>? SchemaChanged;

        /// <summary>
        /// Get the currently active schema definition.
        /// </summary>
        public static SchemaDefinition Current
        {
            get
            {
                EnsureConfigurationLoaded();
                return _currentSchema ?? throw new InvalidOperationException("Schema configuration failed to load.");
            }
        }

        /// <summary>
        /// Get the name of the currently selected schema.
        /// </summary>
        public static string CurrentSchemaName
        {
            get
            {
                EnsureConfigurationLoaded();
                return _currentSchemaName ?? string.Empty;
            }
        }

        /// <summary>
        /// Force reload of the configuration from disk. Primarily used for unit tests or tooling.
        /// </summary>
        public static void Reload()
        {
            lock (_lock)
            {
                _configuration = null;
                _currentSchema = null;
                _currentSchemaName = null;
            }

            EnsureConfigurationLoaded();
        }

        /// <summary>
        /// Retrieve a schema definition by name.
        /// </summary>
        public static SchemaDefinition? GetSchema(string schemaName)
        {
            EnsureConfigurationLoaded();

            if (string.IsNullOrWhiteSpace(schemaName))
                return null;

            return _configuration?.Schemas
                .FirstOrDefault(s => string.Equals(s.Name, schemaName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Change the active schema. If the schema does not exist the call is ignored.
        /// </summary>
        public static void SetCurrentSchema(string schemaName)
        {
            EnsureConfigurationLoaded();

            if (string.IsNullOrWhiteSpace(schemaName))
                return;

            var schema = GetSchema(schemaName);
            if (schema == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Schema '{schemaName}' was not found in configuration. Keeping '{CurrentSchemaName}'.");
                return;
            }

            lock (_lock)
            {
                _currentSchema = schema;
                _currentSchemaName = schema.Name;
            }

            SchemaChanged?.Invoke(null, schema);
        }

        /// <summary>
        /// Ensure configuration has been loaded from file.
        /// </summary>
        private static void EnsureConfigurationLoaded()
        {
            if (_configuration != null && _currentSchema != null)
                return;

            lock (_lock)
            {
                if (_configuration != null && _currentSchema != null)
                    return;

                var configuration = LoadConfiguration();

                if (configuration.Schemas == null || configuration.Schemas.Count == 0)
                {
                    throw new InvalidOperationException("Schema configuration does not contain any schema definitions.");
                }

                _configuration = configuration;

                // Determine the default schema name.
                string defaultSchemaName = string.IsNullOrWhiteSpace(configuration.DefaultSchema)
                    ? configuration.Schemas.First().Name
                    : configuration.DefaultSchema;

                var schema = configuration.Schemas
                    .FirstOrDefault(s => string.Equals(s.Name, defaultSchemaName, StringComparison.OrdinalIgnoreCase))
                    ?? configuration.Schemas.First();

                _currentSchema = schema;
                _currentSchemaName = schema.Name;
            }
        }

        /// <summary>
        /// Load the configuration file from disk.
        /// </summary>
        private static SchemaConfiguration LoadConfiguration()
        {
            string configPath = ResolveConfigurationPath();

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    $"Could not find schema configuration file at '{configPath}'.", configPath);
            }

            var json = File.ReadAllText(configPath);
            var configuration = JsonSerializer.Deserialize<SchemaConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (configuration == null)
            {
                throw new InvalidOperationException("Failed to deserialize schema configuration.");
            }

            return configuration;
        }

        /// <summary>
        /// Resolve the absolute path for the configuration file.
        /// </summary>
        private static string ResolveConfigurationPath()
        {
            // The file is copied to the application output folder. When running design-time tooling the base
            // directory may be different, so we search a couple of sensible locations.
            string baseDirectory = AppContext.BaseDirectory;
            string pathInBase = Path.Combine(baseDirectory, ConfigFileName);

            if (File.Exists(pathInBase))
                return pathInBase;

            string pathInRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? baseDirectory, ConfigFileName);
            if (File.Exists(pathInRoot))
                return pathInRoot;

            // As a final fallback look next to the executing assembly (useful for tests).
            string assemblyLocation = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => !a.IsDynamic && a.Location.Contains("TestAutomationManager"))?.Location ?? string.Empty;

            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation) ?? baseDirectory;
                string candidate = Path.Combine(assemblyDirectory, ConfigFileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Default to base directory path – the caller will receive a FileNotFoundException later.
            return pathInBase;
        }
    }
}
