using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Winton.Data;
using Winton.Models;

namespace Winton.Views
{

    public partial class SalesFloorControl : UserControl, INotifyPropertyChanged
    {

        private string _tooltipContent;
        private ProductListControl _productListControl;
        private Point _lastMousePos;
        private bool _isPanning;

        private DateTime _startDate;
        private DateTime _endDate;

        // Declare the PropertyChanged event
        public event PropertyChangedEventHandler PropertyChanged;

        public string TooltipContent
        {
            get => _tooltipContent;
            set
            {
                if (_tooltipContent != value)
                {
                    _tooltipContent = value;
                    OnPropertyChanged(nameof(TooltipContent));
                }
            }
        }

        // Method to raise the PropertyChanged event
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SalesFloorControl()
        {
            InitializeComponent();

            DataContext = this;

            InitializeFilterControlEvents();
            InitializeZoomCanvasEvents();
        }

        private void InitializeFilterControlEvents()
        {
            //FilterControl.CancelClicked += (s, e) => ToggleFilterPanelVisibility();
            //FilterControl.SaveClicked += (s, e) => ApplyFilters(e);
            //FilterControl.ClearClicked += (s, e) => ResetDisplay();
            //FilterControl.RevenueFilterChanged += (s, e) => HandleRevenueFilterChanged(e);
        }

        private void InitializeZoomCanvasEvents()
        {
            ZoomCanvas.MouseWheel += ZoomCanvas_MouseWheel;
            ZoomCanvas.MouseDown += ZoomCanvas_MouseDown;
            ZoomCanvas.MouseMove += ZoomCanvas_MouseMove;
            ZoomCanvas.MouseUp += ZoomCanvas_MouseUp;
        }

