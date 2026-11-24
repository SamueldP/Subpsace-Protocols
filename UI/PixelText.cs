using Raylib_cs;

namespace SubspaceProtocols.UI;

/// <summary>
/// Crisp pixel-aligned text drawing &amp; measurement helper that renders Unicode em-dash ('\u2014')
/// and en-dash ('\u2013') as integer-aligned pixel bars inside Raylib's Latin-1 bitmap default font.
/// Zero anti-aliasing or font smoothing.
/// </summary>
public static class PixelText
{
    private static readonly char[] SpecialDashes = ['\u2014', '\u2013'];

    public static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (text.IndexOfAny(SpecialDashes) < 0)
        {
            Raylib.DrawText(text, x, y, fontSize, color);
            return;
        }

        int spacing = Math.Max(1, fontSize / 10);
        int barH = Math.Max(1, fontSize / 10);
        int curX = x;
        int segStart = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch != '\u2014' && ch != '\u2013')
                continue;

            if (i > segStart)
            {
                string slice = text[segStart..i];
                Raylib.DrawText(slice, curX, y, fontSize, color);
                curX += Raylib.MeasureText(slice, fontSize) + spacing;
            }

            int dashW = ch == '\u2014'
                ? Math.Max(8, (int)MathF.Round(fontSize * 0.9f))
                : Math.Max(6, (int)MathF.Round(fontSize * 0.65f));
            Raylib.DrawRectangle(curX, y + fontSize / 2, dashW, barH, color);
            curX += dashW + spacing;
            segStart = i + 1;
        }

        if (segStart < text.Length)
        {
            string tail = text[segStart..];
            Raylib.DrawText(tail, curX, y, fontSize, color);
        }
    }

    public static int MeasureText(string text, int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        if (text.IndexOfAny(SpecialDashes) < 0)
            return Raylib.MeasureText(text, fontSize);

        int spacing = Math.Max(1, fontSize / 10);
        int width = 0;
        int segStart = 0;
        bool drewAny = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch != '\u2014' && ch != '\u2013')
                continue;

            if (i > segStart)
            {
                string slice = text[segStart..i];
                width += Raylib.MeasureText(slice, fontSize) + spacing;
                drewAny = true;
            }

            int dashW = ch == '\u2014'
                ? Math.Max(8, (int)MathF.Round(fontSize * 0.9f))
                : Math.Max(6, (int)MathF.Round(fontSize * 0.65f));
            width += dashW + spacing;
            drewAny = true;
            segStart = i + 1;
        }

        if (segStart < text.Length)
        {
            string tail = text[segStart..];
            width += Raylib.MeasureText(tail, fontSize) + spacing;
            drewAny = true;
        }

        return drewAny ? Math.Max(0, width - spacing) : 0;
    }
}
