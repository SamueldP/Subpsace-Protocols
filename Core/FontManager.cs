using System.Numerics;
using Raylib_cs;

namespace SubspaceProtocols.Core;

/// <summary>
/// Discovers custom (.ttf/.otf) and Windows built-in monospace/UI fonts, applies calibrated
/// cap-height scaling + glyph-atlas alpha contrast boosting, manages GPU font atlases without
/// VRAM leaks, and provides deterministic headless metrics for smoke tests.
/// </summary>
public static class FontManager
{
    private sealed record FontPreset(
        string Name,
        string? FilePath,
        bool IsCustom,
        float ScaleMultiplier,
        bool IsPixelFont = false,
        int NativePixelSize = 0,
        int VerticalDrawOffset = 0);

    public const int MinFontSizeOffset = -2;
    public const int MaxFontSizeOffset = 3;

    private static readonly int[] SizeBuckets = [10, 12, 14, 16, 18, 20, 24, 28, 32];
    private static readonly char[] SpecialDashes = ['\u2014', '\u2013'];
    private static readonly int[] Codepoints = BuildCodepoints();

    private const string SpacingBenchmarkSample = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static readonly List<FontPreset> Presets = [new("RaylibDefault", null, false, 1.0f)];
    private static readonly List<string> PresetNameView = ["RaylibDefault"];
    private static readonly Dictionary<int, Font> LoadedGpuFonts = new();

    private static int _activePresetIndex;
    private static int _fontSizeOffset;
    private static float? _cachedTtfSpacing;
    private static bool _isHeadlessMode;

    public static IReadOnlyList<string> PresetNames => PresetNameView;
    public static int PresetCount => Presets.Count;
    public static int ActivePresetIndex => _activePresetIndex;
    public static string ActivePresetName =>
        (_activePresetIndex >= 0 && _activePresetIndex < Presets.Count)
            ? Presets[_activePresetIndex].Name
            : "RaylibDefault";
    public static float ActiveScaleMultiplier => GetActivePreset().ScaleMultiplier;
    public static bool IsActivePresetPixelFont => GetActivePreset().IsPixelFont;
    public static int FontSizeOffset => _fontSizeOffset;
    public static int ActiveGpuFontCount => LoadedGpuFonts.Count;
    public static bool IsHeadlessMode => _isHeadlessMode;

    public static bool IsPixelFontPresetName(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        return presetName.Contains("PixelOperator", StringComparison.OrdinalIgnoreCase)
            || presetName.Contains("DepartureMono", StringComparison.OrdinalIgnoreCase)
            || presetName.Contains("Terminus", StringComparison.OrdinalIgnoreCase);
    }

    private static (bool IsPixelFont, int NativePixelSize, int VerticalDrawOffset, float ScaleMultiplier) ResolvePixelFontProfile(string presetName)
    {
        if (presetName.Contains("PixelOperator", StringComparison.OrdinalIgnoreCase))
            return (IsPixelFont: true, NativePixelSize: 16, VerticalDrawOffset: -3, ScaleMultiplier: 1.0f);

        if (presetName.Contains("DepartureMono", StringComparison.OrdinalIgnoreCase))
            return (IsPixelFont: true, NativePixelSize: 11, VerticalDrawOffset: -1, ScaleMultiplier: 1.15f);

        if (presetName.Contains("Terminus", StringComparison.OrdinalIgnoreCase))
            return (IsPixelFont: true, NativePixelSize: 12, VerticalDrawOffset: -1, ScaleMultiplier: 1.25f);

        return (IsPixelFont: false, NativePixelSize: 0, VerticalDrawOffset: 0, ScaleMultiplier: 1.25f);
    }

