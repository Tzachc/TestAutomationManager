using System.ComponentModel;
using System.Windows;

namespace TestAutomationManager.Dialogs
{
    public partial class SimpleInputDialog : Window, INotifyPropertyChanged
    {
        private string _title;
        private string _prompt;
        private string _inputValue;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public string Prompt
        {
            get => _prompt;
            set
            {
                _prompt = value;
                OnPropertyChanged(nameof(Prompt));
            }
        }

        public string InputValue
        {
            get => _inputValue;
            set
            {
                _inputValue = value;
                OnPropertyChanged(nameof(InputValue));
            }
        }

        public SimpleInputDialog(string title, string prompt, string defaultValue = "", Window owner = null)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            Prompt = prompt;
            InputValue = defaultValue;

            if (owner != null)
                Owner = owner;

            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
