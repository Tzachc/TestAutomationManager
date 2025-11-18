using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TestAutomationManager.Models;
using TestAutomationManager.Repositories;
using TestAutomationManager.Dialogs;

namespace TestAutomationManager.Views
{
    /// <summary>
    /// Amazing Todo Manager with Kanban Board and Drag-Drop Sticky Notes
    /// Features: Task management, priority tracking, time tracking, categories, sticky notes
    /// </summary>
    public partial class TodoView : UserControl
    {
        // ================================================
        // FIELDS
        // ================================================

        private readonly TodoRepository _repository;
        private List<TodoItem> _allTodos;
        private List<StickyNote> _allStickyNotes;

        // Observable collections for Kanban board columns
        private ObservableCollection<TodoItem> _todoItems;
        private ObservableCollection<TodoItem> _inProgressItems;
        private ObservableCollection<TodoItem> _doneItems;

        // Drag and drop state
        private TodoItem? _draggedTodo;
        private Point _dragStartPoint;
        private bool _isDragging;

        private StickyNote? _draggedStickyNote;
        private Point _stickyNoteDragStart;
        private UIElement? _draggedStickyNoteElement;

        // Event for parent window
        public event EventHandler? DataLoaded;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public TodoView()
        {
            InitializeComponent();

            _repository = new TodoRepository();
            _allTodos = new List<TodoItem>();
            _allStickyNotes = new List<StickyNote>();

            _todoItems = new ObservableCollection<TodoItem>();
            _inProgressItems = new ObservableCollection<TodoItem>();
            _doneItems = new ObservableCollection<TodoItem>();

            TodoListBox.ItemsSource = _todoItems;
            InProgressListBox.ItemsSource = _inProgressItems;
            DoneListBox.ItemsSource = _doneItems;

            Loaded += OnLoaded;
        }

        // ================================================
        // INITIALIZATION
        // ================================================

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;

                // Load todos
                _allTodos = await _repository.GetAllTodosAsync();
                ApplyFilter("All Tasks");

                // Load sticky notes
                _allStickyNotes = await _repository.GetAllStickyNotesAsync();
                LoadStickyNotes();

                LoadingPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // KANBAN BOARD - FILTERING AND ORGANIZATION
        // ================================================

        private void ApplyFilter(string filter)
        {
            IEnumerable<TodoItem> filteredTodos = filter switch
            {
                "Active" => _allTodos.Where(t => !t.IsCompleted),
                "Completed" => _allTodos.Where(t => t.IsCompleted),
                "High Priority" => _allTodos.Where(t => t.Priority == "High" && !t.IsCompleted),
                "Overdue" => _allTodos.Where(t => t.IsOverdue),
                _ => _allTodos
            };

            // Apply search filter
            var searchText = SearchTextBox?.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredTodos = filteredTodos.Where(t =>
                    (t.Title?.ToLower().Contains(searchText) ?? false) ||
                    (t.Description?.ToLower().Contains(searchText) ?? false) ||
                    (t.Category?.ToLower().Contains(searchText) ?? false));
            }

            OrganizeTodosIntoColumns(filteredTodos.ToList());
        }

        private void OrganizeTodosIntoColumns(List<TodoItem> todos)
        {
            _todoItems.Clear();
            _inProgressItems.Clear();
            _doneItems.Clear();

            foreach (var todo in todos)
            {
                switch (todo.Status)
                {
                    case "Todo":
                        _todoItems.Add(todo);
                        break;
                    case "InProgress":
                        _inProgressItems.Add(todo);
                        break;
                    case "Done":
                        _doneItems.Add(todo);
                        break;
                }
            }

            // Update count badges
            TodoCountText.Text = _todoItems.Count.ToString();
            InProgressCountText.Text = _inProgressItems.Count.ToString();
            DoneCountText.Text = _doneItems.Count.ToString();
        }

        // ================================================
        // EVENT HANDLERS - TOOLBAR
        // ================================================

