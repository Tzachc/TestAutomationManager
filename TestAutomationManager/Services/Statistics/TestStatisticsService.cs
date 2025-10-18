using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TestAutomationManager.Models;

namespace TestAutomationManager.Services.Statistics
{
    /// <summary>
    /// Service for tracking and updating test statistics in real-time
    /// </summary>
    public class TestStatisticsService : INotifyPropertyChanged
    {
        private static TestStatisticsService _instance;
        private int _activeCount;
        private int _passedCount;
        private int _failedCount;
        private int _runningCount;

        public static TestStatisticsService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TestStatisticsService();
                return _instance;
            }
        }

        private TestStatisticsService()
        {
            // Initialize with default values
            _activeCount = 0;
            _passedCount = 0;
            _failedCount = 0;
            _runningCount = 0;
        }

        #region Properties

        public int ActiveCount
        {
            get => _activeCount;
            set
            {
                if (_activeCount != value)
                {
                    _activeCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PassedCount
        {
            get => _passedCount;
            set
            {
                if (_passedCount != value)
                {
                    _passedCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int FailedCount
        {
            get => _failedCount;
            set
            {
                if (_failedCount != value)
                {
                    _failedCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RunningCount
        {
            get => _runningCount;
            set
            {
                if (_runningCount != value)
                {
                    _runningCount = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Update statistics from a collection of tests
        /// </summary>
        public void UpdateStatistics(IEnumerable<Test> tests)
        {
            if (tests == null)
            {
                ActiveCount = 0;
                PassedCount = 0;
                FailedCount = 0;
                RunningCount = 0;
                return;
            }

            var testList = tests.ToList();

            // Count active tests
            ActiveCount = testList.Count(t => t.IsActive);

            // Count by status
            PassedCount = testList.Count(t => t.Status?.Equals("Passed", StringComparison.OrdinalIgnoreCase) == true);
            FailedCount = testList.Count(t => t.Status?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true);
            RunningCount = testList.Count(t => t.Status?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Increment active count
        /// </summary>
        public void IncrementActive()
        {
            ActiveCount++;
        }

        /// <summary>
        /// Decrement active count
        /// </summary>
        public void DecrementActive()
        {
            if (ActiveCount > 0)
                ActiveCount--;
        }

        /// <summary>
        /// Update status count when a test status changes
        /// </summary>
        public void UpdateStatusCount(string oldStatus, string newStatus)
        {
            // Decrement old status
            if (!string.IsNullOrEmpty(oldStatus))
            {
                if (oldStatus.Equals("Passed", StringComparison.OrdinalIgnoreCase) && PassedCount > 0)
                    PassedCount--;
                else if (oldStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase) && FailedCount > 0)
                    FailedCount--;
                else if (oldStatus.Equals("Running", StringComparison.OrdinalIgnoreCase) && RunningCount > 0)
                    RunningCount--;
            }

            // Increment new status
            if (!string.IsNullOrEmpty(newStatus))
            {
                if (newStatus.Equals("Passed", StringComparison.OrdinalIgnoreCase))
                    PassedCount++;
                else if (newStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                    FailedCount++;
                else if (newStatus.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    RunningCount++;
            }
        }

        /// <summary>
        /// Reset all statistics
        /// </summary>
        public void Reset()
        {
            ActiveCount = 0;
            PassedCount = 0;
            FailedCount = 0;
            RunningCount = 0;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}