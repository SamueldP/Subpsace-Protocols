using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Officer Berth / Quarters fixed room view (960x540, zero movable player sprite)
/// with Side Door Viewport, Personal Terminal, Food Replicator, Bed, and a clean
/// "START NEXT SHIFT" control that advances GameState.Day and resets daily warnings.
/// </summary>
public sealed class QuartersScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private bool _terminalOpen;
    private int _activeTerminalTab;
    private bool _doorViewportOpen;

    private static readonly string[] TerminalTabs =
    [
        "1.MESSAGES",
        "2.NEWS",
        "3.CRYPTO/LOGS",
        "4.PACT REQ",
        "5.DECK 4 EXCH"
    ];

    public QuartersScreen(ScreenManager screenManager, GameState gameState)
    {
        _screenManager = screenManager;
        _gameState = gameState;
    }

    public void OnEnter()
    {
        _terminalOpen = false;
        _doorViewportOpen = false;
        _activeTerminalTab = 0;
    }

    public void OnExit() { }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        // Top header navigation
        if (mouse.IsHit(712, 4, 86, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }
        if (mouse.IsHit(804, 4, 146, 16))
        {
            StartNextDayShift();
            return;
        }

        if (_terminalOpen)
        {
            if (mouse.IsHit(772, 64, 72, 20))
            {
                _terminalOpen = false;
                AudioHooks.Play("terminal_power_off");
                return;
            }

            for (int i = 0; i < TerminalTabs.Length; i++)
            {
                int tabX = 122 + i * 142;
                if (mouse.IsHit(tabX, 94, 136, 22))
                {
                    _activeTerminalTab = i;
                    AudioHooks.Play("terminal_key_tab");
                    return;
                }
            }
            return;
        }

        // Fixed room interactive hotspots
        // 1. Side Door (Left)
        if (mouse.IsHit(28, 72, 164, 380))
        {
            _doorViewportOpen = !_doorViewportOpen;
            AudioHooks.Play("door_viewport_shutter");
        }
        // 2. Personal Terminal (Center-Left)
        else if (mouse.IsHit(222, 146, 228, 176))
        {
            _terminalOpen = true;
            AudioHooks.Play("terminal_power_on");
        }
        // 3. Food Replicator (Center-Right)
        else if (mouse.IsHit(480, 146, 188, 176))
        {
            // Cycle placeholder ration tier for playtesting food -> SPS sway loop
            _gameState.CurrentHungerTier = _gameState.CurrentHungerTier switch
            {
                HungerTier.LowNoFood => HungerTier.MediumNutrientPaste,
                HungerTier.MediumNutrientPaste => HungerTier.HighTierRations,
                _ => HungerTier.LowNoFood
            };
            _gameState.DaysWithoutFood = _gameState.CurrentHungerTier == HungerTier.LowNoFood ? 2 : 0;
            _gameState.SaveToDisk();
            AudioHooks.Play("food_replicator_chime");
        }
        // 4. Bed (Right -- Sleep & Start Next Day Shift)
        else if (mouse.IsHit(698, 220, 234, 210))
        {
            StartNextDayShift();
        }
        // 5. Explicit START NEXT SHIFT button (Bottom Center)
        else if (mouse.IsHit(310, 474, 340, 38))
        {
            StartNextDayShift();
        }
    }

    private void StartNextDayShift()
    {
        AudioHooks.Play("bed_sleep_transition");
        // Cleanly increment GameState.Day, reset daily warnings to 0, apply overnight Day 3 logic & persist save.json
        _gameState.AdvanceToDay(_gameState.Day + 1);
        _screenManager.RouteViaCorridor(ScreenId.Desk);
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "locations/quarters_interior.png",
            0, 24, Constants.VirtualWidth, Constants.VirtualHeight - 24,
            Palette.WeatheredGunmetalMid,
            Palette.WeatheredGunmetalEdge,
            string.Empty);

        // Header Bar
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 23, Constants.VirtualWidth, 23, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"VG-3 // OFFICER BERTH QUARTERS   |   END OF DAY {_gameState.Day:D2}   |   SCRIP: {_gameState.PactScrip} SC   |   PU: {_gameState.PowerUnits}   |   STRIKES: {_gameState.SecurityStrikes}",
            10, 7, 10, Palette.CrtAmberPrimary);

        DrawButton(mouse, 712, 4, 86, 16, "MAIN MENU");
        DrawButton(mouse, 804, 4, 146, 16, $"START DAY {_gameState.Day + 1:D2} -> DESK", Palette.ClearanceTeal);

        // 1. Side Door & Corridor Viewport
        DrawHotspot(mouse, 28, 72, 164, 380, "SIDE DOOR",
            _doorViewportOpen ? "VIEWPORT: [CORRIDOR CLEAR]" : "CLICK: PEEK VIEWPORT");

        // 2. Personal Terminal
        Raylib.DrawRectangle(296, 114, 78, 22, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(296, 114, 78, 22, Palette.CrtAmberPrimary);
        Raylib.DrawText("[! MSG]", 314, 120, 10, Palette.CrtAmberPrimary);

        DrawHotspot(mouse, 222, 146, 228, 176, "PERSONAL TERMINAL", "CLICK: 4:3 CONSOLE (5 TABS)");

        // 3. Food Replicator (Cycles food tier for testing SPS sway)
        string foodTierLabel = _gameState.CurrentHungerTier switch
        {
            HungerTier.HighTierRations => "HIGH-GRADE (0 SWAY)",
            HungerTier.MediumNutrientPaste => "STANDARD (MED SWAY)",
            _ => "NO FOOD 2D+ (MAX SWAY)"
        };
        DrawHotspot(mouse, 480, 146, 188, 176, "FOOD REPLICATOR", $"TIER: {foodTierLabel}");

        // 4. Bed (Sleep & End Day)
        DrawHotspot(mouse, 698, 220, 234, 210, "BERTH BUNK (SLEEP)", $"SLEEP -> START DAY {_gameState.Day + 1:D2} WATCH");

        // 5. Prominent START NEXT SHIFT Button (Fixes Day 1 loop cleanly)
        bool nextHover = mouse.IsHit(310, 474, 340, 38);
        Raylib.DrawRectangle(310, 474, 340, 38, nextHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(310, 474, 340, 38, Palette.ClearanceTeal);
        Raylib.DrawText($"START NEXT SHIFT  ->  DAY {_gameState.Day + 1:D2} (BOOTH 04)", 354, 488, 10, Palette.ClearanceTeal);

        if (_terminalOpen)
        {
            DrawPersonalTerminalModal(mouse);
        }
    }

    private void DrawPersonalTerminalModal(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(104, 52, 752, 412, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(104, 52, 752, 412, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(108, 56, 744, 404, Palette.CrtAmberDimmed);

        Raylib.DrawText("FTC PERSONAL BERTH TERMINAL // 4:3 DISPLAY (PLACEHOLDER -- PROMPT 4 PENDING)", 122, 68, 10, Palette.CrtAmberPrimary);
        DrawButton(mouse, 772, 64, 72, 20, "CLOSE [X]");

        for (int i = 0; i < TerminalTabs.Length; i++)
        {
            int tabX = 122 + i * 142;
            bool selected = _activeTerminalTab == i;
            bool hovered = mouse.IsHit(tabX, 94, 136, 22);
            Raylib.DrawRectangle(tabX, 94, 136, 22, selected ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(tabX, 94, 136, 22, selected || hovered ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            Raylib.DrawText(TerminalTabs[i], tabX + 10, 100, 10, selected ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        }

        Raylib.DrawRectangle(122, 124, 716, 322, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(122, 124, 716, 322, Palette.WeatheredGunmetalEdge);

        string tabInfo = _activeTerminalTab switch
        {
            0 => "MESSAGES (0 PU) -- FACTION CONTACTS & UNPROMPTED RECOGNITION",
            1 => "SECTOR NEWS (0 PU) -- DELAYED CAUSE-AND-EFFECT RIPPLE BULLETINS",
            2 => "CRYPTO & LOGS -- DECRYPT (3 PU/15 SC) | ENCRYPT (4 PU/20 SC) | SCRUB LOGS (5 PU/25 SC)",
            3 => "PACT REQUISITION (0 PU) -- NUTRIENT PASTE (20 SC) | RATIONS (30 SC) | AUDIT DEFERRAL (50 SC)",
            4 => "DECK 4 EXCHANGE (0 PU) -- BLANK SLATE (15 SC) | SHIELDED POCKET (90 SC) | MULTI-BUYER INTEL",
            _ => string.Empty
        };

        Raylib.DrawText(tabInfo, 140, 144, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText($"UNSCRUBBED LOGS: {_gameState.UnauthorizedLogCount} | PASSAGE FUND: {_gameState.IndependentPassageFund}/600 SC",
            140, 174, 10, Palette.ClearanceTeal);
    }

    private static void DrawHotspot(VirtualMouseState mouse, int x, int y, int w, int h, string title, string subtitle)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);
        Raylib.DrawText(title, x + 10, y + 12, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(subtitle, x + 10, y + 30, 10, Palette.ClearanceTeal);
    }

    private static void DrawButton(VirtualMouseState mouse, int x, int y, int w, int h, string text, Color? fg = null)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(text, x + 6, y + 3, 10, fg ?? Palette.CrtAmberPrimary);
    }

    public void Unload() { }
}
