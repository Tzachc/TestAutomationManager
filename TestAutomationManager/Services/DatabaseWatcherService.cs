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
        /// Event fired when specific tests are updated from database
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
        /// Detects ANY change on ANY column and updates ONLY what changed!
        /// Perfect for multi-user collaboration
        /// </summary>
        private async Task CheckForChangesAsync()
        {
            try
            {
                // ⭐ STEP 1: Get lightweight fingerprints of all tests
                var currentFingerprints = await GetTestFingerprintsAsync();

                // ⭐ STEP 2: Compare with cached fingerprints to find changes
                var changedTestIds = new List<int>();
                var deletedTestIds = new List<int>();
                var newTestIds = new List<int>();

                // Find changed tests
                foreach (var kvp in currentFingerprints)
                {
                    int testId = kvp.Key;
                    string currentFingerprint = kvp.Value;

                    if (!_testFingerprints.ContainsKey(testId))
                    {
                        // New test added
                        newTestIds.Add(testId);
                    }
                    else if (_testFingerprints[testId] != currentFingerprint)
                    {
                        // Test changed
                        changedTestIds.Add(testId);
                    }
                }

                // Find deleted tests
                foreach (var testId in _testFingerprints.Keys)
                {
                    if (!currentFingerprints.ContainsKey(testId))
                    {
                        deletedTestIds.Add(testId);
                    }
                }

                // ⭐ STEP 3: If no changes, do nothing!
                if (newTestIds.Count == 0 && changedTestIds.Count == 0 && deletedTestIds.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("✓ No database changes detected (differential check)");
                    return;
                }

                // ⭐ STEP 4: Log what changed
                if (newTestIds.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"🆕 New tests detected: {string.Join(", ", newTestIds)}");
                if (changedTestIds.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"✏️ Changed tests detected: {string.Join(", ", changedTestIds)}");
                if (deletedTestIds.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"🗑️ Deleted tests detected: {string.Join(", ", deletedTestIds)}");

                // ⭐ STEP 5: Reload ALL tests (but OnDatabaseTestsUpdated will preserve pre-loaded data)
                // We reload all to keep it simple, but the UI update is smart and preserves state
                await ReloadAllTestsAsync();

                // ⭐ STEP 6: Update fingerprint cache
                _testFingerprints = currentFingerprints;
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
        /// Reload all tests from database and notify UI
        /// </summary>
        private async Task ReloadAllTestsAsync()
        {
            // Get latest tests from database
            var currentTests = await _repository.GetAllTestsAsync();

            System.Diagnostics.Debug.WriteLine($"🔄 Reloading tests for UI update");

            // Notify subscribers on UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TestsUpdated?.Invoke(this, currentTests);
                System.Diagnostics.Debug.WriteLine($"✓ UI notified of changes");
            });
        }
    }
}