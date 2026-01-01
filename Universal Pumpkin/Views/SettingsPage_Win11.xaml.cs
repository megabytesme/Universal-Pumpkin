using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.Shared.Views;

namespace Universal_Pumpkin.Views.Win11
{
    public sealed partial class SettingsPage_Win11 : SettingsPageBase
    {
        public SettingsPage_Win11()
        {
            this.InitializeComponent();
            this.SizeChanged += SettingsPage_Win11_SizeChanged;
            _ = LoadAllAsync();
        }

        protected override void InitializeModernUiSection()
        {
            if (SwModernUI != null)
                SwModernUI.IsOn = _vm.IsModernUIEnabled;
        }

        protected override ContentDialog CreateDialog()
        {
            return new ContentDialog
            {
                XamlRoot = this.XamlRoot
            };
        }

        private void SettingsPage_Win11_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;

            var titleBlock = this.FindName("TitleBlock") as FrameworkElement;
            if (titleBlock == null) return;

            if (width <= 640)
            {
                titleBlock.Margin = new Thickness(12, 12, 0, 8);
            }
            else
            {
                titleBlock.Margin = new Thickness(0, 0, 0, 8);
            }
        }
    }
}