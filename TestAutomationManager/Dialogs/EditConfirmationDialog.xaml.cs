using System;
using System.Collections.Generic;
using System.Windows;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog to confirm field changes before saving to database
    /// Shows old vs new values for user review
    /// </summary>
    public partial class EditConfirmationDialog : Window
    {
        public bool Confirmed { get; private set; }

        public EditConfirmationDialog(List<FieldChange> changes)
        {
            InitializeComponent();

            if (changes == null || changes.Count == 0)
            {
                throw new ArgumentException("Changes list cannot be null or empty");
            }

            ChangesItemsControl.ItemsSource = changes;

            // Focus on Save button by default
            Loaded += (s, e) => SaveButton.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Show confirmation dialog for changes
        /// </summary>
        public static bool ShowConfirmation(List<FieldChange> changes, Window owner = null)
        {
            var dialog = new EditConfirmationDialog(changes)
            {
                Owner = owner
            };

            return dialog.ShowDialog() == true;
        }
    }

    /// <summary>
    /// Represents a single field change for display
    /// </summary>
    public class FieldChange
    {
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public FieldChange(string fieldName, string oldValue, string newValue)
        {
            FieldName = fieldName;
            OldValue = oldValue ?? "(empty)";
            NewValue = newValue ?? "(empty)";
        }

        public override string ToString()
        {
            return $"{FieldName}: '{OldValue}' → '{NewValue}'";
        }
    }
}
