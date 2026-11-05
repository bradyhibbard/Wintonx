using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using NPOI.SS.UserModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Winton.Models;

namespace Winton.Services
{
    /// <summary>
    /// What happened during a sales report import.
    /// </summary>
    internal sealed class SalesImportResult
    {
        public int RowsImported { get; set; }
        public int RowsSkippedInvalid { get; set; }
        public int RowsSkippedUnknownCode { get; set; }
        public int ItemsNotInProductList { get; set; }
    }

    /// <summary>
    /// Handles importing and deleting sales reports and product lists from external files.
    /// </summary>
    internal class ImportServices
    {
        private static readonly HashSet<string> ValidTransactionCodes = new() { "00", "05", "07", "30", "37" };


        /// <summary>
        /// Imports a sales report, matching columns by header name. All rows are saved or none are:
        /// any failure throws and leaves the database unchanged.
        /// </summary>
        /// <param name="replaceExisting">Removes existing data for this date as part of the same save.</param>
        public static async Task<SalesImportResult> ImportSalesReportWithMappingAsync(
            string filePath, DateTime saleDate, bool replaceExisting = false)
        {
            // How the report's headers map to logical fields in SalesData.
            var headerMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Vendor Model",    "VendorModel" },   // Also used as the ItemNumber
                { "Qty Shp",         "QuantitySold" },
                { "Cse Sell Price",  "Price" },
                { "Group",           "Grp" },
                { "Category",        "Cat" },
                { "Tr",              "TransactionCode" },
                { "Description",     "Description" }
            };

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IWorkbook workbook = WorkbookFactory.Create(stream);
            ISheet sheet = workbook.GetSheetAt(0);
            IRow headerRow = sheet?.GetRow(0)
                ?? throw new InvalidOperationException("The first sheet has no header row.");

            var columnIndexByField = new Dictionary<string, int>();
            for (int col = 0; col < headerRow.LastCellNum; col++)
            {
                string headerText = headerRow.GetCell(col)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(headerText) && headerMapping.TryGetValue(headerText, out var fieldName))
                    columnIndexByField[fieldName] = col;
            }

            var missingHeaders = new[] { ("VendorModel", "Vendor Model"), ("QuantitySold", "Qty Shp"), ("Price", "Cse Sell Price"), ("TransactionCode", "Tr") }
                .Where(required => !columnIndexByField.ContainsKey(required.Item1))
                .Select(required => required.Item2)
                .ToList();
            if (missingHeaders.Count > 0)
                throw new InvalidOperationException($"The report is missing these columns: {string.Join(", ", missingHeaders)}.");

            var result = new SalesImportResult();

            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            string reportDate = saleDate.ToString("yyyy-MM-dd");

            if (replaceExisting)
            {
                using var deleteCmd = new SqliteCommand("DELETE FROM SalesData WHERE date(SaleDate) = @ReportDate", connection, transaction);
                deleteCmd.Parameters.AddWithValue("@ReportDate", reportDate);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            string insertSql = @"
                INSERT INTO SalesData
                (ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode, SaleDate)
                VALUES
                (@ItemNumber, @VendorModel, @Description, @Price, @QuantitySold, @Revenue, @Category, @Group, @TransactionCode, @SaleDate)
            ";
            using var insertCmd = new SqliteCommand(insertSql, connection, transaction);

            for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow excelRow = sheet.GetRow(rowIndex);
                if (excelRow == null) continue;

                string vendorModel = GetCellValue(excelRow, columnIndexByField, "VendorModel");
                string quantityStr = GetCellValue(excelRow, columnIndexByField, "QuantitySold");
                string priceStr = GetCellValue(excelRow, columnIndexByField, "Price");
                string grp = GetCellValue(excelRow, columnIndexByField, "Grp");
                string cat = GetCellValue(excelRow, columnIndexByField, "Cat");
                string transactionCode = NormalizeTransactionCode(GetCellValue(excelRow, columnIndexByField, "TransactionCode"));
                string description = GetCellValue(excelRow, columnIndexByField, "Description");

                // Wholly blank rows at the end of a sheet aren't worth reporting.
                if (string.IsNullOrEmpty(vendorModel) && string.IsNullOrEmpty(quantityStr) &&
                    string.IsNullOrEmpty(priceStr) && string.IsNullOrEmpty(transactionCode))
                    continue;

                if (string.IsNullOrEmpty(vendorModel) ||
                    !decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal price) ||
                    !decimal.TryParse(quantityStr, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsedQuantity) ||
                    parsedQuantity != Math.Truncate(parsedQuantity))
                {
                    result.RowsSkippedInvalid++;
                    continue;
                }

                if (string.IsNullOrEmpty(transactionCode) || !ValidTransactionCodes.Contains(transactionCode))
                {
                    result.RowsSkippedUnknownCode++;
                    continue;
                }

                int quantity = (int)parsedQuantity;
                decimal revenue = transactionCode is "30" or "37"
                    ? -Math.Abs(price * quantity)   // return/refund
                    : price * quantity;             // normal sale

