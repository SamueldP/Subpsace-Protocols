using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.Data;

/// <summary>
/// Inspectable fields displayed on a vessel's 3:4 vertical Data Slate
/// after subspace protocol transmission ("Send subspace protocols.").
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public sealed class DataSlateModel
{
    public string ShipName { get; set; } = string.Empty;

    /// <summary>
    /// Full Registration Code combining canonical prefix + random suffix (Part 2.5),
    /// e.g., "GUV-771-A", "ORN-442819", "KDF-304-V", or "UNR-901-X".
    /// </summary>
    public string RegistrationCode { get; set; } = string.Empty;

    /// <summary>
    /// Canonical 4-character registration prefix (e.g., "GUV-", "KDF-", "BMC-", "FTC-", "UNR-").
    /// </summary>
    public string DeclaredPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Declared Faction or Commercial Organization display name.
    /// </summary>
    public string DeclaredFactionName { get; set; } = string.Empty;

    /// <summary>
    /// Expected hull visual faction schematic associated with DeclaredPrefix
    /// (Note: HBR- canonically maps to ShipFaction.FreeTraders / FTV- schematic).
    /// </summary>
    public ShipFaction DeclaredVisualFaction { get; set; } = ShipFaction.GalacticUnion;

    /// <summary>
    /// Declared Ship Class on the manifest (Military, Civilian, Cargo, Diplomatic, HackJob).
    /// </summary>
    public ShipClass DeclaredShipClass { get; set; } = ShipClass.Civilian;

    /// <summary>
    /// Declared Captain Name on the manifest.
    /// </summary>
    public string DeclaredCaptainName { get; set; } = string.Empty;

    /// <summary>
    /// Declared Captain Species on the manifest.
    /// </summary>
    public PortraitSpecies DeclaredCaptainSpecies { get; set; } = PortraitSpecies.Human;

    /// <summary>
    /// Declared Departure / Origin Star System (one of the 8 sector systems).
    /// </summary>
    public string OriginSystem { get; set; } = string.Empty;

    /// <summary>
    /// Declared Destination Star System or Station Dock.
    /// </summary>
    public string DestinationSystem { get; set; } = string.Empty;

    /// <summary>
    /// Declared hull/cargo tonnage in metric tons (stored on Day 1, cross-checked Day 4-5+).
    /// </summary>
    public int DeclaredTonnage { get; set; } = 420;

    /// <summary>
    /// Declared Cargo Manifest summary string.
    /// </summary>
    public string CargoManifest { get; set; } = string.Empty;

    /// <summary>
    /// Declared Purpose of Visit (verified via Day 1 Auto-Interrogation).
    /// </summary>
    public string PurposeOfVisit { get; set; } = string.Empty;

    /// <summary>
    /// Telemetry-reported shield status ("DOWN" or "ACTIVE").
    /// </summary>
    public string DeclaredShieldsStatus { get; set; } = "DOWN";

    /// <summary>
    /// Telemetry-reported weapons status ("OFFLINE" -- may be spoofed if SPS shows hot forward mounts).
    /// </summary>
    public string DeclaredWeaponsStatus { get; set; } = "OFFLINE";

    // =========================================================================
    // Procedural Holo-Portrait Parameters (64x64 Data Slate ID Photo)
    // =========================================================================
    public string PortraitSeedId { get; set; } = string.Empty;
    public PortraitSpecies HoloPortraitSpecies { get; set; } = PortraitSpecies.Human;
    public bool HoloAmber { get; set; } = false;
    public bool IsMolting { get; set; } = false;
    public bool CrudeCrayon { get; set; } = false;

    // =========================================================================
    // Progressive Rule Escalation Hooks (Day 6+ Waiver, Encrypted Bribes)
    // =========================================================================
    public bool VisualWaiverAuthorized { get; set; } = false;
    public int BribeOfferScrip { get; set; } = 0;
    public bool IsInternalAffairsSting { get; set; } = false;

    public PortraitRenderOptions BuildHoloPortraitOptions() =>
        new()
        {
            SeedId = PortraitSeedId,
            Species = HoloPortraitSpecies,
            Mode = PortraitRenderMode.Holo,
            FactionPrefix = DeclaredPrefix,
            IsMolting = IsMolting,
            DoppleInTrueForm = false,
            CrudeCrayon = CrudeCrayon,
            HoloAmber = HoloAmber
        };
}
