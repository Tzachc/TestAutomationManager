using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TestAutomationManager.Data;
using TestAutomationManager.Models;
using TestAutomationManager.Services;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for Test CRUD operations
    /// </summary>
    public class TestRepository : ITestRepository
    {
        // ================================================
        // READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get all tests WITHOUT processes/functions (optimized with stored procedure)
        /// Uses [dbo].[usp_GetAllTests] for 4-5x faster performance
        /// Use GetProcessesForTestAsync() and GetFunctionsForProcessAsync() for lazy loading
        /// </summary>
        public async Task<List<Test>> GetAllTestsAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    System.Diagnostics.Debug.WriteLine("⏳ Loading tests using stored procedure (FAST!)...");

                    // ✅ Use stored procedure for 4-5x faster performance
                    var tests = await context.Tests
                        .FromSqlRaw("EXEC [dbo].[usp_GetAllTests]")
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests via stored procedure");

                    // Initialize empty collections for UI binding
                    foreach (var test in tests)
                    {
                        test.Processes = new ObservableCollection<Process>();
                        test.AreProcessesLoaded = false;  // Mark as not loaded yet
                    }

                    var schemaName = SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests from schema '{schemaName}' using SP");
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
        /// Load processes for a specific test (optimized with stored procedure)
        /// Uses [dbo].[usp_GetProcessesByTestID] for faster performance
        /// </summary>
        public async Task<List<Process>> GetProcessesForTestAsync(int testId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading processes for Test #{testId} via stored procedure...");

                    // ✅ Use stored procedure with parameter
                    var testIdParam = new SqlParameter("@TestID", testId);

                    var processes = await context.Set<Process>()
                        .FromSqlRaw("EXEC [dbo].[usp_GetProcessesByTestID] @TestID", testIdParam)
                        .ToListAsync();

                    // Initialize empty functions collection for each process
                    foreach (var process in processes)
                    {
                        process.Functions = new ObservableCollection<Function>();
                        process.AreFunctionsLoaded = false;
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes for Test #{testId} via SP");
                    return processes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading processes for test: {ex.Message}");
                throw new Exception("Failed to load processes", ex);
            }
        }

        /// <summary>
        /// Load functions for a specific process (optimized with stored procedure)
        /// Uses [dbo].[usp_GetFunctionsByProcessID] for faster performance
        /// </summary>
        public async Task<List<Function>> GetFunctionsForProcessAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading functions for Process #{processId} via stored procedure...");

                    // ✅ Use stored procedure with parameter
                    var processIdParam = new SqlParameter("@ProcessID", processId);

                    var functions = await context.Set<Function>()
                        .FromSqlRaw("EXEC [dbo].[usp_GetFunctionsByProcessID] @ProcessID", processIdParam)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {functions.Count} functions for Process #{processId} via SP");
                    return functions;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading functions for process: {ex.Message}");
                throw new Exception("Failed to load functions", ex);
            }
        }

        /// <summary>
        /// Get test by ID (with full eager loading for backward compatibility)
        /// </summary>
        public async Task<Test> GetTestByIdAsync(int testId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var test = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .FirstOrDefaultAsync(t => t.TestID == testId);

                    return test;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting test by ID: {ex.Message}");
                throw new Exception("Failed to get test", ex);
            }
        }

        /// <summary>
        /// Get all external tables metadata
        /// </summary>
        public async Task<List<ExternalTableInfo>> GetAllExternalTablesAsync()
        {
            try
            {
                var externalRepo = new ExternalTableRepository();
                var tables = await externalRepo.GetAllExternalTablesAsync();

                // Enrich with test names
                using (var context = new TestAutomationDbContext())
                {
                    foreach (var table in tables)
                    {
                        var test = await context.Tests
                            .FirstOrDefaultAsync(t => t.TestID == table.TestId);

                        if (test != null)
                        {
                            table.TestName = test.Name;
                            table.Category = test.Category;
                        }
                        else
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

        // ================================================
        // CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert new test into database
        /// </summary>
        public async Task InsertTestAsync(Test test)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    bool exists = await context.Tests.AnyAsync(t => t.TestID == test.TestID);
                    if (exists)
                        throw new InvalidOperationException($"Test with ID {test.TestID} already exists");

                    await context.Database.OpenConnectionAsync();
                    await using var transaction = await context.Database.BeginTransactionAsync();

                    var schemaConfig = TestAutomationManager.Services.SchemaConfigService.Instance;
                    string fullTableName = schemaConfig.GetFullTableName(schemaConfig.TestTableName);

                    //await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {fullTableName} ON");
                    context.Tests.Add(test);
                    await context.SaveChangesAsync();
                   // await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {fullTableName} OFF");

                    await transaction.CommitAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Test #{test.TestID} '{test.Name}' inserted successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error inserting test: {ex.Message}");
                throw new Exception("Failed to insert test", ex);
            }
        }


        // ================================================
        // UPDATE OPERATIONS
        // ================================================

        /// <summary>
        /// Update an existing test
        /// </summary>
        public async Task UpdateTestAsync(Test test)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existingTest = await context.Tests.FindAsync(test.TestID);

                    if (existingTest == null)
                    {
                        throw new InvalidOperationException($"Test with ID {test.TestID} not found");
                    }

                    // Update properties from real schema
                    existingTest.TestName = test.TestName;
                    existingTest.Bugs = test.Bugs;
                    existingTest.DisableKillDriver = test.DisableKillDriver;
                    existingTest.EmailOnFailureOnly = test.EmailOnFailureOnly;
                    existingTest.ExceptionMessage = test.ExceptionMessage;
                    existingTest.ExitTestOnFailure = test.ExitTestOnFailure;
                    existingTest.LastRunning = test.LastRunning;
                    existingTest.LastTimePass = test.LastTimePass;
                    existingTest.RecipientsEmailsList = test.RecipientsEmailsList;
                    existingTest.RunStatus = test.RunStatus;
                    existingTest.SendEmailReport = test.SendEmailReport;
                    existingTest.SnapshotMultipleFailure = test.SnapshotMultipleFailure;
                    existingTest.TestRunAgainTimes = test.TestRunAgainTimes;

                    await context.SaveChangesAsync();

                    // Update UI-only settings separately
              //      if (test.TestID.HasValue)
               //     {
               //         await TestAutomationManager.Services.TestUISettingsService.Instance.SetIsActiveAsync((int)test.TestID.Value, test.IsActive);
                //        await TestAutomationManager.Services.TestUISettingsService.Instance.SetCategoryAsync((int)test.TestID.Value, test.Category);
               //     }

                    System.Diagnostics.Debug.WriteLine($"✓ Test #{test.TestID} updated successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating test: {ex.Message}");
                throw new Exception("Failed to update test", ex);
            }
        }

        // ================================================
        // DELETE OPERATIONS
        // ================================================

        /// <summary>
        /// Delete test from database (cascades to processes and functions)
        /// </summary>
        public async Task DeleteTestAsync(int testId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var test = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .FirstOrDefaultAsync(t => t.TestID == testId);

                    if (test == null)
                    {
                        throw new InvalidOperationException($"Test with ID {testId} not found");
                    }

                    int processCount = test.Processes?.Count ?? 0;
                    int functionCount = test.Processes?.Sum(p => p.Functions?.Count ?? 0) ?? 0;

                    // Remove test (cascade delete will handle processes and functions)
                    context.Tests.Remove(test);
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Test #{testId} deleted successfully (including {processCount} processes and {functionCount} functions)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting test: {ex.Message}");
                throw new Exception("Failed to delete test", ex);
            }
        }

        // ================================================
        // SMART ID OPERATIONS
        // ================================================

        /// <summary>
        /// Get next available test ID (finds gaps in sequence)
        /// For example: if IDs are 1,2,4,5 -> returns 3
        /// If no gaps, returns max + 1
        /// </summary>
        public async Task<int?> GetNextAvailableTestIdAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existingIds = await context.Tests
                        .Where(t => t.TestID.HasValue)
                        .OrderBy(t => t.TestID)
                        .Select(t => (int)t.TestID.Value)
                        .ToListAsync();

                    if (existingIds.Count == 0)
                    {
                        // No tests exist, start with ID 1
                        System.Diagnostics.Debug.WriteLine("✓ No tests exist, suggesting ID: 1");
                        return 1;
                    }

                    // Find first gap in sequence
                    for (int i = 0; i < existingIds.Count; i++)
                    {
                        int expectedId = i + 1;
                        if (existingIds[i] != expectedId)
                        {
                            // Found a gap!
                            System.Diagnostics.Debug.WriteLine($"✓ Found gap in sequence, suggesting ID: {expectedId}");
                            return expectedId;
                        }
                    }

                    // No gaps found, return max + 1
                    int nextId = existingIds.Max() + 1;
                    System.Diagnostics.Debug.WriteLine($"✓ No gaps found, suggesting ID: {nextId}");
                    return nextId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting next available test ID: {ex.Message}");
                throw new Exception("Failed to get next available test ID", ex);
            }
        }

        /// <summary>
        /// Get total process count across all tests (optimized for statistics)
        /// </summary>
        public async Task<int> GetTotalProcessCountAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    int count = await context.Set<Process>().CountAsync();
                    return count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting process count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get total function count across all tests (optimized for statistics)
        /// </summary>
        public async Task<int> GetTotalFunctionCountAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    int count = await context.Set<Function>().CountAsync();
                    return count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting function count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Check if test ID already exists
        /// </summary>
        public async Task<bool> TestIdExistsAsync(int testId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    bool exists = await context.Tests.AnyAsync(t => t.TestID == testId);
                    System.Diagnostics.Debug.WriteLine($"✓ Test ID {testId} exists: {exists}");
                    return exists;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking if test ID exists: {ex.Message}");
                throw new Exception("Failed to check test ID", ex);
            }
        }

        /// <summary>
        /// Get specific tests by their IDs (for differential updates in multi-user collaboration)
        /// Only loads the specified tests - much faster than loading all 700!
        /// </summary>
        public async Task<List<Test>> GetTestsByIdsAsync(List<int> testIds)
        {
            try
            {
                if (testIds == null || testIds.Count == 0)
                    return new List<Test>();

                using (var context = new TestAutomationDbContext())
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading {testIds.Count} specific tests...");

                    // ✅ Load ONLY the specified tests (not all 700!)
                    var tests = await context.Tests
                        .Where(t => t.TestID.HasValue && testIds.Contains((int)t.TestID.Value))
                        .OrderBy(t => t.TestID)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests (differential update)");

                    // ✅ Apply UI-only settings
                    var uiSettingsService = TestUISettingsService.Instance;
                    foreach (var test in tests)
                    {
                        if (test.TestID.HasValue)
                        {
                            test.IsActive = uiSettingsService.GetIsActive((int)test.TestID.Value);
                            test.Category = uiSettingsService.GetCategory((int)test.TestID.Value);
                        }

                        // Initialize empty collections
                        test.Processes = new ObservableCollection<Process>();
                        test.AreProcessesLoaded = false;
                    }

                    return tests;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading specific tests: {ex.Message}");
                throw new Exception("Failed to load tests by IDs", ex);
            }
        }
    }
}