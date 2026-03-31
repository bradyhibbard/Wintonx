using Microsoft.Data.Sqlite;
using LiveCharts;
using LiveCharts.Wpf;
using System.Diagnostics;
using Winton.Models;

namespace Winton.Services
{
    /// <summary>
    /// Provides database operations related to sales data.
    /// </summary>
    internal class SalesDataServices
    {
        private static readonly string ConnectionString = $"Data Source={DatabaseConfig.DbPath};";

        /// <summary>
        /// Retrieves the total revenue for the current year.
        /// </summary>
        public static async Task<decimal> GetTotalRevenueForCurrentYearAsync()
        {
            return await GetRevenueForTimePeriodAsync("strftime('%Y', SaleDate) = @Year", DateTime.Now.Year);
        }

        /// <summary>
        /// Retrieves the total revenue for the current month.
        /// </summary>
        public static async Task<decimal> GetTotalRevenueForCurrentMonthAsync()
        {
            return await GetRevenueForTimePeriodAsync("strftime('%Y-%m', SaleDate) = strftime('%Y-%m', 'now')");
        }

        /// <summary>
        /// Retrieves the total revenue for a given year or month.
        /// </summary>
        private static async Task<decimal> GetRevenueForTimePeriodAsync(string whereClause, int? year = null)
        {
            decimal totalRevenue = 0;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = $"SELECT SUM(Revenue) FROM SalesData WHERE {whereClause}";

                using var command = new SqliteCommand(query, connection);
                if (year.HasValue)
                    command.Parameters.AddWithValue("@Year", year.Value.ToString());

                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    totalRevenue = Convert.ToDecimal(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetRevenueForTimePeriodAsync: {ex}");
            }

            return totalRevenue;
        }

        /// <summary>
        /// Retrieves monthly revenue for a given year.
        /// </summary>
        public static async Task<SeriesCollection> GetMonthlyRevenueByYearAsync(int year)
        {
            var values = new ChartValues<decimal>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT SUM(Revenue)
                    FROM SalesData
                    WHERE strftime('%Y', SaleDate) = @Year AND strftime('%m', SaleDate) = @Month";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Year", year.ToString());
                command.Parameters.Add("@Month", SqliteType.Text);

                for (int month = 1; month <= 12; month++)
                {
                    command.Parameters["@Month"].Value = month.ToString("D2");
                    var result = await command.ExecuteScalarAsync();
                    values.Add(result != DBNull.Value && result != null ? Convert.ToDecimal(result) : 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetMonthlyRevenueByYearAsync: {ex}");
            }

            return new SeriesCollection
            {
                new LineSeries
                {
                    Title = $"{year}",
                    Values = values
                }
            };
        }

        /// <summary>
        /// Retrieves sales quantity grouped by category.
        /// </summary>
        public static async Task<SeriesCollection> GetCategorySalesDataAsync()
        {
            var seriesCollection = new SeriesCollection();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT Cat, SUM(QuantitySold) AS Quantity
                    FROM SalesData
                    WHERE strftime('%Y-%m', SaleDate) = strftime('%Y-%m', 'now')
                    GROUP BY Cat";

                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string category = reader["Cat"].ToString();
                    int quantity = Convert.ToInt32(reader["Quantity"]);

                    seriesCollection.Add(new PieSeries
                    {
                        Title = category,
                        Values = new ChartValues<int> { quantity },
                        DataLabels = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetCategorySalesDataAsync: {ex}");
            }

            return seriesCollection;
        }

        /// <summary>
        /// Retrieves the total number of customers.
        /// </summary>
        public static async Task<int> GetTotalCustomersAsync()
        {
            int totalCustomers = 0;

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = "SELECT COUNT(DISTINCT CustomerID) FROM SalesData";

                using var command = new SqliteCommand(query, connection);
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null)
                {
                    totalCustomers = Convert.ToInt32(result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetTotalCustomersAsync: {ex}");
            }

            return totalCustomers;
        }

        /// <summary>
        /// Deletes sales data for a specific date.
        /// </summary>
        public static async Task<bool> DeleteReportAsync(DateTime saleDate)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = "DELETE FROM SalesData WHERE SaleDate = @SaleDate";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@SaleDate", saleDate);

                int rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] DeleteReportAsync: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves distinct report dates from the last two years.
        /// </summary>
        public static async Task<List<DateTime>> GetReportsFromLastTwoYearsAsync()
        {
            var reportDates = new List<DateTime>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT DISTINCT SaleDate 
                    FROM SalesData 
                    WHERE SaleDate >= date('now', '-2 years') 
                    ORDER BY SaleDate DESC";

                using var command = new SqliteCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    reportDates.Add(Convert.ToDateTime(reader["SaleDate"]));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetReportsFromLastTwoYearsAsync: {ex}");
            }

