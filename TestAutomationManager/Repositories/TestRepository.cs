using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TestAutomationManager.Data;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for Test data access operations
    /// Handles all database operations for Tests, Processes, and Functions
    /// </summary>
    public class TestRepository : ITestRepository
    {
        // ================================================
        // READ OPERATIONS (Query)
        // ================================================

        /// <summary>
        /// Get all tests from database with all related data
        /// </summary>
        public async Task<List<Test>> GetAllTestsAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Get all tests with their processes and functions
                    var tests = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .OrderBy(t => t.Id)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests from database");
                    return tests;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading tests: {ex.Message}");
                throw new Exception("Failed to load tests from database", ex);
            }
        }

        /// <summary>
        /// Get single test by ID with all related data
        /// </summary>
        public async Task<Test> GetTestByIdAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Get test with all related data
                    var test = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .FirstOrDefaultAsync(t => t.Id == id);

                    if (test != null)
                        System.Diagnostics.Debug.WriteLine($"✓ Loaded test ID {id}");
                    else
                        System.Diagnostics.Debug.WriteLine($"⚠ Test ID {id} not found");

                    return test;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading test {id}: {ex.Message}");
                throw new Exception($"Failed to load test {id} from database", ex);
            }
        }

        /// <summary>
        /// Get tests by category
        /// </summary>
        public async Task<List<Test>> GetTestsByCategoryAsync(string category)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var tests = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .Where(t => t.Category == category)
                        .OrderBy(t => t.Id)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests in category '{category}'");
                    return tests;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading tests by category: {ex.Message}");
                throw new Exception($"Failed to load tests for category '{category}'", ex);
            }
        }

        /// <summary>
        /// Get only active tests
        /// </summary>
        public async Task<List<Test>> GetActiveTestsAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var tests = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .Where(t => t.IsActive)
                        .OrderBy(t => t.Id)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} active tests");
                    return tests;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading active tests: {ex.Message}");
                throw new Exception("Failed to load active tests from database", ex);
            }
        }

        // ================================================
        // WRITE OPERATIONS (Command)
        // ================================================

        /// <summary>
        /// Add new test to database
        /// </summary>
        public async Task<Test> AddTestAsync(Test test)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Add test to context
                    context.Tests.Add(test);

                    // Save changes
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Added test: {test.Name} (ID: {test.Id})");
                    return test;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error adding test: {ex.Message}");
                throw new Exception("Failed to add test to database", ex);
            }
        }

        /// <summary>
        /// Update existing test
        /// </summary>
        public async Task<bool> UpdateTestAsync(Test test)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Find existing test
                    var existingTest = await context.Tests.FindAsync(test.Id);

                    if (existingTest == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Test ID {test.Id} not found for update");
                        return false;
                    }

                    // Update properties
                    existingTest.Name = test.Name;
                    existingTest.Description = test.Description;
                    existingTest.Category = test.Category;
                    existingTest.IsActive = test.IsActive;
                    existingTest.Status = test.Status;
                    existingTest.LastRun = test.LastRun;

                    // Save changes
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Updated test: {test.Name} (ID: {test.Id})");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating test: {ex.Message}");
                throw new Exception("Failed to update test in database", ex);
            }
        }

        /// <summary>
        /// Delete test by ID
        /// </summary>
        public async Task<bool> DeleteTestAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Find test
                    var test = await context.Tests.FindAsync(id);

                    if (test == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Test ID {id} not found for deletion");
                        return false;
                    }

                    // Remove test (cascade delete will handle processes and functions)
                    context.Tests.Remove(test);

                    // Save changes
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Deleted test ID {id}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting test: {ex.Message}");
                throw new Exception($"Failed to delete test {id} from database", ex);
            }
        }

        // ================================================
        // STATISTICS OPERATIONS
        // ================================================

        /// <summary>
        /// Get count of tests by status
        /// </summary>
        public async Task<int> GetTestCountByStatusAsync(string status)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    int count = await context.Tests
                        .Where(t => t.Status == status)
                        .CountAsync();

                    return count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting test count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get count of active tests
        /// </summary>
        public async Task<int> GetActiveTestCountAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    int count = await context.Tests
                        .Where(t => t.IsActive)
                        .CountAsync();

                    return count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting active test count: {ex.Message}");
                return 0;
            }
        }
    }
}