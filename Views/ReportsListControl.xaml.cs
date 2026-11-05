using System.Diagnostics;
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
        private const string DateFormat = "MMM dd, yyyy";

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
                var reportDates = await SalesDataServices.GetReportsFromLastTwoYearsAsync();
                Reports = reportDates.Select(r => new ReportDateDisplay { DisplayDate = r.ToString(DateFormat), ReportDate = r }).ToList();
                lstReports.ItemsSource = Reports;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReportsListControl] LoadReportsAsync: {ex}");
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
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".xlsx",
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm"
            };

            if (dlg.ShowDialog() != true) return;

            var dialog = new FileNameWindow(DateTime.Now.ToString(DateFormat), DateTime.Now);
            if (dialog.ShowDialog() != true) return;

            DateTime reportDate = dialog.ReportDate;
            string reportName = reportDate.ToString(DateFormat);

            try
            {
                bool replaceExisting = false;
                if (await SalesDataServices.ReportExistsAsync(reportDate))
                {
                    var choice = MessageBox.Show(
                        $"Sales data already exists for {reportName}.\n\n" +
                        "Yes: replace it with this file.\n" +
                        "No: add this file to it (only if this is a different report).\n" +
                        "Cancel: stop without importing.",
                        "Report Already Imported",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (choice == MessageBoxResult.Cancel || choice == MessageBoxResult.None) return;
                    replaceExisting = choice == MessageBoxResult.Yes;
                }

                var result = await ImportServices.ImportSalesReportWithMappingAsync(dlg.FileName, reportDate, replaceExisting);
                await LoadReportsAsync();

                string message = $"Imported {result.RowsImported:N0} rows for {reportName}.";
                if (result.RowsSkippedInvalid > 0)
                    message += $"\n\nSkipped {result.RowsSkippedInvalid:N0} rows with a missing item, price, or quantity.";
                if (result.RowsSkippedUnknownCode > 0)
                    message += $"\n\nSkipped {result.RowsSkippedUnknownCode:N0} rows with a transaction code the app doesn't count.";
                if (result.ItemsNotInProductList > 0)
                    message += $"\n\n{result.ItemsNotInProductList:N0} items aren't in your product list, so their sales won't appear on the sales floor.";

                MessageBox.Show(message, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The report was not imported.\n\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void DeleteSalesReport_Click(object sender, RoutedEventArgs e)
        {
            if (lstReports.SelectedItem is not ReportDateDisplay selectedReportDate)
            {
                MessageBox.Show("Please select a report date to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete all reports for {selectedReportDate.DisplayDate}?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            bool deleted = await SalesDataServices.DeleteReportAsync(selectedReportDate.ReportDate);
            await LoadReportsAsync();

            if (deleted)
                MessageBox.Show($"Successfully deleted reports for {selectedReportDate.DisplayDate}.",
                    "Deletion Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"Could not delete reports for {selectedReportDate.DisplayDate}.",
                    "Deletion Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }



        public class ReportDateDisplay
        {
            public string DisplayDate { get; set; }
            public DateTime ReportDate { get; set; }

        }
    }
}
