using System.Windows;
using System.Windows.Controls;
using Winton.Services;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for ReportsListControl.xaml
    /// </summary>
    public partial class ReportsListControl : UserControl
    {
        public List<ReportDateDisplay> Reports { get; set; }
        public ReportsListControl()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadReportsAsync();
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                // Get reports from the last 2 years asynchronously
                var reportDates = await SalesDataServices.GetReportsFromLastTwoYearsAsync();

                // Ensure data is not null before using it
                if (reportDates != null)
                {
                    // Format dates and bind to the list
                    Reports = reportDates.Select(r => new ReportDateDisplay { DisplayDate = r.ToString("MMM dd, yyyy"), ReportDate = r }).ToList();
                    lstReports.ItemsSource = Reports;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reports: {ex.Message}");
            }
        }


        private void LstReports_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstReports.SelectedItem != null)
            {
                var selectedReport = ((ReportDateDisplay)lstReports.SelectedItem).ReportDate;

                // Get reference to MainWindow
                var mainWindow = (MainWindow)Application.Current.MainWindow;

                // Display ReportDetailsControl in the MainContent area
                mainWindow.SetMainContent(new ReportDetailsControl(selectedReport));
            }
        }
        private async void AddSalesReport_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog to select a sales report file
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".xlsx",
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm|CSV Files|*.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                string filename = dlg.FileName;

                // Open a dialog to get the report date
                var dialog = new FileNameWindow(DateTime.Now.ToString("MMM dd, yyyy"), DateTime.Now);

                if (dialog.ShowDialog() == true)
                {
                    DateTime reportDate = dialog.ReportDate;

                    // Automatically update the filename to match the selected date
                    string newFilename = reportDate.ToString("MMM dd, yyyy");

                    try
                    {
                        // Await the async method call to ensure completion before proceeding
                        await ImportServices.ImportSalesReportWithMappingAsync(filename, reportDate);

                        MessageBox.Show($"Successfully imported {newFilename}!", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while importing: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        private async void DeleteSalesReport_Click(object sender, RoutedEventArgs e)
        {
            var selectedReportDate = lstReports.SelectedItem as ReportDateDisplay;

            if (selectedReportDate != null)
            {
                // Retrieve the actual reports for the selected date from the database
                var reportsToDelete = await SalesDataServices.GetReportDetailsByDateAsync(selectedReportDate.ReportDate);

                if (reportsToDelete.Any())
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete all reports for {selectedReportDate.DisplayDate}?",
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // Delete each report asynchronously
                            foreach (var report in reportsToDelete)
                            {
                                await ImportServices.DeleteSalesReportAsync(report);
                            }

                            // Refresh the report list after deletion
                            await LoadReportsAsync();

                            MessageBox.Show($"Successfully deleted reports for {selectedReportDate.DisplayDate}.",
                                "Deletion Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting reports: {ex.Message}", "Deletion Failed",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("No reports found for the selected date.", "No Reports Found",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a report date to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        public class ReportDateDisplay
        {
            public string DisplayDate { get; set; }
            public DateTime ReportDate { get; set; }

        }
    }
}
