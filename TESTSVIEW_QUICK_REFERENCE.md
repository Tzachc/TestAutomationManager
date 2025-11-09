# TestsView Quick Reference Guide

## File Locations - Quick Lookup

### Core Implementation
| File | Purpose |
|------|---------|
| `/Views/TestsView.xaml` | UI layout with hierarchy structure |
| `/Views/TestsView.xaml.cs` | Logic for lazy loading & expansion |
| `/Repositories/TestRepository.cs` | Database CRUD operations |
| `/Repositories/ITestRepository.cs` | Repository interface |
| `/Models/DataModels.cs` | Test, Process, Function entity classes |
| `/Data/TestAutomationDbContext.cs` | EF Core configuration & relationships |
| `/Services/ProcessCacheService.cs` | Thread-safe caching service |

### Supporting Files
| File | Purpose |
|------|---------|
| `/Dialog/AddTestDialog.xaml.cs` | Edit pattern reference |
| `/Repositories/ProcessRepository.cs` | Process-specific operations |
| `/Services/DatabaseWatcherService.cs` | Multi-user live updates |
| `/SQL_StoredProcedures/usp_GetAllFunctions.sql` | Database SP examples |

---

## Key Classes & Properties

### Test Model
```
Properties:
  - TestID (PK, double)
  - TestName (string)
  - RunStatus (string)
  - IsExpanded (UI-only, bool)
  - Processes (ObservableCollection<Process>)
  - AreProcessesLoaded (bool, lazy load flag)
  - Category, IsActive (UI-only)
```

### Process Model
```
Properties:
  - ProcessID (double)
  - ProcessName (string)
  - ProcessPosition (double, sort order)
  - TestID (FK, double)
  - IsExpanded (UI-only, bool)
  - Functions (ObservableCollection<Function>)
  - AreFunctionsLoaded (bool, lazy load flag)
  - Param1-Param46 (string[])
```

### Function Model
```
Properties:
  - ProcessID (FK, double)
  - FunctionName (string)
  - FunctionDescription (string)
  - FunctionPosition (int, sort order)
  - Param1-Param30 (string[])
```

---

## Data Flow Diagram

```
UI User Action
    |
    v
Test.IsExpanded = true (TwoWay binding)
    |
    v
Test_PropertyChanged() event fires
    |
    +---> Check if AreProcessesLoaded = false
    |
    +---> YES: Call LoadProcessesForTestAsync()
    |
    +---> Cache Check:
    |         |
    |         +---> HIT: Use cached processes (INSTANT)
    |         |
    |         +---> MISS: Query database, add to cache
    |
    v
Update UI on Main Thread
    |
    v
test.Processes.Add(process)
process.PropertyChanged += Process_PropertyChanged  (subscribe)
test.AreProcessesLoaded = true
    |
    v
Process Items Rendered in UI
```

---

## Lazy Loading Strategy

### 3-Tier System:

**Tier 1: Tests (Immediate)**
- Loaded via `LoadTestsFromDatabase()` on view initialization
- Uses stored procedure for performance
- Processes collection initialized but empty

**Tier 2: Processes (On-Demand with Cache)**
- Triggered by `Test.IsExpanded = true`
- Check `ProcessCacheService` first (instant if preloaded)
- Fallback to database if not cached
- Background preload fills cache non-blocking

**Tier 3: Functions (On-Demand)**
- Triggered by `Process.IsExpanded = true`
- Always queried from database
- Cached in `ProcessCacheService` for reuse

### Preload Timing:
1. View loads → `LoadTestsFromDatabase()` - tests appear immediately
2. Fire `DataLoaded` event (dashboard updates)
3. Background thread starts `PreloadAllProcessesInBackgroundAsync()`
4. All processes loaded into cache while user views tests
5. When user expands test → data already in cache (instant)

---

## Key Methods

### TestsView.cs

| Method | Purpose |
|--------|---------|
| `LoadTestsFromDatabase()` | Initial load via stored procedure |
| `Test_PropertyChanged()` | Detect expansion, trigger load |
| `LoadProcessesForTestAsync()` | Load + cache processes for test |
| `Process_PropertyChanged()` | Detect expansion, trigger load |
| `LoadFunctionsForProcessAsync()` | Load + cache functions for process |
| `PreloadAllProcessesInBackgroundAsync()` | Background cache population |
| `FilterTests(query)` | Search/filter tests |
| `DeleteTest_Click()` | Delete test (cascades) |
| `EditTest_Click()` | Placeholder for edit dialog |
| `RefreshData()` | Full reload from database |

