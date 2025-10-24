# WPF Test Automation Manager - Project Structure Analysis

## Project Overview
A modern WPF desktop application for managing test automation workflows with:
- Hierarchical test structure (Tests → Processes → Functions)
- Dynamic external data tables (ExtTables)
- Live database synchronization
- Modern dark theme UI with MahApps.Metro framework

---

## 1. DATA MODELS

### Location: `/home/user/TestAutomationManager/TestAutomationManager/Models/`

#### Test Model (`DataModels.cs` lines 9-123)
```csharp
public class Test : INotifyPropertyChanged
{
    // Properties
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastRun { get; set; }
    public string Status { get; set; }
    public bool IsExpanded { get; set; }                    // UI-only, not persisted
    public ObservableCollection<Process> Processes { get; set; }
    
    // Auto-saves to database when IsActive or Status changes
    private async void SaveToDatabase()
}
```

#### Process Model (`DataModels.cs` lines 125-197)
```csharp
public class Process : INotifyPropertyChanged
{
    // Properties
    public int Id { get; set; }
    public int TestId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Sequence { get; set; }
    public bool IsCritical { get; set; }
    public double Timeout { get; set; }
    public bool IsExpanded { get; set; }                    // UI-only
    public ObservableCollection<Function> Functions { get; set; }
}
```

#### Function Model (`DataModels.cs` lines 199-257)
```csharp
public class Function : INotifyPropertyChanged
{
    // Properties
    public int Id { get; set; }
    public int ProcessId { get; set; }
    public string Name { get; set; }
    public string MethodName { get; set; }
    public string Parameters { get; set; }
    public string ExpectedResult { get; set; }
    public int Sequence { get; set; }
}
```

#### External Table Models (`DataModels.cs` lines 261-287)
```csharp
public class ExternalTableInfo                             // Metadata about ExtTable
{
    public int TestId { get; set; }
    public string TableName { get; set; }                  // e.g., "ExtTable1"
    public string TestName { get; set; }
    public int RowCount { get; set; }
    public string Category { get; set; }
}

public class ExternalTableRow                              // Dynamic row data
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
```

#### ExtTableLayout Model (`/Models/ExtTableLayout.cs`)
```csharp
public class ExtTableLayout
{
    public string TableName { get; set; }                  // e.g., "ExtTable1"
    public Dictionary<string, double> ColumnWidths { get; set; }
    public Dictionary<int, double> RowHeights { get; set; }
    public DateTime LastModified { get; set; }
    public string ModifiedBy { get; set; }
}
```

**Key Feature**: All models implement `INotifyPropertyChanged` for WPF data binding. `IsExpanded` 
properties are UI-only (marked with `[Ignore]` in Entity Framework).

---

## 2. DATABASE CONTEXT

### Location: `/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/`

#### TestAutomationDbContext.cs
- **Framework**: Entity Framework Core with SQL Server
- **DbSets**:
  - `Tests` → Tests table (with cascade delete to Processes)
  - `Processes` → Processes table (with cascade delete to Functions)
  - `Functions` → Functions table

#### DbConnectionConfig.cs
```csharp
// Default connection string
DEFAULT_CONNECTION_STRING = 
  "Server=(localdb)\Local;Database=TestAutomationDB;Integrated Security=true;TrustServerCertificate=true;"

// Methods
public static string GetConnectionString()                 // Reads from App.config or uses default
public static string BuildConnectionString(...)           // Builds dynamic connection strings
public static bool TestConnection()                       // Tests database connectivity
```

**Database Schema**:
- **Tests table**: Id (PK), Name, Description, Category, IsActive, Status, LastRun
- **Processes table**: Id (PK), TestId (FK), Name, Description, Sequence, IsCritical, Timeout
- **Functions table**: Id (PK), ProcessId (FK), Name, MethodName, Parameters, ExpectedResult, Sequence
- **ExtTableLayouts table**: TableName (PK), ColumnWidthsJson, RowHeightsJson, LastModified, ModifiedBy
- **ext schema**: Contains dynamic external tables (ExtTable1, ExtTable2, etc.)

---

## 3. REPOSITORIES

### Location: `/home/user/TestAutomationManager/TestAutomationManager/Repositories/`

#### ITestRepository.cs (Interface)
```csharp
public interface ITestRepository
{
    Task<List<Test>> GetAllTestsAsync();                   // With processes & functions
    Task<Test> GetTestByIdAsync(int testId);
    Task UpdateTestAsync(Test test);                       // Auto-save from UI
    Task InsertTestAsync(Test test);                       // Create new test
    Task DeleteTestAsync(int testId);                      // Cascade delete
    Task<List<ExternalTableInfo>> GetAllExternalTablesAsync();
    Task<int?> GetNextAvailableTestIdAsync();             // Smart ID finder
    Task<bool> TestIdExistsAsync(int testId);
}
```

