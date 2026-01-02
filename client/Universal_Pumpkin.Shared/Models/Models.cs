using System;
using System.Collections.Generic;
using Windows.UI;

namespace Universal_Pumpkin.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO";
        public string Message { get; set; }
        public Guid RenderKey { get; } = Guid.NewGuid();
        public List<LogSegment> Segments { get; set; } = new List<LogSegment>();
    }

    public class LogSegment
    {
        public string Text { get; set; }
        public AnsiStyle Style { get; set; }
        public string HyperlinkTarget { get; set; }
        public string Tooltip { get; set; }
    }

    public class AnsiStyle
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strikethrough { get; set; }
        public bool Dim { get; set; }
        public Color? Foreground { get; set; }

        public AnsiStyle Clone()
        {
            return new AnsiStyle
            {
                Bold = this.Bold,
                Italic = this.Italic,
                Underline = this.Underline,
                Strikethrough = this.Strikethrough,
                Dim = this.Dim,
                Foreground = this.Foreground
            };
        }
    }
}