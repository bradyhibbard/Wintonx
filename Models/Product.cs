namespace Winton.Models
{
    public class Product
    {
        public int ProductID { get; set; }       // Unique identifier for the product.
        public string ItemNumber { get; set; }   // A unique number or code identifying the product.
        public string ItemName { get; set; }     // The name of the product.
        public string Vendor { get; set; }     // Category of the product (e.g., Furniture, Decor).
        public string Grp { get; set; }          // Group to which the product belongs.
        public string Cat { get; set; }
    }
}
