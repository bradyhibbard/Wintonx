using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winton.Models
{
    public class ProductListItem
    {
        public int PlacementID { get; set; }
        public string ItemNumber { get; set; }

        public override string ToString()
        {
            return ItemNumber; // Makes ListBox display only the ItemNumber
        }
    }
}
