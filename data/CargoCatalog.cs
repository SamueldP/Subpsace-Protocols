using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubspaceProtocols.Data;

public sealed class BannedCargoEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("spsViolet")]
    public bool SpsViolet { get; set; }

    [JsonPropertyName("denseMass")]
    public bool DenseMass { get; set; }

    [JsonPropertyName("bannedBio")]
    public bool BannedBio { get; set; }

    [JsonPropertyName("manualTag")]
    public string ManualTag { get; set; } = string.Empty;
}

/// <summary>
/// Data-driven 3-Stage Cargo/Bio Progression catalog loaded from /data/cargo_catalog.json
/// relative to AppContext.BaseDirectory. Strictly decoupled from Raylib.
/// </summary>
public sealed class CargoCatalog
{
    private static CargoCatalog? _cached;

    [JsonPropertyName("stage1Days")]
    public string Stage1Days { get; set; } = "1-11";

    [JsonPropertyName("stage2Days")]
    public string Stage2Days { get; set; } = "12-20";

    [JsonPropertyName("legalInertPool")]
    public List<string> LegalInertPool { get; set; } = [];

    [JsonPropertyName("controlledLegalBioPool")]
    public List<string> ControlledLegalBioPool { get; set; } = [];

    [JsonPropertyName("bannedCargoList")]
    public List<BannedCargoEntry> BannedCargoList { get; set; } = [];

    public static CargoCatalog Load(bool forceReload = false)
    {
        if (_cached != null && !forceReload)
            return _cached;

        string path = ResolveCatalogPath();
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                CargoCatalog? parsed = JsonSerializer.Deserialize<CargoCatalog>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
                if (parsed != null &&
                    parsed.LegalInertPool.Count > 0 &&
                    parsed.ControlledLegalBioPool.Count > 0 &&
                    parsed.BannedCargoList.Count >= 5)
                {
                    _cached = parsed;
                    return _cached;
                }
            }
            catch
            {
                // Fall back to canonical built-in defaults if JSON cannot be read
            }
        }

        _cached = CreateDefault();
        return _cached;
    }

    public static string ResolveCatalogPath()
    {
        string basePath = Path.Combine(AppContext.BaseDirectory, "data", "cargo_catalog.json");
        if (File.Exists(basePath))
            return basePath;

        string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "cargo_catalog.json");
        if (File.Exists(cwdPath))
            return cwdPath;

        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 5 && dir != null; i++)
        {
            string candidate = Path.Combine(dir.FullName, "data", "cargo_catalog.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return basePath;
    }

    public bool IsBannedCargo(string? cargoName, out BannedCargoEntry? matched)
    {
        matched = null;
        if (string.IsNullOrWhiteSpace(cargoName))
            return false;

        foreach (BannedCargoEntry entry in BannedCargoList)
        {
            if (cargoName.Contains(entry.Name, StringComparison.OrdinalIgnoreCase))
            {
                matched = entry;
                return true;
            }
        }
        return false;
    }

    private static CargoCatalog CreateDefault() =>
        new()
        {
            LegalInertPool =
            [
                "Hull Plating",
                "Refined Ore",
                "Coolant Rods",
                "Ration Bricks",
                "Mining Drills",
                "Conduit Wire",
                "Sensor Relays",
                "Reactor Bearings"
            ],
            ControlledLegalBioPool =
            [
                "Hydro-Grain",
                "Cryo-Fish",
                "Vat Algae",
                "Medical Cultures",
                "Livestock Embryos",
                "Synth-Yeast"
            ],
            BannedCargoList =
            [
                new BannedCargoEntry { Name = "Disruptor Cells", Category = "POWERED", SpsViolet = true, ManualTag = "SPS VIOLET" },
                new BannedCargoEntry { Name = "Unshielded Coils", Category = "POWERED", SpsViolet = true, ManualTag = "SPS VIOLET" },
                new BannedCargoEntry { Name = "Raw Slag Isotope", Category = "ISOTOPE", DenseMass = true, ManualTag = "DENSE MASS" },
                new BannedCargoEntry { Name = "Ashen Spores", Category = "ORGANISM", BannedBio = true, ManualTag = "BANNED BIO" },
                new BannedCargoEntry { Name = "Silt Worms", Category = "ORGANISM", BannedBio = true, ManualTag = "BANNED BIO" }
            ]
        };
}
