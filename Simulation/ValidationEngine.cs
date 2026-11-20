using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

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
/// Evaluates Grant Access vs. Detain decisions against ShipSpec.IsValid and ShipSpec.ProtocolReviewVerified
/// using the strict 5-case truth table:
///   1. IsValid == true  + GrantAccess                          -> Accurate (+15 SC, no citation)
///   2. IsValid == true  + Detain                               -> Mistake ("UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT", warning/fine or Kordani Strike if Expected)
///   3. IsValid == false + GrantAccess                          -> Mistake (exact missed violation, warning/fine)
///   4. IsValid == false + Detain + ProtocolReviewVerified==false -> Mistake ("UNVERIFIED DETAINMENT -- PROTOCOL REVIEW NOT COMPLETED", warning/fine, no +15 SC)
///   5. IsValid == false + Detain + ProtocolReviewVerified==true  -> Accurate (+15 SC, no citation)
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
                state.PactScrip += Constants.BasePayPerShipScrip;
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
                    ScripDelta = Constants.BasePayPerShipScrip,
                    WarningNumber = state.ClericalWarningsThisShift,
                    TotalSecurityStrikes = state.SecurityStrikes,
                    HeaderText = "PACT PORT AUTHORITY // DOCKING CLEARANCE VERIFIED",
                    ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) CLEARED UNDER DAY 02 PROTOCOLS.",
                    PenaltyText = $"CREDITED: +{Constants.BasePayPerShipScrip} SC (TOTAL: {state.PactScrip} SC)"
                };
            }
            else
            {
                // Detaining Day 2 Ghost Route suspect:
                // - Standard immediate clerical citation, no +15 SC payout
                // - Invalid-detain reputation change (-2) rather than -4
                // - Zero Expected-Ship Security Strike (excluded from Expected Ship List)
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
                    ViolationReason = $"UNVERIFIED DETAINMENT ({reg}) -- SPECTROMETER OFFLINE, NO HARD PROOF",
                    PenaltyText = penaltyLine
                };
            }
        }

        // =====================================================================
        // STRICT 5-CASE VALIDATION TRUTH TABLE
        // =====================================================================
        bool isValid = ship.IsValid;

        // Case 1: IsValid == true + Grant Access -> Accurate (+15 SC, zero citation popup)
        if (isValid && decision == PlayerDecision.GrantAccess)
        {
            state.ShipsProcessedToday++;
            state.PactScrip += Constants.BasePayPerShipScrip;
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
                ScripDelta = Constants.BasePayPerShipScrip,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = "PACT PORT AUTHORITY // DOCKING CLEARANCE VERIFIED",
                ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) PROCESSED ACCURATELY.",
                PenaltyText = $"CREDITED: +{Constants.BasePayPerShipScrip} SC (TOTAL: {state.PactScrip} SC)"
            };
        }

        // Case 5: IsValid == false + Detain + ProtocolReviewVerified == true -> Accurate (+15 SC, zero citation popup)
        if (!isValid && decision == PlayerDecision.Detain && ship.ProtocolReviewVerified)
        {
            state.ShipsProcessedToday++;
            state.PactScrip += Constants.BasePayPerShipScrip;
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
                ScripDelta = Constants.BasePayPerShipScrip,
                WarningNumber = state.ClericalWarningsThisShift,
                TotalSecurityStrikes = state.SecurityStrikes,
                HeaderText = "PACT PORT AUTHORITY // VIOLATION DETAINMENT CONFIRMED",
                ViolationReason = $"VESSEL {reg} ({ship.Slate.ShipName}) DETAINED WITH VERIFIED PROTOCOL REVIEW.",
                PenaltyText = $"CREDITED: +{Constants.BasePayPerShipScrip} SC (TOTAL: {state.PactScrip} SC)"
            };
        }

        // Case 4: IsValid == false + Detain + ProtocolReviewVerified == false -> Mistake (UNVERIFIED DETAINMENT, no +15 SC)
        if (!isValid && decision == PlayerDecision.Detain && !ship.ProtocolReviewVerified)
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

        // Case 2: IsValid == true + Detain -> Mistake (UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT)
        if (isValid && decision == PlayerDecision.Detain)
        {
            worldSim.ModifyReputation(state, prefix, -4);
            worldSim.ModifyGalacticPower(state, prefix, -1);

            if (truth.IsExpectedShip)
            {
                AddSecurityStrike(state, 1);
                state.SaveToDisk();
                AudioHooks.Play("citation_buzz");

                return new DecisionEvaluationResult
                {
                    Decision = decision,
                    RegistrationCode = reg,
                    ShipName = ship.Slate.ShipName,
                    IsAccurate = false,
                    IsCitation = true,
                    IsSecurityStrike = true,
                    ScripDelta = 0,
                    WarningNumber = state.ClericalWarningsThisShift,
                    TotalSecurityStrikes = state.SecurityStrikes,
                    HeaderText = "KORDANI SECURITY COMMAND // PRIORITY CITATION",
                    ViolationReason = $"UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT (EXPECTED SHIP '{reg}')",
                    PenaltyText = $"PENALTY: +1 KORDANI SECURITY STRIKE ({state.SecurityStrikes}/{state.MaxSecurityStrikesThreshold}) [NON-MONETARY]"
                };
            }

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
                ViolationReason = "UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT",
                PenaltyText = penaltyLine
            };
        }

        // Case 3: IsValid == false + Grant Access -> Mistake (exact missed violation)
        {
            worldSim.ModifyReputation(state, prefix, +3);
            if (truth.IsExpectedShip)
            {
                worldSim.ModifyReputation(state, "KDF-", +3);
            }
            worldSim.ModifyGalacticPower(state, prefix, +1);

            int fine = ApplyClericalMistake(state);
            state.SaveToDisk();
            AudioHooks.Play("citation_buzz");

            string reason = truth.ActiveInfractions.Count > 0
                ? truth.ActiveInfractions[0].CitationReason
                : "ACTIVE SHIELDS OR WEAPONS NOT ADDRESSED";

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
