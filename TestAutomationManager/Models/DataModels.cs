using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TestAutomationManager.Services.Statistics;

namespace TestAutomationManager.Models
{
    public class Test : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _description;
        private string _category;
        private bool _isActive;
        private DateTime _lastRun;
        private string _status;
        private bool _isExpanded;
        private ObservableCollection<Process> _processes;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();

                    // ⭐ AUTO-SAVE TO DATABASE (removed increment/decrement - will be recalculated from data)
                    SaveToDatabase();
                }
            }
        }

        public DateTime LastRun
        {
            get => _lastRun;
            set { _lastRun = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();

                    // ⭐ AUTO-SAVE TO DATABASE (removed UpdateStatusCount - will be recalculated from data)
                    SaveToDatabase();
                }
            }
        }

        /// <summary>
        /// Save changes to database automatically (async fire-and-forget)
        /// Updates the hash to prevent immediate reload after save
        /// </summary>
        private async void SaveToDatabase()
        {
            try
            {
                var repository = new TestAutomationManager.Repositories.TestRepository();
                await repository.UpdateTestAsync(this);
                System.Diagnostics.Debug.WriteLine($"✓ Auto-saved Test #{Id} to database");

                // ⭐ Force immediate check to update hash (prevents unnecessary reload)
                // This tells the watcher "yes, data changed, but I already know about it"
                await TestAutomationManager.Services.DatabaseWatcherService.Instance.ForceCheckAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to auto-save Test #{Id}: {ex.Message}");
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Process> Processes
        {
            get => _processes ?? (_processes = new ObservableCollection<Process>());
            set { _processes = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Process : INotifyPropertyChanged
    {
        private int _id;
        private int _testId;
        private string _name;
        private string _description;
        private int _sequence;
        private bool _isCritical;
        private double _timeout;
        private bool _isExpanded;
        private ObservableCollection<Function> _functions;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int TestId
        {
            get => _testId;
            set { _testId = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public int Sequence
        {
            get => _sequence;
            set { _sequence = value; OnPropertyChanged(); }
        }

        public bool IsCritical
        {
            get => _isCritical;
            set { _isCritical = value; OnPropertyChanged(); }
        }

        public double Timeout
        {
            get => _timeout;
            set { _timeout = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Function> Functions
        {
            get => _functions ?? (_functions = new ObservableCollection<Function>());
            set { _functions = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Function : INotifyPropertyChanged
    {
        private int _id;
        private int _processId;
        private string _name;
        private string _methodName;
        private string _parameters;
        private string _expectedResult;
        private int _sequence;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int ProcessId
        {
            get => _processId;
            set { _processId = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string MethodName
        {
            get => _methodName;
            set { _methodName = value; OnPropertyChanged(); }
        }

        public string Parameters
        {
            get => _parameters;
            set { _parameters = value; OnPropertyChanged(); }
        }

        public string ExpectedResult
        {
            get => _expectedResult;
            set { _expectedResult = value; OnPropertyChanged(); }
        }

        public int Sequence
        {
            get => _sequence;
            set { _sequence = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}