using LiveCharts;
using System;

namespace Winton.ViewModels
{
    public class DetailedChartViewModel
    {
        public SeriesCollection CurrentYearRevenue { get; set; }
        public SeriesCollection PreviousYearRevenue { get; set; }
        public string[] Months { get; set; }
        public Func<double, string> Formatter { get; set; }

        public DetailedChartViewModel(SeriesCollection currentYearRevenue, SeriesCollection previousYearRevenue, string[] months, Func<double, string> formatter)
        {
            CurrentYearRevenue = currentYearRevenue;
            PreviousYearRevenue = previousYearRevenue;
            Months = months;
            Formatter = formatter;
        }
    }
}
