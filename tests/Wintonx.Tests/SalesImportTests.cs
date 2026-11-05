using Winton.Services;
using Xunit;
using static Winton.Tests.ExcelFile;

namespace Winton.Tests
{
    public class SalesImportTests : DatabaseTest
    {
        private string Report(params object[][] rows) => Write(TempFolder, SalesHeaders, rows);

        [Fact]
        public async Task Imports_rows_and_calculates_revenue()
        {
            AddProduct("A");

            var result = await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", 25.5, 2, "00")), Jan(5));

            Assert.Equal(1, result.RowsImported);
            Assert.Equal(0, result.ItemsNotInProductList);
            Assert.Equal(51m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
            Assert.Equal(2, await SalesDataServices.GetTotalUnitsSoldAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Columns_are_found_by_header_in_any_order()
        {
            string path = Write(TempFolder,
                new[] { "Tr", "Extra", "Qty Shp", "Cse Sell Price", "Vendor Model" },
                new object[] { "00", "ignored", 2, 10, "A" });

            await ImportServices.ImportSalesReportWithMappingAsync(path, Jan(5));

            Assert.Equal(20m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Theory]
        [InlineData(0, "00")]
        [InlineData(5, "05")]
        [InlineData(7, "07")]
        [InlineData(30, "30")]
        public async Task Transaction_codes_stored_as_numbers_are_recognized(int cellValue, string expectedCode)
        {
            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", 10, 1, cellValue)), Jan(5));

            Assert.Equal(expectedCode, Scalar<string>("SELECT TransactionCode FROM SalesData"));
        }

        [Fact]
        public async Task Item_numbers_stored_as_numbers_are_read_as_text()
        {
            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale(12345, 10, 1, "00")), Jan(5));

            Assert.Equal("12345", Scalar<string>("SELECT ItemNumber FROM SalesData"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public async Task Returns_are_negative_revenue_whichever_sign_the_report_uses(int quantity)
        {
            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", 10, quantity, "30")), Jan(5));

            Assert.Equal(-10m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Bad_rows_are_skipped_and_counted_by_reason()
        {
            var result = await ImportServices.ImportSalesReportWithMappingAsync(Report(
                Sale("A", 10, 1, "00"),
                Sale(null, 10, 1, "00"),
                Sale("B", "not a price", 1, "00"),
                Sale("C", 10, "not a number", "00"),
                Sale("D", 10, 1.5, "00"),
                Sale("E", 10, 1, "99"),
                Sale("F", 10, 1, null)), Jan(5));

            Assert.Equal(1, result.RowsImported);
            Assert.Equal(4, result.RowsSkippedInvalid);
            Assert.Equal(2, result.RowsSkippedUnknownCode);
            Assert.Equal(1, CountSales());
        }

        [Fact]
        public async Task Blank_rows_are_ignored_without_being_counted()
        {
            var result = await ImportServices.ImportSalesReportWithMappingAsync(Report(
                Sale("A", 10, 1, "00"),
                new object[] { null, null, null, null, null, null, null }), Jan(5));

            Assert.Equal(1, result.RowsImported);
            Assert.Equal(0, result.RowsSkippedInvalid);
            Assert.Equal(0, result.RowsSkippedUnknownCode);
        }

        [Fact]
        public async Task Prices_and_quantities_stored_as_formatted_text_are_read()
        {
            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", "$1,200.50", "1,000", "00")), Jan(5));

            Assert.Equal(1200500m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Formula_cells_use_their_calculated_result()
        {
            await ImportServices.ImportSalesReportWithMappingAsync(
                Report(Sale("A", 10, new Formula("1+2", 3), "00")), Jan(5));

            Assert.Equal(30m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Older_xls_files_can_be_imported()
        {
            string path = Write(TempFolder, ".xls", SalesHeaders, Sale("A", 10, 2, "00"));

            await ImportServices.ImportSalesReportWithMappingAsync(path, Jan(5));

            Assert.Equal(20m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Missing_columns_fail_and_name_what_is_missing()
        {
            string path = Write(TempFolder, new[] { "Vendor Model", "Cse Sell Price" }, new object[] { "A", 10 });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ImportServices.ImportSalesReportWithMappingAsync(path, Jan(5)));

            Assert.Contains("Qty Shp", error.Message);
            Assert.Contains("Tr", error.Message);
            Assert.Equal(0, CountSales());
        }

        [Fact]
        public async Task A_file_with_no_usable_rows_fails()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", 10, 1, "99")), Jan(5)));

            Assert.Equal(0, CountSales());
        }

        [Fact]
        public async Task A_file_that_is_not_a_spreadsheet_fails()
        {
            string path = Path.Combine(TempFolder, "report.xlsx");
            File.WriteAllText(path, "this is not a spreadsheet");

            await Assert.ThrowsAnyAsync<Exception>(() => ImportServices.ImportSalesReportWithMappingAsync(path, Jan(5)));

            Assert.Equal(0, CountSales());
        }

        [Fact]
        public async Task Replacing_swaps_that_date_and_leaves_other_dates_alone()
        {
            AddSale("OLD", Jan(5), 100m);
            AddSale("OTHER", Jan(6), 7m);

            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("NEW", 10, 1, "00")), Jan(5), replaceExisting: true);

            Assert.Equal(10m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
            Assert.Equal(7m, await SalesDataServices.GetTotalRevenueAsync(Jan(6), Jan(6)));
        }

        [Fact]
        public async Task Adding_keeps_existing_data_for_that_date()
        {
            AddSale("OLD", Jan(5), 100m);

            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("NEW", 10, 1, "00")), Jan(5));

            Assert.Equal(110m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task A_failed_replace_keeps_the_existing_report()
        {
            AddSale("OLD", Jan(5), 100m);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("NEW", 10, 1, "99")), Jan(5), replaceExisting: true));

            Assert.Equal(100m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }

        [Fact]
        public async Task Items_missing_from_the_product_list_are_reported()
        {
            AddProduct("KNOWN");

            var result = await ImportServices.ImportSalesReportWithMappingAsync(Report(
                Sale("KNOWN", 10, 1, "00"),
                Sale("UNKNOWN", 10, 1, "00"),
                Sale("UNKNOWN", 10, 1, "05")), Jan(5));

            Assert.Equal(1, result.ItemsNotInProductList);
        }

        [Fact]
        public async Task A_report_time_of_day_is_ignored()
        {
            await ImportServices.ImportSalesReportWithMappingAsync(Report(Sale("A", 10, 1, "00")), Jan(5).AddHours(14));

            Assert.True(await SalesDataServices.ReportExistsAsync(Jan(5)));
            Assert.Equal(10m, await SalesDataServices.GetTotalRevenueAsync(Jan(5), Jan(5)));
        }
    }
}
