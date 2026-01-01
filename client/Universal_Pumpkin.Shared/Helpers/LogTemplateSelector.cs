using Universal_Pumpkin.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Universal_Pumpkin
{
    public class LogTemplateSelector : DataTemplateSelector
    {
        public DataTemplate InfoTemplate { get; set; }
        public DataTemplate WarnTemplate { get; set; }
        public DataTemplate ErrorTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var entry = item as LogEntry;
            if (entry == null)
                return InfoTemplate;

            switch (entry.Level)
            {
                case "ERROR":
                    return ErrorTemplate;
                case "WARN":
                    return WarnTemplate;
                default:
                    return InfoTemplate;
            }
        }
    }
}