# File Paths Reference

## All Absolute File Paths for Analysis

### Data Models & Database
```
/home/user/TestAutomationManager/TestAutomationManager/Models/DataModels.cs
  - Test class (lines 9-123)
  - Process class (lines 125-197)
  - Function class (lines 199-257)
  - ExternalTableInfo class (lines 261-268)
  - ExternalTableRow class (lines 273-287)

/home/user/TestAutomationManager/TestAutomationManager/Models/ExtTableLayout.cs
  - ExtTableLayout class

/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/TestAutomationDbContext.cs
  - EF Core DbContext
  - Database configuration
  - Entity mappings

/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager/Data/DbConnectionConfig.cs
  - Connection string management
  - DEFAULT_CONNECTION_STRING constant
  - GetConnectionString() method
  - BuildConnectionString() method
  - TestConnection() method
```

### Repositories
```
/home/user/TestAutomationManager/TestAutomationManager/Repositories/ITestRepository.cs
  - ITestRepository interface definition

/home/user/TestAutomationManager/TestAutomationManager/Repositories/TestRepository.cs
  - GetAllTestsAsync()
  - GetTestByIdAsync()
  - InsertTestAsync()
  - UpdateTestAsync()
  - DeleteTestAsync()
  - GetAllExternalTablesAsync()
  - GetNextAvailableTestIdAsync()
  - TestIdExistsAsync()

/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExtTableRepository.cs
  - ExtTableExistsAsync()
  - CreateExtTableFromTemplateAsync()
  - DeleteExtTableAsync()
  - GetExtTableRowCountAsync()
  - GetTableColumnDefinitionsAsync()
  - BuildCreateTableQuery()
  - GetSqlDataType()
  - ColumnDefinition helper class

/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExtTableDataRepository.cs
  - UpdateCellValueAsync()
  - GetColumnInfoAsync()
  - ColumnInfo helper class
  - RenameColumnAsync()
  - ColumnExistsAsync()
  - IsValidColumnName()
  - GetColumnDataTypeAsync()
  - ExpandColumnSizeAsync()
  - AddColumnAsync()

/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExternalTableRepository.cs
  - GetAllExternalTablesAsync()
  - GetRowCountAsync()

/home/user/TestAutomationManager/TestAutomationManager/Repositories/ExtTableLayoutRepository.cs
  - SaveLayoutAsync()
  - GetLayoutAsync()
  - DeleteLayoutAsync()
  - LayoutExistsAsync()
  - GetAllLayoutTablesAsync()
```

### Views (XAML & Code-Behind)
```
/home/user/TestAutomationManager/TestAutomationManager/Views/TestsView.xaml
  - XAML markup for test list display
  - Hierarchical ItemsControl template
  - Action buttons (Edit, Run, Delete)

/home/user/TestAutomationManager/TestAutomationManager/Views/TestsView.xaml.cs
  - TestsView UserControl code-behind
  - LoadTestsFromDatabase()
  - FilterTests()
  - OnDatabaseTestsUpdated()
  - ForceRefreshStatistics()
  - FocusTest()
  - EditTest_Click()
  - RunTest_Click()
  - DeleteTest_Click()
  - StartLiveUpdates()

/home/user/TestAutomationManager/TestAutomationManager/Views/ExtTableDetailView.xaml
  - XAML for external table detail view

/home/user/TestAutomationManager/TestAutomationManager/Views/ExtTableDetailView.xaml.cs
  - ExtTableDetailView UserControl code-behind
  - LoadTableData()
  - LoadTableDataFromDatabase()
  - TableDataGrid_CellEditEnding()
  - SaveLayout()

/home/user/TestAutomationManager/TestAutomationManager/Views/SettingsView.xaml
  - XAML for settings view

/home/user/TestAutomationManager/TestAutomationManager/Views/SettingsView.xaml.cs
  - SettingsView UserControl code-behind
```

### Main Application
```
/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml
  - Main application window XAML
  - Tab control for views
  - Status bar with statistics

/home/user/TestAutomationManager/TestAutomationManager/MainWindow.xaml.cs
  - MainWindow MetroWindow code-behind
  - Tab management
  - External tables list
  - Database connection check

/home/user/TestAutomationManager/TestAutomationManager/App.xaml
  - Application-level XAML

/home/user/TestAutomationManager/TestAutomationManager/App.xaml.cs
  - App class
  - OnStartup() - applies theme
```

### Services
```
/home/user/TestAutomationManager/TestAutomationManager/Services/DatabaseWatcherService.cs
  - Singleton pattern implementation
  - StartWatching()
  - StopWatching()
  - ForceCheckAsync()
  - CheckForChangesAsync()
  - CalculateDataHash()
  - TestsUpdated event

/home/user/TestAutomationManager/TestAutomationManager/Services/ThemeService.cs
  - ApplyTheme() method
  - Theme switching logic

/home/user/TestAutomationManager/TestAutomationManager/Services/Statistics/TestStatisticsService.cs
  - UpdateStatistics() method
  - Statistics calculation

/home/user/TestAutomationManager/TestAutomationManager/Services/GlobalSearchHelper.cs
  - Global search functionality

/home/user/TestAutomationManager/TestAutomationManager/Services/VisualSearchHighlighter.cs
  - Search result highlighting

/home/user/TestAutomationManager/TestAutomationManager/Services/ViewportScroller.cs
  - Scrolling utility
```

