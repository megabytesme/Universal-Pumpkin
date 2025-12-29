using System;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Universal_Pumpkin
{
    public sealed partial class ConsolePage : Page
    {
        public static PumpkinController Controller = new PumpkinController();

        public ConsolePage()
        {
            this.InitializeComponent();

            Controller.OnLogReceived += Controller_OnLogReceived;
            Controller.OnServerStopped += Controller_OnServerStopped;
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

            await Controller.StartServerAsync();
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
            Controller.StopServer();
        }

        private async void BtnRestartApp_Click(object sender, RoutedEventArgs e)
        {
            var restartDialog = new ContentDialog
            {
                Title = "Restart app",
                Content = "Due to limitations from Pumpkin, the app has to be restarted in order to start the server again. Would you like the app to restart now?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            ContentDialogResult result = await restartDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await CoreApplication.RequestRestartAsync("");
            }
        }

        private void SendCommand()
        {
            if (!string.IsNullOrWhiteSpace(BoxCommand.Text))
            {
                Controller.SendCommand(BoxCommand.Text);
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