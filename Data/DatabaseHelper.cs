using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Winton.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;


namespace Winton.Data
{
    public static class DatabaseHelper
    {
        private static string dbPath = "WintonDatabase.db";

        public static void InitializeDatabase()
        {


            // Ensure the database file exists; create if it does not.
            bool dbJustCreated = !File.Exists(dbPath);


            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();

                // Drop the old Furniture table if it exists
                DropTableIfExists(connection, "Furniture");
                DropTableIfExists(connection, "ProductPlacementHistory");

                // Create the Sections table if it does not exist
                string createSectionsTableSql = @"
                CREATE TABLE IF NOT EXISTS Sections (
                    SectionID TEXT PRIMARY KEY,
                    Name TEXT
                );";
                ExecuteNonQuery(connection, createSectionsTableSql);

                // Create the SalesData table with the additional columns
                string createSalesDataTableSql = @"
                CREATE TABLE IF NOT EXISTS SalesData (
                    SalesDataID INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlacementID INTEGER,
                    ItemNumber STRING,
                    QuantitySold INTEGER,
                    Revenue DECIMAL(10, 2),
                    SaleDate DATETIME,
                    Grp TEXT,
                    Cat TEXT,
                    TransactionCode TEXT,
                    Price DECIMAL(10, 2),
                    Description TEXT,
                    VendorModel TEXT,
                    FOREIGN KEY (PlacementID) REFERENCES ProductPlacements(PlacementID)
                );";
                ExecuteNonQuery(connection, createSalesDataTableSql);

                // Create the Products table with the additional columns
                string createProductsTableSql = @"
                CREATE TABLE IF NOT EXISTS Products (
                    ProductID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ItemNumber TEXT NOT NULL UNIQUE,
                    ItemName TEXT NOT NULL,
                    Vendor TEXT,
                    Category TEXT,
                    Grp TEXT
                );";
                ExecuteNonQuery(connection, createProductsTableSql);

                // Create the ProductPlacements table with the additional columns
                string createProductsPlacementTableSql = @"
                CREATE TABLE IF NOT EXISTS ProductPlacements (
                    PlacementID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductID INTEGER,
                    SectionID TEXT,
                    ItemNumber STRING,
                    QuantitySold INTEGER,
                    Revenue DECIMAL(10, 2),
                    DatePlaced DATETIME,
                    DateRemoved DATETIME,
                    Cat TEXT,
                    Grp TEXT,
                    FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                    FOREIGN KEY (SectionID) REFERENCES Sections(SectionID)
                );";
                ExecuteNonQuery(connection, createProductsPlacementTableSql);

                // Create the Archive table with the additional columns
                string createArchiveTableSql = @"
                CREATE TABLE IF NOT EXISTS Archive (
                    PlacementID INTEGER PRIMARY KEY,
                    ProductID INTEGER,
                    SectionID TEXT,
                    QuantitySold INTEGER,
                    Revenue DECIMAL(10, 2),
                    DatePlaced DATETIME,
                    DateRemoved DATETIME,
                    RemovalNotes TEXT,
                    FOREIGN KEY(ProductID) REFERENCES Products(ProductID),
                    FOREIGN KEY(SectionID) REFERENCES Sections(SectionID)
                );";
                ExecuteNonQuery(connection, createArchiveTableSql);
            }
        }

        public static string BackupDatabase()
        {
            try
            {
                // Get a secure directory in the AppData\Local folder for the application
                string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp", "DatabaseBackups");

                // Ensure the backup directory exists
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                // Generate a unique backup file name
                string backupFileName = $"DatabaseBackup_{System.DateTime.Now:yyyyMMdd_HHmmss}.db";
                string backupFilePath = Path.Combine(backupDirectory, backupFileName);

                // Copy the database file to the backup location
                File.Copy(dbPath, backupFilePath, overwrite: true);

                return $"Database backup successful! Backup created at: {backupFilePath}";
            }
            catch (Exception ex)
            {
                return $"An error occurred while creating the database backup: {ex.Message}";
            }
        }


