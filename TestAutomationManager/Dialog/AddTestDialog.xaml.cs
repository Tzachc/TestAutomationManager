using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private ObservableCollection<ExternalTableInfo> _allExtTables;
        private ObservableCollection<ExternalTableInfo> _filteredExtTables;

        private int? _suggestedTestId;
        private string _selectedSourceTable = "ExtTable1"; // Default template
        private bool _isManualIdMode = false;

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

                // Get suggested test ID
                _suggestedTestId = await _testRepository.GetNextAvailableTestIdAsync();

                if (_suggestedTestId.HasValue)
                {
                    TestIdTextBox.Text = _suggestedTestId.Value.ToString();
                    SuggestedIdRun.Text = _suggestedTestId.Value.ToString();

                    // Check if it's a gap or next sequential
                    var allTests = await _testRepository.GetAllTestsAsync();
                    if (allTests.Any() && _suggestedTestId.Value <= allTests.Max(t => t.Id))
                    {
                        SuggestedIdInfo.ToolTip = $"Found gap at position {_suggestedTestId.Value}";
                    }
                    else
                    {
                        SuggestedIdInfo.ToolTip = $"Next sequential ID after {allTests.Max(t => t.Id)}";
                    }
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

                // Validate current input
                _ = ValidateTestIdAsync();
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

            // Check if ID already exists
            bool exists = await _testRepository.TestIdExistsAsync(testId);
            if (exists)
            {
                ShowValidationError($"Test ID {testId} already exists!");
                return false;
            }

            // Check if ExtTable already exists
            bool extTableExists = await _extTableRepository.ExtTableExistsAsync($"ExtTable{testId}");
            if (extTableExists)
            {
                ShowValidationError($"ExtTable{testId} already exists!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Show validation error message
        /// </summary>
        private void ShowValidationError(string message)
        {
            ValidationText.Text = $"⚠ {message}";
            ValidationMessage.Visibility = Visibility.Visible;
            SuggestedIdInfo.Visibility = Visibility.Collapsed;
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
                _selectedSourceTable = "ExtTable1"; // Default template
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
                if (string.IsNullOrEmpty(_selectedSourceTable) || _selectedSourceTable == "ExtTable1")
                {
                    _selectedSourceTable = null;
                }
            }
        }

        /// <summary>
        /// Handle ExtTable selection
        /// </summary>
        private void ExtTableSelection_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string tableName)
            {
                _selectedSourceTable = tableName;
                StatusMessage.Text = $"Selected: {tableName}";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"];
            }
        }

        /// <summary>
        /// Handle ExtTable search
        /// </summary>
        private void ExtTableSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchQuery = ExtTableSearchBox.Text?.Trim() ?? "";

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

                string newTableName = $"ExtTable{testId}";
                string sourceTable = _selectedSourceTable ?? "ExtTable1";

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


                // ========== SUCCESS ==========
                CreatedTest = newTest;
                IsSuccess = true;

                StatusMessage.Text = "✓ Test created successfully!";
                StatusMessage.Foreground = (Brush)Application.Current.Resources["SuccessBrush"];

                // Show success message
                ModernMessageDialog.ShowInfo(
                    $"Test #{testId} '{newTest.Name}' has been created successfully!\n\n" +
                    $"• Test added to database\n" +
                    $"• {newTableName} created from {sourceTable}\n\n" +
                    "The UI will refresh to show the new test.",
                    "Success",
                    Window.GetWindow(this));

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