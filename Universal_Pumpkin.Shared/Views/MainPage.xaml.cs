using Universal_Pumpkin.Shared.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#if UWP1709
using Universal_Pumpkin.Services;
#endif

namespace Universal_Pumpkin
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            NavListBox.SelectedIndex = 0;
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
#if UWP1709
                System.Type pageType = null;

                try
                {
                    pageType = NavigationHelper.GetPageType(tag);
                }
                catch (System.ArgumentException)
                {
                    return;
                }

                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType);
                }
#else
                switch (tag)
                {
                    case "Console":
                        ContentFrame.Navigate(typeof(ConsolePage_Win10));
                        break;
                    case "Players":
                        ContentFrame.Navigate(typeof(PlayersPage_Win10));
                        break;
                    case "Settings":
                        ContentFrame.Navigate(typeof(SettingsPage_Win10));
                        break;
                }
#endif
            }
        }
    }
}