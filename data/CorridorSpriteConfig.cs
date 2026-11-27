using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubspaceProtocols.Data;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CorridorSpriteRole
{
    PlayerDeck4Worker = 0,
    Deck4Laborer = 1,
    KordaniSentry = 2,
    BrengopClerk = 3,
    OffDutyCaptain = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CorridorFacing
{
    Right = 0,
    Left = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CorridorAnimMode
{
    Idle = 0,
    Walk = 1
}

public sealed class LockedPlayerCharacterSpec
{
    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 4041;

    [JsonPropertyName("species")]
    public string Species { get; set; } = "Human";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "PlayerDeck4Worker";

    [JsonPropertyName("facingDefault")]
    public string FacingDefault { get; set; } = "Right";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "Deck 4 Officer 884-D locked default candidate.";
}

public sealed class NpcProceduralConfig
{
    [JsonPropertyName("proceduralEnabled")]
    public bool ProceduralEnabled { get; set; } = true;

    [JsonPropertyName("roleWeights")]
    public Dictionary<string, int> RoleWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Deck4Laborer"] = 35,
        ["KordaniSentry"] = 20,
        ["BrengopClerk"] = 20,
        ["OffDutyCaptain"] = 25
    };

    [JsonPropertyName("allowedSpecies")]
    public List<string> AllowedSpecies { get; set; } =
    [
        "Human",
        "Helani",
        "Klaasari",
        "Thane",
        "Veldor",
        "Brengop",
        "Kordani",
        "Krushvak",
        "Hun-Borian",
        "Prunjun",
        "Tolvak",
        "Orun"
    ];
}

/// <summary>
/// Persists the player's locked Deck 4 corridor character seed and procedural NPC weights/species
/// to /data/corridor_sprites.json using atomic temp-file writes.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class CorridorSpritesConfig
{
    public const string FileName = "corridor_sprites.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    [JsonPropertyName("lockedPlayerCharacter")]
    public LockedPlayerCharacterSpec LockedPlayerCharacter { get; set; } = new();

    [JsonPropertyName("npcProceduralConfig")]
    public NpcProceduralConfig NpcProceduralConfig { get; set; } = new();

    public static string GetDefaultFilePath() =>
        Path.Combine(DataLoader.GetDataDirectory(), FileName);

    public static CorridorSpritesConfig Load(string? explicitPath = null)
    {
        string path = string.IsNullOrWhiteSpace(explicitPath) ? GetDefaultFilePath() : explicitPath;
        if (!File.Exists(path))
        {
            return new CorridorSpritesConfig();
        }

        try
        {
            string json = File.ReadAllText(path);
            CorridorSpritesConfig loaded = JsonSerializer.Deserialize<CorridorSpritesConfig>(json, JsonOptions)
                ?? new CorridorSpritesConfig();
            loaded.NormalizeDefaults();
            return loaded;
        }
        catch
        {
            return new CorridorSpritesConfig();
        }
    }

    public void NormalizeDefaults()
    {
        LockedPlayerCharacter ??= new LockedPlayerCharacterSpec();
        if (string.IsNullOrWhiteSpace(LockedPlayerCharacter.Species))
            LockedPlayerCharacter.Species = "Human";
        if (string.IsNullOrWhiteSpace(LockedPlayerCharacter.Role))
            LockedPlayerCharacter.Role = "PlayerDeck4Worker";
        if (string.IsNullOrWhiteSpace(LockedPlayerCharacter.FacingDefault))
            LockedPlayerCharacter.FacingDefault = "Right";

        NpcProceduralConfig ??= new NpcProceduralConfig();
        NpcProceduralConfig.RoleWeights ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (NpcProceduralConfig.RoleWeights.Count == 0)
        {
            NpcProceduralConfig.RoleWeights["Deck4Laborer"] = 35;
            NpcProceduralConfig.RoleWeights["KordaniSentry"] = 20;
            NpcProceduralConfig.RoleWeights["BrengopClerk"] = 20;
            NpcProceduralConfig.RoleWeights["OffDutyCaptain"] = 25;
        }

        NpcProceduralConfig.AllowedSpecies ??= [];
        if (NpcProceduralConfig.AllowedSpecies.Count == 0)
        {
            NpcProceduralConfig.AllowedSpecies.AddRange(["Human", "Kordani", "Brengop", "Klaasari", "Thane", "Veldor", "Krushvak", "Orun"]);
        }
    }

    public bool SaveAtomic(string? explicitPath = null)
    {
        NormalizeDefaults();
        string primaryPath = string.IsNullOrWhiteSpace(explicitPath) ? GetDefaultFilePath() : explicitPath;
        bool primaryOk = WriteFileAtomic(primaryPath, this);

        // When running inside `dotnet run` (AppContext.BaseDirectory == bin/<cfg>/net8.0/),
        // also mirror the atomic save back to the project's /data/corridor_sprites.json if present.
        if (string.IsNullOrWhiteSpace(explicitPath))
        {
            try
            {
                string repoDataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data"));
                string repoConfigSentinel = Path.Combine(repoDataDir, "config.json");
                string repoTarget = Path.Combine(repoDataDir, FileName);
                if (Directory.Exists(repoDataDir) &&
                    File.Exists(repoConfigSentinel) &&
                    !Path.GetFullPath(repoTarget).Equals(Path.GetFullPath(primaryPath), StringComparison.OrdinalIgnoreCase))
                {
                    WriteFileAtomic(repoTarget, this);
                }
            }
            catch
            {
                // Non-fatal in published builds or read-only mounts
            }
        }

        return primaryOk;
    }

    private static bool WriteFileAtomic(string targetPath, CorridorSpritesConfig model)
    {
        try
        {
            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(model, JsonOptions) + Environment.NewLine;
            string tmpPath = $"{targetPath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, targetPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
