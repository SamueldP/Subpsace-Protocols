namespace SubspaceProtocols.Procedural;

/// <summary>
/// 4x4 Bayer ordered dithering across 4-step material ramps [shadow, mid, base, highlight]
/// defined in /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md (Block 2).
/// </summary>
public static class Dither
{
    private static readonly int[,] Bayer4x4 =
    {
        {  0,  8,  2, 10 },
        { 12,  4, 14,  6 },
        {  3, 11,  1,  9 },
        { 15,  7, 13,  5 }
    };

    public static string BayerDither(int x, int y, string colorDark, string colorLight, float ratio = 0.5f)
    {
        int bx = ((x % 4) + 4) % 4;
        int by = ((y % 4) + 4) % 4;
        float threshold = (Bayer4x4[by, bx] + 0.5f) / 16.0f;
        return ratio > threshold ? colorLight : colorDark;
    }

    /// <summary>
    /// Maps normalized light intensity t in [0..1] across a 4-color ramp [shadow, mid, base, highlight].
    /// </summary>
    public static string SampleRamp(int x, int y, ReadOnlySpan<string> ramp, float t)
    {
        if (ramp.IsEmpty)
            return "#ff00ff";
        if (ramp.Length == 1)
            return ramp[0];

        float clamped = Math.Clamp(t, 0.0f, 0.999f);
        float scaled = clamped * (ramp.Length - 1);
        int idx = (int)MathF.Floor(scaled);
        float frac = scaled - idx;

        if (idx >= ramp.Length - 1)
            return ramp[^1];

        return BayerDither(x, y, ramp[idx], ramp[idx + 1], frac);
    }
}
