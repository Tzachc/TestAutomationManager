# TestsView Implementation Analysis

## Overview
TestsView is a hierarchical data view system that displays Tests → Processes → Functions in an expandable tree structure with lazy loading optimization for performance with 700+ tests, 20000+ processes, and even more functions.

---

## 1. VIEW STRUCTURE & LAYOUT

### File Locations:
- **XAML**: `/home/user/TestAutomationManager/TestAutomationManager/Views/TestsView.xaml`
- **Code-Behind**: `/home/user/TestAutomationManager/TestAutomationManager/Views/TestsView.xaml.cs`

### Main UI Hierarchy:
```
Border (Header) - Sticky header
  └─ ScrollViewer (HeaderScrollViewer)
     └─ Grid with Column Definitions
        └─ Test columns: TestID, TestName, LastRunning, Status, etc.

ListBox (TestsItemsControl) - Main content with virtualization
  └─ ItemTemplate: Border (Test Row)
     ├─ Grid with Test data binding
     ├─ Buttons: Edit, Run, Delete
     ├─ ToggleButton (Expand) → IsChecked={Binding IsExpanded}
     └─ Border (IsExpanded) - Processes Container
        ├─ Sticky Process Header
        └─ ScrollViewer (ProcRowsScrollViewer)
           └─ ItemsControl (Processes)
              └─ DataTemplate: Process Row
                 ├─ Process data binding
                 ├─ ToggleButton (Expand) → IsChecked={Binding IsExpanded}
                 └─ Border (IsExpanded) - Functions Container
                    ├─ Sticky Function Header
                    └─ ScrollViewer
                       └─ ItemsControl (Functions)
                          └─ Function data binding
```

### Key XAML Features:
- **Virtualization**: ListBox with `VirtualizingPanel.IsVirtualizing="True"` and recycling mode
- **Sticky Headers**: Horizontal scroll synchronization with `SyncHeaderToBody()` method
- **Shared Size Groups**: Column alignment across all tests/processes/functions
- **Dynamic Visibility**: `Visibility={Binding IsExpanded, Converter={StaticResource BooleanToVisibilityConverter}}`

---

## 2. DATA MODELS

### Test Class (INotifyPropertyChanged)
```csharp
public class Test : INotifyPropertyChanged
{
    // Database columns
    public double? TestID { get; set; }
    public string? TestName { get; set; }
    public string? RunStatus { get; set; }
    public string? LastRunning { get; set; }
    public string? LastTimePass { get; set; }
    public string? Bugs { get; set; }
    public string? RecipientsEmailsList { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? SendEmailReport { get; set; }
    public string? ExitTestOnFailure { get; set; }
    public string? TestRunAgainTimes { get; set; }
    public string? SnapshotMultipleFailure { get; set; }
    public string? EmailOnFailureOnly { get; set; }
    public string? DisableKillDriver { get; set; }
    
    // UI-only properties
    public bool IsExpanded { get; set; }
    public ObservableCollection<Process> Processes { get; set; }
    public bool AreProcessesLoaded { get; set; }  // Lazy load flag
    public string Category { get; set; }
    public bool IsActive { get; set; }
    
    // Backward compatibility
    public int Id { get => (int)(TestID ?? 0); }
    public string Name { get => TestName ?? ""; }
}
```

### Process Class (INotifyPropertyChanged)
```csharp
public class Process : INotifyPropertyChanged
{
    // Database columns
    public double? TestID { get; set; }
    public double? ProcessID { get; set; }
    public string? ProcessName { get; set; }
    public double? ProcessPosition { get; set; }
    public string? Pass_Fail_WEB3Operator { get; set; }
    public string? WEB3Operator { get; set; }
    public string? Comments { get; set; }
    public string?[] _params = new string?[46];  // Param1-Param46
    
    // UI-only properties
    public bool IsExpanded { get; set; }
    public ObservableCollection<Function> Functions { get; set; }
    public bool AreFunctionsLoaded { get; set; }  // Lazy load flag
}
```

