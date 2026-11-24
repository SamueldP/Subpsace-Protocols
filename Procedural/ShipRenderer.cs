using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

public enum ShipFaction
{
    GalacticUnion = 0,      // GUV-
    Kordani = 1,            // KDF-
    Brengop = 2,            // BMC-
    Krushvak = 3,           // KRW-
    Orun = 4,               // ORN-
    Tolvak = 5,             // TLV-
    Prunjun = 6,            // PRJ- (Rigid Hack-Job Assembly)
    FreeTraders = 7,        // FTV- (and HBR- Hun-Borian Free Vessels)
    FarLineTransit = 8,     // FTC- (Far-Line Transit Corp)
    MiningGuild = 9,        // MNG-
    ZenithInterstellar = 10 // ZNI-
}

public enum ShipClass
{
    Military = 0,
    Civilian = 1,
    Cargo = 2,
    Diplomatic = 3,
    HackJob = 4
}

public enum ShipRenderMode
{
    Live = 0,
    Manual = 1
}

public sealed record ShipRenderOptions
{
    public string SeedId { get; init; } = "GUV-771-VERIDIA";
    public ShipFaction Faction { get; init; } = ShipFaction.GalacticUnion;
    public ShipClass RequestedClass { get; init; } = ShipClass.Civilian;
    public ShipRenderMode Mode { get; init; } = ShipRenderMode.Live;
    public bool SpsEnabled { get; init; } = false;
    public bool ShieldsUp { get; init; } = false;
    public bool WeaponsHot { get; init; } = false;
    public bool IsDecommissioned { get; init; } = false;
    public bool WarpPrimed { get; init; } = true;
    public bool UncannyWarpPylonPulse { get; init; } = false;
    public bool CommsBroken { get; init; } = false;
    public bool CargoHighDraw { get; init; } = false;
    public bool HasDownstreamHullScorch { get; init; } = false;
    public int AnimTick { get; init; } = 0;
    public bool IsScriptedOverride { get; init; } = false;
    public string? ScriptedAssetName { get; init; } = null;

    /// <summary>
    /// Quantizes animation tick to 9-frame discrete steps so rasterization/texture upload
    /// occurs only when visual animation state actually changes—never every frame.
    /// Manual mode is completely static (bucket 0).
    /// </summary>
    public int EffectiveAnimBucket =>
        Mode == ShipRenderMode.Manual ? 0 : (AnimTick / 9);
}

/// <summary>
/// Dynamic hardpoint & bounding-box layout returned directly by each ship class silhouette builder
/// so SPS conduits, nav strobes, engine glow, weapon mounts, and shield perimeters always align
/// strictly inside the hull regardless of class length (e.g., 68px shuttle vs. 112px freighter).
/// </summary>
public sealed class ShipHardpointLayout
{
    public (int X, int Y) ReactorCore { get; set; } = (60, 28);
    public (int X, int Y) ForwardWeapons { get; set; } = (18, 28);
    public (int X, int Y) WarpPylons { get; set; } = (94, 14);
    public (int X, int Y) BridgeComms { get; set; } = (34, 19);
    public (int X, int Y) CargoBay { get; set; } = (58, 36);
    public (int Cx, int Cy, int Rx, int Ry) ShieldEnvelope { get; set; } = (60, 28, 54, 23);
    public List<(int X, int Y)> NavStrobes { get; } = [];
    public List<(int X, int Y, int W, int H)> EngineGlowRects { get; } = [];
    public List<(int X, int Y)> WeaponMountPixels { get; } = [];
}

/// <summary>
/// 120x56 left-facing procedural starship generator implementing Section 3 (Blocks 4 & 5)
/// and Section 4 of /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md.
/// Separates Faction visual language (ramps, motifs, pylon styling) from Class macro silhouette
/// (MIL compact/wide, CIV ~65-75px short shuttle/runabout, CRG full-120px heavy hauler, DIP long/sleek cruiser).
/// </summary>
public static class ShipRenderer
{
    public static readonly IReadOnlyList<ShipFaction> AllFactions =
    [
        ShipFaction.GalacticUnion,
        ShipFaction.Kordani,
        ShipFaction.Brengop,
        ShipFaction.Krushvak,
        ShipFaction.Orun,
        ShipFaction.Tolvak,
        ShipFaction.Prunjun,
        ShipFaction.FreeTraders,
        ShipFaction.FarLineTransit,
        ShipFaction.MiningGuild,
        ShipFaction.ZenithInterstellar
    ];

    /// <summary>
    /// Canonical Ship Class Availability Matrix from Section 4 of the Procedural Art Bible.
    /// </summary>
    public static IReadOnlyList<ShipClass> GetAllowedClasses(ShipFaction faction) =>
        faction switch
        {
            ShipFaction.GalacticUnion => [ShipClass.Military, ShipClass.Civilian, ShipClass.Cargo, ShipClass.Diplomatic],
            ShipFaction.Kordani => [ShipClass.Military, ShipClass.Diplomatic],
            ShipFaction.Brengop => [ShipClass.Military, ShipClass.Civilian, ShipClass.Cargo, ShipClass.Diplomatic],
            ShipFaction.Krushvak => [ShipClass.Military, ShipClass.Cargo],
            ShipFaction.Orun => [ShipClass.Civilian, ShipClass.Cargo, ShipClass.Diplomatic],
            ShipFaction.Tolvak => [ShipClass.Civilian, ShipClass.Cargo],
            ShipFaction.Prunjun => [ShipClass.HackJob],
            ShipFaction.FreeTraders => [ShipClass.Military, ShipClass.Civilian, ShipClass.Cargo, ShipClass.Diplomatic],
            ShipFaction.FarLineTransit => [ShipClass.Cargo, ShipClass.Civilian],
            ShipFaction.MiningGuild => [ShipClass.Cargo, ShipClass.Military],
            ShipFaction.ZenithInterstellar => [ShipClass.Diplomatic, ShipClass.Cargo],
            _ => [ShipClass.Civilian]
        };

