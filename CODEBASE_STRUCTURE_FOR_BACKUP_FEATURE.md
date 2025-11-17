# Test Automation Manager - Comprehensive Codebase Analysis

## Executive Summary
This is a WPF desktop application (.NET 8.0) built with MahApps.Metro for managing automated test cases, processes, and functions in SQL Server databases. The architecture follows a clean separation of concerns with presentation, business logic, and data layers.

---

## 1. DATABASE CONNECTION SETUP

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/DbConnectionConfig.cs` (116 lines)
- `/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/TestAutomationDbContext.cs` (251 lines)

### Connection Configuration:
**Default Connection String:**
```csharp
Server=(localdb)\Local;Database=AutomationDB;Integrated Security=true;TrustServerCertificate=true;
```

**Key Features:**
- Uses Entity Framework Core 9.0.10
- Supports both Windows Authentication and SQL Server Authentication
- Connection string can be overridden via App.config ("TestAutomationDB" key)
- Static `DbConnectionConfig` class with methods:
  - `GetConnectionString()` - Reads from config or uses default LocalDB
  - `BuildConnectionString()` - Dynamically builds connection strings with auth options
  - `TestConnection()` - Verifies database connectivity

**Database Context (TestAutomationDbContext):**
- Inherits from `DbContext`
- Maps to SQL Server via `UseSqlServer()`
- Uses lazy logging in DEBUG mode with `EnableSensitiveDataLogging()`
- Three main DbSets:
  - `DbSet<Test>` - Maps to `[SCHEMA].[Test_WEB3]`
  - `DbSet<Process>` - Maps to `[SCHEMA].[Process_WEB3]`
  - `DbSet<Function>` - Maps to `[SCHEMA].[Function_WEB3]`

---

## 2. DATABASE SCHEMAS

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/Schemaconfig.cs` (246 lines)

### Available Schemas:
```csharp
AvailableSchemas = new List<string>
{
    "SeleniumDB",
    "PRODUCTION_Selenium"
    // Can be extended
}
```

### Dynamic Schema Switching:
The application supports switching between schemas at runtime without code changes.

**Schema Configuration Properties:**
- `CurrentSchema` - Active schema name (defaults to "SeleniumDB")
- `TestsTable` - Table name for tests (default: "Test_WEB3")
- `ProcessesTable` - Table name for processes (default: "Process_WEB3")
- `FunctionsTable` - Table name for functions (default: "Function_WEB3")
- `ExtTablePrefix` - Prefix for external tables (default: "ExtTest")

**Fully Qualified Table Names (Example):**
- `[PRODUCTION_Selenium].[Test_WEB3]`
- `[PRODUCTION_Selenium].[Process_WEB3]`
- `[PRODUCTION_Selenium].[Function_WEB3]`
- `[PRODUCTION_Selenium].[ExtTest1]`, `[PRODUCTION_Selenium].[ExtTest2]`, etc.

**ConfigurationManager Integration:**
- Reads from App.config keys: "DatabaseSchema", "TestsTableName", "ProcessesTableName", "FunctionsTableName", "ExtTablePrefix"

---

## 3. DATABASE ENTITY MAPPING

### Test Entity
Maps to `[SCHEMA].[Test_WEB3]` table with columns:
- TestID (double, primary key)
- TestName (string)
- RunStatus (string)
- LastRunning (string, max 50 chars)
- LastTimePass (string)
- Bugs (string)
- ExceptionMessage (string)
- RecipientsEmailsList (string)
- SendEmailReport (string, max 100 chars)
- EmailOnFailureOnly (string)
- ExitTestOnFailure (string, max 100 chars)
- TestRunAgainTimes (string, max 100 chars)
- SnapshotMultipleFailure (string)
- DisableKillDriver (string)

**UI-Only Properties (Not in DB):**
- IsActive, IsExpanded, Category, Processes (collection), AreProcessesLoaded

