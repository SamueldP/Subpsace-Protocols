using System.Text.Json;
using System.Text.Json.Serialization;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Cached loader and non-repeating line selector for <c>data/dialogue/faction_pools.json</c>.
/// Automatically skips null, whitespace, or <c>[TODO...]</c> writer placeholders and returns null
/// when no playable line exists so Build 4.5 fallback lines stay intact.
/// </summary>
public static class FactionDialoguePool
{
    private sealed class FactionPoolEntry
    {
        [JsonPropertyName("greetings")]
        public List<string>? Greetings { get; set; }

        [JsonPropertyName("excuses")]
        public Dictionary<string, List<string>>? Excuses { get; set; }

        [JsonPropertyName("detentionReactions")]
        public List<string>? DetentionReactions { get; set; }

        [JsonPropertyName("recentCallbacks")]
        public Dictionary<string, List<string>>? RecentCallbacks { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly object Sync = new();
    private static readonly Dictionary<(string Faction, string Category), int> LastPickedIndices = new();
    private static Dictionary<string, FactionPoolEntry> _pools = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static int DiskLoadCount { get; private set; }

    public static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (_loaded)
                return;

            ReloadCore(null);
        }
    }

    public static void Reload(string? overridePath = null)
    {
        lock (Sync)
        {
            ReloadCore(overridePath);
        }
    }

    public static void ResetSelectionMemory()
    {
        lock (Sync)
        {
            LastPickedIndices.Clear();
        }
    }

    public static bool IsPlayableLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        string trimmed = line.TrimStart();
        return !trimmed.StartsWith("[TODO", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryPickGreeting(string factionPrefix, ShipSpec? spec = null) =>
        TryPickFromFactionOrDefault(
            factionPrefix,
            "greetings",
            entry => entry.Greetings,
            spec);

    public static string? TryPickExcuse(string factionPrefix, string infractionKey, ShipSpec? spec = null)
    {
        if (string.IsNullOrWhiteSpace(infractionKey))
            return null;

        string canonicalKey = NormalizeExcuseKey(infractionKey);
        return TryPickFromFactionOrDefault(
            factionPrefix,
            $"excuses:{canonicalKey}",
            entry => ResolveDictionaryList(entry.Excuses, infractionKey, canonicalKey),
            spec);
    }

    public static string? TryPickDetentionReaction(string factionPrefix, ShipSpec? spec = null) =>
        TryPickFromFactionOrDefault(
            factionPrefix,
            "detentionReactions",
            entry => entry.DetentionReactions,
            spec);

    public static string? TryPickRecentCallback(
        string factionPrefix,
        RecentFactionCallbackKind kind,
        ShipSpec? spec = null)
    {
        if (kind == RecentFactionCallbackKind.None)
            return null;

        string key = kind.ToString();
        return TryPickFromFactionOrDefault(
            factionPrefix,
            $"recentCallbacks:{key}",
            entry => ResolveDictionaryList(entry.RecentCallbacks, key, key),
            spec);
    }

    private static void ReloadCore(string? overridePath)
    {
        string path = !string.IsNullOrWhiteSpace(overridePath)
            ? overridePath
            : Path.Combine(AppContext.BaseDirectory, "data", "dialogue", "faction_pools.json");

        Dictionary<string, FactionPoolEntry> parsed = new(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            try
            {
                DiskLoadCount++;
                string json = File.ReadAllText(path);
                Dictionary<string, FactionPoolEntry>? raw =
                    JsonSerializer.Deserialize<Dictionary<string, FactionPoolEntry>>(json, JsonOptions);
                if (raw != null)
                {
                    foreach ((string rawKey, FactionPoolEntry? entry) in raw)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(rawKey))
                            continue;

                        string normalizedKey = rawKey.Trim().Equals("DEFAULT", StringComparison.OrdinalIgnoreCase)
                            ? "DEFAULT"
                            : EncounterHistoryQuery.NormalizeFactionPrefix(rawKey);
                        parsed[normalizedKey] = entry;
                    }
                }
            }
            catch
            {
                parsed.Clear();
            }
        }

