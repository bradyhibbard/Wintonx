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

        // Returns (codes 30 and 37) reduce units sold, whatever sign the report used.
        private const string SignedQuantitySql =
            "CASE WHEN sd.TransactionCode IN ('30','37') THEN -ABS(sd.QuantitySold) ELSE sd.QuantitySold END";

        // Assigns each sale to the sections its item was actually in on the sale date.
        // An item in several sections on that date has the sale split evenly between them,
        // so section totals add up to the store total rather than counting it twice.
        // Requires @Start and @End (yyyy-MM-dd).
        private const string AttributedSalesCte = @"
            WITH matched AS (
                SELECT DISTINCT sd.SalesDataID, pp.SectionID
                FROM SalesData sd
                INNER JOIN ProductPlacements pp ON sd.ItemNumber = pp.ItemNumber
                WHERE date(sd.SaleDate) BETWEEN @Start AND @End
                  AND date(sd.SaleDate) >= date(pp.DatePlaced)
                  AND (pp.DateRemoved IS NULL OR date(sd.SaleDate) <= date(pp.DateRemoved))
            ),
            attributed AS (
                SELECT m.SectionID,
                       sd.ItemNumber,
                       date(sd.SaleDate) AS Day,
                       sd.Revenue * 1.0 / COUNT(*) OVER (PARTITION BY m.SalesDataID) AS Revenue,
                       (" + SignedQuantitySql + @") * 1.0 / COUNT(*) OVER (PARTITION BY m.SalesDataID) AS Quantity
                FROM matched m
                INNER JOIN SalesData sd ON sd.SalesDataID = m.SalesDataID
            )";

        private static void AddDateRange(SqliteCommand command, DateTime start, DateTime end)
        {
            command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd"));
        }

        private static decimal ReadDecimal(SqliteDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetDouble(ordinal));




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
        /// Deletes every sales row for one report date.
        /// </summary>
        public static async Task<bool> DeleteReportAsync(DateTime saleDate)
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();

                string query = "DELETE FROM SalesData WHERE date(SaleDate) = @SaleDate";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@SaleDate", saleDate.ToString("yyyy-MM-dd"));

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
        /// Returns true if sales data has already been imported for this date.
        /// </summary>
        public static async Task<bool> ReportExistsAsync(DateTime reportDate)
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            using var command = new SqliteCommand(
                "SELECT EXISTS(SELECT 1 FROM SalesData WHERE date(SaleDate) = @ReportDate)", connection);
            command.Parameters.AddWithValue("@ReportDate", reportDate.ToString("yyyy-MM-dd"));

            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
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
                    SELECT DISTINCT date(SaleDate) AS SaleDate
                    FROM SalesData
                    WHERE date(SaleDate) >= date('now', '-2 years')
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
        /// Retrieves revenue attributed to one section within a date range.
        /// </summary>
        public static async Task<decimal> GetRevenueBySectionAsync(string sectionId, DateTime startDate, DateTime endDate)
        {
            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = AttributedSalesCte + @"
                SELECT SUM(Revenue) FROM attributed WHERE SectionID = @SectionID;";

            using var command = new SqliteCommand(query, connection);
            AddDateRange(command, startDate, endDate);
            command.Parameters.AddWithValue("@SectionID", sectionId);

            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
        }

        /// <summary>
        /// Retrieves revenue grouped by SectionID between two dates.
        /// </summary>
        public static async Task<Dictionary<string, decimal>> GetRevenueDataAsync(DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = AttributedSalesCte + @"
                SELECT SectionID, SUM(Revenue) FROM attributed GROUP BY SectionID;";

            using var command = new SqliteCommand(query, connection);
            AddDateRange(command, startDate, endDate);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string sectionId = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(sectionId))
                    results[sectionId] = ReadDecimal(reader, 1);
            }

            return results;
        }
        /// <summary>
        /// Retrieves units sold grouped by SectionID between two dates.
        /// </summary>
        public static async Task<Dictionary<string, int>> GetQuantityBySectionAsync(DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = AttributedSalesCte + @"
                SELECT SectionID, ROUND(SUM(Quantity)) FROM attributed GROUP BY SectionID;";

            using var command = new SqliteCommand(query, connection);
            AddDateRange(command, startDate, endDate);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string sectionId = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(sectionId))
                    results[sectionId] = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetDouble(1));
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

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = AttributedSalesCte + @"
                SELECT Day, SUM(Revenue)
                FROM attributed
                WHERE SectionID = @SectionID
                GROUP BY Day
                ORDER BY Day;";

            using var command = new SqliteCommand(query, connection);
            AddDateRange(command, startDate, endDate);
            command.Parameters.AddWithValue("@SectionID", sectionId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (DateTime.TryParse(reader.GetString(0), out var day))
                    results[day] = ReadDecimal(reader, 1);
            }

            return results;
        }

        /// <summary>
        /// Returns revenue and units per item number for a single section over the given period.
        /// </summary>
        public static async Task<Dictionary<string, (decimal revenue, int quantity)>> GetItemStatsForSectionAsync(
            string sectionId, DateTime startDate, DateTime endDate)
        {
            var results = new Dictionary<string, (decimal revenue, int quantity)>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            string query = AttributedSalesCte + @"
                SELECT ItemNumber, SUM(Revenue), ROUND(SUM(Quantity))
                FROM attributed
                WHERE SectionID = @SectionID
                GROUP BY ItemNumber;";

            using var command = new SqliteCommand(query, connection);
            AddDateRange(command, startDate, endDate);
            command.Parameters.AddWithValue("@SectionID", sectionId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string item = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(item))
                    results[item] = (ReadDecimal(reader, 1), reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetDouble(2)));
            }

            return results;
        }

        /// <summary>
        /// Total store revenue between two dates, inclusive.
        /// </summary>
        public static async Task<decimal> GetTotalRevenueAsync(DateTime start, DateTime end)
        {
            decimal total = 0;
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                await connection.OpenAsync();
                using var command = new SqliteCommand(
                    "SELECT IFNULL(SUM(Revenue),0) FROM SalesData WHERE date(SaleDate) BETWEEN @Start AND @End",
                    connection);
                AddDateRange(command, start, end);
                var result = await command.ExecuteScalarAsync();
                if (result != DBNull.Value && result != null) total = Convert.ToDecimal(result);
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetTotalRevenueAsync: {ex}"); }
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
                    "SELECT IFNULL(SUM(" + SignedQuantitySql + "),0) FROM SalesData sd WHERE date(sd.SaleDate) BETWEEN @Start AND @End",
                    connection);
                AddDateRange(command, start, end);
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
                string query = AttributedSalesCte + @"
                    SELECT COALESCE(s.Name, a.SectionID) AS SectionName,
                           SUM(a.Revenue) AS TotalRevenue
                    FROM attributed a
                    LEFT JOIN Sections s ON a.SectionID = s.SectionID
                    GROUP BY a.SectionID
                    ORDER BY TotalRevenue DESC
                    LIMIT @Count;";
                using var command = new SqliteCommand(query, connection);
                AddDateRange(command, start, end);
                command.Parameters.AddWithValue("@Count", count);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    results.Add((reader.GetString(0), ReadDecimal(reader, 1)));
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
                    WHERE date(SaleDate) BETWEEN @Start AND @End
                      AND Cat IS NOT NULL AND Cat != ''
                    GROUP BY Cat
                    ORDER BY TotalRevenue DESC;";
                using var command = new SqliteCommand(query, connection);
                AddDateRange(command, start, end);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    results.Add((reader.GetString(0), ReadDecimal(reader, 1)));
            }
            catch (Exception ex) { Debug.WriteLine($"[SalesDataServices] GetCategoryRevenueAsync: {ex}"); }
            return results;
        }
    }

}