### Process Entity
Maps to `[SCHEMA].[Process_WEB3]` table with columns:
- Index (int, primary key) - Unique record identifier
- ProcessID (double) - Links to functions (NOT unique, multiple processes can share)
- TestID (double) - Foreign key to Test
- ProcessName (string)
- ProcessPosition (double)
- Module (string)
- Comments (string)
- Repeat (string)
- LastRunning (string)
- WEB3Operator (string)
- Pass_Fail_WEB3Operator (string)
- Param1-Param46 (string array)
- TempParam, TempParam1, TempParam11, TempParam111, TempParam1111, TempParam11111

**UI-Only Properties:**
- IsExpanded, IsSelected, Functions (collection), AreFunctionsLoaded, DisplayIndex

### Function Entity
Maps to `[SCHEMA].[Function_WEB3]` table with columns:
- Index (int, primary key)
- ProcessID (double) - Links to functions
- FunctionName (string)
- FunctionPosition (double)
- FunctionDescription (string)
- Module (string, max 50 chars)
- Comments (string, max 50 chars)
- BreakPoint (string, max 50 chars)
- ActualValue (string)
- WEB3Operator (string)
- Pass_Fail_WEB3Operator (string)
- Param1-Param30 (string array)

**UI-Only Properties:**
- IsSelected, ParentProcess

---

## 4. SIDE MENU / NAVIGATION STRUCTURE

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml` (Lines 223-570+)
- `/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml.cs` (Lines 600-900+)

### Menu Items (in MainWindow left sidebar):

**Main Navigation (RadioButton controls):**
```
WORKSPACE Section:
├─ Tests (📝) - RadioButton, Tag="Tests"
├─ Processes (⚙️) - RadioButton, Tag="Processes"
├─ Functions (⚡) - RadioButton, Tag="Functions"
├─ ExtTables (📊) - Expandable Border with nested RadioButtons
└─ Schema (🗃️) - Expandable Border with nested RadioButtons
```

**ExtTables Section:**
- Clickable border with expand/collapse arrow animation
- Search box for filtering tables
- Displays count of found tables
- ItemsControl for dynamic table list
- Each table is a navigable item

**Schema Section:**
- Expandable border for schema selection
- ItemsControl for available schemas
- Radio buttons for schema switching

**Header Section:**
- Logo and title
- Current schema display badge
- Statistics badges (Active count, Passed count, Failed count, Running count)
- Settings, Notifications, and Profile buttons

### Click Event Handlers:
- `NavigationButton_Click()` - Handles main tab navigation (Tests, Processes, Functions)
- `ExtTablesNav_Click()` - Expands/collapses ExtTables section
- `ExtTableItem_Click()` - Opens specific ExtTable detail view
- `SchemaNav_Click()` - Expands/collapses Schema section
- `SettingsButton_Click()` - Opens Settings tab

---

## 5. MAIN WINDOW/VIEW STRUCTURE

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml` (1100+ lines)
- `/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml.cs` (1200+ lines)

### Layout Structure:
```
MetroWindow (MahApps.Metro)
├─ TitleTemplate
│  ├─ Left: App Title + Current Schema Badge
│  ├─ Center: Test Statistics (Active, Passed, Failed, Running)
│  └─ Right: Settings, Notifications, Profile buttons
│
└─ Main Grid (2-column)
   ├─ Column 0 (220px): Left Sidebar Navigation
   │  ├─ Header (Logo & Title)
   │  ├─ ScrollViewer with navigation items
   │  └─ Footer (if any)
   │
   └─ Column 1 (*): Main Content Area
      ├─ TabControl (ContentTabControl)
      │  └─ Dynamic tabs created for each view
      │
      └─ Tab Header area (shows page title and record count)
```

### Tab Management:
- Dynamic tab system - tabs created on-demand when user navigates
- Each tab has: ID, Title, Icon, Content, TabItem
- Tab close buttons for quick cleanup
- Drag-and-drop tab reordering support
- "CloseAllTabs()" method for schema reload