### Function Class (INotifyPropertyChanged)
```csharp
public class Function : INotifyPropertyChanged
{
    // Database columns
    public double? ProcessID { get; set; }
    public string? FunctionName { get; set; }
    public string? FunctionDescription { get; set; }
    public int? FunctionPosition { get; set; }
    public string? ActualValue { get; set; }
    public string? Comments { get; set; }
    public string?[] _params = new string?[30];  // Param1-Param30
}
```

---

## 3. DATA BINDING SETUP

### Binding Pattern:
```xaml
<!-- Test Binding -->
<TextBlock Text="{Binding TestName}" />
<TextBlock Text="{Binding RunStatus}" />
<ToggleButton IsChecked="{Binding IsExpanded, Mode=TwoWay}" />

<!-- Process Binding -->
<TextBlock Text="{Binding ProcessName}" />
<ToggleButton IsChecked="{Binding IsExpanded, Mode=TwoWay}" />

<!-- Function Binding -->
<TextBlock Text="{Binding FunctionName}" />
```

### Collections:
```csharp
// Code-Behind
public ObservableCollection<Test> Tests { get; set; }  // Filtered/visible
private ObservableCollection<Test> _allTests;           // All tests (unfiltered)
```

### ItemsSource Binding:
```xaml
<ListBox ItemsSource="{Binding Tests}" />
<ItemsControl ItemsSource="{Binding Processes}" />
<ItemsControl ItemsSource="{Binding Functions}" />
```

---

## 4. EXPANSION FUNCTIONALITY & LAZY LOADING

### Overview
The system uses a **3-level lazy loading strategy** with cache-first approach:

1. **Tests**: Loaded on initial view (via stored procedure)
2. **Processes**: Loaded when test is expanded (cache-first, then database)
3. **Functions**: Loaded when process is expanded (database only, cached)

### Architecture Diagram:
```
TestsView Constructor
  ├─ LoadTestsFromDatabase() [FAST - SP]
  ├─ Fire DataLoaded event
  └─ PreloadAllProcessesInBackgroundAsync() [Background]
     └─ Load all processes into ProcessCacheService

User expands Test (IsExpanded = true)
  └─ Test_PropertyChanged() fires
     └─ LoadProcessesForTestAsync(test)
        ├─ Check ProcessCacheService (instant if preloaded)
        └─ Fallback to database (first expansion)
           └─ Add to cache
           └─ Update UI on Main thread

User expands Process (IsExpanded = true)
  └─ Process_PropertyChanged() fires
     └─ LoadFunctionsForProcessAsync(process)
        ├─ Load from database
        ├─ Add to ProcessCacheService
        └─ Update UI on Main thread
```

### Code Implementation:

#### 1. Test Expansion Handler:
```csharp
private async void Test_PropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(Test.IsExpanded) && sender is Test test)
    {
        // Only load if expanded and not already loaded
        if (test.IsExpanded && !test.AreProcessesLoaded)
        {
            await LoadProcessesForTestAsync(test);
        }
    }
}
```

#### 2. Process Expansion Handler:
```csharp
private async void Process_PropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(Process.IsExpanded) && sender is Process process)
    {
        // Only load if expanded and not already loaded
        if (process.IsExpanded && !process.AreFunctionsLoaded)
        {
            await LoadFunctionsForProcessAsync(process);
        }
    }
}
```

#### 3. Load Processes (Cache-First Strategy):
```csharp
private async Task LoadProcessesForTestAsync(Test test)
{
    if (!test.TestID.HasValue)
        return;

    try
    {
        var testId = (int)test.TestID.Value;
        List<Process> processes;

        // STEP 1: Try cache first (INSTANT!)
        var cachedProcesses = ProcessCacheService.Instance.GetProcessesByTestId(testId);
        if (cachedProcesses != null && cachedProcesses.Count > 0)
        {
            Debug.WriteLine($"⚡ Loading processes for Test #{testId} from CACHE (instant!)");
            processes = cachedProcesses;
        }
        else
        {
            // STEP 2: Fallback to database (cache miss)
            Debug.WriteLine($"⏳ Loading processes for Test #{testId} from DATABASE...");
            processes = await _repository.GetProcessesForTestAsync(testId);

            // Add to cache for next time
            ProcessCacheService.Instance.AddProcessesByTestId(testId, processes);
        }

        // STEP 3: Sort by ProcessPosition
        processes = processes.OrderBy(p => p.ProcessPosition).ToList();

        // STEP 4: Update UI on UI thread
        await Dispatcher.InvokeAsync(() =>
        {
            test.Processes.Clear();
            foreach (var process in processes)
            {
                test.Processes.Add(process);
                process.PropertyChanged += Process_PropertyChanged;  // Subscribe to expansion
            }
            test.AreProcessesLoaded = true;
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"✗ Error loading processes: {ex.Message}");
        MessageBox.Show($"Failed to load processes.\n\nError: {ex.Message}",
            "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
```

