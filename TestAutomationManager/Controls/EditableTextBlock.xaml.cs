using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TestAutomationManager.Controls
{
    /// <summary>
    /// EditableTextBlock - A control that displays as TextBlock and becomes editable on double-click
    /// Features:
    /// - Double-click to edit
    /// - Enter to save, Esc to cancel
    /// - Cursor positioned at end of text
    /// - Smooth hover effects
    /// - Event-driven for validation and confirmation
    /// </summary>
    public partial class EditableTextBlock : UserControl
    {
        // ================================================
        // DEPENDENCY PROPERTIES
        // ================================================

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(EditableTextBlock),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty TextColorProperty =
            DependencyProperty.Register(
                nameof(TextColor),
                typeof(Brush),
                typeof(EditableTextBlock),
                new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(
                nameof(FontSize),
                typeof(double),
                typeof(EditableTextBlock),
                new PropertyMetadata(13.0));

        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(
                nameof(FontWeight),
                typeof(FontWeight),
                typeof(EditableTextBlock),
                new PropertyMetadata(FontWeights.Normal));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(EditableTextBlock),
                new PropertyMetadata(false));

        // ================================================
        // PROPERTIES
        // ================================================

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush TextColor
        {
            get => (Brush)GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public new double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public new FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private string _originalText;
        private bool _isEditing;

        // ================================================
        // EVENTS
        // ================================================

        /// <summary>
        /// Fired when user confirms edit (Enter key)
        /// Handler can set e.Cancel = true to reject the change
        /// </summary>
        public event EventHandler<EditConfirmingEventArgs> EditConfirming;

        /// <summary>
        /// Fired after edit is confirmed and accepted
        /// </summary>
        public event EventHandler EditConfirmed;

        /// <summary>
        /// Fired when edit is cancelled (Esc key or lost focus without confirmation)
        /// </summary>
        public event EventHandler EditCancelled;

        // ================================================
        // CONSTRUCTOR
        // ================================================

        public EditableTextBlock()
        {
            InitializeComponent();

            // Wire up events
            DisplayBorder.MouseLeftButtonDown += DisplayBorder_MouseLeftButtonDown;
            DisplayBorder.MouseEnter += DisplayBorder_MouseEnter;
            DisplayBorder.MouseLeave += DisplayBorder_MouseLeave;

            EditTextBox.KeyDown += EditTextBox_KeyDown;
            EditTextBox.LostFocus += EditTextBox_LostFocus;
            EditTextBox.GotFocus += EditTextBox_GotFocus;
        }

        // ================================================
        // EVENT HANDLERS
        // ================================================

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Text updated externally - update display
            if (d is EditableTextBlock control && !control._isEditing)
            {
                control.DisplayTextBlock.Text = e.NewValue?.ToString() ?? string.Empty;
            }
        }

        private void DisplayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && !IsReadOnly)
            {
                StartEdit();
                e.Handled = true;
            }
        }

        private void DisplayBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!IsReadOnly && !_isEditing)
            {
                // Subtle hover effect
                var animation = new DoubleAnimation(0.3, TimeSpan.FromMilliseconds(150));
                HoverOverlay.BeginAnimation(OpacityProperty, animation);
            }
        }

        private void DisplayBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isEditing)
            {
                var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
                HoverOverlay.BeginAnimation(OpacityProperty, animation);
            }
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.Shift))
            {
                // Enter pressed - attempt to confirm edit
                ConfirmEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Escape pressed - cancel edit
                CancelEdit();
                e.Handled = true;
            }
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Lost focus without confirming - attempt to confirm
            if (_isEditing)
            {
                ConfirmEdit();
            }
        }

        private void EditTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Select all text when focused
            EditTextBox.SelectAll();
        }

        // ================================================
        // EDIT MODE MANAGEMENT
        // ================================================

        /// <summary>
        /// Enter edit mode
        /// </summary>
        private void StartEdit()
        {
            if (_isEditing || IsReadOnly)
                return;

            _isEditing = true;
            _originalText = Text;

            // Switch to edit mode
            DisplayBorder.Visibility = Visibility.Collapsed;
            EditBorder.Visibility = Visibility.Visible;
            HoverOverlay.Opacity = 0;

            // Focus and select all
            EditTextBox.Text = _originalText ?? string.Empty;
            EditTextBox.Focus();

            // Move cursor to end
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EditTextBox.CaretIndex = EditTextBox.Text.Length;
                EditTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);

            System.Diagnostics.Debug.WriteLine($"✏️ Started editing: '{_originalText}'");
        }

        /// <summary>
        /// Attempt to confirm edit with validation
        /// </summary>
        private void ConfirmEdit()
        {
            if (!_isEditing)
                return;

            string newText = EditTextBox.Text?.Trim() ?? string.Empty;
            string oldText = _originalText ?? string.Empty;

            // Check if text actually changed
            if (newText == oldText)
            {
                ExitEditMode();
                System.Diagnostics.Debug.WriteLine($"ℹ️ No changes made");
                return;
            }

            // Fire EditConfirming event - allows validation
            var args = new EditConfirmingEventArgs(oldText, newText);
            EditConfirming?.Invoke(this, args);

            if (args.Cancel)
            {
                // Validation failed - stay in edit mode
                System.Diagnostics.Debug.WriteLine($"⚠️ Edit rejected by handler: {args.CancelReason}");

                // Show error message if provided
                if (!string.IsNullOrEmpty(args.CancelReason))
                {
                    MessageBox.Show(
                        args.CancelReason,
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Keep in edit mode, refocus
                EditTextBox.Focus();
                EditTextBox.SelectAll();
                return;
            }

            // Validation passed - update value
            Text = newText;
            ExitEditMode();

            // Fire EditConfirmed event
            EditConfirmed?.Invoke(this, EventArgs.Empty);

            System.Diagnostics.Debug.WriteLine($"✓ Edit confirmed: '{oldText}' → '{newText}'");
        }

        /// <summary>
        /// Cancel edit and revert to original value
        /// </summary>
        private void CancelEdit()
        {
            if (!_isEditing)
                return;

            // Revert to original text
            EditTextBox.Text = _originalText;
            ExitEditMode();

            // Fire EditCancelled event
            EditCancelled?.Invoke(this, EventArgs.Empty);

            System.Diagnostics.Debug.WriteLine($"✗ Edit cancelled");
        }

        /// <summary>
        /// Exit edit mode and return to display mode
        /// </summary>
        private void ExitEditMode()
        {
            _isEditing = false;

            // Switch back to display mode
            EditBorder.Visibility = Visibility.Collapsed;
            DisplayBorder.Visibility = Visibility.Visible;
            HoverOverlay.Opacity = 0;
        }

        // ================================================
        // PUBLIC METHODS
        // ================================================

        /// <summary>
        /// Programmatically enter edit mode
        /// </summary>
        public void EnterEditMode()
        {
            StartEdit();
        }

        /// <summary>
        /// Check if currently in edit mode
        /// </summary>
        public bool IsEditing => _isEditing;
    }

    // ================================================
    // EVENT ARGS
    // ================================================

    public class EditConfirmingEventArgs : EventArgs
    {
        public string OldValue { get; }
        public string NewValue { get; }
        public bool Cancel { get; set; }
        public string CancelReason { get; set; }

        public EditConfirmingEventArgs(string oldValue, string newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Cancel = false;
            CancelReason = null;
        }
    }
}
