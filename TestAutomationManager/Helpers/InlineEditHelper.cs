using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TestAutomationManager.Controls;

namespace TestAutomationManager.Helpers
{
    /// <summary>
    /// Helper class to make TextBlocks editable inline without modifying XAML
    /// Attaches double-click handlers and creates edit overlays dynamically
    /// </summary>
    public static class InlineEditHelper
    {
        // ================================================
        // ATTACHED PROPERTIES
        // ================================================

        /// <summary>
        /// Attached property to enable inline editing on TextBlocks
        /// </summary>
        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.RegisterAttached(
                "IsEditable",
                typeof(bool),
                typeof(InlineEditHelper),
                new PropertyMetadata(false, OnIsEditableChanged));

        /// <summary>
        /// Field name for validation and change tracking
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.RegisterAttached(
                "FieldName",
                typeof(string),
                typeof(InlineEditHelper),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Event handler for when edit is confirmed
        /// </summary>
        public static readonly DependencyProperty EditConfirmedHandlerProperty =
            DependencyProperty.RegisterAttached(
                "EditConfirmedHandler",
                typeof(EventHandler<EditConfirmedEventArgs>),
                typeof(InlineEditHelper),
                new PropertyMetadata(null));

        // ================================================
        // PROPERTY GETTERS/SETTERS
        // ================================================

        public static bool GetIsEditable(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEditableProperty);
        }

        public static void SetIsEditable(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEditableProperty, value);
        }

        public static string GetFieldName(DependencyObject obj)
        {
            return (string)obj.GetValue(FieldNameProperty);
        }

        public static void SetFieldName(DependencyObject obj, string value)
        {
            obj.SetValue(FieldNameProperty, value);
        }

        public static EventHandler<EditConfirmedEventArgs> GetEditConfirmedHandler(DependencyObject obj)
        {
            return (EventHandler<EditConfirmedEventArgs>)obj.GetValue(EditConfirmedHandlerProperty);
        }

        public static void SetEditConfirmedHandler(DependencyObject obj, EventHandler<EditConfirmedEventArgs> value)
        {
            obj.SetValue(EditConfirmedHandlerProperty, value);
        }

        // ================================================
        // IMPLEMENTATION
        // ================================================

        private static void OnIsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock && (bool)e.NewValue)
            {
                // Make the TextBlock editable
                textBlock.Cursor = Cursors.Hand;
                textBlock.MouseLeftButtonDown += TextBlock_MouseLeftButtonDown;
                textBlock.MouseEnter += TextBlock_MouseEnter;
                textBlock.MouseLeave += TextBlock_MouseLeave;
            }
        }

        private static void TextBlock_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                // Add subtle hover effect
                textBlock.TextDecorations = TextDecorations.Underline;
                textBlock.Opacity = 0.8;
            }
        }

        private static void TextBlock_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.TextDecorations = null;
                textBlock.Opacity = 1.0;
            }
        }

        private static void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock textBlock)
            {
                // Double-click detected - create inline editor
                CreateInlineEditor(textBlock);
                e.Handled = true;
            }
        }

        private static void CreateInlineEditor(TextBlock textBlock)
        {
            var parent = VisualTreeHelper.GetParent(textBlock);
            if (parent is not Panel panel)
                return;

            string originalText = textBlock.Text;
            string fieldName = GetFieldName(textBlock);

            // Hide the TextBlock
            textBlock.Visibility = Visibility.Collapsed;

            // Create TextBox for editing
            var editBox = new TextBox
            {
                Text = originalText,
                FontSize = textBlock.FontSize,
                FontWeight = textBlock.FontWeight,
                Foreground = textBlock.Foreground,
                Background = (Brush)Application.Current.Resources["PrimaryBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["PrimaryBlueBrush"],
                BorderThickness = new Thickness(2),
                Padding = new Thickness(4, 2),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
                VerticalAlignment = textBlock.VerticalAlignment,
                HorizontalAlignment = textBlock.HorizontalAlignment,
                TextAlignment = textBlock.TextAlignment
            };

            // Position editBox at the same Grid location as textBlock
            if (textBlock.Parent is Grid)
            {
                var column = Grid.GetColumn(textBlock);
                var row = Grid.GetRow(textBlock);
                Grid.SetColumn(editBox, column);
                Grid.SetRow(editBox, row);
            }

            // Add to panel
            panel.Children.Add(editBox);

            // Focus and select all
            editBox.Focus();
            editBox.SelectAll();
            editBox.CaretIndex = editBox.Text.Length;

            // Handle Enter key
            editBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ConfirmEdit(textBlock, editBox, fieldName, originalText);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    CancelEdit(textBlock, editBox);
                    e.Handled = true;
                }
            };

            // Handle lost focus
            editBox.LostFocus += (s, e) =>
            {
                ConfirmEdit(textBlock, editBox, fieldName, originalText);
            };

            System.Diagnostics.Debug.WriteLine($"✏️ Started inline edit for '{fieldName}': '{originalText}'");
        }

        private static void ConfirmEdit(TextBlock textBlock, TextBox editBox, string fieldName, string originalText)
        {
            string newText = editBox.Text?.Trim() ?? string.Empty;

            // Remove editBox
            if (VisualTreeHelper.GetParent(editBox) is Panel panel)
            {
                panel.Children.Remove(editBox);
            }

            // Show textBlock
            textBlock.Visibility = Visibility.Visible;

            // Check if changed
            if (newText != originalText)
            {
                // Fire edit confirmed event
                var handler = GetEditConfirmedHandler(textBlock);
                if (handler != null)
                {
                    var args = new EditConfirmedEventArgs(
                        textBlock.DataContext,
                        fieldName,
                        originalText,
                        newText);

                    handler(textBlock, args);

                    if (!args.Cancel)
                    {
                        // Update text
                        textBlock.Text = newText;
                        System.Diagnostics.Debug.WriteLine($"✓ Inline edit confirmed: '{originalText}' → '{newText}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Inline edit rejected: {args.CancelReason}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ℹ️ No changes made to '{fieldName}'");
            }
        }

        private static void CancelEdit(TextBlock textBlock, TextBox editBox)
        {
            // Remove editBox
            if (VisualTreeHelper.GetParent(editBox) is Panel panel)
            {
                panel.Children.Remove(editBox);
            }

            // Show textBlock
            textBlock.Visibility = Visibility.Visible;

            System.Diagnostics.Debug.WriteLine($"✗ Inline edit cancelled");
        }
    }

    /// <summary>
    /// Event args for edit confirmed
    /// </summary>
    public class EditConfirmedEventArgs : EventArgs
    {
        public object DataContext { get; }
        public string FieldName { get; }
        public string OldValue { get; }
        public string NewValue { get; }
        public bool Cancel { get; set; }
        public string CancelReason { get; set; }

        public EditConfirmedEventArgs(object dataContext, string fieldName, string oldValue, string newValue)
        {
            DataContext = dataContext;
            FieldName = fieldName;
            OldValue = oldValue;
            NewValue = newValue;
            Cancel = false;
            CancelReason = null;
        }
    }
}
