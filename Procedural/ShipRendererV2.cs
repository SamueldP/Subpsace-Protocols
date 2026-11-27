using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// Pixel-exact axis-aligned hardpoint rectangle on the 280x120 V2 ship art canvas.
/// Call Scale2x() to project directly into 560x240 viewport coordinates.
/// </summary>
public readonly record struct HardpointRect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
    public int CenterX => X + W / 2;
    public int CenterY => Y + H / 2;
    public bool IsEmpty => W <= 0 || H <= 0;

    public HardpointRect Scale2x() => new(X * 2, Y * 2, W * 2, H * 2);
}

/// <summary>
/// Dorsal and ventral docking clamp grab coordinates at a given X station on the 280x120 V2 canvas.
/// </summary>
public readonly record struct ClampGrabZone(int X, int TopY, int BottomY)
{
    public ClampGrabZone Scale2x() => new(X * 2, TopY * 2, BottomY * 2);
}

/// <summary>
/// Unified per-faction/class hardpoint and structural anchor layout on the 280x120 V2 art grid.
/// Drives both the Live exterior render and the SPS X-ray interior render, with helpers mapping
/// cleanly into 560x240 viewport space so SPS scanning and Detain clamps remain 100% pixel-locked.
/// </summary>
public struct ShipHardpointLayoutV2
{
    public (int X, int Y) ReactorCore { get; set; }
    public HardpointRect ReactorBounds { get; set; }
    public HardpointRect WeaponsBay { get; set; }
    public HardpointRect SecondaryWeaponsBay { get; set; }
    public HardpointRect CargoBay { get; set; }
    public HardpointRect BridgeComms { get; set; }
    public HardpointRect UpperWarpPylon { get; set; }
    public HardpointRect LowerWarpPylon { get; set; }
    public List<HardpointRect> EngineNozzles { get; }
    public ClampGrabZone ForeClampGrab { get; set; }
    public ClampGrabZone AftClampGrab { get; set; }
    public (int Cx, int Cy, int Rx, int Ry) ShieldPerimeter { get; set; }
    public HardpointRect HullBounds { get; set; }
    public List<(int X, int Y)> NavStrobes { get; }
    public List<(int X, int Y)> WeaponMuzzlePixels { get; }

    public ShipHardpointLayoutV2()
    {
        ReactorCore = (140, 60);
        ReactorBounds = new HardpointRect(136, 56, 8, 8);
        WeaponsBay = new HardpointRect(68, 56, 12, 8);
        SecondaryWeaponsBay = default;
        CargoBay = new HardpointRect(148, 63, 14, 8);
        BridgeComms = new HardpointRect(98, 46, 12, 8);
        UpperWarpPylon = new HardpointRect(178, 35, 16, 7);
        LowerWarpPylon = new HardpointRect(178, 78, 16, 7);
        EngineNozzles = [];
        ForeClampGrab = new ClampGrabZone(108, 40, 80);
        AftClampGrab = new ClampGrabZone(178, 40, 80);
        ShieldPerimeter = (140, 60, 80, 32);
        HullBounds = new HardpointRect(66, 30, 148, 60);
        NavStrobes = [];
        WeaponMuzzlePixels = [];
    }

    public readonly HardpointRect ReactorBoundsViewport560 => ReactorBounds.Scale2x();
    public readonly HardpointRect WeaponsBayViewport560 => WeaponsBay.Scale2x();
    public readonly HardpointRect CargoBayViewport560 => CargoBay.Scale2x();
    public readonly HardpointRect BridgeCommsViewport560 => BridgeComms.Scale2x();
    public readonly HardpointRect UpperWarpPylonViewport560 => UpperWarpPylon.Scale2x();
    public readonly HardpointRect HullBoundsViewport560 => HullBounds.Scale2x();
    public readonly (int X, int Y) WeaponsLockCenter560 => (WeaponsBay.CenterX * 2 + 1, WeaponsBay.CenterY * 2 + 1);
    public readonly (int X, int Y) CargoLockCenter560 => (CargoBay.CenterX * 2 + 1, CargoBay.CenterY * 2 + 1);
    public readonly ClampGrabZone ForeClampViewport560 => ForeClampGrab.Scale2x();
    public readonly ClampGrabZone AftClampViewport560 => AftClampGrab.Scale2x();
}

/// <summary>
/// Seed-driven within-class modular customization profile applied consistently to hull geometry,
/// livery markings, and surface wear so two ships of the same class never look like clones while
/// preserving Kordani Manual Tab 2 macro silhouettes.
/// </summary>
internal readonly struct ShipVariantProfile
{
    public int BowSensorStyle { get; init; }       // 0..3
    public int DorsalModuleStyle { get; init; }    // 0..3
    public int VentralModuleStyle { get; init; }   // 0..3
    public int PylonCowlingVariant { get; init; }  // 0..2
    public int LiveryPattern { get; init; }        // 0..3
    public int WearLevel { get; init; }            // 0..3
    public int ToneShiftSign { get; init; }        // -1, 0, +1
    public int StripeOffsetX { get; init; }        // -4..+4
    public int PatchSeedOffset { get; init; }      // 0..99

    public static ShipVariantProfile Roll(DeterministicRng rng, bool canonicalManual)
    {
        if (canonicalManual)
        {
            return new ShipVariantProfile
            {
                BowSensorStyle = 0,
                DorsalModuleStyle = 0,
                VentralModuleStyle = 0,
                PylonCowlingVariant = 0,
                LiveryPattern = 0,
                WearLevel = 0,
                ToneShiftSign = 0,
                StripeOffsetX = 0,
                PatchSeedOffset = 0
            };
        }

        DeterministicRng v = rng.Fork("ship_v2_traits");
        return new ShipVariantProfile
        {
            BowSensorStyle = v.NextInt(0, 4),
            DorsalModuleStyle = v.NextInt(0, 4),
            VentralModuleStyle = v.NextInt(0, 4),
            PylonCowlingVariant = v.NextInt(0, 3),
            LiveryPattern = v.NextInt(0, 4),
            WearLevel = v.NextInt(0, 4),
            ToneShiftSign = v.NextInt(-1, 2),
            StripeOffsetX = v.NextInt(-4, 5),
            PatchSeedOffset = v.NextInt(0, 100)
        };
    }
}

/// <summary>
/// Hi-Bit 280x120 -> 560x240 (exact 2x nearest-neighbor) procedural starship renderer.
/// Parallel V2 upgrade preserving all V1 ShipRenderOptions & ShipRenderer public helpers untouched.
/// Features 4-tone beveled armor shading, 4x4 Bayer dithering, Brown Dwarf crimson-amber rim lighting,
/// modular seed-driven greebles/livery/wear, and 100% hardpoint-locked SPS X-ray interiors.
/// </summary>
public static class ShipRendererV2
{
    public const int ArtWidth = 280;
    public const int ArtHeight = 120;
    public const int OutputWidth = ArtWidth * 2;   // 560
    public const int OutputHeight = ArtHeight * 2; // 240

    public const int SafeMinArtX = 56;
    public const int SafeMaxArtX = 224;
    public const int SafeMinArtY = 24;
    public const int SafeMaxArtY = 95;

    private const int SpsLockMinArtX = 60;
    private const int SpsLockMaxArtX = 218;
    private const int SpsLockMinArtY = 32;
    private const int SpsLockMaxArtY = 90;
    private const float HullFootprintScale = 0.89f;
    public const int MinSpsSubsystemClearancePx = 3;

    public static bool AreRectsSeparatedByAtLeast(HardpointRect a, HardpointRect b, int minGapPx = MinSpsSubsystemClearancePx)
    {
        if (a.IsEmpty || b.IsEmpty)
            return true;

        return a.Right + minGapPx <= b.X ||
               b.Right + minGapPx <= a.X ||
               a.Bottom + minGapPx <= b.Y ||
               b.Bottom + minGapPx <= a.Y;
    }

    public static IReadOnlyList<ShipFaction> AllFactions => ShipRenderer.AllFactions;
    public static IReadOnlyList<ShipClass> GetAllowedClasses(ShipFaction faction) => ShipRenderer.GetAllowedClasses(faction);
    public static bool IsClassAllowed(ShipFaction faction, ShipClass shipClass) => ShipRenderer.IsClassAllowed(faction, shipClass);
    public static ShipClass NormalizeShipClass(ShipFaction faction, ShipClass requestedClass) => ShipRenderer.NormalizeShipClass(faction, requestedClass);
    public static bool IsDecommissionedEffective(ShipFaction faction, ShipClass effectiveClass, bool requestedDecomm, bool isScripted) =>
        ShipRenderer.IsDecommissionedEffective(faction, effectiveClass, requestedDecomm, isScripted);
    public static string GetFactionPrefix(ShipFaction faction) => ShipRenderer.GetFactionPrefix(faction);
    public static string GetFactionDisplayName(ShipFaction faction) => ShipRenderer.GetFactionDisplayName(faction);
    public static ShipFaction ParseFactionFromPrefix(string seedOrPrefix) => ShipRenderer.ParseFactionFromPrefix(seedOrPrefix);
    public static bool TryGetScriptedOverrideTexture(ShipRenderOptions options, out Texture2D texture) =>
        ShipRenderer.TryGetScriptedOverrideTexture(options, out texture);

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

    public static PixelBuffer RenderBuffer(ShipRenderOptions options) =>
        RenderBuffer(options, out _);

    public static PixelBuffer RenderBuffer(ShipRenderOptions options, out ShipHardpointLayoutV2 hardpoints)
    {
        PixelBuffer native = RenderNative280x120Buffer(options, out hardpoints);
        return native.Upscale2x();
    }

    /// <summary>
    /// Renders a 120x56 CRT Amber recognition chart buffer sampled 2:1 from V2's active 240x112 region
    /// so DeskScreen's Reference Manual Tab 2. HULLS mirrors the V2 class silhouette inside its 120x56 slot.
    /// </summary>
    public static PixelBuffer RenderManualTablet120x56Buffer(ShipFaction faction, ShipClass requestedClass, string seedId)
    {
        PixelBuffer native = RenderNative280x120Buffer(new ShipRenderOptions
        {
            SeedId = seedId,
            Faction = faction,
            RequestedClass = requestedClass,
            Mode = ShipRenderMode.Manual
        }, out _);

        PixelBuffer tablet = new(Constants.ShipWidth, Constants.ShipHeight);
        tablet.Clear(Palette.HexOutlineVoid);

        for (int gx = 10; gx < Constants.ShipWidth; gx += 20)
        {
            for (int gy = 8; gy < Constants.ShipHeight; gy += 16)
            {
                tablet.Set(gx, gy, "#3d2006");
            }
        }

        const int offsetX = (ArtWidth - Constants.ShipWidth * 2) / 2; // 20
        const int offsetY = (ArtHeight - Constants.ShipHeight * 2) / 2; // 4

        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            for (int x = 0; x < Constants.ShipWidth; x++)
            {
                int sx = offsetX + x * 2;
                int sy = offsetY + y * 2;
                string? best = null;
                float bestScore = -1f;

                for (int oy = 0; oy < 2; oy++)
                {
                    for (int ox = 0; ox < 2; ox++)
                    {
                        string? pxHex = native.Get(sx + ox, sy + oy);
                        if (pxHex == null || pxHex.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (pxHex.Equals("#3d2006", StringComparison.OrdinalIgnoreCase))
                            continue;

                        float score = pxHex.Equals(Palette.HexCrtAmberPrimary, StringComparison.OrdinalIgnoreCase)
                            ? 2.0f
                            : Palette.GetLuminance(pxHex);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = pxHex;
                        }
                    }
                }

                if (best != null)
                {
                    tablet.Set(x, y, best);
                }
            }
        }