        public static string RestoreDatabase()
        {
            try
            {
                // Define the backup directory in AppData where backups are saved
                string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp", "DatabaseBackups");

                // Get the most recent backup file
                var backupFiles = Directory.GetFiles(backupDirectory, "*.db").OrderByDescending(f => File.GetCreationTime(f)).ToList();

                if (!backupFiles.Any())
                {
                    return "No backup files found!";
                }

                string mostRecentBackup = backupFiles.First();

                // Copy the selected backup file to the original database location
                File.Copy(mostRecentBackup, dbPath, overwrite: true);

                return "Database restored successfully from the most recent backup!";
            }
            catch (Exception ex)
            {
                return $"An error occurred while restoring the database: {ex.Message}";
            }
        }




        // Dummy method to simulate closing database connections.
        // You should implement actual logic to ensure no database connections are active during the backup.
        private static void CloseAllDatabaseConnections()
        {
            // Close or dispose of any active connections
            // Example: Ensure you dispose of connection objects properly.
        }




        private static void DropTableIfExists(SqliteConnection connection, string tableName)
        {
            string sql = $"DROP TABLE IF EXISTS {tableName}";
            ExecuteNonQuery(connection, sql);
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using (var command = new SqliteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }


        public static string TestDatabaseConnection()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};"))
                {
                    connection.Open(); // Attempt to open the database
                                       // Optionally perform a simple SELECT query
                    using (var command = new SqliteCommand("SELECT 1", connection))
                    {
                        var result = command.ExecuteScalar().ToString();
                        return result == "1" ? "Connection successful." : "Connection failed.";
                    }
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }


        }

        //ALL THINGS ABOUT PRODUCT MODEL

        public static void ImportProductList(string filePath)
        {
            // Open the Excel file and read the worksheet
            var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            // Extract rows from Excel and map to a list of objects
            var itemsFromExcel = worksheet.RowsUsed().Skip(1) // Skip header
                                        .Select(row => new
                                        {
                                            ItemNumber = row.Cell(1).GetValue<string>(),
                                            ItemName = row.Cell(2).GetValue<string>(),
                                            Vendor = row.Cell(4).GetValue<string>(),
                                            Cat = row.Cell(6).GetValue<string>(),
                                            Grp = row.Cell(7).GetValue<string>()
                                        }).ToList();

            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();

                // Create a dictionary to hold existing items from the database
                var existingItems = new Dictionary<string, string>();
                using (var cmd = new SqliteCommand("SELECT ItemNumber, ItemName FROM Products", connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            existingItems.Add(reader["ItemNumber"].ToString(), reader["ItemName"].ToString());
                        }
                    }
                }

                var seenItemNumbers = new HashSet<string>();

                // Begin the transaction
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var item in itemsFromExcel)
                    {
                        if (seenItemNumbers.Contains(item.ItemNumber))
                        {
                            // Skip duplicate items from the Excel file
                            continue;
                        }

                        seenItemNumbers.Add(item.ItemNumber);

                        if (!existingItems.ContainsKey(item.ItemNumber))
                        {
                            // Insert new items
                            using (var cmd = new SqliteCommand("INSERT INTO Products (ItemNumber, ItemName, Vendor, Category, Grp) VALUES (@ItemNumber, @ItemName, @Vendor, @Cat, @Grp)", connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemNumber", item.ItemNumber);
                                cmd.Parameters.AddWithValue("@ItemName", item.ItemName);
                                cmd.Parameters.AddWithValue("@Vendor", item.Vendor);
                                cmd.Parameters.AddWithValue("@Cat", item.Cat);
                                cmd.Parameters.AddWithValue("@Grp", item.Grp);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (existingItems[item.ItemNumber] != item.ItemName)
                        {
                            // Update existing items if the names differ
                            using (var cmd = new SqliteCommand("UPDATE Products SET ItemName = @ItemName, Vendor = @Vendor, Cat = @Cat, Grp = @Grp WHERE ItemNumber = @ItemNumber", connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemNumber", item.ItemNumber);
                                cmd.Parameters.AddWithValue("@ItemName", item.ItemName);
                                cmd.Parameters.AddWithValue("@Vendor", item.Vendor);
                                cmd.Parameters.AddWithValue("@Cat", item.Cat);
                                cmd.Parameters.AddWithValue("@Grp", item.Grp);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        existingItems.Remove(item.ItemNumber); // Remove from existing items after processing
                    }

                    // Delete items that are in the database but not in the Excel file
                    foreach (var itemNumber in existingItems.Keys)
                    {
                        using (var cmd = new SqliteCommand("DELETE FROM Products WHERE ItemNumber = @ItemNumber", connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Commit the transaction
                    transaction.Commit();
                }
            }
        }



        public static List<Product> GetProducts()
        {
            List<Product> products = new List<Product>();
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                string sql = "SELECT * FROM Products";
                using (var command = new SqliteCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int productID = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : -1; // -1 indicates a problem
                            string itemNumber = reader["ItemNumber"] != DBNull.Value ? reader["ItemNumber"].ToString() : "NULL";
                            string itemName = reader["ItemName"] != DBNull.Value ? reader["ItemName"].ToString() : "NULL";

                            Console.WriteLine($"Debug: ProductID={productID}, ItemNumber={itemNumber}, ItemName={itemName}"); // Add this line for debugging

                            products.Add(new Product
                            {
                                ProductID = productID,
                                ItemNumber = itemNumber,
                                ItemName = itemName,
                            });
                        }
                    }
                }
            }
            return products;
        }


        public static async Task<bool> ProductExistsAsync(string itemNumber)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE ItemNumber = @ItemNumber", connection);
                command.Parameters.AddWithValue("@ItemNumber", itemNumber);
                int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                return count > 0;
            }
        }


        public static async Task PlaceProductAsync(string itemNumber, string sectionId)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();

                var command = new SqliteCommand(
                    "SELECT ProductID, Grp, Category FROM Products WHERE ItemNumber = @ItemNumber",
                    connection
                );
                command.Parameters.AddWithValue("@ItemNumber", itemNumber);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var productId = reader["ProductID"];
                        var grp = reader["Grp"].ToString();
                        var cat = reader["Category"].ToString();

                        command = new SqliteCommand(
                            @"INSERT INTO ProductPlacements 
                    (ProductID, SectionID, ItemNumber, DatePlaced, Grp, Cat) 
                    VALUES 
                    (@ProductID, @SectionID, @ItemNumber, @DatePlaced, @Grp, @Cat)",
                            connection
                        );

                        command.Parameters.AddWithValue("@ProductID", productId);
                        command.Parameters.AddWithValue("@ItemNumber", itemNumber);
                        command.Parameters.AddWithValue("@SectionID", sectionId);
                        command.Parameters.AddWithValue("@DatePlaced", System.DateTime.Now);
                        command.Parameters.AddWithValue("@Grp", grp);
                        command.Parameters.AddWithValue("@Cat", cat);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }



        public static List<ProductPlacement> GetProductsBySection(string sectionID)
        {
            try
            {
                List<ProductPlacement> products = new List<ProductPlacement>();
                using (var connection = new SqliteConnection($"Data Source={dbPath};"))
                {
                    connection.Open();

                    string sql = @"
                SELECT 
                    pp.SectionID,
                    p.ItemNumber,
                    p.ProductID,
                    pp.DatePlaced,
                    pp.Revenue,
                    pp.QuantitySold,
                    pp.Grp
                FROM 
                    ProductPlacements pp
                JOIN 
                    Products p ON pp.ProductID = p.ProductID
                WHERE 
                    pp.SectionID = @SectionID 
                    AND pp.DateRemoved IS NULL"
                    ;

                    using (var command = new SqliteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@SectionID", sectionID);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                products.Add(new ProductPlacement
                                {
                                    SectionID = reader["SectionID"].ToString(),
                                    ProductID = Convert.ToInt32(reader["ProductID"]),
                                    ItemNumber = reader["ItemNumber"].ToString(),
                                    QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                                    Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                                    DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : System.DateTime.MinValue,
                                    Grp = reader["Grp"] != DBNull.Value ? reader["Grp"].ToString() : string.Empty
                                });
                            }
                        }
                    }
                }
                return products;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        public static async Task ArchiveProductPlacement(int productId, string sectionId, string removalNotes, int quantitySold, decimal revenue)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    var cmdArchive = new SqliteCommand(@"
                INSERT INTO Archive (ProductID, SectionID, DatePlaced, DateRemoved, RemovalNotes, QuantitySold, Revenue)
                SELECT ProductID, SectionID, DatePlaced, @DateRemoved, @RemovalNotes, @QuantitySold, @Revenue
                FROM ProductPlacements 
                WHERE ProductID = @ProductID AND SectionID = @SectionID", connection);
                    cmdArchive.Parameters.AddWithValue("@ProductID", productId);
                    cmdArchive.Parameters.AddWithValue("@SectionID", sectionId);
                    cmdArchive.Parameters.AddWithValue("@DateRemoved", System.DateTime.Now);
                    cmdArchive.Parameters.AddWithValue("@RemovalNotes", removalNotes);
                    cmdArchive.Parameters.AddWithValue("@QuantitySold", quantitySold);
                    cmdArchive.Parameters.AddWithValue("@Revenue", revenue);
                    await cmdArchive.ExecuteNonQueryAsync();

                    var cmdRemove = new SqliteCommand("DELETE FROM ProductPlacements WHERE ProductID = @ProductID AND SectionID = @SectionID", connection);
                    cmdRemove.Parameters.AddWithValue("@ProductID", productId);
                    cmdRemove.Parameters.AddWithValue("@SectionID", sectionId);
                    await cmdRemove.ExecuteNonQueryAsync();

                    transaction.Commit();
                }
            }
        }

        public static async Task RemoveProductFromSection(int productId, string sectionId, System.DateTime dateRemoved)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("UPDATE ProductPlacements SET DateRemoved = @DateRemoved WHERE ProductID = @ProductID AND SectionID = @SectionID AND DateRemoved IS NULL", connection);

                command.Parameters.AddWithValue("@ProductID", productId);
                command.Parameters.AddWithValue("@SectionID", sectionId);
                command.Parameters.AddWithValue("@DateRemoved", dateRemoved);

                await command.ExecuteNonQueryAsync();
            }
        }


        public static async Task<ProductPlacement> GetCurrentProductPlacementAsync(string itemNumber)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand("SELECT * FROM ProductPlacements WHERE ItemNumber = @ItemNumber AND DateRemoved IS NULL", connection);
                command.Parameters.AddWithValue("@ItemNumber", itemNumber);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new ProductPlacement
                        {
                            ProductID = reader["ProductID"] != DBNull.Value ? Convert.ToInt32(reader["ProductID"]) : 0,
                            SectionID = reader["SectionID"].ToString(),
                            ItemNumber = reader["ItemNumber"].ToString(),
                            QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                            Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                            DatePlaced = reader["DatePlaced"] != DBNull.Value ? Convert.ToDateTime(reader["DatePlaced"]) : System.DateTime.MinValue
                        };
                    }
                }
            }
            return null;
        }






        //Add a GetProductPlacementBySectionID

        public static List<string> GetProductItemNumbers()
        {
            List<string> itemNumbers = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                string sql = "SELECT ItemNumber FROM Products";
                using (var command = new SqliteCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string itemNumber = reader["ItemNumber"] as string;
                            if (!string.IsNullOrEmpty(itemNumber))
                            {
                                itemNumbers.Add(itemNumber);
                            }
                        }
                    }
                }
            }
            return itemNumbers;
        }



        public static async Task EnsureSectionExistsAsync(string sectionId)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();

                // Check if the section exists
                var cmd = new SqliteCommand("SELECT COUNT(*) FROM Sections WHERE SectionID = @SectionID", connection);
                cmd.Parameters.AddWithValue("@SectionID", sectionId);
                int exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                if (exists == 0)
                {
                    // Section does not exist, create it
                    cmd = new SqliteCommand("INSERT INTO Sections (SectionID) VALUES (@SectionID)", connection);
                    cmd.Parameters.AddWithValue("@SectionID", sectionId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }



        public static async Task<decimal> GetTotalRevenueForCurrentYearAsync()
        {
            decimal totalRevenue = 0;
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath};"))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT SUM(Revenue) 
                FROM SalesData 
                WHERE strftime('%Y', SaleDate) = @CurrentYear"
                    ;

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CurrentYear", System.DateTime.Now.Year.ToString());

                        var result = await command.ExecuteScalarAsync();
                        if (result != DBNull.Value && result != null)
                        {
                            totalRevenue = Convert.ToDecimal(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                Console.WriteLine($"Error retrieving total revenue: {ex.Message}");
            }

            return totalRevenue;
        }

        public static async Task<decimal> GetTotalRevenueForCurrentMonthAsync()
        {
            decimal totalRevenue = 0;
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand(@"
            SELECT SUM(Revenue) 
            FROM SalesData 
            WHERE strftime('%Y-%m', SaleDate) = strftime('%Y-%m', 'now')", connection);

                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    totalRevenue = Convert.ToDecimal(result);
                }
            }
            return totalRevenue;
        }

        public static async Task<decimal> GetRevenueForMonthAsync(int year, int month)
        {
            decimal revenue = 0;
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand(@"
            SELECT SUM(Revenue) 
            FROM SalesData 
            WHERE strftime('%Y', SaleDate) = @Year 
              AND strftime('%m', SaleDate) = @Month", connection);
                command.Parameters.AddWithValue("@Year", year.ToString());
                command.Parameters.AddWithValue("@Month", month.ToString("D2"));

                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    revenue = Convert.ToDecimal(result);
                }
            }
            return revenue;
        }

        public static List<System.DateTime> GetReportsFromLastTwoYears()
        {
            var reportDates = new List<System.DateTime>();
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                var command = new SqliteCommand(@"
            SELECT DISTINCT SaleDate 
            FROM SalesData 
            WHERE SaleDate >= date('now', '-2 years') 
            ORDER BY SaleDate DESC", connection);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        reportDates.Add(Convert.ToDateTime(reader["SaleDate"]));
                    }
                }
            }
            return reportDates;
        }



        public static List<ReportDetail> GetReportDetailsByDate(System.DateTime reportDate)
        {
            List<ReportDetail> reportDetails = new List<ReportDetail>();
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                var command = new SqliteCommand(
                    @"SELECT ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode 
              FROM SalesData 
              WHERE strftime('%Y-%m', SaleDate) = @ReportDate",
                    connection);
                command.Parameters.AddWithValue("@ReportDate", reportDate.ToString("yyyy-MM"));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        reportDetails.Add(new ReportDetail
                        {
                            ItemNumber = reader["ItemNumber"].ToString(),
                            VendorModel = reader["VendorModel"].ToString(),
                            Description = reader["Description"].ToString(),
                            Price = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : (decimal?)null,
                            QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : (int?)null,
                            Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : (decimal?)null,
                            Category = reader["Cat"].ToString(),
                            Group = reader["Grp"].ToString(),
                            TransactionCode = reader["TransactionCode"].ToString()
                        });
                    }
                }
            }
            return reportDetails;
        }


        public static async Task<bool> DeleteReportAsync(System.DateTime saleDate)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    var command = new SqliteCommand("DELETE FROM SalesData WHERE SaleDate = @SaleDate", connection, transaction);
                    command.Parameters.AddWithValue("@SaleDate", saleDate);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    transaction.Commit();

                    return rowsAffected > 0;
                }
            }
        }


        public static void ImportSalesReport(string filePath, System.DateTime saleDate)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);

                using (var connection = new SqliteConnection($"Data Source={dbPath};"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        for (int row = 1; row <= sheet.LastRowNum; row++)
                        {
                            IRow excelRow = sheet.GetRow(row);
                            if (excelRow != null)
                            {
                                // Validate and parse data
                                var itemNumber = excelRow.GetCell(0)?.ToString().Trim();
                                var vendorModel = excelRow.GetCell(1)?.ToString().Trim();
                                var description = excelRow.GetCell(2)?.ToString().Trim();
                                var group = excelRow.GetCell(3)?.ToString().Trim();
                                var category = excelRow.GetCell(4)?.ToString().Trim();
                                var priceCell = excelRow.GetCell(5);
                                var quantitySoldCell = excelRow.GetCell(6);
                                var transactionCode = excelRow.GetCell(7)?.ToString().Trim();

                                // Ensure mandatory fields are not null or empty
                                if (string.IsNullOrEmpty(itemNumber) ||
                                    string.IsNullOrEmpty(transactionCode) ||
                                    priceCell == null || !decimal.TryParse(priceCell.ToString(), out decimal price) ||
                                    quantitySoldCell == null || !int.TryParse(quantitySoldCell.ToString(), out int quantitySold))
                                {
                                    // Skip this row if validation fails
                                    Console.WriteLine($"Skipping row {row}: Invalid or missing data.");
                                    continue;
                                }

                                // Validate transaction code and determine revenue
                                decimal revenue = 0;
                                bool validTransaction = false;

                                switch (transactionCode)
                                {
                                    case "00":
                                    case "05":
                                    case "07":
                                        revenue = price * quantitySold;
                                        validTransaction = true;
                                        break;
                                    case "30":
                                    case "37":
                                        revenue = -(price * quantitySold);
                                        validTransaction = true;
                                        break;
                                    default:
                                        // Ignore other transaction codes
                                        Console.WriteLine($"Ignoring row {row}: Unsupported transaction code {transactionCode}.");
                                        continue;
                                }

                                // Proceed if transaction is valid
                                if (validTransaction)
                                {
                                    try
                                    {
                                        // Insert into SalesData table
                                        var salesDataCmd = new SqliteCommand(
                                            @"INSERT INTO SalesData (ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode, SaleDate)
                                    VALUES (@ItemNumber, @VendorModel, @Description, @Price, @QuantitySold, @Revenue, @Category, @Group, @TransactionCode, @SaleDate)",
                                            connection, transaction);

                                        salesDataCmd.Parameters.AddWithValue("@ItemNumber", (object)itemNumber ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@VendorModel", (object)vendorModel ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Price", price != 0 ? (object)price : DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@QuantitySold", quantitySold != 0 ? (object)quantitySold : DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Revenue", revenue != 0 ? (object)revenue : DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Category", (object)category ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Group", (object)group ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@TransactionCode", (object)transactionCode ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@SaleDate", saleDate != default(System.DateTime) ? (object)saleDate : DBNull.Value);

                                        // Log the command text and parameters for debugging
                                        Console.WriteLine(salesDataCmd.CommandText);
                                        foreach (SqliteParameter param in salesDataCmd.Parameters)
                                        {
                                            Console.WriteLine($"{param.ParameterName}: {param.Value ?? "NULL"}");
                                        }

                                        salesDataCmd.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"SQL Error: {ex.Message}");
                                        throw;  // Re-throw the exception for further debugging
                                    }
                                }
                            }
                        }
                        transaction.Commit();
                    }
                }
            }
        }





        private static void UpdateProductPlacementForItemNumber(SqliteConnection connection, string itemNumber, int quantitySold, decimal revenue)
        {
            // Retrieve the current placement for the item
            var selectCmd = new SqliteCommand(
                "SELECT PlacementID, QuantitySold, Revenue FROM ProductPlacements WHERE ItemNumber = @ItemNumber AND DateRemoved IS NULL",
                connection);
            selectCmd.Parameters.AddWithValue("@ItemNumber", itemNumber);

            using (var reader = selectCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    var placementId = Convert.ToInt32(reader["PlacementID"]);
                    int currentQuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0;
                    decimal currentRevenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0;

                    int updatedQuantitySold = currentQuantitySold + quantitySold;
                    decimal updatedRevenue = currentRevenue + revenue;

                    var updateCmd = new SqliteCommand(
                        "UPDATE ProductPlacements SET QuantitySold = @UpdatedQuantitySold, Revenue = @UpdatedRevenue WHERE PlacementID = @PlacementID",
                        connection);
                    updateCmd.Parameters.AddWithValue("@UpdatedQuantitySold", updatedQuantitySold);
                    updateCmd.Parameters.AddWithValue("@UpdatedRevenue", updatedRevenue);
                    updateCmd.Parameters.AddWithValue("@PlacementID", placementId);
                    updateCmd.ExecuteNonQuery();
                }

            }
        }


        public static async Task<Dictionary<string, decimal>> GetRevenueDataAsync(System.DateTime startDate, System.DateTime endDate)
        {
            var revenueData = new Dictionary<string, decimal>();

            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT SectionID, SUM(Revenue) as TotalRevenue
            FROM ProductPlacements
            WHERE DatePlaced BETWEEN @StartDate AND @EndDate
            GROUP BY SectionID
            ORDER BY TotalRevenue DESC";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var sectionId = reader.GetString(0);
                            // Safe type conversion, handling possible nulls
                            var revenueObject = reader["TotalRevenue"]; // Use indexer with column name for clarity
                            var totalRevenue = (revenueObject != DBNull.Value) ? Convert.ToDecimal(revenueObject) : 0;
                            revenueData[sectionId] = totalRevenue;
                        }
                    }
                }
            }

            return revenueData;
        }




        public static async Task<Dictionary<string, int>> GetSalesQuantityByCategory(int year, int month)
        {
            var categorySales = new Dictionary<string, int>();

            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                await connection.OpenAsync();
                var command = new SqliteCommand(@"
            SELECT Cat, SUM(QuantitySold) AS Quantity
            FROM SalesData
            WHERE strftime('%Y', SaleDate) = @Year AND strftime('%m', SaleDate) = @Month
            GROUP BY Cat", connection);
                command.Parameters.AddWithValue("@Year", year.ToString());
                command.Parameters.AddWithValue("@Month", month.ToString("D2"));

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var category = reader["Cat"].ToString();
                        var quantity = Convert.ToInt32(reader["Quantity"]);
                        categorySales[category] = quantity;
                    }
                }
            }

            return categorySales;
        }

        public static List<SalesData> GetSalesDataByFilters(System.DateTime startDate, System.DateTime endDate, string grp = null, string cat = null)
        {
            var salesData = new List<SalesData>();

            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                var query = "SELECT * FROM SalesData WHERE SaleDate BETWEEN @StartDate AND @EndDate";

                if (!string.IsNullOrEmpty(grp))
                {
                    query += " AND Grp = @Grp";
                }

                if (!string.IsNullOrEmpty(cat))
                {
                    query += " AND Cat = @Cat";
                }

                var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@StartDate", startDate);
                command.Parameters.AddWithValue("@EndDate", endDate);

                if (!string.IsNullOrEmpty(grp))
                {
                    command.Parameters.AddWithValue("@Grp", grp);
                }

                if (!string.IsNullOrEmpty(cat))
                {
                    command.Parameters.AddWithValue("@Cat", cat);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        salesData.Add(new SalesData
                        {
                            SalesDataID = reader["SalesDataID"] != DBNull.Value ? Convert.ToInt32(reader["SalesDataID"]) : 0,
                            PlacementID = reader["PlacementID"] != DBNull.Value ? Convert.ToInt32(reader["PlacementID"]) : 0,
                            ItemNumber = reader["ItemNumber"] != DBNull.Value ? reader["ItemNumber"].ToString() : string.Empty,
                            QuantitySold = reader["QuantitySold"] != DBNull.Value ? Convert.ToInt32(reader["QuantitySold"]) : 0,
                            Revenue = reader["Revenue"] != DBNull.Value ? Convert.ToDecimal(reader["Revenue"]) : 0m,
                            ReportDate = reader["SaleDate"] != DBNull.Value ? Convert.ToDateTime(reader["SaleDate"]) : System.DateTime.MinValue,
                            Group = reader["Grp"] != DBNull.Value ? reader["Grp"].ToString() : string.Empty,
                            Category = reader["Cat"] != DBNull.Value ? reader["Cat"].ToString() : string.Empty
                        });
                    }

                }
            }

            return salesData;
        }

        public static List<ProductPlacement> GetProductPlacementsByFilters(string grp, string cat)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                var query = "SELECT * FROM ProductPlacements WHERE (@Grp IS NULL OR Grp = @Grp) AND (@Cat IS NULL OR Cat = @Cat)";
                var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Grp", string.IsNullOrEmpty(grp) ? (object)DBNull.Value : grp);
                command.Parameters.AddWithValue("@Cat", string.IsNullOrEmpty(cat) ? (object)DBNull.Value : cat);

                var placements = new List<ProductPlacement>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        try
                        {
                            var placement = new ProductPlacement
                            {
                                PlacementID = reader.GetInt32(reader.GetOrdinal("PlacementID")),
                                SectionID = reader.GetString(reader.GetOrdinal("SectionID")),
                                ProductID = reader.GetInt32(reader.GetOrdinal("ProductID")),
                                DatePlaced = reader.GetDateTime(reader.GetOrdinal("DatePlaced")),
                                DateRemoved = reader.IsDBNull(reader.GetOrdinal("DateRemoved")) ? (System.DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateRemoved")),
                                Grp = reader.IsDBNull(reader.GetOrdinal("Grp")) ? null : reader.GetString(reader.GetOrdinal("Grp")),
                                Cat = reader.IsDBNull(reader.GetOrdinal("Cat")) ? null : reader.GetString(reader.GetOrdinal("Cat"))
                            };
                            placements.Add(placement);
                        }
                        catch (InvalidCastException ex)
                        {
                            // Log the exception and the current row data for debugging
                            Console.WriteLine("Error reading row data. Details: " + ex.Message);
                            Console.WriteLine("PlacementID: " + reader["PlacementID"]);
                            Console.WriteLine("SectionID: " + reader["SectionID"]);
                            Console.WriteLine("ProductID: " + reader["ProductID"]);
                            Console.WriteLine("DatePlaced: " + reader["DatePlaced"]);
                            Console.WriteLine("DateRemoved: " + reader["DateRemoved"]);
                            Console.WriteLine("Grp: " + reader["Grp"]);
                            Console.WriteLine("Cat: " + reader["Cat"]);
                        }
                    }
                }
                return placements;
            }
        }

        public static decimal GetMaxRevenue(System.DateTime startDate, System.DateTime endDate)
        {
            decimal totalRevenue = 0;

            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();

                string query = @"
            SELECT SUM(Revenue) as TotalRevenue
            FROM ProductPlacements
            WHERE DatePlaced BETWEEN @StartDate AND @EndDate";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            totalRevenue = reader.GetDecimal(0);
                        }
                    }
                }
            }
            return totalRevenue;
        }

        public static void DeleteAllProducts()
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // SQL command to delete all records from the Products table
                        var deleteCommand = new SqliteCommand("DELETE FROM Products", connection, transaction);
                        deleteCommand.ExecuteNonQuery();

                        // Optional: Reset the auto-increment primary key (if needed)
                        var resetAutoIncrementCommand = new SqliteCommand("DELETE FROM sqlite_sequence WHERE name='Products'", connection, transaction);
                        resetAutoIncrementCommand.ExecuteNonQuery();

                        transaction.Commit();
                        MessageBox.Show("All products have been successfully deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"An error occurred while deleting products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        public static void DeleteSalesReport(ReportDetail reportDetail)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath};"))
            {
                connection.Open();
                using (var command = new SqliteCommand("DELETE FROM SalesData WHERE ItemNumber = @ItemNumber AND VendorModel = @VendorModel AND TransactionCode = @TransactionCode", connection))
                {
                    command.Parameters.AddWithValue("@ItemNumber", reportDetail.ItemNumber);
                    command.Parameters.AddWithValue("@VendorModel", reportDetail.VendorModel);
                    command.Parameters.AddWithValue("@TransactionCode", reportDetail.TransactionCode);
                    command.ExecuteNonQuery();
                }
            }
        }

    }
}



