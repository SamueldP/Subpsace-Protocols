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
/// Deterministic procedural ship encounter & Daily Expected Ship List generator.
/// Generates 10 daily priority arrivals (guaranteeing 2-3 appear among the first 5 arriving ships),
/// injects the Day 2 hardcoded "Ghost Route" suspect (explicitly excluded from the Expected Ship List),
/// locks outright shield/weapon compliance refusals strictly to Day 14+ / rep_score <= -9,
/// and produces ~65% valid vessels and ~35% vessels with active daily infractions.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShipGenerator
{
    public const int Day2GhostRouteQueueSlot = 1; // 2nd ship on Day 2 (after 1 normal ship)

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

    private static readonly string[] ValidOriginSystems =
    [
        "Veridia",
        "Korr-Vath",
        "Dul-Krag",
        "Gharok",
        "Oruun-4",
        "Caldera Rim",
        "Solvane"
    ];

    private static readonly string[] DestinationPorts =
    [
        "VG-3 DOCK BAY",
        "Veridia",
        "Korr-Vath",
        "Dul-Krag",
        "Oruun-4",
        "Caldera Rim",
        "Solvane"
    ];

    private readonly List<ExpectedShipEntry> _expectedShipsToday = [];
    private readonly Dictionary<int, int> _scheduledQueueSlotToExpectedIndex = [];
    private int _currentDay = 1;

    public IReadOnlyList<ExpectedShipEntry> ExpectedShipsToday => _expectedShipsToday;

    /// <summary>
    /// Initializes the 10 Expected Ships for the day and guarantees 2 to 3 of them
    /// are scheduled among the first 5 arriving vessels (queue indices 0..4).
    /// On Day 2, explicitly reserves queue slot 1 for the non-Expected "Ghost Route" suspect.
    /// </summary>
    public IReadOnlyList<ExpectedShipEntry> InitializeDaySchedule(int day, uint sessionEntropy = 0)
    {
        _currentDay = Math.Max(1, day);
        _expectedShipsToday.Clear();
        _scheduledQueueSlotToExpectedIndex.Clear();

        DeterministicRng dayRng = new($"vg3-day-{_currentDay}-schedule-{sessionEntropy}");
        HashSet<string> usedRegs = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < Constants.ExpectedShipsPerDay; i++)
        {
            DeterministicRng itemRng = dayRng.Fork($"expected_{i}");
            string prefix = PickWeightedPrefix(itemRng);
            ShipFaction faction = ShipRenderer.ParseFactionFromPrefix(prefix);
            IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(faction);
            ShipClass shipClass = itemRng.Pick(allowedClasses);

            string regCode;
            int guard = 0;
            do
            {
                regCode = GenerateRegistrationCode(prefix, itemRng.Fork($"reg_{guard++}"));
            } while (!usedRegs.Add(regCode));

            string shipName = GenerateShipName(prefix, itemRng);
            string origin = GetCanonicalOriginForPrefix(prefix, itemRng);

            _expectedShipsToday.Add(new ExpectedShipEntry
            {
                SlotNumber = i + 1,
                RegistrationCode = regCode,
                ShipName = shipName,
                FactionPrefix = prefix,
                FactionShortName = GetFactionShortLabel(prefix),
                DeclaredFaction = faction,
                DeclaredClass = shipClass,
                OriginSystem = origin,
                DestinationSystem = "VG-3 DOCK BAY",
                HasArrived = false,
                WasResolved = false,
                ResolutionTag = "SCHED"
            });
        }

        // Guarantee 2 or 3 Expected Ships appear among the first 5 arriving ships (queue indices 0..4).
        // On Day 2, exclude Day2GhostRouteQueueSlot (slot 1) so the hardcoded suspect is NEVER an Expected Ship.
        int guaranteedEarlyCount = dayRng.Fork("early_count").NextBool(0.55f) ? 3 : 2;
        List<int> earlyQueueSlots = _currentDay == 2
            ? [0, 2, 3, 4]
            : [0, 1, 2, 3, 4];
        Shuffle(earlyQueueSlots, dayRng.Fork("early_slots"));

        List<int> expectedIndices = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        Shuffle(expectedIndices, dayRng.Fork("exp_indices"));

        for (int i = 0; i < guaranteedEarlyCount; i++)
        {
            _scheduledQueueSlotToExpectedIndex[earlyQueueSlots[i]] = expectedIndices[i];
        }

        List<int> laterQueueSlots = [5, 6, 7, 8, 9, 10, 11];
        Shuffle(laterQueueSlots, dayRng.Fork("later_slots"));
        for (int i = 0; i < 3; i++)
        {
            _scheduledQueueSlotToExpectedIndex[laterQueueSlots[i]] = expectedIndices[guaranteedEarlyCount + i];
        }

        return _expectedShipsToday;
    }

    public List<string> GenerateExpectedShipRegistrations(int day)
    {
        if (_expectedShipsToday.Count == 0 || _currentDay != day)
        {
            InitializeDaySchedule(day);
        }

        return _expectedShipsToday.Select(e => e.RegistrationCode).ToList();
    }

    /// <summary>
    /// Generates the next arriving ship for the shift queue.
    /// Injects the hardcoded "Ghost Route" suspect on Day 2 (never on the Expected Ship List).
    /// Outright shield/weapon compliance refusals are strictly locked to Day 14+ & rep_score <= -9.
    /// </summary>
    public ShipSpec GenerateNextShip(
        GameState state,
        int queueIndex,
        uint encounterEntropy = 0,
        DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto)
    {
        if (_expectedShipsToday.Count == 0 || _currentDay != state.Day)
        {
            InitializeDaySchedule(state.Day, encounterEntropy);
        }

        if (state.Day == 2 && queueIndex == Day2GhostRouteQueueSlot && forceValidity == DebugSpawnValidityMode.Auto)
        {
            return GenerateDay2GhostRouteSuspect(state, queueIndex, encounterEntropy);
        }

        DeterministicRng rng = new($"day{state.Day}-q{queueIndex}-e{encounterEntropy}");

        ExpectedShipEntry? expectedEntry = null;
        int expectedSlotIdx = -1;
        if (_scheduledQueueSlotToExpectedIndex.TryGetValue(queueIndex, out int mappedIdx) &&
            mappedIdx >= 0 && mappedIdx < _expectedShipsToday.Count &&
            !_expectedShipsToday[mappedIdx].HasArrived)
        {
            expectedEntry = _expectedShipsToday[mappedIdx];
            expectedSlotIdx = mappedIdx;
            expectedEntry.HasArrived = true;
            expectedEntry.ResolutionTag = "IN BAY";
        }

        string declaredPrefix = expectedEntry != null
            ? expectedEntry.FactionPrefix
            : PickWeightedPrefix(rng.Fork("prefix"));

        ShipFaction declaredVisualFaction = ShipRenderer.ParseFactionFromPrefix(declaredPrefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(declaredVisualFaction);
        ShipClass declaredClass = expectedEntry != null
            ? expectedEntry.DeclaredClass
            : rng.Fork("class").Pick(allowedClasses);

        string regCode = expectedEntry != null
            ? expectedEntry.RegistrationCode
            : GenerateUniqueNonExpectedRegistration(declaredPrefix, rng.Fork("reg"));

        string shipName = expectedEntry != null
            ? expectedEntry.ShipName
            : GenerateShipName(declaredPrefix, rng.Fork("name"));

        string originSystem = expectedEntry != null
            ? expectedEntry.OriginSystem
            : GetCanonicalOriginForPrefix(declaredPrefix, rng.Fork("origin"));

        string actualOriginSystem = originSystem;
        string destinationSystem = expectedEntry != null
            ? expectedEntry.DestinationSystem
            : rng.Fork("dest").Pick(DestinationPorts);

        PortraitSpecies captainSpecies = PickSpeciesForPrefix(declaredPrefix, rng.Fork("species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("capt_name"));
        int tonnage = GetBaselineTonnage(declaredClass, rng.Fork("tonnage"));
        (string purpose, string cargo) = PickPurposeAndCargo(declaredPrefix, declaredClass, rng.Fork("cargo"));

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
        bool initialShieldsUp = rng.Fork("init_shields").NextFloat() < 0.34f;
        bool initialWeaponsHot = rng.Fork("init_weapons").NextFloat() < 0.22f;
        bool willRefuseShields = false;
        bool willRefuseWeapons = false;
        bool hasSpoofedWeapons = false;
        List<ShipInfraction> infractions = [];

        if (!isValidShip)
        {
            // Build eligible infraction pool strictly adhering to Daily Rule Escalation:
            // - Day 1-2: Prefix/Hull Silhouette Mismatch, Spoofed Weapons Hot on SPS, and (if non-expected) UNR- Prefix
            // - Day 3+:  Also adds Falsified Origin Warp Waveform
            // - Day 14+ & rep_score <= -9 ONLY: Adds Outright Shield/Weapon Compliance Refusal
            List<ShipInfractionType> eligibleInfractions =
            [
                ShipInfractionType.PrefixHullSilhouetteMismatch,
                ShipInfractionType.SpoofedWeaponsHot
            ];

            if (expectedEntry == null)
            {
                eligibleInfractions.Add(ShipInfractionType.UnregisteredPrefix);
            }

            if (state.Day >= 3)
            {
                eligibleInfractions.Add(ShipInfractionType.FalsifiedOriginWaveform);
            }

            if (state.Day >= 14 && factionRep <= Constants.RepExtremeHostileThreshold)
            {
                eligibleInfractions.Add(ShipInfractionType.RefusedShieldsOrWeaponsCompliance);
            }

            ShipInfractionType chosenInfraction = rng.Fork("infraction_type").Pick(eligibleInfractions);

            switch (chosenInfraction)
            {
                case ShipInfractionType.PrefixHullSilhouetteMismatch:
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
                    break;
                }

                case ShipInfractionType.SpoofedWeaponsHot:
                {
                    initialWeaponsHot = true;
                    hasSpoofedWeapons = true;
                    willRefuseWeapons = true;
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.SpoofedWeaponsHot,
                        Code = "PROTO-01C",
                        CitationReason = "SPOOFED WEAPONS WERE HOT ON SPS POWER GRID"
                    });
                    break;
                }

                case ShipInfractionType.UnregisteredPrefix:
                {
                    declaredPrefix = Constants.UnregisteredPrefix;
                    regCode = GenerateUniqueNonExpectedRegistration(Constants.UnregisteredPrefix, rng.Fork("unr_reg"));
                    infractions.Add(new ShipInfraction
                    {
                        Type = ShipInfractionType.UnregisteredPrefix,
                        Code = "PROTO-01B",
                        CitationReason = $"UNREGISTERED TRANSPONDER PREFIX '{regCode}' PROHIBITED"
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
                    // Strictly Day 14+ & rep_score <= -9
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
                        CitationReason = $"CAPTAIN OUTRIGHT REFUSED PRE-CLEARANCE ORDER TO {refusedSystem}"
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

        string seedId = $"{regCode}-{shipName.Replace(' ', '-')}-q{queueIndex}-e{encounterEntropy}";
        string portraitSeed = $"capt-{captainName.Replace(' ', '-')}-{seedId}";

        DataSlateModel slate = new()
        {
            ShipName = shipName,
            RegistrationCode = regCode,
            DeclaredPrefix = declaredPrefix,
            DeclaredFactionName = GetFactionFullName(declaredPrefix),
            DeclaredVisualFaction = declaredVisualFaction,
            DeclaredShipClass = declaredClass,
            DeclaredCaptainName = captainName,
            DeclaredCaptainSpecies = captainSpecies,
            OriginSystem = originSystem,
            DestinationSystem = destinationSystem,
            DeclaredTonnage = tonnage,
            CargoManifest = cargo,
            PurposeOfVisit = purpose,
            DeclaredShieldsStatus = "DOWN",
            DeclaredWeaponsStatus = "OFFLINE",
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
            ActualCargoPowerDraw = false,
            ActualCommsPowered = !staticCamera,
            ActualVisualFaction = actualVisualFaction,
            ActualShipClass = actualShipClass,
            ActualMass = tonnage,
            IsExpectedShip = expectedEntry != null,
            ExpectedShipSlotIndex = expectedSlotIdx,
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
    /// Generates the Day 2 Hardcoded Suspect Encounter ("The Ghost Route") from
    /// /design/07_Daily_Schedule/Expected_Ship_List_and_Traffic.md.
    /// Explicitly excluded from the Day 2 Expected Ship List so detaining them never
    /// triggers an Expected-Ship Security Strike.
    /// </summary>
    public ShipSpec GenerateDay2GhostRouteSuspect(GameState state, int queueIndex, uint encounterEntropy = 0)
    {
        if (_expectedShipsToday.Count == 0 || _currentDay != state.Day)
        {
            InitializeDaySchedule(state.Day, encounterEntropy);
        }

        DeterministicRng rng = new($"day2-ghost-route-suspect-{encounterEntropy}");

        string declaredPrefix = rng.Fork("suspect_prefix").Pick(Day2GhostRouteEligiblePrefixes);
        ShipFaction visualFaction = ShipRenderer.ParseFactionFromPrefix(declaredPrefix);
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(visualFaction);
        ShipClass shipClass = rng.Fork("suspect_class").Pick(allowedClasses);

        string regCode = GenerateUniqueNonExpectedRegistration(declaredPrefix, rng.Fork("suspect_reg"));
        string shipName = GenerateShipName(declaredPrefix, rng.Fork("suspect_name"));
        string declaredOrigin = GetCanonicalOriginForPrefix(declaredPrefix, rng.Fork("suspect_origin"));
        string destination = "VG-3 DOCK BAY";

        PortraitSpecies captainSpecies = PickSpeciesForPrefix(declaredPrefix, rng.Fork("suspect_species"));
        string captainName = GenerateCaptainName(captainSpecies, rng.Fork("suspect_capt"));
        int tonnage = GetBaselineTonnage(shipClass, rng.Fork("suspect_tonnage"));

        (string slotPurpose, string slotCargoSpeech, string slateCargo) =
            GetGhostRouteFactionDialogueSlots(declaredPrefix, declaredOrigin);

        string seedId = $"{regCode}-{shipName.Replace(' ', '-')}-ghostroute";
        string portraitSeed = $"capt-{captainName.Replace(' ', '-')}-{seedId}";

        state.Day2SuspectEncountered = true;
        state.Day2SuspectRegistration = regCode;
        state.Day2SuspectDeclaredOrigin = declaredOrigin;
        state.Day2SuspectActualOrigin = "The Ashen Rift";
        state.Day2SuspectPrefix = declaredPrefix;
        state.Day2SuspectShipName = shipName;
        state.QuestFlags["Day2_GhostRouteEncountered"] = true;
        state.SaveToDisk();

        DataSlateModel slate = new()
        {
            ShipName = shipName,
            RegistrationCode = regCode,
            DeclaredPrefix = declaredPrefix,
            DeclaredFactionName = GetFactionFullName(declaredPrefix),
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
            ActualVisualFaction = visualFaction,
            ActualShipClass = shipClass,
            ActualMass = tonnage,
            IsExpectedShip = false,
            ExpectedShipSlotIndex = -1,
            IsDecommissionedHull = false,
            HasDownstreamHullScorch = false,
            UsesStaticViewscreen = false,
            WillRefuseShieldsCompliance = false,
            WillRefuseWeaponsCompliance = false,
            HasSpoofedWeapons = false,
            ActualCaptainSpecies = captainSpecies,
            ActualOriginSystem = "The Ashen Rift",
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

    private string GenerateUniqueNonExpectedRegistration(string prefix, DeterministicRng rng)
    {
        int attempt = 0;
        while (true)
        {
            string candidate = GenerateRegistrationCode(prefix, rng.Fork($"u_{attempt++}"));
            bool collidesWithExpected = _expectedShipsToday.Any(
                e => e.RegistrationCode.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (!collidesWithExpected)
                return candidate;
        }
    }

    private static string PickFalsifiedOriginSystem(string declaredOrigin, DeterministicRng rng)
    {
        if (rng.NextBool(0.55f))
        {
            return "The Ashen Rift";
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
            "UNR-" => $"Vessel {shipName} ({reg}) on approach. Transponder beacon is cycling -- requesting manual berth.",
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

        spec.AutoInterrogationReplyLine =
            $"Inbound from {spec.Slate.OriginSystem} to {spec.Slate.DestinationSystem}. " +
            $"Purpose: {spec.Slate.PurposeOfVisit}. Cargo: {spec.Slate.CargoManifest}.";

        spec.ConfrontationGuiltyExcuseHull =
            $"Wait -- we bought this hull surplus last quarter! The registry refit paperwork for {reg} must still be stuck in port processing!";

        spec.ConfrontationGuiltyExcuseWeapons =
            "Look, our slate clearly says weapons OFFLINE! That red line on your SPS is just a... faulty diagnostic shunt. We aren't powering anything down!";

        spec.ConfrontationGuiltyExcuseUnregistered =
            "Our registry transponder burned out two jumps back! We don't have an active prefix code -- just give us a temporary berth!";

        spec.ConfrontationGuiltyExcuseOrigin =
            $"Your salvaged spectrometer coil is drifting! Our logbook says {spec.Slate.OriginSystem} -- ignore that waveform strip!";

        spec.ConfrontationInnocentProtest =
            $"Check your Reference Manual again, Officer! Vessel {reg} is 100% regulation and compliant -- detaining us will cost you!";
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
                "ZNI-" => "Solvane",
                _ => "Caldera Rim"
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
            _ => rng.Pick(PortraitRenderer.AllActiveSpecies)
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
            "UNR-" => "UNREGISTERED TRANSPONDER",
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
            "UNR-" => "UNREG (UNR)",
            _ => "UNKNOWN"
        };

    private static void Shuffle<T>(List<T> list, DeterministicRng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
