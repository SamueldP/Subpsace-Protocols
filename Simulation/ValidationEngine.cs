using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.Simulation;

public enum PlayerDecision
{
    GrantAccess = 0,
    Detain = 1
}

public sealed class DecisionEvaluationResult
{
    public PlayerDecision Decision { get; init; }
    public string RegistrationCode { get; init; } = string.Empty;
    public string ShipName { get; init; } = string.Empty;
    public bool IsAccurate { get; init; }
    public bool IsCitation { get; init; }
    public bool IsSecurityStrike { get; init; }
    public int ScripDelta { get; init; }
    public int WarningNumber { get; init; }
    public int TotalSecurityStrikes { get; init; }
    public string HeaderText { get; init; } = string.Empty;
    public string ViolationReason { get; init; } = string.Empty;
    public string PenaltyText { get; init; } = string.Empty;
}

public readonly record struct StokerCalibrationResult(
    bool IsCalibrated,
    float CurrentSafeLoad,
    float CurrentShock,
    float PreviewShockDelta,
    float PreviewTotalStrain,
    float NextSafeLoadOnClearance,
    float NextShockOnClearance,
    float PostClearanceTotalStrain,
    bool OverloadsOnClearance);

/// <summary>
/// Evaluates Grant Access vs. Detain decisions against ShipSpec.IsValid and ShipSpec.ProtocolReviewVerified:
/// - Day 1+: Protocol Review verification is required before detaining (unverified detention still detains
///   the ship but triggers a Brengop Clerical Warning).
/// - Days 1-2: Security Strikes inactive; default 10 SC base pay.
/// - Day 3+: Security Strikes active alongside full Protocol Review verification.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ValidationEngine
{
    public const string UnverifiedDetentionReason =
        "UNVERIFIED DETENTION -- CONFIRM FAULT VIA PROTOCOL REVIEW FIRST";

    /// <summary>
    /// Shared Day 15+ Stellar Stoker calibration &amp; uncalibrated shock evaluator used by BOTH
    /// live [ANCHOR] preview needle spikes and post-departure clearance strain resolution.
    /// - Idle baseline: SafeLoad = 15%, Shock = 0% (configurable via ConfigFlags).
    /// - Every cleared ship steps SafeLoad via SafeLoad += (StokerSafeLoadCap - SafeLoad) * 0.30f (caps &lt;= 65%).
    /// - Uncalibrated clearances additionally add +StokerUncalibratedShock (default +33%) to Shock:
    ///   1 slip-up caps at &lt;= 98% (65% + 33%); 2 slip-ups cross &gt; 100% even on a 2-5 ship shift.
    /// </summary>
    public static StokerCalibrationResult EvaluateStokerMassCalibration(
        ShipSpec ship,
        int day,
        Core.ConfigFlags? config = null,
        float? currentSafeLoad = null,
        float currentShock = 0f)
    {
        float idleBaseline = config?.StokerIdleBaseline ?? 15.0f;
        float safeCap = config?.StokerSafeLoadCap ?? 65.0f;
        float stepFactor = config?.StokerSafeLoadStepFactor ?? 0.30f;
        float shockStep = config?.StokerUncalibratedShock ?? 33.0f;
        int unlockDay = config?.DayUnlockStoker ?? Constants.DefaultDayUnlockStoker;
        bool stokerEnabled = config?.EnableStokerDial ?? true;

        float safeNow = Math.Max(0f, currentSafeLoad ?? idleBaseline);
        float shockNow = Math.Max(0f, currentShock);

        if (day < unlockDay || !stokerEnabled)
        {
            float totalInactive = safeNow + shockNow;
            return new StokerCalibrationResult(
                IsCalibrated: true,
                CurrentSafeLoad: safeNow,
                CurrentShock: shockNow,
                PreviewShockDelta: 0f,
                PreviewTotalStrain: totalInactive,
                NextSafeLoadOnClearance: safeNow,
                NextShockOnClearance: shockNow,
                PostClearanceTotalStrain: totalInactive,
                OverloadsOnClearance: totalInactive > 100.0f);
        }

        ShipGroundTruth truth = ship.GroundTruth;
        int declaredMass = ship.Slate.DeclaredTonnage;
        int actualMass = truth.ActualMass;

        bool legallyAccountedByManifest =
            truth.CargoManifestTransmitted &&
            truth.CargoManifestAccountedExtraMass > 0 &&
            !truth.ActiveInfractions.Any(i => i.Type is ShipInfractionType.BannedCargoItem or ShipInfractionType.StokerMassMismatch) &&
            (declaredMass + truth.CargoManifestAccountedExtraMass) == actualMass;

        bool hasMassMismatchInfraction = truth.ActiveInfractions.Any(i => i.Type == ShipInfractionType.StokerMassMismatch);
        bool isCalibrated = !hasMassMismatchInfraction && (actualMass == declaredMass || legallyAccountedByManifest);

        float previewDelta = isCalibrated ? 0f : shockStep;
        float previewTotal = safeNow + shockNow + previewDelta;

        float headroom = Math.Max(0f, safeCap - safeNow);
        float nextSafe = Math.Min(safeCap, safeNow + headroom * stepFactor);
        float nextShock = isCalibrated ? shockNow : (shockNow + shockStep);
        float postTotal = nextSafe + nextShock;

        return new StokerCalibrationResult(
            IsCalibrated: isCalibrated,
            CurrentSafeLoad: safeNow,
            CurrentShock: shockNow,
            PreviewShockDelta: previewDelta,
            PreviewTotalStrain: previewTotal,
            NextSafeLoadOnClearance: nextSafe,
            NextShockOnClearance: nextShock,
            PostClearanceTotalStrain: postTotal,
            OverloadsOnClearance: postTotal > 100.0f);
    }

    public DecisionEvaluationResult EvaluateDecision(
        GameState state,
        ShipSpec ship,
        PlayerDecision decision,
        WorldSim worldSim,
        Core.ConfigFlags? config = null)
    {
        ShipGroundTruth truth = ship.GroundTruth;
        string prefix = ship.Slate.DeclaredPrefix;
        string reg = ship.Slate.RegistrationCode;
        int basePay = state.BasePayPerShipScrip > 0 ? state.BasePayPerShipScrip : Constants.BasePayPerShipScrip;
        int strikeCap = Math.Max(1, config?.MaxStrikesBeforeAudit ?? Constants.SecurityStrikesAuditThreshold);
        bool protocolReviewRequired = true; // Unlocked starting Day 1, Ship 1
        int warpUnlockDay = config?.DayUnlockWarpScanner ?? Constants.DefaultDayUnlockWarpScanner;
        bool securityStrikesActive = state.SecurityStrikesActive || state.Day >= warpUnlockDay;

        // =====================================================================
        // SPECIAL BRANCH: Day 3 Hardcoded Suspect ("The Ghost Route")
        // =====================================================================
        if (truth.IsDay2GhostRouteSuspect)
        {
            state.Day2SuspectEncountered = true;
            state.Day2SuspectRegistration = reg;
            state.Day2SuspectDeclaredOrigin = ship.Slate.OriginSystem;
            state.Day2SuspectActualOrigin = truth.ActualOriginSystem;
            state.Day2SuspectPrefix = prefix;
            state.Day2SuspectShipName = ship.Slate.ShipName;
            state.GhostRouteSuspectDetained = (decision == PlayerDecision.Detain);
            state.QuestFlags["Day2_GhostRouteEncountered"] = true;

            if (decision == PlayerDecision.GrantAccess)
            {
                state.ShipsProcessedToday++;
                state.ShiftGrossPayEarned += basePay;
                state.PactScrip += basePay;
                worldSim.ModifyReputation(state, prefix, +1);
                worldSim.ModifyGalacticPower(state, prefix, +1);
                RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
                state.SaveToDisk();

                AudioHooks.Play("clearance_verified_chime");

                return new DecisionEvaluationResult
                {
                    Decision = decision,
                    RegistrationCode = reg,
                    ShipName = ship.Slate.ShipName,
                    IsAccurate = true,
                    IsCitation = false,
                    IsSecurityStrike = false,
                    ScripDelta = basePay,
                    WarningNumber = state.ClericalWarningsThisShift,
                    TotalSecurityStrikes = state.SecurityStrikes,
                    HeaderText = "PACT PORT AUTHORITY // DOCKING CLEARANCE VERIFIED",
                    ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) CLEARED UNDER DAY 03 PROTOCOLS.",
                    PenaltyText = $"CREDITED: +{basePay} SC (TOTAL: {state.PactScrip} SC)"
                };
            }
            else
            {
                // Detaining Day 3 Ghost Route suspect:
                // - Standard immediate clerical citation, no base pay payout
                // - Invalid-detain reputation change (-2) rather than -4
                // - Zero Security Strikes on Days 1-3
                worldSim.ModifyReputation(state, prefix, -2);
                worldSim.ModifyGalacticPower(state, prefix, -1);

                int fine = ApplyClericalMistake(state);
                RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
                state.SaveToDisk();
                AudioHooks.Play("citation_buzz");

                string penaltyLine = fine == 0
                    ? $"CLERICAL WARNING {state.ClericalWarningsThisShift} OF {Constants.FreeClericalWarningsPerShift} (0 SC FINE)"
                    : $"BRENGOP ADMINISTRATIVE FINE: -{fine} SC (WARNINGS EXHAUSTED)";

                return new DecisionEvaluationResult
                {
                    Decision = decision,
                    RegistrationCode = reg,
                    ShipName = ship.Slate.ShipName,
                    IsAccurate = false,
                    IsCitation = true,
                    IsSecurityStrike = false,
                    ScripDelta = -fine,
                    WarningNumber = state.ClericalWarningsThisShift,
                    TotalSecurityStrikes = state.SecurityStrikes,
                    HeaderText = "BRENGOP MERCANTILE OPS // PROTOCOL CITATION",
                    ViolationReason = $"UNVERIFIED DETAINMENT ({reg}) -- NO ACTIVE DAY 03 VIOLATION",
                    PenaltyText = penaltyLine
                };
            }
        }

        // =====================================================================
        // VALIDATION TRUTH TABLE (Day 1-3 Simple Yes/No | Day 4+ Protocol Review & Strikes)
        // =====================================================================
        bool isValid = ship.IsValid;
        bool unaddressedCombatSystems = truth.ActualShieldsUp || (state.Day >= Constants.DayUnlockSpsAndCombat && truth.ActualWeaponsHot);
        int cargoUnlockDay = config?.DayUnlockWaferCargo ?? Constants.DefaultDayUnlockWaferCargo;
        bool missingRequiredCargoWafer = state.Day >= cargoUnlockDay &&
                                         truth.ExpectsCargoManifestWafer &&
                                         truth.CargoManifestInitiallyOmitted &&
                                         !truth.CargoManifestTransmitted;
        if (WarpSignatureGenerator.IsRestrictedOrigin(truth.ActualOriginSystem))
        {
            state.AshenRiftEncounteredEver = true;
        }

        // Case 1: IsValid == true + Grant Access (with shields down, weapons offline on Day 2+, & manifest verified on Day 12+) -> Accurate
        if (isValid && decision == PlayerDecision.GrantAccess && !unaddressedCombatSystems && !missingRequiredCargoWafer)
        {
            state.ShipsProcessedToday++;
            state.ShiftGrossPayEarned += basePay;
            state.PactScrip += basePay;
            worldSim.ModifyReputation(state, prefix, +1);
            worldSim.ModifyGalacticPower(state, prefix, +1);
            RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
            state.SaveToDisk();

            AudioHooks.Play("clearance_verified_chime");

            return new DecisionEvaluationResult
            {
                Decision = decision,
                RegistrationCode = reg,
                ShipName = ship.Slate.ShipName,
                IsAccurate = true,
                IsCitation = false,
                IsSecurityStrike = false,
                ScripDelta = basePay,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = "PACT PORT AUTHORITY // DOCKING CLEARANCE VERIFIED",
                ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) PROCESSED ACCURATELY.",
                PenaltyText = $"CREDITED: +{basePay} SC (TOTAL: {state.PactScrip} SC)"
            };
        }

        // Case 5: IsValid == false + Detain (+ ProtocolReviewVerified when Day 3+) -> Accurate
        if (!isValid && decision == PlayerDecision.Detain && (!protocolReviewRequired || ship.ProtocolReviewVerified))
        {
            state.ShipsProcessedToday++;
            state.ShiftGrossPayEarned += basePay;
            state.PactScrip += basePay;
            worldSim.ModifyReputation(state, prefix, -2);
            worldSim.ModifyGalacticPower(state, prefix, -1);
            RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
            state.SaveToDisk();

            AudioHooks.Play("clearance_verified_chime");

            return new DecisionEvaluationResult
            {
                Decision = decision,
                RegistrationCode = reg,
                ShipName = ship.Slate.ShipName,
                IsAccurate = true,
                IsCitation = false,
                IsSecurityStrike = false,
                ScripDelta = basePay,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = "PACT PORT AUTHORITY // VIOLATION DETAINMENT CONFIRMED",
                ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) DETAINED ACCURATELY.",
                PenaltyText = $"CREDITED: +{basePay} SC (TOTAL: {state.PactScrip} SC)"
            };
        }

        // Case 4 (Day 1+): IsValid == false + Detain + ProtocolReviewVerified == false -> Brengop Clerical Warning
        if (!isValid && decision == PlayerDecision.Detain && protocolReviewRequired && !ship.ProtocolReviewVerified)
        {
            worldSim.ModifyReputation(state, prefix, -2);
            worldSim.ModifyGalacticPower(state, prefix, -1);

            int fine = ApplyClericalMistake(state);
            RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string penaltyLine = fine == 0
                ? $"CLERICAL WARNING {state.ClericalWarningsThisShift} OF {Constants.FreeClericalWarningsPerShift} (0 SC FINE)"
                : $"BRENGOP ADMINISTRATIVE FINE: -{fine} SC (WARNINGS EXHAUSTED)";

            return new DecisionEvaluationResult
            {
                Decision = decision,
                RegistrationCode = reg,
                ShipName = ship.Slate.ShipName,
                IsAccurate = false,
                IsCitation = true,
                IsSecurityStrike = false,
                ScripDelta = -fine,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = "BRENGOP MERCANTILE OPS // CLERICAL WARNING",
                ViolationReason = UnverifiedDetentionReason,
                PenaltyText = penaltyLine
            };
        }

        // Case 2: IsValid == true + Detain -> Mistake (+1 Security Strike on Day 3+ if protected Pact/Diplomatic hull)
        if (isValid && decision == PlayerDecision.Detain)
        {
            worldSim.ModifyReputation(state, prefix, -4);
            worldSim.ModifyGalacticPower(state, prefix, -1);

            bool protectedHull = prefix.Equals("KDF-", StringComparison.OrdinalIgnoreCase) ||
                                 prefix.Equals("BMC-", StringComparison.OrdinalIgnoreCase) ||
                                 ship.Slate.DeclaredShipClass == ShipClass.Diplomatic;
            if (protectedHull)
            {
                state.ProtectedHullDetainedEver = true;
            }
            bool addStrike = securityStrikesActive && protectedHull;
            if (addStrike)
            {
                AddSecurityStrike(state, 1, config);
            }

            int fine = ApplyClericalMistake(state);
            RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string fineLine = fine == 0
                ? $"WARNING {state.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift} (0 SC)"
                : $"FINE: -{fine} SC";
            string penaltyLine = addStrike
                ? $"KORDANI SECURITY STRIKE +1 ({state.SecurityStrikes}/{strikeCap}) | {fineLine}"
                : (fine == 0
                    ? $"CLERICAL WARNING {state.ClericalWarningsThisShift} OF {Constants.FreeClericalWarningsPerShift} (0 SC FINE)"
                    : $"BRENGOP ADMINISTRATIVE FINE: -{fine} SC (WARNINGS EXHAUSTED)");

            return new DecisionEvaluationResult
            {
                Decision = decision,
                RegistrationCode = reg,
                ShipName = ship.Slate.ShipName,
                IsAccurate = false,
                IsCitation = true,
                IsSecurityStrike = addStrike,
                ScripDelta = -fine,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = addStrike
                    ? "KORDANI SECURITY COMMAND // SECURITY STRIKE ISSUED"
                    : "BRENGOP MERCANTILE OPS // PROTOCOL CITATION",
                ViolationReason = "UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT",
                PenaltyText = penaltyLine
            };
        }

        // Case 3: IsValid == false + Grant Access (OR cleared valid vessel with shields/weapons still active or missing manifest)
        {
            worldSim.ModifyReputation(state, prefix, isValid ? +1 : +3);
            worldSim.ModifyGalacticPower(state, prefix, +1);

            bool majorSecurityThreat =
                truth.ActualWeaponsHot ||
                (state.Day >= cargoUnlockDay && truth.ActualCargoPowerDraw) ||
                truth.ActiveInfractions.Any(inf => inf.Type is ShipInfractionType.BannedCargoItem or ShipInfractionType.WithheldIllicitCargoManifest) ||
                (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform) &&
                 WarpSignatureGenerator.IsRestrictedOrigin(truth.ActualOriginSystem));

            bool addStrike = securityStrikesActive && majorSecurityThreat;
            if (addStrike)
            {
                AddSecurityStrike(state, 1, config);
            }

            int fine = ApplyClericalMistake(state);
            RecordShipEncounterOnce(state, ship, decision, tookBribe: false);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string reason;
            if (truth.ActiveInfractions.Count > 0)
            {
                reason = truth.ActiveInfractions[0].CitationReason;
            }
            else if (missingRequiredCargoWafer)
            {
                reason = "MISSING CARGO MANIFEST CERT-WAFER NOT VERIFIED";
            }
            else if (truth.ActualShieldsUp && truth.ActualWeaponsHot)
            {
                reason = "ACTIVE SHIELDS AND ARMED WEAPONS NOT LOWERED";
            }
            else if (truth.ActualShieldsUp)
            {
                reason = "ACTIVE SHIELDS NOT LOWERED BEFORE CLEARANCE";
            }
            else
            {
                reason = "ARMED WEAPONS NOT POWERED DOWN BEFORE CLEARANCE";
            }

            string fineLine = fine == 0
                ? $"WARNING {state.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift} (0 SC)"
                : $"FINE: -{fine} SC";
            string penaltyLine = addStrike
                ? $"KORDANI SECURITY STRIKE +1 ({state.SecurityStrikes}/{strikeCap}) | {fineLine}"
                : (fine == 0
                    ? $"CLERICAL WARNING {state.ClericalWarningsThisShift} OF {Constants.FreeClericalWarningsPerShift} (0 SC FINE)"
                    : $"BRENGOP ADMINISTRATIVE FINE: -{fine} SC (WARNINGS EXHAUSTED)");

            return new DecisionEvaluationResult
            {
                Decision = decision,
                RegistrationCode = reg,
                ShipName = ship.Slate.ShipName,
                IsAccurate = false,
                IsCitation = true,
                IsSecurityStrike = addStrike,
                ScripDelta = -fine,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = addStrike
                    ? "KORDANI SECURITY COMMAND // SECURITY STRIKE ISSUED"
                    : "BRENGOP MERCANTILE OPS // PROTOCOL CITATION",
                ViolationReason = $"CLEARED INVALID VESSEL -- {reason}",
                PenaltyText = penaltyLine
            };
        }
    }

    public int ApplyClericalMistake(GameState state)
    {
        state.ClericalWarningsThisShift++;

        if (state.ClericalWarningsThisShift <= Constants.FreeClericalWarningsPerShift)
        {
            return 0;
        }

        state.PactScrip = Math.Max(0, state.PactScrip - Constants.ClericalErrorFineScrip);
        state.ShiftClericalFinesPaid += Constants.ClericalErrorFineScrip;
        return Constants.ClericalErrorFineScrip;
    }

    public void AddSecurityStrike(GameState state, int count = 1, Core.ConfigFlags? config = null)
    {
        int threshold = Math.Max(1, config?.MaxStrikesBeforeAudit ?? Constants.SecurityStrikesAuditThreshold);
        state.SecurityStrikes += count;

        if (state.SecurityStrikes >= threshold)
        {
            state.PendingThreeStrikeAudit = true;
        }
    }

    private static void RecordShipEncounterOnce(
        GameState state,
        ShipSpec ship,
        PlayerDecision decision,
        bool tookBribe)
    {
        if (ship.EncounterHistoryRecorded)
            return;

        ship.EncounterHistoryRecorded = true;

        string faction = EncounterHistoryQuery.NormalizeFactionPrefix(ship.Slate.DeclaredPrefix);
        string species = ship.Slate.DeclaredCaptainSpecies == PortraitSpecies.Orun
            ? "Oruun"
            : ship.Slate.DeclaredCaptainSpecies.ToString();

        bool hadViolation = false;
        string? violationType = null;
        if (ship.GroundTruth.ActiveInfractions.Count > 0)
        {
            hadViolation = true;
            ShipInfractionType rawType = ship.GroundTruth.ActiveInfractions[0].Type;
            violationType = rawType switch
            {
                ShipInfractionType.TransponderIdMismatch => "TransponderMismatch",
                ShipInfractionType.RefusedShieldsOrWeaponsCompliance => "RefusedCompliance",
                _ => rawType.ToString()
            };
        }
        else if (decision == PlayerDecision.GrantAccess &&
                 (ship.GroundTruth.ActualShieldsUp ||
                  (state.Day >= Constants.DayUnlockSpsAndCombat && ship.GroundTruth.ActualWeaponsHot)))
        {
            hadViolation = true;
            violationType = ship.GroundTruth.ActualShieldsUp ? "ActiveShields" : "ArmedWeapons";
        }

        string verdict = decision == PlayerDecision.GrantAccess ? "Cleared" : "Detained";
        string? storyTag = string.IsNullOrWhiteSpace(ship.StoryTag) ? null : ship.StoryTag;

        state.AppendEncounterRecord(new EncounterRecord
        {
            Day = state.Day,
            Faction = faction,
            Species = species,
            CaptainName = ship.Slate.DeclaredCaptainName ?? string.Empty,
            ShipReg = ship.Slate.RegistrationCode ?? string.Empty,
            HadViolation = hadViolation,
            ViolationType = violationType,
            Verdict = verdict,
            TookBribe = tookBribe,
            StoryTag = storyTag
        });
    }
}
