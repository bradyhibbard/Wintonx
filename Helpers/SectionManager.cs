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
            // 1) Build the shape by type
            Shape newShape = shapeType.ToLower() switch
            {
                "square" => new Rectangle { Width = width, Height = height, Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.Transparent },
                "circle" => new Ellipse { Width = width, Height = height, Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.Transparent },
                "triangle" => CreateTriangle(width, height),
                "cross" => CreateCross(width, height),
                _ => null
            };
            if (newShape == null) return;

            // 2) Base position (will apply to the *host* we add)
            Canvas.SetLeft(newShape, x);
            Canvas.SetTop(newShape, y);

            // 3) Compute SectionID
            string sectionId = string.IsNullOrEmpty(existingSectionId) ? Guid.NewGuid().ToString() : existingSectionId;

            // The element we actually add to the canvas (used for saving coords)
            FrameworkElement hostElement;

            if (wrapAsButton)
            {
                // Button (host) carries the GUID and the rotation so your save logic reads it correctly
                var sectionButton = new Button
                {
                    Content = newShape,
                    Tag = sectionId,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Width = width,
                    Height = height,
                    RenderTransform = new TransformGroup
                    {
                        Children = new TransformCollection
                {
                    new TranslateTransform(),
                    new ScaleTransform(1, 1),
                    new RotateTransform(rotation, width / 2, height / 2)
                }
                    }
                };

                // Inner shape is marked for your selection/drag checks in EditableSalesFloor
                newShape.Tag = "SectionButton";

                // Clip by shape name
                if (shapeName == "Circle")
                {
                    sectionButton.Clip = new EllipseGeometry(new Point(width / 2, height / 2), width / 2, height / 2);
                }
                else if (shapeName == "Triangle")
                {
                    var figure = new PathFigure { StartPoint = new Point(width / 2, 0) };
                    figure.Segments.Add(new LineSegment(new Point(width, height), true));
                    figure.Segments.Add(new LineSegment(new Point(0, height), true));
                    figure.IsClosed = true;
                    var geometry = new PathGeometry();
                    geometry.Figures.Add(figure);
                    sectionButton.Clip = geometry;
                }
                else
                {
                    sectionButton.Clip = new RectangleGeometry(new Rect(0, 0, width, height));
                }

                // IMPORTANT: use SectionManager's handlers so the *button* moves immediately after paste
                // Fire even if the Button consumes the event
                sectionButton.AddHandler(
                    UIElement.MouseLeftButtonDownEvent,
                    new MouseButtonEventHandler(Element_MouseLeftButtonDown),
                    /*handledEventsToo:*/ true);

                sectionButton.AddHandler(
                    UIElement.MouseMoveEvent,
                    new MouseEventHandler(Element_MouseMove),
                    /*handledEventsToo:*/ true);

                sectionButton.AddHandler(
                    UIElement.MouseLeftButtonUpEvent,
                    new MouseButtonEventHandler(Element_MouseLeftButtonUp),
                    /*handledEventsToo:*/ true);

                sectionButton.MouseWheel += Element_MouseWheel;
                sectionButton.MouseRightButtonDown += Element_RightClick;

                // Bubble selection event
                sectionButton.Click += (s, e) => { SectionSelected?.Invoke(sectionId); };

                // Add to canvas as host
                _canvas.Children.Add(sectionButton);
                Canvas.SetLeft(sectionButton, x);
                Canvas.SetTop(sectionButton, y);

                // Optional: auto-select the new button for instant drag/visual feedback
                if (_parentControl is EditableSalesFloor ef)
                {
                    ef.SelectElementForContext(sectionButton);
                    sectionButton.Focus();
                }

                hostElement = sectionButton;
            }
            else
            {
                // Raw shape carries GUID and its own rotation (as before)
                newShape.Tag = sectionId;
                newShape.RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection
            {
                new TranslateTransform(),
                new ScaleTransform(1, 1),
                new RotateTransform(rotation, width / 2, height / 2)
            }
                };

                newShape.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                newShape.MouseMove += Element_MouseMove;
                newShape.MouseLeftButtonUp += Element_MouseLeftButtonUp;
                newShape.MouseWheel += Element_MouseWheel;
                newShape.MouseRightButtonDown += Element_RightClick;

                _canvas.Children.Add(newShape);
                hostElement = newShape;
            }

            // 4) Persist only if creating a new section
            if (string.IsNullOrEmpty(existingSectionId))
            {
                double left = Canvas.GetLeft(hostElement);
                if (double.IsNaN(left)) left = x;

                double top = Canvas.GetTop(hostElement);
                if (double.IsNaN(top)) top = y;

                await CanvasService.SaveSectionAsync(
                    sectionId: sectionId,
                    name: shapeName,
                    x: left,
                    y: top,
                    width: width,
                    height: height,
                    rotation: rotation,
                    shapeType: shapeName
                );
            }

            Console.WriteLine($"Adding Shape: Name={shapeName}, ShapeType={shapeType}, X={x}, Y={y}, Width={width}, Height={height}");
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
                Fill = Brushes.Transparent,
                Width = width,
                Height = height
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
                var contextMenu = new ContextMenu();

                var ef = _parentControl as EditableSalesFloor;
                ef?.SelectElementForContext(_selectedElement);

                // COPY
                var copyItem = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
                copyItem.Click += (s2, a2) => ef?.CopySelectedSection();

                // PASTE (enabled dynamically)
                var pasteItem = new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V" };
                pasteItem.Click += async (s2, a2) =>
                {
                    if (ef == null) return;
                    if (!ef.CanPasteSection)
                    {
                        MessageBox.Show("Copy a section first.");
                        return;
                    }
                    await ef.PasteCopiedSectionAsync();
                };

                // OPTIONAL: Duplicate (single click does Copy then Paste)
                var duplicateItem = new MenuItem { Header = "Duplicate" };
                duplicateItem.Click += async (s2, a2) =>
                {
                    if (ef == null) return;
                    ef.CopySelectedSection();
                    if (ef.CanPasteSection)
                        await ef.PasteCopiedSectionAsync();
                };

                contextMenu.Items.Add(copyItem);
                contextMenu.Items.Add(pasteItem);
                contextMenu.Items.Add(duplicateItem); // optional

                // Enable/disable Paste dynamically when the menu opens
                contextMenu.Opened += (s2, a2) =>
                {
                    if (ef != null) pasteItem.IsEnabled = ef.CanPasteSection;
                };

                // --- existing Delete item ---
                var deleteItem = new MenuItem { Header = "Delete Shape" };
                deleteItem.Click += async (s, args) =>
                {
                    if (_selectedElement == null) return;

                    // Prefer Button.Tag (GUID) when wrapped; fallback to Shape.Tag when raw
                    var sectionId =
                        (_selectedElement as Button)?.Tag as string ??
                        (_selectedElement as Shape)?.Tag as string;

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
                            await ProductPlacementServices.ArchiveAndDeleteSectionAsync(sectionId);
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
                e.Handled = true;
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
