using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.Simulation;

public enum DebugSpawnValidityMode
{
    Auto = 0,
    ForceValid = 1,
    ForceInvalid = 2
}

/// <summary>
/// Deterministic procedural ship encounter & pre-generated daily shift queue generator.
/// Pre-generates each day's ship queue from a deterministic seed derived from (RunSeed, CurrentDay),
/// injects the Day 2 hardcoded "Ghost Route" suspect, locks outright shield/weapon compliance
/// refusals strictly to Day 14+ & rep_score <= -9, separates honest armed arrivals from liars,
/// and produces ~65% valid vessels and ~35% vessels with active daily infractions.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShipGenerator
{
    public const int Day2GhostRouteQueueSlot = 1; // 2nd ship on Day 2 (after 1 normal ship)

    // The 12 public declared captain species (strictly excludes unlisted Dopples)
    private static readonly PortraitSpecies[] AllDeclaredCaptainSpecies =
    [
        PortraitSpecies.Human,
        PortraitSpecies.Helani,
        PortraitSpecies.Klaasari,
        PortraitSpecies.Thane,
        PortraitSpecies.Veldor,
        PortraitSpecies.Brengop,
        PortraitSpecies.Kordani,
        PortraitSpecies.Krushvak,
        PortraitSpecies.HunBorian,
        PortraitSpecies.Prunjun,
        PortraitSpecies.Tolvak,
        PortraitSpecies.Orun
    ];

    private static readonly (string Prefix, int Weight)[] WeightedPrefixes =
    [
        // High Traffic (~18-25 ships/run)
        ("KDF-", 4),
        ("BMC-", 4),
        ("FTV-", 4),
        ("MNG-", 4),
        // Mid Traffic (~8-15 ships/run)
        ("KRW-", 2),
        ("FTC-", 2),
        ("ZNI-", 2),
        ("TLV-", 2),
        ("ORN-", 2),
        ("PRJ-", 2),
        // Rare Traffic (~4-8 ships/run)
        ("GUV-", 1),
        ("HBR-", 1)
    ];

    // Day 2 Ghost Route eligible prefixes: strictly excludes Galactic Union (GUV-) and Bulwark Pact (KDF-, BMC-)
    private static readonly string[] Day2GhostRouteEligiblePrefixes =
    [
        "KRW-",
        "HBR-",
        "PRJ-",
        "ORN-",
        "TLV-",
        "FTV-",
        "FTC-",
        "MNG-",
        "ZNI-"
    ];

    private static readonly string[] ValidOriginSystems = WarpSignatureGenerator.LegalOriginSystems;

    private static readonly string[] DestinationPorts =
    [
        "VG-3 DOCK BAY",
        "Veridia",
        "Korr-Vath",
        "Dul-Krag",
        "Gharok",
        "Oruun-4"
    ];

    /// <summary>
    /// Pre-generates the entire shift's ship queue deterministically from (RunSeed, CurrentDay).
    /// Every new save rolls a fresh RunSeed so new games see fresh ships while each day's queue
    /// remains consistent within that save run.
    /// </summary>
    public List<ShipSpec> GenerateDailyQueue(
        GameState state,
        int count = Constants.PreGeneratedQueueSize,
        uint reseedNonce = 0)
    {
        int safeCount = Math.Max(1, count);
        List<ShipSpec> queue = new(safeCount);
        HashSet<string> usedRegs = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < safeCount; i++)
        {
            queue.Add(GenerateShipForSlot(state, i, reseedNonce, DebugSpawnValidityMode.Auto, usedRegs));
        }

        return queue;
    }

    /// <summary>
    /// Generates a deterministic vessel for the given (RunSeed, Day, queueIndex) slot.
    /// Injects the hardcoded "Ghost Route" suspect on Day 2 slot 1.
    /// Outright shield/weapon compliance refusals are strictly locked to Day 14+ & rep_score <= -9.
    /// </summary>
    public ShipSpec GenerateShipForSlot(
        GameState state,
        int queueIndex,
        uint reseedNonce = 0,
        DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto,
        HashSet<string>? usedRegs = null)
    {
        int runSeed = state.RunSeed != 0 ? state.RunSeed : 1;

        if (state.Day == 2 && queueIndex == Day2GhostRouteQueueSlot && forceValidity == DebugSpawnValidityMode.Auto)
        {
            return GenerateDay2GhostRouteSuspect(state, queueIndex, reseedNonce, usedRegs);
        }

        string slotSeedKey = $"run{runSeed}-day{state.Day}-q{queueIndex}-n{reseedNonce}";
        DeterministicRng rng = new(slotSeedKey);

        string declaredPrefix = PickWeightedPrefix(rng.Fork("prefix"));
        ShipFaction declaredVisualFaction = ShipRenderer.ParseFactionFromPrefix(declaredPrefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(declaredVisualFaction);
        ShipClass declaredClass = rng.Fork("class").Pick(allowedClasses);

        string regCode = GenerateUniqueRegistration(declaredPrefix, rng.Fork("reg"), usedRegs);
        string rawTransponderId = regCode;
        string shipName = GenerateShipName(declaredPrefix, rng.Fork("name"));
        string originSystem = GetCanonicalOriginForPrefix(declaredPrefix, rng.Fork("origin"));
        string actualOriginSystem = originSystem;
        string destinationSystem = rng.Fork("dest").Pick(DestinationPorts);

        PortraitSpecies captainSpecies = PickSpeciesForPrefix(declaredPrefix, rng.Fork("species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("capt_name"));
        int tonnage = GetBaselineTonnage(declaredClass, rng.Fork("tonnage"));
        (string purpose, string cargo) = PickPurposeAndCargo(declaredPrefix, declaredClass, rng.Fork("cargo"));
        string declaredFactionName = GetFactionFullName(declaredPrefix);

        (int curYear, int curMonth) = Constants.GetStationCycle(state.Day);
        int validFutureMonths = rng.Fork("exp_valid").NextInt(2, 29);
        (int expiryYear, int expiryMonth) = OffsetStationCycle(curYear, curMonth, validFutureMonths);

        int factionRep = state.GetRep(declaredPrefix);

        // Roll compliance: ~65% Valid, ~35% Infraction (or honor Dev Debug ForceValid / ForceInvalid)
        bool isValidShip = forceValidity switch
        {
            DebugSpawnValidityMode.ForceValid => true,
            DebugSpawnValidityMode.ForceInvalid => false,
            _ => rng.Fork("validity_roll").NextFloat() < 0.65f
        };

        ShipFaction actualVisualFaction = declaredVisualFaction;
        ShipClass actualShipClass = declaredClass;
        bool isClassSilhouetteMismatch = false;

        // Day 1 simplification: shields & weapons controls + SPS are powered down on Day 1,
        // so Day 1 vessels arrive shields-down & weapons-cold for pure registration checks.
        bool initialShieldsUp = state.Day >= 2 && rng.Fork("init_shields").NextFloat() < 0.34f;
        bool initialWeaponsHot = state.Day >= 2 && rng.Fork("init_weapons").NextFloat() < 0.28f;
        bool actualCargoPowerDraw = false;
        bool willRefuseShields = false;
        bool willRefuseWeapons = false;
        bool hasSpoofedWeapons = false;

        List<ShipInfraction> infractions = [];

        if (!isValidShip)
        {
            // Staggered Daily Rule Escalation (UNR- & Relay Ping removed):
            // - Day 1:  ONLY Transponder ID (under Viewscreen) vs. Data Slate REG CODE mismatch.
            // - Day 2:  Transponder ID mismatch + Spoofed Weapons Hot (SPS probe unlocked).
            // - Day 3+: Full inspection pool (Transponder ID, Expiry, Prefix/Org, Prefix/Species,
            //           Hull/Class, Spoofed Weapons Hot, Cargo Power Draw, Falsified Origin Waveform).
            // - Day 14+ & rep_score <= -9 ONLY: Adds Outright Shield/Weapon Compliance Refusal.
            List<ShipInfractionType> eligibleInfractions;
            if (state.Day == 1)
            {
                eligibleInfractions = [ShipInfractionType.TransponderIdMismatch];
            }
            else if (state.Day == 2)
            {
                eligibleInfractions =
                [
                    ShipInfractionType.TransponderIdMismatch,
                    ShipInfractionType.SpoofedWeaponsHot
                ];
            }
            else
            {
                eligibleInfractions =
                [
                    ShipInfractionType.TransponderIdMismatch,
                    ShipInfractionType.ExpiredRegistration,
                    ShipInfractionType.PrefixOrganizationMismatch,
                    ShipInfractionType.PrefixHullSilhouetteMismatch,
                    ShipInfractionType.SpoofedWeaponsHot,
                    ShipInfractionType.UnexplainedCargoPowerDraw,
                    ShipInfractionType.FalsifiedOriginWaveform
                ];

                if (IsSpeciesRestrictedPrefix(declaredPrefix))
                {
                    eligibleInfractions.Add(ShipInfractionType.PrefixSpeciesMismatch);
                }

                if (state.Day >= 14 && factionRep <= Constants.RepExtremeHostileThreshold)
                {
                    eligibleInfractions.Add(ShipInfractionType.RefusedShieldsOrWeaponsCompliance);
                }
            }

            ShipInfractionType chosenInfraction = rng.Fork("infraction_type").Pick(eligibleInfractions);

            switch (chosenInfraction)
            {
                case ShipInfractionType.TransponderIdMismatch:
                {
                    rawTransponderId = MutateTransponderId(declaredPrefix, regCode, rng.Fork("raw_id_mismatch"));
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.TransponderIdMismatch,
                        Code = "PROTO-01T",
                        CitationReason = $"RAW TRANSPONDER ID '{rawTransponderId}' DID NOT MATCH SLATE REG CODE '{regCode}'"
                    });
                    break;
                }

                case ShipInfractionType.ExpiredRegistration:
                {
                    int pastMonths = rng.Fork("exp_past").NextInt(1, 19);
                    (expiryYear, expiryMonth) = OffsetStationCycle(curYear, curMonth, -pastMonths);
                    string expFmt = Constants.FormatStationCycle(expiryYear, expiryMonth);
                    string curFmt = Constants.FormatStationCycle(curYear, curMonth);
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.ExpiredRegistration,
                        Code = "PROTO-01D",
                        CitationReason = $"REGISTRATION EXPIRED (CYC {expFmt} < CYC {curFmt})"
                    });
                    break;
                }

                case ShipInfractionType.PrefixOrganizationMismatch:
                {
                    string mismatchedPrefix = PickDifferentTrackedPrefix(declaredPrefix, rng.Fork("org_mismatch"));
                    declaredFactionName = GetFactionFullName(mismatchedPrefix);
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.PrefixOrganizationMismatch,
                        Code = "PROTO-01E",
                        CitationReason = $"PREFIX '{declaredPrefix}' DID NOT MATCH ORG '{declaredFactionName}'"
                    });
                    break;
                }

                case ShipInfractionType.PrefixSpeciesMismatch:
                {
                    captainSpecies = PickInvalidSpeciesForPrefix(declaredPrefix, rng.Fork("species_mismatch"));
                    captainName = GenerateCaptainName(captainSpecies, rng.Fork("species_mismatch_name"));
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.PrefixSpeciesMismatch,
                        Code = "PROTO-01F",
                        CitationReason = $"RESTRICTED PREFIX '{declaredPrefix}' PROHIBITS {captainSpecies.ToString().ToUpperInvariant()} CAPTAIN"
                    });
                    break;
                }

                case ShipInfractionType.PrefixHullSilhouetteMismatch:
                {
                    bool doClassMismatch = allowedClasses.Count >= 2 &&
                                           rng.Fork("hull_or_class_mismatch").NextBool(0.40f);
                    if (doClassMismatch)
                    {
                        List<ShipClass> otherClasses = allowedClasses
                            .Where(c => c != declaredClass)
                            .ToList();
                        actualShipClass = rng.Fork("mismatch_class").Pick(otherClasses);
                        isClassSilhouetteMismatch = true;
                        infractions.Add(new ShipInfraction
                        {
                            Type = ShipInfractionType.PrefixHullSilhouetteMismatch,
                            Code = "PROTO-01A",
                            CitationReason = $"DECLARED CLASS '{GetShortClassCode(declaredClass)}' DID NOT MATCH PHYSICAL {GetShortClassCode(actualShipClass)} HULL"
                        });
                    }
                    else
                    {
                        actualVisualFaction = PickMismatchedVisualFaction(declaredVisualFaction, rng.Fork("mismatch_hull"));
                        actualShipClass = ShipRenderer.NormalizeShipClass(actualVisualFaction, declaredClass);
                        string actualHullName = GetHullFactionReadableName(actualVisualFaction);
                        infractions.Add(new ShipInfraction
                        {
                            Type = ShipInfractionType.PrefixHullSilhouetteMismatch,
                            Code = "PROTO-01A",
                            CitationReason = $"REG PREFIX '{declaredPrefix}' DID NOT MATCH {actualHullName} HULL"
                        });
                    }
                    break;
                }

                case ShipInfractionType.SpoofedWeaponsHot:
                {
                    // Liar: claims WPN: OFFLINE on Data Slate while actual weapons are hot on SPS.
                    initialWeaponsHot = true;
                    hasSpoofedWeapons = true;
                    willRefuseWeapons = false;
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.SpoofedWeaponsHot,
                        Code = "PROTO-01C",
                        CitationReason = "SPOOFED WEAPONS WERE HOT ON SPS POWER GRID"
                    });
                    break;
                }

                case ShipInfractionType.UnexplainedCargoPowerDraw:
                {
                    actualCargoPowerDraw = true;
                    purpose = "DEADHEAD REPOSITIONING";
                    cargo = "EMPTY CARGO HOLD (UNPOWERED)";
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.UnexplainedCargoPowerDraw,
                        Code = "PROTO-01H",
                        CitationReason = "UNEXPLAINED VIOLET CARGO POWER DRAW IN DECLARED EMPTY HOLD"
                    });
                    break;
                }

                case ShipInfractionType.FalsifiedOriginWaveform:
                {
                    actualOriginSystem = PickFalsifiedOriginSystem(originSystem, rng.Fork("false_origin"));
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.FalsifiedOriginWaveform,
                        Code = "PROTO-03A",
                        CitationReason = $"WARP STRIP ORIGIN '{actualOriginSystem.ToUpperInvariant()}' DID NOT MATCH DECLARED '{originSystem.ToUpperInvariant()}'"
                    });
                    break;
                }

                case ShipInfractionType.RefusedShieldsOrWeaponsCompliance:
                {
                    bool refuseShields = rng.Fork("refuse_mode").NextBool(0.55f);
                    if (refuseShields)
                    {
                        initialShieldsUp = true;
                        willRefuseShields = true;
                    }
                    else
                    {
                        initialWeaponsHot = true;
                        hasSpoofedWeapons = false;
                        willRefuseWeapons = true;
                    }

                    string refusedSystem = willRefuseShields ? "LOWER SHIELDS" : "POWER DOWN WEAPONS";
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.RefusedShieldsOrWeaponsCompliance,
                        Code = "PROTO-14A",
                        CitationReason = $"CAPTAIN OUTRIGHT REFUSED COMPLIANCE ORDER TO {refusedSystem}"
                    });
                    break;
                }
            }
        }

        // Downstream hull visual states
        bool isDecomm = ShipRenderer.IsDecommissionedEffective(
            actualVisualFaction,
            actualShipClass,
            actualVisualFaction == ShipFaction.GalacticUnion && rng.Fork("decomm").NextBool(0.55f),
            false);

        TrafficPowerState mngState = WorldSim.EvaluateTrafficPowerState("MNG-", state.GetPower("MNG-"));
        bool hasScorch = actualVisualFaction == ShipFaction.FreeTraders &&
                         mngState is TrafficPowerState.Dominant or TrafficPowerState.Crisis;

        // Days 1-5 unreliable consortium comms hardware allows occasional static viewscreens (~18%)
        bool staticCamera = rng.Fork("static_cam").NextFloat() < 0.18f;
        bool isMolting = captainSpecies == PortraitSpecies.Tolvak && rng.Fork("molt").NextBool(0.5f);

        float complianceDelay = factionRep >= Constants.RepHighThreshold
            ? 0.20f
            : (factionRep <= Constants.RepLowThreshold ? 1.35f : 0.55f);

        string seedId = $"{regCode}-{shipName.Replace(' ', '-')}-{slotSeedKey}";
        string portraitSeed = $"capt-{captainName.Replace(' ', '-')}-{seedId}";

        // Separate honest armed/shielded arrivals from spoofed liars on the Data Slate:
        // Honest armed ships openly declare "ARMED" (and comply on Days 1-13 when ordered);
        // Liars (hasSpoofedWeapons) claim "OFFLINE" while actual weapons remain hot on SPS!
        string declaredShieldsStatus = initialShieldsUp ? "ACTIVE" : "DOWN";
        string declaredWeaponsStatus = (initialWeaponsHot && !hasSpoofedWeapons) ? "ARMED" : "OFFLINE";

        DataSlateModel slate = new()
        {
            ShipName = shipName,
            RegistrationCode = regCode,
            DeclaredPrefix = declaredPrefix,
            DeclaredFactionName = declaredFactionName,
            RegistrationExpiryYear = expiryYear,
            RegistrationExpiryMonth = expiryMonth,
            DeclaredVisualFaction = declaredVisualFaction,
            DeclaredShipClass = declaredClass,
            DeclaredCaptainName = captainName,
            DeclaredCaptainSpecies = captainSpecies,
            OriginSystem = originSystem,
            DestinationSystem = destinationSystem,
            DeclaredTonnage = tonnage,
            CargoManifest = cargo,
            PurposeOfVisit = purpose,
            DeclaredShieldsStatus = declaredShieldsStatus,
            DeclaredWeaponsStatus = declaredWeaponsStatus,
            PortraitSeedId = portraitSeed,
            HoloPortraitSpecies = captainSpecies,
            HoloAmber = declaredPrefix is "BMC-" or "MNG-" or "KRW-",
            IsMolting = isMolting,
            CrudeCrayon = false
        };

        ShipGroundTruth truth = new()
        {
            ActualShieldsUp = initialShieldsUp,
            ActualWeaponsHot = initialWeaponsHot,
            ActualWarpPrimed = true,
            ActualCargoPowerDraw = actualCargoPowerDraw,
            ActualCommsPowered = !staticCamera,
            RawTransponderId = rawTransponderId,
            ActualVisualFaction = actualVisualFaction,
            ActualShipClass = actualShipClass,
            IsClassSilhouetteMismatch = isClassSilhouetteMismatch,
            ActualMass = tonnage,
            IsDecommissionedHull = isDecomm,
            HasDownstreamHullScorch = hasScorch,
            UsesStaticViewscreen = staticCamera,
            WillRefuseShieldsCompliance = willRefuseShields,
            WillRefuseWeaponsCompliance = willRefuseWeapons,
            HasSpoofedWeapons = hasSpoofedWeapons,
            ActualCaptainSpecies = captainSpecies,
            ActualOriginSystem = actualOriginSystem,
            IsDay2GhostRouteSuspect = false,
            ActiveInfractions = infractions
        };

        ShipSpec spec = new()
        {
            EncounterSeedId = seedId,
            QueueIndex = queueIndex,
            Slate = slate,
            GroundTruth = truth,
            ComplianceDelaySeconds = complianceDelay
        };

        PopulateDialogueLines(spec, factionRep);
        return spec;
    }

    /// <summary>
    /// Generates the Day 2 Hardcoded Suspect Encounter ("The Ghost Route") deterministically
    /// from (RunSeed, reseedNonce). Does NOT mutate GameState during pre-generation;
    /// DeskScreen records Day2Suspect fields when the vessel enters the bay / resolves.
    /// </summary>
    public ShipSpec GenerateDay2GhostRouteSuspect(
        GameState state,
        int queueIndex,
        uint reseedNonce = 0,
        HashSet<string>? usedRegs = null)
    {
        int runSeed = state.RunSeed != 0 ? state.RunSeed : 1;
        DeterministicRng rng = new($"run{runSeed}-day2-ghost-route-suspect-n{reseedNonce}");

        string declaredPrefix = rng.Fork("suspect_prefix").Pick(Day2GhostRouteEligiblePrefixes);
        ShipFaction visualFaction = ShipRenderer.ParseFactionFromPrefix(declaredPrefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(visualFaction);
        ShipClass shipClass = rng.Fork("suspect_class").Pick(allowedClasses);

        string regCode = GenerateUniqueRegistration(declaredPrefix, rng.Fork("suspect_reg"), usedRegs);
        string shipName = GenerateShipName(declaredPrefix, rng.Fork("suspect_name"));
        string declaredOrigin = GetCanonicalOriginForPrefix(declaredPrefix, rng.Fork("suspect_origin"));
        string destination = "VG-3 DOCK BAY";

        PortraitSpecies captainSpecies = PickSpeciesForPrefix(declaredPrefix, rng.Fork("suspect_species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("suspect_capt"));
        int tonnage = GetBaselineTonnage(shipClass, rng.Fork("suspect_tonnage"));

        (string slotPurpose, string slotCargoSpeech, string slateCargo) =
            GetGhostRouteFactionDialogueSlots(declaredPrefix, declaredOrigin);

        string seedId = $"{regCode}-{shipName.Replace(' ', '-')}-run{runSeed}-ghostroute";
        string portraitSeed = $"capt-{captainName.Replace(' ', '-')}-{seedId}";

        (int curYear, int curMonth) = Constants.GetStationCycle(state.Day);
        (int expYear, int expMonth) = OffsetStationCycle(curYear, curMonth, 14);

        DataSlateModel slate = new()
        {
            ShipName = shipName,
            RegistrationCode = regCode,
            DeclaredPrefix = declaredPrefix,
            DeclaredFactionName = GetFactionFullName(declaredPrefix),
            RegistrationExpiryYear = expYear,
            RegistrationExpiryMonth = expMonth,
            DeclaredVisualFaction = visualFaction,
            DeclaredShipClass = shipClass,
            DeclaredCaptainName = captainName,
            DeclaredCaptainSpecies = captainSpecies,
            OriginSystem = declaredOrigin,
            DestinationSystem = destination,
            DeclaredTonnage = tonnage,
            CargoManifest = slateCargo,
            PurposeOfVisit = slotPurpose.ToUpperInvariant(),
            DeclaredShieldsStatus = "DOWN",
            DeclaredWeaponsStatus = "OFFLINE",
            PortraitSeedId = portraitSeed,
            HoloPortraitSpecies = captainSpecies,
            HoloAmber = declaredPrefix is "MNG-" or "KRW-",
            IsMolting = captainSpecies == PortraitSpecies.Tolvak,
            CrudeCrayon = false
        };

        ShipGroundTruth truth = new()
        {
            ActualShieldsUp = false,
            ActualWeaponsHot = false,
            ActualWarpPrimed = true,
            ActualCargoPowerDraw = false,
            ActualCommsPowered = true,
            RawTransponderId = regCode,
            ActualVisualFaction = visualFaction,
            ActualShipClass = shipClass,
            IsClassSilhouetteMismatch = false,
            ActualMass = tonnage,
            IsDecommissionedHull = false,
            HasDownstreamHullScorch = false,
            UsesStaticViewscreen = false,
            WillRefuseShieldsCompliance = false,
            WillRefuseWeaponsCompliance = false,
            HasSpoofedWeapons = false,
            ActualCaptainSpecies = captainSpecies,
            ActualOriginSystem = WarpSignatureGenerator.RestrictedSystemKey,
            IsDay2GhostRouteSuspect = true,
            ActiveInfractions = []
        };

        string uploadLine = declaredPrefix switch
        {
            "KRW-" => "My protocols are clean, human. Do not waste a warrior's time staring at numbers.",
            "HBR-" => "Transmitting now. Keep it brief. I have no desire to linger under Kordani guns.",
            "ORN-" => "Telemetry transmitted. Every checksum is verified to the fourth decimal. You will find no fault.",
            "PRJ-" => "Sending the bright data-packet now! Very fast, very legal, no need to look too close!",
            _ => "Protocols sent. Look, my schedule is tight -- stamp the slate and open the lane."
        };

        string suspectDeclaration =
            $"Inbound from {declaredOrigin} -- uh, I mean, departed {declaredOrigin} four cycles ago. " +
            $"Heading to {destination}. Purpose is {slotPurpose}. Hold is carrying {slotCargoSpeech}";

        return new ShipSpec
        {
            EncounterSeedId = seedId,
            QueueIndex = queueIndex,
            Slate = slate,
            GroundTruth = truth,
            ComplianceDelaySeconds = 0.25f,
            GreetingHailLine = $"{shipName} ({regCode}) holding at approach beacon. Requesting routine lane clearance.",
            ShieldsLoweredLine = "Shields are already down, Booth 04.",
            WeaponsPoweredDownLine = "Weapon banks are completely cold.",
            RefusalLine = string.Empty,
            SpoofedWeaponsDeflectionLine = string.Empty,
            SubspaceUploadLine = uploadLine,
            ReTransmitAnnoyedLine = "You dropped my subspace link?! Fix your port -- re-sending my clean packet now.",
            AutoInterrogationReplyLine = suspectDeclaration,
            SuspectRoutePushbackPromptLine = $"Your departure timing does not match the {declaredOrigin} lane.",
            SuspectRouteDeflectionLine =
                "Your station's own spectrometer is dark, Port Officer. My slate is green, my weapons are cold, and my papers are legal. Unless you want to explain to Kordani Command why you detained a clean vessel on a hunch, clear my dock.",
            ConfrontationGuiltyExcuseOrigin =
                "Your station's own spectrometer is dark, Port Officer. My slate is green and my papers are legal!",
            ConfrontationInnocentProtest =
                "My slate is green, my weapons are cold, and my papers are legal. Check your console again!"
        };
    }

    private static (string Purpose, string CargoSpeech, string SlateCargo) GetGhostRouteFactionDialogueSlots(
        string prefix,
        string declaredOrigin) =>
        prefix switch
        {
            "KRW-" => (
                "routine patrol resupply",
                "sealed spare reactor baffles. Standard war surplus -- nothing that concerns a Pact clerk.",
                "SEALED SPARE REACTOR BAFFLES"),
            "HBR-" => (
                "private relocation",
                "personal ballast and agricultural seed crates. Nothing military, if that is what your sensors are hunting for.",
                "PERSONAL BALLAST & SEED CRATES"),
            "ORN-" => (
                "scheduled archival delivery",
                "unindexed mineral survey canisters. The manifest date? A... minor clerical recalculation on my departure port.",
                "MINERAL SURVEY CANISTERS"),
            "PRJ-" => (
                "buying shiny hull parts",
                "totally normal scrap metal! Definitely did not stop in any scary red nebulae along the way!",
                "SALVAGED HULL SCRAP METAL"),
            "MNG-" => (
                "ore transport",
                "refined silicate slag. Why is the hull dust scorched? Just... standard asteroid grit. Nothing from the dead sectors.",
                "REFINED SILICATE SLAG"),
            "FTC-" or "ZNI-" => (
                "charter transit",
                $"sealed passenger provisions. We took a slight detour for -- navigation calibration. My slate says {declaredOrigin}, so that is where I came from.",
                "SEALED PASSENGER PROVISIONS"),
            _ => (
                "independent trade",
                "industrial lubricants and salvage filters. Look, if your route-scanner is dead, we are done here, right?",
                "LUBRICANTS & SALVAGE FILTERS")
        };

    private static string GenerateUniqueRegistration(string prefix, DeterministicRng rng, HashSet<string>? usedRegs)
    {
        int attempt = 0;
        while (true)
        {
            string candidate = GenerateRegistrationCode(prefix, rng.Fork($"u_{attempt++}"));
            if (usedRegs == null || usedRegs.Add(candidate))
                return candidate;
        }
    }

    private static string MutateTransponderId(string prefix, string regCode, DeterministicRng rng)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            string candidate = GenerateRegistrationCode(prefix, rng.Fork($"mut_{attempt}"));
            if (!candidate.Equals(regCode, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
        return $"{prefix}909-Z";
    }

    public static string GetShortClassCode(ShipClass shipClass) =>
        shipClass switch
        {
            ShipClass.Military => "MIL",
            ShipClass.Civilian => "CIV",
            ShipClass.Cargo => "CRG",
            ShipClass.Diplomatic => "DIP",
            ShipClass.HackJob => "HCK",
            _ => "CIV"
        };

    private static string PickFalsifiedOriginSystem(string declaredOrigin, DeterministicRng rng)
    {
        if (rng.NextBool(0.55f))
        {
            return WarpSignatureGenerator.RestrictedSystemKey;
        }

        List<string> others = ValidOriginSystems
            .Where(s => !s.Equals(declaredOrigin, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return rng.Pick(others);
    }

    private static void PopulateDialogueLines(ShipSpec spec, int factionRep)
    {
        string prefix = spec.Slate.DeclaredPrefix;
        string reg = spec.Slate.RegistrationCode;
        string shipName = spec.Slate.ShipName;
        bool lowRep = factionRep <= Constants.RepLowThreshold;

        spec.GreetingHailLine = prefix switch
        {
            "KDF-" => $"KDF vessel {shipName} ({reg}) approaching VG-3 beacon. Stand by for Pact military clearance.",
            "BMC-" => $"Consortium freighter {shipName} ({reg}) on final approach. Time is Scrip, Booth 04.",
            "KRW-" => $"Krushvak hull {shipName} ({reg}) holding at your gate, human. Process our lane.",
            "ORN-" => $"Orun Registry {reg} ({shipName}) locked to approach vector. Standing by for telemetry.",
            "PRJ-" => $"Hail Booth 04! Salvage rig {shipName} ({reg}) requesting very legal dock lock!",
            "HBR-" => $"Free vessel {shipName} ({reg}) requesting transit lane through Void Gate 3.",
            "GUV-" => $"Galactic Union vessel {shipName} ({reg}) hailing Booth 04. Requesting standard docking clearance.",
            _ => $"{shipName} ({reg}) holding at approach marker. Requesting Void Gate 3 docking clearance."
        };

        spec.ShieldsLoweredLine = lowRep
            ? "Stalling complete... dropping deflector screen now. Satisfied, clerk?"
            : "Copy Booth 04. Lowering shields to standby now.";

        spec.WeaponsPoweredDownLine = lowRep
            ? "Grumbling on bridge... cutting power to weapon capacitors."
            : "Acknowledged. Powering down forward weapon mounts.";

        spec.RefusalLine = prefix switch
        {
            "KRW-" => "A Krushvak warship does not strip its defenses for a Pact tollbooth! Clear us or clamp us!",
            "KDF-" => "Negative, Booth 04! Under patrol directive we keep tactical systems hot. Do not test us!",
            "PRJ-" => "Uh, power switches are rusted welded shut! Cannot turn off! Just let us dock anyway!",
            _ => "Negative, Booth 04! We are not dropping our defenses in this sector. Open the gate or lock clamps!"
        };

        spec.SpoofedWeaponsDeflectionLine =
            "Check your slate, Officer -- our telemetry packet reads weapons OFFLINE. Must be ghost bleed on your antique scanner!";

        spec.SubspaceUploadLine = prefix switch
        {
            "ORN-" => "Subspace packet transmitted. Every checksum is verified to the fourth decimal.",
            "KRW-" => "Protocols sent. Do not waste a warrior's time staring at numbers.",
            "PRJ-" => "Sending bright data-packet now! Very fast, very clean!",
            "BMC-" => "Telemetry uploaded to your slate. Keep the queue moving.",
            _ => "Transmitting subspace protocols now. Telemetry and manifest should be on your Data Slate."
        };

        spec.ReTransmitAnnoyedLine = prefix switch
        {
            "KRW-" => "A Krushvak does not repeat transmissions for clumsy hands! Keep that slate plugged in!",
            "KDF-" => "You severed a Pact military data link mid-stream, Port Officer?! Resending -- do not touch it again.",
            "BMC-" => "Every wasted second costs Scrip! Fix your antique port -- re-transmitting packet now!",
            "ORN-" => "Unscheduled carrier drop detected. Re-initializing checksum transmission... please refrain from unplugging.",
            "PRJ-" => "Hey! You yanked the bright cable right out! Sending shiny packet AGAIN -- hold still!",
            _ => "You pulled the Data Slate mid-download?! Fix that FTC port -- re-sending subspace protocols now."
        };

        spec.AutoInterrogationReplyLine =
            $"Inbound from {spec.Slate.OriginSystem} to {spec.Slate.DestinationSystem}. " +
            $"Purpose: {spec.Slate.PurposeOfVisit}. Cargo: {spec.Slate.CargoManifest}.";

        spec.ConfrontationGuiltyExcuseHull =
            $"Wait -- we bought this hull surplus last quarter! The registry refit paperwork for {reg} must still be stuck in port processing!";

        spec.ConfrontationGuiltyExcuseClassMismatch =
            $"Our {GetShortClassCode(spec.Slate.DeclaredShipClass)} hull classification was updated at drydock -- your visual sensors must be misreading our refit!";

        spec.ConfrontationGuiltyExcuseTransponderId =
            $"Raw sensor ID {spec.GroundTruth.RawTransponderId}?! Brown Dwarf static must be scrambling your local transponder receiver -- trust the Data Slate!";

        spec.ConfrontationGuiltyExcuseWeapons =
            "Look, our slate clearly says weapons OFFLINE! That hot orange glow on your SPS is just a... faulty diagnostic shunt. We aren't powering anything down!";

        spec.ConfrontationGuiltyExcuseOrigin =
            $"Your salvaged spectrometer coil is drifting! Our logbook says {spec.Slate.OriginSystem} -- ignore that waveform strip!";

        spec.ConfrontationGuiltyExcuseExpired =
            $"Our renewal fee was wired to the port factor last month! {spec.Slate.RegistrationExpiryDisplay} is just a clerical lag -- cut us a break!";

        spec.ConfrontationGuiltyExcuseOrgMismatch =
            $"Charter lease paperwork changed hands mid-run! {prefix} and {spec.Slate.DeclaredFactionName} share the same holding broker -- mostly!";

        spec.ConfrontationGuiltyExcuseSpeciesMismatch =
            $"The regular captain came down with drift-fever before departure! I am only filling in on the {prefix} bridge for one jump!";

        spec.ConfrontationGuiltyExcuseCargoPower =
            "Violet draw in an empty hold? Just... deck heaters! Keep your scanner off our cargo bay and clear the berth!";

        spec.ConfrontationInnocentProtest =
            $"Check your Reference Manual again, Officer! Vessel {reg} is 100% regulation and compliant -- detaining us will cost you!";
    }

    public static (int Year, int Month) OffsetStationCycle(int year, int month, int monthDelta)
    {
        int totalMonths = year * 12 + (month - 1) + monthDelta;
        int outYear = totalMonths / 12;
        int outMonth = (totalMonths % 12) + 1;
        return (outYear, outMonth);
    }

    public static bool IsSpeciesRestrictedPrefix(string prefix) =>
        prefix.ToUpperInvariant() is "GUV-" or "KDF-" or "BMC-" or "KRW-" or "ORN-" or "TLV-" or "PRJ-" or "HBR-";

    public static bool IsSpeciesValidForPrefix(string prefix, PortraitSpecies species) =>
        prefix.ToUpperInvariant() switch
        {
            "KDF-" => species == PortraitSpecies.Kordani,
            "BMC-" => species == PortraitSpecies.Brengop,
            "KRW-" => species == PortraitSpecies.Krushvak,
            "ORN-" => species == PortraitSpecies.Orun,
            "TLV-" => species == PortraitSpecies.Tolvak,
            "PRJ-" => species == PortraitSpecies.Prunjun,
            "HBR-" => species == PortraitSpecies.HunBorian,
            "GUV-" => species is PortraitSpecies.Human or PortraitSpecies.Helani or PortraitSpecies.Klaasari
                or PortraitSpecies.Thane or PortraitSpecies.Veldor,
            _ => true // FTV-, FTC-, MNG-, ZNI- are multi-species
        };

    public static string GetAllowedCrewSummary(ShipFaction faction) =>
        faction switch
        {
            ShipFaction.GalacticUnion => "CREW: GU (HUM/HEL/KLA/THN/VEL)",
            ShipFaction.Kordani => "CREW: KORDANI ONLY",
            ShipFaction.Brengop => "CREW: BRENGOP ONLY",
            ShipFaction.Krushvak => "CREW: KRUSHVAK ONLY",
            ShipFaction.Orun => "CREW: ORUN ONLY",
            ShipFaction.Tolvak => "CREW: TOLVAK ONLY",
            ShipFaction.Prunjun => "CREW: PRUNJUN ONLY",
            ShipFaction.FreeTraders => "CREW: FTV=MULTI / HBR=HUN-BORIAN",
            ShipFaction.FarLineTransit => "CREW: MULTI-SPECIES",
            ShipFaction.MiningGuild => "CREW: MULTI-SPECIES",
            ShipFaction.ZenithInterstellar => "CREW: MULTI-SPECIES",
            _ => "CREW: MULTI-SPECIES"
        };

    private static string PickDifferentTrackedPrefix(string currentPrefix, DeterministicRng rng)
    {
        List<string> candidates = Constants.AllTrackedPrefixes
            .Where(p => !p.Equals(currentPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return rng.Pick(candidates);
    }

    private static PortraitSpecies PickInvalidSpeciesForPrefix(string prefix, DeterministicRng rng)
    {
        List<PortraitSpecies> candidates = AllDeclaredCaptainSpecies
            .Where(s => !IsSpeciesValidForPrefix(prefix, s))
            .ToList();
        return candidates.Count > 0 ? rng.Pick(candidates) : PortraitSpecies.Human;
    }

    public static string GetHullFactionReadableName(ShipFaction faction) =>
        faction switch
        {
            ShipFaction.GalacticUnion => "GALACTIC UNION",
            ShipFaction.Kordani => "KORDANI",
            ShipFaction.Brengop => "BRENGOP",
            ShipFaction.Krushvak => "KRUSHVAK",
            ShipFaction.Orun => "ORUN",
            ShipFaction.Tolvak => "TOLVAK",
            ShipFaction.Prunjun => "PRUNJUN HACK-JOB",
            ShipFaction.FreeTraders => "FREE TRADER",
            ShipFaction.FarLineTransit => "FAR-LINE TRANSIT",
            ShipFaction.MiningGuild => "MINING GUILD",
            ShipFaction.ZenithInterstellar => "ZENITH INTERSTELLAR",
            _ => "UNKNOWN"
        };

    private static ShipFaction PickMismatchedVisualFaction(ShipFaction declaredFaction, DeterministicRng rng)
    {
        List<ShipFaction> candidates = [];
        foreach (ShipFaction f in ShipRenderer.AllFactions)
        {
            if (f != declaredFaction)
            {
                candidates.Add(f);
            }
        }
        return rng.Pick(candidates);
    }

    private static string PickWeightedPrefix(DeterministicRng rng)
    {
        int total = 0;
        for (int i = 0; i < WeightedPrefixes.Length; i++)
            total += WeightedPrefixes[i].Weight;

        int roll = rng.NextInt(0, total);
        int accum = 0;
        for (int i = 0; i < WeightedPrefixes.Length; i++)
        {
            accum += WeightedPrefixes[i].Weight;
            if (roll < accum)
                return WeightedPrefixes[i].Prefix;
        }
        return "FTV-";
    }

    public static string GenerateRegistrationCode(string prefix, DeterministicRng rng)
    {
        if (prefix.Equals("ORN-", StringComparison.OrdinalIgnoreCase) && rng.NextBool(0.35f))
        {
            int suffix3 = rng.NextInt(100, 999);
            return $"ORN-442{suffix3:D3}";
        }

        int digits = rng.NextInt(104, 998);
        char checkChar = "ABCDEFGHJKLMNPQRSTUVWXYZ"[rng.NextInt(0, 24)];
        return $"{prefix}{digits:D3}-{checkChar}";
    }

    private static string GenerateShipName(string prefix, DeterministicRng rng)
    {
        string[] pool = prefix switch
        {
            "GUV-" => ["VERIDIA DAWN", "CONCORDIA", "ASTRAL AEGIS", "SOLARIS VOW", "HORIZON WATCH", "SERENITY ARC"],
            "KDF-" => ["IRON TALON", "KORR SPEAR", "VANGUARD IX", "OBSIDIAN FANG", "DREAD LANCE", "BASTION VEX"],
            "BMC-" => ["PROFIT MARGIN", "DUL-KRAG LEDGER", "GILDED BULLION", "SCRIP HARVEST", "FACTOR'S YIELD", "BRASS VAULT"],
            "KRW-" => ["BLOOD ANVIL", "GHAROK CLEAVER", "WARFIRE IV", "IRON MAUL", "STORM TUSK", "HONOR HAMMER"],
            "ORN-" => ["LITHOS MONOLITH", "BASALT AXIOM", "GRANITE PROOF", "SILICATE FOUR", "OBELISK PRIME", "STRATA NINE"],
            "TLV-" => ["TERRACOTTA SUN", "MOLT RUNNER", "RED DUNE", "CARAPACE DRIFT", "AMBER COIL", "SILT CARAVAN"],
            "PRJ-" => ["SHINY BOLT", "RUST ROCKET", "SCRAP KING", "TORCH WELD", "LUCKY SPARK", "JUNK COMET"],
            "HBR-" => ["QUIET STONE", "FREE HEARTH", "DEEP ROOT", "BORIAN WANDERER", "KINDRED STAR", "IRON BARK"],
            "FTV-" => ["CALDERA DRIFTER", "RIM RUNNER", "SOLO VENTURE", "STAR TRAMP", "RUSTY COMPASS", "FREEWAY SEVEN"],
            "FTC-" => ["ENDURANCE ARK", "FAR-LINE PIONEER", "COLONY REACH", "LONG TRANSIT", "WAYFARER IV", "DEEP CORRIDOR"],
            "MNG-" => ["ORE CRUSHER", "SLAG HAULER 09", "CLAIM GUARD", "DRILLBREAKER", "ASTEROID ANVIL", "SMELTER KING"],
            "ZNI-" => ["CROWN SOLVANE", "VELVET STAR", "GRAND CELESTE", "AURELIA LUX", "ROYAL NEBULA", "STARLIGHT GALA"],
            _ => ["GREY PHANTOM", "DRIFT HULL 88", "NULL VECTOR", "SHADOW FREIGHT", "STRAY BEACON"]
        };
        return rng.Pick(pool);
    }

    private static string GetCanonicalOriginForPrefix(string prefix, DeterministicRng rng)
    {
        if (rng.NextBool(0.65f))
        {
            return prefix switch
            {
                "GUV-" => "Veridia",
                "KDF-" => "Korr-Vath",
                "BMC-" => "Dul-Krag",
                "KRW-" => "Gharok",
                "ORN-" or "MNG-" => "Oruun-4",
                "ZNI-" => "Veridia",
                _ => rng.Pick(ValidOriginSystems)
            };
        }
        return rng.Pick(ValidOriginSystems);
    }

    private static PortraitSpecies PickSpeciesForPrefix(string prefix, DeterministicRng rng)
    {
        PortraitSpecies[] guSpecies =
        [
            PortraitSpecies.Human,
            PortraitSpecies.Helani,
            PortraitSpecies.Klaasari,
            PortraitSpecies.Thane,
            PortraitSpecies.Veldor
        ];

        return prefix switch
        {
            "KDF-" => PortraitSpecies.Kordani,
            "BMC-" => PortraitSpecies.Brengop,
            "KRW-" => PortraitSpecies.Krushvak,
            "ORN-" => PortraitSpecies.Orun,
            "TLV-" => PortraitSpecies.Tolvak,
            "PRJ-" => PortraitSpecies.Prunjun,
            "HBR-" => PortraitSpecies.HunBorian,
            "GUV-" => rng.Pick(guSpecies),
            _ => rng.Pick(AllDeclaredCaptainSpecies)
        };
    }

    private static string GenerateCaptainName(PortraitSpecies species, DeterministicRng rng)
    {
        string[] names = species switch
        {
            PortraitSpecies.Human => ["CAPT. E. VANCE", "CAPT. M. CHEN", "CAPT. R. KOVACS", "CAPT. S. HALDEN", "CAPT. J. MERCER"],
            PortraitSpecies.Helani => ["CAPT. AELEN VOS", "CAPT. SERA TAL", "CAPT. LYRA VENN", "CAPT. ORIN SOL"],
            PortraitSpecies.Klaasari => ["CAPT. NAI-LORA", "CAPT. VEL-SHAN", "CAPT. KOR-TIDE", "CAPT. MIRA-SHOAL"],
            PortraitSpecies.Thane => ["CAPT. KAEL-VORN", "CAPT. STEN-GAR", "CAPT. BRYN-HAL", "CAPT. TOR-VIK"],
            PortraitSpecies.Veldor => ["ENVOY SUL-9", "CAPT. VEX-44", "PILOT KHLOR-7", "CAPT. THIO-2"],
            PortraitSpecies.Brengop => ["FACTOR GRIM-TUL", "BROKER SNIV-KEX", "FACTOR DUL-MOK", "CLERK-CAPT. VROK"],
            PortraitSpecies.Kordani => ["CMDR. THAROK", "CAPT. VEX-KORR", "LT. ZHAR-VATH", "CMDR. KREL-DAN"],
            PortraitSpecies.Krushvak => ["WAR-CAPT. GHAR", "CHIEFTAIN BROK", "CAPT. DRAGH-KUL", "WARRIOR RUK-TAR"],
            PortraitSpecies.HunBorian => ["ELDER BOR-HAN", "CAPT. TOR-MAKK", "PILOT GARN-UL", "CAPT. HRON-DA"],
            PortraitSpecies.Prunjun => ["BOSS SPARK-NIX", "CAPT. RATCHET-7", "SCRAPPER ZIK", "PILOT CLANK-BO"],
            PortraitSpecies.Tolvak => ["CAPT. SSKAR-VA", "PILOT TSSK-REN", "TRADER VESS-KA", "CAPT. RHAZ-TOL"],
            PortraitSpecies.Orun => ["ARCHIVIST 404-K", "ARBITER LITH-9", "PREFECT SIL-02", "WARDEN GRAN-7"],
            _ => ["CAPT. N. VALEN", "CAPT. D. CROSS", "CAPT. K. REYES"]
        };
        return rng.Pick(names);
    }

    private static int GetBaselineTonnage(ShipClass shipClass, DeterministicRng rng) =>
        shipClass switch
        {
            ShipClass.Civilian => rng.NextInt(320, 680),
            ShipClass.Military => rng.NextInt(1100, 1950),
            ShipClass.Diplomatic => rng.NextInt(1400, 2400),
            ShipClass.Cargo => rng.NextInt(2800, 4600),
            ShipClass.HackJob => rng.NextInt(550, 1250),
            _ => 600
        };

    private static (string Purpose, string Cargo) PickPurposeAndCargo(
        string prefix,
        ShipClass shipClass,
        DeterministicRng rng)
    {
        if (shipClass == ShipClass.Military)
        {
            return ("SECTOR PATROL & ESCORT", "SEALED ORDNANCE & REACTOR BAFFLES");
        }
        if (shipClass == ShipClass.Diplomatic)
        {
            return ("CONSULAR / VIP TRANSIT", "DIPLOMATIC POUCHES & LUXURY STORES");
        }

        return prefix switch
        {
            "MNG-" => ("INDUSTRIAL ORE HAUL", "REFINED SILICATE & HEAVY ORE SLAG"),
            "BMC-" => ("CONSORTIUM FREIGHT", "STAMPED PACT LEDGERS & SYNTH-RATIONS"),
            "ORN-" => ("ARCHIVAL SURVEY TRANSIT", "INDEXED MINERAL CORE CANISTERS"),
            "PRJ-" => ("SALVAGE YARD RUN", "ASSORTED HULL SCRAP & COIL PARTS"),
            "FTC-" => ("LONG-RANGE COLONY SUPPLY", "HYDRO-FILTERS & HABITAT PREFABS"),
            "ZNI-" => ("STARLINER PROVISIONING", "HOSPITALITY STORES & WINE CASKS"),
            _ => rng.NextBool()
                ? ("INDEPENDENT TRADE", "INDUSTRIAL LUBRICANTS & SPARE FILTERS")
                : ("REFUEL & LANE TRANSIT", "SEALED GENERAL MERCHANDISE CRATES")
        };
    }

    public static string GetFactionFullName(string prefix) =>
        prefix.ToUpperInvariant() switch
        {
            "GUV-" => "GALACTIC UNION",
            "KDF-" => "KORDANI DEFENSE FLEET",
            "BMC-" => "BRENGOP CONSORTIUM",
            "KRW-" => "KRUSHVAK WAR-FLEET",
            "ORN-" => "ORUN REGISTRY NETWORK",
            "TLV-" => "TOLVAK TRANSIT LINE",
            "PRJ-" => "PRUNJUN SALVAGE REG.",
            "FTV-" => "INDEPENDENT FREE TRADERS",
            "HBR-" => "HUN-BORIAN FREE VESSEL",
            "FTC-" => "FAR-LINE TRANSIT CORP",
            "MNG-" => "MINING GUILD SYNDICATE",
            "ZNI-" => "ZENITH INTERSTELLAR",
            _ => "UNKNOWN REGISTRY"
        };

    public static string GetFactionShortLabel(string prefix) =>
        prefix.ToUpperInvariant() switch
        {
            "GUV-" => "GAL. UNION",
            "KDF-" => "KORDANI DEF",
            "BMC-" => "BRENGOP",
            "KRW-" => "KRUSHVAK",
            "ORN-" => "ORUN REG.",
            "TLV-" => "TOLVAK",
            "PRJ-" => "PRUNJUN",
            "FTV-" => "FREE TRADER",
            "HBR-" => "HUN-BORIAN",
            "FTC-" => "FAR-LINE",
            "MNG-" => "MINING GLD",
            "ZNI-" => "ZENITH INT",
            _ => "UNKNOWN"
        };
}
