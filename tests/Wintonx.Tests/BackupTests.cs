using Winton.Services;
using Xunit;

namespace Winton.Tests
{
    public class BackupTests : DatabaseTest
    {
        private static string[] BackupFiles(string prefix) =>
            Directory.GetFiles(DatabaseService.BackupDirectory, prefix + "*.db");

        [Fact]
        public async Task Restore_brings_back_the_backed_up_data_and_saves_a_safety_copy()
        {
            AddSale("A", Jan(5), 100m);
            string message = await DatabaseService.BackupDatabaseAsync();
            Assert.Contains("successful", message);
            string backup = Assert.Single(BackupFiles("DatabaseBackup"));

            AddSale("B", Jan(6), 50m);
            Assert.Equal(2, CountSales());

            string result = await DatabaseService.RestoreDatabaseAsync(backup);

            Assert.Contains("restored successfully", result);
            Assert.Equal(1, CountSales());
            Assert.Single(BackupFiles("BeforeRestore"));
        }

        [Fact]
        public async Task Failed_restore_reports_the_error_and_keeps_current_data()
        {
            AddSale("A", Jan(5), 100m);

            string result = await DatabaseService.RestoreDatabaseAsync(Path.Combine(TempFolder, "missing.db"));

            Assert.StartsWith("Error", result);
            Assert.Equal(1, CountSales());
        }

        [Fact]
        public async Task Connection_test_reports_success()
        {
            Assert.Equal("Connection successful.", await DatabaseService.TestDatabaseConnectionAsync());
        }
    }
}
