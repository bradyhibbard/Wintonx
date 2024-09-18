using System;
using Winton.Models;

namespace Winton.Models
{
    public class ProductPlacement
    {
        public int PlacementID { get; set; }     // Unique identifier for each placement entry.
        public int ProductID { get; set; }       // Foreign key linked to the Product.
        public string ItemNumber { get; set; }
        public string SectionID { get; set; }    // Foreign key linked to the Section where the product is placed.
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public DateTime DatePlaced { get; set; } // The date when the product was placed in the section.
        public DateTime? DateRemoved { get; set; } // The date when the product was removed (nullable for current placements).
        public string Grp { get; set; }          // Group to which the product belongs.
        public string Cat { get; set; }

        public Product Product { get; set; }     // Navigation property to the Product.
        public Section Section { get; set; }     // Navigation property to the Section.
    }

}
