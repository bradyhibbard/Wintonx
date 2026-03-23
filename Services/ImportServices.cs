using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Diagnostics;
using System.IO;
using Winton.Models;

namespace Winton.Services
{
    /// <summary>
    /// Handles importing and deleting sales reports and product lists from external files.
    /// </summary>
    internal class ImportServices
    {
        /// <summary>
        /// Imports a sales report from an Excel file and saves it to the database.
        /// </summary>
        /// <param name="filePath">Path to the sales report Excel file.</param>
        /// <param name="saleDate">Date associated with the sales report.</param>
        public static async Task ImportSalesReportAsync(string filePath, DateTime saleDate)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    IWorkbook workbook = new XSSFWorkbook(stream);
                    ISheet sheet = workbook.GetSheetAt(0);

                    using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                    {
                        await connection.OpenAsync();
                        using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync())
                        {
                            for (int row = 1; row <= sheet.LastRowNum; row++)
                            {
                                IRow excelRow = sheet.GetRow(row);
                                if (excelRow == null) continue; // Skip empty rows

                                try
                                {
                                    // Extract and sanitize data
                                    var itemNumber = excelRow.GetCell(0)?.ToString()?.Trim();
                                    var vendorModel = excelRow.GetCell(1)?.ToString()?.Trim();
                                    var description = excelRow.GetCell(2)?.ToString()?.Trim();
                                    var group = excelRow.GetCell(3)?.ToString()?.Trim();
                                    var category = excelRow.GetCell(4)?.ToString()?.Trim();
                                    var transactionCode = excelRow.GetCell(7)?.ToString()?.Trim();

                                    // Validate numeric fields
                                    bool validPrice = decimal.TryParse(excelRow.GetCell(5)?.ToString(), out decimal price);
                                    bool validQuantity = int.TryParse(excelRow.GetCell(6)?.ToString(), out int quantitySold);

                                    if (string.IsNullOrEmpty(itemNumber) || string.IsNullOrEmpty(transactionCode) ||
                                        !validPrice || !validQuantity)
                                    {
                                        Debug.WriteLine($"[ImportServices] Skipping row {row}: invalid data.");
                                        continue;
                                    }

                                    // Determine revenue based on transaction code
                                    decimal revenue = 0;
                                    bool validTransaction = transactionCode switch
                                    {
                                        "00" or "05" or "07" => true,  // Normal sales transactions
                                        "30" or "37" => true,  // Returns/refunds (negative revenue)
                                        _ => false
                                    };

                                    if (!validTransaction)
                                    {
                                        Debug.WriteLine($"[ImportServices] Ignoring row {row}: unsupported transaction code {transactionCode}.");
                                        continue;
                                    }

                                    revenue = transactionCode switch
                                    {
                                        "00" or "05" or "07" => price * quantitySold,  // Standard sales
                                        "30" or "37" => -(price * quantitySold), // Refunds
                                        _ => 0
                                    };

                                    // Insert data into SalesData table
                                    using (var salesDataCmd = new SqliteCommand(@"
                                INSERT INTO SalesData 
                                (ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode, SaleDate)
                                VALUES 
                                (@ItemNumber, @VendorModel, @Description, @Price, @QuantitySold, @Revenue, @Category, @Group, @TransactionCode, @SaleDate)",
                                        connection, transaction))
                                    {
                                        salesDataCmd.Parameters.AddWithValue("@ItemNumber", (object)itemNumber ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@VendorModel", (object)vendorModel ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Price", price);
                                        salesDataCmd.Parameters.AddWithValue("@QuantitySold", quantitySold);
                                        salesDataCmd.Parameters.AddWithValue("@Revenue", revenue);
                                        salesDataCmd.Parameters.AddWithValue("@Category", (object)category ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@Group", (object)group ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@TransactionCode", (object)transactionCode ?? DBNull.Value);
                                        salesDataCmd.Parameters.AddWithValue("@SaleDate", saleDate);

                                        await salesDataCmd.ExecuteNonQueryAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[ImportServices] SQL error on row {row}: {ex}");
                                }
                            }

                            await transaction.CommitAsync();
                        }
                    }
                }

