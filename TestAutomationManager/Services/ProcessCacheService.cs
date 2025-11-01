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
        /// Cache of all processes by ProcessID
        /// Thread-safe for concurrent access from background workers
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

            _processCache[process.ProcessID.Value] = process;
        }

        /// <summary>
        /// Add multiple processes to the cache
        /// </summary>
        public void AddProcesses(IEnumerable<Process> processes)
        {
            foreach (var process in processes)
            {
                AddProcess(process);
            }
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
        /// Get all cached processes
        /// </summary>
        public List<Process> GetAllCachedProcesses()
        {
            System.Diagnostics.Debug.WriteLine($"📦 ProcessCache: Retrieving {_processCache.Count} cached processes");
            return _processCache.Values.ToList();
        }

        /// <summary>
        /// Check if a process is cached
        /// </summary>
        public bool IsProcessCached(double processId)
        {
            return _processCache.ContainsKey(processId);
        }

        /// <summary>
        /// Get count of cached processes
        /// </summary>
        public int GetCachedProcessCount()
        {
            return _processCache.Count;
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

            System.Diagnostics.Debug.WriteLine($"📊 ProcessCache Statistics:");
            System.Diagnostics.Debug.WriteLine($"   Processes cached: {_processCache.Count}");
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
