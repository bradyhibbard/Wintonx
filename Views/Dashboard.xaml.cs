using System;
using System.Windows;
using System.Windows.Controls;
using Winton.ViewModels;

namespace Winton.Views
{
    public partial class Dashboard : UserControl
    {
        public event Action<string> NavigationRequested;

        public Dashboard()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }

        private void OpenDetailedChart_Click(object sender, RoutedEventArgs e)
        {
            var vm = (DashboardViewModel)DataContext;
            var window = new DetailedChartWindow(
                vm.CurrentYearRevenue,
                vm.PreviousYearRevenue,
                vm.Months,
                vm.Formatter);
            window.Show();
        }

        private void UploadReport_Click(object sender, RoutedEventArgs e)
            => NavigationRequested?.Invoke("Reports");

        private void OpenSalesFloor_Click(object sender, RoutedEventArgs e)
            => NavigationRequested?.Invoke("SalesFloor");
    }
}
