using Windows.UI.Xaml;
using Windows.UI.Xaml.Documents;

namespace Universal_Pumpkin.Helpers
{
    public static class HyperlinkExtensions
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.RegisterAttached(
                "Target",
                typeof(string),
                typeof(HyperlinkExtensions),
                new PropertyMetadata(null));

        public static void SetTarget(DependencyObject obj, string value)
        {
            obj.SetValue(TargetProperty, value);
        }

        public static string GetTarget(DependencyObject obj)
        {
            return (string)obj.GetValue(TargetProperty);
        }
    }
}