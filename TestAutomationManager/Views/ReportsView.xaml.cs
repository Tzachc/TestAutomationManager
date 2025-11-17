using System.Windows.Controls;
using TestAutomationManager.ViewModels;

namespace TestAutomationManager.Views
{
    /// <summary>
    /// Interaction logic for ReportsView.xaml
    /// </summary>
    public partial class ReportsView : UserControl
    {
        public ReportsView()
        {
            InitializeComponent();
            Loaded += ReportsView_Loaded;
        }

        private async void ReportsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ReportsViewModel viewModel)
            {
                await viewModel.LoadReportsAsync();
            }
        }
    }
}
