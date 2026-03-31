using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Winton.Services;

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
            Loaded += async (s, e) => await LoadReportDetailsAsync();
        }

        private async Task LoadReportDetailsAsync()
        {
            try
            {
                // Fetch the report details asynchronously
                var reportDetails = await SalesDataServices.GetReportDetailsByDateAsync(_reportDate);

                // Bind results to the UI list control
                lstReportDetails.ItemsSource = reportDetails;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReportDetailsControl] LoadReportDetailsAsync: {ex}");
            }
        }


        private async void DeleteReport_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete this report?", "Confirm Delete", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                bool isDeleted = await SalesDataServices.DeleteReportAsync(_reportDate);
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
