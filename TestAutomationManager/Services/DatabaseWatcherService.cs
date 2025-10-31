using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Event args for incremental database updates (multi-user collaboration)
    /// Contains ONLY the items that changed - not all data!
    /// Tracks changes across ALL 3 tables: Test, Process, Function
    /// </summary>
    public class DatabaseChangeEventArgs : EventArgs
    {
        // Test changes
        public List<int> NewTestIds { get; set; } = new List<int>();
        public List<int> ChangedTestIds { get; set; } = new List<int>();
        public List<int> DeletedTestIds { get; set; } = new List<int>();
        public List<Test> ChangedTests { get; set; } = new List<Test>();

        // Process changes (composite key: TestID + ProcessID)
        public List<(int TestID, double ProcessID)> NewProcessKeys { get; set; } = new List<(int, double)>();
        public List<(int TestID, double ProcessID)> ChangedProcessKeys { get; set; } = new List<(int, double)>();
        public List<(int TestID, double ProcessID)> DeletedProcessKeys { get; set; } = new List<(int, double)>();
        public List<Process> ChangedProcesses { get; set; } = new List<Process>();

        // Function changes (key: ProcessID)
        public List<double> NewFunctionProcessIds { get; set; } = new List<double>();
        public List<double> ChangedFunctionProcessIds { get; set; } = new List<double>();
        public List<double> DeletedFunctionProcessIds { get; set; } = new List<double>();
        public Dictionary<double, List<Function>> ChangedFunctionsByProcess { get; set; } = new Dictionary<double, List<Function>>();

        public bool HasChanges =>
            NewTestIds.Count > 0 || ChangedTestIds.Count > 0 || DeletedTestIds.Count > 0 ||
            NewProcessKeys.Count > 0 || ChangedProcessKeys.Count > 0 || DeletedProcessKeys.Count > 0 ||
            NewFunctionProcessIds.Count > 0 || ChangedFunctionProcessIds.Count > 0 || DeletedFunctionProcessIds.Count > 0;
    }

    /// <summary>
    /// Monitors database for external changes and updates UI (Multi-user collaboration)
    /// Detects ANY changes on ANY column and updates ONLY what changed
    /// Perfect for real-time collaboration when multiple users work in parallel
    /// </summary>
    public class DatabaseWatcherService
    {
        // ================================================
        // SINGLETON PATTERN
        // ================================================

        private static DatabaseWatcherService _instance;
        public static DatabaseWatcherService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new DatabaseWatcherService();
                return _instance;
            }
        }

        // ================================================
        // FIELDS
        // ================================================

        private readonly ITestRepository _repository;
        private Timer _pollingTimer;
        private bool _isWatching;

        /// <summary>
        /// Cache of test fingerprints for differential updates
        /// Key: TestID, Value: Fingerprint (hash of all important fields)
        /// </summary>
        private Dictionary<int, string> _testFingerprints = new Dictionary<int, string>();

        /// <summary>
        /// Cache of process fingerprints for differential updates
        /// Key: "TestID:ProcessID", Value: Fingerprint (hash of all 46 params)
        /// </summary>
        private Dictionary<string, string> _processFingerprints = new Dictionary<string, string>();

        /// <summary>
        /// Cache of function fingerprints for differential updates
        /// Key: "ProcessID:FunctionPosition", Value: Fingerprint (hash of all 30 params)
        /// </summary>
        private Dictionary<string, string> _functionFingerprints = new Dictionary<string, string>();

        /// <summary>
        /// Event fired when database changes detected (incremental updates only!)
        /// </summary>
        public event EventHandler<DatabaseChangeEventArgs> DatabaseChanged;

        /// <summary>
        /// Legacy event for backward compatibility (full reload)
        /// </summary>
        public event EventHandler<List<Test>> TestsUpdated;

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// Polling interval in seconds (default: 3 seconds for near real-time collaboration)
        /// </summary>
        public int PollingIntervalSeconds { get; set; } = 3;

        /// <summary>
        /// Is the watcher currently active
        /// </summary>
        public bool IsWatching => _isWatching;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        private DatabaseWatcherService()
        {
            _repository = new TestRepository();
            _isWatching = false;
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        /// <summary>
        /// Start watching database for changes
        /// </summary>
        public void StartWatching()
        {
            if (_isWatching)
            {
                System.Diagnostics.Debug.WriteLine("⚠ Database watcher already running");
                return;
            }

            System.Diagnostics.Debug.WriteLine("🔍 Starting database watcher...");
            _isWatching = true;

            // Create timer that fires every X seconds
            _pollingTimer = new Timer(
                async _ => await CheckForChangesAsync(),
                null,
                TimeSpan.FromSeconds(1), // Start after 1 second
                TimeSpan.FromSeconds(PollingIntervalSeconds) // Repeat every X seconds
            );

            System.Diagnostics.Debug.WriteLine($"✓ Database watcher started (polling every {PollingIntervalSeconds}s)");
        }

        /// <summary>
        /// Stop watching database
        /// </summary>
        public void StopWatching()
        {
            if (!_isWatching)
                return;

            System.Diagnostics.Debug.WriteLine("🛑 Stopping database watcher...");

            _pollingTimer?.Dispose();
            _pollingTimer = null;
            _isWatching = false;

            System.Diagnostics.Debug.WriteLine("✓ Database watcher stopped");
        }

        /// <summary>
        /// Force immediate check for changes (useful after user makes changes)
        /// </summary>
        public async Task ForceCheckAsync()
        {
            await CheckForChangesAsync();
        }

        // ================================================
        // PRIVATE METHODS
        // ================================================

        /// <summary>
        /// Check database for changes using DIFFERENTIAL detection
        /// Tracks ALL 3 tables: Test_WEB3, Process_WEB3, Function_WEB3
        /// Detects ANY change on ANY column and updates ONLY what changed!
        /// </summary>
        private async Task CheckForChangesAsync()
        {
            try
            {
                // ⭐ STEP 1: Get fingerprints from all 3 tables
                var currentTestFingerprints = await GetTestFingerprintsAsync();
                var currentProcessFingerprints = await GetProcessFingerprintsAsync();
                var currentFunctionFingerprints = await GetFunctionFingerprintsAsync();

                // ⭐ STEP 2: Detect TEST changes
                var (newTestIds, changedTestIds, deletedTestIds) = DetectChanges(_testFingerprints, currentTestFingerprints);

                // ⭐ STEP 3: Detect PROCESS changes
                var (newProcessKeys, changedProcessKeys, deletedProcessKeys) = DetectProcessChanges(_processFingerprints, currentProcessFingerprints);

                // ⭐ STEP 4: Detect FUNCTION changes
                var (newFunctionKeys, changedFunctionKeys, deletedFunctionKeys) = DetectFunctionChanges(_functionFingerprints, currentFunctionFingerprints);

                // ⭐ STEP 5: If nothing changed, do nothing!
                if (newTestIds.Count == 0 && changedTestIds.Count == 0 && deletedTestIds.Count == 0 &&
                    newProcessKeys.Count == 0 && changedProcessKeys.Count == 0 && deletedProcessKeys.Count == 0 &&
                    newFunctionKeys.Count == 0 && changedFunctionKeys.Count == 0 && deletedFunctionKeys.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("✓ No changes detected (all 3 tables checked)");
                    return;
                }

                // ⭐ STEP 6: Log what changed
                if (newTestIds.Count > 0 || changedTestIds.Count > 0 || deletedTestIds.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"📊 Test changes: +{newTestIds.Count} ~{changedTestIds.Count} -{deletedTestIds.Count}");
                if (newProcessKeys.Count > 0 || changedProcessKeys.Count > 0 || deletedProcessKeys.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"🔧 Process changes: +{newProcessKeys.Count} ~{changedProcessKeys.Count} -{deletedProcessKeys.Count}");
                if (newFunctionKeys.Count > 0 || changedFunctionKeys.Count > 0 || deletedFunctionKeys.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"⚙️ Function changes: +{newFunctionKeys.Count} ~{changedFunctionKeys.Count} -{deletedFunctionKeys.Count}");

                // ⭐ STEP 7: Load ONLY the changed data
                var changedTests = await LoadChangedTestsAsync(newTestIds, changedTestIds);
                var changedProcesses = await LoadChangedProcessesAsync(newProcessKeys, changedProcessKeys);
                var changedFunctionsByProcess = await LoadChangedFunctionsAsync(newFunctionKeys, changedFunctionKeys);

                // ⭐ STEP 8: Fire incremental update event
                var changeEvent = new DatabaseChangeEventArgs
                {
                    NewTestIds = newTestIds,
                    ChangedTestIds = changedTestIds,
                    DeletedTestIds = deletedTestIds,
                    ChangedTests = changedTests,
                    NewProcessKeys = newProcessKeys,
                    ChangedProcessKeys = changedProcessKeys,
                    DeletedProcessKeys = deletedProcessKeys,
                    ChangedProcesses = changedProcesses,
                    NewFunctionProcessIds = newFunctionKeys.Select(k => k.processId).Distinct().ToList(),
                    ChangedFunctionProcessIds = changedFunctionKeys.Select(k => k.processId).Distinct().ToList(),
                    DeletedFunctionProcessIds = deletedFunctionKeys.Select(k => k.processId).Distinct().ToList(),
                    ChangedFunctionsByProcess = changedFunctionsByProcess
                };

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    DatabaseChanged?.Invoke(this, changeEvent);
                    System.Diagnostics.Debug.WriteLine($"✓ UI notified of all changes");
                });

                // ⭐ STEP 9: Update all fingerprint caches
                _testFingerprints = currentTestFingerprints;
                _processFingerprints = currentProcessFingerprints;
                _functionFingerprints = currentFunctionFingerprints;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking database: {ex.Message}");
            }
        }

        /// <summary>
        /// Get fingerprints of all tests (lightweight query for change detection)
        /// Fingerprint includes all important fields that users might edit
        /// </summary>
        private async Task<Dictionary<int, string>> GetTestFingerprintsAsync()
        {
            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                // Query only the fields needed for fingerprint (fast!)
                var testFingerprints = await context.Tests
                    .Select(t => new
                    {
                        t.TestID,
                        // Include all fields that might change
                        t.TestName,
                        t.RunStatus,
                        t.LastRunning,
                        t.LastTimePass,
                        t.Bugs,
                        t.ExceptionMessage,
                        t.RecipientsEmailsList,
                        t.SendEmailReport,
                        t.EmailOnFailureOnly,
                        t.ExitTestOnFailure,
                        t.TestRunAgainTimes,
                        t.SnapshotMultipleFailure,
                        t.DisableKillDriver
                    })
                    .ToListAsync();

                // Create fingerprint dictionary
                var result = new Dictionary<int, string>();
                foreach (var test in testFingerprints)
                {
                    if (!test.TestID.HasValue) continue;

                    // Create fingerprint: concat all important field values
                    string fingerprint = $"{test.TestName}|{test.RunStatus}|{test.LastRunning}|{test.LastTimePass}|" +
                                       $"{test.Bugs}|{test.ExceptionMessage}|{test.RecipientsEmailsList}|" +
                                       $"{test.SendEmailReport}|{test.EmailOnFailureOnly}|{test.ExitTestOnFailure}|" +
                                       $"{test.TestRunAgainTimes}|{test.SnapshotMultipleFailure}|{test.DisableKillDriver}";

                    result[(int)test.TestID.Value] = fingerprint;
                }

                return result;
            }
        }

        /// <summary>
        /// Get fingerprints of all processes (lightweight query for change detection)
        /// Includes ALL 46 parameters + other fields to detect ANY change
        /// Key format: "TestID:ProcessID"
        /// </summary>
        private async Task<Dictionary<string, string>> GetProcessFingerprintsAsync()
        {
            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                // Query only the fields needed for fingerprint
                var processFingerprints = await context.Set<Process>()
                    .Select(p => new
                    {
                        p.TestID,
                        p.ProcessID,
                        p.ProcessName,
                        p.ProcessPosition,
                        p.Comments,
                        p.Index,
                        p.LastRunning,
                        p.Module,
                        p.Pass_Fail_WEB3Operator,
                        p.Repeat,
                        p.TempParam,
                        p.WEB3Operator,
                        p.TempParam1,
                        p.TempParam11,
                        p.TempParam111,
                        p.TempParam1111,
                        p.TempParam11111,
                        p.TempParam111111,
                        // All 46 params
                        p.Param1, p.Param2, p.Param3, p.Param4, p.Param5,
                        p.Param6, p.Param7, p.Param8, p.Param9, p.Param10,
                        p.Param11, p.Param12, p.Param13, p.Param14, p.Param15,
                        p.Param16, p.Param17, p.Param18, p.Param19, p.Param20,
                        p.Param21, p.Param22, p.Param23, p.Param24, p.Param25,
                        p.Param26, p.Param27, p.Param28, p.Param29, p.Param30,
                        p.Param31, p.Param32, p.Param33, p.Param34, p.Param35,
                        p.Param36, p.Param37, p.Param38, p.Param39, p.Param40,
                        p.Param41, p.Param42, p.Param43, p.Param44, p.Param45,
                        p.Param46
                    })
                    .ToListAsync();

                var result = new Dictionary<string, string>();
                foreach (var proc in processFingerprints)
                {
                    if (!proc.TestID.HasValue || !proc.ProcessID.HasValue) continue;

                    // Create unique key
                    string key = $"{(int)proc.TestID.Value}:{proc.ProcessID.Value}";

                    // Create fingerprint: concat all fields that might change
                    string fingerprint = $"{proc.ProcessName}|{proc.ProcessPosition}|{proc.Comments}|{proc.Index}|" +
                                       $"{proc.LastRunning}|{proc.Module}|{proc.Pass_Fail_WEB3Operator}|{proc.Repeat}|" +
                                       $"{proc.TempParam}|{proc.WEB3Operator}|{proc.TempParam1}|{proc.TempParam11}|" +
                                       $"{proc.TempParam111}|{proc.TempParam1111}|{proc.TempParam11111}|{proc.TempParam111111}|" +
                                       $"{proc.Param1}|{proc.Param2}|{proc.Param3}|{proc.Param4}|{proc.Param5}|" +
                                       $"{proc.Param6}|{proc.Param7}|{proc.Param8}|{proc.Param9}|{proc.Param10}|" +
                                       $"{proc.Param11}|{proc.Param12}|{proc.Param13}|{proc.Param14}|{proc.Param15}|" +
                                       $"{proc.Param16}|{proc.Param17}|{proc.Param18}|{proc.Param19}|{proc.Param20}|" +
                                       $"{proc.Param21}|{proc.Param22}|{proc.Param23}|{proc.Param24}|{proc.Param25}|" +
                                       $"{proc.Param26}|{proc.Param27}|{proc.Param28}|{proc.Param29}|{proc.Param30}|" +
                                       $"{proc.Param31}|{proc.Param32}|{proc.Param33}|{proc.Param34}|{proc.Param35}|" +
                                       $"{proc.Param36}|{proc.Param37}|{proc.Param38}|{proc.Param39}|{proc.Param40}|" +
                                       $"{proc.Param41}|{proc.Param42}|{proc.Param43}|{proc.Param44}|{proc.Param45}|" +
                                       $"{proc.Param46}";

                    result[key] = fingerprint;
                }

                return result;
            }
        }

        /// <summary>
        /// Get fingerprints of all functions (lightweight query for change detection)
        /// Includes ALL 30 parameters + other fields to detect ANY change
        /// Key format: "ProcessID:FunctionPosition"
        /// </summary>
        private async Task<Dictionary<string, string>> GetFunctionFingerprintsAsync()
        {
            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                // Query only the fields needed for fingerprint
                var functionFingerprints = await context.Set<Function>()
                    .Select(f => new
                    {
                        f.ProcessID,
                        f.FunctionPosition,
                        f.FunctionName,
                        f.FunctionDescription,
                        f.ActualValue,
                        f.BreakPoint,
                        f.Comments,
                        f.Index,
                        f.Pass_Fail_WEB3Operator,
                        f.WEB3Operator,
                        // All 30 params
                        f.Param1, f.Param2, f.Param3, f.Param4, f.Param5,
                        f.Param6, f.Param7, f.Param8, f.Param9, f.Param10,
                        f.Param11, f.Param12, f.Param13, f.Param14, f.Param15,
                        f.Param16, f.Param17, f.Param18, f.Param19, f.Param20,
                        f.Param21, f.Param22, f.Param23, f.Param24, f.Param25,
                        f.Param26, f.Param27, f.Param28, f.Param29, f.Param30
                    })
                    .ToListAsync();

                var result = new Dictionary<string, string>();
                foreach (var func in functionFingerprints)
                {
                    if (!func.ProcessID.HasValue || !func.FunctionPosition.HasValue) continue;

                    // Create unique key
                    string key = $"{func.ProcessID.Value}:{func.FunctionPosition.Value}";

                    // Create fingerprint: concat all fields that might change
                    string fingerprint = $"{func.FunctionName}|{func.FunctionDescription}|{func.ActualValue}|" +
                                       $"{func.BreakPoint}|{func.Comments}|{func.Index}|{func.Pass_Fail_WEB3Operator}|" +
                                       $"{func.WEB3Operator}|{func.Param1}|{func.Param2}|{func.Param3}|{func.Param4}|{func.Param5}|" +
                                       $"{func.Param6}|{func.Param7}|{func.Param8}|{func.Param9}|{func.Param10}|" +
                                       $"{func.Param11}|{func.Param12}|{func.Param13}|{func.Param14}|{func.Param15}|" +
                                       $"{func.Param16}|{func.Param17}|{func.Param18}|{func.Param19}|{func.Param20}|" +
                                       $"{func.Param21}|{func.Param22}|{func.Param23}|{func.Param24}|{func.Param25}|" +
                                       $"{func.Param26}|{func.Param27}|{func.Param28}|{func.Param29}|{func.Param30}";

                    result[key] = fingerprint;
                }

                return result;
            }
        }

        /// <summary>
        /// Detect changes in Process table (composite key: TestID + ProcessID)
        /// Returns (new, changed, deleted) process keys
        /// </summary>
        private ((int TestID, double ProcessID)[] newKeys, (int TestID, double ProcessID)[] changedKeys, (int TestID, double ProcessID)[] deletedKeys)
            DetectProcessChanges(Dictionary<string, string> oldFingerprints, Dictionary<string, string> newFingerprints)
        {
            var newKeys = new List<(int, double)>();
            var changedKeys = new List<(int, double)>();
            var deletedKeys = new List<(int, double)>();

            // Find new and changed processes
            foreach (var kvp in newFingerprints)
            {
                var parts = kvp.Key.Split(':');
                int testId = int.Parse(parts[0]);
                double processId = double.Parse(parts[1]);

                if (!oldFingerprints.ContainsKey(kvp.Key))
                {
                    // New process
                    newKeys.Add((testId, processId));
                }
                else if (oldFingerprints[kvp.Key] != kvp.Value)
                {
                    // Changed process
                    changedKeys.Add((testId, processId));
                }
            }

            // Find deleted processes
            foreach (var kvp in oldFingerprints)
            {
                if (!newFingerprints.ContainsKey(kvp.Key))
                {
                    var parts = kvp.Key.Split(':');
                    int testId = int.Parse(parts[0]);
                    double processId = double.Parse(parts[1]);
                    deletedKeys.Add((testId, processId));
                }
            }

            return (newKeys.ToArray(), changedKeys.ToArray(), deletedKeys.ToArray());
        }

        /// <summary>
        /// Detect changes in Function table (key: ProcessID:FunctionPosition)
        /// Returns (new, changed, deleted) function keys as (processId, functionPosition)
        /// </summary>
        private ((double processId, int functionPosition)[] newKeys, (double processId, int functionPosition)[] changedKeys, (double processId, int functionPosition)[] deletedKeys)
            DetectFunctionChanges(Dictionary<string, string> oldFingerprints, Dictionary<string, string> newFingerprints)
        {
            var newKeys = new List<(double, int)>();
            var changedKeys = new List<(double, int)>();
            var deletedKeys = new List<(double, int)>();

            // Find new and changed functions
            foreach (var kvp in newFingerprints)
            {
                var parts = kvp.Key.Split(':');
                double processId = double.Parse(parts[0]);
                int functionPosition = int.Parse(parts[1]);

                if (!oldFingerprints.ContainsKey(kvp.Key))
                {
                    // New function
                    newKeys.Add((processId, functionPosition));
                }
                else if (oldFingerprints[kvp.Key] != kvp.Value)
                {
                    // Changed function
                    changedKeys.Add((processId, functionPosition));
                }
            }

            // Find deleted functions
            foreach (var kvp in oldFingerprints)
            {
                if (!newFingerprints.ContainsKey(kvp.Key))
                {
                    var parts = kvp.Key.Split(':');
                    double processId = double.Parse(parts[0]);
                    int functionPosition = int.Parse(parts[1]);
                    deletedKeys.Add((processId, functionPosition));
                }
            }

            return (newKeys.ToArray(), changedKeys.ToArray(), deletedKeys.ToArray());
        }

        /// <summary>
        /// Load ONLY the changed tests (not all 700!)
        /// </summary>
        private async Task<List<Test>> LoadChangedTestsAsync(List<int> newIds, List<int> changedIds)
        {
            var allIds = newIds.Concat(changedIds).Distinct().ToList();
            if (allIds.Count == 0)
                return new List<Test>();

            return await _repository.GetTestsByIdsAsync(allIds);
        }

        /// <summary>
        /// Load ONLY the changed processes by their composite keys
        /// </summary>
        private async Task<List<Process>> LoadChangedProcessesAsync(
            List<(int TestID, double ProcessID)> newKeys,
            List<(int TestID, double ProcessID)> changedKeys)
        {
            var allKeys = newKeys.Concat(changedKeys).Distinct().ToList();
            if (allKeys.Count == 0)
                return new List<Process>();

            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                System.Diagnostics.Debug.WriteLine($"⏳ Loading {allKeys.Count} specific processes...");

                // Build query for specific processes
                var processes = new List<Process>();
                foreach (var key in allKeys)
                {
                    var process = await context.Set<Process>()
                        .FirstOrDefaultAsync(p => p.TestID == key.TestID && p.ProcessID == key.ProcessID);

                    if (process != null)
                    {
                        // Initialize empty functions collection
                        process.Functions = new ObservableCollection<Function>();
                        process.AreFunctionsLoaded = false;
                        processes.Add(process);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {processes.Count} processes (differential update)");
                return processes;
            }
        }

        /// <summary>
        /// Load ONLY the changed functions by their keys, grouped by ProcessID
        /// </summary>
        private async Task<Dictionary<double, List<Function>>> LoadChangedFunctionsAsync(
            List<(double processId, int functionPosition)> newKeys,
            List<(double processId, int functionPosition)> changedKeys)
        {
            var allKeys = newKeys.Concat(changedKeys).Distinct().ToList();
            if (allKeys.Count == 0)
                return new Dictionary<double, List<Function>>();

            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                System.Diagnostics.Debug.WriteLine($"⏳ Loading {allKeys.Count} specific functions...");

                // Build query for specific functions
                var functions = new List<Function>();
                foreach (var key in allKeys)
                {
                    var function = await context.Set<Function>()
                        .FirstOrDefaultAsync(f => f.ProcessID == key.processId && f.FunctionPosition == key.functionPosition);

                    if (function != null)
                        functions.Add(function);
                }

                // Group by ProcessID
                var result = functions.GroupBy(f => f.ProcessID.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {functions.Count} functions across {result.Count} processes (differential update)");
                return result;
            }
        }

        /// <summary>
        /// Generic change detection helper
        /// </summary>
        private (List<int> newIds, List<int> changedIds, List<int> deletedIds) DetectChanges(
            Dictionary<int, string> oldFingerprints,
            Dictionary<int, string> newFingerprints)
        {
            var newIds = new List<int>();
            var changedIds = new List<int>();
            var deletedIds = new List<int>();

            // Find new and changed tests
            foreach (var kvp in newFingerprints)
            {
                if (!oldFingerprints.ContainsKey(kvp.Key))
                {
                    // New test
                    newIds.Add(kvp.Key);
                }
                else if (oldFingerprints[kvp.Key] != kvp.Value)
                {
                    // Changed test
                    changedIds.Add(kvp.Key);
                }
            }

            // Find deleted tests
            foreach (var kvp in oldFingerprints)
            {
                if (!newFingerprints.ContainsKey(kvp.Key))
                {
                    deletedIds.Add(kvp.Key);
                }
            }

            return (newIds, changedIds, deletedIds);
        }
    }
}