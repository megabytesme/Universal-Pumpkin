using System;
using System.Diagnostics;
using System.Linq;
using Universal_Pumpkin.Services;
using Universal_Pumpkin.Shared.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin
{
    public sealed partial class SettingsPage_Win10_1507 : SettingsPageBase
    {
        public SettingsPage_Win10_1507()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] InitializeComponent FAILED: " + ex);
            }

            try
            {
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] LoadAllAsync FAILED: " + ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] OnNavigatedTo FAILED: " + ex);
            }
#if UWP1709
            try
            {
                string tag = ModeToTag(AppearanceService.Current);

                _suppressAppearanceChange = true;

                foreach (var rb in AppearanceStackPanel.Children.OfType<RadioButton>())
                {
                    rb.IsChecked = (string)rb.Tag == tag;
                }

                _suppressAppearanceChange = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win10_1507] Radio selection FAILED: " + ex);
                _suppressAppearanceChange = false;
            }
#else
            AppearanceStackPanel.Visibility = Visibility.Collapsed;
#endif
        }

        private void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearanceChange)
                return;

            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                SetAppearance(TagToMode(tag));
            }
        }
    }
}