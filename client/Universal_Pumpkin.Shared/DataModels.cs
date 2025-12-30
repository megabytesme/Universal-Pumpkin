using System.Collections.Generic;
using Newtonsoft.Json;

namespace Universal_Pumpkin.Models
{
    public class Vector3
    {
        [JsonProperty("x")] public double X { get; set; }
        [JsonProperty("y")] public double Y { get; set; }
        [JsonProperty("z")] public double Z { get; set; }

        [JsonIgnore] public string FmtX => X.ToString("0.000");
        [JsonIgnore] public string FmtY => Y.ToString("0.00000");
        [JsonIgnore] public string FmtZ => Z.ToString("0.000");
    }

    public class PlayerData
    {
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("ip_address")] public string IpAddress { get; set; }
        [JsonProperty("platform")] public string Platform { get; set; }
        [JsonProperty("gamemode")] public string Gamemode { get; set; }
        [JsonProperty("health")] public float Health { get; set; }
        [JsonProperty("food_level")] public int FoodLevel { get; set; }
        [JsonProperty("saturation")] public float Saturation { get; set; }
        [JsonProperty("exp_level")] public int ExpLevel { get; set; }
        [JsonProperty("exp_progress")] public float ExpProgress { get; set; }
        [JsonProperty("total_exp")] public int TotalExp { get; set; }
        [JsonProperty("permission_level")] public int PermissionLevel { get; set; }
        [JsonProperty("is_on_ground")] public bool IsOnGround { get; set; }
        [JsonProperty("position")] public Vector3 Position { get; set; }
        [JsonProperty("dimension")] public string Dimension { get; set; }
        [JsonProperty("is_sneaking")] public bool IsSneaking { get; set; }
        [JsonProperty("is_sprinting")] public bool IsSprinting { get; set; }
    }

    public class ServerMetrics
    {
        [JsonProperty("tps")] public float TPS { get; set; }
        [JsonProperty("mspt")] public float MSPT { get; set; }
        [JsonProperty("tick_count")] public int TickCount { get; set; }
        [JsonProperty("loaded_chunks")] public int LoadedChunks { get; set; }
        [JsonProperty("player_count")] public int PlayerCount { get; set; }

        [JsonIgnore] public string FmtTPS => TPS.ToString("0.0");
        [JsonIgnore] public string FmtMSPT => MSPT.ToString("0.00") + "ms";
    }

    public class CommandSuggestion
    {
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("tooltip")] public string Tooltip { get; set; }
    }
}