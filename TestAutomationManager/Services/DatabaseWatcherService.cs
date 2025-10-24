using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Monitors database for external changes and updates UI
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
        /// Store hash of current data to detect changes
        /// </summary>
        private string _lastDataHash;

        /// <summary>
        /// Event fired when tests are updated from database
        /// </summary>
        public event EventHandler<List<Test>> TestsUpdated;

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// Polling interval in seconds (default: 3 seconds)
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
            _lastDataHash = string.Empty;
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
        /// Check database for changes - only reload if data actually changed
        /// </summary>
        private async Task CheckForChangesAsync()
        {
            try
            {
                // Get latest tests from database
                var currentTests = await _repository.GetAllTestsAsync();

                // Calculate hash of current data state
                string currentHash = CalculateDataHash(currentTests);

                // Compare with last known state
                if (currentHash == _lastDataHash)
                {
                    // No changes detected - don't reload
                    return;
                }

                // Data has changed - update hash and notify
                _lastDataHash = currentHash;

                System.Diagnostics.Debug.WriteLine($"🔄 Database changes detected!");

                // Notify subscribers on UI thread
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    TestsUpdated?.Invoke(this, currentTests);
                    System.Diagnostics.Debug.WriteLine($"✓ UI updated with {currentTests.Count} tests");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error checking database: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate a hash representing the current state of data
        /// Used to detect if anything changed
        /// </summary>
        private string CalculateDataHash(List<Test> tests)
        {
            if (tests == null || tests.Count == 0)
                return string.Empty;

            // Create a simple hash from key properties
            // Format: "TestID:TestName:IsActive:RunStatus|TestID:TestName:IsActive:RunStatus|..."
            var hashString = string.Join("|", tests.Select(t =>
                $"{t.TestID}:{t.TestName}:{t.IsActive}:{t.RunStatus}:{t.Processes.Count}"
            ));

            // Return the hash string
            return hashString;
        }
    }
}