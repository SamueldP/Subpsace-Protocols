using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Centralized UI text drawing, width measurement, truncation, and pixel-width wrapping
/// routed through <see cref="FontManager"/> for crisp point-filtered rendering.
/// </summary>
public static class UiText
{
    public static void Draw(string text, int x, int y, int fontSize, Color color, bool denseRow = false)
    {
        FontManager.DrawText(text, x, y, fontSize, color, denseRow);
    }

    public static void Draw(string text, float x, float y, int fontSize, Color color, bool denseRow = false)
    {
        FontManager.DrawText(text, (int)MathF.Round(x), (int)MathF.Round(y), fontSize, color, denseRow);
    }

    public static int MeasureWidth(string text, int fontSize, bool denseRow = false)
    {
        return FontManager.MeasureWidth(text, fontSize, denseRow);
    }

    public static string TruncateToWidth(string text, int fontSize, int maxPixelWidth, bool denseRow = false)
    {
        if (string.IsNullOrEmpty(text) || maxPixelWidth <= 0)
            return string.Empty;

        if (MeasureWidth(text, fontSize, denseRow) <= maxPixelWidth)
            return text;

        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (MeasureWidth(text[..mid], fontSize, denseRow) <= maxPixelWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        return text[..lo];
    }

    public static List<string> Wrap(string text, int fontSize, int maxPixelWidth, int maxLines = int.MaxValue, bool denseRow = false)
    {
        List<string> lines = [];
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0 || maxPixelWidth <= 0)
            return lines;

        string normalized = text.Replace("\r\n", "\n");
        string[] paragraphs = normalized.Split('\n');

        for (int p = 0; p < paragraphs.Length; p++)
        {
            string[] words = paragraphs[p].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string candidate = current.Length == 0 ? word : $"{current} {word}";

                if (MeasureWidth(candidate, fontSize, denseRow) <= maxPixelWidth)
                {
                    current = candidate;
                }
                else if (current.Length == 0)
                {
                    lines.Add(TruncateToWidth(word, fontSize, maxPixelWidth, denseRow));
                    if (lines.Count >= maxLines)
                        return lines;
                }
                else
                {
                    lines.Add(TruncateToWidth(current, fontSize, maxPixelWidth, denseRow));
                    if (lines.Count >= maxLines)
                        return lines;

                    current = MeasureWidth(word, fontSize, denseRow) <= maxPixelWidth
                        ? word
                        : TruncateToWidth(word, fontSize, maxPixelWidth, denseRow);
                }
            }

            if (current.Length > 0 && lines.Count < maxLines)
            {
                lines.Add(TruncateToWidth(current, fontSize, maxPixelWidth, denseRow));
                if (lines.Count >= maxLines)
                    return lines;
            }
        }

        return lines;
    }
}
