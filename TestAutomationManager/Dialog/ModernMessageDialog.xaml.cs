using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TestAutomationManager.Dialogs
{
    public enum MessageType
    {
        Success,
        Error,
        Warning,
        Info,
        Question
    }

    public enum MessageButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public partial class ModernMessageDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;
        public Brush BorderColor { get; set; }
        public Brush IconBackground { get; set; }

        public ModernMessageDialog(string title, string message, MessageType type = MessageType.Info, MessageButtons buttons = MessageButtons.OK)
        {
            InitializeComponent();

            TitleText.Text = title;
            MessageText.Text = message;

            ConfigureAppearance(type);
            ConfigureButtons(buttons);

            // Set data context for bindings
            DataContext = this;
        }

        private void ConfigureAppearance(MessageType type)
        {
            switch (type)
            {
                case MessageType.Success:
                    IconText.Text = "✓";
                    IconBackground = (Brush)Application.Current.Resources["SuccessBrush"];
                    BorderColor = (Brush)Application.Current.Resources["SuccessBrush"];
                    break;

                case MessageType.Error:
                    IconText.Text = "✕";
                    IconBackground = (Brush)Application.Current.Resources["ErrorBrush"];
                    BorderColor = (Brush)Application.Current.Resources["ErrorBrush"];
                    break;

                case MessageType.Warning:
                    IconText.Text = "⚠";
                    IconBackground = (Brush)Application.Current.Resources["WarningBrush"];
                    BorderColor = (Brush)Application.Current.Resources["WarningBrush"];
                    break;

                case MessageType.Question:
                    IconText.Text = "?";
                    IconBackground = (Brush)Application.Current.Resources["PrimaryBlueBrush"];
                    BorderColor = (Brush)Application.Current.Resources["PrimaryBlueBrush"];
                    break;

                case MessageType.Info:
                default:
                    IconText.Text = "ℹ";
                    IconBackground = (Brush)Application.Current.Resources["PrimaryBlueBrush"];
                    BorderColor = (Brush)Application.Current.Resources["PrimaryBlueBrush"];
                    break;
            }
        }

        private void ConfigureButtons(MessageButtons buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageButtons.OK:
                    AddButton("OK", MessageBoxResult.OK, true);
                    break;

                case MessageButtons.OKCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel, false);
                    AddButton("OK", MessageBoxResult.OK, true);
                    break;

                case MessageButtons.YesNo:
                    AddButton("No", MessageBoxResult.No, false);
                    AddButton("Yes", MessageBoxResult.Yes, true);
                    break;

                case MessageButtons.YesNoCancel:
                    AddButton("Cancel", MessageBoxResult.Cancel, false);
                    AddButton("No", MessageBoxResult.No, false);
                    AddButton("Yes", MessageBoxResult.Yes, true);
                    break;
            }
        }

        private void AddButton(string content, MessageBoxResult result, bool isPrimary)
        {
            var button = new Button
            {
                Content = content,
                Height = 36,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 12, 0)
            };

            if (isPrimary)
            {
                button.Background = (Brush)Application.Current.Resources["PrimaryBlueBrush"];
                button.Foreground = Brushes.White;
                button.BorderThickness = new Thickness(0);
            }
            else
            {
                button.Background = Brushes.Transparent;
                button.Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"];
                button.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
                button.BorderThickness = new Thickness(1);
            }

            // Apply style
            button.Style = (Style)this.Resources["ModernButton"];

            // Handle click
            button.Click += (s, e) =>
            {
                Result = result;
                DialogResult = (result == MessageBoxResult.OK || result == MessageBoxResult.Yes);
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        // ================================================
        // STATIC HELPER METHODS FOR EASY USAGE
        // ================================================

        public static void ShowSuccess(string message, string title = "Success", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Success, MessageButtons.OK);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }

        public static void ShowError(string message, string title = "Error", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Error, MessageButtons.OK);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }

        public static void ShowWarning(string message, string title = "Warning", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Warning, MessageButtons.OK);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }

        public static void ShowInfo(string message, string title = "Information", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Info, MessageButtons.OK);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
        }

        public static MessageBoxResult ShowQuestion(string message, string title = "Confirm", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Question, MessageButtons.YesNo);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.Result;
        }

        public static MessageBoxResult ShowConfirmation(string message, string title = "Confirm", Window owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageType.Warning, MessageButtons.YesNo);
            if (owner != null) dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}