    public static bool IsClassAllowed(ShipFaction faction, ShipClass shipClass)
    {
        IReadOnlyList<ShipClass> allowed = GetAllowedClasses(faction);
        for (int i = 0; i < allowed.Count; i++)
        {
            if (allowed[i] == shipClass)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Enforces the Ship Class Availability Matrix: if requestedClass is not valid for the faction,
    /// snaps to the first allowed class for that faction.
    /// </summary>
    public static ShipClass NormalizeShipClass(ShipFaction faction, ShipClass requestedClass)
    {
        IReadOnlyList<ShipClass> allowed = GetAllowedClasses(faction);
        for (int i = 0; i < allowed.Count; i++)
        {
            if (allowed[i] == requestedClass)
                return requestedClass;
        }
        return allowed[0];
    }

    /// <summary>
    /// Enforces the GU Decommissioned Hull rule: ONLY GU Civilian and GU Cargo hulls can use the
    /// rundown weathering pass (never GU Military unless flagged as a unique scripted story entity).
    /// </summary>
    public static bool IsDecommissionedEffective(ShipFaction faction, ShipClass effectiveClass, bool requestedDecomm, bool isScripted)
    {
        if (!requestedDecomm || faction != ShipFaction.GalacticUnion)
            return false;

        if (effectiveClass is ShipClass.Civilian or ShipClass.Cargo)
            return true;

        if (effectiveClass == ShipClass.Military && isScripted)
            return true;

        return false;
    }

    public static string GetFactionPrefix(ShipFaction faction) =>
        faction switch
        {
            ShipFaction.GalacticUnion => "GUV-",
            ShipFaction.Kordani => "KDF-",
            ShipFaction.Brengop => "BMC-",
            ShipFaction.Krushvak => "KRW-",
            ShipFaction.Orun => "ORN-",
            ShipFaction.Tolvak => "TLV-",
            ShipFaction.Prunjun => "PRJ-",
            ShipFaction.FreeTraders => "FTV-",
            ShipFaction.FarLineTransit => "FTC-",
            ShipFaction.MiningGuild => "MNG-",
            ShipFaction.ZenithInterstellar => "ZNI-",
            _ => "FTV-"
        };

    public static string GetFactionDisplayName(ShipFaction faction) =>
        faction switch
        {
            ShipFaction.GalacticUnion => "GUV- Galactic Union",
            ShipFaction.Kordani => "KDF- Kordani Defense",
            ShipFaction.Brengop => "BMC- Brengop Consortium",
            ShipFaction.Krushvak => "KRW- Krushvak War-Fleet",
            ShipFaction.Orun => "ORN- Orun Registry",
            ShipFaction.Tolvak => "TLV- Tolvak Transit",
            ShipFaction.Prunjun => "PRJ- Prunjun Hack-Job",
            ShipFaction.FreeTraders => "FTV- Free Traders (HBR-)",
            ShipFaction.FarLineTransit => "FTC- Far-Line Transit Corp",
            ShipFaction.MiningGuild => "MNG- Mining Guild",
            ShipFaction.ZenithInterstellar => "ZNI- Zenith Interstellar",
            _ => "Unknown"
        };

    public static ShipFaction ParseFactionFromPrefix(string seedOrPrefix)
    {
        if (string.IsNullOrWhiteSpace(seedOrPrefix))
            return ShipFaction.GalacticUnion;

        string s = seedOrPrefix.Trim().ToUpperInvariant();
        if (s.StartsWith("GUV-")) return ShipFaction.GalacticUnion;
        if (s.StartsWith("KDF-")) return ShipFaction.Kordani;
        if (s.StartsWith("BMC-")) return ShipFaction.Brengop;
        if (s.StartsWith("KRW-")) return ShipFaction.Krushvak;
        if (s.StartsWith("ORN-")) return ShipFaction.Orun;
        if (s.StartsWith("TLV-")) return ShipFaction.Tolvak;
        if (s.StartsWith("PRJ-")) return ShipFaction.Prunjun;
        if (s.StartsWith("FTV-") || s.StartsWith("HBR-")) return ShipFaction.FreeTraders;
        if (s.StartsWith("FTC-")) return ShipFaction.FarLineTransit;
        if (s.StartsWith("MNG-")) return ShipFaction.MiningGuild;
        if (s.StartsWith("ZNI-")) return ShipFaction.ZenithInterstellar;
        return ShipFaction.GalacticUnion;
    }

    /// <summary>
    /// Checks assets/images/scripted/ ONLY when options.IsScriptedOverride == true.
    /// </summary>
    public static bool TryGetScriptedOverrideTexture(ShipRenderOptions options, out Texture2D texture)
    {
        texture = default;
        if (!options.IsScriptedOverride || string.IsNullOrWhiteSpace(options.ScriptedAssetName))
            return false;

        string relPath = Path.Combine("scripted", options.ScriptedAssetName);
        return AssetManager.TryGetTexture(relPath, out texture);
    }

    public static PixelBuffer RenderPlaceholderBuffer(string seedId, bool spsMode = false)
    {
        ShipFaction faction = ParseFactionFromPrefix(seedId);
        ShipClass defaultClass = NormalizeShipClass(faction, ShipClass.Civilian);
        return RenderBuffer(new ShipRenderOptions
        {
            SeedId = seedId,
            Faction = faction,
            RequestedClass = defaultClass,
            SpsEnabled = spsMode
        });
    }

    /// <summary>
    /// Main entry point rendering a 120x56 left-facing starship buffer across Live, Manual, and SPS modes.
    /// </summary>
    public static PixelBuffer RenderBuffer(ShipRenderOptions options) =>
        RenderBuffer(options, out _);

    /// <summary>
    /// Main entry point rendering a 120x56 left-facing starship buffer and returning the dynamic
    /// ShipHardpointLayout in 120x56 ship-local coordinates for exact SPS reticle hardpoint locking.
    /// </summary>
    public static PixelBuffer RenderBuffer(ShipRenderOptions options, out ShipHardpointLayout hardpoints)
    {
        DeterministicRng rng = new(options.SeedId);
        ShipClass effectiveClass = NormalizeShipClass(options.Faction, options.RequestedClass);
        bool effectiveDecomm = IsDecommissionedEffective(
            options.Faction,
            effectiveClass,
            options.IsDecommissioned,
            options.IsScriptedOverride);

        // 1. Build isolated transparent 120x56 ship silhouette buffer + dynamic hardpoint metadata
        PixelBuffer shipBuf = new(Constants.ShipWidth, Constants.ShipHeight);
        hardpoints = new();

        BuildShipHullGeometry(
            shipBuf,
            hardpoints,
            options.Faction,
            effectiveClass,
            effectiveDecomm,
            options.HasDownstreamHullScorch,
            rng,
            out string engineGlowHex);

        // Automatically compute a tight shield envelope around the generated class silhouette
        ComputeDynamicShieldEnvelope(shipBuf, hardpoints);

        // Apply crisp 4-neighbor 1px dark outline (#080b10) around the ship silhouette
        shipBuf.ApplyOutline(Palette.HexOutlineVoid);

        // 2. Mode Branch: Inspection Manual Recognition Chart (CRT Amber silhouette)
        if (options.Mode == ShipRenderMode.Manual)
        {
            return ComposeManualChartBuffer(shipBuf);
        }

        // 3. Mode Branch: Live Viewport or SPS Subspace Power-Grid Scanner Layer
        // Leave background strictly transparent (alpha = 0 outside the 1px hull outline & shield envelope)
        // so the Desk viewport's deep space and arcing Brown Dwarf sky render cleanly behind every vessel.
        PixelBuffer viewportBuf = new(Constants.ShipWidth, Constants.ShipHeight);

        int animBucket = options.EffectiveAnimBucket;
        bool enginePulseBright = (animBucket % 2) == 0;
        bool navStrobeOn = ((animBucket / 2) % 2) == 0;

        if (options.SpsEnabled)
        {
            // Block 5: Subspace Power-Grid Scanner (SPS) Viewport Render Layer
            ComposeSpsViewportLayer(
                viewportBuf,
                shipBuf,
                hardpoints,
                options,
                animBucket);
            return viewportBuf;
        }

        // Standard Live Viewport Mode:
        // Apply Brown Dwarf underside crimson rim lighting (#7a2b26) along bottom edge
        shipBuf.ApplyRimLight(Palette.HexBrownDwarfGlowRim, "bottom");

        // Pulsing Warp Pylon engine glow
        foreach (var eg in hardpoints.EngineGlowRects)
        {
            string glowCol = enginePulseBright
                ? engineGlowHex
                : Dither.BayerDither(eg.X, eg.Y, Palette.HexOutlineVoid, engineGlowHex, 0.65f);
            shipBuf.FillRect(eg.X, eg.Y, eg.W, eg.H, glowCol);
            if (enginePulseBright && eg.X + eg.W < Constants.ShipWidth - 1)
            {
                shipBuf.Set(eg.X + eg.W, eg.Y + eg.H / 2, engineGlowHex);
            }
        }

        // Blinking navigation strobes (floor(tick / 18) % 2)
        if (navStrobeOn)
        {
            for (int i = 0; i < hardpoints.NavStrobes.Count; i++)
            {
                var st = hardpoints.NavStrobes[i];
                string strobeCol = (i % 2 == 0) ? "#f8fafc" : Palette.HexCrtAmberPrimary;
                shipBuf.Set(st.X, st.Y, strobeCol);
            }
        }

        // Visual Violation Tell 1: weaponsHot == true -> pulsing #dc2626 Alert Crimson glow on bow/dorsal weapon mounts
        if (options.WeaponsHot)
        {
            foreach (var wp in hardpoints.WeaponMountPixels)
            {
                shipBuf.FillRect(wp.X - 1, wp.Y - 1, 3, 3, Palette.HexAlertCrimson);
                if (enginePulseBright)
                {
                    shipBuf.Set(wp.X, wp.Y, "#fca5a5");
                }
            }
        }

        // Composite ship onto deep space + Brown Dwarf viewport backdrop
        viewportBuf.BlitFrom(shipBuf);

        // Visual Violation Tell 2: shieldsUp == true -> animated 1px dithered teal/cyan oval shimmer perimeter
        if (options.ShieldsUp)
        {
            DrawLiveShieldPerimeter(viewportBuf, hardpoints.ShieldEnvelope, animBucket);
        }

        return viewportBuf;
    }

    private static void ComputeDynamicShieldEnvelope(PixelBuffer shipBuf, ShipHardpointLayout hp)
    {
        int minX = Constants.ShipWidth - 1;
        int maxX = 0;
        int minY = Constants.ShipHeight - 1;
        int maxY = 0;
        bool any = false;

        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            for (int x = 0; x < Constants.ShipWidth; x++)
            {
                if (shipBuf.HasPixel(x, y))
                {
                    any = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (!any)
        {
            hp.ShieldEnvelope = (60, 28, 54, 23);
            return;
        }

        int cx = (minX + maxX) / 2;
        int cy = (minY + maxY) / 2;
        int rx = Math.Clamp((maxX - minX) / 2 + 6, 26, 56);
        int ry = Math.Clamp((maxY - minY) / 2 + 5, 14, 25);
        hp.ShieldEnvelope = (cx, cy, rx, ry);
    }

    private static void BuildShipHullGeometry(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipFaction faction,
        ShipClass shipClass,
        bool isDecommissioned,
        bool hasHullScorch,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        switch (faction)
        {
            case ShipFaction.GalacticUnion:
                BuildGalacticUnionShip(buf, hp, shipClass, isDecommissioned, rng, out engineGlowHex);
                break;
            case ShipFaction.Kordani:
                BuildKordaniShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.Brengop:
                BuildBrengopShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.Krushvak:
                BuildKrushvakShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.Orun:
                BuildOrunShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.Tolvak:
                BuildTolvakShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.Prunjun:
                BuildPrunjunHackJobShip(buf, hp, rng, out engineGlowHex);
                break;
            case ShipFaction.FreeTraders:
                BuildFreeTraderShip(buf, hp, shipClass, hasHullScorch, rng, out engineGlowHex);
                break;
            case ShipFaction.FarLineTransit:
                BuildFarLineTransitShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.MiningGuild:
                BuildMiningGuildShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            case ShipFaction.ZenithInterstellar:
                BuildZenithInterstellarShip(buf, hp, shipClass, rng, out engineGlowHex);
                break;
            default:
                BuildGalacticUnionShip(buf, hp, shipClass, false, rng, out engineGlowHex);
                break;
        }
    }

    // =========================================================================
    // 1. GALACTIC UNION (GUV-) — 4 Distinct Macro Class Silhouettes:
    //    - MIL: Compact fused armored saucer-escort (no thin neck), stubby tucked pylons
    //    - CIV: Short ~68px shuttle/runabout pod with two short underslung pylons
    //    - CRG: Blunt nose, long central white spine carrying blocky modular pods, wide-set rear pylons
    //    - DIP: Sleek long-necked explorer cruiser with swept dorsal fin & long outrigger pylons
    // =========================================================================
    private static void BuildGalacticUnionShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        bool isDecommissioned,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] primaryRamp = isDecommissioned ? Palette.GuDecommissionedWhiteRamp : Palette.GuPrimaryWhiteRamp;
        string[] secondaryRamp = Palette.GuSecondaryGunmetalRamp;
        engineGlowHex = isDecommissioned ? Palette.HexGuDecommissionedEngineGlow : Palette.HexGuEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // MIL: Thick, compact armored nose, heavy forward saucer fused directly into the body (no thin neck),
                // twin stubby pylons tucked tight near the hull (length ~80px: x = 14..94)
                ReadOnlySpan<(int X, int Y)> fusedBody =
                [
                    (14, 24), (24, 17), (78, 17), (90, 22), (90, 34), (78, 39), (24, 39), (14, 32)
                ];
                buf.FillPoly(fusedBody, (px, py) =>
                {
                    float t = Math.Clamp(0.92f - MathF.Abs(py - 26) / 14.0f, 0.16f, 0.96f);
                    return Dither.SampleRamp(px, py, primaryRamp, t);
                });

                // Heavy forward saucer fused directly into the armored hull
                buf.FillEllipse(32, 28, 17, 10, (px, py, dx, dy, dist) =>
                {
                    float t = Math.Clamp(0.95f - dist * 0.45f - dy * 0.25f - dx * 0.1f, 0.18f, 0.98f);
                    return Dither.SampleRamp(px, py, primaryRamp, t);
                });

                // Thick gunmetal armored nose cowling & twin pulse cannon emitters on bow (left)
                buf.FillRect(12, 22, 6, 3, secondaryRamp[1]);
                buf.FillRect(12, 31, 6, 3, secondaryRamp[1]);
                buf.FillRect(15, 25, 10, 6, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.70f));

                // Armored tactical bridge dome & gunmetal dorsal plating
                buf.FillEllipse(38, 19, 8, 3, (px, py, _, _, dist) =>
                    Dither.SampleRamp(px, py, secondaryRamp, 0.85f - dist * 0.4f));
                buf.DrawLine(20, 28, 84, 28, secondaryRamp[2]);

                // Twin stubby warp pylons tucked tight near the hull (x = 62..94)
                buf.FillRect(62, 11, 32, 5, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.82f));
                buf.FillRect(64, 12, 28, 3, primaryRamp[2]);
                buf.DrawLine(68, 13, 88, 13, engineGlowHex);

                buf.FillRect(62, 40, 32, 5, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.55f));
                buf.FillRect(64, 41, 28, 3, primaryRamp[1]);
                buf.DrawLine(68, 42, 88, 42, engineGlowHex);

