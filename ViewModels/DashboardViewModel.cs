using LiveCharts;
using LiveCharts.Wpf;
using Winton.Data;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


namespace Winton.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private decimal _totalRevenue;
        private decimal _monthlyRevenue;
        private string _topPerformingSection;
        private int _totalItemsSold;
        private int _totalCustomers;
        private SeriesCollection _currentYearRevenue;
        private SeriesCollection _previousYearRevenue;
        private SeriesCollection _categorySales;
        private string[] _months;

        public event PropertyChangedEventHandler? PropertyChanged;

        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set => SetField(ref _totalRevenue, value);
        }

        public decimal MonthlyRevenue
        {
            get => _monthlyRevenue;
            set => SetField(ref _monthlyRevenue, value);
        }

        public string TopPerformingSection
        {
            get => _topPerformingSection;
            set => SetField(ref _topPerformingSection, value);
        }

        public int TotalItemsSold
        {
            get => _totalItemsSold;
            set => SetField(ref _totalItemsSold, value);
        }

        public int TotalCustomers
        {
            get => _totalCustomers;
            set => SetField(ref _totalCustomers, value);
        }

        public SeriesCollection CurrentYearRevenue
        {
            get => _currentYearRevenue;
            set => SetField(ref _currentYearRevenue, value);
        }

        public SeriesCollection PreviousYearRevenue
        {
            get => _previousYearRevenue;
            set => SetField(ref _previousYearRevenue, value);
        }

        public SeriesCollection CategorySales
        {
            get => _categorySales;
            set => SetField(ref _categorySales, value);
        }

        public string[] Months
        {
            get => _months;
            set => SetField(ref _months, value);
        }

        public Func<double, string> Formatter { get; set; }

        public DashboardViewModel()
        {
            // Initialize non-nullable fields
            _topPerformingSection = string.Empty;
            _currentYearRevenue = new SeriesCollection();
            _previousYearRevenue = new SeriesCollection();
            _categorySales = new SeriesCollection();
            _months = Array.Empty<string>(); // or you can initialize with a fixed array of months
            PropertyChanged = null;
            Formatter = value => value.ToString("C");

            // Start loading the data asynchronously
            _ = LoadDataAsync();
        }


        private async Task LoadDataAsync()
        {
            try
            {
                // Load summary data
                TotalRevenue = await DatabaseHelper.GetTotalRevenueForCurrentYearAsync();
                MonthlyRevenue = await DatabaseHelper.GetTotalRevenueForCurrentMonthAsync();
                // Uncomment once the following methods are ready in DatabaseHelper
                // TopPerformingSection = await DatabaseHelper.GetTopPerformingSectionAsync();
                // TotalItemsSold = await DatabaseHelper.GetTotalItemsSoldForCurrentYearAsync();
                // TotalCustomers = await DatabaseHelper.GetTotalCustomersForCurrentYearAsync();

                // Load chart data
                CurrentYearRevenue = await GetMonthlyRevenueForCurrentYear();
                PreviousYearRevenue = await GetMonthlyRevenueForPreviousYear();
                CategorySales = await GetCategorySalesData();

                Months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

                Formatter = value => value.ToString("C");

                // Debug information
                Debug.WriteLine("TotalRevenue: " + TotalRevenue);
                Debug.WriteLine("MonthlyRevenue: " + MonthlyRevenue);
                if (CurrentYearRevenue.Count > 0)
                {
                    Debug.WriteLine("CurrentYearRevenue: " + CurrentYearRevenue[0].Values);
                }
                if (PreviousYearRevenue.Count > 0)
                {
                    Debug.WriteLine("PreviousYearRevenue: " + PreviousYearRevenue[0].Values);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading dashboard data: {ex.Message}");
            }
        }

        private async Task<SeriesCollection> GetMonthlyRevenueForCurrentYear()
        {
            var values = new ChartValues<decimal>();
            for (int month = 1; month <= 12; month++)
            {
                decimal revenue = await DatabaseHelper.GetRevenueForMonthAsync(DateTime.Now.Year, month);
                values.Add(revenue);
            }

            return new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Current Year",
                    Values = values
                }
            };
        }

        private async Task<SeriesCollection> GetMonthlyRevenueForPreviousYear()
        {
            var values = new ChartValues<decimal>();
            for (int month = 1; month <= 12; month++)
            {
                decimal revenue = await DatabaseHelper.GetRevenueForMonthAsync(DateTime.Now.Year - 1, month);
                values.Add(revenue);
            }

            return new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Previous Year",
                    Values = values
                }
            };
        }

        private async Task<SeriesCollection> GetCategorySalesData()
        {
            var categorySales = await DatabaseHelper.GetSalesQuantityByCategory(DateTime.Now.Year, DateTime.Now.Month);
            var seriesCollection = new SeriesCollection();

            foreach (var category in categorySales)
            {
                seriesCollection.Add(new PieSeries
                {
                    Title = category.Key,
                    Values = new ChartValues<int> { category.Value },
                    DataLabels = true
                });
            }

            return seriesCollection;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
