namespace SubspaceProtocols.Core;

public enum HungerTier
{
    LowNoFood = 0,       // 0 PU (0 SC) — Max SPS cursor sway (triggered after 2+ days without food)
    MediumNutrientPaste = 1, // 4 PU (20 SC) — Minimal SPS cursor sway
    HighTierRations = 2  // 6 PU (30 SC) — No SPS cursor sway
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

/// <summary>
/// Maps every tracked state variable in /design/00_System_Index/_State_Variables.md.
/// Kept strictly decoupled from Raylib rendering and input.
/// </summary>
public sealed class GameState
{
    // 1. Progression & Shift Counters
    public int Day { get; set; } = 1;
    public int ShipsProcessedToday { get; set; } = 0;
    public bool BribeOfferedThisShift { get; set; } = false;

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
        foreach (string prefix in Constants.AllTrackedPrefixes)
        {
            if (!RepScore.ContainsKey(prefix))
                RepScore[prefix] = 0;

            if (!PowerScore.ContainsKey(prefix))
                PowerScore[prefix] = 0;
        }

        // Canonical Story Path & Failure Ending Flags (00_System_Index/_Project_Overview.md)
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
    }

    public int GetRep(string prefix) =>
        RepScore.TryGetValue(prefix, out int val) ? val : 0;

    public int GetPower(string prefix) =>
        PowerScore.TryGetValue(prefix, out int val) ? val : 0;
}
