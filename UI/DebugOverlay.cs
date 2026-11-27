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
/// - Dev-only Manual Sync toggle & Cycle Food Sway Tier
/// - Day Jump (1..25) + Day2SuspectDetained toggle + Reset Strikes
/// - Live Balance buttons (-50 SC / +50 SC / +200 SC) & Live Runtime Price Tuners (Price Scale 0.5x..1.5x + Berth Lease / Ration / Deck 4 steppers)
/// </summary>
public sealed class DebugOverlay
{
    private const int PanelX = 16;
    private const int PanelY = 40;
    private const int PanelW = 656;
    private const int PanelH = 674;

    private int MaxJumpDay => Math.Max(Constants.MaxCampaignDay, _config.CampaignFinalDay);

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly DevDebugState _devDebug;
    private readonly Action? _onShowTutorialNow;

    private int _selectedJumpDay = 1;

    public bool IsVisible { get; set; }
    internal int SelectedJumpDay => _selectedJumpDay;

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
        _selectedJumpDay = Math.Clamp(gameState.Day, 1, Constants.MaxCampaignDay);
        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;
        IsVisible = false;
    }

    internal string BuildPrimaryTelemetryLine(VirtualMouseState mouse)
    {
        int shiftSec = _config.GetShiftDurationSeconds(_gameState.Day);
        string shiftTag = $"{shiftSec}s/{shiftSec / 60}m{shiftSec % 60:D2}s";
        return $"VM:{mouse.X},{mouse.Y} D{_gameState.Day:D2}({shiftTag}) {_gameState.PactScrip}SC W:{_gameState.ClericalWarningsThisShift}/2 S:{_gameState.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}";
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
                _selectedJumpDay = Math.Clamp(_gameState.Day, 1, Constants.MaxCampaignDay);
            }
            AudioHooks.Play("debug_overlay_toggle");
        }

        if (!IsVisible)
            return false;

        // Direct screen routing hotkeys (F2 - F5) strictly gated behind DebugOverlay.IsVisible == true
        if (Raylib.IsKeyPressed(KeyboardKey.F2))
            return TryHandleScreenJumpKey(KeyboardKey.F2);
        if (Raylib.IsKeyPressed(KeyboardKey.F3))
            return TryHandleScreenJumpKey(KeyboardKey.F3);
        if (Raylib.IsKeyPressed(KeyboardKey.F4))
            return TryHandleScreenJumpKey(KeyboardKey.F4);
        if (Raylib.IsKeyPressed(KeyboardKey.F5))
            return TryHandleScreenJumpKey(KeyboardKey.F5);

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
                    EnsureSandboxedIfLeavingMainMenu();
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
                CycleFoodSwayTier();
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

            // Row 6 (y = PanelY + 214): Day Jump 1..28 ([- DAY] [DAY >] [LOAD DAY XX] [D1] [D3] [D4])
            if (mouse.IsHit(PanelX + 8, PanelY + 214, 58, 20))
            {
                _selectedJumpDay = Math.Max(1, _selectedJumpDay - 1);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 70, PanelY + 214, 58, 20))
            {
                _selectedJumpDay = Math.Min(MaxJumpDay, _selectedJumpDay + 1);
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
                _selectedJumpDay = 3;
                JumpToDay(3);
                return true;
            }
            if (mouse.IsHit(PanelX + 404, PanelY + 214, 56, 20))
            {
                _selectedJumpDay = _config.DayUnlockWarpScanner;
                JumpToDay(_selectedJumpDay);
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
                EnsureSandboxedIfLeavingMainMenu();
                _screenManager.SwitchTo(ScreenId.Corridor);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 192, PanelY + 242, 86, 20))
            {
                EnsureSandboxedIfLeavingMainMenu();
                _screenManager.SwitchTo(ScreenId.Desk);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 284, PanelY + 242, 86, 20))
            {
                EnsureSandboxedIfLeavingMainMenu();
                _screenManager.SwitchTo(ScreenId.Quarters);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 376, PanelY + 242, 84, 20))
            {
                EnsureSandboxedIfLeavingMainMenu();
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

            // Row 9 (y = PanelY + 294): Quarters/Audit Playtest Shortcuts (+1 STRIKE, STOKER OVERLOAD, AM/PM -- ZERO PU)
            if (mouse.IsHit(PanelX + 8, PanelY + 294, 134, 20))
            {
                _gameState.SecurityStrikes++;
                if (_gameState.SecurityStrikes >= _config.MaxStrikesBeforeAudit)
                    _gameState.PendingThreeStrikeAudit = true;
                _gameState.SaveToDisk();
                AudioHooks.Play("citation_buzz");
                return true;
            }
            if (mouse.IsHit(PanelX + 148, PanelY + 294, 174, 20))
            {
                _gameState.StokerOverloadedToday = !_gameState.StokerOverloadedToday;
                _gameState.PendingStokerBerthSearch = _gameState.StokerOverloadedToday;
                _gameState.NextDayPowerBrownout = _gameState.StokerOverloadedToday;
                _gameState.SaveToDisk();
                AudioHooks.Play("detain_alarm");
                return true;
            }
            if (mouse.IsHit(PanelX + 328, PanelY + 294, 132, 20))
            {
                _gameState.QuartersPhase = _gameState.QuartersPhase == QuartersPhase.Morning
                    ? QuartersPhase.Evening
                    : QuartersPhase.Morning;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_tab");
                return true;
            }

            // Row 10 (y = PanelY + 320): Live Balance (-50 SC / +50 SC / +200 SC) & Live Price Scale (0.5x..1.5x)
            if (mouse.IsHit(PanelX + 8, PanelY + 320, 68, 20))
            {
                _gameState.PactScrip = Math.Max(0, _gameState.PactScrip - 50);
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 80, PanelY + 320, 68, 20))
            {
                _gameState.PactScrip += 50;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 152, PanelY + 320, 78, 20))
            {
                _gameState.PactScrip += 200;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 320, 28, 20))
            {
                _devDebug.StepPriceScale(-1, _config);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 320, 28, 20))
            {
                _devDebug.StepPriceScale(1, _config);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 11 (y = PanelY + 344): Individual BerthLeaseCost & BlankDataSlateCost Steppers
            if (mouse.IsHit(PanelX + 8, PanelY + 344, 28, 20))
            {
                _config.BerthLeaseCost = Math.Max(0, _config.BerthLeaseCost - 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 202, PanelY + 344, 28, 20))
            {
                _config.BerthLeaseCost = Math.Min(150, _config.BerthLeaseCost + 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 344, 28, 20))
            {
                _config.BlankDataSlateCost = Math.Max(5, _config.BlankDataSlateCost - 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 344, 28, 20))
            {
                _config.BlankDataSlateCost = Math.Min(100, _config.BlankDataSlateCost + 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 12 (y = PanelY + 368): Ration Costs (Dry/Paste/High-Tier) & Deck 4 Upgrade/Deferral Steppers
            if (mouse.IsHit(PanelX + 8, PanelY + 368, 28, 20))
            {
                _config.DryRationBricksCost = Math.Max(2, _config.DryRationBricksCost - 2);
                _config.NutrientPasteCost = Math.Max(5, _config.NutrientPasteCost - 5);
                _config.HighTierRationsCost = Math.Max(10, _config.HighTierRationsCost - 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 202, PanelY + 368, 28, 20))
            {
                _config.DryRationBricksCost = Math.Min(60, _config.DryRationBricksCost + 2);
                _config.NutrientPasteCost = Math.Min(90, _config.NutrientPasteCost + 5);
                _config.HighTierRationsCost = Math.Min(120, _config.HighTierRationsCost + 5);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 368, 28, 20))
            {
                _config.AuditDeferralFeeCost = Math.Max(10, _config.AuditDeferralFeeCost - 10);
                _config.PocketUpgrade2Cost = Math.Max(20, _config.PocketUpgrade2Cost - 10);
                _config.PocketUpgrade3Cost = Math.Max(40, _config.PocketUpgrade3Cost - 20);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 368, 28, 20))
            {
                _config.AuditDeferralFeeCost = Math.Min(150, _config.AuditDeferralFeeCost + 10);
                _config.PocketUpgrade2Cost = Math.Min(200, _config.PocketUpgrade2Cost + 10);
                _config.PocketUpgrade3Cost = Math.Min(300, _config.PocketUpgrade3Cost + 20);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 13 (y = PanelY + 392): Live Terminal Hack Unlock Toggle & Day 6 Morning Hack Drop Jump
            if (mouse.IsHit(PanelX + 8, PanelY + 392, 222, 20))
            {
                _gameState.TerminalHackedUnlocked = !_gameState.TerminalHackedUnlocked;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 392, 224, 20))
            {
                EnsureSandboxedIfLeavingMainMenu();
                Simulation.ShipGenerator.EnsureDay6IntroTargetAssigned(_gameState);
                _gameState.TerminalHackedUnlocked = false;
                _gameState.Day6UnsolicitedDropDelivered = false;
                _gameState.Emails.RemoveAll(e =>
                    e.IsTerminalHackEmail ||
                    e.EmailId.Equals(Simulation.EconomyEngine.Day6HackEmailId, StringComparison.OrdinalIgnoreCase) ||
                    e.EmailId.Equals("morning_roster_day_06", StringComparison.OrdinalIgnoreCase));
                _gameState.TodayMorningRoster.Clear();
                _gameState.ServiceChuteItems.RemoveAll(i =>
                    i.ItemId.StartsWith("d6_chute_blank", StringComparison.OrdinalIgnoreCase));
                _gameState.AdvanceToDay(6);
                _gameState.QuartersPhase = QuartersPhase.Morning;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                _screenManager.SwitchTo(ScreenId.Quarters);
                return true;
            }

            // Row 14 (y = PanelY + 416): Live Booth 04 Layout Switcher (A / B1 / B2), Portrait (B) 3x/2x & Ships V1/V2 Toggle
            if (mouse.IsHit(PanelX + 8, PanelY + 416, 70, 20))
            {
                DevDebugState.SetDeskUpperLayoutMode(DeskUpperLayoutMode.OptionA_Classic, _config);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 82, PanelY + 416, 72, 20))
            {
                DevDebugState.SetDeskUpperLayoutMode(DeskUpperLayoutMode.OptionB1_CommsSwap, _config);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 158, PanelY + 416, 72, 20))
            {
                DevDebugState.SetDeskUpperLayoutMode(DeskUpperLayoutMode.OptionB2_CommsSwapFlush, _config);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 416, 114, 20))
            {
                _devDebug.TogglePortraitScaleB();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 354, PanelY + 416, 106, 20))
            {
                _config.ShipArtVersion = _config.ShipArtVersion == ShipArtVersion.V1_Classic
                    ? ShipArtVersion.V2_HiBit
                    : ShipArtVersion.V1_Classic;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }

            // Row 15 (y = PanelY + 440): Talking Heads Advance Mode (AUTO / CLICK) & Path E Buyout Goal Stepper
            if (mouse.IsHit(PanelX + 8, PanelY + 440, 222, 20))
            {
                _config.TalkingHeadAdvanceMode = _config.TalkingHeadAdvanceMode == TalkingHeadAdvanceMode.AutoAdvance
                    ? TalkingHeadAdvanceMode.ClickToAdvance
                    : TalkingHeadAdvanceMode.AutoAdvance;
                _config.TryPersistTalkingHeadAdvanceMode(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 440, 28, 20))
            {
                _config.IndependentPassageFundTarget = Math.Max(100, _config.IndependentPassageFundTarget - 100);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 440, 28, 20))
            {
                _config.IndependentPassageFundTarget = Math.Min(5000, _config.IndependentPassageFundTarget + 100);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 16 (y = PanelY + 464): Invalid Ship Rate Tuners (D1-5 & D6+)
            if (mouse.IsHit(PanelX + 8, PanelY + 464, 28, 20))
            {
                _config.InvalidShipRateEarly = Math.Clamp(_config.InvalidShipRateEarly - 0.05f, 0.0f, 1.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 202, PanelY + 464, 28, 20))
            {
                _config.InvalidShipRateEarly = Math.Clamp(_config.InvalidShipRateEarly + 0.05f, 0.0f, 1.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 464, 28, 20))
            {
                _config.InvalidShipRateMid = Math.Clamp(_config.InvalidShipRateMid - 0.05f, 0.0f, 1.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 464, 28, 20))
            {
                _config.InvalidShipRateMid = Math.Clamp(_config.InvalidShipRateMid + 0.05f, 0.0f, 1.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 17 (y = PanelY + 488): Live Font Preset Cycler (<, center readout, >)
            if (mouse.IsHit(PanelX + 8, PanelY + 488, 28, 20))
            {
                FontManager.CyclePreset(-1);
                _config.UiFontPreset = FontManager.ActivePresetName;
                _config.TryPersistUiFontPreset(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 38, PanelY + 488, 392, 20) ||
                mouse.IsHit(PanelX + 432, PanelY + 488, 28, 20))
            {
                FontManager.CyclePreset(1);
                _config.UiFontPreset = FontManager.ActivePresetName;
                _config.TryPersistUiFontPreset(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 18 (y = PanelY + 512): Live Font Size Offset (-2 .. +3) & Intro Skip toggle
            if (mouse.IsHit(PanelX + 8, PanelY + 512, 28, 20))
            {
                _config.UiFontSizeOffset = FontManager.StepFontSizeOffset(-1);
                _config.TryPersistUiFontSizeOffset(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 38, PanelY + 512, 260, 20))
            {
                int nextOffset = FontManager.FontSizeOffset >= FontManager.MaxFontSizeOffset
                    ? FontManager.MinFontSizeOffset
                    : FontManager.FontSizeOffset + 1;
                _config.UiFontSizeOffset = FontManager.SetFontSizeOffset(nextOffset);
                _config.TryPersistUiFontSizeOffset(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 300, PanelY + 512, 28, 20))
            {
                _config.UiFontSizeOffset = FontManager.StepFontSizeOffset(1);
                _config.TryPersistUiFontSizeOffset(_gameState.SuppressDiskPersistence);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 334, PanelY + 512, 126, 20))
            {
                _devDebug.SkipIntroSequence = !_devDebug.SkipIntroSequence;
                if (_devDebug.SkipIntroSequence && _screenManager.CurrentScreenId == ScreenId.Intro)
                {
                    _screenManager.SwitchTo(ScreenId.Quarters);
                }
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 19 (y = PanelY + 536): WaferScoopMode Cycler, Force Empty Wafer Hopper, Spawn Test Mission Slate / Chute Drop
            if (mouse.IsHit(PanelX + 8, PanelY + 536, 152, 20))
            {
                DevDebugState.CycleWaferScoopMode(_config);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 164, PanelY + 536, 136, 20))
            {
                _devDebug.PendingForceEmptyWaferHopper = true;
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 304, PanelY + 536, 156, 20))
            {
                SpawnTestMissionSlateAndChuteDrop();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }

            // Row 20 (y = PanelY + 578): StokerSafeLoadCap (65%) & StokerUncalibratedShock (33%) live steppers
            if (mouse.IsHit(PanelX + 8, PanelY + 578, 28, 20))
            {
                _config.StokerSafeLoadCap = Math.Clamp(_config.StokerSafeLoadCap - 5.0f, 20.0f, 95.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 202, PanelY + 578, 28, 20))
            {
                _config.StokerSafeLoadCap = Math.Clamp(_config.StokerSafeLoadCap + 5.0f, 20.0f, 95.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 578, 28, 20))
            {
                _config.StokerUncalibratedShock = Math.Clamp(_config.StokerUncalibratedShock - 1.0f, 5.0f, 100.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 578, 28, 20))
            {
                _config.StokerUncalibratedShock = Math.Clamp(_config.StokerUncalibratedShock + 1.0f, 5.0f, 100.0f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Row 21 (y = PanelY + 600): RESET STRAIN (15%) & FORCE STOKER OVL
            if (mouse.IsHit(PanelX + 8, PanelY + 600, 222, 20))
            {
                _devDebug.PendingResetStokerStrain = true;
                _devDebug.LiveStokerSafeLoad = _config.StokerIdleBaseline;
                _devDebug.LiveStokerShock = 0f;
                _devDebug.LiveStokerPreviewShock = 0f;
                _gameState.StokerOverloadedToday = false;
                _gameState.PendingStokerBerthSearch = false;
                _gameState.NextDayPowerBrownout = false;
                _gameState.LastShiftStokerOverload = false;
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 600, 224, 20))
            {
                _devDebug.PendingForceStokerOverload = true;
                _devDebug.LiveStokerShock = Math.Max(_devDebug.LiveStokerShock, 105.0f - _devDebug.LiveStokerSafeLoad);
                _gameState.StokerOverloadedToday = true;
                _gameState.PendingStokerBerthSearch = true;
                _gameState.NextDayPowerBrownout = true;
                _gameState.LastShiftStokerOverload = true;
                _gameState.SaveToDisk();
                AudioHooks.Play("detain_alarm");
                return true;
            }

            // Row 22 (y = PanelY + 622): Live Brownout Lighting test triggers (AUTO / FORCE ON / FORCE DIM / FORCE OFF)
            if (mouse.IsHit(PanelX + 8, PanelY + 622, 98, 20))
            {
                _devDebug.BrownoutForceMode = DebugBrownoutForceMode.Auto;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 110, PanelY + 622, 112, 20))
            {
                _devDebug.BrownoutForceMode = _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOn
                    ? DebugBrownoutForceMode.Auto
                    : DebugBrownoutForceMode.ForceOn;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 226, PanelY + 622, 116, 20))
            {
                _devDebug.BrownoutForceMode = _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceDim
                    ? DebugBrownoutForceMode.Auto
                    : DebugBrownoutForceMode.ForceDim;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(PanelX + 346, PanelY + 622, 114, 20))
            {
                _devDebug.BrownoutForceMode = _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOff
                    ? DebugBrownoutForceMode.Auto
                    : DebugBrownoutForceMode.ForceOff;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }

            // Row 23 (y = PanelY + 644): Live EnableStokerDial toggle & POCKET CAP (1 / 2 / 3) stepper
            if (mouse.IsHit(PanelX + 8, PanelY + 644, 222, 20))
            {
                _config.EnableStokerDial = !_config.EnableStokerDial;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 236, PanelY + 644, 28, 20))
            {
                _gameState.PocketCapacity = Math.Clamp(_gameState.PocketCapacity - 1, 1, 3);
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(PanelX + 432, PanelY + 644, 28, 20))
            {
                _gameState.PocketCapacity = Math.Clamp(_gameState.PocketCapacity + 1, 1, 3);
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            return true;
        }

        return false;
    }

    private void CycleFoodSwayTier()
    {
        if (_gameState.CurrentHungerTier == HungerTier.HighTierRations)
        {
            _gameState.CurrentHungerTier = HungerTier.MediumNutrientPaste;
            _gameState.DaysWithoutFood = 0;
            _gameState.ConsecutiveDryOrNoFoodDays = 0;
        }
        else if (_gameState.CurrentHungerTier == HungerTier.MediumNutrientPaste)
        {
            _gameState.CurrentHungerTier = HungerTier.LowNoFood;
            _gameState.DaysWithoutFood = 2;
            _gameState.ConsecutiveDryOrNoFoodDays = 2;
        }
        else
        {
            _gameState.CurrentHungerTier = HungerTier.HighTierRations;
            _gameState.DaysWithoutFood = 0;
            _gameState.ConsecutiveDryOrNoFoodDays = 0;
        }
        _gameState.SaveToDisk();
    }

    internal bool TryHandleScreenJumpKey(KeyboardKey key)
    {
        if (!_config.EnableDebugMenu || !IsVisible)
            return false;

        ScreenId? target = key switch
        {
            KeyboardKey.F2 => ScreenId.Corridor,
            KeyboardKey.F3 => ScreenId.Desk,
            KeyboardKey.F4 => ScreenId.Quarters,
            KeyboardKey.F5 => ScreenId.ArtTest,
            _ => null
        };
        if (!target.HasValue)
            return false;

        EnsureSandboxedIfLeavingMainMenu();
        _screenManager.SwitchTo(target.Value);
        return true;
    }

    private void EnsureSandboxedIfLeavingMainMenu()
    {
        if (_screenManager.CurrentScreenId == ScreenId.MainMenu || !_gameState.HasActivePlayableSlot)
        {
            _gameState.SuppressDiskPersistence = true;
            _gameState.ActiveSaveSlot = 0;
            _gameState.HasActivePlayableSlot = false;
        }
    }

    private void JumpToDay(int targetDay)
    {
        EnsureSandboxedIfLeavingMainMenu();
        targetDay = Math.Clamp(targetDay, 1, MaxJumpDay);
        if (targetDay < 6)
        {
            _gameState.TerminalHackedUnlocked = false;
        }
        if (targetDay == 1)
        {
            _gameState.Day1VorstHailCompleted = false;
        }
        if (targetDay <= 2)
        {
            _gameState.Day2NixaHailCompleted = false;
        }
        if (targetDay < _config.DayUnlockManualPart1)
        {
            _gameState.Day8NixaHailCompleted = false;
        }
        else if (targetDay == _config.DayUnlockManualPart1)
        {
            _gameState.Day8NixaHailCompleted = false;
        }
        if (targetDay < _config.DayUnlockWarpScanner)
        {
            _gameState.Day3KordaniOvernightRepApplied = false;
            _gameState.Day4PreShiftHailCompleted = false;
        }
        else if (targetDay == _config.DayUnlockWarpScanner)
        {
            _gameState.Day4PreShiftHailCompleted = false;
        }

        _gameState.AdvanceToDay(targetDay);
        if (targetDay == _config.DayUnlockWaferCargo)
        {
            _gameState.Day12CargoIntroShown = false;
        }
        if (targetDay <= _config.DayUnlockStoker)
        {
            _gameState.Day15ForemanHailCompleted = false;
        }
        if (targetDay == 6)
        {
            Simulation.ShipGenerator.EnsureDay6IntroTargetAssigned(_gameState);
            _gameState.TerminalHackedUnlocked = true;
            _gameState.Day6UnsolicitedDropDelivered = true;
            if (!_gameState.CompletedInterceptMissionIds.Contains(Simulation.EconomyEngine.Day6IntroMissionId, StringComparer.OrdinalIgnoreCase) &&
                !_gameState.ActiveInterceptMissionIds.Contains(Simulation.EconomyEngine.Day6IntroMissionId, StringComparer.OrdinalIgnoreCase))
            {
                _gameState.ActiveInterceptMissionIds.Add(Simulation.EconomyEngine.Day6IntroMissionId);
            }

            bool hasBlankSlate =
                _gameState.PocketItems.Any(i => !i.IsBurnt && i.ItemType == ContrabandPocketItem.TypeBlankDataSlate) ||
                _gameState.VentStashItems.Any(i => !i.IsBurnt && i.ItemType == ContrabandPocketItem.TypeBlankDataSlate) ||
                _gameState.ServiceChuteItems.Any(i => !i.IsBurnt && i.ItemType == ContrabandPocketItem.TypeBlankDataSlate);

            if (!hasBlankSlate && _gameState.PocketItems.Count < _gameState.PocketCapacity)
            {
                _gameState.PocketItems.Add(new ContrabandPocketItem
                {
                    ItemId = _gameState.NextUniqueItemId("blankslate"),
                    ItemType = ContrabandPocketItem.TypeBlankDataSlate,
                    DisplayName = "BLANK DATA SLATE (UNREG)"
                });
            }

            _gameState.SaveToDisk();
        }
        AudioHooks.Play("terminal_key_confirm");
        _screenManager.SwitchTo(ScreenId.Desk);
    }

    private void SpawnTestMissionSlateAndChuteDrop()
    {
        EnsureSandboxedIfLeavingMainMenu();
        Data.CovertMissionDatabase db = Data.CovertMissionDatabase.Load();
        Data.CovertMissionSpec? deliverySpec = db.Missions.FirstOrDefault(m => m.IsDeliveryMission);

        string missionId = deliverySpec?.MissionId ?? "ftc_relay_drop";
        string targetPfx = deliverySpec?.TargetPrefix ?? "FTC-";
        string targetReg = deliverySpec?.TargetRegistration ?? "FTC-418-C";
        string targetName = deliverySpec?.TargetShipName ?? "SOLARIS DRIFT";
        string label = deliverySpec?.SlatePayloadLabel ?? "SEALED RELAY SLATE #9";
        string desc = deliverySpec?.PayloadDescription ?? "Pre-loaded single-use subspace navigation override packet.";

        _gameState.TerminalHackedUnlocked = true;
        _gameState.ActiveCovertMissionId = missionId;
        _gameState.ActiveCovertTargetPrefix = targetPfx;
        _gameState.ActiveCovertTargetReg = targetReg;
        _gameState.ActiveCovertTargetShipName = targetName;
        _gameState.ActiveCovertDeliveryDay = _gameState.Day;
        _gameState.DeliveredCovertMissionIds.Remove(missionId);

        ContrabandPocketItem CreateMissionSlate(string tag) => new()
        {
            ItemId = _gameState.NextUniqueItemId(tag),
            ItemType = ContrabandPocketItem.TypeMissionDataSlate,
            DisplayName = label,
            TargetShipPrefix = targetPfx,
            TargetShipRegistration = targetReg,
            TargetShipName = targetName,
            IsMissionSlate = true,
            MissionId = missionId,
            MissionTargetReg = targetReg,
            MissionTargetPrefix = targetPfx,
            MissionDescription = desc
        };

        if (!_gameState.ServiceChuteItems.Any(i => i.IsMissionSlate && !i.IsBurnt))
        {
            _gameState.ServiceChuteItems.Add(CreateMissionSlate("dbg_chute_mission"));
        }

        if (_screenManager.CurrentScreenId == ScreenId.Desk &&
            !_gameState.PocketItems.Any(i => i.IsMissionSlate && !i.IsBurnt))
        {
            if (_gameState.PocketItems.Count >= _gameState.PocketCapacity && _gameState.PocketCapacity < 3)
            {
                _gameState.PocketCapacity = Math.Min(3, _gameState.PocketItems.Count + 1);
            }
            if (_gameState.PocketItems.Count < _gameState.PocketCapacity)
            {
                _gameState.PocketItems.Add(CreateMissionSlate("dbg_pocket_mission"));
            }
        }

        _gameState.SaveToDisk();
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!_config.EnableDebugMenu)
            return;

        if (!IsVisible)
            return;

        Raylib.DrawRectangle(PanelX, PanelY, PanelW, PanelH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(PanelX, PanelY, PanelW, PanelH, Palette.ClearanceTeal);

        UiText.Draw(
            UiText.TruncateToWidth($"DEV DEBUG [F1/`] | SCREEN: {_screenManager.CurrentScreenId} | FPS: {Raylib.GetFPS()} | {Constants.VirtualWidth}x{Constants.VirtualHeight} ({mouse.Scale:0.##}X)", 10, PanelW - 16, denseRow: true),
            PanelX + 8, PanelY + 8, 10, Palette.ClearanceTeal, denseRow: true);

        int activeShiftSec = _config.GetShiftDurationSeconds(_gameState.Day);
        int selectedShiftSec = _config.GetShiftDurationSeconds(_selectedJumpDay);

        UiText.Draw(
            UiText.TruncateToWidth(BuildPrimaryTelemetryLine(mouse), 10, PanelW - 16, denseRow: true),
            PanelX + 8, PanelY + 24, 10, Palette.CrtAmberPrimary, denseRow: true);

        string foodLabel = _gameState.CurrentHungerTier switch
        {
            HungerTier.HighTierRations => "HIGH-TIER (0 SWAY)",
            HungerTier.MediumNutrientPaste => "PASTE (MED SWAY)",
            _ => "DRY/UNFED 2D+ (MAX SWAY)"
        };
        UiText.Draw(
            UiText.TruncateToWidth($"KDF REP: {_gameState.GetRep("KDF-"):+#;-#;0} | SWAY: {foodLabel} | LOGS: {_gameState.UnscrubbedLogCount}", 10, PanelW - 16, denseRow: true),
            PanelX + 8, PanelY + 40, 10, Palette.CrtAmberPrimary, denseRow: true);

        UiText.Draw(
            UiText.TruncateToWidth($"D3 SUSPECT: {_gameState.Day2SuspectRegistration} ({_gameState.Day2SuspectDeclaredOrigin}) | D4 HAIL: {(_gameState.GhostRouteSuspectDetained ? "BRANCH A (DETAINED)" : "BRANCH B (CLEARED)")}", 10, PanelW - 16, denseRow: true),
            PanelX + 8, PanelY + 56, 10, Palette.ClearanceTeal, denseRow: true);

        UiText.Draw(
            UiText.TruncateToWidth($"SEED:{_gameState.RunSeed} | VRAM TEX:{PixelBuffer.ActiveProceduralTextureCount} | SFX:{AudioHooks.LastPlayedSoundId}", 10, PanelW - 16, denseRow: true),
            PanelX + 8, PanelY + 72, 10, Palette.CrtAmberDimmed, denseRow: true);

        // Row 1: Time Scale & End Shift Now
        DrawDebugButton(mouse, PanelX + 8, PanelY + 90, 222, 20,
            $"SHIFT TIME SCALE: {_devDebug.ShiftTimeScale:0}X (CYCLE)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 90, 224, 20,
            $"END SHIFT NOW ({activeShiftSec}s)", Palette.AlertCrimson);

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
            "CYCLE FOOD SWAY TIER", Palette.CrtAmberPrimary);

        // Row 5: GhostRouteSuspectDetained, Reset Strikes/Warn, +50 SC
        DrawDebugButton(mouse, PanelX + 8, PanelY + 186, 222, 20,
            $"GhostRouteDetained: {(_gameState.GhostRouteSuspectDetained ? "TRUE" : "FALSE")}",
            _gameState.GhostRouteSuspectDetained ? Palette.AlertCrimson : Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 186, 134, 20,
            "RESET STRIKES", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 374, PanelY + 186, 86, 20,
            "+50 SC", Palette.ClearanceTeal);

        // Row 6: Day Jump 1..28 + Quick D1/D3/D4
        DrawDebugButton(mouse, PanelX + 8, PanelY + 214, 58, 20, "< DAY", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 70, PanelY + 214, 58, 20, "DAY >", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 132, PanelY + 214, 150, 20, $"LOAD DAY {_selectedJumpDay:D2} ({selectedShiftSec}s)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 288, PanelY + 214, 54, 20, "DAY 1", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 346, PanelY + 214, 54, 20, "DAY 3", Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 404, PanelY + 214, 56, 20, $"DAY {_config.DayUnlockWarpScanner}", Palette.CrtAmberDimmed);

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

        // Row 9: Quarters/Audit Playtest Controls (ZERO PU)
        DrawDebugButton(mouse, PanelX + 8, PanelY + 294, 134, 20, "+1 STRIKE", Palette.AlertCrimson);
        DrawDebugButton(mouse, PanelX + 148, PanelY + 294, 174, 20,
            $"STOKER OVL: {(_gameState.StokerOverloadedToday ? "ON" : "OFF")}",
            _gameState.StokerOverloadedToday ? Palette.AlertCrimson : Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 328, PanelY + 294, 132, 20,
            $"PHASE: {(_gameState.QuartersPhase == QuartersPhase.Morning ? "AM" : "PM")}", Palette.ClearanceTeal);

        // Row 10: Live Balance (-50 SC / +50 SC / +200 SC) & Price Scale (0.5x..1.5x)
        DrawDebugButton(mouse, PanelX + 8, PanelY + 320, 68, 20, "-50 SC", Palette.AlertCrimson);
        DrawDebugButton(mouse, PanelX + 80, PanelY + 320, 68, 20, "+50 SC", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 152, PanelY + 320, 78, 20, "+200 SC", Palette.ClearanceTeal);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 320, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 320, 164, 20, $"PRICE SCALE: {_devDebug.PriceScaleMultiplier:0.00}X", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 320, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 11: Berth Lease & Blank Data Slate live price steppers
        DrawDebugButton(mouse, PanelX + 8, PanelY + 344, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 344, 162, 20, $"LEASE: {_config.BerthLeaseCost} SC", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 202, PanelY + 344, 28, 20, " >", Palette.CrtAmberPrimary);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 344, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 344, 164, 20, $"BLANK SLATE: {_config.BlankDataSlateCost} SC", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 344, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 12: Ration Costs & Deck 4 Upgrade/Deferral live price steppers
        DrawDebugButton(mouse, PanelX + 8, PanelY + 368, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 368, 162, 20,
            $"FOOD: {_config.DryRationBricksCost}/{_config.NutrientPasteCost}/{_config.HighTierRationsCost}", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 202, PanelY + 368, 28, 20, " >", Palette.CrtAmberPrimary);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 368, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 368, 164, 20,
            $"D4 UPG:{_config.PocketUpgrade2Cost} DEF:{_config.AuditDeferralFeeCost}", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 368, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 13: Live Terminal Hack Unlock Toggle (Days 1-5 hidden vs Day 6+ revealed) & Day 6 AM jump
        DrawDebugButton(mouse, PanelX + 8, PanelY + 392, 222, 20,
            $"HACK UNLOCK: {(_gameState.TerminalHackedUnlocked ? "ON" : "OFF")}",
            _gameState.TerminalHackedUnlocked ? Palette.FromHex("#a78bfa") : Palette.CrtAmberDimmed);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 392, 224, 20,
            "LOAD DAY 06 AM (HACK DROP)", Palette.CrtAmberPrimary);

        // Row 14: Live Booth 04 Layout Switcher (A / B1 / B2), Portrait (B) 3x/2x & Ships V1/V2 Toggle
        bool isA = _config.DeskUpperLayoutMode == DeskUpperLayoutMode.OptionA_Classic;
        bool isB1 = _config.DeskUpperLayoutMode == DeskUpperLayoutMode.OptionB1_CommsSwap;
        bool isB2 = _config.DeskUpperLayoutMode == DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
        DrawDebugToggleButton(mouse, PanelX + 8, PanelY + 416, 70, 20, "DESK: A", isA, Palette.ClearanceTeal);
        DrawDebugToggleButton(mouse, PanelX + 82, PanelY + 416, 72, 20, "DESK: B1", isB1, Palette.ClearanceTeal);
        DrawDebugToggleButton(mouse, PanelX + 158, PanelY + 416, 72, 20, "DESK: B2", isB2, Palette.ClearanceTeal);

        string portraitScaleLabel = _devDebug.Use3xPortraitInLayoutB
            ? "PORTRAIT: 3x"
            : "PORTRAIT: 2x";
        DrawDebugToggleButton(mouse, PanelX + 236, PanelY + 416, 114, 20,
            portraitScaleLabel,
            !isA, Palette.ClearanceTeal);

        bool isShipV2 = _config.ShipArtVersion == ShipArtVersion.V2_HiBit;
        DrawDebugToggleButton(mouse, PanelX + 354, PanelY + 416, 106, 20,
            isShipV2 ? "SHIPS: V2" : "SHIPS: V1",
            isShipV2, Palette.ClearanceTeal);

        // Row 15: Talking-Head Advance Mode Toggle (AUTO / CLICK) & Path E Buyout Target Stepper
        bool clickAdvance = _config.TalkingHeadAdvanceMode == TalkingHeadAdvanceMode.ClickToAdvance;
        DrawDebugButton(mouse, PanelX + 8, PanelY + 440, 222, 20,
            $"TALKING HEADS: {(clickAdvance ? "CLICK" : "AUTO")}",
            clickAdvance ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 440, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 440, 164, 20, $"BUYOUT: {_config.IndependentPassageFundTarget} SC", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 440, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 16: Live Invalid-Ship Rate Tuners (Days 1-5 & Days 6+)
        DrawDebugButton(mouse, PanelX + 8, PanelY + 464, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 464, 162, 20, $"INV D1-5: {(_config.InvalidShipRateEarly * 100f):0}%", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 202, PanelY + 464, 28, 20, " >", Palette.CrtAmberPrimary);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 464, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 464, 164, 20, $"INV D6+: {(_config.InvalidShipRateMid * 100f):0}%", Palette.CrtAmberPrimary);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 464, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 17: Live Font Preset Cycler
        DrawDebugButton(mouse, PanelX + 8, PanelY + 488, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 488, 392, 20,
            UiText.TruncateToWidth($"FONT: {FontManager.ActivePresetName} ({FontManager.ActiveScaleMultiplier:0.00}x)", 10, 384), Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 488, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 18: Live Font Size Offset (-2 .. +3) & Intro Skip Toggle
        string offsetText = FontManager.FontSizeOffset > 0 ? $"+{FontManager.FontSizeOffset}" : FontManager.FontSizeOffset.ToString();
        DrawDebugButton(mouse, PanelX + 8, PanelY + 512, 28, 20, " -", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 512, 260, 20, $"FONT SIZE: {offsetText} (-2..+3)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 300, PanelY + 512, 28, 20, " +", Palette.CrtAmberPrimary);
        DrawDebugToggleButton(mouse, PanelX + 334, PanelY + 512, 126, 20,
            _devDebug.SkipIntroSequence ? "INTRO: SKIP" : "INTRO: ON",
            !_devDebug.SkipIntroSequence, Palette.ClearanceTeal);

        // Row 19: Build 5.1 WaferScoopMode Cycler, Force Empty Hopper & Spawn Test Mission Slate / Chute Drop
        string scoopTxt = $"SCOOP: {DevDebugState.GetWaferScoopShortLabel(_config.WaferScoopMode)}";
        DrawDebugButton(mouse, PanelX + 8, PanelY + 536, 152, 20, scoopTxt, Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 164, PanelY + 536, 136, 20, "EMPTY HOPPER", Palette.AlertCrimson);
        DrawDebugButton(mouse, PanelX + 304, PanelY + 536, 156, 20, "TEST CHUTE SLATE", Palette.CrtAmberPrimary);

        // Live Stoker Strain Telemetry Banner (y = PanelY + 559, h = 16)
        string stokerTelemetry = $"STOKER: SAFE {_devDebug.LiveStokerSafeLoad:0}% | SHK {_devDebug.LiveStokerShock:0}% | TOT {_devDebug.LiveStokerTotal:0}%";
        Color stokerTelCol = _devDebug.LiveStokerTotal > 100f
            ? Palette.AlertCrimson
            : (_devDebug.LiveStokerTotal > _config.StokerSafeLoadCap ? Palette.FromHex("#bdb040") : Palette.ClearanceTeal);
        DrawStepperReadout(PanelX + 8, PanelY + 559, 452, 17, stokerTelemetry, stokerTelCol);

        // Row 20: StokerSafeLoadCap (default 65%) & StokerUncalibratedShock (default 33%) steppers
        DrawDebugButton(mouse, PanelX + 8, PanelY + 578, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 38, PanelY + 578, 162, 20, $"SAFE CAP: {_config.StokerSafeLoadCap:0}%", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 202, PanelY + 578, 28, 20, " >", Palette.CrtAmberPrimary);

        DrawDebugButton(mouse, PanelX + 236, PanelY + 578, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 578, 164, 20, $"SHK: +{_config.StokerUncalibratedShock:0}%", Palette.FromHex("#bdb040"));
        DrawDebugButton(mouse, PanelX + 432, PanelY + 578, 28, 20, " >", Palette.CrtAmberPrimary);

        // Row 21: RESET STRAIN (15%) & FORCE STOKER OVL
        DrawDebugButton(mouse, PanelX + 8, PanelY + 600, 222, 20, $"RESET STRAIN ({_config.StokerIdleBaseline:0}%)", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 236, PanelY + 600, 224, 20, "FORCE STOKER OVL", Palette.AlertCrimson);

        // Row 22: BROWNOUT: AUTO / FORCE ON / FORCE DIM / FORCE OFF
        DrawDebugToggleButton(mouse, PanelX + 8, PanelY + 622, 98, 20, "BRWN: AUTO",
            _devDebug.BrownoutForceMode == DebugBrownoutForceMode.Auto, Palette.ClearanceTeal);
        DrawDebugToggleButton(mouse, PanelX + 110, PanelY + 622, 112, 20, "FORCE ON",
            _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOn, Palette.ClearanceTeal);
        DrawDebugToggleButton(mouse, PanelX + 226, PanelY + 622, 116, 20, "FORCE DIM",
            _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceDim, Palette.CrtAmberPrimary);
        DrawDebugToggleButton(mouse, PanelX + 346, PanelY + 622, 114, 20, "FORCE OFF",
            _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOff, Palette.AlertCrimson);

        // Row 23: Live EnableStokerDial toggle & POCKET CAP (1 / 2 / 3) stepper
        DrawDebugToggleButton(mouse, PanelX + 8, PanelY + 644, 222, 20,
            _config.EnableStokerDial ? "STOKER DIAL: ON" : "STOKER DIAL: OFF",
            _config.EnableStokerDial, Palette.FromHex("#bdb040"));
        DrawDebugButton(mouse, PanelX + 236, PanelY + 644, 28, 20, " <", Palette.CrtAmberPrimary);
        DrawStepperReadout(PanelX + 266, PanelY + 644, 164, 20, $"POCKET CAP: {_gameState.PocketCapacity}/3", Palette.ClearanceTeal);
        DrawDebugButton(mouse, PanelX + 432, PanelY + 644, 28, 20, " >", Palette.CrtAmberPrimary);

        // Right-Hand Telemetry Sidebar (x = PanelX + 468 .. PanelX + PanelW - 8)
        int sideX = PanelX + 468;
        int sideW = PanelW - 476;
        Raylib.DrawLine(PanelX + 464, PanelY + 88, PanelX + 464, PanelY + PanelH - 8, Palette.WeatheredGunmetalEdge);

        UiText.Draw("LIVE TELEMETRY", sideX, PanelY + 92, 10, Palette.ClearanceTeal, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"SAFE: {_devDebug.LiveStokerSafeLoad:0.0}%", 10, sideW, denseRow: true), sideX, PanelY + 110, 10, Palette.ClearanceTeal, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"SHK:  {_devDebug.LiveStokerShock:0.0}% (+{_devDebug.LiveStokerPreviewShock:0}%)", 10, sideW, denseRow: true), sideX, PanelY + 125, 10, Palette.CrtAmberPrimary, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"TOT:  {_devDebug.LiveStokerTotal:0.0}%", 10, sideW, denseRow: true), sideX, PanelY + 140, 10, stokerTelCol, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"BRWN: {_devDebug.BrownoutForceMode}", 10, sideW, denseRow: true), sideX, PanelY + 155, 10, Palette.CrtAmberDimmed, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"NEXT BRWN: {(_gameState.NextDayPowerBrownout ? "YES" : "NO")}", 10, sideW, denseRow: true), sideX, PanelY + 170, 10,
            _gameState.NextDayPowerBrownout ? Palette.AlertCrimson : Palette.CrtAmberDimmed, denseRow: true);

        string voiceReadout = $"VOICE: {AudioHooks.ActivePresentedSpecies}";
        UiText.Draw(UiText.TruncateToWidth(voiceReadout, 10, sideW, denseRow: true), sideX, PanelY + 196, 10, Palette.ClearanceTeal, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth($"BANK: {AudioHooks.ActiveVoiceBank} ({AudioHooks.ActiveEncounterBasePitch:0.00})", 10, sideW, denseRow: true), sideX, PanelY + 211, 10, Palette.CrtAmberDimmed, denseRow: true);

        int histCount = _gameState.EncounterHistory.Count;
        UiText.Draw($"HISTORY: {histCount}", sideX, PanelY + 236, 10, Palette.CrtAmberPrimary, denseRow: true);
        if (histCount == 0)
        {
            UiText.Draw("(none yet)", sideX, PanelY + 251, 10, Palette.CrtAmberDimmed, denseRow: true);
        }
        else
        {
            int startIdx = Math.Max(0, histCount - 5);
            int lineIdx = 0;
            for (int i = histCount - 1; i >= startIdx; i--)
            {
                EncounterRecord r = _gameState.EncounterHistory[i];
                string histLine = $"D{r.Day:D2} {r.Faction} {r.Verdict}";
                UiText.Draw(UiText.TruncateToWidth(histLine, 10, sideW, denseRow: true), sideX, PanelY + 251 + lineIdx * 14, 10, Palette.CrtAmberDimmed, denseRow: true);
                lineIdx++;
            }
        }

        UiText.Draw(UiText.TruncateToWidth($"CPS: {_config.DialogueTypewriterCharsPerSec:0}/{_config.TalkingHeadCharsPerSec:0}", 10, sideW, denseRow: true), sideX, PanelY + 628, 10, Palette.CrtAmberDimmed, denseRow: true);
        UiText.Draw(UiText.TruncateToWidth("[F1/`] CLOSE DEBUG", 10, sideW, denseRow: true), sideX, PanelY + 646, 10, Palette.WeatheredGunmetalEdge, denseRow: true);
    }

    private static void DrawDebugToggleButton(VirtualMouseState mouse, int x, int y, int w, int h, string label, bool active, Color activeCol)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, active || hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, active ? activeCol : (hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));
        string clipped = UiText.TruncateToWidth(label, 10, Math.Max(8, w - 8), denseRow: true);
        int tw = UiText.MeasureWidth(clipped, 10, denseRow: true);
        UiText.Draw(clipped, x + Math.Max(4, (w - tw) / 2), y + 5, 10, active ? activeCol : Palette.CrtAmberDimmed, denseRow: true);
    }

    private static void DrawStepperReadout(int x, int y, int w, int h, string label, Color textCol)
    {
        Raylib.DrawRectangle(x, y, w, h, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(x, y, w, h, Palette.WeatheredGunmetalEdge);
        string clipped = UiText.TruncateToWidth(label, 10, Math.Max(8, w - 8), denseRow: true);
        int tw = UiText.MeasureWidth(clipped, 10, denseRow: true);
        UiText.Draw(clipped, x + Math.Max(4, (w - tw) / 2), y + 5, 10, textCol, denseRow: true);
    }

    private static void DrawDebugButton(VirtualMouseState mouse, int x, int y, int w, int h, string label, Color textCol)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        string clipped = UiText.TruncateToWidth(label, 10, Math.Max(8, w - 10), denseRow: true);
        UiText.Draw(clipped, x + 5, y + 5, 10, textCol, denseRow: true);
    }
}
