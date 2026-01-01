using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Universal_Pumpkin.Helpers
{
    public static class VisualTreeHelperExtensions
    {
        public static T GetFirstDescendantOfType<T>(this DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = GetFirstDescendantOfType<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}