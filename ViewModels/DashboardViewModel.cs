using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Winton.Services;

namespace Winton.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ─── KPI values ───────────────────────────────────────────────────────
        private string _annualRevenue   = "—";
        private string _monthlyRevenue  = "—";
        private string _unitsThisMonth  = "—";
        private string _activeSections  = "—";

        public string AnnualRevenue  { get => _annualRevenue;  set { _annualRevenue  = value; OnPropertyChanged(); } }
        public string MonthlyRevenue { get => _monthlyRevenue; set { _monthlyRevenue = value; OnPropertyChanged(); } }
        public string UnitsThisMonth { get => _unitsThisMonth; set { _unitsThisMonth = value; OnPropertyChanged(); } }
        public string ActiveSections { get => _activeSections; set { _activeSections = value; OnPropertyChanged(); } }

        // ─── Delta badges ─────────────────────────────────────────────────────
        private string _annualDeltaText  = "";
        private string _monthDeltaText   = "";
        private string _unitsDeltaText   = "";
        private Brush  _annualDeltaBrush = Brushes.Gray;
        private Brush  _monthDeltaBrush  = Brushes.Gray;
        private Brush  _unitsDeltaBrush  = Brushes.Gray;

        public string AnnualDeltaText  { get => _annualDeltaText;  set { _annualDeltaText  = value; OnPropertyChanged(); } }
        public string MonthDeltaText   { get => _monthDeltaText;   set { _monthDeltaText   = value; OnPropertyChanged(); } }
        public string UnitsDeltaText   { get => _unitsDeltaText;   set { _unitsDeltaText   = value; OnPropertyChanged(); } }
        public Brush  AnnualDeltaBrush { get => _annualDeltaBrush; set { _annualDeltaBrush = value; OnPropertyChanged(); } }
        public Brush  MonthDeltaBrush  { get => _monthDeltaBrush;  set { _monthDeltaBrush  = value; OnPropertyChanged(); } }
        public Brush  UnitsDeltaBrush  { get => _unitsDeltaBrush;  set { _unitsDeltaBrush  = value; OnPropertyChanged(); } }

        // ─── Header ───────────────────────────────────────────────────────────
        private string _lastReportDate = "Loading…";
        public string LastReportDate { get => _lastReportDate; set { _lastReportDate = value; OnPropertyChanged(); } }

        // ─── Charts ───────────────────────────────────────────────────────────
        private SeriesCollection _revenueComparison = new();
        private SeriesCollection _topSections       = new();
        private SeriesCollection _categoryRevenue   = new();
        private string[]         _months            = Array.Empty<string>();
        private string[]         _topSectionLabels  = Array.Empty<string>();
        private string[]         _categoryLabels    = Array.Empty<string>();

        public SeriesCollection RevenueComparison { get => _revenueComparison; set { _revenueComparison = value; OnPropertyChanged(); } }
        public SeriesCollection TopSections       { get => _topSections;       set { _topSections       = value; OnPropertyChanged(); } }
        public SeriesCollection CategoryRevenue   { get => _categoryRevenue;   set { _categoryRevenue   = value; OnPropertyChanged(); } }
        public string[]         Months            { get => _months;            set { _months            = value; OnPropertyChanged(); } }
        public string[]         TopSectionLabels  { get => _topSectionLabels;  set { _topSectionLabels  = value; OnPropertyChanged(); } }
        public string[]         CategoryLabels    { get => _categoryLabels;    set { _categoryLabels    = value; OnPropertyChanged(); } }

        public Func<double, string> Formatter { get; } = v => ((decimal)v).ToString("C0");

        // Kept for DetailedChartWindow compatibility
        public SeriesCollection CurrentYearRevenue  { get; private set; } = new();
        public SeriesCollection PreviousYearRevenue { get; private set; } = new();

        // ─── Constructor ─────────────────────────────────────────────────────
        public DashboardViewModel() => _ = LoadDataAsync();

        // ─── Data loading ─────────────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            try
            {
                int    year      = DateTime.Now.Year;
                int    month     = DateTime.Now.Month;
                var    monthStart = new DateTime(year, month, 1);
                var    monthEnd   = DateTime.Today;
                var    prevMonthStart = new DateTime(month == 1 ? year - 1 : year, month == 1 ? 12 : month - 1, 1);
                var    prevMonthEnd   = prevMonthStart.AddMonths(1).AddDays(-1);

                // ── Kick off all independent queries in parallel ──────────────
                var tAnnual      = SalesDataServices.GetTotalRevenueForYearAsync(year);
                var tPrevAnnual  = SalesDataServices.GetTotalRevenueForYearAsync(year - 1);
                var tMonth       = SalesDataServices.GetTotalRevenueForMonthAsync(year, month);
                var tPrevMonth   = SalesDataServices.GetTotalRevenueForMonthAsync(
                                       month == 1 ? year - 1 : year,
                                       month == 1 ? 12 : month - 1);
                var tUnits       = SalesDataServices.GetTotalUnitsSoldAsync(monthStart, monthEnd);
                var tPrevUnits   = SalesDataServices.GetTotalUnitsSoldAsync(prevMonthStart, prevMonthEnd);
                var tLastReport  = SalesDataServices.GetLastReportDateAsync();
                var tTopSections = SalesDataServices.GetTopSectionsByRevenueAsync(monthStart, monthEnd);
                var tCategories  = SalesDataServices.GetCategoryRevenueAsync(monthStart, monthEnd);
                var tCurYear     = SalesDataServices.GetMonthlyRevenueByYearAsync(year);
                var tPrevYear    = SalesDataServices.GetMonthlyRevenueByYearAsync(year - 1);
                var tRevMap      = SalesDataServices.GetRevenueDataAsync(monthStart, monthEnd);

                await Task.WhenAll(tAnnual, tPrevAnnual, tMonth, tPrevMonth,
                                   tUnits, tPrevUnits, tLastReport,
                                   tTopSections, tCategories,
                                   tCurYear, tPrevYear, tRevMap);

                // ── KPI cards ────────────────────────────────────────────────
                decimal annual     = tAnnual.Result;
                decimal prevAnnual = tPrevAnnual.Result;
                decimal curMonth   = tMonth.Result;
                decimal prevMonth  = tPrevMonth.Result;
                int     units      = tUnits.Result;
                int     prevUnits  = tPrevUnits.Result;

                AnnualRevenue  = annual.ToString("C0");
                MonthlyRevenue = curMonth.ToString("C0");
                UnitsThisMonth = units.ToString("N0");

                var revMap = tRevMap.Result;
                ActiveSections = revMap.Count(kv => kv.Value > 0).ToString();

                // ── Delta badges ─────────────────────────────────────────────
                (AnnualDeltaText,  AnnualDeltaBrush) = BuildDelta(annual,    prevAnnual, "vs last year");
                (MonthDeltaText,   MonthDeltaBrush)  = BuildDelta(curMonth,  prevMonth,  "vs last month");
                (UnitsDeltaText,   UnitsDeltaBrush)  = BuildDelta(units,     prevUnits,  "vs last month");

                // ── Last report date ─────────────────────────────────────────
                var lastDate = tLastReport.Result;
                LastReportDate = lastDate.HasValue
                    ? $"Data as of {lastDate.Value:MMM dd, yyyy}"
                    : "No reports uploaded yet";

                // ── Revenue comparison line chart ─────────────────────────────
                CurrentYearRevenue  = tCurYear.Result;
                PreviousYearRevenue = tPrevYear.Result;
                Months = new[] { "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };

                var combined = new SeriesCollection();
                foreach (var s in CurrentYearRevenue)  combined.Add(s);
                foreach (var s in PreviousYearRevenue) combined.Add(s);
                RevenueComparison = combined;

                // ── Top sections horizontal bar ───────────────────────────────
                var topList = tTopSections.Result;
                if (topList.Any())
                {
                    TopSectionLabels = topList.Select(t => t.name).ToArray();
                    TopSections = new SeriesCollection
                    {
                        new RowSeries
                        {
                            Title  = "Revenue",
                            Values = new ChartValues<decimal>(topList.Select(t => t.revenue)),
                            DataLabels = false
                        }
                    };
                }

                // ── Category revenue horizontal bar ───────────────────────────
                var catList = tCategories.Result;
                if (catList.Any())
                {
                    CategoryLabels  = catList.Select(c => c.category).ToArray();
                    CategoryRevenue = new SeriesCollection
                    {
                        new RowSeries
                        {
                            Title  = "Revenue",
                            Values = new ChartValues<decimal>(catList.Select(c => c.revenue)),
                            DataLabels = false
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardViewModel] LoadDataAsync: {ex}");
            }
        }

        // ─── Delta helper ─────────────────────────────────────────────────────
        private static (string text, Brush brush) BuildDelta(decimal current, decimal previous, string label)
        {
            if (previous == 0)
                return ("— no prior data", Brushes.Gray);

            double pct = (double)((current - previous) / Math.Abs(previous) * 100);
            bool positive = pct >= 0;
            string arrow = positive ? "▲" : "▼";
            Brush brush   = positive
                ? new SolidColorBrush(Color.FromRgb(52, 199, 89))
                : new SolidColorBrush(Color.FromRgb(255, 69, 58));
            return ($"{arrow} {Math.Abs(pct):F0}% {label}", brush);
        }

        private static (string text, Brush brush) BuildDelta(int current, int previous, string label)
            => BuildDelta((decimal)current, (decimal)previous, label);
    }
}
