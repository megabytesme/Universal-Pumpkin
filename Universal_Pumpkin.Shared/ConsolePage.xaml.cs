using System;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin
{
    public sealed partial class ConsolePage : Page
    {
        public ConsolePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            TxtLog.Text = App.Server.GetLogHistory();
            // Try/Catch for scroll in case layout isn't ready
            try { LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null); } catch { }

            App.Server.OnLogReceived += Controller_OnLogReceived;
            App.Server.OnServerStopped += Controller_OnServerStopped;

            UpdateUiState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            App.Server.OnLogReceived -= Controller_OnLogReceived;
            App.Server.OnServerStopped -= Controller_OnServerStopped;
        }

        private void UpdateUiState()
        {
            if (App.Server.IsRunning)
            {
                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Visible;
                BtnStop.IsEnabled = true;

                BoxCommand.IsEnabled = true;
                BtnSend.IsEnabled = true;

                TxtStatus.Text = "Running";
                IpCard.Visibility = Visibility.Visible;
                TxtIpAddress.Text = IpAddressHelper.GetLocalIpAddress();
            }
            else
            {
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;

                IpCard.Visibility = Visibility.Collapsed;
                if (string.IsNullOrEmpty(TxtStatus.Text)) TxtStatus.Text = "Ready";
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var h = NativeProbe.TryLoadPumpkin();
            if (h == IntPtr.Zero)
            {
                TxtStatus.Text = "Error: DLL missing";
                return;
            }

            BtnStart.Visibility = Visibility.Collapsed;
            BtnRestartApp.Visibility = Visibility.Collapsed;
            BtnStop.Visibility = Visibility.Visible;
            BtnStop.IsEnabled = true;

            BoxCommand.IsEnabled = true;
            BtnSend.IsEnabled = true;

            TxtStatus.Text = "Running";
            TxtLog.Text = "";

            TxtIpAddress.Text = IpAddressHelper.GetLocalIpAddress();
            IpCard.Visibility = Visibility.Visible;

            await App.Server.StartServerAsync();
        }

        private async void Controller_OnServerStopped(object sender, int e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IpCard.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Visible;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;

                TxtStatus.Text = $"Server Stopped (Code {e})";
                TxtLog.Text += $"\n[System] Server stopped. Please restart the app to run again.\n";
            });
        }

        private async void Controller_OnLogReceived(object sender, string e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TxtLog.Text += e + "\n";
                LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
            });
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Stopping...";
            App.Server.StopServer();
        }

        private async void BtnRestartApp_Click(object sender, RoutedEventArgs e)
        {
#if UWP1709
            bool canRestart = true;
#else
            bool canRestart = false;
#endif

            var restartDialog = new ContentDialog
            {
                Title = "Restart app",
                Content = canRestart
                    ? "Due to limitations, the app must restart to run the server again. Restart now?"
                    : "Due to limitations, the app must close to run the server again. Close now?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            ContentDialogResult result = await restartDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
#if UWP1709
                await CoreApplication.RequestRestartAsync("");
#else
                CoreApplication.Exit();
#endif
            }
        }

        private void SendCommand()
        {
            if (!string.IsNullOrWhiteSpace(BoxCommand.Text))
            {
                App.Server.SendCommand(BoxCommand.Text);
                TxtLog.Text += $"> {BoxCommand.Text}\n";
                BoxCommand.Text = "";
                BoxCommand.Focus(FocusState.Programmatic);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e) => SendCommand();

        private void BoxCommand_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) SendCommand();
        }
    }
}