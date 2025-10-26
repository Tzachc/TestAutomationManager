using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TestAutomationManager.Services.Statistics;

namespace TestAutomationManager.Models
{
    /// <summary>
    /// Test entity matching Test_WEB3 schema
    /// Maps to [PRODUCTION_Selenium].[Test_WEB3] table
    /// </summary>
    public class Test : INotifyPropertyChanged
    {
        private double? _testID;
        private string? _testName;
        private string? _bugs;
        private string? _disableKillDriver;
        private string? _emailOnFailureOnly;
        private string? _exceptionMessage;
        private string? _exitTestOnFailure;
        private string? _lastRunning;
        private string? _lastTimePass;
        private string? _recipientsEmailsList;
        private string? _runStatus;
        private string? _sendEmailReport;
        private string? _snapshotMultipleFailure;
        private string? _testRunAgainTimes;

        // UI-only properties
        private bool _isActive;
        private bool _isExpanded;
        private string? _category;
        private ObservableCollection<Process>? _processes;

        // ================================================
        // DATABASE COLUMNS
        // ================================================

        public double? TestID
        {
            get => _testID;
            set { _testID = value; OnPropertyChanged(); }
        }

        public string? TestName
        {
            get => _testName;
            set { _testName = value; OnPropertyChanged(); }
        }

        public string? Bugs
        {
            get => _bugs;
            set { _bugs = value; OnPropertyChanged(); }
        }

        public string? DisableKillDriver
        {
            get => _disableKillDriver;
            set { _disableKillDriver = value; OnPropertyChanged(); }
        }

        public string? EmailOnFailureOnly
        {
            get => _emailOnFailureOnly;
            set { _emailOnFailureOnly = value; OnPropertyChanged(); }
        }

        public string? ExceptionMessage
        {
            get => _exceptionMessage;
            set { _exceptionMessage = value; OnPropertyChanged(); }
        }

        public string? ExitTestOnFailure
        {
            get => _exitTestOnFailure;
            set { _exitTestOnFailure = value; OnPropertyChanged(); }
        }

        public string? LastRunning
        {
            get => _lastRunning;
            set { _lastRunning = value; OnPropertyChanged(); }
        }

        public string? LastTimePass
        {
            get => _lastTimePass;
            set { _lastTimePass = value; OnPropertyChanged(); }
        }

        public string? RecipientsEmailsList
        {
            get => _recipientsEmailsList;
            set { _recipientsEmailsList = value; OnPropertyChanged(); }
        }

        public string? RunStatus
        {
            get => _runStatus;
            set { _runStatus = value; OnPropertyChanged(); }
        }

        public string? SendEmailReport
        {
            get => _sendEmailReport;
            set { _sendEmailReport = value; OnPropertyChanged(); }
        }

        public string? SnapshotMultipleFailure
        {
            get => _snapshotMultipleFailure;
            set { _snapshotMultipleFailure = value; OnPropertyChanged(); }
        }

        public string? TestRunAgainTimes
        {
            get => _testRunAgainTimes;
            set { _testRunAgainTimes = value; OnPropertyChanged(); }
        }