### TestRepository.cs

| Method | Purpose |
|--------|---------|
| `GetAllTestsAsync()` | Load tests via SP (no processes) |
| `GetProcessesForTestAsync(testId)` | Load processes for specific test via SP |
| `GetFunctionsForProcessAsync(processId)` | Load functions for specific process via SP |
| `DeleteTestAsync(testId)` | Delete test (cascade delete) |
| `InsertTestAsync(test)` | Create new test |
| `UpdateTestAsync(test)` | Update existing test |
| `GetNextAvailableTestIdAsync()` | Smart ID generator (finds gaps) |
| `TestIdExistsAsync(testId)` | Duplicate check |

---

## Database Schema

### Tables (Schema: PRODUCTION_Selenium or SeleniumDB)

**Test_WEB3**
```
PrimaryKey: TestID
Columns: TestName, RunStatus, LastRunning, LastTimePass, Bugs,
         RecipientsEmailsList, ExceptionMessage, SendEmailReport,
         ExitTestOnFailure, TestRunAgainTimes, SnapshotMultipleFailure,
         EmailOnFailureOnly, DisableKillDriver
```

**Process_WEB3**
```
PrimaryKey: Index (record ID)
AlternateKey: ProcessID (template ID)
ForeignKey: TestID → Test_WEB3.TestID (CASCADE DELETE)
Columns: ProcessName, ProcessPosition, WEB3Operator,
         Pass_Fail_WEB3Operator, Comments, Module, Repeat,
         TempParam, Param1-Param46
```

**Function_WEB3**
```
PrimaryKey: Index (record ID)
ForeignKey: ProcessID → Process_WEB3.ProcessID (CASCADE DELETE)
Columns: FunctionName, FunctionDescription, FunctionPosition,
         WEB3Operator, Pass_Fail_WEB3Operator, Comments,
         ActualValue, BreakPoint, Param1-Param30
```

### Relationships:
```
Test (1) ──CASCADE── (M) Process [TestID]
Process (1) ──CASCADE── (M) Function [ProcessID]
```

---

## Stored Procedures

All located in schema-qualified format: `[SCHEMA].[usp_Name]`

| SP | Purpose | Parameter |
|----|---------|-----------|
| `usp_GetAllTests` | Get all tests | None |
| `usp_GetProcessesByTestID` | Get processes for test | @TestID |
| `usp_GetFunctionsByProcessID` | Get functions for process | @ProcessID |
| `usp_GetAllProcesses` | Get all processes | None |
| `usp_GetAllFunctions` | Get all functions | None |

Performance optimizations:
- `WITH (NOLOCK)` - read uncommitted
- `OPTION (MAXDOP 4, RECOMPILE)` - parallel execution

---

## Caching Strategy

### ProcessCacheService (Singleton)

**Cache Levels:**
```
1. _processesByTestIdCache
   Key: TestID (int)
   Value: List<Process>
   Used: Fast test expansion
   
2. _processCache
   Key: ProcessID (double)
   Value: Process (single)
   Used: Quick process lookup
   
3. _functionCache
   Key: ProcessID (double)
   Value: List<Function>
   Used: Function reuse
```

**Hit Rate:**
- Preload phase: ~70-80% hit rate before user interaction
- After expansion: 95%+ hit rate for subsequent expansions

**Memory Usage:**
- All 20000+ processes: ~100-150 MB
- Thread-safe with ConcurrentBag/ConcurrentDictionary

---

## Important Flags & States

### Lazy Load Flags:
```csharp
Test.AreProcessesLoaded       // Set to false initially, true after load
Process.AreFunctionsLoaded    // Set to false initially, true after load
```

### UI Flags:
```csharp
Test.IsExpanded              // Binding to ToggleButton.IsChecked
Process.IsExpanded           // Binding to ToggleButton.IsChecked
```

