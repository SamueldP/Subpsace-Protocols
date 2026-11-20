using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubspaceProtocols.Core;

public enum HungerTier
{
    LowNoFood = 0,           // 0 PU (0 SC) -- Max SPS cursor sway (triggered after 2+ days without food)
    MediumNutrientPaste = 1, // 4 PU (20 SC) -- Medium SPS cursor sway
    HighTierRations = 2      // 6 PU (30 SC) -- No SPS cursor sway
}

public enum TrafficPowerState
{
    Normal = 0,
    Weakened = 1,
    Dominant = 2,
    Crisis = 3
}

public sealed class DelayedRippleEvent
{
    public string EventId { get; set; } = string.Empty;
    public string SourcePrefix { get; set; } = string.Empty;
    public TrafficPowerState TriggeredState { get; set; } = TrafficPowerState.Normal;
    public int TriggeredOnDay { get; set; }
    public int ActivateOnDay { get; set; } // 2-3 shift lag
    public string DirectiveText { get; set; } = string.Empty;
    public bool AffectsDownstreamHullScorch { get; set; }
}

public sealed class ContrabandPocketItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // "BlankDataSlate", "CopiedDataSlate", "WarpResidueStrip"
    public string TargetShipPrefix { get; set; } = string.Empty;
    public string TargetShipRegistration { get; set; } = string.Empty;
    public string OriginStarSystem { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
}

public sealed class SaveSlotSummary
{
    public int SlotNumber { get; init; }
    public bool Exists { get; init; }
    public int Day { get; init; }
    public int PactScrip { get; init; }
    public int SecurityStrikes { get; init; }
    public bool Day2SuspectDetained { get; init; }
}

/// <summary>
/// Maps every tracked state variable in /design/00_System_Index/_State_Variables.md
/// and persists cleanly to %APPDATA%/SubspaceProtocols/save_slot_1..3.json.
/// Kept strictly decoupled from Raylib rendering and input.
/// </summary>
public sealed class GameState
{
    public const int TotalSaveSlots = 3;

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    // Active Save Slot (1..3) and runtime test flag so --smoke-test never overwrites real saves
    public int ActiveSaveSlot { get; set; } = 1;

    [JsonIgnore]
    public bool SuppressDiskPersistence { get; set; } = false;

    // 1. Progression & Shift Counters
    public int Day { get; set; } = 1;
    public int ShipsProcessedToday { get; set; } = 0;
    public bool BribeOfferedThisShift { get; set; } = false;

    // 1b. Day 2 Hardcoded Suspect ("The Ghost Route") & Day 3 Branching State
    public bool Day2SuspectEncountered { get; set; } = false;
    public bool Day2SuspectDetained { get; set; } = false;
    public string Day2SuspectRegistration { get; set; } = "ORN-442-K";
    public string Day2SuspectDeclaredOrigin { get; set; } = "Oruun-4";
    public string Day2SuspectActualOrigin { get; set; } = "The Ashen Rift";
    public string Day2SuspectPrefix { get; set; } = "ORN-";
    public string Day2SuspectShipName { get; set; } = "LITHOS MONOLITH";
    public bool Day3KordaniOvernightRepApplied { get; set; } = false;
    public bool Day3PreShiftHailCompleted { get; set; } = false;

    // 2. Currency (Pact Scrip / SC), Power Units (PU) & Living Fees
    public int PactScrip { get; set; } = 45;
    public int PowerUnits { get; set; } = 0;
    public int MissedBerthLeaseCount { get; set; } = 0;
    public int IndependentPassageFund { get; set; } = 0;

    // 3. Hunger (3-Tier Food System), Hypoxia, Stim-Caff & Scrubber Filter States
    public HungerTier CurrentHungerTier { get; set; } = HungerTier.MediumNutrientPaste;
    public int DaysWithoutFood { get; set; } = 0;
    public bool IsHypoxic { get; set; } = false;
    public bool StimCaffActive { get; set; } = false;
    public int ScrubberFilterShiftCounter { get; set; } = 0;

    // 4. Clerical Warnings, Fines & Kordani Security Strikes
    public int ClericalWarningsThisShift { get; set; } = 0;
    public int SecurityStrikes { get; set; } = 0;
    public int MaxSecurityStrikesThreshold { get; set; } = Constants.DefaultMaxSecurityStrikes;
    public bool PendingThreeStrikeAudit { get; set; } = false;

