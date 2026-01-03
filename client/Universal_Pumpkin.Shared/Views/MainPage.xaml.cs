using System;
using Universal_Pumpkin.Models;
using Universal_Pumpkin.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Universal_Pumpkin
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            ApplyAppearanceStyling();
            NavListBox.SelectedIndex = 0;
        }

        private void ApplyAppearanceStyling()
        {
            var mode = AppearanceService.Current;


            if (mode == AppearanceMode.Win10_1709)
            {
#if UWP1709
                try
                {
                    this.Background = (Brush)Application.Current.Resources["AppBackgroundAcrylic"];
                }
                catch
                {
                }

                try
                {
                    RootSplitView.PaneBackground =
                        (Brush)Application.Current.Resources["SystemControlAcrylicWindowBrush"];
                }
                catch
                {
                }
#endif
            }
            else
            {
                this.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
                RootSplitView.PaneBackground = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"];
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListBox.SelectedItem is ListBoxItem item)
            {
                string tag = item.Tag.ToString();

                Type pageType = null;

                try
                {
                    pageType = NavigationHelper.GetPageType(tag);
                }
                catch (ArgumentException)
                {
                    return;
                }

                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }
    }
}