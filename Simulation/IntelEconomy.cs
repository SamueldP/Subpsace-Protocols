using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

namespace SubspaceProtocols.Simulation;

public sealed class MissionDispatchMatch
{
    public CovertMissionSpec Mission { get; init; } = new();
    public ContrabandPocketItem? MatchingSlate { get; init; }
    public bool SlateIsInVentStash { get; init; }
    public bool CanTransmit => MatchingSlate != null;
}

public sealed class MissionTransmitResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string MissionId { get; init; } = string.Empty;
    public int ScripEarned { get; init; }
    public int BuyerRepDelta { get; init; }
    public int TargetPowerDelta { get; init; }
    public int DoubleAgentTrustDelta { get; init; }
}

/// <summary>
/// Missions-Only Espionage dispatch engine inside CRYPTO / LOGS.
/// Strictly supports Intercept &amp; Transmit contracts (Type 2) with direct contact transmission
/// writing an [AUDIT_FLAG] hex trace. Generic 5-buyer prefix farming and generic Kordani strike-clear
/// intel trades are intentionally cut (Option A: DECK 4 50 SC Audit Deferral Fee is the sole regular strike clearer).
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class IntelEconomy
{
    /// <summary>
    /// Lists all active Intercept &amp; Transmit contracts and matches each against any captured
    /// Intercept Data Slate currently held in the Officer's Coat Pocket or Under-Desk Vent Stash.
    /// </summary>
    public List<MissionDispatchMatch> GetActiveInterceptDispatches(
        GameState state,
        CovertMissionDatabase? db = null)
    {
        CovertMissionDatabase effectiveDb = db ?? CovertMissionDatabase.Load();
        List<MissionDispatchMatch> matches = [];

        foreach (string activeId in state.ActiveInterceptMissionIds)
        {
            if (state.CompletedInterceptMissionIds.Contains(activeId, StringComparer.OrdinalIgnoreCase))
                continue;

            CovertMissionSpec? spec = effectiveDb.Missions.FirstOrDefault(
                m => m.IsInterceptMission && m.MissionId.Equals(activeId, StringComparison.OrdinalIgnoreCase));
            if (spec == null)
                continue;

            ContrabandPocketItem? matched = null;
            bool inVent = false;

            foreach (ContrabandPocketItem p in state.PocketItems)
            {
                if (spec.MatchesInterceptSlate(p))
                {
                    matched = p;
                    inVent = false;
                    break;
                }
            }

            if (matched == null)
            {
                foreach (ContrabandPocketItem v in state.VentStashItems)
                {
                    if (spec.MatchesInterceptSlate(v))
                    {
                        matched = v;
                        inVent = true;
                        break;
                    }
                }
            }

            matches.Add(new MissionDispatchMatch
            {
                Mission = spec,
                MatchingSlate = matched,
                SlateIsInVentStash = inVent
            });
        }

        return matches;
    }

    /// <summary>
    /// Counts captured Intercept Data Slates in Pocket/Vent that do not match any active Intercept contract.
    /// </summary>
    public int CountUnmatchedCapturedSlates(GameState state, CovertMissionDatabase? db = null)
    {
        List<MissionDispatchMatch> dispatches = GetActiveInterceptDispatches(state, db);
        HashSet<string> matchedIds = new(
            dispatches.Where(d => d.MatchingSlate != null).Select(d => d.MatchingSlate!.ItemId),
            StringComparer.OrdinalIgnoreCase);

        int unmatched = 0;
        foreach (ContrabandPocketItem p in state.PocketItems)
        {
            if (!p.IsBurnt && p.ItemType == ContrabandPocketItem.TypeCopiedDataSlate && !matchedIds.Contains(p.ItemId))
                unmatched++;
        }
        foreach (ContrabandPocketItem v in state.VentStashItems)
        {
            if (!v.IsBurnt && v.ItemType == ContrabandPocketItem.TypeCopiedDataSlate && !matchedIds.Contains(v.ItemId))
                unmatched++;
        }
        return unmatched;
    }

    /// <summary>
    /// Directly transmits a captured Intercept Data Slate to the requesting mission contact in CRYPTO / LOGS.
    /// Always logs an [AUDIT_FLAG] entry so the player must scrub logs before a Kordani Berth Audit.
    /// </summary>
    public MissionTransmitResult ExecuteMissionInterceptTransmit(
        GameState state,
        WorldSim worldSim,
        string missionId,
        ContrabandPocketItem slate,
        bool slateIsInVentStash,
        CovertMissionDatabase? db = null)
    {
        if (string.IsNullOrWhiteSpace(missionId))
        {
            return new MissionTransmitResult { Success = false, Message = "INVALID MISSION ID." };
        }

        if (!state.ActiveInterceptMissionIds.Contains(missionId, StringComparer.OrdinalIgnoreCase))
        {
            return new MissionTransmitResult { Success = false, Message = "CONTRACT IS NOT CURRENTLY ACTIVE." };
        }

        if (state.CompletedInterceptMissionIds.Contains(missionId, StringComparer.OrdinalIgnoreCase))
        {
            return new MissionTransmitResult { Success = false, Message = "CONTRACT ALREADY SETTLED." };
        }

        CovertMissionDatabase effectiveDb = db ?? CovertMissionDatabase.Load();
        CovertMissionSpec? spec = effectiveDb.Missions.FirstOrDefault(
            m => m.IsInterceptMission && m.MissionId.Equals(missionId, StringComparison.OrdinalIgnoreCase));

        if (spec == null)
        {
            return new MissionTransmitResult { Success = false, Message = "MISSION CONTRACT RECORD NOT FOUND." };
        }

        if (!spec.MatchesInterceptSlate(slate))
        {
            return new MissionTransmitResult
            {
                Success = false,
                Message = $"SLATE DOES NOT MATCH CONTRACT TARGET ({spec.TargetPrefix})."
            };
        }

        List<ContrabandPocketItem> sourceList = slateIsInVentStash ? state.VentStashItems : state.PocketItems;
        int idx = sourceList.FindIndex(x => x.ItemId.Equals(slate.ItemId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return new MissionTransmitResult { Success = false, Message = "CAPTURED SLATE NO LONGER IN STORAGE." };
        }

        // Consume captured Intercept slate
        sourceList.RemoveAt(idx);

        // Complete contract
        state.ActiveInterceptMissionIds.RemoveAll(id => id.Equals(spec.MissionId, StringComparison.OrdinalIgnoreCase));
        if (!state.CompletedInterceptMissionIds.Contains(spec.MissionId, StringComparer.OrdinalIgnoreCase))
        {
            state.CompletedInterceptMissionIds.Add(spec.MissionId);
        }
        state.QuestFlags[$"{spec.MissionId}_completed"] = true;
        state.CovertTasksCompleted++;

        // Apply contract payout, faction rep, target power & optional DoubleAgentTrust
        int payout = Math.Max(0, spec.RewardScrip);
        state.PactScrip += payout;

        int repDelta = 0;
        if (!string.IsNullOrWhiteSpace(spec.RewardFactionPrefix) && spec.RewardRepDelta != 0)
        {
            worldSim.ModifyReputation(state, spec.RewardFactionPrefix, spec.RewardRepDelta);
            repDelta = spec.RewardRepDelta;
        }

        int powerDelta = spec.TargetPowerDelta;
        if (!string.IsNullOrWhiteSpace(slate.TargetShipPrefix) && powerDelta != 0)
        {
            worldSim.ModifyGalacticPower(state, slate.TargetShipPrefix, powerDelta);
        }

        int trustDelta = 0;
        if (spec.GrantDoubleAgentTrust)
        {
            state.DoubleAgentTrust++;
            trustDelta = 1;
        }

        // Sending mission intel ALWAYS writes an [AUDIT_FLAG] security hex log
        state.AddSecurityLog(
            "[AUDIT_FLAG]",
            $"MISSION TX -> {spec.AssigningFaction}: {slate.TargetShipRegistration}",
            isSuspicious: true);

        // Send read receipt to MESSAGES so player has an archival record without spamming Unread LED
        (int cy, int cm) = Constants.GetStationCycle(state.Day);
        state.AddEmail(new TerminalEmail
        {
            EmailId = $"mission_tx_{spec.MissionId}_{state.NextLogSequence}",
            DayReceived = state.Day,
            PhaseReceived = state.QuartersPhase.ToString().ToUpperInvariant(),
            CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // {(state.QuartersPhase == QuartersPhase.Morning ? "05:30" : "22:15")}",
            Sender = spec.AssigningFaction,
            Subject = $"CONTRACT SETTLED // {slate.TargetShipRegistration}",
            Body = $"INTERCEPT PACKET VERIFIED FOR {slate.TargetShipRegistration} ({slate.TargetShipPrefix}).\n\n" +
                   $"CONTRACT: {spec.Title}\n" +
                   $"SETTLEMENT: +{payout} SC CREDITED TO BERTH ACCOUNT.\n\n" +
                   $"WARNING: TRANSMISSION WROTE [AUDIT_FLAG]. RUN [SCRUB LOGS ({Constants.ScrubSecurityLogsCostScrip} SC)] BEFORE KORDANI CHECKS. [TODO: USER DIALOGUE PASS]",
            IsRead = true
        });

        state.SaveToDisk();
        AudioHooks.Play("terminal_key_confirm");

        return new MissionTransmitResult
        {
            Success = true,
            MissionId = spec.MissionId,
            Message = $"TRANSMITTED TO {spec.AssigningFaction} -- +{payout} SC (LOGGED [AUDIT_FLAG]).",
            ScripEarned = payout,
            BuyerRepDelta = repDelta,
            TargetPowerDelta = powerDelta,
            DoubleAgentTrustDelta = trustDelta
        };
    }
}
