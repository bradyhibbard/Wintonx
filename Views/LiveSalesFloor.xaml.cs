using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Winton.Helpers;
using Winton.Models;
using Winton.Services;
using Winton.ViewModels;

namespace Winton.Views
{
    public partial class LiveSalesFloor : UserControl
    {
        // ─── State ────────────────────────────────────────────────────────────
        private List<Product> _allProducts = new();
        private string _currentSectionId = null;
        private DateTime _periodStart;
        private DateTime _periodEnd;
        private bool _heatByRevenue = true;

        private SectionManager _sectionManager;
        private Polyline _perimeterLine = new() { Stroke = Brushes.Black, StrokeThickness = 2 };
        private Action<string> _sectionSelectedHandler;

        // ─── Debounce timers ──────────────────────────────────────────────────
        private DispatcherTimer _filterDebounce;
        private DispatcherTimer _drawerFilterDebounce;

        private bool _initialized = false;

        // ─── Constructor ──────────────────────────────────────────────────────
        public LiveSalesFloor()
        {
            InitializeComponent();

            _sectionManager = new SectionManager(LiveFloorCanvas, this);

            // Initialise date range before any UI handlers fire
            SetDateRange("This Month");

            _sectionSelectedHandler = sectionId => _ = OpenSectionDrawerAsync(sectionId);
            _sectionManager.SectionSelected += _sectionSelectedHandler;

            Loaded   += async (s, e) => await InitializeAsync();
            Unloaded += (s, e)       => _sectionManager.SectionSelected -= _sectionSelectedHandler;
        }

        // ─── Initialisation ───────────────────────────────────────────────────
        private async Task InitializeAsync()
        {
            _initialized = true;
            _allProducts = await ProductService.GetProductsAsync();
            await LoadSectionsAsync();
            await LoadPerimeterAsync();
            await LoadPartitionsAsync();
            await RefreshKpisAsync();
            await ApplyHeatMapAsync();
        }

        private async Task LoadSectionsAsync()
        {
            var sections = await CanvasService.LoadSectionsAsync();

            foreach (var section in sections)
            {
                await _sectionManager.AddShapeToCanvasAsync(
                    shapeName: section.name,
                    shapeType: section.shapeType ?? section.name,
                    x: section.x,
                    y: section.y,
                    width: section.width,
                    height: section.height,
                    rotation: section.rotation,
                    existingSectionId: section.sectionId,
                    wrapAsButton: true);

                var button = LiveFloorCanvas.Children.OfType<Button>()
                    .FirstOrDefault(b => b.Tag as string == section.sectionId);

                if (button != null)
                    button.Focusable = false;
            }
        }

        private async Task LoadPerimeterAsync()
        {
            var points = await CanvasService.LoadPerimeterAsync();
            _perimeterLine.Points.Clear();
            foreach (var pt in points)
                _perimeterLine.Points.Add(pt);

            if (!LiveFloorCanvas.Children.Contains(_perimeterLine))
                LiveFloorCanvas.Children.Add(_perimeterLine);
        }

        private async Task LoadPartitionsAsync()
        {
            var partitions = await CanvasService.LoadPartitionsAsync();
            foreach (var partition in partitions)
            {
                var polyline = new Polyline { Stroke = Brushes.DarkGray, StrokeThickness = 1.5 };
                foreach (var point in partition.Points)
                    polyline.Points.Add(point);
                LiveFloorCanvas.Children.Add(polyline);
            }
        }

        // ─── Date range ───────────────────────────────────────────────────────
        private void DateRangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateRangeCombo.SelectedItem is ComboBoxItem item)
                SetDateRange(item.Content?.ToString());

