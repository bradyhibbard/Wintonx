using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Winton
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            Loaded += SplashWindow_Loaded;
        }

        private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500));
            BeginAnimation(Window.OpacityProperty, fadeIn);

            // Stay for a moment, then fade out
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500))
            {
                BeginTime = TimeSpan.FromSeconds(1.8)
            };

            fadeOut.Completed += (s, a) =>
            {
                // Open MainWindow and close splash
                var main = new MainWindow();
                main.Show();
                Close();
            };

            BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}
