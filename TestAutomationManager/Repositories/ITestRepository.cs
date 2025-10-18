using System.Collections.Generic;
using System.Threading.Tasks;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Interface for Test data access operations
    /// Defines contract for all test-related database operations
    /// </summary>
    public interface ITestRepository
    {
        // ================================================
        // READ OPERATIONS (Query)
        // ================================================

        /// <summary>
        /// Get all tests from database
        /// </summary>
        /// <returns>List of all tests</returns>
        Task<List<Test>> GetAllTestsAsync();

        /// <summary>
        /// Get test by ID with all related data (processes, functions)
        /// </summary>
        /// <param name="id">Test ID</param>
        /// <returns>Test with related data or null if not found</returns>
        Task<Test> GetTestByIdAsync(int id);

        /// <summary>
        /// Get tests by category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>List of tests in category</returns>
        Task<List<Test>> GetTestsByCategoryAsync(string category);

        /// <summary>
        /// Get active tests only
        /// </summary>
        /// <returns>List of active tests</returns>
        Task<List<Test>> GetActiveTestsAsync();

        // ================================================
        // WRITE OPERATIONS (Command)
        // ================================================

        /// <summary>
        /// Add new test to database
        /// </summary>
        /// <param name="test">Test to add</param>
        /// <returns>Added test with generated ID</returns>
        Task<Test> AddTestAsync(Test test);

        /// <summary>
        /// Update existing test
        /// </summary>
        /// <param name="test">Test with updated values</param>
        /// <returns>True if update successful</returns>
        Task<bool> UpdateTestAsync(Test test);

        /// <summary>
        /// Delete test by ID
        /// </summary>
        /// <param name="id">Test ID to delete</param>
        /// <returns>True if delete successful</returns>
        Task<bool> DeleteTestAsync(int id);

        // ================================================
        // STATISTICS OPERATIONS
        // ================================================

        /// <summary>
        /// Get count of tests by status
        /// </summary>
        /// <param name="status">Status name (Passed, Failed, Running, etc.)</param>
        /// <returns>Count of tests with status</returns>
        Task<int> GetTestCountByStatusAsync(string status);

        /// <summary>
        /// Get count of active tests
        /// </summary>
        /// <returns>Count of active tests</returns>
        Task<int> GetActiveTestCountAsync();
    }
}