using System;
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

        private async void Controller_OnLogReceived(object sender, string e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TxtLog.Text += e + "\n";

                LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
            });
        }

        private async void Controller_OnServerStopped(object sender, int e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;
                TxtStatus.Text = $"Stopped (Code {e})";
                TxtLog.Text += $"\n[System] Server stopped with code {e}.\n";
            });
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            var h = NativeProbe.TryLoadPumpkin();
            if (h == IntPtr.Zero)
            {
                TxtStatus.Text = "Failed to load DLL!";
                return;
            }

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BoxCommand.IsEnabled = true;
            BtnSend.IsEnabled = true;
            TxtStatus.Text = "Running";
            TxtLog.Text = "";

            await Controller.StartServerAsync();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            Controller.StopServer();
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Stopping...";
        }

        private void SendCommand()
        {
            if (!string.IsNullOrWhiteSpace(BoxCommand.Text))
            {
                Controller.SendCommand(BoxCommand.Text);
                TxtLog.Text += $"> {BoxCommand.Text}\n";
                BoxCommand.Text = "";
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e) => SendCommand();

        private void BoxCommand_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter) SendCommand();
        }
    }
}