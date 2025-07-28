using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Winton.Helpers
{
    public class DoubleAnimatable : Animatable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(DoubleAnimatable),
                new PropertyMetadata(0.0, OnValueChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public event EventHandler ValueChanged;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DoubleAnimatable da)
            {
                da.ValueChanged?.Invoke(da, EventArgs.Empty);
            }
        }

        // Animatable requires you to override CreateInstanceCore().
        protected override Freezable CreateInstanceCore()
        {
            return new DoubleAnimatable();
        }
    }
}
