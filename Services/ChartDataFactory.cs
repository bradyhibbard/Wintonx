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
            var dailyMap = await SalesDataServices.GetDailyRevenueBySectionAsync(sectionId, start, end);

            var values = new ChartValues<decimal>();
            int totalDays = (end - start).Days + 1;

            for (int i = 0; i < totalDays; i++)
            {
                DateTime day = start.AddDays(i).Date;
                values.Add(dailyMap.TryGetValue(day, out decimal rev) ? rev : 0m);
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
            var itemRevenue = await SalesDataServices.GetRevenueByItemForSectionAsync(sectionId, start, end);

            var top5 = itemRevenue
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .ToList();

            return new SeriesCollection
            {
                new ColumnSeries
                {
                    Title  = "Revenue",
                    Values = new ChartValues<decimal>(top5.Select(kv => kv.Value))
                }
            };
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
