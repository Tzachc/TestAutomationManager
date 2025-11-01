using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TestAutomationManager.Models;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Singleton service that caches Process and Function data to avoid duplicate database loads.
    /// Shared between TestsView (which loads processes per test) and ProcessView (which loads all processes).
    /// </summary>
    public class ProcessCacheService
    {
        private static readonly Lazy<ProcessCacheService> _instance =
            new Lazy<ProcessCacheService>(() => new ProcessCacheService());

        public static ProcessCacheService Instance => _instance.Value;

        // ================================================
        // CACHE STORAGE
        // ================================================

        /// <summary>
        /// Cache of ALL processes (including duplicates with same ProcessID)
        /// Thread-safe for concurrent access from background workers
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentBag<Process> _allProcesses = new();

        /// <summary>
        /// Quick lookup cache by ProcessID (for single process lookup)
        /// Note: May contain duplicates - use _allProcesses for complete list
        /// </summary>
        private readonly ConcurrentDictionary<double, Process> _processCache = new();

        /// <summary>
        /// Cache of functions by ProcessID
        /// Key: ProcessID, Value: List of functions for that process
        /// </summary>
        private readonly ConcurrentDictionary<double, List<Function>> _functionCache = new();

        /// <summary>
        /// Track which processes have had their functions loaded
        /// </summary>
        private readonly ConcurrentDictionary<double, bool> _functionsLoaded = new();

        // ================================================
        // STATISTICS
        // ================================================

        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        private ProcessCacheService()
        {
            System.Diagnostics.Debug.WriteLine("✓ ProcessCacheService initialized");
        }

        // ================================================
        // PROCESS CACHE METHODS
        // ================================================

        /// <summary>
        /// Add a process to the cache
        /// </summary>
        public void AddProcess(Process process)
        {
            if (process?.ProcessID == null)
                return;

            // Add to complete list (allows duplicates)
            _allProcesses.Add(process);

            // Add to quick lookup (last one wins for duplicates)
            _processCache[process.ProcessID.Value] = process;
        }

        /// <summary>
        /// Add multiple processes to the cache
        /// </summary>
        public void AddProcesses(IEnumerable<Process> processes)
        {
            int addedCount = 0;
            int skippedCount = 0;
            int duplicateCount = 0;

            foreach (var process in processes)
            {
                if (process?.ProcessID == null)
                {
                    skippedCount++;
                    continue;
                }

                // Add to complete list (keeps ALL processes)
                _allProcesses.Add(process);

                // Track if this is a duplicate ProcessID
                if (_processCache.ContainsKey(process.ProcessID.Value))
                    duplicateCount++;

                // Add to quick lookup (overwrites duplicates)
                _processCache[process.ProcessID.Value] = process;
                addedCount++;
            }

            System.Diagnostics.Debug.WriteLine($"📦 Cache: Added {addedCount} processes ({duplicateCount} duplicate IDs), skipped {skippedCount} (null IDs). Total cached: {_allProcesses.Count} (unique IDs: {_processCache.Count})");
        }

        /// <summary>
        /// Get a process from cache by ID
        /// </summary>
        public Process GetProcess(double processId)
        {
            if (_processCache.TryGetValue(processId, out var process))
            {
                _cacheHits++;
                return process;
            }

            _cacheMisses++;
            return null;
        }

        /// <summary>
        /// Get all cached processes (including duplicates)
        /// </summary>
        public List<Process> GetAllCachedProcesses()
        {
            var allProcessesList = _allProcesses.ToList();
            System.Diagnostics.Debug.WriteLine($"📦 ProcessCache: Retrieving {allProcessesList.Count} cached processes ({_processCache.Count} unique IDs)");
            return allProcessesList;
        }

        /// <summary>
        /// Check if a process is cached
        /// </summary>
        public bool IsProcessCached(double processId)
        {
            return _processCache.ContainsKey(processId);
        }

        /// <summary>
        /// Get count of cached processes (including duplicates)
        /// </summary>
        public int GetCachedProcessCount()
        {
            return _allProcesses.Count;
        }

        // ================================================
        // FUNCTION CACHE METHODS
        // ================================================

        /// <summary>
        /// Add functions for a process to the cache
        /// </summary>
        public void AddFunctions(double processId, List<Function> functions)
        {
            _functionCache[processId] = functions;
            _functionsLoaded[processId] = true;
        }

        /// <summary>
        /// Get functions for a process from cache
        /// </summary>
        public List<Function> GetFunctions(double processId)
        {
            if (_functionCache.TryGetValue(processId, out var functions))
            {
                _cacheHits++;
                return functions;
            }

            _cacheMisses++;
            return null;
        }

        /// <summary>
        /// Check if functions are loaded for a process
        /// </summary>
        public bool AreFunctionsLoaded(double processId)
        {
            return _functionsLoaded.ContainsKey(processId) && _functionsLoaded[processId];
        }

        // ================================================
        // UTILITY METHODS
        // ================================================

        /// <summary>
        /// Clear all cached data
        /// </summary>
        public void Clear()
        {
            _allProcesses.Clear();
            _processCache.Clear();
            _functionCache.Clear();
            _functionsLoaded.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            System.Diagnostics.Debug.WriteLine("🗑️ ProcessCache: Cleared all cached data");
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public void LogStatistics()
        {
            double hitRate = _cacheHits + _cacheMisses > 0
                ? (_cacheHits / (double)(_cacheHits + _cacheMisses)) * 100
                : 0;

            int duplicates = _allProcesses.Count - _processCache.Count;

            System.Diagnostics.Debug.WriteLine($"📊 ProcessCache Statistics:");
            System.Diagnostics.Debug.WriteLine($"   Processes cached: {_allProcesses.Count} total ({_processCache.Count} unique IDs, {duplicates} duplicates)");
            System.Diagnostics.Debug.WriteLine($"   Functions cached: {_functionCache.Count}");
            System.Diagnostics.Debug.WriteLine($"   Cache hits: {_cacheHits}");
            System.Diagnostics.Debug.WriteLine($"   Cache misses: {_cacheMisses}");
            System.Diagnostics.Debug.WriteLine($"   Hit rate: {hitRate:F1}%");
        }

        /// <summary>
        /// Check if we have enough cached data to skip initial database load
        /// </summary>
        public bool HasSignificantCachedData()
        {
            // If we have more than 100 processes cached, we can probably use them
            // instead of reloading from database
            return _processCache.Count > 100;
        }
    }
}
