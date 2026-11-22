using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 21:9 Ultrawide Panoramic Cutaway (1280 x 548) non-interactive Walk-to-Work Corridor Strip
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

        // Industrial bulkhead stanchions and Brown Dwarf infrared porthole slits across 1280px
        for (int i = 0; i < 10; i++)
        {
            int bx = 32 + i * 124;
            Raylib.DrawRectangle(bx, stripY + 40, 20, Constants.CorridorHeight - 80, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(bx + 32, stripY + 104, 72, 160, Palette.BrownDwarfGlowDeep);
            Raylib.DrawRectangleLines(bx + 32, stripY + 104, 72, 160, Palette.BrownDwarfGlowRim);
        }

        float progress = Math.Clamp(_elapsedSeconds / TransitionDurationSeconds, 0f, 1f);
        const int boxW = 460;
        const int boxH = 68;
        int boxX = (Constants.VirtualWidth - boxW) / 2;
        int boxY = stripY + Constants.CorridorHeight - 110;
        const int barMaxW = 420;
        int barW = (int)(barMaxW * progress);

        Raylib.DrawRectangle(boxX, boxY, boxW, boxH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, Palette.CrtAmberDimmed);

        string destLabel = _screenManager.NextCorridorDestination == ScreenId.Desk
            ? $"TRANSIT: BERTH QUARTERS -> BOOTH 04 (DAY {_gameState.Day:D2})"
            : $"TRANSIT: CORRIDOR -> {_screenManager.NextCorridorDestination.ToString().ToUpperInvariant()}";

        int labelW = Raylib.MeasureText(destLabel, 10);
        Raylib.DrawText(destLabel, boxX + (boxW - labelW) / 2, boxY + 14, 10, Palette.CrtAmberPrimary);
        Raylib.DrawRectangle(boxX + 20, boxY + 38, barMaxW, 14, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(boxX + 20, boxY + 38, barW, 14, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(boxX + 20, boxY + 38, barMaxW, 14, Palette.CrtAmberDimmed);

        string footer = $"DAY {_gameState.Day:D2} // VOID GATE 3 SECTOR TRANSIT (CLICK OR SPACE TO SKIP)";
        int footerW = Raylib.MeasureText(footer, 10);
        Raylib.DrawText(footer, (Constants.VirtualWidth - footerW) / 2, Constants.VirtualHeight - 36, 10, Palette.CrtAmberDimmed);
    }

    public void Unload() { }
}
