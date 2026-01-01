using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.Shared.Views;

namespace Universal_Pumpkin
{
    public sealed partial class SettingsPage_Win10 : SettingsPageBase
    {
        public SettingsPage_Win10()
        {
            this.InitializeComponent();
            _ = LoadAllAsync();
        }

        protected override void InitializeModernUiSection()
        {
#if UWP1709
            if (_vm.IsHostWin11)
            {
                if (AppearanceCard != null)
                    AppearanceCard.Visibility = Visibility.Visible;

                if (SwModernUI != null)
                    SwModernUI.IsOn = _vm.IsModernUIEnabled;
            }
            else
            {
                if (AppearanceCard != null)
                    AppearanceCard.Visibility = Visibility.Collapsed;
            }
#else
            if (AppearanceCard != null)
                AppearanceCard.Visibility = Visibility.Collapsed;
#endif
        }
    }
}