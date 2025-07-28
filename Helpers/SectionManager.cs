using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Winton.Views;
using Winton.Services;

namespace Winton.Helpers
{
    public class SectionManager
    {
        private readonly Canvas _canvas;
        private FrameworkElement _selectedElement;
        private Point _mouseOffset;
        private readonly SectionInteraction _sectionInteraction = new SectionInteraction();
        private readonly UserControl _parentControl;

        public bool IsEditMode { get; set; } = false;


        // Event raised when a section is selected (clicked/double-clicked).
        public event Action<string> SectionSelected;

        public SectionManager(Canvas canvas, UserControl parentControl)
        {
            _canvas = canvas;
            _parentControl = parentControl;
        }

        public async Task AddShapeToCanvasAsync(
      string shapeName,
      string shapeType,
      double x = 100,
      double y = 100,
      double width = 50,
      double height = 50,
      double rotation = 0,
      string existingSectionId = null,
      bool wrapAsButton = false)
        {

            Shape newShape = null;

            // Use shapeName to determine which shape to create.
            switch (shapeType.ToLower())
            {
                case "square":
                    newShape = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        Fill = Brushes.Transparent
                    };
                    break;
                case "circle":
                    newShape = new Ellipse
                    {
                        Width = width,
                        Height = height,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        Fill = Brushes.Transparent
                    };
                    break;
                case "triangle":
                    newShape = CreateTriangle(width, height);
                    break;
                case "cross":
                    newShape = CreateCross(width, height);
                    break;
                    // Optionally, handle "Custom" or other shape types here.
            }

            if (newShape != null)
            {
                // Set position on the canvas.
                newShape.Tag = "SectionShape"; // Tag for identification
                Canvas.SetLeft(newShape, x);
                Canvas.SetTop(newShape, y);

                // Create a TransformGroup with a ScaleTransform and a RotateTransform.
                newShape.RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection
            {
                new ScaleTransform(1, 1),
                new RotateTransform(rotation, width / 2, height / 2)
            }
                };

                // Generate or reuse the SectionID.
                string sectionId = string.IsNullOrEmpty(existingSectionId)
                    ? Guid.NewGuid().ToString()
                    : existingSectionId;

                newShape.Tag = sectionId;

                if (wrapAsButton)
                {
                    // Wrap the shape in a Button.
                    Button sectionButton = new Button
                    {
                        Content = newShape,
                        Tag = sectionId,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        Width = width,
                        Height = height
                    };

                    // Apply clipping based on the shape.
                    if (shapeName == "Circle")
                    {
                        sectionButton.Clip = new EllipseGeometry(new Point(width / 2, height / 2), width / 2, height / 2);
                    }
                    else if (shapeName == "Triangle")
                    {
                        var figure = new System.Windows.Media.PathFigure { StartPoint = new Point(width / 2, 0) };
                        figure.Segments.Add(new LineSegment(new Point(width, height), true));
                        figure.Segments.Add(new LineSegment(new Point(0, height), true));
                        figure.IsClosed = true;
                        var geometry = new System.Windows.Media.PathGeometry();
                        geometry.Figures.Add(figure);
                        sectionButton.Clip = geometry;
                    }
                    else // default: square/rectangle
                    {
                        sectionButton.Clip = new RectangleGeometry(new Rect(0, 0, width, height));
                    }

                    // Attach unified event handlers to the Button.
                    // Attach the *EditableSalesFloor* event handlers instead of SectionManager handlers.
                    if (Application.Current.MainWindow?.Content is EditableSalesFloor editableSalesFloor)
                    {
                        sectionButton.MouseLeftButtonDown += editableSalesFloor.Shape_MouseLeftButtonDown;
                        sectionButton.MouseMove += editableSalesFloor.Shape_MouseMove;
                        sectionButton.MouseLeftButtonUp += editableSalesFloor.Shape_MouseLeftButtonUp;
                    }

                    // Set important Tag for selection/deletion logic
                    sectionButton.Tag = "SectionButton";


                    // Raise the SectionSelected event when the button is clicked.
                    sectionButton.Click += (s, e) =>
                    {
                        SectionSelected?.Invoke(sectionId);
                    };

                    _canvas.Children.Add(sectionButton);
                    Canvas.SetLeft(sectionButton, x);
                    Canvas.SetTop(sectionButton, y);
                }
                else
                {
                    // Attach unified event handlers to the raw shape.
                    newShape.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                    newShape.MouseMove += Element_MouseMove;
                    newShape.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                    newShape.MouseWheel += Element_MouseWheel;
                    newShape.MouseRightButtonDown += Element_RightClick;

                    _canvas.Children.Add(newShape);
                }