#### TestRepository.cs
**Key Operations**:
- `GetAllTestsAsync()`: Eager loads Processes → Functions with `.Include().ThenInclude()`
- `InsertTestAsync()`: Uses `SET IDENTITY_INSERT` for explicit ID control
- `UpdateTestAsync()`: Only updates top-level Test properties (not child collections)
- `GetNextAvailableTestIdAsync()`: Finds gaps in ID sequence (e.g., if IDs are 1,2,4,5 → returns 3)
- `DeleteTestAsync()`: Cascade deletes processes and functions

#### ExtTableRepository.cs
**Key Operations**:
- `ExtTableExistsAsync()`: Queries `INFORMATION_SCHEMA.TABLES` for ext.TableName
- `CreateExtTableFromTemplateAsync()`: 
  - Copies table structure from source
  - Creates ID column as IDENTITY(1,1)
  - Copies data (excluding ID column)
  - Uses transaction for atomicity
- `GetTableColumnDefinitionsAsync()`: Retrieves column metadata with data types
- `BuildCreateTableQuery()`: Generates SQL CREATE TABLE with ID as first column
- `DeleteExtTableAsync()`: Drops table from database
- `GetExtTableRowCountAsync()`: Returns row count

#### ExtTableDataRepository.cs
**Key Operations**:
- `UpdateCellValueAsync()`: Updates single cell with validation
  - Checks column max length before update
  - Throws custom `ColumnLengthExceededException` if exceeded
- `GetColumnInfoAsync()`: Retrieves column metadata (DataType, MaxLength, IsNullable)
- `AddColumnAsync()`: Adds new column to ExtTable with validation
- `RenameColumnAsync()`: Renames column using `sp_rename`
- `ExpandColumnSizeAsync()`: Increases VARCHAR column size
- `ColumnExistsAsync()`: Checks if column exists
- `IsValidColumnName()`: SQL injection prevention (alphanumeric + underscore)

#### ExternalTableRepository.cs
**Key Operations**:
- `GetAllExternalTablesAsync()`: 
  - Queries sys.tables for tables in 'ext' schema matching ExtTable% pattern
  - Extracts TestId from table name (ExtTable1 → 1)
  - Gets row count for each table

#### ExtTableLayoutRepository.cs
**Key Operations**:
- `SaveLayoutAsync()`: Saves/updates column widths and row heights as JSON
- `GetLayoutAsync()`: Loads saved layout configuration
- `DeleteLayoutAsync()`: Removes layout configuration
- `GetAllLayoutTablesAsync()`: Lists all tables with saved layouts

---

## 4. VIEW LAYER

### TestsView (Main Tests Display)

#### Location: `/home/user/TestAutomationManager/TestAutomationManager/Views/TestsView.xaml`

**XAML Structure**:
- Grid with 2 rows (header + scrollable content)
- Header shows columns: ID | Name | Category | Status | Last Run | Actions
- ItemsControl for hierarchical data display:
  - **Test Row**: Expandable header with test info
    - **Process Row** (expanded): Shows process details with sequence badge, timeout, critical flag
      - **Function Row** (nested): Inline display with method name, parameters, expected result
- Action buttons: Edit, Run, Delete
- Status indicator circle (color-coded by Status: Passed/Failed/Running/Not Run)

**Code-Behind (`TestsView.xaml.cs`)**:
```csharp
public partial class TestsView : UserControl
{
    private readonly ITestRepository _repository;
    public ObservableCollection<Test> Tests { get; set; }
    private ObservableCollection<Test> _allTests;           // Keep all for filtering
    
    // Methods
    private async void LoadTestsFromDatabase()              // Initial load
    public void FilterTests(string searchQuery)             // Search with fuzzy matching
    private void OnDatabaseTestsUpdated(object sender, List<Test> updatedTests)
                                                            // Handle live updates
    public void ForceRefreshStatistics()                    // Called when tab active
    public void FocusTest(int testId)                       // Scroll to specific test
    
    // Event Handlers
    private void EditTest_Click()
    private void RunTest_Click()
    private async void DeleteTest_Click()
}
```

**Data Binding**:
- Tests ItemsControl bound to `Tests` ObservableCollection
- Two-way binding for `IsActive` checkbox
- One-way binding for display properties
- IsExpanded preserves UI state during data updates

### ExtTableDetailView (External Table Editor)

#### Location: `/home/user/TestAutomationManager/TestAutomationManager/Views/ExtTableDetailView.xaml.cs`

