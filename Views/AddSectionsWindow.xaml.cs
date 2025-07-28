using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Winton.Services;

namespace Winton.Views
{
    public partial class AddSectionsWindow : Window
    {
        public string SelectedShape { get; private set; } = string.Empty;
        private double shapeWidth = 50;
        private double shapeHeight = 50;
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

                WidthSlider.Value = shapeWidth;
                HeightSlider.Value = shapeHeight;
                RotationSlider.Value = rotationAngle;
            }
        }

        private void UpdatePreview()
        {
            if (previewShape == null) return;

            previewShape.Width = shapeWidth;
            previewShape.Height = shapeHeight;
            previewShape.RenderTransform = new RotateTransform(rotationAngle, shapeWidth / 5, shapeHeight / 5);
        }

        private void AddSquare_Click(object sender, RoutedEventArgs e) => SelectShape("Square");
        private void AddCircle_Click(object sender, RoutedEventArgs e) => SelectShape("Circle");
        private void AddTriangle_Click(object sender, RoutedEventArgs e) => SelectShape("Triangle");
        private void AddCross_Click(object sender, RoutedEventArgs e) => SelectShape("Cross");
        private void DrawCustomSection_Click(object sender, RoutedEventArgs e) => SelectShape("Custom");

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
                case "Cross":
                    previewShape = CreateCross();
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

        private Path CreateCross()
        {
            GeometryGroup crossGeometry = new GeometryGroup();
            crossGeometry.Children.Add(new LineGeometry(new Point(0, shapeHeight / 2), new Point(shapeWidth, shapeHeight / 2)));
            crossGeometry.Children.Add(new LineGeometry(new Point(shapeWidth / 2, 0), new Point(shapeWidth / 2, shapeHeight)));
            return new Path { Data = crossGeometry, Stroke = Brushes.White, StrokeThickness = 1 };
        }


        private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedShape != null)
            {
                double newWidth = e.NewValue;

                // If the shape is inside a Button, adjust the Button as well
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Keep the center consistent while resizing
                double centerX = Canvas.GetLeft(targetElement) + (targetElement.Width / 2);

                _selectedShape.Width = newWidth;
                if (parentButton != null)
                    parentButton.Width = newWidth;

                Canvas.SetLeft(targetElement, centerX - (newWidth / 2));

                // Keep rotation pivot centered after resize
                UpdateRotationPivot();
            }
            else
            {
                shapeWidth = e.NewValue;
                UpdatePreview();
            }
        }

        private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedShape != null)
            {
                double newHeight = e.NewValue;

                // If the shape is inside a Button, adjust the Button as well
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Keep the center consistent while resizing
                double centerY = Canvas.GetTop(targetElement) + (targetElement.Height / 2);

                _selectedShape.Height = newHeight;
                if (parentButton != null)
                    parentButton.Height = newHeight;

                Canvas.SetTop(targetElement, centerY - (newHeight / 2));

                // Keep rotation pivot centered after resize
                UpdateRotationPivot();
            }
            else
            {
                shapeHeight = e.NewValue;
                UpdatePreview();
            }
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_selectedShape != null)
            {
                double angle = e.NewValue;

                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Always pivot around center of the current width/height
                var rotateTransform = new RotateTransform(angle, _selectedShape.Width / 2, _selectedShape.Height / 2);
                targetElement.RenderTransform = rotateTransform;

                targetElement.InvalidateVisual();
                targetElement.UpdateLayout();
            }
            else
            {
                rotationAngle = e.NewValue;
                UpdatePreview();
            }
        }



        // Helper to fix pivot dynamically after resizing
        private void UpdateRotationPivot()
        {
            if (_selectedShape != null)
            {
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Find current rotation (if any)
                double angle = 0;
                if (targetElement.RenderTransform is RotateTransform rotateTransform)
                {
                    angle = rotateTransform.Angle;
                }

                // Reapply rotation with updated center point (middle of the shape)
                var newRotate = new RotateTransform(angle, _selectedShape.Width / 2, _selectedShape.Height / 2);
                targetElement.RenderTransform = newRotate;

                targetElement.InvalidateVisual();
                targetElement.UpdateLayout();
            }
        }



        private void RotateLeft_Click(object sender, RoutedEventArgs e)
        {
            rotationAngle -= 90;
            UpdatePreview();
        }

        private void RotateRight_Click(object sender, RoutedEventArgs e)
        {
            rotationAngle += 90;
            UpdatePreview();
        }

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

                        // Handle rotation (works even if it's just a RotateTransform)
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

                        // Preserve the current name (only change if user picked a new type)
                        string currentName = _selectedShape?.Tag?.ToString();
                        string newName = !string.IsNullOrEmpty(SelectedShape) ? SelectedShape : currentName;

                        if (!string.IsNullOrEmpty(newName) && newName != currentName)
                        {
                            await CanvasService.UpdateSectionNameAsync(sectionId, newName);
                        }

                        // Always update size, position, and rotation
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
