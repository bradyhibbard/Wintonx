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
using Winton.Data;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for ReportDetailsControl.xaml
    /// </summary>
    public partial class ReportDetailsControl : UserControl
    {
        private DateTime _reportDate;

        public ReportDetailsControl(DateTime reportDate)
        {
            InitializeComponent();
            _reportDate = reportDate;
            LoadReportDetails();
        }

        private void LoadReportDetails()
        {
            // Assuming DatabaseHelper has a method to get report details by SaleDate
            var reportDetails = DatabaseHelper.GetReportDetailsByDate(_reportDate);
            lstReportDetails.ItemsSource = reportDetails;
        }

        private async void DeleteReport_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete this report?", "Confirm Delete", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                bool isDeleted = await DatabaseHelper.DeleteReportAsync(_reportDate);
                if (isDeleted)
                {
                    MessageBox.Show("Report deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Navigate back to the report list
                    var mainWindow = (MainWindow)Application.Current.MainWindow;
                    mainWindow.SetMainContent(new ReportsListControl());
                }
                else
                {
                    MessageBox.Show("Failed to delete the report.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