        private void NewTask_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TodoEditDialog(null, _repository);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void NewStickyNote_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StickyNoteDialog(null, _repository);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FilterComboBox?.SelectedItem is ComboBoxItem item)
            {
                ApplyFilter(item.Content?.ToString() ?? "All Tasks");
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FilterComboBox?.SelectedItem is ComboBoxItem item)
            {
                ApplyFilter(item.Content?.ToString() ?? "All Tasks");
            }
        }

        private void ViewToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (sender == KanbanViewToggle && KanbanViewToggle.IsChecked == true)
            {
                StickyNotesViewToggle.IsChecked = false;
                KanbanScrollViewer.Visibility = Visibility.Visible;
                StickyNotesScrollViewer.Visibility = Visibility.Collapsed;
            }
            else if (sender == StickyNotesViewToggle && StickyNotesViewToggle.IsChecked == true)
            {
                KanbanViewToggle.IsChecked = false;
                KanbanScrollViewer.Visibility = Visibility.Collapsed;
                StickyNotesScrollViewer.Visibility = Visibility.Visible;
            }
        }

        // ================================================
        // DRAG AND DROP - TODO CARDS (KANBAN BOARD)
        // ================================================

        private void TodoCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is TodoItem todo)
            {
                _draggedTodo = todo;
                _dragStartPoint = e.GetPosition(null);
                _isDragging = false;
            }
        }

        private void TodoCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedTodo != null && !_isDragging)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var dragData = new DataObject("TodoItem", _draggedTodo);
                    DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                }
            }
        }

        private void TodoCard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && sender is Border border && border.Tag is TodoItem todo)
            {
                // Double click to edit
                if (e.ClickCount == 2)
                {
                    var dialog = new TodoEditDialog(todo, _repository);
                    if (dialog.ShowDialog() == true)
                    {
                        _ = LoadDataAsync();
                    }
                }
            }

            _draggedTodo = null;
            _isDragging = false;
        }

        // ================================================
        // DRAG AND DROP - COLUMN EVENTS
        // ================================================

        private async void TodoColumn_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoItem"))
            {
                var todo = e.Data.GetData("TodoItem") as TodoItem;
                if (todo != null && todo.Status != "Todo")
                {
                    await UpdateTodoStatus(todo, "Todo");
                }
            }

            ResetColumnHighlight(sender);
        }

        private async void InProgressColumn_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoItem"))
            {
                var todo = e.Data.GetData("TodoItem") as TodoItem;
                if (todo != null && todo.Status != "InProgress")
                {
                    await UpdateTodoStatus(todo, "InProgress");
                }
            }

            ResetColumnHighlight(sender);
        }

        private async void DoneColumn_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoItem"))
            {
                var todo = e.Data.GetData("TodoItem") as TodoItem;
                if (todo != null && todo.Status != "Done")
                {
                    await UpdateTodoStatus(todo, "Done");
                }
            }

            ResetColumnHighlight(sender);
        }

        private void Column_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TodoItem") && sender is ListBox listBox)
            {
                // Visual feedback for drag over
                listBox.Opacity = 0.8;
            }
        }

        private void Column_DragLeave(object sender, DragEventArgs e)
        {
            ResetColumnHighlight(sender);
        }

        private void ResetColumnHighlight(object sender)
        {
            if (sender is ListBox listBox)
            {
                listBox.Opacity = 1.0;
            }
        }

        // ================================================
        // DATABASE OPERATIONS - TODO ITEMS
        // ================================================

        private async System.Threading.Tasks.Task UpdateTodoStatus(TodoItem todo, string newStatus)
        {
            try
            {
                // Calculate new sort order (add to end of column)
                var itemsInColumn = newStatus switch
                {
                    "Todo" => _todoItems.Count,
                    "InProgress" => _inProgressItems.Count,
                    "Done" => _doneItems.Count,
                    _ => 0
                };

                await _repository.UpdateTodoStatusAsync(todo.Id, newStatus, itemsInColumn);

                // Reload data to reflect changes
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating todo status: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================================================
        // STICKY NOTES - CANVAS RENDERING
        // ================================================

        private void LoadStickyNotes()
        {
            StickyNotesCanvas.Children.Clear();

            foreach (var note in _allStickyNotes)
            {
                AddStickyNoteToCanvas(note);
            }
        }

        private void AddStickyNoteToCanvas(StickyNote note)
        {
            // Create sticky note visual
            var border = new Border
            {
                Width = note.Width,
                Height = note.Height,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color ?? "#FFE57F")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Cursor = Cursors.SizeAll,
                Tag = note,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.15,
                    BlurRadius = 15,
                    ShadowDepth = 3
                }
            };

            var textBox = new TextBox
            {
                Text = note.Content ?? "",
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brushes.Black,
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsReadOnly = false,
                Tag = note
            };

            textBox.LostFocus += async (s, e) =>
            {
                if (s is TextBox tb && tb.Tag is StickyNote sn)
                {
                    sn.Content = tb.Text;
                    await _repository.UpdateStickyNoteAsync(sn);
                }
            };

            border.Child = textBox;

            // Set position
            Canvas.SetLeft(border, note.PositionX);
            Canvas.SetTop(border, note.PositionY);
            Canvas.SetZIndex(border, note.ZIndex);

            // Add drag-drop handlers
            border.MouseLeftButtonDown += StickyNote_MouseDown;
            border.MouseMove += StickyNote_MouseMove;
            border.MouseLeftButtonUp += StickyNote_MouseUp;

            // Add context menu
            var contextMenu = new ContextMenu();

            var deleteMenuItem = new MenuItem { Header = "Delete" };
            deleteMenuItem.Click += async (s, e) => await DeleteStickyNote(note);

            var colorMenuItem = new MenuItem { Header = "Change Color" };
            var yellowItem = new MenuItem { Header = "Yellow" };
            yellowItem.Click += async (s, e) => await ChangeStickyNoteColor(note, border, "#FFE57F");
            var greenItem = new MenuItem { Header = "Green" };
            greenItem.Click += async (s, e) => await ChangeStickyNoteColor(note, border, "#A7FFEB");
            var blueItem = new MenuItem { Header = "Blue" };
            blueItem.Click += async (s, e) => await ChangeStickyNoteColor(note, border, "#B3E5FC");
            var pinkItem = new MenuItem { Header = "Pink" };
            pinkItem.Click += async (s, e) => await ChangeStickyNoteColor(note, border, "#F8BBD0");
            var purpleItem = new MenuItem { Header = "Purple" };
            purpleItem.Click += async (s, e) => await ChangeStickyNoteColor(note, border, "#E1BEE7");

            colorMenuItem.Items.Add(yellowItem);
            colorMenuItem.Items.Add(greenItem);
            colorMenuItem.Items.Add(blueItem);
            colorMenuItem.Items.Add(pinkItem);
            colorMenuItem.Items.Add(purpleItem);

            var bringToFrontItem = new MenuItem { Header = "Bring to Front" };
            bringToFrontItem.Click += async (s, e) => await BringStickyNoteToFront(note, border);

            contextMenu.Items.Add(colorMenuItem);
            contextMenu.Items.Add(bringToFrontItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(deleteMenuItem);

            border.ContextMenu = contextMenu;

            StickyNotesCanvas.Children.Add(border);
        }

        // ================================================
        // STICKY NOTES - DRAG AND DROP
        // ================================================

        private void StickyNote_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is StickyNote note)
            {
                _draggedStickyNote = note;
                _draggedStickyNoteElement = border;
                _stickyNoteDragStart = e.GetPosition(StickyNotesCanvas);

                // Bring to front visually
                Canvas.SetZIndex(border, 9999);

                border.CaptureMouse();
            }
        }

        private void StickyNote_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                _draggedStickyNote != null &&
                _draggedStickyNoteElement != null &&
                _draggedStickyNoteElement == sender)
            {
                Point currentPosition = e.GetPosition(StickyNotesCanvas);

                double newLeft = currentPosition.X - (_draggedStickyNoteElement.RenderSize.Width / 2);
                double newTop = currentPosition.Y - 20; // Offset for better UX

                // Constrain to canvas bounds
                newLeft = Math.Max(0, Math.Min(newLeft, StickyNotesCanvas.Width - _draggedStickyNoteElement.RenderSize.Width));
                newTop = Math.Max(0, Math.Min(newTop, StickyNotesCanvas.Height - _draggedStickyNoteElement.RenderSize.Height));

                Canvas.SetLeft(_draggedStickyNoteElement, newLeft);
                Canvas.SetTop(_draggedStickyNoteElement, newTop);
            }
        }

        private async void StickyNote_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedStickyNote != null && _draggedStickyNoteElement != null)
            {
                var border = _draggedStickyNoteElement as Border;
                border?.ReleaseMouseCapture();

                // Save new position
                _draggedStickyNote.PositionX = Canvas.GetLeft(_draggedStickyNoteElement);
                _draggedStickyNote.PositionY = Canvas.GetTop(_draggedStickyNoteElement);

                await _repository.UpdateStickyNotePositionAsync(
                    _draggedStickyNote.Id,
                    _draggedStickyNote.PositionX,
                    _draggedStickyNote.PositionY);

                // Reset z-index
                await BringStickyNoteToFront(_draggedStickyNote, _draggedStickyNoteElement);
            }

            _draggedStickyNote = null;
            _draggedStickyNoteElement = null;
        }

        // ================================================
        // STICKY NOTES - OPERATIONS
        // ================================================

        private async System.Threading.Tasks.Task DeleteStickyNote(StickyNote note)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this sticky note?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _repository.DeleteStickyNoteAsync(note.Id);
                await LoadDataAsync();
            }
        }

        private async System.Threading.Tasks.Task ChangeStickyNoteColor(StickyNote note, Border border, string color)
        {
            note.Color = color;
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            await _repository.UpdateStickyNoteAsync(note);
        }

        private async System.Threading.Tasks.Task BringStickyNoteToFront(StickyNote note, UIElement element)
        {
            await _repository.BringStickyNoteToFrontAsync(note.Id);

            // Reload to update all z-indexes properly
            await LoadDataAsync();
        }
    }
}
