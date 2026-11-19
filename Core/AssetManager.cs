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

    public static string GetImagesRootDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "assets", "images");

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

        string displayLabel = string.IsNullOrWhiteSpace(label) ? relativeImagePath : label;
        if (height >= 14 && width >= 24)
        {
            Raylib.DrawText(displayLabel, x + 5, y + 4, 10, borderColor);
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
