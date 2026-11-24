using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.Data;

public enum ShipInfractionType
{
    None = 0,
    PrefixHullSilhouetteMismatch = 2,
    SpoofedWeaponsHot = 3,
    RefusedShieldsOrWeaponsCompliance = 4,
    FalsifiedOriginWaveform = 5,
    StokerMassMismatch = 6,
    BiometricSpeciesMismatch = 7,
    UndeclaredCargoBioSignature = 8,
    InvalidBrengopTransitPass = 9,
    ExpiredRegistration = 10,
    PrefixOrganizationMismatch = 11,
    PrefixSpeciesMismatch = 12,
    UnexplainedCargoPowerDraw = 15,
    TransponderIdMismatch = 16
}

public sealed class ShipInfraction
{
    public ShipInfractionType Type { get; set; } = ShipInfractionType.None;
    public string Code { get; set; } = string.Empty;
    public string CitationReason { get; set; } = string.Empty;
}

/// <summary>
/// Deterministic physical ground truth stored alongside DataSlateModel so
/// viewport rendering, SPS internal power-grid conduits, and validation are 100% bug-free.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShipGroundTruth
{
    // Live physical shield & power-grid states
    public bool ActualShieldsUp { get; set; } = false;
    public bool ActualWeaponsHot { get; set; } = false;
    public bool ActualWarpPrimed { get; set; } = true;
    public bool ActualCargoPowerDraw { get; set; } = false;
    public bool ActualCommsPowered { get; set; } = true;

    // Physical hull, raw transponder & mass truth
    public string RawTransponderId { get; set; } = string.Empty;
    public ShipFaction ActualVisualFaction { get; set; } = ShipFaction.GalacticUnion;
    public ShipClass ActualShipClass { get; set; } = ShipClass.Civilian;
    public bool IsClassSilhouetteMismatch { get; set; } = false;
    public int ActualMass { get; set; } = 420;

    // Hull & viewscreen visual states
    public bool IsDecommissionedHull { get; set; } = false;
    public bool HasDownstreamHullScorch { get; set; } = false;
    public bool UsesStaticViewscreen { get; set; } = false;

    // Pre-clearance compliance & spoofing truth
    public bool WillRefuseShieldsCompliance { get; set; } = false;
    public bool WillRefuseWeaponsCompliance { get; set; } = false;
    public bool HasSpoofedWeapons { get; set; } = false;

    // Captain & route ground truth (for Day 3+ / Day 6+ progressive checks)
    public PortraitSpecies ActualCaptainSpecies { get; set; } = PortraitSpecies.Human;
    public string ActualOriginSystem { get; set; } = "Veridia";
    public bool IsDay2GhostRouteSuspect { get; set; } = false;
    public bool IsCovertMissionTarget { get; set; } = false;
    public string CovertMissionId { get; set; } = string.Empty;

    /// <summary>
    /// Exact list of active infractions on this vessel.
    /// If empty, the ship is 100% valid (once pre-clearance shields/weapons are lowered).
    /// </summary>
    public List<ShipInfraction> ActiveInfractions { get; set; } = [];

    public bool HasAnyInfraction => ActiveInfractions.Count > 0;
    public bool IsValid => ActiveInfractions.Count == 0;

    /// <summary>
    /// True when exterior weapon mounts glow crimson (#dc2626) in normal non-SPS live view.
    /// Spoofed weapons conceal their exterior mount glow and claim OFFLINE on the Data Slate,
    /// requiring the Subspace Power-Grid Scanner (SPS) to reveal #ef4444 hot forward conduits.
    /// </summary>
    public bool ExteriorWeaponsGlowVisible => ActualWeaponsHot && !HasSpoofedWeapons;

    /// <summary>
    /// True when exterior shields are up or honest exterior weapons are still active.
    /// Does NOT block subspace protocol download; must be cleared before Grant Access.
    /// </summary>
    public bool HasActiveShieldsOrHonestWeapons => ActualShieldsUp || ExteriorWeaponsGlowVisible;
}

