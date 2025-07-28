using System;
using System.IO;
using System.Xml;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Reflection;
using Winton.Services;
using Winton.Views;
using System.Net.Http;

namespace Winton
{
    public partial class MainWindow : Window
    {
        private const string UpdateManifestUrl = @"\\YOUR_NETWORK_PATH\Winton.application"; // Update with actual UNC path to the Winton.application manifest file
        private EditableSalesFloor _salesFloor;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                await InitializeAsync();
            };
        }


        /// <summary>
        /// Asynchronous initialization of database and other startup tasks.
        /// </summary>
        private async Task InitializeAsync()
        {
            await DatabaseService.InitializeDatabaseAsync();
            Console.WriteLine($"Using DB path: {Path.GetFullPath(DatabaseConfig.DbPath)}");

            MainContent.Content = new Dashboard();  // Load Dashboard after DB initializes
        }


        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            Sidebar.Visibility = Visibility.Collapsed;
            ExpandButton.Visibility = Visibility.Visible;  // Show expand button in the same top position
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            Sidebar.Visibility = Visibility.Visible;
            ExpandButton.Visibility = Visibility.Collapsed; // Hide expand button when sidebar is shown
        }


        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new Dashboard();
        }

        private void SalesFloor_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new LiveSalesFloor();
        }

        private void BugReport_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new BugReport();
        }

        private void ProductList_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ProductListControl();
        }

        private void Reports_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new ReportsListControl();
        }

        private void DB_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new DBControl();
        }

        public EditableSalesFloor SalesFloorInstance
        {
            get { return _salesFloor; }
        }

        private void Canvas_Click(object sender, RoutedEventArgs e)
        {
            if (_salesFloor == null)
            {
                _salesFloor = new EditableSalesFloor();
            }

            MainContent.Content = _salesFloor; // Ensure we always use the same instance
        }

        public void SetMainContent(UserControl control)
        {
            MainContent.Content = control;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartOverlay.Visibility = Visibility.Collapsed;
            BackgroundImage.Visibility = Visibility.Collapsed;
            MainDockPanel.Visibility = Visibility.Visible;
            Sidebar.Visibility = Visibility.Visible;
            MainContent.Content = new Dashboard();
        }
    }
}

