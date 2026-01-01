using System;
using Universal_Pumpkin.Models;

namespace Universal_Pumpkin
{
    public static class LogParserHelper
    {
        public static LogEntry Parse(string raw)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = "INFO",
                Message = raw
            };

            int firstBracket = raw.IndexOf('[');
            int lastBracket = raw.IndexOf(']');

            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                string level = raw.Substring(firstBracket + 1, lastBracket - firstBracket - 1).Trim();
                entry.Level = level;

                int prefixEnd = raw.IndexOf(']', lastBracket);
                if (prefixEnd < 0) prefixEnd = lastBracket;

                if (prefixEnd + 1 < raw.Length)
                    entry.Message = raw.Substring(prefixEnd + 1).TrimStart();
            }

            return entry;
        }
    }
}