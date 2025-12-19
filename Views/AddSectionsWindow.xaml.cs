using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Winton.Services;

namespace Winton.Views
{
    public partial class AddSectionsWindow : Window
    {
        // Public result
        public string SelectedShape { get; private set; } = "Square";

        // Current editable values
        private double _shapeWidth = 50;
        private double _shapeHeight = 50;
        private double _rotationAngle = 0;

        // Preview + optional selected existing shape
        private Shape _previewShape;
        private Shape _selectedShape;

        public AddSectionsWindow(FrameworkElement selectedElement = null)
        {
            InitializeComponent();

            // Determine if we're editing an existing section (shape inside canvas/button)
            _selectedShape = ResolveSelectedShape(selectedElement);

            if (_selectedShape != null)
            {
                // Editing existing: pull values from the selected shape
                InitializeValuesFromSelectedShape();
                SetSelectedShapeFromShapeType(_selectedShape);
            }
            else
            {
                // Creating new: default to Square
                SelectedShape = "Square";
                _shapeWidth = ParseOrFallback(WidthBox?.Text, 50);
                _shapeHeight = ParseOrFallback(HeightBox?.Text, 50);
                _rotationAngle = ParseOrFallback(RotationBox?.Text, 0);

                // Ensure radio selection reflects default
                if (SquareOption != null)
                    SquareOption.IsChecked = true;
            }

            // Build initial preview
            UpdatePreviewShape();
        }

        // ----------------------------
        // Shape selection (RadioButtons)
        // ----------------------------
        private void SquareOption_Checked(object sender, RoutedEventArgs e) => SelectShape("Square");
        private void CircleOption_Checked(object sender, RoutedEventArgs e) => SelectShape("Circle");
        private void TriangleOption_Checked(object sender, RoutedEventArgs e) => SelectShape("Triangle");

        private void SelectShape(string shapeType)
        {
            if (string.IsNullOrWhiteSpace(shapeType))
                return;

            SelectedShape = shapeType;
            UpdatePreviewShape();
        }

        // ----------------------------
        // Preview rendering
        // ----------------------------
        private void UpdatePreviewShape()
        {
            if (PreviewCanvas == null)
                return;

            PreviewCanvas.Children.Clear();

            _previewShape = SelectedShape switch
            {
                "Circle" => new Ellipse(),
                "Triangle" => CreateTriangle(),
                _ => new Rectangle(), // Square default
            };

            // Common style
            ApplyPreviewStyle(_previewShape);

            // Apply size + rotation
            ApplySizeAndRotation(_previewShape, _shapeWidth, _shapeHeight, _rotationAngle);

            // Center preview in the canvas (simple + consistent)
            CenterPreviewOnCanvas(_previewShape);

            PreviewCanvas.Children.Add(_previewShape);
        }

        private void ApplyPreviewStyle(Shape shape)
        {
            // Keep your original look: white outline, transparent fill
            shape.Stroke = Brushes.White;
            shape.StrokeThickness = 1;
            shape.Fill = Brushes.Transparent;
        }

        private void ApplySizeAndRotation(Shape shape, double width, double height, double angle)
        {
            shape.Width = width;
            shape.Height = height;

            // Rotate around center
            shape.RenderTransform = new RotateTransform(angle, width / 2, height / 2);
        }

        private void CenterPreviewOnCanvas(Shape shape)
        {
            // Canvas may not have ActualWidth yet (first load),
            // so we use a safe default centering point.
            double canvasW = PreviewCanvas.ActualWidth > 0 ? PreviewCanvas.ActualWidth : 100;
            double canvasH = PreviewCanvas.ActualHeight > 0 ? PreviewCanvas.ActualHeight : 100;

            double left = (canvasW - shape.Width) / 2;
            double top = (canvasH - shape.Height) / 2;

            Canvas.SetLeft(shape, left);
            Canvas.SetTop(shape, top);
        }

        private Polygon CreateTriangle()
        {
            // Points will be updated as width/height changes by recreating the polygon
            var poly = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(_shapeWidth / 2, 0),
                    new Point(_shapeWidth, _shapeHeight),
                    new Point(0, _shapeHeight)
                }
            };

