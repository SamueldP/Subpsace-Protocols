namespace SubspaceProtocols.Core;

public static class Constants
{
    // Virtual Framebuffer & Window Dimensions (Exact 16:9 -> 1280x720 Native)
    public const int VirtualWidth = 1280;
    public const int VirtualHeight = 720;
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;

    // Static Aspect-Ratio Sub-Views
    public const int CorridorWidth = 1280;
    public const int CorridorHeight = 548; // 21:9 Walk-to-Work Corridor Strip inside 1280x720

    // Procedural Buffer Grids
    public const int PortraitWidth = 64;
    public const int PortraitHeight = 64;
    public const int ShipWidth = 120;
    public const int ShipHeight = 56;

    // Shift Timing & Station Cycle Calendar (CYC YYYY.MM)
    public const float ShiftDurationSeconds = 270.0f; // Stage 4 baseline fallback
    public const int DefaultShiftDurationDay1 = 210;       // 3m 30s
    public const int DefaultShiftDurationDays2To4 = 240;   // 4m 00s
    public const int DefaultShiftDurationDays5To9 = 255;   // 4m 15s
    public const int DefaultShiftDurationDays10To15 = 270; // 4m 30s
    public const int DefaultShiftDurationDays16Plus = 285; // 4m 45s
    public const int MaxCampaignDay = 25;
    public const int PreGeneratedQueueSize = 16;      // Pre-loaded deterministic daily shift queue
    public const int BaseStationCycleYear = 2409;
    public const int BaseStationCycleMonth = 4;

    public static (int Year, int Month) GetStationCycle(int day)
    {
        int monthOffset = Math.Max(0, day - 1) / 30;
        int totalMonths = BaseStationCycleYear * 12 + (BaseStationCycleMonth - 1) + monthOffset;
        return (totalMonths / 12, (totalMonths % 12) + 1);
    }

    public static string FormatStationCycle(int year, int month) =>
        $"{year:D4}.{month:D2}";

    public static int CompareCycle(int yearA, int monthA, int yearB, int monthB) =>
        (yearA * 12 + monthA).CompareTo(yearB * 12 + monthB);

    // Economy & Penalties (Pure Pact Scrip / SC Economy; 10 SC default base pay per accurately processed ship)
    public const int BasePayPerShipScrip = 10;
    public const int BerthLeaseNightlyScrip = 30;
    public const int MaxMissedBerthLeaseNights = 2;

    public const int FreeClericalWarningsPerShift = 2;
    public const int ClericalErrorFineScrip = 10;
    public const int SecurityStrikesAuditThreshold = 3;

    public const int DryRationBricksCostScrip = 10;
    public const int NutrientPasteCostScrip = 20;
    public const int HighTierRationsCostScrip = 30;
    public const int ScrubSecurityLogsCostScrip = 15;

    public const int BlankDataSlateCostScrip = 15;
    public const int ShieldedPocketLiningCostScrip = 90;
    public const int BrengopAuditDeferralCostScrip = 50;
    public const int IndependentPassageFundTarget = 1000;
    public const int IndependentPassageFundTargetScrip = IndependentPassageFundTarget;

    // Reputation Thresholds
    public const int RepLowThreshold = -5;
    public const int RepExtremeHostileThreshold = -9;
    public const int RepHighThreshold = 6;
    public const int RepKordaniDestroyUnlock = 8;

    // Progressive Rule Escalation & Hardware Unlock Days
    public const int DayUnlockSpsAndCombat = 2;
    public const int DayUnlockWarpAndStrikes = 3;
    public const int DefaultDayUnlockManualPart1 = 5;
    public const int DefaultDayUnlockManualHulls = 7;
    public const int DefaultDayUnlockWaferCargo = 10;
    public const int DefaultDayUnlockStoker = 13;
    public const int DefaultDayUnlockFaceBribes = 16;
    public const int DefaultDayUnlockCargoBioScan = 19;
    public const int DefaultDayUnlockHostilityDestroy = 20;
    public const int DefaultDayUnlockTransitPass = 22;
    public const int DefaultDayPactClimax = 23;
    public const int DefaultPocketUpgrade2MinDay = 6;
    public const int DefaultPocketUpgrade3MinDay = 13;

    // Canonical Ship Registration Prefixes (03_Gameplay_Rules/Documents_and_Data/Data_Slate_and_Telemetry.md)
    public static readonly string[] AllTrackedPrefixes =
    [
        "GUV-", // Galactic Union Vessel (Rare Traffic)
        "KRW-", // Krushvak War-Vessel (Mid Traffic)
        "KDF-", // Kordani Defense Fleet (High Traffic)
        "BMC-", // Brengop Mercantile Consortium (High Traffic)
        "HBR-", // Hun-Borian Free Vessel (Rare Traffic, uses FTV- schematic)
        "PRJ-", // Prunjun Salvage Registry (Mid Traffic)
        "ORN-", // Orun Registry Network (Mid Traffic)
        "TLV-", // Tolvak Transit Line (Mid Traffic)
        "FTV-", // Independent Free Traders (High Traffic)
        "FTC-", // Far-Line Transit Corp (Mid Traffic)
        "MNG-", // Mining Guild (High Traffic)
        "ZNI-"  // Zenith Interstellar (Mid Traffic)
    ];
}
