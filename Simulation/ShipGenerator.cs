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
    public const int CovertMissionTargetQueueSlot = 2;      // 3rd ship inside pre-gen 16 when a covert delivery mission is scheduled today
    public const int Day6MiningGuildGuaranteeSlot = 2;      // Guaranteed Day 6 Mining Guild (MNG-) target inside pre-gen 16
    public const int ActiveInterceptTargetGuaranteeSlot = 3; // Early slot for Day 7+ active intercept contract prefix target

    private readonly Core.ConfigFlags _config;

    public ShipGenerator(Core.ConfigFlags? config = null)
    {
        _config = config ?? new Core.ConfigFlags();
    }

    /// <summary>
    /// Deterministically chooses the Day 3 "Ghost Route" suspect's queue position between
    /// slot index 1 or 2 (2nd or 3rd ship) from RunSeed.
    /// </summary>
    public static int GetGhostRouteQueueSlot(int runSeed)
    {
        int safeSeed = runSeed != 0 ? runSeed : 1;
        uint hash = DeterministicRng.HashSeedFnv1a($"run{safeSeed}-day2-ghost-slot");
        return (hash & 1u) == 0u ? 1 : 2;
    }

    public static int GetGhostRouteQueueSlot(GameState state) =>
        GetGhostRouteQueueSlot(state.RunSeed);

    public static int GetDay2GhostRouteQueueSlot(int runSeed) =>
        GetGhostRouteQueueSlot(runSeed);

    public static int GetDay2GhostRouteQueueSlot(GameState state) =>
        GetGhostRouteQueueSlot(state.RunSeed);

    /// <summary>
    /// Deterministically assigns and persists the Day 6 MNG- intro intercept target registration
    /// (MNG-XXX-X) and queue slot index (1..3, i.e., Ship 2, Ship 3, or Ship 4) from RunSeed.
    /// Idempotent when valid values are already assigned on GameState.
    /// </summary>
    public static (string TargetReg, int TargetSlot) EnsureDay6IntroTargetAssigned(GameState state)
    {
        int safeSeed = state.RunSeed != 0 ? state.RunSeed : 1;
        DeterministicRng rng = new($"run{safeSeed}-day6-intro-target");

        if (string.IsNullOrWhiteSpace(state.Day6IntroTargetReg) ||
            !state.Day6IntroTargetReg.StartsWith("MNG-", StringComparison.OrdinalIgnoreCase))
        {
            state.Day6IntroTargetReg = GenerateRegistrationCode("MNG-", rng.Fork("reg"));
        }

        if (state.Day6IntroTargetSlot < 1 || state.Day6IntroTargetSlot > 3)
        {
            state.Day6IntroTargetSlot = rng.Fork("slot").NextInt(1, 4); // 1, 2, or 3 (Ship 2..4)
        }

        return (state.Day6IntroTargetReg, state.Day6IntroTargetSlot);
    }

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
        uint reseedNonce = 0,
        Core.ConfigFlags? config = null)
    {
        int safeCount = Math.Max(1, count);
        List<ShipSpec> queue = new(safeCount);
        HashSet<string> usedRegs = new(StringComparer.OrdinalIgnoreCase);

        if (state.Day == 6)
        {
            (string d6TargetReg, _) = EnsureDay6IntroTargetAssigned(state);
            usedRegs.Add(d6TargetReg);
        }

        for (int i = 0; i < safeCount; i++)
        {
            queue.Add(GenerateShipForSlot(state, i, reseedNonce, DebugSpawnValidityMode.Auto, usedRegs, config));
        }

        return queue;
    }

    /// <summary>
    /// Generates a deterministic vessel for the given (RunSeed, Day, queueIndex) slot.
    /// Injects the hardcoded "Ghost Route" suspect on Day 2 slot 1 or 2 (derived from RunSeed).
    /// Outright shield/weapon compliance refusals are strictly locked to Day 14+ &amp; rep_score &lt;= -9.
    /// </summary>
    public ShipSpec GenerateShipForSlot(
        GameState state,
        int queueIndex,
        uint reseedNonce = 0,
        DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto,
        HashSet<string>? usedRegs = null,
        Core.ConfigFlags? config = null)
    {
        Core.ConfigFlags cfg = config ?? _config;
        int runSeed = state.RunSeed != 0 ? state.RunSeed : 1;

        if (state.Day == Constants.DayGhostRouteSuspect && queueIndex == GetGhostRouteQueueSlot(runSeed) && forceValidity == DebugSpawnValidityMode.Auto)
        {
            return GenerateDay2GhostRouteSuspect(state, queueIndex, reseedNonce, usedRegs);
        }

        if (queueIndex == CovertMissionTargetQueueSlot &&
            forceValidity == DebugSpawnValidityMode.Auto &&
            !string.IsNullOrWhiteSpace(state.ActiveCovertMissionId) &&
            state.ActiveCovertDeliveryDay == state.Day &&
            !state.DeliveredCovertMissionIds.Contains(state.ActiveCovertMissionId))
        {
            return GenerateCovertMissionTargetShip(state, queueIndex, reseedNonce, usedRegs);
        }

        string slotSeedKey = $"run{runSeed}-day{state.Day}-q{queueIndex}-n{reseedNonce}";
        DeterministicRng rng = new(slotSeedKey);

        string d6TargetReg = string.Empty;
        int d6TargetSlot = -1;
        if (state.Day == 6)
        {
            (d6TargetReg, d6TargetSlot) = EnsureDay6IntroTargetAssigned(state);
        }
        bool isDay6IntroTargetSlot = state.Day == 6 &&
                                     queueIndex == d6TargetSlot &&
                                     forceValidity == DebugSpawnValidityMode.Auto;

        string declaredPrefix = PickWeightedPrefix(rng.Fork("prefix"));
        if (forceValidity == DebugSpawnValidityMode.Auto)
        {
            if (isDay6IntroTargetSlot)
            {
                // Guaranteed Mining Guild (MNG-) vessel inside Day 6's 16-ship pre-generated queue
                declaredPrefix = "MNG-";
            }
            else if (state.Day >= 7 &&
                     (queueIndex == ActiveInterceptTargetGuaranteeSlot || queueIndex == ActiveInterceptTargetGuaranteeSlot + 1) &&
                     state.ActiveInterceptMissionIds.Count > 0)
            {
                CovertMissionDatabase missionDb = CovertMissionDatabase.Load();
                int revIdx = queueIndex - ActiveInterceptTargetGuaranteeSlot;
                string? activePrefix = state.ActiveInterceptMissionIds
                    .AsEnumerable()
                    .Reverse()
                    .Select(id => missionDb.Missions.FirstOrDefault(
                        m => m.IsInterceptMission && m.MissionId.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    .Where(m => m != null && !string.IsNullOrWhiteSpace(m.TargetPrefix))
                    .ElementAtOrDefault(revIdx)
                    ?.TargetPrefix;
                if (!string.IsNullOrWhiteSpace(activePrefix))
                {
                    declaredPrefix = activePrefix;
                }
            }
        }
        ShipFaction declaredVisualFaction = ShipRenderer.ParseFactionFromPrefix(declaredPrefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(declaredVisualFaction);
        ShipClass declaredClass = rng.Fork("class").Pick(allowedClasses);

        string regCode;
        if (isDay6IntroTargetSlot)
        {
            regCode = d6TargetReg;
            usedRegs?.Add(regCode);
        }
        else
        {
            HashSet<string>? effectiveUsedRegs = usedRegs;
            if (state.Day == 6 && !string.IsNullOrWhiteSpace(d6TargetReg))
            {
                effectiveUsedRegs ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                effectiveUsedRegs.Add(d6TargetReg);
            }
            regCode = GenerateUniqueRegistration(declaredPrefix, rng.Fork("reg"), effectiveUsedRegs);
        }
        string rawTransponderId = regCode;
        string shipName = GenerateShipName(declaredPrefix, rng.Fork("name"));
        string originSystem = GetCanonicalOriginForPrefix(declaredPrefix, rng.Fork("origin"));
        string actualOriginSystem = originSystem;
        string destinationSystem = rng.Fork("dest").Pick(DestinationPorts);

        PortraitSpecies captainSpecies = PickSpeciesForPrefix(declaredPrefix, rng.Fork("species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("capt_name"));
        int tonnage = GetBaselineTonnage(declaredClass, rng.Fork("tonnage"));
        int actualMass = tonnage;
        int cargoAccountedExtraMass = 0;
        (string purpose, string cargo) = PickPurposeAndCargo(declaredPrefix, declaredClass, rng.Fork("cargo"));
        string declaredFactionName = GetFactionFullName(declaredPrefix);

        // Stage 2 Cargo Progression (Day 12+ / DayUnlockWaferCargo): populate manifests from Legal Inert + Controlled Legal Bio pools
        bool expectsCargoWafer = state.Day >= cfg.DayUnlockWaferCargo;
        string waferManifestItem = string.Empty;
        string waferSecondaryNote = string.Empty;
        bool cargoManifestInitiallyOmitted = false;
        bool omittedManifestIsDishonest = false;
        if (expectsCargoWafer)
        {
            CargoCatalog catalog = CargoCatalog.Load();
            DeterministicRng cargoPoolRng = rng.Fork("stage2_cargo_pool");
            bool pickBio = cargoPoolRng.NextFloat() < 0.42f && catalog.ControlledLegalBioPool.Count > 0;
            waferManifestItem = pickBio
                ? cargoPoolRng.Pick(catalog.ControlledLegalBioPool)
                : cargoPoolRng.Pick(catalog.LegalInertPool);
            waferSecondaryNote = pickBio ? "STAGE-2 REGISTERED BIO" : "INERT INDUSTRIAL LOT";
            cargo = waferManifestItem.ToUpperInvariant();
        }
        string spokenOriginSystem = originSystem;
        string spokenCargoItem = expectsCargoWafer ? waferManifestItem : ShortCargoSpeech(cargo);

        (int curYear, int curMonth) = Constants.GetStationCycle(state.Day);
        int validFutureMonths = rng.Fork("exp_valid").NextInt(2, 29);
        (int expiryYear, int expiryMonth) = OffsetStationCycle(curYear, curMonth, validFutureMonths);

        int factionRep = state.GetRep(declaredPrefix);

        // Roll compliance from tunable invalid-ship rate (default 38% Days 1-5, 45% Days 6+)
        float invalidRate = cfg.GetInvalidShipRate(state.Day);
        bool isValidShip = forceValidity switch
        {
            DebugSpawnValidityMode.ForceValid => true,
            DebugSpawnValidityMode.ForceInvalid => false,
            _ => rng.Fork("validity_roll").NextFloat() >= invalidRate
        };

        ShipFaction actualVisualFaction = declaredVisualFaction;
        ShipClass actualShipClass = declaredClass;
        bool isClassSilhouetteMismatch = false;

        // Auto-Shields-Down cadence is active Day 1+ (configurable ShieldsUpBaseChance);
        // weapons-hot spawns & SPS sweeping remain unlocked Day 2+.
        bool initialShieldsUp = state.Day >= 1 && rng.Fork("init_shields").NextFloat() < cfg.ShieldsUpBaseChance;
        bool initialWeaponsHot = state.Day >= Constants.DayUnlockSpsAndCombat && rng.Fork("init_weapons").NextFloat() < 0.28f;
        bool actualCargoPowerDraw = false;
        bool willRefuseShields = false;
        bool willRefuseWeapons = false;
        bool hasSpoofedWeapons = false;

        List<ShipInfraction> infractions = [];

        if (!isValidShip)
        {
            // Staggered Daily Rule Escalation (driven by ConfigFlags unlock days):
            // - Day 1:  ONLY Transponder ID (under Viewscreen) vs. Data Slate REG CODE mismatch.
            // - Day 2:  Transponder ID mismatch + Spoofed Weapons Hot (SPS probe unlocked).
            // - Day 3:  Adds Falsified Origin Waveform (Warp Residue Printer + Glowing Warp Checker; Warp Speed Class Limits NOT active).
            // - DayUnlockManualPart1 (Day 4): Unlocks Kordani Reference Manual Tablet (1. PREFIXES & 2. HULLS). Enforces ExpiredRegistration + PrefixOrganizationMismatch + PrefixSpeciesMismatch (Hull Silhouettes NOT enforced yet).
            // - DayUnlockManualHulls (Day 5): Enforces Hull Silhouette & Ship Class mismatches (PrefixHullSilhouetteMismatch).
            // - Day 6:  Breather / Espionage Day (no new infraction added).
            // - DayUnlockWaferCargo (Day 7+): Adds UnexplainedCargoPowerDraw (SPS Violet Cargo Bay).
            // - DayUnlockHostilityDestroy (Day 14+) & rep_score <= -9 ONLY: Adds Outright Shield/Weapon Compliance Refusal.
            List<ShipInfractionType> eligibleInfractions = [ShipInfractionType.TransponderIdMismatch];

            if (state.Day >= Constants.DayUnlockSpsAndCombat)
            {
                eligibleInfractions.Add(ShipInfractionType.SpoofedWeaponsHot);
            }

            if (state.Day >= cfg.DayUnlockWarpScanner)
            {
                eligibleInfractions.Add(ShipInfractionType.FalsifiedOriginWaveform);
                eligibleInfractions.Add(ShipInfractionType.SpokenRouteDiscrepancy);
            }

            if (state.Day >= cfg.DayUnlockManualPart1)
            {
                eligibleInfractions.Add(ShipInfractionType.ExpiredRegistration);
                eligibleInfractions.Add(ShipInfractionType.PrefixOrganizationMismatch);

                if (IsSpeciesRestrictedPrefix(declaredPrefix))
                {
                    eligibleInfractions.Add(ShipInfractionType.PrefixSpeciesMismatch);
                }
            }

            if (state.Day >= cfg.DayUnlockManualHulls)
            {
                eligibleInfractions.Add(ShipInfractionType.PrefixHullSilhouetteMismatch);
            }

            if (state.Day >= cfg.DayUnlockWaferCargo)
            {
                eligibleInfractions.Add(ShipInfractionType.UnexplainedCargoPowerDraw);
                eligibleInfractions.Add(ShipInfractionType.BannedCargoItem);
                eligibleInfractions.Add(ShipInfractionType.SpokenCargoDiscrepancy);
                eligibleInfractions.Add(ShipInfractionType.CargoManifestDiscrepancy);
                eligibleInfractions.Add(ShipInfractionType.WithheldIllicitCargoManifest);
            }

            if (state.Day >= cfg.DayUnlockStoker && cfg.EnableStokerDial)
            {
                eligibleInfractions.Add(ShipInfractionType.StokerMassMismatch);
            }

            if (state.Day >= cfg.DayUnlockHostilityDestroy && factionRep <= Constants.RepExtremeHostileThreshold)
            {
                eligibleInfractions.Add(ShipInfractionType.RefusedShieldsOrWeaponsCompliance);
            }

            // Keep Viewscreen RawTransponderId identical to Day6IntroTargetReg before protocols arrive
            if (isDay6IntroTargetSlot && eligibleInfractions.Count > 1)
            {
                eligibleInfractions.Remove(ShipInfractionType.TransponderIdMismatch);
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
                    waferManifestItem = "Empty Hold (Unpowered)";
                    waferSecondaryNote = "ZERO-DRAW BALLAST";
                    spokenCargoItem = "Empty hold";
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.UnexplainedCargoPowerDraw,
                        Code = "PROTO-01H",
                        CitationReason = "UNEXPLAINED VIOLET CARGO POWER DRAW IN DECLARED EMPTY HOLD"
                    });
                    break;
                }

                case ShipInfractionType.BannedCargoItem:
                {
                    CargoCatalog catalog = CargoCatalog.Load();
                    BannedCargoEntry banned = rng.Fork("banned_cargo").Pick(catalog.BannedCargoList);
                    waferManifestItem = banned.Name;
                    waferSecondaryNote = $"LOT // {banned.Category}";
                    cargo = banned.Name.ToUpperInvariant();
                    spokenCargoItem = banned.Name;
                    if (banned.SpsViolet)
                    {
                        actualCargoPowerDraw = true;
                    }
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.BannedCargoItem,
                        Code = "PROTO-12A",
                        CitationReason = $"PROHIBITED CONTRABAND CARGO '{banned.Name.ToUpperInvariant()}' ON MANIFEST"
                    });
                    if (state.Day >= cfg.DayUnlockStoker && cfg.EnableStokerDial && banned.DenseMass)
                    {
                        actualMass = tonnage + RollOverweightDelta(declaredClass, rng.Fork("dense_slag_overweight"));
                        infractions.Add(new ShipInfraction
                        {
                            Type = ShipInfractionType.StokerMassMismatch,
                            Code = "PROTO-15M",
                            CitationReason = $"UNDECLARED DENSE ISOTOPE MASS ({actualMass:N0} T MEASURED vs {tonnage:N0} T DECLARED)"
                        });
                    }
                    break;
                }

                case ShipInfractionType.StokerMassMismatch:
                {
                    actualMass = tonnage + RollOverweightDelta(declaredClass, rng.Fork("stoker_overweight"));
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.StokerMassMismatch,
                        Code = "PROTO-15M",
                        CitationReason = $"UNDECLARED PHYSICAL MASS ({actualMass:N0} T MEASURED vs {tonnage:N0} T DECLARED)"
                    });
                    break;
                }

                case ShipInfractionType.SpokenRouteDiscrepancy:
                {
                    List<string> otherLegalOrigins = ValidOriginSystems
                        .Where(s => !s.Equals(originSystem, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    spokenOriginSystem = rng.Fork("spoken_route_lie").Pick(otherLegalOrigins);
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.SpokenRouteDiscrepancy,
                        Code = "PROTO-04S",
                        CitationReason = $"SPOKEN ORIGIN '{spokenOriginSystem.ToUpperInvariant()}' CONTRADICTED FILED ROUTE '{originSystem.ToUpperInvariant()}'"
                    });
                    break;
                }

                case ShipInfractionType.SpokenCargoDiscrepancy:
                {
                    CargoCatalog catalog = CargoCatalog.Load();
                    List<string> altCargo = catalog.LegalInertPool
                        .Concat(catalog.ControlledLegalBioPool)
                        .Where(c => !c.Equals(waferManifestItem, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    spokenCargoItem = altCargo.Count > 0 ? rng.Fork("spoken_cargo_lie").Pick(altCargo) : "Ration Bricks";
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.SpokenCargoDiscrepancy,
                        Code = "PROTO-12S",
                        CitationReason = $"SPOKEN CARGO '{spokenCargoItem.ToUpperInvariant()}' CONTRADICTED CARGO MANIFEST '{waferManifestItem.ToUpperInvariant()}'"
                    });
                    break;
                }

                case ShipInfractionType.CargoManifestDiscrepancy:
                {
                    CargoCatalog catalog = CargoCatalog.Load();
                    List<string> altManifest = catalog.LegalInertPool
                        .Concat(catalog.ControlledLegalBioPool)
                        .Where(c => !c.Equals(waferManifestItem, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    string mismatchedSlateCargo = altManifest.Count > 0 ? rng.Fork("manifest_mismatch").Pick(altManifest) : "Coolant Rods";
                    cargo = mismatchedSlateCargo.ToUpperInvariant();
                    spokenCargoItem = mismatchedSlateCargo;
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.CargoManifestDiscrepancy,
                        Code = "PROTO-12M",
                        CitationReason = $"CARGO MANIFEST WAFER '{waferManifestItem.ToUpperInvariant()}' DID NOT MATCH SLATE '{cargo}'"
                    });
                    break;
                }

                case ShipInfractionType.WithheldIllicitCargoManifest:
                {
                    cargoManifestInitiallyOmitted = true;
                    omittedManifestIsDishonest = true;
                    actualCargoPowerDraw = rng.Fork("withheld_violet").NextBool(0.45f);
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.WithheldIllicitCargoManifest,
                        Code = "PROTO-12W",
                        CitationReason = "WITHHELD CARGO MANIFEST PACKET TO CONCEAL UNREGISTERED FREIGHT"
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

        if (isValidShip && expectsCargoWafer && forceValidity == DebugSpawnValidityMode.Auto &&
            rng.Fork("honest_omitted_manifest").NextFloat() < 0.12f)
        {
            cargoManifestInitiallyOmitted = true;
            omittedManifestIsDishonest = false;
            if (state.Day >= cfg.DayUnlockStoker && cfg.EnableStokerDial)
            {
                cargoAccountedExtraMass = rng.Fork("honest_manifest_extra_mass").NextInt(180, 460);
                actualMass = tonnage + cargoAccountedExtraMass;
                waferSecondaryNote = $"+{cargoAccountedExtraMass} T FREIGHT LOT";
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
            ActualMass = actualMass,
            IsDecommissionedHull = isDecomm,
            HasDownstreamHullScorch = hasScorch,
            UsesStaticViewscreen = staticCamera,
            WillRefuseShieldsCompliance = willRefuseShields,
            WillRefuseWeaponsCompliance = willRefuseWeapons,
            HasSpoofedWeapons = hasSpoofedWeapons,
            ActualCaptainSpecies = captainSpecies,
            ActualOriginSystem = actualOriginSystem,
            SpokenOriginSystem = spokenOriginSystem,
            SpokenCargoItem = spokenCargoItem,
            IsDay2GhostRouteSuspect = false,
            ExpectsCargoManifestWafer = expectsCargoWafer,
            CargoManifestInitiallyOmitted = cargoManifestInitiallyOmitted,
            OmittedManifestIsDishonest = omittedManifestIsDishonest,
            CargoManifestTransmitted = expectsCargoWafer && !cargoManifestInitiallyOmitted,
            WaferCargoManifestItem = waferManifestItem,
            WaferCargoSecondaryNote = waferSecondaryNote,
            CargoManifestAccountedExtraMass = cargoAccountedExtraMass,
            ActiveInfractions = infractions
        };

        string? storyTag = isDay6IntroTargetSlot
            ? EconomyEngine.Day6IntroMissionId
            : null;

        ShipSpec spec = new()
        {
            EncounterSeedId = seedId,
            QueueIndex = queueIndex,
            Slate = slate,
            GroundTruth = truth,
            ComplianceDelaySeconds = complianceDelay,
            StoryTag = storyTag
        };

        PopulateDialogueLines(spec, factionRep, state);
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
            StoryTag = "ghost_route",
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

    private static void PopulateDialogueLines(ShipSpec spec, int factionRep, GameState state)
    {
        FactionDialoguePool.EnsureLoaded();

        string prefix = spec.Slate.DeclaredPrefix;
        string reg = spec.Slate.RegistrationCode;
        string shipName = spec.Slate.ShipName;
        bool lowRep = factionRep <= Constants.RepLowThreshold;

        spec.GreetingHailLine = prefix switch
        {
            "KDF-" => $"{shipName} holding. Pact military transit.",
            "BMC-" => $"{shipName} inbound. Time is Scrip.",
            "KRW-" => $"{shipName} at gate. Process our lane.",
            "ORN-" => $"{reg} holding vector. Telemetry ready.",
            "PRJ-" => $"{shipName} holding! Very legal dock!",
            "HBR-" => $"{shipName} requesting Void Gate transit.",
            "GUV-" => $"{shipName} holding. Requesting standard dock.",
            _ => $"{shipName} holding. Clear our lane."
        };

        string? poolGreeting = FactionDialoguePool.TryPickGreeting(prefix, spec);
        if (poolGreeting != null)
        {
            spec.GreetingHailLine = poolGreeting;
        }

        RecentFactionCallbackKind callbackKind = EncounterHistoryQuery.GetRecentFactionPattern(
            state.EncounterHistory, state.Day, prefix);
        if (callbackKind != RecentFactionCallbackKind.None)
        {
            string? callbackLine = FactionDialoguePool.TryPickRecentCallback(prefix, callbackKind, spec);
            if (callbackLine != null)
            {
                spec.GreetingHailLine = $"{spec.GreetingHailLine} {callbackLine}";
            }
        }

        spec.ShieldsLoweredLine = lowRep
            ? "Fine. Dropping shields now."
            : "Shields down.";

        spec.WeaponsPoweredDownLine = lowRep
            ? "Cutting weapon power."
            : "Weapons cold.";

        spec.RefusalLine = prefix switch
        {
            "KRW-" => "A Krushvak warship does not strip its defenses for a Pact tollbooth! Clear us or clamp us!",
            "KDF-" => "Negative, Booth 04! Under patrol directive we keep tactical systems hot. Do not test us!",
            "PRJ-" => "Uh, power switches are rusted welded shut! Cannot turn off! Just let us dock anyway!",
            _ => "Negative, Booth 04! We are not dropping our defenses in this sector. Open the gate or lock clamps!"
        };

        spec.SpoofedWeaponsBluffLine = prefix switch
        {
            "KRW-" => "Complying. Weapons cycling cold.",
            "BMC-" => "Acknowledged. Weapon banks cold.",
            "PRJ-" => "Flipping weapons cold right now!",
            _ => "Complying. Weapons cycling cold."
        };

        spec.SpoofedWeaponsDeflectionLine =
            "My board reads green -- your SPS probe must be faulty. Check our slate, it clearly says WPN: OFFLINE!";

        spec.SubspaceUploadLine = prefix switch
        {
            "ORN-" => "Checksum packet sent.",
            "KRW-" => "Protocols sent.",
            "PRJ-" => "Packet sent!",
            "BMC-" => "Telemetry uploaded.",
            _ => "Protocols sent."
        };

        spec.ReTransmitAnnoyedLine = prefix switch
        {
            "KRW-" => "Keep that slate plugged in! Resending.",
            "KDF-" => "Link severed?! Resending packet now.",
            "BMC-" => "Fix your port -- resending packet!",
            "ORN-" => "Carrier dropped. Resending packet.",
            "PRJ-" => "Hey! Sending packet again!",
            _ => "Link dropped -- resending protocols."
        };

        string spokenOrigin = string.IsNullOrWhiteSpace(spec.GroundTruth.SpokenOriginSystem)
            ? spec.Slate.OriginSystem
            : spec.GroundTruth.SpokenOriginSystem;
        string shortDest = spec.Slate.DestinationSystem.Equals("VG-3 DOCK BAY", StringComparison.OrdinalIgnoreCase)
            ? "VG-3"
            : spec.Slate.DestinationSystem;
        string shortPurpose = ShortPurposeSpeech(spec.Slate.PurposeOfVisit);
        string spokenCargo = string.IsNullOrWhiteSpace(spec.GroundTruth.SpokenCargoItem)
            ? ShortCargoSpeech(spec.Slate.CargoManifest)
            : spec.GroundTruth.SpokenCargoItem;

        spec.AutoInterrogationReplyLine =
            $"{spokenOrigin} to {shortDest}. {shortPurpose}. {spokenCargo}.";

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
            "Violet draw in our hold? Just... deck heaters! Keep your scanner off our cargo bay and clear the berth!";

        spec.ConfrontationGuiltyExcuseSpokenRoute =
            $"Did I say {spokenOrigin}? Long watch on the bridge -- check the slate, I meant {spec.Slate.OriginSystem}!";

        spec.ConfrontationGuiltyExcuseSpokenCargo =
            $"Uh, {spokenCargo} was our last haul! Quartermaster must have swapped the hold crates at launch!";

        spec.ConfrontationGuiltyExcuseBannedCargo =
            $"Wait -- {spec.GroundTruth.WaferCargoManifestItem} is on the permanent contraband list?! The broker swore we had a transit exemption!";

        spec.ConfrontationGuiltyExcuseManifestMismatch =
            $"The cert-wafer says {spec.GroundTruth.WaferCargoManifestItem} while the slate says {spec.Slate.CargoManifest}? Dockside clerk loaded the wrong manifest lot!";

        spec.ConfrontationGuiltyExcuseWithheldManifest =
            "We... didn't file a cargo wafer at departure! Look, our hold is private charter -- just wave us through without it!";

        spec.ConfrontationGuiltyExcuseMassMismatch = // TODO: USER DIALOGUE PASS
            $"Extra {Math.Max(120, spec.GroundTruth.ActualMass - spec.Slate.DeclaredTonnage)} tons? Just... spare reactor ballast we loaded after filing!";

        spec.HonestOmittedManifestReplyLine =
            "Oversight on bridge -- transmitting Cargo Manifest packet now.";

        spec.ConfrontationInnocentProtest =
            $"Check your Reference Manual again, Officer! Vessel {reg} is 100% regulation and compliant -- detaining us will cost you!";

        spec.ConfrontationGuiltyExcuseHull = PickExcuseOrDefault(
            prefix, "PrefixHullSilhouetteMismatch", spec, spec.ConfrontationGuiltyExcuseHull);
        spec.ConfrontationGuiltyExcuseClassMismatch = PickExcuseOrDefault(
            prefix, "PrefixHullSilhouetteMismatch", spec, spec.ConfrontationGuiltyExcuseClassMismatch);
        spec.ConfrontationGuiltyExcuseTransponderId = PickExcuseOrDefault(
            prefix, "TransponderMismatch", spec, spec.ConfrontationGuiltyExcuseTransponderId);
        spec.ConfrontationGuiltyExcuseWeapons = PickExcuseOrDefault(
            prefix, "SpoofedWeaponsHot", spec, spec.ConfrontationGuiltyExcuseWeapons);
        spec.ConfrontationGuiltyExcuseOrigin = PickExcuseOrDefault(
            prefix, "FalsifiedOriginWaveform", spec, spec.ConfrontationGuiltyExcuseOrigin);
        spec.ConfrontationGuiltyExcuseExpired = PickExcuseOrDefault(
            prefix, "ExpiredRegistration", spec, spec.ConfrontationGuiltyExcuseExpired);
        spec.ConfrontationGuiltyExcuseOrgMismatch = PickExcuseOrDefault(
            prefix, "PrefixOrganizationMismatch", spec, spec.ConfrontationGuiltyExcuseOrgMismatch);
        spec.ConfrontationGuiltyExcuseSpeciesMismatch = PickExcuseOrDefault(
            prefix, "PrefixSpeciesMismatch", spec, spec.ConfrontationGuiltyExcuseSpeciesMismatch);
        spec.ConfrontationGuiltyExcuseCargoPower = PickExcuseOrDefault(
            prefix, "UnexplainedCargoPowerDraw", spec, spec.ConfrontationGuiltyExcuseCargoPower);

        spec.DetentionReactionLine = FactionDialoguePool.TryPickDetentionReaction(prefix, spec) ?? string.Empty;
    }

    private static string PickExcuseOrDefault(
        string prefix,
        string infractionKey,
        ShipSpec spec,
        string fallback) =>
        FactionDialoguePool.TryPickExcuse(prefix, infractionKey, spec)
        ?? FactionDialoguePool.TryPickExcuse(prefix, "General", spec)
        ?? fallback;

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

    private static int RollOverweightDelta(ShipClass shipClass, DeterministicRng rng) =>
        shipClass switch
        {
            ShipClass.Civilian => rng.NextInt(240, 520),
            ShipClass.Military => rng.NextInt(480, 1050),
            ShipClass.Diplomatic => rng.NextInt(520, 1180),
            ShipClass.Cargo => rng.NextInt(950, 2250),
            ShipClass.HackJob => rng.NextInt(340, 780),
            _ => rng.NextInt(320, 720)
        };

    private static string ShortPurposeSpeech(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return "Routine transit";

        if (purpose.Contains("PATROL", StringComparison.OrdinalIgnoreCase)) return "Patrol escort";
        if (purpose.Contains("CONSULAR", StringComparison.OrdinalIgnoreCase) || purpose.Contains("VIP", StringComparison.OrdinalIgnoreCase)) return "Consular transit";
        if (purpose.Contains("ORE", StringComparison.OrdinalIgnoreCase)) return "Ore haul";
        if (purpose.Contains("CONSORTIUM", StringComparison.OrdinalIgnoreCase)) return "Consortium freight";
        if (purpose.Contains("ARCHIVAL", StringComparison.OrdinalIgnoreCase) || purpose.Contains("SURVEY", StringComparison.OrdinalIgnoreCase)) return "Survey transit";
        if (purpose.Contains("SALVAGE", StringComparison.OrdinalIgnoreCase)) return "Salvage run";
        if (purpose.Contains("COLONY", StringComparison.OrdinalIgnoreCase)) return "Colony supply";
        if (purpose.Contains("STARLINER", StringComparison.OrdinalIgnoreCase)) return "Starliner supply";
        if (purpose.Contains("DEADHEAD", StringComparison.OrdinalIgnoreCase)) return "Deadhead transit";
        if (purpose.Contains("REFUEL", StringComparison.OrdinalIgnoreCase)) return "Refuel transit";
        return "Commercial trade";
    }

    private static string ShortCargoSpeech(string cargo)
    {
        if (string.IsNullOrWhiteSpace(cargo))
            return "General freight";

        if (cargo.Contains("ORDNANCE", StringComparison.OrdinalIgnoreCase)) return "Sealed ordnance";
        if (cargo.Contains("DIPLOMATIC", StringComparison.OrdinalIgnoreCase)) return "Diplomatic pouches";
        if (cargo.Contains("SILICATE", StringComparison.OrdinalIgnoreCase)) return "Refined silicate";
        if (cargo.Contains("LEDGERS", StringComparison.OrdinalIgnoreCase)) return "Stamped ledgers";
        if (cargo.Contains("MINERAL CORE", StringComparison.OrdinalIgnoreCase)) return "Mineral canisters";
        if (cargo.Contains("HULL SCRAP", StringComparison.OrdinalIgnoreCase)) return "Hull scrap";
        if (cargo.Contains("HYDRO-FILTERS", StringComparison.OrdinalIgnoreCase)) return "Hydro-filters";
        if (cargo.Contains("HOSPITALITY", StringComparison.OrdinalIgnoreCase)) return "Hospitality stores";
        if (cargo.Contains("LUBRICANTS", StringComparison.OrdinalIgnoreCase)) return "Industrial lubricants";
        if (cargo.Contains("MERCHANDISE", StringComparison.OrdinalIgnoreCase)) return "General crates";
        if (cargo.Contains("EMPTY", StringComparison.OrdinalIgnoreCase)) return "Empty hold";

        // Stage 2 single-item cargo names (e.g. "HYDRO-GRAIN", "HULL PLATING")
        CargoCatalog catalog = CargoCatalog.Load();
        foreach (string item in catalog.LegalInertPool.Concat(catalog.ControlledLegalBioPool).Concat(catalog.BannedCargoList.Select(b => b.Name)))
        {
            if (cargo.Equals(item, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return cargo.Length <= 22 ? cargo : cargo[..22].TrimEnd();
    }

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

    /// <summary>
    /// Generates a deterministic covert delivery target ship inside the pre-generated 16-ship queue
    /// when the player has decrypted a covert mission briefing scheduled for today.
    /// </summary>
    public ShipSpec GenerateCovertMissionTargetShip(
        GameState state,
        int queueIndex,
        uint reseedNonce = 0,
        HashSet<string>? usedRegs = null)
    {
        int runSeed = state.RunSeed != 0 ? state.RunSeed : 1;
        string prefix = string.IsNullOrWhiteSpace(state.ActiveCovertTargetPrefix) ? "FTV-" : state.ActiveCovertTargetPrefix;
        string regCode = string.IsNullOrWhiteSpace(state.ActiveCovertTargetReg) ? "FTV-719-R" : state.ActiveCovertTargetReg;
        string shipName = string.IsNullOrWhiteSpace(state.ActiveCovertTargetShipName) ? "CINDER LARK" : state.ActiveCovertTargetShipName;
        usedRegs?.Add(regCode);

        DeterministicRng rng = new($"run{runSeed}-day{state.Day}-covert-{state.ActiveCovertMissionId}-n{reseedNonce}");
        ShipFaction visualFaction = ShipRenderer.ParseFactionFromPrefix(prefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(visualFaction);
        ShipClass shipClass = allowedClasses.Contains(ShipClass.Cargo) ? ShipClass.Cargo : allowedClasses[0];
        PortraitSpecies captainSpecies = PickSpeciesForPrefix(prefix, rng.Fork("species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("capt"));
        string originSystem = GetCanonicalOriginForPrefix(prefix, rng.Fork("origin"));
        string destination = "VG-3 DOCK BAY";
        int tonnage = GetBaselineTonnage(shipClass, rng.Fork("tonnage"));
        (string purpose, string cargo) = PickPurposeAndCargo(prefix, shipClass, rng.Fork("cargo"));
        bool expectsCargoWafer = state.Day >= _config.DayUnlockWaferCargo;
        string waferManifestItem = string.Empty;
        string waferSecondaryNote = string.Empty;
        if (expectsCargoWafer)
        {
            CargoCatalog catalog = CargoCatalog.Load();
            waferManifestItem = rng.Fork("covert_stage2_cargo").Pick(catalog.LegalInertPool);
            waferSecondaryNote = "INERT INDUSTRIAL LOT";
            cargo = waferManifestItem.ToUpperInvariant();
        }

        (int curYear, int curMonth) = Constants.GetStationCycle(state.Day);
        (int expYear, int expMonth) = OffsetStationCycle(curYear, curMonth, 12);

        string seedId = $"{regCode}-{shipName.Replace(' ', '-')}-covert";
        string portraitSeed = $"capt-{captainName.Replace(' ', '-')}-{seedId}";

        DataSlateModel slate = new()
        {
            ShipName = shipName,
            RegistrationCode = regCode,
            DeclaredPrefix = prefix,
            DeclaredFactionName = GetFactionFullName(prefix),
            RegistrationExpiryYear = expYear,
            RegistrationExpiryMonth = expMonth,
            DeclaredVisualFaction = visualFaction,
            DeclaredShipClass = shipClass,
            DeclaredCaptainName = captainName,
            DeclaredCaptainSpecies = captainSpecies,
            OriginSystem = originSystem,
            DestinationSystem = destination,
            DeclaredTonnage = tonnage,
            CargoManifest = cargo,
            PurposeOfVisit = purpose,
            DeclaredShieldsStatus = "DOWN",
            DeclaredWeaponsStatus = "OFFLINE",
            PortraitSeedId = portraitSeed,
            HoloPortraitSpecies = captainSpecies,
            HoloAmber = false,
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
            ActualOriginSystem = originSystem,
            SpokenOriginSystem = originSystem,
            SpokenCargoItem = expectsCargoWafer ? waferManifestItem : ShortCargoSpeech(cargo),
            IsDay2GhostRouteSuspect = false,
            ExpectsCargoManifestWafer = expectsCargoWafer,
            CargoManifestInitiallyOmitted = false,
            OmittedManifestIsDishonest = false,
            CargoManifestTransmitted = expectsCargoWafer,
            WaferCargoManifestItem = waferManifestItem,
            WaferCargoSecondaryNote = waferSecondaryNote,
            IsCovertMissionTarget = true,
            CovertMissionId = state.ActiveCovertMissionId,
            ActiveInfractions = []
        };

        ShipSpec spec = new()
        {
            EncounterSeedId = seedId,
            QueueIndex = queueIndex,
            Slate = slate,
            GroundTruth = truth,
            ComplianceDelaySeconds = 0.25f,
            StoryTag = string.IsNullOrWhiteSpace(state.ActiveCovertMissionId) ? null : state.ActiveCovertMissionId
        };

        PopulateDialogueLines(spec, state.GetRep(prefix), state);
        return spec;
    }

    /// <summary>
    /// Builds the automated Day 2+ Morning Ship Roster (exact 10 expected vessels):
    /// - Draws strictly between 1 and 3 real arrivals from the deterministic 16-ship pre-generated queue
    ///   (always including any active scheduled covert mission target ship, never Day 2 Ghost Route suspect,
    ///   and NEVER pulled from random overflow beyond ship 16).
    /// - Appearing on the Morning Ship Roster does NOT guarantee validity (invalid ships remain detainable).
    /// - Fills the remaining 7 to 9 entries with plausible non-colliding decoy vessels and shuffles deterministically.
    /// </summary>
    public List<MorningRosterEntry> GenerateMorningShipRoster(
        GameState state,
        IReadOnlyList<ShipSpec> dailyQueue,
        Core.ConfigFlags? config = null)
    {
        Core.ConfigFlags cfg = config ?? _config;
        int runSeed = state.RunSeed != 0 ? state.RunSeed : 1;
        DeterministicRng rng = new($"run{runSeed}-day{state.Day}-morning-roster");

        List<MorningRosterEntry> entries = [];
        HashSet<string> usedRegs = new(StringComparer.OrdinalIgnoreCase);

        foreach (ShipSpec qShip in dailyQueue)
        {
            usedRegs.Add(qShip.Slate.RegistrationCode);
        }

        // Eligible real ships in slots 0..15 (exclude Day 2 Ghost Route suspect)
        List<ShipSpec> eligibleReal = dailyQueue
            .Where(s => !s.GroundTruth.IsDay2GhostRouteSuspect)
            .ToList();

        // Between 1 and 3 real arrivals from the 16-ship queue (preferring earlier slots so players actually see them)
        int targetRealCount = rng.NextInt(1, 4); // 1, 2, or 3
        List<ShipSpec> chosenReal = [];

        // Always include active scheduled covert delivery mission target if present in today's 16-ship queue
        ShipSpec? missionTarget = eligibleReal.FirstOrDefault(s => s.GroundTruth.IsCovertMissionTarget);
        if (missionTarget != null)
        {
            chosenReal.Add(missionTarget);
        }

        // Day 6: always include the exact deterministic Day 6 MNG- intro target vessel on the Day 6 Morning Ship Roster
        if (state.Day == 6)
        {
            (string d6TargetReg, int d6TargetSlot) = EnsureDay6IntroTargetAssigned(state);
            if (!chosenReal.Any(s => s.Slate.RegistrationCode.Equals(d6TargetReg, StringComparison.OrdinalIgnoreCase)))
            {
                ShipSpec? mngShip = eligibleReal.FirstOrDefault(s =>
                    s.Slate.RegistrationCode.Equals(d6TargetReg, StringComparison.OrdinalIgnoreCase))
                    ?? eligibleReal.FirstOrDefault(s =>
                        s.QueueIndex == d6TargetSlot &&
                        s.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase))
                    ?? eligibleReal.FirstOrDefault(s => s.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase));
                if (mngShip != null && !chosenReal.Contains(mngShip))
                {
                    chosenReal.Add(mngShip);
                }
            }
        }

        // Day 7+: if Intercept contracts are active, include matching real vessels (newest contract first) from today's 16-ship queue
        if (state.Day >= 7 && state.ActiveInterceptMissionIds.Count > 0 && chosenReal.Count < 3)
        {
            CovertMissionDatabase missionDb = CovertMissionDatabase.Load();
            foreach (string activeId in state.ActiveInterceptMissionIds.AsEnumerable().Reverse())
            {
                if (chosenReal.Count >= 3)
                    break;

                string? targetPfx = missionDb.Missions
                    .FirstOrDefault(m => m.IsInterceptMission && m.MissionId.Equals(activeId, StringComparison.OrdinalIgnoreCase))
                    ?.TargetPrefix;

                if (!string.IsNullOrWhiteSpace(targetPfx) &&
                    !chosenReal.Any(s => s.Slate.DeclaredPrefix.Equals(targetPfx, StringComparison.OrdinalIgnoreCase)))
                {
                    ShipSpec? interceptShip = eligibleReal.FirstOrDefault(s =>
                        (s.QueueIndex == ActiveInterceptTargetGuaranteeSlot || s.QueueIndex == ActiveInterceptTargetGuaranteeSlot + 1) &&
                        s.Slate.DeclaredPrefix.Equals(targetPfx, StringComparison.OrdinalIgnoreCase))
                        ?? eligibleReal.FirstOrDefault(s => s.Slate.DeclaredPrefix.Equals(targetPfx, StringComparison.OrdinalIgnoreCase));
                    if (interceptShip != null && !chosenReal.Contains(interceptShip))
                    {
                        chosenReal.Add(interceptShip);
                    }
                }
            }
        }

        // Prefer ships inside slots 0..7 so an average 4-8 ship watch reliably meets rostered vessels
        List<ShipSpec> earlyPool = eligibleReal
            .Where(s => s.QueueIndex < 8 && !chosenReal.Contains(s))
            .ToList();
        List<ShipSpec> remainingPool = eligibleReal
            .Where(s => !chosenReal.Contains(s))
            .ToList();

        while (chosenReal.Count < targetRealCount && (earlyPool.Count > 0 || remainingPool.Count > 0))
        {
            List<ShipSpec> pickFrom = earlyPool.Count > 0 ? earlyPool : remainingPool;
            int idx = rng.NextInt(0, pickFrom.Count);
            ShipSpec picked = pickFrom[idx];
            chosenReal.Add(picked);
            earlyPool.Remove(picked);
            remainingPool.Remove(picked);
        }

        foreach (ShipSpec real in chosenReal)
        {
            entries.Add(new MorningRosterEntry
            {
                RegistrationCode = real.Slate.RegistrationCode,
                Prefix = real.Slate.DeclaredPrefix,
                OrganizationName = GetFactionFullName(real.Slate.DeclaredPrefix),
                ShipName = real.Slate.ShipName,
                ShipClassCode = GetShortClassCode(real.Slate.DeclaredShipClass),
                IsRealArrival = true,
                RealQueueSlot = real.QueueIndex,
                IsMissionTarget = real.GroundTruth.IsCovertMissionTarget
            });
        }

        // Fill remaining slots up to exact 10 with unique decoy vessels not in the 16-ship queue
        int decoyIdx = 0;
        while (entries.Count < 10)
        {
            DeterministicRng dRng = rng.Fork($"decoy_{decoyIdx++}");
            string prefix = PickWeightedPrefix(dRng.Fork("pfx"));
            ShipFaction vf = ShipRenderer.ParseFactionFromPrefix(prefix);
            ShipClass cls = dRng.Fork("cls").Pick(ShipRenderer.GetAllowedClasses(vf));
            string reg = GenerateUniqueRegistration(prefix, dRng.Fork("reg"), usedRegs);
            string name = GenerateShipName(prefix, dRng.Fork("name"));

            entries.Add(new MorningRosterEntry
            {
                RegistrationCode = reg,
                Prefix = prefix,
                OrganizationName = GetFactionFullName(prefix),
                ShipName = name,
                ShipClassCode = GetShortClassCode(cls),
                IsRealArrival = false,
                RealQueueSlot = -1,
                IsMissionTarget = false
            });
        }

        // Deterministic Fisher-Yates shuffle so real arrivals aren't always at top
        DeterministicRng shufRng = rng.Fork("shuffle");
        for (int i = entries.Count - 1; i > 0; i--)
        {
            int j = shufRng.NextInt(0, i + 1);
            (entries[i], entries[j]) = (entries[j], entries[i]);
        }

        return entries;
    }
}
