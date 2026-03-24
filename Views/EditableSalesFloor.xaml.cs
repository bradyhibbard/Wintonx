using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Winton.Helpers;
using Winton.Models;
using Winton.Services;

namespace Winton.Views
{
    public partial class EditableSalesFloor : UserControl
    {
        // --------------------------------------------------
        // Constants and State
        // --------------------------------------------------
        private const int GridSpacing = 20;
        private const double DotSize = 5;
        private const double DotRadius = DotSize / 2;
        private const double CloseTolerance = 10;

        private bool _isEditNavVisible = false;
        private bool _isDrawingPerimeter = false;
        private bool _isDrawingPartition = false;
        private bool _isSectionsPanelVisible = false;
        private bool _isEditMode = false;
        private bool _isClearingCanvas = false;
        private int _saveFlashVersion = 0;
        private Section _copiedSection;




        private readonly SectionManager _sectionManager;
        private Shape _selectedShape;
        private List<string> _deletedPartitionIds = new();


        // --------------------------------------------------
        // Dragging Fields for Shapes
        // --------------------------------------------------
        private bool _isDragging = false;
        private Point _dragStart;
        private TranslateTransform _dragTransform;

        // --------------------------------------------------
        // Perimeter Drawing State
        // --------------------------------------------------
        private readonly List<Point> _perimeterPoints = new();
        private readonly List<Ellipse> _perimeterDots = new();
        private Polyline _perimeterLine = new()
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };

        // --------------------------------------------------
        // Partition Drawing State
        // --------------------------------------------------
        private readonly List<Point> _partitionPoints = new();
        private readonly List<Ellipse> _partitionDots = new();
        private Polyline _partitionLine = new()
        {
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };

        // --------------------------------------------------
        // Undo Stack (for finalized shapes)
        // --------------------------------------------------
        // Instead of storing UIElements directly, store a "FinalizedShape"
        // that groups a line plus its dots.
        private readonly Stack<FinalizedShape> _finalizedShapes = new();
        private List<Partition> _partitions = new();

        // --------------------------------------------------
        // Context Menu for Perimeter
        // --------------------------------------------------
        private ContextMenu _perimeterContextMenu;

        // --------------------------------------------------
        // Helper class to group finalized shapes
        // --------------------------------------------------
        private class FinalizedShape
        {
            public List<UIElement> Elements { get; } = new List<UIElement>();
        }

        // ---- Copy/Paste clipboard for a section button ----
        private class SectionClipboard
        {
            public string ShapeType { get; init; } = "Square"; // Square|Circle|Triangle|Cross
            public double Width { get; init; }
            public double Height { get; init; }
            public double Rotation { get; init; }
        }
        private SectionClipboard? _copyBuffer;

        public EditableSalesFloor()
        {
            InitializeComponent();
            this.Focusable = true;
            this.Focus();

            DrawGrid();

            // Add initial perimeter line to the canvas
            SalesFloorCanvas.Children.Add(_perimeterLine);

            SalesFloorCanvas.MouseLeftButtonDown += SalesFloorCanvas_MouseLeftButtonDown;
            this.PreviewKeyDown += EditableSalesFloor_PreviewKeyDown;
            this.PreviewKeyUp += EditableSalesFloor_PreviewKeyUp;

            _sectionManager = new SectionManager(SalesFloorCanvas, this);

            Loaded += async (s, e) =>
            {
                this.Focus(); // Ensure focus on load.
                InitializeCanvas();
                await LoadSectionsFromDatabaseAsync();
                await LoadPerimeterFromDatabaseAsync();
                await LoadPartitionsFromDatabaseAsync();
            };

            InitializePerimeterContextMenu();
        }

        private void InitializeCanvas()
        {
            DrawGrid();

            // Check and re-add the perimeter line only if it is not already in the canvas
            if (!SalesFloorCanvas.Children.Contains(_perimeterLine))
            {
                SalesFloorCanvas.Children.Add(_perimeterLine);
            }
        }

        private static string GetShapeTypeFromShape(Shape s)
        {
            return s switch
            {
                Rectangle => "Square",
                Ellipse => "Circle",
                Polygon => "Triangle",
                Path => "Cross",
                _ => "Square"
            };
        }

