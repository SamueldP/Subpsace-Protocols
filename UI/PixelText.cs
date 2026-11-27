using Raylib_cs;

namespace SubspaceProtocols.UI;

/// <summary>
/// Compatibility facade routing pixel-aligned text drawing &amp; measurement through <see cref="UiText"/>.
/// </summary>
public static class PixelText
{
    public static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        UiText.Draw(text, x, y, fontSize, color);
    }

    public static int MeasureText(string text, int fontSize)
    {
        return UiText.MeasureWidth(text, fontSize);
    }
}
