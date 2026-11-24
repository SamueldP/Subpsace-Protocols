using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Resolves Sleep-triggered Kordani Berth Audits (3-Strike Audit &amp; Stellar Stoker Sabotage Audit)
/// using the logical Two-Tier Raid structure:
/// 1. Coat Frisk (Always Runs): Any illicit/stolen item left in Coat Pocket is confiscated and triggers
///    the contraband smuggling penalty -- EXCEPT up to 1 item concealed by Shielded Pocket Lining.
/// 2. Terminal Log Check -> Vent Sweep:
///    - Zero unscrubbed [UNAUTH_IO] / [AUDIT_FLAG] entries: Standard sweep only; Under-Desk Vent Stash
///      is NOT discovered; resolves as Clean Audit (Clean-Audit Fine + Kordani rep drop if coat was clean).
///    - Unscrubbed [UNAUTH_IO] / [AUDIT_FLAG] entries present: Kordani flags unauthorized traffic and
///      orders a Deep Structural Sweep, discovering and confiscating everything in the Under-Desk Vent
///      Stash and issuing the severe contraband penalty.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class RaidController
{
    public BerthAuditKind CheckPendingSleepAudit(GameState state, Core.ConfigFlags config)
    {
        if (state.Day >= Constants.DayUnlockWarpAndStrikes && state.SecurityStrikes >= config.MaxStrikesBeforeAudit)
        {
            return BerthAuditKind.ThreeStrikeBerthAudit;
        }

        if (state.PendingStokerBerthSearch)
        {
            return BerthAuditKind.StokerSabotageAudit;
        }

        return BerthAuditKind.None;
    }

    public BerthAuditResolution ResolveBerthAudit(
        GameState state,
        Core.ConfigFlags config,
        WorldSim worldSim,
        BerthAuditKind kind)
    {
        List<string> lines = [];
        bool isThreeStrike = kind == BerthAuditKind.ThreeStrikeBerthAudit;

        lines.Add(isThreeStrike
            ? $"KORDANI SECURITY RAID // 3-STRIKE BERTH WARRANT (STRIKES: {state.SecurityStrikes}/{config.MaxStrikesBeforeAudit})"
            : "KORDANI SABOTAGE RAID // STELLAR STOKER OVERLOAD WARRANT (+0 STRIKES)");

        // =====================================================================
        // TIER 1: COAT FRISK (ALWAYS RUNS)
        // Confiscates any unshielded contraband left in the Coat Pocket;
        // Shielded Pocket Lining conceals up to 1 contraband item.
        // =====================================================================
        List<ContrabandPocketItem> pocketContrabandItems = state.PocketItems.Where(i => i.IsContraband).ToList();
        int pocketContrabandTotal = pocketContrabandItems.Count;
        int concealedByLining = state.HasShieldedPocketLining ? Math.Min(1, pocketContrabandTotal) : 0;
        int exposedPocketCount = Math.Max(0, pocketContrabandTotal - concealedByLining);

        int pocketConfiscated = 0;
        if (exposedPocketCount > 0)
        {
            int skippedForShield = 0;
            List<ContrabandPocketItem> remaining = [];
            foreach (ContrabandPocketItem item in state.PocketItems)
            {
                if (!item.IsContraband)
                {
                    remaining.Add(item);
                    continue;
                }

                if (skippedForShield < concealedByLining)
                {
                    skippedForShield++;
                    remaining.Add(item);
                }
                else
                {
                    pocketConfiscated++;
                }
            }
            state.PocketItems = remaining;
        }

        if (pocketContrabandTotal == 0)
        {
            lines.Add("[TIER 1 // COAT FRISK] CLEAR -- NO ILLICIT ITEMS IN OFFICER COAT POCKET.");
        }
        else if (pocketConfiscated == 0)
        {
            lines.Add($"[TIER 1 // COAT FRISK] SHIELDED LINING CONCEALED {concealedByLining} ITEM -- FRISK CLEAR.");
        }
        else if (concealedByLining > 0)
        {
            lines.Add($"[TIER 1 // COAT FRISK] VIOLATION: {pocketConfiscated} UNSHIELDED CONTRABAND ITEM(S) CONFISCATED (1 SHIELDED).");
        }
        else
        {
            lines.Add($"[TIER 1 // COAT FRISK] VIOLATION: {pocketConfiscated} ILLICIT ITEM(S) CONFISCATED FROM COAT POCKET!");
        }

        // =====================================================================
        // TIER 2: TERMINAL LOG CHECK -> STANDARD SWEEP vs DEEP STRUCTURAL SWEEP
        // =====================================================================
        int unscrubbedLogs = Math.Max(state.UnscrubbedLogCount, state.UnauthorizedLogCount);
        bool deepStructuralSweep = unscrubbedLogs > 0;
        int ventConfiscated = 0;

        if (!deepStructuralSweep)
        {
            lines.Add("[TIER 2 // TERMINAL LOGS] CLEAN -- ZERO [UNAUTH_IO] / [AUDIT_FLAG] ENTRIES.");
            lines.Add("[TIER 2 // BERTH SWEEP] STANDARD SWEEP ONLY -- UNDER-DESK VENT STASH UNDISCOVERED.");
        }
        else
        {
            lines.Add($"[TIER 2 // TERMINAL LOGS] VIOLATION: {unscrubbedLogs} UNSCRUBBED [UNAUTH_IO]/[AUDIT_FLAG] ENTRIES!");

            // Dirty logs trigger a Deep Structural Sweep: discover & confiscate EVERYTHING in Under-Desk Vent Stash!
            ventConfiscated = state.VentStashItems.Count;
            if (ventConfiscated > 0)
            {
                state.VentStashItems.Clear();
                lines.Add($"[TIER 2 // DEEP SWEEP] UNDER-DESK VENT BREACHED -- {ventConfiscated} STASHED ITEM(S) CONFISCATED!");
            }
            else
            {
                lines.Add("[TIER 2 // DEEP SWEEP] UNDER-DESK VENT SEARCHED (EMPTY) -- UNAUTHORIZED TRAFFIC FLAGGED!");
            }
        }

        // =====================================================================
        // FINAL RAID VERDICT
        // =====================================================================
        bool caught = pocketConfiscated > 0 || deepStructuralSweep;
        string caughtReason;
        if (pocketConfiscated > 0 && ventConfiscated > 0)
        {
            caughtReason = "CONTRABAND CONFISCATED FROM COAT POCKET & UNDER-DESK VENT (DEEP SWEEP)";
        }
        else if (deepStructuralSweep && ventConfiscated > 0)
        {
            caughtReason = "UNSCRUBBED LOGS TRIGGERED DEEP SWEEP -- VENT STASH CONFISCATED";
        }
        else if (deepStructuralSweep)
        {
            caughtReason = "UNSCRUBBED [UNAUTH_IO] / [AUDIT_FLAG] TERMINAL TRAFFIC FLAGGED";
        }
        else if (pocketConfiscated > 0)
        {
            caughtReason = "UNSHIELDED CONTRABAND CONFISCATED DURING COAT FRISK";
        }
        else
        {
            caughtReason = string.Empty;
        }

        int cleanAuditFine = 0;
        int repDelta = 0;
        bool strikesReset = false;

        state.BerthSearchAuditOccurred = true;
        if (state.PendingStokerBerthSearch || kind == BerthAuditKind.StokerSabotageAudit)
        {
            state.KordaniSabotageAuditOccurred = true;
        }

        if (caught)
        {
            state.EndingFlags["Fail_SecurityDetainment"] = true;
            lines.Add($"[VERDICT] SEVERE CONTRABAND PENALTY -- {caughtReason}.");
        }
        else
        {
            state.PendingStokerBerthSearch = false;
            state.PendingThreeStrikeAudit = false;

            if (isThreeStrike)
            {
                state.SecurityStrikes = 0;
                strikesReset = true;
                cleanAuditFine = Math.Max(0, config.CleanAuditFine);
                state.PactScrip = Math.Max(0, state.PactScrip - cleanAuditFine);
                state.LastCleanAuditFineApplied = cleanAuditFine;
                repDelta = config.CleanAuditKordaniRepDrop;
                worldSim.ModifyReputation(state, "KDF-", repDelta);

                lines.Add("[VERDICT] CLEAN AUDIT -- STRIKES RESET TO 0.");
                lines.Add($"[ADMIN FEE] CLEAN-AUDIT FINE: -{cleanAuditFine} SC  |  KDF REP {repDelta}");
            }
            else
            {
                lines.Add("[VERDICT] SABOTAGE AUDIT CLEAN -- NO EVIDENCE OF ORBITAL TAMPERING (+0 STRIKES).");
            }
        }

        state.SaveToDisk();

        return new BerthAuditResolution
        {
            Kind = kind,
            WasCaught = caught,
            CaughtReason = caughtReason,
            DeepStructuralSweepTriggered = deepStructuralSweep,
            UnscrubbedLogsFound = unscrubbedLogs,
            PocketContrabandTotal = pocketContrabandTotal,
            PocketContrabandConcealedByLining = concealedByLining,
            PocketContrabandConfiscated = pocketConfiscated,
            VentContrabandExposed = ventConfiscated,
            VentContrabandConfiscated = ventConfiscated,
            CleanAuditFineCharged = cleanAuditFine,
            KordaniRepDelta = repDelta,
            StrikesResetToZero = strikesReset,
            InspectionLogLines = lines
        };
    }
}
