using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

/// <summary>
/// Expanded Dev Debug Overlay (toggled with F1, Backtick `, or F12) on the 960x540 virtual canvas.
/// Provides playtest controls:
/// - Shift Time Scale (1x / 2x / 5x / 10x) & instant End Shift Now
/// - Force Reseed / Spawn Next Ship + Force Valid / Force Invalid filter
/// - Dev-only Manual Sync toggle
/// - Day Jump (1..20) + Day2SuspectDetained toggle
/// - Reset Strikes/Warnings, Add +50 SC, Cycle Hunger/Hypoxia SPS Sway
/// </summary>
public sealed class DebugOverlay
{
    private const int PanelX = 10;
    private const int PanelY = 28;
    private const int PanelW = 430;
    private const int PanelH = 266;

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly DevDebugState _devDebug;

    private int _selectedJumpDay = 1;

    public bool IsVisible { get; set; }

    public DebugOverlay(
        ScreenManager screenManager,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;
        _selectedJumpDay = Math.Clamp(gameState.Day, 1, 20);
        IsVisible = false;
    }

    public bool HandleInput(VirtualMouseState mouse)
    {
        if (!_config.EnableDebugMenu)
            return false;

        if (Raylib.IsKeyPressed(KeyboardKey.F1) ||
            Raylib.IsKeyPressed(KeyboardKey.Grave) ||
            Raylib.IsKeyPressed(KeyboardKey.F12))
        {
            IsVisible = !IsVisible;
            if (IsVisible)
            {
                _selectedJumpDay = Math.Clamp(_gameState.Day, 1, 20);
            }
            AudioHooks.Play("debug_overlay_toggle");
        }

        // Direct screen routing hotkeys (F2 - F5)
        if (Raylib.IsKeyPressed(KeyboardKey.F2))
            _screenManager.SwitchTo(ScreenId.Corridor);
        else if (Raylib.IsKeyPressed(KeyboardKey.F3))
            _screenManager.SwitchTo(ScreenId.Desk);
        else if (Raylib.IsKeyPressed(KeyboardKey.F4))
            _screenManager.SwitchTo(ScreenId.Quarters);
        else if (Raylib.IsKeyPressed(KeyboardKey.F5))
            _screenManager.SwitchTo(ScreenId.ArtTest);

        if (!IsVisible)
            return false;

        if (mouse.LeftPressed && mouse.IsHit(PanelX, PanelY, PanelW, PanelH))
        {
            // Row 1 (y = PanelY + 90): Time Scale & End Shift Now
            if (mouse.IsHit(PanelX + 8, PanelY + 90, 202, 20))
            {
                _devDebug.CycleTimeScale();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 216, PanelY + 90, 206, 20))
            {
                _devDebug.PendingEndShiftNow = true;
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }

            // Row 2 (y = PanelY + 114): Force Reseed / Spawn Next Ship & Spawn Mode Filter
            if (mouse.IsHit(PanelX + 8, PanelY + 114, 202, 20))
            {
                _devDebug.PendingForceReseedShip = true;
                if (_screenManager.CurrentScreenId != ScreenId.Desk)
                {
                    _screenManager.SwitchTo(ScreenId.Desk);
                }
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 216, PanelY + 114, 206, 20))
            {
                _devDebug.CycleSpawnValidityMode();
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 3 (y = PanelY + 138): Dev Manual Sync Toggle & Cycle Food/Sway
            if (mouse.IsHit(PanelX + 8, PanelY + 138, 202, 20))
            {
                _devDebug.DevManualSyncEnabled = !_devDebug.DevManualSyncEnabled;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 216, PanelY + 138, 206, 20))
            {
                CycleFoodAndHypoxiaState();
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 4 (y = PanelY + 162): Day2SuspectDetained Toggle & Reset Strikes / +50 SC
            if (mouse.IsHit(PanelX + 8, PanelY + 162, 202, 20))
            {
                _gameState.Day2SuspectDetained = !_gameState.Day2SuspectDetained;
                _gameState.Day3KordaniOvernightRepApplied = false;
                _gameState.Day3PreShiftHailCompleted = false;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 216, PanelY + 162, 124, 20))
            {
                _gameState.SecurityStrikes = 0;
                _gameState.ClericalWarningsThisShift = 0;
                _gameState.PendingThreeStrikeAudit = false;
                _gameState.EndingFlags["Fail_SecurityDetainment"] = false;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 344, PanelY + 162, 78, 20))
            {
                _gameState.PactScrip += 50;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }

            // Row 5 (y = PanelY + 186): Day Jump 1..20 ([- DAY] [DAY >] [LOAD DAY XX] [D1] [D2] [D3])
            if (mouse.IsHit(PanelX + 8, PanelY + 186, 56, 20))
            {
                _selectedJumpDay = Math.Max(1, _selectedJumpDay - 1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 68, PanelY + 186, 56, 20))
            {
                _selectedJumpDay = Math.Min(20, _selectedJumpDay + 1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 128, PanelY + 186, 130, 20))
            {
                JumpToDay(_selectedJumpDay);
                return true;
            }
            if (mouse.IsHit(PanelX + 264, PanelY + 186, 50, 20))
            {
                _selectedJumpDay = 1;
                JumpToDay(1);
                return true;
            }
            if (mouse.IsHit(PanelX + 318, PanelY + 186, 50, 20))
            {
                _selectedJumpDay = 2;
                JumpToDay(2);
                return true;
            }
            if (mouse.IsHit(PanelX + 372, PanelY + 186, 50, 20))
            {
                _selectedJumpDay = 3;
                JumpToDay(3);
                return true;
            }

            // Row 6 (y = PanelY + 214): Screen Routing [MENU] [CORR] [DESK] [QTRS] [ART]
            if (mouse.IsHit(PanelX + 8, PanelY + 214, 78, 20))
            {
                _screenManager.SwitchTo(ScreenId.MainMenu);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 92, PanelY + 214, 78, 20))
            {
                _screenManager.SwitchTo(ScreenId.Corridor);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 176, PanelY + 214, 78, 20))
            {
                _screenManager.SwitchTo(ScreenId.Desk);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 260, PanelY + 214, 78, 20))
            {
                _screenManager.SwitchTo(ScreenId.Quarters);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 344, PanelY + 214, 78, 20))
            {
                _screenManager.SwitchTo(ScreenId.ArtTest);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            return true;
        }

        return false;
    }

    private void CycleFoodAndHypoxiaState()
    {
        if (_gameState.CurrentHungerTier == HungerTier.HighTierRations && !_gameState.IsHypoxic)
        {
            _gameState.CurrentHungerTier = HungerTier.MediumNutrientPaste;
            _gameState.DaysWithoutFood = 0;
            _gameState.IsHypoxic = false;
        }
        else if (_gameState.CurrentHungerTier == HungerTier.MediumNutrientPaste && !_gameState.IsHypoxic)
        {
            _gameState.CurrentHungerTier = HungerTier.LowNoFood;
            _gameState.DaysWithoutFood = 2;
            _gameState.IsHypoxic = true;
        }
        else
        {
            _gameState.CurrentHungerTier = HungerTier.HighTierRations;
            _gameState.DaysWithoutFood = 0;
            _gameState.IsHypoxic = false;
        }
        _gameState.SaveToDisk();
    }

    private void JumpToDay(int targetDay)
    {
        targetDay = Math.Clamp(targetDay, 1, 20);
        if (targetDay <= 2)
        {
            _gameState.Day3KordaniOvernightRepApplied = false;
            _gameState.Day3PreShiftHailCompleted = false;
        }
        else if (targetDay == 3)
        {
            _gameState.Day3PreShiftHailCompleted = false;
        }

        _gameState.AdvanceToDay(targetDay);
        AudioHooks.Play("terminal_key_confirm");
        _screenManager.SwitchTo(ScreenId.Desk);
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!_config.EnableDebugMenu)
            return;

        if (mouse.IsInsideViewport)
        {
            Raylib.DrawLine(mouse.X - 3, mouse.Y, mouse.X + 3, mouse.Y, Palette.CrtAmberPrimary);
            Raylib.DrawLine(mouse.X, mouse.Y - 3, mouse.X, mouse.Y + 3, Palette.CrtAmberPrimary);
        }

        if (!IsVisible)
            return;

        Raylib.DrawRectangle(PanelX, PanelY, PanelW, PanelH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(PanelX, PanelY, PanelW, PanelH, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"DEV DEBUG [F1/`] | SCREEN: {_screenManager.CurrentScreenId} | FPS: {Raylib.GetFPS()} | {Constants.VirtualWidth}x{Constants.VirtualHeight} ({mouse.Scale}X)",
            PanelX + 8, PanelY + 8, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"VMOUSE: ({mouse.X},{mouse.Y}) | DAY {_gameState.Day:D2} | {_gameState.PactScrip} SC | WARN: {_gameState.ClericalWarningsThisShift}/2 | STRIKES: {_gameState.SecurityStrikes}",
            PanelX + 8, PanelY + 24, 10, Palette.CrtAmberPrimary);

        string foodLabel = _gameState.IsHypoxic
            ? "LOW/HYPOXIC (MAX SWAY)"
            : _gameState.CurrentHungerTier switch
            {
                HungerTier.HighTierRations => "HIGH-GRADE (0 SWAY)",
                HungerTier.MediumNutrientPaste => "STANDARD (MED SWAY)",
                _ => "UNFED 2D+ (MAX SWAY)"
            };
        Raylib.DrawText(
            $"KDF REP: {_gameState.GetRep("KDF-"):+#;-#;0} | SWAY TIER: {foodLabel}",
            PanelX + 8, PanelY + 40, 10, Palette.CrtAmberPrimary);

        Raylib.DrawText(
            $"D2 SUSPECT: {_gameState.Day2SuspectRegistration} ({_gameState.Day2SuspectDeclaredOrigin}) | DAY 3: {(_gameState.Day2SuspectDetained ? "VARIANT B + HAIL" : "VARIANT A")}",
            PanelX + 8, PanelY + 56, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"VRAM PROC TEX: {PixelBuffer.ActiveProceduralTextureCount} | AUDIO: {AudioHooks.LastPlayedSoundId}",
            PanelX + 8, PanelY + 72, 10, Palette.CrtAmberDimmed);

        // Row 1: Time Scale & End Shift Now
        DrawDebugButton(mouse, PanelX + 8, PanelY + 90, 202, 20,
            $"TIME SCALE: {_devDebug.ShiftTimeScale:0}X (CYCLE)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 216, PanelY + 90, 206, 20,
            "END SHIFT NOW (270s)", Palette.AlertCrimson);

        // Row 2: Reseed / Spawn Next Ship & Validity Filter
        DrawDebugButton(mouse, PanelX + 8, PanelY + 114, 202, 20,
            "RESEED / SPAWN NEXT SHIP", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 216, PanelY + 114, 206, 20,
            $"SPAWN MODE: {_devDebug.SpawnValidityLabel}", Palette.ClearanceTeal);

        // Row 3: Dev Manual Sync & Food/Sway Cycle
        DrawDebugButton(mouse, PanelX + 8, PanelY + 138, 202, 20,
            $"DEV MANUAL SYNC: {(_devDebug.DevManualSyncEnabled ? "ON" : "OFF")}",
            _devDebug.DevManualSyncEnabled ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 216, PanelY + 138, 206, 20,
            "CYCLE FOOD / HYPOXIA SWAY", Palette.CrtAmberPrimary);

        // Row 4: Day2SuspectDetained, Reset Strikes/Warn, +50 SC
        DrawDebugButton(mouse, PanelX + 8, PanelY + 162, 202, 20,
            $"Day2SuspectDetained: {(_gameState.Day2SuspectDetained ? "TRUE" : "FALSE")}",
            _gameState.Day2SuspectDetained ? Palette.AlertCrimson : Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 216, PanelY + 162, 124, 20,
            "RESET STRIKES", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 344, PanelY + 162, 78, 20,
            "+50 SC", Palette.ClearanceTeal);

        // Row 5: Day Jump 1..20 + Quick D1/D2/D3
        DrawDebugButton(mouse, PanelX + 8, PanelY + 186, 56, 20, "< DAY", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 68, PanelY + 186, 56, 20, "DAY >", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 128, PanelY + 186, 130, 20, $"LOAD DAY {_selectedJumpDay:D2}", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 264, PanelY + 186, 50, 20, "D1", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 318, PanelY + 186, 50, 20, "D2", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 372, PanelY + 186, 50, 20, "D3", Palette.CrtAmberDimmed);

        // Row 6: Screen Routing
        DrawDebugButton(mouse, PanelX + 8, PanelY + 214, 78, 20, "MENU", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 92, PanelY + 214, 78, 20, "CORRIDOR", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 176, PanelY + 214, 78, 20, "DESK", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 260, PanelY + 214, 78, 20, "QUARTERS", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 344, PanelY + 214, 78, 20, "ART TEST", Palette.CrtAmberDimmed);

        Raylib.DrawText("PRESS [F1] OR [`] TO CLOSE DEV DEBUG OVERLAY", PanelX + 8, PanelY + 244, 10, Palette.WeatheredGunmetalEdge);
    }

    private static void DrawDebugButton(VirtualMouseState mouse, int x, int y, int w, int h, string label, Color textCol)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(label, x + 6, y + 5, 10, textCol);
    }
}
