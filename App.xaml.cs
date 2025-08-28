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

            // We'll publish this file from CI to your latest GitHub release
            var appcastUrl =
                "https://github.com/bradyhibbard/Wintonx/releases/latest/download/appcast.xml";

            _updater = new SparkleUpdater(
                appcastUrl,
                // TEMP while we wire CI: allow unsigned appcast/installer
                new Ed25519Checker(SecurityMode.Unsafe)
            )
            {
                UIFactory = new UIFactory()
            };

            // Start background loop and do an initial check now
            _updater.StartLoop(true);
        }
    }
}
