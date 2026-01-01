using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Controls;
using Universal_Pumpkin.Shared.Views;
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using NavigationViewSelectionChangedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class PlayersPage_Win11 : PlayersPageBase
    {
        public PlayersPage_Win11()
        {
            this.InitializeComponent();
        }

        protected override ContentDialog CreateDialog()
        {
            return new ContentDialog
            {
                XamlRoot = this.XamlRoot
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            TopNav.SelectedItem = TopNav.FooterMenuItems[0];
            HandleNavigation("Online");
        }

        private void TopNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                HandleNavigation(tag);
            }
        }

        private void HandleNavigation(string tag)
        {
            ViewOnline.Visibility = Visibility.Collapsed;
            ViewOps.Visibility = Visibility.Collapsed;
            ViewBans.Visibility = Visibility.Collapsed;
            ViewIpBans.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "Online":
                    ViewOnline.Visibility = Visibility.Visible;
                    EnterOnlineSection();
                    break;

                case "Ops":
                    ViewOps.Visibility = Visibility.Visible;
                    EnterOpsSection();
                    break;

                case "Bans":
                    ViewBans.Visibility = Visibility.Visible;
                    EnterBansSection();
                    break;

                case "IpBans":
                    ViewIpBans.Visibility = Visibility.Visible;
                    EnterIpBansSection();
                    break;
            }
        }
    }
}