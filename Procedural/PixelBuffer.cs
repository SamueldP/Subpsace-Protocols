using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.Procedural;

/// <summary>
/// In-memory nullable RGBA pixel rasterizer implementing Block 1 of
/// /design/08_Physical_Look/Procedural_Art_Bible_and_Asset_Spec.md.
/// Uploads to a cached GPU Texture2D with strict nearest-neighbor (TextureFilter.Point)
/// and tracks active VRAM textures to enforce zero memory leaks.
/// </summary>
public sealed class PixelBuffer
{
    private static readonly HashSet<uint> ActiveProceduralTextureIds = [];

    public static int ActiveProceduralTextureCount => ActiveProceduralTextureIds.Count;
    public static int TotalProceduralTexturesCreated { get; private set; }
    public static int TotalProceduralTexturesUnloaded { get; private set; }
    public static int TotalActiveVramTextures => ActiveProceduralTextureIds.Count + AssetManager.ActiveTextureCount;

    public int Width { get; }
    public int Height { get; }

    // Nullable RGBA pixels and synchronized hex strings (null represents transparent pixel)
    private readonly Color?[] _rgbaPixels;
    private readonly string?[] _hexPixels;

    public PixelBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _rgbaPixels = new Color?[width * height];
        _hexPixels = new string?[width * height];
    }

    public PixelBuffer Clone()
    {
        PixelBuffer copy = new(Width, Height);
        Array.Copy(_rgbaPixels, copy._rgbaPixels, _rgbaPixels.Length);
        Array.Copy(_hexPixels, copy._hexPixels, _hexPixels.Length);
        return copy;
    }

    /// <summary>
    /// Crisp nearest-neighbor 2x integer upscale copying both RGBA and hex pixels directly.
    /// </summary>
    public PixelBuffer Upscale2x()
    {
        int dstW = Width * 2;
        int dstH = Height * 2;
        PixelBuffer dst = new(dstW, dstH);

        for (int y = 0; y < Height; y++)
        {
            int srcRow = y * Width;
            int dstRow0 = (y * 2) * dstW;
            int dstRow1 = dstRow0 + dstW;
            for (int x = 0; x < Width; x++)
            {
                int srcIdx = srcRow + x;
                Color? rgba = _rgbaPixels[srcIdx];
                if (!rgba.HasValue)
                    continue;

                string? hex = _hexPixels[srcIdx];
                int dx = x * 2;

                dst._rgbaPixels[dstRow0 + dx] = rgba;
                dst._rgbaPixels[dstRow0 + dx + 1] = rgba;
                dst._rgbaPixels[dstRow1 + dx] = rgba;
                dst._rgbaPixels[dstRow1 + dx + 1] = rgba;

                dst._hexPixels[dstRow0 + dx] = hex;
                dst._hexPixels[dstRow0 + dx + 1] = hex;
                dst._hexPixels[dstRow1 + dx] = hex;
                dst._hexPixels[dstRow1 + dx + 1] = hex;
            }
        }

        return dst;
    }

    /// <summary>
    /// Returns a horizontally mirrored copy of this buffer (preserving RGBA and hex pixels).
    /// </summary>
    public PixelBuffer MirrorHorizontal()
    {
        PixelBuffer dst = new(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            int row = y * Width;
            for (int x = 0; x < Width; x++)
            {
                int srcIdx = row + x;
                int dstIdx = row + (Width - 1 - x);
                dst._rgbaPixels[dstIdx] = _rgbaPixels[srcIdx];
                dst._hexPixels[dstIdx] = _hexPixels[srcIdx];
            }
        }
        return dst;
    }

    public void Clear(string? hexColor = null)
    {
        Color? rgba = hexColor != null ? Palette.FromHex(hexColor) : null;
        Array.Fill(_hexPixels, hexColor);
        Array.Fill(_rgbaPixels, rgba);
    }

    public void Clear(Color? rgbaColor)
    {
        string? hex = rgbaColor.HasValue ? Palette.ToHex(rgbaColor.Value) : null;
        Array.Fill(_rgbaPixels, rgbaColor);
        Array.Fill(_hexPixels, hex);
    }

    public string? Get(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;
        return _hexPixels[y * Width + x];
    }

    public Color? GetRgba(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return null;
        return _rgbaPixels[y * Width + x];
    }

    public bool HasPixel(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height && _rgbaPixels[y * Width + x].HasValue;

    /// <summary>
    /// Captures a boolean mask of all currently non-null (and non-outline) pixels in the buffer,
    /// used by FillClippedEllipse / FillClippedPoly for Klaasari spots, Tolvak molt patches, and Prunjun flames.
    /// </summary>
    public bool[] CaptureNonNullMask(bool excludeOutline = true)
    {
        bool[] mask = new bool[Width * Height];
        for (int i = 0; i < _hexPixels.Length; i++)
        {
            string? h = _hexPixels[i];
            if (h != null && (!excludeOutline || !h.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase)))
            {
                mask[i] = true;
            }
        }
        return mask;
    }

    public bool IsMasked(bool[] mask, int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height && mask[y * Width + x];

    public void Set(int x, int y, string? hexColor)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        int idx = y * Width + x;
        _hexPixels[idx] = hexColor;
        _rgbaPixels[idx] = hexColor != null ? Palette.FromHex(hexColor) : null;
    }

    public void Set(int x, int y, Color? rgbaColor)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return;

        int idx = y * Width + x;
        if (!rgbaColor.HasValue || rgbaColor.Value.A == 0)
        {
            _rgbaPixels[idx] = null;
            _hexPixels[idx] = null;
        }
        else
        {
            _rgbaPixels[idx] = rgbaColor.Value;
            _hexPixels[idx] = Palette.ToHex(rgbaColor.Value);
        }
    }

    public void FillRect(int x, int y, int w, int h, string? hexColor)
    {
        if (w <= 0 || h <= 0)
            return;

        int startX = Math.Max(0, x);
        int endX = Math.Min(Width, x + w);
        int startY = Math.Max(0, y);
        int endY = Math.Min(Height, y + h);

        for (int py = startY; py < endY; py++)
        {
            for (int px = startX; px < endX; px++)
            {
                Set(px, py, hexColor);
            }
        }
    }

    public void FillRect(int x, int y, int w, int h, Func<int, int, string?> shaderFn)
    {
        if (w <= 0 || h <= 0)
            return;

        int startX = Math.Max(0, x);
        int endX = Math.Min(Width, x + w);
        int startY = Math.Max(0, y);
        int endY = Math.Min(Height, y + h);

        for (int py = startY; py < endY; py++)
        {
            for (int px = startX; px < endX; px++)
            {
                string? col = shaderFn(px, py);
                if (col != null)
                    Set(px, py, col);
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
    /// Returns the exact integer points along a Bresenham line (used for animated pulse pips on SPS conduits).
    /// </summary>
    public static List<(int X, int Y)> TraceBresenhamLine(int x0, int y0, int x1, int y1)
    {
        List<(int X, int Y)> pts = [];
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            pts.Add((x0, y0));
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
        return pts;
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

    public void FillEllipse(int cx, int cy, int rx, int ry, string hexColor) =>
        FillEllipse(cx, cy, rx, ry, (_, _, _, _, _) => hexColor);

    /// <summary>
    /// Writes ellipse pixels ONLY where existing mask pixels in the buffer are already non-null.
    /// Required for Klaasari spots, Tolvak molt patches, and Prunjun flames.
    /// </summary>
    public void FillClippedEllipse(
        int cx,
        int cy,
        int rx,
        int ry,
        Func<int, int, float, float, float, string?> colFn)
    {
        FillClippedEllipse(
            cx, cy, rx, ry,
            (px, py) =>
            {
                string? existing = Get(px, py);
                return existing != null && !existing.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase);
            },
            colFn);
    }

    /// <summary>
    /// Writes ellipse pixels ONLY where maskFn(px, py) is true (e.g., inside head/hull mask boundaries).
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

    public void FillPoly(ReadOnlySpan<(int X, int Y)> pts, string hexColor) =>
        FillPoly(pts, (_, _) => hexColor);

    /// <summary>
    /// Scanline polygon filler writing ONLY where maskFn(px, py) is true.
    /// </summary>
    public void FillClippedPoly(
        ReadOnlySpan<(int X, int Y)> pts,
        Func<int, int, bool> maskFn,
        Func<int, int, string?> shaderFn)
    {
        FillPoly(pts, (px, py) => maskFn(px, py) ? shaderFn(px, py) : null);
    }

    /// <summary>
    /// Scanline polygon filler writing ONLY where existing buffer pixels are non-null.
    /// </summary>
    public void FillClippedPoly(ReadOnlySpan<(int X, int Y)> pts, Func<int, int, string?> shaderFn)
    {
        FillClippedPoly(
            pts,
            (px, py) =>
            {
                string? existing = Get(px, py);
                return existing != null && !existing.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase);
            },
            shaderFn);
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

        Color outlineCol = Palette.FromHex(outlineHex);
        for (int i = 0; i < _hexPixels.Length; i++)
        {
            if (toOutline[i])
            {
                _hexPixels[i] = outlineHex;
                _rgbaPixels[i] = outlineCol;
            }
        }
    }

    /// <summary>
    /// Applies a 1px dim infrared Brown Dwarf rim highlight along the specified silhouette edge
    /// ("bottom", "top", "left", or "right").
    /// </summary>
    public void ApplyRimLight(string rimHex = Palette.HexBrownDwarfGlowRim, string edge = "bottom")
    {
        int dx = 0;
        int dy = 1;
        if (edge.Equals("top", StringComparison.OrdinalIgnoreCase))
        {
            dx = 0;
            dy = -1;
        }
        else if (edge.Equals("left", StringComparison.OrdinalIgnoreCase))
        {
            dx = -1;
            dy = 0;
        }
        else if (edge.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            dx = 1;
            dy = 0;
        }

        bool[] toRim = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                string? cur = Get(x, y);
                if (cur == null || cur.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? neighbor = Get(x + dx, y + dy);
                if (neighbor == null || neighbor.Equals(Palette.HexOutlineVoid, StringComparison.OrdinalIgnoreCase))
                {
                    toRim[y * Width + x] = true;
                }
            }
        }

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (toRim[y * Width + x])
                {
                    Set(x, y, rimHex);
                }
            }
        }
    }

    /// <summary>
    /// Composites all non-null pixels from source buffer onto this buffer.
    /// </summary>
    public void BlitFrom(PixelBuffer src, int offsetX = 0, int offsetY = 0)
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                string? hex = src.Get(x, y);
                if (hex != null)
                {
                    Set(x + offsetX, y + offsetY, hex);
                }
            }
        }
    }

    /// <summary>
    /// Crisp 3x5 pixel bitmap font rasterizer for in-buffer labels such as "NO VISUAL SIGNAL".
    /// </summary>
    public void DrawBitmapText(int startX, int startY, string text, string hexColor)
    {
        if (string.IsNullOrEmpty(text))
            return;

        int cursorX = startX;
        foreach (char rawChar in text)
        {
            char c = char.ToUpperInvariant(rawChar);
            if (c == ' ')
            {
                cursorX += 3;
                continue;
            }

            if (TinyFont3x5.TryGetValue(c, out ushort glyph))
            {
                for (int gy = 0; gy < 5; gy++)
                {
                    for (int gx = 0; gx < 3; gx++)
                    {
                        int bitIndex = (4 - gy) * 3 + (2 - gx);
                        if (((glyph >> bitIndex) & 1) != 0)
                        {
                            Set(cursorX + gx, startY + gy, hexColor);
                        }
                    }
                }
            }
            cursorX += 4;
        }
    }

    private static readonly Dictionary<char, ushort> TinyFont3x5 = new()
    {
        ['A'] = 0b010_101_111_101_101,
        ['B'] = 0b110_101_110_101_110,
        ['C'] = 0b011_100_100_100_011,
        ['D'] = 0b110_101_101_101_110,
        ['E'] = 0b111_100_110_100_111,
        ['F'] = 0b111_100_110_100_100,
        ['G'] = 0b011_100_101_101_011,
        ['H'] = 0b101_101_111_101_101,
        ['I'] = 0b111_010_010_010_111,
        ['J'] = 0b001_001_001_101_010,
        ['K'] = 0b101_101_110_101_101,
        ['L'] = 0b100_100_100_100_111,
        ['M'] = 0b101_111_111_101_101,
        ['N'] = 0b111_101_101_101_101,
        ['O'] = 0b010_101_101_101_010,
        ['P'] = 0b110_101_110_100_100,
        ['Q'] = 0b010_101_101_111_011,
        ['R'] = 0b110_101_110_101_101,
        ['S'] = 0b011_100_010_001_110,
        ['T'] = 0b111_010_010_010_010,
        ['U'] = 0b101_101_101_101_111,
        ['V'] = 0b101_101_101_101_010,
        ['W'] = 0b101_101_111_111_101,
        ['X'] = 0b101_101_010_101_101,
        ['Y'] = 0b101_101_010_010_010,
        ['Z'] = 0b111_001_010_100_111,
        ['0'] = 0b111_101_101_101_111,
        ['1'] = 0b010_110_010_010_111,
        ['2'] = 0b111_001_111_100_111,
        ['3'] = 0b111_001_011_001_111,
        ['4'] = 0b101_101_111_001_001,
        ['5'] = 0b111_100_111_001_111,
        ['6'] = 0b111_100_111_101_111,
        ['7'] = 0b111_001_001_010_010,
        ['8'] = 0b111_101_111_101_111,
        ['9'] = 0b111_101_111_001_111,
        ['-'] = 0b000_000_111_000_000,
        [':'] = 0b000_010_000_010_000,
        ['/'] = 0b001_001_010_100_100
    };

    /// <summary>
    /// Converts this PixelBuffer into a Raylib Image -> Texture2D with TextureFilter.Point,
    /// immediately unloads the temporary CPU Image, and registers the GPU texture in the active VRAM tracker.
    /// Caller MUST dispose via PixelBuffer.UnloadTexture(ref tex).
    /// </summary>
    public Texture2D ToTexture2D()
    {
        Image img = Raylib.GenImageColor(Width, Height, Color.Blank);
        for (int y = 0; y < Height; y++)
        {
            int rowOffset = y * Width;
            for (int x = 0; x < Width; x++)
            {
                Color? col = _rgbaPixels[rowOffset + x];
                if (col.HasValue)
                {
                    Raylib.ImageDrawPixel(ref img, x, y, col.Value);
                }
            }
        }

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        if (tex.Id != 0)
        {
            Raylib.SetTextureFilter(tex, TextureFilter.Point);
            ActiveProceduralTextureIds.Add(tex.Id);
            TotalProceduralTexturesCreated++;
        }
        return tex;
    }

    /// <summary>
    /// Explicitly unloads a procedural GPU Texture2D and removes it from the active VRAM tracker.
    /// </summary>
    public static void UnloadTexture(ref Texture2D tex)
    {
        if (tex.Id != 0)
        {
            if (ActiveProceduralTextureIds.Remove(tex.Id))
            {
                TotalProceduralTexturesUnloaded++;
            }
            Raylib.UnloadTexture(tex);
            tex = default;
        }
    }
}
