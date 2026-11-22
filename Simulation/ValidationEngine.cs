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

/// <summary>
/// Evaluates Grant Access vs. Detain decisions against ShipSpec.IsValid and ShipSpec.ProtocolReviewVerified:
/// - Days 1-2: Simple Yes/No (Protocol Review not yet unlocked; Security Strikes inactive; default 10 SC base pay)
/// - Day 3+:   Strict 5-case truth table requiring Protocol Review verification before detaining + Security Strikes active
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ValidationEngine
{
    public DecisionEvaluationResult EvaluateDecision(
        GameState state,
        ShipSpec ship,
        PlayerDecision decision,
        WorldSim worldSim)
    {
        ShipGroundTruth truth = ship.GroundTruth;
        string prefix = ship.Slate.DeclaredPrefix;
        string reg = ship.Slate.RegistrationCode;
        int basePay = state.BasePayPerShipScrip > 0 ? state.BasePayPerShipScrip : Constants.BasePayPerShipScrip;
        bool protocolReviewRequired = state.Day >= 3;
        bool securityStrikesActive = state.Day >= 3;

        // =====================================================================
        // SPECIAL BRANCH: Day 2 Hardcoded Suspect ("The Ghost Route")
        // =====================================================================
        if (truth.IsDay2GhostRouteSuspect)
        {
            state.Day2SuspectEncountered = true;
            state.Day2SuspectRegistration = reg;
            state.Day2SuspectDeclaredOrigin = ship.Slate.OriginSystem;
            state.Day2SuspectActualOrigin = truth.ActualOriginSystem;
            state.Day2SuspectPrefix = prefix;
            state.Day2SuspectShipName = ship.Slate.ShipName;
            state.Day2SuspectDetained = (decision == PlayerDecision.Detain);
            state.QuestFlags["Day2_GhostRouteEncountered"] = true;

            if (decision == PlayerDecision.GrantAccess)
            {
                state.ShipsProcessedToday++;
                state.PactScrip += basePay;
                worldSim.ModifyReputation(state, prefix, +1);
                worldSim.ModifyGalacticPower(state, prefix, +1);
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
                    ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) CLEARED UNDER DAY 02 PROTOCOLS.",
                    PenaltyText = $"CREDITED: +{basePay} SC (TOTAL: {state.PactScrip} SC)"
                };
            }
            else
            {
                // Detaining Day 2 Ghost Route suspect:
                // - Standard immediate clerical citation, no base pay payout
                // - Invalid-detain reputation change (-2) rather than -4
                // - Zero Security Strikes on Days 1-2
                worldSim.ModifyReputation(state, prefix, -2);
                worldSim.ModifyGalacticPower(state, prefix, -1);

                int fine = ApplyClericalMistake(state);
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
                    ViolationReason = $"UNVERIFIED DETAINMENT ({reg}) -- NO ACTIVE DAY 02 VIOLATION",
                    PenaltyText = penaltyLine
                };
            }
        }

        // =====================================================================
        // VALIDATION TRUTH TABLE (Day 1-2 Simple Yes/No | Day 3+ Protocol Review & Strikes)
        // =====================================================================
        bool isValid = ship.IsValid;
        bool unaddressedCombatSystems = state.Day >= 2 && (truth.ActualShieldsUp || truth.ActualWeaponsHot);

        // Case 1: IsValid == true + Grant Access (with shields down & weapons offline on Day 2+) -> Accurate
        if (isValid && decision == PlayerDecision.GrantAccess && !unaddressedCombatSystems)
        {
            state.ShipsProcessedToday++;
            state.PactScrip += basePay;
            worldSim.ModifyReputation(state, prefix, +1);
            worldSim.ModifyGalacticPower(state, prefix, +1);
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
            state.PactScrip += basePay;
            worldSim.ModifyReputation(state, prefix, -2);
            worldSim.ModifyGalacticPower(state, prefix, -1);
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

        // Case 4 (Day 3+ only): IsValid == false + Detain + ProtocolReviewVerified == false -> Mistake
        if (!isValid && decision == PlayerDecision.Detain && protocolReviewRequired && !ship.ProtocolReviewVerified)
        {
            worldSim.ModifyReputation(state, prefix, -2);
            worldSim.ModifyGalacticPower(state, prefix, -1);

            int fine = ApplyClericalMistake(state);
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
                ViolationReason = "UNVERIFIED DETAINMENT -- PROTOCOL REVIEW NOT COMPLETED",
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
            bool addStrike = securityStrikesActive && protectedHull;
            if (addStrike)
            {
                AddSecurityStrike(state, 1);
            }

            int fine = ApplyClericalMistake(state);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string fineLine = fine == 0
                ? $"WARNING {state.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift} (0 SC)"
                : $"FINE: -{fine} SC";
            string penaltyLine = addStrike
                ? $"KORDANI SECURITY STRIKE +1 ({state.SecurityStrikes}/{state.MaxSecurityStrikesThreshold}) | {fineLine}"
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

        // Case 3: IsValid == false + Grant Access (OR cleared valid vessel with shields/weapons still active on Day 2+)
        {
            worldSim.ModifyReputation(state, prefix, isValid ? +1 : +3);
            worldSim.ModifyGalacticPower(state, prefix, +1);

            bool majorSecurityThreat =
                truth.ActualWeaponsHot ||
                truth.ActualCargoPowerDraw ||
                (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform) &&
                 WarpSignatureGenerator.IsRestrictedOrigin(truth.ActualOriginSystem));

            bool addStrike = securityStrikesActive && majorSecurityThreat;
            if (addStrike)
            {
                AddSecurityStrike(state, 1);
            }

            int fine = ApplyClericalMistake(state);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string reason;
            if (truth.ActiveInfractions.Count > 0)
            {
                reason = truth.ActiveInfractions[0].CitationReason;
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
                ? $"KORDANI SECURITY STRIKE +1 ({state.SecurityStrikes}/{state.MaxSecurityStrikesThreshold}) | {fineLine}"
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
        return Constants.ClericalErrorFineScrip;
    }

    public void AddSecurityStrike(GameState state, int count = 1)
    {
        state.SecurityStrikes += count;

        if (state.SecurityStrikes >= Constants.SecurityStrikesAuditThreshold)
        {
            state.PendingThreeStrikeAudit = true;
        }

        if (state.SecurityStrikes >= state.MaxSecurityStrikesThreshold)
        {
            state.EndingFlags["Fail_SecurityDetainment"] = true;
        }
    }
}
