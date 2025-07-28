using System;
using System.Collections.Generic;
using System.Windows;

namespace Winton.Models
{
    public class Partition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<Point> Points { get; set; } = new List<Point>();
    }
}
