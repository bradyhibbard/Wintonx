using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winton.Services;
using Winton.Models;

namespace Winton.Helpers
{
    internal static class ChartDataFactory
    {
        // ---------------------------------------------------------
        // 1) DAILY TRENDLINE (Revenue by Day)
        // ---------------------------------------------------------
        public static async Task<SeriesCollection> CreateDailyTrendlineAsync(
            string sectionId,
            DateTime start,
            DateTime end)
        {
            var values = new ChartValues<decimal>();
            var labels = new List<string>();

            // Pull raw revenue from your SalesData table
            var allRevenue = await SalesDataServices.GetRevenueDataAsync(start, end);

            // Filter to this section
            decimal sectionRevenue = allRevenue.ContainsKey(sectionId)
                ? allRevenue[sectionId]
                : 0;

            // Build daily series (placeholder: real implementation needs daily breakdown)
            // For now we assume revenue evenly distributed OR stored by date.
            int totalDays = (end - start).Days + 1;
            decimal perDay = totalDays > 0 ? sectionRevenue / totalDays : 0;

            for (int i = 0; i < totalDays; i++)
            {
                DateTime day = start.AddDays(i);
                labels.Add(day.ToString("MM/dd"));
                values.Add(perDay);
            }

            return new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Revenue",
                    Values = values,
                    PointGeometrySize = 6
                }
            };
        }


        // ---------------------------------------------------------
        // 2) TOP PRODUCTS BAR CHART
        // ---------------------------------------------------------
        public static async Task<SeriesCollection> CreateTopProductsBarAsync(
            string sectionId,
            DateTime start,
            DateTime end)
        {
            var placements = await ProductPlacementServices.GetProductsBySectionAsync(sectionId);

            // Only current placements
            var active = placements
                .Where(p => p.DatePlaced <= end)
                .ToList();

            // Group by ItemNumber
            var grouped = active
                .GroupBy(p => p.ItemNumber)
                .Select(g => new
                {
                    Item = g.Key,
                    Revenue = g.Sum(x => x.Revenue)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToList();

            var barValues = new ChartValues<decimal>(grouped.Select(g => g.Revenue));

            var series = new ColumnSeries
            {
                Title = "Revenue",
                Values = barValues
            };

            return new SeriesCollection { series };
        }


        // ---------------------------------------------------------
        // 3) SECTION VS STORE AVERAGE
        // ---------------------------------------------------------
        public static async Task<SeriesCollection> CreateSectionVsStoreComparisonAsync(
            string sectionId,
            DateTime start,
            DateTime end)
        {
            var rev = await ProductPlacementServices.GetRevenueDataAsync(start, end);

            decimal sectionRevenue = rev.ContainsKey(sectionId) ? rev[sectionId] : 0;

            decimal storeAverage =
                rev.Values.Count > 0
                ? rev.Values.Average()
                : 0;

            return new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Section Revenue",
                    Values = new ChartValues<decimal> { sectionRevenue }
                },
                new ColumnSeries
                {
                    Title = "Store Avg Revenue",
                    Values = new ChartValues<decimal> { storeAverage }
                }
            };
        }
    }
}