                // Only save to the database if this is a new section.
                if (string.IsNullOrEmpty(existingSectionId))
                {
                    // Save using 'shapeName' for the Name column and for the ShapeType column.
                    await CanvasService.SaveSectionAsync(sectionId, shapeName, x, y, width, height, rotation, shapeName);
                }
                Console.WriteLine($"Adding Shape: Name={shapeName}, ShapeType={shapeType}, X={x}, Y={y}, Width={width}, Height={height}");

            }
        }



        private Polygon CreateTriangle(double width, double height)
        {
            return new Polygon
            {
                Points = new PointCollection
                {
                    new Point(width / 2, 0),
                    new Point(width, height),
                    new Point(0, height)
                },
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
        }

        private Path CreateCross(double width, double height)
        {
            GeometryGroup crossGeometry = new GeometryGroup();
            crossGeometry.Children.Add(new LineGeometry(new Point(0, height / 2), new Point(width, height / 2)));
            crossGeometry.Children.Add(new LineGeometry(new Point(width / 2, 0), new Point(width / 2, height)));
            return new Path
            {
                Data = crossGeometry,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
        }

        private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEditMode) return;

            var newlyClickedElement = sender as FrameworkElement;

            // Log the clicked element for debugging
            Console.WriteLine($"Clicked Element: {newlyClickedElement?.Tag}");

            if (newlyClickedElement == null)
            {
                Console.WriteLine("No valid element selected.");
                return;
            }

            // 🛠 1. Clear highlight from previously selected shape
            if (_selectedElement is Shape previousShape)
            {
                previousShape.Stroke = Brushes.Black;
                previousShape.StrokeThickness = 1;
            }
            else if (_selectedElement is Button previousButton && previousButton.Content is Shape previousButtonShape)
            {
                previousButtonShape.Stroke = Brushes.Black;
                previousButtonShape.StrokeThickness = 1;
            }

            // 🛠 2. Update _selectedElement to the new one
            _selectedElement = newlyClickedElement;
            Console.WriteLine($"Newly Selected Element: {_selectedElement?.Tag}");

            _mouseOffset = e.GetPosition(_canvas);
            _mouseOffset.X -= Canvas.GetLeft(_selectedElement);
            _mouseOffset.Y -= Canvas.GetTop(_selectedElement);
            _selectedElement.CaptureMouse();

            // 🛠 3. Highlight the newly clicked shape
            if (_selectedElement is Shape selectedShape)
            {
                selectedShape.Stroke = Brushes.DeepSkyBlue;
                selectedShape.StrokeThickness = 3;
            }
            else if (_selectedElement is Button selectedButton && selectedButton.Content is Shape selectedButtonShape)
            {
                selectedButtonShape.Stroke = Brushes.DeepSkyBlue;
                selectedButtonShape.StrokeThickness = 3;
            }

            // ✨ Double-click to open the edit window
            if (e.ClickCount == 2)
            {
                Window mainWindow = Window.GetWindow(_canvas);

                Shape shapeToEdit = _selectedElement as Shape;
                if (_selectedElement is Button btn)
                {
                    shapeToEdit = btn.Content as Shape;
                }

                AddSectionsWindow editWindow = new AddSectionsWindow(shapeToEdit)
                {
                    Owner = mainWindow
                };
                editWindow.Show();
            }
        }




        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsEditMode) return;

            if (_selectedElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point newPosition = e.GetPosition(_canvas);
                Canvas.SetLeft(_selectedElement, newPosition.X - _mouseOffset.X);
                Canvas.SetTop(_selectedElement, newPosition.Y - _mouseOffset.Y);
            }
        }

        private async void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_selectedElement != null)
            {
                _selectedElement.ReleaseMouseCapture();

                double x = Canvas.GetLeft(_selectedElement);
                double y = Canvas.GetTop(_selectedElement);
                double width = _selectedElement.Width;
                double height = _selectedElement.Height;
                double rotation = 0;
                if (_selectedElement.RenderTransform is TransformGroup tg)
                {
                    var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                    if (rotateTransform != null)
                    {
                        rotation = rotateTransform.Angle;
                    }
                }

                string sectionId = _selectedElement?.Tag as string;
                if (!string.IsNullOrEmpty(sectionId))
                {

                    // Only set the move save flag for EditableSalesFloor
                    if (_parentControl is EditableSalesFloor editableSalesFloor)
                    {
                        editableSalesFloor._isMoveSave = true;
                    }

                    _canvas.Children.Remove(_selectedElement);

                    // Save the new position in the database
                    await CanvasService.UpdateSectionDimensionsAsync(sectionId, x, y, width, height, rotation);

                    // Re-add the element at the new position
                    // Optionally, use a method to re-render the section based on the database data
                    await ReRenderSection(sectionId);
                }

                _selectedElement = null;

            }
        }

        private async Task ReRenderSection(string sectionId)
        {
            var sections = await CanvasService.LoadSectionsAsync();
            var section = sections.FirstOrDefault(s => s.sectionId == sectionId);

            if (section != default)
            {
                await AddShapeToCanvasAsync(
                    shapeName: section.name,
                    shapeType: section.shapeType,
                    x: section.x,
                    y: section.y,
                    width: section.width,
                    height: section.height,
                    rotation: section.rotation,
                    existingSectionId: section.sectionId,
                    wrapAsButton: false);
            }
        }


        private void Element_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_selectedElement != null && _selectedElement.RenderTransform is TransformGroup tg)
            {
                ScaleTransform scaleTransform = tg.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    double scaleChange = e.Delta > 0 ? 1.1 : 0.9;
                    scaleTransform.ScaleX *= scaleChange;
                    scaleTransform.ScaleY *= scaleChange;
                }
            }
        }

        private void Element_RightClick(object sender, MouseButtonEventArgs e)
        {
            _selectedElement = sender as FrameworkElement;
            if (_selectedElement != null && e.RightButton == MouseButtonState.Pressed)
            {
                ContextMenu contextMenu = new ContextMenu();
                MenuItem deleteItem = new MenuItem { Header = "Delete Shape" };
                deleteItem.Click += async (s, args) =>
                {
                    if (_selectedElement == null) return;

                    string sectionId = _selectedElement.Tag as string;
                    if (string.IsNullOrEmpty(sectionId)) return;

                    var result = MessageBox.Show(
                        "Are you sure you want to delete this section? Products assigned to this section will be archived.",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // 🛠 Move products to Archive and delete Section
                            await ProductPlacementServices.ArchiveAndDeleteSectionAsync(sectionId);

                            // 🛠 Remove visual from canvas
                            _canvas.Children.Remove(_selectedElement);
                            _selectedElement = null;

                            MessageBox.Show("Section deleted and products archived successfully.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to delete section: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };

                contextMenu.Items.Add(deleteItem);
                _selectedElement.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }

        public void ClearSelection()
        {
            Console.WriteLine("ClearSelection called. Current _selectedElement: " + (_selectedElement?.Tag ?? "None"));

            if (_selectedElement != null)
            {
                if (_selectedElement is Shape shape)
                {
                    shape.Stroke = Brushes.Black;
                    shape.StrokeThickness = 1;
                }
                else if (_selectedElement is Button button && button.Content is Shape buttonShape)
                {
                    buttonShape.Stroke = Brushes.Black;
                    buttonShape.StrokeThickness = 1;
                }

                Console.WriteLine("Element unhighlighted: " + _selectedElement?.Tag);
                _selectedElement = null;
            }
        }


    }
}
