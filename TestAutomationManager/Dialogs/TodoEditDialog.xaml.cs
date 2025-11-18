using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog for creating and editing todo items
    /// Provides comprehensive form with all todo fields
    /// </summary>
    public partial class TodoEditDialog : Window
    {
        private readonly TodoItem? _todoItem;
        private readonly TodoRepository _repository;
        private readonly bool _isEditMode;
        private string? _selectedColor;

        public TodoEditDialog(TodoItem? todoItem, TodoRepository repository)
        {
            InitializeComponent();

            _todoItem = todoItem;
            _repository = repository;
            _isEditMode = todoItem != null;

            if (_isEditMode)
            {
                DialogTitle.Text = "Edit Task";
                DeleteButton.Visibility = Visibility.Visible;
                LoadTodoData();
            }
            else
            {
                DialogTitle.Text = "Create New Task";
                DeleteButton.Visibility = Visibility.Collapsed;
            }

            // Focus on title
            Loaded += (s, e) => TitleTextBox.Focus();
        }

        private void LoadTodoData()
        {
            if (_todoItem == null) return;

            TitleTextBox.Text = _todoItem.Title ?? "";
            DescriptionTextBox.Text = _todoItem.Description ?? "";

            if (_todoItem.DueDate.HasValue)
                DueDatePicker.SelectedDate = _todoItem.DueDate;

            // Set priority
            switch (_todoItem.Priority)
            {
                case "High":
                    PriorityComboBox.SelectedIndex = 0;
                    break;
                case "Medium":
                    PriorityComboBox.SelectedIndex = 1;
                    break;
                case "Low":
                    PriorityComboBox.SelectedIndex = 2;
                    break;
            }

            // Set status
            switch (_todoItem.Status)
            {
                case "Todo":
                    StatusComboBox.SelectedIndex = 0;
                    break;
                case "InProgress":
                    StatusComboBox.SelectedIndex = 1;
                    break;
                case "Done":
                    StatusComboBox.SelectedIndex = 2;
                    break;
            }

            CategoryComboBox.Text = _todoItem.Category ?? "";
            TagsTextBox.Text = _todoItem.Tags ?? "";

            if (_todoItem.EstimatedMinutes.HasValue)
                EstimatedMinutesTextBox.Text = _todoItem.EstimatedMinutes.Value.ToString();

            if (_todoItem.ActualMinutes.HasValue)
                ActualMinutesTextBox.Text = _todoItem.ActualMinutes.Value.ToString();

            AttachmentUrlTextBox.Text = _todoItem.AttachmentUrl ?? "";

            IsRecurringCheckBox.IsChecked = _todoItem.IsRecurring;

            if (_todoItem.IsRecurring && !string.IsNullOrEmpty(_todoItem.RecurrencePattern))
            {
                switch (_todoItem.RecurrencePattern)
                {
                    case "Daily":
                        RecurrenceComboBox.SelectedIndex = 0;
                        break;
                    case "Weekly":
                        RecurrenceComboBox.SelectedIndex = 1;
                        break;
                    case "Monthly":
                        RecurrenceComboBox.SelectedIndex = 2;
                        break;
                    case "Yearly":
                        RecurrenceComboBox.SelectedIndex = 3;
                        break;
                }
            }

            // Set color selection
            _selectedColor = _todoItem.Color;
            HighlightSelectedColor();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string color)
            {
                _selectedColor = color;
                HighlightSelectedColor();
            }
        }

        private void HighlightSelectedColor()
        {
            // Reset all borders
            ColorButton1.BorderBrush = Brushes.Transparent;
            ColorButton2.BorderBrush = Brushes.Transparent;
            ColorButton3.BorderBrush = Brushes.Transparent;
            ColorButton4.BorderBrush = Brushes.Transparent;
            ColorButton5.BorderBrush = Brushes.Transparent;
            ColorButton6.BorderBrush = Brushes.Transparent;

            // Highlight selected
            if (!string.IsNullOrEmpty(_selectedColor))
            {
                if (_selectedColor == "#539BF5") ColorButton1.BorderBrush = Brushes.White;
                else if (_selectedColor == "#66BB6A") ColorButton2.BorderBrush = Brushes.White;
                else if (_selectedColor == "#FFA726") ColorButton3.BorderBrush = Brushes.White;
                else if (_selectedColor == "#EF5350") ColorButton4.BorderBrush = Brushes.White;
                else if (_selectedColor == "#AB47BC") ColorButton5.BorderBrush = Brushes.White;
                else if (_selectedColor == "#9E9E9E") ColorButton6.BorderBrush = Brushes.White;
            }
        }

        private void IsRecurring_CheckedChanged(object sender, RoutedEventArgs e)
        {
            RecurrencePanel.Visibility = IsRecurringCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Please enter a task title.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }

            try
            {
                // Parse time values
                int? estimatedMinutes = null;
                if (!string.IsNullOrWhiteSpace(EstimatedMinutesTextBox.Text))
                {
                    if (int.TryParse(EstimatedMinutesTextBox.Text, out int estMins))
                        estimatedMinutes = estMins;
                }

                int? actualMinutes = null;
                if (!string.IsNullOrWhiteSpace(ActualMinutesTextBox.Text))
                {
                    if (int.TryParse(ActualMinutesTextBox.Text, out int actMins))
                        actualMinutes = actMins;
                }

                // Get selected values
                string priority = (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Medium";
                string status = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Todo";
                string? recurrencePattern = null;

                if (IsRecurringCheckBox.IsChecked == true)
                {
                    recurrencePattern = (RecurrenceComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                }

                if (_isEditMode && _todoItem != null)
                {
                    // Update existing todo
                    _todoItem.Title = TitleTextBox.Text.Trim();
                    _todoItem.Description = DescriptionTextBox.Text.Trim();
                    _todoItem.DueDate = DueDatePicker.SelectedDate;
                    _todoItem.Priority = priority;
                    _todoItem.Status = status;
                    _todoItem.Category = CategoryComboBox.Text.Trim();
                    _todoItem.Tags = TagsTextBox.Text.Trim();
                    _todoItem.Color = _selectedColor;
                    _todoItem.EstimatedMinutes = estimatedMinutes;
                    _todoItem.ActualMinutes = actualMinutes;
                    _todoItem.IsRecurring = IsRecurringCheckBox.IsChecked ?? false;
                    _todoItem.RecurrencePattern = recurrencePattern;
                    _todoItem.AttachmentUrl = AttachmentUrlTextBox.Text.Trim();
                    _todoItem.IsCompleted = status == "Done";

                    await _repository.UpdateTodoAsync(_todoItem);
                }
                else
                {
                    // Create new todo
                    var newTodo = new TodoItem
                    {
                        Title = TitleTextBox.Text.Trim(),
                        Description = DescriptionTextBox.Text.Trim(),
                        DueDate = DueDatePicker.SelectedDate,
                        Priority = priority,
                        Status = status,
                        Category = CategoryComboBox.Text.Trim(),
                        Tags = TagsTextBox.Text.Trim(),
                        Color = _selectedColor,
                        EstimatedMinutes = estimatedMinutes,
                        ActualMinutes = actualMinutes,
                        IsRecurring = IsRecurringCheckBox.IsChecked ?? false,
                        RecurrencePattern = recurrencePattern,
                        AttachmentUrl = AttachmentUrlTextBox.Text.Trim(),
                        IsCompleted = status == "Done",
                        SortOrder = 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = Environment.UserName
                    };

                    await _repository.InsertTodoAsync(newTodo);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving task: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_todoItem == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the task '{_todoItem.Title}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _repository.DeleteTodoAsync(_todoItem.Id);
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting task: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
