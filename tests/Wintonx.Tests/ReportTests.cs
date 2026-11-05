using Winton.Services;
using Xunit;

namespace Winton.Tests
{
    public class ReportTests : DatabaseTest
    {
        [Fact]
        public async Task Deleting_a_report_leaves_the_same_items_on_other_dates()
        {
            AddSale("A", Jan(5), 100m);
            AddSale("A", Jan(6), 50m);

            bool deleted = await SalesDataServices.DeleteReportAsync(Jan(5));

            Assert.True(deleted);
            Assert.Equal(1, CountSales());
            Assert.Equal(50m, await SalesDataServices.GetTotalRevenueAsync(Jan(6), Jan(6)));
        }

        [Fact]
        public async Task Deleting_a_date_removes_rows_saved_with_a_time_of_day()
        {
            AddSale("A", Jan(5).AddHours(14), 100m);

            Assert.True(await SalesDataServices.DeleteReportAsync(Jan(5)));
            Assert.Equal(0, CountSales());
        }

        [Fact]
        public async Task Deleting_a_date_with_no_data_reports_nothing_deleted()
        {
            Assert.False(await SalesDataServices.DeleteReportAsync(Jan(5)));
        }

        [Fact]
        public async Task Existing_reports_are_detected_by_date()
        {
            AddSale("A", Jan(5), 100m);

            Assert.True(await SalesDataServices.ReportExistsAsync(Jan(5)));
            Assert.False(await SalesDataServices.ReportExistsAsync(Jan(6)));
        }

        [Fact]
        public async Task Report_list_shows_each_recent_date_once_newest_first()
        {
            var today = DateTime.Today;
            AddSale("A", today.AddDays(-10), 1m);
            AddSale("B", today.AddDays(-10).AddHours(9), 1m);
            AddSale("A", today.AddDays(-5), 1m);
            AddSale("A", today.AddYears(-3), 1m);

            var dates = await SalesDataServices.GetReportsFromLastTwoYearsAsync();

            Assert.Equal(new[] { today.AddDays(-5), today.AddDays(-10) }, dates);
        }

        [Fact]
        public async Task Report_details_cover_only_the_chosen_date()
        {
            AddSale("A", Jan(5), 100m);
            AddSale("B", Jan(6), 50m);

            var details = await SalesDataServices.GetReportDetailsByDateAsync(Jan(5));

            Assert.Equal("A", Assert.Single(details).ItemNumber);
        }
    }
}
