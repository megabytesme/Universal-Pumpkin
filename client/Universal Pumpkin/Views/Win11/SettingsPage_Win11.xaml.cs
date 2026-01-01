using System;
using System.Diagnostics;
using System.Linq;
using Universal_Pumpkin.Services;
using Universal_Pumpkin.Shared.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class SettingsPage_Win11 : SettingsPageBase
    {
        public SettingsPage_Win11()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win11] InitializeComponent FAILED: " + ex);
            }

            try
            {
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SettingsPage_Win11] LoadAllAsync FAILED: " + ex);
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
                Debug.WriteLine("[SettingsPage_Win11] OnNavigatedTo FAILED: " + ex);
            }

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
                Debug.WriteLine("[SettingsPage_Win11] Radio selection FAILED: " + ex);
                _suppressAppearanceChange = false;
            }
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