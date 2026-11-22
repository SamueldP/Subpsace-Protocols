using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Officer Berth / Quarters fixed room view preview (1280x720, zero movable player sprite)
/// with Side Door Viewport, Personal Terminal placeholder, Food Replicator, Bed, and a clean
/// "START NEXT SHIFT" control that advances GameState.Day and resets daily warnings.
/// (Prompt 4 Quarters Economy intentionally deferred.)
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
        if (mouse.IsHit(980, 6, 104, 20))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }
        if (mouse.IsHit(1092, 6, 180, 20))
        {
            StartNextDayShift();
            return;
        }

        if (_terminalOpen)
        {
            if (mouse.IsHit(1012, 92, 84, 24))
            {
                _terminalOpen = false;
                AudioHooks.Play("terminal_power_off");
                return;
            }

            for (int i = 0; i < TerminalTabs.Length; i++)
            {
                int tabX = 184 + i * 184;
                if (mouse.IsHit(tabX, 128, 176, 26))
                {
                    _activeTerminalTab = i;
                    AudioHooks.Play("terminal_key_tab");
                    return;
                }
            }
            return;
        }

        // Fixed room interactive hotspots (1280x720)
        // 1. Side Door (Left)
        if (mouse.IsHit(40, 96, 220, 480))
        {
            _doorViewportOpen = !_doorViewportOpen;
            AudioHooks.Play("door_viewport_shutter");
        }
        // 2. Personal Terminal (Center-Left)
        else if (mouse.IsHit(300, 196, 300, 224))
        {
            _terminalOpen = true;
            AudioHooks.Play("terminal_power_on");
        }
        // 3. Food Replicator (Center-Right)
        else if (mouse.IsHit(640, 196, 260, 224))
        {
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
        else if (mouse.IsHit(940, 280, 300, 260))
        {
            StartNextDayShift();
        }
        // 5. Explicit START NEXT SHIFT button (Bottom Center)
        else if (mouse.IsHit(440, 620, 400, 48))
        {
            StartNextDayShift();
        }
    }

    private void StartNextDayShift()
    {
        AudioHooks.Play("bed_sleep_transition");
        _gameState.AdvanceToDay(_gameState.Day + 1);
        _screenManager.RouteViaCorridor(ScreenId.Desk);
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "locations/quarters_interior.png",
            0, 32, Constants.VirtualWidth, Constants.VirtualHeight - 32,
            Palette.WeatheredGunmetalMid,
            Palette.WeatheredGunmetalEdge,
            string.Empty);

        // Header Bar (1280x32)
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 32, Palette.OutlineVoid);
        Raylib.DrawLine(0, 31, Constants.VirtualWidth, 31, Palette.WeatheredGunmetalEdge);
        string strikesText = _gameState.Day >= 3 ? $"   |   STRIKES: {_gameState.SecurityStrikes}" : string.Empty;
        Raylib.DrawText(
            $"VG-3 // OFFICER BERTH QUARTERS   |   END OF DAY {_gameState.Day:D2}   |   SCRIP: {_gameState.PactScrip} SC   |   PU: {_gameState.PowerUnits}{strikesText}",
            14, 11, 10, Palette.CrtAmberPrimary);

        DrawButton(mouse, 980, 6, 104, 20, "MAIN MENU");
        DrawButton(mouse, 1092, 6, 180, 20, $"START DAY {_gameState.Day + 1:D2} -> DESK", Palette.ClearanceTeal);

        // 1. Side Door & Corridor Viewport
        DrawHotspot(mouse, 40, 96, 220, 480, "SIDE DOOR",
            _doorViewportOpen ? "VIEWPORT: [CORRIDOR CLEAR]" : "CLICK: PEEK VIEWPORT");

        // 2. Personal Terminal
        Raylib.DrawRectangle(408, 160, 84, 24, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(408, 160, 84, 24, Palette.CrtAmberPrimary);
        Raylib.DrawText("[! MSG]", 428, 167, 10, Palette.CrtAmberPrimary);

        DrawHotspot(mouse, 300, 196, 300, 224, "PERSONAL TERMINAL", "CLICK: 4:3 CONSOLE (PROMPT 4)");

        // 3. Food Replicator
        string foodTierLabel = _gameState.CurrentHungerTier switch
        {
            HungerTier.HighTierRations => "HIGH-GRADE (0 SWAY)",
            HungerTier.MediumNutrientPaste => "STANDARD (MED SWAY)",
            _ => "NO FOOD 2D+ (MAX SWAY)"
        };
        DrawHotspot(mouse, 640, 196, 260, 224, "FOOD REPLICATOR", $"TIER: {foodTierLabel}");

        // 4. Bed (Sleep & End Day)
        DrawHotspot(mouse, 940, 280, 300, 260, "BERTH BUNK (SLEEP)", $"SLEEP -> START DAY {_gameState.Day + 1:D2} WATCH");

        // 5. Prominent START NEXT SHIFT Button
        bool nextHover = mouse.IsHit(440, 620, 400, 48);
        Raylib.DrawRectangle(440, 620, 400, 48, nextHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(440, 620, 400, 48, Palette.ClearanceTeal);
        string nextLabel = $"START NEXT SHIFT  ->  DAY {_gameState.Day + 1:D2} (BOOTH 04)";
        int nw = Raylib.MeasureText(nextLabel, 10);
        Raylib.DrawText(nextLabel, 440 + (400 - nw) / 2, 639, 10, Palette.ClearanceTeal);

        if (_terminalOpen)
        {
            DrawPersonalTerminalModal(mouse);
        }
    }

    private void DrawPersonalTerminalModal(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(160, 76, 960, 520, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(160, 76, 960, 520, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(164, 80, 952, 512, Palette.CrtAmberDimmed);

        Raylib.DrawText("FTC PERSONAL BERTH TERMINAL // 4:3 DISPLAY (PROMPT 4 PENDING)", 184, 98, 10, Palette.CrtAmberPrimary);
        DrawButton(mouse, 1012, 92, 84, 24, "CLOSE [X]");

        for (int i = 0; i < TerminalTabs.Length; i++)
        {
            int tabX = 184 + i * 184;
            bool selected = _activeTerminalTab == i;
            bool hovered = mouse.IsHit(tabX, 128, 176, 26);
            Raylib.DrawRectangle(tabX, 128, 176, 26, selected ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(tabX, 128, 176, 26, selected || hovered ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            Raylib.DrawText(TerminalTabs[i], tabX + 12, 136, 10, selected ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        }

        Raylib.DrawRectangle(184, 164, 912, 408, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(184, 164, 912, 408, Palette.WeatheredGunmetalEdge);

        string tabInfo = _activeTerminalTab switch
        {
            0 => "MESSAGES (0 PU) -- FACTION CONTACTS & UNPROMPTED RECOGNITION",
            1 => "SECTOR NEWS (0 PU) -- DELAYED CAUSE-AND-EFFECT RIPPLE BULLETINS",
            2 => "CRYPTO & LOGS -- DECRYPT (3 PU/15 SC) | ENCRYPT (4 PU/20 SC) | SCRUB LOGS (5 PU/25 SC)",
            3 => "PACT REQUISITION (0 PU) -- NUTRIENT PASTE (20 SC) | RATIONS (30 SC) | AUDIT DEFERRAL (50 SC)",
            4 => "DECK 4 EXCHANGE (0 PU) -- BLANK SLATE (15 SC) | SHIELDED POCKET (90 SC) | MULTI-BUYER INTEL",
            _ => string.Empty
        };

        Raylib.DrawText(tabInfo, 204, 188, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText($"UNSCRUBBED LOGS: {_gameState.UnauthorizedLogCount} | PASSAGE FUND: {_gameState.IndependentPassageFund}/600 SC",
            204, 218, 10, Palette.ClearanceTeal);
    }

    private static void DrawHotspot(VirtualMouseState mouse, int x, int y, int w, int h, string title, string subtitle)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);
        Raylib.DrawText(title, x + 12, y + 14, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(subtitle, x + 12, y + 34, 10, Palette.ClearanceTeal);
    }

    private static void DrawButton(VirtualMouseState mouse, int x, int y, int w, int h, string text, Color? fg = null)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(text, x + 8, y + (h - 10) / 2, 10, fg ?? Palette.CrtAmberPrimary);
    }

    public void Unload() { }
}
