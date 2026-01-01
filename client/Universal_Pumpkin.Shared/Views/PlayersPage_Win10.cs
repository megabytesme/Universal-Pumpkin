using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Universal_Pumpkin.Shared.Views;

namespace Universal_Pumpkin
{
    public sealed partial class PlayersPage_Win10 : PlayersPageBase
    {
        public PlayersPage_Win10()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HandlePivotState();
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HandlePivotState();
        }

        private void HandlePivotState()
        {
            switch (MainPivot.SelectedIndex)
            {
                case 0:
                    EnterOnlineSection();
                    break;
                case 1:
                    EnterOpsSection();
                    break;
                case 2:
                    EnterBansSection();
                    break;
                case 3:
                    EnterIpBansSection();
                    break;
            }
        }
    }
}