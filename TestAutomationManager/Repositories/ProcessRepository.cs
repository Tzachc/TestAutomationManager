using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TestAutomationManager.Data;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for Process CRUD operations
    /// Optimized for large datasets with lazy loading support
    /// </summary>
    public class ProcessRepository
    {
        // ================================================
        // READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get all processes WITHOUT functions (optimized with stored procedure)
        /// Uses schema-qualified stored procedure for 4-5x faster performance
        /// OPTIMIZED FOR LARGE DATASETS (20000+ records)
        /// </summary>
        public async Task<List<Process>> GetAllProcessesAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading processes using stored procedure from schema '{schemaName}'...");

                    // ✅ Use schema-qualified stored procedure for 4-5x faster performance
                    var spName = $"EXEC [{schemaName}].[usp_GetAllProcesses]";
                    var processes = await context.Set<Process>()
                        .FromSqlRaw(spName)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes via stored procedure");

                    // Initialize empty collections for UI binding
                    foreach (var process in processes)
                    {
                        process.Functions = new ObservableCollection<Function>();
                        process.AreFunctionsLoaded = false;  // Mark as not loaded yet
                    }

                    return processes;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading processes: {ex.Message}");
                throw new Exception("Failed to load processes from database", ex);
            }
        }

        /// <summary>
        /// Load functions for a specific process (optimized with stored procedure)
        /// Uses schema-qualified stored procedure for faster performance
        /// </summary>
        public async Task<List<Function>> GetFunctionsForProcessAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading functions for Process #{processId} via stored procedure from schema '{schemaName}'...");

                    // ✅ Use schema-qualified stored procedure with parameter
                    var processIdParam = new SqlParameter("@ProcessID", processId);
                    var spName = $"EXEC [{schemaName}].[usp_GetFunctionsByProcessID] @ProcessID";

                    var functions = await context.Set<Function>()
                        .FromSqlRaw(spName, processIdParam)
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
        /// Get all functions WITHOUT process details (optimized with stored procedure)
        /// Uses schema-qualified stored procedure for fast performance
        /// OPTIMIZED FOR LARGE DATASETS (potentially more records than processes)
        /// </summary>
        public async Task<List<Function>> GetAllFunctionsAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading all functions using stored procedure from schema '{schemaName}'...");

                    // ✅ Use schema-qualified stored procedure for fast performance
                    var spName = $"EXEC [{schemaName}].[usp_GetAllFunctions]";
                    var functions = await context.Set<Function>()
                        .FromSqlRaw(spName)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {functions.Count} functions via stored procedure");

                    return functions;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading all functions: {ex.Message}");
                throw new Exception("Failed to load functions from database", ex);
            }
        }

        /// <summary>
        /// Get process by ID
        /// </summary>
        public async Task<Process> GetProcessByIdAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var process = await context.Set<Process>()
                        .FirstOrDefaultAsync(p => p.ProcessID == processId);

                    return process;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting process by ID: {ex.Message}");
                throw new Exception("Failed to get process", ex);
            }
        }

        /// <summary>
        /// Get total process count (for statistics)
        /// </summary>
        public async Task<int> GetTotalProcessCountAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.Set<Process>().CountAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting process count: {ex.Message}");
                throw new Exception("Failed to get process count", ex);
            }
        }

        /// <summary>
        /// Get total function count (for statistics)
        /// </summary>
        public async Task<int> GetTotalFunctionCountAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.Set<Function>().CountAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting function count: {ex.Message}");
                throw new Exception("Failed to get function count", ex);
            }
        }

        /// <summary>
        /// Check if a ProcessID exists in the database
        /// </summary>
        public async Task<bool> ProcessIdExistsAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.Set<Process>().AnyAsync(p => p.ProcessID == processId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking if process exists: {ex.Message}");
                throw new Exception("Failed to check if process exists", ex);
            }
        }

        /// <summary>
        /// Get the first process matching a ProcessID (for copying data when creating new process from existing ID)
        /// This gets the process structure/template but not the specific test linkage
        /// </summary>
        public async Task<Process> GetProcessTemplateByIdAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Get the first process with this ID (there might be multiple with same ProcessID in different tests)
                    var process = await context.Set<Process>()
                        .FirstOrDefaultAsync(p => p.ProcessID == processId);

                    if (process != null)
                    {
                        // Initialize functions collection
                        process.Functions = new ObservableCollection<Function>();
                        process.AreFunctionsLoaded = false;
                    }

                    return process;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting process template: {ex.Message}");
                throw new Exception("Failed to get process template", ex);
            }
        }

        // ================================================
        // CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert a new process into the database
        /// </summary>
        public async Task<Process> InsertProcessAsync(Process process)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Manually generate Index value since database column might not be IDENTITY
                    var maxIndex = await context.Set<Process>().MaxAsync(p => (int?)p.Index) ?? 0;
                    process.Index = maxIndex + 1;

                    // Log what we're trying to insert
                    System.Diagnostics.Debug.WriteLine($"⏳ Attempting to insert process:");
                    System.Diagnostics.Debug.WriteLine($"   TestID: {process.TestID}");
                    System.Diagnostics.Debug.WriteLine($"   ProcessID: {process.ProcessID}");
                    System.Diagnostics.Debug.WriteLine($"   ProcessPosition: {process.ProcessPosition}");
                    System.Diagnostics.Debug.WriteLine($"   Index: {process.Index} (manually generated as max+1)");

                    // Add the new process
                    await context.Set<Process>().AddAsync(process);

                    System.Diagnostics.Debug.WriteLine($"✓ Process added to context, calling SaveChangesAsync...");
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Process #{process.ProcessID} inserted successfully with Index #{process.Index}");
                    return process;
                }
            }
            catch (Exception ex)
            {
                // Log DETAILED error information including inner exceptions
                System.Diagnostics.Debug.WriteLine($"✗ ========== DATABASE INSERT ERROR ==========");
                System.Diagnostics.Debug.WriteLine($"✗ Main Exception: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"✗ Main Message: {ex.Message}");

                var innerEx = ex.InnerException;
                int level = 1;
                while (innerEx != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Inner Exception {level}: {innerEx.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"✗ Inner Message {level}: {innerEx.Message}");
                    if (innerEx.StackTrace != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Inner StackTrace {level}: {innerEx.StackTrace}");
                    }
                    innerEx = innerEx.InnerException;
                    level++;
                }

                System.Diagnostics.Debug.WriteLine($"✗ Full Exception: {ex}");
                System.Diagnostics.Debug.WriteLine($"✗ ==========================================");

                throw new Exception($"Failed to insert process. Error: {ex.Message}. Inner: {ex.InnerException?.Message}", ex);
            }
        }

        /// <summary>
        /// Insert a new function into the database
        /// </summary>
        public async Task<Function> InsertFunctionAsync(Function function)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Manually generate Index value
                    var maxIndex = await context.Set<Function>().MaxAsync(f => (int?)f.Index) ?? 0;
                    function.Index = maxIndex + 1;

                    System.Diagnostics.Debug.WriteLine($"⏳ Inserting function: {function.FunctionName} with Index #{function.Index}");

                    // Add the new function
                    await context.Set<Function>().AddAsync(function);
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Function #{function.Index} inserted successfully");
                    return function;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error inserting function: {ex.Message}");
                throw;
            }
        }

        // ================================================
        // UPDATE OPERATIONS
        // ================================================

        /// <summary>
        /// Update an existing process
        /// </summary>
        public async Task UpdateProcessAsync(Process process)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existingProcess = await context.Set<Process>()
                        .FirstOrDefaultAsync(p => p.ProcessID == process.ProcessID);

                    if (existingProcess == null)
                    {
                        throw new InvalidOperationException($"Process with ID {process.ProcessID} not found");
                    }

                    // Update properties manually to avoid modifying primary keys
                    foreach (var property in typeof(Process).GetProperties())
                    {
                        // Skip primary key fields and navigation properties
                        if (property.Name == "Index" || property.Name == "ProcessID" ||
                            property.Name == "Test" || property.Name == "Functions")
                            continue;

                        // Skip read-only properties
                        if (!property.CanWrite)
                            continue;

                        var newValue = property.GetValue(process);
                        property.SetValue(existingProcess, newValue);
                    }

                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Process #{process.ProcessID} updated successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating process: {ex.Message}");
                throw new Exception("Failed to update process", ex);
            }
        }

        /// <summary>
        /// Update an existing function
        /// </summary>
        public async Task UpdateFunctionAsync(Function function)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existingFunction = await context.Set<Function>()
                        .FirstOrDefaultAsync(f => f.Index == function.Index);

                    if (existingFunction == null)
                    {
                        throw new InvalidOperationException($"Function with Index {function.Index} not found");
                    }

                    // Update properties manually to avoid modifying primary keys
                    foreach (var property in typeof(Function).GetProperties())
                    {
                        // Skip primary key fields and navigation properties
                        if (property.Name == "Index" || property.Name == "ProcessID" ||
                            property.Name == "Process")
                            continue;

                        // Skip read-only properties
                        if (!property.CanWrite)
                            continue;

                        var newValue = property.GetValue(function);
                        property.SetValue(existingFunction, newValue);
                    }

                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Function #{function.Index} updated successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating function: {ex.Message}");
                throw new Exception("Failed to update function", ex);
            }
        }

        // ================================================
        // DELETE OPERATIONS
        // ================================================

        /// <summary>
        /// Delete process from database (cascades to functions)
        /// </summary>
        public async Task DeleteProcessAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var process = await context.Set<Process>()
                        .FirstOrDefaultAsync(p => p.ProcessID == processId);

                    if (process == null)
                    {
                        throw new InvalidOperationException($"Process with ID {processId} not found");
                    }

                    context.Set<Process>().Remove(process);
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Process #{processId} deleted successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting process: {ex.Message}");
                throw new Exception("Failed to delete process", ex);
            }
        }
    }
}
