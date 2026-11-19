using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

/// <summary>
/// Harness screen verifying the canonical 08_Physical_Look color palette,
/// 4x4 Bayer dithering ramps, 64x64 PortraitRenderer buffer, and 120x56 ShipRenderer buffer,
/// with strict GPU Texture2D cache cleanup on seed change or screen exit.
/// </summary>
public sealed class ArtTestScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private Texture2D _cachedPortraitTex;
    private Texture2D _cachedShipTex;
    private int _seedIndex;
    private bool _spsPreview;

    private static readonly string[] SampleSeeds =
    [
        "capt-vek-409",
        "GUV-771-VERIDIA",
        "KDF-104-KORRVATH",
        "BMC-882-DULKRAG",
        "KRW-519-GHAROK",
        "PRJ-009-HACKJOB"
    ];

    private static readonly (string Name, string Hex, Color Col)[] CoreSwatches =
    [
        ("OutlineVoid", Palette.HexOutlineVoid, Palette.OutlineVoid),
        ("DeepSpaceVoid", Palette.HexDeepSpaceVoid, Palette.DeepSpaceVoid),
        ("BrownDwarfDeep", Palette.HexBrownDwarfGlowDeep, Palette.BrownDwarfGlowDeep),
        ("BrownDwarfRim", Palette.HexBrownDwarfGlowRim, Palette.BrownDwarfGlowRim),
        ("GunmetalMid", Palette.HexWeatheredGunmetalMid, Palette.WeatheredGunmetalMid),
        ("GunmetalEdge", Palette.HexWeatheredGunmetalEdge, Palette.WeatheredGunmetalEdge),
        ("BronzeShadow", Palette.HexOxidizedBronzeShadow, Palette.OxidizedBronzeShadow),
        ("BronzeWorn", Palette.HexOxidizedBronzeWorn, Palette.OxidizedBronzeWorn),
        ("CrtAmberPrimary", Palette.HexCrtAmberPrimary, Palette.CrtAmberPrimary),
        ("CrtAmberDimmed", Palette.HexCrtAmberDimmed, Palette.CrtAmberDimmed),
        ("ClearanceTeal", Palette.HexClearanceTeal, Palette.ClearanceTeal),
        ("AlertCrimson", Palette.HexAlertCrimson, Palette.AlertCrimson)
    ];

    public ArtTestScreen(ScreenManager screenManager)
    {
        _screenManager = screenManager;
    }

    public void OnEnter()
    {
        RegenerateCachedTextures();
    }

    public void OnExit()
    {
        UnloadCachedTextures();
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(548, 4, 84, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
        }
        else if (mouse.IsHit(310, 44, 110, 20))
        {
            _seedIndex = (_seedIndex + 1) % SampleSeeds.Length;
            AudioHooks.Play("terminal_key_tab");
            RegenerateCachedTextures();
        }
        else if (mouse.IsHit(428, 44, 120, 20))
        {
            _spsPreview = !_spsPreview;
            AudioHooks.Play("sps_scanner_on");
            RegenerateCachedTextures();
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.DeepSpaceVoid);

        // Header
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 23, Constants.VirtualWidth, 23, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("08_PHYSICAL_LOOK // PALETTE & PROCEDURAL BUFFER HARNESS", 8, 7, 10, Palette.CrtAmberPrimary);
        DrawButton(mouse, 548, 4, 84, 16, "MAIN MENU");

        // Left Column: 12 Core Station & UI Color Swatches
        Raylib.DrawText("CORE STATION & UI PALETTE", 12, 32, 10, Palette.CrtAmberPrimary);
        for (int i = 0; i < CoreSwatches.Length; i++)
        {
            int sy = 48 + i * 25;
            var sw = CoreSwatches[i];
            Raylib.DrawRectangle(12, sy, 36, 20, sw.Col);
            Raylib.DrawRectangleLines(12, sy, 36, 20, Palette.WeatheredGunmetalEdge);
            Raylib.DrawText($"{sw.Hex}  {sw.Name}", 54, sy + 5, 10, Palette.CrtAmberPrimary);
        }

        // Right Column: Procedural Buffers & Bayer Dither Ramp Preview
        string currentSeed = SampleSeeds[_seedIndex];
        Raylib.DrawText($"SEED: {currentSeed}", 310, 30, 10, Palette.ClearanceTeal);
        DrawButton(mouse, 310, 44, 110, 20, "CYCLE SEED");
        DrawButton(mouse, 428, 44, 120, 20, _spsPreview ? "SPS MODE: ON" : "SPS MODE: OFF");

        // 64 x 64 Portrait Preview (1x native and 2x integer scaled)
        Raylib.DrawText("PORTRAIT (64x64 NATIVE & 2X):", 310, 76, 10, Palette.CrtAmberPrimary);
        if (_cachedPortraitTex.Id != 0)
        {
            Raylib.DrawTexture(_cachedPortraitTex, 310, 92, Color.White);
            Raylib.DrawRectangleLines(309, 91, 66, 66, Palette.WeatheredGunmetalEdge);

            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(390, 92, Constants.PortraitWidth * 2, Constants.PortraitHeight * 2);
            Raylib.DrawTexturePro(_cachedPortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
            Raylib.DrawRectangleLines(389, 91, 130, 130, Palette.CrtAmberDimmed);
        }

        // 120 x 56 Ship Preview (2x integer scaled -> 240 x 112)
        Raylib.DrawText("STARSHIP (120x56 LEFT-FACING 2X):", 310, 228, 10, Palette.CrtAmberPrimary);
        if (_cachedShipTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.ShipWidth, Constants.ShipHeight);
            Rectangle dst = new(310, 242, Constants.ShipWidth * 2, Constants.ShipHeight * 2);
            Raylib.DrawTexturePro(_cachedShipTex, src, dst, Vector2.Zero, 0f, Color.White);
            Raylib.DrawRectangleLines(309, 241, Constants.ShipWidth * 2 + 2, Constants.ShipHeight * 2 + 2, Palette.BrownDwarfGlowRim);
        }
    }

    private void RegenerateCachedTextures()
    {
        UnloadCachedTextures();

        string seed = SampleSeeds[_seedIndex];
        PixelBuffer portraitBuf = PortraitRenderer.RenderPlaceholderBuffer(seed);
        _cachedPortraitTex = portraitBuf.ToTexture2D();

        PixelBuffer shipBuf = ShipRenderer.RenderPlaceholderBuffer(seed, _spsPreview);
        _cachedShipTex = shipBuf.ToTexture2D();
    }

    private void UnloadCachedTextures()
    {
        if (_cachedPortraitTex.Id != 0)
        {
            Raylib.UnloadTexture(_cachedPortraitTex);
            _cachedPortraitTex = default;
        }
        if (_cachedShipTex.Id != 0)
        {
            Raylib.UnloadTexture(_cachedShipTex);
            _cachedShipTex = default;
        }
    }

    private static void DrawButton(VirtualMouseState mouse, int x, int y, int w, int h, string text)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(text, x + 6, y + 4, 10, Palette.CrtAmberPrimary);
    }

    public void Unload()
    {
        UnloadCachedTextures();
    }
}