        return tablet;
    }

    /// <summary>
    /// Core 280x120 native pixel-art rasterizer (upscaled strictly 2x for 560x240 output).
    /// </summary>
    public static PixelBuffer RenderNative280x120Buffer(ShipRenderOptions options, out ShipHardpointLayoutV2 hardpoints)
    {
        DeterministicRng rng = new(options.SeedId);
        ShipClass effectiveClass = NormalizeShipClass(options.Faction, options.RequestedClass);
        bool effectiveDecomm = IsDecommissionedEffective(
            options.Faction,
            effectiveClass,
            options.IsDecommissioned,
            options.IsScriptedOverride);

        bool canonicalManual = options.Mode == ShipRenderMode.Manual &&
                               options.SeedId.StartsWith("manual-chart-", StringComparison.OrdinalIgnoreCase);
        ShipVariantProfile variant = ShipVariantProfile.Roll(rng, canonicalManual);

        PixelBuffer rawHullBuf = new(ArtWidth, ArtHeight);
        hardpoints = new ShipHardpointLayoutV2();

        BuildShipHullGeometryV2(
            rawHullBuf,
            ref hardpoints,
            options.Faction,
            effectiveClass,
            effectiveDecomm,
            options.HasDownstreamHullScorch,
            variant,
            rng,
            out string engineGlowHex,
            out int foreClampX,
            out int aftClampX);

        PixelBuffer shipBuf = CompressHullFootprintTo90Percent(
            rawHullBuf,
            ref hardpoints,
            ref foreClampX,
            ref aftClampX);

        FinalizeHardpointsAndClampPads(shipBuf, ref hardpoints, foreClampX, aftClampX);

        shipBuf.ApplyOutline(Palette.HexOutlineVoid);

        if (options.Mode == ShipRenderMode.Manual)
        {
            return ComposeManualChartBufferV2(shipBuf);
        }

        PixelBuffer viewportBuf = new(ArtWidth, ArtHeight);
        int animBucket = options.EffectiveAnimBucket;
        bool enginePulseBright = (animBucket % 2) == 0;
        bool navStrobeOn = ((animBucket / 2) % 2) == 0;

        if (options.SpsEnabled)
        {
            ComposeSpsViewportLayerV2(viewportBuf, shipBuf, hardpoints, options, animBucket);
            return viewportBuf;
        }

        // Directional Brown Dwarf crimson-amber underside rim lighting
        ApplyBrownDwarfRimLightV2(shipBuf, hardpoints);

        // Animated layered thruster plumes at each engine nozzle
        DrawAnimatedEnginePlumesV2(shipBuf, hardpoints, engineGlowHex, enginePulseBright, animBucket);

        // Navigation strobes
        if (navStrobeOn)
        {
            for (int i = 0; i < hardpoints.NavStrobes.Count; i++)
            {
                var st = hardpoints.NavStrobes[i];
                string strobeCol = (i % 2 == 0) ? "#f8fafc" : Palette.HexCrtAmberPrimary;
                shipBuf.Set(st.X, st.Y, strobeCol);
                shipBuf.Set(st.X + 1, st.Y, strobeCol);
            }
        }

        // Visual Violation Tell 1: WeaponsHot -> pulsing #dc2626 / #fca5a5 muzzle flare
        if (options.WeaponsHot)
        {
            foreach (var wp in hardpoints.WeaponMuzzlePixels)
            {
                shipBuf.FillRect(wp.X - 2, wp.Y - 1, 4, 3, Palette.HexAlertCrimson);
                shipBuf.Set(wp.X, wp.Y, enginePulseBright ? "#fca5a5" : "#ef4444");
                if (enginePulseBright)
                {
                    shipBuf.Set(wp.X - 3, wp.Y, "#ef4444");
                }
            }
        }

        viewportBuf.BlitFrom(shipBuf);

        // Visual Violation Tell 2: ShieldsUp -> animated dithered teal/cyan perimeter
        if (options.ShieldsUp)
        {
            DrawLiveShieldPerimeterV2(viewportBuf, hardpoints.ShieldPerimeter, animBucket);
        }

        return viewportBuf;
    }

    private static int ScaleFootprintX(int x)
    {
        const int cx = ArtWidth / 2; // 140
        int delta = x - cx;
        return cx + Math.Sign(delta) * (int)MathF.Round(MathF.Abs(delta) * HullFootprintScale);
    }

    private static int ScaleFootprintY(int y)
    {
        const int cy = ArtHeight / 2; // 60
        int delta = y - cy;
        return cy + Math.Sign(delta) * (int)MathF.Round(MathF.Abs(delta) * HullFootprintScale);
    }

    private static HardpointRect ScaleFootprintRect(HardpointRect r)
    {
        if (r.IsEmpty)
            return default;

        int x0 = ScaleFootprintX(r.X);
        int x1 = Math.Max(x0 + 1, ScaleFootprintX(r.Right));
        int y0 = ScaleFootprintY(r.Y);
        int y1 = Math.Max(y0 + 1, ScaleFootprintY(r.Bottom));
        return new HardpointRect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// Compresses the 280x120 logical hull canvas cleanly toward (140, 60) by ~89% BEFORE outlining
    /// and integer 2x upscaling so vessels sit with generous dark-space margin around nose, tail,
    /// and docking clamps at razor-sharp integer 2x without fractional GPU scaling.
    /// </summary>
    private static PixelBuffer CompressHullFootprintTo90Percent(
        PixelBuffer raw,
        ref ShipHardpointLayoutV2 hp,
        ref int foreClampX,
        ref int aftClampX)
    {
        PixelBuffer dst = new(ArtWidth, ArtHeight);
        const int cx = ArtWidth / 2;
        const int cy = ArtHeight / 2;
        const float invScale = 1.0f / HullFootprintScale;

        // 1. Symmetric integer inverse-map for crisp solid hull silhouette & bevel shading
        for (int dy = 1; dy < ArtHeight - 1; dy++)
        {
            int ddy = dy - cy;
            int sy = cy + Math.Sign(ddy) * (int)MathF.Round(MathF.Abs(ddy) * invScale);
            if ((uint)sy >= ArtHeight)
                continue;

            for (int dx = 1; dx < ArtWidth - 1; dx++)
            {
                int ddx = dx - cx;
                int sx = cx + Math.Sign(ddx) * (int)MathF.Round(MathF.Abs(ddx) * invScale);
                if ((uint)sx >= ArtWidth)
                    continue;

                string? px = raw.Get(sx, sy);
                if (px != null)
                {
                    dst.Set(dx, dy, px);
                }
            }
        }

        // 2. Preserve 1px thin fins/antennas and bright viewport slits / pinstripes that fell on skipped rows/cols
        for (int sy = 1; sy < ArtHeight - 1; sy++)
        {
            int dy = ScaleFootprintY(sy);
            if ((uint)dy >= ArtHeight)
                continue;

            for (int sx = 1; sx < ArtWidth - 1; sx++)
            {
                string? srcHex = raw.Get(sx, sy);
                if (srcHex == null)
                    continue;

                int dx = ScaleFootprintX(sx);
                if ((uint)dx >= ArtWidth)
                    continue;

                string? curHex = dst.Get(dx, dy);
                if (curHex == null)
                {
                    dst.Set(dx, dy, srcHex);
                }
                else if (Palette.GetLuminance(srcHex) > Palette.GetLuminance(curHex) + 0.28f)
                {
                    dst.Set(dx, dy, srcHex);
                }
            }
        }

        // 3. Remap hardpoints, nozzles, strobes, muzzles, and clamp stations consistently into ~89% logical space
        foreClampX = ScaleFootprintX(foreClampX);
        aftClampX = ScaleFootprintX(aftClampX);
        hp.ReactorCore = (ScaleFootprintX(hp.ReactorCore.X), ScaleFootprintY(hp.ReactorCore.Y));
        hp.WeaponsBay = ScaleFootprintRect(hp.WeaponsBay);
        hp.SecondaryWeaponsBay = ScaleFootprintRect(hp.SecondaryWeaponsBay);
        hp.CargoBay = ScaleFootprintRect(hp.CargoBay);
        hp.BridgeComms = ScaleFootprintRect(hp.BridgeComms);
        hp.UpperWarpPylon = ScaleFootprintRect(hp.UpperWarpPylon);
        hp.LowerWarpPylon = ScaleFootprintRect(hp.LowerWarpPylon);

        for (int i = 0; i < hp.EngineNozzles.Count; i++)
        {
            HardpointRect n = hp.EngineNozzles[i];
            int nx = ScaleFootprintX(n.X);
            int ny = ScaleFootprintY(n.Y);
            int nw = Math.Max(3, (int)MathF.Round(n.W * HullFootprintScale));
            int nh = Math.Max(3, ScaleFootprintY(n.Bottom) - ny);
            hp.EngineNozzles[i] = new HardpointRect(nx, ny, nw, nh);
        }

        for (int i = 0; i < hp.NavStrobes.Count; i++)
        {
            var st = hp.NavStrobes[i];
            hp.NavStrobes[i] = (ScaleFootprintX(st.X), ScaleFootprintY(st.Y));
        }

        for (int i = 0; i < hp.WeaponMuzzlePixels.Count; i++)
        {
            var wp = hp.WeaponMuzzlePixels[i];
            hp.WeaponMuzzlePixels[i] = (ScaleFootprintX(wp.X), ScaleFootprintY(wp.Y));
        }

        return dst;
    }

    // =========================================================================
    // HARDPOINT FINALIZATION, CLAMP LUGS & SHIELD ENVELOPE
    // =========================================================================
    private static void FinalizeHardpointsAndClampPads(
        PixelBuffer shipBuf,
        ref ShipHardpointLayoutV2 hp,
        int foreClampX,
        int aftClampX)
    {
        int minX = ArtWidth - 1;
        int maxX = 0;
        int minY = ArtHeight - 1;
        int maxY = 0;
        bool any = false;

        for (int y = 0; y < ArtHeight; y++)
        {
            for (int x = 0; x < ArtWidth; x++)
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
            hp.HullBounds = new HardpointRect(84, 38, 112, 44);
            hp.ShieldPerimeter = (140, 60, 62, 27);
            hp.ForeClampGrab = new ClampGrabZone(108, 38, 81);
            hp.AftClampGrab = new ClampGrabZone(172, 38, 81);
            return;
        }

        hp.HullBounds = new HardpointRect(minX, minY, maxX - minX + 1, maxY - minY + 1);

        int cx = Math.Clamp((minX + maxX) / 2, SafeMinArtX + 8, SafeMaxArtX - 8);
        int cy = Math.Clamp((minY + maxY) / 2, SafeMinArtY + 8, SafeMaxArtY - 8);
        int rx = Math.Clamp((maxX - minX) / 2 + 7, 32, 84);
        int ry = Math.Clamp((maxY - minY) / 2 + 5, 18, 35);

        if (cx - rx < 50) rx = cx - 50;
        if (cx + rx > 230) rx = 230 - cx;
        if (cy - ry < 20) ry = cy - 20;
        if (cy + ry > 99) ry = 99 - cy;
        hp.ShieldPerimeter = (cx, cy, Math.Max(20, rx), Math.Max(14, ry));

        hp.ForeClampGrab = ResolveAndDrawClampZone(shipBuf, foreClampX, hp.HullBounds);
        hp.AftClampGrab = ResolveAndDrawClampZone(shipBuf, aftClampX, hp.HullBounds);

        ResolveHullBoundNonOverlappingHardpoints(shipBuf, ref hp);
    }

    /// <summary>
    /// Places every internal SPS subsystem bounding box strictly inside the vessel's solid hull silhouette
    /// with >= 3px clearance between Bridge/Comms, Weapons, Reactor, Cargo Bay, and Engines/Warp Pylons,
    /// while keeping reticle lock centers within DeskScreen's reachable SPS scan window.
    /// </summary>
    private static void ResolveHullBoundNonOverlappingHardpoints(PixelBuffer shipBuf, ref ShipHardpointLayoutV2 hp)
    {
        bool[] hullMask = shipBuf.CaptureNonNullMask(excludeOutline: false);
        bool[] strictInteriorMask = new bool[ArtWidth * ArtHeight];
        for (int y = 2; y < ArtHeight - 2; y++)
        {
            for (int x = 2; x < ArtWidth - 2; x++)
            {
                int idx = y * ArtWidth + x;
                if (hullMask[idx] &&
                    hullMask[idx - 1] &&
                    hullMask[idx + 1] &&
                    hullMask[idx - ArtWidth] &&
                    hullMask[idx + ArtWidth])
                {
                    strictInteriorMask[idx] = true;
                }
            }
        }

        HardpointRect b = hp.HullBounds;
        Span<HardpointRect> placed = stackalloc HardpointRect[7];
        int placedCount = 0;

        // 1. Reactor Core (center-mid anchor)
        HardpointRect prefReactor = new(hp.ReactorCore.X - 4, hp.ReactorCore.Y - 4, 8, 7);
        hp.ReactorBounds = PlaceSubsystemBox(
            hullMask, strictInteriorMask, b, prefReactor,
            prefW: 8, prefH: 7, minW: 7, minH: 6,
            requireLockWindow: true, placed, ref placedCount);
        hp.ReactorCore = (hp.ReactorBounds.CenterX, hp.ReactorBounds.CenterY);

        // 2. Forward Weapons Mount (bow/fore station)
        HardpointRect prefWeapons = hp.WeaponsBay.IsEmpty
            ? new HardpointRect(b.X + 8, hp.ReactorCore.Y - 3, 10, 7)
            : hp.WeaponsBay;
        hp.WeaponsBay = PlaceSubsystemBox(
            hullMask, strictInteriorMask, b, prefWeapons,
            prefW: Math.Clamp(prefWeapons.W, 9, 12), prefH: Math.Clamp(prefWeapons.H, 6, 8), minW: 8, minH: 6,
            requireLockWindow: true, placed, ref placedCount);

        // 3. Aft Thruster / Upper Warp Pylon Manifold (stern/nacelle station)
        HardpointRect prefUpperWarp = hp.UpperWarpPylon.IsEmpty
            ? new HardpointRect(b.Right - 22, hp.ReactorCore.Y - 8, 13, 6)
            : hp.UpperWarpPylon;
        hp.UpperWarpPylon = PlaceSubsystemBox(
            hullMask, strictInteriorMask, b, prefUpperWarp,
            prefW: Math.Clamp(prefUpperWarp.W, 11, 16), prefH: Math.Clamp(prefUpperWarp.H, 6, 7), minW: 8, minH: 5,
            requireLockWindow: true, placed, ref placedCount);

        // 4. Bridge / Comms Transceiver (fore-dorsal / command station)
        HardpointRect prefComms = hp.BridgeComms.IsEmpty
            ? new HardpointRect(b.X + b.W / 3, hp.ReactorCore.Y - 8, 10, 7)
            : hp.BridgeComms;
        hp.BridgeComms = PlaceSubsystemBox(
            hullMask, strictInteriorMask, b, prefComms,
            prefW: Math.Clamp(prefComms.W, 9, 12), prefH: Math.Clamp(prefComms.H, 6, 8), minW: 8, minH: 5,
            requireLockWindow: true, placed, ref placedCount);

        // 5. Cargo Bay Hold (aft-mid / ventral-mid station)
        HardpointRect prefCargo = hp.CargoBay.IsEmpty
            ? new HardpointRect(hp.ReactorBounds.Right + 5, hp.ReactorCore.Y + 2, 12, 7)
            : hp.CargoBay;
        hp.CargoBay = PlaceSubsystemBox(
            hullMask, strictInteriorMask, b, prefCargo,
            prefW: Math.Clamp(prefCargo.W, 11, 14), prefH: Math.Clamp(prefCargo.H, 6, 8), minW: 8, minH: 6,
            requireLockWindow: true, placed, ref placedCount);

        // 6. Optional Lower Thruster / Warp Pylon (only if hull fits a clean non-overlapping twin nacelle)
        if (!hp.LowerWarpPylon.IsEmpty && hp.LowerWarpPylon != hp.UpperWarpPylon)
        {
            if (TryFindInteriorCandidate(
                    strictInteriorMask, b,
                    hp.LowerWarpPylon.CenterX, hp.LowerWarpPylon.CenterY,
                    Math.Clamp(hp.LowerWarpPylon.W, 10, 15), Math.Clamp(hp.LowerWarpPylon.H, 5, 7),
                    requireLockWindow: false, maxSearchRadius: 18, placed[..placedCount], out HardpointRect lowFit) ||
                TryFindInteriorCandidate(
                    hullMask, b,
                    hp.LowerWarpPylon.CenterX, hp.LowerWarpPylon.CenterY,
                    10, 5,
                    requireLockWindow: false, maxSearchRadius: 14, placed[..placedCount], out lowFit))
            {
                hp.LowerWarpPylon = lowFit;
                placed[placedCount++] = lowFit;
            }
            else
            {
                hp.LowerWarpPylon = default;
            }
        }
        else
        {
            hp.LowerWarpPylon = default;
        }

        // 7. Optional Secondary Weapons Bay (only if hull fits without overlapping or crowding within 3px)
        if (!hp.SecondaryWeaponsBay.IsEmpty)
        {
            if (TryFindInteriorCandidate(
                    strictInteriorMask, b,
                    hp.SecondaryWeaponsBay.CenterX, hp.SecondaryWeaponsBay.CenterY,
                    9, 6,
                    requireLockWindow: false, maxSearchRadius: 14, placed[..placedCount], out HardpointRect secWpn))
            {
                hp.SecondaryWeaponsBay = secWpn;
                placed[placedCount++] = secWpn;
            }
            else
            {
                hp.SecondaryWeaponsBay = default;
            }
        }
    }

    private static HardpointRect PlaceSubsystemBox(
        bool[] hullMask,
        bool[] strictInteriorMask,
        HardpointRect hullBounds,
        HardpointRect preferred,
        int prefW,
        int prefH,
        int minW,
        int minH,
        bool requireLockWindow,
        Span<HardpointRect> placed,
        ref int placedCount)
    {
        int targetCx = preferred.CenterX;
        int targetCy = preferred.CenterY;
        ReadOnlySpan<HardpointRect> existing = placed[..placedCount];

        (bool[] Mask, int W, int H)[] passes =
        [
            (strictInteriorMask, prefW, prefH),
            (strictInteriorMask, minW, minH),
            (hullMask, prefW, prefH),
            (hullMask, minW, minH),
            (strictInteriorMask, 6, 5),
            (hullMask, 6, 5),
            (hullMask, 5, 4)
        ];

        for (int p = 0; p < passes.Length; p++)
        {
            var pass = passes[p];
            if (TryFindInteriorCandidate(
                    pass.Mask,
                    hullBounds,
                    targetCx,
                    targetCy,
                    pass.W,
                    pass.H,
                    requireLockWindow,
                    maxSearchRadius: 180,
                    existing,
                    out HardpointRect found))
            {
                placed[placedCount++] = found;
                return found;
            }
        }

        // Last-resort scan without lock-window constraint if an exotic silhouette forced wider search
        if (TryFindInteriorCandidate(hullMask, hullBounds, targetCx, targetCy, 5, 4, false, 200, existing, out HardpointRect fallback))
        {
            placed[placedCount++] = fallback;
            return fallback;
        }

        HardpointRect clamped = ClampHardpointCenter(new HardpointRect(targetCx - 3, targetCy - 2, 6, 5));
        placed[placedCount++] = clamped;
        return clamped;
    }

    private static bool TryFindInteriorCandidate(
        bool[] mask,
        HardpointRect hullBounds,
        int targetCx,
        int targetCy,
        int w,
        int h,
        bool requireLockWindow,
        int maxSearchRadius,
        ReadOnlySpan<HardpointRect> existing,
        out HardpointRect bestRect)
    {
        bestRect = default;
        int minX = Math.Max(2, hullBounds.X);
        int maxX = Math.Min(ArtWidth - 2 - w, hullBounds.Right - w);
        int minY = Math.Max(2, hullBounds.Y);
        int maxY = Math.Min(ArtHeight - 2 - h, hullBounds.Bottom - h);
        if (maxX < minX || maxY < minY)
            return false;

        int bestDistSq = int.MaxValue;
        int maxDistSq = maxSearchRadius * maxSearchRadius;

        for (int y = minY; y <= maxY; y++)
        {
            int cy = y + h / 2;
            if (requireLockWindow && (cy < SpsLockMinArtY || cy > SpsLockMaxArtY))
                continue;

            int dy = cy - targetCy;
            int dySq = dy * dy * 2; // Slight vertical affinity keeps dorsal/ventral stations distinct
            if (dySq > bestDistSq || dySq > maxDistSq)
                continue;

            for (int x = minX; x <= maxX; x++)
            {
                int cx = x + w / 2;
                if (requireLockWindow && (cx < SpsLockMinArtX || cx > SpsLockMaxArtX))
                    continue;

                int dx = cx - targetCx;
                int distSq = dx * dx + dySq;
                if (distSq >= bestDistSq || distSq > maxDistSq)
                    continue;

                HardpointRect cand = new(x, y, w, h);
                if (!HasRequiredClearance(cand, existing))
                    continue;
                if (!IsRectFullyInsideMask(mask, cand))
                    continue;

                bestDistSq = distSq;
                bestRect = cand;
            }
        }

        return !bestRect.IsEmpty;
    }

    private static bool HasRequiredClearance(HardpointRect candidate, ReadOnlySpan<HardpointRect> existing)
    {
        for (int i = 0; i < existing.Length; i++)
        {
            if (!AreRectsSeparatedByAtLeast(candidate, existing[i], MinSpsSubsystemClearancePx))
                return false;
        }
        return true;
    }

    public static bool IsRectFullyInsideMask(bool[] mask, HardpointRect r)
    {
        if (r.IsEmpty || r.X < 1 || r.Y < 1 || r.Right > ArtWidth - 1 || r.Bottom > ArtHeight - 1)
            return false;

        for (int y = r.Y; y < r.Bottom; y++)
        {
            int row = y * ArtWidth;
            for (int x = r.X; x < r.Right; x++)
            {
                if (!mask[row + x])
                    return false;
            }
        }
        return true;
    }

    private static HardpointRect ClampHardpointCenter(HardpointRect r)
    {
        int w = Math.Max(6, r.W);
        int h = Math.Max(6, r.H);
        int cx = Math.Clamp(r.X + w / 2, SpsLockMinArtX, SpsLockMaxArtX);
        int cy = Math.Clamp(r.Y + h / 2, SpsLockMinArtY, SpsLockMaxArtY);
        return new HardpointRect(cx - w / 2, cy - h / 2, w, h);
    }

    private static ClampGrabZone ResolveAndDrawClampZone(PixelBuffer buf, int targetX, HardpointRect bounds)
    {
        int x = Math.Clamp(targetX, bounds.X + 6, bounds.Right - 7);
        int topY = -1;
        int botY = -1;

        for (int dx = 0; dx <= 4 && topY < 0; dx++)
        {
            int[] offsets = dx == 0 ? [0] : [dx, -dx];
            foreach (int off in offsets)
            {
                int probeX = Math.Clamp(x + off, bounds.X + 2, bounds.Right - 3);
                int first = -1;
                int last = -1;
                for (int y = bounds.Y; y < bounds.Bottom; y++)
                {
                    if (buf.HasPixel(probeX, y))
                    {
                        if (first < 0) first = y;
                        last = y;
                    }
                }
                if (first >= 0 && last - first >= 6)
                {
                    x = probeX;
                    topY = first;
                    botY = last;
                    break;
                }
            }
        }

        if (topY < 0)
        {
            topY = bounds.Y;
            botY = bounds.Bottom - 1;
        }

        // Draw subtle recessed clamp-collar lugs directly onto dorsal/ventral edges at the resolved X
        buf.FillRect(x - 2, topY, 5, 2, Palette.HexWeatheredGunmetalMid);
        buf.Set(x, topY, Palette.HexOxidizedBronzeWorn);
        buf.FillRect(x - 2, botY - 1, 5, 2, Palette.HexWeatheredGunmetalMid);
        buf.Set(x, botY, Palette.HexOxidizedBronzeShadow);

        return new ClampGrabZone(x, topY, botY);
    }

    // =========================================================================
    // HI-BIT PRIMITIVES: 4-TONE BEVELS, SEAMS, ENGINE BELLS, GREEBLES, WEAR
    // =========================================================================
    private static void DrawBeveledHullPoly(
        PixelBuffer buf,
        ReadOnlySpan<(int X, int Y)> pts,
        string[] ramp,
        float shadeBias = 0f,
        bool directionalBevel = true)
    {
        if (pts.Length < 3)
            return;

        int minX = ArtWidth - 1;
        int maxX = 0;
        int minY = ArtHeight - 1;
        int maxY = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i].X < minX) minX = pts[i].X;
            if (pts[i].X > maxX) maxX = pts[i].X;
            if (pts[i].Y < minY) minY = pts[i].Y;
            if (pts[i].Y > maxY) maxY = pts[i].Y;
        }

        int spanY = Math.Max(1, maxY - minY);
        int spanX = Math.Max(1, maxX - minX);

        PixelBuffer local = new(ArtWidth, ArtHeight);
        local.FillPoly(pts, (px, py) =>
        {
            float ny = (py - minY) / (float)spanY;
            float nx = (px - minX) / (float)spanX;
            float t = 0.94f - ny * 0.72f - nx * 0.14f + shadeBias;
            return Dither.SampleRamp(px, py, ramp, t);
        });

        if (directionalBevel)
        {
            ApplyLocalBevelPass(local, ramp);
        }

        buf.BlitFrom(local);
    }

    private static void DrawBeveledArmorPlate(
        PixelBuffer buf,
        int x,
        int y,
        int w,
        int h,
        string[] ramp,
        float shadeBias = 0f,
        bool withRivets = false)
    {
        if (w <= 0 || h <= 0)
            return;

        buf.FillRect(x, y, w, h, (px, py) =>
        {
            float ny = h > 1 ? (py - y) / (float)(h - 1) : 0.5f;
            float nx = w > 1 ? (px - x) / (float)(w - 1) : 0.5f;
            float t = 0.86f - ny * 0.58f - nx * 0.12f + shadeBias;
            return Dither.SampleRamp(px, py, ramp, t);
        });

        // Top & left crisp lit chamfer
        if (w >= 2)
        {
            buf.DrawLine(x, y, x + w - 1, y, ramp[3]);
            buf.DrawLine(x, y + h - 1, x + w - 1, y + h - 1, ramp[0]);
        }
        if (h >= 2)
        {
            buf.DrawLine(x, y, x, y + h - 1, ramp[2]);
            buf.DrawLine(x + w - 1, y, x + w - 1, y + h - 1, ramp[0]);
        }

        if (withRivets && w >= 6 && h >= 5)
        {
            buf.Set(x + 1, y + 1, ramp[0]);
            buf.Set(x + w - 2, y + 1, ramp[0]);
            buf.Set(x + 1, y + h - 2, ramp[2]);
            buf.Set(x + w - 2, y + h - 2, ramp[0]);
        }
    }

    private static void DrawContourEllipse(
        PixelBuffer buf,
        int cx,
        int cy,
        int rx,
        int ry,
        string[] ramp,
        float shadeBias = 0f)
    {
        if (rx <= 0 || ry <= 0)
            return;

        PixelBuffer local = new(ArtWidth, ArtHeight);
        local.FillEllipse(cx, cy, rx, ry, (px, py, dx, dy, dist) =>
        {
            float t = 0.94f - dist * 0.32f - dy * 0.34f - dx * 0.16f + shadeBias;
            return Dither.SampleRamp(px, py, ramp, t);
        });

        ApplyLocalBevelPass(local, ramp);
        buf.BlitFrom(local);
    }

    private static void ApplyLocalBevelPass(PixelBuffer local, string[] ramp)
    {
        bool[] mask = local.CaptureNonNullMask(excludeOutline: false);
        for (int y = 1; y < ArtHeight - 1; y++)
        {
            for (int x = 1; x < ArtWidth - 1; x++)
            {
                if (!local.IsMasked(mask, x, y))
                    continue;

                bool topEmpty = !local.IsMasked(mask, x, y - 1);
                bool leftEmpty = !local.IsMasked(mask, x - 1, y);
                bool botEmpty = !local.IsMasked(mask, x, y + 1);
                bool rightEmpty = !local.IsMasked(mask, x + 1, y);

                if (botEmpty)
                {
                    local.Set(x, y, ramp[0]);
                }
                else if (rightEmpty && !topEmpty)
                {
                    local.Set(x, y, ramp[1]);
                }
                else if (topEmpty || (leftEmpty && !botEmpty))
                {
                    local.Set(x, y, ramp[3]);
                }
                else if (y >= 2 && y < ArtHeight - 2 && !local.IsMasked(mask, x, y - 2) && local.IsMasked(mask, x, y + 2))
                {
                    local.Set(x, y, Dither.BayerDither(x, y, ramp[2], ramp[3], 0.65f));
                }
            }
        }
    }

    private static void DrawRecessedPanelSeams(
        PixelBuffer buf,
        bool[] hullMask,
        int xStart,
        int xEnd,
        int yTop,
        int yBot,
        int stepX,
        string[] ramp,
        bool includeMidBelt = true)
    {
        int midY = (yTop + yBot) / 2;
        if (includeMidBelt)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                if (IsInteriorPixel(hullMask, x, midY))
                {
                    buf.Set(x, midY, ramp[0]);
                    if (IsInteriorPixel(hullMask, x, midY + 1))
                    {
                        buf.Set(x, midY + 1, ramp[2]);
                    }
                }
            }
        }

        for (int sx = xStart; sx <= xEnd; sx += Math.Max(6, stepX))
        {
            for (int y = yTop + 1; y <= yBot - 1; y++)
            {
                if (IsInteriorPixel(hullMask, sx, y))
                {
                    buf.Set(sx, y, ramp[0]);
                }
                if (IsInteriorPixel(hullMask, sx + 1, y))
                {
                    buf.Set(sx + 1, y, ramp[2]);
                }
            }
        }
    }

    private static bool IsInteriorPixel(bool[] mask, int x, int y)
    {
        if (x < 2 || x >= ArtWidth - 2 || y < 2 || y >= ArtHeight - 2)
            return false;
        int idx = y * ArtWidth + x;
        return mask[idx] && mask[idx - 1] && mask[idx + 1] && mask[idx - ArtWidth] && mask[idx + ArtWidth];
    }

    private static void DrawEngineBellAndCowling(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        int nozzleMountX,
        int topY,
        int bellLen,
        int bellHeight,
        string[] cowlingRamp)
    {
        int h = Math.Max(4, bellHeight);
        int w = Math.Max(4, bellLen);

        // Collar ring
        DrawBeveledArmorPlate(buf, nozzleMountX - 2, topY - 1, 3, h + 2, cowlingRamp, 0.05f);
        // Tapered ribbed exhaust cowling
        DrawBeveledArmorPlate(buf, nozzleMountX + 1, topY, w, h, cowlingRamp, -0.10f);
        for (int rx = nozzleMountX + 2; rx < nozzleMountX + w; rx += 2)
        {
            buf.DrawLine(rx, topY, rx, topY + h - 1, cowlingRamp[0]);
        }

        int throatH = Math.Max(2, h - 2);
        int throatY = topY + (h - throatH) / 2;
        buf.FillRect(nozzleMountX + w - 1, throatY, 2, throatH, Palette.HexDeepSpaceVoid);

        hp.EngineNozzles.Add(new HardpointRect(nozzleMountX + w + 1, throatY, 4, throatH));
    }

    private static void DrawBridgeViewportSlit(
        PixelBuffer buf,
        int x,
        int y,
        int w,
        int h,
        string glassHex,
        string highlightHex,
        string[] frameRamp)
    {
        buf.FillRect(x - 1, y - 1, w + 2, h + 2, frameRamp[0]);
        buf.DrawLine(x - 1, y - 1, x + w, y - 1, frameRamp[2]);
        buf.FillRect(x, y, w, h, glassHex);
        if (w >= 4)
        {
            buf.DrawLine(x, y, x + Math.Max(1, w / 2), y, highlightHex);
        }
        // Armored window mullion dividers
        for (int mx = x + 4; mx < x + w - 1; mx += 5)
        {
            buf.DrawLine(mx, y, mx, y + h - 1, frameRamp[0]);
        }
    }

    private static void ApplySeededModularGreebles(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipFaction faction,
        ShipClass shipClass,
        ShipVariantProfile variant,
        string[] primaryRamp,
        string[] secondaryRamp,
        string accentHex,
        DeterministicRng rng)
    {
        bool strictlySymmetrical = faction == ShipFaction.Orun;
        bool angularOnly = faction == ShipFaction.Kordani;
        bool[] preGreebleMask = buf.CaptureNonNullMask(excludeOutline: false);
        HardpointRect b = FindMaskBounds(preGreebleMask);
        if (b.IsEmpty)
            return;

        // 1. Seed-selected Bow Sensor / Antenna Assembly (clamped safely inside SafeMinArtX..SafeMaxArtX)
        int noseY = b.CenterY;
        int noseX = b.X;
        for (int x = b.X; x < Math.Min(b.X + 14, b.Right); x++)
        {
            if (buf.HasPixel(x, noseY))
            {
                noseX = x;
                break;
            }
        }

        if (noseX - 6 >= SafeMinArtX && !strictlySymmetrical)
        {
            switch (variant.BowSensorStyle)
            {
                case 0:
                    // Forward telemetry probe lance with glowing sensor tip
                    DrawBeveledArmorPlate(buf, noseX - 3, noseY - 2, 5, 4, secondaryRamp);
                    buf.FillRect(noseX - 6, noseY - 1, 4, 2, primaryRamp[3]);
                    buf.Set(noseX - 6, noseY, accentHex);
                    break;
                case 1:
                    // Twin forward sensor whiskers
                    buf.DrawLine(noseX - 6, noseY - 3, noseX + 1, noseY - 3, primaryRamp[3]);
                    buf.DrawLine(noseX - 5, noseY + 2, noseX + 1, noseY + 2, secondaryRamp[2]);
                    buf.Set(noseX - 6, noseY - 3, accentHex);
                    break;
                case 2:
                    // Armored bow radar blister
                    DrawBeveledArmorPlate(buf, noseX - 5, noseY - 3, 6, 6, secondaryRamp, 0.04f);
                    buf.FillRect(noseX - 4, noseY - 1, 2, 2, accentHex);
                    break;
                default:
                    // Armored bow splitter vane / chin probe
                    ReadOnlySpan<(int X, int Y)> vane = [(noseX - 6, noseY), (noseX, noseY - 4), (noseX + 2, noseY + 3), (noseX - 3, noseY + 3)];
                    DrawBeveledHullPoly(buf, vane, secondaryRamp);
                    break;
            }
        }

        // 2. Seed-selected Dorsal Utility / Turret / Radiator Module anchored cleanly onto top solid spine
        int dorsalX = Math.Clamp(hp.ReactorCore.X - 6 + variant.StripeOffsetX * 3, b.X + 18, b.Right - 24);
        int dorsalY = FindSurfaceY(preGreebleMask, dorsalX, fromTop: true);
        if (dorsalY >= SafeMinArtY + 6 && dorsalY > 0)
        {
            if (strictlySymmetrical)
            {
                int symX = Math.Clamp(hp.ReactorCore.X - 6 + variant.StripeOffsetX * 3, b.X + 22, b.Right - 28);
                int topY = FindSurfaceY(preGreebleMask, symX, fromTop: true);
                int botY = FindSurfaceY(preGreebleMask, symX, fromTop: false);
                if (topY >= SafeMinArtY + 5 && botY <= SafeMaxArtY - 5)
                {
                    switch (variant.DorsalModuleStyle)
                    {
                        case 0:
                            DrawBeveledArmorPlate(buf, symX - 7, topY - 3, 14, 4, secondaryRamp);
                            DrawBeveledArmorPlate(buf, symX - 7, botY, 14, 4, secondaryRamp, -0.2f);
                            break;
                        case 1:
                            buf.FillRect(symX - 9, topY - 3, 18, 4, primaryRamp[2]);
                            buf.FillRect(symX - 9, botY, 18, 4, primaryRamp[1]);
                            buf.FillRect(symX - 2, topY - 4, 5, 2, accentHex);
                            buf.FillRect(symX - 2, botY + 3, 5, 2, accentHex);
                            break;
                        case 2:
                            DrawBeveledArmorPlate(buf, symX - 11, topY - 4, 8, 4, secondaryRamp);
                            DrawBeveledArmorPlate(buf, symX + 3, topY - 4, 8, 4, secondaryRamp);
                            DrawBeveledArmorPlate(buf, symX - 11, botY + 1, 8, 4, secondaryRamp, -0.2f);
                            DrawBeveledArmorPlate(buf, symX + 3, botY + 1, 8, 4, secondaryRamp, -0.2f);
                            break;
                        default:
                            ReadOnlySpan<(int X, int Y)> topArch = [(symX - 9, topY + 1), (symX - 5, topY - 4), (symX + 5, topY - 4), (symX + 9, topY + 1)];
                            ReadOnlySpan<(int X, int Y)> botArch = [(symX - 9, botY), (symX - 5, botY + 4), (symX + 5, botY + 4), (symX + 9, botY)];
                            DrawBeveledHullPoly(buf, topArch, secondaryRamp);
                            DrawBeveledHullPoly(buf, botArch, secondaryRamp, -0.2f);
                            break;
                    }
                }
            }
            else
            {
                switch (variant.DorsalModuleStyle)
                {
                    case 0:
                        // Twin-whip comms tower + blister
                        DrawBeveledArmorPlate(buf, dorsalX - 4, dorsalY - 3, 9, 4, secondaryRamp);
                        buf.DrawLine(dorsalX - 2, dorsalY - 6, dorsalX - 2, dorsalY - 3, primaryRamp[3]);
                        buf.DrawLine(dorsalX + 2, dorsalY - 5, dorsalX + 2, dorsalY - 3, primaryRamp[2]);
                        buf.Set(dorsalX - 2, dorsalY - 6, accentHex);
                        break;
                    case 1:
                        // Wide louvered dorsal radiator manifold
                        DrawBeveledArmorPlate(buf, dorsalX - 9, dorsalY - 4, 18, 5, secondaryRamp);
                        for (int rx = dorsalX - 7; rx <= dorsalX + 7; rx += 3)
                        {
                            buf.DrawLine(rx, dorsalY - 3, rx, dorsalY, primaryRamp[0]);
                        }
                        buf.DrawLine(dorsalX - 8, dorsalY - 4, dorsalX + 8, dorsalY - 4, accentHex);
                        break;
                    case 2:
                        // Armored dorsal cupola / turret housing with forward stub barrels
                        if (angularOnly)
                        {
                            ReadOnlySpan<(int X, int Y)> fin = [(dorsalX - 8, dorsalY + 1), (dorsalX - 2, dorsalY - 5), (dorsalX + 8, dorsalY + 1)];
                            DrawBeveledHullPoly(buf, fin, secondaryRamp);
                        }
                        else
                        {
                            DrawContourEllipse(buf, dorsalX, dorsalY - 2, 7, 3, secondaryRamp);
                        }
                        buf.FillRect(dorsalX - 12, dorsalY - 3, 6, 2, primaryRamp[3]);
                        break;
                    default:
                        // Slanted dorsal utility / lifeboat module
                        ReadOnlySpan<(int X, int Y)> pod = [(dorsalX - 8, dorsalY + 1), (dorsalX - 5, dorsalY - 4), (dorsalX + 7, dorsalY - 4), (dorsalX + 10, dorsalY + 1)];
                        DrawBeveledHullPoly(buf, pod, primaryRamp, 0.04f);
                        buf.DrawLine(dorsalX - 4, dorsalY - 2, dorsalX + 6, dorsalY - 2, accentHex);
                        break;
                }
            }
        }

        // 3. Seed-selected Ventral Auxiliary Pod / Fin anchored onto bottom spine
        int ventralProbeX = Math.Clamp(hp.ReactorCore.X + 8 - variant.StripeOffsetX * 3, b.X + 20, b.Right - 24);
        int ventralSurfaceY = FindSurfaceY(preGreebleMask, ventralProbeX, fromTop: false);
        if (!strictlySymmetrical && ventralSurfaceY > 0 && ventralSurfaceY <= SafeMaxArtY - 6)
        {
            switch (variant.VentralModuleStyle)
            {
                case 0:
                    // Swept ventral stabilizer fin
                    ReadOnlySpan<(int X, int Y)> keelFin = [(ventralProbeX - 6, ventralSurfaceY), (ventralProbeX + 3, ventralSurfaceY + 5), (ventralProbeX + 9, ventralSurfaceY)];
                    DrawBeveledHullPoly(buf, keelFin, secondaryRamp, -0.15f);
                    break;
                case 1:
                    // Underslung auxiliary coolant/fuel drum
                    DrawBeveledArmorPlate(buf, ventralProbeX - 9, ventralSurfaceY - 1, 18, 5, secondaryRamp, -0.15f, withRivets: true);
                    buf.DrawLine(ventralProbeX - 4, ventralSurfaceY, ventralProbeX - 4, ventralSurfaceY + 3, accentHex);
                    buf.DrawLine(ventralProbeX + 4, ventralSurfaceY, ventralProbeX + 4, ventralSurfaceY + 3, accentHex);
                    break;
                case 2:
                    // Twin ventral sensor blisters
                    DrawBeveledArmorPlate(buf, ventralProbeX - 8, ventralSurfaceY - 1, 7, 4, secondaryRamp);
                    DrawBeveledArmorPlate(buf, ventralProbeX + 2, ventralSurfaceY - 1, 7, 4, secondaryRamp);
                    buf.Set(ventralProbeX - 6, ventralSurfaceY + 2, accentHex);
                    break;
                default:
                    // Heavy armored belly keel fairing
                    DrawBeveledArmorPlate(buf, ventralProbeX - 11, ventralSurfaceY - 1, 22, 4, primaryRamp, -0.20f);
                    buf.DrawLine(ventralProbeX - 8, ventralSurfaceY + 2, ventralProbeX + 8, ventralSurfaceY + 2, secondaryRamp[0]);
                    break;
            }
        }

        // 4. Seed-varied Livery, Stripes & Subtle Armor Plate Tonal Shift (strictly clipped inside hull mask)
        bool[] currentMask = buf.CaptureNonNullMask(excludeOutline: false);
        ApplyClippedLiveryAndWeathering(buf, currentMask, hp, faction, shipClass, variant, primaryRamp, secondaryRamp, accentHex, rng);
    }

    private static void ApplyClippedLiveryAndWeathering(
        PixelBuffer buf,
        bool[] hullMask,
        ShipHardpointLayoutV2 hp,
        ShipFaction faction,
        ShipClass shipClass,
        ShipVariantProfile variant,
        string[] primaryRamp,
        string[] secondaryRamp,
        string accentHex,
        DeterministicRng rng)
    {
        HardpointRect b = FindMaskBounds(hullMask);
        if (b.IsEmpty)
            return;

        // Livery bands & chevrons clipped strictly to interior hull pixels
        int bandX = Math.Clamp(hp.ReactorCore.X - 18 + variant.StripeOffsetX * 3, b.X + 10, b.Right - 14);
        int stripeY = Math.Clamp(hp.ReactorCore.Y - 4 + (variant.LiveryPattern % 3) * 2, b.Y + 4, b.Bottom - 5);

        // Protect glass canopies, lit bridge slits, brass inlays, and Prunjun flames from livery/wear overwrites
        bool IsPaintSafe(int px, int py)
        {
            if (!IsInteriorPixel(hullMask, px, py))
                return false;
            if (faction == ShipFaction.Orun && shipClass == ShipClass.Cargo && px >= 90 && px <= 178 && (py <= 52 || py >= 67))
                return false;

            string? cur = buf.Get(px, py);
            if (cur == null)
                return false;

            if (cur.Equals("#38bdf8", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#e0f2fe", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#93c5fd", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#7dd3fc", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#1e3a5f", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#ffb84d", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals("#fef08a", StringComparison.OrdinalIgnoreCase) ||
                cur.Equals(Palette.HexOrunBrassTrim, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (faction == ShipFaction.Prunjun &&
                (cur.Equals("#b91c1c", StringComparison.OrdinalIgnoreCase) ||
                 cur.Equals("#ef4444", StringComparison.OrdinalIgnoreCase) ||
                 cur.Equals("#f97316", StringComparison.OrdinalIgnoreCase) ||
                 cur.Equals("#facc15", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        switch (variant.LiveryPattern)
        {
            case 0:
                // Wide bold horizontal corporate/fleet trim stripe + crisp accent pin
                for (int x = b.X + 6; x < b.Right - 6; x++)
                {
                    if (faction == ShipFaction.Orun)
                    {
                        if (IsPaintSafe(x, hp.ReactorCore.Y - 6)) buf.Set(x, hp.ReactorCore.Y - 6, accentHex);
                        if (IsPaintSafe(x, hp.ReactorCore.Y + 6)) buf.Set(x, hp.ReactorCore.Y + 6, accentHex);
                    }
                    else
                    {
                        if (IsPaintSafe(x, stripeY))
                            buf.Set(x, stripeY, secondaryRamp[1]);
                        if (IsPaintSafe(x, stripeY + 1))
                            buf.Set(x, stripeY + 1, secondaryRamp[2]);
                        if (IsPaintSafe(x, stripeY + 2))
                            buf.Set(x, stripeY + 2, accentHex);
                    }
                }
                break;

            case 1:
                // Bold twin vertical hull ID bands + contrasting nose cap
                for (int dx = 0; dx < 6; dx++)
                {
                    for (int y = b.Y + 2; y < b.Bottom - 2; y++)
                    {
                        if (IsPaintSafe(bandX + dx, y))
                            buf.Set(bandX + dx, y, secondaryRamp[1]);
                        if (IsPaintSafe(bandX + 9 + dx, y))
                            buf.Set(bandX + 9 + dx, y, dx < 2 ? accentHex : secondaryRamp[2]);
                    }
                }
                for (int x = b.X + 3; x < Math.Min(b.X + 14, b.Right - 4); x++)
                {
                    for (int y = b.Y + 3; y < b.Bottom - 3; y++)
                    {
                        if (IsPaintSafe(x, y))
                            buf.Set(x, y, secondaryRamp[1]);
                    }
                }
                break;

            case 2:
                // Bold angled tactical chevron stripes
                for (int i = -14; i <= 14; i++)
                {
                    int py = hp.ReactorCore.Y + i;
                    int px = bandX + Math.Abs(i);
                    for (int w = 0; w < 5; w++)
                    {
                        if (IsPaintSafe(px + w, py))
                        {
                            buf.Set(px + w, py, w == 2 ? accentHex : secondaryRamp[2]);
                        }
                        if (IsPaintSafe(px + 9 + w, py))
                        {
                            buf.Set(px + 9 + w, py, secondaryRamp[1]);
                        }
                    }
                }
                break;

            default:
                // Two-tone upper/lower armor block split + contrasting panel plates
                DeterministicRng plateRng = rng.Fork("plate_tone");
                if (faction != ShipFaction.Orun)
                {
                    for (int y = hp.ReactorCore.Y + 1; y < b.Bottom - 2; y++)
                    {
                        for (int x = b.X + 8; x < b.Right - 8; x++)
                        {
                            if (IsPaintSafe(x, y))
                            {
                                buf.Set(x, y, Dither.BayerDither(x, y, secondaryRamp[0], secondaryRamp[1], 0.45f));
                            }
                        }
                    }
                }
                for (int p = 0; p < 4; p++)
                {
                    int px0 = plateRng.NextInt(b.X + 8, Math.Max(b.X + 10, b.Right - 18));
                    int py0 = faction == ShipFaction.Orun
                        ? hp.ReactorCore.Y - plateRng.NextInt(5, 14)
                        : plateRng.NextInt(b.Y + 3, Math.Max(b.Y + 5, b.Bottom - 8));
                    int pw = plateRng.NextInt(10, 18);
                    int ph = plateRng.NextInt(4, 7);
                    string patchCol = (p % 2 == 0) ? primaryRamp[1] : secondaryRamp[2];
                    for (int y = py0; y < py0 + ph; y++)
                    {
                        for (int x = px0; x < px0 + pw; x++)
                        {
                            if (IsPaintSafe(x, y))
                            {
                                buf.Set(x, y, Dither.BayerDither(x, y, patchCol, primaryRamp[3], p % 2 == 0 ? 0.25f : 0.65f));
                            }
                            if (faction == ShipFaction.Orun)
                            {
                                int mirrorY = hp.ReactorCore.Y + (hp.ReactorCore.Y - y);
                                if (IsPaintSafe(x, mirrorY))
                                {
                                    buf.Set(x, mirrorY, Dither.BayerDither(x, mirrorY, patchCol, primaryRamp[2], 0.30f));
                                }
                            }
                        }
                    }
                }
                break;
        }

        // Hazard accent stripe on industrial factions
        if (faction is ShipFaction.Brengop or ShipFaction.MiningGuild or ShipFaction.FreeTraders && variant.PylonCowlingVariant > 0)
        {
            int hx0 = Math.Clamp(b.X + 8, b.X + 4, b.Right - 14);
            for (int y = hp.ReactorCore.Y - 4; y <= hp.ReactorCore.Y + 4; y++)
            {
                for (int x = hx0; x < hx0 + 8; x++)
                {
                    if (IsPaintSafe(x, y))
                    {
                        bool stripe = (((x + y) / 2) % 2) == 0;
                        buf.Set(x, y, stripe ? accentHex : secondaryRamp[0]);
                    }
                }
            }
        }

        // Procedural Wear & Weathering (weld patches & grime streaks; skip Orun symmetrical monoliths)
        if (faction != ShipFaction.Orun)
        {
            bool ruggedFaction = faction is ShipFaction.Brengop or ShipFaction.Krushvak or ShipFaction.FreeTraders
                or ShipFaction.MiningGuild or ShipFaction.Tolvak or ShipFaction.Prunjun;
            int patchCount = ruggedFaction ? (1 + variant.WearLevel) : (variant.WearLevel >= 2 ? 1 : 0);
            DeterministicRng wearRng = rng.Fork($"wear_{variant.PatchSeedOffset}");
            for (int w = 0; w < patchCount; w++)
            {
                int wx = wearRng.NextInt(b.X + 10, Math.Max(b.X + 12, b.Right - 20));
                int wy = wearRng.NextInt(b.Y + 4, Math.Max(b.Y + 6, b.Bottom - 10));
                int ww = wearRng.NextInt(7, 13);
                int wh = wearRng.NextInt(4, 7);

                for (int y = wy; y < wy + wh; y++)
                {
                    for (int x = wx; x < wx + ww; x++)
                    {
                        if (!IsPaintSafe(x, y))
                            continue;

                        bool edge = x == wx || x == wx + ww - 1 || y == wy || y == wy + wh - 1;
                        buf.Set(x, y, edge ? primaryRamp[0] : secondaryRamp[1]);
                    }
                }
            }

            // Subtle aft exhaust soot streaks
            if (variant.WearLevel >= 1 && shipClass != ShipClass.Diplomatic)
            {
                int sootStart = Math.Max(b.X + 12, b.Right - 26);
                for (int y = b.Y + 4; y < b.Bottom - 4; y += 5)
                {
                    for (int x = sootStart; x < b.Right - 2; x++)
                    {
                        if (IsPaintSafe(x, y))
                        {
                            float t = Math.Clamp((x - sootStart) / 22.0f, 0.1f, 0.65f);
                            buf.Set(x, y, Dither.BayerDither(x, y, buf.Get(x, y) ?? primaryRamp[1], primaryRamp[0], t));
                        }
                    }
                }
            }
        }
    }

    private static HardpointRect FindMaskBounds(bool[] mask)
    {
        int minX = ArtWidth - 1;
        int maxX = 0;
        int minY = ArtHeight - 1;
        int maxY = 0;
        bool any = false;

        for (int y = 0; y < ArtHeight; y++)
        {
            for (int x = 0; x < ArtWidth; x++)
            {
                if (mask[y * ArtWidth + x])
                {
                    any = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        return any ? new HardpointRect(minX, minY, maxX - minX + 1, maxY - minY + 1) : default;
    }

    private static int FindSurfaceY(bool[] mask, int x, bool fromTop)
    {
        if (x < 0 || x >= ArtWidth)
            return -1;

        if (fromTop)
        {
            for (int y = 0; y < ArtHeight; y++)
            {
                if (mask[y * ArtWidth + x])
                    return y;
            }
        }
        else
        {
            for (int y = ArtHeight - 1; y >= 0; y--)
            {
                if (mask[y * ArtWidth + x])
                    return y;
            }
        }
        return -1;
    }

    // =========================================================================
    // FACTION BUILDER DISPATCH (ALL 11 FACTIONS + ALL ALLOWED CLASSES)
    // =========================================================================
    private static void BuildShipHullGeometryV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipFaction faction,
        ShipClass shipClass,
        bool isDecommissioned,
        bool hasHullScorch,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        switch (faction)
        {
            case ShipFaction.GalacticUnion:
                BuildGalacticUnionShipV2(buf, ref hp, shipClass, isDecommissioned, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Kordani:
                BuildKordaniShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Brengop:
                BuildBrengopShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Krushvak:
                BuildKrushvakShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Orun:
                BuildOrunShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Tolvak:
                BuildTolvakShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.Prunjun:
                BuildPrunjunHackJobShipV2(buf, ref hp, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.FreeTraders:
                BuildFreeTraderShipV2(buf, ref hp, shipClass, hasHullScorch, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.FarLineTransit:
                BuildFarLineTransitShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.MiningGuild:
                BuildMiningGuildShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            case ShipFaction.ZenithInterstellar:
                BuildZenithInterstellarShipV2(buf, ref hp, shipClass, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
            default:
                BuildGalacticUnionShipV2(buf, ref hp, shipClass, false, variant, rng, out engineGlowHex, out foreClampX, out aftClampX);
                break;
        }
    }

    // =========================================================================
    // 1. GALACTIC UNION (GUV-)
    // =========================================================================
    private static void BuildGalacticUnionShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        bool isDecommissioned,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] primaryRamp = isDecommissioned ? Palette.GuDecommissionedWhiteRamp : Palette.GuPrimaryWhiteRamp;
        string[] secondaryRamp = Palette.GuSecondaryGunmetalRamp;
        engineGlowHex = isDecommissioned ? Palette.HexGuDecommissionedEngineGlow : Palette.HexGuEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // Thick fused armored saucer-escort with tucked stubby pylons (x = 68..214)
                ReadOnlySpan<(int X, int Y)> fusedBody =
                [
                    (74, 48), (94, 38), (190, 38), (206, 46), (206, 74), (190, 82), (94, 82), (74, 72)
                ];
                DrawBeveledHullPoly(buf, fusedBody, primaryRamp);

                // Heavy fused forward saucer
                DrawContourEllipse(buf, 102, 60, 32, 18, primaryRamp, 0.04f);

                // Armored gunmetal nose cowling & twin pulse cannons
                DrawBeveledArmorPlate(buf, 68, 49, 12, 5, secondaryRamp);
                DrawBeveledArmorPlate(buf, 68, 66, 12, 5, secondaryRamp, -0.15f);
                DrawBeveledArmorPlate(buf, 74, 54, 18, 12, secondaryRamp);

                // Armored tactical bridge cupola & recessed bridge slits
                DrawContourEllipse(buf, 108, 41, 14, 5, secondaryRamp, 0.06f);
                DrawBridgeViewportSlit(buf, 98, 40, 12, 2, "#38bdf8", "#e0f2fe", secondaryRamp);

                bool[] bodyMask = buf.CaptureNonNullMask(excludeOutline: false);
                DrawRecessedPanelSeams(buf, bodyMask, 92, 198, 42, 78, 16, primaryRamp);

                // Tucked stubby upper/lower warp pylons (x = 152..212)
                DrawBeveledArmorPlate(buf, 152, 27, 56, 10, secondaryRamp, 0.08f, withRivets: true);
                buf.FillRect(156, 29, 46, 5, primaryRamp[2]);
                buf.DrawLine(162, 31, 198, 31, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 207, 28, 6, 8, secondaryRamp);

                DrawBeveledArmorPlate(buf, 152, 83, 56, 10, secondaryRamp, -0.15f, withRivets: true);
                buf.FillRect(156, 85, 46, 5, primaryRamp[1]);
                buf.DrawLine(162, 87, 198, 87, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 207, 84, 6, 8, secondaryRamp);

                hp.ReactorCore = (146, 60);
                hp.WeaponsBay = new HardpointRect(68, 52, 18, 16);
                hp.BridgeComms = new HardpointRect(100, 38, 18, 12);
                hp.CargoBay = new HardpointRect(152, 66, 24, 12);
                hp.UpperWarpPylon = new HardpointRect(156, 28, 50, 14);
                hp.LowerWarpPylon = new HardpointRect(156, 82, 50, 12);
                hp.WeaponMuzzlePixels.Add((67, 51));
                hp.WeaponMuzzlePixels.Add((67, 68));
                hp.NavStrobes.Add((108, 36));
                hp.NavStrobes.Add((204, 27));
                hp.NavStrobes.Add((204, 92));
                foreClampX = 116;
                aftClampX = 178;
                break;
            }

            case ShipClass.Civilian:
            {
                // Compact ~114px shuttle/runabout pod (x = 84..198) with two short underslung pylons
                ReadOnlySpan<(int X, int Y)> runaboutPod =
                [
                    (84, 58), (98, 42), (176, 41), (190, 50), (190, 68), (176, 73), (92, 73)
                ];
                DrawBeveledHullPoly(buf, runaboutPod, primaryRamp);

                // Curved wrap-around cockpit canopy
                ReadOnlySpan<(int X, int Y)> canopy = [(89, 57), (100, 46), (120, 46), (116, 57)];
                buf.FillPoly(canopy, (px, py) => Dither.BayerDither(px, py, secondaryRamp[0], "#38bdf8", 0.58f));
                buf.DrawLine(98, 48, 114, 48, "#e0f2fe");

                // Compact dorsal telemetry fin
                ReadOnlySpan<(int X, int Y)> tailFin = [(152, 41), (164, 31), (178, 31), (174, 41)];
                DrawBeveledHullPoly(buf, tailFin, secondaryRamp);

                bool[] bodyMask = buf.CaptureNonNullMask(excludeOutline: false);
                DrawRecessedPanelSeams(buf, bodyMask, 100, 184, 45, 70, 16, primaryRamp);

                // Underslung warp pylons (x = 138..198)
                DrawBeveledArmorPlate(buf, 142, 73, 6, 12, secondaryRamp);
                DrawBeveledArmorPlate(buf, 168, 73, 6, 12, secondaryRamp);
                DrawBeveledArmorPlate(buf, 138, 75, 52, 7, primaryRamp, 0.04f);
                buf.DrawLine(144, 78, 184, 78, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 189, 76, 5, 5, secondaryRamp);

                DrawBeveledArmorPlate(buf, 144, 84, 48, 7, secondaryRamp, -0.15f);
                buf.DrawLine(150, 87, 186, 87, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 191, 85, 5, 5, secondaryRamp);

                hp.ReactorCore = (142, 58);
                hp.WeaponsBay = new HardpointRect(86, 58, 14, 12);
                hp.BridgeComms = new HardpointRect(98, 46, 18, 12);
                hp.CargoBay = new HardpointRect(152, 58, 22, 12);
                hp.UpperWarpPylon = new HardpointRect(144, 74, 44, 12);
                hp.LowerWarpPylon = new HardpointRect(148, 84, 42, 8);
                hp.WeaponMuzzlePixels.Add((84, 62));
                hp.NavStrobes.Add((166, 31));
                hp.NavStrobes.Add((188, 75));
                hp.NavStrobes.Add((190, 90));
                foreClampX = 114;
                aftClampX = 166;
                break;
            }

            case ShipClass.Cargo:
            {
                // Stretched ~180px utility spine freighter (x = 50..230) with 3 modular cargo pods & wide pylons
                ReadOnlySpan<(int X, int Y)> topStrut = [(164, 44), (174, 44), (192, 26), (182, 26)];
                ReadOnlySpan<(int X, int Y)> botStrut = [(164, 64), (174, 64), (192, 91), (182, 91)];
                DrawBeveledHullPoly(buf, topStrut, secondaryRamp);
                DrawBeveledHullPoly(buf, botStrut, secondaryRamp, -0.2f);

                // Long central white spine (x = 80..220, y = 42..57)
                DrawBeveledArmorPlate(buf, 80, 42, 140, 15, primaryRamp, 0.04f, withRivets: true);
                buf.DrawLine(84, 49, 214, 49, secondaryRamp[1]);

                // Blunt utility bow + bridge cupola
                ReadOnlySpan<(int X, int Y)> bluntBow =
                [
                    (50, 47), (60, 39), (86, 39), (86, 68), (60, 68), (50, 59)
                ];
                DrawBeveledHullPoly(buf, bluntBow, primaryRamp);
                DrawBeveledArmorPlate(buf, 50, 49, 7, 10, secondaryRamp);
                DrawBeveledArmorPlate(buf, 64, 33, 18, 7, secondaryRamp);
                DrawBridgeViewportSlit(buf, 66, 35, 10, 3, "#38bdf8", "#e0f2fe", secondaryRamp);

                // 3 blocky modular cargo pods suspended beneath spine
                int[] podStarts = [90, 126, 162];
                for (int i = 0; i < podStarts.Length; i++)
                {
                    int px0 = podStarts[i];
                    string[] podRamp = (i % 2 == 0) ? secondaryRamp : primaryRamp;
                    DrawBeveledArmorPlate(buf, px0, 57, 32, 24, podRamp, i % 2 == 0 ? 0.02f : -0.05f, withRivets: true);
                    buf.DrawLine(px0 + 8, 59, px0 + 8, 79, secondaryRamp[0]);
                    buf.DrawLine(px0 + 22, 59, px0 + 22, 79, secondaryRamp[0]);
                }

                // Wide-set rear warp nacelles (x = 168..228)
                DrawBeveledArmorPlate(buf, 168, 22, 54, 10, primaryRamp, 0.06f);
                buf.DrawLine(174, 27, 216, 27, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 221, 23, 6, 8, secondaryRamp);

                DrawBeveledArmorPlate(buf, 168, 86, 54, 10, primaryRamp, -0.14f);
                buf.DrawLine(174, 91, 216, 91, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 221, 87, 6, 8, secondaryRamp);

                hp.ReactorCore = (146, 50);
                hp.WeaponsBay = new HardpointRect(56, 48, 16, 14);
                hp.BridgeComms = new HardpointRect(64, 33, 18, 12);
                hp.CargoBay = new HardpointRect(128, 60, 28, 18);
                hp.UpperWarpPylon = new HardpointRect(168, 25, 50, 16);
                hp.LowerWarpPylon = new HardpointRect(168, 84, 50, 12);
                hp.WeaponMuzzlePixels.Add((51, 51));
                hp.WeaponMuzzlePixels.Add((51, 57));
                hp.NavStrobes.Add((72, 32));
                hp.NavStrobes.Add((218, 22));
                hp.NavStrobes.Add((218, 96));
                foreClampX = 104;
                aftClampX = 186;
                break;
            }

            default: // Diplomatic (Sleek Long-Necked Explorer Cruiser)
            {
                ReadOnlySpan<(int X, int Y)> upperStrut = [(148, 48), (158, 48), (180, 31), (170, 31)];
                ReadOnlySpan<(int X, int Y)> lowerStrut = [(148, 72), (158, 72), (180, 89), (170, 89)];
                DrawBeveledHullPoly(buf, upperStrut, secondaryRamp);
                DrawBeveledHullPoly(buf, lowerStrut, secondaryRamp, -0.18f);

                // Slender connecting neck + tapered engineering fuselage
                DrawBeveledArmorPlate(buf, 104, 54, 36, 12, secondaryRamp);
                ReadOnlySpan<(int X, int Y)> fuselage =
                [
                    (132, 45), (196, 45), (212, 60), (196, 75), (132, 75)
                ];
                DrawBeveledHullPoly(buf, fuselage, primaryRamp);

                // Swept dorsal diplomatic fin
                ReadOnlySpan<(int X, int Y)> dorsalFin = [(138, 45), (158, 30), (174, 30), (168, 45)];
                DrawBeveledHullPoly(buf, dorsalFin, primaryRamp, 0.08f);

                // Elongated forward saucer bow
                DrawContourEllipse(buf, 82, 60, 30, 13, primaryRamp, 0.06f);
                DrawContourEllipse(buf, 86, 47, 13, 5, primaryRamp, 0.08f);
                DrawBridgeViewportSlit(buf, 58, 59, 44, 2, "#38bdf8", "#e0f2fe", secondaryRamp);

                bool[] bodyMask = buf.CaptureNonNullMask(excludeOutline: false);
                DrawRecessedPanelSeams(buf, bodyMask, 136, 200, 48, 72, 16, primaryRamp);

                // Long outrigger warp pylons
                DrawBeveledArmorPlate(buf, 164, 25, 58, 9, primaryRamp, 0.08f);
                buf.DrawLine(172, 29, 216, 29, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 221, 26, 6, 7, secondaryRamp);

                DrawBeveledArmorPlate(buf, 164, 86, 58, 9, primaryRamp, -0.12f);
                buf.DrawLine(172, 90, 216, 90, engineGlowHex);
                DrawEngineBellAndCowling(buf, ref hp, 221, 87, 6, 7, secondaryRamp);

                hp.ReactorCore = (160, 60);
                hp.WeaponsBay = new HardpointRect(58, 54, 16, 12);
                hp.BridgeComms = new HardpointRect(78, 43, 18, 12);
                hp.CargoBay = new HardpointRect(160, 64, 24, 12);
                hp.UpperWarpPylon = new HardpointRect(168, 27, 50, 14);
                hp.LowerWarpPylon = new HardpointRect(168, 84, 50, 11);
                hp.WeaponMuzzlePixels.Add((53, 59));
                hp.WeaponMuzzlePixels.Add((53, 62));
                hp.NavStrobes.Add((86, 42));
                hp.NavStrobes.Add((218, 25));
                hp.NavStrobes.Add((218, 94));
                foreClampX = 96;
                aftClampX = 186;
                break;
            }
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.GalacticUnion, shipClass, variant, primaryRamp, secondaryRamp, engineGlowHex, rng);

        if (isDecommissioned)
        {
            ApplyGuDecommissionedWeatheringV2(buf, rng);
        }
    }

    private static void ApplyGuDecommissionedWeatheringV2(PixelBuffer buf, DeterministicRng rng)
    {
        bool[] hullMask = buf.CaptureNonNullMask(excludeOutline: true);
        HardpointRect b = FindMaskBounds(hullMask);
        if (b.IsEmpty)
            return;

        DeterministicRng weatherRng = rng.Fork("gu_decomm_v2");
        int patchCount = weatherRng.NextInt(3, 6);
        for (int i = 0; i < patchCount; i++)
        {
            int px = weatherRng.NextInt(b.X + 8, Math.Max(b.X + 10, b.Right - 22));
            int py = weatherRng.NextInt(b.Y + 4, Math.Max(b.Y + 6, b.Bottom - 12));
            int pw = weatherRng.NextInt(10, 18);
            int ph = weatherRng.NextInt(5, 9);

            for (int y = py; y < py + ph; y++)
            {
                for (int x = px; x < px + pw; x++)
                {
                    if (IsInteriorPixel(hullMask, x, y))
                    {
                        bool isRivetEdge = (x == px || x == px + pw - 1) && ((y - py) % 2 == 0);
                        buf.Set(x, y, isRivetEdge ? "#1c2026" : "#3d444d");
                    }
                }
            }
        }

        int aftStart = Math.Max(b.X + 20, b.Right - 46);
        for (int y = b.Y; y < b.Bottom; y++)
        {
            for (int x = aftStart; x < b.Right; x++)
            {
                if (IsInteriorPixel(hullMask, x, y) && ((y % 6 == 0) || y <= b.Y + 12 || y >= b.Bottom - 12))
                {
                    float scorchT = Math.Clamp((x - aftStart) / 42.0f, 0.1f, 0.78f);
                    buf.Set(x, y, Dither.BayerDither(x, y, buf.Get(x, y) ?? "#4e565e", "#1c2026", scorchT));
                }
            }
        }
    }

    // =========================================================================
    // 2. KORDANI (KDF-) — Zero curves: sharp angular blades & crests
    // =========================================================================
    private static void BuildKordaniShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] primaryRamp = Palette.KordaniPrimaryBlueGreenRamp;
        string[] secondaryRamp = Palette.KordaniSecondaryDarkSlateRamp;
        engineGlowHex = Palette.HexKordaniEngineGlow;

        if (shipClass == ShipClass.Diplomatic)
        {
            // Razor-thin needle-nosed command yacht with smooth swept-back delta wings & tall regal crest
            ReadOnlySpan<(int X, int Y)> topWing = [(134, 52), (190, 24), (224, 24), (202, 52)];
            ReadOnlySpan<(int X, int Y)> botWing = [(134, 68), (190, 96), (224, 96), (202, 68)];
            DrawBeveledHullPoly(buf, topWing, secondaryRamp);
            DrawBeveledHullPoly(buf, botWing, secondaryRamp, -0.22f);

            ReadOnlySpan<(int X, int Y)> regalCrest = [(96, 52), (118, 22), (138, 22), (130, 52)];
            DrawBeveledHullPoly(buf, regalCrest, primaryRamp, 0.08f);
            buf.DrawLine(118, 25, 134, 25, engineGlowHex);

            ReadOnlySpan<(int X, int Y)> needleHull = [(50, 60), (96, 50), (212, 50), (226, 60), (212, 70), (96, 70)];
            DrawBeveledHullPoly(buf, needleHull, primaryRamp);
            buf.DrawLine(58, 60, 218, 60, engineGlowHex);
            DrawBridgeViewportSlit(buf, 108, 44, 14, 3, engineGlowHex, "#fef08a", secondaryRamp);

            DrawBeveledArmorPlate(buf, 186, 28, 34, 7, primaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 219, 29, 5, 5, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 224, 57, 5, 6, secondaryRamp);
            DrawBeveledArmorPlate(buf, 186, 85, 34, 7, primaryRamp, -0.15f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 86, 5, 5, secondaryRamp);

            hp.ReactorCore = (158, 60);
            hp.WeaponsBay = new HardpointRect(58, 54, 16, 12);
            hp.BridgeComms = new HardpointRect(108, 34, 18, 14);
            hp.CargoBay = new HardpointRect(166, 58, 22, 10);
            hp.UpperWarpPylon = new HardpointRect(184, 28, 36, 12);
            hp.LowerWarpPylon = new HardpointRect(184, 83, 36, 10);
            hp.WeaponMuzzlePixels.Add((52, 60));
            hp.NavStrobes.Add((126, 22));
            hp.NavStrobes.Add((218, 24));
            hp.NavStrobes.Add((218, 95));
            foreClampX = 118;
            aftClampX = 186;
        }
        else
        {
            // MIL: Compact wide aggressive spearhead with forward-swept blades & serrated dorsal spikes
            ReadOnlySpan<(int X, int Y)> topBlade = [(72, 23), (124, 28), (160, 44), (104, 44)];
            ReadOnlySpan<(int X, int Y)> botBlade = [(72, 97), (124, 92), (160, 76), (104, 76)];
            DrawBeveledHullPoly(buf, topBlade, secondaryRamp);
            DrawBeveledHullPoly(buf, botBlade, secondaryRamp, -0.2f);

            ReadOnlySpan<(int X, int Y)> strikeHull =
            [
                (62, 60), (92, 41), (196, 41), (212, 51), (202, 60), (212, 69), (196, 79), (92, 79)
            ];
            DrawBeveledHullPoly(buf, strikeHull, primaryRamp);

            // Serrated dorsal battle spikes
            for (int sx = 114; sx <= 166; sx += 14)
            {
                ReadOnlySpan<(int X, int Y)> spike = [(sx, 41), (sx + 6, 30), (sx + 11, 41)];
                DrawBeveledHullPoly(buf, spike, secondaryRamp, 0.08f);
            }

            // Angular armor chevrons & bridge slit
            buf.DrawLine(74, 60, 106, 44, secondaryRamp[2]);
            buf.DrawLine(74, 60, 106, 76, secondaryRamp[1]);
            DrawBridgeViewportSlit(buf, 102, 45, 16, 3, engineGlowHex, "#fef08a", secondaryRamp);

            bool[] bodyMask = buf.CaptureNonNullMask(excludeOutline: false);
            DrawRecessedPanelSeams(buf, bodyMask, 96, 196, 44, 76, 18, primaryRamp);

            DrawBeveledArmorPlate(buf, 170, 34, 38, 8, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 207, 35, 5, 6, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 209, 49, 5, 6, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 209, 65, 5, 6, secondaryRamp);
            DrawBeveledArmorPlate(buf, 170, 78, 38, 8, secondaryRamp, -0.18f);
            DrawEngineBellAndCowling(buf, ref hp, 207, 79, 5, 6, secondaryRamp);

            hp.ReactorCore = (140, 60);
            hp.WeaponsBay = new HardpointRect(64, 53, 18, 14);
            hp.BridgeComms = new HardpointRect(102, 42, 18, 12);
            hp.CargoBay = new HardpointRect(142, 66, 22, 12);
            hp.UpperWarpPylon = new HardpointRect(170, 33, 38, 12);
            hp.LowerWarpPylon = new HardpointRect(170, 77, 38, 11);
            hp.WeaponMuzzlePixels.Add((63, 60));
            hp.WeaponMuzzlePixels.Add((73, 24));
            hp.WeaponMuzzlePixels.Add((73, 96));
            hp.NavStrobes.Add((120, 30));
            hp.NavStrobes.Add((204, 34));
            hp.NavStrobes.Add((204, 85));
            foreClampX = 104;
            aftClampX = 182;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Kordani, shipClass, variant, primaryRamp, secondaryRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // 3. BRENGOP (BMC-)
    // =========================================================================
    private static void BuildBrengopShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] primaryRamp = Palette.BrengopPrimaryGrossYellowRamp;
        string[] secondaryRamp = Palette.BrengopSecondaryGrimyBrassRamp;
        engineGlowHex = Palette.HexBrengopEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // Chunkier blunted Kordani-style military frame painted gross yellow
                ReadOnlySpan<(int X, int Y)> topWing = [(80, 26), (126, 30), (160, 44), (104, 44)];
                ReadOnlySpan<(int X, int Y)> botWing = [(80, 94), (126, 90), (160, 76), (104, 76)];
                DrawBeveledHullPoly(buf, topWing, secondaryRamp);
                DrawBeveledHullPoly(buf, botWing, secondaryRamp, -0.2f);

                ReadOnlySpan<(int X, int Y)> chunkyHull =
                [
                    (64, 52), (96, 37), (198, 37), (212, 48), (206, 60), (212, 72), (198, 83), (96, 83), (64, 68)
                ];
                DrawBeveledHullPoly(buf, chunkyHull, primaryRamp);

                for (int rx = 86; rx <= 182; rx += 18)
                {
                    buf.DrawLine(rx, 39, rx, 81, secondaryRamp[1]);
                    buf.Set(rx + 2, 44, secondaryRamp[3]);
                    buf.Set(rx + 2, 75, secondaryRamp[3]);
                }
                DrawBridgeViewportSlit(buf, 98, 43, 14, 3, Palette.HexCrtAmberPrimary, "#fef08a", secondaryRamp);

                DrawBeveledArmorPlate(buf, 172, 31, 38, 10, secondaryRamp, 0.05f, withRivets: true);
                DrawEngineBellAndCowling(buf, ref hp, 209, 32, 6, 8, secondaryRamp);
                DrawBeveledArmorPlate(buf, 172, 79, 38, 10, secondaryRamp, -0.18f, withRivets: true);
                DrawEngineBellAndCowling(buf, ref hp, 209, 80, 6, 8, secondaryRamp);

                hp.ReactorCore = (140, 60);
                hp.WeaponsBay = new HardpointRect(66, 53, 18, 14);
                hp.BridgeComms = new HardpointRect(96, 40, 18, 12);
                hp.CargoBay = new HardpointRect(142, 66, 22, 12);
                hp.UpperWarpPylon = new HardpointRect(172, 32, 38, 12);
                hp.LowerWarpPylon = new HardpointRect(172, 78, 38, 11);
                hp.WeaponMuzzlePixels.Add((64, 55));
                hp.WeaponMuzzlePixels.Add((64, 65));
                hp.NavStrobes.Add((102, 37));
                hp.NavStrobes.Add((206, 31));
                hp.NavStrobes.Add((206, 88));
                foreClampX = 104;
                aftClampX = 180;
                break;
            }

            case ShipClass.Civilian:
            {
                // Short ~112px asymmetric industrial utility shuttle with tall rear-top cab
                DrawBeveledArmorPlate(buf, 82, 48, 8, 24, secondaryRamp, withRivets: true);
                ReadOnlySpan<(int X, int Y)> stubbyBody =
                [
                    (88, 46), (184, 46), (190, 54), (190, 72), (88, 74)
                ];
                DrawBeveledHullPoly(buf, stubbyBody, primaryRamp);

                DrawBeveledArmorPlate(buf, 134, 32, 40, 15, primaryRamp, 0.06f, withRivets: true);
                DrawBridgeViewportSlit(buf, 138, 36, 14, 4, Palette.HexCrtAmberPrimary, "#fef08a", secondaryRamp);

                DrawBeveledArmorPlate(buf, 144, 74, 46, 12, secondaryRamp, -0.15f, withRivets: true);
                DrawEngineBellAndCowling(buf, ref hp, 189, 55, 5, 9, secondaryRamp);
                DrawEngineBellAndCowling(buf, ref hp, 189, 76, 5, 8, secondaryRamp);

                hp.ReactorCore = (138, 60);
                hp.WeaponsBay = new HardpointRect(84, 54, 14, 12);
                hp.BridgeComms = new HardpointRect(138, 35, 22, 12);
                hp.CargoBay = new HardpointRect(112, 58, 24, 14);
                hp.UpperWarpPylon = new HardpointRect(146, 72, 42, 12);
                hp.LowerWarpPylon = new HardpointRect(146, 76, 42, 10);
                hp.WeaponMuzzlePixels.Add((82, 60));
                hp.NavStrobes.Add((152, 32));
                hp.NavStrobes.Add((188, 82));
                foreClampX = 106;
                aftClampX = 166;
                break;
            }

            case ShipClass.Cargo:
            {
                // Heavy slab ore barge (x = 50..230) with low bow cab & 4 deep ventral hoppers
                DrawBeveledArmorPlate(buf, 50, 54, 28, 24, secondaryRamp, withRivets: true);
                DrawBridgeViewportSlit(buf, 54, 58, 12, 4, Palette.HexCrtAmberPrimary, "#fef08a", secondaryRamp);

                DrawBeveledArmorPlate(buf, 74, 40, 144, 28, primaryRamp, 0.02f, withRivets: true);
                bool[] deckMask = buf.CaptureNonNullMask(excludeOutline: false);
                DrawRecessedPanelSeams(buf, deckMask, 82, 212, 42, 66, 22, primaryRamp);

                for (int hx = 80; hx <= 170; hx += 30)
                {
                    ReadOnlySpan<(int X, int Y)> hopper =
                    [
                        (hx, 68), (hx + 26, 68), (hx + 21, 92), (hx + 5, 92)
                    ];
                    DrawBeveledHullPoly(buf, hopper, secondaryRamp, -0.14f);
                }

                DrawBeveledArmorPlate(buf, 180, 28, 42, 13, secondaryRamp, 0.05f, withRivets: true);
                DrawEngineBellAndCowling(buf, ref hp, 221, 30, 6, 9, secondaryRamp);
                DrawEngineBellAndCowling(buf, ref hp, 217, 48, 6, 12, secondaryRamp);
                DrawBeveledArmorPlate(buf, 180, 72, 42, 13, secondaryRamp, -0.16f, withRivets: true);
                DrawEngineBellAndCowling(buf, ref hp, 221, 74, 6, 9, secondaryRamp);

                hp.ReactorCore = (146, 54);
                hp.WeaponsBay = new HardpointRect(54, 62, 16, 14);
                hp.BridgeComms = new HardpointRect(54, 54, 18, 14);
                hp.CargoBay = new HardpointRect(124, 72, 32, 16);
                hp.UpperWarpPylon = new HardpointRect(182, 29, 40, 14);
                hp.LowerWarpPylon = new HardpointRect(182, 72, 40, 12);
                hp.WeaponMuzzlePixels.Add((50, 66));
                hp.NavStrobes.Add((62, 54));
                hp.NavStrobes.Add((218, 28));
                hp.NavStrobes.Add((218, 84));
                foreClampX = 96;
                aftClampX = 186;
                break;
            }

            default: // Diplomatic (Stepped 3-Tier Guild Factor Ziggurat Barge)
            {
                ReadOnlySpan<(int X, int Y)> baseTier =
                [
                    (54, 64), (72, 50), (212, 50), (222, 64), (212, 78), (68, 78)
                ];
                DrawBeveledHullPoly(buf, baseTier, primaryRamp);
                DrawBeveledArmorPlate(buf, 80, 36, 114, 15, secondaryRamp, 0.04f, withRivets: true);
                DrawBeveledArmorPlate(buf, 102, 24, 66, 13, primaryRamp, 0.08f, withRivets: true);
                DrawBridgeViewportSlit(buf, 108, 28, 48, 4, Palette.HexCrtAmberPrimary, "#fef08a", secondaryRamp);

                DrawBeveledArmorPlate(buf, 168, 28, 52, 8, primaryRamp, 0.04f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 29, 6, 6, secondaryRamp);
                DrawEngineBellAndCowling(buf, ref hp, 221, 59, 6, 8, secondaryRamp);
                DrawBeveledArmorPlate(buf, 168, 82, 52, 8, primaryRamp, -0.15f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 83, 6, 6, secondaryRamp);

                hp.ReactorCore = (140, 62);
                hp.WeaponsBay = new HardpointRect(58, 56, 16, 14);
                hp.BridgeComms = new HardpointRect(122, 28, 26, 14);
                hp.CargoBay = new HardpointRect(142, 66, 26, 12);
                hp.UpperWarpPylon = new HardpointRect(172, 29, 44, 12);
                hp.LowerWarpPylon = new HardpointRect(172, 81, 44, 10);
                hp.WeaponMuzzlePixels.Add((54, 64));
                hp.NavStrobes.Add((134, 24));
                hp.NavStrobes.Add((216, 28));
                hp.NavStrobes.Add((216, 89));
                foreClampX = 96;
                aftClampX = 186;
                break;
            }
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Brengop, shipClass, variant, primaryRamp, secondaryRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // 4. KRUSHVAK (KRW-) — Military & Cargo ONLY
    // =========================================================================
    private static void BuildKrushvakShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] primaryRamp = Palette.KrushvakPrimaryOliveGreenRamp;
        string[] secondaryRamp = Palette.KrushvakSecondaryEarthBrownRamp;
        engineGlowHex = Palette.HexKrushvakEngineGlow;

        if (shipClass == ShipClass.Cargo)
        {
            // Bloated ribbed caterpillar hauler (x = 50..228)
            for (int seg = 0; seg < 5; seg++)
            {
                int sx = 80 + seg * 23;
                DrawContourEllipse(buf, sx + 12, 54, 13, 21, primaryRamp);
                DrawBeveledArmorPlate(buf, sx, 34, 5, 40, secondaryRamp, -0.05f, withRivets: true);
            }

            DrawBeveledArmorPlate(buf, 50, 38, 30, 38, secondaryRamp, 0.04f, withRivets: true);
            DrawBridgeViewportSlit(buf, 56, 44, 10, 4, Palette.HexCrtAmberPrimary, "#fef08a", secondaryRamp);

            DrawBeveledArmorPlate(buf, 84, 72, 46, 20, secondaryRamp, -0.14f, withRivets: true);
            DrawBeveledArmorPlate(buf, 136, 72, 46, 20, secondaryRamp, -0.18f, withRivets: true);

            DrawBeveledArmorPlate(buf, 188, 28, 32, 16, secondaryRamp, 0.05f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 31, 6, 10, secondaryRamp);
            DrawBeveledArmorPlate(buf, 188, 48, 30, 18, primaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 217, 52, 6, 10, secondaryRamp);
            DrawBeveledArmorPlate(buf, 188, 70, 32, 16, secondaryRamp, -0.16f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 73, 6, 10, secondaryRamp);

            hp.ReactorCore = (142, 54);
            hp.WeaponsBay = new HardpointRect(54, 52, 16, 14);
            hp.BridgeComms = new HardpointRect(54, 40, 18, 14);
            hp.CargoBay = new HardpointRect(126, 74, 30, 16);
            hp.UpperWarpPylon = new HardpointRect(186, 30, 32, 14);
            hp.LowerWarpPylon = new HardpointRect(186, 70, 32, 14);
            hp.WeaponMuzzlePixels.Add((50, 58));
            hp.NavStrobes.Add((66, 37));
            hp.NavStrobes.Add((216, 28));
            hp.NavStrobes.Add((216, 85));
            foreClampX = 96;
            aftClampX = 186;
        }
        else
        {
            // MIL: Lean, low predatory raider with sharp horned pincer prow & angled rear pylons
            ReadOnlySpan<(int X, int Y)> topPylon = [(152, 48), (176, 27), (212, 27), (194, 48)];
            ReadOnlySpan<(int X, int Y)> botPylon = [(152, 72), (176, 93), (212, 93), (194, 72)];
            DrawBeveledHullPoly(buf, topPylon, secondaryRamp);
            DrawBeveledHullPoly(buf, botPylon, secondaryRamp, -0.2f);

            ReadOnlySpan<(int X, int Y)> leanTorso = [(88, 46), (194, 46), (210, 60), (194, 74), (88, 74)];
            DrawBeveledHullPoly(buf, leanTorso, primaryRamp);

            ReadOnlySpan<(int X, int Y)> upperHorn = [(58, 41), (82, 34), (106, 46), (84, 54)];
            ReadOnlySpan<(int X, int Y)> lowerHorn = [(58, 79), (82, 86), (106, 74), (84, 66)];
            DrawBeveledHullPoly(buf, upperHorn, primaryRamp, 0.06f);
            DrawBeveledHullPoly(buf, lowerHorn, primaryRamp, -0.14f);

            for (int rx = 104; rx <= 170; rx += 14)
            {
                buf.DrawLine(rx, 46, rx - 5, 73, secondaryRamp[0]);
                buf.DrawLine(rx + 2, 46, rx - 3, 73, primaryRamp[3]);
            }
            DrawBridgeViewportSlit(buf, 96, 50, 14, 3, engineGlowHex, "#fca5a5", secondaryRamp);

            DrawEngineBellAndCowling(buf, ref hp, 211, 29, 6, 7, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 209, 56, 6, 8, secondaryRamp);
            DrawEngineBellAndCowling(buf, ref hp, 211, 84, 6, 7, secondaryRamp);

            hp.ReactorCore = (140, 60);
            hp.WeaponsBay = new HardpointRect(64, 44, 18, 16);
            hp.BridgeComms = new HardpointRect(94, 46, 18, 12);
            hp.CargoBay = new HardpointRect(148, 64, 22, 10);
            hp.UpperWarpPylon = new HardpointRect(172, 29, 36, 14);
            hp.LowerWarpPylon = new HardpointRect(172, 78, 36, 14);
            hp.WeaponMuzzlePixels.Add((59, 41));
            hp.WeaponMuzzlePixels.Add((59, 79));
            hp.WeaponMuzzlePixels.Add((86, 60));
            hp.NavStrobes.Add((84, 34));
            hp.NavStrobes.Add((208, 27));
            hp.NavStrobes.Add((208, 92));
            foreClampX = 110;
            aftClampX = 182;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Krushvak, shipClass, variant, primaryRamp, secondaryRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // 5. ORUN (ORN-) — Strictly symmetrical granite architecture, zero turrets
    // =========================================================================
    private static void BuildOrunShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] graniteRamp = Palette.OrunPrimaryGraniteRamp;
        string[] glassRamp = Palette.OrunGlassContainerRamp;
        engineGlowHex = "#38bdf8";

        switch (shipClass)
        {
            case ShipClass.Civilian:
            {
                // Short boxy symmetrical granite utility tug (x = 84..196)
                ReadOnlySpan<(int X, int Y)> tugHull = [(84, 54), (96, 42), (176, 42), (176, 78), (96, 78), (84, 66)];
                DrawBeveledHullPoly(buf, tugHull, graniteRamp);

                buf.FillRect(86, 59, 88, 2, Palette.HexOrunBrassTrim);
                DrawBridgeViewportSlit(buf, 96, 49, 26, 5, "#38bdf8", "#e0f2fe", graniteRamp);

                DrawBeveledArmorPlate(buf, 162, 33, 8, 54, graniteRamp);
                DrawBeveledArmorPlate(buf, 156, 28, 34, 8, graniteRamp, 0.05f);
                DrawEngineBellAndCowling(buf, ref hp, 189, 29, 5, 6, graniteRamp);
                DrawBeveledArmorPlate(buf, 156, 84, 34, 8, graniteRamp, -0.15f);
                DrawEngineBellAndCowling(buf, ref hp, 189, 85, 5, 6, graniteRamp);

                hp.ReactorCore = (138, 60);
                hp.WeaponsBay = new HardpointRect(88, 54, 14, 12);
                hp.BridgeComms = new HardpointRect(98, 48, 22, 12);
                hp.CargoBay = new HardpointRect(136, 65, 22, 12);
                hp.UpperWarpPylon = new HardpointRect(156, 30, 32, 10);
                hp.LowerWarpPylon = new HardpointRect(156, 82, 32, 10);
                hp.NavStrobes.Add((98, 42));
                hp.NavStrobes.Add((188, 28));
                hp.NavStrobes.Add((188, 91));
                foreClampX = 112;
                aftClampX = 166;
                break;
            }

            case ShipClass.Diplomatic:
            {
                // Symmetrical double-deck archive barge with tall faceted glass dome
                ReadOnlySpan<(int X, int Y)> archiveBarge =
                [
                    (54, 55), (68, 41), (196, 41), (208, 52), (208, 68), (196, 79), (68, 79), (54, 65)
                ];
                DrawBeveledHullPoly(buf, archiveBarge, graniteRamp);

                buf.FillRect(58, 59, 146, 2, Palette.HexOrunBrassTrim);
                for (int px = 80; px <= 164; px += 24)
                {
                    DrawBeveledArmorPlate(buf, px, 45, 18, 12, graniteRamp, 0.04f);
                    DrawBeveledArmorPlate(buf, px, 63, 18, 12, graniteRamp, -0.10f);
                }

                // Tall raised faceted glass observation dome
                ReadOnlySpan<(int X, int Y)> glassDome = [(100, 41), (112, 23), (148, 23), (160, 41)];
                buf.FillPoly(glassDome, (px, py) => Dither.BayerDither(px, py, "#1e3a5f", "#38bdf8", 0.56f));
                buf.DrawLine(110, 38, 122, 25, glassRamp[3]);
                buf.DrawLine(132, 38, 144, 25, glassRamp[3]);
                buf.FillRect(100, 40, 60, 2, Palette.HexOrunBrassTrim);

                DrawBeveledArmorPlate(buf, 178, 30, 8, 60, graniteRamp);
                DrawBeveledArmorPlate(buf, 166, 25, 54, 8, graniteRamp, 0.06f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 26, 6, 6, graniteRamp);
                DrawBeveledArmorPlate(buf, 166, 87, 54, 8, graniteRamp, -0.14f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 88, 6, 6, graniteRamp);

                hp.ReactorCore = (138, 60);
                hp.WeaponsBay = new HardpointRect(58, 54, 14, 12);
                hp.BridgeComms = new HardpointRect(118, 30, 24, 12);
                hp.CargoBay = new HardpointRect(138, 64, 24, 12);
                hp.UpperWarpPylon = new HardpointRect(170, 28, 44, 12);
                hp.LowerWarpPylon = new HardpointRect(170, 84, 44, 10);
                hp.NavStrobes.Add((130, 23));
                hp.NavStrobes.Add((218, 25));
                hp.NavStrobes.Add((218, 94));
                foreClampX = 96;
                aftClampX = 182;
                break;
            }

            default: // Cargo (Symmetrical spine + 3 pairs of clear glass cargo bays with interior crates)
            {
                DrawBeveledArmorPlate(buf, 78, 52, 134, 16, graniteRamp, 0.04f, withRivets: true);
                ReadOnlySpan<(int X, int Y)> facetedBow = [(52, 56), (66, 41), (86, 41), (86, 79), (66, 79), (52, 64)];
                DrawBeveledHullPoly(buf, facetedBow, graniteRamp);
                buf.FillRect(56, 59, 28, 2, Palette.HexOrunBrassTrim);
                DrawBridgeViewportSlit(buf, 64, 46, 14, 4, "#38bdf8", "#e0f2fe", graniteRamp);

                DrawBeveledArmorPlate(buf, 186, 36, 9, 48, graniteRamp);
                DrawBeveledArmorPlate(buf, 180, 27, 40, 11, graniteRamp, 0.05f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 29, 6, 7, graniteRamp);
                DrawBeveledArmorPlate(buf, 180, 82, 40, 11, graniteRamp, -0.14f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 84, 6, 7, graniteRamp);

                string[] crateColors = ["#ffb84d", "#2dd4bf", "#cc6b35", "#a855f7", "#8ab06c", "#38bdf8"];
                for (int b = 0; b < 3; b++)
                {
                    int bx = 92 + b * 30;
                    DrawClearGlassCargoContainerV2(buf, bx, 30, 26, 22, glassRamp, crateColors, rng.Fork($"v2_ubay_{b}"));
                    DrawClearGlassCargoContainerV2(buf, bx, 68, 26, 22, glassRamp, crateColors, rng.Fork($"v2_lbay_{b}"));
                }

                hp.ReactorCore = (140, 60);
                hp.WeaponsBay = new HardpointRect(56, 54, 16, 12);
                hp.BridgeComms = new HardpointRect(62, 44, 18, 12);
                hp.CargoBay = new HardpointRect(124, 68, 28, 20);
                hp.UpperWarpPylon = new HardpointRect(182, 29, 36, 14);
                hp.LowerWarpPylon = new HardpointRect(182, 81, 36, 12);
                hp.NavStrobes.Add((66, 41));
                hp.NavStrobes.Add((218, 27));
                hp.NavStrobes.Add((218, 92));
                foreClampX = 82;
                aftClampX = 190;
                break;
            }
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Orun, shipClass, variant, graniteRamp, glassRamp, Palette.HexOrunBrassTrim, rng);
    }

    private static void DrawClearGlassCargoContainerV2(
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

        int cursorX = x + 3;
        while (cursorX + 5 < x + w - 2)
        {
            int cw = rng.NextInt(5, 8);
            int ch1 = rng.NextInt(5, 8);
            string c1 = rng.Pick(crateColors);
            buf.FillRect(cursorX, y + h - 2 - ch1, cw, ch1, c1);
            buf.DrawLine(cursorX, y + h - 2 - ch1, cursorX + cw - 1, y + h - 2 - ch1, "#f8fafc");

            if (rng.NextBool(0.8f) && ch1 + 6 < h - 3)
            {
                int ch2 = rng.NextInt(4, 7);
                string c2 = rng.Pick(crateColors);
                buf.FillRect(cursorX, y + h - 3 - ch1 - ch2, cw, ch2, c2);
            }
            cursorX += cw + 2;
        }

        buf.DrawLine(x, y, x + w - 1, y, glassRamp[2]);
        buf.DrawLine(x, y + h - 1, x + w - 1, y + h - 1, glassRamp[2]);
        buf.DrawLine(x, y, x, y + h - 1, glassRamp[1]);
        buf.DrawLine(x + w - 1, y, x + w - 1, y + h - 1, glassRamp[1]);
        buf.DrawLine(x + 3, y + h - 5, x + 10, y + 3, glassRamp[3]);
        buf.DrawLine(x + 14, y + h - 5, x + 21, y + 3, glassRamp[3]);
    }

    // =========================================================================
    // 6. TOLVAK (TLV-) — Civilian & Cargo ONLY
    // =========================================================================
    private static void BuildTolvakShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] ramp = Palette.TolvakPrimaryTerracottaRamp;
        string[] trimRamp = Palette.BrengopSecondaryGrimyBrassRamp;
        engineGlowHex = "#f59e0b";

        if (shipClass == ShipClass.Civilian)
        {
            // Compact ~114px single-sail desert-skiff courier
            ReadOnlySpan<(int X, int Y)> tallSail = [(118, 54), (146, 26), (168, 26), (154, 54)];
            DrawBeveledHullPoly(buf, tallSail, ramp, 0.04f);
            for (int ry = 31; ry <= 50; ry += 4)
            {
                buf.DrawLine(134, ry, 156, ry, ramp[0]);
            }

            ReadOnlySpan<(int X, int Y)> compactSkiff = [(82, 66), (104, 51), (186, 51), (192, 61), (186, 73), (88, 73)];
            DrawBeveledHullPoly(buf, compactSkiff, ramp);
            DrawBridgeViewportSlit(buf, 104, 55, 14, 3, Palette.HexCrtAmberPrimary, "#fef08a", ramp);

            DrawBeveledArmorPlate(buf, 158, 60, 32, 8, ramp, 0.02f);
            DrawEngineBellAndCowling(buf, ref hp, 189, 61, 5, 6, trimRamp);

            hp.ReactorCore = (142, 62);
            hp.WeaponsBay = new HardpointRect(86, 60, 14, 12);
            hp.BridgeComms = new HardpointRect(102, 52, 18, 12);
            hp.CargoBay = new HardpointRect(148, 62, 22, 10);
            hp.UpperWarpPylon = new HardpointRect(160, 56, 30, 12);
            hp.LowerWarpPylon = hp.UpperWarpPylon;
            hp.WeaponMuzzlePixels.Add((83, 66));
            hp.NavStrobes.Add((152, 26));
            hp.NavStrobes.Add((188, 61));
            foreClampX = 114;
            aftClampX = 164;
        }
        else
        {
            // Heavy caravan barge with twin radiator fins & 3 belly pods
            ReadOnlySpan<(int X, int Y)> foreFin = [(94, 48), (112, 31), (138, 31), (130, 48)];
            ReadOnlySpan<(int X, int Y)> aftFin = [(144, 48), (164, 29), (192, 29), (182, 48)];
            DrawBeveledHullPoly(buf, foreFin, ramp);
            DrawBeveledHullPoly(buf, aftFin, ramp);
            for (int ry = 35; ry <= 45; ry += 4)
            {
                buf.DrawLine(112, ry, 129, ry, ramp[0]);
                buf.DrawLine(162, ry, 182, ry, ramp[0]);
            }

            ReadOnlySpan<(int X, int Y)> heavyBarge = [(52, 62), (70, 46), (210, 46), (220, 59), (214, 75), (60, 75)];
            DrawBeveledHullPoly(buf, heavyBarge, ramp);
            DrawBridgeViewportSlit(buf, 74, 51, 16, 4, Palette.HexCrtAmberPrimary, "#fef08a", ramp);

            int[] pods = [92, 130, 168];
            foreach (int cx in pods)
            {
                DrawContourEllipse(buf, cx, 83, 15, 9, ramp, -0.06f);
            }

            DrawBeveledArmorPlate(buf, 182, 39, 38, 8, ramp, 0.04f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 40, 6, 6, trimRamp);
            DrawBeveledArmorPlate(buf, 182, 75, 38, 8, ramp, -0.14f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 76, 6, 6, trimRamp);

            hp.ReactorCore = (142, 60);
            hp.WeaponsBay = new HardpointRect(58, 56, 14, 14);
            hp.BridgeComms = new HardpointRect(74, 48, 18, 12);
            hp.CargoBay = new HardpointRect(118, 74, 28, 16);
            hp.UpperWarpPylon = new HardpointRect(182, 38, 36, 12);
            hp.LowerWarpPylon = new HardpointRect(182, 74, 36, 10);
            hp.WeaponMuzzlePixels.Add((53, 62));
            hp.NavStrobes.Add((118, 31));
            hp.NavStrobes.Add((216, 39));
            hp.NavStrobes.Add((216, 82));
            foreClampX = 96;
            aftClampX = 188;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Tolvak, shipClass, variant, ramp, trimRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // 7. PRUNJUN (PRJ-) — Rigid Mismatched Hack-Job + Bubble Dome + Clipped Flames
    // =========================================================================
    private static void BuildPrunjunHackJobShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
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

        // Core hull (x = 94..180, y = 40..80) from Faction A
        ReadOnlySpan<(int X, int Y)> corePoly = [(94, 41), (180, 39), (184, 80), (94, 81)];
        DrawBeveledHullPoly(buf, corePoly, coreRamp);

        // Bow mount (x = 56..98) from Faction B
        ReadOnlySpan<(int X, int Y)> bowPoly = factionB switch
        {
            1 => [(56, 60), (96, 42), (96, 78)],
            3 => [(58, 45), (96, 38), (96, 81), (58, 75), (68, 60)],
            _ => [(60, 49), (78, 42), (98, 42), (98, 78), (78, 78), (60, 70)]
        };
        DrawBeveledHullPoly(buf, bowPoly, bowRamp);

        // Stern warp pylons (x = 166..218) from Faction C
        DrawBeveledArmorPlate(buf, 168, 27, 46, 11, sternRamp, 0.05f, withRivets: true);
        DrawEngineBellAndCowling(buf, ref hp, 213, 28, 6, 9, sternRamp);
        DrawBeveledArmorPlate(buf, 166, 80, 48, 12, sternRamp, -0.15f, withRivets: true);
        DrawEngineBellAndCowling(buf, ref hp, 213, 81, 6, 9, sternRamp);

        // Welded scrap seams & bronze girder patch
        buf.DrawLine(95, 41, 98, 80, "#181a1c");
        buf.DrawLine(97, 41, 100, 80, Palette.HexOxidizedBronzeWorn);
        buf.DrawLine(176, 39, 176, 80, "#181a1c");

        bool[] hullMask = buf.CaptureNonNullMask(excludeOutline: true);
        bool MaskFn(int px, int py) => buf.IsMasked(hullMask, px, py);

        // Mandatory Lock 2: Clipped multi-tongue painted flames
        int[] flameY = [47, 56, 66, 74];
        int[] flameLen = [54, 68, 62, 48];
        for (int i = 0; i < flameY.Length; i++)
        {
            int fy = flameY[i];
            int flen = flameLen[i];
            ReadOnlySpan<(int X, int Y)> flameTongue = [(58, fy - 4), (58 + flen, fy), (58, fy + 5)];
            buf.FillClippedPoly(flameTongue, MaskFn, (px, py) =>
            {
                float t = Math.Clamp(1.0f - (px - 58) / (float)flen, 0.06f, 0.98f);
                return Dither.SampleRamp(px, py, Palette.PrunjunFlameRamp, t);
            });
            buf.FillClippedEllipse(70 + i * 7, fy, 12, 3, MaskFn,
                (px, py, _, _, dist) => Dither.SampleRamp(px, py, Palette.PrunjunFlameRamp, 0.95f - dist * 0.6f));
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.Prunjun, ShipClass.HackJob, variant, coreRamp, bowRamp, engineGlowHex, rng);

        // Mandatory Lock 1: Prominent circular bubble cockpit dome protruding from upper-forward hull
        const int domeCx = 98;
        const int domeCy = 37;
        buf.FillEllipse(domeCx, domeCy, 11, 11, (px, py, dx, dy, dist) =>
        {
            float t = Math.Clamp(0.92f - dist * 0.62f - dx * 0.2f - dy * 0.2f, 0.12f, 0.98f);
            return Dither.SampleRamp(px, py, Palette.PrunjunCockpitGlassRamp, t);
        });
        buf.DrawLine(domeCx - 6, domeCy - 5, domeCx - 2, domeCy - 8, "#e0f2fe");
        buf.DrawLine(domeCx - 6, domeCy - 4, domeCx - 3, domeCy - 7, "#e0f2fe");
        DrawBeveledArmorPlate(buf, domeCx - 11, domeCy + 8, 23, 3, Palette.BrengopSecondaryGrimyBrassRamp);

        hp.ReactorCore = (138, 60);
        hp.WeaponsBay = new HardpointRect(62, 54, 18, 14);
        hp.BridgeComms = new HardpointRect(domeCx - 9, domeCy - 8, 18, 16);
        hp.CargoBay = new HardpointRect(136, 66, 24, 12);
        hp.UpperWarpPylon = new HardpointRect(172, 29, 38, 12);
        hp.LowerWarpPylon = new HardpointRect(172, 80, 38, 12);
        hp.WeaponMuzzlePixels.Add((59, 52));
        hp.WeaponMuzzlePixels.Add((59, 69));
        hp.NavStrobes.Add((domeCx, domeCy - 11));
        hp.NavStrobes.Add((210, 27));
        hp.NavStrobes.Add((210, 91));
        foreClampX = 114;
        aftClampX = 180;
    }

    // =========================================================================
    // 8. INDEPENDENT FREE TRADERS (FTV-) & HUN-BORIAN (HBR-)
    // =========================================================================
    private static void BuildFreeTraderShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        bool hasHullScorch,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] slateRamp = Palette.FreeTraderPrimarySlateRamp;
        string[] rustRamp = Palette.FreeTraderTrimRustRamp;
        engineGlowHex = Palette.HexFreeTraderEngineGlow;

        switch (shipClass)
        {
            case ShipClass.Military:
            {
                // Compact snub-nosed wedge gunboat (x = 62..212)
                DrawBeveledArmorPlate(buf, 62, 51, 20, 4, slateRamp, 0.08f);
                DrawBeveledArmorPlate(buf, 62, 65, 20, 4, slateRamp);

                ReadOnlySpan<(int X, int Y)> wedge = [(78, 46), (104, 37), (194, 37), (206, 60), (194, 83), (104, 83), (78, 74)];
                DrawBeveledHullPoly(buf, wedge, slateRamp);

                DrawContourEllipse(buf, 108, 37, 11, 5, rustRamp);
                DrawBridgeViewportSlit(buf, 100, 36, 12, 2, Palette.HexCrtAmberPrimary, "#fef08a", slateRamp);
                ReadOnlySpan<(int X, int Y)> stubFin = [(146, 37), (156, 26), (174, 26), (178, 37)];
                DrawBeveledHullPoly(buf, stubFin, rustRamp);

                DrawBeveledArmorPlate(buf, 170, 30, 38, 7, slateRamp, 0.04f);
                DrawEngineBellAndCowling(buf, ref hp, 207, 31, 5, 5, slateRamp);
                DrawBeveledArmorPlate(buf, 170, 83, 38, 7, slateRamp, -0.16f);
                DrawEngineBellAndCowling(buf, ref hp, 207, 84, 5, 5, slateRamp);

                hp.ReactorCore = (142, 60);
                hp.WeaponsBay = new HardpointRect(64, 52, 18, 16);
                hp.BridgeComms = new HardpointRect(100, 34, 18, 12);
                hp.CargoBay = new HardpointRect(142, 66, 24, 12);
                hp.UpperWarpPylon = new HardpointRect(170, 30, 36, 10);
                hp.LowerWarpPylon = new HardpointRect(170, 82, 36, 10);
                hp.WeaponMuzzlePixels.Add((62, 53));
                hp.WeaponMuzzlePixels.Add((62, 67));
                hp.NavStrobes.Add((160, 26));
                hp.NavStrobes.Add((204, 30));
                foreClampX = 112;
                aftClampX = 178;
                break;
            }

            case ShipClass.Civilian:
            {
                // Compact ~114px Tramp Runner with top-left offset bridge pod & single underslung pylon
                buf.DrawLine(78, 59, 92, 59, slateRamp[3]);
                buf.DrawLine(82, 55, 82, 63, rustRamp[2]);

                DrawContourEllipse(buf, 138, 60, 48, 14, slateRamp);
                DrawBeveledArmorPlate(buf, 96, 36, 26, 11, rustRamp, 0.05f, withRivets: true);
                DrawBridgeViewportSlit(buf, 99, 40, 10, 3, Palette.HexCrtAmberPrimary, "#fef08a", slateRamp);

                DrawBeveledArmorPlate(buf, 142, 74, 48, 10, slateRamp, -0.12f);
                buf.DrawLine(148, 79, 184, 79, rustRamp[2]);
                DrawEngineBellAndCowling(buf, ref hp, 189, 75, 5, 7, slateRamp);

                hp.ReactorCore = (138, 60);
                hp.WeaponsBay = new HardpointRect(88, 54, 14, 12);
                hp.BridgeComms = new HardpointRect(98, 36, 22, 12);
                hp.CargoBay = new HardpointRect(146, 62, 22, 10);
                hp.UpperWarpPylon = new HardpointRect(144, 72, 42, 12);
                hp.LowerWarpPylon = hp.UpperWarpPylon;
                hp.WeaponMuzzlePixels.Add((88, 60));
                hp.NavStrobes.Add((104, 36));
                hp.NavStrobes.Add((186, 74));
                foreClampX = 110;
                aftClampX = 166;
                break;
            }

            case ShipClass.Diplomatic:
            {
                // Refitted Private Yacht dart with gull-wings & long sensor nosecone
                buf.DrawLine(50, 60, 74, 60, rustRamp[3]);
                ReadOnlySpan<(int X, int Y)> dartHull = [(64, 60), (96, 46), (206, 46), (222, 60), (206, 74), (96, 74)];
                DrawBeveledHullPoly(buf, dartHull, slateRamp);

                ReadOnlySpan<(int X, int Y)> topWing = [(114, 46), (160, 22), (182, 22), (154, 46)];
                ReadOnlySpan<(int X, int Y)> botWing = [(114, 74), (160, 98), (182, 98), (154, 74)];
                DrawBeveledHullPoly(buf, topWing, rustRamp);
                DrawBeveledHullPoly(buf, botWing, rustRamp, -0.2f);
                DrawBridgeViewportSlit(buf, 96, 51, 18, 3, "#38bdf8", "#e0f2fe", slateRamp);
                DrawEngineBellAndCowling(buf, ref hp, 221, 55, 6, 10, slateRamp);

                hp.ReactorCore = (148, 60);
                hp.WeaponsBay = new HardpointRect(64, 54, 16, 12);
                hp.BridgeComms = new HardpointRect(96, 48, 20, 12);
                hp.CargoBay = new HardpointRect(162, 62, 24, 10);
                hp.UpperWarpPylon = new HardpointRect(184, 54, 34, 12);
                hp.LowerWarpPylon = hp.UpperWarpPylon;
                hp.WeaponMuzzlePixels.Add((64, 60));
                hp.NavStrobes.Add((166, 22));
                hp.NavStrobes.Add((218, 60));
                foreClampX = 104;
                aftClampX = 186;
                break;
            }

            default: // Cargo (Modular Tramp Hauler with 3 mismatched cargo crates)
            {
                DrawBeveledArmorPlate(buf, 74, 42, 144, 15, slateRamp, 0.04f, withRivets: true);
                ReadOnlySpan<(int X, int Y)> cab = [(50, 50), (64, 40), (86, 40), (86, 70), (60, 70), (50, 60)];
                DrawBeveledHullPoly(buf, cab, slateRamp);
                DrawBridgeViewportSlit(buf, 56, 49, 12, 4, Palette.HexCrtAmberPrimary, "#fef08a", slateRamp);

                DrawBeveledArmorPlate(buf, 88, 57, 28, 26, rustRamp, withRivets: true);
                DrawBeveledArmorPlate(buf, 120, 57, 30, 30, Palette.BrengopSecondaryGrimyBrassRamp, -0.05f, withRivets: true);
                DrawBeveledArmorPlate(buf, 154, 57, 28, 24, slateRamp, -0.10f, withRivets: true);

                DrawBeveledArmorPlate(buf, 184, 32, 36, 11, slateRamp, 0.05f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 34, 6, 7, slateRamp);
                DrawBeveledArmorPlate(buf, 184, 72, 36, 11, slateRamp, -0.15f);
                DrawEngineBellAndCowling(buf, ref hp, 219, 74, 6, 7, slateRamp);

                hp.ReactorCore = (142, 50);
                hp.WeaponsBay = new HardpointRect(56, 52, 16, 14);
                hp.BridgeComms = new HardpointRect(58, 44, 18, 12);
                hp.CargoBay = new HardpointRect(122, 62, 28, 20);
                hp.UpperWarpPylon = new HardpointRect(184, 32, 34, 12);
                hp.LowerWarpPylon = new HardpointRect(184, 72, 34, 11);
                hp.WeaponMuzzlePixels.Add((51, 56));
                hp.NavStrobes.Add((66, 40));
                hp.NavStrobes.Add((216, 32));
                foreClampX = 96;
                aftClampX = 186;
                break;
            }
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.FreeTraders, shipClass, variant, slateRamp, rustRamp, engineGlowHex, rng);

        if (hasHullScorch)
        {
            ApplyDownstreamHullScorchV2(buf, rng);
        }
    }

    private static void ApplyDownstreamHullScorchV2(PixelBuffer buf, DeterministicRng rng)
    {
        bool[] mask = buf.CaptureNonNullMask(excludeOutline: true);
        HardpointRect b = FindMaskBounds(mask);
        if (b.IsEmpty)
            return;

        DeterministicRng scRng = rng.Fork("scorch_v2");
        int sx = scRng.NextInt(b.X + 16, Math.Max(b.X + 18, b.Right - 24));
        int sy = scRng.NextInt(b.Y + 8, Math.Max(b.Y + 10, b.Bottom - 10));
        buf.FillClippedEllipse(sx, sy, 14, 7, (px, py) => buf.IsMasked(mask, px, py),
            (px, py, _, _, dist) => Dither.BayerDither(px, py, "#080b10", "#3d2410", 0.55f - dist * 0.25f));
    }

    // =========================================================================
    // 9. FAR-LINE TRANSIT CORP (FTC-) — Civilian & Cargo ONLY
    // =========================================================================
    private static void BuildFarLineTransitShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] creamRamp = Palette.FarLinePrimaryCreamRamp;
        string[] navyRamp = Palette.FarLineAccentNavyRamp;
        engineGlowHex = Palette.HexFarLineEngineGlow;

        if (shipClass == ShipClass.Civilian)
        {
            // Compact ~116px passenger commuter sleeper tube with continuous lit windows
            buf.DrawLine(78, 60, 86, 60, navyRamp[3]);
            buf.DrawLine(80, 53, 80, 67, creamRamp[3]);

            ReadOnlySpan<(int X, int Y)> sleeperTube = [(83, 52), (96, 43), (182, 43), (190, 60), (182, 77), (96, 77), (83, 68)];
            DrawBeveledHullPoly(buf, sleeperTube, creamRamp);

            buf.FillRect(92, 54, 86, 12, navyRamp[2]);
            for (int wx = 96; wx <= 172; wx += 6)
            {
                buf.FillRect(wx, 56, 3, 2, Palette.HexCrtAmberPrimary);
                buf.FillRect(wx, 62, 3, 2, Palette.HexCrtAmberPrimary);
            }

            buf.DrawLine(146, 43, 158, 35, navyRamp[2]);
            buf.DrawLine(146, 77, 158, 85, navyRamp[2]);
            DrawBeveledArmorPlate(buf, 152, 31, 38, 6, creamRamp, 0.04f);
            DrawEngineBellAndCowling(buf, ref hp, 189, 31, 5, 5, navyRamp);
            DrawBeveledArmorPlate(buf, 152, 83, 38, 6, creamRamp, -0.14f);
            DrawEngineBellAndCowling(buf, ref hp, 189, 84, 5, 5, navyRamp);

            hp.ReactorCore = (138, 60);
            hp.WeaponsBay = new HardpointRect(84, 54, 14, 12);
            hp.BridgeComms = new HardpointRect(96, 44, 18, 12);
            hp.CargoBay = new HardpointRect(138, 64, 22, 12);
            hp.UpperWarpPylon = new HardpointRect(154, 31, 34, 10);
            hp.LowerWarpPylon = new HardpointRect(154, 81, 34, 10);
            hp.WeaponMuzzlePixels.Add((83, 60));
            hp.NavStrobes.Add((80, 53));
            hp.NavStrobes.Add((188, 31));
            hp.NavStrobes.Add((188, 88));
            foreClampX = 108;
            aftClampX = 166;
        }
        else
        {
            // Deep-Space Colony Ark (x = 50..230) with 4 pairs of spherical fuel/cryo pods
            DrawContourEllipse(buf, 138, 60, 80, 17, creamRamp);
            ReadOnlySpan<(int X, int Y)> noseShield = [(50, 47), (74, 39), (78, 81), (50, 73)];
            DrawBeveledHullPoly(buf, noseShield, navyRamp);
            DrawBridgeViewportSlit(buf, 58, 52, 12, 4, "#38bdf8", "#e0f2fe", navyRamp);

            int[] podX = [94, 120, 146, 172];
            foreach (int pxCenter in podX)
            {
                DrawContourEllipse(buf, pxCenter, 37, 11, 9, creamRamp, 0.04f);
                buf.FillRect(pxCenter - 1, 28, 3, 18, navyRamp[2]);
                DrawContourEllipse(buf, pxCenter, 83, 11, 9, creamRamp, -0.10f);
                buf.FillRect(pxCenter - 1, 74, 3, 18, navyRamp[1]);
            }

            DrawBeveledArmorPlate(buf, 186, 25, 34, 7, navyRamp);
            DrawEngineBellAndCowling(buf, ref hp, 219, 26, 6, 5, navyRamp);
            DrawBeveledArmorPlate(buf, 190, 36, 30, 7, creamRamp);
            DrawEngineBellAndCowling(buf, ref hp, 219, 37, 6, 5, navyRamp);
            DrawBeveledArmorPlate(buf, 190, 77, 30, 7, creamRamp, -0.10f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 78, 6, 5, navyRamp);
            DrawBeveledArmorPlate(buf, 186, 88, 34, 7, navyRamp, -0.16f);
            DrawEngineBellAndCowling(buf, ref hp, 219, 89, 6, 5, navyRamp);

            hp.ReactorCore = (142, 60);
            hp.WeaponsBay = new HardpointRect(56, 53, 16, 14);
            hp.BridgeComms = new HardpointRect(60, 46, 18, 14);
            hp.CargoBay = new HardpointRect(134, 72, 28, 16);
            hp.UpperWarpPylon = new HardpointRect(184, 28, 36, 14);
            hp.LowerWarpPylon = new HardpointRect(184, 78, 36, 14);
            hp.WeaponMuzzlePixels.Add((51, 52));
            hp.WeaponMuzzlePixels.Add((51, 68));
            hp.NavStrobes.Add((66, 39));
            hp.NavStrobes.Add((218, 25));
            hp.NavStrobes.Add((218, 94));
            foreClampX = 94;
            aftClampX = 186;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.FarLineTransit, shipClass, variant, creamRamp, navyRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // 10. MINING GUILD (MNG-) — Military & Cargo ONLY
    // =========================================================================
    private static void BuildMiningGuildShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] ironRamp = Palette.MiningGuildPrimaryCharcoalRamp;
        string[] hazardRamp = Palette.MiningGuildTrimHazardYellowRamp;
        engineGlowHex = Palette.HexMiningGuildEngineGlow;

        if (shipClass == ShipClass.Military)
        {
            // Slab-sided Armored Claim-Guard Frigate brick with twin turrets & hazard prow
            ReadOnlySpan<(int X, int Y)> brick = [(64, 41), (76, 34), (198, 34), (206, 44), (206, 76), (198, 86), (76, 86), (64, 79)];
            DrawBeveledHullPoly(buf, brick, ironRamp);

            for (int y = 41; y <= 79; y++)
            {
                for (int x = 65; x <= 84; x++)
                {
                    bool yellow = (((x + y) / 4) % 2) == 0;
                    buf.Set(x, y, yellow ? hazardRamp[3] : ironRamp[0]);
                }
            }

            DrawBridgeViewportSlit(buf, 88, 43, 14, 4, Palette.HexCrtAmberPrimary, "#fef08a", ironRamp);
            bool[] mask = buf.CaptureNonNullMask(excludeOutline: false);
            DrawRecessedPanelSeams(buf, mask, 90, 198, 38, 82, 18, ironRamp);

            DrawBeveledArmorPlate(buf, 98, 25, 24, 9, ironRamp, 0.06f, withRivets: true);
            DrawBeveledArmorPlate(buf, 84, 27, 14, 4, hazardRamp);
            DrawBeveledArmorPlate(buf, 98, 86, 24, 9, ironRamp, -0.14f, withRivets: true);
            DrawBeveledArmorPlate(buf, 84, 89, 14, 4, hazardRamp);

            DrawBeveledArmorPlate(buf, 164, 28, 42, 8, ironRamp, 0.04f);
            buf.DrawLine(168, 32, 202, 32, hazardRamp[2]);
            DrawEngineBellAndCowling(buf, ref hp, 205, 29, 6, 6, ironRamp);
            DrawEngineBellAndCowling(buf, ref hp, 206, 56, 6, 8, ironRamp);
            DrawBeveledArmorPlate(buf, 164, 84, 42, 8, ironRamp, -0.16f);
            buf.DrawLine(168, 88, 202, 88, hazardRamp[2]);
            DrawEngineBellAndCowling(buf, ref hp, 205, 85, 6, 6, ironRamp);

            hp.ReactorCore = (140, 60);
            hp.WeaponsBay = new HardpointRect(84, 29, 20, 14);
            hp.SecondaryWeaponsBay = new HardpointRect(84, 81, 20, 12);
            hp.BridgeComms = new HardpointRect(88, 40, 18, 12);
            hp.CargoBay = new HardpointRect(142, 66, 24, 14);
            hp.UpperWarpPylon = new HardpointRect(166, 29, 38, 12);
            hp.LowerWarpPylon = new HardpointRect(166, 82, 38, 10);
            hp.WeaponMuzzlePixels.Add((84, 29));
            hp.WeaponMuzzlePixels.Add((84, 91));
            hp.WeaponMuzzlePixels.Add((64, 60));
            hp.NavStrobes.Add((110, 25));
            hp.NavStrobes.Add((204, 28));
            foreClampX = 132;
            aftClampX = 180;
        }
        else
        {
            // Open-truss Industrial Ore Excavator Rig (x = 50..228) with exposed rotary drills & tower
            DrawBeveledArmorPlate(buf, 74, 30, 142, 12, ironRamp, 0.04f, withRivets: true);
            DrawBeveledArmorPlate(buf, 90, 72, 126, 17, ironRamp, -0.12f, withRivets: true);
            DrawBeveledArmorPlate(buf, 180, 24, 36, 52, ironRamp, 0.02f, withRivets: true);
            DrawBridgeViewportSlit(buf, 184, 29, 14, 5, Palette.HexCrtAmberPrimary, "#fef08a", ironRamp);

            // Open girder cross-braces
            for (int tx = 96; tx < 176; tx += 18)
            {
                buf.DrawLine(tx, 42, tx + 18, 72, ironRamp[3]);
                buf.DrawLine(tx + 1, 42, tx + 19, 72, ironRamp[1]);
                buf.DrawLine(tx + 18, 42, tx, 72, ironRamp[2]);
                buf.FillRect(tx, 42, 3, 30, ironRamp[1]);
            }

            // Massive dual rotary ore-drill jaws on lower bow
            ReadOnlySpan<(int X, int Y)> topDrill = [(50, 58), (70, 49), (90, 49), (90, 66), (70, 66)];
            ReadOnlySpan<(int X, int Y)> botDrill = [(50, 79), (70, 70), (90, 70), (90, 89), (70, 89)];
            DrawBeveledHullPoly(buf, topDrill, hazardRamp);
            DrawBeveledHullPoly(buf, botDrill, hazardRamp, -0.12f);

            for (int y = 30; y <= 48; y++)
            {
                for (int x = 60; x <= 74; x++)
                {
                    buf.Set(x, y, (((x + y) / 3) % 2 == 0) ? hazardRamp[3] : ironRamp[0]);
                }
            }

            DrawEngineBellAndCowling(buf, ref hp, 215, 32, 6, 10, ironRamp);
            DrawEngineBellAndCowling(buf, ref hp, 215, 74, 6, 12, ironRamp);

            hp.ReactorCore = (194, 56);
            hp.WeaponsBay = new HardpointRect(56, 54, 18, 16);
            hp.BridgeComms = new HardpointRect(184, 28, 20, 14);
            hp.CargoBay = new HardpointRect(126, 72, 32, 16);
            hp.UpperWarpPylon = new HardpointRect(184, 30, 32, 16);
            hp.LowerWarpPylon = new HardpointRect(184, 72, 32, 14);
            hp.WeaponMuzzlePixels.Add((51, 58));
            hp.WeaponMuzzlePixels.Add((51, 79));
            hp.NavStrobes.Add((196, 24));
            hp.NavStrobes.Add((214, 31));
            foreClampX = 94;
            aftClampX = 190;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.MiningGuild, shipClass, variant, ironRamp, hazardRamp, hazardRamp[3], rng);
    }

    // =========================================================================
    // 11. ZENITH INTERSTELLAR (ZNI-) — Diplomatic & Cargo ONLY
    // =========================================================================
    private static void BuildZenithInterstellarShipV2(
        PixelBuffer buf,
        ref ShipHardpointLayoutV2 hp,
        ShipClass shipClass,
        ShipVariantProfile variant,
        DeterministicRng rng,
        out string engineGlowHex,
        out int foreClampX,
        out int aftClampX)
    {
        string[] burgundyRamp = Palette.ZenithPrimaryBurgundyRamp;
        string[] pearlRamp = Palette.ZenithPearlWhiteRamp;
        string[] goldRamp = Palette.ZenithTrimPolishedGoldRamp;
        engineGlowHex = Palette.HexZenithEngineGlowLavender;

        if (shipClass == ShipClass.Diplomatic)
        {
            // Flagship Starliner yacht with swept pearl dorsal tailfin & panoramic glass promenade
            ReadOnlySpan<(int X, int Y)> tailFin = [(158, 40), (188, 24), (212, 24), (200, 40)];
            DrawBeveledHullPoly(buf, tailFin, pearlRamp, 0.05f);

            ReadOnlySpan<(int X, int Y)> starlinerHull =
            [
                (52, 61), (70, 41), (112, 35), (198, 39), (222, 54), (210, 78), (74, 78), (56, 70)
            ];
            PixelBuffer hullLocal = new(ArtWidth, ArtHeight);
            hullLocal.FillPoly(starlinerHull, (px, py) =>
            {
                if (py < 61)
                {
                    float t = Math.Clamp(0.96f - (py - 35) / 28.0f, 0.24f, 0.98f);
                    return Dither.SampleRamp(px, py, pearlRamp, t);
                }
                float tb = Math.Clamp(0.86f - (py - 61) / 18.0f, 0.14f, 0.92f);
                return Dither.SampleRamp(px, py, burgundyRamp, tb);
            });
            buf.BlitFrom(hullLocal);

            ReadOnlySpan<(int X, int Y)> promenade = [(68, 50), (80, 39), (132, 39), (128, 50)];
            buf.FillPoly(promenade, (px, py) => Dither.BayerDither(px, py, "#38bdf8", "#93c5fd", 0.62f));
            buf.DrawLine(78, 42, 122, 42, "#e0f2fe");

            buf.DrawLine(56, 60, 214, 60, goldRamp[3]);
            buf.DrawLine(60, 62, 212, 62, goldRamp[1]);

            DrawBeveledArmorPlate(buf, 168, 32, 50, 7, burgundyRamp);
            buf.DrawLine(172, 35, 214, 35, goldRamp[2]);
            DrawEngineBellAndCowling(buf, ref hp, 217, 33, 6, 5, burgundyRamp);

            DrawBeveledArmorPlate(buf, 168, 78, 50, 7, burgundyRamp, -0.14f);
            buf.DrawLine(172, 81, 214, 81, goldRamp[2]);
            DrawEngineBellAndCowling(buf, ref hp, 217, 79, 6, 5, burgundyRamp);

            hp.ReactorCore = (146, 60);
            hp.WeaponsBay = new HardpointRect(58, 56, 16, 12);
            hp.BridgeComms = new HardpointRect(88, 40, 24, 14);
            hp.CargoBay = new HardpointRect(154, 64, 24, 12);
            hp.UpperWarpPylon = new HardpointRect(172, 32, 44, 10);
            hp.LowerWarpPylon = new HardpointRect(172, 76, 44, 10);
            hp.WeaponMuzzlePixels.Add((53, 61));
            hp.NavStrobes.Add((196, 24));
            hp.NavStrobes.Add((216, 32));
            hp.NavStrobes.Add((216, 84));
            foreClampX = 96;
            aftClampX = 186;
        }
        else
        {
            // Hospitality & Provision Tender: bulbous teardrop bow + 3 vertical pearl-and-burgundy supply cylinders
            DrawBeveledArmorPlate(buf, 84, 51, 128, 18, burgundyRamp, 0.02f);
            DrawContourEllipse(buf, 82, 60, 26, 19, pearlRamp, 0.05f);
            DrawBridgeViewportSlit(buf, 62, 52, 16, 4, "#38bdf8", "#e0f2fe", pearlRamp);

            int[] podCenters = [124, 154, 184];
            foreach (int cx in podCenters)
            {
                buf.FillEllipse(cx, 60, 12, 22, (px, py, _, dy, _) =>
                    py < 60
                        ? Dither.SampleRamp(px, py, pearlRamp, 0.94f - MathF.Abs(dy) * 0.35f)
                        : Dither.SampleRamp(px, py, burgundyRamp, 0.84f - MathF.Abs(dy) * 0.35f));
                buf.DrawLine(cx, 39, cx, 81, goldRamp[2]);
            }

            buf.DrawLine(58, 60, 210, 60, goldRamp[3]);
            DrawBeveledArmorPlate(buf, 174, 26, 44, 9, pearlRamp, 0.04f);
            DrawEngineBellAndCowling(buf, ref hp, 217, 27, 6, 7, burgundyRamp);
            DrawBeveledArmorPlate(buf, 174, 85, 44, 9, burgundyRamp, -0.14f);
            DrawEngineBellAndCowling(buf, ref hp, 217, 86, 6, 7, burgundyRamp);

            hp.ReactorCore = (154, 60);
            hp.WeaponsBay = new HardpointRect(58, 54, 14, 14);
            hp.BridgeComms = new HardpointRect(64, 48, 18, 12);
            hp.CargoBay = new HardpointRect(142, 66, 26, 16);
            hp.UpperWarpPylon = new HardpointRect(176, 28, 40, 14);
            hp.LowerWarpPylon = new HardpointRect(176, 82, 40, 12);
            hp.WeaponMuzzlePixels.Add((56, 60));
            hp.NavStrobes.Add((82, 41));
            hp.NavStrobes.Add((214, 26));
            hp.NavStrobes.Add((214, 93));
            foreClampX = 88;
            aftClampX = 186;
        }

        ApplySeededModularGreebles(buf, ref hp, ShipFaction.ZenithInterstellar, shipClass, variant, pearlRamp, goldRamp, engineGlowHex, rng);
    }

    // =========================================================================
    // LIVE EXTERIOR FX, RIM-LIGHT, MANUAL CHART & SPS X-RAY INTERIOR LAYER
    // =========================================================================
    private static void ApplyBrownDwarfRimLightV2(PixelBuffer buf, ShipHardpointLayoutV2 hp)
    {
        bool[] mask = buf.CaptureNonNullMask(excludeOutline: true);
        int midX = hp.HullBounds.CenterX;

        for (int y = 2; y < ArtHeight - 2; y++)
        {
            for (int x = 2; x < ArtWidth - 2; x++)
            {
                if (!buf.IsMasked(mask, x, y))
                    continue;

                bool botExposed = !buf.IsMasked(mask, x, y + 1);
                bool leftExposed = !buf.IsMasked(mask, x - 1, y);

                if (botExposed)
                {
                    bool bowCatchLight = leftExposed || x < midX;
                    buf.Set(x, y, bowCatchLight ? "#9c4827" : Palette.HexBrownDwarfGlowRim);
                    if (buf.IsMasked(mask, x, y - 1))
                    {
                        string? prev = buf.Get(x, y - 1);
                        if (prev != null)
                        {
                            buf.Set(x, y - 1, Dither.BayerDither(x, y - 1, prev, Palette.HexBrownDwarfGlowDeep, 0.35f));
                        }
                    }
                }
            }
        }
    }

    private static void DrawAnimatedEnginePlumesV2(
        PixelBuffer buf,
        ShipHardpointLayoutV2 hp,
        string engineGlowHex,
        bool pulseBright,
        int animBucket)
    {
        foreach (HardpointRect nozzle in hp.EngineNozzles)
        {
            int plumeLen = pulseBright ? nozzle.W : Math.Max(2, nozzle.W - 1);
            int maxW = Math.Min(plumeLen, SafeMaxArtX - nozzle.X);
            if (maxW <= 0)
                continue;

            for (int px = nozzle.X; px < nozzle.X + maxW; px++)
            {
                float frac = (px - nozzle.X) / (float)Math.Max(1, maxW);
                for (int py = nozzle.Y; py < nozzle.Y + nozzle.H; py++)
                {
                    bool isCore = py > nozzle.Y && py < nozzle.Y + nozzle.H - 1 && frac < 0.55f;
                    if (isCore)
                    {
                        buf.Set(px, py, pulseBright ? "#f8fafc" : engineGlowHex);
                    }
                    else
                    {
                        string col = frac < 0.65f
                            ? engineGlowHex
                            : Dither.BayerDither(px + animBucket, py, Palette.HexOutlineVoid, engineGlowHex, 0.45f);
                        if (!col.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                        {
                            buf.Set(px, py, col);
                        }
                    }
                }
            }
        }
    }

    private static void DrawLiveShieldPerimeterV2(
        PixelBuffer buf,
        (int Cx, int Cy, int Rx, int Ry) env,
        int animBucket)
    {
        for (int deg = 0; deg < 360; deg += 2)
        {
            float rad = deg * (MathF.PI / 180.0f);
            int px = env.Cx + (int)MathF.Round(MathF.Cos(rad) * env.Rx);
            int py = env.Cy + (int)MathF.Round(MathF.Sin(rad) * env.Ry);

            if (((deg / 6 + animBucket) % 2) == 0)
            {
                buf.Set(px, py, Palette.HexClearanceTeal);
            }
            else if (((deg / 6 + animBucket) % 3) == 0)
            {
                buf.Set(px, py, Palette.HexSpsShieldArcCyan);
            }
        }
    }

    private static PixelBuffer ComposeManualChartBufferV2(PixelBuffer shipBuf)
    {
        PixelBuffer manual = new(ArtWidth, ArtHeight);
        manual.Clear(Palette.HexOutlineVoid);

        for (int gx = 16; gx < ArtWidth; gx += 24)
        {
            for (int gy = 12; gy < ArtHeight; gy += 18)
            {
                manual.Set(gx, gy, "#3d2006");
            }
        }

        for (int y = 0; y < ArtHeight; y++)
        {
            for (int x = 0; x < ArtWidth; x++)
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
                    string amberCol = lum > 0.48f
                        ? Palette.HexCrtAmberPrimary
                        : Dither.BayerDither(x, y, Palette.HexCrtAmberDimmed, Palette.HexCrtAmberPrimary, lum * 1.65f);
                    manual.Set(x, y, amberCol);
                }
            }
        }

        return manual;
    }

    private static void ComposeSpsViewportLayerV2(
        PixelBuffer viewportBuf,
        PixelBuffer shipBuf,
        ShipHardpointLayoutV2 hp,
        ShipRenderOptions options,
        int animBucket)
    {
        bool[] hullMask = shipBuf.CaptureNonNullMask(excludeOutline: true);

        // Step 1: Render scanned vessel as a crisp dark-slate ribbed bulkhead cutaway silhouette
        for (int y = 0; y < ArtHeight; y++)
        {
            for (int x = 0; x < ArtWidth; x++)
            {
                string? srcHex = shipBuf.Get(x, y);
                if (srcHex == null)
                    continue;

                if (srcHex.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                {
                    viewportBuf.Set(x, y, Palette.HexOutlineVoid);
                    continue;
                }

                bool isSkinEdge = !shipBuf.IsMasked(hullMask, x - 1, y) ||
                                  !shipBuf.IsMasked(hullMask, x + 1, y) ||
                                  !shipBuf.IsMasked(hullMask, x, y - 1) ||
                                  !shipBuf.IsMasked(hullMask, x, y + 1);
                if (isSkinEdge)
                {
                    viewportBuf.Set(x, y, "#243b53");
                    continue;
                }

                float lum = Palette.GetLuminance(srcHex);
                int relX = x - hp.HullBounds.X;
                bool isVerticalRib = relX > 3 && x < hp.HullBounds.Right - 4 && (relX % 10 == 0);
                bool isKeelSpine = y == hp.ReactorCore.Y && ((x + y) % 2 == 0);
                bool isGussetTick = (relX % 10 == 1) &&
                                    (!shipBuf.IsMasked(hullMask, x, y - 2) || !shipBuf.IsMasked(hullMask, x, y + 2));

                if (isVerticalRib || isGussetTick)
                {
                    viewportBuf.Set(x, y, "#1b2e42");
                }
                else if (isKeelSpine)
                {
                    viewportBuf.Set(x, y, "#162638");
                }
                else
                {
                    string cutawayCol = Dither.BayerDither(
                        x, y,
                        "#0a121c",
                        "#121f2f",
                        Math.Clamp(lum * 0.85f, 0.10f, 0.72f));
                    viewportBuf.Set(x, y, cutawayCol);
                }
            }
        }

        // ShieldsUp blocks internal SPS visibility with animated static arcs + SHIELD BLOCK readout
        if (options.ShieldsUp)
        {
            DrawLiveShieldPerimeterV2(viewportBuf, hp.ShieldPerimeter, animBucket);
            DeterministicRng arcRng = new($"{options.SeedId}:sps_v2_shield:{animBucket}");
            int minArcX = Math.Max(SafeMinArtX, hp.ShieldPerimeter.Cx - hp.ShieldPerimeter.Rx + 6);
            int maxArcX = Math.Min(SafeMaxArtX, hp.ShieldPerimeter.Cx + hp.ShieldPerimeter.Rx - 6);
            int minArcY = Math.Max(SafeMinArtY, hp.ShieldPerimeter.Cy - hp.ShieldPerimeter.Ry + 4);
            int maxArcY = Math.Min(SafeMaxArtY, hp.ShieldPerimeter.Cy + hp.ShieldPerimeter.Ry - 4);

            for (int a = 0; a < 8; a++)
            {
                int curX = arcRng.NextInt(minArcX, Math.Max(minArcX + 1, maxArcX - 24));
                int curY = arcRng.NextInt(minArcY, Math.Max(minArcY + 1, maxArcY));
                for (int seg = 0; seg < 4; seg++)
                {
                    int nextX = Math.Clamp(curX + arcRng.NextInt(6, 16), minArcX, maxArcX);
                    int nextY = Math.Clamp(curY + arcRng.NextInt(-10, 11), minArcY, maxArcY);
                    viewportBuf.DrawLine(curX, curY, nextX, nextY, (seg % 2 == 0) ? Palette.HexSpsShieldArcCyan : Palette.HexSpsShieldArcIce);
                    curX = nextX;
                    curY = nextY;
                }
            }

            viewportBuf.FillRect(hp.ReactorCore.X - 30, hp.ReactorCore.Y - 5, 60, 9, Palette.HexOutlineVoid);
            viewportBuf.DrawBitmapText(hp.ReactorCore.X - 25, hp.ReactorCore.Y - 3, "SHIELD BLOCK", Palette.HexSpsShieldArcIce);
            return;
        }

        bool pulseBright = (animBucket % 2) == 0;
        Span<HardpointRect> allModules =
        [
            hp.ReactorBounds,
            hp.WeaponsBay,
            hp.SecondaryWeaponsBay,
            hp.BridgeComms,
            hp.CargoBay,
            hp.UpperWarpPylon,
            hp.LowerWarpPylon
        ];

        // Step 2: Thin 1px dotted/pulsing energy trace lines from Reactor to subsystems (strictly inside hullMask, never crossing modules)
        string wpnTraceCol = options.WeaponsHot ? Palette.HexSpsWeaponsHotOrange : "#283848";
        DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.WeaponsBay, wpnTraceCol, options.WeaponsHot, animBucket);
        if (!hp.SecondaryWeaponsBay.IsEmpty)
        {
            DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.SecondaryWeaponsBay, wpnTraceCol, options.WeaponsHot, animBucket);
        }

        bool warpActive = options.WarpPrimed || options.UncannyWarpPylonPulse;
        string warpTraceCol = warpActive ? "#d97706" : "#283848";
        DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.UpperWarpPylon, warpTraceCol, warpActive, animBucket);
        if (!hp.LowerWarpPylon.IsEmpty && hp.LowerWarpPylon != hp.UpperWarpPylon)
        {
            DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.LowerWarpPylon, warpTraceCol, warpActive, animBucket);
        }

        bool commsActive = !options.CommsBroken;
        string commsTraceCol = commsActive
            ? (options.MilitaryCommsEncryption ? "#0ea5e9" : "#0284c7")
            : "#283848";
        DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.BridgeComms, commsTraceCol, commsActive, animBucket);

        bool cargoActive = options.CargoHighDraw || options.CargoBioSignature;
        string cargoTraceCol = options.CargoHighDraw
            ? Palette.HexSpsCargoHighDraw
            : (options.CargoBioSignature ? "#22c55e" : "#283848");
        DrawSpsTraceLine1px(viewportBuf, hullMask, allModules, hp.ReactorBounds, hp.CargoBay, cargoTraceCol, cargoActive, animBucket);

        // Step 3: Compact, translucent/dithered glowing subsystem schematics with crisp 1px outlines & micro hardware details
        // 3a. Weapons (Crimson/Red-Orange when hot; fades to cool slate on compliance)
        DrawSpsWeaponsSchematic(viewportBuf, hullMask, hp.WeaponsBay, options.WeaponsHot, pulseBright);
        if (!hp.SecondaryWeaponsBay.IsEmpty)
        {
            DrawSpsWeaponsSchematic(viewportBuf, hullMask, hp.SecondaryWeaponsBay, options.WeaponsHot, pulseBright);
        }

        // 3b. Thruster / Warp Manifolds (Warm Amber/Gold seated inside engine nacelles; erratic bloom on Ghost Route)
        bool erraticHigh = options.UncannyWarpPylonPulse && (animBucket % 3) != 1;
        DrawSpsThrusterManifoldSchematic(viewportBuf, hullMask, hp.UpperWarpPylon, warpActive, options.UncannyWarpPylonPulse, erraticHigh);
        if (!hp.LowerWarpPylon.IsEmpty && hp.LowerWarpPylon != hp.UpperWarpPylon)
        {
            DrawSpsThrusterManifoldSchematic(viewportBuf, hullMask, hp.LowerWarpPylon, warpActive, options.UncannyWarpPylonPulse, erraticHigh);
        }

        if (options.UncannyWarpPylonPulse)
        {
            foreach (HardpointRect nozzle in hp.EngineNozzles)
            {
                for (int py = nozzle.Y; py < nozzle.Bottom; py++)
                {
                    for (int px = Math.Max(2, nozzle.X - 3); px < nozzle.X; px++)
                    {
                        if (shipBuf.IsMasked(hullMask, px, py))
                        {
                            viewportBuf.Set(px, py, erraticHigh ? "#fbbf24" : Palette.HexSpsWarpPrimed);
                        }
                    }
                }
            }
        }

        // 3c. Bridge / Comms Transceiver (Cool Cyan; dead slate when broken; future MilitaryCommsEncryption hook)
        DrawSpsBridgeCommsSchematic(viewportBuf, hullMask, hp.BridgeComms, !options.CommsBroken, options.MilitaryCommsEncryption, pulseBright);

        // 3d. Cargo Bay Grid Cells (Violet #a855f7 powered contraband; reserved Day 21 Bio-Green #22c55e hook; cool slate when inert)
        DrawSpsCargoGridSchematic(viewportBuf, hullMask, hp.CargoBay, options.CargoHighDraw, options.CargoBioSignature, pulseBright);

        // 3e. Concentric Reactor Core Ring (Warm Amber/Gold)
        DrawSpsReactorCoreSchematic(viewportBuf, hullMask, hp.ReactorBounds, pulseBright);
    }

    private static void SetHullClipped(PixelBuffer buf, bool[] hullMask, int x, int y, string hex)
    {
        if ((uint)x < ArtWidth && (uint)y < ArtHeight && hullMask[y * ArtWidth + x])
        {
            buf.Set(x, y, hex);
        }
    }

    private static void DrawSpsSchematicFrameAndDitherGlow(
        PixelBuffer buf,
        bool[] hullMask,
        HardpointRect r,
        string outlineHex,
        string cornerHex,
        string glowHex,
        float ditherDensity)
    {
        if (r.IsEmpty)
            return;

        // Translucent Bayer-dithered energy glow over the cutaway interior
        for (int y = r.Y + 1; y < r.Bottom - 1; y++)
        {
            for (int x = r.X + 1; x < r.Right - 1; x++)
            {
                if (!hullMask[y * ArtWidth + x])
                    continue;

                string baseCol = buf.Get(x, y) ?? "#0c1622";
                string blended = Dither.BayerDither(x, y, baseCol, glowHex, ditherDensity);
                buf.Set(x, y, blended);
            }
        }

        // Crisp 1px schematic outline with bright corner ticks
        for (int x = r.X; x < r.Right; x++)
        {
            SetHullClipped(buf, hullMask, x, r.Y, outlineHex);
            SetHullClipped(buf, hullMask, x, r.Bottom - 1, outlineHex);
        }
        for (int y = r.Y; y < r.Bottom; y++)
        {
            SetHullClipped(buf, hullMask, r.X, y, outlineHex);
            SetHullClipped(buf, hullMask, r.Right - 1, y, outlineHex);
        }

        SetHullClipped(buf, hullMask, r.X, r.Y, cornerHex);
        SetHullClipped(buf, hullMask, r.Right - 1, r.Y, cornerHex);
        SetHullClipped(buf, hullMask, r.X, r.Bottom - 1, cornerHex);
        SetHullClipped(buf, hullMask, r.Right - 1, r.Bottom - 1, cornerHex);
    }

    private static void DrawSpsReactorCoreSchematic(PixelBuffer buf, bool[] hullMask, HardpointRect r, bool pulseBright)
    {
        if (r.IsEmpty)
            return;

        DrawSpsSchematicFrameAndDitherGlow(
            buf, hullMask, r,
            outlineHex: "#b45309",
            cornerHex: "#fbbf24",
            glowHex: "#78350f",
            ditherDensity: 0.55f);

        int cx = r.CenterX;
        int cy = r.CenterY;
        int ringRx = Math.Max(2, (r.W - 3) / 2);
        int ringRy = Math.Max(2, (r.H - 3) / 2);

        // Concentric 1px amber reactor containment ring + bright gold/ivory core pip
        for (int y = r.Y + 1; y < r.Bottom - 1; y++)
        {
            for (int x = r.X + 1; x < r.Right - 1; x++)
            {
                float dx = (x - cx) / (float)Math.Max(1, ringRx);
                float dy = (y - cy) / (float)Math.Max(1, ringRy);
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d <= 0.38f)
                {
                    SetHullClipped(buf, hullMask, x, y, pulseBright ? "#fef9c3" : "#fde047");
                }
                else if (d is >= 0.68f and <= 1.12f)
                {
                    SetHullClipped(buf, hullMask, x, y, pulseBright ? "#fbbf24" : "#f59e0b");
                }
            }
        }
        SetHullClipped(buf, hullMask, cx, cy, "#fef9c3");
    }

    private static void DrawSpsWeaponsSchematic(PixelBuffer buf, bool[] hullMask, HardpointRect r, bool weaponsHot, bool pulseBright)
    {
        if (r.IsEmpty)
            return;

        string outline = weaponsHot ? Palette.HexSpsWeaponsHot : "#334155";
        string corner = weaponsHot ? Palette.HexSpsWeaponsHotOrange : "#475569";
        string glow = weaponsHot ? "#7f1d1d" : "#152232";
        DrawSpsSchematicFrameAndDitherGlow(buf, hullMask, r, outline, corner, glow, weaponsHot ? 0.58f : 0.25f);

        // Sleek internal weapon capacitor coils + central firing rail inside mount
        int midY = r.CenterY;
        string railCol = weaponsHot ? (pulseBright ? "#fde047" : "#f97316") : "#334155";
        string coilCol = weaponsHot ? "#ef4444" : "#293849";
        for (int x = r.X + 2; x < r.Right - 2; x++)
        {
            SetHullClipped(buf, hullMask, x, midY, railCol);
            if (((x - r.X) % 2 == 0) && r.H >= 6)
            {
                SetHullClipped(buf, hullMask, x, Math.Max(r.Y + 1, midY - 1), coilCol);
                SetHullClipped(buf, hullMask, x, Math.Min(r.Bottom - 2, midY + 1), coilCol);
            }
        }
    }

    private static void DrawSpsThrusterManifoldSchematic(
        PixelBuffer buf,
        bool[] hullMask,
        HardpointRect r,
        bool active,
        bool uncannyPulse,
        bool erraticHigh)
    {
        if (r.IsEmpty)
            return;

        string outline = uncannyPulse ? "#fbbf24" : (active ? "#d97706" : "#334155");
        string corner = uncannyPulse ? "#ffffff" : (active ? "#fbbf24" : "#475569");
        string glow = uncannyPulse ? "#92400e" : (active ? "#78350f" : "#152232");
        DrawSpsSchematicFrameAndDitherGlow(buf, hullMask, r, outline, corner, glow, active ? 0.52f : 0.25f);

        // Compact thruster manifold ribs + injector spine seated neatly inside nacelle
        int midY = r.CenterY;
        string spineCol = uncannyPulse ? (erraticHigh ? "#ffffff" : "#fde047") : (active ? "#fbbf24" : "#334155");
        string vaneCol = active ? "#f59e0b" : "#293849";
        for (int x = r.X + 2; x < r.Right - 2; x++)
        {
            SetHullClipped(buf, hullMask, x, midY, spineCol);
            if ((x - r.X) % 3 == 0 && r.H >= 5)
            {
                for (int y = r.Y + 1; y < r.Bottom - 1; y++)
                {
                    SetHullClipped(buf, hullMask, x, y, y == midY ? spineCol : vaneCol);
                }
            }
        }
    }

    private static void DrawSpsBridgeCommsSchematic(
        PixelBuffer buf,
        bool[] hullMask,
        HardpointRect r,
        bool commsOnline,
        bool militaryCommsEncryption,
        bool pulseBright)
    {
        if (r.IsEmpty)
            return;

        string outline = !commsOnline ? "#475569" : (militaryCommsEncryption ? "#22d3ee" : "#0284c7");
        string corner = !commsOnline ? "#64748b" : "#38bdf8";
        string glow = !commsOnline ? "#1e293b" : "#0c4a6e";
        DrawSpsSchematicFrameAndDitherGlow(buf, hullMask, r, outline, corner, glow, commsOnline ? 0.50f : 0.22f);

        // Internal transceiver carrier wave / encrypted node pips
        int midY = r.CenterY;
        for (int x = r.X + 2; x < r.Right - 2; x++)
        {
            if (!commsOnline)
            {
                if ((x % 2) == 0)
                    SetHullClipped(buf, hullMask, x, midY, "#475569");
                continue;
            }

            int waveOff = ((x - r.X + (pulseBright ? 1 : 0)) % 3 == 0) ? -1 : 0;
            int py = Math.Clamp(midY + waveOff, r.Y + 1, r.Bottom - 2);
            string nodeCol = militaryCommsEncryption && ((x - r.X) % 2 == 1) ? "#fbbf24" : "#7dd3fc";
            SetHullClipped(buf, hullMask, x, py, nodeCol);
        }
    }

    private static void DrawSpsCargoGridSchematic(
        PixelBuffer buf,
        bool[] hullMask,
        HardpointRect r,
        bool violetPoweredCargo,
        bool bioGreenSignature,
        bool pulseBright)
    {
        if (r.IsEmpty)
            return;

        string outline;
        string corner;
        string glow;
        string gridCol;
        string cellCoreCol;
        float density;

        if (violetPoweredCargo)
        {
            outline = "#a855f7";
            corner = "#d8b4fe";
            glow = "#581c87";
            gridCol = "#9333ea";
            cellCoreCol = pulseBright ? "#f3e8ff" : "#c084fc";
            density = 0.60f;
        }
        else if (bioGreenSignature)
        {
            outline = "#22c55e";
            corner = "#86efac";
            glow = "#14532d";
            gridCol = "#16a34a";
            cellCoreCol = pulseBright ? "#dcfce7" : "#4ade80";
            density = 0.58f;
        }
        else
        {
            outline = "#334155";
            corner = "#475569";
            glow = "#131f2e";
            gridCol = "#263547";
            cellCoreCol = "#1e293b";
            density = 0.24f;
        }

        DrawSpsSchematicFrameAndDitherGlow(buf, hullMask, r, outline, corner, glow, density);

        // Clean cargo bay grid cells (vertical & horizontal 1px dividers + cell nodes)
        int midY = r.CenterY;
        if (r.H >= 6)
        {
            for (int x = r.X + 1; x < r.Right - 1; x++)
            {
                SetHullClipped(buf, hullMask, x, midY, gridCol);
            }
        }

        int stepX = Math.Max(3, (r.W - 2) / 3);
        for (int gx = r.X + stepX; gx < r.Right - 2; gx += stepX)
        {
            for (int y = r.Y + 1; y < r.Bottom - 1; y++)
            {
                SetHullClipped(buf, hullMask, gx, y, gridCol);
            }
        }

        if (violetPoweredCargo || bioGreenSignature)
        {
            for (int y = r.Y + 2; y < r.Bottom - 1; y += Math.Max(2, r.H - 4))
            {
                if (y == midY && r.H >= 6)
                    continue;
                for (int x = r.X + 2; x < r.Right - 2; x += stepX)
                {
                    if (x < r.Right - 1)
                    {
                        SetHullClipped(buf, hullMask, x, Math.Clamp(y, r.Y + 1, r.Bottom - 2), cellCoreCol);
                    }
                }
            }
        }
    }

    private static void DrawSpsTraceLine1px(
        PixelBuffer buf,
        bool[] hullMask,
        ReadOnlySpan<HardpointRect> modules,
        HardpointRect fromReactor,
        HardpointRect toModule,
        string traceHex,
        bool activePower,
        int animBucket)
    {
        if (fromReactor.IsEmpty || toModule.IsEmpty)
            return;

        (int X, int Y) start = (fromReactor.CenterX, fromReactor.CenterY);
        (int X, int Y) end = (toModule.CenterX, toModule.CenterY);

        // Pick orthogonal elbow (horizontal-first vs vertical-first) that stays maximally inside the hull cutaway
        List<(int X, int Y)> pathA = BuildOrthogonalTrace(start.X, start.Y, end.X, start.Y, end.X, end.Y);
        List<(int X, int Y)> pathB = BuildOrthogonalTrace(start.X, start.Y, start.X, end.Y, end.X, end.Y);
        List<(int X, int Y)> chosen = CountMaskedPoints(hullMask, pathA) >= CountMaskedPoints(hullMask, pathB) ? pathA : pathB;

        string pulsePipHex = activePower ? "#f8fafc" : "#334155";
        for (int i = 0; i < chosen.Count; i++)
        {
            var pt = chosen[i];
            if ((uint)pt.X >= ArtWidth || (uint)pt.Y >= ArtHeight || !hullMask[pt.Y * ArtWidth + pt.X])
                continue;
            if (IsPointInsideAnyModule(pt.X, pt.Y, modules))
                continue;

            bool isPulsePip = activePower && (((i - animBucket * 2) % 7 + 7) % 7 == 0);
            if (isPulsePip)
            {
                buf.Set(pt.X, pt.Y, pulsePipHex);
            }
            else if ((i % 2) == 0)
            {
                buf.Set(pt.X, pt.Y, traceHex);
            }
        }
    }

    private static List<(int X, int Y)> BuildOrthogonalTrace(int x0, int y0, int ex, int ey, int x1, int y1)
    {
        List<(int X, int Y)> path = PixelBuffer.TraceBresenhamLine(x0, y0, ex, ey);
        List<(int X, int Y)> leg2 = PixelBuffer.TraceBresenhamLine(ex, ey, x1, y1);
        for (int i = 1; i < leg2.Count; i++)
        {
            path.Add(leg2[i]);
        }
        return path;
    }

    private static int CountMaskedPoints(bool[] hullMask, List<(int X, int Y)> pts)
    {
        int count = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if ((uint)p.X < ArtWidth && (uint)p.Y < ArtHeight && hullMask[p.Y * ArtWidth + p.X])
                count++;
        }
        return count;
    }

    private static bool IsPointInsideAnyModule(int x, int y, ReadOnlySpan<HardpointRect> modules)
    {
        for (int i = 0; i < modules.Length; i++)
        {
            HardpointRect m = modules[i];
            if (!m.IsEmpty && x >= m.X && x < m.Right && y >= m.Y && y < m.Bottom)
                return true;
        }
        return false;
    }
}
