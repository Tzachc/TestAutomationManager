using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TestAutomationManager.Dialogs;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Services
{
    /// <summary>
    /// Service for handling inline editing of Tests, Processes, and Functions
    /// Features:
    /// - Validation (e.g., TestID uniqueness)
    /// - Confirmation dialogs
    /// - Database updates
    /// - Real-time synchronization via DatabaseWatcherService
    /// </summary>
    public class TestEditService
    {
        private readonly ITestRepository _repository;
        private readonly ProcessRepository _processRepository;

        public TestEditService(
            ITestRepository testRepository,
            ProcessRepository processRepository)
        {
            _repository = testRepository ?? throw new ArgumentNullException(nameof(testRepository));
            _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        }

        // ================================================
        // TEST EDITING
        // ================================================

        /// <summary>
        /// Validate and edit a test field
        /// </summary>
        public async Task<EditResult> EditTestFieldAsync(Test test, string fieldName, string oldValue, string newValue, Window owner = null)
        {
            try
            {
                if (test == null)
                    return EditResult.Failed("Test is null");

                // Use reflection to set any property
                var property = typeof(Test).GetProperty(fieldName);
                if (property == null)
                    return EditResult.Failed($"Unknown field: {fieldName}");

                // Special validation for TestID
                if (fieldName == "TestID")
                {
                    if (!double.TryParse(newValue, out double newTestId))
                        return EditResult.Failed("TestID must be a valid number");

                    // Check if ID already exists (and it's not the current test)
                    if (await _repository.TestIdExistsAsync((int)newTestId))
                    {
                        if (test.TestID.HasValue && (int)newTestId != (int)test.TestID.Value)
                        {
                            return EditResult.Failed($"TestID {(int)newTestId} already exists. Please choose a different ID.");
                        }
                    }

                    property.SetValue(test, newTestId);
                }
                else
                {
                    // Handle different property types
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(test, newValue);
                    }
                    else if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                    {
                        if (bool.TryParse(newValue, out bool boolValue))
                            property.SetValue(test, boolValue);
                        else
                            return EditResult.Failed($"{fieldName} must be true or false");
                    }
                    else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                    {
                        if (int.TryParse(newValue, out int intValue))
                            property.SetValue(test, intValue);
                        else
                            return EditResult.Failed($"{fieldName} must be a number");
                    }
                    else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                    {
                        if (double.TryParse(newValue, out double doubleValue))
                            property.SetValue(test, doubleValue);
                        else
                            return EditResult.Failed($"{fieldName} must be a number");
                    }
                    else
                    {
                        // Default: try to set as string
                        property.SetValue(test, newValue);
                    }
                }

                // Show confirmation dialog
                var changes = new List<FieldChange>
                {
                    new FieldChange(fieldName, oldValue, newValue)
                };

                bool confirmed = EditConfirmationDialog.ShowConfirmation(changes, owner);
                if (!confirmed)
                {
                    // User cancelled - revert changes
                    property.SetValue(test, ConvertValue(oldValue, property.PropertyType));
                    return EditResult.Cancelled();
                }

                // Save to database
                await _repository.UpdateTestAsync(test);

                System.Diagnostics.Debug.WriteLine($"✓ Test {test.TestID} updated: {fieldName} = '{newValue}'");

                return EditResult.Success($"{fieldName} updated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error editing test field: {ex.Message}");
                return EditResult.Failed($"Failed to update {fieldName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert string value to the target type
        /// </summary>
        private object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;
            else if (targetType == typeof(bool) || targetType == typeof(bool?))
                return bool.TryParse(value, out bool b) ? b : (object)null;
            else if (targetType == typeof(int) || targetType == typeof(int?))
                return int.TryParse(value, out int i) ? i : (object)null;
            else if (targetType == typeof(double) || targetType == typeof(double?))
                return double.TryParse(value, out double d) ? d : (object)null;
            else
                return value;
        }

        // ================================================
        // PROCESS EDITING
        // ================================================

        /// <summary>
        /// Validate and edit a process field
        /// </summary>
        public async Task<EditResult> EditProcessFieldAsync(Process process, string fieldName, string oldValue, string newValue, Window owner = null)
        {
            try
            {
                if (process == null)
                    return EditResult.Failed("Process is null");

                // Use reflection to set any property
                var property = typeof(Process).GetProperty(fieldName);
                if (property == null)
                    return EditResult.Failed($"Unknown field: {fieldName}");

                // Don't allow editing primary keys (except ProcessID on placeholder rows for new process creation)
                if (fieldName == "Index")
                    return EditResult.Failed($"Cannot edit primary key field: {fieldName}");

                // Block all edits on placeholder rows except ProcessID
                if (process.IsPlaceholder && fieldName != "ProcessID")
                    return EditResult.Failed($"To create a new process, please click on the ProcessID column and enter a ProcessID number.");

                // Special handling for ProcessID on placeholder rows - creates new process
                if (fieldName == "ProcessID" && process.IsPlaceholder)
                {
                    // Parse and validate ProcessID
                    if (!double.TryParse(newValue, out double processIdValue))
                        return EditResult.Failed("ProcessID must be a valid number");

                    // Set the value (this will trigger PlaceholderProcess_PropertyChanged which handles the rest)
                    property.SetValue(process, processIdValue);

                    System.Diagnostics.Debug.WriteLine($"✓ ProcessID {processIdValue} set on placeholder - PropertyChanged handler will create process");

                    // Return success without saving (the PropertyChanged handler will handle creation)
                    return EditResult.Success($"Processing new ProcessID {processIdValue}...");
                }

                // Special handling for ProcessID on existing processes - reloads template data
                if (fieldName == "ProcessID" && !process.IsPlaceholder)
                {
                    // Parse and validate ProcessID
                    if (!double.TryParse(newValue, out double processIdValue))
                        return EditResult.Failed("ProcessID must be a valid number");

                    // Set the value (this will trigger Process_PropertyChanged which handles the rest)
                    property.SetValue(process, processIdValue);

                    System.Diagnostics.Debug.WriteLine($"✓ ProcessID changed to {processIdValue} on existing process - PropertyChanged handler will reload template");

                    // Return success without saving (the PropertyChanged handler will handle reload and save)
                    return EditResult.Success($"Reloading data for ProcessID {processIdValue}...");
                }

                // Handle different property types for normal fields
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(process, newValue);
                }
                else if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                {
                    if (bool.TryParse(newValue, out bool boolValue))
                        property.SetValue(process, boolValue);
                    else
                        return EditResult.Failed($"{fieldName} must be true or false");
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    if (int.TryParse(newValue, out int intValue))
                        property.SetValue(process, intValue);
                    else
                        return EditResult.Failed($"{fieldName} must be a number");
                }
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                {
                    if (double.TryParse(newValue, out double doubleValue))
                        property.SetValue(process, doubleValue);
                    else
                        return EditResult.Failed($"{fieldName} must be a number");
                }
                else
                {
                    // Default: try to set as string
                    property.SetValue(process, newValue);
                }

                // Show confirmation dialog
                var changes = new List<FieldChange>
                {
                    new FieldChange(fieldName, oldValue, newValue)
                };

                bool confirmed = EditConfirmationDialog.ShowConfirmation(changes, owner);
                if (!confirmed)
                {
                    // Revert changes
                    property.SetValue(process, ConvertValue(oldValue, property.PropertyType));
                    return EditResult.Cancelled();
                }

                // Save to database
                await _processRepository.UpdateProcessAsync(process);

                System.Diagnostics.Debug.WriteLine($"✓ Process {process.ProcessID} updated: {fieldName} = '{newValue}'");

                return EditResult.Success($"{fieldName} updated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error editing process field: {ex.Message}");
                return EditResult.Failed($"Failed to update {fieldName}: {ex.Message}");
            }
        }

        // ================================================
        // FUNCTION EDITING
        // ================================================

        /// <summary>
        /// Validate and edit a function field
        /// </summary>
        public async Task<EditResult> EditFunctionFieldAsync(Function function, string fieldName, string oldValue, string newValue, Window owner = null)
        {
            try
            {
                if (function == null)
                    return EditResult.Failed("Function is null");

                // Use reflection to set any property
                var property = typeof(Function).GetProperty(fieldName);
                if (property == null)
                    return EditResult.Failed($"Unknown field: {fieldName}");

                // Don't allow editing primary keys
                if (fieldName == "Index" || fieldName == "ProcessID")
                    return EditResult.Failed($"Cannot edit primary key field: {fieldName}");

                // ⭐ PLACEHOLDER ROW HANDLING
                // Block editing all fields except FunctionName on placeholder rows
                if (function.IsPlaceholder && fieldName != "FunctionName")
                {
                    return EditResult.Failed("To create a new function, click FunctionName and enter a name");
                }

                // Special handling for FunctionName on placeholder rows - creates new function
                if (fieldName == "FunctionName" && function.IsPlaceholder)
                {
                    // Validate that FunctionName is not empty
                    if (string.IsNullOrWhiteSpace(newValue))
                        return EditResult.Failed("FunctionName cannot be empty");

                    // Set the value (this will trigger PlaceholderFunction_PropertyChanged which handles the rest)
                    property.SetValue(function, newValue);

                    System.Diagnostics.Debug.WriteLine($"✓ FunctionName '{newValue}' set on placeholder - PropertyChanged handler will create function");

                    // Return success without saving (the PropertyChanged handler will handle creation)
                    return EditResult.Success($"Creating new function '{newValue}'...");
                }

                // Handle different property types
                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(function, newValue);
                }
                else if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
                {
                    if (bool.TryParse(newValue, out bool boolValue))
                        property.SetValue(function, boolValue);
                    else
                        return EditResult.Failed($"{fieldName} must be true or false");
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    if (int.TryParse(newValue, out int intValue))
                        property.SetValue(function, intValue);
                    else
                        return EditResult.Failed($"{fieldName} must be a number");
                }
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                {
                    if (double.TryParse(newValue, out double doubleValue))
                        property.SetValue(function, doubleValue);
                    else
                        return EditResult.Failed($"{fieldName} must be a number");
                }
                else
                {
                    // Default: try to set as string
                    property.SetValue(function, newValue);
                }

                // Show confirmation dialog
                var changes = new List<FieldChange>
                {
                    new FieldChange(fieldName, oldValue, newValue)
                };

                bool confirmed = EditConfirmationDialog.ShowConfirmation(changes, owner);
                if (!confirmed)
                {
                    // Revert changes
                    property.SetValue(function, ConvertValue(oldValue, property.PropertyType));
                    return EditResult.Cancelled();
                }

                // Save to database
                await _processRepository.UpdateFunctionAsync(function);

                System.Diagnostics.Debug.WriteLine($"✓ Function {function.Index} updated: {fieldName} = '{newValue}'");

                return EditResult.Success($"{fieldName} updated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error editing function field: {ex.Message}");
                return EditResult.Failed($"Failed to update {fieldName}: {ex.Message}");
            }
        }

        // ================================================
        // VALIDATION HELPERS
        // ================================================

        /// <summary>
        /// Validate TestID (check uniqueness)
        /// </summary>
        public async Task<bool> ValidateTestIdAsync(int testId, int currentTestId)
        {
            if (testId == currentTestId)
                return true; // Same ID, no change

            return !await _repository.TestIdExistsAsync(testId);
        }

        /// <summary>
        /// Get next available TestID
        /// </summary>
        public async Task<int?> GetNextAvailableTestIdAsync()
        {
            return await _repository.GetNextAvailableTestIdAsync();
        }
    }

    // ================================================
    // EDIT RESULT
    // ================================================

    /// <summary>
    /// Result of an edit operation
    /// </summary>
    public class EditResult
    {
        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public string Message { get; set; }

        public static EditResult Success(string message = null)
        {
            return new EditResult
            {
                IsSuccess = true,
                IsCancelled = false,
                Message = message ?? "Edit successful"
            };
        }

        public static EditResult Failed(string message)
        {
            return new EditResult
            {
                IsSuccess = false,
                IsCancelled = false,
                Message = message
            };
        }

        public static EditResult Cancelled()
        {
            return new EditResult
            {
                IsSuccess = false,
                IsCancelled = true,
                Message = "Edit cancelled by user"
            };
        }
    }
}
