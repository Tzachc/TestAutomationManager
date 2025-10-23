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
    }
}