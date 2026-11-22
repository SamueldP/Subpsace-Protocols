using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

namespace SubspaceProtocols.Simulation;

public enum ComplianceOrderOutcome
{
    AlreadyCompliant = 0,
    Complied = 1,
    SpoofedDeflection = 2,
    RefusedOutright = 3
}

/// <summary>
/// Manages the 4-minute 30-second (270s) shift timer, Brown Dwarf orbital progress (0..1),
/// pre-generated 16-vessel daily ship queue seeded from (RunSeed, Day) plus unscripted random
/// overflow spawning if a fast player clears all 16 before the bell rings, unified compliance
/// resolution, hold-until-first-hail rule, pre-shift pause state, debug TimeScale, and shift-end horn trigger.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShiftController
{
    /// <summary>
    /// Unified deliberate base duration (seconds at 1.0x AnimationSpeedMultiplier) shared across:
    /// - Slate Bus incoming protocol download (RX PACKET)
    /// - Clearance Dock encryption upload
    /// - Slate Bus outgoing clearance transmission (TX CLEARANCE)
    /// - Detain clamp-lock + slate-wipe sequence
    /// </summary>
    public const float StandardTransferDurationSeconds = 1.80f;
    public const float DetainClampImpactFraction = 0.35f; // Clamps slam shut on hull
    public const float DetainSlateWipeFraction = 0.65f;   // Telemetry wipes cleanly after clamp lock
    public const float UnplugMidTransferDragThresholdPx = 8.0f;

    /// <summary>
    /// Evaluates whether a mid-transfer Data Slate unplug (1%-99% progress) should cleanly abort & reset to 0%.
    /// Applies to both Clearance Upload Dock encryption and Slate Bus TX CLEARANCE transmission.
    /// </summary>
    public static bool CanInterruptTransfer(float elapsedSeconds, float durationSeconds = StandardTransferDurationSeconds) =>
        durationSeconds > 0f && elapsedSeconds < durationSeconds;

    /// <summary>
    /// True whenever a vessel's weapon hardpoints are physically hot and must register on SPS,
    /// regardless of whether the captain honestly declared WPN: ARMED or spoofed WPN: OFFLINE.
    /// </summary>
    public static bool ShouldSpsDetectHotWeapons(ShipSpec? ship) =>
        ship != null &&
        !ship.GroundTruth.ActualShieldsUp &&
        ship.GroundTruth.ActualWeaponsHot;

    private bool _shiftHornPlayed;
    private readonly List<ShipSpec> _dailyQueue = [];
    private readonly HashSet<string> _usedQueueRegs = new(StringComparer.OrdinalIgnoreCase);
    private int _nextQueueIndex;

    public float ElapsedShiftSeconds { get; private set; }
    public bool HasShiftTimerStarted { get; private set; }
    public bool IsShiftTimerExpired => ElapsedShiftSeconds >= Constants.ShiftDurationSeconds;
    public bool HasActiveShipInBay { get; set; }
    public bool IsPreShiftSequenceActive { get; set; }
    public float TimeScale { get; set; } = 1.0f;

    public IReadOnlyList<ShipSpec> DailyQueue => _dailyQueue;
    public int NextQueueIndex => _nextQueueIndex;
    public bool IsInRandomOverflow => _nextQueueIndex > _dailyQueue.Count;

    public float BrownDwarfArcProgress =>
        Math.Clamp(ElapsedShiftSeconds / Constants.ShiftDurationSeconds, 0.0f, 1.0f);

    public bool CanTriggerNextHail => !IsShiftTimerExpired && !HasActiveShipInBay && !IsPreShiftSequenceActive;
    public bool CanEndShift => IsShiftTimerExpired && !HasActiveShipInBay && !IsPreShiftSequenceActive;

    public void StartNewShift(GameState state)
    {
        ElapsedShiftSeconds = 0.0f;
        HasShiftTimerStarted = false; // Hold timer at t = 0 until player clicks "Next Hail" for first ship
        HasActiveShipInBay = false;
        IsPreShiftSequenceActive = false;
        _shiftHornPlayed = false;
        _dailyQueue.Clear();
        _usedQueueRegs.Clear();
        _nextQueueIndex = 0;

        // Cleanly reset daily clerical warning counter (2 free grace warnings/shift) at the start of every shift
        state.ShipsProcessedToday = 0;
        state.ClericalWarningsThisShift = 0;
        state.BribeOfferedThisShift = false;
    }

    /// <summary>
    /// Pre-generates the full 16-vessel daily ship queue deterministically from (state.RunSeed, state.Day).
    /// </summary>
    public IReadOnlyList<ShipSpec> PrepareDailyQueue(
        GameState state,
        ShipGenerator generator,
        int queueSize = Constants.PreGeneratedQueueSize,
        uint reseedNonce = 0)
    {
        _dailyQueue.Clear();
        _usedQueueRegs.Clear();
        _nextQueueIndex = 0;
        List<ShipSpec> generated = generator.GenerateDailyQueue(state, queueSize, reseedNonce);
        _dailyQueue.AddRange(generated);
        foreach (ShipSpec ship in generated)
        {
            _usedQueueRegs.Add(ship.Slate.RegistrationCode);
        }
        return _dailyQueue;
    }

    /// <summary>
    /// Dequeues the next pre-generated ship (slots 0..15) for the shift.
    /// If a fast player clears all 16 pre-generated vessels before the shift timer expires,
    /// spawns unscripted random overflow ships until the shift bell rings.
    /// </summary>
    public ShipSpec TakeNextShip(
        GameState state,
        ShipGenerator generator,
        DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto,
        uint reseedNonce = 0)
    {
        int slotIndex = _nextQueueIndex++;

        if (forceValidity == DebugSpawnValidityMode.Auto &&
            reseedNonce == 0 &&
            slotIndex < _dailyQueue.Count)
        {
            return _dailyQueue[slotIndex];
        }

        // Unscripted random overflow beyond the 16 pre-generated ships
        uint effectiveNonce = reseedNonce != 0
            ? reseedNonce
            : (uint)Random.Shared.Next(1, int.MaxValue);

        return generator.GenerateShipForSlot(state, slotIndex, effectiveNonce, forceValidity, _usedQueueRegs);
    }

    /// <summary>
    /// Resolves an officer's LOWER SHIELDS order.
    /// On Days 1-13, captains ALWAYS comply. Outright refusal is strictly gated to Day 14+ & rep_score <= -9.
    /// </summary>
    public static ComplianceOrderOutcome ResolveShieldsOrder(GameState state, ShipSpec ship)
    {
        if (!ship.GroundTruth.ActualShieldsUp)
            return ComplianceOrderOutcome.AlreadyCompliant;

        bool canRefuse = state.Day >= 14 &&
                         state.GetRep(ship.Slate.DeclaredPrefix) <= Constants.RepExtremeHostileThreshold &&
                         ship.GroundTruth.WillRefuseShieldsCompliance;

        if (canRefuse)
            return ComplianceOrderOutcome.RefusedOutright;

        ship.GroundTruth.ActualShieldsUp = false;
        ship.Slate.DeclaredShieldsStatus = "DOWN";
        return ComplianceOrderOutcome.Complied;
    }

    /// <summary>
    /// Resolves an officer's POWER DOWN WPN order.
    /// Honest armed ships ALWAYS comply on Days 1-13. Liars claiming WPN: OFFLINE with spoofed hot
    /// weapons deflect verbally without triggering a refusal lock (must be caught via swaying SPS lens).
    /// Outright refusal is strictly gated to Day 14+ & rep_score <= -9.
    /// </summary>
    public static ComplianceOrderOutcome ResolveWeaponsOrder(GameState state, ShipSpec ship)
    {
        if (!ship.GroundTruth.ActualWeaponsHot)
            return ComplianceOrderOutcome.AlreadyCompliant;

        if (ship.GroundTruth.HasSpoofedWeapons)
            return ComplianceOrderOutcome.SpoofedDeflection;

        bool canRefuse = state.Day >= 14 &&
                         state.GetRep(ship.Slate.DeclaredPrefix) <= Constants.RepExtremeHostileThreshold &&
                         ship.GroundTruth.WillRefuseWeaponsCompliance;

        if (canRefuse)
            return ComplianceOrderOutcome.RefusedOutright;

        ship.GroundTruth.ActualWeaponsHot = false;
        ship.Slate.DeclaredWeaponsStatus = "OFFLINE";
        return ComplianceOrderOutcome.Complied;
    }

    /// <summary>
    /// Starts the 270-second shift timer when the player triggers "Next Hail" for the first vessel of the shift.
    /// </summary>
    public void NotifyFirstHailTriggered()
    {
        if (!IsPreShiftSequenceActive)
        {
            HasShiftTimerStarted = true;
        }
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || IsPreShiftSequenceActive || !HasShiftTimerStarted)
            return;

        float scaledDelta = deltaSeconds * Math.Clamp(TimeScale, 0.1f, 20.0f);
        bool wasExpired = IsShiftTimerExpired;
        ElapsedShiftSeconds = Math.Min(Constants.ShiftDurationSeconds, ElapsedShiftSeconds + scaledDelta);

        if (!wasExpired && IsShiftTimerExpired && !_shiftHornPlayed)
        {
            _shiftHornPlayed = true;
            AudioHooks.Play("shift_horn");
        }
    }

    /// <summary>
    /// Debug / test helper to fast-forward or immediately end the shift timer.
    /// </summary>
    public void ForceSetElapsedSeconds(float seconds)
    {
        HasShiftTimerStarted = true;
        bool wasExpired = IsShiftTimerExpired;
        ElapsedShiftSeconds = Math.Clamp(seconds, 0f, Constants.ShiftDurationSeconds);
        if (!wasExpired && IsShiftTimerExpired && !_shiftHornPlayed)
        {
            _shiftHornPlayed = true;
            AudioHooks.Play("shift_horn");
        }
    }

    public void ForceEndShiftNow()
    {
        ForceSetElapsedSeconds(Constants.ShiftDurationSeconds);
    }
}
