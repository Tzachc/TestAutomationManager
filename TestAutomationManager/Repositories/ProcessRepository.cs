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
        /// Get all processes WITHOUT functions (direct query with NULL handling)
        /// Uses direct EF Core query to handle NULL values safely
        /// OPTIMIZED FOR LARGE DATASETS (20000+ records)
        /// </summary>
        public async Task<List<Process>> GetAllProcessesAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = TestAutomationManager.Services.SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading processes using direct query from schema '{schemaName}'...");

                    // ✅ Use direct EF Core query with AsNoTracking for performance
                    var processes = await context.Set<Process>()
                        .AsNoTracking()
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes via direct query");

                    // Initialize empty collections for UI binding
                    foreach (var process in processes)
                    {
                        // Handle NULL values by replacing with empty strings
                        process.ProcessName = process.ProcessName ?? "";
                        process.WEB3Operator = process.WEB3Operator ?? "";
                        process.Pass_Fail_WEB3Operator = process.Pass_Fail_WEB3Operator ?? "";
                        process.Comments = process.Comments ?? "";
                        process.Module = process.Module ?? "";
                        process.Repeat = process.Repeat ?? "";
                        process.LastRunning = process.LastRunning ?? "";

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
        // CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert a new process instance into a test
        /// Returns the generated Index (primary key) for the new process
        /// </summary>
        public async Task<int> InsertProcessAsync(Process process)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Log what we're trying to insert for debugging
                    System.Diagnostics.Debug.WriteLine($"Attempting to insert process:");
                    System.Diagnostics.Debug.WriteLine($"  TestID: {process.TestID}");
                    System.Diagnostics.Debug.WriteLine($"  ProcessID: {process.ProcessID}");
                    System.Diagnostics.Debug.WriteLine($"  ProcessPosition: {process.ProcessPosition}");
                    System.Diagnostics.Debug.WriteLine($"  ProcessName: {process.ProcessName ?? "(null)"}");
                    System.Diagnostics.Debug.WriteLine($"  Index: {process.Index?.ToString() ?? "(null)"}");

                    // Add the process to the context
                    context.Set<Process>().Add(process);
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Process #{process.ProcessID} inserted successfully with Index {process.Index}");
                    return process.Index ?? 0;
                }
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                var fullMsg = ex.InnerException != null ? $"{ex.Message} | Inner: {innerMsg}" : ex.Message;
                System.Diagnostics.Debug.WriteLine($"✗ Error inserting process: {fullMsg}");

                // Log full exception details for debugging
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  Inner exception type: {ex.InnerException.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Inner-inner exception: {ex.InnerException.InnerException.Message}");
                    }
                }

                throw new Exception($"Failed to insert process: {innerMsg}", ex);
            }
        }

        /// <summary>
        /// Check if a ProcessID exists in the database
        /// </summary>
        public async Task<bool> ProcessIDExistsAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.Set<Process>()
                        .AnyAsync(p => p.ProcessID == processId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking ProcessID existence: {ex.Message}");
                throw new Exception("Failed to check ProcessID", ex);
            }
        }

        /// <summary>
        /// Get a process template by ProcessID (to copy its data for new instances)
        /// </summary>
        public async Task<Process?> GetProcessTemplateByProcessIDAsync(double processId)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    // Get the first process with this ProcessID as a template
                    var process = await context.Set<Process>()
                        .Where(p => p.ProcessID == processId)
                        .Select(p => new Process
                        {
                            ProcessID = p.ProcessID,
                            ProcessName = p.ProcessName,
                            WEB3Operator = p.WEB3Operator,
                            Pass_Fail_WEB3Operator = p.Pass_Fail_WEB3Operator,
                            Comments = p.Comments,
                            Module = p.Module,
                            Repeat = p.Repeat,
                            LastRunning = p.LastRunning,
                            TempParam = p.TempParam,
                            TempParam1 = p.TempParam1,
                            TempParam11 = p.TempParam11,
                            TempParam111 = p.TempParam111,
                            TempParam1111 = p.TempParam1111,
                            TempParam11111 = p.TempParam11111,
                            Param1 = p.Param1,
                            Param2 = p.Param2,
                            Param3 = p.Param3,
                            Param4 = p.Param4,
                            Param5 = p.Param5,
                            Param6 = p.Param6,
                            Param7 = p.Param7,
                            Param8 = p.Param8,
                            Param9 = p.Param9,
                            Param10 = p.Param10,
                            Param11 = p.Param11,
                            Param12 = p.Param12,
                            Param13 = p.Param13,
                            Param14 = p.Param14,
                            Param15 = p.Param15,
                            Param16 = p.Param16,
                            Param17 = p.Param17,
                            Param18 = p.Param18,
                            Param19 = p.Param19,
                            Param20 = p.Param20,
                            Param21 = p.Param21,
                            Param22 = p.Param22,
                            Param23 = p.Param23,
                            Param24 = p.Param24,
                            Param25 = p.Param25,
                            Param26 = p.Param26,
                            Param27 = p.Param27,
                            Param28 = p.Param28,
                            Param29 = p.Param29,
                            Param30 = p.Param30,
                            Param31 = p.Param31,
                            Param32 = p.Param32,
                            Param33 = p.Param33,
                            Param34 = p.Param34,
                            Param35 = p.Param35,
                            Param36 = p.Param36,
                            Param37 = p.Param37,
                            Param38 = p.Param38,
                            Param39 = p.Param39,
                            Param40 = p.Param40,
                            Param41 = p.Param41,
                            Param42 = p.Param42,
                            Param43 = p.Param43,
                            Param44 = p.Param44,
                            Param45 = p.Param45,
                            Param46 = p.Param46
                        })
                        .FirstOrDefaultAsync();

                    if (process != null)
                    {
                        // Initialize empty functions collection
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
