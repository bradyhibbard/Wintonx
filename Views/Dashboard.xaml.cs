using System.Windows;
using System.Windows.Controls;
using Winton.ViewModels;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for Dashboard.xaml
    /// </summary>
    public partial class Dashboard : UserControl
    {
        public Dashboard()
        {
            InitializeComponent();

            this.DataContext = new DashboardViewModel();
        }

        private void OpenDetailedChart_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (DashboardViewModel)DataContext;
            var detailedChartWindow = new DetailedChartWindow(viewModel.CurrentYearRevenue, viewModel.PreviousYearRevenue, viewModel.Months, viewModel.Formatter);
            detailedChartWindow.Show();
        }
    }
}
