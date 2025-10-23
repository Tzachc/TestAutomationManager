using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestAutomationManager.Dialogs
{
    public partial class SearchOverlay : Window
    {
        public string SearchQuery => SearchTextBox.Text.Trim();
        public bool ExactMatch => ExactMatchCheckBox.IsChecked == true;

        public SearchOverlay(Window owner)
        {
            InitializeComponent();
            Owner = owner;

            Loaded += (s, e) =>
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            };
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void SetStatus(string message)
        {
            StatusText.Text = message;
        }

        /// <summary>
        /// Enable dragging the window by clicking anywhere on the border
        /// </summary>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only allow dragging if not clicking on an interactive element
            if (e.OriginalSource is TextBox ||
                e.OriginalSource is Button ||
                e.OriginalSource is CheckBox)
            {
                return;
            }

            try
            {
                this.DragMove();
            }
            catch
            {
                // Ignore any drag exceptions
            }
        }
    }
}