**Features**:
- Loads data from `ext.[TableName]` dynamically
- Displays DataTable in DataGrid with inline editing
- Saves layout (column widths, row heights) after edits
- Supports add/rename/delete columns
- Cell-level validation with custom exception handling

**Key Methods**:
- `LoadTableData()`: Loads metadata and data
- `LoadTableDataFromDatabase()`: Queries table dynamically using SqlCommand
- `TableDataGrid_CellEditEnding()`: Saves changes to database
- `SaveLayout()`: Persists column widths and row heights

---

## 5. KEY SERVICES

### DatabaseWatcherService

#### Location: `/home/user/TestAutomationManager/TestAutomationManager/Services/`

**Singleton pattern** that monitors database for external changes:
```csharp
public class DatabaseWatcherService
{
    // Properties
    public int PollingIntervalSeconds { get; set; } = 3;
    public bool IsWatching { get; }
    
    // Events
    public event EventHandler<List<Test>> TestsUpdated;
    
    // Methods
    public void StartWatching()                            // Starts polling timer
    public void StopWatching()                             // Stops polling
    public async Task ForceCheckAsync()                    // Manual check
    
    // How it works
    1. Polls database every N seconds
    2. Calculates MD5 hash of current test data
    3. If hash differs, fires TestsUpdated event with new data
    4. Views handle event and update UI while preserving IsExpanded state
}
```

### TestStatisticsService

#### Location: `/home/user/TestAutomationManager/TestAutomationManager/Services/Statistics/`

Updates statistics displayed in title bar:
- Total Tests, Processes, Functions count
- Status breakdown (Passed, Failed, Running, Not Run)

---

## 6. CURRENT DATA FLOW

### Initial Load
```
App.xaml.cs → On startup, applies theme
↓
MainWindow.xaml.cs → Creates TestsView
↓
TestsView.xaml.cs constructor:
  1. Creates TestRepository
  2. Subscribes to DatabaseWatcherService.TestsUpdated event
  3. Calls LoadTestsFromDatabase()
↓
TestRepository.GetAllTestsAsync():
  - Uses EF Core Include(t => t.Processes).ThenInclude(p => p.Functions)
  - Returns List<Test> from SQL Server
↓
Tests loaded into ObservableCollection
↓
XAML DataBinding → ItemsControl displays hierarchy
↓
DatabaseWatcherService.StartWatching():
  - Polls database every 3 seconds
  - Detects changes via MD5 hash
  - Fires TestsUpdated event if changed
```

### UI → Database Save Flow
```
User modifies Test.IsActive or Test.Status
↓
INotifyPropertyChanged setter calls SaveToDatabase()
↓
TestRepository.UpdateTestAsync(test)
↓
EF Core DbContext.SaveChangesAsync()
↓
DatabaseWatcherService.ForceCheckAsync() called
↓
Hash updated immediately (prevents reload loop)
```

### External Table Data Flow
```
User clicks ExtTable in main window
↓
ExtTableDetailView(tableName) constructor
↓
LoadTableData() async:
  1. Gets ExternalTableInfo from ExternalTableRepository
  2. Loads DataTable from ext.[TableName] using SqlCommand
  3. Displays in DataGrid
↓
User edits cell
↓
TableDataGrid_CellEditEnding() event:
  1. Validates data (check column length)
  2. Calls ExtTableDataRepository.UpdateCellValueAsync()
  3. Updates database
  4. Saves layout configuration
```

---

## 7. FAKE DATA GENERATION

**Currently**: No fake data generation found. Application relies on:
1. Pre-existing SQL Server database with test data
2. SQL scripts (not shown) that create sample Tests, Processes, Functions
3. Sample ExtTable templates (ExtTable1, ExtTable2, etc.)

**When new test created via AddTestDialog**:
1. User enters test details
2. Selects source ExtTable to copy structure from
3. `ExtTableRepository.CreateExtTableFromTemplateAsync()` creates new table
4. `TestRepository.InsertTestAsync()` creates Test record
5. No default data populated - user must add rows manually via UI

---

## 8. HOW UI BINDS TO DATA

