using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// Foundational 120x56 procedural left-facing starship buffer generator (Blocks 4 & 5).
/// Bow on Left (x = 8..35), Core at (x = 35..85), Stern / Warp Pylons on Right (x = 75..114).
/// </summary>
public static class ShipRenderer
{
    public static PixelBuffer RenderPlaceholderBuffer(string seedId, bool spsMode = false)
    {
        PixelBuffer buf = new(Constants.ShipWidth, Constants.ShipHeight);
        DeterministicRng rng = new(seedId);

        // Deep space + Brown Dwarf ambient backdrop
        for (int y = 0; y < Constants.ShipHeight; y++)
        {
            string bg = Dither.BayerDither(0, y, Palette.HexOutlineVoid, Palette.HexBrownDwarfGlowDeep, y / (float)Constants.ShipHeight);
            buf.FillRect(0, y, Constants.ShipWidth, 1, bg);
        }

        string[] hullRamp = DetermineHullRamp(seedId, rng);
        if (spsMode)
        {
            hullRamp = [Palette.HexSpsHullDimDark, Palette.HexSpsHullDimDark, Palette.HexSpsHullDimLight, Palette.HexSpsHullDimLight];
        }

        // Core Hull facing Left
        ReadOnlySpan<(int X, int Y)> fuselage =
        [
            (14, 28), (34, 18), (94, 18), (104, 28), (94, 38), (34, 38)
        ];
        buf.FillPoly(fuselage, (px, py) =>
        {
            float t = 1.0f - MathF.Abs(py - 26) / 16.0f;
            return Dither.SampleRamp(px, py, hullRamp, t);
        });

        // Rear Warp Pylons (right side x = 75..112)
        buf.FillRect(74, 11, 34, 5, hullRamp[2]);
        buf.FillRect(74, 40, 34, 5, hullRamp[1]);
        buf.DrawLine(66, 18, 76, 13, hullRamp[1]);
        buf.DrawLine(66, 38, 76, 42, hullRamp[1]);

        // Engine glow pixels
        buf.FillRect(106, 12, 3, 3, Palette.HexClearanceTeal);
        buf.FillRect(106, 41, 3, 3, Palette.HexClearanceTeal);

        buf.ApplyOutline(Palette.HexOutlineVoid);
        if (!spsMode)
        {
            buf.ApplyRimLight(Palette.HexBrownDwarfGlowRim);
        }
        else
        {
            // Block 5: SPS Reactor Core + 1px Bresenham conduits to 4 fixed hardpoints
            buf.FillEllipse(60, 28, 5, 4, (_, _, _, _, _) => Palette.HexSpsWarpPrimed);
            buf.DrawLine(60, 28, 20, 28, Palette.HexSpsWeaponsHot);    // Forward Weapon Mounts
            buf.DrawLine(60, 28, 92, 13, Palette.HexSpsWarpPrimed);    // Warp Pylons
            buf.DrawLine(60, 28, 36, 22, Palette.HexSpsCommsActive);   // Bridge Comms Array
            buf.DrawLine(60, 28, 64, 35, Palette.HexSpsCargoHighDraw); // Cargo Bay
        }

        return buf;
    }

    private static string[] DetermineHullRamp(string seedId, DeterministicRng rng)
    {
        if (seedId.StartsWith("GUV-", StringComparison.OrdinalIgnoreCase))
            return Palette.GuPrimaryWhiteRamp;
        if (seedId.StartsWith("KDF-", StringComparison.OrdinalIgnoreCase))
            return Palette.KordaniPrimaryBlueGreenRamp;
        if (seedId.StartsWith("BMC-", StringComparison.OrdinalIgnoreCase))
            return Palette.BrengopPrimaryGrossYellowRamp;
        if (seedId.StartsWith("KRW-", StringComparison.OrdinalIgnoreCase))
            return Palette.KrushvakPrimaryOliveGreenRamp;
        if (seedId.StartsWith("ORN-", StringComparison.OrdinalIgnoreCase))
            return Palette.OrunPrimaryGraniteRamp;

        string[][] fallbackPool =
        [
            Palette.GuPrimaryWhiteRamp,
            Palette.KordaniPrimaryBlueGreenRamp,
            Palette.BrengopPrimaryGrossYellowRamp,
            Palette.KrushvakPrimaryOliveGreenRamp,
            Palette.FreeTraderPrimarySlateRamp
        ];
        return fallbackPool[rng.NextInt(0, fallbackPool.Length)];
    }
}
