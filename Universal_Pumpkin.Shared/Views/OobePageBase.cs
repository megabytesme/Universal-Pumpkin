using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Universal_Pumpkin.ViewModels;
using Universal_Pumpkin.Models;
using Universal_Pumpkin.Helpers;

namespace Universal_Pumpkin.Shared.Views
{
    public abstract class OobePageBase : Page
    {
        protected OobeViewModel _vm;
        protected bool _isRestored = false;

        protected FlipView OobeFlipView => FindName("OobeFlipView") as FlipView;
        protected TextBlock TxtPermStatus => FindName("TxtPermStatus") as TextBlock;
        protected TextBlock TxtRestoreStatus => FindName("TxtRestoreStatus") as TextBlock;
        protected Button BtnRestore => FindName("BtnRestore") as Button;
        protected Button BtnStartFresh => FindName("BtnStartFresh") as Button;
        protected Button BtnFinish => FindName("BtnFinish") as Button;
        protected ProgressRing RestoreRing => FindName("RestoreRing") as ProgressRing;
        protected ProgressBar RestoreProgress => FindName("RestoreProgress") as ProgressBar;

        public void InitializeOobe()
        {
            _vm = new OobeViewModel();

            _vm.StatusMessage += (s, msg) =>
            {
                if (TxtRestoreStatus != null)
                    TxtRestoreStatus.Text = msg;
            };

            _vm.RestoreCompleted += async (s, e) =>
            {
                _isRestored = true;

                if (RestoreRing != null)
                {
                    RestoreRing.IsActive = false;
                    RestoreRing.Visibility = Visibility.Collapsed;
                }

                if (RestoreProgress != null)
                    RestoreProgress.Visibility = Visibility.Collapsed;

                if (BtnRestore != null) BtnRestore.IsEnabled = false;
                if (BtnStartFresh != null) BtnStartFresh.IsEnabled = false;

                await Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        if (OobeFlipView != null &&
                            OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
                        {
                            OobeFlipView.SelectedIndex += 1;
                            System.Diagnostics.Debug.WriteLine($"[OOBE] RestoreCompleted: after={OobeFlipView.SelectedIndex}");
                        }
                    }
                );
            };
        }

        protected void Next_Click(object sender, RoutedEventArgs e)
        {
            _ = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    if (OobeFlipView != null &&
                        OobeFlipView.SelectedIndex < OobeFlipView.Items.Count - 1)
                    {
                        OobeFlipView.SelectedIndex += 1;
                    }
                }
            );
        }

        protected async void BtnPermission_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var result = await _vm.RequestBackgroundPermission();

                switch (result)
                {
                    case OobePermissionStatus.Allowed:
                        btn.Content = "\uE73E";
                        btn.FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets");
                        break;

                    case OobePermissionStatus.Denied:
                        await ShowPermissionDeniedDialog();
                        break;

                    case OobePermissionStatus.Restricted:
                        await ShowPermissionRestrictedDialog();
                        break;
                }
            }
        }

        private async Task ShowPermissionDeniedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Background Permission Required",
                Content = "Background execution is disabled. Universal Pumpkin cannot run minimized unless you enable background permissions.",
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
                BackgroundKeeper.OpenBackgroundSettings();
        }

        private async Task ShowPermissionRestrictedDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "Background Execution Restricted",
                Content = "Windows is currently restricting background execution. You may need to adjust system settings.",
                PrimaryButtonText = "Open Settings",
                SecondaryButtonText = "Cancel"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
                BackgroundKeeper.OpenBackgroundSettings();
        }

        protected async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (BtnRestore != null) BtnRestore.IsEnabled = false;
            if (BtnStartFresh != null) BtnStartFresh.IsEnabled = false;

            if (RestoreRing != null)
            {
                RestoreRing.Visibility = Visibility.Visible;
                RestoreRing.IsActive = true;
            }

            if (RestoreProgress != null)
                RestoreProgress.Visibility = Visibility.Visible;

            await _vm.RestoreBackup();

            if (!_isRestored)
            {
                if (BtnRestore != null) BtnRestore.IsEnabled = true;
                if (BtnStartFresh != null) BtnStartFresh.IsEnabled = true;

                if (RestoreRing != null)
                {
                    RestoreRing.IsActive = false;
                    RestoreRing.Visibility = Visibility.Collapsed;
                }

                if (RestoreProgress != null)
                    RestoreProgress.Visibility = Visibility.Collapsed;
            }
        }

        protected void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            _vm.CompleteOobe();
            Frame.Navigate(NavigationHelper.GetPageType("Shell"));
            Frame.BackStack.Clear();
        }
    }
}