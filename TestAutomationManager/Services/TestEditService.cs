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

                if (string.IsNullOrWhiteSpace(newValue))
                    return EditResult.Failed($"{fieldName} cannot be empty");

                // Special validation for TestID
                if (fieldName == "TestID")
                {
                    if (!int.TryParse(newValue, out int newTestId))
                        return EditResult.Failed("TestID must be a valid number");

                    // Check if ID already exists (and it's not the current test)
                    if (await _repository.TestIdExistsAsync(newTestId))
                    {
                        if (newTestId != test.TestID)
                        {
                            return EditResult.Failed($"TestID {newTestId} already exists. Please choose a different ID.");
                        }
                    }

                    test.TestID = newTestId;
                }
                else if (fieldName == "TestName")
                {
                    test.TestName = newValue;
                }
                else if (fieldName == "Bugs")
                {
                    test.Bugs = newValue;
                }
                else if (fieldName == "Recipients")
                {
                    test.Recipients = newValue;
                }
                else if (fieldName == "ExceptionMessage")
                {
                    test.ExceptionMessage = newValue;
                }
                else if (fieldName == "LastRunning")
                {
                    test.LastRunning = newValue;
                }
                else if (fieldName == "LastTimePass")
                {
                    test.LastTimePass = newValue;
                }
                else if (fieldName == "SendEmailOnFail")
                {
                    if (bool.TryParse(newValue, out bool value))
                        test.SendEmailOnFail = value;
                    else
                        return EditResult.Failed("SendEmailOnFail must be true or false");
                }
                else if (fieldName == "SendEmailOnPass")
                {
                    if (bool.TryParse(newValue, out bool value))
                        test.SendEmailOnPass = value;
                    else
                        return EditResult.Failed("SendEmailOnPass must be true or false");
                }
                else if (fieldName == "SendAlwaysEmail")
                {
                    if (bool.TryParse(newValue, out bool value))
                        test.SendAlwaysEmail = value;
                    else
                        return EditResult.Failed("SendAlwaysEmail must be true or false");
                }
                else
                {
                    return EditResult.Failed($"Unknown field: {fieldName}");
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
                    RevertTestField(test, fieldName, oldValue);
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
        /// Revert a test field to its original value
        /// </summary>
        private void RevertTestField(Test test, string fieldName, string oldValue)
        {
            if (fieldName == "TestID" && int.TryParse(oldValue, out int testId))
                test.TestID = testId;
            else if (fieldName == "TestName")
                test.TestName = oldValue;
            else if (fieldName == "Bugs")
                test.Bugs = oldValue;
            else if (fieldName == "Recipients")
                test.Recipients = oldValue;
            else if (fieldName == "ExceptionMessage")
                test.ExceptionMessage = oldValue;
            else if (fieldName == "LastRunning")
                test.LastRunning = oldValue;
            else if (fieldName == "LastTimePass")
                test.LastTimePass = oldValue;
            else if (fieldName == "SendEmailOnFail" && bool.TryParse(oldValue, out bool fail))
                test.SendEmailOnFail = fail;
            else if (fieldName == "SendEmailOnPass" && bool.TryParse(oldValue, out bool pass))
                test.SendEmailOnPass = pass;
            else if (fieldName == "SendAlwaysEmail" && bool.TryParse(oldValue, out bool always))
                test.SendAlwaysEmail = always;
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

                // Handle both regular fields and parameters
                if (fieldName.StartsWith("Param"))
                {
                    // Parameter field (Param1-Param46)
                    var property = typeof(Process).GetProperty(fieldName);
                    if (property != null)
                    {
                        property.SetValue(process, newValue);
                    }
                    else
                    {
                        return EditResult.Failed($"Unknown parameter: {fieldName}");
                    }
                }
                else if (fieldName == "ProcessName")
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                        return EditResult.Failed("ProcessName cannot be empty");
                    process.ProcessName = newValue;
                }
                else if (fieldName == "State")
                {
                    process.State = newValue;
                }
                else
                {
                    return EditResult.Failed($"Unknown field: {fieldName}");
                }

                // Show confirmation dialog
                var changes = new List<FieldChange>
                {
                    new FieldChange(fieldName, oldValue, newValue)
                };

                bool confirmed = EditConfirmationDialog.ShowConfirmation(changes, owner);
                if (!confirmed)
                {
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

                // Handle both regular fields and parameters
                if (fieldName.StartsWith("Param"))
                {
                    // Parameter field (Param1-Param30)
                    var property = typeof(Function).GetProperty(fieldName);
                    if (property != null)
                    {
                        property.SetValue(function, newValue);
                    }
                    else
                    {
                        return EditResult.Failed($"Unknown parameter: {fieldName}");
                    }
                }
                else if (fieldName == "FunctionName")
                {
                    if (string.IsNullOrWhiteSpace(newValue))
                        return EditResult.Failed("FunctionName cannot be empty");
                    function.FunctionName = newValue;
                }
                else
                {
                    return EditResult.Failed($"Unknown field: {fieldName}");
                }

                // Show confirmation dialog
                var changes = new List<FieldChange>
                {
                    new FieldChange(fieldName, oldValue, newValue)
                };

                bool confirmed = EditConfirmationDialog.ShowConfirmation(changes, owner);
                if (!confirmed)
                {
                    return EditResult.Cancelled();
                }

                // Save to database
                await _processRepository.UpdateFunctionAsync(function);

                System.Diagnostics.Debug.WriteLine($"✓ Function {function.FunctionID} updated: {fieldName} = '{newValue}'");

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
        public async Task<int> GetNextAvailableTestIdAsync()
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
