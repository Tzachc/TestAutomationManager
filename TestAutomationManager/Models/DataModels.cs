using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TestAutomationManager.Services.Statistics;

namespace TestAutomationManager.Models
{
    public class Test : INotifyPropertyChanged
    {
        // Database columns
        private int _testID;
        private DateTime? _lastRunning;
        private DateTime? _lastTimePassed;
        private string _runStatus;
        private string _bugs;
        private string _testName;
        private string _recipientsEmailsList;
        private string _exceptionMessage;
        private bool _sendEmailReport;
        private bool _exitTestOnFailure;
        private int _testRunAgainTimes;
        private bool _snapshotMultipleFailure;
        private bool _emailOnFailureOnly;
        private bool _disableKillDriver;

        // UI-only properties
        private bool _isExpanded;
        private ObservableCollection<Process> _processes;

        public int TestID
        {
            get => _testID;
            set { _testID = value; OnPropertyChanged(); }
        }

        public DateTime? LastRunning
        {
            get => _lastRunning;
            set { _lastRunning = value; OnPropertyChanged(); }
        }

        public DateTime? LastTimePassed
        {
            get => _lastTimePassed;
            set { _lastTimePassed = value; OnPropertyChanged(); }
        }

        public string RunStatus
        {
            get => _runStatus;
            set
            {
                if (_runStatus != value)
                {
                    _runStatus = value;
                    OnPropertyChanged();
                    SaveToDatabase();
                }
            }
        }

        public string Bugs
        {
            get => _bugs;
            set { _bugs = value; OnPropertyChanged(); }
        }

