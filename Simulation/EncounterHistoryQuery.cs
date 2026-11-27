using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

public enum RecentFactionCallbackKind
{
    None = 0,
    ClearedViolation = 1,
    RepeatedDetentions = 2,
    TookBribe = 3
}

/// <summary>
/// Read-only query helpers over <see cref="GameState.EncounterHistory"/>.
/// Rolling faction pattern queries strictly inspect the previous 1 to 3 shifts
/// (<c>[currentDay - 3 .. currentDay - 1]</c>) and never match by captain name or hull ID.
/// Story-tag queries search the full 28-day campaign log.
/// </summary>
public static class EncounterHistoryQuery
{
    public static string NormalizeFactionPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        string normalized = prefix.Trim().ToUpperInvariant();
        return normalized.EndsWith('-') ? normalized : $"{normalized}-";
    }

    private static bool IsWithinRecentWindow(int recordDay, int currentDay) =>
        currentDay > 1 && recordDay >= currentDay - 3 && recordDay <= currentDay - 1;

    private static bool MatchesOptionalFaction(EncounterRecord record, string? normalizedTarget)
    {
        if (string.IsNullOrEmpty(normalizedTarget))
            return true;

        return string.Equals(
            NormalizeFactionPrefix(record.Faction),
            normalizedTarget,
            StringComparison.OrdinalIgnoreCase);
    }

    public static int CountRecentDetentions(
        IEnumerable<EncounterRecord>? history,
        int currentDay,
        string? factionPrefix = null)
    {
        if (history == null || currentDay <= 1)
            return 0;

        string target = NormalizeFactionPrefix(factionPrefix);
        int count = 0;
        foreach (EncounterRecord record in history)
        {
            if (record == null || !IsWithinRecentWindow(record.Day, currentDay))
                continue;
            if (!MatchesOptionalFaction(record, target))
                continue;
            if (string.Equals(record.Verdict, "Detained", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    public static int CountRecentClearedViolations(
        IEnumerable<EncounterRecord>? history,
        int currentDay,
        string? factionPrefix = null)
    {
        if (history == null || currentDay <= 1)
            return 0;

        string target = NormalizeFactionPrefix(factionPrefix);
        int count = 0;
        foreach (EncounterRecord record in history)
        {
            if (record == null || !IsWithinRecentWindow(record.Day, currentDay))
                continue;
            if (!MatchesOptionalFaction(record, target))
                continue;
            if (record.HadViolation &&
                string.Equals(record.Verdict, "Cleared", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
    }

    public static bool HasRecentBribe(
        IEnumerable<EncounterRecord>? history,
        int currentDay,
        string? factionPrefix = null)
    {
        if (history == null || currentDay <= 1)
            return false;

        string target = NormalizeFactionPrefix(factionPrefix);
        foreach (EncounterRecord record in history)
        {
            if (record == null || !IsWithinRecentWindow(record.Day, currentDay))
                continue;
            if (!MatchesOptionalFaction(record, target))
                continue;
            if (record.TookBribe)
                return true;
        }
        return false;
    }

    public static string? GetMostDetainedRecentFaction(
        IEnumerable<EncounterRecord>? history,
        int currentDay)
    {
        if (history == null || currentDay <= 1)
            return null;

        Dictionary<string, (int Count, int LatestDay)> counts = new(StringComparer.OrdinalIgnoreCase);
        foreach (EncounterRecord record in history)
        {
            if (record == null || !IsWithinRecentWindow(record.Day, currentDay))
                continue;
            if (!string.Equals(record.Verdict, "Detained", StringComparison.OrdinalIgnoreCase))
                continue;

            string faction = NormalizeFactionPrefix(record.Faction);
            if (string.IsNullOrEmpty(faction))
                continue;

            if (counts.TryGetValue(faction, out var existing))
            {
                counts[faction] = (existing.Count + 1, Math.Max(existing.LatestDay, record.Day));
            }
            else
            {
                counts[faction] = (1, record.Day);
            }
        }

        if (counts.Count == 0)
            return null;

        string? bestFaction = null;
        int bestCount = 0;
        int bestDay = 0;
        foreach ((string faction, (int count, int latestDay)) in counts)
        {
            if (bestFaction == null ||
                count > bestCount ||
                (count == bestCount && (latestDay > bestDay ||
                    (latestDay == bestDay && string.CompareOrdinal(faction, bestFaction) < 0))))
            {
                bestFaction = faction;
                bestCount = count;
                bestDay = latestDay;
            }
        }
        return bestFaction;
    }

    public static RecentFactionCallbackKind GetRecentFactionPattern(
        IEnumerable<EncounterRecord>? history,
        int currentDay,
        string factionPrefix)
    {
        if (history == null || currentDay <= 1 || string.IsNullOrWhiteSpace(factionPrefix))
            return RecentFactionCallbackKind.None;

        if (HasRecentBribe(history, currentDay, factionPrefix))
            return RecentFactionCallbackKind.TookBribe;

        if (CountRecentDetentions(history, currentDay, factionPrefix) >= 2)
            return RecentFactionCallbackKind.RepeatedDetentions;

        if (CountRecentClearedViolations(history, currentDay, factionPrefix) >= 1)
            return RecentFactionCallbackKind.ClearedViolation;

        return RecentFactionCallbackKind.None;
    }

    public static EncounterRecord? FindByStoryTag(
        IEnumerable<EncounterRecord>? history,
        string storyTag)
    {
        if (history == null || string.IsNullOrWhiteSpace(storyTag))
            return null;

        EncounterRecord? match = null;
        foreach (EncounterRecord record in history)
        {
            if (record != null &&
                !string.IsNullOrWhiteSpace(record.StoryTag) &&
                string.Equals(record.StoryTag, storyTag, StringComparison.OrdinalIgnoreCase))
            {
                match = record;
            }
        }
        return match;
    }

    public static bool HasStoryTagVerdict(
        IEnumerable<EncounterRecord>? history,
        string storyTag,
        string verdict)
    {
        if (history == null || string.IsNullOrWhiteSpace(storyTag) || string.IsNullOrWhiteSpace(verdict))
            return false;

        foreach (EncounterRecord record in history)
        {
            if (record != null &&
                !string.IsNullOrWhiteSpace(record.StoryTag) &&
                string.Equals(record.StoryTag, storyTag, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(record.Verdict, verdict, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
