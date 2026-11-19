using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Tracks hidden rep_score and power_score across High, Mid, and Rare Traffic Tiers
/// and schedules Suzerain-inspired 2-3 shift delayed cause-and-effect ripples.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class WorldSim
{
    private static readonly HashSet<string> HighTrafficPrefixes = new(StringComparer.OrdinalIgnoreCase)
        { "KDF-", "BMC-", "FTV-", "MNG-" };

    private static readonly HashSet<string> RareTrafficPrefixes = new(StringComparer.OrdinalIgnoreCase)
        { "GUV-", "HBR-" };

    public void ModifyReputation(GameState state, string prefix, int delta)
    {
        if (!state.RepScore.ContainsKey(prefix))
            state.RepScore[prefix] = 0;

        state.RepScore[prefix] += delta;
    }

    public void ModifyGalacticPower(GameState state, string prefix, int delta)
    {
        if (!state.PowerScore.ContainsKey(prefix))
            state.PowerScore[prefix] = 0;

        TrafficPowerState before = EvaluateTrafficPowerState(prefix, state.PowerScore[prefix]);
        state.PowerScore[prefix] += delta;
        TrafficPowerState after = EvaluateTrafficPowerState(prefix, state.PowerScore[prefix]);

        if (after != before && after != TrafficPowerState.Normal)
        {
            state.DelayedRippleQueue.Add(new DelayedRippleEvent
            {
                EventId = $"ripple_{prefix.TrimEnd('-')}_{after}_day{state.Day}",
                SourcePrefix = prefix,
                TriggeredState = after,
                TriggeredOnDay = state.Day,
                ActivateOnDay = state.Day + 2, // 2-3 shift lag
                DirectiveText = $"SECTOR TRAFFIC ALERT: {prefix} REGISTRY REACHED {after.ToString().ToUpperInvariant()} THRESHOLD.",
                AffectsDownstreamHullScorch = prefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase) &&
                                             after is TrafficPowerState.Dominant or TrafficPowerState.Crisis
            });
        }
    }

    public static TrafficPowerState EvaluateTrafficPowerState(string prefix, int powerScore)
    {
        if (HighTrafficPrefixes.Contains(prefix))
        {
            if (powerScore <= -4) return TrafficPowerState.Weakened;
            if (powerScore >= 14) return TrafficPowerState.Crisis;
            if (powerScore >= 8) return TrafficPowerState.Dominant;
            return TrafficPowerState.Normal;
        }

        if (RareTrafficPrefixes.Contains(prefix))
        {
            if (powerScore <= -2) return TrafficPowerState.Weakened;
            if (powerScore >= 5) return TrafficPowerState.Crisis;
            if (powerScore >= 3) return TrafficPowerState.Dominant;
            return TrafficPowerState.Normal;
        }

        // Mid Traffic: KRW-, FTC-, ZNI-, TLV-, ORN-, PRJ-
        if (powerScore <= -3) return TrafficPowerState.Weakened;
        if (powerScore >= 9) return TrafficPowerState.Crisis;
        if (powerScore >= 5) return TrafficPowerState.Dominant;
        return TrafficPowerState.Normal;
    }
}
