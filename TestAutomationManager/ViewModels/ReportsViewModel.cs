using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;
using TestAutomationManager.Services;

namespace TestAutomationManager.ViewModels
{
    public class ReportsViewModel : INotifyPropertyChanged
    {
        private readonly TestRepository _testRepository;

        // Summary Statistics
        private int _totalTests;
        private int _passedTests;
        private int _failedTests;
        private int _runningTests;
        private int _activeTests;
        private double _passPercentage;
        private double _failPercentage;
        private bool _isLoading;
        private string _statusMessage;
        private string _currentSchema;

        // Chart Data Collections
        private ObservableCollection<StatusChartData> _statusChartData;
        private ObservableCollection<CategoryStat> _categoryStatistics;
        private ObservableCollection<DailyTrend> _dailyTrends;
        private ObservableCollection<TopFailure> _topFailures;
        private ObservableCollection<RecentActivity> _recentActivities;

        public event PropertyChangedEventHandler PropertyChanged;

        // Properties
        public int TotalTests
        {
            get => _totalTests;
            set { _totalTests = value; OnPropertyChanged(); }
        }

        public int PassedTests
        {
            get => _passedTests;
            set { _passedTests = value; OnPropertyChanged(); }
        }

        public int FailedTests
        {
            get => _failedTests;
            set { _failedTests = value; OnPropertyChanged(); }
        }

        public int RunningTests
        {
            get => _runningTests;
            set { _runningTests = value; OnPropertyChanged(); }
        }

        public int ActiveTests
        {
            get => _activeTests;
            set { _activeTests = value; OnPropertyChanged(); }
        }

        public double PassPercentage
        {
            get => _passPercentage;
            set { _passPercentage = value; OnPropertyChanged(); }
        }