        public string TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(); }
        }

        public string RecipientsEmailsList
        {
            get => _recipientsEmailsList;
            set { _recipientsEmailsList = value; OnPropertyChanged(); }
        }

        public string ExceptionMessage
        {
            get => _exceptionMessage;
            set { _exceptionMessage = value; OnPropertyChanged(); }
        }

        public bool SendEmailReport
        {
            get => _sendEmailReport;
            set { _sendEmailReport = value; OnPropertyChanged(); }
        }

        public bool ExitTestOnFailure
        {
            get => _exitTestOnFailure;
            set { _exitTestOnFailure = value; OnPropertyChanged(); }
        }

        public int TestRunAgainTimes
        {
            get => _testRunAgainTimes;
            set { _testRunAgainTimes = value; OnPropertyChanged(); }
        }

        public bool SnapshotMultipleFailure
        {
            get => _snapshotMultipleFailure;
            set { _snapshotMultipleFailure = value; OnPropertyChanged(); }
        }

        public bool EmailOnFailureOnly
        {
            get => _emailOnFailureOnly;
            set { _emailOnFailureOnly = value; OnPropertyChanged(); }
        }

        public bool DisableKillDriver
        {
            get => _disableKillDriver;
            set { _disableKillDriver = value; OnPropertyChanged(); }
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
                System.Diagnostics.Debug.WriteLine($"✓ Auto-saved Test #{TestID} to database");

                // ⭐ Force immediate check to update hash (prevents unnecessary reload)
                // This tells the watcher "yes, data changed, but I already know about it"
                await TestAutomationManager.Services.DatabaseWatcherService.Instance.ForceCheckAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to auto-save Test #{TestID}: {ex.Message}");
            }
        }

        // UI-only property
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
        // Database columns
        private int _testID;
        private int _processID;
        private string _web3Operator;
        private string _pass_Fail;
        private DateTime? _lastRunning;
        private int _processPosition;
        private string _processName;

        // Parameters 1-40
        private string _param1; private string _param2; private string _param3; private string _param4; private string _param5;
        private string _param6; private string _param7; private string _param8; private string _param9; private string _param10;
        private string _param11; private string _param12; private string _param13; private string _param14; private string _param15;
        private string _param16; private string _param17; private string _param18; private string _param19; private string _param20;
        private string _param21; private string _param22; private string _param23; private string _param24; private string _param25;
        private string _param26; private string _param27; private string _param28; private string _param29; private string _param30;
        private string _param31; private string _param32; private string _param33; private string _param34; private string _param35;
        private string _param36; private string _param37; private string _param38; private string _param39; private string _param40;

        // UI-only properties
        private bool _isExpanded;
        private ObservableCollection<Function> _functions;

        public int TestID
        {
            get => _testID;
            set { _testID = value; OnPropertyChanged(); }
        }

        public int ProcessID
        {
            get => _processID;
            set { _processID = value; OnPropertyChanged(); }
        }

        public string WEB3Operator
        {
            get => _web3Operator;
            set { _web3Operator = value; OnPropertyChanged(); }
        }

        public string Pass_Fail
        {
            get => _pass_Fail;
            set { _pass_Fail = value; OnPropertyChanged(); }
        }

        public DateTime? LastRunning
        {
            get => _lastRunning;
            set { _lastRunning = value; OnPropertyChanged(); }
        }

        public int ProcessPosition
        {
            get => _processPosition;
            set { _processPosition = value; OnPropertyChanged(); }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        // Parameters 1-40
        public string Param1 { get => _param1; set { _param1 = value; OnPropertyChanged(); } }
        public string Param2 { get => _param2; set { _param2 = value; OnPropertyChanged(); } }
        public string Param3 { get => _param3; set { _param3 = value; OnPropertyChanged(); } }
        public string Param4 { get => _param4; set { _param4 = value; OnPropertyChanged(); } }
        public string Param5 { get => _param5; set { _param5 = value; OnPropertyChanged(); } }
        public string Param6 { get => _param6; set { _param6 = value; OnPropertyChanged(); } }
        public string Param7 { get => _param7; set { _param7 = value; OnPropertyChanged(); } }
        public string Param8 { get => _param8; set { _param8 = value; OnPropertyChanged(); } }
        public string Param9 { get => _param9; set { _param9 = value; OnPropertyChanged(); } }
        public string Param10 { get => _param10; set { _param10 = value; OnPropertyChanged(); } }
        public string Param11 { get => _param11; set { _param11 = value; OnPropertyChanged(); } }
        public string Param12 { get => _param12; set { _param12 = value; OnPropertyChanged(); } }
        public string Param13 { get => _param13; set { _param13 = value; OnPropertyChanged(); } }
        public string Param14 { get => _param14; set { _param14 = value; OnPropertyChanged(); } }
        public string Param15 { get => _param15; set { _param15 = value; OnPropertyChanged(); } }
        public string Param16 { get => _param16; set { _param16 = value; OnPropertyChanged(); } }
        public string Param17 { get => _param17; set { _param17 = value; OnPropertyChanged(); } }
        public string Param18 { get => _param18; set { _param18 = value; OnPropertyChanged(); } }
        public string Param19 { get => _param19; set { _param19 = value; OnPropertyChanged(); } }
        public string Param20 { get => _param20; set { _param20 = value; OnPropertyChanged(); } }
        public string Param21 { get => _param21; set { _param21 = value; OnPropertyChanged(); } }
        public string Param22 { get => _param22; set { _param22 = value; OnPropertyChanged(); } }
        public string Param23 { get => _param23; set { _param23 = value; OnPropertyChanged(); } }
        public string Param24 { get => _param24; set { _param24 = value; OnPropertyChanged(); } }
        public string Param25 { get => _param25; set { _param25 = value; OnPropertyChanged(); } }
        public string Param26 { get => _param26; set { _param26 = value; OnPropertyChanged(); } }
        public string Param27 { get => _param27; set { _param27 = value; OnPropertyChanged(); } }
        public string Param28 { get => _param28; set { _param28 = value; OnPropertyChanged(); } }
        public string Param29 { get => _param29; set { _param29 = value; OnPropertyChanged(); } }
        public string Param30 { get => _param30; set { _param30 = value; OnPropertyChanged(); } }
        public string Param31 { get => _param31; set { _param31 = value; OnPropertyChanged(); } }
        public string Param32 { get => _param32; set { _param32 = value; OnPropertyChanged(); } }
        public string Param33 { get => _param33; set { _param33 = value; OnPropertyChanged(); } }
        public string Param34 { get => _param34; set { _param34 = value; OnPropertyChanged(); } }
        public string Param35 { get => _param35; set { _param35 = value; OnPropertyChanged(); } }
        public string Param36 { get => _param36; set { _param36 = value; OnPropertyChanged(); } }
        public string Param37 { get => _param37; set { _param37 = value; OnPropertyChanged(); } }
        public string Param38 { get => _param38; set { _param38 = value; OnPropertyChanged(); } }
        public string Param39 { get => _param39; set { _param39 = value; OnPropertyChanged(); } }
        public string Param40 { get => _param40; set { _param40 = value; OnPropertyChanged(); } }

        // UI-only property
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
        // Database columns
        private int _processID;
        private string _web3Operator;
        private string _pass_Fail;
        private string _comments;
        private int _functionPosition;
        private string _functionDescription;
        private string _functionName;

        // Parameters 1-30
        private string _param1; private string _param2; private string _param3; private string _param4; private string _param5;
        private string _param6; private string _param7; private string _param8; private string _param9; private string _param10;
        private string _param11; private string _param12; private string _param13; private string _param14; private string _param15;
        private string _param16; private string _param17; private string _param18; private string _param19; private string _param20;
        private string _param21; private string _param22; private string _param23; private string _param24; private string _param25;
        private string _param26; private string _param27; private string _param28; private string _param29; private string _param30;

        public int ProcessID
        {
            get => _processID;
            set { _processID = value; OnPropertyChanged(); }
        }

        public string WEB3Operator
        {
            get => _web3Operator;
            set { _web3Operator = value; OnPropertyChanged(); }
        }

        public string Pass_Fail
        {
            get => _pass_Fail;
            set { _pass_Fail = value; OnPropertyChanged(); }
        }

        public string Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(); }
        }

        public int FunctionPosition
        {
            get => _functionPosition;
            set { _functionPosition = value; OnPropertyChanged(); }
        }

        public string FunctionDescription
        {
            get => _functionDescription;
            set { _functionDescription = value; OnPropertyChanged(); }
        }

        public string FunctionName
        {
            get => _functionName;
            set { _functionName = value; OnPropertyChanged(); }
        }

        // Parameters 1-30
        public string Param1 { get => _param1; set { _param1 = value; OnPropertyChanged(); } }
        public string Param2 { get => _param2; set { _param2 = value; OnPropertyChanged(); } }
        public string Param3 { get => _param3; set { _param3 = value; OnPropertyChanged(); } }
        public string Param4 { get => _param4; set { _param4 = value; OnPropertyChanged(); } }
        public string Param5 { get => _param5; set { _param5 = value; OnPropertyChanged(); } }
        public string Param6 { get => _param6; set { _param6 = value; OnPropertyChanged(); } }
        public string Param7 { get => _param7; set { _param7 = value; OnPropertyChanged(); } }
        public string Param8 { get => _param8; set { _param8 = value; OnPropertyChanged(); } }
        public string Param9 { get => _param9; set { _param9 = value; OnPropertyChanged(); } }
        public string Param10 { get => _param10; set { _param10 = value; OnPropertyChanged(); } }
        public string Param11 { get => _param11; set { _param11 = value; OnPropertyChanged(); } }
        public string Param12 { get => _param12; set { _param12 = value; OnPropertyChanged(); } }
        public string Param13 { get => _param13; set { _param13 = value; OnPropertyChanged(); } }
        public string Param14 { get => _param14; set { _param14 = value; OnPropertyChanged(); } }
        public string Param15 { get => _param15; set { _param15 = value; OnPropertyChanged(); } }
        public string Param16 { get => _param16; set { _param16 = value; OnPropertyChanged(); } }
        public string Param17 { get => _param17; set { _param17 = value; OnPropertyChanged(); } }
        public string Param18 { get => _param18; set { _param18 = value; OnPropertyChanged(); } }
        public string Param19 { get => _param19; set { _param19 = value; OnPropertyChanged(); } }
        public string Param20 { get => _param20; set { _param20 = value; OnPropertyChanged(); } }
        public string Param21 { get => _param21; set { _param21 = value; OnPropertyChanged(); } }
        public string Param22 { get => _param22; set { _param22 = value; OnPropertyChanged(); } }
        public string Param23 { get => _param23; set { _param23 = value; OnPropertyChanged(); } }
        public string Param24 { get => _param24; set { _param24 = value; OnPropertyChanged(); } }
        public string Param25 { get => _param25; set { _param25 = value; OnPropertyChanged(); } }
        public string Param26 { get => _param26; set { _param26 = value; OnPropertyChanged(); } }
        public string Param27 { get => _param27; set { _param27 = value; OnPropertyChanged(); } }
        public string Param28 { get => _param28; set { _param28 = value; OnPropertyChanged(); } }
        public string Param29 { get => _param29; set { _param29 = value; OnPropertyChanged(); } }
        public string Param30 { get => _param30; set { _param30 = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    /// <summary>
    /// Represents metadata about an external table (actual SQL table)
    /// </summary>
    public class ExternalTableInfo
    {
        public int TestId { get; set; }
        public string TableName { get; set; }  // e.g., "ExtTable1"
        public string TestName { get; set; }   // e.g., "Login Test - Standard User"
        public int RowCount { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// Represents a row from any ExtTable (dynamic data)
    /// </summary>
    public class ExternalTableRow
    {
        public int Id { get; set; }
        public string IterationName { get; set; }
        public string Run { get; set; }
        public string ExcludeProcess { get; set; }
        public DateTime? LastTimePassed { get; set; }
        public string Exception { get; set; }
        public string Image { get; set; }
        public string RegistrationURL2 { get; set; }
        public string Username3 { get; set; }
        public string Password4 { get; set; }
        public string ConnStr75 { get; set; }
        public string SqlQueryResult { get; set; }
    }
}