### Available Views (as Tabs):
1. **TestsView** - Hierarchical tree of Tests → Processes → Functions
2. **ProcessView** - Flat list of all processes
3. **FunctionView** - Flat list of all functions
4. **ExtTableDetailView** - DataGrid editor for ExtTable data
5. **NewsView** - News/updates section
6. **SettingsView** - Application settings

---

## 6. CURRENT UI PATTERNS AND STYLES

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/App.xaml` (400+ lines of styles)
- `/home/user/TestAutomationManager/TestAutomationManager/Themes/DarkTheme.xaml` (47 lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Themes/LightTheme.xaml` (47 lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Themes/RedTheme.xaml` (47 lines)

### Theme System:
**Available Themes:**
- Dark (Default) - GitHub-inspired dark theme
- Light - Bright/clean theme
- Red (Crimson Rush) - Red accent theme

### Color Palette (Dark Theme):
```
Primary Background:    #0D1117 (Very dark blue-black)
Secondary Background:  #161B22 (Slightly lighter)
Card Background:       #1C2128 (Card surfaces)
Hover Background:      #21262D (Interactive hover state)

Accent Colors:
  Primary Blue:        #539BF5 (Interactive elements)
  Primary Blue Hover:  #6CB6FF (Hover state)
  Primary Red:         #F85149 (Destructive actions)

Status Colors:
  Success:             #3FB950 (Green - passed tests)
  Warning:             #D29922 (Yellow - running tests)
  Error:               #F85149 (Red - failed tests)

Text Colors:
  Primary:             #E6EDF3 (Main text)
  Secondary:           #7D8590 (Secondary text)
  Tertiary:            #484F58 (Subtle text)

Border:                #30363D (Default borders)
Border Hover:          #484F58 (Interactive borders)
```

### Key Style Resources:

**Component Styles:**
- `ModernCard` - Card with border, background, rounded corners (8px radius)
- `NavigationRadioButton` - Navigation items with selection indicator
- `ChildSchemaRadioButton` - Nested schema selection radio buttons
- `PrimaryButton` - Main action buttons (blue background)
- `SecondaryButton` - Secondary/alternative buttons (transparent, bordered)
- `IconButton` - 32x32 icon buttons (settings, notifications, profile)
- `ModernTextBox` - Input fields
- `ModernToggle` - Toggle switches

### Design Patterns Used:
1. **Radio Button Groups** - For exclusive selection (Tests, Processes, Functions, Schemas)
2. **Cards** - For content sections and information display
3. **Icons + Text Labels** - Navigation items combine emoji/text
4. **Expandable Sections** - Collapsible navigation items (ExtTables, Schema)
5. **Animated Arrows** - Rotation animation for expand/collapse state
6. **Badge/Pill Components** - Statistics display in header
7. **Hover States** - Color changes on mouse over for interactive elements

---

## 7. EXISTING TIMER & BACKGROUND SERVICE IMPLEMENTATIONS

### DatabaseWatcherService (Real-time Sync)
**File:** `/home/user/TestAutomationManager/TestAutomationManager/Services/DatabaseWatcherService.cs` (297 lines)

**Purpose:** Monitors database for external changes and updates UI in real-time (multi-user collaboration)

**Key Features:**
- **Singleton Pattern** - Single instance across application
- **Timer-based Polling** - Uses `System.Threading.Timer` every 5 seconds (configurable)
- **Differential Change Detection** - Only detects what changed using fingerprints
- **Event-driven Updates** - Fires `DatabaseChanged` event with change details
- **Fingerprinting** - MD5-like hash comparison of important fields

**Events:**
- `DatabaseChanged` - Event fired with incremental changes only (new/changed/deleted test IDs)
- `TestsUpdated` - Legacy event for backward compatibility (full reload)

**Public Methods:**
- `StartWatching()` - Starts the polling timer
- `StopWatching()` - Stops the watcher
- `ForceCheckAsync()` - Manual immediate check for changes

**Properties:**
- `PollingIntervalSeconds` - Configurable polling interval (default: 5 seconds)
- `IsWatching` - Current watch status

**How It Works:**
1. Creates fingerprints of all test records (concat of important fields)
2. Compares with previous fingerprints to detect changes
3. Only loads changed/new tests from database (not all 700!)
4. Fires event with list of: new test IDs, changed test IDs, deleted test IDs
5. Updates fingerprint cache for next comparison

### ProcessCacheService (Caching Layer)
**File:** `/home/user/TestAutomationManager/TestAutomationManager/Services/ProcessCacheService.cs` (150+ lines)

**Purpose:** Caches Process and Function data to avoid duplicate database loads

**Thread-Safe Collections:**
- `ConcurrentBag<Process>` - All processes cache
- `ConcurrentDictionary<double, Process>` - Quick lookup by ProcessID
- `ConcurrentDictionary<double, List<Function>>` - Functions by ProcessID
- `ConcurrentDictionary<int, List<Process>>` - Processes grouped by TestID

**Statistics:**
- Tracks cache hits and misses
- Helps identify performance issues

### TestStatisticsService (Real-time Stats)
**File:** `/home/user/TestAutomationManager/TestAutomationManager/Services/Statistics/TestStatisticsService.cs` (150+ lines)

**Purpose:** Tracks and updates test statistics in real-time

**Tracks:**
- `ActiveCount` - Number of active tests
- `PassedCount` - Number of passed tests
- `FailedCount` - Number of failed tests
- `RunningCount` - Number of running tests

**Features:**
- `INotifyPropertyChanged` for UI binding
- `ForceRefresh()` - Manual statistics refresh
- Updates header badges in real-time

### Other Services:

**NewsService** - Fetches and manages news/updates
**TestEditService** - Handles test editing operations
**TestUISettingsService** - Persists UI settings per test
**ThemeService** - Theme switching functionality
**SchemaConfigService** - Schema management and switching

---

## 8. REPOSITORIES & DATA ACCESS LAYER

### Files:
- `/home/user/TestAutomationManager/TestAutomationManager/Repositories/TestRepository.cs` (500+ lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Repositories/ProcessRepository.cs` (300+ lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExtTableRepository.cs` (350+ lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExtTableDataRepository.cs` (400+ lines)
- `/home/user/TestAutomationManager/TestAutomationManager/Repositories/ITestRepository.cs` (Interface)

### ITestRepository Interface:
Defines contracts for:
- `GetAllTestsAsync()` - Load all tests via stored procedure
- `GetTestByIdAsync()` - Load single test
- `GetProcessesForTestAsync()` - Load processes for test
- `GetFunctionsForProcessAsync()` - Load functions for process
- `InsertTestAsync()` - Create new test
- `UpdateTestAsync()` - Save test changes
- `DeleteTestAsync()` - Delete test
- `TestIdExistsAsync()` - Check if test ID exists
- Bulk operations and search methods

### TestRepository Implementation:
**Key Methods:**
- Uses schema-qualified stored procedures for 4-5x performance improvement
- Example: `EXEC [PRODUCTION_Selenium].[usp_GetAllTests]`
- Lazy loading of processes and functions
- Supports both EF Core queries and raw SQL

**Optimization Techniques:**
- Stored procedure execution via `FromSqlRaw()`
- Parameterized queries to prevent SQL injection
- Lazy loading indicators to avoid over-fetching
- ObservableCollection initialization for UI binding

### ExtTableRepository:
**Capabilities:**
- Check if ExtTable exists
- Create new ExtTable from template
- Delete ExtTable
- Get row counts
- Discover external tables dynamically

### ExtTableDataRepository:
**CRUD Operations:**
- Update cell values (with column length validation)
- Add columns dynamically
- Rename columns (via SQL sp_rename)
- Expand column size (ALTER COLUMN)
- Get column metadata

---

## 9. SUMMARY - KEY FACTS FOR BACKUP FEATURE

### Connection Points:
1. **DbConnectionConfig** - Central connection configuration (modify for backup connections)
2. **TestAutomationDbContext** - Entity Framework context (for backup DB operations)
3. **Repositories** - Data access layer (can be extended for backup operations)
4. **Services** - Singleton services ideal for backup scheduling

### Available Patterns to Follow:
1. **Timer Pattern** - DatabaseWatcherService uses `System.Threading.Timer` for periodic tasks
2. **Singleton Services** - SchemaConfigService, TestStatisticsService, ProcessCacheService models
3. **Event System** - DatabaseChanged event for notifying UI of changes
4. **Async/Await** - All data operations use async Task patterns
5. **Repository Pattern** - Centralized data access in repositories

### Database Operations:
- All database access via EntityFrameworkCore
- Raw SQL support via `FromSqlRaw()` and `FromSqlInterpolated()`
- Stored procedures support for complex operations
- Transaction support available

### Menu Integration Points:
- SettingsView has empty space for new settings sections
- MainWindow navigation can be extended with backup menu items
- Header toolbar has space for new status indicators
- SettingsButton click handler for settings access

### UI Components Available:
- Modern card components with borders
- Button styles (Primary, Secondary, Icon)
- Status color indicators (Success=Green, Warning=Yellow, Error=Red)
- Badge/pill components for status display
- Animated expand/collapse patterns
- Async operation progress patterns

---

## File Structure Summary

```
/TestAutomationManager/
├─ Data/
│  ├─ DbConnectionConfig.cs         (Connection setup)
│  ├─ TestAutomationDbContext.cs    (EF Core context)
│  └─ SchemaConfig.cs               (Schema management)
├─ Models/
│  └─ DataModels.cs                 (Test, Process, Function entities)
├─ Repositories/
│  ├─ ITestRepository.cs            (Interface)
│  ├─ TestRepository.cs             (Tests CRUD)
│  ├─ ProcessRepository.cs          (Processes CRUD)
│  └─ ExtTableRepository.cs         (ExtTable operations)
├─ Services/
│  ├─ DatabaseWatcherService.cs     (Real-time sync with Timer)
│  ├─ ProcessCacheService.cs        (Caching)
│  ├─ TestStatisticsService.cs      (Statistics tracking)
│  ├─ SchemaConfigService.cs        (Schema switching)
│  ├─ TestEditService.cs            (Test editing)
│  ├─ NewsService.cs                (News/updates)
│  ├─ TestUISettingsService.cs      (UI persistence)
│  └─ Statistics/
│     └─ TestStatisticsService.cs
├─ Views/
│  ├─ TestsView.xaml                (Hierarchical test display)
│  ├─ ProcessView.xaml              (Process list)
│  ├─ FunctionView.xaml             (Function list)
│  ├─ ExtTableDetailView.xaml       (DataGrid editor)
│  ├─ SettingsView.xaml             (Settings UI)
│  └─ NewsView.xaml                 (News display)
├─ MainWindow.xaml                  (Main UI with navigation)
├─ App.xaml                         (Styles and themes)
├─ Themes/
│  ├─ DarkTheme.xaml                (Color palette)
│  ├─ LightTheme.xaml               (Alternative theme)
│  └─ RedTheme.xaml                 (Alternative theme)
└─ TestAutomationManager.csproj     (Project file)
```

---

## Key Dependencies
- **MahApps.Metro** (2.4.11) - WPF theme/styling
- **MaterialDesignThemes** (5.3.0) - Material Design components
- **Microsoft.EntityFrameworkCore.SqlServer** (9.0.10) - ORM
- **System.Data.SqlClient** (4.9.0) - SQL Server connectivity
- **System.Configuration.ConfigurationManager** (9.0.10) - Config file support