        // ================================================
        // UI-ONLY PROPERTIES (for WPF)
        // ================================================

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    SaveIsActiveToSettings();
                }
            }
        }

        private async void SaveIsActiveToSettings()
        {
            try
            {
                if (TestID.HasValue)
                {
                    await TestAutomationManager.Services.TestUISettingsService.Instance
                        .SetIsActiveAsync((int)TestID.Value, _isActive);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to save IsActive for Test #{TestID}: {ex.Message}");
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
            set
            {
                if (_processes != value)
                {
                    _processes = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Category
        {
            get => _category ?? "General";
            set
            {
                if (_category != value)
                {
                    _category = value;
                    OnPropertyChanged();
                    SaveCategoryToSettings();
                }
            }
        }

        private async void SaveCategoryToSettings()
        {
            try
            {
                if (TestID.HasValue)
                {
                    await TestAutomationManager.Services.TestUISettingsService.Instance
                        .SetCategoryAsync((int)TestID.Value, _category);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Failed to save Category for Test #{TestID}: {ex.Message}");
            }
        }

        // ================================================
        // BACKWARD COMPATIBILITY (for existing UI logic)
        // ================================================

        public int Id
        {
            get => TestID.HasValue ? (int)TestID.Value : 0;
            set => TestID = value;
        }

        public string Name
        {
            get => TestName ?? string.Empty;
            set => TestName = value;
        }

        public string Status
        {
            get => RunStatus ?? string.Empty;
            set => RunStatus = value;
        }

        public string Description
        {
            get => ExceptionMessage ?? string.Empty;
            set => ExceptionMessage = value;
        }

        public DateTime LastRun
        {
            get
            {
                if (!string.IsNullOrEmpty(LastRunning) && DateTime.TryParse(LastRunning, out DateTime result))
                    return result;
                return DateTime.MinValue;
            }
            set => LastRunning = value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // ================================================
        // INotifyPropertyChanged
        // ================================================

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



    /// <summary>
    /// Process entity matching Process_WEB3 schema
    /// Maps to [PRODUCTION_Selenium].[Process_WEB3] table
    /// </summary>
    public class Process : INotifyPropertyChanged
    {
        private double? _testID;
        private string? _comments;
        private int? _index;
        private string? _lastRunning;
        private string? _module;
        private string? _passFailOperator;
        private double? _processID;
        private string? _processName;
        private double? _processPosition;
        private string? _repeat;
        private string? _tempParam;
        private string? _web3Operator;

        // TempParam extras
        private string? _tempParam1;
        private string? _tempParam11;
        private string? _tempParam111;
        private string? _tempParam1111;
        private string? _tempParam11111;
        private string? _tempParam111111;

        // Parameters
        private string?[] _params = new string?[46];

        private bool _isExpanded;
        private ObservableCollection<Function>? _functions;

        // ================================================
        // DATABASE COLUMNS
        // ================================================

        public double? TestID
        {
            get => _testID;
            set { _testID = value; OnPropertyChanged(); }
        }

        public string? Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(); }
        }

        public int? Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public string? LastRunning
        {
            get => _lastRunning;
            set { _lastRunning = value; OnPropertyChanged(); }
        }

        public string? Module
        {
            get => _module;
            set { _module = value; OnPropertyChanged(); }
        }

        public string? Pass_Fail_WEB3Operator
        {
            get => _passFailOperator;
            set { _passFailOperator = value; OnPropertyChanged(); }
        }

        public double? ProcessID
        {
            get => _processID;
            set { _processID = value; OnPropertyChanged(); }
        }

        public string? ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public double? ProcessPosition
        {
            get => _processPosition;
            set { _processPosition = value; OnPropertyChanged(); }
        }

        public string? Repeat
        {
            get => _repeat;
            set { _repeat = value; OnPropertyChanged(); }
        }

        public string? TempParam
        {
            get => _tempParam;
            set { _tempParam = value; OnPropertyChanged(); }
        }

        public string? WEB3Operator
        {
            get => _web3Operator;
            set { _web3Operator = value; OnPropertyChanged(); }
        }

        // ================================================
        // ADDITIONAL TEMP PARAMS
        // ================================================

        public string? TempParam1 { get => _tempParam1; set { _tempParam1 = value; OnPropertyChanged(); } }
        public string? TempParam11 { get => _tempParam11; set { _tempParam11 = value; OnPropertyChanged(); } }
        public string? TempParam111 { get => _tempParam111; set { _tempParam111 = value; OnPropertyChanged(); } }
        public string? TempParam1111 { get => _tempParam1111; set { _tempParam1111 = value; OnPropertyChanged(); } }
        public string? TempParam11111 { get => _tempParam11111; set { _tempParam11111 = value; OnPropertyChanged(); } }
        public string? TempParam111111 { get => _tempParam111111; set { _tempParam111111 = value; OnPropertyChanged(); } }

        // ================================================
        // PARAMS 1–46
        // ================================================

        public string? Param1 { get => _params[0]; set { _params[0] = value; OnPropertyChanged(); } }
        public string? Param2 { get => _params[1]; set { _params[1] = value; OnPropertyChanged(); } }
        public string? Param3 { get => _params[2]; set { _params[2] = value; OnPropertyChanged(); } }
        public string? Param4 { get => _params[3]; set { _params[3] = value; OnPropertyChanged(); } }
        public string? Param5 { get => _params[4]; set { _params[4] = value; OnPropertyChanged(); } }
        public string? Param6 { get => _params[5]; set { _params[5] = value; OnPropertyChanged(); } }
        public string? Param7 { get => _params[6]; set { _params[6] = value; OnPropertyChanged(); } }
        public string? Param8 { get => _params[7]; set { _params[7] = value; OnPropertyChanged(); } }
        public string? Param9 { get => _params[8]; set { _params[8] = value; OnPropertyChanged(); } }
        public string? Param10 { get => _params[9]; set { _params[9] = value; OnPropertyChanged(); } }
        public string? Param11 { get => _params[10]; set { _params[10] = value; OnPropertyChanged(); } }
        public string? Param12 { get => _params[11]; set { _params[11] = value; OnPropertyChanged(); } }
        public string? Param13 { get => _params[12]; set { _params[12] = value; OnPropertyChanged(); } }
        public string? Param14 { get => _params[13]; set { _params[13] = value; OnPropertyChanged(); } }
        public string? Param15 { get => _params[14]; set { _params[14] = value; OnPropertyChanged(); } }
        public string? Param16 { get => _params[15]; set { _params[15] = value; OnPropertyChanged(); } }
        public string? Param17 { get => _params[16]; set { _params[16] = value; OnPropertyChanged(); } }
        public string? Param18 { get => _params[17]; set { _params[17] = value; OnPropertyChanged(); } }
        public string? Param19 { get => _params[18]; set { _params[18] = value; OnPropertyChanged(); } }
        public string? Param20 { get => _params[19]; set { _params[19] = value; OnPropertyChanged(); } }
        public string? Param21 { get => _params[20]; set { _params[20] = value; OnPropertyChanged(); } }
        public string? Param22 { get => _params[21]; set { _params[21] = value; OnPropertyChanged(); } }
        public string? Param23 { get => _params[22]; set { _params[22] = value; OnPropertyChanged(); } }
        public string? Param24 { get => _params[23]; set { _params[23] = value; OnPropertyChanged(); } }
        public string? Param25 { get => _params[24]; set { _params[24] = value; OnPropertyChanged(); } }
        public string? Param26 { get => _params[25]; set { _params[25] = value; OnPropertyChanged(); } }
        public string? Param27 { get => _params[26]; set { _params[26] = value; OnPropertyChanged(); } }
        public string? Param28 { get => _params[27]; set { _params[27] = value; OnPropertyChanged(); } }
        public string? Param29 { get => _params[28]; set { _params[28] = value; OnPropertyChanged(); } }
        public string? Param30 { get => _params[29]; set { _params[29] = value; OnPropertyChanged(); } }
        public string? Param31 { get => _params[30]; set { _params[30] = value; OnPropertyChanged(); } }
        public string? Param32 { get => _params[31]; set { _params[31] = value; OnPropertyChanged(); } }
        public string? Param33 { get => _params[32]; set { _params[32] = value; OnPropertyChanged(); } }
        public string? Param34 { get => _params[33]; set { _params[33] = value; OnPropertyChanged(); } }
        public string? Param35 { get => _params[34]; set { _params[34] = value; OnPropertyChanged(); } }
        public string? Param36 { get => _params[35]; set { _params[35] = value; OnPropertyChanged(); } }
        public string? Param37 { get => _params[36]; set { _params[36] = value; OnPropertyChanged(); } }
        public string? Param38 { get => _params[37]; set { _params[37] = value; OnPropertyChanged(); } }
        public string? Param39 { get => _params[38]; set { _params[38] = value; OnPropertyChanged(); } }
        public string? Param40 { get => _params[39]; set { _params[39] = value; OnPropertyChanged(); } }
        public string? Param41 { get => _params[40]; set { _params[40] = value; OnPropertyChanged(); } }
        public string? Param42 { get => _params[41]; set { _params[41] = value; OnPropertyChanged(); } }
        public string? Param43 { get => _params[42]; set { _params[42] = value; OnPropertyChanged(); } }
        public string? Param44 { get => _params[43]; set { _params[43] = value; OnPropertyChanged(); } }
        public string? Param45 { get => _params[44]; set { _params[44] = value; OnPropertyChanged(); } }
        public string? Param46 { get => _params[45]; set { _params[45] = value; OnPropertyChanged(); } }

        // ================================================
        // UI ONLY
        // ================================================

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Function> Functions
        {
            get => _functions ??= new ObservableCollection<Function>();
            set { _functions = value; OnPropertyChanged(); }
        }

        // ================================================
        // INOTIFYPROPERTYCHANGED
        // ================================================

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ================================================
        // BACKWARD COMPATIBILITY (for repository + UI)
        // ================================================

        public int Id
        {
            get => (int)(ProcessID ?? 0);
            set => ProcessID = value;
        }

        public int TestId
        {
            get => (int)(TestID ?? 0);
            set => TestID = value;
        }

        public string Name
        {
            get => ProcessName ?? string.Empty;
            set => ProcessName = value;
        }

        public string Description
        {
            get => Comments ?? string.Empty;
            set => Comments = value;
        }

        public int Sequence
        {
            get => (int)(ProcessPosition ?? 0);
            set => ProcessPosition = value;
        }

        public bool IsCritical => false;
        public double Timeout => 30;

    }


    /// <summary>
    /// Function entity matching Function_WEB3 schema
    /// Maps to [PRODUCTION_Selenium].[Function_WEB3] table
    /// </summary>
    public class Function : INotifyPropertyChanged
    {
        private string? _actualValue;
        private string? _breakPoint;
        private string? _comments;
        private string? _functionDescription;
        private string? _functionName;
        private int? _functionPosition;
        private int? _index;
        private string? _passFailOperator;
        private double? _processID;
        private string? _web3Operator;

        private string?[] _params = new string?[30];

        // ================================================
        // DATABASE COLUMNS (Function_WEB3)
        // ================================================

        public string? ActualValue
        {
            get => _actualValue;
            set { _actualValue = value; OnPropertyChanged(); }
        }

        public string? BreakPoint
        {
            get => _breakPoint;
            set { _breakPoint = value; OnPropertyChanged(); }
        }

        public string? Comments
        {
            get => _comments;
            set { _comments = value; OnPropertyChanged(); }
        }

        public string? FunctionDescription
        {
            get => _functionDescription;
            set { _functionDescription = value; OnPropertyChanged(); }
        }

        public string? FunctionName
        {
            get => _functionName;
            set { _functionName = value; OnPropertyChanged(); }
        }

        public int? FunctionPosition
        {
            get => _functionPosition;
            set { _functionPosition = value; OnPropertyChanged(); }
        }

        public int? Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(); }
        }

        public string? Pass_Fail_WEB3Operator
        {
            get => _passFailOperator;
            set { _passFailOperator = value; OnPropertyChanged(); }
        }

        public double? ProcessID
        {
            get => _processID;
            set { _processID = value; OnPropertyChanged(); }
        }


        public string? WEB3Operator
        {
            get => _web3Operator;
            set { _web3Operator = value; OnPropertyChanged(); }
        }

        // ================================================
        // PARAMS 1–30
        // ================================================

        public string? Param1 { get => _params[0]; set { _params[0] = value; OnPropertyChanged(); } }
        public string? Param2 { get => _params[1]; set { _params[1] = value; OnPropertyChanged(); } }
        public string? Param3 { get => _params[2]; set { _params[2] = value; OnPropertyChanged(); } }
        public string? Param4 { get => _params[3]; set { _params[3] = value; OnPropertyChanged(); } }
        public string? Param5 { get => _params[4]; set { _params[4] = value; OnPropertyChanged(); } }
        public string? Param6 { get => _params[5]; set { _params[5] = value; OnPropertyChanged(); } }
        public string? Param7 { get => _params[6]; set { _params[6] = value; OnPropertyChanged(); } }
        public string? Param8 { get => _params[7]; set { _params[7] = value; OnPropertyChanged(); } }
        public string? Param9 { get => _params[8]; set { _params[8] = value; OnPropertyChanged(); } }
        public string? Param10 { get => _params[9]; set { _params[9] = value; OnPropertyChanged(); } }
        public string? Param11 { get => _params[10]; set { _params[10] = value; OnPropertyChanged(); } }
        public string? Param12 { get => _params[11]; set { _params[11] = value; OnPropertyChanged(); } }
        public string? Param13 { get => _params[12]; set { _params[12] = value; OnPropertyChanged(); } }
        public string? Param14 { get => _params[13]; set { _params[13] = value; OnPropertyChanged(); } }
        public string? Param15 { get => _params[14]; set { _params[14] = value; OnPropertyChanged(); } }
        public string? Param16 { get => _params[15]; set { _params[15] = value; OnPropertyChanged(); } }
        public string? Param17 { get => _params[16]; set { _params[16] = value; OnPropertyChanged(); } }
        public string? Param18 { get => _params[17]; set { _params[17] = value; OnPropertyChanged(); } }
        public string? Param19 { get => _params[18]; set { _params[18] = value; OnPropertyChanged(); } }
        public string? Param20 { get => _params[19]; set { _params[19] = value; OnPropertyChanged(); } }
        public string? Param21 { get => _params[20]; set { _params[20] = value; OnPropertyChanged(); } }
        public string? Param22 { get => _params[21]; set { _params[21] = value; OnPropertyChanged(); } }
        public string? Param23 { get => _params[22]; set { _params[22] = value; OnPropertyChanged(); } }
        public string? Param24 { get => _params[23]; set { _params[23] = value; OnPropertyChanged(); } }
        public string? Param25 { get => _params[24]; set { _params[24] = value; OnPropertyChanged(); } }
        public string? Param26 { get => _params[25]; set { _params[25] = value; OnPropertyChanged(); } }
        public string? Param27 { get => _params[26]; set { _params[26] = value; OnPropertyChanged(); } }
        public string? Param28 { get => _params[27]; set { _params[27] = value; OnPropertyChanged(); } }
        public string? Param29 { get => _params[28]; set { _params[28] = value; OnPropertyChanged(); } }
        public string? Param30 { get => _params[29]; set { _params[29] = value; OnPropertyChanged(); } }

        // ================================================
        // BACKWARD COMPATIBILITY (for UI / repo)
        // ================================================

        public int Id
        {
            get => Index ?? 0;
            set => Index = value;
        }

        public int ProcessId
        {
            get => (int)(ProcessID ?? 0);
            set => ProcessID = value;
        }


        public string Name
        {
            get => FunctionName ?? string.Empty;
            set => FunctionName = value;
        }

        public string MethodName
        {
            get => FunctionName ?? string.Empty;
            set => FunctionName = value;
        }

        public string Parameters
        {
            get => Param1 ?? string.Empty;
            set => Param1 = value;
        }

        public string ExpectedResult
        {
            get => ActualValue ?? string.Empty;
            set => ActualValue = value;
        }

        public int Sequence
        {
            get => FunctionPosition ?? 0;
            set => FunctionPosition = value;
        }

        // ================================================
        // INotifyPropertyChanged
        // ================================================

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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