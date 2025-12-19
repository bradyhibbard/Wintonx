using NPOI.SS.UserModel.Charts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;      // ✅ For DispatcherTimer
using Winton.Helpers;
using Winton.Models;
using Winton.Services;
using Winton.ViewModels;

namespace Winton.Views
{
    public partial class LiveSalesFloor : UserControl
    {
        private List<Product> _allProducts;
        private string _currentSectionId = null;
        private bool _isFilterPanelOpen = false;
        private bool _isProductPanelOpen = false;
        private double _filterPanelWidth => ActualWidth * 0.3;
        private double _productPanelWidth => ActualWidth * 0.3;
        private SectionManager _sectionManager;
        private Polyline _perimeterLine = new Polyline { Stroke = Brushes.Black, StrokeThickness = 2 };

        // ✅ Debounce timers for filters
        private DispatcherTimer _filterPanelDebounceTimer;
        private DispatcherTimer _productPanelDebounceTimer;

        public LiveSalesFloor()
        {
            InitializeComponent();
            _sectionManager = new SectionManager(LiveFloorCanvas, this);
            Loaded += LiveSalesFloor_Loaded;

            // Subscribe to the SectionSelected event.
            _sectionManager.SectionSelected += async (sectionId) =>
            {
                await LoadSectionDetails(sectionId);
            };
        }

        private async void LiveSalesFloor_Loaded(object sender, RoutedEventArgs e)
        {
            _allProducts = await ProductService.GetProductsAsync();
            await LoadSectionsAsync();
            await LoadPerimeterAsync();
            await LoadPartitionsAsync();
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

                // Find the button after it's added to the canvas
                var button = LiveFloorCanvas.Children.OfType<Button>()
                                                     .FirstOrDefault(b => b.Tag as string == section.sectionId);

                if (button != null)
                {
                    button.Focusable = true;  // Ensure the button can receive focus
                    button.PreviewMouseDoubleClick += Section_MouseDoubleClick;


                    // Add a debug log to verify the event is attached
                    Console.WriteLine($"DoubleClick event attached to section {section.sectionId}");
                }
            }
        }

