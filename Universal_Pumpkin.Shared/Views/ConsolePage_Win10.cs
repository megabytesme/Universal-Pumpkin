using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.Shared.Views;

namespace Universal_Pumpkin.Shared.Views
{
    public sealed partial class ConsolePage_Win10 : ConsolePageBase
    {
        public ConsolePage_Win10()
        {
            this.InitializeComponent();
        }

        protected override void UpdateUiState()
        {
            if (App.Server.IsRunning)
            {
                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BtnStop.Visibility = Visibility.Visible;
                BtnStop.IsEnabled = true;

                TxtStatus.Text = "Running";

                BoxCommand.IsEnabled = true;
                BtnSend.IsEnabled = true;

                StatusGrid.Visibility = Visibility.Visible;
                TxtIpAddress.Text = _vm.LocalIpAddress;
            }
            else
            {
                StatusGrid.Visibility = Visibility.Collapsed;

                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;

                TxtStatus.Text = "Ready";
            }
        }

        protected override void UpdateInfoBarForNotRunning()
        {
            TxtStatus.Text = "Ready";
        }

        protected override void OnServerStoppedUI(int code)
        {
            TxtStatus.Text = $"Server stopped. Code: {code}";
        }

        protected override void OnServerStoppingUI()
        {
            TxtStatus.Text = "Stopping...";
        }

        protected override void OnStartServerError()
        {
            TxtStatus.Text = "Error: DLL missing";
        }

        protected override async void ShowRestartDialog()
        {
#if UWP1709
            bool canRestart = true;
#else
            bool canRestart = false;
#endif

            var dialog = new ContentDialog
            {
                Title = canRestart
                    ? "Restart app"
                    : "Exit app",
                Content = canRestart
                    ? "Due to limitations, the app must restart to run the server again. Restart now?"
                    : "Due to limitations, the app must restart to run the server again. Close now?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
#if UWP1709
                await CoreApplication.RequestRestartAsync("");
#else
                CoreApplication.Exit();
#endif
            }
        }
    }
}