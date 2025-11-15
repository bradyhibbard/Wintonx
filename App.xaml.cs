using System;
using System.Linq;
using System.Windows;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.WPF;

namespace Winton
{
    public partial class App : Application
    {
        private SparkleUpdater? _updater;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitializeAutoUpdater();
        }

        /// <summary>
        /// Initializes the NetSparkle auto-update loop.
        /// </summary>
        private void InitializeAutoUpdater()
        {
            var appcastUrl =
                "https://github.com/bradyhibbard/Wintonx/releases/latest/download/appcast.xml";

            _updater = new SparkleUpdater(
                appcastUrl,
                // TEMP: Unsafe mode while CI signing is wired up
                new Ed25519Checker(SecurityMode.Unsafe))
            {
                UIFactory = new UIFactory()
            };

            // Start background update check loop
            _updater.StartLoop(true);
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

            // Get merged dictionaries
            var dictionaries = Resources.MergedDictionaries;

            // Find existing Light or Dark dictionary
            var oldTheme = dictionaries.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("Light.xaml") == true ||
                d.Source?.OriginalString.Contains("Dark.xaml") == true);

            // Remove old theme
            if (oldTheme != null)
                dictionaries.Remove(oldTheme);

            // Add new theme
            dictionaries.Insert(0, new ResourceDictionary()
            {
                Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
            });
        }
    }
}