    public static void Initialize(string? preferredPreset, bool headlessMode = false, string? customFontsDirOverride = null)
    {
        UnloadCachedGpuFonts();
        _isHeadlessMode = headlessMode;
        _cachedTtfSpacing = null;

        Presets.Clear();
        PresetNameView.Clear();
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        string customFontsDir = string.IsNullOrWhiteSpace(customFontsDirOverride)
            ? Path.Combine(AppContext.BaseDirectory, "assets", "fonts")
            : customFontsDirOverride;

        if (Directory.Exists(customFontsDir))
        {
            List<string> fontFiles = Directory.EnumerateFiles(customFontsDir, "*.*", SearchOption.TopDirectoryOnly)
                .Where(static p =>
                {
                    string ext = Path.GetExtension(p);
                    return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(static p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                .ThenBy(static p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string filePath in fontFiles)
            {
                string stem = Path.GetFileNameWithoutExtension(filePath).Trim();
                if (string.IsNullOrEmpty(stem) || !seenNames.Add(stem))
                    continue;

                var profile = ResolvePixelFontProfile(stem);
                Presets.Add(new FontPreset(
                    stem,
                    filePath,
                    IsCustom: true,
                    ScaleMultiplier: profile.ScaleMultiplier,
                    IsPixelFont: profile.IsPixelFont,
                    NativePixelSize: profile.NativePixelSize,
                    VerticalDrawOffset: profile.VerticalDrawOffset));
                PresetNameView.Add(stem);
            }
        }

        List<string> systemFontDirs = GetSystemFontDirectories();
        TryAddSystemPreset("ConsolasBold", ["consolab.ttf"], systemFontDirs, seenNames, 1.25f);
        TryAddSystemPreset("Consolas", ["consola.ttf"], systemFontDirs, seenNames, 1.25f);
        TryAddSystemPreset("CascadiaMonoSemiBold", ["CascadiaMonoSB.ttf", "CascadiaCodeSB.ttf", "CascadiaMono.ttf", "CascadiaCode.ttf"], systemFontDirs, seenNames, 1.26f);
        TryAddSystemPreset("CascadiaMono", ["CascadiaMono.ttf", "CascadiaCode.ttf"], systemFontDirs, seenNames, 1.24f);
        TryAddSystemPreset("LucidaConsole", ["lucon.ttf"], systemFontDirs, seenNames, 1.23f);
        TryAddSystemPreset("CourierNewBold", ["courbd.ttf"], systemFontDirs, seenNames, 1.24f);
        TryAddSystemPreset("CourierNew", ["cour.ttf"], systemFontDirs, seenNames, 1.24f);
        TryAddSystemPreset("SegoeUISemibold", ["seguisb.ttf"], systemFontDirs, seenNames, 1.25f);
        TryAddSystemPreset("SegoeUIBold", ["segoeuib.ttf"], systemFontDirs, seenNames, 1.24f);
        TryAddSystemPreset("SegoeUI", ["segoeui.ttf"], systemFontDirs, seenNames, 1.25f);
        TryAddSystemPreset("TahomaBold", ["tahomabd.ttf"], systemFontDirs, seenNames, 1.22f);
        TryAddSystemPreset("VerdanaBold", ["verdanab.ttf"], systemFontDirs, seenNames, 1.22f);
        TryAddSystemPreset("TrebuchetBold", ["trebucbd.ttf"], systemFontDirs, seenNames, 1.24f);
        TryAddSystemPreset("Bahnschrift", ["bahnschrift.ttf"], systemFontDirs, seenNames, 1.25f);

        if (seenNames.Add("RaylibDefault"))
        {
            Presets.Add(new FontPreset("RaylibDefault", null, IsCustom: false, ScaleMultiplier: 1.0f));
            PresetNameView.Add("RaylibDefault");
        }

        _activePresetIndex = ResolveInitialPresetIndex(preferredPreset);
    }

    public static bool SetActivePreset(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return false;

        int index = FindPresetIndex(presetName.Trim());
        if (index < 0)
            return false;

        if (index != _activePresetIndex)
        {
            UnloadCachedGpuFonts();
            _activePresetIndex = index;
            _cachedTtfSpacing = null;
        }

        return true;
    }

    public static float GetPresetScaleMultiplier(string presetName)
    {
        int idx = FindPresetIndex(presetName);
        return idx >= 0 ? Presets[idx].ScaleMultiplier : 1.0f;
    }

    public static int SetFontSizeOffset(int offset)
    {
        int clamped = Math.Clamp(offset, MinFontSizeOffset, MaxFontSizeOffset);
        if (clamped != _fontSizeOffset)
        {
            UnloadCachedGpuFonts();
            _fontSizeOffset = clamped;
            _cachedTtfSpacing = null;
        }
        return _fontSizeOffset;
    }

    public static int StepFontSizeOffset(int delta) =>
        SetFontSizeOffset(_fontSizeOffset + delta);

    public static string CyclePreset(int delta)
    {
        if (Presets.Count == 0)
            return "RaylibDefault";

        if (Presets.Count == 1)
            return Presets[0].Name;

        int nextIndex = ((_activePresetIndex + delta) % Presets.Count + Presets.Count) % Presets.Count;
        if (nextIndex != _activePresetIndex)
        {
            UnloadCachedGpuFonts();
            _activePresetIndex = nextIndex;
            _cachedTtfSpacing = null;
        }

        return ActivePresetName;
    }

    public static int SnapToBucket(int requestedFontSize)
    {
        if (requestedFontSize <= SizeBuckets[0])
            return SizeBuckets[0];

        if (requestedFontSize >= SizeBuckets[^1])
            return SizeBuckets[^1];

        int bestBucket = SizeBuckets[0];
        int bestDist = Math.Abs(requestedFontSize - bestBucket);

        for (int i = 1; i < SizeBuckets.Length; i++)
        {
            int candidate = SizeBuckets[i];
            int dist = Math.Abs(requestedFontSize - candidate);
            // Strict '<' preserves tie-breaking downward for compact UI fit.
            if (dist < bestDist)
            {
                bestDist = dist;
                bestBucket = candidate;
            }
        }

        return bestBucket;
    }

    public static int ResolveEffectivePixelSize(int requestedFontSize) =>
        ResolveEffectivePixelSize(requestedFontSize, denseRow: false);

    public static int ResolveEffectivePixelSize(int requestedFontSize, bool denseRow)
    {
        int baseBucket = SnapToBucket(requestedFontSize);
        int effectiveOffset = denseRow && _fontSizeOffset > 0 && baseBucket <= 14 ? 0 : _fontSizeOffset;
        int adjusted = Math.Clamp(baseBucket + effectiveOffset, 8, 40);
        FontPreset preset = GetActivePreset();
        if (string.IsNullOrEmpty(preset.FilePath))
            return adjusted;

        if (preset.IsPixelFont && preset.NativePixelSize > 0)
        {
            if (denseRow && baseBucket <= 14)
            {
                return _fontSizeOffset < 0
                    ? Math.Clamp(preset.NativePixelSize + _fontSizeOffset, 10, preset.NativePixelSize)
                    : preset.NativePixelSize;
            }

            if (baseBucket >= 20)
            {
                int doublePx = preset.NativePixelSize * 2;
                return Math.Clamp(doublePx + Math.Clamp(effectiveOffset, -4, 4), 16, 48);
            }

            if (baseBucket >= 16)
            {
                int midPx = preset.NativePixelSize + (baseBucket - 12);
                return Math.Clamp(midPx + effectiveOffset, 12, 48);
            }

            int extraBucket = baseBucket >= 14 && preset.NativePixelSize < 16 ? 2 : 0;
            return Math.Clamp(preset.NativePixelSize + extraBucket + effectiveOffset, 10, 48);
        }

        return Math.Clamp((int)MathF.Round(adjusted * preset.ScaleMultiplier), 8, 48);
    }

    public static byte SnapPixelGlyphAlpha(byte alpha) =>
        alpha >= 112 ? (byte)255 : (byte)0;

    /// <summary>
    /// Alpha gamma + contrast boost for anti-aliased TrueType glyph atlases so 1px stems
    /// achieve crisp full-brightness rendering rather than washed-out grey smearing.
    /// </summary>
    public static byte BoostGlyphAlpha(byte alpha)
    {
        if (alpha == 0)
            return 0;

        float boosted = MathF.Pow(alpha / 255f, 0.50f) * 255f * 1.18f;
        return (byte)Math.Clamp((int)MathF.Round(boosted), 0, 255);
    }

    public static void DrawText(string text, int x, int y, int fontSize, Color color, bool denseRow = false) =>
        DrawText(text, (float)x, (float)y, fontSize, color, denseRow);

    public static void DrawText(string text, float x, float y, int fontSize, Color color, bool denseRow = false)
    {
        if (string.IsNullOrEmpty(text) || !Raylib.IsWindowReady())
            return;

        FontPreset preset = GetActivePreset();
        int effectivePx = ResolveEffectivePixelSize(fontSize, denseRow);
        int snapX = (int)MathF.Round(x);
        int snapY = (int)MathF.Round(y);

        if (_isHeadlessMode || string.IsNullOrEmpty(preset.FilePath))
        {
            DrawDefaultFont(text, snapX, snapY, effectivePx, color);
            return;
        }

        if (!TryGetOrLoadBucketFont(preset.FilePath, effectivePx, preset.IsPixelFont, out Font font))
        {
            DrawDefaultFont(text, snapX, snapY, effectivePx, color);
            return;
        }

        float spacing = GetOrComputeTtfSpacing(preset);
        int drawY = preset.IsPixelFont
            ? snapY + preset.VerticalDrawOffset
            : (effectivePx > fontSize ? snapY - 1 : snapY);
        Raylib.DrawTextEx(font, text, new Vector2(snapX, drawY), effectivePx, spacing, color);
    }

    public static int MeasureWidth(string text, int fontSize, bool denseRow = false)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        FontPreset preset = GetActivePreset();
        int effectivePx = ResolveEffectivePixelSize(fontSize, denseRow);
        if (!Raylib.IsWindowReady())
            return MeasureHeadlessFallbackWidth(text, effectivePx, preset);

        if (_isHeadlessMode || string.IsNullOrEmpty(preset.FilePath))
            return MeasureDefaultFontWidth(text, effectivePx);

        if (!TryGetOrLoadBucketFont(preset.FilePath, effectivePx, preset.IsPixelFont, out Font font))
            return MeasureDefaultFontWidth(text, effectivePx);

        float spacing = GetOrComputeTtfSpacing(preset);
        Vector2 measured = Raylib.MeasureTextEx(font, text, effectivePx, spacing);
        return Math.Max(0, (int)MathF.Ceiling(measured.X));
    }

    public static void Shutdown()
    {
        UnloadCachedGpuFonts();
        _cachedTtfSpacing = null;
    }

    private static FontPreset GetActivePreset() =>
        (_activePresetIndex >= 0 && _activePresetIndex < Presets.Count)
            ? Presets[_activePresetIndex]
            : new FontPreset("RaylibDefault", null, false, 1.0f);

    private static int FindPresetIndex(string presetName)
    {
        for (int i = 0; i < Presets.Count; i++)
        {
            if (Presets[i].Name.Equals(presetName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int ResolveInitialPresetIndex(string? preferredPreset)
    {
        if (!string.IsNullOrWhiteSpace(preferredPreset))
        {
            int preferredIdx = FindPresetIndex(preferredPreset.Trim());
            if (preferredIdx >= 0)
                return preferredIdx;
        }

        int pixelOpBoldIdx = FindPresetIndex("PixelOperatorMono-Bold");
        if (pixelOpBoldIdx >= 0)
            return pixelOpBoldIdx;

        for (int i = 0; i < Presets.Count; i++)
        {
            if (Presets[i].IsCustom)
                return i;
        }

        int consolasBoldIdx = FindPresetIndex("ConsolasBold");
        if (consolasBoldIdx >= 0)
            return consolasBoldIdx;

        int consolasIdx = FindPresetIndex("Consolas");
        if (consolasIdx >= 0)
            return consolasIdx;

        int defaultIdx = FindPresetIndex("RaylibDefault");
        return defaultIdx >= 0 ? defaultIdx : 0;
    }

    private static List<string> GetSystemFontDirectories()
    {
        List<string> dirs = [];

        string specialFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(specialFonts) && Directory.Exists(specialFonts))
            dirs.Add(specialFonts);

        string? winDir = Environment.GetEnvironmentVariable("WINDIR");
        if (!string.IsNullOrWhiteSpace(winDir))
        {
            string winFonts = Path.Combine(winDir, "Fonts");
            if (Directory.Exists(winFonts) &&
                !dirs.Any(d => d.Equals(winFonts, StringComparison.OrdinalIgnoreCase)))
            {
                dirs.Add(winFonts);
            }
        }

        return dirs;
    }

    private static void TryAddSystemPreset(
        string presetName,
        string[] candidateFileNames,
        List<string> systemFontDirs,
        HashSet<string> seenNames,
        float scaleMultiplier)
    {
        if (seenNames.Contains(presetName))
            return;

        foreach (string dir in systemFontDirs)
        {
            foreach (string fileName in candidateFileNames)
            {
                string fullPath = Path.Combine(dir, fileName);
                if (File.Exists(fullPath))
                {
                    seenNames.Add(presetName);
                    Presets.Add(new FontPreset(presetName, fullPath, IsCustom: false, ScaleMultiplier: scaleMultiplier));
                    PresetNameView.Add(presetName);
                    return;
                }
            }
        }
    }

    private static bool TryGetOrLoadBucketFont(string filePath, int effectivePx, bool isPixelFont, out Font font)
    {
        if (LoadedGpuFonts.TryGetValue(effectivePx, out font))
            return true;

        font = default;
        if (!File.Exists(filePath))
            return false;

        Font loaded = Raylib.LoadFontEx(filePath, effectivePx, Codepoints, Codepoints.Length);
        Font defaultFont = Raylib.GetFontDefault();
        if (loaded.Texture.Id == 0 ||
            loaded.Texture.Id == defaultFont.Texture.Id ||
            loaded.GlyphCount <= 0 ||
            !Raylib.IsFontReady(loaded))
        {
            return false;
        }

        ApplyGlyphAtlasContrastBoost(ref loaded, effectivePx, isPixelFont);

        LoadedGpuFonts[effectivePx] = loaded;
        font = loaded;
        return true;
    }

    private static unsafe void ApplyGlyphAtlasContrastBoost(ref Font font, int effectivePx, bool isPixelFont)
    {
        if (font.Texture.Id == 0)
            return;

        Image atlasImg = Raylib.LoadImageFromTexture(font.Texture);
        if (atlasImg.Width > 0 && atlasImg.Height > 0 && atlasImg.Data != null)
        {
            Raylib.ImageFormat(ref atlasImg, PixelFormat.UncompressedR8G8B8A8);
            byte* pixels = (byte*)atlasImg.Data;
            int totalBytes = atlasImg.Width * atlasImg.Height * 4;
            for (int i = 0; i < totalBytes; i += 4)
            {
                byte a = pixels[i + 3];
                if (a > 0)
                {
                    pixels[i] = 255;
                    pixels[i + 1] = 255;
                    pixels[i + 2] = 255;
                    pixels[i + 3] = isPixelFont ? SnapPixelGlyphAlpha(a) : BoostGlyphAlpha(a);
                }
            }

            Texture2D boostedTex = Raylib.LoadTextureFromImage(atlasImg);
            Raylib.UnloadImage(atlasImg);
            if (boostedTex.Id != 0)
            {
                Raylib.UnloadTexture(font.Texture);
                font.Texture = boostedTex;
            }
        }
        else
        {
            Raylib.UnloadImage(atlasImg);
        }

        _ = effectivePx;
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Point);
    }

    private static float GetOrComputeTtfSpacing(FontPreset preset)
    {
        if (_cachedTtfSpacing.HasValue)
            return _cachedTtfSpacing.Value;

        if (preset.IsPixelFont)
        {
            _cachedTtfSpacing = 0f;
            return 0f;
        }

        float spacing = 0f;
        if (!string.IsNullOrEmpty(preset.FilePath))
        {
            int defaultPx = Math.Clamp(10 + _fontSizeOffset, 8, 40);
            int scaledPx = ResolveEffectivePixelSize(10);
            if (TryGetOrLoadBucketFont(preset.FilePath, scaledPx, preset.IsPixelFont, out Font font10))
            {
                float defaultWidth = MeasureDefaultFontWidth(SpacingBenchmarkSample, defaultPx);
                float ttfWidth = Raylib.MeasureTextEx(font10, SpacingBenchmarkSample, scaledPx, 0f).X;
                if (defaultWidth > 0f)
                {
                    if (ttfWidth > defaultWidth * 1.18f)
                        spacing = -1.0f;
                    else if (ttfWidth > defaultWidth * 1.04f)
                        spacing = -0.5f;
                }
            }
        }

        _cachedTtfSpacing = spacing;
        return spacing;
    }

    private static void UnloadCachedGpuFonts()
    {
        if (LoadedGpuFonts.Count == 0)
            return;

        if (Raylib.IsWindowReady())
        {
            foreach (Font font in LoadedGpuFonts.Values)
            {
                if (font.Texture.Id != 0)
                    Raylib.UnloadFont(font);
            }
        }

        LoadedGpuFonts.Clear();
    }

    private static string NormalizeDefaultBitmapCharacters(string text)
    {
        if (text.IndexOfAny(['\u2018', '\u2019', '\u201C', '\u201D', '\u2026']) < 0)
            return text;

        return text
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace("\u2026", "...", StringComparison.Ordinal);
    }

    private static void DrawDefaultFont(string text, int x, int y, int bucket, Color color)
    {
        string safe = NormalizeDefaultBitmapCharacters(text);
        if (safe.IndexOfAny(SpecialDashes) < 0)
        {
            Raylib.DrawText(safe, x, y, bucket, color);
            return;
        }

        int lineStep = bucket + Math.Max(2, bucket / 5);
        int curY = y;
        string[] lines = safe.Split('\n');
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            DrawDefaultFontLineWithDashes(lines[lineIdx], x, curY, bucket, color);
            curY += lineStep;
        }
    }

    private static void DrawDefaultFontLineWithDashes(string line, int x, int y, int bucket, Color color)
    {
        if (string.IsNullOrEmpty(line))
            return;

        if (line.IndexOfAny(SpecialDashes) < 0)
        {
            Raylib.DrawText(line, x, y, bucket, color);
            return;
        }

        int spacing = Math.Max(1, bucket / 10);
        int barH = Math.Max(1, bucket / 10);
        int curX = x;
        int segStart = 0;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch != '\u2014' && ch != '\u2013')
                continue;

            if (i > segStart)
            {
                string slice = line[segStart..i];
                Raylib.DrawText(slice, curX, y, bucket, color);
                curX += Raylib.MeasureText(slice, bucket) + spacing;
            }

            int dashW = GetSpecialDashWidth(ch, bucket);
            Raylib.DrawRectangle(curX, y + bucket / 2, dashW, barH, color);
            curX += dashW + spacing;
            segStart = i + 1;
        }

        if (segStart < line.Length)
        {
            string tail = line[segStart..];
            Raylib.DrawText(tail, curX, y, bucket, color);
        }
    }

    private static int MeasureDefaultFontWidth(string text, int bucket)
    {
        string safe = NormalizeDefaultBitmapCharacters(text);
        if (safe.IndexOfAny(SpecialDashes) < 0)
            return Raylib.MeasureText(safe, bucket);

        int maxLineWidth = 0;
        string[] lines = safe.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int w = MeasureDefaultFontLineWithDashes(lines[i], bucket);
            if (w > maxLineWidth)
                maxLineWidth = w;
        }
        return maxLineWidth;
    }

