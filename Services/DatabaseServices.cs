using DocumentFormat.OpenXml.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;
using NPOI.SS.Formula.Functions;
using System.IO;
using static NPOI.HSSF.Util.HSSFColor;

namespace Winton.Services
{
    internal sealed class DatabaseService
    {
        private static readonly string dbPath = DatabaseConfig.DbPath;

        public static async Task InitializeDatabaseAsync()
        {
            try
            {
                // Ensure the LocalAppData folder exists
                Directory.CreateDirectory(Path.GetDirectoryName(DatabaseConfig.DbPath));

                // Detect legacy DB in the application folder (Program Files)
                string legacyDbPath = Path.Combine(AppContext.BaseDirectory, "WintonDatabase.db");

                // If new DB doesn't exist but the legacy one does, migrate it
                if (!File.Exists(DatabaseConfig.DbPath) && File.Exists(legacyDbPath))
                {
                    File.Copy(legacyDbPath, DatabaseConfig.DbPath);
                }

                bool dbJustCreated = !File.Exists(DatabaseConfig.DbPath);

                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    // Drop old unused tables
                    await DropTableIfExistsAsync(connection, "Furniture");
                    await DropTableIfExistsAsync(connection, "ProductPlacementHistory");

                    // Create tables
                    await CreateTableAsync(connection, "Sections", @"
                SectionID TEXT PRIMARY KEY,
                Name TEXT,
                XPosition REAL NOT NULL,
                YPosition REAL NOT NULL,
                Width REAL NOT NULL,
                Height REAL NOT NULL,
                Rotation REAL NOT NULL,
                ShapeType TEXT NOT NULL");

                    await CreateTableAsync(connection, "Perimeter", @"
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                XPosition REAL NOT NULL,
                YPosition REAL NOT NULL");

                    await CreateTableAsync(connection, "Partitions", @"
                PartitionID TEXT NOT NULL,
                PointOrder INTEGER NOT NULL,
                XPosition REAL NOT NULL,
                YPosition REAL NOT NULL,
                PRIMARY KEY (PartitionID, PointOrder)");

                    await CreateTableAsync(connection, "SalesData", @"
                SalesDataID INTEGER PRIMARY KEY AUTOINCREMENT,
                PlacementID INTEGER,
                ItemNumber TEXT,
                QuantitySold INTEGER,
                Revenue DECIMAL(10,2),
                SaleDate DATETIME,
                Grp TEXT,
                Cat TEXT,
                TransactionCode TEXT,
                Price DECIMAL(10,2),
                Description TEXT,
                VendorModel TEXT,
                FOREIGN KEY (PlacementID) REFERENCES ProductPlacements(PlacementID)");

                    await CreateTableAsync(connection, "Products", @"
                ProductID INTEGER PRIMARY KEY AUTOINCREMENT,
                ItemNumber TEXT NOT NULL UNIQUE,
                ItemName TEXT NOT NULL,
                Vendor TEXT,
                Category TEXT,
                Grp TEXT");

                    await CreateTableAsync(connection, "ProductPlacements", @"
                PlacementID INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductID INTEGER,
                SectionID TEXT,
                ItemNumber TEXT,
                QuantitySold INTEGER,
                Revenue DECIMAL(10,2),
                DatePlaced DATETIME,
                DateRemoved DATETIME,
                Cat TEXT,
                Grp TEXT,
                FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                FOREIGN KEY (SectionID) REFERENCES Sections(SectionID)");

                    await CreateTableAsync(connection, "Archive", @"
                PlacementID INTEGER PRIMARY PRIMARY KEY,
                ProductID INTEGER,
                SectionID TEXT,
                QuantitySold INTEGER,
                Revenue DECIMAL(10,2),
                DatePlaced DATETIME,
                DateRemoved DATETIME,
                RemovalNotes TEXT,
                FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                FOREIGN KEY (SectionID) REFERENCES Sections(SectionID)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Initialization Error: {ex.Message}");
            }
        }


        public static async Task<string> BackupDatabaseAsync()
        {
            try
            {
                string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winton", "DatabaseBackups");

                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                string backupFileName = $"DatabaseBackup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                string backupFilePath = Path.Combine(backupDirectory, backupFileName);

                await Task.Run(() => File.Copy(dbPath, backupFilePath, overwrite: true));

                return $"Database backup successful! Backup created at: {backupFilePath}";
            }
            catch (Exception ex)
            {
                return $"Error during database backup: {ex.Message}";
            }
        }

        public static async Task<string> RestoreDatabaseAsync()
        {
            try
            {
                string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winton", "DatabaseBackups");

                var backupFiles = Directory.GetFiles(backupDirectory, "*.db")
                                           .OrderByDescending(File.GetCreationTime)
                                           .ToList();

                if (!backupFiles.Any())
                {
                    return "No backup files found!";
                }

                string mostRecentBackup = backupFiles.First();
                await Task.Run(() => File.Copy(mostRecentBackup, dbPath, overwrite: true));

                return "Database restored successfully from the most recent backup!";
            }
            catch (Exception ex)
            {
                return $"Error during database restoration: {ex.Message}";
            }
        }

        public static async Task<string> TestDatabaseConnectionAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};"))
                {
                    await connection.OpenAsync();
                    using (var command = new SqliteCommand("SELECT 1", connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString() == "1" ? "Connection successful." : "Connection failed.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Database connection error: {ex.Message}";
            }
        }

        private static async Task CreateTableAsync(SqliteConnection connection, string tableName, string columnDefinitions)
        {
            string sql = $"CREATE TABLE IF NOT EXISTS {tableName} ({columnDefinitions});";
            await ExecuteNonQueryAsync(connection, sql);
        }

        private static async Task DropTableIfExistsAsync(SqliteConnection connection, string tableName)
        {
            string sql = $"DROP TABLE IF EXISTS {tableName}";
            await ExecuteNonQueryAsync(connection, sql);
        }

        private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
        {
            using (var command = new SqliteCommand(sql, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
