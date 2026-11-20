using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 21:9 Ultrawide Panoramic Cutaway (960 x 411) non-interactive Walk-to-Work Corridor Strip
/// transition screen between Quarters and Booth 04 (Desk).
/// </summary>
public sealed class CorridorScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private float _elapsedSeconds;
    private const float TransitionDurationSeconds = 2.0f;

    public CorridorScreen(ScreenManager screenManager, GameState gameState)
    {
        _screenManager = screenManager;
        _gameState = gameState;
    }

    public void OnEnter()
    {
        _elapsedSeconds = 0f;
        AudioHooks.Play("corridor_footsteps_loop");
    }

    public void OnExit() { }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _elapsedSeconds += deltaTime;

        if (mouse.LeftPressed ||
            Raylib.IsKeyPressed(KeyboardKey.Space) ||
            Raylib.IsKeyPressed(KeyboardKey.Enter) ||
            _elapsedSeconds >= TransitionDurationSeconds)
        {
            AudioHooks.Play("bulkhead_door_lock");
            _screenManager.SwitchTo(_screenManager.NextCorridorDestination);
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.OutlineVoid);

        int stripY = (Constants.VirtualHeight - Constants.CorridorHeight) / 2;

        AssetManager.DrawImageOrPlaceholder(
            "locations/corridor_strip.png",
            0, stripY, Constants.CorridorWidth, Constants.CorridorHeight,
            Palette.WeatheredGunmetalMid,
            Palette.OxidizedBronzeWorn,
            string.Empty);

        // Industrial bulkhead stanchions and Brown Dwarf infrared porthole slits
        for (int i = 0; i < 8; i++)
        {
            int bx = 28 + i * 118;
            Raylib.DrawRectangle(bx, stripY + 34, 18, Constants.CorridorHeight - 68, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(bx + 30, stripY + 84, 68, 120, Palette.BrownDwarfGlowDeep);
            Raylib.DrawRectangleLines(bx + 30, stripY + 84, 68, 120, Palette.BrownDwarfGlowRim);
        }

        float progress = Math.Clamp(_elapsedSeconds / TransitionDurationSeconds, 0f, 1f);
        int barW = (int)(360 * progress);

        Raylib.DrawRectangle(280, stripY + 290, 400, 58, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(280, stripY + 290, 400, 58, Palette.CrtAmberDimmed);

        string destLabel = _screenManager.NextCorridorDestination == ScreenId.Desk
            ? $"TRANSIT: BERTH QUARTERS -> BOOTH 04 (DAY {_gameState.Day:D2})"
            : $"TRANSIT: CORRIDOR -> {_screenManager.NextCorridorDestination.ToString().ToUpperInvariant()}";

        Raylib.DrawText(destLabel, 298, stripY + 302, 10, Palette.CrtAmberPrimary);
        Raylib.DrawRectangle(300, stripY + 324, 360, 12, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(300, stripY + 324, barW, 12, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(300, stripY + 324, 360, 12, Palette.CrtAmberDimmed);

        Raylib.DrawText(
            $"DAY {_gameState.Day:D2} // VOID GATE 3 SECTOR TRANSIT (CLICK OR SPACE TO SKIP)",
            286, Constants.VirtualHeight - 28, 10, Palette.CrtAmberDimmed);
    }

    public void Unload() { }
}
