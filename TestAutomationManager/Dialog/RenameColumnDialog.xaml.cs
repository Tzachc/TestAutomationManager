using System.Windows;
using System.Windows.Input;

namespace TestAutomationManager.Dialogs
{
    public partial class RenameColumnDialog : Window
    {
        public string CurrentColumnName { get; set; }
        public string NewColumnName { get; private set; }

        public RenameColumnDialog(string currentColumnName)
        {
            InitializeComponent();

            CurrentColumnName = currentColumnName;
            CurrentNameText.Text = currentColumnName;
            NewNameTextBox.Text = currentColumnName;

            // Focus and select text when dialog loads
            Loaded += (s, e) =>
            {
                NewNameTextBox.Focus();
                NewNameTextBox.SelectAll();
            };
        }

        private void NewNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Enable/disable save button based on input
            string newName = NewNameTextBox.Text?.Trim() ?? "";

            bool isValid = !string.IsNullOrWhiteSpace(newName) &&
                          newName != CurrentColumnName;

            SaveButton.IsEnabled = isValid;
        }

        private void NewNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SaveButton.IsEnabled)
            {
                SaveChanges();
            }
            else if (e.Key == Key.Escape)
            {
                CancelChanges();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveChanges();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelChanges();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelChanges();
        }

        private void SaveChanges()
        {
            NewColumnName = NewNameTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelChanges()
        {
            DialogResult = false;
            Close();
        }
    }
}