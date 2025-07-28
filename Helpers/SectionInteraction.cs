using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;

namespace Winton.Helpers
{
    public class SectionInteraction
    {
        private bool _isDragging = false;
        private bool _isResizing = false;
        private Point _startPosition;
        private UIElement _selectedElement;

        public void EnableInteractions(Shape section)
        {
            section.MouseLeftButtonDown += Section_MouseLeftButtonDown;
            section.MouseMove += Section_MouseMove;
            section.MouseLeftButtonUp += Section_MouseLeftButtonUp;
        }

        private void Section_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _selectedElement = sender as UIElement;
            if (_selectedElement == null) return;

            Canvas parentCanvas = GetParentCanvas(_selectedElement);
            if (parentCanvas == null) return; // Prevent crashes if the parent is not found

            _startPosition = e.GetPosition(parentCanvas);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _selectedElement.CaptureMouse();
            }
        }

        private void Section_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _selectedElement != null)
            {
                Canvas parentCanvas = GetParentCanvas(_selectedElement);
                if (parentCanvas == null) return;

                Point newPosition = e.GetPosition(parentCanvas);

                double offsetX = newPosition.X - _startPosition.X;
                double offsetY = newPosition.Y - _startPosition.Y;

                Canvas.SetLeft(_selectedElement, Canvas.GetLeft(_selectedElement) + offsetX);
                Canvas.SetTop(_selectedElement, Canvas.GetTop(_selectedElement) + offsetY);

                _startPosition = newPosition;
            }
        }

        private void Section_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _selectedElement?.ReleaseMouseCapture();
        }

        // 🔥 New Method: Find the Parent Canvas Correctly
        private Canvas GetParentCanvas(UIElement element)
        {
            DependencyObject parent = element;
            while (parent != null)
            {
                if (parent is Canvas canvas)
                    return canvas;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
