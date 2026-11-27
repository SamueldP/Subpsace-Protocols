using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubspaceProtocols.Core;

public enum HungerTier
{
    LowNoFood = 0,           // Dry Ration Bricks (10 SC) / No Food 2+ days -- Max SPS cursor sway
    MediumNutrientPaste = 1, // Nutrient Paste (20 SC) -- Medium SPS cursor sway
    HighTierRations = 2      // High-Tier Rations (30 SC) -- No SPS cursor sway
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
    public const string TypeBlankDataSlate = "BlankDataSlate";
    public const string TypeCopiedDataSlate = "CopiedDataSlate";
    public const string TypeMissionDataSlate = "MissionDataSlate";
    public const string TypeBurntMissionSlate = "BurntMissionSlate";
    public const string TypeShieldedPocketLining = "ShieldedPocketLining";

    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TargetShipPrefix { get; set; } = string.Empty;
    public string TargetShipRegistration { get; set; } = string.Empty;
    public string TargetShipName { get; set; } = string.Empty;
    public string OriginStarSystem { get; set; } = string.Empty;
    public bool IsConcordOrDoppleFlagged { get; set; }
    public bool IsMissionSlate { get; set; }
    public bool IsBurnt { get; set; }
    public string MissionId { get; set; } = string.Empty;
    public string MissionTargetReg { get; set; } = string.Empty;
    public string MissionTargetPrefix { get; set; } = string.Empty;
    public string MissionDescription { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }

    [JsonIgnore]
    public bool IsContraband =>
        !IsBurnt && ItemType is TypeBlankDataSlate or TypeCopiedDataSlate or TypeMissionDataSlate;

    [JsonIgnore]
    public bool IsSellableIntel =>
        !IsBurnt && ItemType == TypeCopiedDataSlate;

    public ContrabandPocketItem Clone() => new()
    {
        ItemId = ItemId,
        ItemType = ItemType,
        DisplayName = DisplayName,
        TargetShipPrefix = TargetShipPrefix,
        TargetShipRegistration = TargetShipRegistration,
        TargetShipName = TargetShipName,
        OriginStarSystem = OriginStarSystem,
        IsConcordOrDoppleFlagged = IsConcordOrDoppleFlagged,
        IsMissionSlate = IsMissionSlate,
        IsBurnt = IsBurnt,
        MissionId = MissionId,
        MissionTargetReg = MissionTargetReg,
        MissionTargetPrefix = MissionTargetPrefix,
        MissionDescription = MissionDescription,
        IsEncrypted = IsEncrypted
    };
}

public enum TutorialEventHook
{
    None = 0,
    OnDay1Start = 1,
    OnDay2Start = 2,
    OnDay3Start = 3,
    OnFirstMorningQuarters = 4,
    OnFirstEveningQuarters = 5
}

/// <summary>
/// Persisted per-save tutorial progression tracker keyed by reusable event hooks
/// (OnDay1Start, OnDay2Start, OnDay3Start, OnFirstMorningQuarters, OnFirstEveningQuarters)
/// so playtest CRT tutorial cards can be swapped cleanly into in-character comms dialogue in Milestone 4.
/// </summary>
public sealed class TutorialFlags
{
    public bool TutorialsEnabled { get; set; } = true;
    public bool Day1StartSeen { get; set; } = false;
    public bool Day2StartSeen { get; set; } = false;
    public bool Day3StartSeen { get; set; } = false;
    public bool FirstMorningQuartersSeen { get; set; } = false;
    public bool FirstEveningQuartersSeen { get; set; } = false;

    public bool HasSeen(TutorialEventHook hook) => hook switch
    {
        TutorialEventHook.OnDay1Start => Day1StartSeen,
        TutorialEventHook.OnDay2Start => Day2StartSeen,
        TutorialEventHook.OnDay3Start => Day3StartSeen,
        TutorialEventHook.OnFirstMorningQuarters => FirstMorningQuartersSeen,
        TutorialEventHook.OnFirstEveningQuarters => FirstEveningQuartersSeen,
        _ => true
    };

    public void MarkSeen(TutorialEventHook hook)
    {
        switch (hook)
        {
            case TutorialEventHook.OnDay1Start:
                Day1StartSeen = true;
                break;
            case TutorialEventHook.OnDay2Start:
                Day2StartSeen = true;
                break;
            case TutorialEventHook.OnDay3Start:
                Day3StartSeen = true;
                break;
            case TutorialEventHook.OnFirstMorningQuarters:
                FirstMorningQuartersSeen = true;
                break;
            case TutorialEventHook.OnFirstEveningQuarters:
                FirstEveningQuartersSeen = true;
                break;
        }
    }

    public void ResetSeenFlags()
    {
        Day1StartSeen = false;
        Day2StartSeen = false;
        Day3StartSeen = false;
        FirstMorningQuartersSeen = false;
        FirstEveningQuartersSeen = false;
    }