### XAML Data Binding Strategy
```xml
<!-- Hierarchical ItemsControl binding -->
<ItemsControl x:Name="TestsItemsControl" 
              ItemsSource="{Binding Tests}">  <!-- Points to ObservableCollection -->
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <!-- Test row -->
            <ToggleButton IsChecked="{Binding IsExpanded}" />
            <TextBlock Text="{Binding Id, StringFormat='#{0}'}" />
            <TextBlock Text="{Binding Name}" />
            
            <!-- Nested Processes -->
            <ItemsControl ItemsSource="{Binding Processes}">
                <!-- Process row -->
                <TextBlock Text="{Binding Sequence}" />
                <TextBlock Text="{Binding Name}" />
                
                <!-- Nested Functions -->
                <ItemsControl ItemsSource="{Binding Functions}">
                    <DataTemplate>
                        <TextBlock Text="{Binding MethodName}" />
                    </DataTemplate>
                </ItemsControl>
            </ItemsControl>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### Collection Binding
- **Source**: Code-behind property `public ObservableCollection<Test> Tests`
- **Data Context**: Set in constructor via `TestsItemsControl.ItemsSource = Tests`
- **Updates**: When `Tests.Clear()` and `Tests.Add()` called, UI updates automatically

### Two-Way Binding
```xml
<!-- Example: Active checkbox -->
<CheckBox IsChecked="{Binding IsActive}" />

<!-- When user clicks:
     1. IsActive property setter called
     2. INotifyPropertyChanged.OnPropertyChanged() fires
     3. SaveToDatabase() executes (async)
     4. UI remains responsive
-->
```

### Converter Usage
```xml
<Visibility Binding="{Binding IsExpanded, 
                               Converter={StaticResource BooleanToVisibilityConverter}}" />
<Background Binding="{Binding Status}" 
            DataTrigger Value="Passed" 
            Property="Background" 
            Value="{DynamicResource SuccessBrush}" />
```

---

## 9. KEY FEATURES & PATTERNS

### Smart Data Management
1. **Hierarchical Lazy Loading**: EF Core eagerly loads full hierarchy in one query
2. **Observable Collections**: Automatic UI updates when data changes
3. **XAML-Based MVVM**: Code-behind acts as ViewModel (simplified, no separate VM files)
4. **Live Sync**: Database watcher prevents stale data during concurrent edits

### Input Validation
1. **Column Name Validation**: Regex pattern prevents SQL injection
2. **Column Length Validation**: Checks before save, throws custom exception
3. **ID Conflicts**: Smart ID finder checks for duplicates

### Responsive UI
1. **Async Operations**: Database calls don't block UI thread
2. **Fire-and-Forget**: Auto-saves don't wait for completion
3. **Debounced Refresh**: Live watcher uses hash to prevent unnecessary reloads

### Theme System
- Dark/Light/Red themes (loaded from XAML resource files)
- Dynamic brush resources for consistent styling

---

## 10. FILE SUMMARY TABLE

| Component | File Path | Purpose |
|-----------|-----------|---------|
| Data Models | `/Models/DataModels.cs` | Test, Process, Function, ExternalTableInfo classes |
| Layout Model | `/Models/ExtTableLayout.cs` | Column width/row height persistence |
| DB Context | `/Data/TestAutomationDbContext.cs` | EF Core DbContext with mappings |
| DB Config | `/Data/DbConnectionConfig.cs` | Connection string management |
| Test Repository | `/Repositories/TestRepository.cs` | CRUD operations for Tests |
| Test Interface | `/Repositories/ITestRepository.cs` | Repository contract |
| ExtTable Repo | `/Repositories/ExtTableRepository.cs` | Dynamic table creation/deletion |
| ExtTable Data Repo | `/Repositories/ExtTableDataRepository.cs` | Cell/column operations |
| External Repo | `/Repositories/ExternalTableRepository.cs` | Table discovery |
| Layout Repo | `/Repositories/ExtTableLayoutRepository.cs` | Layout persistence |
| Tests View | `/Views/TestsView.xaml` | Main UI for test list |
| Tests CodeBehind | `/Views/TestsView.xaml.cs` | Test view logic & binding |
| ExtTable View | `/Views/ExtTableDetailView.xaml.cs` | External table editor |
| DB Watcher | `/Services/DatabaseWatcherService.cs` | Live sync service |
| Statistics | `/Services/Statistics/...` | Status bar stats |
| Add Test Dialog | `/Dialog/AddTestDialog.xaml.cs` | Create new test UI |
| Theme Service | `/Services/ThemeService.cs` | Dark/Light theme switching |

---

## Key Observations

1. **No Repositories for Process/Function**: Direct EF Core operations only
2. **No ViewModel Layer**: Code-behind handles all logic (simplified MVVM)
3. **Direct DbContext Usage**: Each repository creates new context instance
4. **Fire-and-Forget Auto-Save**: Test model auto-saves without waiting
5. **Polling-Based Sync**: Uses MD5 hash to detect changes (not database triggers)
6. **Dynamic SQL Tables**: ExtTables created in 'ext' schema outside EF Core
7. **Manual Layout Management**: UI layout persisted as JSON, not bound data