### Dialogs & Exceptions
```
/home/user/TestAutomationManager/TestAutomationManager/Dialog/AddTestDialog.xaml
  - Add test dialog XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/AddTestDialog.xaml.cs
  - AddTestDialog Window code-behind
  - Test creation logic
  - ExtTable selection and copying

/home/user/TestAutomationManager/TestAutomationManager/Dialog/AddColumnDialog.xaml
  - Add column dialog XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/AddColumnDialog.xaml.cs
  - AddColumnDialog code-behind

/home/user/TestAutomationManager/TestAutomationManager/Dialog/EditColumnLengthDialog.xaml
  - Edit column length dialog XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/EditColumnLengthDialog.xaml.cs
  - EditColumnLengthDialog code-behind

/home/user/TestAutomationManager/TestAutomationManager/Dialog/RenameColumnDialog.xaml
  - Rename column dialog XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/RenameColumnDialog.xaml.cs
  - RenameColumnDialog code-behind

/home/user/TestAutomationManager/TestAutomationManager/Dialog/ModernMessageDialog.xaml
  - Custom message dialog XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/ModernMessageDialog.xaml.cs
  - ModernMessageDialog code-behind
  - ShowConfirmation()
  - ShowSuccess()
  - ShowError()

/home/user/TestAutomationManager/TestAutomationManager/Dialog/SearchOverlay.xaml
  - Search overlay XAML

/home/user/TestAutomationManager/TestAutomationManager/Dialog/SearchOverlay.xaml.cs
  - SearchOverlay code-behind

/home/user/TestAutomationManager/TestAutomationManager/Dialog/SearchHelper.cs
  - Search utility methods

/home/user/TestAutomationManager/TestAutomationManager/Exceptions/ColumnLengthExceededException.cs
  - Custom exception for column validation
```

### Themes
```
/home/user/TestAutomationManager/TestAutomationManager/Themes/DarkTheme.xaml
  - Dark theme resources

/home/user/TestAutomationManager/TestAutomationManager/Themes/LightTheme.xaml
  - Light theme resources

/home/user/TestAutomationManager/TestAutomationManager/Themes/RedTheme.xaml
  - Red theme resources
```

### Project Configuration
```
/home/user/TestAutomationManager/TestAutomationManager/TestAutomationManager.csproj
  - Project file
  - Dependencies
  - Build configuration
```

---

## Summary of Components

### View-Layer Files
- TestsView (XAML + CS) - Hierarchical test display
- ExtTableDetailView (CS) - External table editor
- SettingsView (XAML + CS) - Settings/theme switching
- MainWindow (XAML + CS) - Main application window

### Data-Layer Files
- DataModels.cs - Test, Process, Function classes
- ExtTableLayout.cs - Layout configuration model
- TestAutomationDbContext.cs - EF Core context
- DbConnectionConfig.cs - Connection management

### Repository-Layer Files
- ITestRepository.cs - Test operations interface
- TestRepository.cs - Test CRUD operations
- ExtTableRepository.cs - ExtTable creation/deletion
- ExtTableDataRepository.cs - Cell/column operations
- ExternalTableRepository.cs - Table discovery
- ExtTableLayoutRepository.cs - Layout persistence

### Service-Layer Files
- DatabaseWatcherService.cs - Live sync (polling)
- TestStatisticsService.cs - Statistics calculation
- ThemeService.cs - Theme switching

### Dialog/Exception Files
- AddTestDialog.xaml.cs - New test creation
- ModernMessageDialog.xaml.cs - Custom dialogs
- ColumnLengthExceededException.cs - Input validation
- SearchHelper.cs - Search utilities

### Theme Files
- DarkTheme.xaml - Dark color scheme
- LightTheme.xaml - Light color scheme
- RedTheme.xaml - Red color scheme

---

## Key Paths for Data Loading

1. **Initial Data Load**:
   - TestsView.xaml.cs constructor
   - → LoadTestsFromDatabase()
   - → TestRepository.GetAllTestsAsync()
   - → EF Core DbContext.Tests.Include().ThenInclude()
   - → SQL Server database

2. **Saving Changes**:
   - Test model (DataModels.cs)
   - → SaveToDatabase() method
   - → TestRepository.UpdateTestAsync()
   - → EF Core DbContext.SaveChangesAsync()
   - → SQL Server database

3. **Live Sync**:
   - DatabaseWatcherService.cs
   - → Timer polling every 3 seconds
   - → MD5 hash comparison
   - → TestsUpdated event
   - → TestsView.OnDatabaseTestsUpdated()

4. **External Table Data**:
   - ExtTableDetailView.xaml.cs
   - → LoadTableDataFromDatabase()
   - → SqlCommand to ext.[TableName]
   - → DataTable binding to DataGrid

