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
        public string[] DailyLabels { get; set; } = Array.Empty<string>();
        public string[] TopProductLabels { get; set; } = Array.Empty<string>();

        // --- Optional Notes ---
        public string Notes { get; set; }

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
