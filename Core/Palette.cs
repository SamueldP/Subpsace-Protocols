using System.Globalization;
using Raylib_cs;

namespace SubspaceProtocols.Core;

/// <summary>
/// Canonical color palette and 4-step material ramps defined in
/// /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md.
/// </summary>
public static class Palette
{
    // Must be declared FIRST before any static Color fields that call FromHex() during static initialization
    private static readonly Dictionary<string, Color> HexCache = new(StringComparer.OrdinalIgnoreCase);

    // =========================================================================
    // 1. Core Station & UI Color Palette (Section 2)
    // =========================================================================
    public const string HexOutlineVoid = "#080b10";
    public const string HexDeepSpaceVoid = "#0f1319";
    public const string HexBrownDwarfGlowDeep = "#3b1518";
    public const string HexBrownDwarfGlowRim = "#7a2b26";
    public const string HexWeatheredGunmetalMid = "#262d36";
    public const string HexWeatheredGunmetalEdge = "#434e5c";
    public const string HexOxidizedBronzeShadow = "#5c3a21";
    public const string HexOxidizedBronzeWorn = "#8c5832";
    public const string HexCrtAmberPrimary = "#ffb84d";
    public const string HexCrtAmberDimmed = "#b45309";
    public const string HexClearanceTeal = "#2dd4bf";
    public const string HexAlertCrimson = "#dc2626";

    public static readonly Color OutlineVoid = FromHex(HexOutlineVoid);
    public static readonly Color DeepSpaceVoid = FromHex(HexDeepSpaceVoid);
    public static readonly Color BrownDwarfGlowDeep = FromHex(HexBrownDwarfGlowDeep);
    public static readonly Color BrownDwarfGlowRim = FromHex(HexBrownDwarfGlowRim);
    public static readonly Color WeatheredGunmetalMid = FromHex(HexWeatheredGunmetalMid);
    public static readonly Color WeatheredGunmetalEdge = FromHex(HexWeatheredGunmetalEdge);
    public static readonly Color OxidizedBronzeShadow = FromHex(HexOxidizedBronzeShadow);
    public static readonly Color OxidizedBronzeWorn = FromHex(HexOxidizedBronzeWorn);
    public static readonly Color CrtAmberPrimary = FromHex(HexCrtAmberPrimary);
    public static readonly Color CrtAmberDimmed = FromHex(HexCrtAmberDimmed);
    public static readonly Color ClearanceTeal = FromHex(HexClearanceTeal);
    public static readonly Color AlertCrimson = FromHex(HexAlertCrimson);

    // =========================================================================
    // 2. Subspace Power-Grid Scanner (SPS) Viewport Render Layer (Section 3, Block 5)
    // =========================================================================
    public const string HexSpsShieldArcCyan = "#38bdf8";
    public const string HexSpsShieldArcIce = "#e0f2fe";
    public const string HexSpsHullDimDark = "#0c141d";
    public const string HexSpsHullDimLight = "#1c2b3a";
    public const string HexSpsWeaponsHot = "#ef4444";
    public const string HexSpsWeaponsHotOrange = "#f97316";
    public const string HexSpsWeaponsHotCore = "#fdba74";
    public const string HexSpsHardpointOffline = "#1e293b";
    public const string HexSpsWarpPrimed = "#f59e0b";
    public const string HexSpsWarpIdle = "#115e59";
    public const string HexSpsCommsActive = "#38bdf8";
    public const string HexSpsCommsBroken = "#475569";
    public const string HexSpsCargoHighDraw = "#a855f7";

    public static readonly Color SpsShieldArcCyan = FromHex(HexSpsShieldArcCyan);
    public static readonly Color SpsShieldArcIce = FromHex(HexSpsShieldArcIce);
    public static readonly Color SpsHullDimDark = FromHex(HexSpsHullDimDark);
    public static readonly Color SpsHullDimLight = FromHex(HexSpsHullDimLight);
    public static readonly Color SpsWeaponsHot = FromHex(HexSpsWeaponsHot);
    public static readonly Color SpsWeaponsHotOrange = FromHex(HexSpsWeaponsHotOrange);
    public static readonly Color SpsWeaponsHotCore = FromHex(HexSpsWeaponsHotCore);
    public static readonly Color SpsHardpointOffline = FromHex(HexSpsHardpointOffline);
    public static readonly Color SpsWarpPrimed = FromHex(HexSpsWarpPrimed);
    public static readonly Color SpsWarpIdle = FromHex(HexSpsWarpIdle);
    public static readonly Color SpsCommsActive = FromHex(HexSpsCommsActive);
    public static readonly Color SpsCommsBroken = FromHex(HexSpsCommsBroken);
    public static readonly Color SpsCargoHighDraw = FromHex(HexSpsCargoHighDraw);

