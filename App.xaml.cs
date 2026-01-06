using System;
using System.Linq;
using System.Windows;

namespace Winton
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Keep the app alive while Splash is open
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var splash = new SplashWindow();
            splash.Show();
        }

        /// <summary>
        /// Dynamically switches between Light.xaml and Dark.xaml.
        /// Usage:
        /// (App.Current as App).SetTheme("Light");
        /// (App.Current as App).SetTheme("Dark");
        /// </summary>
        public void SetTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return;

            var dictionaries = Resources.MergedDictionaries;

            var oldTheme = dictionaries.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("Light.xaml") == true ||
                d.Source?.OriginalString.Contains("Dark.xaml") == true);

            if (oldTheme != null)
                dictionaries.Remove(oldTheme);

            dictionaries.Insert(0, new ResourceDictionary()
            {
                Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
            });
        }

        /// <summary>
        /// Call this when you're ready to show the main window and switch shutdown behavior.
        /// </summary>
        internal void ShowMainWindowAndSwitchShutdownMode()
        {
            var main = new MainWindow();
            MainWindow = main;

            // Now "normal" app behavior
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            main.Show();
        }
    }
}
