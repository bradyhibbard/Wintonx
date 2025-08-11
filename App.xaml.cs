using System.Configuration;
using System.Data;
using System.Windows;

namespace Winton
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var checker = new Winton.Views.UpdateChecker();
            await checker.AutoCheckOnStartupAsync(showNoUpdateToast: false);
        }

        private async void MenuCheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await new Winton.Views.UpdateChecker().ManualCheckAsync();
        }


    }

}
