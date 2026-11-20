namespace SubspaceProtocols.Core;

/// <summary>
/// Runtime dev-only playtesting flags toggled from the F1 / Backtick Debug Overlay.
/// Kept strictly separate from persisted GameState.
/// </summary>
public sealed class DevDebugState
{
    public static readonly float[] TimeScalePresets = [1.0f, 2.0f, 5.0f, 10.0f];

    public int TimeScalePresetIndex { get; set; } = 0;
    public float ShiftTimeScale => TimeScalePresets[Math.Clamp(TimeScalePresetIndex, 0, TimeScalePresets.Length - 1)];

    /// <summary>
    /// Controls whether the dev-only [SYNC] shortcut button is visible inside the Desk Reference Manual.
    /// Hidden by default during normal gameplay.
    /// </summary>
    public bool DevManualSyncEnabled { get; set; } = false;

    /// <summary>
    /// 0 = Auto (~65% valid / ~35% invalid), 1 = Force Valid, 2 = Force Invalid
    /// </summary>
    public int ForceSpawnValidityMode { get; set; } = 0;

    // One-shot command triggers consumed by DeskScreen.Update()
    public bool PendingEndShiftNow { get; set; } = false;
    public bool PendingForceReseedShip { get; set; } = false;

    public void CycleTimeScale()
    {
        TimeScalePresetIndex = (TimeScalePresetIndex + 1) % TimeScalePresets.Length;
    }

    public void CycleSpawnValidityMode()
    {
        ForceSpawnValidityMode = (ForceSpawnValidityMode + 1) % 3;
    }

    public string SpawnValidityLabel => ForceSpawnValidityMode switch
    {
        1 => "FORCE VALID",
        2 => "FORCE INVALID",
        _ => "AUTO (65/35)"
    };
}