**Flow:** 
1. IsExpanded changes → PropertyChanged fires
2. Check AreProcessesLoaded flag
3. If false, trigger load
4. Set flag to true after load
5. Next expansion: flag prevents reload

---

## Extension Points (For Edit Functionality)

### Where to Add Edit Dialog:

**1. EditTest_Click() Method:**
```csharp
private void EditTest_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button button && button.Tag is Test test)
    {
        // Create dialog
        var dialog = new EditTestDialog(test);
        
        // Show modal
        var result = dialog.ShowDialog();
        
        // If success, refresh
        if (result == true)
        {
            RefreshData();  // Or incremental update
        }
    }
}
```

**2. Create EditTestDialog (follow AddTestDialog pattern):**
- Accept `Test` parameter in constructor
- Populate fields from test object
- Validate on save
- Call `_repository.UpdateTestAsync(test)`
- Close on success

**3. Dialog Template:**
```xaml
<Window Title="Edit Test" Width="500" Height="400">
    <StackPanel Margin="20">
        <TextBox x:Name="TestNameTextBox" 
                 Text="{Binding TestName, Mode=TwoWay}" />
        <TextBox x:Name="DescriptionTextBox" 
                 Text="{Binding Description, Mode=TwoWay}" />
        <Button Click="SaveButton_Click">Save</Button>
    </StackPanel>
</Window>
```

### Data Binding Considerations:
- Use TwoWay binding for editable fields
- Validate before calling `UpdateTestAsync()`
- Update only changed properties
- Notify UI of changes via PropertyChanged

---

## Performance Optimization Tips

### What Works Well:
- Stored procedures: 4-5x faster than LINQ
- Lazy loading: Defer expensive operations
- Background preload: Non-blocking UI
- Virtualization: Only render visible items
- Caching: Avoid duplicate database hits
- Incremental updates: Only load changed data

### Potential Issues:
- Large parameter arrays (Param1-Param46) - consider JSON serialization
- Sticky header sync - complex visual tree traversal
- Live database polling - 3-second interval (may miss updates)

### If Slow:
1. Check database stored procedures (add indices on TestID, ProcessID)
2. Verify cache is populating (check debug output)
3. Profile lazy load operations (should be <500ms)
4. Check network latency to database

---

## Testing Checklist

- Test initial load: 700+ tests appear in <2 seconds
- Test expansion: First test expansion <500ms (database), second <100ms (cache)
- Test filtering: Search updates visible results <200ms
- Test deletion: Delete cascades to all child processes/functions
- Test scrolling: Smooth with 2000+ visible items (virtualization)
- Test multi-user: Live updates reflect changes from other users
- Test edit dialog: Changes persist to database and UI updates

---

## Quick Debugging

### Enable Debug Output:
View debug output in Visual Studio Output window:
```
✓ Initial load complete - incremental updates now enabled
⚡ Loading processes for Test #123 from CACHE (instant!)
📊 ProcessCache Statistics: 20000 cached
✅ Test #456 deleted successfully!
```

### Common Issues:
| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Tests don't appear | DB connection | Check `DbConnectionConfig` |
| Expansion hangs | Database query timeout | Verify SP has NOLOCK |
| Cache not filling | Background task failed | Check exception handling |
| Sticky header misaligned | Window size changed | Call `UpdateProcessHeaderPositions()` |
| Filter not working | Query collection empty | Ensure `_allTests` populated |

---

## Architecture Summary

```
TestsView (UI)
  |
  +---> ITestRepository
  |       |
  |       +---> TestAutomationDbContext (EF Core)
  |       |       |
  |       |       +---> [PRODUCTION_Selenium] Database
  |       |       |       |
  |       |       |       +---> Stored Procedures
  |       |
  |       +---> ProcessRepository (background)
  |
  +---> ProcessCacheService (Singleton)
  |       |
  |       +---> Concurrent collections (thread-safe)
  |
  +---> DatabaseWatcherService (multi-user)
  |
  +---> FilterManager (search/filter)
```

---

## Related Views (Reference Architecture)

- **ProcessView.xaml** - Similar lazy-load pattern for processes
- **FunctionView.xaml** - Similar pattern for functions
- **ExtTableDetailView.xaml** - External table data viewer

All follow same hierarchy and caching patterns.

