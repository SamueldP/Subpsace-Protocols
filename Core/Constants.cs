namespace SubspaceProtocols.Core;

public static class Constants
{
    // Virtual Framebuffer & Window Dimensions (Exact 16:9 -> 2x Integer Scale = 1920x1080)
    public const int VirtualWidth = 960;
    public const int VirtualHeight = 540;
    public const int DefaultWindowWidth = 1920;
    public const int DefaultWindowHeight = 1080;

    // Static Aspect-Ratio Sub-Views
    public const int CorridorWidth = 960;
    public const int CorridorHeight = 411; // 21:9 Walk-to-Work Corridor Strip

    // Procedural Buffer Grids
    public const int PortraitWidth = 64;
    public const int PortraitHeight = 64;
    public const int ShipWidth = 120;
    public const int ShipHeight = 56;

    // Shift Timing
    public const float ShiftDurationSeconds = 270.0f; // 4m 30s diegetic Brown Dwarf arc
    public const int ExpectedShipsPerDay = 10;

    // Economy & Penalties (00_System_Index/_State_Variables.md & 03_Gameplay_Rules)
    public const int BasePayPerShipScrip = 15;
    public const int ScripPerPowerUnit = 5;
    public const int BerthLeaseNightlyScrip = 30;
    public const int MaxMissedBerthLeaseNights = 2;

    public const int FreeClericalWarningsPerShift = 2;
    public const int ClericalErrorFineScrip = 10;
    public const int SecurityStrikesAuditThreshold = 3;
    public const int DefaultMaxSecurityStrikes = 5;

    public const int NutrientPasteCostPU = 4;      // 20 SC
    public const int HighTierRationsCostPU = 6;    // 30 SC
    public const int DecryptMessageCostPU = 3;     // 15 SC
    public const int EncryptAndSendDataCostPU = 4; // 20 SC
    public const int ScrubSecurityLogsCostPU = 5;  // 25 SC

    public const int BlankDataSlateCostScrip = 15;
    public const int ShieldedPocketLiningCostScrip = 90;
    public const int BrengopAuditDeferralCostScrip = 50;
    public const int IndependentPassageFundTargetScrip = 600;

    // Reputation Thresholds
    public const int RepLowThreshold = -5;
    public const int RepExtremeHostileThreshold = -9;
    public const int RepHighThreshold = 6;
    public const int RepKordaniDestroyUnlock = 8;

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

    public const string UnregisteredPrefix = "UNR-";
}
