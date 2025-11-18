using System.Collections.Generic;
using System.Threading.Tasks;
using TestAutomationManager.Models;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository interface for History Log operations
    /// Tracks changes to Tests, Processes, and Functions
    /// </summary>
    public interface IHistoryRepository
    {
        /// <summary>
        /// Insert a new history entry
        /// </summary>
        /// <param name="entry">History entry to insert</param>
        /// <returns>The inserted entry with generated ID</returns>
        Task<HistoryEntry> InsertHistoryEntryAsync(HistoryEntry entry);

        /// <summary>
        /// Get history entries for a specific entity (Test, Process, or Function)
        /// </summary>
        /// <param name="entityType">Type of entity (Test/Process/Function)</param>
        /// <param name="entityId">ID of the entity</param>
        /// <param name="top">Number of recent entries to retrieve (default: 3)</param>
        /// <returns>List of history entries ordered by most recent first</returns>
        Task<List<HistoryEntry>> GetHistoryForEntityAsync(string entityType, string entityId, int top = 3);

        /// <summary>
        /// Get history for a Process including all its related Functions
        /// </summary>
        /// <param name="processIndex">Process Index (primary key)</param>
        /// <param name="processId">Process ID (for finding related functions)</param>
        /// <param name="top">Number of recent entries to retrieve (default: 3)</param>
        /// <returns>Combined list of Process and Function history entries</returns>
        Task<List<HistoryEntry>> GetProcessHistoryWithFunctionsAsync(int processIndex, double processId, int top = 3);

        /// <summary>
        /// Delete old history entries (for cleanup/maintenance)
        /// </summary>
        /// <param name="daysToKeep">Number of days of history to keep</param>
        /// <returns>Number of entries deleted</returns>
        Task<int> DeleteOldHistoryAsync(int daysToKeep = 90);
    }
}