    // 5. Espionage, Logs & Contraband Pocket Slot
    public int UnauthorizedLogCount { get; set; } = 0;
    public ContrabandPocketItem? ContrabandPocket { get; set; } = null;
    public bool HasShieldedPocketLining { get; set; } = false;
    public int BlankDataSlatesInQuarters { get; set; } = 0;

    // 6. Counter A: Hidden Faction Reputation (rep_score, starts at 0)
    public Dictionary<string, int> RepScore { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // 7. Counter B: Hidden Galactic Power (power_score, starts at 0)
    public Dictionary<string, int> PowerScore { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // 8. Suzerain-Inspired Delayed Cause-and-Effect Ripple Queue (2-3 Shift Lag)
    public List<DelayedRippleEvent> DelayedRippleQueue { get; set; } = [];

    // 9. Covert Double-Agent & Quest / Ending Flags
    public int DoubleAgentTrust { get; set; } = 0;
    public int CovertTasksCompleted { get; set; } = 0;
    public Dictionary<string, bool> QuestFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> EndingFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // 10. Optional Under-Consideration Variables (_State_Variables.md Section 6)
    public int BrengopKordaniTensionMeter { get; set; } = 0;
    public bool HasKordaniMilitaryInsigniaOnDesk { get; set; } = false;

    public GameState()
    {
        InitializeDefaultScores();
    }

    public void InitializeDefaultScores()
    {
        RepScore = new Dictionary<string, int>(RepScore, StringComparer.OrdinalIgnoreCase);
        PowerScore = new Dictionary<string, int>(PowerScore, StringComparer.OrdinalIgnoreCase);
        QuestFlags = new Dictionary<string, bool>(QuestFlags, StringComparer.OrdinalIgnoreCase);
        EndingFlags = new Dictionary<string, bool>(EndingFlags, StringComparer.OrdinalIgnoreCase);

        foreach (string prefix in Constants.AllTrackedPrefixes)
        {
            if (!RepScore.ContainsKey(prefix))
                RepScore[prefix] = 0;

            if (!PowerScore.ContainsKey(prefix))
                PowerScore[prefix] = 0;
        }

        string[] defaultEndingFlags =
        [
            "PathA_AcrossTheLine",
            "PathB_TheKordaniCoup",
            "PathC_TheBuyout",
            "PathD_TheQuietReturn",
            "PathE_OutIntoTheDark",
            "Fail_SecurityDetainment",
            "Fail_DebtForeclosure"
        ];

        foreach (string flag in defaultEndingFlags)
        {
            if (!EndingFlags.ContainsKey(flag))
                EndingFlags[flag] = false;
        }

        string[] defaultQuestFlags =
        [
            "Day2_GhostRouteEncountered",
            "Day3_SpectrometerRepaired",
            "SideQuest_HelaniCourierWarpSignature",
            "SideQuest_OrunFreighter442",
            "KordaniDestroyOverrideUnlocked"
        ];

        foreach (string flag in defaultQuestFlags)
        {
            if (!QuestFlags.ContainsKey(flag))
                QuestFlags[flag] = false;
        }

        if (string.IsNullOrWhiteSpace(Day2SuspectRegistration))
            Day2SuspectRegistration = "ORN-442-K";
        if (string.IsNullOrWhiteSpace(Day2SuspectDeclaredOrigin))
            Day2SuspectDeclaredOrigin = "Oruun-4";
        if (string.IsNullOrWhiteSpace(Day2SuspectActualOrigin))
            Day2SuspectActualOrigin = "The Ashen Rift";
        if (string.IsNullOrWhiteSpace(Day2SuspectPrefix))
            Day2SuspectPrefix = "ORN-";
        if (string.IsNullOrWhiteSpace(Day2SuspectShipName))
            Day2SuspectShipName = "LITHOS MONOLITH";
    }

    public int GetRep(string prefix) =>
        RepScore.TryGetValue(prefix, out int val) ? val : 0;

    public int GetPower(string prefix) =>
        PowerScore.TryGetValue(prefix, out int val) ? val : 0;

    /// <summary>
    /// Strictly initializes a brand-new save strictly on Day 1, resetting Scrip, warnings,
    /// strikes, spectrometer state, and shift flags cleanly before persisting to save_slot_N.json.
    /// </summary>
    public void ResetForNewSave(int slotNumber)
    {
        ActiveSaveSlot = Math.Clamp(slotNumber, 1, TotalSaveSlots);
        Day = 1;
        ShipsProcessedToday = 0;
        BribeOfferedThisShift = false;

        Day2SuspectEncountered = false;
        Day2SuspectDetained = false;
        Day2SuspectRegistration = "ORN-442-K";
        Day2SuspectDeclaredOrigin = "Oruun-4";
        Day2SuspectActualOrigin = "The Ashen Rift";
        Day2SuspectPrefix = "ORN-";
        Day2SuspectShipName = "LITHOS MONOLITH";
        Day3KordaniOvernightRepApplied = false;
        Day3PreShiftHailCompleted = false;

        PactScrip = 45;
        PowerUnits = 0;
        MissedBerthLeaseCount = 0;
        IndependentPassageFund = 0;

        CurrentHungerTier = HungerTier.MediumNutrientPaste;
        DaysWithoutFood = 0;
        IsHypoxic = false;
        StimCaffActive = false;
        ScrubberFilterShiftCounter = 0;

        ClericalWarningsThisShift = 0;
        SecurityStrikes = 0;
        MaxSecurityStrikesThreshold = Constants.DefaultMaxSecurityStrikes;
        PendingThreeStrikeAudit = false;

        UnauthorizedLogCount = 0;
        ContrabandPocket = null;
        HasShieldedPocketLining = false;
        BlankDataSlatesInQuarters = 0;

        DoubleAgentTrust = 0;
        CovertTasksCompleted = 0;
        BrengopKordaniTensionMeter = 0;
        HasKordaniMilitaryInsigniaOnDesk = false;

        RepScore.Clear();
        PowerScore.Clear();
        DelayedRippleQueue.Clear();
        QuestFlags.Clear();
        EndingFlags.Clear();

        InitializeDefaultScores();
        QuestFlags["Day3_SpectrometerRepaired"] = false;
        SaveToDisk();
    }

    public void CopyFrom(GameState src)
    {
        ActiveSaveSlot = Math.Clamp(src.ActiveSaveSlot, 1, TotalSaveSlots);
        Day = Math.Max(1, src.Day);
        ShipsProcessedToday = src.ShipsProcessedToday;
        BribeOfferedThisShift = src.BribeOfferedThisShift;

        Day2SuspectEncountered = src.Day2SuspectEncountered;
        Day2SuspectDetained = src.Day2SuspectDetained;
        Day2SuspectRegistration = src.Day2SuspectRegistration;
        Day2SuspectDeclaredOrigin = src.Day2SuspectDeclaredOrigin;
        Day2SuspectActualOrigin = src.Day2SuspectActualOrigin;
        Day2SuspectPrefix = src.Day2SuspectPrefix;
        Day2SuspectShipName = src.Day2SuspectShipName;
        Day3KordaniOvernightRepApplied = src.Day3KordaniOvernightRepApplied;
        Day3PreShiftHailCompleted = src.Day3PreShiftHailCompleted;

        PactScrip = src.PactScrip;
        PowerUnits = src.PowerUnits;
        MissedBerthLeaseCount = src.MissedBerthLeaseCount;
        IndependentPassageFund = src.IndependentPassageFund;

        CurrentHungerTier = src.CurrentHungerTier;
        DaysWithoutFood = src.DaysWithoutFood;
        IsHypoxic = src.IsHypoxic;
        StimCaffActive = src.StimCaffActive;
        ScrubberFilterShiftCounter = src.ScrubberFilterShiftCounter;

        ClericalWarningsThisShift = src.ClericalWarningsThisShift;
        SecurityStrikes = src.SecurityStrikes;
        MaxSecurityStrikesThreshold = src.MaxSecurityStrikesThreshold;
        PendingThreeStrikeAudit = src.PendingThreeStrikeAudit;

        UnauthorizedLogCount = src.UnauthorizedLogCount;
        ContrabandPocket = src.ContrabandPocket;
        HasShieldedPocketLining = src.HasShieldedPocketLining;
        BlankDataSlatesInQuarters = src.BlankDataSlatesInQuarters;

        DoubleAgentTrust = src.DoubleAgentTrust;
        CovertTasksCompleted = src.CovertTasksCompleted;
        BrengopKordaniTensionMeter = src.BrengopKordaniTensionMeter;
        HasKordaniMilitaryInsigniaOnDesk = src.HasKordaniMilitaryInsigniaOnDesk;

        RepScore = new Dictionary<string, int>(src.RepScore, StringComparer.OrdinalIgnoreCase);
        PowerScore = new Dictionary<string, int>(src.PowerScore, StringComparer.OrdinalIgnoreCase);
        DelayedRippleQueue = [.. src.DelayedRippleQueue];
        QuestFlags = new Dictionary<string, bool>(src.QuestFlags, StringComparer.OrdinalIgnoreCase);
        EndingFlags = new Dictionary<string, bool>(src.EndingFlags, StringComparer.OrdinalIgnoreCase);

        InitializeDefaultScores();
        // Enforce strict Day 1-2 maintenance tag vs Day 3+ spectrometer activation
        QuestFlags["Day3_SpectrometerRepaired"] = Day >= 3;
    }

    /// <summary>
    /// Advances or jumps to the target day, resetting per-shift clerical warnings to 0,
    /// activating the Day 3+ Warp Field Residue Spectrometer repair, and silently applying
    /// the overnight +2 Kordani rep_score bonus if the Day 2 suspect was detained.
    /// </summary>
    public void AdvanceToDay(int targetDay)
    {
        Day = Math.Max(1, targetDay);
        ShipsProcessedToday = 0;
        ClericalWarningsThisShift = 0;
        BribeOfferedThisShift = false;

        if (Day >= 3)
        {
            QuestFlags["Day3_SpectrometerRepaired"] = true;

            if (Day2SuspectDetained && !Day3KordaniOvernightRepApplied)
            {
                // Silently increment hidden Kordani rep_score by +2 overnight (zero UI notification)
                RepScore["KDF-"] = GetRep("KDF-") + 2;
                Day3KordaniOvernightRepApplied = true;
            }
        }
        else
        {
            QuestFlags["Day3_SpectrometerRepaired"] = false;
        }

        if (Day == 3)
        {
            Day3PreShiftHailCompleted = false;
        }

        SaveToDisk();
    }

    public static string GetSaveDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubspaceProtocols");

    public static string GetSlotFilePath(int slotNumber)
    {
        int clamped = Math.Clamp(slotNumber, 1, TotalSaveSlots);
        return Path.Combine(GetSaveDirectory(), $"save_slot_{clamped}.json");
    }

    public static SaveSlotSummary[] GetAllSlotSummaries()
    {
        SaveSlotSummary[] summaries = new SaveSlotSummary[TotalSaveSlots];
        for (int i = 0; i < TotalSaveSlots; i++)
        {
            int slot = i + 1;
            string path = GetSlotFilePath(slot);
            if (!File.Exists(path))
            {
                summaries[i] = new SaveSlotSummary { SlotNumber = slot, Exists = false };
                continue;
            }

            try
            {
                string json = File.ReadAllText(path);
                GameState? loaded = JsonSerializer.Deserialize<GameState>(json, SaveJsonOptions);
                if (loaded != null)
                {
                    summaries[i] = new SaveSlotSummary
                    {
                        SlotNumber = slot,
                        Exists = true,
                        Day = Math.Max(1, loaded.Day),
                        PactScrip = loaded.PactScrip,
                        SecurityStrikes = loaded.SecurityStrikes,
                        Day2SuspectDetained = loaded.Day2SuspectDetained
                    };
                    continue;
                }
            }
            catch
            {
                // Treat corrupt file as empty
            }

            summaries[i] = new SaveSlotSummary { SlotNumber = slot, Exists = false };
        }
        return summaries;
    }

    public bool LoadSlotIntoCurrent(int slotNumber)
    {
        int slot = Math.Clamp(slotNumber, 1, TotalSaveSlots);
        string path = GetSlotFilePath(slot);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            GameState? loaded = JsonSerializer.Deserialize<GameState>(json, SaveJsonOptions);
            if (loaded != null)
            {
                loaded.ActiveSaveSlot = slot;
                CopyFrom(loaded);
                return true;
            }
        }
        catch
        {
            // Ignore malformed save
        }

        return false;
    }

    public static bool DeleteSlot(int slotNumber)
    {
        string path = GetSlotFilePath(slotNumber);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Non-fatal
        }
        return false;
    }

    public void SaveToDisk()
    {
        if (SuppressDiskPersistence)
            return;

        try
        {
            string dir = GetSaveDirectory();
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, SaveJsonOptions);
            File.WriteAllText(GetSlotFilePath(ActiveSaveSlot), json);
        }
        catch
        {
            // Non-fatal if disk write is restricted
        }
    }
}
