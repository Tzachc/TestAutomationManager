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
    /// Monitors database for external changes and updates UI
    /// Uses LIGHTWEIGHT polling - only checks counts/timestamps, not full data
    /// Only reloads when actual changes detected to avoid disrupting user experience
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
        /// Lightweight snapshot of database state (counts only)
        /// </summary>
        private int _lastTestCount = -1;
        private string _lastTestsFingerprint = string.Empty;

        /// <summary>
        /// Event fired when tests are updated from database
        /// </summary>
        public event EventHandler<List<Test>> TestsUpdated;

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// Polling interval in seconds (default: 30 seconds - much less aggressive!)
        /// </summary>
        public int PollingIntervalSeconds { get; set; } = 30;

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
        /// Check database for changes using LIGHTWEIGHT detection
        /// Only loads full data if actually changed!
        /// </summary>
        private async Task CheckForChangesAsync()
        {
            try
            {
                // ⭐ STEP 1: LIGHTWEIGHT CHECK - Fast query to detect changes
                var snapshot = await GetDatabaseSnapshotAsync();

                // Check if count changed
                if (snapshot.TestCount != _lastTestCount)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 Test count changed: {_lastTestCount} → {snapshot.TestCount}");
                    await ReloadDataAsync();
                    _lastTestCount = snapshot.TestCount;
                    _lastTestsFingerprint = snapshot.Fingerprint;
                    return;
                }

                // Check if data fingerprint changed (based on LastRunning timestamps)
                if (snapshot.Fingerprint != _lastTestsFingerprint)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 Test data changed (fingerprint mismatch)");
                    await ReloadDataAsync();
                    _lastTestsFingerprint = snapshot.Fingerprint;
                    return;
                }

                // No changes detected - no reload needed!
                System.Diagnostics.Debug.WriteLine("✓ No database changes detected (lightweight check)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking database: {ex.Message}");
            }
        }

        /// <summary>
        /// Get lightweight snapshot of database state (COUNT + fingerprint)
        /// This is MUCH faster than loading all data!
        /// </summary>
        private async Task<DatabaseSnapshot> GetDatabaseSnapshotAsync()
        {
            using (var context = new TestAutomationManager.Data.TestAutomationDbContext())
            {
                // Count tests
                int testCount = await context.Tests.CountAsync();

                // Get fingerprint based on most recent LastRunning timestamp
                // This detects when tests are executed/updated
                var mostRecentUpdate = await context.Tests
                    .OrderByDescending(t => t.LastRunning)
                    .Select(t => t.LastRunning)
                    .FirstOrDefaultAsync();

                string fingerprint = $"{testCount}:{mostRecentUpdate}";

                return new DatabaseSnapshot
                {
                    TestCount = testCount,
                    Fingerprint = fingerprint
                };
            }
        }

        /// <summary>
        /// Reload full data from database and notify UI
        /// Only called when changes actually detected
        /// </summary>
        private async Task ReloadDataAsync()
        {
            // Get latest tests from database
            var currentTests = await _repository.GetAllTestsAsync();

            System.Diagnostics.Debug.WriteLine($"🔄 Database changes detected - reloading {currentTests.Count} tests");

            // Notify subscribers on UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TestsUpdated?.Invoke(this, currentTests);
                System.Diagnostics.Debug.WriteLine($"✓ UI updated with {currentTests.Count} tests");
            });
        }

        /// <summary>
        /// Lightweight database state snapshot
        /// </summary>
        private class DatabaseSnapshot
        {
            public int TestCount { get; set; }
            public string Fingerprint { get; set; }
        }
    }
}