                hp.ReactorCore = (54, 28);
                hp.ForwardWeapons = (14, 24);
                hp.WarpPylons = (80, 13);
                hp.BridgeComms = (38, 19);
                hp.CargoBay = (60, 34);
                hp.WeaponMountPixels.Add((12, 23));
                hp.WeaponMountPixels.Add((12, 32));
                hp.NavStrobes.Add((38, 16));
                hp.NavStrobes.Add((92, 11));
                hp.NavStrobes.Add((92, 44));
                hp.EngineGlowRects.Add((94, 12, 3, 3));
                hp.EngineGlowRects.Add((94, 41, 3, 3));
                break;
            }

            case ShipClass.Civilian:
            {
                // CIV: Compact ~68px shuttle/runabout pod (x = 24..92) with two short underslung pylons
                ReadOnlySpan<(int X, int Y)> runaboutPod =
                [
                    (24, 29),
                    (31, 20),
                    (78, 19),
                    (88, 25),
                    (88, 33),
                    (78, 35),
                    (28, 35)
                ];
                buf.FillPoly(runaboutPod, (px, py) =>
                {
                    float t = Math.Clamp(0.95f - (py - 19) / 18.0f, 0.18f, 0.98f);
                    return Dither.SampleRamp(px, py, primaryRamp, t);
                });

                // Curved wrap-around runabout cockpit window on left bow
                ReadOnlySpan<(int X, int Y)> canopy =
                [
                    (26, 28), (32, 22), (43, 22), (41, 28)
                ];
                buf.FillPoly(canopy, (px, py) => Dither.BayerDither(px, py, secondaryRamp[0], "#38bdf8", 0.55f));
                buf.DrawLine(31, 23, 39, 23, "#e0f2fe");

                // Compact dorsal telemetry fin & gunmetal hull stripe
                ReadOnlySpan<(int X, int Y)> tailFin = [(66, 19), (74, 14), (82, 14), (80, 19)];
                buf.FillPoly(tailFin, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.78f));
                buf.FillRect(30, 30, 54, 2, secondaryRamp[2]);

                // Two short underslung warp pylons mounted below the runabout belly (x = 56..92)
                buf.DrawLine(58, 35, 64, 40, secondaryRamp[1]);
                buf.DrawLine(74, 35, 78, 40, secondaryRamp[1]);
                buf.FillRect(56, 37, 32, 4, primaryRamp[2]);
                buf.DrawLine(60, 39, 84, 39, engineGlowHex);

                buf.FillRect(60, 42, 32, 4, secondaryRamp[2]);
                buf.DrawLine(64, 44, 88, 44, engineGlowHex);

                if (isDecommissioned)
                {
                    ApplyGuDecommissionedWeathering(buf, rng);
                }

                hp.ReactorCore = (58, 28);
                hp.ForwardWeapons = (26, 31);
                hp.WarpPylons = (76, 40);
                hp.BridgeComms = (35, 24);
                hp.CargoBay = (68, 31);
                hp.WeaponMountPixels.Add((25, 31));
                hp.NavStrobes.Add((75, 14));
                hp.NavStrobes.Add((88, 37));
                hp.NavStrobes.Add((90, 45));
                hp.EngineGlowRects.Add((88, 38, 2, 2));
                hp.EngineGlowRects.Add((92, 43, 2, 2));
                break;
            }

            case ShipClass.Cargo:
            {
                // CRG: Stretched ~106px across (x = 8..114): Blunt nose, long central white spine
                // carrying 3 blocky gunmetal/white modular hull pods, rear pylons set wide apart
                // Wide-set splayed diagonal pylon struts at stern
                ReadOnlySpan<(int X, int Y)> topStrut = [(74, 20), (80, 20), (92, 9), (86, 9)];
                ReadOnlySpan<(int X, int Y)> botStrut = [(74, 32), (80, 32), (92, 47), (86, 47)];
                buf.FillPoly(topStrut, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.72f));
                buf.FillPoly(botStrut, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.40f));

                // Long central white spine (x = 22..106, y = 20..29)
                buf.FillRect(22, 20, 84, 9, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.94f - (py - 20) / 10.0f));
                buf.DrawLine(24, 24, 102, 24, secondaryRamp[2]);

                // Blunt utility nose on LEFT (x = 8..26, y = 18..34) + recessed utilitarian bridge
                ReadOnlySpan<(int X, int Y)> bluntBow =
                [
                    (8, 22), (14, 18), (26, 18), (26, 34), (14, 34), (8, 30)
                ];
                buf.FillPoly(bluntBow, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.90f - (py - 18) / 18.0f));
                buf.FillRect(8, 23, 4, 6, secondaryRamp[1]);
                buf.FillRect(16, 15, 9, 4, secondaryRamp[2]);
                buf.FillRect(17, 16, 4, 2, "#38bdf8");

                // 3 blocky gunmetal/white modular cargo hull pods clamped beneath the spine (y = 29..43)
                int[] podStarts = [28, 48, 68];
                for (int i = 0; i < podStarts.Length; i++)
                {
                    int px0 = podStarts[i];
                    string[] podRamp = (i % 2 == 0) ? secondaryRamp : primaryRamp;
                    buf.FillRect(px0, 29, 17, 13, (px, py) =>
                        Dither.SampleRamp(px, py, podRamp, 0.80f - (py - 29) / 15.0f));
                    buf.DrawLine(px0 + 4, 29, px0 + 4, 41, secondaryRamp[0]);
                    buf.DrawLine(px0 + 12, 29, px0 + 12, 41, secondaryRamp[0]);
                }

                // Rear warp pylons set wide apart (y = 6..10 and y = 46..50, x = 76..112)
                buf.FillRect(76, 6, 36, 5, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.92f));
                buf.DrawLine(80, 8, 108, 8, engineGlowHex);

                buf.FillRect(76, 45, 36, 5, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.70f));
                buf.DrawLine(80, 47, 108, 47, engineGlowHex);

                if (isDecommissioned)
                {
                    ApplyGuDecommissionedWeathering(buf, rng);
                }

                hp.ReactorCore = (64, 24);
                hp.ForwardWeapons = (10, 26);
                hp.WarpPylons = (96, 8);
                hp.BridgeComms = (20, 17);
                hp.CargoBay = (56, 36);
                hp.WeaponMountPixels.Add((9, 24));
                hp.WeaponMountPixels.Add((9, 29));
                hp.NavStrobes.Add((20, 15));
                hp.NavStrobes.Add((110, 6));
                hp.NavStrobes.Add((110, 49));
                hp.EngineGlowRects.Add((112, 7, 2, 3));
                hp.EngineGlowRects.Add((112, 46, 2, 3));
                break;
            }

            default: // ShipClass.Diplomatic (Sleek Long-Necked Explorer Cruiser)
            {
                // Swept diagonal pylon struts at stern (right side x = 68..86)
                ReadOnlySpan<(int X, int Y)> upperStrut = [(64, 22), (70, 22), (82, 12), (76, 12)];
                ReadOnlySpan<(int X, int Y)> lowerStrut = [(64, 34), (70, 34), (82, 44), (76, 44)];
                buf.FillPoly(upperStrut, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.65f));
                buf.FillPoly(lowerStrut, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.35f));

                // Long slender connecting neck + tapered engineering fuselage (x = 36..96)
                buf.FillRect(36, 25, 18, 6, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.75f));
                ReadOnlySpan<(int X, int Y)> fuselage =
                [
                    (50, 21), (86, 21), (96, 28), (86, 35), (50, 35)
                ];
                buf.FillPoly(fuselage, (px, py) =>
                {
                    float t = Math.Clamp(0.92f - MathF.Abs(py - 26) / 11.0f, 0.15f, 0.96f);
                    return Dither.SampleRamp(px, py, primaryRamp, t);
                });

                // Sleek swept dorsal diplomatic fin
                ReadOnlySpan<(int X, int Y)> dorsalFin = [(52, 21), (66, 13), (74, 13), (72, 21)];
                buf.FillPoly(dorsalFin, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.88f));

                // Elongated graceful forward saucer bow on LEFT (cx = 26, rx = 18, ry = 7)
                buf.FillEllipse(26, 28, 18, 7, (px, py, dx, dy, dist) =>
                {
                    float t = Math.Clamp(0.96f - dist * 0.45f - dy * 0.30f - dx * 0.10f, 0.18f, 0.98f);
                    return Dither.SampleRamp(px, py, primaryRamp, t);
                });

                // Panoramic diplomatic bridge dome on saucer top
                buf.FillEllipse(28, 21, 7, 3, (px, py, _, _, dist) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.96f - dist * 0.3f));
                buf.DrawLine(11, 28, 41, 28, "#38bdf8");

                // Long horizontal Warp Pylons above and below stern (x = 74..112)
                buf.FillRect(74, 9, 38, 5, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.90f - (py - 9) / 6.0f));
                buf.FillRect(74, 42, 38, 5, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.72f - (py - 42) / 6.0f));
                buf.DrawLine(80, 11, 108, 11, engineGlowHex);
                buf.DrawLine(80, 44, 108, 44, engineGlowHex);

                hp.ReactorCore = (66, 28);
                hp.ForwardWeapons = (10, 28);
                hp.WarpPylons = (94, 11);
                hp.BridgeComms = (28, 20);
                hp.CargoBay = (68, 33);
                hp.WeaponMountPixels.Add((10, 27));
                hp.WeaponMountPixels.Add((10, 29));
                hp.NavStrobes.Add((28, 19));
                hp.NavStrobes.Add((110, 9));
                hp.NavStrobes.Add((110, 46));
                hp.EngineGlowRects.Add((112, 10, 2, 3));
                hp.EngineGlowRects.Add((112, 43, 2, 3));
                break;
            }
        }
    }

    private static void ApplyGuDecommissionedWeathering(PixelBuffer buf, DeterministicRng rng)
    {
        bool[] hullMask = buf.CaptureNonNullMask(excludeOutline: true);
        DeterministicRng weatherRng = rng.Fork("gu_decomm");

        // Overlay 2-4 seeded dark rectangular hull-patch polygons (#3d444d) with 1px rivet lines (#1c2026)
        int patchCount = weatherRng.NextInt(2, 5);
        for (int i = 0; i < patchCount; i++)
        {
            int px = weatherRng.NextInt(22, 82);
            int py = weatherRng.NextInt(19, 39);
            int pw = weatherRng.NextInt(6, 12);
            int ph = weatherRng.NextInt(3, 6);

            for (int y = py; y < py + ph; y++)
            {
                for (int x = px; x < px + pw; x++)
                {
                    if (buf.IsMasked(hullMask, x, y))
                    {
                        bool isRivetEdge = (x == px || x == px + pw - 1) && ((y - py) % 2 == 0);
                        buf.Set(x, y, isRivetEdge ? "#1c2026" : "#3d444d");
                    }
                }
            }
        }

        // Bayer-dithered carbon-scorch streaks across the aft hull & warp pylons
        for (int y = 6; y < Constants.ShipHeight - 4; y++)
        {
            for (int x = 72; x < 110; x++)
            {
                if (buf.IsMasked(hullMask, x, y) && ((y % 5 == 0) || y <= 14 || y >= 37))
                {
                    float scorchT = Math.Clamp((x - 72) / 34.0f, 0.1f, 0.75f);
                    buf.Set(x, y, Dither.BayerDither(x, y, buf.Get(x, y) ?? "#4e565e", "#1c2026", scorchT));
                }
            }
        }
    }

    // =========================================================================
    // 2. KORDANI (KDF-) — Military & Diplomatic ONLY:
    //    - MIL: Compact, wide, aggressive jagged spearhead with heavier forward blades & serrated dorsal spikes
    //    - DIP: Stretched razor-thin needle-nosed command yacht with smooth swept-back wings & tall regal crest
    // =========================================================================
    private static void BuildKordaniShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] primaryRamp = Palette.KordaniPrimaryBlueGreenRamp;
        string[] secondaryRamp = Palette.KordaniSecondaryDarkSlateRamp;
        engineGlowHex = Palette.HexKordaniEngineGlow;

        if (shipClass == ShipClass.Diplomatic)
        {
            // DIP: Razor-thin, needle-nosed command yacht stretched across x = 6..114
            // Zero serrated battle spikes; smooth swept-back rear wings and tall regal dorsal crest
            ReadOnlySpan<(int X, int Y)> sweptTopWing =
            [
                (52, 24), (88, 9), (108, 9), (94, 24)
            ];
            ReadOnlySpan<(int X, int Y)> sweptBotWing =
            [
                (52, 32), (88, 47), (108, 47), (94, 32)
            ];
            buf.FillPoly(sweptTopWing, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.82f));
            buf.FillPoly(sweptBotWing, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.38f));

            // Tall regal dorsal command crest
            ReadOnlySpan<(int X, int Y)> regalCrest =
            [
                (30, 24), (44, 7), (56, 7), (52, 24)
            ];
            buf.FillPoly(regalCrest, (px, py) =>
                Dither.SampleRamp(px, py, primaryRamp, 0.95f - (py - 7) / 20.0f));
            buf.DrawLine(45, 9, 53, 9, Palette.HexKordaniEngineGlow);

            // Razor-thin needle-nosed fuselage (y = 23..33, x = 6..112)
            ReadOnlySpan<(int X, int Y)> needleHull =
            [
                (6, 28), (32, 23), (102, 23), (112, 28), (102, 33), (32, 33)
            ];
            buf.FillPoly(needleHull, (px, py) =>
            {
                float t = Math.Clamp(0.95f - MathF.Abs(py - 27) / 7.0f, 0.20f, 0.98f);
                return Dither.SampleRamp(px, py, primaryRamp, t);
            });

            // Royal amber command stripe along needle spine & sleek rear warp slits
            buf.DrawLine(10, 28, 106, 28, Palette.HexKordaniEngineGlow);
            buf.FillRect(88, 12, 22, 3, primaryRamp[2]);
            buf.FillRect(88, 41, 22, 3, primaryRamp[1]);

            hp.ReactorCore = (66, 28);
            hp.ForwardWeapons = (10, 28);
            hp.WarpPylons = (98, 13);
            hp.BridgeComms = (46, 14);
            hp.CargoBay = (74, 31);
            hp.WeaponMountPixels.Add((8, 28));
            hp.NavStrobes.Add((48, 7));
            hp.NavStrobes.Add((108, 12));
            hp.NavStrobes.Add((108, 43));
            hp.EngineGlowRects.Add((110, 12, 3, 2));
            hp.EngineGlowRects.Add((111, 27, 3, 2));
            hp.EngineGlowRects.Add((110, 42, 3, 2));
        }
        else
        {
            // MIL: Compact, wide, aggressive jagged spearhead (x = 10..96) with heavy forward-swept blades
            ReadOnlySpan<(int X, int Y)> heavyUpperBlade =
            [
                (15, 8), (44, 11), (66, 21), (34, 21)
            ];
            ReadOnlySpan<(int X, int Y)> heavyLowerBlade =
            [
                (15, 48), (44, 45), (66, 35), (34, 35)
            ];
            buf.FillPoly(heavyUpperBlade, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.78f));
            buf.FillPoly(heavyLowerBlade, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.35f));

            // Shorter, chunkier, muscular spearhead body (x = 10..96, y = 17..39)
            ReadOnlySpan<(int X, int Y)> strikeHull =
            [
                (10, 28), (28, 17), (88, 17), (96, 23), (90, 28), (96, 33), (88, 39), (28, 39)
            ];
            buf.FillPoly(strikeHull, (px, py) =>
            {
                float t = Math.Clamp(0.92f - MathF.Abs(py - 26) / 14.0f, 0.14f, 0.96f);
                return Dither.SampleRamp(px, py, primaryRamp, t);
            });

            // Serrated dorsal battle spikes (x = 42..78)
            for (int sx = 42; sx <= 74; sx += 8)
            {
                ReadOnlySpan<(int X, int Y)> spike = [(sx, 17), (sx + 3, 11), (sx + 6, 17)];
                buf.FillPoly(spike, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.85f));
            }

            // Angular armor chevrons & tucked-in rear warp slits (x = 74..94)
            buf.DrawLine(18, 28, 36, 19, secondaryRamp[1]);
            buf.DrawLine(18, 28, 36, 37, secondaryRamp[1]);
            buf.FillRect(74, 14, 20, 4, secondaryRamp[1]);
            buf.FillRect(74, 38, 20, 4, secondaryRamp[1]);

            hp.ReactorCore = (56, 28);
            hp.ForwardWeapons = (13, 28);
            hp.WarpPylons = (84, 16);
            hp.BridgeComms = (36, 19);
            hp.CargoBay = (58, 35);
            hp.WeaponMountPixels.Add((11, 28));
            hp.WeaponMountPixels.Add((16, 9));
            hp.WeaponMountPixels.Add((16, 47));
            hp.NavStrobes.Add((45, 11));
            hp.NavStrobes.Add((92, 14));
            hp.NavStrobes.Add((92, 41));
            hp.EngineGlowRects.Add((94, 15, 3, 2));
            hp.EngineGlowRects.Add((95, 22, 3, 3));
            hp.EngineGlowRects.Add((95, 31, 3, 3));
            hp.EngineGlowRects.Add((94, 39, 3, 2));
        }
    }

    // =========================================================================
    // 3. BRENGOP (BMC-) — All 4 Classes with Distinct Macro Silhouettes:
    //    - MIL: Chunky blunted Kordani-style military frame in gross yellow
    //    - CIV: Short ~66px asymmetric industrial utility shuttle with rear-top cab
    //    - CRG: Full-112px low-cab heavy slab barge with 4 deep ventral ore hoppers
    //    - DIP: Stepped 3-tier Guild Factor Ziggurat Barge with brass prow bumper
    // =========================================================================
    private static void BuildBrengopShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] primaryRamp = Palette.BrengopPrimaryGrossYellowRamp;
        string[] secondaryRamp = Palette.BrengopSecondaryGrimyBrassRamp;
        engineGlowHex = Palette.HexBrengopEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // MIL: Blunted, chunkier Kordani angular military silhouette painted gross yellow (x = 12..98)
                ReadOnlySpan<(int X, int Y)> topWing = [(20, 10), (46, 12), (66, 20), (34, 20)];
                ReadOnlySpan<(int X, int Y)> botWing = [(20, 46), (46, 44), (66, 36), (34, 36)];
                buf.FillPoly(topWing, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.75f));
                buf.FillPoly(botWing, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.35f));

                ReadOnlySpan<(int X, int Y)> chunkyHull =
                [
                    (12, 24), (30, 16), (90, 16), (98, 22), (94, 28), (98, 34), (90, 40), (30, 40), (12, 32)
                ];
                buf.FillPoly(chunkyHull, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.90f - MathF.Abs(py - 26) / 16.0f));

                // Bolted grimy-brass armor strips & chunky rear warp boxes
                for (int rx = 24; rx <= 84; rx += 12)
                {
                    buf.DrawLine(rx, 17, rx, 39, secondaryRamp[1]);
                    buf.Set(rx + 1, 20, secondaryRamp[3]);
                    buf.Set(rx + 1, 36, secondaryRamp[3]);
                }
                buf.FillRect(74, 13, 22, 5, secondaryRamp[2]);
                buf.FillRect(74, 38, 22, 5, secondaryRamp[1]);

                hp.ReactorCore = (56, 28);
                hp.ForwardWeapons = (14, 28);
                hp.WarpPylons = (86, 15);
                hp.BridgeComms = (36, 19);
                hp.CargoBay = (58, 35);
                hp.WeaponMountPixels.Add((12, 25));
                hp.WeaponMountPixels.Add((12, 31));
                hp.NavStrobes.Add((34, 16));
                hp.NavStrobes.Add((94, 13));
                hp.NavStrobes.Add((94, 42));
                hp.EngineGlowRects.Add((96, 14, 3, 3));
                hp.EngineGlowRects.Add((96, 39, 3, 3));
                break;
            }

            case ShipClass.Civilian:
            {
                // CIV: Visibly shorter (~66px: x = 25..91) asymmetric stubby industrial shuttle with tall rear-top cab
                buf.FillRect(25, 22, 4, 13, secondaryRamp[2]);
                ReadOnlySpan<(int X, int Y)> stubbyBody =
                [
                    (28, 21), (86, 21), (90, 26), (90, 35), (28, 36)
                ];
                buf.FillPoly(stubbyBody, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.88f - (py - 21) / 16.0f));

                // Tall asymmetric rear-top control cab (x = 56..82, y = 13..21)
                buf.FillRect(56, 13, 24, 8, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.92f));
                buf.FillRect(58, 15, 7, 3, Palette.HexCrtAmberPrimary);

                // Single underslung grimy-brass warp thruster box (x = 64..91, y = 36..42)
                buf.FillRect(64, 36, 27, 6, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.70f));

                hp.ReactorCore = (56, 28);
                hp.ForwardWeapons = (27, 28);
                hp.WarpPylons = (78, 39);
                hp.BridgeComms = (64, 16);
                hp.CargoBay = (42, 31);
                hp.WeaponMountPixels.Add((25, 28));
                hp.NavStrobes.Add((68, 13));
                hp.NavStrobes.Add((89, 39));
                hp.EngineGlowRects.Add((90, 27, 2, 5));
                hp.EngineGlowRects.Add((91, 37, 2, 4));
                break;
            }

            case ShipClass.Cargo:
            {
                // CRG: Stretched ~108px (x = 6..114) heavy slab-sided bulk ore barge with low-slung bow cab & 4 deep ventral hoppers
                // Low-slung forward tug cab on LEFT (x = 6..22, y = 26..39)
                buf.FillRect(6, 26, 16, 13, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.78f));
                buf.FillRect(8, 28, 5, 3, Palette.HexCrtAmberPrimary);

                // Long heavy girder deck (x = 20..108, y = 19..34)
                buf.FillRect(20, 19, 88, 15, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.90f - (py - 19) / 16.0f));

                // 4 large trapezoidal ore hoppers bulging beneath the belly (y = 34..47)
                for (int hx = 24; hx <= 78; hx += 18)
                {
                    ReadOnlySpan<(int X, int Y)> hopper =
                    [
                        (hx, 34), (hx + 15, 34), (hx + 12, 47), (hx + 3, 47)
                    ];
                    buf.FillPoly(hopper, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.60f));
                }

                // Wide twin rear thruster blocks (x = 84..112)
                buf.FillRect(84, 12, 28, 7, secondaryRamp[2]);
                buf.FillRect(84, 35, 28, 7, secondaryRamp[1]);

                hp.ReactorCore = (64, 26);
                hp.ForwardWeapons = (8, 33);
                hp.WarpPylons = (98, 15);
                hp.BridgeComms = (13, 30);
                hp.CargoBay = (50, 41);
                hp.WeaponMountPixels.Add((6, 33));
                hp.NavStrobes.Add((12, 26));
                hp.NavStrobes.Add((110, 12));
                hp.NavStrobes.Add((110, 41));
                hp.EngineGlowRects.Add((112, 13, 2, 5));
                hp.EngineGlowRects.Add((108, 24, 3, 6));
                hp.EngineGlowRects.Add((112, 36, 2, 5));
                break;
            }

            default: // ShipClass.Diplomatic (Stepped 3-Tier Guild Factor Ziggurat Barge)
            {
                // Base ziggurat hull (x = 10..110, y = 24..38)
                ReadOnlySpan<(int X, int Y)> baseTier =
                [
                    (10, 31), (18, 24), (104, 24), (110, 31), (104, 38), (16, 38)
                ];
                buf.FillPoly(baseTier, (px, py) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.88f - (py - 24) / 16.0f));

                // Mid ziggurat tier (x = 24..94, y = 16..24)
                buf.FillRect(24, 16, 70, 8, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.82f));
                // Top Factor observation citadel (x = 38..76, y = 9..16)
                buf.FillRect(38, 9, 38, 7, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.95f));
                buf.FillRect(42, 11, 28, 3, Palette.HexCrtAmberPrimary);

                // Elongated dual rear warp outriggers (x = 76..112)
                buf.FillRect(76, 12, 36, 4, primaryRamp[2]);
                buf.FillRect(76, 40, 36, 4, primaryRamp[1]);

                hp.ReactorCore = (60, 30);
                hp.ForwardWeapons = (13, 31);
                hp.WarpPylons = (96, 14);
                hp.BridgeComms = (54, 12);
                hp.CargoBay = (64, 35);
                hp.WeaponMountPixels.Add((11, 31));
                hp.NavStrobes.Add((56, 9));
                hp.NavStrobes.Add((110, 12));
                hp.NavStrobes.Add((110, 43));
                hp.EngineGlowRects.Add((112, 13, 2, 2));
                hp.EngineGlowRects.Add((110, 29, 2, 4));
                hp.EngineGlowRects.Add((112, 41, 2, 2));
                break;
            }
        }
    }

    // =========================================================================
    // 4. KRUSHVAK (KRW-) — Military & Cargo ONLY:
    //    - MIL: Lean, low, predatory raider with sharp horned pincer prow, sleek torso & angled rear pylons
    //    - CRG: Heavy, bloated green ribbed caterpillar hauler with blunt flat bow & chunky underside barges
    // =========================================================================
    private static void BuildKrushvakShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] primaryRamp = Palette.KrushvakPrimaryOliveGreenRamp;
        string[] secondaryRamp = Palette.KrushvakSecondaryEarthBrownRamp;
        engineGlowHex = Palette.HexKrushvakEngineGlow;

        if (shipClass == ShipClass.Cargo)
        {
            // CRG: Heavy, bloated green ribbed caterpillar hull stretched across x = 8..112, taller/boxier (y = 11..47)
            // Blunt flat bow (no pincer jaw!) and chunky underside ore barges
            for (int seg = 0; seg < 5; seg++)
            {
                int sx = 24 + seg * 16;
                buf.FillEllipse(sx + 8, 25, 9, 13, (px, py, _, dy, _) =>
                    Dither.SampleRamp(px, py, primaryRamp, 0.92f - (dy + 1.0f) * 0.38f));
                // Earth-brown iron rib bands separating caterpillar segments
                buf.FillRect(sx, 14, 3, 24, secondaryRamp[1]);
                buf.DrawLine(sx + 1, 14, sx + 1, 37, secondaryRamp[3]);
            }

            // Blunt flat armored bow on LEFT (x = 8..24, y = 15..38)
            buf.FillRect(8, 17, 16, 20, (px, py) =>
                Dither.SampleRamp(px, py, secondaryRamp, 0.85f - (py - 17) / 22.0f));
            buf.FillRect(12, 19, 4, 4, Palette.HexCrtAmberPrimary);

            // Chunky underside ore/supply barges along belly (x = 26..90, y = 36..47)
            buf.FillRect(26, 36, 28, 11, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.65f));
            buf.FillRect(58, 36, 28, 11, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.60f));

            // Heavy double-stacked boxy rear warp thrusters (x = 90..112)
            buf.FillRect(90, 12, 22, 9, secondaryRamp[2]);
            buf.FillRect(90, 23, 20, 10, primaryRamp[1]);
            buf.FillRect(90, 35, 22, 9, secondaryRamp[1]);

            hp.ReactorCore = (62, 25);
            hp.ForwardWeapons = (10, 27);
            hp.WarpPylons = (100, 16);
            hp.BridgeComms = (15, 21);
            hp.CargoBay = (56, 41);
            hp.WeaponMountPixels.Add((8, 27));
            hp.NavStrobes.Add((16, 15));
            hp.NavStrobes.Add((110, 12));
            hp.NavStrobes.Add((110, 43));
            hp.EngineGlowRects.Add((112, 14, 3, 5));
            hp.EngineGlowRects.Add((110, 26, 3, 4));
            hp.EngineGlowRects.Add((112, 37, 3, 5));
        }
        else
        {
            // MIL: Lean, low, and predatory raider (x = 8..102) — sharp horned prow, sleeker torso, angled rear pylons
            // Angled canted rear warp pylons
            ReadOnlySpan<(int X, int Y)> topAngledPylon = [(66, 22), (82, 11), (102, 11), (92, 22)];
            ReadOnlySpan<(int X, int Y)> botAngledPylon = [(66, 34), (82, 45), (102, 45), (92, 34)];
            buf.FillPoly(topAngledPylon, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.85f));
            buf.FillPoly(botAngledPylon, (px, py) => Dither.SampleRamp(px, py, secondaryRamp, 0.50f));

            // Sleek low predatory torso (x = 26..96, y = 21..35)
            ReadOnlySpan<(int X, int Y)> leanTorso =
            [
                (26, 21), (90, 21), (98, 28), (90, 35), (26, 35)
            ];
            buf.FillPoly(leanTorso, (px, py) =>
                Dither.SampleRamp(px, py, primaryRamp, 0.92f - MathF.Abs(py - 27) / 10.0f));

            // Sharp horned / forward-swept curved assault blades on LEFT bow (x = 8..34)
            ReadOnlySpan<(int X, int Y)> upperHorn = [(8, 18), (22, 15), (36, 21), (22, 25)];
            ReadOnlySpan<(int X, int Y)> lowerHorn = [(8, 38), (22, 41), (36, 35), (22, 31)];
            buf.FillPoly(upperHorn, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.90f));
            buf.FillPoly(lowerHorn, (px, py) => Dither.SampleRamp(px, py, primaryRamp, 0.60f));

            // Low dorsal armor ribs
            for (int rx = 38; rx <= 74; rx += 9)
            {
                buf.DrawLine(rx, 21, rx - 3, 35, secondaryRamp[0]);
                buf.DrawLine(rx + 1, 21, rx - 2, 35, primaryRamp[3]);
            }

            hp.ReactorCore = (58, 28);
            hp.ForwardWeapons = (12, 19);
            hp.WarpPylons = (90, 14);
            hp.BridgeComms = (34, 23);
            hp.CargoBay = (64, 32);
            hp.WeaponMountPixels.Add((9, 18));
            hp.WeaponMountPixels.Add((9, 38));
            hp.WeaponMountPixels.Add((22, 28));
            hp.NavStrobes.Add((24, 15));
            hp.NavStrobes.Add((100, 11));
            hp.NavStrobes.Add((100, 44));
            hp.EngineGlowRects.Add((101, 12, 3, 3));
            hp.EngineGlowRects.Add((98, 26, 3, 4));
            hp.EngineGlowRects.Add((101, 41, 3, 3));
        }
    }

    // =========================================================================
    // 5. ORUN (ORN-) — Cargo, Civilian, Diplomatic ONLY:
    //    - CRG: Long spine with transparent glass cargo bays and visible interior crates
    //    - CIV: Short ~68px boxy granite utility tug/shuttle (no glass cargo bays) with single cabin window band
    //    - DIP: Symmetrical double-deck rectangular archive barge (no cargo bays) with tall raised glass dome
    // =========================================================================
    private static void BuildOrunShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] graniteRamp = Palette.OrunPrimaryGraniteRamp;
        string[] glassRamp = Palette.OrunGlassContainerRamp;
        engineGlowHex = "#38bdf8";

        switch (shipClass)
        {
            case ShipClass.Civilian:
            {
                // CIV: Short, boxy utility tug / personnel shuttle (~68px long: x = 24..92)
                // Zero glass cargo bays; single small illuminated cabin window band + twin vertical-strut rear pylons
                ReadOnlySpan<(int X, int Y)> tugHull =
                [
                    (24, 25), (30, 19), (82, 19), (82, 37), (30, 37), (24, 31)
                ];
                buf.FillPoly(tugHull, (px, py) =>
                {
                    float t = py < 24 ? 0.90f : (py > 32 ? 0.30f : 0.65f);
                    return Dither.SampleRamp(px, py, graniteRamp, t);
                });

                // Brass bridge stripe & single illuminated cabin window band (x = 28..48, y = 24..26)
                buf.DrawLine(25, 28, 80, 28, Palette.HexOrunBrassTrim);
                buf.FillRect(29, 23, 18, 3, "#38bdf8");
                buf.DrawLine(30, 23, 44, 23, glassRamp[3]);

                // Vertical granite struts + short rear warp pylons (x = 72..92)
                buf.FillRect(74, 15, 4, 26, graniteRamp[1]);
                buf.FillRect(70, 12, 22, 4, graniteRamp[2]);
                buf.FillRect(70, 40, 22, 4, graniteRamp[1]);

                hp.ReactorCore = (58, 28);
                hp.ForwardWeapons = (26, 28);
                hp.WarpPylons = (82, 14);
                hp.BridgeComms = (36, 24);
                hp.CargoBay = (62, 33);
                hp.NavStrobes.Add((32, 19));
                hp.NavStrobes.Add((90, 12));
                hp.NavStrobes.Add((90, 43));
                hp.EngineGlowRects.Add((92, 13, 2, 2));
                hp.EngineGlowRects.Add((92, 41, 2, 2));
                break;
            }

            case ShipClass.Diplomatic:
            {
                // DIP: Clean symmetrical double-deck rectangular archive barge (x = 10..112)
                // Solid plated mid-section (no cargo bays) with tall raised glass observation dome on top & longer twin pylons
                ReadOnlySpan<(int X, int Y)> archiveBarge =
                [
                    (10, 25), (18, 18), (96, 18), (102, 24), (102, 32), (96, 38), (18, 38), (10, 31)
                ];
                buf.FillPoly(archiveBarge, (px, py) =>
                {
                    float t = py < 23 ? 0.88f : (py > 33 ? 0.28f : 0.62f);
                    return Dither.SampleRamp(px, py, graniteRamp, t);
                });

                // Ceremonial brass stripe & solid chiseled monolith relief panels along mid-section
                buf.DrawLine(12, 28, 98, 28, Palette.HexOrunBrassTrim);
                for (int px = 28; px <= 76; px += 14)
                {
                    buf.FillRect(px, 21, 10, 6, graniteRamp[2]);
                    buf.FillRect(px, 29, 10, 6, graniteRamp[1]);
                }

                // Tall raised faceted glass observation dome on top (x = 36..68, y = 9..18)
                ReadOnlySpan<(int X, int Y)> glassDome =
                [
                    (36, 18), (42, 9), (62, 9), (68, 18)
                ];
                buf.FillPoly(glassDome, (px, py) =>
                    Dither.BayerDither(px, py, "#1e3a5f", "#38bdf8", 0.55f));
                buf.DrawLine(42, 16, 48, 10, glassRamp[3]);
                buf.DrawLine(52, 16, 58, 10, glassRamp[3]);
                buf.DrawLine(36, 18, 68, 18, Palette.HexOrunBrassTrim);

                // Vertical struts + longer twin rear warp pylons (x = 74..112)
                buf.FillRect(84, 13, 5, 30, graniteRamp[1]);
                buf.FillRect(74, 9, 38, 5, graniteRamp[2]);
                buf.FillRect(74, 42, 38, 5, graniteRamp[1]);

                hp.ReactorCore = (60, 28);
                hp.ForwardWeapons = (13, 28);
                hp.WarpPylons = (96, 11);
                hp.BridgeComms = (52, 13);
                hp.CargoBay = (64, 34);
                hp.NavStrobes.Add((52, 9));
                hp.NavStrobes.Add((110, 9));
                hp.NavStrobes.Add((110, 46));
                hp.EngineGlowRects.Add((112, 10, 2, 3));
                hp.EngineGlowRects.Add((112, 43, 2, 3));
                break;
            }

            default: // ShipClass.Cargo (Long Spine with Transparent Glass Cargo Bays & Visible Interior Crates)
            {
                // Long strictly symmetrical heavy granite girder spine (x = 10..106, y = 24..32)
                buf.FillRect(22, 24, 82, 8, (px, py) =>
                    Dither.SampleRamp(px, py, graniteRamp, 0.90f - MathF.Abs(py - 27) / 6.0f));

                // Faceted polygonal stone bow on LEFT (x = 10..28)
                ReadOnlySpan<(int X, int Y)> facetedBow =
                [
                    (10, 26), (18, 19), (28, 19), (28, 37), (18, 37), (10, 30)
                ];
                buf.FillPoly(facetedBow, (px, py) =>
                {
                    float t = py < 24 ? 0.90f : (py > 32 ? 0.30f : 0.65f);
                    return Dither.SampleRamp(px, py, graniteRamp, t);
                });
                buf.DrawLine(12, 28, 26, 28, Palette.HexOrunBrassTrim);

                // Symmetrical rear geometric warp blocks on vertical struts (x = 86..110)
                buf.FillRect(88, 18, 6, 20, graniteRamp[1]);
                buf.FillRect(86, 11, 24, 7, (px, py) => Dither.SampleRamp(px, py, graniteRamp, 0.80f));
                buf.FillRect(86, 38, 24, 7, (px, py) => Dither.SampleRamp(px, py, graniteRamp, 0.55f));

                // 3 pairs of large rectangular CLEAR/TRANSPARENT cargo bays along midsection (x = 30..84)
                string[] crateColors = ["#ffb84d", "#2dd4bf", "#cc6b35", "#a855f7", "#8ab06c", "#38bdf8"];
                for (int b = 0; b < 3; b++)
                {
                    int bx = 31 + b * 18;
                    DrawClearGlassCargoContainer(buf, bx, 13, 15, 11, glassRamp, crateColors, rng.Fork($"ubay_{b}"));
                    DrawClearGlassCargoContainer(buf, bx, 32, 15, 11, glassRamp, crateColors, rng.Fork($"lbay_{b}"));
                }

                hp.ReactorCore = (62, 28);
                hp.ForwardWeapons = (13, 28);
                hp.WarpPylons = (98, 14);
                hp.BridgeComms = (20, 22);
                hp.CargoBay = (56, 37);
                hp.NavStrobes.Add((18, 19));
                hp.NavStrobes.Add((108, 11));
                hp.NavStrobes.Add((108, 44));
                hp.EngineGlowRects.Add((110, 13, 2, 3));
                hp.EngineGlowRects.Add((110, 40, 2, 3));
                break;
            }
        }
    }

    private static void DrawClearGlassCargoContainer(
        PixelBuffer buf,
        int x,
        int y,
        int w,
        int h,
        string[] glassRamp,
        string[] crateColors,
        DeterministicRng rng)
    {
        buf.FillRect(x, y, w, h, "#0c141d");

        int cursorX = x + 2;
        while (cursorX + 3 < x + w - 1)
        {
            int cw = rng.NextInt(3, 5);
            int ch1 = rng.NextInt(3, 5);
            string c1 = rng.Pick(crateColors);
            buf.FillRect(cursorX, y + h - 1 - ch1, cw, ch1, c1);

            if (rng.NextBool(0.75f))
            {
                int ch2 = rng.NextInt(2, 4);
                string c2 = rng.Pick(crateColors);
                buf.FillRect(cursorX, y + h - 1 - ch1 - ch2, cw, ch2, c2);
            }
            cursorX += cw + 1;
        }

        buf.DrawLine(x, y, x + w - 1, y, glassRamp[2]);
        buf.DrawLine(x, y + h - 1, x + w - 1, y + h - 1, glassRamp[2]);
        buf.DrawLine(x, y, x, y + h - 1, glassRamp[1]);
        buf.DrawLine(x + w - 1, y, x + w - 1, y + h - 1, glassRamp[1]);

        buf.DrawLine(x + 2, y + h - 3, x + 6, y + 2, glassRamp[3]);
        if (w >= 14)
        {
            buf.DrawLine(x + 8, y + h - 3, x + 12, y + 2, glassRamp[3]);
        }
    }

    // =========================================================================
    // 6. TOLVAK (TLV-) — Civilian & Cargo ONLY:
    //    - CIV: Compact ~68px single-sail desert-skiff courier (x = 22..90)
    //    - CRG: Stretched ~106px heavy caravan barge with 3 deep belly pods & twin radiator arrays
    // =========================================================================
    private static void BuildTolvakShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] ramp = Palette.TolvakPrimaryTerracottaRamp;
        engineGlowHex = "#f59e0b";

        if (shipClass == ShipClass.Civilian)
        {
            // CIV: Compact ~68px desert-skiff courier (x = 22..90) with a single tall swept dorsal radiator sail
            ReadOnlySpan<(int X, int Y)> tallSail = [(44, 26), (62, 10), (74, 10), (66, 26)];
            buf.FillPoly(tallSail, (px, py) => Dither.SampleRamp(px, py, ramp, 0.85f));
            for (int ry = 13; ry <= 23; ry += 2)
            {
                buf.DrawLine(54, ry, 66, ry, ramp[0]);
            }

            ReadOnlySpan<(int X, int Y)> compactSkiff =
            [
                (22, 33), (36, 25), (86, 25), (90, 30), (86, 36), (26, 36)
            ];
            buf.FillPoly(compactSkiff, (px, py) =>
                Dither.SampleRamp(px, py, ramp, 0.94f - (py - 25) / 13.0f));

            buf.FillRect(68, 29, 22, 4, ramp[2]);
            buf.DrawLine(70, 31, 88, 31, Palette.HexCrtAmberPrimary);

            hp.ReactorCore = (58, 31);
            hp.ForwardWeapons = (25, 33);
            hp.WarpPylons = (80, 31);
            hp.BridgeComms = (38, 27);
            hp.CargoBay = (62, 34);
            hp.WeaponMountPixels.Add((23, 33));
            hp.NavStrobes.Add((64, 10));
            hp.NavStrobes.Add((88, 30));
            hp.EngineGlowRects.Add((90, 30, 2, 3));
        }
        else
        {
            // CRG: Stretched ~106px heavy caravan barge (x = 8..114) with twin low radiator fins & 3 bulky underside canisters
            ReadOnlySpan<(int X, int Y)> foreFin = [(32, 23), (44, 14), (58, 14), (54, 23)];
            ReadOnlySpan<(int X, int Y)> aftFin = [(62, 23), (76, 13), (92, 13), (86, 23)];
            buf.FillPoly(foreFin, (px, py) => Dither.SampleRamp(px, py, ramp, 0.80f));
            buf.FillPoly(aftFin, (px, py) => Dither.SampleRamp(px, py, ramp, 0.75f));

            ReadOnlySpan<(int X, int Y)> heavyBarge =
            [
                (8, 30), (18, 22), (104, 22), (110, 29), (106, 37), (12, 37)
            ];
            buf.FillPoly(heavyBarge, (px, py) =>
                Dither.SampleRamp(px, py, ramp, 0.94f - (py - 22) / 17.0f));

            // 3 deep rounded terracotta cargo canisters slung below the flat keel (y = 36..46)
            int[] pods = [32, 54, 76];
            foreach (int cx in pods)
            {
                buf.FillEllipse(cx, 41, 9, 5, (px, py, _, dy, _) =>
                    Dither.SampleRamp(px, py, ramp, 0.72f - dy * 0.25f));
            }

            // Dual rear caravan warp nacelles (x = 84..112)
            buf.FillRect(84, 18, 28, 5, ramp[2]);
            buf.FillRect(84, 37, 28, 5, ramp[1]);

            hp.ReactorCore = (62, 29);
            hp.ForwardWeapons = (11, 30);
            hp.WarpPylons = (98, 20);
            hp.BridgeComms = (22, 25);
            hp.CargoBay = (54, 41);
            hp.WeaponMountPixels.Add((9, 30));
            hp.NavStrobes.Add((46, 14));
            hp.NavStrobes.Add((110, 18));
            hp.NavStrobes.Add((110, 41));
            hp.EngineGlowRects.Add((112, 19, 2, 3));
            hp.EngineGlowRects.Add((112, 38, 2, 3));
        }
    }

    // =========================================================================
    // 7. PRUNJUN (PRJ-) — Rigid Procedural "Hack-Job" Assembly:
    //    Mismatched Factions A/B/C + Mandatory Round Bubble Cockpit + Clipped Painted Flames
    // =========================================================================
    private static void BuildPrunjunHackJobShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[][] donorRamps =
        [
            Palette.GuPrimaryWhiteRamp,
            Palette.KordaniPrimaryBlueGreenRamp,
            Palette.BrengopPrimaryGrossYellowRamp,
            Palette.KrushvakPrimaryOliveGreenRamp,
            Palette.OrunPrimaryGraniteRamp
        ];

        int factionA = rng.NextInt(0, donorRamps.Length);
        int factionB = (factionA + rng.NextInt(1, donorRamps.Length)) % donorRamps.Length;
        int factionC = (factionA + rng.NextInt(1, donorRamps.Length)) % donorRamps.Length;
        if (factionC == factionB)
            factionC = (factionC + 1) % donorRamps.Length;

        string[] coreRamp = donorRamps[factionA];
        string[] bowRamp = donorRamps[factionB];
        string[] sternRamp = donorRamps[factionC];
        engineGlowHex = "#f97316";

        // Draw Core Hull (x = 32..84) from Faction A
        ReadOnlySpan<(int X, int Y)> corePoly =
        [
            (32, 18), (84, 17), (86, 39), (32, 40)
        ];
        buf.FillPoly(corePoly, (px, py) =>
            Dither.SampleRamp(px, py, coreRamp, 0.90f - (py - 17) / 24.0f));

        // Draw Bow Mount on LEFT (x = 10..36) from Faction B
        ReadOnlySpan<(int X, int Y)> bowPoly = factionB switch
        {
            1 => [(10, 28), (34, 19), (34, 38)],
            3 => [(11, 20), (34, 16), (34, 40), (11, 36), (16, 28)],
            _ => [(12, 23), (24, 19), (35, 19), (35, 38), (24, 38), (12, 34)]
        };
        buf.FillPoly(bowPoly, (px, py) =>
            Dither.SampleRamp(px, py, bowRamp, 0.88f - (py - 18) / 22.0f));

        // Draw Stern Warp Pylons on RIGHT (x = 74..108) from Faction C
        buf.FillRect(76, 11, 32, 6, (px, py) =>
            Dither.SampleRamp(px, py, sternRamp, 0.85f - (py - 11) / 7.0f));
        buf.FillRect(74, 39, 34, 7, (px, py) =>
            Dither.SampleRamp(px, py, sternRamp, 0.65f - (py - 39) / 8.0f));

        buf.DrawLine(33, 18, 35, 39, "#181a1c");
        buf.DrawLine(34, 18, 36, 39, "#8c5832");
        buf.DrawLine(78, 17, 78, 39, "#181a1c");

        bool[] hullMask = buf.CaptureNonNullMask(excludeOutline: true);
        bool MaskFn(int px, int py) => buf.IsMasked(hullMask, px, py);

        // Mandatory Lock 2: Clipped Painted Flames
        int[] flameY = [22, 27, 32, 36];
        int[] flameLen = [38, 46, 42, 34];
        for (int i = 0; i < flameY.Length; i++)
        {
            int fy = flameY[i];
            int flen = flameLen[i];
            ReadOnlySpan<(int X, int Y)> flameTongue =
            [
                (12, fy - 2),
                (12 + flen, fy),
                (12, fy + 3)
            ];
            buf.FillClippedPoly(flameTongue, MaskFn, (px, py) =>
            {
                float t = Math.Clamp(1.0f - (px - 12) / (float)flen, 0.05f, 0.98f);
                return Dither.SampleRamp(px, py, Palette.PrunjunFlameRamp, t);
            });

            buf.FillClippedEllipse(18 + i * 5, fy, 8, 2, MaskFn,
                (px, py, _, _, dist) => Dither.SampleRamp(px, py, Palette.PrunjunFlameRamp, 0.95f - dist * 0.6f));
        }

        // Mandatory Lock 1: Prominent circular bubble cockpit dome (fillEllipse(cx, cy, 7, 7))
        const int domeCx = 33;
        const int domeCy = 16;
        buf.FillEllipse(domeCx, domeCy, 7, 7, (px, py, dx, dy, dist) =>
        {
            float t = Math.Clamp(0.92f - dist * 0.65f - dx * 0.2f - dy * 0.2f, 0.10f, 0.98f);
            return Dither.SampleRamp(px, py, Palette.PrunjunCockpitGlassRamp, t);
        });
        buf.DrawLine(domeCx - 4, domeCy - 3, domeCx - 1, domeCy - 5, "#e0f2fe");
        buf.Set(domeCx - 4, domeCy - 2, "#e0f2fe");
        buf.FillRect(domeCx - 7, domeCy + 5, 15, 2, "#8c5832");

        hp.ReactorCore = (60, 28);
        hp.ForwardWeapons = (14, 28);
        hp.WarpPylons = (92, 14);
        hp.BridgeComms = (domeCx, domeCy);
        hp.CargoBay = (58, 35);
        hp.WeaponMountPixels.Add((12, 24));
        hp.WeaponMountPixels.Add((12, 33));
        hp.NavStrobes.Add((domeCx, domeCy - 7));
        hp.NavStrobes.Add((106, 11));
        hp.NavStrobes.Add((106, 45));
        hp.EngineGlowRects.Add((108, 12, 3, 4));
        hp.EngineGlowRects.Add((108, 41, 3, 4));
    }

    // =========================================================================
    // 8. INDEPENDENT FREE TRADERS (FTV-) & HUN-BORIAN (HBR-) — All 4 Classes:
    //    - MIL: Compact snub-nosed wedge Mercenary Gunboat with twin forward gun barrels & tucked pylons
    //    - CIV: Compact ~68px asymmetrical Tramp Shuttle with top-left offset bridge pod & single underslung pylon
    //    - CRG: Stretched ~104px Modular Tramp Hauler spine with 3 mismatched ventral cargo crates
    //    - DIP: Stretched ~106px Refitted Private Yacht dart with tall swept gull-wings & long sensor nosecone
    // =========================================================================
    private static void BuildFreeTraderShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        bool hasHullScorch,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] slateRamp = Palette.FreeTraderPrimarySlateRamp;
        string[] rustRamp = Palette.FreeTraderTrimRustRamp;
        engineGlowHex = Palette.HexFreeTraderEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // MIL: Compact snub-nosed wedge gunboat (x = 10..94)
                buf.FillRect(10, 23, 12, 2, slateRamp[3]);
                buf.FillRect(10, 31, 12, 2, slateRamp[2]);

                ReadOnlySpan<(int X, int Y)> wedge =
                [
                    (18, 20), (34, 15), (84, 15), (92, 28), (84, 41), (34, 41), (18, 36)
                ];
                buf.FillPoly(wedge, (px, py) =>
                    Dither.SampleRamp(px, py, slateRamp, 0.90f - (py - 15) / 28.0f));

                buf.FillEllipse(36, 15, 7, 3, (px, py, _, _, _) => Dither.SampleRamp(px, py, rustRamp, 0.78f));
                ReadOnlySpan<(int X, int Y)> stubFin = [(58, 15), (64, 10), (74, 10), (76, 15)];
                buf.FillPoly(stubFin, (px, py) => Dither.SampleRamp(px, py, rustRamp, 0.82f));

                buf.FillRect(42, 26, 36, 4, rustRamp[2]);
                buf.FillRect(70, 12, 24, 4, slateRamp[2]);
                buf.FillRect(70, 40, 24, 4, slateRamp[1]);

                hp.ReactorCore = (56, 28);
                hp.ForwardWeapons = (12, 24);
                hp.WarpPylons = (82, 14);
                hp.BridgeComms = (36, 16);
                hp.CargoBay = (56, 36);
                hp.WeaponMountPixels.Add((10, 24));
                hp.WeaponMountPixels.Add((10, 32));
                hp.NavStrobes.Add((66, 10));
                hp.NavStrobes.Add((92, 12));
                hp.EngineGlowRects.Add((94, 13, 3, 2));
                hp.EngineGlowRects.Add((94, 41, 3, 2));
                break;
            }

            case ShipClass.Civilian:
            {
                // CIV: Compact ~68px Tramp Runner / Shuttle (x = 22..90) with top-left offset bridge & single underslung pylon
                buf.DrawLine(18, 27, 26, 27, slateRamp[3]);
                buf.DrawLine(20, 25, 20, 29, rustRamp[2]);

                buf.FillEllipse(56, 28, 30, 8, (px, py, _, dy, _) =>
                    Dither.SampleRamp(px, py, slateRamp, 0.88f - dy * 0.35f));

                // Asymmetrical offset bridge pod on top-left
                buf.FillRect(28, 16, 16, 6, (px, py) => Dither.SampleRamp(px, py, rustRamp, 0.82f));
                buf.FillRect(30, 18, 5, 2, Palette.HexCrtAmberPrimary);

                // Single underslung rear warp pylon (x = 58..90, y = 36..41)
                buf.FillRect(58, 36, 32, 5, slateRamp[2]);
                buf.DrawLine(62, 38, 86, 38, rustRamp[2]);

                hp.ReactorCore = (56, 28);
                hp.ForwardWeapons = (26, 28);
                hp.WarpPylons = (76, 38);
                hp.BridgeComms = (35, 19);
                hp.CargoBay = (64, 31);
                hp.WeaponMountPixels.Add((25, 28));
                hp.NavStrobes.Add((34, 16));
                hp.NavStrobes.Add((88, 38));
                hp.EngineGlowRects.Add((90, 37, 2, 3));
                break;
            }

            case ShipClass.Diplomatic:
            {
                // DIP: Stretched ~106px Refitted Private Yacht dart (x = 6..112) with dramatic swept mid-wings
                buf.DrawLine(6, 28, 18, 28, rustRamp[3]);
                ReadOnlySpan<(int X, int Y)> dartHull =
                [
                    (14, 28), (34, 21), (100, 21), (110, 28), (100, 35), (34, 35)
                ];
                buf.FillPoly(dartHull, (px, py) =>
                    Dither.SampleRamp(px, py, slateRamp, 0.92f - MathF.Abs(py - 26) / 10.0f));

                ReadOnlySpan<(int X, int Y)> topWing = [(44, 21), (72, 8), (84, 8), (68, 21)];
                ReadOnlySpan<(int X, int Y)> botWing = [(44, 35), (72, 48), (84, 48), (68, 35)];
                buf.FillPoly(topWing, (px, py) => Dither.SampleRamp(px, py, rustRamp, 0.85f));
                buf.FillPoly(botWing, (px, py) => Dither.SampleRamp(px, py, rustRamp, 0.55f));

                buf.DrawLine(24, 27, 102, 27, rustRamp[3]);
                buf.DrawLine(24, 29, 102, 29, rustRamp[2]);

                hp.ReactorCore = (62, 28);
                hp.ForwardWeapons = (16, 28);
                hp.WarpPylons = (98, 26);
                hp.BridgeComms = (36, 23);
                hp.CargoBay = (74, 31);
                hp.WeaponMountPixels.Add((15, 28));
                hp.NavStrobes.Add((76, 8));
                hp.NavStrobes.Add((108, 28));
                hp.EngineGlowRects.Add((110, 26, 3, 4));
                break;
            }

            default: // ShipClass.Cargo (Stretched ~104px Modular Tramp Hauler)
            {
                buf.FillRect(22, 18, 84, 8, (px, py) =>
                    Dither.SampleRamp(px, py, slateRamp, 0.88f - (py - 18) / 9.0f));

                ReadOnlySpan<(int X, int Y)> cab =
                [
                    (8, 22), (16, 17), (28, 17), (28, 33), (14, 33), (8, 28)
                ];
                buf.FillPoly(cab, (px, py) => Dither.SampleRamp(px, py, slateRamp, 0.85f - (py - 17) / 17.0f));
                buf.FillRect(11, 22, 4, 2, Palette.HexCrtAmberPrimary);

                // 3 mismatched cargo crates clamped underneath
                buf.FillRect(30, 26, 17, 15, (px, py) => Dither.SampleRamp(px, py, rustRamp, 0.75f));
                buf.FillRect(49, 26, 18, 17, (px, py) => Dither.SampleRamp(px, py, Palette.BrengopSecondaryGrimyBrassRamp, 0.70f));
                buf.FillRect(69, 26, 16, 14, (px, py) => Dither.SampleRamp(px, py, slateRamp, 0.55f));

                buf.FillRect(86, 13, 24, 6, slateRamp[2]);
                buf.FillRect(86, 35, 24, 6, slateRamp[1]);

                hp.ReactorCore = (60, 22);
                hp.ForwardWeapons = (11, 25);
                hp.WarpPylons = (98, 16);
                hp.BridgeComms = (18, 20);
                hp.CargoBay = (58, 35);
                hp.WeaponMountPixels.Add((9, 25));
                hp.NavStrobes.Add((18, 17));
                hp.NavStrobes.Add((108, 13));
                hp.EngineGlowRects.Add((110, 14, 2, 4));
                hp.EngineGlowRects.Add((110, 36, 2, 4));
                break;
            }
        }

        if (hasHullScorch)
        {
            bool[] mask = buf.CaptureNonNullMask(excludeOutline: true);
            buf.FillClippedEllipse(48, 26, 9, 4, (px, py) => buf.IsMasked(mask, px, py),
                (px, py, _, _, _) => Dither.BayerDither(px, py, "#080b10", "#3d2410", 0.45f));
        }
    }

    // =========================================================================
    // 9. FAR-LINE TRANSIT CORP (FTC-) — Cargo & Civilian ONLY:
    //    - CIV: Compact ~72px Passenger Commuter Sleeper Tube (x = 20..92) with lit cabin windows
    //    - CRG: Stretched ~108px Deep-Space Colony Ark (x = 6..114) with 8 huge spherical fuel/cryo pods
    // =========================================================================
    private static void BuildFarLineTransitShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] creamRamp = Palette.FarLinePrimaryCreamRamp;
        string[] navyRamp = Palette.FarLineAccentNavyRamp;
        engineGlowHex = Palette.HexFarLineEngineGlow;

        if (shipClass == ShipClass.Civilian)
        {
            // CIV: Visibly compact ~72px Passenger Sleeper Shuttle (x = 20..92)
            buf.DrawLine(16, 28, 22, 28, navyRamp[3]);
            buf.DrawLine(18, 24, 18, 32, creamRamp[3]);

            ReadOnlySpan<(int X, int Y)> sleeperTube =
            [
                (21, 23), (28, 19), (84, 19), (90, 28), (84, 37), (28, 37), (21, 33)
            ];
            buf.FillPoly(sleeperTube, (px, py) =>
                Dither.SampleRamp(px, py, creamRamp, 0.92f - MathF.Abs(py - 26) / 11.0f));

            // Deep Space Navy waist band & 2 rows of 1px amber lit passenger windows
            buf.FillRect(26, 25, 54, 6, navyRamp[2]);
            for (int wx = 29; wx <= 77; wx += 3)
            {
                buf.Set(wx, 26, Palette.HexCrtAmberPrimary);
                buf.Set(wx, 29, Palette.HexCrtAmberPrimary);
            }

            // Compact twin rear endurance warp pylons (x = 64..94)
            buf.FillRect(64, 13, 30, 3, creamRamp[2]);
            buf.FillRect(64, 40, 30, 3, creamRamp[1]);
            buf.DrawLine(58, 19, 66, 14, navyRamp[2]);
            buf.DrawLine(58, 37, 66, 41, navyRamp[2]);

            hp.ReactorCore = (56, 28);
            hp.ForwardWeapons = (22, 28);
            hp.WarpPylons = (80, 14);
            hp.BridgeComms = (30, 21);
            hp.CargoBay = (58, 34);
            hp.WeaponMountPixels.Add((21, 28));
            hp.NavStrobes.Add((18, 24));
            hp.NavStrobes.Add((92, 13));
            hp.NavStrobes.Add((92, 42));
            hp.EngineGlowRects.Add((94, 13, 2, 3));
            hp.EngineGlowRects.Add((94, 40, 2, 3));
        }
        else
        {
            // CRG: Full-width ~108px Deep-Space Colony Ark (x = 6..114)
            buf.FillEllipse(58, 28, 48, 9, (px, py, _, dy, _) =>
                Dither.SampleRamp(px, py, creamRamp, 0.92f - MathF.Abs(dy) * 0.55f));

            ReadOnlySpan<(int X, int Y)> noseShield =
            [
                (8, 21), (20, 17), (22, 39), (8, 35)
            ];
            buf.FillPoly(noseShield, (px, py) => Dither.SampleRamp(px, py, navyRamp, 0.80f - (py - 17) / 24.0f));

            int[] podX = [32, 48, 64, 80];
            foreach (int pxCenter in podX)
            {
                buf.FillEllipse(pxCenter, 16, 6, 5, (px, py, _, _, dist) =>
                    Dither.SampleRamp(px, py, creamRamp, 0.90f - dist * 0.5f));
                buf.DrawLine(pxCenter, 11, pxCenter, 21, navyRamp[2]);

                buf.FillEllipse(pxCenter, 40, 6, 5, (px, py, _, _, dist) =>
                    Dither.SampleRamp(px, py, creamRamp, 0.75f - dist * 0.5f));
                buf.DrawLine(pxCenter, 35, pxCenter, 45, navyRamp[1]);
            }

            buf.FillRect(88, 9, 24, 4, navyRamp[2]);
            buf.FillRect(90, 15, 22, 4, creamRamp[2]);
            buf.FillRect(90, 37, 22, 4, creamRamp[1]);
            buf.FillRect(88, 43, 24, 4, navyRamp[1]);

            hp.ReactorCore = (60, 28);
            hp.ForwardWeapons = (11, 28);
            hp.WarpPylons = (100, 11);
            hp.BridgeComms = (24, 22);
            hp.CargoBay = (64, 40);
            hp.WeaponMountPixels.Add((9, 24));
            hp.WeaponMountPixels.Add((9, 32));
            hp.NavStrobes.Add((18, 17));
            hp.NavStrobes.Add((110, 9));
            hp.NavStrobes.Add((110, 46));
            hp.EngineGlowRects.Add((112, 10, 2, 2));
            hp.EngineGlowRects.Add((112, 16, 2, 2));
            hp.EngineGlowRects.Add((112, 38, 2, 2));
            hp.EngineGlowRects.Add((112, 44, 2, 2));
        }
    }

    // =========================================================================
    // 10. MINING GUILD (MNG-) — Cargo & Military ONLY:
    //     - MIL: Compact muscular Armored Claim-Guard Frigate brick with twin heavy turrets & hazard ramming prow
    //     - CRG: Full-width open-truss Industrial Ore Excavator Rig with giant lower-bow drill jaws & ore hoppers
    // =========================================================================
    private static void BuildMiningGuildShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] ironRamp = Palette.MiningGuildPrimaryCharcoalRamp;
        string[] hazardRamp = Palette.MiningGuildTrimHazardYellowRamp;
        engineGlowHex = Palette.HexMiningGuildEngineGlow;

        if (shipClass == ShipClass.Military)
        {
            // MIL: Compact, slab-sided Armored Claim-Guard Frigate (x = 12..96)
            ReadOnlySpan<(int X, int Y)> brick =
            [
                (12, 19), (18, 15), (90, 15), (94, 20), (94, 36), (90, 41), (18, 41), (12, 37)
            ];
            buf.FillPoly(brick, (px, py) =>
                Dither.SampleRamp(px, py, ironRamp, 0.88f - (py - 15) / 28.0f));

            // Reinforced ramming prow with diagonal yellow-and-black hazard stripes (x = 12..24)
            for (int y = 19; y <= 37; y++)
            {
                for (int x = 12; x <= 23; x++)
                {
                    bool yellowStripe = (((x + y) / 3) % 2) == 0;
                    buf.Set(x, y, yellowStripe ? hazardRamp[3] : ironRamp[0]);
                }
            }

            // Twin dorsal & ventral heavy turret blocks with forward gun barrels pointing LEFT
            buf.FillRect(34, 10, 14, 5, ironRamp[2]);
            buf.FillRect(26, 11, 8, 2, hazardRamp[2]);
            buf.FillRect(34, 41, 14, 5, ironRamp[1]);
            buf.FillRect(26, 43, 8, 2, hazardRamp[2]);

            // Tucked-in shielded boxy warp pylons (x = 68..96)
            buf.FillRect(68, 11, 26, 5, ironRamp[2]);
            buf.FillRect(68, 40, 26, 5, ironRamp[1]);
            buf.DrawLine(70, 13, 90, 13, hazardRamp[2]);
            buf.DrawLine(70, 42, 90, 42, hazardRamp[2]);

            hp.ReactorCore = (56, 28);
            hp.ForwardWeapons = (28, 12);
            hp.WarpPylons = (82, 13);
            hp.BridgeComms = (28, 20);
            hp.CargoBay = (58, 35);
            hp.WeaponMountPixels.Add((26, 12));
            hp.WeaponMountPixels.Add((26, 43));
            hp.WeaponMountPixels.Add((12, 28));
            hp.NavStrobes.Add((38, 10));
            hp.NavStrobes.Add((92, 11));
            hp.EngineGlowRects.Add((94, 12, 3, 3));
            hp.EngineGlowRects.Add((94, 26, 3, 4));
            hp.EngineGlowRects.Add((94, 41, 3, 3));
        }
        else
        {
            // CRG: Stretched ~106px Open-Truss Industrial Ore Rig (x = 8..112)
            buf.FillRect(22, 13, 84, 7, (px, py) => Dither.SampleRamp(px, py, ironRamp, 0.85f));
            buf.FillRect(32, 34, 74, 10, (px, py) => Dither.SampleRamp(px, py, ironRamp, 0.55f));
            buf.FillRect(82, 10, 22, 28, ironRamp[1]);
            buf.FillRect(84, 13, 6, 3, Palette.HexCrtAmberPrimary);

            // Open truss-frame scaffolding (drawLine cross-braces across x = 34..82, y = 20..34)
            for (int tx = 34; tx < 80; tx += 11)
            {
                buf.DrawLine(tx, 20, tx + 11, 33, ironRamp[3]);
                buf.DrawLine(tx + 11, 20, tx, 33, ironRamp[2]);
                buf.DrawLine(tx, 20, tx, 33, ironRamp[1]);
            }

            // Massive exposed rotary ore-drills mounted on lower bow (LEFT x = 8..30, y = 24..45)
            ReadOnlySpan<(int X, int Y)> topDrill = [(8, 29), (20, 24), (30, 24), (30, 33), (20, 33)];
            ReadOnlySpan<(int X, int Y)> botDrill = [(8, 40), (20, 35), (30, 35), (30, 45), (20, 45)];
            buf.FillPoly(topDrill, (px, py) => Dither.SampleRamp(px, py, hazardRamp, 0.88f));
            buf.FillPoly(botDrill, (px, py) => Dither.SampleRamp(px, py, hazardRamp, 0.72f));

            // Hazard-striped upper bow bumper plate (x = 14..24, y = 13..23)
            for (int y = 13; y <= 23; y++)
            {
                for (int x = 14; x <= 23; x++)
                {
                    buf.Set(x, y, (((x + y) / 2) % 2 == 0) ? hazardRamp[3] : ironRamp[0]);
                }
            }

            hp.ReactorCore = (88, 26);
            hp.ForwardWeapons = (11, 29);
            hp.WarpPylons = (100, 16);
            hp.BridgeComms = (86, 14);
            hp.CargoBay = (56, 39);
            hp.WeaponMountPixels.Add((9, 29));
            hp.WeaponMountPixels.Add((9, 40));
            hp.NavStrobes.Add((88, 10));
            hp.NavStrobes.Add((104, 13));
            hp.EngineGlowRects.Add((106, 14, 3, 5));
            hp.EngineGlowRects.Add((106, 35, 3, 6));
        }
    }

    // =========================================================================
    // 11. ZENITH INTERSTELLAR (ZNI-) — Diplomatic & Cargo ONLY:
    //     - DIP: Stretched graceful whale-like Flagship Starliner with curved glass promenade & dorsal fin
    //     - CRG: Bulbous teardrop bow + 3 tall vertical enclosed supply cylinders + wide-set twin pods
    // =========================================================================
    private static void BuildZenithInterstellarShip(
        PixelBuffer buf,
        ShipHardpointLayout hp,
        ShipClass shipClass,
        DeterministicRng rng,
        out string engineGlowHex)
    {
        string[] burgundyRamp = Palette.ZenithPrimaryBurgundyRamp;
        string[] pearlRamp = Palette.ZenithPearlWhiteRamp;
        string[] goldRamp = Palette.ZenithTrimPolishedGoldRamp;
        engineGlowHex = Palette.HexZenithEngineGlowLavender;

        if (shipClass == ShipClass.Diplomatic)
        {
            // DIP: Graceful sweeping Starliner yacht (x = 8..112) with dorsal tailfin & panoramic glass promenade
            ReadOnlySpan<(int X, int Y)> tailFin = [(74, 17), (92, 9), (104, 9), (98, 17)];
            buf.FillPoly(tailFin, (px, py) => Dither.SampleRamp(px, py, pearlRamp, 0.90f));

            ReadOnlySpan<(int X, int Y)> starlinerHull =
            [
                (8, 29),
                (18, 18),
                (44, 15),
                (96, 17),
                (110, 25),
                (104, 38),
                (20, 38),
                (10, 34)
            ];
            buf.FillPoly(starlinerHull, (px, py) =>
            {
                if (py < 29)
                {
                    float t = Math.Clamp(0.96f - (py - 15) / 16.0f, 0.25f, 0.98f);
                    return Dither.SampleRamp(px, py, pearlRamp, t);
                }
                else
                {
                    float t = Math.Clamp(0.85f - (py - 29) / 11.0f, 0.15f, 0.90f);
                    return Dither.SampleRamp(px, py, burgundyRamp, t);
                }
            });

            // Curved panoramic glass promenade along upper-left bow
            ReadOnlySpan<(int X, int Y)> glassPromenade =
            [
                (16, 23), (24, 17), (54, 17), (52, 23)
            ];
            buf.FillPoly(glassPromenade, (px, py) =>
                Dither.BayerDither(px, py, "#38bdf8", "#93c5fd", 0.6f));
            buf.DrawLine(22, 19, 48, 19, "#e0f2fe");

            // Polished Gold racing pinstripes
            buf.DrawLine(10, 29, 106, 29, goldRamp[3]);
            buf.DrawLine(12, 30, 104, 30, goldRamp[1]);

            // Recessed elegantly faired warp pylons at stern (x = 78..110)
            buf.FillRect(78, 13, 32, 4, burgundyRamp[2]);
            buf.DrawLine(80, 14, 108, 14, goldRamp[2]);
            buf.FillRect(78, 38, 32, 4, burgundyRamp[1]);
            buf.DrawLine(80, 40, 108, 40, goldRamp[2]);

            hp.ReactorCore = (62, 28);
            hp.ForwardWeapons = (12, 29);
            hp.WarpPylons = (96, 15);
            hp.BridgeComms = (34, 20);
            hp.CargoBay = (68, 34);
            hp.WeaponMountPixels.Add((10, 29));
            hp.NavStrobes.Add((94, 9));
            hp.NavStrobes.Add((108, 13));
            hp.NavStrobes.Add((108, 41));
            hp.EngineGlowRects.Add((110, 14, 3, 2));
            hp.EngineGlowRects.Add((110, 39, 3, 2));
        }
        else
        {
            // CRG: Hospitality & Provision Tender — Plump teardrop bow + 3 tall vertical enclosed supply cylinders
            buf.FillRect(26, 23, 78, 10, (px, py) =>
                Dither.SampleRamp(px, py, burgundyRamp, 0.85f - (py - 23) / 11.0f));

            // Bulbous teardrop bow on LEFT (cx = 26, rx = 15, ry = 11)
            buf.FillEllipse(26, 28, 15, 11, (px, py, _, dy, dist) =>
                Dither.SampleRamp(px, py, pearlRamp, 0.95f - dist * 0.4f - dy * 0.25f));
            buf.FillRect(16, 24, 8, 3, "#38bdf8");

            // 3 tall vertical enclosed pearl-and-burgundy supply cylinders (y = 16..40)
            int[] podCenters = [48, 66, 84];
            foreach (int cx in podCenters)
            {
                buf.FillEllipse(cx, 28, 7, 12, (px, py, _, dy, _) =>
                    py < 28
                        ? Dither.SampleRamp(px, py, pearlRamp, 0.92f - MathF.Abs(dy) * 0.35f)
                        : Dither.SampleRamp(px, py, burgundyRamp, 0.82f - MathF.Abs(dy) * 0.35f));
                buf.DrawLine(cx, 17, cx, 39, goldRamp[2]);
            }

            // Polished gold corporate spine stripe & wide-set rear warp nacelles
            buf.DrawLine(12, 28, 104, 28, goldRamp[3]);
            buf.FillRect(82, 10, 26, 5, pearlRamp[2]);
            buf.FillRect(82, 41, 26, 5, burgundyRamp[2]);

            hp.ReactorCore = (66, 28);
            hp.ForwardWeapons = (13, 28);
            hp.WarpPylons = (96, 12);
            hp.BridgeComms = (22, 24);
            hp.CargoBay = (66, 36);
            hp.WeaponMountPixels.Add((12, 28));
            hp.NavStrobes.Add((26, 17));
            hp.NavStrobes.Add((106, 10));
            hp.NavStrobes.Add((106, 45));
            hp.EngineGlowRects.Add((108, 11, 3, 3));
            hp.EngineGlowRects.Add((108, 42, 3, 3));
        }
    }

    // =========================================================================
    // VIEWPORT BACKDROP, SHIELD PERIMETER, MANUAL CHART, AND SPS RENDER LAYER
    // =========================================================================
    private static void RenderViewportBackdrop(PixelBuffer buf, bool spsMode, DeterministicRng rng)
    {
        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            float ratio = y / (float)Constants.ShipHeight;
            for (int x = 0; x < Constants.ShipWidth; x++)
            {
                string col = spsMode
                    ? Dither.BayerDither(x, y, Palette.HexOutlineVoid, "#09131c", ratio * 0.7f)
                    : Dither.BayerDither(x, y, Palette.HexOutlineVoid, Palette.HexBrownDwarfGlowDeep, ratio);
                buf.Set(x, y, col);
            }
        }

        DeterministicRng starRng = rng.Fork("viewport_stars");
        for (int i = 0; i < 14; i++)
        {
            int sx = starRng.NextInt(2, Constants.ShipWidth - 2);
            int sy = starRng.NextInt(2, Constants.ShipHeight - 2);
            buf.Set(sx, sy, spsMode ? "#1e293b" : Palette.HexBrownDwarfGlowRim);
        }
    }

    private static void DrawLiveShieldPerimeter(
        PixelBuffer buf,
        (int Cx, int Cy, int Rx, int Ry) env,
        int animBucket)
    {
        for (int deg = 0; deg < 360; deg += 3)
        {
            float rad = deg * (MathF.PI / 180.0f);
            int px = env.Cx + (int)MathF.Round(MathF.Cos(rad) * env.Rx);
            int py = env.Cy + (int)MathF.Round(MathF.Sin(rad) * env.Ry);

            if (((deg / 9 + animBucket) % 2) == 0)
            {
                buf.Set(px, py, Palette.HexClearanceTeal);
            }
            else if (((deg / 9 + animBucket) % 3) == 0)
            {
                buf.Set(px, py, Palette.HexSpsShieldArcCyan);
            }
        }
    }

    private static PixelBuffer ComposeManualChartBuffer(PixelBuffer shipBuf)
    {
        PixelBuffer manual = new(Constants.ShipWidth, Constants.ShipHeight);
        manual.Clear(Palette.HexOutlineVoid);

        for (int gx = 10; gx < Constants.ShipWidth; gx += 20)
        {
            for (int gy = 8; gy < Constants.ShipHeight; gy += 16)
            {
                manual.Set(gx, gy, "#3d2006");
            }
        }

        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            for (int x = 0; x < Constants.ShipWidth; x++)
            {
                string? srcHex = shipBuf.Get(x, y);
                if (srcHex == null)
                    continue;

                if (srcHex.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                {
                    manual.Set(x, y, Palette.HexCrtAmberDimmed);
                }
                else
                {
                    float lum = Palette.GetLuminance(srcHex);
                    string amberCol = lum > 0.45f
                        ? Palette.HexCrtAmberPrimary
                        : Dither.BayerDither(x, y, Palette.HexCrtAmberDimmed, Palette.HexCrtAmberPrimary, lum * 1.6f);
                    manual.Set(x, y, amberCol);
                }
            }
        }

        return manual;
    }

    private static void ComposeSpsViewportLayer(
        PixelBuffer viewportBuf,
        PixelBuffer shipBuf,
        ShipHardpointLayout hp,
        ShipRenderOptions options,
        int animBucket)
    {
        // Step 1: Dim all standard hull pixels by shifting them down into dark navy/slate monochrome (#0c141d - #1c2b3a)
        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            for (int x = 0; x < Constants.ShipWidth; x++)
            {
                string? srcHex = shipBuf.Get(x, y);
                if (srcHex == null)
                    continue;

                if (srcHex.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                {
                    viewportBuf.Set(x, y, Palette.HexOutlineVoid);
                }
                else
                {
                    float lum = Palette.GetLuminance(srcHex);
                    string dimCol = Dither.BayerDither(
                        x, y,
                        Palette.HexSpsHullDimDark,
                        Palette.HexSpsHullDimLight,
                        Math.Clamp(lum * 1.2f, 0.15f, 0.90f));
                    viewportBuf.Set(x, y, dimCol);
                }
            }
        }

        // Trigger Condition Check: If shieldsUp == true, draw animated static-interference arcs across the shield bubble
        if (options.ShieldsUp)
        {
            DrawLiveShieldPerimeter(viewportBuf, hp.ShieldEnvelope, animBucket);
            DeterministicRng arcRng = new($"{options.SeedId}:sps_shield_arc:{animBucket}");

            int minArcX = Math.Max(8, hp.ShieldEnvelope.Cx - hp.ShieldEnvelope.Rx + 4);
            int maxArcX = Math.Min(112, hp.ShieldEnvelope.Cx + hp.ShieldEnvelope.Rx - 4);
            for (int a = 0; a < 6; a++)
            {
                int curX = arcRng.NextInt(minArcX, Math.Max(minArcX + 1, maxArcX - 16));
                int curY = arcRng.NextInt(12, 44);
                for (int seg = 0; seg < 4; seg++)
                {
                    int nextX = Math.Clamp(curX + arcRng.NextInt(4, 10), minArcX, maxArcX);
                    int nextY = Math.Clamp(curY + arcRng.NextInt(-8, 9), 8, 48);
                    string arcCol = (seg % 2 == 0) ? Palette.HexSpsShieldArcCyan : Palette.HexSpsShieldArcIce;
                    viewportBuf.DrawLine(curX, curY, nextX, nextY, arcCol);
                    curX = nextX;
                    curY = nextY;
                }
            }

            viewportBuf.FillRect(30, 24, 60, 9, Palette.HexOutlineVoid);
            viewportBuf.DrawBitmapText(34, 26, "SHIELD BLOCK", Palette.HexSpsShieldArcIce);
            return;
        }

        // Step 2: Draw 1px Bresenham conduit lines with animated pulse pips from Reactor Core to the 4 dynamic class hardpoints
        // Hot/armed weapons (both honest declared AND spoofed) visibly render hot orange conduits & glowing weapon mounts inside SPS!
        string weaponConduitCol = options.WeaponsHot ? Palette.HexSpsWeaponsHotOrange : Palette.HexSpsHardpointOffline;
        DrawSpsConduitWithPips(viewportBuf, hp.ReactorCore, hp.ForwardWeapons, weaponConduitCol, options.WeaponsHot, animBucket);
        if (options.WeaponsHot)
        {
            // Vivid hot-orange glowing weapon mount thermal signature at ForwardWeapons hardpoint
            int wx = hp.ForwardWeapons.X;
            int wy = hp.ForwardWeapons.Y;
            bool pulseBright = (animBucket % 2) == 0;
            viewportBuf.FillRect(wx - 2, wy - 2, 5, 5, Palette.HexSpsWeaponsHot);
            viewportBuf.FillRect(wx - 1, wy - 1, 3, 3, Palette.HexSpsWeaponsHotOrange);
            viewportBuf.Set(wx, wy, pulseBright ? "#fef08a" : Palette.HexSpsWeaponsHotCore);
            if (pulseBright)
            {
                viewportBuf.Set(wx - 3, wy, Palette.HexSpsWeaponsHotOrange);
                viewportBuf.Set(wx + 3, wy, Palette.HexSpsWeaponsHotOrange);
                viewportBuf.Set(wx, wy - 3, Palette.HexSpsWeaponsHotOrange);
                viewportBuf.Set(wx, wy + 3, Palette.HexSpsWeaponsHotOrange);
            }
        }

        string warpConduitCol = options.WarpPrimed ? Palette.HexSpsWarpPrimed : Palette.HexSpsWarpIdle;
        DrawSpsConduitWithPips(viewportBuf, hp.ReactorCore, hp.WarpPylons, warpConduitCol, options.WarpPrimed || options.UncannyWarpPylonPulse, animBucket);
        if (options.UncannyWarpPylonPulse)
        {
            // Day 2 Ghost Route uncanny non-violating sensor cue: abnormally hot, erratically pulsing amber Warp Pylons
            int px = hp.WarpPylons.X;
            int py = hp.WarpPylons.Y;
            bool erraticHigh = (animBucket % 3) != 1;
            viewportBuf.FillRect(px - 3, py - 3, 7, 7, "#b45309");
            viewportBuf.FillRect(px - 2, py - 2, 5, 5, Palette.HexSpsWarpPrimed);
            viewportBuf.FillRect(px - 1, py - 1, 3, 3, erraticHigh ? "#fef08a" : "#fbbf24");
            viewportBuf.Set(px, py, "#ffffff");
            if (erraticHigh)
            {
                viewportBuf.Set(px - 4, py, "#fbbf24");
                viewportBuf.Set(px + 4, py, "#fbbf24");
                viewportBuf.Set(px, py - 4, "#fbbf24");
                viewportBuf.Set(px, py + 4, "#fbbf24");
            }
            foreach (var eg in hp.EngineGlowRects)
            {
                viewportBuf.FillRect(Math.Max(1, eg.X - 2), Math.Max(1, eg.Y - 1), eg.W + 2, eg.H + 2,
                    erraticHigh ? "#fbbf24" : Palette.HexSpsWarpPrimed);
            }
        }

        string commsConduitCol = !options.CommsBroken ? Palette.HexSpsCommsActive : Palette.HexSpsCommsBroken;
        DrawSpsConduitWithPips(viewportBuf, hp.ReactorCore, hp.BridgeComms, commsConduitCol, !options.CommsBroken, animBucket);

        string cargoConduitCol = options.CargoHighDraw ? Palette.HexSpsCargoHighDraw : Palette.HexSpsHardpointOffline;
        DrawSpsConduitWithPips(viewportBuf, hp.ReactorCore, hp.CargoBay, cargoConduitCol, options.CargoHighDraw, animBucket);

        // Step 3: Draw pulsing Reactor Core ellipse at hull's dynamic central anchor
        int coreRx = (animBucket % 2 == 0) ? 4 : 3;
        int coreRy = (animBucket % 2 == 0) ? 3 : 2;
        viewportBuf.FillEllipse(hp.ReactorCore.X, hp.ReactorCore.Y, coreRx, coreRy, (_, _, _, _, dist) =>
            dist < 0.45f ? "#fce08b" : Palette.HexSpsWarpPrimed);
    }

    private static void DrawSpsConduitWithPips(
        PixelBuffer buf,
        (int X, int Y) fromCore,
        (int X, int Y) toHardpoint,
        string conduitHex,
        bool isActiveHighPower,
        int animBucket)
    {
        buf.DrawLine(fromCore.X, fromCore.Y, toHardpoint.X, toHardpoint.Y, conduitHex);

        // Hardpoint terminal node box (3x3)
        buf.FillRect(toHardpoint.X - 1, toHardpoint.Y - 1, 3, 3, conduitHex);

        List<(int X, int Y)> linePts = PixelBuffer.TraceBresenhamLine(fromCore.X, fromCore.Y, toHardpoint.X, toHardpoint.Y);
        if (linePts.Count > 3)
        {
            string pipHex = isActiveHighPower ? "#f8fafc" : conduitHex;
            for (int i = 0; i < linePts.Count; i++)
            {
                if (((i - animBucket) % 6 + 6) % 6 == 0)
                {
                    buf.Set(linePts[i].X, linePts[i].Y, pipHex);
                }
            }
        }
    }
}