#### 4. Load Functions:
```csharp
private async Task LoadFunctionsForProcessAsync(Process process)
{
    if (!process.ProcessID.HasValue)
        return;

    try
    {
        Debug.WriteLine($"⏳ Lazy loading functions for Process #{process.ProcessID}...");

        // Load from database
        var functions = await _repository.GetFunctionsForProcessAsync(process.ProcessID.Value);

        // Add to shared cache
        ProcessCacheService.Instance.AddFunctions(process.ProcessID.Value, functions);

        // Update UI on UI thread
        await Dispatcher.InvokeAsync(() =>
        {
            process.Functions.Clear();
            foreach (var function in functions)
            {
                process.Functions.Add(function);
            }
            process.AreFunctionsLoaded = true;
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"✗ Error lazy loading functions: {ex.Message}");
        MessageBox.Show($"Failed to load functions.\n\nError: {ex.Message}",
            "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
```

#### 5. Background Preload (on view load):
```csharp
private async Task PreloadAllProcessesInBackgroundAsync()
{
    try
    {
        // Run on background thread
        await Task.Run(async () =>
        {
            Debug.WriteLine("📊 [Background] Loading all processes...");

            var processRepository = new ProcessRepository();
            var allProcesses = await processRepository.GetAllProcessesAsync();

            // Group by TestID
            var processesByTestId = allProcesses
                .Where(p => p.TestID.HasValue)
                .GroupBy(p => (int)p.TestID.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Add ALL to cache
            foreach (var kvp in processesByTestId)
            {
                ProcessCacheService.Instance.AddProcessesByTestId(kvp.Key, kvp.Value);
            }

            Debug.WriteLine($"✅ [Background] Preloaded {allProcesses.Count} processes into cache!");
        });
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"⚠️ [Background] Error preloading: {ex.Message}");
        // Non-fatal - users can still lazy load from database
    }
}
```

---

## 5. DATABASE ACCESS LAYER

### Repository: TestRepository

Location: `/home/user/TestAutomationManager/TestAutomationManager/Repositories/TestRepository.cs`

#### Key Methods:

**1. Get All Tests (Fast - Stored Procedure)**
```csharp
public async Task<List<Test>> GetAllTestsAsync()
{
    using (var context = new TestAutomationDbContext())
    {
        var schemaName = SchemaConfigService.Instance.CurrentSchema;
        var spName = $"EXEC [{schemaName}].[usp_GetAllTests]";
        
        var tests = await context.Tests
            .FromSqlRaw(spName)
            .ToListAsync();

        // Initialize empty collections
        foreach (var test in tests)
        {
            test.Processes = new ObservableCollection<Process>();
            test.AreProcessesLoaded = false;  // Mark for lazy loading
        }

        return tests;
    }
}
```

**2. Get Processes for Test**
```csharp
public async Task<List<Process>> GetProcessesForTestAsync(int testId)
{
    using (var context = new TestAutomationDbContext())
    {
        var schemaName = SchemaConfigService.Instance.CurrentSchema;
        var testIdParam = new SqlParameter("@TestID", testId);
        var spName = $"EXEC [{schemaName}].[usp_GetProcessesByTestID] @TestID";

        var processes = await context.Set<Process>()
            .FromSqlRaw(spName, testIdParam)
            .ToListAsync();

        // Initialize functions collection
        foreach (var process in processes)
        {
            process.Functions = new ObservableCollection<Function>();
            process.AreFunctionsLoaded = false;
        }

        return processes;
    }
}
```

