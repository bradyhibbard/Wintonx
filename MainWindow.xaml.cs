using System.Windows;
using System.Windows.Controls;
using Winton.Data;
using Winton.Views;

namespace Winton
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DatabaseHelper.InitializeDatabase();
            MainContent.Content = new Dashboard();
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Sidebar.Visibility == Visibility.Visible)
            {
                Sidebar.Visibility = Visibility.Collapsed;
            }
            else
            {
                Sidebar.Visibility = Visibility.Visible;
            }
        }


        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new Dashboard();

        }

        private void SalesFloor_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SalesFloorControl();

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


        public void SetMainContent(UserControl control)
        {
            MainContent.Content = control;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide the start overlay and the background image
            StartOverlay.Visibility = Visibility.Collapsed;
            BackgroundImage.Visibility = Visibility.Collapsed;

            // Make the DockPanel containing sidebar and main content visible
            MainDockPanel.Visibility = Visibility.Visible;

            // Show the sidebar and set the initial content to Dashboard
            Sidebar.Visibility = Visibility.Visible;
            MainContent.Content = new Dashboard();  // Start with the Dashboard
        }


    }
}
