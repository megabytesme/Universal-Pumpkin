using System;

namespace Universal_Pumpkin.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }

        public override string ToString() => Message;
    }
}