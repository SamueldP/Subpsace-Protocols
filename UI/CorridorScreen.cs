using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 21:9 Ultrawide Panoramic Cutaway (640 x 274) non-interactive Walk-to-Work Corridor Strip
/// transition screen between Quarters and Booth 04 (Desk).
/// </summary>
public sealed class CorridorScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private float _elapsedSeconds;
    private const float TransitionDurationSeconds = 2.5f;

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

        // Allow skipping via click or space/enter, or auto-complete after 2.5s
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

        // Centered 21:9 strip (640 x 274) at Y = 43
        const int stripY = (Constants.VirtualHeight - Constants.CorridorHeight) / 2;

        AssetManager.DrawImageOrPlaceholder(
            "locations/corridor_strip.png",
            0, stripY, Constants.CorridorWidth, Constants.CorridorHeight,
            Palette.WeatheredGunmetalMid,
            Palette.OxidizedBronzeWorn,
            "STATIC ASSET: locations/corridor_strip.png (640x274 21:9 CUTAWAY)");

        // Industrial bulkhead stanchions and Brown Dwarf infrared porthole slits inside placeholder
        for (int i = 0; i < 6; i++)
        {
            int bx = 36 + i * 100;
            Raylib.DrawRectangle(bx, stripY + 24, 14, Constants.CorridorHeight - 48, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(bx + 24, stripY + 56, 52, 80, Palette.BrownDwarfGlowDeep);
            Raylib.DrawRectangleLines(bx + 24, stripY + 56, 52, 80, Palette.BrownDwarfGlowRim);
        }

        // Progress indicator
        float progress = Math.Clamp(_elapsedSeconds / TransitionDurationSeconds, 0f, 1f);
        int barW = (int)(260 * progress);

        Raylib.DrawRectangle(180, stripY + 190, 280, 48, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(180, stripY + 190, 280, 48, Palette.CrtAmberDimmed);

        string destLabel = _screenManager.NextCorridorDestination == ScreenId.Desk
            ? "TRANSIT: BERTH QUARTERS -> CUSTOMS BOOTH 04"
            : $"TRANSIT: CORRIDOR -> {_screenManager.NextCorridorDestination.ToString().ToUpperInvariant()}";

        Raylib.DrawText(destLabel, 192, stripY + 198, 10, Palette.CrtAmberPrimary);
        Raylib.DrawRectangle(190, stripY + 216, 260, 10, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(190, stripY + 216, barW, 10, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(190, stripY + 216, 260, 10, Palette.CrtAmberDimmed);

        Raylib.DrawText(
            $"DAY {_gameState.Day:D2} // VOID GATE 3 SECTOR TRANSIT (CLICK OR SPACE TO SKIP)",
            148, 336, 10, Palette.CrtAmberDimmed);
    }

    public void Unload() { }
}
