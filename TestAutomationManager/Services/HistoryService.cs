using System;
using System.Threading.Tasks;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Service for managing history log entries
    /// Provides centralized methods for tracking changes to Tests, Processes, and Functions
    /// </summary>
    public class HistoryService
    {
        private readonly IHistoryRepository _historyRepository;

        // Singleton instance
        private static HistoryService? _instance;
        private static readonly object _lock = new object();

        public static HistoryService Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new HistoryService();
                    }
                    return _instance;
                }
            }
        }

        private HistoryService()
        {
            _historyRepository = new HistoryRepository();
        }

        // ================================================
        // TEST HISTORY LOGGING
        // ================================================

        /// <summary>
        /// Log a Test creation
        /// </summary>
        public async Task LogTestCreatedAsync(Test test)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Test",
                    EntityId = test.TestID?.ToString() ?? "0",
                    EntityName = test.TestName ?? "(Unnamed)",
                    OperationType = "INSERT",
                    ChangeDescription = $"Test '{test.TestName}' created",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log test creation: {ex.Message}");
                // Don't throw - history logging should not break the main operation
            }
        }

        /// <summary>
        /// Log a Test field update
        /// </summary>
        public async Task LogTestFieldUpdatedAsync(Test test, string fieldName, string? oldValue, string? newValue)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Test",
                    EntityId = test.TestID?.ToString() ?? "0",
                    EntityName = test.TestName ?? "(Unnamed)",
                    OperationType = "UPDATE",
                    FieldName = fieldName,
                    OldValue = oldValue ?? "",
                    NewValue = newValue ?? "",
                    ChangeDescription = $"Updated {fieldName}: '{oldValue}' → '{newValue}'",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log test update: {ex.Message}");
                // Don't throw - history logging should not break the main operation
            }
        }

        /// <summary>
        /// Log a Test deletion
        /// </summary>
        public async Task LogTestDeletedAsync(int testId, string? testName)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Test",
                    EntityId = testId.ToString(),
                    EntityName = testName ?? "(Unnamed)",
                    OperationType = "DELETE",
                    ChangeDescription = $"Test '{testName}' deleted",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log test deletion: {ex.Message}");
                // Don't throw - history logging should not break the main operation
            }
        }

        // ================================================
        // PROCESS HISTORY LOGGING
        // ================================================

        /// <summary>
        /// Log a Process creation
        /// </summary>
        public async Task LogProcessCreatedAsync(Process process)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Process",
                    EntityId = process.Index?.ToString() ?? "0",
                    EntityName = process.ProcessName ?? "(Unnamed)",
                    OperationType = "INSERT",
                    ChangeDescription = $"Process '{process.ProcessName}' created (ProcessID: {process.ProcessID})",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log process creation: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a Process field update
        /// </summary>
        public async Task LogProcessFieldUpdatedAsync(Process process, string fieldName, string? oldValue, string? newValue)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Process",
                    EntityId = process.Index?.ToString() ?? "0",
                    EntityName = process.ProcessName ?? "(Unnamed)",
                    OperationType = "UPDATE",
                    FieldName = fieldName,
                    OldValue = oldValue ?? "",
                    NewValue = newValue ?? "",
                    ChangeDescription = $"Updated {fieldName}: '{oldValue}' → '{newValue}'",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log process update: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a Process deletion
        /// </summary>
        public async Task LogProcessDeletedAsync(int processIndex, string? processName)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Process",
                    EntityId = processIndex.ToString(),
                    EntityName = processName ?? "(Unnamed)",
                    OperationType = "DELETE",
                    ChangeDescription = $"Process '{processName}' deleted",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log process deletion: {ex.Message}");
            }
        }

        // ================================================
        // FUNCTION HISTORY LOGGING
        // ================================================

        /// <summary>
        /// Log a Function creation
        /// </summary>
        public async Task LogFunctionCreatedAsync(Function function)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Function",
                    EntityId = function.Index?.ToString() ?? "0",
                    EntityName = function.FunctionName ?? "(Unnamed)",
                    OperationType = "INSERT",
                    ChangeDescription = $"Function '{function.FunctionName}' created (Pos: {function.FunctionPosition})",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log function creation: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a Function field update
        /// </summary>
        public async Task LogFunctionFieldUpdatedAsync(Function function, string fieldName, string? oldValue, string? newValue)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Function",
                    EntityId = function.Index?.ToString() ?? "0",
                    EntityName = function.FunctionName ?? "(Unnamed)",
                    OperationType = "UPDATE",
                    FieldName = fieldName,
                    OldValue = oldValue ?? "",
                    NewValue = newValue ?? "",
                    ChangeDescription = $"Updated {fieldName}: '{oldValue}' → '{newValue}'",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log function update: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a Function deletion
        /// </summary>
        public async Task LogFunctionDeletedAsync(int functionIndex, string? functionName)
        {
            try
            {
                var entry = new HistoryEntry
                {
                    EntityType = "Function",
                    EntityId = functionIndex.ToString(),
                    EntityName = functionName ?? "(Unnamed)",
                    OperationType = "DELETE",
                    ChangeDescription = $"Function '{functionName}' deleted",
                    ChangedBy = "System",
                    ChangedAt = DateTime.Now
                };

                await _historyRepository.InsertHistoryEntryAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to log function deletion: {ex.Message}");
            }
        }

        // ================================================
        // RETRIEVE HISTORY
        // ================================================

        /// <summary>
        /// Get history for a Test
        /// </summary>
        public async Task<System.Collections.Generic.List<HistoryEntry>> GetTestHistoryAsync(int testId, int top = 3)
        {
            return await _historyRepository.GetHistoryForEntityAsync("Test", testId.ToString(), top);
        }

        /// <summary>
        /// Get history for a Process (including its Functions)
        /// </summary>
        public async Task<System.Collections.Generic.List<HistoryEntry>> GetProcessHistoryAsync(int processIndex, double processId, int top = 3)
        {
            return await _historyRepository.GetProcessHistoryWithFunctionsAsync(processIndex, processId, top);
        }

        /// <summary>
        /// Get history for a Function
        /// </summary>
        public async Task<System.Collections.Generic.List<HistoryEntry>> GetFunctionHistoryAsync(int functionIndex, int top = 3)
        {
            return await _historyRepository.GetHistoryForEntityAsync("Function", functionIndex.ToString(), top);
        }
    }
}