    // =========================================================================
    // 3. Faction Starship Hull Ramps [shadow, mid, base, highlight] (Section 4)
    // =========================================================================
    public static readonly string[] GuPrimaryWhiteRamp = ["#64748b", "#94a3b8", "#cbd5e1", "#f1f5f9"];
    public static readonly string[] GuSecondaryGunmetalRamp = ["#1e293b", "#334155", "#475569", "#64748b"];
    public static readonly string[] GuDecommissionedWhiteRamp = ["#4e565e", "#78828a", "#a3acb3", "#c8cfd4"];
    public const string HexGuEngineGlow = "#2dd4bf";
    public const string HexGuDecommissionedEngineGlow = "#5b8c85";

    public static readonly string[] KordaniPrimaryBlueGreenRamp = ["#0a2730", "#0f4c5c", "#1b6b73", "#2e8b8f"];
    public static readonly string[] KordaniSecondaryDarkSlateRamp = ["#0f172a", "#1e293b", "#334155", "#475569"];
    public const string HexKordaniEngineGlow = "#f59e0b";

    public static readonly string[] BrengopPrimaryGrossYellowRamp = ["#423c0e", "#695f18", "#968826", "#bdb040"];
    public static readonly string[] BrengopSecondaryGrimyBrassRamp = ["#2e2414", "#4a3a1e", "#6b5324", "#8a6d33"];
    public const string HexBrengopEngineGlow = "#d97706";

    public static readonly string[] KrushvakPrimaryOliveGreenRamp = ["#2f4526", "#4a693b", "#688c51", "#8ab06c"];
    public static readonly string[] KrushvakSecondaryEarthBrownRamp = ["#362418", "#523826", "#704e36", "#8c6549"];
    public const string HexKrushvakEngineGlow = "#ef4444";

    public static readonly string[] PrunjunFlameRamp = ["#b91c1c", "#ef4444", "#f97316", "#facc15"];
    public static readonly string[] PrunjunCockpitGlassRamp = ["#1e3a5f", "#38bdf8", "#7dd3fc", "#e0f2fe"];

    public static readonly string[] OrunPrimaryGraniteRamp = ["#334155", "#64748b", "#94a3b8", "#cbd5e1"];
    public static readonly string[] OrunGlassContainerRamp = ["#1e293b", "#475569", "#94a3b8", "#e2e8f0"];

    public static readonly string[] TolvakPrimaryTerracottaRamp = ["#4a251d", "#8c3b27", "#b45338", "#d6c29f"];

    public static readonly string[] FreeTraderPrimarySlateRamp = ["#23272e", "#3e4552", "#5f697a", "#8591a6"];
    public static readonly string[] FreeTraderTrimRustRamp = ["#6b2f1a", "#9c4827", "#cc6b35", "#eb9654"];
    public const string HexFreeTraderEngineGlow = "#f59e0b";

    public static readonly string[] FarLinePrimaryCreamRamp = ["#545147", "#827d6e", "#b5afa0", "#dcd7c9"];
    public static readonly string[] FarLineAccentNavyRamp = ["#10233d", "#1c3b66", "#2a568f", "#4678b8"];
    public const string HexFarLineEngineGlow = "#38bdf8";

    public static readonly string[] MiningGuildPrimaryCharcoalRamp = ["#181a1c", "#2c3036", "#464c54", "#666f7a"];
    public static readonly string[] MiningGuildTrimHazardYellowRamp = ["#6e5009", "#a67b11", "#dba61f", "#facc15"];
    public const string HexMiningGuildEngineGlow = "#fb923c";

    public static readonly string[] ZenithPrimaryBurgundyRamp = ["#361424", "#5c223e", "#853459", "#ad4b77"];
    public static readonly string[] ZenithPearlWhiteRamp = ["#737a82", "#a6aeb8", "#d4dce6", "#f2f6fa"];
    public static readonly string[] ZenithTrimPolishedGoldRamp = ["#785616", "#b38424", "#e0b043", "#fce08b"];
    public const string HexZenithEngineGlowLavender = "#a78bfa";

    // =========================================================================
    // 4. Species Portrait Skin & Material Ramps (Section 5)
    // =========================================================================
    public static readonly string[][] HumanSkinRamps =
    [
        ["#2b1810", "#4a2a1c", "#6b3e2b", "#87543e"], // Deep
        ["#422416", "#6e3e27", "#94573a", "#b37150"], // Rich Brown
        ["#5c3d28", "#8c6142", "#b8855f", "#d4a37d"], // Olive/Tan
        ["#6e4732", "#a36e50", "#cc9474", "#e3b296"], // Warm Medium
        ["#7a5444", "#b3836d", "#dba88f", "#f2cab6"], // Light
        ["#825e52", "#bd9182", "#e3b9aa", "#f7d8cc"]  // Fair
    ];

