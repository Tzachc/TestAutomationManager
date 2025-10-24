using System;
using System.Configuration;

namespace TestAutomationManager.Data
{
    /// <summary>
    /// Manages database connection configuration
    /// </summary>
    public static class DbConnectionConfig
    {
        // ================================================
        // Connection String Configuration
        // ================================================

        /// <summary>
        /// Default connection string for AutomationDB
        /// Change this when deploying to production
        /// </summary>
        private const string DEFAULT_CONNECTION_STRING =
            @"Server=(localdb)\Local;Database=AutomationDB;Integrated Security=true;TrustServerCertificate=true;";

        /// <summary>
        /// Get the connection string
        /// First tries to read from App.config, then falls back to default
        /// </summary>
        /// <returns>SQL Server connection string</returns>
        public static string GetConnectionString()
        {
            try
            {
                // Try to read from App.config first
                string configConnectionString = ConfigurationManager.ConnectionStrings["TestAutomationDB"]?.ConnectionString;

                if (!string.IsNullOrEmpty(configConnectionString))
                {
                    System.Diagnostics.Debug.WriteLine("✓ Using connection string from App.config");
                    return configConnectionString;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ Could not read from App.config: {ex.Message}");
            }

            // Use default LocalDB connection string
            System.Diagnostics.Debug.WriteLine("✓ Using default LocalDB connection string");
            return DEFAULT_CONNECTION_STRING;
        }

        /// <summary>
        /// Update connection string for production environment
        /// Call this method to set a different connection string at runtime
        /// </summary>
        /// <param name="server">Server name or IP</param>
        /// <param name="database">Database name</param>
        /// <param name="username">SQL username (optional for Windows Auth)</param>
        /// <param name="password">SQL password (optional for Windows Auth)</param>
        /// <param name="useWindowsAuth">Use Windows Authentication (default: true)</param>
        /// <returns>Formatted connection string</returns>
        public static string BuildConnectionString(
            string server,
            string database,
            string username = null,
            string password = null,
            bool useWindowsAuth = true)
        {
            if (useWindowsAuth)
            {
                // Windows Authentication
                return $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true;";
            }
            else
            {
                // SQL Server Authentication
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    throw new ArgumentException("Username and password required for SQL authentication");
                }

                return $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=true;";
            }
        }

        /// <summary>
        /// Test database connection
        /// </summary>
        /// <returns>True if connection successful</returns>
        public static bool TestConnection()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Try to open connection
                    bool canConnect = context.Database.CanConnect();

                    if (canConnect)
                    {
                        System.Diagnostics.Debug.WriteLine("✓ Database connection successful!");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("✗ Database connection failed!");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Database connection error: {ex.Message}");
                return false;
            }
        }
    }
}