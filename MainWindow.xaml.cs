using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Winton.Services;
using Winton.Views;

namespace Winton
{
    public partial class MainWindow : Window
    {
        private EditableSalesFloor _salesFloor;
        private Button _activeNavBtn;

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
            Debug.WriteLine($"Using DB path: {Path.GetFullPath(DatabaseConfig.DbPath)}");

            NavigateToDashboard();
        }

        private void NavigateToDashboard()
        {
            var dashboard = new Dashboard();
            dashboard.NavigationRequested += dest =>
            {
                switch (dest)
                {
                    case "Reports":    Reports_Click(ReportsButton, null);   break;
                    case "SalesFloor": SalesFloor_Click(SalesFloorButton, null); break;
                }
            };
            MainContent.Content = dashboard;
            SetActiveNav(DashboardButton);
        }

        private void SetActiveNav(Button clicked)
        {
            if (_activeNavBtn != null)
                _activeNavBtn.Tag = null;
            _activeNavBtn = clicked;
            clicked.Tag = "Active";
        }

        // -----------------------------------------------------------
        //  ⭐ WORKING-AREA MAXIMIZE / RESTORE LOGIC (CUSTOM MAXIMIZE)
        // -----------------------------------------------------------

        private bool _isWorkingAreaMaximized = false;
        private Rect _restoreBounds;

        private void ToggleWindowState()
        {
            if (!_isWorkingAreaMaximized)
            {
                // Save current bounds for restore
                _restoreBounds = new Rect(Left, Top, Width, Height);

                // Get usable screen area (taskbar excluded)
                var workingArea = SystemParameters.WorkArea;

                Left = workingArea.Left;
                Top = workingArea.Top;
                Width = workingArea.Width;
                Height = workingArea.Height;

                _isWorkingAreaMaximized = true;
                MaximizeButton.Content = "❐";
            }
            else
            {
                // Restore back to previous size/position
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;

                _isWorkingAreaMaximized = false;
                MaximizeButton.Content = "□";
            }
        }

        private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleWindowState();

        //adding a comment for testing git
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                ToggleWindowState();
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // If dragging while maximized, restore first (optional Windows-like behavior)
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // -----------------------------------------------------------
        //               ⭐ NAVIGATION BUTTON HANDLERS
        // -----------------------------------------------------------

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            NavigateToDashboard();
        }

        private void SalesFloor_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            MainContent.Content = new LiveSalesFloor();
        }

        private void ProductList_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            MainContent.Content = new ProductListControl();
        }

        private void Reports_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            MainContent.Content = new ReportsListControl();
        }

        private void DB_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            MainContent.Content = new DBControl();
        }

        private void Canvas_Click(object sender, RoutedEventArgs e)
        {
            SetActiveNav((Button)sender);
            if (_salesFloor == null)
                _salesFloor = new EditableSalesFloor();
            MainContent.Content = _salesFloor;
        }

        public EditableSalesFloor SalesFloorInstance
        {
            get { return _salesFloor; }
        }

        public void SetMainContent(UserControl control)
        {
            MainContent.Content = control;
        }
    }
}

