using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin
{
    public sealed partial class ConsolePage : Page
    {
        private DispatcherTimer _metricsTimer;

        public ConsolePage()
        {
            this.InitializeComponent();

            _metricsTimer = new DispatcherTimer();
            _metricsTimer.Interval = TimeSpan.FromSeconds(1);
            _metricsTimer.Tick += MetricsTimer_Tick;
        }

        private async void MetricsTimer_Tick(object sender, object e)
        {
            if (App.Server.IsRunning)
            {
                ulong memUsage = MemoryManager.AppMemoryUsage / 1024 / 1024;
                TxtRAM.Text = $"{memUsage} MB";

                var metrics = await Task.Run(() => App.Server.GetMetrics());

                if (metrics != null)
                {
                    TxtTPS.Text = metrics.FmtTPS;
                    TxtTPS.Foreground = metrics.TPS >= 18.0
                        ? new SolidColorBrush(Windows.UI.Colors.LightGreen)
                        : new SolidColorBrush(Windows.UI.Colors.OrangeRed);

                    TxtMSPT.Text = metrics.FmtMSPT;
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            TxtLog.Text = App.Server.GetLogHistory();
            try { LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null); } catch { }

            App.Server.OnLogReceived += Controller_OnLogReceived;
            App.Server.OnServerStopped += Controller_OnServerStopped;

            UpdateUiState();

            if (App.Server.IsRunning) _metricsTimer.Start();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            App.Server.OnLogReceived -= Controller_OnLogReceived;
            App.Server.OnServerStopped -= Controller_OnServerStopped;

            _metricsTimer.Stop();
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
                StatusGrid.Visibility = Visibility.Visible;
                TxtIpAddress.Text = IpAddressHelper.GetLocalIpAddress();
            }
            else
            {
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Collapsed;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;

                StatusGrid.Visibility = Visibility.Collapsed;
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
            StatusGrid.Visibility = Visibility.Visible;

            await App.Server.StartServerAsync();

            _metricsTimer.Start();
        }

        private async void Controller_OnServerStopped(object sender, int e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _metricsTimer.Stop();

                StatusGrid.Visibility = Visibility.Collapsed;
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

        private async void BoxCommand_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

            string text = sender.Text;
            
            var suggestions = await App.Server.GetSuggestionsAsync(text);

            sender.ItemsSource = suggestions.Select(s => s.Text).ToList();
        }

        private void BoxCommand_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string command = string.IsNullOrEmpty(args.QueryText) ? sender.Text : args.QueryText;
            SendCommand(command);
        }

        private void BoxCommand_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            string selected = args.SelectedItem.ToString();
            string currentText = sender.Text;
            
            int lastSpace = currentText.LastIndexOf(' ');
            
            if (lastSpace >= 0)
            {
                string prefix = currentText.Substring(0, lastSpace + 1);
                sender.Text = prefix + selected;
            }
            else
            {
                sender.Text = selected;
            }

            var textBox = FindChild<TextBox>(sender); 
            if (textBox != null) textBox.SelectionStart = sender.Text.Length;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SendCommand(string text = null)
        {
            string cmd = text ?? BoxCommand.Text;
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                App.Server.SendCommand(cmd);
                BoxCommand.Text = "";
                BoxCommand.Focus(FocusState.Programmatic);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e) => SendCommand();
    }
}