    public TutorialFlags Clone() => new()
    {
        TutorialsEnabled = TutorialsEnabled,
        Day1StartSeen = Day1StartSeen,
        Day2StartSeen = Day2StartSeen,
        Day3StartSeen = Day3StartSeen,
        FirstMorningQuartersSeen = FirstMorningQuartersSeen,
        FirstEveningQuartersSeen = FirstEveningQuartersSeen
    };

    public static TutorialEventHook GetHookForDay(int day, int warpUnlockDay = Constants.DefaultDayUnlockWarpScanner)
    {
        if (day <= 1)
            return TutorialEventHook.OnDay1Start;
        if (day < warpUnlockDay)
            return TutorialEventHook.OnDay2Start;
        return TutorialEventHook.OnDay3Start;
    }
}

public sealed class EncounterRecord
{
    [JsonPropertyName("day")] public int Day { get; set; }
    [JsonPropertyName("faction")] public string Faction { get; set; } = string.Empty;
    [JsonPropertyName("species")] public string Species { get; set; } = string.Empty;
    [JsonPropertyName("captainName")] public string CaptainName { get; set; } = string.Empty;
    [JsonPropertyName("shipReg")] public string ShipReg { get; set; } = string.Empty;
    [JsonPropertyName("hadViolation")] public bool HadViolation { get; set; }
    [JsonPropertyName("violationType")] public string? ViolationType { get; set; }
    [JsonPropertyName("verdict")] public string Verdict { get; set; } = "Cleared";
    [JsonPropertyName("tookBribe")] public bool TookBribe { get; set; }
    [JsonPropertyName("storyTag")] public string? StoryTag { get; set; }

    public EncounterRecord Clone() => new()
    {
        Day = Day, Faction = Faction, Species = Species, CaptainName = CaptainName,
        ShipReg = ShipReg, HadViolation = HadViolation, ViolationType = ViolationType,
        Verdict = Verdict, TookBribe = TookBribe, StoryTag = StoryTag
    };
}

public sealed class SaveSlotSummary
{
    public int SlotNumber { get; init; }
    public bool Exists { get; init; }
    public int Day { get; init; }
    public QuartersPhase QuartersPhase { get; init; } = QuartersPhase.Morning;
    public int RunSeed { get; init; }
    public int PactScrip { get; init; }
    public int SecurityStrikes { get; init; }
    public bool SecurityStrikesActive { get; init; }
    public bool Day2SuspectDetained { get; init; }
    public bool GhostRouteSuspectDetained { get; init; }
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

    // Active Save Slot (1..3), Per-Save Random RunSeed, and runtime test flag so --smoke-test never overwrites real saves
    public int ActiveSaveSlot { get; set; } = 1;
    public int RunSeed { get; set; } = 0;

    [JsonIgnore]
    public bool SuppressDiskPersistence { get; set; } = false;

    [JsonIgnore]
    public bool HasActivePlayableSlot { get; set; } = false;

    internal static string? SaveDirectoryOverride { get; set; }

    // 1. Progression, Split Quarters Cycle & Shift Counters
    public int Day { get; set; } = 1;
    public QuartersPhase QuartersPhase { get; set; } = QuartersPhase.Morning;
    public int ShipsProcessedToday { get; set; } = 0;
    public bool BribeOfferedThisShift { get; set; } = false;
    public int ShiftStartScrip { get; set; } = 45;
    public int ShiftGrossPayEarned { get; set; } = 0;
    public int ShiftClericalFinesPaid { get; set; } = 0;
    public int LastCleanAuditFineApplied { get; set; } = 0;
    public ShiftLedgerSummary? LastShiftSummary { get; set; } = null;

    // 1b. Day 3 Hardcoded Suspect ("The Ghost Route") & Day 4 Branching State
    // Preserves Day2SuspectDetained / Day3PreShiftHailCompleted aliases for backward save compatibility.
    private bool _ghostRouteSuspectDetained = false;
    private bool _day4PreShiftHailCompleted = false;

    public bool Day2SuspectEncountered { get; set; } = false;

    public bool Day2SuspectDetained
    {
        get => _ghostRouteSuspectDetained;
        set => _ghostRouteSuspectDetained = value;
    }

    public bool GhostRouteSuspectDetained
    {
        get => _ghostRouteSuspectDetained;
        set => _ghostRouteSuspectDetained = value;
    }

    public string Day2SuspectRegistration { get; set; } = "ORN-442-K";
    public string Day2SuspectDeclaredOrigin { get; set; } = "Oruun-4";
    public string Day2SuspectActualOrigin { get; set; } = "Ashen Rift";
    public string Day2SuspectPrefix { get; set; } = "ORN-";
    public string Day2SuspectShipName { get; set; } = "LITHOS MONOLITH";
    public bool Day3KordaniOvernightRepApplied { get; set; } = false;

    public bool Day3PreShiftHailCompleted
    {
        get => _day4PreShiftHailCompleted;
        set => _day4PreShiftHailCompleted = value;
    }

