using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubspaceProtocols.Core;

public sealed class ConfigFlags
{
    [JsonPropertyName("enableGravityGauge")]
    public bool EnableGravityGauge { get; set; } = true;

    [JsonPropertyName("enableDockingDrone")]
    public bool EnableDockingDrone { get; set; } = false;

    [JsonPropertyName("enableDebugMenu")]
    public bool EnableDebugMenu { get; set; } = true;

    [JsonPropertyName("enableDenyTurnBack")]
    public bool EnableDenyTurnBack { get; set; } = false;

    [JsonPropertyName("enableLifeSupportAndStimCaff")]
    public bool EnableLifeSupportAndStimCaff { get; set; } = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static ConfigFlags Load()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "data", "config.json");
        if (!File.Exists(configPath))
        {
            return new ConfigFlags();
        }

        try
        {
            string json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<ConfigFlags>(json, JsonOptions) ?? new ConfigFlags();
        }
        catch
        {
            return new ConfigFlags();
        }
    }
}
