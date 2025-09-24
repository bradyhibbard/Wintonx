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
        public string SelectedShape { get; private set; } = string.Empty;
        private double shapeWidth = 25;
        private double shapeHeight = 25;
        private double rotationAngle = 0;
        private Shape previewShape;
        private Shape _selectedShape;

        public AddSectionsWindow(FrameworkElement selectedElement = null)
        {
            InitializeComponent();

            if (selectedElement is Shape shape)
            {
                _selectedShape = shape;
                InitializeValues();
            }
            else if (selectedElement is Button button && button.Content is Shape shapeInside)
            {
                _selectedShape = shapeInside;
                InitializeValues();
            }
            else
            {
                InitializePreview();
            }
        }

        private void InitializePreview()
        {
            previewShape = new Rectangle
            {
                Width = shapeWidth,
                Height = shapeHeight,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            PreviewCanvas.Children.Add(previewShape);
        }

        private void InitializeValues()
        {
            if (_selectedShape != null)
            {
                shapeWidth = _selectedShape.Width;
                shapeHeight = _selectedShape.Height;
                rotationAngle = (_selectedShape.RenderTransform as RotateTransform)?.Angle ?? 0;

                WidthBox.Text = shapeWidth.ToString("F0");
                HeightBox.Text = shapeHeight.ToString("F0");
                RotationBox.Text = rotationAngle.ToString("F0");
            }
        }

        private void UpdatePreview()
        {
            if (previewShape == null) return;

            previewShape.Width = shapeWidth;
            previewShape.Height = shapeHeight;
            previewShape.RenderTransform = new RotateTransform(rotationAngle, shapeWidth / 2, shapeHeight / 2);
        }

        private void AddSquare_Click(object sender, RoutedEventArgs e) => SelectShape("Square");
        private void AddCircle_Click(object sender, RoutedEventArgs e) => SelectShape("Circle");
        private void AddTriangle_Click(object sender, RoutedEventArgs e) => SelectShape("Triangle");

        private void SelectShape(string shapeType)
        {
            SelectedShape = shapeType;
            UpdatePreviewShape();
        }

        private void UpdatePreviewShape()
        {
            PreviewCanvas.Children.Clear();
            switch (SelectedShape)
            {
                case "Square":
                    previewShape = new Rectangle
                    {
                        Width = shapeWidth,
                        Height = shapeHeight,
                        Stroke = Brushes.White,
                        Fill = Brushes.Transparent,
                        StrokeThickness = 1
                    };
                    break;
                case "Circle":
                    previewShape = new Ellipse
                    {
                        Width = shapeWidth,
                        Height = shapeHeight,
                        Stroke = Brushes.White,
                        Fill = Brushes.Transparent,
                        StrokeThickness = 1
                    };
                    break;
                case "Triangle":
                    previewShape = CreateTriangle();
                    break;
            }
            UpdatePreview();
            PreviewCanvas.Children.Add(previewShape);
        }

        private Polygon CreateTriangle()
        {
            return new Polygon
            {
                Points = new PointCollection
                {
                    new Point(shapeWidth / 2, 0),
                    new Point(shapeWidth, shapeHeight),
                    new Point(0, shapeHeight)
                },
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
        }

        // --- Numeric Entry Handling ---
        private void WidthBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(WidthBox.Text, out var val))
            {
                shapeWidth = val;
                if (_selectedShape != null)
                {
                    _selectedShape.Width = val;
                    UpdateRotationPivot();
                }
                UpdatePreview();
            }
            else
            {
                WidthBox.Text = shapeWidth.ToString("F0");
            }
        }

        private void HeightBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(HeightBox.Text, out var val))
            {
                shapeHeight = val;
                if (_selectedShape != null)
                {
                    _selectedShape.Height = val;
                    UpdateRotationPivot();
                }
                UpdatePreview();
            }
            else
            {
                HeightBox.Text = shapeHeight.ToString("F0");
            }
        }

        private void RotationBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(RotationBox.Text, out var val))
            {
                rotationAngle = val;
                if (_selectedShape != null)
                {
                    var parentButton = _selectedShape.Parent as Button;
                    FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;
                    targetElement.RenderTransform = new RotateTransform(rotationAngle, _selectedShape.Width / 2, _selectedShape.Height / 2);
                }
                UpdatePreview();
            }
            else
            {
                RotationBox.Text = rotationAngle.ToString("F0");
            }
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow digits, decimal point, and minus sign
            e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.' || c == '-');
        }

        // --- Rotation pivot update after resize ---
        private void UpdateRotationPivot()
        {
            if (_selectedShape != null)
            {
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                double angle = 0;
                if (targetElement.RenderTransform is RotateTransform rotateTransform)
                {
                    angle = rotateTransform.Angle;
                }

                var newRotate = new RotateTransform(angle, _selectedShape.Width / 2, _selectedShape.Height / 2);
                targetElement.RenderTransform = newRotate;

                targetElement.InvalidateVisual();
                targetElement.UpdateLayout();
            }
        }

        // --- Apply / Close ---
        private async void ApplyShape_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Owner is MainWindow mainWin)
                {
                    if (_selectedShape != null)
                    {
                        // Editing an existing section
                        string sectionId = _selectedShape.Tag as string;
                        double x = Canvas.GetLeft(_selectedShape);
                        double y = Canvas.GetTop(_selectedShape);
                        double newWidth = _selectedShape.Width;
                        double newHeight = _selectedShape.Height;

                        double rotation = 0;
                        if (_selectedShape.RenderTransform is RotateTransform rotate)
                        {
                            rotation = rotate.Angle;
                        }
                        else if (_selectedShape.RenderTransform is TransformGroup tg)
                        {
                            var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                            if (rotateTransform != null)
                                rotation = rotateTransform.Angle;
                        }

                        string currentName = _selectedShape?.Tag?.ToString();
                        string newName = !string.IsNullOrEmpty(SelectedShape) ? SelectedShape : currentName;

                        if (!string.IsNullOrEmpty(newName) && newName != currentName)
                        {
                            await CanvasService.UpdateSectionNameAsync(sectionId, newName);
                        }

                        await CanvasService.UpdateSectionDimensionsAsync(sectionId, x, y, newWidth, newHeight, rotation);

                        MessageBox.Show("Section updated successfully!");
                    }
                    else
                    {
                        // Creating a new section
                        string shapeType = SelectedShape;
                        if (string.IsNullOrEmpty(shapeType))
                        {
                            MessageBox.Show("Error: No shape type selected.");
                            return;
                        }

                        string sectionName = shapeType;

                        await mainWin.SalesFloorInstance.AddShapeToCanvasAsync(
                            name: sectionName,
                            shapeType: shapeType,
                            x: 100,
                            y: 100,
                            width: shapeWidth,
                            height: shapeHeight,
                            rotation: rotationAngle,
                            existingSectionId: null,
                            wrapAsButton: false
                        );
                    }

                    this.Close();
                }
                else
                {
                    Console.WriteLine("ERROR: Parent window not found!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ApplyShape_Click: {ex.Message}");
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
