using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winton.Helpers;
using Winton.Models;
using Winton.Services;

namespace Winton.ViewModels
{
    public class SectionDetailViewModel
    {
        // --- Core Info ---
        public string SectionId { get; set; }
        public string SectionName { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // --- Summary Metrics ---
        public decimal TotalRevenue { get; set; }
        public int TotalQuantitySold { get; set; }

        public decimal AvgRevenuePerProduct =>
            ProductBreakdown.Count > 0
                ? Math.Round(TotalRevenue / ProductBreakdown.Count, 2)
                : 0;

        public string TopProductName { get; set; }
        public decimal TopProductRevenue { get; set; }

        // --- Product Breakdown ---
        public List<ProductBreakdownItem> ProductBreakdown { get; set; } = new();

        // --- Chart Data ---
        public SeriesCollection DailyTrendline { get; set; } = new();
        public SeriesCollection TopProductsBarChart { get; set; } = new();
        public SeriesCollection SectionVsStoreComparison { get; set; } = new();

        // --- Optional Notes ---
        public string Notes { get; set; }

        // -------------------------------------------------------
        // MAIN LOADER — called by LiveSalesFloor
        // -------------------------------------------------------
        public async Task LoadAsync()
        {
            // 1) Revenue for this section
            var revenueMap = await SalesDataServices.GetRevenueDataAsync(StartDate, EndDate);
            revenueMap.TryGetValue(SectionId, out decimal revenue);
            TotalRevenue = revenue;

            // 2) Load products (active + archived)
            var activeProducts = await ProductPlacementServices.GetProductsBySectionAsync(SectionId);
            var archivedProducts = await ProductPlacementServices.GetArchivedProductsBySectionAsync(SectionId);

            var allProducts = new List<ProductPlacement>();
            allProducts.AddRange(activeProducts);
            allProducts.AddRange(archivedProducts);

            TotalQuantitySold = allProducts.Sum(p => p.QuantitySold);

            // 3) Product Breakdown
            ProductBreakdown.Clear();
            foreach (var p in allProducts)
            {
                ProductBreakdown.Add(new ProductBreakdownItem
                {
                    ItemNumber = p.ItemNumber,
                    ProductName = p.ItemNumber,
                    QuantitySold = p.QuantitySold,
                    Revenue = p.Revenue,
                    IsActive = p.DateRemoved == null,
                    DatePlaced = p.DatePlaced
                });
            }

            // 4) Top Product
            var top = allProducts.OrderByDescending(p => p.Revenue).FirstOrDefault();
            if (top != null)
            {
                TopProductName = top.ItemNumber;
                TopProductRevenue = top.Revenue;
            }

            // 5) Load Charts (ChartDataFactory will be built next)
            DailyTrendline = await ChartDataFactory.CreateDailyTrendlineAsync(SectionId, StartDate, EndDate);
            TopProductsBarChart = await ChartDataFactory.CreateTopProductsBarAsync(SectionId, StartDate, EndDate);
            SectionVsStoreComparison = await ChartDataFactory.CreateSectionVsStoreComparisonAsync(SectionId, StartDate, EndDate);
        }
    }

    public class ProductBreakdownItem
    {
        public string ItemNumber { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public bool IsActive { get; set; }
        public DateTime DatePlaced { get; set; }
    }
}
