using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Manages shift timer (270s / 4m 30s), Brown Dwarf orbital progress (0..1),
/// and the Time Runout Rule (no new hails after 270s, but current ship can finish).
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShiftController
{
    public float ElapsedShiftSeconds { get; private set; }
    public bool IsShiftTimerExpired => ElapsedShiftSeconds >= Constants.ShiftDurationSeconds;
    public bool HasActiveShipInBay { get; set; }

    public float BrownDwarfArcProgress =>
        Math.Clamp(ElapsedShiftSeconds / Constants.ShiftDurationSeconds, 0.0f, 1.0f);

    public bool CanTriggerNextHail => !IsShiftTimerExpired;
    public bool CanEndShift => IsShiftTimerExpired && !HasActiveShipInBay;

    public void StartNewShift(GameState state)
    {
        ElapsedShiftSeconds = 0.0f;
        HasActiveShipInBay = false;
        state.ShipsProcessedToday = 0;
        state.ClericalWarningsThisShift = 0;
        state.BribeOfferedThisShift = false;
    }

    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        ElapsedShiftSeconds = Math.Min(Constants.ShiftDurationSeconds, ElapsedShiftSeconds + deltaSeconds);
    }
}
