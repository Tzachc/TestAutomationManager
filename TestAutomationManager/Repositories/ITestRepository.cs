using System.Collections.Generic;
using System.Threading.Tasks;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository interface for Test operations
    /// </summary>
    public interface ITestRepository
    {
        // ================================================
        // EXISTING METHODS (keep your current ones)
        // ================================================

        /// <summary>
        /// Get all tests with their processes and functions
        /// </summary>
        Task<List<Test>> GetAllTestsAsync();

        /// <summary>
        /// Update an existing test
        /// </summary>
        Task UpdateTestAsync(Test test);

        /// <summary>
        /// Get all external tables metadata
        /// </summary>
        Task<List<ExternalTableInfo>> GetAllExternalTablesAsync();

        // ================================================
        // NEW METHODS FOR ADD/DELETE FUNCTIONALITY
        // ================================================

        /// <summary>
        /// Get next available test ID (finds gaps in sequence)
        /// </summary>
        Task<int?> GetNextAvailableTestIdAsync();

        /// <summary>
        /// Check if test ID already exists
        /// </summary>
        Task<bool> TestIdExistsAsync(int testId);

        /// <summary>
        /// Insert new test into database
        /// </summary>
        Task InsertTestAsync(Test test);

        /// <summary>
        /// Delete test from database (cascades to processes and functions)
        /// </summary>
        Task DeleteTestAsync(int testId);

        /// <summary>
        /// Get test by ID
        /// </summary>
        Task<Test> GetTestByIdAsync(int testId);

        // ================================================
        // LAZY LOADING METHODS (Performance Optimization)
        // ================================================

        /// <summary>
        /// Load processes for a specific test (lazy loading optimization)
        /// </summary>
        Task<List<Process>> GetProcessesForTestAsync(int testId);

        /// <summary>
        /// Load functions for a specific process (lazy loading optimization)
        /// </summary>
        Task<List<Function>> GetFunctionsForProcessAsync(double processId);

        /// <summary>
        /// Get total process count across all tests (optimized for statistics)
        /// </summary>
        Task<int> GetTotalProcessCountAsync();

        /// <summary>
        /// Get total function count across all tests (optimized for statistics)
        /// </summary>
        Task<int> GetTotalFunctionCountAsync();

        // ================================================
        // DIFFERENTIAL UPDATE METHODS (Multi-user collaboration)
        // ================================================

        /// <summary>
        /// Get specific tests by their IDs (for incremental updates)
        /// </summary>
        Task<List<Test>> GetTestsByIdsAsync(List<int> testIds);
    }
}