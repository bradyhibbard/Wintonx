using System;

namespace Windton.Models
{
    internal class Archive
    {
        public int ProductID { get; set; }
        public string SectionID { get; set; }
        public int QuantitySold { get; set; }
        public Decimal Revenue { get; set; }
        public DateTime DateRemoved { get; set; }
        public string RemovalNotes { get; set; }
    }
}
