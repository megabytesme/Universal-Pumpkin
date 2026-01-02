using System;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Universal_Pumpkin.Models;
using Windows.UI.Xaml.Controls;

namespace Universal_Pumpkin.Helpers
{
    public class InlineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var entry = value as LogEntry;
            if (entry == null)
                return null;

            var tb = new TextBlock();
            var inlines = tb.Inlines;

            if (entry.Segments == null || entry.Segments.Count == 0)
            {
                inlines.Add(new Run { Text = entry.Message });
                return inlines;
            }

            foreach (var seg in entry.Segments)
            {
                Inline inline;

                if (!string.IsNullOrEmpty(seg.HyperlinkTarget))
                {
                    var link = new Hyperlink();
                    link.Inlines.Add(new Run { Text = seg.Text });
                    HyperlinkExtensions.SetTarget(link, seg.HyperlinkTarget);
                    inline = link;
                }
                else
                {
                    inline = new Run { Text = seg.Text };
                }

                var style = seg.Style;
                if (style != null)
                {
                    if (inline is Run run)
                    {
                        if (style.Bold) run.FontWeight = Windows.UI.Text.FontWeights.Bold;
                        if (style.Italic) run.FontStyle = Windows.UI.Text.FontStyle.Italic;
#if UWP1709
                        if (style.Underline) run.TextDecorations |= Windows.UI.Text.TextDecorations.Underline;
                        if (style.Strikethrough) run.TextDecorations |= Windows.UI.Text.TextDecorations.Strikethrough;
#endif
                        if (style.Foreground.HasValue) run.Foreground = new SolidColorBrush(style.Foreground.Value);
                    }
                    else if (inline is Hyperlink link)
                    {
                        if (style.Foreground.HasValue) link.Foreground = new SolidColorBrush(style.Foreground.Value);
                    }
                }

                inlines.Add(inline);
            }

            return inlines;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
