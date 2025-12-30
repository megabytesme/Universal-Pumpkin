using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.ViewModels;
using Windows.ApplicationModel.Core;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class ConsolePage_Win11 : Page
    {
        private readonly ConsoleViewModel _vm;

        public ConsolePage_Win11()
        {
            this.InitializeComponent();
            _vm = new ConsoleViewModel();

            _vm.LogReceived += Vm_LogReceived;
            _vm.ServerStopped += Vm_ServerStopped;
            _vm.MetricsUpdated += Vm_MetricsUpdated;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _vm.OnNavigatedTo();

            TxtLog.Text = _vm.GetInitialLog();
            ScrollLogToBottom();
            UpdateUiState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.OnNavigatedFrom();
        }

        private void UpdateUiState()
        {
            StatusInfoBar.IsClosable = true;

            if (App.Server.IsRunning)
            {
                StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success;
                StatusInfoBar.Title = "Server Running";
                StatusInfoBar.Message = $"Listening on {_vm.LocalIpAddress}";
                StatusInfoBar.IsOpen = true;

                StatusGrid.Visibility = Visibility.Visible;
                TxtIpAddress.Text = _vm.LocalIpAddress;

                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BtnStop.Visibility = Visibility.Visible;
                BtnStop.IsEnabled = true;

                BoxCommand.IsEnabled = true;
                BtnSend.IsEnabled = true;
            }
            else
            {
                StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
                StatusInfoBar.Title = "Ready";
                StatusInfoBar.Message = "Server is ready to launch.";
                StatusInfoBar.IsOpen = true;

                StatusGrid.Visibility = Visibility.Collapsed;

                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;
            }
        }

        private async void Vm_MetricsUpdated(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TxtRAM.Text = _vm.RamUsage;
                TxtTPS.Text = _vm.TpsText;
                TxtMSPT.Text = _vm.MsptText;

                TxtTPS.Foreground = _vm.IsTpsGood
                    ? new SolidColorBrush(Windows.UI.Colors.LightGreen)
                    : new SolidColorBrush(Windows.UI.Colors.OrangeRed);
            });
        }

        private async void Vm_LogReceived(object sender, string e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TxtLog.Text += e + "\n";
                ScrollLogToBottom();
            });
        }

        private async void Vm_ServerStopped(object sender, int code)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    UpdateUiState();

                    BtnStart.Visibility = Visibility.Collapsed;
                    BtnStop.Visibility = Visibility.Collapsed;
                    BtnRestartApp.Visibility = Visibility.Visible;

                    StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                    StatusInfoBar.Title = "Server Stopped";
                    StatusInfoBar.Message = $"Code: {code}. Please restart the app.";
                    StatusInfoBar.IsClosable = true;
                    StatusInfoBar.IsOpen = true;

                    TxtLog.Text += $"\n[System] Server stopped (Code {code}).\n";
                    ScrollLogToBottom();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in Vm_ServerStopped: {ex.Message}");
                }
            });
        }

        private void ScrollLogToBottom()
        {
            try { LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null); } catch { }
        }
        
        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.StartServerAsync();
                UpdateUiState();
            }
            catch (Exception)
            {
                StatusInfoBar.Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error;
                StatusInfoBar.Title = "Error";
                StatusInfoBar.Message = "Pumpkin DLL is missing.";
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            BtnStop.IsEnabled = false;
            StatusInfoBar.Message = "Stopping...";
            _vm.StopServer();
        }

        private async void BtnRestartApp_Click(object sender, RoutedEventArgs e)
        {
            var restartDialog = new ContentDialog
            {
                Title = "Restart app",
                Content = "Restart now?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "No"
            };
            
            restartDialog.XamlRoot = this.XamlRoot;

            var result = await restartDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await CoreApplication.RequestRestartAsync("");
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            _vm.SendCommand(BoxCommand.Text);
            BoxCommand.Text = "";
            BoxCommand.Focus(FocusState.Programmatic);
        }
        
        private async void BoxCommand_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var items = await _vm.GetSuggestionsAsync(sender.Text);
            sender.ItemsSource = items;
        }

        private void BoxCommand_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string command = string.IsNullOrEmpty(args.QueryText) ? sender.Text : args.QueryText;
            _vm.SendCommand(command);
            BoxCommand.Text = "";
            BoxCommand.Focus(FocusState.Programmatic);
        }

        private void BoxCommand_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            string selected = args.SelectedItem.ToString();
            string currentText = sender.Text;
            int lastSpace = currentText.LastIndexOf(' ');

            sender.Text = (lastSpace >= 0 ? currentText.Substring(0, lastSpace + 1) : "") + selected;

            var textBox = FindChild<TextBox>(sender);
            if (textBox != null) textBox.SelectionStart = sender.Text.Length;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}