            return reportDates;
        }

        public static async Task<List<ReportDetail>> GetReportDetailsByDateAsync(DateTime reportDate)
        {
            var reportDetails = new List<ReportDetail>();

            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = @"
            SELECT ItemNumber, VendorModel, Description, Price, QuantitySold, Revenue, Cat, Grp, TransactionCode
            FROM SalesData
            WHERE date(SaleDate) = date(@ReportDate)";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@ReportDate", reportDate.ToString("yyyy-MM-dd"));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[SalesDataServices] GetReportDetailsByDateAsync: {ex}");
            }

            return reportDetails;
        }

        /// <summary>
        /// Retrieves total revenue for a given section within a date range.
        /// Returns 0 if no sales are found.
        /// </summary>
        public static async Task<decimal> GetRevenueBySectionAsync(string sectionId, DateTime startDate, DateTime endDate)
        {
            decimal totalRevenue = 0;

            using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT IFNULL(SUM(Revenue), 0)
            FROM ProductPlacements
            WHERE SectionID = @SectionID
              AND DatePlaced BETWEEN @StartDate AND @EndDate
              AND DateRemoved IS NULL;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SectionID", sectionId);
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    var result = await command.ExecuteScalarAsync();
                    if (result != DBNull.Value && result != null)
                        totalRevenue = Convert.ToDecimal(result);
                }
            }

            return totalRevenue;
        }

        /// <summary>
        /// Retrieves revenue grouped by SectionID between two dates.
        /// Joins SalesData → ProductPlacements because SalesData has no SectionID.
        /// </summary>
        public static async Task<Dictionary<string, decimal>> GetRevenueDataAsync(DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT 
                pp.SectionID,
                IFNULL(SUM(sd.Revenue), 0) AS TotalRevenue
            FROM SalesData sd
            INNER JOIN ProductPlacements pp 
                ON sd.ItemNumber = pp.ItemNumber
            WHERE sd.SaleDate BETWEEN @StartDate AND @EndDate
              AND pp.DateRemoved IS NULL
            GROUP BY pp.SectionID;
        ";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string sectionId = reader["SectionID"]?.ToString();
                            decimal revenue = reader["TotalRevenue"] != DBNull.Value
                                ? Convert.ToDecimal(reader["TotalRevenue"])
                                : 0m;

                            if (!string.IsNullOrWhiteSpace(sectionId))
                                results[sectionId] = revenue;
                        }
                    }
                }
            }

            return results;
        }
        /// <summary>
        /// Retrieves units sold grouped by SectionID between two dates.
        /// Joins SalesData → ProductPlacements because SalesData has no SectionID.
        /// </summary>
        public static async Task<Dictionary<string, int>> GetQuantityBySectionAsync(DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT
                pp.SectionID,
                IFNULL(SUM(sd.QuantitySold), 0) AS TotalQty
            FROM SalesData sd
            INNER JOIN ProductPlacements pp
                ON sd.ItemNumber = pp.ItemNumber
            WHERE sd.SaleDate BETWEEN @StartDate AND @EndDate
              AND pp.DateRemoved IS NULL
            GROUP BY pp.SectionID;
        ";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string sectionId = reader["SectionID"]?.ToString();
                            int qty = reader["TotalQty"] != DBNull.Value
                                ? Convert.ToInt32(reader["TotalQty"])
                                : 0;

                            if (!string.IsNullOrWhiteSpace(sectionId))
                                results[sectionId] = qty;
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns daily revenue totals for a single section over the given period.
        /// </summary>
        public static async Task<Dictionary<DateTime, decimal>> GetDailyRevenueBySectionAsync(
            string sectionId, DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<DateTime, decimal>();

            using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT date(sd.SaleDate) AS Day, IFNULL(SUM(sd.Revenue), 0) AS DayRevenue
            FROM SalesData sd
            INNER JOIN ProductPlacements pp ON sd.ItemNumber = pp.ItemNumber
            WHERE sd.SaleDate BETWEEN @StartDate AND @EndDate
              AND pp.SectionID = @SectionID
              AND pp.DateRemoved IS NULL
            GROUP BY date(sd.SaleDate)
            ORDER BY date(sd.SaleDate);";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@EndDate",   endDate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@SectionID", sectionId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (DateTime.TryParse(reader.GetString(0), out var day))
                                results[day] = reader.GetDecimal(1);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns revenue per item number for a single section over the given period.
        /// </summary>
        public static async Task<Dictionary<string, decimal>> GetRevenueByItemForSectionAsync(
            string sectionId, DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            using (var connection = new SqliteConnection(ConnectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT sd.ItemNumber, IFNULL(SUM(sd.Revenue), 0) AS ItemRevenue
            FROM SalesData sd
            INNER JOIN ProductPlacements pp ON sd.ItemNumber = pp.ItemNumber
            WHERE sd.SaleDate BETWEEN @StartDate AND @EndDate
              AND pp.SectionID = @SectionID
              AND pp.DateRemoved IS NULL
            GROUP BY sd.ItemNumber
            ORDER BY ItemRevenue DESC;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@StartDate", startDate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@EndDate",   endDate.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@SectionID", sectionId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string item = reader.GetString(0);
                            decimal rev = reader.GetDecimal(1);
                            if (!string.IsNullOrWhiteSpace(item))
                                results[item] = rev;
                        }
                    }
                }
            }

            return results;
        }

        public static async Task<decimal> GetTotalRevenueForYearAsync(int year)
        {
            decimal total = 0;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(
                    "SELECT IFNULL(SUM(Revenue),0) FROM SalesData WHERE strftime('%Y',SaleDate)=@Year",
                    connection);
                command.Parameters.AddWithValue("@Year", year.ToString());
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null) total = Convert.ToDecimal(result);
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetTotalRevenueForYearAsync: {ex}"); }
            return total;
        }

        public static async Task<decimal> GetTotalRevenueForMonthAsync(int year, int month)
        {
            decimal total = 0;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(
                    "SELECT IFNULL(SUM(Revenue),0) FROM SalesData WHERE strftime('%Y',SaleDate)=@Year AND strftime('%m',SaleDate)=@Month",
                    connection);
                command.Parameters.AddWithValue("@Year", year.ToString());
                command.Parameters.AddWithValue("@Month", month.ToString("D2"));
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null) total = Convert.ToDecimal(result);
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetTotalRevenueForMonthAsync: {ex}"); }
            return total;
        }

        public static async Task<int> GetTotalUnitsSoldAsync(DateTime start, DateTime end)
        {
            int total = 0;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(
                    "SELECT IFNULL(SUM(QuantitySold),0) FROM SalesData WHERE SaleDate BETWEEN @Start AND @End",
                    connection);
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@End",   end.ToString("yyyy-MM-dd"));
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null) total = Convert.ToInt32(result);
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetTotalUnitsSoldAsync: {ex}"); }
            return total;
        }

        public static async Task<DateTime?> GetLastReportDateAsync()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand("SELECT MAX(SaleDate) FROM SalesData", connection);
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null && DateTime.TryParse(result.ToString(), out var date))
                    return date;
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetLastReportDateAsync: {ex}"); }
            return null;
        }

        public static async Task<List<(string name, decimal revenue)>> GetTopSectionsByRevenueAsync(
            DateTime start, DateTime end, int count = 5)
        {
            var results = new List<(string, decimal)>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                string query = @"
                    SELECT COALESCE(s.Name, pp.SectionID) AS SectionName,
                           IFNULL(SUM(sd.Revenue), 0) AS TotalRevenue
                    FROM SalesData sd
                    INNER JOIN ProductPlacements pp ON sd.ItemNumber = pp.ItemNumber
                    LEFT JOIN Sections s ON pp.SectionID = s.SectionID
                    WHERE sd.SaleDate BETWEEN @Start AND @End
                      AND pp.DateRemoved IS NULL
                    GROUP BY pp.SectionID
                    ORDER BY TotalRevenue DESC
                    LIMIT @Count;";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@End",   end.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@Count", count);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    results.Add((reader.GetString(0), reader.GetDecimal(1)));
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetTopSectionsByRevenueAsync: {ex}"); }
            return results;
        }

        public static async Task<List<(string category, decimal revenue)>> GetCategoryRevenueAsync(
            DateTime start, DateTime end)
        {
            var results = new List<(string, decimal)>();
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                string query = @"
                    SELECT Cat, IFNULL(SUM(Revenue), 0) AS TotalRevenue
                    FROM SalesData
                    WHERE SaleDate BETWEEN @Start AND @End
                      AND Cat IS NOT NULL AND Cat != ''
                    GROUP BY Cat
                    ORDER BY TotalRevenue DESC;";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@End",   end.ToString("yyyy-MM-dd"));
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    results.Add((reader.GetString(0), reader.GetDecimal(1)));
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetCategoryRevenueAsync: {ex}"); }
            return results;
        }
    }

}