**3. Get Functions for Process**
```csharp
public async Task<List<Function>> GetFunctionsForProcessAsync(double processId)
{
    using (var context = new TestAutomationDbContext())
    {
        var schemaName = SchemaConfigService.Instance.CurrentSchema;
        var processIdParam = new SqlParameter("@ProcessID", processId);
        var spName = $"EXEC [{schemaName}].[usp_GetFunctionsByProcessID] @ProcessID";

        var functions = await context.Set<Function>()
            .FromSqlRaw(spName, processIdParam)
            .ToListAsync();

        return functions;
    }
}
```

**4. Delete Test (Cascade)**
```csharp
public async Task DeleteTestAsync(int testId)
{
    using (var context = new TestAutomationDbContext())
    {
        var test = await context.Tests
            .Include(t => t.Processes)
                .ThenInclude(p => p.Functions)
            .FirstOrDefaultAsync(t => t.TestID == testId);

        if (test == null)
            throw new InvalidOperationException($"Test with ID {testId} not found");

        // Cascade delete via DbContext relationships
        context.Tests.Remove(test);
        await context.SaveChangesAsync();
    }
}
```

**5. Get Next Available Test ID (Smart)**
```csharp
public async Task<int?> GetNextAvailableTestIdAsync()
{
    using (var context = new TestAutomationDbContext())
    {
        var tests = await context.Tests.AsNoTracking().ToListAsync();

        // 1) Check for FREE tests (reusable)
        var freeIds = tests
            .Where(t => t.TestName?.Contains("FREE") == true || t.RunStatus?.Contains("FREE") == true)
            .Select(t => (int?)t.TestID)
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .OrderBy(id => id)
            .ToList();

        if (freeIds.Any())
            return freeIds.First();

        // 2) Find gap in sequence
        var existingIds = tests
            .Select(t => (int?)t.TestID)
            .Where(id => id.HasValue)
            .Select(id => id.Value)
            .OrderBy(id => id)
            .ToList();

        // Gap finding logic...
        // Returns next available ID
    }
}
```

### ProcessRepository Methods:
- `GetAllProcessesAsync()` - All processes via SP
- `GetFunctionsForProcessAsync(processId)` - Functions for specific process
- `GetTotalProcessCountAsync()` - For statistics

---

## 6. ENTITY FRAMEWORK CONFIGURATION

Location: `/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/TestAutomationDbContext.cs`

### Database Schema Mapping:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var schemaConfig = SchemaConfigService.Instance;
    string currentSchema = schemaConfig.CurrentSchema;  // e.g., "PRODUCTION_Selenium"

    // Test Entity
    modelBuilder.Entity<Test>(entity =>
    {
        entity.ToTable(schemaConfig.TestTableName, currentSchema);
        entity.HasKey(e => e.TestID);
        
        // Cascade delete to Processes
        entity.HasMany(t => t.Processes)
            .WithOne()
            .HasForeignKey(p => p.TestID)
            .OnDelete(DeleteBehavior.Cascade);
    });

    // Process Entity
    modelBuilder.Entity<Process>(entity =>
    {
        entity.ToTable(schemaConfig.ProcessTableName, currentSchema);
        entity.HasKey(e => e.Index);
        entity.HasAlternateKey(e => e.ProcessID);
        
        // Cascade delete to Functions
        entity.HasMany(p => p.Functions)
            .WithOne()
            .HasForeignKey(f => f.ProcessID)
            .HasPrincipalKey(p => p.ProcessID)
            .OnDelete(DeleteBehavior.Cascade);
    });

    // Function Entity
    modelBuilder.Entity<Function>(entity =>
    {
        entity.ToTable(schemaConfig.FunctionTableName, currentSchema);
        entity.HasKey(e => e.Index);
    });
}
```

### DbSets:
```csharp
public DbSet<Test> Tests { get; set; }
public DbSet<Process> Processes { get; set; }
public DbSet<Function> Functions { get; set; }
```

---

## 7. DATABASE STRUCTURE

### SQL Schema Tables:
```sql
-- [PRODUCTION_Selenium].[Test_WEB3]
Columns: TestID, TestName, Bugs, DisableKillDriver, EmailOnFailureOnly,
         ExceptionMessage, ExitTestOnFailure, LastRunning, LastTimePass,
         RecipientsEmailsList, RunStatus, SendEmailReport, SnapshotMultipleFailure,
         TestRunAgainTimes

