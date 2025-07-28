using System.Windows;
using System.Windows.Controls;
using Winton.Services;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for DatabaseControl.xaml
    /// </summary>
    public partial class DBControl : UserControl
    {
        public DBControl()
        {
            InitializeComponent();
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string result = await DatabaseService.TestDatabaseConnectionAsync();
            MessageBox.Show(result, "Database Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BackupDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Call the BackupDatabase function asynchronously
            string result = await DatabaseService.BackupDatabaseAsync();

            // Display the result (success or error message) in a MessageBox
            MessageBox.Show(result, "Database Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RestoreDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Call the RestoreDatabase function asynchronously
            string result = await DatabaseService.RestoreDatabaseAsync();

            // Display the result (success or error message) in a MessageBox
            MessageBox.Show(result, "Database Restore", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // Updated function to handle Check for Updates using the UpdateChecker class
        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var updateChecker = new UpdateChecker();
                string result = await updateChecker.UpdateApplication();  // Call the update logic from the UpdateChecker class
                MessageBox.Show(result, "Update", MessageBoxButton.OK, MessageBoxImage.Information);  // Display the result of the update check
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);  // Show error if any exception occurs
            }
        }
    }
}
