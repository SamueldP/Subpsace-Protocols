using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SubspaceProtocols.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeskUpperLayoutMode
{
    OptionA_Classic = 0,
    OptionB1_CommsSwap = 1,
    OptionB2_CommsSwapFlush = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TalkingHeadAdvanceMode
{
    AutoAdvance = 0,
    ClickToAdvance = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipArtVersion
{
    V1_Classic = 0,
    V2_HiBit = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WaferScoopMode
{
    PostVerdictAutoScoop = 0,
    CenterSwipeScoop = 1,
    ModifierScoop = 2
}

public sealed class ConfigFlags
{
    [JsonPropertyName("enableGravityGauge")]
    public bool EnableGravityGauge { get; set; } = true;

    [JsonPropertyName("enableStokerDial")]
    public bool EnableStokerDial { get; set; } = true;

    [JsonPropertyName("enableDockingDrone")]
    public bool EnableDockingDrone { get; set; } = false;

    [JsonPropertyName("enableDebugMenu")]
    public bool EnableDebugMenu { get; set; } = true;

    [JsonPropertyName("enableDenyTurnBack")]
    public bool EnableDenyTurnBack { get; set; } = false;

    // Booth 04 Upper/Lower-Right Layout Mode (OptionA_Classic / OptionB1_CommsSwap / OptionB2_CommsSwapFlush)
    [JsonPropertyName("deskUpperLayoutMode")]
    public DeskUpperLayoutMode DeskUpperLayoutMode { get; set; } = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;

    // Procedural Ship Art Renderer (V1_Classic / V2_HiBit)
    [JsonPropertyName("shipArtVersion")]
    public ShipArtVersion ShipArtVersion { get; set; } = ShipArtVersion.V2_HiBit;

    // Cert-Wafer Card-Stack Scooping Mode (PostVerdictAutoScoop | CenterSwipeScoop | ModifierScoop)
    [JsonPropertyName("waferScoopMode")]
    public WaferScoopMode WaferScoopMode { get; set; } = WaferScoopMode.PostVerdictAutoScoop;

    // Active UI Font Preset (auto-discovered TTF/OTF stem, built-in Windows preset, or RaylibDefault)
    [JsonPropertyName("uiFontPreset")]
    public string UiFontPreset { get; set; } = "PixelOperatorMono-Bold";

    // Global F1 Font Size Offset (-2 .. +3)
    [JsonPropertyName("uiFontSizeOffset")]
    public int UiFontSizeOffset { get; set; } = 0;

    // Deliberate Non-Blocking CRT Comms Dialogue Typewriter Speed (chars/sec at 1.0x AnimationSpeedMultiplier)
    [JsonPropertyName("dialogueTypewriterCharsPerSec")]
    public float DialogueTypewriterCharsPerSec { get; set; } = 65.0f;

    // Slower Scripted Talking-Head Story Hail Pacing (Day 1 Vorst, Day 3 Kordani Officer, future tentpole hails)
    [JsonPropertyName("talkingHeadCharsPerSec")]
    public float TalkingHeadCharsPerSec { get; set; } = 32.0f;

    [JsonPropertyName("talkingHeadLinePauseSeconds")]
    public float TalkingHeadLinePauseSeconds { get; set; } = 0.95f;

    [JsonPropertyName("talkingHeadAdvanceMode")]
    public TalkingHeadAdvanceMode TalkingHeadAdvanceMode { get; set; } = TalkingHeadAdvanceMode.AutoAdvance;

    // Clock-out Booth 04 sequential shutdown duration (3.8-4.2s configurable, scaled by AnimSpeedMultiplier)
    [JsonPropertyName("clockOutPowerDownDelaySeconds")]
    public float ClockOutPowerDownDelaySeconds { get; set; } = 4.0f;

    // Encounter Compliance & Invalid-Ship Tunables (Build 4.5)
    [JsonPropertyName("shieldsUpBaseChance")]
    public float ShieldsUpBaseChance { get; set; } = Constants.DefaultShieldsUpBaseChance;

    [JsonPropertyName("invalidShipRateEarly")]
    public float InvalidShipRateEarly { get; set; } = Constants.DefaultInvalidShipRateEarly;

    [JsonPropertyName("invalidShipRateMid")]
    public float InvalidShipRateMid { get; set; } = Constants.DefaultInvalidShipRateMid;

    public float GetInvalidShipRate(int day) =>
        Math.Clamp(day <= 5 ? InvalidShipRateEarly : InvalidShipRateMid, 0.0f, 1.0f);

    // Subtle Variable Shift Clock (seconds per stage)
    [JsonPropertyName("shiftDurationDay1")]
    public int ShiftDurationDay1 { get; set; } = Constants.DefaultShiftDurationDay1;

    [JsonPropertyName("shiftDurationDays2To5")]
    public int ShiftDurationDays2To5 { get; set; } = Constants.DefaultShiftDurationDays2To5;

    [JsonPropertyName("shiftDurationDays6To11")]
    public int ShiftDurationDays6To11 { get; set; } = Constants.DefaultShiftDurationDays6To11;

    [JsonPropertyName("shiftDurationDays12To17")]
    public int ShiftDurationDays12To17 { get; set; } = Constants.DefaultShiftDurationDays12To17;

    [JsonPropertyName("shiftDurationDays18Plus")]
    public int ShiftDurationDays18Plus { get; set; } = Constants.DefaultShiftDurationDays18Plus;

    [JsonPropertyName("shiftDurationDays2To4")]
    public int? LegacyShiftDurationDays2To4
    {
        get => null;
        set { if (value.HasValue) ShiftDurationDays2To5 = value.Value; }
    }

    [JsonPropertyName("shiftDurationDays5To9")]
    public int? LegacyShiftDurationDays5To9
    {
        get => null;
        set { if (value.HasValue) ShiftDurationDays6To11 = value.Value; }
    }

    [JsonPropertyName("shiftDurationDays10To15")]
    public int? LegacyShiftDurationDays10To15
    {
        get => null;
        set { if (value.HasValue) ShiftDurationDays12To17 = value.Value; }
    }

    [JsonPropertyName("shiftDurationDays16Plus")]
    public int? LegacyShiftDurationDays16Plus
    {
        get => null;
        set { if (value.HasValue) ShiftDurationDays18Plus = value.Value; }
    }

    public int GetShiftDurationSeconds(int day)
    {
        int raw = day switch
        {
            <= 1 => ShiftDurationDay1,
            <= 5 => ShiftDurationDays2To5,
            <= 11 => ShiftDurationDays6To11,
            <= 17 => ShiftDurationDays12To17,
            _ => ShiftDurationDays18Plus
        };
        return Math.Max(1, raw);
    }

    // PACT REQ & CRYPTO Playtest Placeholder Prices (Pure SC Economy // TODO: BALANCE)
    [JsonPropertyName("berthLeaseCost")]
    public int BerthLeaseCost { get; set; } = Constants.BerthLeaseNightlyScrip;

    [JsonPropertyName("dryRationBricksCost")]
    public int DryRationBricksCost { get; set; } = Constants.DryRationBricksCostScrip;

    [JsonPropertyName("nutrientPasteCost")]
    public int NutrientPasteCost { get; set; } = Constants.NutrientPasteCostScrip;

    [JsonPropertyName("highTierRationsCost")]
    public int HighTierRationsCost { get; set; } = Constants.HighTierRationsCostScrip;

    [JsonPropertyName("scrubSecurityLogsCost")]
    public int ScrubSecurityLogsCost { get; set; } = Constants.ScrubSecurityLogsCostScrip;

    [JsonPropertyName("independentPassageFundTarget")]
    public int IndependentPassageFundTarget { get; set; } = Constants.IndependentPassageFundTarget;

    [JsonPropertyName("passageFundGoal")]
    public int? LegacyPassageFundGoal
    {
        get => null;
        set
        {
            if (value.HasValue)
            {
                IndependentPassageFundTarget = value.Value;
            }
        }
    }

    [JsonIgnore]
    public int PassageFundGoal
    {
        get => IndependentPassageFundTarget;
        set => IndependentPassageFundTarget = value;
    }

    // DECK 4 Grey-Market Playtest Placeholder Prices & Day Gates (// TODO: BALANCE)
    [JsonPropertyName("blankDataSlateCost")]
    public int BlankDataSlateCost { get; set; } = Constants.BlankDataSlateCostScrip;

    [JsonPropertyName("shieldedPocketLiningCost")]
    public int ShieldedPocketLiningCost { get; set; } = Constants.ShieldedPocketLiningCostScrip;

    [JsonPropertyName("pocketUpgrade2Cost")]
    public int PocketUpgrade2Cost { get; set; } = 60;

    [JsonPropertyName("pocketUpgrade3Cost")]
    public int PocketUpgrade3Cost { get; set; } = 120;

    [JsonPropertyName("pocketUpgrade2MinDay")]
    public int PocketUpgrade2MinDay { get; set; } = Constants.DefaultPocketUpgrade2MinDay;

    [JsonPropertyName("pocketUpgrade3MinDay")]
    public int PocketUpgrade3MinDay { get; set; } = Constants.DefaultPocketUpgrade3MinDay;

    [JsonPropertyName("auditDeferralFeeCost")]
    public int AuditDeferralFeeCost { get; set; } = Constants.BrengopAuditDeferralCostScrip;

    // Security Strikes, Clean-Audit Fine & Stellar Stoker Overload (// TODO: BALANCE)
    [JsonPropertyName("maxStrikesBeforeAudit")]
    public int MaxStrikesBeforeAudit { get; set; } = Constants.SecurityStrikesAuditThreshold;

    [JsonPropertyName("cleanAuditFine")]
    public int CleanAuditFine { get; set; } = 40;

    [JsonPropertyName("cleanAuditKordaniRepDrop")]
    public int CleanAuditKordaniRepDrop { get; set; } = -2;

    [JsonPropertyName("stokerOverloadFine")]
    public int StokerOverloadFine { get; set; } = 35; // TODO: BALANCE

    [JsonPropertyName("stokerIdleBaseline")]
    public float StokerIdleBaseline { get; set; } = 15.0f;

    [JsonPropertyName("stokerSafeLoadCap")]
    public float StokerSafeLoadCap { get; set; } = 65.0f;

    [JsonPropertyName("stokerSafeLoadStepFactor")]
    public float StokerSafeLoadStepFactor { get; set; } = 0.30f;

    [JsonPropertyName("stokerUncalibratedShock")]
    public float StokerUncalibratedShock { get; set; } = 33.0f;

    // Quarters Contraband Vent Stash (// TODO: BALANCE against 90 SC Shielded Pocket Lining)
    [JsonPropertyName("ventStashCapacity")]
    public int VentStashCapacity { get; set; } = 3;

    // Configurable Rule Escalation & Hardware Unlock Days (28-Day Spaced Schedule)
    [JsonPropertyName("dayUnlockWarpScanner")]
    public int DayUnlockWarpScanner { get; set; } = Constants.DefaultDayUnlockWarpScanner;

    [JsonPropertyName("dayUnlockManualPart1")]
    public int DayUnlockManualPart1 { get; set; } = Constants.DefaultDayUnlockManualPart1;

    [JsonPropertyName("dayUnlockManualHulls")]
    public int DayUnlockManualHulls { get; set; } = Constants.DefaultDayUnlockManualHulls;

    [JsonPropertyName("dayUnlockWaferCargo")]
    public int DayUnlockWaferCargo { get; set; } = Constants.DefaultDayUnlockWaferCargo;

    [JsonPropertyName("dayUnlockStoker")]
    public int DayUnlockStoker { get; set; } = Constants.DefaultDayUnlockStoker;

    [JsonPropertyName("dayUnlockFaceBribes")]
    public int DayUnlockFaceBribes { get; set; } = Constants.DefaultDayUnlockFaceBribes;

    [JsonPropertyName("dayUnlockCargoBioScan")]
    public int DayUnlockCargoBioScan { get; set; } = Constants.DefaultDayUnlockCargoBioScan;

    [JsonPropertyName("dayUnlockHostilityDestroy")]
    public int DayUnlockHostilityDestroy { get; set; } = Constants.DefaultDayUnlockHostilityDestroy;

    [JsonPropertyName("dayUnlockTransitPass")]
    public int DayUnlockTransitPass { get; set; } = Constants.DefaultDayUnlockTransitPass;

    [JsonPropertyName("dayPactClimax")]
    public int DayPactClimax { get; set; } = Constants.DefaultDayPactClimax;

    [JsonPropertyName("campaignFinalDay")]
    public int CampaignFinalDay { get; set; } = Constants.DefaultCampaignFinalDay;

    public void ApplyRuntimePriceScale(float scale)
    {
        float clamped = Math.Clamp(scale, 0.25f, 3.0f);
        static int ScaleCost(int baseCost, float s) => Math.Max(1, (int)MathF.Round(baseCost * s));

        BerthLeaseCost = ScaleCost(Constants.BerthLeaseNightlyScrip, clamped);
        DryRationBricksCost = ScaleCost(Constants.DryRationBricksCostScrip, clamped);
        NutrientPasteCost = ScaleCost(Constants.NutrientPasteCostScrip, clamped);
        HighTierRationsCost = ScaleCost(Constants.HighTierRationsCostScrip, clamped);
        ScrubSecurityLogsCost = ScaleCost(Constants.ScrubSecurityLogsCostScrip, clamped);
        BlankDataSlateCost = ScaleCost(Constants.BlankDataSlateCostScrip, clamped);
        ShieldedPocketLiningCost = ScaleCost(Constants.ShieldedPocketLiningCostScrip, clamped);
        PocketUpgrade2Cost = ScaleCost(60, clamped);
        PocketUpgrade3Cost = ScaleCost(120, clamped);
        AuditDeferralFeeCost = ScaleCost(Constants.BrengopAuditDeferralCostScrip, clamped);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static ConfigFlags Load()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "data", "config.json");
        if (!File.Exists(configPath))
        {
            return new ConfigFlags();
        }

        try
        {
            string json = File.ReadAllText(configPath);
            ConfigFlags loaded = JsonSerializer.Deserialize<ConfigFlags>(json, JsonOptions) ?? new ConfigFlags();
            loaded.ClockOutPowerDownDelaySeconds = Math.Clamp(loaded.ClockOutPowerDownDelaySeconds, 3.8f, 4.2f);
            loaded.DialogueTypewriterCharsPerSec = Math.Clamp(loaded.DialogueTypewriterCharsPerSec, 15.0f, 300.0f);
            loaded.TalkingHeadCharsPerSec = Math.Clamp(loaded.TalkingHeadCharsPerSec, 10.0f, 150.0f);
            loaded.TalkingHeadLinePauseSeconds = Math.Clamp(loaded.TalkingHeadLinePauseSeconds, 0.10f, 5.0f);
            if (!Enum.IsDefined(loaded.TalkingHeadAdvanceMode))
            {
                loaded.TalkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance;
            }
            if (!Enum.IsDefined(loaded.DeskUpperLayoutMode))
            {
                loaded.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            }
            if (!Enum.IsDefined(loaded.ShipArtVersion))
            {
                loaded.ShipArtVersion = ShipArtVersion.V2_HiBit;
            }
            loaded.UiFontPreset = string.IsNullOrWhiteSpace(loaded.UiFontPreset)
                ? "ConsolasBold"
                : loaded.UiFontPreset.Trim();
            loaded.UiFontSizeOffset = Math.Clamp(loaded.UiFontSizeOffset, -2, 3);
            loaded.IndependentPassageFundTarget = Math.Clamp(loaded.IndependentPassageFundTarget, 100, 10000);
            loaded.ShieldsUpBaseChance = Math.Clamp(loaded.ShieldsUpBaseChance, 0.0f, 1.0f);
            loaded.InvalidShipRateEarly = Math.Clamp(loaded.InvalidShipRateEarly, 0.0f, 1.0f);
            loaded.InvalidShipRateMid = Math.Clamp(loaded.InvalidShipRateMid, 0.0f, 1.0f);
            loaded.ShiftDurationDay1 = Math.Clamp(loaded.ShiftDurationDay1, 60, 1800);
            loaded.ShiftDurationDays2To5 = Math.Clamp(loaded.ShiftDurationDays2To5, 60, 1800);
            loaded.ShiftDurationDays6To11 = Math.Clamp(loaded.ShiftDurationDays6To11, 60, 1800);
            loaded.ShiftDurationDays12To17 = Math.Clamp(loaded.ShiftDurationDays12To17, 60, 1800);
            loaded.ShiftDurationDays18Plus = Math.Clamp(loaded.ShiftDurationDays18Plus, 60, 1800);
            loaded.VentStashCapacity = Math.Clamp(loaded.VentStashCapacity, 1, 8);
            loaded.MaxStrikesBeforeAudit = Math.Clamp(loaded.MaxStrikesBeforeAudit, 1, 10);
            loaded.StokerIdleBaseline = Math.Clamp(loaded.StokerIdleBaseline, 0.0f, 50.0f);
            loaded.StokerSafeLoadCap = Math.Clamp(loaded.StokerSafeLoadCap, 20.0f, 95.0f);
            loaded.StokerSafeLoadStepFactor = Math.Clamp(loaded.StokerSafeLoadStepFactor, 0.05f, 0.90f);
            loaded.StokerUncalibratedShock = Math.Clamp(loaded.StokerUncalibratedShock, 5.0f, 100.0f);
            loaded.PocketUpgrade2MinDay = Math.Clamp(loaded.PocketUpgrade2MinDay, 1, 35);
            loaded.PocketUpgrade3MinDay = Math.Clamp(loaded.PocketUpgrade3MinDay, 1, 35);
            loaded.DayUnlockWarpScanner = Math.Clamp(loaded.DayUnlockWarpScanner, 1, 35);
            loaded.DayUnlockManualPart1 = Math.Clamp(loaded.DayUnlockManualPart1, 1, 35);
            loaded.DayUnlockManualHulls = Math.Clamp(loaded.DayUnlockManualHulls, 1, 35);
            loaded.DayUnlockWaferCargo = Math.Clamp(loaded.DayUnlockWaferCargo, 1, 35);
            loaded.DayUnlockStoker = Math.Clamp(loaded.DayUnlockStoker, 1, 35);
            loaded.DayUnlockFaceBribes = Math.Clamp(loaded.DayUnlockFaceBribes, 1, 35);
            loaded.DayUnlockCargoBioScan = Math.Clamp(loaded.DayUnlockCargoBioScan, 1, 35);
            loaded.DayUnlockHostilityDestroy = Math.Clamp(loaded.DayUnlockHostilityDestroy, 1, 35);
            loaded.DayUnlockTransitPass = Math.Clamp(loaded.DayUnlockTransitPass, 1, 35);
            loaded.DayPactClimax = Math.Clamp(loaded.DayPactClimax, 1, 35);
            loaded.CampaignFinalDay = Math.Clamp(loaded.CampaignFinalDay, 1, 35);
            return loaded;
        }
        catch
        {
            return new ConfigFlags();
        }
    }

    public void TryPersistTalkingHeadAdvanceMode(bool suppressDiskPersistence = false)
    {
        if (suppressDiskPersistence)
            return;

        string configPath = Path.Combine(AppContext.BaseDirectory, "data", "config.json");
        if (!File.Exists(configPath))
            return;

        try
        {
            string json = File.ReadAllText(configPath);
            string modeStr = TalkingHeadAdvanceMode.ToString();
            Regex rx = new("(\"talkingHeadAdvanceMode\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase);
            string updated;
            if (rx.IsMatch(json))
            {
                updated = rx.Replace(json, m => m.Groups[1].Value + modeStr + m.Groups[3].Value, 1);
            }
            else
            {
                int insertIdx = json.LastIndexOf('}');
                if (insertIdx < 0)
                    return;
                string prefix = json[..insertIdx].TrimEnd();
                bool needsComma = prefix.Length > 0 && !prefix.EndsWith(',') && !prefix.EndsWith('{');
                updated = prefix + (needsComma ? "," : "") +
                          Environment.NewLine + $"  \"talkingHeadAdvanceMode\": \"{modeStr}\"" +
                          Environment.NewLine + "}" + Environment.NewLine;
            }
            File.WriteAllText(configPath, updated);
        }
        catch
        {
            // Non-fatal if data/config.json is read-only
        }
    }

    public bool TryPersistUiFontPreset(bool suppressDiskPersistence = false)
    {
        if (suppressDiskPersistence)
            return false;

        string configPath = Path.Combine(AppContext.BaseDirectory, "data", "config.json");
        return TryPersistUiFontPresetToPath(configPath);
    }

    public bool TryPersistUiFontPresetToPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            return false;

        string tmpPath = configPath + ".tmp";
        try
        {
            string json = File.ReadAllText(configPath);
            string presetValue = string.IsNullOrWhiteSpace(UiFontPreset) ? "ConsolasBold" : UiFontPreset.Trim();
            string escapedPreset = presetValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

            Regex rx = new("(\"uiFontPreset\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase);
            string updated;
            if (rx.IsMatch(json))
            {
                updated = rx.Replace(json, m => m.Groups[1].Value + escapedPreset + m.Groups[3].Value, 1);
            }
            else
            {
                int insertIdx = json.LastIndexOf('}');
                if (insertIdx < 0)
                    return false;
                string prefix = json[..insertIdx].TrimEnd();
                bool needsComma = prefix.Length > 0 && !prefix.EndsWith(',') && !prefix.EndsWith('{');
                updated = prefix + (needsComma ? "," : "") +
                          Environment.NewLine + $"  \"uiFontPreset\": \"{escapedPreset}\"" +
                          Environment.NewLine + "}" + Environment.NewLine;
            }

            File.WriteAllText(tmpPath, updated);
            File.Move(tmpPath, configPath, overwrite: true);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            return false;
        }
    }

    public bool TryPersistUiFontSizeOffset(bool suppressDiskPersistence = false)
    {
        if (suppressDiskPersistence)
            return false;

        string configPath = Path.Combine(AppContext.BaseDirectory, "data", "config.json");
        return TryPersistUiFontSizeOffsetToPath(configPath);
    }

    public bool TryPersistUiFontSizeOffsetToPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            return false;

        string tmpPath = configPath + ".tmp";
        try
        {
            string json = File.ReadAllText(configPath);
            int clampedOffset = Math.Clamp(UiFontSizeOffset, -2, 3);

            Regex rx = new("(\"uiFontSizeOffset\"\\s*:\\s*)(-?\\d+)", RegexOptions.IgnoreCase);
            string updated;
            if (rx.IsMatch(json))
            {
                updated = rx.Replace(json, m => m.Groups[1].Value + clampedOffset.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            }
            else
            {
                int insertIdx = json.LastIndexOf('}');
                if (insertIdx < 0)
                    return false;
                string prefix = json[..insertIdx].TrimEnd();
                bool needsComma = prefix.Length > 0 && !prefix.EndsWith(',') && !prefix.EndsWith('{');
                updated = prefix + (needsComma ? "," : "") +
                          Environment.NewLine + $"  \"uiFontSizeOffset\": {clampedOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                          Environment.NewLine + "}" + Environment.NewLine;
            }

            File.WriteAllText(tmpPath, updated);
            File.Move(tmpPath, configPath, overwrite: true);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            return false;
        }
    }
}
