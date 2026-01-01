using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.Shared.Views;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class ConsolePage_Win11 : ConsolePageBase
    {
        public ConsolePage_Win11()
        {
            this.InitializeComponent();
        }

        protected override void UpdateUiState()
        {
            if (App.Server.IsRunning)
            {
                StatusInfoBar.IsOpen = false;

                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Visible;
                BtnStop.IsEnabled = true;

                BoxCommand.IsEnabled = true;
                BtnSend.IsEnabled = true;

                StatusGrid.Visibility = Visibility.Visible;
                TxtIpAddress.Text = _vm.LocalIpAddress;
            }
            else
            {
                UpdateInfoBarForNotRunning();

                StatusGrid.Visibility = Visibility.Collapsed;

                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;
            }
        }

        protected override void UpdateInfoBarForNotRunning()
        {
            StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
            StatusInfoBar.Title = "Ready";
            StatusInfoBar.Message = "Server is ready to launch.";
            StatusInfoBar.IsClosable = true;
            StatusInfoBar.IsOpen = true;
        }

        protected override void OnServerStoppedUI(int code)
        {
            StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
            StatusInfoBar.Title = "Server Stopped";
            StatusInfoBar.Message = $"Code: {code}. Please restart the app.";
            StatusInfoBar.IsClosable = true;
            StatusInfoBar.IsOpen = true;
        }

        protected override void OnServerStoppingUI()
        {
            StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
            StatusInfoBar.Title = "Stopping";
            StatusInfoBar.Message = "Stopping...";
            StatusInfoBar.IsOpen = true;
        }

        protected override void OnStartServerError()
        {
            StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
            StatusInfoBar.Title = "Error";
            StatusInfoBar.Message = "Pumpkin DLL is missing.";
            StatusInfoBar.IsOpen = true;
        }

        protected override async void ShowRestartDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Restart app",
                Content = "Restart now?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await CoreApplication.RequestRestartAsync("");
            }
        }
    }
}