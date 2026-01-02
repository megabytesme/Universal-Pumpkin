using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Universal_Pumpkin.Models;
using Windows.UI;

namespace Universal_Pumpkin.Helpers
{
    public static class LogParserHelper
    {
        private static readonly Regex Tokenizer = new Regex(
            @"(\x1b\[[0-9;]*m)|(\x1b\]8;.*?\x1b\\)",
            RegexOptions.Compiled);

        private static readonly Regex UrlDetector = new Regex(
            @"(https?://[^\s]+)",
            RegexOptions.Compiled);

        private static readonly Regex MetadataDetector = new Regex(
            @"^.*?(\d{2}:\d{2}:\d{2})?.*\[(INFO|WARN|ERROR|FATAL|DEBUG|TRACE)\].*?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AnsiStripper = new Regex(
            @"\x1B\[[^@-~]*[@-~]|\x1B\]8;.*?\x1B\\",
            RegexOptions.Compiled);
        
        private static readonly Regex TagStripper = new Regex(
            @"^(\x1b\[[0-9;]*m)*\s*\[(INFO|WARN|ERROR|FATAL|DEBUG|TRACE)\]\s*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static LogEntry Parse(string rawLine)
        {
            var entry = new LogEntry { Message = rawLine };

            string cleanLine = AnsiStripper.Replace(rawLine, "");
            var metaMatch = MetadataDetector.Match(cleanLine);

            if (metaMatch.Success)
            {
                if (metaMatch.Groups[2].Success)
                {
                    entry.Level = metaMatch.Groups[2].Value.ToUpper();
                }

                if (metaMatch.Groups[1].Success && TimeSpan.TryParse(metaMatch.Groups[1].Value, out var time))
                {
                    entry.Timestamp = DateTime.Today.Add(time);
                }
            }
            
            string visualLine = TagStripper.Replace(rawLine, "$1");

            var segments = new List<LogSegment>();
            var currentStyle = new AnsiStyle();
            var linkStack = new Stack<string>();

            int lastIndex = 0;

            foreach (Match match in Tokenizer.Matches(visualLine))
            {
                if (match.Index > lastIndex)
                {
                    string text = visualLine.Substring(lastIndex, match.Index - lastIndex);
                    AddTextSegments(segments, text, currentStyle, linkStack);
                }

                string token = match.Value;

                if (token.StartsWith("\x1b["))
                {
                    ParseAnsiSgr(token, currentStyle);
                }
                else if (token.StartsWith("\x1b]8;"))
                {
                    string content = token.Substring(4, token.Length - 6);
                    int firstSemi = content.IndexOf(';');
                    string url = (firstSemi >= 0) ? content.Substring(firstSemi + 1) : content;

                    if (string.IsNullOrEmpty(url))
                    {
                        if (linkStack.Count > 0) linkStack.Pop();
                    }
                    else
                    {
                        linkStack.Push(url);
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < visualLine.Length)
            {
                string text = visualLine.Substring(lastIndex);
                AddTextSegments(segments, text, currentStyle, linkStack);
            }

            entry.Segments = segments;
            return entry;
        }

        private static void AddTextSegments(List<LogSegment> segments, string text, AnsiStyle style, Stack<string> linkStack)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (linkStack.Count > 0)
            {
                segments.Add(CreateSegment(text, style, linkStack));
                return;
            }

            var matches = UrlDetector.Matches(text);

            if (matches.Count == 0)
            {
                segments.Add(CreateSegment(text, style, linkStack));
                return;
            }

            int lastIdx = 0;
            foreach (Match m in matches)
            {
                if (m.Index > lastIdx)
                {
                    string preText = text.Substring(lastIdx, m.Index - lastIdx);
                    segments.Add(CreateManualSegment(preText, style, null));
                }

                segments.Add(CreateManualSegment(m.Value, style, m.Value));
                lastIdx = m.Index + m.Length;
            }

            if (lastIdx < text.Length)
            {
                string postText = text.Substring(lastIdx);
                segments.Add(CreateManualSegment(postText, style, null));
            }
        }

        private static LogSegment CreateSegment(string text, AnsiStyle style, Stack<string> links)
        {
            string target = null;
            string tooltip = null;

            foreach (var link in links)
            {
                if (link.StartsWith("tooltip:", StringComparison.OrdinalIgnoreCase))
                {
                    if (tooltip == null) tooltip = link.Substring(8);
                }
                else
                {
                    if (target == null) target = link;
                }
            }

            return new LogSegment
            {
                Text = text,
                Style = style.Clone(),
                HyperlinkTarget = target,
                Tooltip = tooltip
            };
        }

        private static LogSegment CreateManualSegment(string text, AnsiStyle style, string targetUrl)
        {
            return new LogSegment
            {
                Text = text,
                Style = style.Clone(),
                HyperlinkTarget = targetUrl,
                Tooltip = null
            };
        }

        private static void ParseAnsiSgr(string seq, AnsiStyle style)
        {
            string content = seq.Substring(2, seq.Length - 3);
            if (string.IsNullOrEmpty(content)) return;

            var parts = content.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int code)) continue;
                switch (code)
                {
                    case 0:
                        style.Bold = false; style.Italic = false;
                        style.Underline = false; style.Strikethrough = false;
                        style.Dim = false; style.Foreground = null;
                        break;
                    case 1: style.Bold = true; break;
                    case 2: style.Dim = true; break;
                    case 3: style.Italic = true; break;
                    case 4: style.Underline = true; break;
                    case 9: style.Strikethrough = true; break;
                    case 22: style.Bold = false; style.Dim = false; break;
                    case 23: style.Italic = false; break;
                    case 24: style.Underline = false; break;
                    case 29: style.Strikethrough = false; break;
                    case 30: style.Foreground = Colors.Black; break;
                    case 31: style.Foreground = Colors.Red; break;
                    case 32: style.Foreground = Colors.Green; break;
                    case 33: style.Foreground = Colors.Goldenrod; break;
                    case 34: style.Foreground = Colors.DodgerBlue; break;
                    case 35: style.Foreground = Colors.Magenta; break;
                    case 36: style.Foreground = Colors.Cyan; break;
                    case 37: style.Foreground = Colors.White; break;
                    case 38:
                        if (i + 4 < parts.Length && parts[i + 1] == "2")
                        {
                            byte r = byte.Parse(parts[i + 2]);
                            byte g = byte.Parse(parts[i + 3]);
                            byte b = byte.Parse(parts[i + 4]);
                            style.Foreground = Color.FromArgb(255, r, g, b);
                            i += 4;
                        }
                        break;
                    case 39: style.Foreground = null; break;
                    case 90: style.Foreground = Colors.Gray; break;
                    case 91: style.Foreground = Colors.Salmon; break;
                    case 92: style.Foreground = Colors.LightGreen; break;
                    case 93: style.Foreground = Colors.Yellow; break;
                    case 94: style.Foreground = Colors.LightBlue; break;
                    case 95: style.Foreground = Colors.Pink; break;
                    case 96: style.Foreground = Colors.LightCyan; break;
                    case 97: style.Foreground = Colors.White; break;
                }
            }
        }
    }
}