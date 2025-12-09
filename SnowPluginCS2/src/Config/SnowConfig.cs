using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace SnowPluginCS2;

public class SnowConfig : BasePluginConfig
{
    [JsonPropertyName("particle_name")]
    public string ParticleName { get; set; } = "particles/snow.vpcf";

    [JsonPropertyName("CreateSnowOnConnect")]
    public bool CreateSnowOnConnect { get; set; } = false;
}