            return poly;
        }

        // ----------------------------
        // Numeric entry handling
        // ----------------------------
        private void WidthBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(WidthBox.Text, out var val))
            {
                _shapeWidth = val;

                // If editing existing, apply to the existing shape too
                if (_selectedShape != null)
                {
                    _selectedShape.Width = val;
                    UpdateRotationPivotForSelected();
                }

                // Triangle needs a rebuild to update points
                UpdatePreviewShape();
            }
            else
            {
                WidthBox.Text = _shapeWidth.ToString("F0");
            }
        }

        private void HeightBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(HeightBox.Text, out var val))
            {
                _shapeHeight = val;

                if (_selectedShape != null)
                {
                    _selectedShape.Height = val;
                    UpdateRotationPivotForSelected();
                }

                UpdatePreviewShape();
            }
            else
            {
                HeightBox.Text = _shapeHeight.ToString("F0");
            }
        }

        private void RotationBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(RotationBox.Text, out var val))
            {
                _rotationAngle = val;

                if (_selectedShape != null)
                {
                    var target = GetRotationTargetElement(_selectedShape);
                    target.RenderTransform = new RotateTransform(_rotationAngle, _selectedShape.Width / 2, _selectedShape.Height / 2);
                }

                UpdatePreviewShape();
            }
            else
            {
                RotationBox.Text = _rotationAngle.ToString("F0");
            }
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits, decimal point, and minus sign
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.' || c == '-');
        }

        // ----------------------------
        // Rotation pivot update after resize (existing behavior, cleaned up)
        // ----------------------------
        private void UpdateRotationPivotForSelected()
        {
            if (_selectedShape == null)
                return;

            var target = GetRotationTargetElement(_selectedShape);

            double angle = 0;
            if (target.RenderTransform is RotateTransform rt)
            {
                angle = rt.Angle;
            }

            target.RenderTransform = new RotateTransform(angle, _selectedShape.Width / 2, _selectedShape.Height / 2);

            target.InvalidateVisual();
            target.UpdateLayout();
        }

        private FrameworkElement GetRotationTargetElement(Shape shape)
        {
            // If the shape is wrapped in a Button, rotate the wrapper (keeps click geometry stable)
            var parentButton = shape.Parent as Button;
            return parentButton as FrameworkElement ?? shape;
        }

        // ----------------------------
        // Apply / Close
        // ----------------------------
        private async void ApplyShape_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Owner is not MainWindow mainWin)
                {
                    Console.WriteLine("ERROR: Owner window not found.");
                    return;
                }

                if (_selectedShape != null)
                {
                    // Editing existing section
                    string sectionId = _selectedShape.Tag as string;
                    if (string.IsNullOrWhiteSpace(sectionId))
                    {
                        MessageBox.Show("Error: Selected section is missing an ID.");
                        return;
                    }

                    double x = Canvas.GetLeft(_selectedShape);
                    double y = Canvas.GetTop(_selectedShape);
                    double newWidth = _selectedShape.Width;
                    double newHeight = _selectedShape.Height;

                    double rotation = 0;
                    var target = GetRotationTargetElement(_selectedShape);

                    if (target.RenderTransform is RotateTransform rotate)
                    {
                        rotation = rotate.Angle;
                    }
                    else if (target.RenderTransform is TransformGroup tg)
                    {
                        var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                        if (rotateTransform != null)
                            rotation = rotateTransform.Angle;
                    }

                    // OPTIONAL: If you truly want "shape type" stored as the name, keep this.
                    // But do NOT overwrite name accidentally.
                    // Here we only update if SelectedShape is set and differs from the stored name.
                    // (Your old code was comparing against Tag which is sectionId, not name.) :contentReference[oaicite:2]{index=2}
                    var existing = (await CanvasService.LoadSectionsAsync())
                        .FirstOrDefault(s => s.sectionId == sectionId);

                    if (existing.sectionId != null)
                    {
                        string currentName = existing.name;
                        string newName = SelectedShape;

                        if (!string.IsNullOrWhiteSpace(newName) &&
                            !string.Equals(newName, currentName, StringComparison.OrdinalIgnoreCase))
                        {
                            await CanvasService.UpdateSectionNameAsync(sectionId, newName);
                        }
                    }

                    await CanvasService.UpdateSectionDimensionsAsync(sectionId, x, y, newWidth, newHeight, rotation);
                }
                else
                {
                    // Creating new section
                    string shapeType = SelectedShape;
                    if (string.IsNullOrWhiteSpace(shapeType))
                    {
                        MessageBox.Show("Error: No shape type selected.");
                        return;
                    }

                    await mainWin.SalesFloorInstance.AddShapeToCanvasAsync(
                        name: shapeType,
                        shapeType: shapeType,
                        x: 100,
                        y: 100,
                        width: _shapeWidth,
                        height: _shapeHeight,
                        rotation: _rotationAngle,
                        existingSectionId: null,
                        wrapAsButton: false
                    );
                }

                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ApplyShape_Click: {ex}");
                MessageBox.Show($"Apply failed: {ex.Message}");
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        // ----------------------------
        // Helpers
        // ----------------------------
        private Shape ResolveSelectedShape(FrameworkElement selectedElement)
        {
            if (selectedElement is Shape shape)
                return shape;

            if (selectedElement is Button button && button.Content is Shape shapeInside)
                return shapeInside;

            return null;
        }

        private void InitializeValuesFromSelectedShape()
        {
            _shapeWidth = _selectedShape.Width;
            _shapeHeight = _selectedShape.Height;

            // Grab rotation from either direct RotateTransform or TransformGroup
            var target = GetRotationTargetElement(_selectedShape);

            _rotationAngle = 0;
            if (target.RenderTransform is RotateTransform rt)
            {
                _rotationAngle = rt.Angle;
            }
            else if (target.RenderTransform is TransformGroup tg)
            {
                var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                if (rotateTransform != null)
                    _rotationAngle = rotateTransform.Angle;
            }

            WidthBox.Text = _shapeWidth.ToString("F0");
            HeightBox.Text = _shapeHeight.ToString("F0");
            RotationBox.Text = _rotationAngle.ToString("F0");
        }

        private void SetSelectedShapeFromShapeType(Shape shape)
        {
            // Infer type based on actual WPF shape type
            if (shape is Ellipse)
            {
                SelectedShape = "Circle";
                if (CircleOption != null) CircleOption.IsChecked = true;
            }
            else if (shape is Polygon)
            {
                SelectedShape = "Triangle";
                if (TriangleOption != null) TriangleOption.IsChecked = true;
            }
            else
            {
                SelectedShape = "Square";
                if (SquareOption != null) SquareOption.IsChecked = true;
            }
        }

        private double ParseOrFallback(string text, double fallback)
        {
            return double.TryParse(text, out var v) ? v : fallback;
        }
    }
}