-- [PRODUCTION_Selenium].[Process_WEB3]
PrimaryKey: Index
ForeignKey: TestID → Test_WEB3
Columns: TestID, Comments, Index, LastRunning, Module, Pass_Fail_WEB3Operator,
         ProcessID, ProcessName, ProcessPosition, Repeat, TempParam, WEB3Operator,
         Param1..Param46, TempParam1, TempParam11, TempParam111, etc.

-- [PRODUCTION_Selenium].[Function_WEB3]
PrimaryKey: Index
ForeignKey: ProcessID → Process_WEB3.ProcessID
Columns: Index, ProcessID, FunctionName, FunctionDescription, FunctionPosition,
         WEB3Operator, Pass_Fail_WEB3Operator, Comments, ActualValue, BreakPoint,
         Param1..Param30
```

### Relationships:
```
Test (1) ──→ (M) Process [TestID]
      └─ OnDelete.Cascade

Process (1) ──→ (M) Function [ProcessID]
         └─ OnDelete.Cascade
```

### Stored Procedures (in schema):
- `[SCHEMA].[usp_GetAllTests]` - Get all tests (optimized)
- `[SCHEMA].[usp_GetProcessesByTestID] @TestID` - Get processes for test
- `[SCHEMA].[usp_GetFunctionsByProcessID] @ProcessID` - Get functions for process
- `[SCHEMA].[usp_GetAllProcesses]` - Get all processes
- `[SCHEMA].[usp_GetAllFunctions]` - Get all functions

Location: `/home/user/TestAutomationManager/SQL_StoredProcedures/usp_GetAllFunctions.sql`

---

## 8. CACHING STRATEGY

### ProcessCacheService (Singleton)
Location: `/home/user/TestAutomationManager/TestAutomationManager/Services/ProcessCacheService.cs`

```csharp
public class ProcessCacheService
{
    // Thread-safe concurrent collections
    private ConcurrentBag<Process> _allProcesses;              // All processes (allows duplicates)
    private ConcurrentDictionary<double, Process> _processCache;  // Quick lookup by ProcessID
    private ConcurrentDictionary<double, List<Function>> _functionCache;  // Functions by ProcessID
    private ConcurrentDictionary<int, List<Process>> _processesByTestIdCache;  // Processes by TestID
    
    // Key Methods:
    public void AddProcessesByTestId(int testId, List<Process> processes);
    public List<Process> GetProcessesByTestId(int testId);
    public void AddFunctions(double processId, List<Function> functions);
    public List<Function> GetFunctions(double processId);
}
```

### Cache Hit Flow:
1. User expands Test → `LoadProcessesForTestAsync(test)` called
2. Check `ProcessCacheService.GetProcessesByTestId(testId)`
3. If found: Use cached data (instant)
4. If not found: Query database, add to cache, return

---

## 9. EXISTING EDIT PATTERN (Reference)

### AddTestDialog as Reference Pattern

Location: `/home/user/TestAutomationManager/TestAutomationManager/Dialog/AddTestDialog.xaml.cs`

#### Pattern Used:
1. **Dialog Window** - Modal dialog for edit operations
2. **Repository Injection** - `_testRepository` passed to dialog
3. **Validation** - Real-time validation with visual feedback
4. **Save to Database** - `await _testRepository.InsertTestAsync(newTest)`
5. **Notification** - ModernMessageDialog for user feedback
6. **UI Refresh** - Dialog closes, parent view refreshes

#### Key Code:
```csharp
public partial class AddTestDialog : Window
{
    private readonly ITestRepository _testRepository;

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 1. Validate input
            if (!int.TryParse(TestIdTextBox.Text, out int testId))
                throw new ValidationException("Invalid Test ID");

            // 2. Check existing ID
            bool exists = await _testRepository.TestIdExistsAsync(testId);
            if (exists)
                throw new ValidationException($"Test {testId} already exists!");

            // 3. Create test object
            var newTest = new Test
            {
                Id = testId,
                Name = TestNameTextBox.Text,
                Category = CategoryComboBox.SelectedItem.ToString(),
                IsActive = true
            };

            // 4. Save to database
            await _testRepository.InsertTestAsync(newTest);