        // Step 1: public wrappers to expose existing copy/paste logic

        public void CopySelectedSection()
        {
            CopySelectedShape();
            Debug.WriteLine("[EditableSalesFloor] Copy buffer set? " + (_copyBuffer != null));
        }
        public async Task PasteCopiedSectionAsync()
        {
            Debug.WriteLine("[EditableSalesFloor] Paste requested, buffer set? " + (_copyBuffer != null));
            await PasteCopiedShapeAsync();
            Debug.WriteLine("[EditableSalesFloor] Paste: AddShapeToCanvasAsync() called.");
        }


        // Optional convenience for UI to enable/disable "Paste"
        public bool CanPasteSection => _copyBuffer != null;

        // Select an element (shape or button-wrapped shape) from a context-click
        public void SelectElementForContext(FrameworkElement element)
        {
            // If it’s a Button wrapping a Shape, unwrap it
            if (element is Button btn && btn.Content is Shape wrapped) element = wrapped;

            var shape = element as Shape;
            if (shape == null) return;

            // Remove highlight from previous selection
            if (_selectedShape != null)
            {
                _selectedShape.Stroke = Brushes.Black;
                _selectedShape.StrokeThickness = 2;
            }

            // Set & highlight new selection
            _selectedShape = shape;
            _selectedShape.Stroke = Brushes.DeepSkyBlue;
            _selectedShape.StrokeThickness = 3;

            // Ensure we’re not starting a drag from a right-click
            _isDragging = false;
        }


