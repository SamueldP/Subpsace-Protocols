using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

/// <summary>
/// Expanded Dev Debug Overlay (toggled with F1, Backtick `, or F12) on the 1280x720 virtual canvas.
/// Provides playtest controls:
/// - Shift Time Scale (1x / 2x / 5x / 10x) & instant End Shift Now
/// - Live Economy Pay Per Ship stepper (5 / 8 / 10 / 15 SC)
/// - Live Animation Speed Multiplier stepper (0.75x / 1.0x / 1.25x / 1.5x / 2.0x)
/// - Force Reseed / Spawn Next Ship + Force Valid / Force Invalid filter
/// - Dev-only Manual Sync toggle & Cycle Hunger/Hypoxia SPS Sway
/// - Day Jump (1..20) + Day2SuspectDetained toggle + Reset Strikes / +50 SC
/// </summary>
public sealed class DebugOverlay
{
    private const int PanelX = 16;
    private const int PanelY = 40;
    private const int PanelW = 468;
    private const int PanelH = 342;

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly DevDebugState _devDebug;
    private readonly Action? _onShowTutorialNow;

    private int _selectedJumpDay = 1;

    public bool IsVisible { get; set; }

    public DebugOverlay(
        ScreenManager screenManager,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug,
        Action? onShowTutorialNow = null)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;
        _onShowTutorialNow = onShowTutorialNow;
        _selectedJumpDay = Math.Clamp(gameState.Day, 1, 20);
        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;
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
            if (mouse.IsHit(PanelX + 8, PanelY + 90, 222, 20))
            {
                _devDebug.CycleTimeScale();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 90, 224, 20))
            {
                _devDebug.PendingEndShiftNow = true;
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }

            // Row 2 (y = PanelY + 114): Pay Per Ship Stepper (5/8/10/15 SC) & Anim Speed Stepper (0.75x..2.0x)
            if (mouse.IsHit(PanelX + 8, PanelY + 114, 28, 20))
            {
                _devDebug.StepPayPerShip(-1);
                _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 202, PanelY + 114, 28, 20))
            {
                _devDebug.StepPayPerShip(1);
                _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 114, 28, 20))
            {
                _devDebug.StepAnimationSpeed(-1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 114, 28, 20))
            {
                _devDebug.StepAnimationSpeed(1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 3 (y = PanelY + 138): Force Reseed / Spawn Next Ship & Spawn Mode Filter
            if (mouse.IsHit(PanelX + 8, PanelY + 138, 222, 20))
            {
                _devDebug.PendingForceReseedShip = true;
                if (_screenManager.CurrentScreenId != ScreenId.Desk)
                {
                    _screenManager.SwitchTo(ScreenId.Desk);
                }
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 138, 224, 20))
            {
                _devDebug.CycleSpawnValidityMode();
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 4 (y = PanelY + 162): Dev Manual Sync Toggle & Cycle Food/Sway
            if (mouse.IsHit(PanelX + 8, PanelY + 162, 222, 20))
            {
                _devDebug.DevManualSyncEnabled = !_devDebug.DevManualSyncEnabled;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 162, 224, 20))
            {
                CycleFoodAndHypoxiaState();
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 5 (y = PanelY + 186): Day2SuspectDetained Toggle & Reset Strikes / +50 SC
            if (mouse.IsHit(PanelX + 8, PanelY + 186, 222, 20))
            {
                _gameState.Day2SuspectDetained = !_gameState.Day2SuspectDetained;
                _gameState.Day3KordaniOvernightRepApplied = false;
                _gameState.Day3PreShiftHailCompleted = false;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 186, 134, 20))
            {
                _gameState.SecurityStrikes = 0;
                _gameState.ClericalWarningsThisShift = 0;
                _gameState.PendingThreeStrikeAudit = false;
                _gameState.EndingFlags["Fail_SecurityDetainment"] = false;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 374, PanelY + 186, 86, 20))
            {
                _gameState.PactScrip += 50;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }

            // Row 6 (y = PanelY + 214): Day Jump 1..20 ([- DAY] [DAY >] [LOAD DAY XX] [D1] [D2] [D3])
            if (mouse.IsHit(PanelX + 8, PanelY + 214, 58, 20))
            {
                _selectedJumpDay = Math.Max(1, _selectedJumpDay - 1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 70, PanelY + 214, 58, 20))
            {
                _selectedJumpDay = Math.Min(20, _selectedJumpDay + 1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 132, PanelY + 214, 150, 20))
            {
                JumpToDay(_selectedJumpDay);
                return true;
            }
            if (mouse.IsHit(PanelX + 288, PanelY + 214, 54, 20))
            {
                _selectedJumpDay = 1;
                JumpToDay(1);
                return true;
            }
            if (mouse.IsHit(PanelX + 346, PanelY + 214, 54, 20))
            {
                _selectedJumpDay = 2;
                JumpToDay(2);
                return true;
            }
            if (mouse.IsHit(PanelX + 404, PanelY + 214, 56, 20))
            {
                _selectedJumpDay = 3;
                JumpToDay(3);
                return true;
            }

            // Row 7 (y = PanelY + 242): Screen Routing [MENU] [CORR] [DESK] [QTRS] [ART]
            if (mouse.IsHit(PanelX + 8, PanelY + 242, 86, 20))
            {
                _screenManager.SwitchTo(ScreenId.MainMenu);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 100, PanelY + 242, 86, 20))
            {
                _screenManager.SwitchTo(ScreenId.Corridor);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 192, PanelY + 242, 86, 20))
            {
                _screenManager.SwitchTo(ScreenId.Desk);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 284, PanelY + 242, 86, 20))
            {
                _screenManager.SwitchTo(ScreenId.Quarters);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 376, PanelY + 242, 84, 20))
            {
                _screenManager.SwitchTo(ScreenId.ArtTest);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 8 (y = PanelY + 268): Playtest Tutorial Popups Toggle, Reset Seen Flags & Show Now
            if (mouse.IsHit(PanelX + 8, PanelY + 268, 222, 20))
            {
                _gameState.TutorialFlags ??= new TutorialFlags();
                _gameState.TutorialFlags.TutorialsEnabled = !_gameState.TutorialFlags.TutorialsEnabled;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 268, 134, 20))
            {
                _gameState.TutorialFlags ??= new TutorialFlags();
                _gameState.TutorialFlags.ResetSeenFlags();
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 374, PanelY + 268, 86, 20))
            {
                IsVisible = false;
                _onShowTutorialNow?.Invoke();
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

        if (!IsVisible)
            return;

        Raylib.DrawRectangle(PanelX, PanelY, PanelW, PanelH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(PanelX, PanelY, PanelW, PanelH, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"DEV DEBUG [F1/`] | SCREEN: {_screenManager.CurrentScreenId} | FPS: {Raylib.GetFPS()} | {Constants.VirtualWidth}x{Constants.VirtualHeight} ({mouse.Scale:0.##}X)",
            PanelX + 8, PanelY + 8, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"VM:({mouse.X},{mouse.Y}) | D{_gameState.Day:D2} | {_gameState.PactScrip} SC | WARN:{_gameState.ClericalWarningsThisShift}/2 | STRK:{_gameState.SecurityStrikes} | SEED:{_gameState.RunSeed}",
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
            $"KDF REP: {_gameState.GetRep("KDF-"):+#;-#;0} | SWAY: {foodLabel}",
            PanelX + 8, PanelY + 40, 10, Palette.CrtAmberPrimary);

        Raylib.DrawText(
            $"D2 SUSPECT: {_gameState.Day2SuspectRegistration} ({_gameState.Day2SuspectDeclaredOrigin}) | D3 HAIL: {(_gameState.Day2SuspectDetained ? "BRANCH A (DETAINED)" : "BRANCH B (CLEARED)")}",
            PanelX + 8, PanelY + 56, 10, Palette.ClearanceTeal);

        Raylib.DrawText(
            $"VRAM PROC TEX: {PixelBuffer.ActiveProceduralTextureCount} | AUDIO: {AudioHooks.LastPlayedSoundId}",
            PanelX + 8, PanelY + 72, 10, Palette.CrtAmberDimmed);

        // Row 1: Time Scale & End Shift Now
        DrawDebugButton(mouse, PanelX + 8, PanelY + 90, 222, 20,
            $"SHIFT TIME SCALE: {_devDebug.ShiftTimeScale:0}X (CYCLE)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 90, 224, 20,
            "END SHIFT NOW (270s)", Palette.AlertCrimson);

        // Row 2: Live Economy Pay Per Ship & Animation Speed Multiplier Steppers
        DrawDebugButton(mouse, PanelX + 8, PanelY + 114, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 114, 162, 20, $"PAY/SHIP: {_devDebug.PayPerShipScrip} SC", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 202, PanelY + 114, 28, 20, " >", Palette.CrtAmberPrimary);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 114, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 114, 164, 20, $"ANIM SPEED: {_devDebug.AnimationSpeedMultiplier:0.00}X", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 114, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 3: Reseed / Spawn Next Ship & Validity Filter
        DrawDebugButton(mouse, PanelX + 8, PanelY + 138, 222, 20,
            "RESEED / SPAWN NEXT SHIP", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 138, 224, 20,
            $"SPAWN MODE: {_devDebug.SpawnValidityLabel}", Palette.ClearanceTeal);

        // Row 4: Dev Manual Sync & Food/Sway Cycle
        DrawDebugButton(mouse, PanelX + 8, PanelY + 162, 222, 20,
            $"DEV MANUAL SYNC: {(_devDebug.DevManualSyncEnabled ? "ON" : "OFF")}",
            _devDebug.DevManualSyncEnabled ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 162, 224, 20,
            "CYCLE FOOD / HYPOXIA SWAY", Palette.CrtAmberPrimary);

        // Row 5: Day2SuspectDetained, Reset Strikes/Warn, +50 SC
        DrawDebugButton(mouse, PanelX + 8, PanelY + 186, 222, 20,
            $"Day2SuspectDetained: {(_gameState.Day2SuspectDetained ? "TRUE" : "FALSE")}",
            _gameState.Day2SuspectDetained ? Palette.AlertCrimson : Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 186, 134, 20,
            "RESET STRIKES", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 374, PanelY + 186, 86, 20,
            "+50 SC", Palette.ClearanceTeal);

        // Row 6: Day Jump 1..20 + Quick D1/D2/D3
        DrawDebugButton(mouse, PanelX + 8, PanelY + 214, 58, 20, "< DAY", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 70, PanelY + 214, 58, 20, "DAY >", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 132, PanelY + 214, 150, 20, $"LOAD DAY {_selectedJumpDay:D2}", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 288, PanelY + 214, 54, 20, "DAY 1", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 346, PanelY + 214, 54, 20, "DAY 2", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 404, PanelY + 214, 56, 20, "DAY 3", Palette.CrtAmberDimmed);

        // Row 7: Screen Routing
        DrawDebugButton(mouse, PanelX + 8, PanelY + 242, 86, 20, "MENU", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 100, PanelY + 242, 86, 20, "CORRIDOR", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 192, PanelY + 242, 86, 20, "DESK", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 284, PanelY + 242, 86, 20, "QUARTERS", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 376, PanelY + 242, 84, 20, "ART TEST", Palette.CrtAmberDimmed);

        // Row 8: Tutorial Popups Toggle, Reset Seen Flags, Show Current Day Tutorial Now
        bool tutEnabled = _gameState.TutorialFlags?.TutorialsEnabled ?? true;
        DrawDebugButton(mouse, PanelX + 8, PanelY + 268, 222, 20,
            $"TUTORIAL POPUPS: {(tutEnabled ? "ON" : "OFF")}",
            tutEnabled ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 268, 134, 20,
            "RESET TUTORIALS", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 374, PanelY + 268, 86, 20,
            "SHOW [?]", Palette.ClearanceTeal);

        Raylib.DrawText("PAY PRESETS: 5/8/10/15 SC  |  ANIM PRESETS: 0.75/1.0/1.25/1.5/2.0X", PanelX + 8, PanelY + 298, 10, Palette.CrtAmberDimmed);
        Raylib.DrawText("PRESS [F1] OR [`] TO CLOSE DEV DEBUG OVERLAY", PanelX + 8, PanelY + 318, 10, Palette.WeatheredGunmetalEdge);
    }

    private static void DrawStepperReadout(int x, int y, int w, int h, string label, Color textCol)
    {
        Raylib.DrawRectangle(x, y, w, h, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(x, y, w, h, Palette.WeatheredGunmetalEdge);
        int tw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, x + Math.Max(4, (w - tw) / 2), y + 5, 10, textCol);
    }

    private static void DrawDebugButton(VirtualMouseState mouse, int x, int y, int w, int h, string label, Color textCol)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(label, x + 6, y + 5, 10, textCol);
    }
}
