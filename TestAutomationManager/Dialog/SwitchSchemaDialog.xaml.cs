using System.Windows;

namespace TestAutomationManager.Dialogs
{
    public enum SchemaSwitchAction
    {
        Cancel,
        Reload,
        OpenNew
    }

    public partial class SwitchSchemaDialog : Window
    {
        public SchemaSwitchAction Result { get; private set; } = SchemaSwitchAction.Cancel;

        public SwitchSchemaDialog(string currentSchema, string newSchema)
        {
            InitializeComponent();
            CurrentSchemaText.Text = currentSchema;
            NewSchemaText.Text = newSchema;
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            Result = SchemaSwitchAction.Reload;
            DialogResult = true;
            Close();
        }

        private void NewInstanceButton_Click(object sender, RoutedEventArgs e)
        {
            Result = SchemaSwitchAction.OpenNew;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = SchemaSwitchAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}