    private static int MeasureDefaultFontLineWithDashes(string line, int bucket)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        if (line.IndexOfAny(SpecialDashes) < 0)
            return Raylib.MeasureText(line, bucket);

        int spacing = Math.Max(1, bucket / 10);
        int width = 0;
        int segStart = 0;
        bool drewAny = false;

        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch != '\u2014' && ch != '\u2013')
                continue;

            if (i > segStart)
            {
                string slice = line[segStart..i];
                width += Raylib.MeasureText(slice, bucket) + spacing;
                drewAny = true;
            }

            width += GetSpecialDashWidth(ch, bucket) + spacing;
            drewAny = true;
            segStart = i + 1;
        }

        if (segStart < line.Length)
        {
            string tail = line[segStart..];
            width += Raylib.MeasureText(tail, bucket) + spacing;
            drewAny = true;
        }

        return drewAny ? Math.Max(0, width - spacing) : 0;
    }

    private static int MeasureHeadlessFallbackWidth(string text, int effectivePx, FontPreset preset)
    {
        int spacing = Math.Max(1, effectivePx / 10);
        float advanceRatio = string.IsNullOrEmpty(preset.FilePath) ? 0.50f : 0.48f;
        float glyphAdvance = MathF.Max(1f, effectivePx * advanceRatio);
        int maxLineWidth = 0;

        string[] lines = text.Split('\n');
        for (int l = 0; l < lines.Length; l++)
        {
            string line = lines[l];
            if (line.Length == 0)
                continue;

            float width = 0f;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '\r')
                    continue;

                if (i > 0)
                    width += spacing;

                width += (ch == '\u2014' || ch == '\u2013')
                    ? GetSpecialDashWidth(ch, effectivePx)
                    : glyphAdvance;
            }

            int linePx = Math.Max(0, (int)MathF.Round(width));
            if (linePx > maxLineWidth)
                maxLineWidth = linePx;
        }

        return maxLineWidth;
    }

    private static int GetSpecialDashWidth(char ch, int bucket) =>
        ch == '\u2014'
            ? Math.Max(8, (int)MathF.Round(bucket * 0.9f))
            : Math.Max(6, (int)MathF.Round(bucket * 0.65f));

    private static int[] BuildCodepoints()
    {
        List<int> codepoints = new(210);
        for (int cp = 32; cp <= 126; cp++)
            codepoints.Add(cp);
        for (int cp = 160; cp <= 255; cp++)
            codepoints.Add(cp);

        codepoints.Add(0x2013); // en-dash
        codepoints.Add(0x2014); // em-dash
        codepoints.Add(0x2018); // left single quote
        codepoints.Add(0x2019); // right single quote
        codepoints.Add(0x201C); // left double quote
        codepoints.Add(0x201D); // right double quote
        codepoints.Add(0x2022); // bullet
        codepoints.Add(0x2026); // ellipsis
        return codepoints.ToArray();
    }
}
