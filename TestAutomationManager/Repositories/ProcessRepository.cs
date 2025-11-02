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

                    // Update properties
                    context.Entry(existingProcess).CurrentValues.SetValues(process);
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
