using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Universal_Pumpkin.ViewModels;

namespace Universal_Pumpkin.Shared.Views
{
    public sealed partial class OobePage : OobePageBase
    {
        public OobePage()
        {
            InitializeComponent();
            InitializeOobe();
        }
    }
}