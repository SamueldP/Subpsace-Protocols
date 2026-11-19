using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// In-memory RGBA rasterizer implementing Block 1 of
/// /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md.
/// Uploads to a cached GPU Texture2D with strict nearest-neighbor (TextureFilter.Point).
/// </summary>
public sealed class PixelBuffer
{
    public int Width { get; }
    public int Height { get; }

    // Null entry represents transparent pixel (consistent with Art Bible spec)
    private readonly string?[] _pixels;

    public PixelBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new string?[width * height];
    }

    public void Clear(string? hexColor = null)
    {
        Array.Fill(_pixels, hexColor);
    }

    public string? Get(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;
        return _pixels[y * Width + x];
    }

    public void Set(int x, int y, string? hexColor)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;
        _pixels[y * Width + x] = hexColor;
    }

    public void FillRect(int x, int y, int w, int h, string? hexColor)
    {
        for (int py = y; py < y + h; py++)
        {
            for (int px = x; px < x + w; px++)
            {
                Set(px, py, hexColor);
            }
        }
    }

    /// <summary>
    /// Classic integer Bresenham line algorithm for 1px seams, ribs, tubes, conduits, and insignia.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1, string hexColor)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            Set(x0, y0, hexColor);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Normalized-distance ellipse filler passing (px, py, dx, dy, dist).
    /// </summary>
    public void FillEllipse(int cx, int cy, int rx, int ry, Func<int, int, float, float, float, string?> colFn)
    {
        if (rx <= 0 || ry <= 0)
            return;

        for (int py = cy - ry; py <= cy + ry; py++)
        {
            for (int px = cx - rx; px <= cx + rx; px++)
            {
                float dx = (px - cx) / (float)rx;
                float dy = (py - cy) / (float)ry;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist <= 1.0f)
                {
                    string? col = colFn(px, py, dx, dy, dist);
                    if (col != null)
                        Set(px, py, col);
                }
            }
        }
    }

    /// <summary>
    /// Only writes pixels where maskFn(px, py) is true (e.g., already non-null inside head/hull boundaries).
    /// </summary>
    public void FillClippedEllipse(
        int cx,
        int cy,
        int rx,
        int ry,
        Func<int, int, bool> maskFn,
        Func<int, int, float, float, float, string?> colFn)
    {
        if (rx <= 0 || ry <= 0)
            return;

        for (int py = cy - ry; py <= cy + ry; py++)
        {
            for (int px = cx - rx; px <= cx + rx; px++)
            {
                if (!maskFn(px, py))
                    continue;

                float dx = (px - cx) / (float)rx;
                float dy = (py - cy) / (float)ry;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist <= 1.0f)
                {
                    string? col = colFn(px, py, dx, dy, dist);
                    if (col != null)
                        Set(px, py, col);
                }
            }
        }
    }

    /// <summary>
    /// Scanline polygon filler accepting shaderFn(px, py) for directional Bayer-dithered shading.
    /// </summary>
    public void FillPoly(ReadOnlySpan<(int X, int Y)> pts, Func<int, int, string?> shaderFn)
    {
        if (pts.Length < 3)
            return;

        int minY = Height - 1;
        int maxY = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i].Y < minY) minY = pts[i].Y;
            if (pts[i].Y > maxY) maxY = pts[i].Y;
        }

        minY = Math.Max(0, minY);
        maxY = Math.Min(Height - 1, maxY);

        List<int> nodeX = new(pts.Length);
        for (int py = minY; py <= maxY; py++)
        {
            nodeX.Clear();
            int j = pts.Length - 1;
            for (int i = 0; i < pts.Length; i++)
            {
                int iy = pts[i].Y;
                int jy = pts[j].Y;
                if ((iy < py && jy >= py) || (jy < py && iy >= py))
                {
                    int ix = pts[i].X;
                    int jx = pts[j].X;
                    int x = ix + (int)MathF.Round((float)(py - iy) / (jy - iy) * (jx - ix));
                    nodeX.Add(x);
                }
                j = i;
            }

            nodeX.Sort();
            for (int i = 0; i < nodeX.Count - 1; i += 2)
            {
                int startX = Math.Max(0, nodeX[i]);
                int endX = Math.Min(Width - 1, nodeX[i + 1]);
                for (int px = startX; px <= endX; px++)
                {
                    string? col = shaderFn(px, py);
                    if (col != null)
                        Set(px, py, col);
                }
            }
        }
    }

    /// <summary>
    /// 4-neighbor pass adding a crisp 1px dark outline around non-null pixels.
    /// </summary>
    public void ApplyOutline(string outlineHex = Palette.HexOutlineVoid)
    {
        bool[] toOutline = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (Get(x, y) != null)
                    continue;

                bool hasSolidNeighbor =
                    Get(x - 1, y) != null ||
                    Get(x + 1, y) != null ||
                    Get(x, y - 1) != null ||
                    Get(x, y + 1) != null;

                if (hasSolidNeighbor)
                    toOutline[y * Width + x] = true;
            }
        }

        for (int i = 0; i < _pixels.Length; i++)
        {
            if (toOutline[i])
                _pixels[i] = outlineHex;
        }
    }

    /// <summary>
    /// Optional 1px dim infrared Brown Dwarf rim highlight along the lower edge of ships.
    /// </summary>
    public void ApplyRimLight(string rimHex = Palette.HexBrownDwarfGlowRim)
    {
        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                string? cur = Get(x, y);
                string? below = Get(x, y + 1);
                if (cur != null && cur != Palette.HexOutlineVoid &&
                    (below == null || below == Palette.HexOutlineVoid))
                {
                    Set(x, y, rimHex);
                }
            }
        }
    }

    /// <summary>
    /// Uploads this RGBA PixelBuffer to a Raylib GPU Texture2D with TextureFilter.Point.
    /// Caller owns the returned Texture2D and MUST call Raylib.UnloadTexture when done.
    /// </summary>
    public unsafe Texture2D ToTexture2D()
    {
        Image img = Raylib.GenImageColor(Width, Height, Color.Blank);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                string? hex = Get(x, y);
                if (hex != null)
                {
                    Raylib.ImageDrawPixel(&img, x, y, Palette.FromHex(hex));
                }
            }
        }

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        return tex;
    }
}