        public double FailPercentage
        {
            get => _failPercentage;
            set { _failPercentage = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string CurrentSchema
        {
            get => _currentSchema;
            set { _currentSchema = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StatusChartData> StatusChartData
        {
            get => _statusChartData;
            set { _statusChartData = value; OnPropertyChanged(); }
        }

        public ObservableCollection<CategoryStat> CategoryStatistics
        {
            get => _categoryStatistics;
            set { _categoryStatistics = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DailyTrend> DailyTrends
        {
            get => _dailyTrends;
            set { _dailyTrends = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TopFailure> TopFailures
        {
            get => _topFailures;
            set { _topFailures = value; OnPropertyChanged(); }
        }

        public ObservableCollection<RecentActivity> RecentActivities
        {
            get => _recentActivities;
            set { _recentActivities = value; OnPropertyChanged(); }
        }

        public ReportsViewModel()
        {
            _testRepository = new TestRepository();

            StatusChartData = new ObservableCollection<StatusChartData>();
            CategoryStatistics = new ObservableCollection<CategoryStat>();
            DailyTrends = new ObservableCollection<DailyTrend>();
            TopFailures = new ObservableCollection<TopFailure>();
            RecentActivities = new ObservableCollection<RecentActivity>();

            CurrentSchema = SchemaConfigService.Instance.CurrentSchema;

            // Subscribe to schema changes
            SchemaConfigService.Instance.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(SchemaConfigService.CurrentSchema))
                {
                    CurrentSchema = SchemaConfigService.Instance.CurrentSchema;
                    await LoadReportsAsync();
                }
            };
        }

        public async Task LoadReportsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading reports...";

            try
            {
                await Task.Run(async () =>
                {
                    var tests = await _testRepository.GetAllTestsAsync();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CalculateSummaryStatistics(tests);
                        CalculateStatusChartData(tests);
                        CalculateCategoryStatistics(tests);
                        CalculateDailyTrends(tests);
                        CalculateTopFailures(tests);
                        CalculateRecentActivity(tests);
                    });
                });

                StatusMessage = $"Reports loaded successfully - {TotalTests} tests analyzed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading reports: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading reports: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateSummaryStatistics(List<Test> tests)
        {
            TotalTests = tests.Count;
            PassedTests = tests.Count(t => string.Equals(t.RunStatus, "PASS", StringComparison.OrdinalIgnoreCase));
            FailedTests = tests.Count(t => string.Equals(t.RunStatus, "FAIL", StringComparison.OrdinalIgnoreCase));
            RunningTests = tests.Count(t => string.Equals(t.RunStatus, "RUNNING", StringComparison.OrdinalIgnoreCase));
            ActiveTests = tests.Count(t => t.IsActive);

            PassPercentage = TotalTests > 0 ? Math.Round((double)PassedTests / TotalTests * 100, 1) : 0;
            FailPercentage = TotalTests > 0 ? Math.Round((double)FailedTests / TotalTests * 100, 1) : 0;
        }

        private void CalculateStatusChartData(List<Test> tests)
        {
            StatusChartData.Clear();

            var statusGroups = tests
                .GroupBy(t => t.RunStatus ?? "Unknown")
                .Select(g => new StatusChartData
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Percentage = TotalTests > 0 ? Math.Round((double)g.Count() / TotalTests * 100, 1) : 0,
                    Color = GetStatusColor(g.Key)
                })
                .OrderByDescending(s => s.Count);

            foreach (var status in statusGroups)
            {
                StatusChartData.Add(status);
            }
        }

        private void CalculateCategoryStatistics(List<Test> tests)
        {
            CategoryStatistics.Clear();

            var categoryGroups = tests
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category)
                .Select(g => new CategoryStat
                {
                    Category = g.Key,
                    Total = g.Count(),
                    Passed = g.Count(t => string.Equals(t.RunStatus, "PASS", StringComparison.OrdinalIgnoreCase)),
                    Failed = g.Count(t => string.Equals(t.RunStatus, "FAIL", StringComparison.OrdinalIgnoreCase)),
                    Running = g.Count(t => string.Equals(t.RunStatus, "RUNNING", StringComparison.OrdinalIgnoreCase)),
                    SuccessRate = 0
                })
                .OrderByDescending(c => c.Total);

            foreach (var category in categoryGroups)
            {
                var totalCompleted = category.Passed + category.Failed;
                category.SuccessRate = totalCompleted > 0
                    ? Math.Round((double)category.Passed / totalCompleted * 100, 1)
                    : 0;

                CategoryStatistics.Add(category);
            }
        }

        private void CalculateDailyTrends(List<Test> tests)
        {
            DailyTrends.Clear();

            var testsWithDates = tests
                .Where(t => !string.IsNullOrWhiteSpace(t.LastRunning) && DateTime.TryParse(t.LastRunning, out _))
                .Select(t => new { Test = t, Date = DateTime.Parse(t.LastRunning!).Date })
                .ToList();

            if (!testsWithDates.Any())
                return;

            var last30Days = Enumerable.Range(0, 30)
                .Select(i => DateTime.Today.AddDays(-29 + i))
                .ToList();

            var dailyGroups = testsWithDates
                .GroupBy(t => t.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var date in last30Days)
            {
                if (dailyGroups.TryGetValue(date, out var testsOnDate))
                {
                    var passed = testsOnDate.Count(t => string.Equals(t.Test.RunStatus, "PASS", StringComparison.OrdinalIgnoreCase));
                    var failed = testsOnDate.Count(t => string.Equals(t.Test.RunStatus, "FAIL", StringComparison.OrdinalIgnoreCase));
                    var total = testsOnDate.Count;

                    DailyTrends.Add(new DailyTrend
                    {
                        Date = date,
                        DateDisplay = date.ToString("MM/dd"),
                        TotalRuns = total,
                        PassedRuns = passed,
                        FailedRuns = failed,
                        SuccessRate = total > 0 ? Math.Round((double)passed / total * 100, 1) : 0
                    });
                }
                else
                {
                    DailyTrends.Add(new DailyTrend
                    {
                        Date = date,
                        DateDisplay = date.ToString("MM/dd"),
                        TotalRuns = 0,
                        PassedRuns = 0,
                        FailedRuns = 0,
                        SuccessRate = 0
                    });
                }
            }
        }

        private void CalculateTopFailures(List<Test> tests)
        {
            TopFailures.Clear();

            var failedTests = tests
                .Where(t => string.Equals(t.RunStatus, "FAIL", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.TestName)
                .Take(10)
                .Select(t => new TopFailure
                {
                    TestName = t.TestName ?? "Unknown",
                    TestId = t.TestID?.ToString() ?? "N/A",
                    Category = string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category,
                    LastFailed = string.IsNullOrWhiteSpace(t.LastRunning) ? "Unknown" :
                        DateTime.TryParse(t.LastRunning, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm") : t.LastRunning,
                    ErrorMessage = string.IsNullOrWhiteSpace(t.ExceptionMessage)
                        ? "No error message"
                        : (t.ExceptionMessage.Length > 100
                            ? t.ExceptionMessage.Substring(0, 100) + "..."
                            : t.ExceptionMessage)
                });

            foreach (var failure in failedTests)
            {
                TopFailures.Add(failure);
            }
        }

        private void CalculateRecentActivity(List<Test> tests)
        {
            RecentActivities.Clear();

            var recentTests = tests
                .Where(t => !string.IsNullOrWhiteSpace(t.LastRunning) && DateTime.TryParse(t.LastRunning, out _))
                .OrderByDescending(t => DateTime.Parse(t.LastRunning!))
                .Take(15)
                .Select(t => new RecentActivity
                {
                    TestName = t.TestName ?? "Unknown",
                    Status = t.RunStatus ?? "Unknown",
                    Timestamp = DateTime.Parse(t.LastRunning!),
                    TimeDisplay = DateTime.Parse(t.LastRunning!).ToString("yyyy-MM-dd HH:mm:ss"),
                    Category = string.IsNullOrWhiteSpace(t.Category) ? "Uncategorized" : t.Category,
                    StatusColor = GetStatusColor(t.RunStatus ?? "Unknown")
                });

            foreach (var activity in recentTests)
            {
                RecentActivities.Add(activity);
            }
        }

        private string GetStatusColor(string status)
        {
            return status.ToUpperInvariant() switch
            {
                "PASS" => "#4CAF50",      // Green
                "FAIL" => "#F44336",      // Red
                "RUNNING" => "#FF9800",   // Orange
                "ACTIVE" => "#2196F3",    // Blue
                _ => "#9E9E9E"            // Gray
            };
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Data models for charts and statistics
    public class StatusChartData : INotifyPropertyChanged
    {
        private string _status;
        private int _count;
        private double _percentage;
        private string _color;

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CategoryStat : INotifyPropertyChanged
    {
        private string _category;
        private int _total;
        private int _passed;
        private int _failed;
        private int _running;
        private double _successRate;

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public int Total
        {
            get => _total;
            set { _total = value; OnPropertyChanged(); }
        }

        public int Passed
        {
            get => _passed;
            set { _passed = value; OnPropertyChanged(); }
        }

        public int Failed
        {
            get => _failed;
            set { _failed = value; OnPropertyChanged(); }
        }

        public int Running
        {
            get => _running;
            set { _running = value; OnPropertyChanged(); }
        }

        public double SuccessRate
        {
            get => _successRate;
            set { _successRate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DailyTrend : INotifyPropertyChanged
    {
        private DateTime _date;
        private string _dateDisplay;
        private int _totalRuns;
        private int _passedRuns;
        private int _failedRuns;
        private double _successRate;

        public DateTime Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(); }
        }

        public string DateDisplay
        {
            get => _dateDisplay;
            set { _dateDisplay = value; OnPropertyChanged(); }
        }

        public int TotalRuns
        {
            get => _totalRuns;
            set { _totalRuns = value; OnPropertyChanged(); }
        }

        public int PassedRuns
        {
            get => _passedRuns;
            set { _passedRuns = value; OnPropertyChanged(); }
        }

        public int FailedRuns
        {
            get => _failedRuns;
            set { _failedRuns = value; OnPropertyChanged(); }
        }

        public double SuccessRate
        {
            get => _successRate;
            set { _successRate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TopFailure : INotifyPropertyChanged
    {
        private string _testName;
        private string _testId;
        private string _category;
        private string _lastFailed;
        private string _errorMessage;

        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(); }
        }

        public string TestId
        {
            get => _testId;
            set { _testId = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string LastFailed
        {
            get => _lastFailed;
            set { _lastFailed = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RecentActivity : INotifyPropertyChanged
    {
        private string _testName;
        private string _status;
        private DateTime _timestamp;
        private string _timeDisplay;
        private string _category;
        private string _statusColor;

        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); }
        }

        public string TimeDisplay
        {
            get => _timeDisplay;
            set { _timeDisplay = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
