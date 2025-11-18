using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog for creating a new test with ExtTable configuration
    /// </summary>
    public partial class AddTestDialog : Window
    {
        // ================================================
        // FIELDS
        // ================================================

        private readonly ITestRepository _testRepository;
        private readonly IExtTableRepository _extTableRepository;
        private readonly IProcessRepository _processRepository;

        private ObservableCollection<ExternalTableInfo> _allExtTables;
        private ObservableCollection<ExternalTableInfo> _filteredExtTables;

        private int? _suggestedTestId;
        private string _selectedSourceTable = "ExtTest1"; // Default template
        private bool _isManualIdMode = false;
        private List<int> _allExistingTestIds = new List<int>();

        // ================================================
        // PROPERTIES
        // ================================================

        /// <summary>
        /// The created test (set after successful creation)
        /// </summary>
        public Test CreatedTest { get; private set; }

        /// <summary>
        /// Whether the test was successfully created
        /// </summary>
        public bool IsSuccess { get; private set; }

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public AddTestDialog()
        {
            InitializeComponent();

            // Initialize repositories
            _testRepository = new TestRepository();
            _extTableRepository = new ExtTableRepository();
            _processRepository = new ProcessRepository();

            // Initialize collections
            _allExtTables = new ObservableCollection<ExternalTableInfo>();
            _filteredExtTables = new ObservableCollection<ExternalTableInfo>();

            // Bind filtered list to UI
            ExtTablesList.ItemsSource = _filteredExtTables;

            // Wire up search
            ExtTableSearchBox.TextChanged += ExtTableSearch_TextChanged;

            // Load initial data
            _ = LoadInitialDataAsync();
        }

        // ================================================
        // INITIALIZATION
        // ================================================

        /// <summary>
        /// Load initial data (available IDs and ExtTables)
        /// </summary>
        private async Task LoadInitialDataAsync()
        {
            try
            {
                StatusMessage.Text = "Loading...";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"];

                // Get all existing tests to track IDs
                var allTests = await _testRepository.GetAllTestsAsync();
                _allExistingTestIds = allTests.Select(t => t.Id).OrderBy(id => id).ToList();

                // Get suggested test ID
                _suggestedTestId = await _testRepository.GetNextAvailableTestIdAsync();

                if (_suggestedTestId.HasValue)
                {
                    TestIdTextBox.Text = _suggestedTestId.Value.ToString();
                    SuggestedIdRun.Text = _suggestedTestId.Value.ToString();
                }

                // Load all ExtTables
                var extTables = await _testRepository.GetAllExternalTablesAsync();
                _allExtTables.Clear();
                foreach (var table in extTables.OrderBy(t => t.TestId))
                {
                    _allExtTables.Add(table);
                    _filteredExtTables.Add(table);
                }

                StatusMessage.Text = $"Ready • {_allExtTables.Count} ExtTables available";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["SuccessBrush"];

                System.Diagnostics.Debug.WriteLine($"✓ Dialog initialized: Suggested ID = {_suggestedTestId}, ExtTables = {_allExtTables.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading initial data: {ex.Message}");
                StatusMessage.Text = "Error loading data";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["ErrorBrush"];
            }
        }

        // ================================================
        // ID MODE HANDLERS
        // ================================================

        /// <summary>
        /// Handle Auto ID selection
        /// </summary>
        private void AutoIdRadio_Checked(object sender, RoutedEventArgs e)
        {
            _isManualIdMode = false;

            if (TestIdTextBox != null)
            {
                TestIdTextBox.IsReadOnly = true;
                TestIdTextBox.Background = (Brush)Application.Current.Resources["CardBackgroundBrush"];

                if (_suggestedTestId.HasValue)
                {
                    TestIdTextBox.Text = _suggestedTestId.Value.ToString();
                }

                SuggestedIdInfo.Visibility = Visibility.Visible;
                ValidationMessage.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle Manual ID selection
        /// </summary>
        private void ManualIdRadio_Checked(object sender, RoutedEventArgs e)
        {
            _isManualIdMode = true;

            if (TestIdTextBox != null)
            {
                TestIdTextBox.IsReadOnly = false;
                TestIdTextBox.Background = (Brush)Application.Current.Resources["SecondaryBackgroundBrush"];
                TestIdTextBox.Focus();
                TestIdTextBox.SelectAll();

                SuggestedIdInfo.Visibility = Visibility.Collapsed;

                // Validate current input immediately
                _ = ValidateTestIdAsync();
            }
        }

        /// <summary>
        /// Real-time validation when user types in Test ID
        /// </summary>
        private async void TestIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only validate in manual mode
            if (_isManualIdMode)
            {
                await ValidateTestIdAsync();
            }
        }

        /// <summary>
        /// Validate Test ID when manually entered
        /// </summary>
        private async Task<bool> ValidateTestIdAsync()
        {
            if (!_isManualIdMode)
                return true;

            ValidationMessage.Visibility = Visibility.Collapsed;

            // Check if valid integer
            if (!int.TryParse(TestIdTextBox.Text, out int testId))
            {
                ShowValidationError("Please enter a valid number");
                return false;
            }

            // Check if ID > 0
            if (testId <= 0)
            {
                ShowValidationError("ID must be greater than 0");
                return false;
            }

            // Check if ID already exists - REAL-TIME CHECK
            bool exists = await _testRepository.TestIdExistsAsync(testId);
            if (exists)
            {
                ShowValidationError($"Test {testId} already exists!");
                return false;
            }

            // Valid ID
            ValidationMessage.Visibility = Visibility.Collapsed;
            return true;
        }

        // ================================================
        // EXTTABLE MODE HANDLERS
        // ================================================

        /// <summary>
        /// Handle Template selection
        /// </summary>
        private void TemplateExtRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (TemplateInfo != null && CopyFromPanel != null)
            {
                TemplateInfo.Visibility = Visibility.Visible;
                CopyFromPanel.Visibility = Visibility.Collapsed;
                _selectedSourceTable = "ExtTest1"; // Default template
            }
        }

        /// <summary>
        /// Handle Copy From selection
        /// </summary>
        private void CopyExtRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (TemplateInfo != null && CopyFromPanel != null)
            {
                TemplateInfo.Visibility = Visibility.Collapsed;
                CopyFromPanel.Visibility = Visibility.Visible;

                // Reset selection if no table selected
                if (string.IsNullOrEmpty(_selectedSourceTable) || _selectedSourceTable == "ExtTest1")
                {
                    _selectedSourceTable = null;
                }
            }
        }

        /// <summary>
        /// Show validation error message
        /// </summary>
        private void ShowValidationError(string message)
        {
            ValidationTextRun.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
        }

        // ================================================
        // FIND FREE TESTS FEATURE
        // ================================================

        /// <summary>
        /// Find and display FREE tests (tests that exist but are marked as available for reuse)
        /// </summary>
        private async void FindFreeTestsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var freeTestIds = await _testRepository.GetFreeTestIdsAsync();

                if (freeTestIds.Count == 0)
                {
                    ModernMessageDialog.ShowInfo(
                        "No FREE tests found.\n\n" +
                        "FREE tests are existing tests that have \"FREE\" in their name or status, " +
                        "indicating they are available for reuse.",
                        "No FREE Tests",
                        Window.GetWindow(this));
                }
                else
                {
                    string message = $"Found {freeTestIds.Count} FREE test(s) available for reuse:\n\n" +
                                   string.Join(", ", freeTestIds.Select(id => $"Test #{id}")) + "\n\n" +
                                   "These tests exist in the database but are marked as FREE, " +
                                   "indicating they can be reused or repurposed.";

                    ModernMessageDialog.ShowInfo(message, "FREE Tests Found", Window.GetWindow(this));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error finding FREE tests: {ex.Message}");
                ModernMessageDialog.ShowInfo(
                    $"Failed to find FREE tests.\n\nError: {ex.Message}",
                    "Error",
                    Window.GetWindow(this));
            }
        }

        // ================================================
        // EXTTABLE HANDLERS
        // ================================================

        /// <summary>
        /// Handle ExtTable mode change
        /// </summary>
        private void ExtTableMode_Changed(object sender, RoutedEventArgs e)
        {
            if (CopyFromPanel == null) return;

            if (CopyExtRadio.IsChecked == true)
            {
                CopyFromPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CopyFromPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle ExtTable selection from list
        /// </summary>
        private void ExtTableSelection_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tableName)
            {
                _selectedSourceTable = tableName;
                System.Diagnostics.Debug.WriteLine($"Selected ExtTable: {tableName}");
            }
        }

        /// <summary>
        /// Handle ExtTable search
        /// </summary>
        private void ExtTableSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchQuery = ExtTableSearchBox.Text?.Trim() ?? "";

            // Toggle placeholder visibility
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(searchQuery)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            _filteredExtTables.Clear();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                // Show all
                foreach (var table in _allExtTables)
                {
                    _filteredExtTables.Add(table);
                }
            }
            else
            {
                // Filter by search
                var filtered = _allExtTables.Where(t =>
                    t.TableName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.TestName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.TestId.ToString().Contains(searchQuery)
                ).ToList();

                foreach (var table in filtered)
                {
                    _filteredExtTables.Add(table);
                }
            }
        }

        // ================================================
        // CREATE TEST
        // ================================================

        /// <summary>
        /// Handle Create button click
        /// </summary>
        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable UI during creation
                CreateButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                StatusMessage.Text = "Creating test...";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["WarningBrush"];

                // ========== VALIDATION ==========

                // Validate Test ID
                if (!int.TryParse(TestIdTextBox.Text, out int testId))
                {
                    ModernMessageDialog.ShowInfo("Please enter a valid Test ID", "Validation Error",
                        Window.GetWindow(this));
                    return;
                }

                if (_isManualIdMode && !await ValidateTestIdAsync())
                {
                    ModernMessageDialog.ShowInfo($"Test ID {testId} is not valid or already exists!",
                        "Validation Error", Window.GetWindow(this));

                    return;
                }

                // Validate Test Name
                if (string.IsNullOrWhiteSpace(TestNameTextBox.Text))
                {
                    MessageBox.Show("Please enter a Test Name", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TestNameTextBox.Focus();
                    return;
                }

                // Validate ExtTable source
                if (CopyExtRadio.IsChecked == true && string.IsNullOrEmpty(_selectedSourceTable))
                {
                    MessageBox.Show("Please select an ExtTable to copy from", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ========== CREATE TEST ==========
                StatusMessage.Text = $"Creating Test #{testId}...";

                var newTest = new Test
                {
                    Id = testId,
                    Name = TestNameTextBox.Text.Trim(),
                    Description = DescriptionTextBox.Text?.Trim() ?? "",
                    Category = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                              ?? CategoryComboBox.Text?.Trim()
                              ?? "General",
                    IsActive = true,
                    Status = "Not Run",
                    LastRun = DateTime.Now,
                    Processes = new ObservableCollection<Process>()
                };

                // Insert test into database
                await _testRepository.InsertTestAsync(newTest);
                System.Diagnostics.Debug.WriteLine($"✓ Test #{testId} created in database");

                // ========== CREATE EXTTABLE ==========
                StatusMessage.Text = $"Creating ExtTable{testId}...";

                string newTableName = $"ExtTest{testId}";
                string sourceTable = _selectedSourceTable ?? "ExtTest1";

                // Check if ExtTable already exists
                bool extTableExists = await _extTableRepository.ExtTableExistsAsync(newTableName);

                if (extTableExists)
                {
                    var result = MessageBox.Show(
                        $"⚠ Notice: {newTableName} already exists!\n\n" +
                        "Do you want to replace it with a new one copied from " +
                        $"{sourceTable}?\n\n" +
                        "Click Yes to replace it, or No to keep the existing one.",
                        "External Table Already Exists",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Delete and recreate
                        await _extTableRepository.DeleteExtTableAsync(newTableName);
                        await _extTableRepository.CreateExtTableFromTemplateAsync(newTableName, sourceTable);
                        System.Diagnostics.Debug.WriteLine($"✓ {newTableName} replaced from {sourceTable}");
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        System.Diagnostics.Debug.WriteLine($"ℹ Kept existing {newTableName}");
                    }
                    else
                    {
                        // Cancel entire creation flow
                        MessageBox.Show("Test creation cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                        StatusMessage.Text = "Creation cancelled.";
                        StatusMessage.Foreground = (Brush)Application.Current.Resources["WarningBrush"];
                        IsSuccess = false;
                        return;
                    }
                }
                else
                {
                    // Create new ExtTable normally
                    await _extTableRepository.CreateExtTableFromTemplateAsync(newTableName, sourceTable);
                    System.Diagnostics.Debug.WriteLine($"✓ {newTableName} created from {sourceTable}");
                }

                // ========== ADD SELECTED PROCESSES ==========
                var selectedProcessIds = new List<int>();
                if (Process988CheckBox.IsChecked == true) selectedProcessIds.Add(988);
                if (Process271CheckBox.IsChecked == true) selectedProcessIds.Add(271);
                if (Process137CheckBox.IsChecked == true) selectedProcessIds.Add(137);

                if (selectedProcessIds.Count > 0)
                {
                    StatusMessage.Text = $"Adding {selectedProcessIds.Count} process(es) to test...";

                    foreach (var processId in selectedProcessIds)
                    {
                        try
                        {
                            // Get the process template
                            var processTemplate = await _processRepository.GetProcessTemplateByIdAsync(processId);

                            if (processTemplate != null)
                            {
                                // Clone the process with new TestID
                                var newProcess = new Process
                                {
                                    TestID = testId,
                                    ProcessID = processTemplate.ProcessID,
                                    ProcessName = processTemplate.ProcessName,
                                    ProcessPosition = processTemplate.ProcessPosition,
                                    Module = processTemplate.Module,
                                    Comments = processTemplate.Comments,
                                    Repeat = processTemplate.Repeat,
                                    WEB3Operator = processTemplate.WEB3Operator,
                                    Pass_Fail_WEB3Operator = processTemplate.Pass_Fail_WEB3Operator,
                                    LastRunning = processTemplate.LastRunning,
                                    TempParam = processTemplate.TempParam,
                                    TempParam1 = processTemplate.TempParam1,
                                    TempParam11 = processTemplate.TempParam11,
                                    TempParam111 = processTemplate.TempParam111,
                                    TempParam1111 = processTemplate.TempParam1111,
                                    TempParam11111 = processTemplate.TempParam11111,
                                    // Copy all Param columns
                                    Param1 = processTemplate.Param1,
                                    Param2 = processTemplate.Param2,
                                    Param3 = processTemplate.Param3,
                                    Param4 = processTemplate.Param4,
                                    Param5 = processTemplate.Param5,
                                    Param6 = processTemplate.Param6,
                                    Param7 = processTemplate.Param7,
                                    Param8 = processTemplate.Param8,
                                    Param9 = processTemplate.Param9,
                                    Param10 = processTemplate.Param10,
                                    Param11 = processTemplate.Param11,
                                    Param12 = processTemplate.Param12,
                                    Param13 = processTemplate.Param13,
                                    Param14 = processTemplate.Param14,
                                    Param15 = processTemplate.Param15,
                                    Param16 = processTemplate.Param16,
                                    Param17 = processTemplate.Param17,
                                    Param18 = processTemplate.Param18,
                                    Param19 = processTemplate.Param19,
                                    Param20 = processTemplate.Param20,
                                    Param21 = processTemplate.Param21,
                                    Param22 = processTemplate.Param22,
                                    Param23 = processTemplate.Param23,
                                    Param24 = processTemplate.Param24,
                                    Param25 = processTemplate.Param25,
                                    Param26 = processTemplate.Param26,
                                    Param27 = processTemplate.Param27,
                                    Param28 = processTemplate.Param28,
                                    Param29 = processTemplate.Param29,
                                    Param30 = processTemplate.Param30,
                                    Param31 = processTemplate.Param31,
                                    Param32 = processTemplate.Param32,
                                    Param33 = processTemplate.Param33,
                                    Param34 = processTemplate.Param34,
                                    Param35 = processTemplate.Param35,
                                    Param36 = processTemplate.Param36,
                                    Param37 = processTemplate.Param37,
                                    Param38 = processTemplate.Param38,
                                    Param39 = processTemplate.Param39,
                                    Param40 = processTemplate.Param40,
                                    Param41 = processTemplate.Param41,
                                    Param42 = processTemplate.Param42,
                                    Param43 = processTemplate.Param43,
                                    Param44 = processTemplate.Param44,
                                    Param45 = processTemplate.Param45,
                                    Param46 = processTemplate.Param46,
                                    Functions = new ObservableCollection<Function>()
                                };

                                // Insert the process
                                await _processRepository.InsertProcessAsync(newProcess);
                                System.Diagnostics.Debug.WriteLine($"✓ Added Process #{processId} to Test #{testId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to add Process #{processId}: {ex.Message}");
                            // Continue with other processes even if one fails
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Added {selectedProcessIds.Count} process(es) to test");
                }

                // ========== SUCCESS ==========
                CreatedTest = newTest;
                IsSuccess = true;

                StatusMessage.Text = "✓ Test created successfully!";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["SuccessBrush"];

                // Show success message
                string successMessage = $"Test #{testId} '{newTest.Name}' has been created successfully!\n\n" +
                    $"• Test added to database\n" +
                    $"• {newTableName} created from {sourceTable}";

                if (selectedProcessIds.Count > 0)
                {
                    successMessage += $"\n• {selectedProcessIds.Count} process(es) added to test";
                }

                successMessage += "\n\nThe UI will refresh to show the new test.";

                ModernMessageDialog.ShowInfo(successMessage, "Success", Window.GetWindow(this));

                // Close dialog
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error creating test: {ex.Message}");

                StatusMessage.Text = "Failed to create test";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["ErrorBrush"];

                ModernMessageDialog.ShowInfo(
                    $"Failed to create test.\n\nError: {ex.Message}",
                    "Error",
                    Window.GetWindow(this));
            }
            finally
            {
                // Re-enable UI
                CreateButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Handle Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsSuccess = false;
            DialogResult = false;
            Close();
        }
    }
}