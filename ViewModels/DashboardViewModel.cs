using LiveCharts;
using System.ComponentModel;
using System.Diagnostics;
using Winton.Services;

public class DashboardViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private decimal _totalRevenue;
    private decimal _monthlyRevenue;
    private int _totalCustomers;
    private SeriesCollection _currentYearRevenue = new();
    private SeriesCollection _previousYearRevenue = new();
    private SeriesCollection _categorySales = new();
    private string[] _months = Array.Empty<string>();

    protected bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }



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

    public Func<double, string> Formatter { get; } = value => value.ToString("C");

    public DashboardViewModel()
    {
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var revenueTask = SalesDataServices.GetTotalRevenueForCurrentYearAsync();
            var monthlyTask = SalesDataServices.GetTotalRevenueForCurrentMonthAsync();
            var customersTask = SalesDataServices.GetTotalCustomersAsync();

            await Task.WhenAll(revenueTask, monthlyTask, customersTask);

            TotalRevenue = await revenueTask;
            MonthlyRevenue = await monthlyTask;
            TotalCustomers = await customersTask;

            Months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            CurrentYearRevenue = await SalesDataServices.GetMonthlyRevenueByYearAsync(DateTime.Now.Year);
            PreviousYearRevenue = await SalesDataServices.GetMonthlyRevenueByYearAsync(DateTime.Now.Year - 1);
            CategorySales = await SalesDataServices.GetCategorySalesDataAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading data: {ex.Message}");
        }
    }
}
