using System;
using System.Collections.Generic;
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
    /// Repository for History Log CRUD operations
    /// Tracks changes to Tests, Processes, and Functions for audit trail
    /// </summary>
    public class HistoryRepository : IHistoryRepository
    {
        // ================================================
        // CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert a new history entry into the database
        /// </summary>
        public async Task<HistoryEntry> InsertHistoryEntryAsync(HistoryEntry entry)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Inserting history entry: {entry.OperationType} for {entry.EntityType} #{entry.EntityId}...");

                    // Use stored procedure for insertion
                    var parameters = new[]
                    {
                        new SqlParameter("@EntityType", entry.EntityType ?? (object)DBNull.Value),
                        new SqlParameter("@EntityId", entry.EntityId ?? (object)DBNull.Value),
                        new SqlParameter("@EntityName", entry.EntityName ?? (object)DBNull.Value),
                        new SqlParameter("@OperationType", entry.OperationType ?? (object)DBNull.Value),
                        new SqlParameter("@FieldName", entry.FieldName ?? (object)DBNull.Value),
                        new SqlParameter("@OldValue", entry.OldValue ?? (object)DBNull.Value),
                        new SqlParameter("@NewValue", entry.NewValue ?? (object)DBNull.Value),
                        new SqlParameter("@ChangedBy", entry.ChangedBy ?? "System"),
                        new SqlParameter("@ChangeDescription", entry.ChangeDescription ?? (object)DBNull.Value)
                    };

                    var spName = $"EXEC [{schemaName}].[usp_InsertHistoryLog] " +
                                 "@EntityType, @EntityId, @EntityName, @OperationType, " +
                                 "@FieldName, @OldValue, @NewValue, @ChangedBy, @ChangeDescription";

                    // Execute and get the new ID
                    await context.Database.ExecuteSqlRawAsync(spName, parameters);

                    System.Diagnostics.Debug.WriteLine($"✓ History entry inserted successfully");
                    return entry;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error inserting history entry: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"✗ Stack trace: {ex.StackTrace}");
                throw new Exception("Failed to insert history entry", ex);
            }
        }

        // ================================================
        // READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get history entries for a specific entity (Test, Process, or Function)
        /// Returns entries ordered by most recent first
        /// </summary>
        public async Task<List<HistoryEntry>> GetHistoryForEntityAsync(string entityType, string entityId, int top = 3)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading history for {entityType} #{entityId} (top {top})...");

                    var parameters = new[]
                    {
                        new SqlParameter("@EntityType", entityType),
                        new SqlParameter("@EntityId", entityId),
                        new SqlParameter("@Top", top)
                    };

                    var spName = $"EXEC [{schemaName}].[usp_GetHistoryLog] @EntityType, @EntityId, @Top";

                    var history = await context.Set<HistoryEntry>()
                        .FromSqlRaw(spName, parameters)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {history.Count} history entries for {entityType} #{entityId}");
                    return history;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading history: {ex.Message}");
                throw new Exception("Failed to load history entries", ex);
            }
        }

        /// <summary>
        /// Get history for a Process including all its related Functions
        /// This shows both process changes AND function changes in one view
        /// </summary>
        public async Task<List<HistoryEntry>> GetProcessHistoryWithFunctionsAsync(int processIndex, double processId, int top = 3)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = SchemaConfigService.Instance.CurrentSchema;
                    System.Diagnostics.Debug.WriteLine($"⏳ Loading history for Process Index={processIndex}, ProcessID={processId} with functions (top {top})...");

                    var parameters = new[]
                    {
                        new SqlParameter("@ProcessIndex", processIndex),
                        new SqlParameter("@ProcessID", processId),
                        new SqlParameter("@Top", top)
                    };

                    var spName = $"EXEC [{schemaName}].[usp_GetProcessHistoryWithFunctions] @ProcessIndex, @ProcessID, @Top";

                    var history = await context.Set<HistoryEntry>()
                        .FromSqlRaw(spName, parameters)
                        .ToListAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Loaded {history.Count} history entries for Process (including functions)");
                    return history;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading process history with functions: {ex.Message}");
                throw new Exception("Failed to load process history", ex);
            }
        }

        // ================================================
        // DELETE OPERATIONS (Maintenance)
        // ================================================

        /// <summary>
        /// Delete old history entries older than specified days
        /// Used for database cleanup to prevent unlimited growth
        /// </summary>
        public async Task<int> DeleteOldHistoryAsync(int daysToKeep = 90)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var schemaName = SchemaConfigService.Instance.CurrentSchema;
                    var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                    System.Diagnostics.Debug.WriteLine($"⏳ Deleting history entries older than {daysToKeep} days (before {cutoffDate:yyyy-MM-dd})...");

                    var deletedCount = await context.HistoryLogs
                        .Where(h => h.ChangedAt < cutoffDate)
                        .ExecuteDeleteAsync();

                    System.Diagnostics.Debug.WriteLine($"✓ Deleted {deletedCount} old history entries");
                    return deletedCount;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error deleting old history: {ex.Message}");
                throw new Exception("Failed to delete old history entries", ex);
            }
        }
    }
}
