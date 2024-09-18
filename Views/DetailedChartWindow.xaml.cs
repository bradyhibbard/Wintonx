using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