/// <summary>
/// Complete vessel encounter specification combining the inspectable 3:4 DataSlateModel,
/// deterministic ShipGroundTruth, and modular captain dialogue lines.
/// </summary>
public sealed class ShipSpec
{
    public string EncounterSeedId { get; set; } = string.Empty;
    public int QueueIndex { get; set; }
    public DataSlateModel Slate { get; set; } = new();
    public ShipGroundTruth GroundTruth { get; set; } = new();

    public bool IsValid => GroundTruth.IsValid;
    public bool ProtocolReviewVerified { get; set; } = false;
    public int WeaponsPowerDownOrdersIssued { get; set; } = 0;

    // Rep-tier compliance timing delay (High >= +6: 0.20s, Mid: 0.55s, Low <= -5: 1.35s)
    public float ComplianceDelaySeconds { get; set; } = 0.50f;

    // Modular Dialogue Transcript Lines
    public string GreetingHailLine { get; set; } = string.Empty;
    public string ShieldsLoweredLine { get; set; } = string.Empty;
    public string WeaponsPoweredDownLine { get; set; } = string.Empty;
    public string RefusalLine { get; set; } = string.Empty;
    public string SpoofedWeaponsBluffLine { get; set; } = string.Empty;
    public string SpoofedWeaponsDeflectionLine { get; set; } = string.Empty;
    public string SubspaceUploadLine { get; set; } = string.Empty;
    public string ReTransmitAnnoyedLine { get; set; } = string.Empty;
    public string AutoInterrogationReplyLine { get; set; } = string.Empty;
    public string SuspectRoutePushbackPromptLine { get; set; } = string.Empty;
    public string SuspectRouteDeflectionLine { get; set; } = string.Empty;

    // Protocol Review Confrontation / Interrogation Responses
    public string ConfrontationGuiltyExcuseHull { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseClassMismatch { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseWeapons { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseOrigin { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseExpired { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseOrgMismatch { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseSpeciesMismatch { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseCargoPower { get; set; } = string.Empty;
    public string ConfrontationGuiltyExcuseTransponderId { get; set; } = string.Empty;
    public string ConfrontationInnocentProtest { get; set; } = string.Empty;

    public ShipRenderOptions BuildLiveShipRenderOptions(bool spsEnabled, int animTick) =>
        new()
        {
            SeedId = EncounterSeedId,
            Faction = GroundTruth.ActualVisualFaction,
            RequestedClass = GroundTruth.ActualShipClass,
            Mode = ShipRenderMode.Live,
            SpsEnabled = spsEnabled,
            ShieldsUp = GroundTruth.ActualShieldsUp,
            // In normal live mode, spoofed weapons hide their exterior crimson glow;
            // in SPS mode, internal power conduits expose ActualWeaponsHot (#ef4444).
            WeaponsHot = spsEnabled ? GroundTruth.ActualWeaponsHot : GroundTruth.ExteriorWeaponsGlowVisible,
            IsDecommissioned = GroundTruth.IsDecommissionedHull,
            WarpPrimed = GroundTruth.ActualWarpPrimed,
            UncannyWarpPylonPulse = GroundTruth.IsDay2GhostRouteSuspect,
            CommsBroken = !GroundTruth.ActualCommsPowered,
            CargoHighDraw = GroundTruth.ActualCargoPowerDraw,
            HasDownstreamHullScorch = GroundTruth.HasDownstreamHullScorch,
            AnimTick = animTick
        };

    public PortraitRenderOptions BuildLivePortraitRenderOptions(int animTick) =>
        new()
        {
            SeedId = Slate.PortraitSeedId,
            Species = GroundTruth.ActualCaptainSpecies,
            Mode = GroundTruth.UsesStaticViewscreen ? PortraitRenderMode.Static : PortraitRenderMode.Live,
            FactionPrefix = Slate.DeclaredPrefix,
            IsMolting = Slate.IsMolting,
            DoppleInTrueForm = false,
            AnimTick = animTick
        };
}
