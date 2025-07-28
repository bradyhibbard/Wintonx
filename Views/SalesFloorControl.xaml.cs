using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace Winton.Views
{
    public partial class SalesFloorControl : UserControl, INotifyPropertyChanged
    {
        private string _tooltipContent;
        private DateTime _startDate;
        private DateTime _endDate;

        // Transformations for zoom and pan
        private TransformGroup transformGroup = new TransformGroup();
        private ScaleTransform scaleTransform = new ScaleTransform();
        private TranslateTransform translateTransform = new TranslateTransform();

        private Point lastMousePosition;
        private bool isPanning = false;

        private string _selectedSection = "";

        // Declare PropertyChanged event
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

        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public SalesFloorControl()
        {
            InitializeComponent();

            // Set up zoom and pan transformations
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            ZoomCanvas.RenderTransform = transformGroup;

            // Hook up event handlers
            ZoomCanvas.MouseWheel += ZoomCanvas_MouseWheel;
            ZoomCanvas.MouseLeftButtonDown += ZoomCanvas_MouseLeftButtonDown;
            ZoomCanvas.MouseMove += ZoomCanvas_MouseMove;
            ZoomCanvas.MouseLeftButtonUp += ZoomCanvas_MouseLeftButtonUp;

            DataContext = this;

            InitializeFilterControlEvents();

            // Ensure sidebar is visible on load
            SidebarPanel.Visibility = Visibility.Visible;
            SidebarColumn.Width = new GridLength(300);
        }

        public string SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (_selectedSection != value)
                {
                    _selectedSection = value;
                    OnPropertyChanged(nameof(SelectedSection));
                }
            }
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add product functionality goes here.");
        }


        private void InitializeFilterControlEvents()
        {
            // Add event handlers for the filter control (modify as per actual logic)
            FilterControl.CancelClicked += (s, e) => ToggleFilterVisibility(s, null);
            //FilterControl.SaveClicked += (s, e) => ApplyFilters(new List<string>());
            FilterControl.ClearClicked += (s, e) => ClearFilters_Click(s, null);
        }


        // Destructor to unhook event handlers and prevent memory leaks
        ~SalesFloorControl()
        {
            ZoomCanvas.MouseWheel -= ZoomCanvas_MouseWheel;
            ZoomCanvas.MouseLeftButtonDown -= ZoomCanvas_MouseLeftButtonDown;
            ZoomCanvas.MouseMove -= ZoomCanvas_MouseMove;
            ZoomCanvas.MouseLeftButtonUp -= ZoomCanvas_MouseLeftButtonUp;
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // Reset Zoom and Pan to default
            scaleTransform.ScaleX = 1;
            scaleTransform.ScaleY = 1;
            translateTransform.X = 0;
            translateTransform.Y = 0;

            // Clear any applied filters (modify this as per your logic)
            //ResetDisplay();
        }

        private void ToggleFilterVisibility(object sender, RoutedEventArgs e)
        {
            FilterControl.Visibility = FilterControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }


        // Zoom in/out with Mouse Wheel
        private void ZoomCanvas_MouseWheel(object sender, MouseWheelEventArgs e) =>
            Zoom(e.Delta > 0 ? 1.1 : 0.9, e.GetPosition(ZoomCanvas));

        private void Zoom(double zoomFactor, Point position)
        {
            double absoluteX = position.X * scaleTransform.ScaleX + translateTransform.X;
            double absoluteY = position.Y * scaleTransform.ScaleY + translateTransform.Y;

            scaleTransform.ScaleX *= zoomFactor;
            scaleTransform.ScaleY *= zoomFactor;

            translateTransform.X = absoluteX - position.X * scaleTransform.ScaleX;
            translateTransform.Y = absoluteY - position.Y * scaleTransform.ScaleY;
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e) =>
            Zoom(1.1, new Point(ZoomCanvas.ActualWidth / 2, ZoomCanvas.ActualHeight / 2));

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e) =>
            Zoom(0.9, new Point(ZoomCanvas.ActualWidth / 2, ZoomCanvas.ActualHeight / 2));

        // Start Panning
        private void ZoomCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            lastMousePosition = e.GetPosition(ZoomCanvas);
            isPanning = true;
            ZoomCanvas.CaptureMouse();
        }

        // Pan while moving
        private void ZoomCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                Point newMousePosition = e.GetPosition(ZoomCanvas);

                translateTransform.X += (newMousePosition.X - lastMousePosition.X);
                translateTransform.Y += (newMousePosition.Y - lastMousePosition.Y);

                lastMousePosition = newMousePosition;
            }
        }

        // Stop Panning
        private void ZoomCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isPanning = false;
            ZoomCanvas.ReleaseMouseCapture();
        }

        // Toggle Sidebar Visibility
        private void ToggleSidebar()
        {
            if (SidebarPanel.Visibility == Visibility.Collapsed)
            {
                SidebarPanel.Visibility = Visibility.Visible;
                SidebarColumn.Width = new GridLength(300);
            }
            else
            {
                SidebarPanel.Visibility = Visibility.Collapsed;
                SidebarColumn.Width = new GridLength(0);
            }
        }

        private void SaveSectionName_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SectionNameTextBox.Text))
            {
                SelectedSection = SectionNameTextBox.Text;
                MessageBox.Show($"Section name updated to: {SelectedSection}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SectionButton_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                string sectionTag = clickedButton.Tag.ToString();
                SectionNameTextBox.Text = sectionTag;
                ProductListBox.Items.Clear();
                ProductListBox.Items.Add("Product 1");
                ProductListBox.Items.Add("Product 2");

            }
        }

        private void CloseSidebar_Click(object sender, RoutedEventArgs e) => ToggleSidebar();


        private void ProductList_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null) mainWindow.MainContent.Content = new ProductListControl();
        }

        private void SalesReport_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null) mainWindow.MainContent.Content = new ReportsListControl();
        }
    }
}
