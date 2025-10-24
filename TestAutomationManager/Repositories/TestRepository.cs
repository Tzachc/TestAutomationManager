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
    /// Repository for Test CRUD operations
    /// </summary>
    public class TestRepository : ITestRepository
    {
        // ================================================
        // READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get all tests with their processes and functions from the current schema
        /// Loads data from [PRODUCTION_Selenium].[Test_WEB3], [Process_WEB3], [Function_WEB3]
        /// </summary>
        public async Task<List<Test>> GetAllTestsAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var tests = await context.Tests
                        .Include(t => t.Processes)
                            .ThenInclude(p => p.Functions)
                        .OrderBy(t => t.TestID)
                        .ToListAsync();

                    // Load UI-only settings (IsActive, Category) from settings service
                    var uiSettingsService = TestAutomationManager.Services.TestUISettingsService.Instance;
                    foreach (var test in tests)
                    {
                        if (test.TestID.HasValue)
                        {
                            test.IsActive = uiSettingsService.GetIsActive((int)test.TestID.Value);
                            test.Category = uiSettingsService.GetCategory((int)test.TestID.Value);
                        }
                    }

                    var schemaName = TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {tests.Count} tests from schema '{schemaName}'");
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
        /// Get test by ID
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

                    await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {fullTableName} ON");
                    context.Tests.Add(test);
                    await context.SaveChangesAsync();
                    await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {fullTableName} OFF");

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
                    if (test.TestID.HasValue)
                    {
                        await TestAutomationManager.Services.TestUISettingsService.Instance.SetIsActiveAsync((int)test.TestID.Value, test.IsActive);
                        await TestAutomationManager.Services.TestUISettingsService.Instance.SetCategoryAsync((int)test.TestID.Value, test.Category);
                    }

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
                        .OrderBy(t => t.TestID)
                        .Select(t => (int)t.TestID)
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
    }
}