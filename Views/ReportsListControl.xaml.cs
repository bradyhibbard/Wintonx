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
    /// Interaction logic for ReportsListControl.xaml
    /// </summary>
    public partial class ReportsListControl : UserControl
    {
        public List<ReportDateDisplay> Reports { get; set; }
        public ReportsListControl()
        {
            InitializeComponent();
            LoadReports();
        }

        private void LoadReports()
        {
            // Get reports from the last 2 years
            var reportDates = DatabaseHelper.GetReportsFromLastTwoYears();

            // Format dates and bind to the list
            Reports = reportDates.Select(r => new ReportDateDisplay { DisplayDate = r.ToString("MMM dd, yyyy"), ReportDate = r }).ToList();
            lstReports.ItemsSource = Reports;
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
        private void AddSalesReport_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".xlsx";
            dlg.Filter = "Excel Files|*.xls;*.xlsx;*.xlsm|CSV Files|*.csv";

            if (dlg.ShowDialog() == true)
            {
                string filename = dlg.FileName;

                // Open the dialog to get the report date
                var dialog = new FileNameWindow(DateTime.Now.ToString("MMM dd, yyyy"), DateTime.Now);
                if (dialog.ShowDialog() == true)
                {
                    DateTime reportDate = dialog.ReportDate;

                    // Automatically update the filename to the selected date
                    string newFileName = reportDate.ToString("MMM dd, yyyy");

                    // Now use the updated filename in the database helper function
                    DatabaseHelper.ImportSalesReport(filename, reportDate);

                    MessageBox.Show($"Successfully imported Report!");
                }
            }
        }

        private void DeleteSalesReport_Click(object sender, RoutedEventArgs e)
        {
            var selectedReportDate = lstReports.SelectedItem as ReportDateDisplay;

            if (selectedReportDate != null)
            {
                // Retrieve the actual reports for the selected date from the database
                var reportsToDelete = DatabaseHelper.GetReportDetailsByDate(selectedReportDate.ReportDate);

                if (reportsToDelete.Any())
                {
                    var result = MessageBox.Show($"Are you sure you want to delete all reports for {selectedReportDate.DisplayDate}?",
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Delete each report that matches the selected date
                        foreach (var report in reportsToDelete)
                        {
                            DatabaseHelper.DeleteSalesReport(report);
                        }

                        LoadReports(); // Refresh the list after deletion
                    }
                }
                else
                {
                    MessageBox.Show("No reports found for the selected date.", "No Reports Found", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a report date to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        public class ReportDateDisplay
        {
            public string DisplayDate { get; set; }
            public DateTime ReportDate { get; set; }

        }
    }
}
