using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Manages the 4-minute 30-second (270s) shift timer, Brown Dwarf orbital progress (0..1),
/// hold-until-first-hail rule, pre-shift directive / Kordani hail pause state, debug TimeScale,
/// shift-end horn trigger, and the Time Runout Rule (no new hails after 270s,
/// while allowing the player to finish resolving the ship currently in the bay).
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShiftController
{
    private bool _shiftHornPlayed;

    public float ElapsedShiftSeconds { get; private set; }
    public bool HasShiftTimerStarted { get; private set; }
    public bool IsShiftTimerExpired => ElapsedShiftSeconds >= Constants.ShiftDurationSeconds;
    public bool HasActiveShipInBay { get; set; }
    public bool IsPreShiftSequenceActive { get; set; }
    public float TimeScale { get; set; } = 1.0f;

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

        // Cleanly reset daily clerical warning counter (2 free grace warnings/shift) at the start of every shift
        state.ShipsProcessedToday = 0;
        state.ClericalWarningsThisShift = 0;
        state.BribeOfferedThisShift = false;
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
