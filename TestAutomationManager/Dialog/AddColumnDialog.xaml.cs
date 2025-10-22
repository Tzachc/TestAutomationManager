using System;
using System.Linq;
using System.Windows;

namespace TestAutomationManager.Dialogs
{
    public partial class AddColumnDialog : Window
    {
        public string NewColumnName { get; private set; }

        public AddColumnDialog()
        {
            InitializeComponent();

            // Focus on text box when dialog opens
            Loaded += (s, e) => ColumnNameTextBox.Focus();
        }

        private void ColumnNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateInput();
        }

        private void ValidateInput()
        {
            string columnName = ColumnNameTextBox.Text.Trim();

            // Reset validation
            ValidationMessage.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = false;

            // Check if empty
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return;
            }

            // Validate column name format
            if (!IsValidColumnName(columnName))
            {
                ValidationMessage.Text = "❌ Invalid column name. Must start with a letter or underscore and contain only letters, numbers, and underscores.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            // Check for reserved words
            string[] reservedWords = { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE", "ID" };
            if (reservedWords.Contains(columnName.ToUpperInvariant()))
            {
                ValidationMessage.Text = $"❌ '{columnName}' is a reserved word and cannot be used as a column name.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            // All validations passed
            SaveButton.IsEnabled = true;
        }

        private bool IsValidColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;

            // Must start with a letter or underscore
            if (!char.IsLetter(columnName[0]) && columnName[0] != '_')
                return false;

            // Only allow letters, numbers, and underscores
            foreach (char c in columnName)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            NewColumnName = ColumnNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(NewColumnName))
            {
                MessageBox.Show("Please enter a column name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}