    public bool Day4PreShiftHailCompleted
    {
        get => _day4PreShiftHailCompleted;
        set => _day4PreShiftHailCompleted = value;
    }

    public bool Day1BrengopEmailRead { get; set; } = false;
    public bool Day1VorstHailCompleted { get; set; } = false;
    public bool Day2NixaHailCompleted { get; set; } = false;
    public bool Day8NixaHailCompleted { get; set; } = false;
    public bool Day12CargoIntroShown { get; set; } = false;
    public bool Day15ForemanHailCompleted { get; set; } = false;
    public List<string> AutoUnfoldedBookletTags { get; set; } = [];

    // 2. Currency (Pure Pact Scrip / SC Economy) & Living Fees
    public int PactScrip { get; set; } = 45;
    [JsonIgnore]
    public int BasePayPerShipScrip { get; set; } = Constants.BasePayPerShipScrip;
    public int MissedBerthLeaseCount { get; set; } = 0;

    // 3. Hunger (3-Tier Food System -- sole driver of SPS cursor sway)
    public HungerTier CurrentHungerTier { get; set; } = HungerTier.MediumNutrientPaste;
    public NightlyRationOrder TonightRationOrder { get; set; } = NightlyRationOrder.None;
    public int DaysWithoutFood { get; set; } = 0;
    public int ConsecutiveDryOrNoFoodDays { get; set; } = 0;

    // 4. Clerical Warnings, Fines, Kordani Security Strikes (/3 cap) & Stellar Stoker Overload Consequences
    public int ClericalWarningsThisShift { get; set; } = 0;
    public int SecurityStrikes { get; set; } = 0;
    public bool SecurityStrikesActive { get; set; } = false;
    public bool PendingThreeStrikeAudit { get; set; } = false;
    public bool StokerOverloadedToday { get; set; } = false;
    public bool PendingStokerBerthSearch { get; set; } = false;
    public bool NextDayPowerBrownout { get; set; } = false;
    public bool LastShiftStokerOverload { get; set; } = false;

    // 5. Espionage, Logs, Upgradeable Contraband Pocket (1->3), Vent Stash & 0500 Service Chute
    public int UnauthorizedLogCount { get; set; } = 0;
    public int PocketCapacity { get; set; } = 1;
    public List<ContrabandPocketItem> PocketItems { get; set; } = [];
    public List<ContrabandPocketItem> VentStashItems { get; set; } = [];
    public List<ContrabandPocketItem> ServiceChuteItems { get; set; } = [];
    public List<ContrabandPocketItem> PendingChuteDeliveries { get; set; } = [];
    public bool HasShieldedPocketLining { get; set; } = false;
    public bool ShieldedLiningOrdered { get; set; } = false;

    // 5b. Personal Terminal Inbox (MESSAGES), Security Hex Logs (CRYPTO / LOGS) & Morning Ship Roster
    public List<TerminalEmail> Emails { get; set; } = [];
    public List<SecurityLogEntry> SecurityLogs { get; set; } = [];
    public List<MorningRosterEntry> TodayMorningRoster { get; set; } = [];
    public int NextLogSequence { get; set; } = 1;
    public int NextItemSequence { get; set; } = 1;

    // 5c. Active Slate Bus Covert Mission Tracking, Terminal Hack Unlock & Forum Event Flags
    public bool TerminalHackedUnlocked { get; set; } = false;
    public bool Day6UnsolicitedDropDelivered { get; set; } = false;
    public string Day6IntroTargetReg { get; set; } = string.Empty;
    public int Day6IntroTargetSlot { get; set; } = -1;
    public string ActiveCovertMissionId { get; set; } = string.Empty;
    public string ActiveCovertTargetReg { get; set; } = string.Empty;
    public string ActiveCovertTargetPrefix { get; set; } = string.Empty;
    public string ActiveCovertTargetShipName { get; set; } = string.Empty;
    public int ActiveCovertDeliveryDay { get; set; } = 0;
    public List<string> DeliveredCovertMissionIds { get; set; } = [];
    public List<string> ActiveInterceptMissionIds { get; set; } = [];
    public List<string> CompletedInterceptMissionIds { get; set; } = [];
    public bool KordaniSabotageAuditOccurred { get; set; } = false;
    public bool BerthSearchAuditOccurred { get; set; } = false;
    public bool ProtectedHullDetainedEver { get; set; } = false;
    public bool AshenRiftEncounteredEver { get; set; } = false;
    public bool Deck4UsedEver { get; set; } = false;

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

    // 11. Reusable Playtest Tutorial Event Hook Tracker (persisted per save, reset on New Game)
    public TutorialFlags TutorialFlags { get; set; } = new();

    // 11b. Synchronized "884-D — MY NOTES" Field Booklet Page & Seen-Page Count
    public int ActiveBookletPageIndex { get; set; } = 0;
    public int SeenBookletPageCount { get; set; } = 0;

