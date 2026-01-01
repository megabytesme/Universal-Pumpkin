using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.ViewModels;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class OobePage_Win11 : Page
    {
        private OobeViewModel _vm;
        private bool _isRestored = false;

        public OobePage_Win11()
        {
            this.InitializeComponent();
            _vm = new OobeViewModel();
            _vm.StatusMessage += (s, msg) => TxtRestoreStatus.Text = msg;

            _vm.RestoreCompleted += (s, e) =>
            {
                _isRestored = true;
                RestoreProgress.Visibility = Visibility.Collapsed;

                BtnRestore.IsEnabled = false;
                BtnStartFresh.IsEnabled = false;

                if (OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
                    OobeFlipView.SelectedIndex += 1;
            };
        }

        private void OobeFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pager != null) Pager.SelectedPageIndex = OobeFlipView.SelectedIndex;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
            {
                OobeFlipView.SelectedIndex += 1;
            }
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
            RestoreProgress.Visibility = Visibility.Visible;

            await _vm.RestoreBackup();

            if (!_isRestored)
            {
                BtnRestore.IsEnabled = true;
                BtnStartFresh.IsEnabled = true;
                RestoreProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            _vm.CompleteOobe();
            this.Frame.Navigate(NavigationHelper.GetPageType("Shell"));
            this.Frame.BackStack.Clear();
        }
    }
}