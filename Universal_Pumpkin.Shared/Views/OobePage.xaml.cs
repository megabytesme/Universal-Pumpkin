using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.ViewModels;

namespace Universal_Pumpkin
{
    public sealed partial class OobePage : Page
    {
        private OobeViewModel _vm;
        private bool _isRestored = false;

        public OobePage()
        {
            this.InitializeComponent();
            _vm = new OobeViewModel();
            _vm.StatusMessage += (s, msg) => TxtRestoreStatus.Text = msg;

            _vm.RestoreCompleted += (s, e) =>
            {
                _isRestored = true;
                RestoreRing.IsActive = false;
                RestoreRing.Visibility = Visibility.Collapsed;

                BtnRestore.IsEnabled = false;
                BtnStartFresh.IsEnabled = false;
                BtnRestore.Content = "Restore Complete";

                Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        if (OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
                        {
                            OobeFlipView.SelectedIndex += 1;
                            System.Diagnostics.Debug.WriteLine($"[OOBE] RestoreCompleted: after={OobeFlipView.SelectedIndex}");
                        }
                    }
                );
            };
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[OOBE] Next_Click fired (before={OobeFlipView.SelectedIndex})");

            Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    if (OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
                    {
                        OobeFlipView.SelectedIndex += 1;
                        System.Diagnostics.Debug.WriteLine($"[OOBE] Next_Click dispatcher: after={OobeFlipView.SelectedIndex}");
                    }
                }
            );
        }

        private async void BtnPermission_Click(object sender, RoutedEventArgs e)
        {
            string result = await _vm.RequestBackgroundPermission();
            TxtPermStatus.Text = result;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) => _vm.OpenSettings();

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            BtnRestore.IsEnabled = false;
            BtnStartFresh.IsEnabled = false;
            RestoreRing.Visibility = Visibility.Visible;
            RestoreRing.IsActive = true;
            TxtRestoreStatus.Text = "Initializing...";

            await _vm.RestoreBackup();
            
            if (!_isRestored)
            {
                BtnRestore.IsEnabled = true;
                BtnStartFresh.IsEnabled = true;
                RestoreRing.IsActive = false;
                RestoreRing.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[OOBE] BtnFinish_Click fired");
            _vm.CompleteOobe();
            this.Frame.Navigate(NavigationHelper.GetPageType("Shell"));
            this.Frame.BackStack.Clear();
        }
    }
}