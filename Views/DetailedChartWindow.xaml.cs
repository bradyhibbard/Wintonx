using LiveCharts;
using System.Windows;
using Winton.ViewModels;

namespace Winton.Views
{
    /// <summary>
    /// Interaction logic for DetailedChartWindow.xaml
    /// </summary>
    public partial class DetailedChartWindow : Window
    {
        public DetailedChartWindow(SeriesCollection currentYearRevenue, SeriesCollection previousYearRevenue, string[] months, Func<double, string> formatter)
        {
            InitializeComponent();
            DataContext = new DetailedChartViewModel(currentYearRevenue, previousYearRevenue, months, formatter);
        }
    }
}
