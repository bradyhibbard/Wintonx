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

        private void UseOlderDatabaseWithHistory()
        {
            Execute("DROP TABLE ProductPlacements");
            Execute("DROP TABLE Archive");
            Execute(@"CREATE TABLE ProductPlacements (
                PlacementID INTEGER PRIMARY KEY AUTOINCREMENT, ProductID INTEGER, SectionID TEXT, ItemNumber TEXT,
                QuantitySold INTEGER, Revenue DECIMAL(10,2), DatePlaced DATETIME, DateRemoved DATETIME, Cat TEXT, Grp TEXT,
                FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                FOREIGN KEY (SectionID) REFERENCES Sections(SectionID))");
            Execute(@"CREATE TABLE Archive (
                PlacementID INTEGER PRIMARY KEY, ProductID INTEGER, SectionID TEXT, ItemNumber TEXT,
                QuantitySold INTEGER, Revenue DECIMAL(10,2), DatePlaced DATETIME, DateRemoved DATETIME, RemovalNotes TEXT,
                FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                FOREIGN KEY (SectionID) REFERENCES Sections(SectionID))");

            AddSection("S1");
            AddProduct("A");
            int productId = Scalar<int>("SELECT ProductID FROM Products");
            AddPlacement("A", "S1", Jan(1), productId: productId);
            Execute("INSERT INTO Archive (PlacementID, ProductID, SectionID, ItemNumber, RemovalNotes) VALUES (7, @Id, 'S1', 'A', 'Old note')", ("@Id", productId));
        }

        [Fact]
        public async Task Upgrade_keeps_history_saves_a_backup_and_unblocks_deletes()
        {
            UseOlderDatabaseWithHistory();

            await DatabaseService.InitializeDatabaseAsync();

            Assert.Equal(1, Scalar<int>("SELECT COUNT(*) FROM ProductPlacements"));
            Assert.Equal("Old note", Scalar<string>("SELECT RemovalNotes FROM Archive WHERE PlacementID = 7"));
            Assert.Single(BackupFiles("BeforeUpgrade"));

            Assert.True(await ImportServices.DeleteAllProductsAsync());
            await ProductPlacementServices.ArchiveAndDeleteSectionAsync("S1");
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Sections"));
            Assert.Equal(2, Scalar<int>("SELECT COUNT(*) FROM Archive"));
        }

        [Fact]
        public async Task Upgrade_runs_only_once()
        {
            UseOlderDatabaseWithHistory();

            await DatabaseService.InitializeDatabaseAsync();
            await DatabaseService.InitializeDatabaseAsync();

            Assert.Single(BackupFiles("BeforeUpgrade"));
        }

        [Fact]
        public async Task Foreign_keys_stay_enforced_after_an_upgrade()
        {
            UseOlderDatabaseWithHistory();

            await DatabaseService.InitializeDatabaseAsync();

            Assert.Equal(1, Scalar<int>("PRAGMA foreign_keys"));
        }

        [Fact]
        public async Task Restoring_an_older_backup_upgrades_it()
        {
            UseOlderDatabaseWithHistory();
            await DatabaseService.BackupDatabaseAsync();
            string backup = Assert.Single(BackupFiles("DatabaseBackup"));

            string result = await DatabaseService.RestoreDatabaseAsync(backup);

            Assert.Contains("restored successfully", result);
            await ProductPlacementServices.ArchiveAndDeleteSectionAsync("S1");
            Assert.Equal(0, Scalar<int>("SELECT COUNT(*) FROM Sections"));
        }

        [Fact]
        public async Task Connection_test_reports_success()
        {
            Assert.Equal("Connection successful.", await DatabaseService.TestDatabaseConnectionAsync());
        }
    }
}
