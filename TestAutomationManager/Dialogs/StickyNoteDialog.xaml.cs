using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;

namespace TestAutomationManager.Dialogs
{
    /// <summary>
    /// Dialog for quickly creating sticky notes
    /// Simple and fast interface for quick note capture
    /// </summary>
    public partial class StickyNoteDialog : Window
    {
        private readonly StickyNote? _stickyNote;
        private readonly TodoRepository _repository;
        private string _selectedColor = "#FFE57F"; // Default yellow

        public StickyNoteDialog(StickyNote? stickyNote, TodoRepository repository)
        {
            InitializeComponent();

            _stickyNote = stickyNote;
            _repository = repository;

            if (_stickyNote != null)
            {
                Title = "Edit Sticky Note";
                ContentTextBox.Text = _stickyNote.Content ?? "";
                _selectedColor = _stickyNote.Color ?? "#FFE57F";
            }
            else
            {
                Title = "Create Quick Note";
            }

            HighlightSelectedColor();

            // Focus on content
            Loaded += (s, e) => ContentTextBox.Focus();
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

            // Highlight selected
            var highlightBrush = new SolidColorBrush(Colors.White);
            if (_selectedColor == "#FFE57F") ColorButton1.BorderBrush = highlightBrush;
            else if (_selectedColor == "#A7FFEB") ColorButton2.BorderBrush = highlightBrush;
            else if (_selectedColor == "#B3E5FC") ColorButton3.BorderBrush = highlightBrush;
            else if (_selectedColor == "#F8BBD0") ColorButton4.BorderBrush = highlightBrush;
            else if (_selectedColor == "#E1BEE7") ColorButton5.BorderBrush = highlightBrush;
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_stickyNote != null)
                {
                    // Update existing note
                    _stickyNote.Content = ContentTextBox.Text.Trim();
                    _stickyNote.Color = _selectedColor;
                    _stickyNote.UpdatedAt = DateTime.Now;

                    await _repository.UpdateStickyNoteAsync(_stickyNote);
                }
                else
                {
                    // Create new sticky note
                    var random = new Random();

                    var newNote = new StickyNote
                    {
                        Content = ContentTextBox.Text.Trim(),
                        Color = _selectedColor,
                        PositionX = 100 + random.Next(0, 300),  // Random starting position
                        PositionY = 100 + random.Next(0, 300),
                        Width = 200,
                        Height = 200,
                        ZIndex = 1,
                        IsPinned = false,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = Environment.UserName
                    };

                    await _repository.InsertStickyNoteAsync(newNote);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving sticky note: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
