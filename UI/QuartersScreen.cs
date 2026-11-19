using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Officer Berth / Quarters fixed room view (zero movable player sprite)
/// with Side Door Viewport, Bed, Food Replicator, and 4:3 Personal Terminal (5 tabs).
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
        if (mouse.IsHit(468, 4, 76, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }
        if (mouse.IsHit(548, 4, 86, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Desk);
            return;
        }

        if (_terminalOpen)
        {
            // Close terminal button
            if (mouse.IsHit(510, 44, 62, 18))
            {
                _terminalOpen = false;
                AudioHooks.Play("terminal_power_off");
                return;
            }

            // 5 Terminal Tabs
            for (int i = 0; i < TerminalTabs.Length; i++)
            {
                int tabX = 72 + i * 98;
                if (mouse.IsHit(tabX, 68, 94, 18))
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
        if (mouse.IsHit(20, 56, 108, 250))
        {
            _doorViewportOpen = !_doorViewportOpen;
            AudioHooks.Play("door_viewport_shutter");
        }
        // 2. Personal Terminal (Center-Left, 4:3 Horizontal monitor)
        else if (mouse.IsHit(154, 110, 152, 120))
        {
            _terminalOpen = true;
            AudioHooks.Play("terminal_power_on");
        }
        // 3. Food Replicator (Center-Right)
        else if (mouse.IsHit(326, 110, 124, 120))
        {
            AudioHooks.Play("food_replicator_chime");
        }
        // 4. Bed (Right — Sleep & End Day -> Walk to Booth 04)
        else if (mouse.IsHit(466, 154, 154, 152))
        {
            AudioHooks.Play("bed_sleep_transition");
            _screenManager.RouteViaCorridor(ScreenId.Desk);
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        // Background static room view
        AssetManager.DrawImageOrPlaceholder(
            "locations/quarters_interior.png",
            0, 24, Constants.VirtualWidth, Constants.VirtualHeight - 24,
            Palette.WeatheredGunmetalMid,
            Palette.WeatheredGunmetalEdge,
            "STATIC ASSET: locations/quarters_interior.png (FIXED ROOM - NO PLAYER SPRITE)");

        // Header Bar
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 23, Constants.VirtualWidth, 23, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"VG-3 // OFFICER BERTH QUARTERS | DAY {_gameState.Day:D2} | SCRIP: {_gameState.PactScrip} SC | PU: {_gameState.PowerUnits} | LEASE: 30 SC",
            8, 7, 10, Palette.CrtAmberPrimary);

        DrawButton(mouse, 468, 4, 76, 16, "MAIN MENU");
        DrawButton(mouse, 548, 4, 86, 16, "WALK -> DESK");

        // 1. Side Door & Corridor Viewport
        DrawHotspot(mouse, 20, 56, 108, 250, "SIDE DOOR",
            _doorViewportOpen ? "VIEWPORT: [CORRIDOR CLEAR]" : "CLICK: PEEK VIEWPORT");

        // 2. Personal Terminal (with unread status icon above)
        Raylib.DrawRectangle(200, 84, 60, 18, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(200, 84, 60, 18, Palette.CrtAmberPrimary);
        Raylib.DrawText("[! MSG]", 210, 88, 10, Palette.CrtAmberPrimary);

        DrawHotspot(mouse, 154, 110, 152, 120, "PERSONAL TERMINAL", "CLICK: 4:3 CONSOLE (5 TABS)");

        // 3. Food Replicator
        DrawHotspot(mouse, 326, 110, 124, 120, "FOOD REPLICATOR",
            $"TIER: {_gameState.CurrentHungerTier}");

        // 4. Bed (Sleep & End Day)
        DrawHotspot(mouse, 466, 154, 154, 152, "BERTH BUNK (BED)", "CLICK: SLEEP & NEXT SHIFT");

        // 4:3 Personal Terminal Modal Overlay
        if (_terminalOpen)
        {
            DrawPersonalTerminalModal(mouse);
        }
    }

    private void DrawPersonalTerminalModal(VirtualMouseState mouse)
    {
        // 4:3 Horizontal Terminal Frame (520 x 284)
        Raylib.DrawRectangle(60, 36, 520, 288, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(60, 36, 520, 288, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(63, 39, 514, 282, Palette.CrtAmberDimmed);

        Raylib.DrawText("FTC PERSONAL BERTH TERMINAL // 4:3 DISPLAY", 74, 48, 10, Palette.CrtAmberPrimary);
        DrawButton(mouse, 510, 44, 62, 18, "CLOSE [X]");

        for (int i = 0; i < TerminalTabs.Length; i++)
        {
            int tabX = 72 + i * 98;
            bool selected = _activeTerminalTab == i;
            bool hovered = mouse.IsHit(tabX, 68, 94, 18);
            Raylib.DrawRectangle(tabX, 68, 94, 18, selected ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(tabX, 68, 94, 18, selected || hovered ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            Raylib.DrawText(TerminalTabs[i], tabX + 6, 72, 10, selected ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        }

        Raylib.DrawRectangle(72, 92, 496, 218, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(72, 92, 496, 218, Palette.WeatheredGunmetalEdge);

        string tabInfo = _activeTerminalTab switch
        {
            0 => "MESSAGES (0 PU) — FACTION CONTACTS & UNPROMPTED RECOGNITION",
            1 => "SECTOR NEWS (0 PU) — DELAYED CAUSE-AND-EFFECT RIPPLE BULLETINS",
            2 => "CRYPTO & LOGS — DECRYPT (3 PU/15 SC) | ENCRYPT (4 PU/20 SC) | SCRUB LOGS (5 PU/25 SC)",
            3 => "PACT REQUISITION (0 PU) — NUTRIENT PASTE (20 SC) | RATIONS (30 SC) | AUDIT DEFERRAL (50 SC)",
            4 => "DECK 4 EXCHANGE (0 PU) — BLANK SLATE (15 SC) | SHIELDED POCKET (90 SC) | MULTI-BUYER INTEL",
            _ => string.Empty
        };

        Raylib.DrawText(tabInfo, 84, 108, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText($"UNSCRUBBED LOGS: {_gameState.UnauthorizedLogCount} | PASSAGE FUND: {_gameState.IndependentPassageFund}/600 SC",
            84, 132, 10, Palette.ClearanceTeal);
    }

    private static void DrawHotspot(VirtualMouseState mouse, int x, int y, int w, int h, string title, string subtitle)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);
        Raylib.DrawText(title, x + 6, y + 8, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(subtitle, x + 6, y + 24, 10, Palette.ClearanceTeal);
    }

    private static void DrawButton(VirtualMouseState mouse, int x, int y, int w, int h, string text)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(text, x + 6, y + 3, 10, Palette.CrtAmberPrimary);
    }

    public void Unload() { }
}