        private void ZoomCanvas_MouseWheel(object sender, MouseWheelEventArgs e) =>
            Zoom(e.Delta > 0 ? 1.1 : 0.9, e.GetPosition(ZoomCanvas));

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) =>
            Zoom(1.1, new Point(ZoomCanvas.ActualWidth / 2, ZoomCanvas.ActualHeight / 2));

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) =>
            Zoom(0.9, new Point(ZoomCanvas.ActualWidth / 2, ZoomCanvas.ActualHeight / 2));

        private void Zoom(double zoomFactor, Point position)
        {
            var transformGroup = (TransformGroup)ZoomCanvas.RenderTransform;
            var scaleTransform = (ScaleTransform)transformGroup.Children[0];
            var translateTransform = (TranslateTransform)transformGroup.Children[1];

            var absoluteX = position.X * scaleTransform.ScaleX + translateTransform.X;
            var absoluteY = position.Y * scaleTransform.ScaleY + translateTransform.Y;

            scaleTransform.ScaleX *= zoomFactor;
            scaleTransform.ScaleY *= zoomFactor;

            translateTransform.X = absoluteX - position.X * scaleTransform.ScaleX;
            translateTransform.Y = absoluteY - position.Y * scaleTransform.ScaleY;
        }

        private void ZoomCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _lastMousePos = e.GetPosition(this);
                _isPanning = true;
                ZoomCanvas.CaptureMouse();
            }
        }

        private void ZoomCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
            {
                var currentMousePos = e.GetPosition(this);
                var transformGroup = (TransformGroup)ZoomCanvas.RenderTransform;
                var translateTransform = (TranslateTransform)transformGroup.Children[1];

                translateTransform.X += currentMousePos.X - _lastMousePos.X;
                translateTransform.Y += currentMousePos.Y - _lastMousePos.Y;

                _lastMousePos = currentMousePos;
            }
        }

        private void ZoomCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ZoomCanvas.ReleaseMouseCapture();
            }
        }

        private void HandleRevenueFilterChanged(object filterData)
        {
            if (filterData is RevenueFilterData data)
            {
                DisplayRevenueForSections(data.StartDate, data.EndDate);
            }
            else
            {
                ClearRevenueDisplay();
            }
        }

        private void ResetDisplay()
        {
            foreach (var button in ZoomCanvas.Children.OfType<Button>())
            {
                button.Background = new SolidColorBrush(Colors.Transparent); // Reset to default color
            }
        }

        private void ApplyFilters(List<string> filters)
        {
            var selectedGrp = filters.FirstOrDefault(f => f.StartsWith("Grp:"))?.Replace("Grp:", "").Trim();
            var selectedCat = filters.FirstOrDefault(f => f.StartsWith("Cat:"))?.Replace("Cat:", "").Trim();

            var startDateString = filters.FirstOrDefault(f => f.StartsWith("StartDate:"))?.Replace("StartDate:", "").Trim();
            var endDateString = filters.FirstOrDefault(f => f.StartsWith("EndDate:"))?.Replace("EndDate:", "").Trim();

            DateTime.TryParse(startDateString, out _startDate);
            DateTime.TryParse(endDateString, out _endDate);

            var salesData = DatabaseHelper.GetSalesDataByFilters(_startDate, _endDate, selectedGrp, selectedCat);
            var productPlacements = DatabaseHelper.GetProductPlacementsByFilters(selectedGrp, selectedCat);

            DisplaySalesData(productPlacements, salesData);
        }

        private void DisplaySalesData(List<ProductPlacement> productPlacements, List<SalesData> salesData)
        {
            foreach (var placement in productPlacements)
            {
                var button = ZoomCanvas.Children.OfType<Button>()
                    .FirstOrDefault(b => b.Tag?.ToString() == placement.SectionID);

                if (button != null)
                {
                    var data = salesData.FirstOrDefault(sd => sd.PlacementID == placement.PlacementID);
                    SetButtonDisplay(button);
                }
            }
        }
        private async void SetButtonDisplay(Button button)
        {
            string sectionID = button.Tag.ToString();
            decimal revenue = await GetRevenueForSection(sectionID);

            if (revenue >= 0)
            {
                Console.WriteLine($"Section ID: {sectionID}, Revenue: {revenue}");
                button.Background = GetRevenueColor(revenue, _startDate, _endDate);
                button.Content = null; // Remove "+" if there's revenue
            }
            else
            {
                Console.WriteLine($"Section ID: {sectionID}, No revenue data available.");
                button.Background = new SolidColorBrush(Colors.Gray); // Default color for sections with no data
                button.Content = "+"; // Display "+" if there's no furniture in the section
            }
        }



        private async Task<decimal> GetRevenueForSection(string sectionID)
        {
            // Assuming GetRevenueData returns a dictionary
            var revenueData = await DatabaseHelper.GetRevenueDataAsync(_startDate, _endDate); // Ensure this method is async or properly handles asynchronous calls
            return revenueData.TryGetValue(sectionID, out decimal revenue) ? revenue : 0;
        }

        private Brush GetRevenueColor(decimal revenue, DateTime startDate, DateTime endDate)
        {
            // Example of dynamic threshold adjustment based on dates if needed
            decimal minRevenue = 1;  // Minimum revenue threshold for coloring
            decimal maxRevenue = DatabaseHelper.GetMaxRevenue(startDate, endDate);  // Dynamically fetch max revenue based on dates

            // Color thresholds could be adjusted dynamically based on some business logic
            decimal lowThreshold = minRevenue + (maxRevenue - minRevenue) * 0.45m;
            decimal highThreshold = minRevenue + (maxRevenue - minRevenue) * 0.90m;

            if (revenue == 0) return new SolidColorBrush(Colors.Blue);  // No revenue
            if (revenue <= lowThreshold) return new SolidColorBrush(Colors.Green);  // Low revenue
            if (revenue <= highThreshold) return new SolidColorBrush(Colors.Yellow);  // Medium revenue
            return new SolidColorBrush(Colors.Red);  // High revenue
        }

        private void ToggleFilterVisibility(object sender, RoutedEventArgs e)
        {
            FilterControl.Visibility = FilterControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleFilterPanelVisibility()
        {
            FilterControl.Visibility = FilterControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            ZoomCanvas.Visibility = ZoomCanvas.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                string sectionID = button.Tag.ToString();
                var sectionDetailsWindow = new SectionDetails(sectionID);
                sectionDetailsWindow.ShowDialog();
            }
        }

        private List<string> GetTopSectionsByRevenue(Dictionary<string, decimal> revenueData)
        {
            decimal totalRevenue = revenueData.Values.Sum();
            decimal cumulativeRevenue = 0;
            var topSections = new List<string>();

            foreach (var section in revenueData.OrderByDescending(kv => kv.Value))
            {
                cumulativeRevenue += section.Value;
                topSections.Add(section.Key);

                if (cumulativeRevenue >= 0.8m * totalRevenue)
                {
                    break;
                }
            }

            return topSections;
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;

            // Set the content of MainContent to a new SalesFloorControl
            if (mainWindow != null)
            {
                mainWindow.MainContent.Content = new SalesFloorControl();
            }
        }


        private async void DisplayRevenueForSections(DateTime startDate, DateTime endDate)
        {
            try
            {
                var revenueData = await DatabaseHelper.GetRevenueDataAsync(startDate, endDate);

                if (revenueData.Count == 0)
                {
                    Console.WriteLine("No revenue data found.");
                    return;
                }

                var topSections = GetTopSectionsByRevenue(revenueData);

                foreach (var button in ZoomCanvas.Children.OfType<Button>())
                {
                    var sectionId = button.Tag?.ToString();
                    if (sectionId != null && revenueData.TryGetValue(sectionId, out var revenue))
                    {
                        button.Background = topSections.Contains(sectionId)
                            ? new SolidColorBrush(Colors.Red)   // High revenue sections
                            : new SolidColorBrush(Colors.Blue); // Low revenue sections
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching revenue data: {ex.Message}");
            }
        }


        private void ClearRevenueDisplay()
        {
            foreach (var button in ZoomCanvas.Children.OfType<Button>())
            {
                button.Background = new SolidColorBrush(Colors.Gray); // Reset to default color
            }
        }

        private void ProductList_Click(object sender, RoutedEventArgs e)
        {
            // Access the MainWindow instance
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

            // Set the content if MainWindow is found
            if (mainWindow != null)
            {
                mainWindow.MainContent.Content = new ProductListControl();
            }
        }

        private void SalesReport_Click(object sender, RoutedEventArgs e)
        {
            // Access the MainWindow instance
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

            // Set the content if MainWindow is found
            if (mainWindow != null)
            {
                mainWindow.MainContent.Content = new ReportsListControl();
            }
        }
    }
}
