using System;
using Winton.Models;

namespace Winton.Models
{
    public class SalesData
    {
        public int SalesDataID { get; set; }        // Unique identifier for the sales data entry.
        public int PlacementID { get; set; }        // Links sales data to a specific product placement.
        public string ItemNumber { get; set; }      // Product Number
        public string VendorModel { get; set; }     // Vendor Model
        public string Description { get; set; }     // All Language Descriptions
        public decimal Price { get; set; }          // Cse Sell Price
        public int QuantitySold { get; set; }       // Quantity of the product sold (Qty Ord)
        public decimal Revenue { get; set; }        // Total revenue generated from the sales (Price * QuantitySold)
        public string Category { get; set; }        // Category of the product.
        public string Group { get; set; }           // Group of the product within the category.
        public string TransactionCode { get; set; } // Order Trns Cd
        public DateTime ReportDate { get; set; }    // The date when the sales data was reported.

        public ProductPlacement ProductPlacement { get; set; } // Navigation property to the ProductPlacement.
    }

}