            // 5. Show success
            ModernMessageDialog.ShowSuccess("Test created!", "Success", this);

            // 6. Close and notify
            CreatedTest = newTest;
            IsSuccess = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ModernMessageDialog.ShowError(ex.Message, "Error", this);
        }
    }
}
```

---

## 10. STATISTICS & PERFORMANCE

### Statistics Service:
- `TestStatisticsService.Instance.UpdateStatistics(_allTests)`
- Tracks: Total tests, processes, functions, pass/fail counts

### Performance Features:
1. **Virtualization** - Only renders visible items
2. **Lazy Loading** - 3-tier on-demand loading
3. **Background Preload** - Non-blocking process cache population
4. **Stored Procedures** - 4-5x faster than LINQ queries
5. **Concurrent Caching** - Thread-safe process cache
6. **Incremental Updates** - Only update changed tests (multi-user)

### Live Updates (Multi-User Collaboration):
- `DatabaseWatcherService.Instance` polls database every 3 seconds
- Fires `OnDatabaseChanged` event with delta changes
- Updates only changed tests (not full reload)

---

## 11. KEY INTEGRATION POINTS

### Code-Behind Methods:
```csharp
TestsView.cs
├─ LoadTestsFromDatabase()          # Initial load with SP
├─ PreloadAllProcessesInBackgroundAsync()  # Background cache population
├─ Test_PropertyChanged()            # Expansion detection
├─ Process_PropertyChanged()         # Expansion detection
├─ LoadProcessesForTestAsync()       # Load + cache
├─ LoadFunctionsForProcessAsync()    # Load + cache
├─ FilterTests(query)                # Search/filter
├─ DeleteTest_Click()                # Delete with cascade
├─ EditTest_Click()                  # Placeholder for edit (coming soon)
├─ RunTest_Click()                   # Placeholder for run (coming soon)
└─ OnDatabaseChanged()               # Multi-user incremental updates
```

### UI Event Handlers:
```csharp
├─ MainScrollViewer_ScrollChanged()      # Header sync
├─ MainScrollViewer_PreviewMouseDown/Move/Up()  # Middle-mouse pan
├─ ShowFilter_Click()                # Filter popup
├─ FilterControl_FilterApplied()     # Apply filter
└─ RequestBringIntoView handlers     # Prevent auto-scroll
```

### Related Services:
- `ProcessCacheService` - Singleton cache management
- `DatabaseWatcherService` - Live updates polling
- `TestStatisticsService` - Dashboard statistics
- `SchemaConfigService` - Dynamic schema selection
- `TestUISettingsService` - User preferences

---

## 12. SEARCH & FILTERING

### Filter Implementation:
```csharp
private FilterManager<Test> _filterManager;

public void FilterTests(string searchQuery)
{
    _currentSearchQuery = searchQuery ?? "";
    Tests.Clear();

    if (string.IsNullOrWhiteSpace(searchQuery))
    {
        // Show all
        foreach (var test in _allTests)
            Tests.Add(test);
    }
    else
    {
        // Filter by: TestName, TestID, Category, RunStatus
        var filtered = _allTests.Where(t =>
            (t.TestName?.Contains(searchQuery, OrdinalIgnoreCase) ?? false) ||
            (t.TestID?.ToString().Contains(searchQuery) ?? false) ||
            (t.Category?.Contains(searchQuery, OrdinalIgnoreCase) ?? false) ||
            (t.RunStatus?.Contains(searchQuery, OrdinalIgnoreCase) ?? false)
        );

        foreach (var test in filtered)
            Tests.Add(test);
    }

    UpdateStatistics();
}
```

---

## Summary

The TestsView is a sophisticated, performance-optimized hierarchical data viewer that:

1. **Displays** 700+ tests with 20000+ processes and thousands of functions
2. **Loads** tests immediately (via SP), processes on-demand (cache-first), functions on expansion
3. **Caches** all processes in background, reuses for instant expansion
4. **Synchronizes** UI across tests/processes/functions with sticky headers
5. **Supports** multi-user collaboration with live database polling
6. **Provides** filtering, searching, deletion, and future edit/run operations
7. **Uses** Entity Framework with stored procedures for performance
8. **Handles** large datasets efficiently via virtualization and lazy loading