    public static readonly string[] KlaasariOceanBlueRamp = ["#113859", "#1d5d8f", "#2e86c1", "#5dade2"];
    public static readonly string[] KlaasariEmeraldGreenRamp = ["#114d3a", "#1d7a5b", "#2eb083", "#5ee0b0"];

    public static readonly string[] ThaneTitaniumWhiteRamp = ["#7c8a99", "#aebac7", "#d8e2eb", "#f1f5f9"];
    public static readonly string[] ThaneBeardDeepBlackRamp = ["#05070a", "#0f131a", "#1e2633", "#2d3748"];

    public static readonly string[] VeldorHelmetPorcelainRamp = ["#64748b", "#94a3b8", "#cbd5e1", "#f8fafc"];
    public static readonly string[] VeldorBrassTrimRamp = ["#5c3a21", "#8c5832", "#b45309", "#d97706"];
    public static readonly string[] VeldorSulfurVisorRamp = ["#3b2b0a", "#785712", "#b5841d", "#e8b93c"];

    public static readonly string[][] BrengopSkinRamps =
    [
        ["#5c5c54", "#85857a", "#abab9f", "#c9c9be"], // Pale Putty
        ["#595052", "#827477", "#a8999c", "#c7b8bb"]  // Clammy Grey-Pink
    ];

    public static readonly string[][] KordaniSkinRamps =
    [
        ["#1e3a4c", "#335c75", "#4b7c9b", "#6ea2c2"], // Cold Blue
        ["#2c3540", "#475569", "#64748b", "#94a3b8"], // Slate Grey
        ["#4c356e", "#6d4c9c", "#9170c4", "#b89be6"]  // Royal Light Purple
    ];

    public static readonly string[][] KrushvakSkinRamps =
    [
        ["#26150b", "#422615", "#5c3a21", "#784e30"], // Deep Brown
        ["#121214", "#222226", "#333338", "#47474f"], // Charcoal Black
        ["#132418", "#1f3b27", "#2d5438", "#3f704d"]  // Deep Forest Green
    ];

    public static readonly string[] DoppleTrueFormRamp = ["#5c4a38", "#8a7259", "#b5997b", "#d4ba9c"];

    public static readonly string[][] HunBorianSkinRamps =
    [
        ["#6b4710", "#9e6c19", "#cf9325", "#e8b343"], // Warm Ochre-Yellow
        ["#3d2410", "#633c1b", "#875428", "#a86e3b"]  // Rich Earth-Brown
    ];
    public const string HexHunBorianKindEye = "#93c5fd";
    public const string HexHunBorianEyeSpecular = "#eff6ff";

    public static readonly string[][] PrunjunSkinRamps =
    [
        ["#4f4a36", "#787153", "#9c9470", "#b8b08c"], // Olive-Tan
        ["#524444", "#7a6666", "#9e8686", "#bda4a4"]  // Dull Grey-Pink
    ];

    public static readonly string[] TolvakSkinRamp = ["#4a251d", "#7d3f2b", "#b45338", "#d6c29f"];
    public const string HexTolvakMoltPatch = "#e8dcc5";

    public static readonly string[] OrunSkinRamp = ["#334155", "#64748b", "#94a3b8", "#cbd5e1"];
    public const string HexOrunBrassTrim = "#d4af37";

    /// <summary>
    /// Converts a CSS-style hex color string (e.g. "#ffb84d" or "#080b10") into a Raylib Color.
    /// Uses an in-memory lookup table for zero-overhead repeated conversions.
    /// </summary>
    public static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Color.Blank;

        if (HexCache != null && HexCache.TryGetValue(hex, out Color cached))
            return cached;

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith("#"))
            span = span[1..];

        Color parsed = Color.Magenta;
        if (span.Length == 6 &&
            byte.TryParse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
            byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
            byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            parsed = new Color(r, g, b, (byte)255);
        }
        else if (span.Length == 8 &&
            byte.TryParse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r8) &&
            byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g8) &&
            byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b8) &&
            byte.TryParse(span[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte a8))
        {
            parsed = new Color(r8, g8, b8, a8);
        }

        if (HexCache != null)
        {
            HexCache[hex] = parsed;
        }
        return parsed;
    }

    public static string ToHex(Color c) =>
        $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    public static float GetLuminance(Color c) =>
        (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255.0f;

    public static float GetLuminance(string hex) =>
        GetLuminance(FromHex(hex));
}