    // 12. Persisted Campaign Encounter History Log (all 28 days, atomic inside save_slot_X.json)
    [JsonPropertyName("encounterHistory")]
    public List<EncounterRecord> EncounterHistory { get; set; } = new();

    [JsonIgnore]
    public int UnreadEmailCount => Emails.Count(e => !e.IsRead);

    [JsonIgnore]
    public int UnreadMessageCount => UnreadEmailCount;

    [JsonIgnore]
    public bool HasUnreadEmails => UnreadEmailCount > 0;

    [JsonIgnore]
    public int UnscrubbedLogCount => SecurityLogs.Count(l => l.IsSuspicious && !l.IsPurged);

    [JsonIgnore]
    public bool ReplicatorDispensedTonight => TonightRationOrder != NightlyRationOrder.None;

    [JsonIgnore]
    public bool IsStokerCornerFlickerActive =>
        (QuartersPhase == QuartersPhase.Evening && StokerOverloadedToday) ||
        (QuartersPhase == QuartersPhase.Morning && NextDayPowerBrownout);

    public GameState()
    {
        RunSeed = GenerateFreshRunSeed(0);
        InitializeDefaultScores();
    }

    public static int GenerateFreshRunSeed(int previousSeed)
    {
        int candidate = Random.Shared.Next(1, int.MaxValue);
        if (candidate == previousSeed)
        {
            candidate = Random.Shared.Next(1, int.MaxValue);
            if (candidate == previousSeed)
            {
                candidate = previousSeed == int.MaxValue - 1 ? 1 : previousSeed + 1;
            }
        }
        return candidate;
    }

    public string NextUniqueItemId(string prefix) =>
        $"{prefix}_{Day:D2}_{NextItemSequence++:D3}";

    public SecurityLogEntry AddSecurityLog(string tag, string description, bool isSuspicious = true)
    {
        int seq = NextLogSequence++;
        int hi = (0x9E + (Day * 3 + seq)) & 0xFF;
        int lo = (0x1A0 + seq * 13) & 0xFFF;
        SecurityLogEntry entry = new()
        {
            HexAddr = $"0x{hi:X2}:{lo:X3}",
            Day = Day,
            Tag = tag,
            Description = description,
            IsSuspicious = isSuspicious,
            IsPurged = false
        };
        SecurityLogs.Insert(0, entry);
        if (SecurityLogs.Count > 64)
        {
            SecurityLogs.RemoveAt(SecurityLogs.Count - 1);
        }

        if (isSuspicious)
        {
            UnauthorizedLogCount = UnscrubbedLogCount;

            // Fail-safe against Day 6+ unscrubbable audit trap: if unauthorized I/O occurs on Day 6+,
            // ensure CRYPTO / LOGS & DECK 4 are unlocked so SCRUB LOGS is always accessible before sleep.
            if (Day >= 6 && !TerminalHackedUnlocked)
            {
                TerminalHackedUnlocked = true;
                if (!CompletedInterceptMissionIds.Contains("day6_mng_intro", StringComparer.OrdinalIgnoreCase) &&
                    !ActiveInterceptMissionIds.Contains("day6_mng_intro", StringComparer.OrdinalIgnoreCase))
                {
                    ActiveInterceptMissionIds.Add("day6_mng_intro");
                }
            }
        }
        return entry;
    }

