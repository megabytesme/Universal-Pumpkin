using Newtonsoft.Json;
using System;

namespace Universal_Pumpkin.Models
{
    public class OpEntry
    {
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("level")] public int Level { get; set; }
        [JsonProperty("bypasses_player_limit")] public bool BypassesPlayerLimit { get; set; }

        [JsonIgnore] public bool IsPendingRemoval { get; set; }
        [JsonIgnore]
        public string HeadUrl => string.IsNullOrEmpty(Uuid) 
            ? null 
            : $"https://api.mineatar.io/face/{Uuid}?scale=8&overlay=true";
    }

    public class BanEntry
    {
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("created")] public string Created { get; set; }
        [JsonProperty("source")] public string Source { get; set; }
        [JsonProperty("expires")] public string Expires { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }

        [JsonIgnore]
        public string HeadUrl => string.IsNullOrEmpty(Uuid) 
            ? null 
            : $"https://api.mineatar.io/face/{Uuid}?scale=8&overlay=true";
    }

    public class IpBanEntry
    {
        [JsonProperty("ip")] public string Ip { get; set; }
        [JsonProperty("created")] public string Created { get; set; }
        [JsonProperty("source")] public string Source { get; set; }
        [JsonProperty("expires")] public string Expires { get; set; }
        [JsonProperty("reason")] public string Reason { get; set; }
    }
}