            if (!_initialized) return;
            _ = RefreshAllAsync();
        }

        private void SetDateRange(string preset)
        {
            _periodEnd   = DateTime.Today;
            _periodStart = preset switch
            {
                "Last 7 Days"   => _periodEnd.AddDays(-7),
                "This Month"    => new DateTime(_periodEnd.Year, _periodEnd.Month, 1),
                "Last 30 Days"  => _periodEnd.AddDays(-30),
                "This Quarter"  => new DateTime(_periodEnd.Year, ((_periodEnd.Month - 1) / 3) * 3 + 1, 1),
                "Year to Date"  => new DateTime(_periodEnd.Year, 1, 1),
                _               => _periodEnd.AddMonths(-1)
            };

            string label = preset ?? "Period";
            if (KpiRevenuePeriod != null) KpiRevenuePeriod.Text = label;
            if (KpiUnitsPeriod   != null) KpiUnitsPeriod.Text   = label;
        }

        private async Task RefreshAllAsync()
        {
            await RefreshKpisAsync();
            await ApplyHeatMapAsync();

            if (AnyFilterActive())
                _ = ApplyFiltersAsync();

            if (_currentSectionId != null)
                await RefreshDrawerStatsAsync();
        }

        // ─── KPI bar ──────────────────────────────────────────────────────────
        private async Task RefreshKpisAsync()
        {
            try
            {
                var revenueMap = await SalesDataServices.GetRevenueDataAsync(_periodStart, _periodEnd);
                KpiRevenue.Text  = revenueMap.Values.Sum().ToString("C0");
                KpiSections.Text = revenueMap.Count(kv => kv.Value > 0).ToString();

                var qtyMap = await SalesDataServices.GetQuantityBySectionAsync(_periodStart, _periodEnd);
                KpiUnits.Text = qtyMap.Values.Sum().ToString("N0");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveSalesFloor] RefreshKpisAsync: {ex}");
            }
        }

        // ─── Heat map ─────────────────────────────────────────────────────────
        private async Task ApplyHeatMapAsync()
        {
            try
            {
                Dictionary<string, decimal> valueMap;

                if (_heatByRevenue)
                {
                    valueMap = await SalesDataServices.GetRevenueDataAsync(_periodStart, _periodEnd);
                }
                else
                {
                    var qtyMap = await SalesDataServices.GetQuantityBySectionAsync(_periodStart, _periodEnd);
                    valueMap = qtyMap.ToDictionary(
                        kv => kv.Key,
                        kv => (decimal)kv.Value,
                        StringComparer.OrdinalIgnoreCase);
                }

                decimal maxVal = valueMap.Count > 0 ? valueMap.Values.Max() : 0;

                foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
                {
                    if (btn.Tag is not string sectionId) continue;

                    decimal val = valueMap.GetValueOrDefault(sectionId, 0m);
                    var heat    = GetHeatColor(val, maxVal);

                    if (btn.Content is Shape shape)
                        shape.Fill = new SolidColorBrush(heat);

                    btn.Opacity = 1.0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiveSalesFloor] ApplyHeatMapAsync: {ex}");
            }
        }

        private static Color GetHeatColor(decimal value, decimal max)
        {
            if (max <= 0 || value <= 0)
                return Color.FromRgb(70, 70, 75); // gray: no data

            double t = Math.Min(1.0, (double)(value / max));

            return t < 0.5
                ? LerpColor(Color.FromRgb(42, 82, 152),   Color.FromRgb(245, 166, 35), t * 2)
                : LerpColor(Color.FromRgb(245, 166, 35),  Color.FromRgb(52,  199, 89),  (t - 0.5) * 2);
        }

        private static Color LerpColor(Color a, Color b, double t) =>
            Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));

        // ─── Color mode toggle ────────────────────────────────────────────────
        private void ColorMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            _heatByRevenue = RevenueModeBtn.IsChecked == true;
            LegendLabel.Text = _heatByRevenue ? "REVENUE" : "UNITS SOLD";
            _ = ApplyHeatMapAsync();
        }

        // ─── Floor filters ────────────────────────────────────────────────────
        private void FilterInput_Changed(object sender, TextChangedEventArgs e)
        {
            UpdateFilterChips();
            Debounce(ref _filterDebounce, () => _ = ApplyFiltersAsync());
        }

        private bool AnyFilterActive() =>
            !string.IsNullOrWhiteSpace(VendorFilter.Text)   ||
            !string.IsNullOrWhiteSpace(CategoryFilter.Text) ||
            !string.IsNullOrWhiteSpace(GroupFilter.Text)    ||
            !string.IsNullOrWhiteSpace(ProductFilter.Text);

        private async Task ApplyFiltersAsync()
        {
            if (!AnyFilterActive())
            {
                await ApplyHeatMapAsync();
                return;
            }

            var vendor   = VendorFilter.Text.Trim().ToLowerInvariant();
            var category = CategoryFilter.Text.Trim().ToLowerInvariant();
            var group    = GroupFilter.Text.Trim().ToLowerInvariant();
            var product  = ProductFilter.Text.Trim().ToLowerInvariant();

            var filtered = _allProducts
                .Where(p =>
                    (string.IsNullOrEmpty(vendor)   || p.Vendor.ToLowerInvariant().Contains(vendor))   &&
                    (string.IsNullOrEmpty(category) || p.Cat.ToLowerInvariant().Contains(category))    &&
                    (string.IsNullOrEmpty(group)    || p.Grp.ToLowerInvariant().Contains(group))       &&
                    (string.IsNullOrEmpty(product)  || p.ItemNumber.ToLowerInvariant().Contains(product)))
                .ToList();

            if (filtered.Count == 0)
            {
                foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
                    btn.Opacity = 0.2;
                return;
            }

            var itemNumbers = filtered
                .Select(p => p.ItemNumber)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchingIds = await ProductPlacementServices.GetSectionIdsForItemNumbersAsync(itemNumbers);

            foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
            {
                var id    = btn.Tag as string;
                bool match = !string.IsNullOrEmpty(id) && matchingIds.Contains(id);
                btn.Opacity = match ? 1.0 : 0.15;
            }
        }

        // ─── Filter chips ─────────────────────────────────────────────────────
        private void UpdateFilterChips()
        {
            FilterChipsPanel.Children.Clear();
            AddChipIfNeeded("Vendor",   VendorFilter);
            AddChipIfNeeded("Category", CategoryFilter);
            AddChipIfNeeded("Group",    GroupFilter);
            AddChipIfNeeded("Item #",   ProductFilter);
            FilterChipsPanel.Visibility = FilterChipsPanel.Children.Count > 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddChipIfNeeded(string label, TextBox box)
        {
            if (string.IsNullOrWhiteSpace(box.Text)) return;

            var closeBtn = new Button
            {
                Content         = "✕",
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground      = Brushes.White,
                FontSize        = 10,
                Padding         = new Thickness(5, 0, 0, 0),
                Cursor          = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag             = box
            };
            closeBtn.Click += (s, _) => ((TextBox)((Button)s).Tag).Clear();

            FilterChipsPanel.Children.Add(new Border
            {
                Background    = (Brush)TryFindResource("Accent"),
                CornerRadius  = new CornerRadius(12),
                Padding       = new Thickness(10, 4, 6, 4),
                Margin        = new Thickness(0, 0, 6, 4),
                Child         = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children    =
                    {
                        new TextBlock
                        {
                            Text = $"{label}: {box.Text}",
                            Foreground = Brushes.White,
                            FontSize   = 11,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        closeBtn
                    }
                }
            });
        }

        // ─── Section drawer ───────────────────────────────────────────────────
        private async Task OpenSectionDrawerAsync(string sectionId)
        {
            _currentSectionId = sectionId;

            // Reset search filters and lists
            DrawerVendorFilter.Text   = "";
            DrawerCategoryFilter.Text = "";
            DrawerProductFilter.Text  = "";
            DrawerProductsListBox.Items.Clear();
            DrawerCurrentProductsListBox.Items.Clear();
            DrawerDatePicker.SelectedDate = null;

            // Section name
            var sections = await CanvasService.LoadSectionsAsync();
            var section  = sections.FirstOrDefault(s => s.sectionId == sectionId);
            DrawerSectionName.Text = string.IsNullOrWhiteSpace(section.name) ? sectionId : section.name;

            await RefreshDrawerStatsAsync();

            // Default product search shows all (empty filters)
            FilterDrawerProducts();

            AnimateDrawer(open: true);
        }

        private async Task RefreshDrawerStatsAsync()
        {
            var sectionId = _currentSectionId;

            decimal revenue = await SalesDataServices.GetRevenueBySectionAsync(sectionId, _periodStart, _periodEnd);
            DrawerRevenue.Text = revenue.ToString("C0");

            var qtyMap = await SalesDataServices.GetQuantityBySectionAsync(_periodStart, _periodEnd);
            DrawerQty.Text = qtyMap.GetValueOrDefault(sectionId, 0).ToString("N0");

            var products = await ProductPlacementServices.GetProductsBySectionAsync(sectionId);
            DrawerProductCount.Text = products.Count.ToString();

            DrawerCurrentProductsListBox.Items.Clear();
            foreach (var p in products)
                DrawerCurrentProductsListBox.Items.Add(p.ItemNumber);
        }

        private void CloseSectionDrawer()
        {
            _currentSectionId = null;
            AnimateDrawer(open: false);
        }

        private void AnimateDrawer(bool open)
        {
            double to = open ? 0 : 320;
            DrawerSlideTransform.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(to, TimeSpan.FromMilliseconds(open ? 250 : 200))
                {
                    EasingFunction = new CubicEase
                    {
                        EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn
                    }
                });
        }

        private void CloseDrawer_Click(object sender, RoutedEventArgs e) => CloseSectionDrawer();

        // ─── Section rename ───────────────────────────────────────────────────
        private void DrawerRename_Click(object sender, RoutedEventArgs e)
        {
            DrawerSectionNameBox.Text = DrawerSectionName.Text;
            DrawerNameDisplay.Visibility = Visibility.Collapsed;
            DrawerNameEdit.Visibility    = Visibility.Visible;
            DrawerSectionNameBox.Focus();
            DrawerSectionNameBox.SelectAll();
        }

        private void DrawerRenameConfirm_Click(object sender, RoutedEventArgs e)
            => _ = CommitSectionRenameAsync();

        private void DrawerSectionNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  _ = CommitSectionRenameAsync();
            if (e.Key == Key.Escape) CancelRename();
        }

        private async Task CommitSectionRenameAsync()
        {
            string newName = DrawerSectionNameBox.Text.Trim();
            if (string.IsNullOrEmpty(newName)) { CancelRename(); return; }

            await CanvasService.UpdateSectionNameAsync(_currentSectionId, newName);
            DrawerSectionName.Text = newName;
            CancelRename();
        }

        private void CancelRename()
        {
            DrawerNameEdit.Visibility    = Visibility.Collapsed;
            DrawerNameDisplay.Visibility = Visibility.Visible;
        }

        // ─── Drawer product search ─────────────────────────────────────────────
        private void DrawerFilter_Changed(object sender, TextChangedEventArgs e)
            => Debounce(ref _drawerFilterDebounce, FilterDrawerProducts);

        private void FilterDrawerProducts()
        {
            var vendor   = DrawerVendorFilter.Text.Trim().ToLowerInvariant();
            var category = DrawerCategoryFilter.Text.Trim().ToLowerInvariant();
            var product  = DrawerProductFilter.Text.Trim().ToLowerInvariant();

            var filtered = _allProducts
                .Where(p =>
                    (string.IsNullOrEmpty(vendor)   || p.Vendor.ToLowerInvariant().Contains(vendor))   &&
                    (string.IsNullOrEmpty(category) || p.Cat.ToLowerInvariant().Contains(category))    &&
                    (string.IsNullOrEmpty(product)  || p.ItemNumber.ToLowerInvariant().Contains(product)))
                .Take(200)
                .ToList();

            DrawerSelectAll.IsChecked = false;
            DrawerProductsListBox.Items.Clear();

            foreach (var p in filtered)
                DrawerProductsListBox.Items.Add(new CheckBox
                {
                    Content    = p.ItemNumber,
                    Foreground = (Brush)TryFindResource("PrimaryText"),
                    IsChecked  = false
                });
        }

        private void DrawerSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in DrawerProductsListBox.Items)
                cb.IsChecked = true;
        }

        private void DrawerSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox cb in DrawerProductsListBox.Items)
                cb.IsChecked = false;
        }

        // ─── Drawer: Add products ─────────────────────────────────────────────
        private async void DrawerAddProducts_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId)) return;

            var selected = DrawerProductsListBox.Items.Cast<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Content.ToString())
                .ToList();

            if (!selected.Any())
            {
                MessageBox.Show("No products selected.", "Add Products",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var itemNumber in selected)
                await ProductPlacementServices.PlaceProductAsync(itemNumber, _currentSectionId);

            await RefreshDrawerStatsAsync();
            await RefreshKpisAsync();
            await ApplyHeatMapAsync();
        }

        // ─── Drawer: Current products ─────────────────────────────────────────
        private async void DrawerCurrentProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DrawerCurrentProductsListBox.SelectedItem is string itemNumber)
            {
                var placement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);
                DrawerDatePicker.SelectedDate = placement?.DatePlaced;
            }
            else
            {
                DrawerDatePicker.SelectedDate = null;
            }
        }

        private async void DrawerDatePicker_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (DrawerCurrentProductsListBox.SelectedItem is string itemNumber &&
                DrawerDatePicker.SelectedDate.HasValue &&
                !string.IsNullOrEmpty(_currentSectionId))
            {
                var placement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);
                if (placement != null)
                {
                    await ProductPlacementServices.UpdateDatePlacedAsync(
                        placement.ProductID, _currentSectionId, DrawerDatePicker.SelectedDate.Value);
                    Debug.WriteLine($"[LiveSalesFloor] DatePlaced updated: {itemNumber} → {DrawerDatePicker.SelectedDate.Value:d}");
                }
            }
        }

        // ─── Drawer: Remove products ──────────────────────────────────────────
        private async void DrawerRemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId)) return;

            var selected = DrawerCurrentProductsListBox.SelectedItems.Cast<string>().ToList();
            if (!selected.Any())
            {
                MessageBox.Show("No products selected for removal.", "Remove Products",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var itemNumber in selected)
            {
                var placement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);
                if (placement != null && placement.SectionID == _currentSectionId)
                {
                    await ProductPlacementServices.RemoveProductFromSectionAsync(
                        placement.ProductID, _currentSectionId, DateTime.Now);

                    await ProductPlacementServices.ArchiveProductPlacementAsync(
                        productId:     placement.ProductID,
                        sectionId:     _currentSectionId,
                        removalNotes:  "Removed manually",
                        quantitySold:  placement.QuantitySold,
                        revenue:       placement.Revenue);
                }
            }

            await RefreshDrawerStatsAsync();
            await RefreshKpisAsync();
            await ApplyHeatMapAsync();
        }

        // ─── Drawer: Archive ──────────────────────────────────────────────────
        private void DrawerViewArchive_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId)) return;
            new SectionArchiveWindow(_currentSectionId).Show();
        }

        // ─── Drawer: Full Analytics ───────────────────────────────────────────
        private void DrawerViewFullAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentSectionId))
                _ = ShowFullAnalyticsAsync(_currentSectionId);
        }

        private async Task ShowFullAnalyticsAsync(string sectionId)
        {
            try
            {
                CloseSectionDrawer();

                var sections    = await CanvasService.LoadSectionsAsync();
                var section     = sections.FirstOrDefault(s => s.sectionId == sectionId);
                string name     = string.IsNullOrWhiteSpace(section.name) ? sectionId : section.name;

                decimal revenue          = await SalesDataServices.GetRevenueBySectionAsync(sectionId, _periodStart, _periodEnd);
                var activeProducts       = await ProductPlacementServices.GetProductsBySectionAsync(sectionId);
                var archivedProducts     = await ProductPlacementServices.GetArchivedProductsBySectionAsync(sectionId);

                var breakdown = activeProducts
                    .Select(p => new ProductBreakdownItem
                    {
                        ItemNumber   = p.ItemNumber,   ProductName  = p.ItemNumber,
                        QuantitySold = p.QuantitySold, Revenue      = p.Revenue,
                        IsActive     = true,           DatePlaced   = p.DatePlaced
                    })
                    .Concat(archivedProducts.Select(a => new ProductBreakdownItem
                    {
                        ItemNumber   = a.ItemNumber,  ProductName  = a.ItemNumber,
                        QuantitySold = 0,              Revenue     = a.Revenue,
                        IsActive     = false,          DatePlaced  = a.DatePlaced
                    }))
                    .ToList();

                var top = breakdown.OrderByDescending(p => p.Revenue).FirstOrDefault();

                var vm = new SectionDetailViewModel
                {
                    SectionId          = sectionId,
                    SectionName        = name,
                    StartDate          = _periodStart,
                    EndDate            = _periodEnd,
                    TotalRevenue       = revenue,
                    TotalQuantitySold  = breakdown.Sum(p => p.QuantitySold),
                    TopProductName     = top?.ProductName    ?? "N/A",
                    TopProductRevenue  = top?.Revenue        ?? 0,
                    ProductBreakdown   = breakdown
                };

                vm.DailyTrendline           = await ChartDataFactory.CreateDailyTrendlineAsync(sectionId, _periodStart, _periodEnd);
                vm.TopProductsBarChart      = await ChartDataFactory.CreateTopProductsBarAsync(sectionId, _periodStart, _periodEnd);
                vm.SectionVsStoreComparison = await ChartDataFactory.CreateSectionVsStoreComparisonAsync(sectionId, _periodStart, _periodEnd);

                SectionDetailOverlay.Children.Clear();
                SectionDetailOverlay.Children.Add(new SectionDetail(vm));
                Panel.SetZIndex(SectionDetailOverlay, 9999);
                SectionDetailOverlay.IsHitTestVisible = true;
                SectionDetailOverlay.Visibility       = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading section details: {ex.Message}");
            }
        }

        // ─── Debounce helper ──────────────────────────────────────────────────
        private void Debounce(ref DispatcherTimer timer, Action action, int ms = 250)
        {
            if (timer == null)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (s, e) => { t.Stop(); action(); };
                timer = t;
            }
            timer.Stop();
            timer.Start();
        }
    }
}
