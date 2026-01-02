using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Universal_Pumpkin.Models;

namespace Universal_Pumpkin.Helpers
{
    public static class InlineHelper
    {
        public static Action<string> RunCommandCallback { get; set; }
        public static Action<string> SuggestCommandCallback { get; set; }
        public static Action<string> ChangePageCallback { get; set; }

        public static readonly DependencyProperty RichTextProperty =
            DependencyProperty.RegisterAttached(
                "RichText", typeof(object), typeof(InlineHelper),
                new PropertyMetadata(null, OnRichTextChanged));

        public static void SetRichText(DependencyObject obj, object value)
            => obj.SetValue(RichTextProperty, value);

        public static object GetRichText(DependencyObject obj)
            => obj.GetValue(RichTextProperty);

        private static void OnRichTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBlock tb)) return;

            tb.Inlines.Clear();
            ToolTipService.SetToolTip(tb, null);

            var entry = tb.DataContext as LogEntry;
            if (entry == null) return;

            if (entry.Segments == null || entry.Segments.Count == 0)
            {
                tb.Inlines.Add(new Run { Text = entry.Message });
                return;
            }

            foreach (var seg in entry.Segments)
            {
                Inline inline;

                if (!string.IsNullOrEmpty(seg.HyperlinkTarget))
                {
                    var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
                    link.Inlines.Add(new Run { Text = seg.Text });

                    if (!string.IsNullOrEmpty(seg.Tooltip))
                    {
                        ToolTipService.SetToolTip(link, seg.Tooltip);
                    }

                    link.Click += async (s, args) => await HandleClick(seg.HyperlinkTarget);
                    inline = link;
                }
                else
                {
                    var run = new Run { Text = seg.Text };
                    if (!string.IsNullOrEmpty(seg.Tooltip))
                    {
                        // Only support tooltips on Links/block
                    }
                    inline = run;
                }

                ApplyStyle(inline, seg.Style);
                tb.Inlines.Add(inline);
            }
        }

        private static void ApplyStyle(Inline inline, AnsiStyle style)
        {
            if (style == null) return;

            if (inline is TextElement el)
            {
                if (style.Bold) el.FontWeight = Windows.UI.Text.FontWeights.Bold;
                if (style.Italic) el.FontStyle = Windows.UI.Text.FontStyle.Italic;

                if (style.Foreground.HasValue)
                {
                    var brush = new SolidColorBrush(style.Foreground.Value);
                    if (style.Dim) brush.Opacity = 0.6;
                    el.Foreground = brush;
                }
                else if (style.Dim)
                {
                    el.Foreground = new SolidColorBrush(Windows.UI.Colors.Gray);
                }

                if (inline is Run run)
                {
#if UWP1709
                    if (style.Underline) run.TextDecorations |= Windows.UI.Text.TextDecorations.Underline;
                    if (style.Strikethrough) run.TextDecorations |= Windows.UI.Text.TextDecorations.Strikethrough;
#endif
                }
            }
        }

        private static async Task HandleClick(string target)
        {
            try
            {
                if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    await Launcher.LaunchUriAsync(new Uri(target));
                }
                else if (target.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    await Launcher.LaunchUriAsync(new Uri(target));
                }
                else if (target.StartsWith("mc:copy_to_clipboard:", StringComparison.OrdinalIgnoreCase))
                {
                    var val = target.Substring("mc:copy_to_clipboard:".Length);
                    var dp = new DataPackage();
                    dp.SetText(val);
                    Clipboard.SetContent(dp);
                }
                else if (target.StartsWith("mc:run_command:", StringComparison.OrdinalIgnoreCase))
                {
                    var cmd = target.Substring("mc:run_command:".Length).TrimStart('/');
                    RunCommandCallback?.Invoke(cmd);
                }
                else if (target.StartsWith("mc:suggest_command:", StringComparison.OrdinalIgnoreCase))
                {
                    var cmd = target.Substring("mc:suggest_command:".Length).TrimStart('/');
                    SuggestCommandCallback?.Invoke(cmd);
                }
                else if (target.StartsWith("mc:change_page:", StringComparison.OrdinalIgnoreCase))
                {
                    var page = target.Substring("mc:change_page:".Length);
                    ChangePageCallback?.Invoke(page);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Link Error: {ex.Message}");
            }
        }
    }
}