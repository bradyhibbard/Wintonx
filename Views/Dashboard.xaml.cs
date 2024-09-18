using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
