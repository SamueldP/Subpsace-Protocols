using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 5-card new-game intro sequence framed in 16-bit crimson/charcoal borders with AssetManager PNG hooks
/// and Space/Click advance fading cleanly into Day 1 Morning Quarters.
/// </summary>
public sealed class IntroScreen : IScreen
{
    private static readonly string[] Cards =
    [
        "Three years after the Concord War, Void Gate 3 remains the sole operational corridor through the rifts.",
        "Under the terms of the Bulwark Pact, joint control is split between Brengop logistics and Kordani military command.",
        "When the Far-Line Transit Corp sold the station, the stranded crew were transferred as part of the inventory under updated contracts.",
        "Due to a sudden vacancy at Booth 04, a replacement has been promoted to customs gate operations. Authority level: Provisional.",
        "Congratulations"
    ];

    public static IReadOnlyList<string> VerbatimCards => Cards;

    private const float OutroFadeDurationSeconds = 0.42f;
    private const int FrameX = 180;
    private const int FrameY = 96;
    private const int FrameW = 920;
    private const int FrameH = 528;

    private readonly ScreenManager _screenManager;
    private readonly DevDebugState? _devDebug;

    private int _cardIndex;
    private float _cardFadeIn;
    private float _outroFade;
    private bool _fadingToQuarters;

    internal int CurrentCardIndex => _cardIndex;
    internal bool IsFadingToQuarters => _fadingToQuarters;

    public IntroScreen(ScreenManager screenManager, DevDebugState? devDebug = null)
    {
        _screenManager = screenManager;
        _devDebug = devDebug;
    }

    public void OnEnter()
    {
        _cardIndex = 0;
        _cardFadeIn = 0f;
        _outroFade = 0f;
        _fadingToQuarters = false;

        if (_devDebug?.SkipIntroSequence == true)
        {
            _screenManager.SwitchTo(ScreenId.Quarters);
        }
    }

    public void OnExit() { }

