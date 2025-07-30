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
                rotationAngle = 0;

                if (_selectedShape.RenderTransform is RotateTransform rotate)
                    rotationAngle = rotate.Angle;
                else if (_selectedShape.RenderTransform is TransformGroup tg)
                {
                    var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                    if (rotateTransform != null)
                        rotationAngle = rotateTransform.Angle;
                }

                WidthSlider.Value = shapeWidth;
                HeightSlider.Value = shapeHeight;
                RotationSlider.Value = rotationAngle;

                // Build a clone for the PreviewCanvas so the user sees the current shape
                PreviewCanvas.Children.Clear();

                if (_selectedShape is Rectangle)
                    previewShape = new Rectangle();
                else if (_selectedShape is Ellipse)
                    previewShape = new Ellipse();
                else if (_selectedShape is Polygon poly)
                    previewShape = new Polygon { Points = new PointCollection(poly.Points) };
                else if (_selectedShape is Path path)
                    previewShape = new Path { Data = path.Data.Clone() };

                previewShape.Width = shapeWidth;
                previewShape.Height = shapeHeight;
                previewShape.Stroke = Brushes.White;
                previewShape.StrokeThickness = 1;
                previewShape.Fill = Brushes.Transparent;
                previewShape.RenderTransform = new RotateTransform(rotationAngle, shapeWidth / 2, shapeHeight / 2);

                PreviewCanvas.Children.Add(previewShape);
            }
            else
            {
                InitializePreview(); // For new shapes, keep old behavior
            }
        }


        private void UpdatePreview()
        {
            if (previewShape == null) return;

            // Update size
            previewShape.Width = shapeWidth;
            previewShape.Height = shapeHeight;

            // Center pivot for rotation relative to the shape's actual size
            double centerX = previewShape.Width / 2;
            double centerY = previewShape.Height / 2;

            previewShape.RenderTransform = new RotateTransform(rotationAngle, centerX, centerY);

            previewShape.InvalidateVisual(); // Force a redraw
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
            shapeWidth = e.NewValue; // Always keep shapeWidth in sync

            if (_selectedShape != null)
            {
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Keep center position consistent while resizing
                double centerX = Canvas.GetLeft(targetElement) + (targetElement.Width / 2);

                targetElement.Width = shapeWidth;
                if (parentButton != null)
                    parentButton.Width = shapeWidth;

                Canvas.SetLeft(targetElement, centerX - (shapeWidth / 2));

                UpdateRotationPivot();
            }

            UpdatePreview();
        }

        private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            shapeHeight = e.NewValue; // Always keep shapeHeight in sync

            if (_selectedShape != null)
            {
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Keep center position consistent while resizing
                double centerY = Canvas.GetTop(targetElement) + (targetElement.Height / 2);

                targetElement.Height = shapeHeight;
                if (parentButton != null)
                    parentButton.Height = shapeHeight;

                Canvas.SetTop(targetElement, centerY - (shapeHeight / 2));

                UpdateRotationPivot();
            }

            UpdatePreview();
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            rotationAngle = e.NewValue; // Always keep rotationAngle in sync

            if (_selectedShape != null)
            {
                var parentButton = _selectedShape.Parent as Button;
                FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                // Always pivot rotation around center
                var rotateTransform = new RotateTransform(rotationAngle, targetElement.Width / 2, targetElement.Height / 2);
                targetElement.RenderTransform = rotateTransform;

                targetElement.InvalidateVisual();
                targetElement.UpdateLayout();
            }

            UpdatePreview();
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

                        var parentButton = _selectedShape.Parent as Button;
                        FrameworkElement targetElement = parentButton as FrameworkElement ?? _selectedShape;

                        // Get position, size, and rotation from the live element
                        double x = Canvas.GetLeft(targetElement);
                        double y = Canvas.GetTop(targetElement);
                        double newWidth = targetElement.Width;
                        double newHeight = targetElement.Height;

                        double rotation = 0;
                        if (targetElement.RenderTransform is RotateTransform rotate)
                        {
                            rotation = rotate.Angle;
                        }
                        else if (targetElement.RenderTransform is TransformGroup tg)
                        {
                            var rotateTransform = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                            if (rotateTransform != null)
                                rotation = rotateTransform.Angle;
                        }

                        // Preserve or update the section name
                        string currentName = sectionId ?? "Section";
                        string newName = !string.IsNullOrEmpty(SelectedShape) ? SelectedShape : currentName;

                        if (!string.IsNullOrEmpty(newName) && newName != currentName)
                        {
                            await CanvasService.UpdateSectionNameAsync(sectionId, newName);
                        }

                        // Always update dimensions and rotation in the DB
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
