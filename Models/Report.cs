namespace Winton.Models
{
    public class Report
    {
        public int SalesDataID { get; set; }
        public System.DateTime ReportDate { get; set; }
        // Add other properties if needed
    }

    public class ReportDetail
    {
        public string ItemNumber { get; set; }
        public string VendorModel { get; set; }
        public string Description { get; set; }
        public decimal? Price { get; set; }
        public int? QuantitySold { get; set; }
        public decimal? Revenue { get; set; }
        public string Category { get; set; }
        public string Group { get; set; }
        public string TransactionCode { get; set; }

    }

}