    internal void SmokeAdvanceCard()
    {
        if (_fadingToQuarters)
            return;

        if (_cardIndex < Cards.Length - 1)
        {
            _cardIndex++;
            _cardFadeIn = 1f;
        }
        else
        {
            _fadingToQuarters = true;
            _outroFade = 0f;
        }
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (_devDebug?.SkipIntroSequence == true)
        {
            _screenManager.SwitchTo(ScreenId.Quarters);
            return;
        }

        if (_fadingToQuarters)
        {
            _outroFade = Math.Min(1f, _outroFade + deltaTime / OutroFadeDurationSeconds);
            if (_outroFade >= 1f)
            {
                _screenManager.SwitchTo(ScreenId.Quarters);
            }
            return;
        }

        _cardFadeIn = Math.Min(1f, _cardFadeIn + deltaTime * 3.5f);

        bool advancePressed = mouse.LeftPressed ||
                              Raylib.IsKeyPressed(KeyboardKey.Space) ||
                              Raylib.IsKeyPressed(KeyboardKey.Enter);
        if (!advancePressed)
            return;

        AudioHooks.Play("terminal_key_confirm");
        if (_cardIndex < Cards.Length - 1)
        {
            _cardIndex++;
            _cardFadeIn = 0f;
        }
        else
        {
            _fadingToQuarters = true;
            _outroFade = 0f;
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        _ = mouse;

        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, Palette.OutlineVoid);

        // Subtle atmospheric horizon wash matching the gate corridor
        Raylib.DrawRectangle(0, 32, Constants.VirtualWidth, 120, Palette.BrownDwarfGlowDeep);
        Raylib.DrawLine(0, 152, Constants.VirtualWidth, 152, Palette.AlertCrimson);

        // Outer 16-bit crimson & charcoal card frame
        Raylib.DrawRectangle(FrameX, FrameY, FrameW, FrameH, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(FrameX, FrameY, FrameW, FrameH, Palette.AlertCrimson);
        Raylib.DrawRectangleLines(FrameX + 4, FrameY + 4, FrameW - 8, FrameH - 8, Palette.BrownDwarfGlowRim);
        Raylib.DrawRectangleLines(FrameX + 8, FrameY + 8, FrameW - 16, FrameH - 16, Palette.OutlineVoid);

        // Corner steel rivets
        DrawFrameRivet(FrameX + 12, FrameY + 12);
        DrawFrameRivet(FrameX + FrameW - 16, FrameY + 12);
        DrawFrameRivet(FrameX + 12, FrameY + FrameH - 16);
        DrawFrameRivet(FrameX + FrameW - 16, FrameY + FrameH - 16);

        // Visual art panel (AssetManager PNG hook with stylized pixel-art border fallback)
        const int artX = FrameX + 32;
        const int artY = FrameY + 26;
        const int artW = FrameW - 64;
        const int artH = 296;
        string imageRelPath = $"intro/card_{_cardIndex + 1:D2}.png";
        AssetManager.DrawImageOrPlaceholder(
            imageRelPath,
            artX, artY, artW, artH,
            Palette.DeepSpaceVoid,
            Palette.AlertCrimson,
            $"ARCHIVE FEED // {_cardIndex + 1:D2} OF {Cards.Length:D2}");

        // Subtle CRT horizontal scanlines across visual frame
        for (int sy = artY + 2; sy < artY + artH - 2; sy += 4)
        {
            Raylib.DrawLine(artX + 2, sy, artX + artW - 3, sy, new Color(8, 10, 14, 72));
        }

        // Narrative text plaque
        const int boxX = FrameX + 32;
        const int boxY = FrameY + 338;
        const int boxW = FrameW - 64;
        const int boxH = 140;
        Raylib.DrawRectangle(boxX, boxY, boxW, boxH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, Palette.AlertCrimson);
        Raylib.DrawRectangleLines(boxX + 2, boxY + 2, boxW - 4, boxH - 4, Palette.WeatheredGunmetalEdge);

        string text = Cards[Math.Clamp(_cardIndex, 0, Cards.Length - 1)];
        bool isFinalCard = _cardIndex == Cards.Length - 1;
        int fontSize = isFinalCard ? 24 : 18;
        List<string> wrapped = WrapCenteredLines(text, fontSize, boxW - 48);
        int lineStep = isFinalCard ? 28 : 24;
        int startY = boxY + Math.Max(14, (boxH - wrapped.Count * lineStep) / 2);

        Color textCol = isFinalCard ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;
        for (int i = 0; i < wrapped.Count; i++)
        {
            string line = wrapped[i];
            int lw = UiText.MeasureWidth(line, fontSize);
            int lx = boxX + Math.Max(16, (boxW - lw) / 2);
            UiText.Draw(line, lx, startY + i * lineStep, fontSize, textCol);
        }

        // Progress pips (1..5) + prompt footer
        int pipTotalW = Cards.Length * 18 + (Cards.Length - 1) * 10;
        int pipStartX = FrameX + (FrameW - pipTotalW) / 2;
        int pipY = FrameY + FrameH - 34;
        for (int i = 0; i < Cards.Length; i++)
        {
            int px = pipStartX + i * 28;
            Color pipCol = i == _cardIndex
                ? Palette.CrtAmberPrimary
                : (i < _cardIndex ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangle(px, pipY, 18, 6, pipCol);
            Raylib.DrawRectangleLines(px, pipY, 18, 6, Palette.OutlineVoid);
        }

        string prompt = isFinalCard ? "[SPACE / CLICK TO ENTER QUARTERS]" : "[SPACE / CLICK TO CONTINUE]";
        int pw = UiText.MeasureWidth(prompt, 10);
        UiText.Draw(prompt, FrameX + FrameW - pw - 28, pipY - 2, 10, Palette.CrtAmberDimmed);

        // Card fade-in / outro fade-to-black into Day 1 Morning Quarters
        float fadeAlpha = _fadingToQuarters
            ? _outroFade
            : (1f - Math.Clamp(_cardFadeIn, 0f, 1f)) * 0.72f;
        if (fadeAlpha > 0.005f)
        {
            byte a = (byte)Math.Clamp((int)MathF.Round(fadeAlpha * 255f), 0, 255);
            Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color((byte)0, (byte)0, (byte)0, a));
        }
    }

    private static void DrawFrameRivet(int x, int y)
    {
        Raylib.DrawRectangle(x, y, 4, 4, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(x, y, 4, 4, Palette.OutlineVoid);
    }

    private static List<string> WrapCenteredLines(string text, int fontSize, int maxWidth)
    {
        List<string> lines = [];
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;

        foreach (string word in words)
        {
            string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (UiText.MeasureWidth(candidate, fontSize) <= maxWidth || string.IsNullOrEmpty(current))
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines;
    }

    public void Unload() { }
}