                Debug.WriteLine("[ImportServices] Sales report imported successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportServices] ImportSalesReportAsync: {ex}");
            }
        }

        public static async Task ImportSalesReportWithMappingAsync(string filePath, DateTime saleDate)
        {
            // 1) Define how your Excel headers map to logical fields in SalesData
            // We ignore "Purchase Status" (not mapped).
            // If you have TWO columns named "Vendor Model," see note below.
            var headerMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Vendor Model",    "VendorModel" },   // We'll also treat this as ItemNumber
                { "Qty Shp",         "QuantitySold" },
                { "Cse Sell Price",  "Price" },
                { "Group",           "Grp" },
                { "Category",        "Cat" },
                { "Tr",              "TransactionCode" },
                { "Description",     "Description" }
            };

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);
                if (sheet == null)
                {
                    Debug.WriteLine("[ImportServices] No sheet found in the Excel file.");
                    return;
                }

                // 2) Read the header row (row 0) to build a { fieldName -> columnIndex } map
                IRow headerRow = sheet.GetRow(0);
                if (headerRow == null)
                {
                    Debug.WriteLine("[ImportServices] Header row is missing or empty. Cannot import.");
                    return;
                }

                // This dictionary will tell us which column index corresponds to each field name
                var columnIndexByField = new Dictionary<string, int>();

                for (int col = 0; col < headerRow.LastCellNum; col++)
                {
                    string headerText = headerRow.GetCell(col)?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(headerText)) continue;

                    // If this header is in our mapping, record the field name -> column index
                    if (headerMapping.TryGetValue(headerText, out var fieldName))
                    {
                        // If there's a chance you have two "Vendor Model" columns, you might skip the first or handle duplicates here
                        // For now, we assume only one "Vendor Model" column or that the second is not encountered.
                        columnIndexByField[fieldName] = col;
                    }
                }

                // 3) Connect to the database
                using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                // Prepare the INSERT statement
                string insertSql = @"
                    INSERT INTO SalesData
                    (ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode, SaleDate)
                    VALUES
                    (@ItemNumber, @VendorModel, @Description, @Price, @QuantitySold, @Revenue, @Category, @Group, @TransactionCode, @SaleDate)
                ";
                using var insertCmd = new SqliteCommand(insertSql, connection, transaction);

                // 4) Parse each data row (starting from row 1)
                for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow excelRow = sheet.GetRow(rowIndex);
                    if (excelRow == null) continue; // skip empty rows

                    try
                    {
                        // Safely get the string for each mapped field
                        string vendorModel = GetCellValue(excelRow, columnIndexByField, "VendorModel");
                        string quantityStr = GetCellValue(excelRow, columnIndexByField, "QuantitySold");
                        string priceStr = GetCellValue(excelRow, columnIndexByField, "Price");
                        string grp = GetCellValue(excelRow, columnIndexByField, "Grp");
                        string cat = GetCellValue(excelRow, columnIndexByField, "Cat");
                        string transactionCode = GetCellValue(excelRow, columnIndexByField, "TransactionCode");
                        string description = GetCellValue(excelRow, columnIndexByField, "Description");

                        // We'll treat itemNumber as the same as vendorModel for now
                        string itemNumber = vendorModel;

                        if (string.IsNullOrEmpty(itemNumber))
                        {
                            continue;
                        }

                        // Validate numeric fields
                        if (!decimal.TryParse(priceStr, out decimal price) || !int.TryParse(quantityStr, out int quantity))
                        {
                            continue;
                        }

                        // Check transaction code
                        var validCodes = new HashSet<string> { "00", "05", "07", "30", "37" };
                        if (string.IsNullOrEmpty(transactionCode) || !validCodes.Contains(transactionCode))
                        {
                            Debug.WriteLine($"[ImportServices] Skipping row {rowIndex}: invalid transaction code '{transactionCode}'.");
                            continue;
                        }

                        // Calculate revenue
                        decimal revenue = transactionCode switch
                        {
                            "00" or "05" or "07" => price * quantity,    // normal sale
                            "30" or "37" => -(price * quantity), // return/refund
                            _ => 0
                        };

                        // 5) Insert into SalesData
                        insertCmd.Parameters.Clear();
                        insertCmd.Parameters.AddWithValue("@ItemNumber", itemNumber ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@VendorModel", vendorModel ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Price", price);
                        insertCmd.Parameters.AddWithValue("@QuantitySold", quantity);
                        insertCmd.Parameters.AddWithValue("@Revenue", revenue);
                        insertCmd.Parameters.AddWithValue("@Category", cat ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Group", grp ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@TransactionCode", transactionCode ?? (object)DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@SaleDate", saleDate);

                        await insertCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ImportServices] Row {rowIndex} error: {ex}");
                    }
                }

                await transaction.CommitAsync();
                Debug.WriteLine("[ImportServices] Import complete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportServices] ImportSalesReportWithMappingAsync: {ex}");
            }
        }

        /// <summary>
        /// Helper to safely get the cell value as a string, given a row and a field mapping.
        /// </summary>
        private static string GetCellValue(IRow row, Dictionary<string, int> columnIndexByField, string fieldName)
        {
            if (columnIndexByField.TryGetValue(fieldName, out int colIndex))
            {
                return row.GetCell(colIndex)?.ToString()?.Trim();
            }
            return null;
        }
    


    /// <summary>
    /// Deletes a specific sales report entry from the database.
    /// </summary>
    /// <param name="reportDetail">Report detail object containing identifying information.</param>
    public static async Task DeleteSalesReportAsync(ReportDetail reportDetail)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();

                    using (var command = new SqliteCommand(
                        "DELETE FROM SalesData WHERE ItemNumber = @ItemNumber AND VendorModel = @VendorModel AND TransactionCode = @TransactionCode",
                        connection))
                    {
                        command.Parameters.AddWithValue("@ItemNumber", reportDetail.ItemNumber);
                        command.Parameters.AddWithValue("@VendorModel", reportDetail.VendorModel);
                        command.Parameters.AddWithValue("@TransactionCode", reportDetail.TransactionCode);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                Debug.WriteLine("[ImportServices] Sales report entry deleted successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportServices] DeleteSalesReportAsync: {ex}");
            }
        }


        /// <summary>
        /// Imports a product list from an Excel file and updates the database.
        /// </summary>
        /// <param name="filePath">Path to the Excel file.</param>
        public static async Task ImportProductListAsync(string filePath)
        {
            try
            {
                var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);

                // Extract rows from Excel and map to a list of objects
                var itemsFromExcel = worksheet.RowsUsed().Skip(1)
                    .Select(row => new
                    {
                        ItemNumber = row.Cell(1).GetValue<string>(),
                        ItemName = row.Cell(2).GetValue<string>(),
                        Vendor = row.Cell(4).GetValue<string>(),
                        Cat = row.Cell(6).GetValue<string>(),
                        Grp = row.Cell(7).GetValue<string>()
                    })
                    .ToList();

                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    var existingItems = new Dictionary<string, string>();

                    // Fetch existing product data
                    using (var cmd = new SqliteCommand("SELECT ItemNumber, ItemName FROM Products", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            existingItems[reader.GetString(0)] = reader.GetString(1);
                        }
                    }

                    var seenItemNumbers = new HashSet<string>();

                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var item in itemsFromExcel)
                        {
                            if (seenItemNumbers.Contains(item.ItemNumber)) continue; // Skip duplicates
                            seenItemNumbers.Add(item.ItemNumber);

                            if (!existingItems.ContainsKey(item.ItemNumber))
                            {
                                using (var cmd = new SqliteCommand(
                                    "INSERT INTO Products (ItemNumber, ItemName, Vendor, Category, Grp) VALUES (@ItemNumber, @ItemName, @Vendor, @Cat, @Grp)",
                                    connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemNumber", item.ItemNumber);
                                    cmd.Parameters.AddWithValue("@ItemName", item.ItemName);
                                    cmd.Parameters.AddWithValue("@Vendor", item.Vendor);
                                    cmd.Parameters.AddWithValue("@Cat", item.Cat);
                                    cmd.Parameters.AddWithValue("@Grp", item.Grp);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            else if (existingItems[item.ItemNumber] != item.ItemName)
                            {
                                using (var cmd = new SqliteCommand(
                                    "UPDATE Products SET ItemName = @ItemName, Vendor = @Vendor, Cat = @Cat, Grp = @Grp WHERE ItemNumber = @ItemNumber",
                                    connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemNumber", item.ItemNumber);
                                    cmd.Parameters.AddWithValue("@ItemName", item.ItemName);
                                    cmd.Parameters.AddWithValue("@Vendor", item.Vendor);
                                    cmd.Parameters.AddWithValue("@Cat", item.Cat);
                                    cmd.Parameters.AddWithValue("@Grp", item.Grp);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            existingItems.Remove(item.ItemNumber);
                        }

                        // Delete items not in the Excel file
                        foreach (var itemNumber in existingItems.Keys)
                        {
                            using (var cmd = new SqliteCommand("DELETE FROM Products WHERE ItemNumber = @ItemNumber", connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportServices] ImportProductListAsync: {ex}");
            }
        }

        /// <summary>
        /// Deletes all products from the database and resets the auto-increment counter.
        /// </summary>
        public static async Task<bool> DeleteAllProductsAsync()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};"))
                {
                    await connection.OpenAsync();
                    using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Delete all records
                            using (var deleteCommand = new SqliteCommand("DELETE FROM Products", connection, transaction))
                            {
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            // Reset auto-increment counter
                            using (var resetCommand = new SqliteCommand("DELETE FROM sqlite_sequence WHERE name='Products'", connection, transaction))
                            {
                                await resetCommand.ExecuteNonQueryAsync();
                            }

                            await transaction.CommitAsync();
                            Debug.WriteLine("[ImportServices] All products deleted successfully.");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            Debug.WriteLine($"[ImportServices] DeleteAllProductsAsync: {ex}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportServices] DeleteAllProductsAsync (outer): {ex}");
                return false;
            }
        }
    }
}