        private void Section_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is Button sectionButton && sectionButton.Tag is string sectionId)
            {
                _currentSectionId = sectionId;

                // Load the section data
                _ = LoadSectionDetails(sectionId);

                // Open the Add Product Panel
                ToggleProductPanel(true);
            }
        }


        private async Task LoadPerimeterAsync()
        {
            var points = await CanvasService.LoadPerimeterAsync();
            _perimeterLine.Points.Clear();
            foreach (var pt in points)
            {
                _perimeterLine.Points.Add(pt);
            }
            if (!LiveFloorCanvas.Children.Contains(_perimeterLine))
            {
                LiveFloorCanvas.Children.Add(_perimeterLine);
            }
        }

        private async Task LoadPartitionsAsync()
        {
            var partitions = await CanvasService.LoadPartitionsAsync();
            foreach (var partition in partitions)
            {
                var polyline = new Polyline { Stroke = Brushes.DarkGray, StrokeThickness = 1.5 };
                foreach (var point in partition.Points)
                {
                    polyline.Points.Add(point);
                }
                LiveFloorCanvas.Children.Add(polyline);
            }
        }

        private void ToggleProductPanel(bool open)
        {
            if(open && _isFilterPanelOpen)
                ToggleFilterPanel(false);

            _isProductPanelOpen = open;
            AnimatePanel(ProductPanelColumn, ProductPanel, open, _productPanelWidth);
        }

        private void AnimatePanel(ColumnDefinition column, FrameworkElement panel, bool open, double targetWidth)
        {
            GridLengthAnimation animation = new GridLengthAnimation
            {
                From = new GridLength(column.Width.Value, GridUnitType.Pixel),
                To = open ? new GridLength(targetWidth, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel),
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                FillBehavior = FillBehavior.HoldEnd
            };

            animation.Completed += (s, e) =>
            {
                panel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            };

            if (open)
                panel.Visibility = Visibility.Visible;

            column.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }

        private void AddProducts_Click(object sender, RoutedEventArgs e)
        {
            ToggleProductPanel(!_isProductPanelOpen);
        }

        private void ToggleFilterPanel(bool open)
        {
            if (open && _isProductPanelOpen)
                ToggleProductPanel(false);

            _isFilterPanelOpen = open;

            GridLengthAnimation animation = new GridLengthAnimation
            {
                From = new GridLength(FilterPanelColumn.Width.Value, GridUnitType.Pixel),
                To = open ? new GridLength(_filterPanelWidth, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel),
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                FillBehavior = FillBehavior.HoldEnd
            };

            animation.Completed += (s, e) =>
            {
                FilterPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            };

            if (open)
                FilterPanel.Visibility = Visibility.Visible;

            FilterPanelColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }

        // ✅ Debounce helper to avoid filtering on every keystroke
        private void DebounceFilter(Action action, ref DispatcherTimer timer, int delayMs = 250)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(delayMs);

                var localTimer = timer;

                localTimer.Tick += (s, e) =>
                {
                    localTimer.Stop();
                    action();
                };
            }

            timer.Stop();
            timer.Start();
        }



        // PRODUCT PANEL METHODS
        private void ProductVendorTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProductPanel, ref _productPanelDebounceTimer);

        private void ProductCategoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProductPanel, ref _productPanelDebounceTimer);

        private void ProductGroupTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProductPanel, ref _productPanelDebounceTimer);

        private void ProductProductTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProductPanel, ref _productPanelDebounceTimer);

        private void FilterProductPanel()
        {
            var vendorInput = ProductVendorTextBox.Text.ToLower();
            var categoryInput = ProductCategoryTextBox.Text.ToLower();
            var groupInput = ProductGroupTextBox.Text.ToLower();
            var productInput = AddProductTextBox.Text.ToLower();

            var filteredProducts = _allProducts
                .Where(p => (string.IsNullOrEmpty(vendorInput) || p.Vendor.ToLower().Contains(vendorInput)) &&
                            (string.IsNullOrEmpty(categoryInput) || p.Cat.ToLower().Contains(categoryInput)) &&
                            (string.IsNullOrEmpty(groupInput) || p.Grp.ToLower().Contains(groupInput)) &&
                            (string.IsNullOrEmpty(productInput) || p.ItemNumber.ToLower().Contains(productInput)))
                .OrderBy(p => p.ItemNumber)
                .ToList();

            UpdateFilteredProductsList(filteredProducts);
        }

        private void Filters_Click(object sender, RoutedEventArgs e)
        {
            ToggleFilterPanel(!_isFilterPanelOpen);
        }

        // FILTER PANEL METHODS (now debounced)
        private void VendorTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProducts, ref _filterPanelDebounceTimer);

        private void CategoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProducts, ref _filterPanelDebounceTimer);

        private void GroupTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProducts, ref _filterPanelDebounceTimer);

        private void ProductTextBox_TextChanged(object sender, TextChangedEventArgs e)
            => DebounceFilter(FilterProducts, ref _filterPanelDebounceTimer);

        private void FilterProducts()
        {
            var vendorInput = VendorTextBox.Text.ToLower();
            var categoryInput = CategoryTextBox.Text.ToLower();
            var groupInput = GroupTextBox.Text.ToLower();
            var productInput = ProductTextBox.Text.ToLower();

            var filteredProducts = _allProducts
                .Where(p => (string.IsNullOrEmpty(vendorInput) || p.Vendor.ToLower().Contains(vendorInput)) &&
                            (string.IsNullOrEmpty(categoryInput) || p.Cat.ToLower().Contains(categoryInput)) &&
                            (string.IsNullOrEmpty(groupInput) || p.Grp.ToLower().Contains(groupInput)) &&
                            (string.IsNullOrEmpty(productInput) || p.ItemNumber.ToLower().Contains(productInput)))
                .OrderBy(p => p.ItemNumber)
                .ToList();

            UpdateFilteredProductsList(filteredProducts);

            // ✅ Skip heavy DB + overlay work if there is no actual filter input
            bool anyInput = !(string.IsNullOrEmpty(vendorInput) &&
                              string.IsNullOrEmpty(categoryInput) &&
                              string.IsNullOrEmpty(groupInput) &&
                              string.IsNullOrEmpty(productInput));

            if (!anyInput)
            {
                ClearHighlights();
                RevenueOverlayCanvas.Children.Clear();
                return;
            }

            _ = HighlightForFilteredProductsAsync(filteredProducts);
        }

        private void UpdateFilteredProductsList(List<Product> products)
        {
            FilteredProductsListBox.Items.Clear();

            foreach (var product in products)
            {
                var checkbox = new CheckBox
                {
                    Content = product.ItemNumber,
                    IsChecked = false
                };

                FilteredProductsListBox.Items.Add(checkbox);
            }
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox checkbox in FilteredProductsListBox.Items)
            {
                checkbox.IsChecked = true;
            }
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox checkbox in FilteredProductsListBox.Items)
            {
                checkbox.IsChecked = false;
            }
        }

        private async void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId))
            {
                MessageBox.Show("No section selected. Please select a section first.");
                return;
            }

            var selectedProducts = FilteredProductsListBox.Items.Cast<CheckBox>()
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Content.ToString())
                .ToList();

            if (!selectedProducts.Any())
            {
                MessageBox.Show("No products selected.");
                return;
            }

            foreach (var productId in selectedProducts)
            {
                await ProductPlacementServices.PlaceProductAsync(productId, _currentSectionId);
            }

            MessageBox.Show("Products added successfully: " + string.Join(", ", selectedProducts));

            // Refresh the Added Products List
            await LoadAddedProducts(_currentSectionId);
        }

        private async Task LoadSectionDetails(string sectionId)
        {
            _currentSectionId = sectionId;

            var sections = await CanvasService.LoadSectionsAsync();
            var section = sections.FirstOrDefault(s => s.sectionId == sectionId);

            if (!string.IsNullOrEmpty(section.sectionId))
            {
                SectionIdTextBox.Text = section.name;

                await LoadAddedProducts(sectionId);
            }
        }

        private async Task LoadAddedProducts(string sectionId)
        {
            // Clear the list to avoid duplicates
            AddedProductsListBox.Items.Clear();

            if (string.IsNullOrEmpty(sectionId))
                return;

            try
            {
                var productsInSection = await ProductPlacementServices.GetProductsBySectionAsync(sectionId);

                foreach (var product in productsInSection)
                {
                    AddedProductsListBox.Items.Add(product.ItemNumber);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading products for section {sectionId}: {ex.Message}");
            }
        }

        private async void SectionIdTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentSectionId) && !string.IsNullOrEmpty(SectionIdTextBox.Text))
            {
                string newName = SectionIdTextBox.Text;

                try
                {
                    await CanvasService.UpdateSectionNameAsync(_currentSectionId, newName);
                    Console.WriteLine($"Section ID updated to: {newName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating Section ID: {ex.Message}");
                }
            }
        }

        private async void RemoveProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId))
            {
                MessageBox.Show("No section selected. Please select a section first.");
                return;
            }

            var selectedProducts = AddedProductsListBox.SelectedItems.Cast<string>().ToList();

            if (!selectedProducts.Any())
            {
                MessageBox.Show("No products selected for removal.");
                return;
            }

            try
            {
                foreach (var itemNumber in selectedProducts)
                {
                    var productPlacement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);

                    if (productPlacement != null && productPlacement.SectionID == _currentSectionId)
                    {
                        DateTime removalDate = DateTime.Now;

                        // Update DateRemoved before archiving
                        await ProductPlacementServices.RemoveProductFromSectionAsync(
                            productPlacement.ProductID,
                            _currentSectionId,
                            removalDate
                        );

                        // Archive the product placement
                        await ProductPlacementServices.ArchiveProductPlacementAsync(
                            productId: productPlacement.ProductID,
                            sectionId: _currentSectionId,
                            removalNotes: "Removed manually",
                            quantitySold: productPlacement.QuantitySold,
                            revenue: productPlacement.Revenue
                        );
                    }
                }

                MessageBox.Show("Selected products removed and archived successfully.");

                // Refresh the Added Products List
                await LoadAddedProducts(_currentSectionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing and archiving products: {ex.Message}");
            }
        }

        private async void DateAddedPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddedProductsListBox.SelectedItem is string itemNumber && DateAddedPicker.SelectedDate.HasValue)
            {
                DateTime newDateAdded = DateAddedPicker.SelectedDate.Value;

                var productPlacement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);

                if (productPlacement != null)
                {
                    try
                    {
                        await ProductPlacementServices.UpdateDatePlacedAsync(productPlacement.ProductID, _currentSectionId, newDateAdded);
                        Console.WriteLine($"Date Added updated for {itemNumber}: {newDateAdded}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating Date Added: {ex.Message}");
                    }
                }
            }
        }

        private async void AddedProductsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AddedProductsListBox.SelectedItem is string itemNumber)
            {
                var productPlacement = await ProductPlacementServices.GetCurrentProductPlacementAsync(itemNumber);

                if (productPlacement != null)
                {
                    DateAddedPicker.SelectedDate = productPlacement.DatePlaced;
                }
                else
                {
                    DateAddedPicker.SelectedDate = null; // Clear the DatePicker if no valid product is found
                }
            }
            else
            {
                DateAddedPicker.SelectedDate = null; // Clear the DatePicker if no product is selected
            }
        }

        private void ViewSectionArchive_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSectionId))
            {
                MessageBox.Show("No section selected.");
                return;
            }

            var archiveWindow = new SectionArchiveWindow(_currentSectionId);
            archiveWindow.Show();
        }

        // Highlighting the Filtered Products

        private void ClearHighlights()
        {
            foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
            {
                btn.Opacity = 1.0;

                // If Button wraps a Shape, reset stroke
                if (btn.Content is Shape shape)
                {
                    shape.Stroke = Brushes.Black;
                    shape.StrokeThickness = 2;
                }
            }
        }

        private void HighlightSections(HashSet<string> matchingSectionIds)
        {
            foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
            {
                var sectionId = btn.Tag as string;

                if (!string.IsNullOrEmpty(sectionId) && matchingSectionIds.Contains(sectionId))
                {
                    // MATCH: brighten + bold stroke
                    btn.Opacity = 1.0;
                    if (btn.Content is Shape shape)
                    {
                        shape.Stroke = Brushes.DeepSkyBlue;
                        shape.StrokeThickness = 3;
                    }
                }
                else
                {
                    // NON-MATCH: dim + reset stroke
                    btn.Opacity = 0.3;
                    if (btn.Content is Shape shape)
                    {
                        shape.Stroke = Brushes.Black;
                        shape.StrokeThickness = 2;
                    }
                }
            }
        }

        private void DimAllSections_NoResults()
        {
            foreach (var btn in LiveFloorCanvas.Children.OfType<Button>())
            {
                btn.Opacity = 0.3;
                if (btn.Content is Shape shape)
                {
                    shape.Stroke = Brushes.Black;
                    shape.StrokeThickness = 2;
                }
            }
        }

        private async Task HighlightForFilteredProductsAsync(List<Product> filteredProducts)
        {
            Console.WriteLine("HighlightForFilteredProductsAsync triggered");

            // If the section detail overlay is open, don't draw / intercept anything underneath.
            if (SectionDetailOverlay.Visibility == Visibility.Visible)
            {
                RevenueOverlayCanvas.Children.Clear();
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            var vendorInput = VendorTextBox.Text?.Trim();
            var categoryInput = CategoryTextBox.Text?.Trim();
            var groupInput = GroupTextBox.Text?.Trim();
            var productInput = ProductTextBox.Text?.Trim();

            bool anyInput = !(string.IsNullOrWhiteSpace(vendorInput) &&
                              string.IsNullOrWhiteSpace(categoryInput) &&
                              string.IsNullOrWhiteSpace(groupInput) &&
                              string.IsNullOrWhiteSpace(productInput));

            if (!anyInput)
            {
                ClearHighlights();

                RevenueOverlayCanvas.Children.Clear();
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;

                return;
            }

            if (filteredProducts == null || filteredProducts.Count == 0)
            {
                DimAllSections_NoResults();

                RevenueOverlayCanvas.Children.Clear();
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;

                return;
            }

            var itemNumbers = filteredProducts
                .Select(p => p?.ItemNumber)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (itemNumbers.Count == 0)
            {
                DimAllSections_NoResults();

                RevenueOverlayCanvas.Children.Clear();
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;

                return;
            }

            var matchingSectionIds = await ProductPlacementServices.GetSectionIdsForItemNumbersAsync(itemNumbers);

            Console.WriteLine($"Found {matchingSectionIds?.Count ?? 0} matching sections");

            if (matchingSectionIds == null || matchingSectionIds.Count == 0)
            {
                DimAllSections_NoResults();

                RevenueOverlayCanvas.Children.Clear();
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;

                return;
            }

            HighlightSections(matchingSectionIds);

            // Prepare overlay for clickable labels
            RevenueOverlayCanvas.Children.Clear();
            RevenueOverlayCanvas.Visibility = Visibility.Visible;
            RevenueOverlayCanvas.IsHitTestVisible = true;

            foreach (var sectionId in matchingSectionIds)
            {
                if (string.IsNullOrWhiteSpace(sectionId))
                    continue;

                try
                {
                    DateTime startDate = DateTime.Now.AddMonths(-1);
                    DateTime endDate = DateTime.Now;

                    decimal revenue = await SalesDataServices.GetRevenueBySectionAsync(sectionId, startDate, endDate);

                    // Create the clickable revenue label
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(220, 45, 45, 48)),
                        CornerRadius = new CornerRadius(5),
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6),
                        Tag = sectionId,
                        Cursor = Cursors.Hand,
                        IsHitTestVisible = true,
                        Child = new TextBlock
                        {
                            Text = $"Revenue: {revenue:C0}",
                            Foreground = Brushes.White,
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold
                        }
                    };

                    border.MouseLeftButtonUp += RevenueLabel_Click;

                    // Find the UI element for the section so we can position the label near it
                    var sectionElement = LiveFloorCanvas.Children
                        .OfType<FrameworkElement>()
                        .FirstOrDefault(el => el.Tag != null &&
                                              string.Equals(el.Tag.ToString(), sectionId, StringComparison.OrdinalIgnoreCase));

                    if (sectionElement == null)
                        continue;

                    // Convert coordinates from live canvas to overlay canvas
                    Point relativeToLive = sectionElement.TranslatePoint(new Point(0, 0), LiveFloorCanvas);
                    Point relativeToOverlay = LiveFloorCanvas.TranslatePoint(relativeToLive, RevenueOverlayCanvas);

                    double labelX = relativeToOverlay.X + (sectionElement.RenderSize.Width / 2) - 40;
                    double labelY = relativeToOverlay.Y - 30;

                    Canvas.SetLeft(border, labelX);
                    Canvas.SetTop(border, labelY);

                    RevenueOverlayCanvas.Children.Add(border);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding overlay for sectionId {sectionId}: {ex.Message}");
                }
            }
        }



        // LiveSalesFloor.xaml.cs
        private async void RevenueLabel_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is not Border border || border.Tag is not string sectionId || string.IsNullOrWhiteSpace(sectionId))
                return;

            try
            {
                // ✅ IMPORTANT: Once we open SectionDetail, the revenue overlay must NOT block clicks
                RevenueOverlayCanvas.IsHitTestVisible = false;
                RevenueOverlayCanvas.Visibility = Visibility.Collapsed;

                // --- Load section + date range ---
                DateTime startDate = DateTime.Now.AddMonths(-1);
                DateTime endDate = DateTime.Now;

                // --- Get section name (safe) ---
                var sections = await CanvasService.LoadSectionsAsync();
                var section = sections.FirstOrDefault(s => s.sectionId == sectionId);

                string sectionName =
                    (section == default) ? sectionId :
                    (string.IsNullOrWhiteSpace(section.name) ? sectionId : section.name);

                // --- Revenue + product breakdown ---
                decimal totalRevenue = await SalesDataServices.GetRevenueBySectionAsync(sectionId, startDate, endDate);

                var activeProducts = await ProductPlacementServices.GetProductsBySectionAsync(sectionId);
                var archivedProducts = await ProductPlacementServices.GetArchivedProductsBySectionAsync(sectionId);

                var productBreakdown = activeProducts
                    .Select(p => new ProductBreakdownItem
                    {
                        ItemNumber = p.ItemNumber,
                        ProductName = p.ItemNumber,
                        QuantitySold = p.QuantitySold,
                        Revenue = p.Revenue,
                        IsActive = true,
                        DatePlaced = p.DatePlaced
                    })
                    .Concat(archivedProducts.Select(a => new ProductBreakdownItem
                    {
                        ItemNumber = a.ItemNumber,
                        ProductName = a.ItemNumber,
                        QuantitySold = 0,
                        Revenue = a.Revenue,
                        IsActive = false,
                        DatePlaced = a.DatePlaced
                    }))
                    .ToList();

                int totalQty = productBreakdown.Sum(p => p.QuantitySold);
                var topProduct = productBreakdown.OrderByDescending(p => p.Revenue).FirstOrDefault();

                // --- Build VM ---
                var vm = new SectionDetailViewModel
                {
                    SectionId = sectionId,
                    SectionName = sectionName,
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalRevenue = totalRevenue,
                    TotalQuantitySold = totalQty,
                    TopProductName = topProduct?.ProductName ?? "N/A",
                    TopProductRevenue = topProduct?.Revenue ?? 0,
                    ProductBreakdown = productBreakdown
                };

                // Optional chart loads
                vm.DailyTrendline = await ChartDataFactory.CreateDailyTrendlineAsync(sectionId, startDate, endDate);
                vm.TopProductsBarChart = await ChartDataFactory.CreateTopProductsBarAsync(sectionId, startDate, endDate);
                vm.SectionVsStoreComparison = await ChartDataFactory.CreateSectionVsStoreComparisonAsync(sectionId, startDate, endDate);

                // --- Show overlay ---
                SectionDetailOverlay.Children.Clear();
                SectionDetailOverlay.Children.Add(new SectionDetail(vm));

                // ✅ Make sure it’s above anything else
                Panel.SetZIndex(SectionDetailOverlay, 9999);

                SectionDetailOverlay.IsHitTestVisible = true;
                SectionDetailOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading section details: {ex.Message}");
            }
        }




    }
}
