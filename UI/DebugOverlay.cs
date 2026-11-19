using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Diagnostic overlay rendered inside the 640x360 virtual framebuffer
/// to verify integer scaling, virtual mouse coordinate mapping (0..639 x 0..359),
/// screen routing shortcuts, ConfigFlags, and GameState variables.
/// </summary>
public sealed class DebugOverlay
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;

    public bool IsVisible { get; set; }

    public DebugOverlay(ScreenManager screenManager, GameState gameState, Core.ConfigFlags config)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        IsVisible = false;
    }

    public void HandleInput()
    {
        if (!_config.EnableDebugMenu)
            return;

        if (Raylib.IsKeyPressed(KeyboardKey.F12) || Raylib.IsKeyPressed(KeyboardKey.Grave))
        {
            IsVisible = !IsVisible;
            AudioHooks.Play("debug_overlay_toggle");
        }

        // Direct screen routing hotkeys (F1 - F5)
        if (Raylib.IsKeyPressed(KeyboardKey.F1))
            _screenManager.SwitchTo(ScreenId.MainMenu);
        else if (Raylib.IsKeyPressed(KeyboardKey.F2))
            _screenManager.SwitchTo(ScreenId.Corridor);
        else if (Raylib.IsKeyPressed(KeyboardKey.F3))
            _screenManager.SwitchTo(ScreenId.Desk);
        else if (Raylib.IsKeyPressed(KeyboardKey.F4))
            _screenManager.SwitchTo(ScreenId.Quarters);
        else if (Raylib.IsKeyPressed(KeyboardKey.F5))
            _screenManager.SwitchTo(ScreenId.ArtTest);
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!_config.EnableDebugMenu)
            return;

        // Crisp 1px virtual cursor crosshair
        if (mouse.IsInsideViewport)
        {
            Raylib.DrawLine(mouse.X - 3, mouse.Y, mouse.X + 3, mouse.Y, Palette.CrtAmberPrimary);
            Raylib.DrawLine(mouse.X, mouse.Y - 3, mouse.X, mouse.Y + 3, Palette.CrtAmberPrimary);
        }

        if (!IsVisible)
            return;

        const int panelX = 6;
        const int panelY = 26;
        const int panelW = 284;
        const int panelH = 114;

        Raylib.DrawRectangle(panelX, panelY, panelW, panelH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(panelX, panelY, panelW, panelH, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"DEBUG [F12/`] | SCREEN: {_screenManager.CurrentScreenId} | FPS: {Raylib.GetFPS()}",
            panelX + 6, panelY + 6, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"VMOUSE: ({mouse.X}, {mouse.Y}) [IN:{mouse.IsInsideViewport}] | SCALE: {mouse.Scale}X",
            panelX + 6, panelY + 20, 10, Palette.CrtAmberPrimary);

        Raylib.DrawText(
            $"WIN MOUSE: ({mouse.ScreenX}, {mouse.ScreenY}) | LETTERBOX: ({mouse.OffsetX}, {mouse.OffsetY})",
            panelX + 6, panelY + 34, 10, Palette.CrtAmberDimmed);

        Raylib.DrawText(
            $"CFG: GRAV={_config.EnableGravityGauge} DRONE={_config.EnableDockingDrone} DBG={_config.EnableDebugMenu}",
            panelX + 6, panelY + 48, 10, Palette.CrtAmberPrimary);

        Raylib.DrawText(
            $"STATE: DAY {_gameState.Day} | {_gameState.PactScrip} SC | {_gameState.PowerUnits} PU | STRIKES: {_gameState.SecurityStrikes}",
            panelX + 6, panelY + 62, 10, Palette.CrtAmberPrimary);

        Raylib.DrawText(
            $"AUDIO LAST: {AudioHooks.LastPlayedSoundId}",
            panelX + 6, panelY + 76, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            "HOTKEYS: F1:MENU F2:CORR F3:DESK F4:QTRS F5:ART",
            panelX + 6, panelY + 92, 10, Palette.CrtAmberDimmed);
    }
}
