using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestAutomationManager.Dialogs
{
    public partial class EditColumnLengthDialog : Window
    {
        public string ColumnName { get; set; }
        public int CurrentLength { get; set; }
        public int NewLength { get; private set; }

        public EditColumnLengthDialog(string columnName, int currentLength)
        {
            InitializeComponent();

            ColumnName = columnName;
            CurrentLength = currentLength;

            ColumnNameText.Text = columnName;
            CurrentLengthText.Text = $"{currentLength} characters";
            NewLengthTextBox.Text = currentLength.ToString();

            // Focus textbox when dialog loads
            Loaded += (s, e) =>
            {
                NewLengthTextBox.Focus();
                NewLengthTextBox.SelectAll();
            };
        }

        private void NewLengthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateInput();
        }

        private void ValidateInput()
        {
            string input = NewLengthTextBox.Text?.Trim() ?? "";

            // Check for "MAX" keyword
            if (input.Equals("MAX", StringComparison.OrdinalIgnoreCase))
            {
                ValidationMessage.Visibility = Visibility.Collapsed;
                SaveButton.IsEnabled = true;
                return;
            }

            // Check if numeric
            if (!int.TryParse(input, out int newLength))
            {
                ValidationMessage.Text = "Please enter a valid number or 'MAX'";
                ValidationMessage.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = false;
                return;
            }

            // Check if less than current
            if (newLength <= CurrentLength)
            {
                ValidationMessage.Text = $"New length must be greater than current length ({CurrentLength})";
                ValidationMessage.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = false;
                return;
            }

            // Check reasonable maximum
            if (newLength > 8000)
            {
                ValidationMessage.Text = "For lengths over 8000, please use 'MAX'";
                ValidationMessage.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = false;
                return;
            }

            // Valid input
            ValidationMessage.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = true;
        }

        private void NewLengthTextBox_KeyDown(object sender, KeyEventArgs e)
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

        private void Suggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                string value = button.Tag.ToString();
                if (value == "-1")
                {
                    NewLengthTextBox.Text = "MAX";
                }
                else
                {
                    NewLengthTextBox.Text = value;
                }
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
            string input = NewLengthTextBox.Text?.Trim() ?? "";

            if (input.Equals("MAX", StringComparison.OrdinalIgnoreCase))
            {
                NewLength = -1; // -1 indicates MAX
            }
            else if (int.TryParse(input, out int length))
            {
                NewLength = length;
            }
            else
            {
                return; // Invalid input
            }

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