        _pools = parsed;
        LastPickedIndices.Clear();
        _loaded = true;
    }

    private static string? TryPickFromFactionOrDefault(
        string factionPrefix,
        string categoryKey,
        Func<FactionPoolEntry, List<string>?> selector,
        ShipSpec? spec)
    {
        lock (Sync)
        {
            if (!_loaded)
            {
                ReloadCore(null);
            }

            string normalizedFaction = NormalizePoolKey(factionPrefix);
            if (!string.IsNullOrEmpty(normalizedFaction) &&
                !string.Equals(normalizedFaction, "DEFAULT", StringComparison.OrdinalIgnoreCase) &&
                _pools.TryGetValue(normalizedFaction, out FactionPoolEntry? factionEntry))
            {
                string? picked = PickPlayableFromList(normalizedFaction, categoryKey, selector(factionEntry), spec);
                if (picked != null)
                    return picked;
            }

            if (_pools.TryGetValue("DEFAULT", out FactionPoolEntry? defaultEntry))
            {
                return PickPlayableFromList("DEFAULT", categoryKey, selector(defaultEntry), spec);
            }

            return null;
        }
    }

    private static string? PickPlayableFromList(
        string poolKey,
        string categoryKey,
        IReadOnlyList<string>? rawLines,
        ShipSpec? spec)
    {
        if (rawLines == null || rawLines.Count == 0)
            return null;

        List<string> playable = new(rawLines.Count);
        for (int i = 0; i < rawLines.Count; i++)
        {
            if (IsPlayableLine(rawLines[i]))
            {
                playable.Add(rawLines[i].Trim());
            }
        }

        if (playable.Count == 0)
            return null;

        int chosenIndex = 0;
        if (playable.Count >= 2)
        {
            if (spec != null && !string.IsNullOrWhiteSpace(spec.EncounterSeedId))
            {
                uint hash = DeterministicRng.HashSeedFnv1a($"{spec.EncounterSeedId}:{poolKey}:{categoryKey}");
                chosenIndex = (int)(hash % (uint)playable.Count);
            }

            (string, string) memoryKey = (poolKey, categoryKey);
            if (LastPickedIndices.TryGetValue(memoryKey, out int previousIndex) && chosenIndex == previousIndex)
            {
                chosenIndex = (chosenIndex + 1) % playable.Count;
            }
            LastPickedIndices[memoryKey] = chosenIndex;
        }
        else
        {
            LastPickedIndices[(poolKey, categoryKey)] = 0;
        }

        return ApplyTokens(playable[chosenIndex], spec);
    }

    private static string ApplyTokens(string template, ShipSpec? spec)
    {
        if (spec == null)
            return template;

        string result = template;
        if (result.Contains("{shipName}", StringComparison.OrdinalIgnoreCase))
            result = result.Replace("{shipName}", spec.Slate.ShipName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result.Contains("{reg}", StringComparison.OrdinalIgnoreCase))
            result = result.Replace("{reg}", spec.Slate.RegistrationCode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result.Contains("{origin}", StringComparison.OrdinalIgnoreCase))
            result = result.Replace("{origin}", spec.Slate.OriginSystem ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (result.Contains("{destination}", StringComparison.OrdinalIgnoreCase))
            result = result.Replace("{destination}", spec.Slate.DestinationSystem ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string NormalizePoolKey(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return "DEFAULT";
        if (prefix.Trim().Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
            return "DEFAULT";
        return EncounterHistoryQuery.NormalizeFactionPrefix(prefix);
    }

    private static string NormalizeExcuseKey(string key) =>
        key.Trim() switch
        {
            "TransponderIdMismatch" => "TransponderMismatch",
            "RefusedShieldsOrWeaponsCompliance" => "General",
            _ => key.Trim()
        };

    private static List<string>? ResolveDictionaryList(
        Dictionary<string, List<string>>? dict,
        string rawKey,
        string canonicalKey)
    {
        if (dict == null || dict.Count == 0)
            return null;

        if (dict.TryGetValue(rawKey, out List<string>? exact))
            return exact;
        if (!string.Equals(rawKey, canonicalKey, StringComparison.OrdinalIgnoreCase) &&
            dict.TryGetValue(canonicalKey, out List<string>? aliased))
            return aliased;

        foreach ((string k, List<string> v) in dict)
        {
            if (string.Equals(k, rawKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(k, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }
        return null;
    }
}
