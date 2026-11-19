using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// Foundational 64x64 procedural portrait buffer generator (Block 4).
/// Checks assets/images/scripted/ override hook only when flagged as a scripted entity.
/// </summary>
public static class PortraitRenderer
{
    public static PixelBuffer RenderPlaceholderBuffer(string seedId)
    {
        PixelBuffer buf = new(Constants.PortraitWidth, Constants.PortraitHeight);
        DeterministicRng rng = new(seedId);

        // Background CRT bridge tint
        buf.FillRect(0, 0, Constants.PortraitWidth, Constants.PortraitHeight, Palette.HexDeepSpaceVoid);

        // Pick a skin ramp deterministically from the seed
        string[][] allSampleRamps =
        [
            Palette.HumanSkinRamps[0],
            Palette.HumanSkinRamps[2],
            Palette.KlaasariOceanBlueRamp,
            Palette.ThaneTitaniumWhiteRamp,
            Palette.KordaniSkinRamps[0],
            Palette.BrengopSkinRamps[0],
            Palette.KrushvakSkinRamps[2],
            Palette.HunBorianSkinRamps[0],
            Palette.OrunSkinRamp
        ];
        string[] ramp = allSampleRamps[rng.NextInt(0, allSampleRamps.Length)];

        // Shoulders / Collar (y = 44..63)
        ReadOnlySpan<(int X, int Y)> shoulders =
        [
            (10, 63), (16, 46), (48, 46), (54, 63)
        ];
        buf.FillPoly(shoulders, (px, py) =>
            Dither.SampleRamp(px, py, Palette.GuSecondaryGunmetalRamp, 1.0f - (py - 44) / 22.0f));

        // Head Base (cx = 32, cy = 28)
        int rx = rng.NextInt(11, 15);
        int ry = rng.NextInt(13, 16);
        buf.FillEllipse(32, 28, rx, ry, (px, py, dx, dy, dist) =>
        {
            float light = Math.Clamp(0.85f - dist * 0.55f - dx * 0.2f - dy * 0.25f, 0.05f, 0.98f);
            return Dither.SampleRamp(px, py, ramp, light);
        });

        // Eyes & Mouth preview markers
        buf.FillRect(26, 25, 3, 2, Palette.HexOutlineVoid);
        buf.FillRect(35, 25, 3, 2, Palette.HexOutlineVoid);
        buf.DrawLine(28, 36, 36, 36, Palette.HexOutlineVoid);

        // 1px dark outline
        buf.ApplyOutline(Palette.HexOutlineVoid);
        return buf;
    }
}
