using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Winton.Views; // <-- UpdateChecker namespace

namespace Winton
{
    public partial class SplashWindow : Window
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TaskCompletionSource<bool> _skipTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SplashWindow()
        {
            InitializeComponent();
            Loaded += SplashWindow_Loaded;
            Closed += (_, __) => _cts.Cancel();
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350));
            BeginAnimation(OpacityProperty, fadeIn);

            // Let user skip immediately (so startup never feels "stuck")
            SkipButton.Visibility = Visibility.Visible;

            try
            {
                await RunStartupFlowAsync(_cts.Token);
            }
            catch
            {
                // If anything goes wrong, just continue to the app
                await ContinueToMainAsync();
            }
        }

        private async Task RunStartupFlowAsync(CancellationToken ct)
        {
            StatusText.Text = "Checking for updates...";

            // Kick off update check (uses your existing GitHub release logic + prompts)
            var checker = new UpdateChecker();
            var updateTask = checker.AutoCheckOnStartupAsync(showNoUpdateToast: false, ct);

            // Wait for either update flow to finish OR user hits Skip
            var winner = await Task.WhenAny(updateTask, _skipTcs.Task);

            if (winner == _skipTcs.Task)
            {
                // user skipped; we continue immediately
            }
            else
            {
                // update check completed (might have prompted + possibly launched installer)
                await updateTask;
            }

            StatusText.Text = "Starting Wintonx...";
            await ContinueToMainAsync();
        }

        private async Task ContinueToMainAsync()
        {
            SkipButton.Visibility = Visibility.Collapsed;
            BusyBar.Visibility = Visibility.Collapsed;

            // Small fade-out so it feels polished
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var fadeOut = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (_, __) => tcs.TrySetResult(true);
            BeginAnimation(OpacityProperty, fadeOut);

            await tcs.Task;

            // Open main window via App helper
            if (Application.Current is App app)
            {
                app.ShowMainWindowAndSwitchShutdownMode();
            }
            else
            {
                // fallback
                var main = new MainWindow();
                main.Show();
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Application.Current.MainWindow = main;
            }

            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            _skipTcs.TrySetResult(true);
        }
    }
}
