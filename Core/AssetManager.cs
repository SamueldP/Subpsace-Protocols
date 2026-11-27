using System.Numerics;
using Raylib_cs;

namespace SubspaceProtocols.Core;

/// <summary>
/// Loads optional static PNGs from assets/images/ (relative to AppContext.BaseDirectory)
/// with TextureFilter.Point, falling back to flat palette-colored placeholder rectangles
/// with labeled bounding boxes whenever an image is missing.
/// Manages texture cache unloading to prevent VRAM leaks.
/// </summary>
public static class AssetManager
{
    private static readonly Dictionary<string, Texture2D> LoadedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingPaths = new(StringComparer.OrdinalIgnoreCase);

    private const string AppLogoCacheKey = "__app_logo_64__";

    public static int ActiveTextureCount => LoadedTextures.Count;

    public static string GetImagesRootDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "assets", "images");

    /// <summary>
    /// Enumerates candidate app logo / icon paths across assets/images/app_icon.(png|jpg)
    /// and optional assets/images/app_icon/* or assets/image/app_icon/* folders.
    /// PNG candidates come first because Raylib's built-in rtextures supports PNG natively without SUPPORT_FILEFORMAT_JPG.
    /// </summary>
    private static IEnumerable<string> EnumerateAppIconCandidates()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] directCandidates =
        [
            Path.Combine(baseDir, "assets", "images", "app_icon.png"),
            Path.Combine(baseDir, "assets", "image", "app_icon.png"),
            Path.Combine(baseDir, "assets", "images", "app_icon.jpg"),
            Path.Combine(baseDir, "assets", "images", "app_icon.jpeg"),
            Path.Combine(baseDir, "assets", "image", "app_icon.jpg")
        ];

        for (int i = 0; i < directCandidates.Length; i++)
        {
            if (File.Exists(directCandidates[i]))
                yield return directCandidates[i];
        }

        string[] folderCandidates =
        [
            Path.Combine(baseDir, "assets", "images", "app_icon"),
            Path.Combine(baseDir, "assets", "image", "app_icon")
        ];

        string[] extPriority = [".png", ".jpg", ".jpeg"];
        for (int e = 0; e < extPriority.Length; e++)
        {
            string wantedExt = extPriority[e];
            for (int f = 0; f < folderCandidates.Length; f++)
            {
                if (!Directory.Exists(folderCandidates[f]))
                    continue;

                foreach (string file in Directory.EnumerateFiles(folderCandidates[f], "*.*", SearchOption.TopDirectoryOnly))
                {
                    if (Path.GetExtension(file).Equals(wantedExt, StringComparison.OrdinalIgnoreCase))
                        yield return file;
                }
            }
        }
    }

    /// <summary>
    /// Resolves the preferred app logo / icon image path.
    /// </summary>
    public static string? ResolveAppIconPath() =>
        EnumerateAppIconCandidates().FirstOrDefault();

    private static bool TryLoadCrispAppLogoImage(int gridSize, out Image logoImage)
    {
        logoImage = default;
        Image src = default;
        bool loadedAny = false;

        foreach (string iconPath in EnumerateAppIconCandidates())
        {
            Image candidate = Raylib.LoadImage(iconPath);
            if (candidate.Width > 0 && candidate.Height > 0)
            {
                src = candidate;
                loadedAny = true;
                break;
            }
        }

        if (!loadedAny)
            return false;

        Raylib.ImageFormat(ref src, PixelFormat.UncompressedR8G8B8A8);
        int size = Math.Max(16, gridSize);
        Image sampled = Raylib.GenImageColor(size, size, Color.Black);

        for (int y = 0; y < size; y++)
        {
            int sy = Math.Clamp((y * src.Height + src.Height / 2) / size, 0, src.Height - 1);
            for (int x = 0; x < size; x++)
            {
                int sx = Math.Clamp((x * src.Width + src.Width / 2) / size, 0, src.Width - 1);
                Color px = Raylib.GetImageColor(src, sx, sy);
                Raylib.ImageDrawPixel(ref sampled, x, y, new Color(px.R, px.G, px.B, (byte)255));
            }
        }

        Raylib.UnloadImage(src);
        logoImage = sampled;
        return true;
    }

    /// <summary>
    /// Sets the desktop OS window / taskbar icon from app_icon.jpg (converted to RGBA8) and unloads temporary CPU memory.
    /// </summary>
    public static bool TryApplyWindowIcon()
    {
        if (!Raylib.IsWindowReady() || !TryLoadCrispAppLogoImage(64, out Image iconImg))
            return false;

        Raylib.SetWindowIcon(iconImg);
        Raylib.UnloadImage(iconImg);
        return true;
    }

    /// <summary>
    /// Returns a cached crisp 64x64 point-filtered GPU Texture2D sampled from app_icon.jpg
    /// (managed by AssetManager.UnloadAll so zero VRAM leaks occur at exit).
    /// </summary>
    public static bool TryGetAppLogoTexture(out Texture2D texture)
    {
        texture = default;
        if (LoadedTextures.TryGetValue(AppLogoCacheKey, out texture))
            return texture.Id != 0;

        if (MissingPaths.Contains(AppLogoCacheKey))
            return false;

        if (!TryLoadCrispAppLogoImage(64, out Image logoImg))
        {
            MissingPaths.Add(AppLogoCacheKey);
            return false;
        }

        Texture2D loaded = Raylib.LoadTextureFromImage(logoImg);
        Raylib.UnloadImage(logoImg);
        if (loaded.Id == 0)
        {
            MissingPaths.Add(AppLogoCacheKey);
            return false;
        }

        Raylib.SetTextureFilter(loaded, TextureFilter.Point);
        LoadedTextures[AppLogoCacheKey] = loaded;
        texture = loaded;
        return true;
    }

    public static bool TryGetTexture(string relativeImagePath, out Texture2D texture)
    {
        texture = default;
        if (string.IsNullOrWhiteSpace(relativeImagePath))
            return false;

        string normalizedKey = NormalizeRelativePath(relativeImagePath);

        if (LoadedTextures.TryGetValue(normalizedKey, out texture))
            return texture.Id != 0;

        if (MissingPaths.Contains(normalizedKey))
            return false;

        string fullPath = Path.Combine(GetImagesRootDirectory(), normalizedKey);
        if (!File.Exists(fullPath))
        {
            MissingPaths.Add(normalizedKey);
            return false;
        }

        Texture2D loaded = Raylib.LoadTexture(fullPath);
        if (loaded.Id == 0)
        {
            MissingPaths.Add(normalizedKey);
            return false;
        }

        Raylib.SetTextureFilter(loaded, TextureFilter.Point);
        LoadedTextures[normalizedKey] = loaded;
        texture = loaded;
        return true;
    }

    /// <summary>
    /// Draws an optional static PNG from assets/images/ if present, or falls back to
    /// a flat palette-colored placeholder rectangle with a labeled 1px bounding box.
    /// </summary>
    public static void DrawImageOrPlaceholder(
        string relativeImagePath,
        int x,
        int y,
        int width,
        int height,
        Color fillColor,
        Color borderColor,
        string? label = null)
    {
        if (TryGetTexture(relativeImagePath, out Texture2D tex))
        {
            Rectangle src = new(0, 0, tex.Width, tex.Height);
            Rectangle dst = new(x, y, width, height);
            Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, Color.White);
            return;
        }

        // Flat palette-colored placeholder rectangle with 1px bounding box
        Raylib.DrawRectangle(x, y, width, height, fillColor);
        Raylib.DrawRectangleLines(x, y, width, height, borderColor);

        // Inner corner ticks for technical DOS VGA aesthetic
        int tick = Math.Min(4, Math.Min(width, height) / 4);
        if (tick >= 2)
        {
            Raylib.DrawLine(x + 2, y + 2, x + 2 + tick, y + 2, borderColor);
            Raylib.DrawLine(x + 2, y + 2, x + 2, y + 2 + tick, borderColor);

            Raylib.DrawLine(x + width - 3 - tick, y + 2, x + width - 3, y + 2, borderColor);
            Raylib.DrawLine(x + width - 3, y + 2, x + width - 3, y + 2 + tick, borderColor);

            Raylib.DrawLine(x + 2, y + height - 3, x + 2 + tick, y + height - 3, borderColor);
            Raylib.DrawLine(x + 2, y + height - 3 - tick, x + 2, y + height - 3, borderColor);

            Raylib.DrawLine(x + width - 3 - tick, y + height - 3, x + width - 3, y + height - 3, borderColor);
            Raylib.DrawLine(x + width - 3, y + height - 3 - tick, x + width - 3, y + height - 3, borderColor);
        }

        string? displayLabel = label ?? relativeImagePath;
        if (!string.IsNullOrEmpty(displayLabel) && height >= 14 && width >= 24)
        {
            FontManager.DrawText(displayLabel, x + 5, y + 4, 10, borderColor);
        }
    }

    public static void UnloadAsset(string relativeImagePath)
    {
        string normalizedKey = NormalizeRelativePath(relativeImagePath);
        if (LoadedTextures.Remove(normalizedKey, out Texture2D tex) && tex.Id != 0)
        {
            Raylib.UnloadTexture(tex);
        }
    }

    public static void ClearMissingCache()
    {
        MissingPaths.Clear();
    }

    public static void UnloadAll()
    {
        foreach (Texture2D tex in LoadedTextures.Values)
        {
            if (tex.Id != 0)
            {
                Raylib.UnloadTexture(tex);
            }
        }
        LoadedTextures.Clear();
        MissingPaths.Clear();
    }

    private static string NormalizeRelativePath(string path)
    {
        string cleaned = path.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (!Path.HasExtension(cleaned))
        {
            cleaned += ".png";
        }
        return cleaned;
    }
}
