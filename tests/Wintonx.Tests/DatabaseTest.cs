using Microsoft.Data.Sqlite;
using Winton.Services;
using Xunit;

// Tests share the app's database location setting, so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Winton.Tests
{
    /// <summary>
    /// Gives each test its own temporary database and backup folder, so tests never touch real data.
    /// </summary>
    public abstract class DatabaseTest : IAsyncLifetime
    {
        private readonly string _originalDbPath = DatabaseConfig.DbPath;
        private readonly string _originalBackupFolder = DatabaseConfig.BackupFolder;

        protected string TempFolder { get; } = Path.Combine(Path.GetTempPath(), "WintonxTests", Guid.NewGuid().ToString("N"));

        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(TempFolder);
            DatabaseConfig.DbPath = Path.Combine(TempFolder, "test.db");
            DatabaseConfig.BackupFolder = Path.Combine(TempFolder, "backups");
            await DatabaseService.InitializeDatabaseAsync();
        }

        public Task DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            DatabaseConfig.DbPath = _originalDbPath;
            DatabaseConfig.BackupFolder = _originalBackupFolder;
            try { Directory.Delete(TempFolder, recursive: true); } catch (IOException) { }
            return Task.CompletedTask;
        }

        protected void Execute(string sql, params (string name, object value)[] parameters)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        protected T Scalar<T>(string sql, params (string name, object value)[] parameters)
        {
            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            connection.Open();
            using var command = new SqliteCommand(sql, connection);
            foreach (var (name, value) in parameters)
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return (T)Convert.ChangeType(command.ExecuteScalar(), typeof(T));
        }

        protected int CountSales() => Scalar<int>("SELECT COUNT(*) FROM SalesData");

        protected void AddSection(string id, string name = null) =>
            Execute(@"INSERT INTO Sections (SectionID, Name, XPosition, YPosition, Width, Height, Rotation, ShapeType)
                      VALUES (@Id, @Name, 0, 0, 10, 10, 0, 'Square')",
                ("@Id", id), ("@Name", name ?? id));

        protected void AddProduct(string item, string name = null, string vendor = "Vendor", string category = "Cat", string group = "Grp") =>
            Execute("INSERT INTO Products (ItemNumber, ItemName, Vendor, Category, Grp) VALUES (@Item, @Name, @Vendor, @Cat, @Grp)",
                ("@Item", item), ("@Name", name ?? item), ("@Vendor", vendor), ("@Cat", category), ("@Grp", group));

        protected void AddPlacement(string item, string section, DateTime placed, DateTime? removed = null, int productId = 1) =>
            Execute("INSERT INTO ProductPlacements (ProductID, SectionID, ItemNumber, DatePlaced, DateRemoved) VALUES (@ProductId, @Section, @Item, @Placed, @Removed)",
                ("@ProductId", productId), ("@Section", section), ("@Item", item), ("@Placed", placed), ("@Removed", removed));

        protected void AddSale(string item, DateTime date, decimal revenue, int quantity = 1, string code = "00", string category = "Cat") =>
            Execute("INSERT INTO SalesData (ItemNumber, VendorModel, Revenue, QuantitySold, SaleDate, TransactionCode, Cat) VALUES (@Item, @Item, @Revenue, @Quantity, @Date, @Code, @Cat)",
                ("@Item", item), ("@Revenue", revenue), ("@Quantity", quantity), ("@Date", date), ("@Code", code), ("@Cat", category));

        protected static DateTime Jan(int day) => new DateTime(2026, 1, day);
    }
}