                insertCmd.Parameters.Clear();
                insertCmd.Parameters.AddWithValue("@ItemNumber", vendorModel);
                insertCmd.Parameters.AddWithValue("@VendorModel", vendorModel);
                insertCmd.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Price", price);
                insertCmd.Parameters.AddWithValue("@QuantitySold", quantity);
                insertCmd.Parameters.AddWithValue("@Revenue", revenue);
                insertCmd.Parameters.AddWithValue("@Category", cat ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Group", grp ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@TransactionCode", transactionCode);
                insertCmd.Parameters.AddWithValue("@SaleDate", saleDate.Date);

                await insertCmd.ExecuteNonQueryAsync();
                result.RowsImported++;
            }

            if (result.RowsImported == 0)
                throw new InvalidOperationException(
                    $"No rows could be imported ({result.RowsSkippedInvalid} had invalid data, " +
                    $"{result.RowsSkippedUnknownCode} had an unrecognized transaction code).");

            using (var unmatchedCmd = new SqliteCommand(@"
                SELECT COUNT(DISTINCT ItemNumber)
                FROM SalesData
                WHERE date(SaleDate) = @ReportDate
                  AND ItemNumber NOT IN (SELECT ItemNumber FROM Products)", connection, transaction))
            {
                unmatchedCmd.Parameters.AddWithValue("@ReportDate", reportDate);
                result.ItemsNotInProductList = Convert.ToInt32(await unmatchedCmd.ExecuteScalarAsync());
            }

            await transaction.CommitAsync();
            return result;
        }

        /// <summary>
        /// Excel can store codes as numbers, which turns "05" into 5. Restores the two-digit form.
        /// </summary>
        private static string NormalizeTransactionCode(string code) =>
            code != null && code.Length == 1 && char.IsDigit(code[0]) ? "0" + code : code;

        /// <summary>
        /// Gets a cell's value as text. Formula cells use their calculated result.
        /// </summary>
        private static string GetCellValue(IRow row, Dictionary<string, int> columnIndexByField, string fieldName)
        {
            if (!columnIndexByField.TryGetValue(fieldName, out int colIndex)) return null;

            ICell cell = row.GetCell(colIndex);
            if (cell == null) return null;

            CellType type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
            return type switch
            {
                CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.CurrentCulture),
                CellType.String  => cell.StringCellValue?.Trim(),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                _                => null
            };
        }
    




        /// <summary>
        /// Imports a product list from an Excel file. The file replaces the current list:
        /// new items are added, existing items updated, and items not in the file removed.
        /// Any failure throws and leaves the database unchanged.
        /// </summary>
        /// <returns>The number of products in the imported list.</returns>
        public static async Task<int> ImportProductListAsync(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var items = worksheet.RowsUsed().Skip(1)
                .Select(row => new
                {
                    ItemNumber = row.Cell(1).GetValue<string>().Trim(),
                    ItemName = row.Cell(2).GetValue<string>(),
                    Vendor = row.Cell(4).GetValue<string>(),
                    Cat = row.Cell(6).GetValue<string>(),
                    Grp = row.Cell(7).GetValue<string>()
                })
                .Where(item => !string.IsNullOrEmpty(item.ItemNumber))
                .GroupBy(item => item.ItemNumber)
                .Select(group => group.First())
                .ToList();

            if (items.Count == 0)
                throw new InvalidOperationException("No products were found in the file, so the current list was left unchanged.");

            using var connection = new SqliteConnection($"Data Source={DatabaseConfig.DbPath};");
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            var existingItems = new HashSet<string>();
            using (var cmd = new SqliteCommand("SELECT ItemNumber FROM Products", connection, transaction))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    existingItems.Add(reader.GetString(0));
            }

            foreach (var item in items)
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO Products (ItemNumber, ItemName, Vendor, Category, Grp)
                    VALUES (@ItemNumber, @ItemName, @Vendor, @Cat, @Grp)
                    ON CONFLICT(ItemNumber) DO UPDATE SET
                        ItemName = excluded.ItemName,
                        Vendor = excluded.Vendor,
                        Category = excluded.Category,
                        Grp = excluded.Grp", connection, transaction);
                cmd.Parameters.AddWithValue("@ItemNumber", item.ItemNumber);
                cmd.Parameters.AddWithValue("@ItemName", item.ItemName);
                cmd.Parameters.AddWithValue("@Vendor", item.Vendor);
                cmd.Parameters.AddWithValue("@Cat", item.Cat);
                cmd.Parameters.AddWithValue("@Grp", item.Grp);
                await cmd.ExecuteNonQueryAsync();

                existingItems.Remove(item.ItemNumber);
            }

            foreach (var itemNumber in existingItems)
            {
                using var cmd = new SqliteCommand("DELETE FROM Products WHERE ItemNumber = @ItemNumber", connection, transaction);
                cmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
                await cmd.ExecuteNonQueryAsync();
            }

            // Keep placements pointing at the right product, including any whose
            // IDs went stale after the whole product list was deleted and re-imported.
            using (var relinkCmd = new SqliteCommand(@"
                UPDATE ProductPlacements
                SET ProductID = (SELECT p.ProductID FROM Products p WHERE p.ItemNumber = ProductPlacements.ItemNumber)
                WHERE ItemNumber IN (SELECT ItemNumber FROM Products)", connection, transaction))
            {
                await relinkCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return items.Count;
        }

        /// <summary>
        /// Deletes all products. IDs are not reused, so existing placements cannot end up pointing at a different product.
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