        private void Help_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = new HelpWindow
            {
                Owner = Window.GetWindow(this),
                Topmost = true
            };
            helpWindow.ShowDialog();
        }

        private void CopySelectedShape()
        {
            if (_selectedShape != null)
            {
                double rotation = 0;
                if (_selectedShape.RenderTransform is TransformGroup tg)
                {
                    var rt = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                    if (rt != null) rotation = rt.Angle;
                }

                _copyBuffer = new SectionClipboard
                {
                    ShapeType = GetShapeTypeFromShape(_selectedShape),
                    Width = _selectedShape.Width,
                    Height = _selectedShape.Height,
                    Rotation = rotation
                };
            }
        }

        private async Task PasteCopiedShapeAsync()
        {
            if (_copyBuffer != null && _sectionManager != null)
            {
                double x = 100, y = 100;

                FrameworkElement host = (_selectedShape?.Parent as FrameworkElement) ?? (FrameworkElement)_selectedShape;
                if (host != null)
                {
                    var left = Canvas.GetLeft(host);
                    var top = Canvas.GetTop(host);
                    if (!double.IsNaN(left)) x = left + 20;
                    if (!double.IsNaN(top)) y = top + 20;
                }

                await _sectionManager.AddShapeToCanvasAsync(
                    shapeName: _copyBuffer.ShapeType,
                    shapeType: _copyBuffer.ShapeType,
                    x: x, y: y,
                    width: _copyBuffer.Width,
                    height: _copyBuffer.Height,
                    rotation: _copyBuffer.Rotation,
                    existingSectionId: null, // ensures new UUID
                    wrapAsButton: true
                );
            }
        }


        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedShape();
        }

        private async void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await PasteCopiedShapeAsync();
        }

        #region Context Menu Setup

        private void InitializePerimeterContextMenu()
        {
            _perimeterContextMenu = new ContextMenu();

            MenuItem drawItem = new MenuItem { Header = "Draw" };
            drawItem.Click += DrawPerimeter_Click;

            MenuItem undoItem = new MenuItem { Header = "Undo" };
            undoItem.Click += UndoPerimeter_Click;

            MenuItem clearItem = new MenuItem { Header = "Clear" };
            clearItem.Click += ClearPerimeter_Click;

            _perimeterContextMenu.Items.Add(drawItem);
            _perimeterContextMenu.Items.Add(undoItem);
            _perimeterContextMenu.Items.Add(clearItem);
        }

        #endregion

        #region Helper Methods

        private Ellipse CreateDot(Point position, Brush fill)
        {
            var dot = new Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                Fill = fill
            };
            Canvas.SetLeft(dot, position.X - DotRadius);
            Canvas.SetTop(dot, position.Y - DotRadius);
            return dot;
        }

        private Point SnapToGrid(Point rawPoint)
        {
            double snappedX = Math.Round(rawPoint.X / GridSpacing) * GridSpacing;
            double snappedY = Math.Round(rawPoint.Y / GridSpacing) * GridSpacing;
            return new Point(snappedX, snappedY);
        }

        private bool IsCloseToFirstPerimeter(Point newPoint)
        {
            return _perimeterPoints.Any() && (newPoint - _perimeterPoints.First()).Length < CloseTolerance;
        }

        private bool IsCloseToFirstPartition(Point newPoint)
        {
            return _partitionPoints.Any() && (newPoint - _partitionPoints.First()).Length < CloseTolerance;
        }

        private void DrawGrid()
        {
            double width = SalesFloorCanvas.ActualWidth;
            double height = SalesFloorCanvas.ActualHeight;

            // Use theme-aware color:
            Brush gridBrush = (Brush)Application.Current.Resources["GridLineColor"]
                               ?? new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00)); // default

            // Remove any old grid lines first
            var oldLines = SalesFloorCanvas.Children.OfType<Line>()
                            .Where(l => l.Tag as string == "GridLine")
                            .ToList();

            foreach (var line in oldLines)
                SalesFloorCanvas.Children.Remove(line);

            // Redraw grid
            for (int x = 0; x < width; x += GridSpacing)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = gridBrush,
                    StrokeThickness = 0.7,
                    SnapsToDevicePixels = true,
                    Tag = "GridLine"
                };
                SalesFloorCanvas.Children.Add(line);
            }

            for (int y = 0; y < height; y += GridSpacing)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.7,
                    SnapsToDevicePixels = true,
                    Tag = "GridLine"
                };
                SalesFloorCanvas.Children.Add(line);
            }
        }


        #endregion

        #region Event Handlers

        private void EditFloor_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = !_isEditMode;
            _sectionManager.IsEditMode = _isEditMode;
            EditToolsPanel.Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed;
            EditModeButton.Content = _isEditMode ? "Disable Edit Mode" : "Enable Edit Mode";
        }


        private void EditableSalesFloor_Unloaded(object sender, RoutedEventArgs e)
        {
            ClearSectionsFromCanvas();
            Debug.WriteLine("[EditableSalesFloor] Canvas cleared upon exit.");
        }





        private void ManageProducts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Opening Manage Products window...");
        }

        private void Filters_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Applying filters...");
        }




        public async void ShowAutoSaved()
        {
            int version = ++_saveFlashVersion;
            SaveStatusText.Opacity = 1;
            await Task.Delay(1500);
            if (version == _saveFlashVersion)
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
                SaveStatusText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private void ExitEditMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isDrawingPartition)
                FloorDesign_Click(null, null);

            _isEditMode = false;
            _isEditNavVisible = false;
            _sectionManager.IsEditMode = false;
            EditToolsPanel.Visibility = Visibility.Collapsed;
            EditModeButton.Content = "Enable Edit Mode";
        }



        private async void ToggleSectionsPanel_Click(object sender, RoutedEventArgs e)
        {

            _isSectionsPanelVisible = !_isSectionsPanelVisible;
            SectionsPanel.Visibility = _isSectionsPanelVisible ? Visibility.Visible : Visibility.Collapsed;

        }

        private void AddSquare_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adding a Square section...");
        }

        private void AddCircle_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adding a Circle section...");
        }

        private void AddTriangle_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adding a Triangle section...");
        }

        private void AddCross_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adding a Cross section...");
        }

        private void DrawCustomSection_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Entering Custom Drawing Mode...");
        }

        private void AddSection_Click(object sender, RoutedEventArgs e)
        {
            var addSectionsWindow = new AddSectionsWindow
            {
                Owner = Window.GetWindow(this),
                Topmost = true
            };
            addSectionsWindow.Show();
            Application.Current.MainWindow?.Focus();
        }

        private void RemoveSection_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Remove Section tool activated.");
        }

        private void MoveSection_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Move Section tool activated.");
        }

        private void PerimeterButton_Click(object sender, RoutedEventArgs e)
        {
            PerimeterButton.ContextMenu = _perimeterContextMenu;
            _perimeterContextMenu.PlacementTarget = PerimeterButton;
            _perimeterContextMenu.IsOpen = true;
        }

        private void DrawPerimeter_Click(object sender, RoutedEventArgs e)
        {
            _isDrawingPartition = false;
            _isDrawingPerimeter = !_isDrawingPerimeter;

            if (_isDrawingPerimeter)
            {
                _perimeterPoints.Clear();
                _perimeterDots.Clear();
                _perimeterLine.Points.Clear();
                SalesFloorCanvas.Cursor = Cursors.Cross;
            }
            else
            {
                SalesFloorCanvas.Cursor = Cursors.Arrow;
            }
        }

        private void UndoPerimeter_Click(object sender, RoutedEventArgs e)
        {
            // Trigger partial undo while drawing.
            UndoPerimeterPoint();
        }

        private Partition RemovePartitionAssociatedWithShape(Shape shape)
        {
            if (shape is Polyline polyline)
            {
                var partitionToRemove = _partitions.FirstOrDefault(p =>
                    p.Points.Count == polyline.Points.Count &&
                    p.Points.Zip(polyline.Points, (p1, p2) => (p1 - p2).Length < 1.0).All(x => x)
                );

                if (partitionToRemove != null)
                {
                    _partitions.Remove(partitionToRemove);
                    return partitionToRemove; // 🛠 Return the removed partition
                }
            }
            return null; // 🛠 Return null if no matching partition found
        }



        private async void EditableSalesFloor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isEditMode) return;

            // ----- COPY (Ctrl+C) -----
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopySelectedShape();   // 🔹 Call helper instead of inline logic
                e.Handled = true;
                return;
            }

            // ----- PASTE (Ctrl+V) -----
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                await PasteCopiedShapeAsync();  // 🔹 Call helper
                e.Handled = true;
                return;
            }

            // ----- UNDO (Ctrl+Z) -----
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // 1) Partial undo if actively drawing perimeter
                if (_isDrawingPerimeter && _perimeterPoints.Any())
                {
                    UndoPerimeterPoint();
                }
                // 2) Partial undo if actively drawing partition
                else if (_isDrawingPartition && _partitionPoints.Any())
                {
                    UndoPartitionPoint();
                }
                // 3) Otherwise, undo the last finalized shape
                else if (_finalizedShapes.Any())
                {
                    UndoLastFinalizedShape();
                }
                e.Handled = true;
                return;
            }

            // ----- DELETE (Delete key) -----
            if (e.Key == Key.Delete && _selectedShape != null)
            {
                // Remove from Canvas
                SalesFloorCanvas.Children.Remove(_selectedShape);

                if (_selectedShape.Tag?.ToString() == "Partition")
                {
                    var removedPartition = RemovePartitionAssociatedWithShape(_selectedShape);

                    if (removedPartition != null)
                    {
                        _partitions.Remove(removedPartition);

                        if (!string.IsNullOrEmpty(removedPartition.Id))
                        {
                            if (!_deletedPartitionIds.Contains(removedPartition.Id))
                                _deletedPartitionIds.Add(removedPartition.Id);

                            await CanvasService.DeletePartitionAsync(removedPartition.Id);
                            ShowAutoSaved();
                            Debug.WriteLine($"[EditableSalesFloor] Partition {removedPartition.Id} deleted via Delete key.");
                        }
                    }
                }
                else if (_selectedShape.Tag?.ToString() == "SectionButton")
                {
                    // Future: handle section deletion if needed
                }

                _selectedShape = null;
                e.Handled = true;
                return;
            }
        }



        private async void EditableSalesFloor_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // Finalize partition when Shift is released
            if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && _isDrawingPartition)
            {
                if (_partitionPoints.Any())
                {
                    // Create a new Partition from the current drawing
                    var partition = new Partition();
                    partition.Points.AddRange(_partitionPoints);
                    _partitions.Add(partition);
                }

                // Finalize the partition drawing by pushing to the undo stack.
                var finalized = new FinalizedShape();
                finalized.Elements.Add(_partitionLine);
                finalized.Elements.AddRange(_partitionDots);
                _finalizedShapes.Push(finalized);

                // Clear the local drawing state for partition.
                _partitionPoints.Clear();
                _partitionDots.Clear();

                // Create a fresh Polyline for future partition drawing.
                _partitionLine = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                SalesFloorCanvas.Children.Add(_partitionLine);
                SalesFloorCanvas.Cursor = Cursors.Cross;

                // Save partitions to the database.
                await CanvasService.SavePartitionsAsync(_partitions, new List<string>());
                ShowAutoSaved();
            }
        }

        private void SalesFloorCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            Point clickedPoint = SnapToGrid(e.GetPosition(SalesFloorCanvas));

            // Clear selection if clicking on empty space
            var clickedElement = e.OriginalSource as FrameworkElement;
            if (clickedElement == SalesFloorCanvas)
            {
                _sectionManager.ClearSelection();
            }

            if (_isDrawingPerimeter)
                HandlePerimeterDrawing(clickedPoint);
            else if (_isDrawingPartition)
                HandlePartitionDrawing(clickedPoint);
        }


        // NEW or Modified: Shape event handlers to support double-click editing and dragging.
        public void Shape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            Shape shape = null;
            Button button = null;

            if (sender is Button btn && btn.Content is Shape wrappedShape)
            {
                button = btn;          // Keep the Button for editing
                shape = wrappedShape;  // Still track the shape for highlighting
            }
            else if (sender is Shape s)
            {
                shape = s;

                // Check if this Shape is inside a Button (its Parent might be a Button)
                if (s.Parent is Button parentBtn)
                {
                    button = parentBtn;
                }
            }

            if (shape == null) return;

            // Handle double-click
            if (e.ClickCount == 2)
            {
                if (shape.Tag?.ToString() == "SectionButton")
                {
                    // Always pass the Button (or fallback to the shape if no button exists)
                    var shapeElement = (button != null) ? (FrameworkElement)button : shape;

                    var editWindow = new AddSectionsWindow(shapeElement)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    editWindow.Show();
                }
                return;
            }

            // Deselect the previous shape (remove highlight)
            if (_selectedShape != null)
            {
                _selectedShape.Stroke = Brushes.Black;
                _selectedShape.StrokeThickness = 2;
            }

            // Set the new selected shape
            _selectedShape = shape;

            // Highlight the new selected shape
            _selectedShape.Stroke = Brushes.DeepSkyBlue;
            _selectedShape.StrokeThickness = 3;

            // Enable dragging only for Section Buttons
            if (_selectedShape.Tag?.ToString() == "SectionButton")
            {
                _dragStart = e.GetPosition(SalesFloorCanvas);

                _dragTransform = _selectedShape.RenderTransform as TranslateTransform;
                if (_dragTransform == null)
                {
                    _dragTransform = new TranslateTransform();
                    _selectedShape.RenderTransform = _dragTransform;
                }

                _isDragging = true;
                _selectedShape.CaptureMouse();
            }
            else
            {
                _isDragging = false;
            }
        }





        public void Shape_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _selectedShape != null)
            {
                Point currentPos = e.GetPosition(SalesFloorCanvas);
                double offsetX = currentPos.X - _dragStart.X;
                double offsetY = currentPos.Y - _dragStart.Y;
                _dragTransform.X += offsetX;
                _dragTransform.Y += offsetY;
                _dragStart = currentPos;
            }
        }

        public void Shape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _selectedShape?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Drawing Logic

        private void HandlePerimeterDrawing(Point clickedPoint)
        {

            _perimeterPoints.Add(clickedPoint);
            _perimeterLine.Points.Add(clickedPoint);

            var dot = CreateDot(clickedPoint, Brushes.Red);
            SalesFloorCanvas.Children.Add(dot);
            _perimeterDots.Add(dot);

            // If the user closes the perimeter (clicking near the first point):
            if (_perimeterPoints.Count > 2 && IsCloseToFirstPerimeter(clickedPoint))
            {
                // Close the shape visually.
                _perimeterLine.Points.Add(_perimeterPoints.First());
                _isDrawingPerimeter = false;
                SalesFloorCanvas.Cursor = Cursors.Arrow;

                _ = SavePerimeterToDatabaseAsync();
                ShowAutoSaved();

                // Finalize the perimeter shape.
                var finalized = new FinalizedShape();
                finalized.Elements.Add(_perimeterLine);
                finalized.Elements.AddRange(_perimeterDots);
                _finalizedShapes.Push(finalized);

                // Clear local lists so partial undo no longer applies.
                _perimeterPoints.Clear();
                _perimeterDots.Clear();

                // Optionally, create a new perimeter line for additional perimeters.
                _perimeterLine = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 2
                };
                SalesFloorCanvas.Children.Add(_perimeterLine);
            }
        }

        private void HandlePartitionDrawing(Point clickedPoint)
        {

            bool multiPoint = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var dot = CreateDot(clickedPoint, Brushes.Blue);

            if (multiPoint)
            {
                _partitionPoints.Add(clickedPoint);
                _partitionLine.Points.Add(clickedPoint);
                SalesFloorCanvas.Children.Add(dot);
                _partitionDots.Add(dot);

                // Optionally, if the user clicks near the first point while holding Shift, you could close the shape.
                if (_partitionPoints.Count > 2 && IsCloseToFirstPartition(clickedPoint))
                {
                    _partitionLine.Points.Add(_partitionPoints.First());
                }
            }
            else
            {
                if (_partitionPoints.Count == 0)
                {
                    _partitionPoints.Add(clickedPoint);
                    _partitionLine.Points.Add(clickedPoint);
                    SalesFloorCanvas.Children.Add(dot);
                    _partitionDots.Add(dot);
                }
                else if (_partitionPoints.Count == 1)
                {
                    _partitionPoints.Add(clickedPoint);
                    _partitionLine.Points.Add(clickedPoint);
                    SalesFloorCanvas.Children.Add(dot);
                    _partitionDots.Add(dot);

                    // Immediately finalize the line after 2 points if desired.
                    _partitionPoints.Clear();
                    _partitionDots.Clear();

                    var finalized = new FinalizedShape();
                    finalized.Elements.Add(_partitionLine);
                    finalized.Elements.Add(dot);
                    _finalizedShapes.Push(finalized);

                    // Create a new partition line for future drawing
                    _partitionLine = new Polyline
                    {
                        Stroke = Brushes.Black,
                        StrokeThickness = 2
                    };

                    // 🆕 ADD these two lines to make partitions selectable:
                    _partitionLine.Tag = "Partition";
                    _partitionLine.MouseLeftButtonDown += Shape_MouseLeftButtonDown;

                    SalesFloorCanvas.Children.Add(_partitionLine);
                }
            }
        }


        // Partial Undo for Perimeter
        private void UndoPerimeterPoint()
        {
            if (_perimeterPoints.Any())
            {
                _perimeterPoints.RemoveAt(_perimeterPoints.Count - 1);
                _perimeterLine.Points.RemoveAt(_perimeterLine.Points.Count - 1);

                if (_perimeterDots.Any())
                {
                    SalesFloorCanvas.Children.Remove(_perimeterDots.Last());
                    _perimeterDots.RemoveAt(_perimeterDots.Count - 1);
                }
            }
        }

        // Partial Undo for Partition
        private void UndoPartitionPoint()
        {
            if (_partitionPoints.Any())
            {
                _partitionPoints.RemoveAt(_partitionPoints.Count - 1);
                _partitionLine.Points.RemoveAt(_partitionLine.Points.Count - 1);

                if (_partitionDots.Any())
                {
                    SalesFloorCanvas.Children.Remove(_partitionDots.Last());
                    _partitionDots.RemoveAt(_partitionDots.Count - 1);
                }
            }
        }

        // Full Undo for the last finalized shape
        private async void UndoLastFinalizedShape()
        {
            if (!_finalizedShapes.Any()) return;

            var shapeGroup = _finalizedShapes.Pop();

            var partitionLine = shapeGroup.Elements
                                           .OfType<Polyline>()
                                           .FirstOrDefault(l => l.Tag?.ToString() == "Partition");

            if (partitionLine != null)
            {
                var partitionToRemove = _partitions.FirstOrDefault(p =>
                    p.Points.Count == partitionLine.Points.Count &&
                    p.Points.Zip(partitionLine.Points, (p1, p2) => (p1 - p2).Length < 1.0).All(x => x));

                if (partitionToRemove != null)
                {
                    _partitions.Remove(partitionToRemove);

                    if (!string.IsNullOrEmpty(partitionToRemove.Id))
                    {
                        _deletedPartitionIds.Add(partitionToRemove.Id);
                        await CanvasService.DeletePartitionAsync(partitionToRemove.Id);
                        Debug.WriteLine($"[EditableSalesFloor] Partition {partitionToRemove.Id} deleted via Ctrl+Z.");
                    }
                }
            }

            foreach (var element in shapeGroup.Elements)
            {
                SalesFloorCanvas.Children.Remove(element);
            }
        }




        private async void ClearPerimeter_Click(object sender, RoutedEventArgs e)
        {

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to clear the entire sales floor layout?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ClearPerimeter();
                await CanvasService.SavePerimeterAsync(_perimeterPoints);
            }
        }

        private void ClearPerimeter()
        {

            _perimeterPoints.Clear();
            _perimeterLine.Points.Clear();

            foreach (var dot in _perimeterDots)
                SalesFloorCanvas.Children.Remove(dot);

            _perimeterDots.Clear();
        }

        #endregion

        #region Database Interaction

        private async Task SavePerimeterToDatabaseAsync()
        {
            await CanvasService.SavePerimeterAsync(_perimeterPoints);
        }

        private async Task LoadPerimeterFromDatabaseAsync()
        {
            List<Point> loadedPoints = await CanvasService.LoadPerimeterAsync();
            _perimeterPoints.Clear();
            _perimeterLine.Points.Clear();

            foreach (Point pt in loadedPoints)
            {
                _perimeterPoints.Add(pt);
                _perimeterLine.Points.Add(pt);
            }
        }

        private void ClearSectionsFromCanvas()
        {
            _isClearingCanvas = true;

            // Remove all children from the canvas
            SalesFloorCanvas.Children.Clear();

            _isClearingCanvas = false;
        }



        private async Task LoadSectionsFromDatabaseAsync()
        {

            var sections = await CanvasService.LoadSectionsAsync();
            Debug.WriteLine($"[EditableSalesFloor] Loading {sections.Count} sections from the database.");

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
                    wrapAsButton: false);
            }
        }


        private async Task LoadPartitionsFromDatabaseAsync()
        {
            List<Partition> loadedPartitions = await CanvasService.LoadPartitionsAsync();

            foreach (var partition in loadedPartitions)
            {
                var partitionLine = new Polyline
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                foreach (var pt in partition.Points)
                {
                    partitionLine.Points.Add(pt);
                }

                // 🆕 Tag and wire up click to make loaded partitions selectable:
                partitionLine.Tag = "Partition";
                partitionLine.MouseLeftButtonDown += Shape_MouseLeftButtonDown;


                SalesFloorCanvas.Children.Add(partitionLine);
                _partitions.Add(partition);
            }
        }


        #endregion

        #region Floor Design Mode

        private void FloorDesign_Click(object sender, RoutedEventArgs e)
        {
            _isDrawingPerimeter = false;
            _isDrawingPartition = !_isDrawingPartition;


            if (_isDrawingPartition)
            {
                _partitionPoints.Clear();
                _partitionDots.Clear();
                _partitionLine.Points.Clear();

                if (!SalesFloorCanvas.Children.Contains(_partitionLine))
                    SalesFloorCanvas.Children.Add(_partitionLine);

                SalesFloorCanvas.Cursor = Cursors.Cross;
            }
            else
            {
                SalesFloorCanvas.Cursor = Cursors.Arrow;
            }
        }

        #endregion

        #region Async Shape Addition

        public async Task AddShapeToCanvasAsync(
            string name,
            double x = 100,
            double y = 100,
            double width = 50,
            double height = 50,
            double rotation = 0,
            string existingSectionId = null,
            string shapeType = null,
            bool wrapAsButton = false)
        {
            string finalShapeType = shapeType ?? name;
            await _sectionManager.AddShapeToCanvasAsync(
                shapeName: name,
                shapeType: finalShapeType,
                x: x,
                y: y,
                width: width,
                height: height,
                rotation: rotation,
                existingSectionId: existingSectionId,
                wrapAsButton: wrapAsButton);
        }

        #endregion
    }
}