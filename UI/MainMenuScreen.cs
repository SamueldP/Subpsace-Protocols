using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;

    public MainMenuScreen(ScreenManager screenManager, GameState gameState, Core.ConfigFlags config)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
    }

    public void OnEnter() { }

    public void OnExit() { }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(200, 150, 240, 24))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Desk);
        }
        else if (mouse.IsHit(200, 182, 240, 24))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.Desk);
        }
        else if (mouse.IsHit(200, 214, 240, 24))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.Quarters);
        }
        else if (mouse.IsHit(200, 246, 240, 24))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
        }
        else if (mouse.IsHit(200, 278, 240, 24))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.ArtTest);
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        // 16:9 Widescreen Landscape (640 x 360) Main Menu Banner
        AssetManager.DrawImageOrPlaceholder(
            "ui/main_menu_banner.png",
            0, 0, Constants.VirtualWidth, Constants.VirtualHeight,
            Palette.DeepSpaceVoid,
            Palette.WeatheredGunmetalEdge,
            "STATIC ASSET: ui/main_menu_banner.png (640x360 16:9)");

        // Dim Brown Dwarf horizon wash across top backdrop
        Raylib.DrawRectangle(1, 20, Constants.VirtualWidth - 2, 76, Palette.BrownDwarfGlowDeep);
        Raylib.DrawLine(1, 96, Constants.VirtualWidth - 2, 96, Palette.BrownDwarfGlowRim);

        // Title header panel (Legacy FTC Software Terminal Framing)
        Raylib.DrawRectangle(140, 36, 360, 88, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(140, 36, 360, 88, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(143, 39, 354, 82, Palette.OutlineVoid);

        Raylib.DrawText("SUBSPACE PROTOCOLS", 184, 52, 20, Palette.CrtAmberPrimary);
        Raylib.DrawText("VOID GATE 3 (VG-3) // BOOTH 04 TERMINAL", 184, 78, 10, Palette.CrtAmberDimmed);
        Raylib.DrawText("LEGACY FTC CONSORTIUM OS v4.08 — BULWARK PACT OVERRIDE", 162, 96, 10, Palette.ClearanceTeal);

        // Navigation Buttons
        DrawMenuButton(mouse, 200, 150, 240, 24, "1. WALK TO WORK -> BOOTH 04");
        DrawMenuButton(mouse, 200, 182, 240, 24, "2. DIRECT TO BOOTH 04 (DESK)");
        DrawMenuButton(mouse, 200, 214, 240, 24, "3. OFFICER QUARTERS (BERTH)");
        DrawMenuButton(mouse, 200, 246, 240, 24, "4. CORRIDOR LOADING STRIP");
        DrawMenuButton(mouse, 200, 278, 240, 24, "5. PROCEDURAL ART & PALETTE");

        // Footer bar
        Raylib.DrawRectangle(0, 336, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 336, Constants.VirtualWidth, 336, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"DAY {_gameState.Day:D2} | SCRIP: {_gameState.PactScrip} SC | STRIKES: {_gameState.SecurityStrikes} | F1-F5: ROUTE | F11/ALT+ENTER: FULLSCREEN",
            12, 344, 10, Palette.CrtAmberDimmed);
    }

    private static void DrawMenuButton(VirtualMouseState mouse, int x, int y, int w, int h, string text)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Color textCol = hovered ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        Raylib.DrawText(text, x + 12, y + 7, 10, textCol);
    }

    public void Unload() { }
}
