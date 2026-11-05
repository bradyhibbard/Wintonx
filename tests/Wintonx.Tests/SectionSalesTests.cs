using Winton.Services;
using Xunit;

namespace Winton.Tests
{
    public class SectionSalesTests : DatabaseTest
    {
        private static readonly DateTime Start = Jan(1);
        private static readonly DateTime End = Jan(31);

        [Fact]
        public async Task Sale_counts_while_the_product_is_in_the_section()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10));
            AddSale("A", Jan(15), 100m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.Equal(100m, revenue["S1"]);
        }

        [Fact]
        public async Task Sale_before_placement_does_not_count()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10));
            AddSale("A", Jan(9), 100m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.False(revenue.ContainsKey("S1"));
        }

        [Fact]
        public async Task Sale_after_removal_does_not_count()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10), removed: Jan(20));
            AddSale("A", Jan(21), 100m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.False(revenue.ContainsKey("S1"));
        }

        [Fact]
        public async Task Removing_a_product_keeps_its_past_sales_in_the_section()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10), removed: Jan(20));
            AddSale("A", Jan(15), 100m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.Equal(100m, revenue["S1"]);
        }

        [Fact]
        public async Task Sales_on_the_placed_and_removed_dates_count_whatever_the_time_of_day()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(10).AddHours(15), removed: Jan(20).AddHours(9));
            AddSale("A", Jan(10), 100m);
            AddSale("A", Jan(20), 50m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.Equal(150m, revenue["S1"]);
        }

        [Fact]
        public async Task Item_in_two_sections_splits_the_sale_evenly()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(1));
            AddSale("A", Jan(5), 100m, quantity: 10);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);
            var units = await SalesDataServices.GetQuantityBySectionAsync(Start, End);

            Assert.Equal(50m, revenue["S1"]);
            Assert.Equal(50m, revenue["S2"]);
            Assert.Equal(5, units["S1"]);
            Assert.Equal(5, units["S2"]);
        }

        [Fact]
        public async Task Split_only_applies_on_days_the_item_is_in_both_sections()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(10));
            AddSale("A", Jan(5), 100m);
            AddSale("A", Jan(15), 100m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.Equal(150m, revenue["S1"]);
            Assert.Equal(50m, revenue["S2"]);
        }

        [Fact]
        public async Task Duplicate_placement_in_one_section_does_not_double_count()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S1", Jan(1));
            AddSale("A", Jan(5), 100m, quantity: 2);

            Assert.Equal(100m, (await SalesDataServices.GetRevenueDataAsync(Start, End))["S1"]);
            Assert.Equal(2, (await SalesDataServices.GetQuantityBySectionAsync(Start, End))["S1"]);
        }

        [Fact]
        public async Task Section_totals_add_up_to_the_store_total()
        {
            AddSection("S1");
            AddSection("S2");
            AddSection("S3");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(1));
            AddPlacement("A", "S3", Jan(1));
            AddPlacement("B", "S1", Jan(1));
            AddSale("A", Jan(5), 100m);
            AddSale("B", Jan(5), 40m);

            var revenue = await SalesDataServices.GetRevenueDataAsync(Start, End);
            decimal store = await SalesDataServices.GetTotalRevenueAsync(Start, End);

            Assert.Equal((double)store, (double)revenue.Values.Sum(), precision: 2);
        }

        [Theory]
        [InlineData("30")]
        [InlineData("37")]
        public async Task Returns_reduce_revenue_and_units(string returnCode)
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddSale("A", Jan(5), 100m, quantity: 4);
            AddSale("A", Jan(6), -25m, quantity: 1, code: returnCode);

            Assert.Equal(75m, (await SalesDataServices.GetRevenueDataAsync(Start, End))["S1"]);
            Assert.Equal(3, (await SalesDataServices.GetQuantityBySectionAsync(Start, End))["S1"]);
            Assert.Equal(3, await SalesDataServices.GetTotalUnitsSoldAsync(Start, End));
        }

        [Fact]
        public async Task Returns_reduce_units_when_the_report_lists_them_as_negative()
        {
            AddSale("A", Jan(5), 100m, quantity: 4);
            AddSale("A", Jan(6), -25m, quantity: -1, code: "30");

            Assert.Equal(3, await SalesDataServices.GetTotalUnitsSoldAsync(Start, End));
        }

        [Fact]
        public async Task Date_ranges_include_their_first_and_last_day_only()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddSale("A", Jan(9), 1m);
            AddSale("A", Jan(10), 10m);
            AddSale("A", Jan(20), 100m);
            AddSale("A", Jan(21), 1000m);

            var from = Jan(10);
            var to = Jan(20);

            Assert.Equal(110m, (await SalesDataServices.GetRevenueDataAsync(from, to))["S1"]);
            Assert.Equal(110m, await SalesDataServices.GetRevenueBySectionAsync("S1", from, to));
            Assert.Equal(110m, await SalesDataServices.GetTotalRevenueAsync(from, to));
            Assert.Equal(2, await SalesDataServices.GetTotalUnitsSoldAsync(from, to));
            Assert.Equal(110m, (await SalesDataServices.GetCategoryRevenueAsync(from, to)).Single().revenue);
            Assert.Equal(110m, (await SalesDataServices.GetTopSectionsByRevenueAsync(from, to)).Single().revenue);
        }

        [Fact]
        public async Task Section_revenue_matches_the_heat_map()
        {
            AddSection("S1");
            AddSection("S2");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("A", "S2", Jan(1));
            AddSale("A", Jan(5), 100m);

            decimal drawer = await SalesDataServices.GetRevenueBySectionAsync("S1", Start, End);
            var heatMap = await SalesDataServices.GetRevenueDataAsync(Start, End);

            Assert.Equal(heatMap["S1"], drawer);
        }

        [Fact]
        public async Task Section_with_no_sales_has_zero_revenue()
        {
            AddSection("S1");

            Assert.Equal(0m, await SalesDataServices.GetRevenueBySectionAsync("S1", Start, End));
        }

        [Fact]
        public async Task Item_stats_are_per_item_within_the_section()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("B", "S1", Jan(1));
            AddSale("A", Jan(5), 100m, quantity: 2);
            AddSale("B", Jan(5), 30m, quantity: 3);

            var stats = await SalesDataServices.GetItemStatsForSectionAsync("S1", Start, End);

            Assert.Equal((100m, 2), stats["A"]);
            Assert.Equal((30m, 3), stats["B"]);
        }

        [Fact]
        public async Task Daily_revenue_is_grouped_by_day()
        {
            AddSection("S1");
            AddPlacement("A", "S1", Jan(1));
            AddSale("A", Jan(5), 100m);
            AddSale("A", Jan(5), 20m);
            AddSale("A", Jan(31), 7m);

            var daily = await SalesDataServices.GetDailyRevenueBySectionAsync("S1", Start, End);

            Assert.Equal(2, daily.Count);
            Assert.Equal(120m, daily[Jan(5)]);
            Assert.Equal(7m, daily[Jan(31)]);
        }

        [Fact]
        public async Task Top_sections_use_section_names_in_revenue_order_and_respect_the_limit()
        {
            AddSection("S1", "Front");
            AddSection("S2", "Back");
            AddSection("S3", "Side");
            AddPlacement("A", "S1", Jan(1));
            AddPlacement("B", "S2", Jan(1));
            AddPlacement("C", "S3", Jan(1));
            AddSale("A", Jan(5), 10m);
            AddSale("B", Jan(5), 30m);
            AddSale("C", Jan(5), 20m);

            var top = await SalesDataServices.GetTopSectionsByRevenueAsync(Start, End, count: 2);

            Assert.Equal(new[] { "Back", "Side" }, top.Select(t => t.name));
        }
    }
}
