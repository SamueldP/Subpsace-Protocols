namespace SubspaceProtocols.Simulation;

public sealed record StarSystemSignatureInfo(
    string Key,
    string DisplayLabel,
    bool IsRestricted);

/// <summary>
/// Canonical 6-system harmonic warp signature model for Day 3+ Warp Field Residue inspection.
/// Uses subtly distinct smooth multi-harmonic waveforms so the officer genuinely needs to
/// overlay the translucent magnetic strip onto the Glowing Warp Checker to catch phase/harmonic
/// deviations via red mismatch bleed.
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public static class WarpSignatureGenerator
{
    public const int WaveformCanvasWidth = 216;
    public const int WaveformCanvasHeight = 52;
    public const string RestrictedSystemKey = "Ashen Rift";

    /// <summary>
    /// Exact 6 sector star systems shown on the Glowing Warp Reference Spot.
    /// </summary>
    public static readonly StarSystemSignatureInfo[] SectorSystems =
    [
        new("Veridia", "VERIDIA", IsRestricted: false),
        new("Korr-Vath", "KORR-VATH", IsRestricted: false),
        new("Dul-Krag", "DUL-KRAG", IsRestricted: false),
        new("Gharok", "GHAROK", IsRestricted: false),
        new("Oruun-4", "ORUUN-4", IsRestricted: false),
        new(RestrictedSystemKey, "ASHEN RIFT*", IsRestricted: true)
    ];

    /// <summary>
    /// The 5 legal origin star systems for standard vessel manifests.
    /// </summary>
    public static readonly string[] LegalOriginSystems =
    [
        "Veridia",
        "Korr-Vath",
        "Dul-Krag",
        "Gharok",
        "Oruun-4"
    ];

    /// <summary>
    /// Normalizes legacy or prefixed aliases (such as "The Ashen Rift") to canonical keys.
    /// </summary>
    public static string NormalizeSystemKey(string? systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return "Veridia";

        string trimmed = systemName.Trim();
        if (trimmed.Equals("The Ashen Rift", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Ashen Rift", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ASHEN RIFT*", StringComparison.OrdinalIgnoreCase))
        {
            return RestrictedSystemKey;
        }

        foreach (StarSystemSignatureInfo info in SectorSystems)
        {
            if (info.Key.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                return info.Key;
        }

        return "Veridia";
    }

    public static bool IsRestrictedOrigin(string? systemName) =>
        NormalizeSystemKey(systemName).Equals(RestrictedSystemKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Evaluates the smooth harmonic warp signature at normalized horizontal coordinate t in [0, 1].
    /// Returns a continuous value roughly within [-0.95, +0.95].
    /// All 6 systems share a visually similar ~3-crest smooth harmonic profile with subtle
    /// differences in crest spacing, secondary harmonic bumps, and phase shifts.
    /// </summary>
    public static float SampleNormalized(string? systemName, float t)
    {
        float clampedT = Math.Clamp(t, 0.0f, 1.0f);
        float u = clampedT * MathF.PI * 2.0f;
        string key = NormalizeSystemKey(systemName);

        float raw = key switch
        {
            // Baseline smooth 3.0-cycle carrier with gentle trailing 2nd harmonic shoulder
            "Veridia" =>
                0.68f * MathF.Sin(3.00f * u + 0.15f) +
                0.22f * MathF.Sin(6.00f * u - 0.55f) +
                0.08f * MathF.Sin(9.00f * u + 0.40f),

            // Slightly compressed crest spacing (3.32 cycles) with leading 2nd harmonic lift
            "Korr-Vath" =>
                0.66f * MathF.Sin(3.32f * u + 0.25f) +
                0.24f * MathF.Sin(6.64f * u + 0.35f) +
                0.08f * MathF.Sin(9.96f * u - 0.25f),

            // Slightly wider crest spacing (2.68 cycles) with soft asymmetric shoulder
            "Dul-Krag" =>
                0.66f * MathF.Sin(2.68f * u + 0.12f) +
                0.26f * MathF.Sin(5.36f * u - 1.15f) +
                0.07f * MathF.Sin(8.04f * u + 0.70f),

            // Similar 3.05-cycle cadence to Veridia, subtle phase lead + inverted secondary swell
            "Gharok" =>
                0.65f * MathF.Sin(3.05f * u + 0.82f) +
                0.25f * MathF.Sin(6.10f * u + 1.35f) +
                0.09f * MathF.Sin(9.15f * u - 0.50f),

            // 2.92-cycle carrier with subtle 3rd-harmonic resonant twin-bump near peaks
            "Oruun-4" =>
                0.62f * MathF.Sin(2.92f * u - 0.34f) +
                0.18f * MathF.Sin(5.84f * u + 0.55f) +
                0.20f * MathF.Sin(8.76f * u - 0.90f),

            // Restricted Ashen Rift: deceptive smooth 3.16-cycle carrier with eerie sub-harmonic drift
            _ =>
                0.63f * MathF.Sin(3.16f * u - 0.68f) +
                0.23f * MathF.Sin(6.32f * u + 0.88f) +
                0.15f * MathF.Sin(4.74f * u + 0.22f)
        };

        return Math.Clamp(raw, -0.96f, 0.96f);
    }

    /// <summary>
    /// Converts the sampled harmonic waveform at pixel column px into a crisp vertical pixel offset
    /// inside [padY, canvasHeight - 1 - padY].
    /// </summary>
    public static int SamplePixelY(string? systemName, int px, int canvasWidth, int canvasHeight)
    {
        int safeW = Math.Max(2, canvasWidth);
        int safeH = Math.Max(8, canvasHeight);
        float t = Math.Clamp((float)px / (safeW - 1), 0.0f, 1.0f);
        float sample = SampleNormalized(systemName, t);
        float midY = (safeH - 1) * 0.5f;
        float amp = Math.Max(3.0f, (safeH * 0.5f) - 4.0f);
        int y = (int)MathF.Round(midY - sample * amp);
        return Math.Clamp(y, 2, safeH - 3);
    }

    /// <summary>
    /// Computes the mean absolute vertical pixel deviation between two star systems across a 1:1 canvas.
    /// Useful for both alignment verification and automated tests.
    /// </summary>
    public static float ComputeMeanPixelDeviation(
        string? systemA,
        string? systemB,
        int canvasWidth = WaveformCanvasWidth,
        int canvasHeight = WaveformCanvasHeight)
    {
        float sum = 0.0f;
        for (int x = 0; x < canvasWidth; x++)
        {
            int yA = SamplePixelY(systemA, x, canvasWidth, canvasHeight);
            int yB = SamplePixelY(systemB, x, canvasWidth, canvasHeight);
            sum += Math.Abs(yA - yB);
        }
        return sum / Math.Max(1, canvasWidth);
    }
}
