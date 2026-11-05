using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.IO;

namespace Winton.Services
{
    internal sealed class DatabaseService
    {
        private static string dbPath => DatabaseConfig.DbPath;

        // Placements and the archive are history. They keep their own item numbers so they
        // outlive changes to the product list, and archive rows outlive deleted sections.
        private const string PlacementColumns = @"
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
                FOREIGN KEY (SectionID) REFERENCES Sections(SectionID)";

        private const string ArchiveColumns = @"
                PlacementID INTEGER PRIMARY KEY,
                ProductID INTEGER,
                SectionID TEXT,
                ItemNumber TEXT,
                QuantitySold INTEGER,
                Revenue DECIMAL(10,2),
                DatePlaced DATETIME,
                DateRemoved DATETIME,
                RemovalNotes TEXT";

        /// <summary>
        /// Older databases tied placements to products, and the archive to products and sections.
        /// That blocked deleting a section with archive history and removing a placed product from
        /// the product list. Rebuilds those tables without the ties, keeping all rows, after saving a backup.
        /// </summary>
        private static async Task UpgradeHistoryTablesAsync()
        {
            const string needsUpgradeSql = @"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table'
                  AND ((name = 'ProductPlacements' AND sql LIKE '%REFERENCES Products%')
                    OR (name = 'Archive' AND sql LIKE '%FOREIGN KEY%'))";

            using (var checkConnection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await checkConnection.OpenAsync();
                using var checkCommand = new SqliteCommand(needsUpgradeSql, checkConnection);
                if (Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) == 0) return;
            }

            await CopyDatabaseToBackupAsync("BeforeUpgrade");

            using var connection = new SqliteConnection($"Data Source={dbPath};");
            await connection.OpenAsync();

            // SQLite ignores this setting inside a transaction, so it goes first and is restored after.
            await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = OFF");
            try
            {
                using var transaction = connection.BeginTransaction();

                await RebuildTableAsync(connection, transaction, "ProductPlacements", PlacementColumns,
                    "PlacementID, ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, DateRemoved, Cat, Grp");
                await RebuildTableAsync(connection, transaction, "Archive", ArchiveColumns,
                    "PlacementID, ProductID, SectionID, ItemNumber, QuantitySold, Revenue, DatePlaced, DateRemoved, RemovalNotes");

                await transaction.CommitAsync();
            }
            finally
            {
                await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON");
            }
        }

        private static async Task RebuildTableAsync(
            SqliteConnection connection, SqliteTransaction transaction, string table, string columnDefinitions, string columnNames)
        {
            string[] statements =
            {
                $"CREATE TABLE {table}_upgraded ({columnDefinitions})",
                $"INSERT INTO {table}_upgraded ({columnNames}) SELECT {columnNames} FROM {table}",
                $"DROP TABLE {table}",
                $"ALTER TABLE {table}_upgraded RENAME TO {table}"
            };

            foreach (string sql in statements)
            {
                using var command = new SqliteCommand(sql, connection, transaction);
                await command.ExecuteNonQueryAsync();
            }
        }

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

                    await CreateTableAsync(connection, "ProductPlacements", PlacementColumns);
                    await CreateTableAsync(connection, "Archive", ArchiveColumns);
                }

                await UpgradeHistoryTablesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database Initialization Error: {ex}");
            }
        }


        /// <summary>
        /// Folder holding database backups.
        /// </summary>
        public static string BackupDirectory => DatabaseConfig.BackupFolder;

        public static async Task<string> BackupDatabaseAsync()
        {
            try
            {
                string backupFilePath = await CopyDatabaseToBackupAsync("DatabaseBackup");
                return $"Database backup successful! Backup created at: {backupFilePath}";
            }
            catch (Exception ex)
            {
                return $"Error during database backup: {ex.Message}";
            }
        }

        private static async Task<string> CopyDatabaseToBackupAsync(string prefix)
        {
            Directory.CreateDirectory(BackupDirectory);
            string backupFilePath = Path.Combine(BackupDirectory, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            // Pooled connections can keep the database file open.
            SqliteConnection.ClearAllPools();
            await Task.Run(() => File.Copy(dbPath, backupFilePath, overwrite: true));
            return backupFilePath;
        }

        /// <summary>
        /// Replaces the database with a backup, first saving a copy of the current database.
        /// </summary>
        public static async Task<string> RestoreDatabaseAsync(string backupFilePath)
        {
            try
            {
                string safetyCopy = await CopyDatabaseToBackupAsync("BeforeRestore");

                SqliteConnection.ClearAllPools();
                await Task.Run(() => File.Copy(backupFilePath, dbPath, overwrite: true));

                // An older backup may need the same upgrade the app does at startup.
                await UpgradeHistoryTablesAsync();

                return $"Database restored successfully. Your previous data was saved to: {safetyCopy}";
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
