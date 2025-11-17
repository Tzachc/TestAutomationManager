using System.Windows.Controls;
using TestAutomationManager.ViewModels;

namespace TestAutomationManager.Views
{
    public partial class BackupsView : UserControl
    {
        public BackupsView()
        {
            InitializeComponent();
            Loaded += BackupsView_Loaded;
        }

        private async void BackupsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is BackupsViewModel viewModel)
            {
                await viewModel.LoadBackupsAsync();
            }
        }
    }
}