    public void AddEmail(TerminalEmail email)
    {
        if (string.IsNullOrWhiteSpace(email.EmailId))
        {
            email.EmailId = $"mail_d{Day:D2}_{Emails.Count + 1:D3}";
        }
        if (Emails.Any(e => e.EmailId.Equals(email.EmailId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        Emails.Insert(0, email);
    }

    public void AppendEncounterRecord(EncounterRecord record)
    {
        if (record == null)
            return;

        EncounterHistory ??= [];
        EncounterHistory.Add(record.Clone());
    }

    public void InitializeDefaultScores()
    {
        RepScore = new Dictionary<string, int>(RepScore, StringComparer.OrdinalIgnoreCase);
        PowerScore = new Dictionary<string, int>(PowerScore, StringComparer.OrdinalIgnoreCase);
        QuestFlags = new Dictionary<string, bool>(QuestFlags, StringComparer.OrdinalIgnoreCase);
        EndingFlags = new Dictionary<string, bool>(EndingFlags, StringComparer.OrdinalIgnoreCase);

        PocketItems ??= [];
        VentStashItems ??= [];
        ServiceChuteItems ??= [];
        PendingChuteDeliveries ??= [];
        PocketItems.RemoveAll(i => !i.IsContraband);
        VentStashItems.RemoveAll(i => !i.IsContraband);
        ServiceChuteItems.RemoveAll(i => !i.IsContraband && i.ItemType != ContrabandPocketItem.TypeShieldedPocketLining);
        PendingChuteDeliveries.RemoveAll(i => !i.IsContraband && i.ItemType != ContrabandPocketItem.TypeShieldedPocketLining);
        Emails ??= [];
        SecurityLogs ??= [];
        TodayMorningRoster ??= [];
        DeliveredCovertMissionIds ??= [];
        ActiveInterceptMissionIds ??= [];
        CompletedInterceptMissionIds ??= [];
        DelayedRippleQueue ??= [];
        PocketCapacity = Math.Clamp(PocketCapacity, 1, 3);

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
            "Day4_SpectrometerRepaired",
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
            Day2SuspectActualOrigin = "Ashen Rift";
        if (string.IsNullOrWhiteSpace(Day2SuspectPrefix))
            Day2SuspectPrefix = "ORN-";
        if (string.IsNullOrWhiteSpace(Day2SuspectShipName))
            Day2SuspectShipName = "LITHOS MONOLITH";

        TutorialFlags ??= new TutorialFlags();
        EncounterHistory ??= [];
    }

    public int GetRep(string prefix) =>
        RepScore.TryGetValue(prefix, out int val) ? val : 0;

    public int GetPower(string prefix) =>
        PowerScore.TryGetValue(prefix, out int val) ? val : 0;

    /// <summary>
    /// Path E ("Out into the Dark") 1,000 SC requirement is evaluated directly against live spendable PactScrip.
    /// </summary>
    public bool IsPathEScripRequirementMet(ConfigFlags? config = null) =>
        PactScrip >= Math.Max(100, config?.IndependentPassageFundTarget ?? Constants.IndependentPassageFundTarget);

    /// <summary>
    /// Enters a sandboxed throwaway preview state with SuppressDiskPersistence = true
    /// so actions taken in Main Menu BERTH PREVIEW can never overwrite save_slot_1.json.
    /// </summary>
    public void StartSandboxedBerthPreview()
    {
        SuppressDiskPersistence = true;
        ResetForNewSave(1);
        ActiveSaveSlot = 0;
        HasActivePlayableSlot = false;
    }

    /// <summary>
    /// Strictly initializes a brand-new save on Day 1 Morning Quarters with a fresh random RunSeed,
    /// 1 mandatory unread Brengop reassignment email (blinking amber LED on Day 1 Morning), PocketCapacity = 1,
    /// and clean economy/strike/audit state before persisting to save_slot_N.json.
    /// </summary>
    public void ResetForNewSave(int slotNumber)
    {
        ActiveSaveSlot = Math.Clamp(slotNumber, 1, TotalSaveSlots);
        HasActivePlayableSlot = true;
        RunSeed = GenerateFreshRunSeed(RunSeed);
        Day = 1;
        QuartersPhase = QuartersPhase.Morning;
        ShipsProcessedToday = 0;
        BribeOfferedThisShift = false;
        ShiftStartScrip = 45;
        ShiftGrossPayEarned = 0;
        ShiftClericalFinesPaid = 0;
        LastCleanAuditFineApplied = 0;
        LastShiftSummary = null;

        Day2SuspectEncountered = false;
        Day2SuspectDetained = false;
        Day2SuspectRegistration = "ORN-442-K";
        Day2SuspectDeclaredOrigin = "Oruun-4";
        Day2SuspectActualOrigin = "Ashen Rift";
        Day2SuspectPrefix = "ORN-";
        Day2SuspectShipName = "LITHOS MONOLITH";
        Day3KordaniOvernightRepApplied = false;
        Day3PreShiftHailCompleted = false;
        Day1BrengopEmailRead = false;
        Day1VorstHailCompleted = false;
        Day2NixaHailCompleted = false;
        Day8NixaHailCompleted = false;
        Day12CargoIntroShown = false;
        Day15ForemanHailCompleted = false;
        AutoUnfoldedBookletTags.Clear();

        PactScrip = 45;
        MissedBerthLeaseCount = 0;

        CurrentHungerTier = HungerTier.MediumNutrientPaste;
        TonightRationOrder = NightlyRationOrder.None;
        DaysWithoutFood = 0;
        ConsecutiveDryOrNoFoodDays = 0;

        ClericalWarningsThisShift = 0;
        SecurityStrikes = 0;
        SecurityStrikesActive = false;
        PendingThreeStrikeAudit = false;
        StokerOverloadedToday = false;
        PendingStokerBerthSearch = false;
        NextDayPowerBrownout = false;
        LastShiftStokerOverload = false;

        UnauthorizedLogCount = 0;
        PocketCapacity = 1;
        PocketItems.Clear();
        VentStashItems.Clear();
        ServiceChuteItems.Clear();
        PendingChuteDeliveries.Clear();
        HasShieldedPocketLining = false;
        ShieldedLiningOrdered = false;

        // Day 1 Morning starts with 1 mandatory unread Brengop email so the physical terminal Unread LED blinks amber
        Emails.Clear();
        AddEmail(TerminalEmail.CreateDay1BrengopReassignmentEmail());
        SecurityLogs.Clear();
        TodayMorningRoster.Clear();
        NextLogSequence = 1;
        NextItemSequence = 1;

        TerminalHackedUnlocked = false;
        Day6UnsolicitedDropDelivered = false;
        Day6IntroTargetReg = string.Empty;
        Day6IntroTargetSlot = -1;
        ActiveCovertMissionId = string.Empty;
        ActiveCovertTargetReg = string.Empty;
        ActiveCovertTargetPrefix = string.Empty;
        ActiveCovertTargetShipName = string.Empty;
        ActiveCovertDeliveryDay = 0;
        DeliveredCovertMissionIds.Clear();
        ActiveInterceptMissionIds.Clear();
        CompletedInterceptMissionIds.Clear();
        KordaniSabotageAuditOccurred = false;
        BerthSearchAuditOccurred = false;
        ProtectedHullDetainedEver = false;
        AshenRiftEncounteredEver = false;
        Deck4UsedEver = false;

        DoubleAgentTrust = 0;
        CovertTasksCompleted = 0;
        BrengopKordaniTensionMeter = 0;
        HasKordaniMilitaryInsigniaOnDesk = false;
        TutorialFlags = new TutorialFlags();
        ActiveBookletPageIndex = 0;
        SeenBookletPageCount = 0;

        RepScore.Clear();
        PowerScore.Clear();
        DelayedRippleQueue.Clear();
        QuestFlags.Clear();
        EndingFlags.Clear();
        EncounterHistory ??= [];
        EncounterHistory.Clear();

        InitializeDefaultScores();
        QuestFlags["Day3_SpectrometerRepaired"] = false;
        QuestFlags["Day4_SpectrometerRepaired"] = false;
        SaveToDisk();
    }

    public void CopyFrom(GameState src)
    {
        ActiveSaveSlot = Math.Clamp(src.ActiveSaveSlot, 1, TotalSaveSlots);
        RunSeed = src.RunSeed != 0 ? src.RunSeed : GenerateFreshRunSeed(RunSeed);
        Day = Math.Max(1, src.Day);
        QuartersPhase = src.QuartersPhase;
        ShipsProcessedToday = src.ShipsProcessedToday;
        BribeOfferedThisShift = src.BribeOfferedThisShift;
        ShiftStartScrip = src.ShiftStartScrip;
        ShiftGrossPayEarned = src.ShiftGrossPayEarned;
        ShiftClericalFinesPaid = src.ShiftClericalFinesPaid;
        LastCleanAuditFineApplied = src.LastCleanAuditFineApplied;
        LastShiftSummary = src.LastShiftSummary?.Clone();

        Day2SuspectEncountered = src.Day2SuspectEncountered;
        Day2SuspectDetained = src.Day2SuspectDetained;
        Day2SuspectRegistration = src.Day2SuspectRegistration;
        Day2SuspectDeclaredOrigin = src.Day2SuspectDeclaredOrigin;
        Day2SuspectActualOrigin = src.Day2SuspectActualOrigin;
        Day2SuspectPrefix = src.Day2SuspectPrefix;
        Day2SuspectShipName = src.Day2SuspectShipName;
        Day3KordaniOvernightRepApplied = src.Day3KordaniOvernightRepApplied;
        Day3PreShiftHailCompleted = src.Day3PreShiftHailCompleted;
        Day1BrengopEmailRead = Day >= 2 || src.Day1BrengopEmailRead;
        Day1VorstHailCompleted = Day >= 2 || src.Day1VorstHailCompleted;
        Day2NixaHailCompleted = Day >= 3 || src.Day2NixaHailCompleted;
        Day8NixaHailCompleted = Day >= 9 || src.Day8NixaHailCompleted;
        Day12CargoIntroShown = Day > Constants.DefaultDayUnlockWaferCargo || src.Day12CargoIntroShown;
        Day15ForemanHailCompleted = Day > Constants.DefaultDayUnlockStoker || src.Day15ForemanHailCompleted;
        AutoUnfoldedBookletTags = src.AutoUnfoldedBookletTags != null ? [.. src.AutoUnfoldedBookletTags] : [];

        PactScrip = src.PactScrip;
        MissedBerthLeaseCount = src.MissedBerthLeaseCount;

        CurrentHungerTier = src.CurrentHungerTier;
        TonightRationOrder = src.TonightRationOrder;
        DaysWithoutFood = src.DaysWithoutFood;
        ConsecutiveDryOrNoFoodDays = src.ConsecutiveDryOrNoFoodDays;

        ClericalWarningsThisShift = src.ClericalWarningsThisShift;
        SecurityStrikes = src.SecurityStrikes;
        SecurityStrikesActive = src.SecurityStrikesActive || Day >= Constants.DefaultDayUnlockWarpScanner;
        PendingThreeStrikeAudit = src.PendingThreeStrikeAudit;
        StokerOverloadedToday = src.StokerOverloadedToday;
        PendingStokerBerthSearch = src.PendingStokerBerthSearch;
        NextDayPowerBrownout = src.NextDayPowerBrownout;
        LastShiftStokerOverload = src.LastShiftStokerOverload;

        UnauthorizedLogCount = src.UnauthorizedLogCount;
        PocketCapacity = Math.Clamp(src.PocketCapacity, 1, 3);
        PocketItems = src.PocketItems?.Select(i => i.Clone()).ToList() ?? [];
        VentStashItems = src.VentStashItems?.Select(i => i.Clone()).ToList() ?? [];
        ServiceChuteItems = src.ServiceChuteItems?.Select(i => i.Clone()).ToList() ?? [];
        PendingChuteDeliveries = src.PendingChuteDeliveries?.Select(i => i.Clone()).ToList() ?? [];
        HasShieldedPocketLining = src.HasShieldedPocketLining;
        ShieldedLiningOrdered = src.ShieldedLiningOrdered;

        Emails = src.Emails?.Select(e => e.Clone()).ToList() ?? [];
        if (Day >= 2)
        {
            foreach (TerminalEmail e in Emails)
            {
                if (e.IsDay1BrengopEmail || e.EmailId.Equals(TerminalEmail.Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase))
                {
                    e.IsRead = true;
                }
            }
        }
        SecurityLogs = src.SecurityLogs?.Select(l => l.Clone()).ToList() ?? [];
        TodayMorningRoster = src.TodayMorningRoster?.Select(r => r.Clone()).ToList() ?? [];
        NextLogSequence = Math.Max(1, src.NextLogSequence);
        NextItemSequence = Math.Max(1, src.NextItemSequence);

        TerminalHackedUnlocked = src.TerminalHackedUnlocked;
        Day6UnsolicitedDropDelivered = src.Day6UnsolicitedDropDelivered;
        Day6IntroTargetReg = src.Day6IntroTargetReg ?? string.Empty;
        Day6IntroTargetSlot = src.Day6IntroTargetSlot is >= 1 and <= 3 ? src.Day6IntroTargetSlot : -1;
        ActiveCovertMissionId = src.ActiveCovertMissionId ?? string.Empty;
        ActiveCovertTargetReg = src.ActiveCovertTargetReg ?? string.Empty;
        ActiveCovertTargetPrefix = src.ActiveCovertTargetPrefix ?? string.Empty;
        ActiveCovertTargetShipName = src.ActiveCovertTargetShipName ?? string.Empty;
        ActiveCovertDeliveryDay = src.ActiveCovertDeliveryDay;
        DeliveredCovertMissionIds = src.DeliveredCovertMissionIds != null ? [.. src.DeliveredCovertMissionIds] : [];
        ActiveInterceptMissionIds = src.ActiveInterceptMissionIds != null ? [.. src.ActiveInterceptMissionIds] : [];
        CompletedInterceptMissionIds = src.CompletedInterceptMissionIds != null ? [.. src.CompletedInterceptMissionIds] : [];
        KordaniSabotageAuditOccurred = src.KordaniSabotageAuditOccurred;
        BerthSearchAuditOccurred = src.BerthSearchAuditOccurred;
        ProtectedHullDetainedEver = src.ProtectedHullDetainedEver;
        AshenRiftEncounteredEver = src.AshenRiftEncounteredEver;
        Deck4UsedEver = src.Deck4UsedEver;

        DoubleAgentTrust = src.DoubleAgentTrust;
        CovertTasksCompleted = src.CovertTasksCompleted;
        BrengopKordaniTensionMeter = src.BrengopKordaniTensionMeter;
        HasKordaniMilitaryInsigniaOnDesk = src.HasKordaniMilitaryInsigniaOnDesk;
        TutorialFlags = (src.TutorialFlags ?? new TutorialFlags()).Clone();
        ActiveBookletPageIndex = Math.Max(0, src.ActiveBookletPageIndex);
        SeenBookletPageCount = Math.Max(0, src.SeenBookletPageCount);

        RepScore = new Dictionary<string, int>(src.RepScore, StringComparer.OrdinalIgnoreCase);
        PowerScore = new Dictionary<string, int>(src.PowerScore, StringComparer.OrdinalIgnoreCase);
        DelayedRippleQueue = [.. src.DelayedRippleQueue];
        QuestFlags = new Dictionary<string, bool>(src.QuestFlags, StringComparer.OrdinalIgnoreCase);
        EndingFlags = new Dictionary<string, bool>(src.EndingFlags, StringComparer.OrdinalIgnoreCase);
        EncounterHistory = src.EncounterHistory?.Select(r => r.Clone()).ToList() ?? [];

        InitializeDefaultScores();
        bool warpUnlocked = Day >= Constants.DefaultDayUnlockWarpScanner;
        QuestFlags["Day3_SpectrometerRepaired"] = warpUnlocked;
        QuestFlags["Day4_SpectrometerRepaired"] = warpUnlocked;
    }

    /// <summary>
    /// Advances or jumps to the target day, resetting per-shift clerical warnings to 0,
    /// activating Day 4+ Warp Field Residue Spectrometer & /3 Security Strikes, and silently applying
    /// the overnight +2 Kordani rep_score bonus if the Day 3 Ghost Route suspect was detained.
    /// </summary>
    public void AdvanceToDay(int targetDay)
    {
        Day = Math.Clamp(targetDay, 1, Constants.MaxCampaignDay);
        QuartersPhase = QuartersPhase.Morning;
        ShipsProcessedToday = 0;
        ClericalWarningsThisShift = 0;
        ShiftGrossPayEarned = 0;
        ShiftClericalFinesPaid = 0;
        BribeOfferedThisShift = false;
        TonightRationOrder = NightlyRationOrder.None;

        SecurityStrikesActive = Day >= Constants.DefaultDayUnlockWarpScanner;
        if (SecurityStrikesActive)
        {
            QuestFlags["Day3_SpectrometerRepaired"] = true;
            QuestFlags["Day4_SpectrometerRepaired"] = true;

            if (GhostRouteSuspectDetained && !Day3KordaniOvernightRepApplied)
            {
                // Silently increment hidden Kordani rep_score by +2 overnight (zero UI notification)
                RepScore["KDF-"] = GetRep("KDF-") + 2;
                Day3KordaniOvernightRepApplied = true;
            }
        }
        else
        {
            QuestFlags["Day3_SpectrometerRepaired"] = false;
            QuestFlags["Day4_SpectrometerRepaired"] = false;
        }

        if (Day <= 1)
        {
            Day1BrengopEmailRead = false;
            Day1VorstHailCompleted = false;
            bool foundDay1Mail = false;
            foreach (TerminalEmail e in Emails)
            {
                if (e.IsDay1BrengopEmail || e.EmailId.Equals(TerminalEmail.Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase))
                {
                    e.IsRead = false;
                    foundDay1Mail = true;
                }
            }
            if (!foundDay1Mail)
            {
                AddEmail(TerminalEmail.CreateDay1BrengopReassignmentEmail());
            }
        }
        else if (Day >= 2)
        {
            Day1BrengopEmailRead = true;
            Day1VorstHailCompleted = true;
            foreach (TerminalEmail e in Emails)
            {
                if (e.IsDay1BrengopEmail || e.EmailId.Equals(TerminalEmail.Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase))
                {
                    e.IsRead = true;
                }
            }
        }

        if (Day == Constants.DefaultDayUnlockWarpScanner)
        {
            Day4PreShiftHailCompleted = false;
        }

        Day2NixaHailCompleted = Day > 2;
        Day8NixaHailCompleted = Day > Constants.DefaultDayUnlockManualPart1;
        Day15ForemanHailCompleted = Day > Constants.DefaultDayUnlockStoker;
        if (Day <= Constants.DefaultDayUnlockWaferCargo)
        {
            Day12CargoIntroShown = false;
        }
        AutoUnfoldedBookletTags.RemoveAll(t =>
            (Day == 1 && t == "day1_shift") ||
            (Day == 2 && t == "day2_sps") ||
            (Day == Constants.DefaultDayUnlockWarpScanner && t == "day4_warp") ||
            (Day == Constants.DefaultDayUnlockManualPart1 && t == "day8_manual") ||
            (Day == Constants.DefaultDayUnlockManualHulls && t == "day10_hulls") ||
            (Day == Constants.DefaultDayUnlockWaferCargo && t == "day12_cargo") ||
            (Day == Constants.DefaultDayUnlockStoker && t == "day15_stoker"));

        if (Day >= 7)
        {
            TerminalHackedUnlocked = true;
        }

        SaveToDisk();
    }

    public static string GetSaveDirectory() =>
        !string.IsNullOrWhiteSpace(SaveDirectoryOverride)
            ? SaveDirectoryOverride
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubspaceProtocols");

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
                        QuartersPhase = loaded.QuartersPhase,
                        RunSeed = loaded.RunSeed,
                        PactScrip = loaded.PactScrip,
                        SecurityStrikes = loaded.SecurityStrikes,
                        SecurityStrikesActive = loaded.SecurityStrikesActive || loaded.Day >= Constants.DefaultDayUnlockWarpScanner,
                        Day2SuspectDetained = loaded.Day2SuspectDetained,
                        GhostRouteSuspectDetained = loaded.GhostRouteSuspectDetained
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
                bool needsSeedBackfill = loaded.RunSeed == 0;
                loaded.ActiveSaveSlot = slot;
                CopyFrom(loaded);
                HasActivePlayableSlot = true;
                if (needsSeedBackfill)
                {
                    SaveToDisk();
                }
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
        if (SuppressDiskPersistence || ActiveSaveSlot < 1 || ActiveSaveSlot > TotalSaveSlots)
            return;

        string path = GetSlotFilePath(ActiveSaveSlot);
        string tmpPath = path + ".tmp";
        try
        {
            string dir = GetSaveDirectory();
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, SaveJsonOptions);
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            // Non-fatal if disk write is restricted; clean up orphan .tmp if present
            try
            {
                if (File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch
            {
                // Ignore secondary cleanup failure
            }
        }
    }
}
