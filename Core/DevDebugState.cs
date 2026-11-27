namespace SubspaceProtocols.Core;

public enum DebugBrownoutForceMode
{
    Auto = 0,
    ForceOn = 1,
    ForceDim = 2,
    ForceOff = 3
}

/// <summary>
/// Runtime dev-only playtesting flags and live tuners toggled from the F1 / Backtick Debug Overlay.
/// Kept strictly separate from persisted GameState.
/// </summary>
public sealed class DevDebugState
{
    public static readonly float[] TimeScalePresets = [1.0f, 2.0f, 5.0f, 10.0f];
    public static readonly int[] PayPerShipPresets = [5, 8, 10, 15];
    public static readonly float[] AnimationSpeedPresets = [0.75f, 1.0f, 1.25f, 1.5f, 2.0f];
    public static readonly float[] PriceScalePresets = [0.5f, 0.75f, 1.0f, 1.25f, 1.5f];

    public int TimeScalePresetIndex { get; set; } = 0;
    public float ShiftTimeScale => TimeScalePresets[Math.Clamp(TimeScalePresetIndex, 0, TimeScalePresets.Length - 1)];

    /// <summary>
    /// Live economy tuner for base pay per accurately processed ship (default 10 SC).
    /// </summary>
    public int PayPerShipPresetIndex { get; set; } = 2; // 10 SC
    public int PayPerShipScrip => PayPerShipPresets[Math.Clamp(PayPerShipPresetIndex, 0, PayPerShipPresets.Length - 1)];

    /// <summary>
    /// Live pacing tuner scaling ship glide-in, protocol download, clearance upload/transmit, and departure speeds.
    /// </summary>
    public int AnimationSpeedPresetIndex { get; set; } = 1; // 1.0x
    public float AnimationSpeedMultiplier =>
        AnimationSpeedPresets[Math.Clamp(AnimationSpeedPresetIndex, 0, AnimationSpeedPresets.Length - 1)];

    /// <summary>
    /// Live runtime price scale multiplier (0.5x / 0.75x / 1.0x / 1.25x / 1.5x) mutating ConfigFlags in memory.
    /// </summary>
    public int PriceScalePresetIndex { get; set; } = 2; // 1.0x
    public float PriceScaleMultiplier =>
        PriceScalePresets[Math.Clamp(PriceScalePresetIndex, 0, PriceScalePresets.Length - 1)];

    /// <summary>
    /// Controls whether the dev-only [SYNC] shortcut button is visible inside the Desk Reference Manual.
    /// Hidden by default during normal gameplay.
    /// </summary>
    public bool DevManualSyncEnabled { get; set; } = false;

    /// <summary>
    /// Live B1/B2 Hailing Viewscreen portrait scale toggle in F1 Dev Debug Overlay:
    /// true = 3x (192x192 crisp nearest-neighbor), false = 2x (128x128 framed with LIVE FEED / AUDIO ONLY bar).
    /// </summary>
    public bool Use3xPortraitInLayoutB { get; set; } = true;

    /// <summary>
    /// Allows skipping the 5-card intro sequence from the F1 Dev Debug Overlay when starting a new save.
    /// </summary>
    public bool SkipIntroSequence { get; set; } = false;

    /// <summary>
    /// 0 = Auto (~65% valid / ~35% invalid), 1 = Force Valid, 2 = Force Invalid
    /// </summary>
    public int ForceSpawnValidityMode { get; set; } = 0;

    // One-shot command triggers consumed by DeskScreen.Update()
    public bool PendingEndShiftNow { get; set; } = false;
    public bool PendingForceReseedShip { get; set; } = false;
    public bool PendingForceEmptyWaferHopper { get; set; } = false;
    public bool PendingResetStokerStrain { get; set; } = false;
    public bool PendingForceStokerOverload { get; set; } = false;

    // Live Day 15 Stellar Stoker & Brownout debug telemetry / overrides
    public DebugBrownoutForceMode BrownoutForceMode { get; set; } = DebugBrownoutForceMode.Auto;
    public float LiveStokerSafeLoad { get; set; } = 15.0f;
    public float LiveStokerShock { get; set; } = 0.0f;
    public float LiveStokerPreviewShock { get; set; } = 0.0f;
    public float LiveStokerTotal => LiveStokerSafeLoad + LiveStokerShock + LiveStokerPreviewShock;

    public static void CycleWaferScoopMode(ConfigFlags config)
    {
        config.WaferScoopMode = config.WaferScoopMode switch
        {
            WaferScoopMode.PostVerdictAutoScoop => WaferScoopMode.CenterSwipeScoop,
            WaferScoopMode.CenterSwipeScoop => WaferScoopMode.ModifierScoop,
            _ => WaferScoopMode.PostVerdictAutoScoop
        };
    }

    public static string GetWaferScoopShortLabel(WaferScoopMode mode) => mode switch
    {
        WaferScoopMode.CenterSwipeScoop => "SWIPE",
        WaferScoopMode.ModifierScoop => "MODIFIER",
        _ => "POST-VERD"
    };

    public void CycleTimeScale()
    {
        TimeScalePresetIndex = (TimeScalePresetIndex + 1) % TimeScalePresets.Length;
    }

    public void StepPayPerShip(int delta)
    {
        PayPerShipPresetIndex = Math.Clamp(PayPerShipPresetIndex + delta, 0, PayPerShipPresets.Length - 1);
    }

    public void StepAnimationSpeed(int delta)
    {
        AnimationSpeedPresetIndex = Math.Clamp(AnimationSpeedPresetIndex + delta, 0, AnimationSpeedPresets.Length - 1);
    }

    public void StepPriceScale(int delta, ConfigFlags config)
    {
        PriceScalePresetIndex = Math.Clamp(PriceScalePresetIndex + delta, 0, PriceScalePresets.Length - 1);
        config.ApplyRuntimePriceScale(PriceScaleMultiplier);
    }

    public void TogglePortraitScaleB()
    {
        Use3xPortraitInLayoutB = !Use3xPortraitInLayoutB;
    }

    public static void SetDeskUpperLayoutMode(DeskUpperLayoutMode mode, ConfigFlags config)
    {
        config.DeskUpperLayoutMode = mode;
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
