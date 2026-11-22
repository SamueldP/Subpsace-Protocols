using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Themed CRT Pause Menu overlay toggled via ESC or the Desk Top-Bar Pause Button during active gameplay.
/// Freezes the 270-second shift timer and all screen animations while open,
/// offering RESUME, SETTINGS (display mode + audio placeholder controls),
/// RETURN TO MAIN MENU, and QUIT TO DESKTOP.
/// </summary>
public sealed class PauseMenuOverlay
{
    private const int BoxW = 420;
    private const int BoxH = 322;
    private static int BoxX => (Constants.VirtualWidth - BoxW) / 2;
    private static int BoxY => (Constants.VirtualHeight - BoxH) / 2;

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly VirtualCanvas _virtualCanvas;
    private readonly Action? _onOpenTutorialSummary;

    private bool _showingSettings;

    public bool IsOpen { get; private set; }
    public bool ShouldQuitApplication { get; private set; }

    public PauseMenuOverlay(
        ScreenManager screenManager,
        GameState gameState,
        VirtualCanvas virtualCanvas,
        Action? onOpenTutorialSummary = null)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _virtualCanvas = virtualCanvas;
        _onOpenTutorialSummary = onOpenTutorialSummary;
    }

    public bool CanToggleOnCurrentScreen() =>
        _screenManager.CurrentScreenId != ScreenId.MainMenu;

    public void Open()
    {
        if (!CanToggleOnCurrentScreen())
            return;

        IsOpen = true;
        _showingSettings = false;
        AudioHooks.Play("terminal_key_tab");
    }

    public void Toggle()
    {
        if (!CanToggleOnCurrentScreen())
            return;

        if (!IsOpen)
        {
            Open();
        }
        else
        {
            Close();
            AudioHooks.Play("terminal_key_confirm");
        }
    }

    public void Close()
    {
        IsOpen = false;
        _showingSettings = false;
    }

    /// <summary>
    /// Processes ESC toggle and Pause Menu button clicks.
    /// Returns true while the Pause Menu is open so the main loop freezes
    /// active gameplay updates and blocks input from passing through.
    /// </summary>
    public bool HandleInput(VirtualMouseState mouse)
    {
        if (!CanToggleOnCurrentScreen())
        {
            if (IsOpen)
            {
                Close();
            }
            return false;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (!IsOpen)
            {
                Open();
                return true;
            }

            if (_showingSettings)
            {
                _showingSettings = false;
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            Close();
            AudioHooks.Play("terminal_key_confirm");
            return true;
        }

        if (!IsOpen)
            return false;

        if (!mouse.LeftPressed)
            return true;

        int bx = BoxX;
        int by = BoxY;
        int btnX = bx + 40;
        int btnW = BoxW - 80;

        if (!_showingSettings)
        {
            // 1. RESUME
            if (mouse.IsHit(btnX, by + 74, btnW, 32))
            {
                AudioHooks.Play("terminal_key_confirm");
                Close();
                return true;
            }

            // 2. [?] RULES / CONTROLS (re-opens current day's tutorial summary card)
            if (mouse.IsHit(btnX, by + 116, btnW, 32))
            {
                Close();
                _onOpenTutorialSummary?.Invoke();
                return true;
            }

            // 3. SETTINGS
            if (mouse.IsHit(btnX, by + 158, btnW, 32))
            {
                AudioHooks.Play("terminal_key_tab");
                _showingSettings = true;
                return true;
            }

            // 4. RETURN TO MAIN MENU
            if (mouse.IsHit(btnX, by + 200, btnW, 32))
            {
                AudioHooks.Play("terminal_key_confirm");
                Close();
                _screenManager.SwitchTo(ScreenId.MainMenu);
                return true;
            }

            // 5. QUIT TO DESKTOP
            if (mouse.IsHit(btnX, by + 242, btnW, 32))
            {
                AudioHooks.Play("terminal_key_confirm");
                ShouldQuitApplication = true;
                return true;
            }
        }
        else
        {
            // Settings Row 1: Display Mode Toggle (Fullscreen / Windowed)
            if (mouse.IsHit(btnX, by + 76, btnW, 30))
            {
                _virtualCanvas.ToggleFullscreen();
                return true;
            }

            // Settings Row 2: Master Volume (- / +)
            if (mouse.IsHit(btnX, by + 120, 48, 30))
            {
                AudioHooks.MasterVolume = Math.Clamp(MathF.Round((AudioHooks.MasterVolume - 0.10f) * 10f) / 10f, 0f, 1f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (mouse.IsHit(btnX + btnW - 48, by + 120, 48, 30))
            {
                AudioHooks.MasterVolume = Math.Clamp(MathF.Round((AudioHooks.MasterVolume + 0.10f) * 10f) / 10f, 0f, 1f);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Settings Row 3: Mute Toggle (Audio Stubs)
            if (mouse.IsHit(btnX, by + 164, btnW, 30))
            {
                AudioHooks.IsMuted = !AudioHooks.IsMuted;
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            // Settings Row 4: BACK
            if (mouse.IsHit(btnX, by + 236, btnW, 32))
            {
                AudioHooks.Play("terminal_key_click");
                _showingSettings = false;
                return true;
            }
        }

        return true;
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!IsOpen)
            return;

        // Dim CRT backdrop with subtle horizontal scanlines
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 204));
        for (int sy = 0; sy < Constants.VirtualHeight; sy += 4)
        {
            Raylib.DrawLine(0, sy, Constants.VirtualWidth, sy, new Color(15, 19, 25, 90));
        }

        int bx = BoxX;
        int by = BoxY;

        // Outer Industrial Frame
        Raylib.DrawRectangle(bx, by, BoxW, BoxH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(bx, by, BoxW, BoxH, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(bx + 3, by + 3, BoxW - 6, BoxH - 6, Palette.OxidizedBronzeWorn);

        // Header Bar
        Raylib.DrawRectangle(bx + 4, by + 4, BoxW - 8, 24, Palette.OxidizedBronzeShadow);
        string title = _showingSettings
            ? "BOOTH 04 TERMINAL // SETTINGS"
            : "VG-3 WATCH TERMINAL // PAUSED";
        Raylib.DrawText(title, bx + 16, by + 11, 10, Palette.CrtAmberPrimary);

        string subLine = $"SLOT {_gameState.ActiveSaveSlot:D2}  |  DAY {_gameState.Day:D2}  |  TIMER & ANIMATIONS FROZEN";
        Raylib.DrawText(subLine, bx + 16, by + 38, 10, Palette.ClearanceTeal);
        Raylib.DrawLine(bx + 12, by + 56, bx + BoxW - 12, by + 56, Palette.WeatheredGunmetalEdge);

        int btnX = bx + 40;
        int btnW = BoxW - 80;

        if (!_showingSettings)
        {
            DrawPauseButton(mouse, btnX, by + 74, btnW, 32, "RESUME WATCH [ESC]", Palette.ClearanceTeal);
            DrawPauseButton(mouse, btnX, by + 116, btnW, 32, "[?] RULES / CONTROLS", Palette.CrtAmberPrimary);
            DrawPauseButton(mouse, btnX, by + 158, btnW, 32, "SETTINGS", Palette.CrtAmberPrimary);
            DrawPauseButton(mouse, btnX, by + 200, btnW, 32, "RETURN TO MAIN MENU", Palette.CrtAmberPrimary);
            DrawPauseButton(mouse, btnX, by + 242, btnW, 32, "QUIT TO DESKTOP", Palette.AlertCrimson);

            const string footer = "PRESS [ESC] TO RESUME SHIFT";
            int fw = Raylib.MeasureText(footer, 10);
            Raylib.DrawText(footer, bx + (BoxW - fw) / 2, by + BoxH - 24, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            string displayLabel = _virtualCanvas.IsBorderlessFullscreen
                ? "DISPLAY: BORDERLESS FULLSCREEN"
                : "DISPLAY: WINDOWED (1280x720)";
            DrawPauseButton(mouse, btnX, by + 76, btnW, 30, displayLabel, Palette.ClearanceTeal);

            // Volume Stepper Row
            DrawPauseButton(mouse, btnX, by + 120, 48, 30, " - ", Palette.CrtAmberPrimary);
            Raylib.DrawRectangle(btnX + 52, by + 120, btnW - 104, 30, Palette.DeepSpaceVoid);
            Raylib.DrawRectangleLines(btnX + 52, by + 120, btnW - 104, 30, Palette.WeatheredGunmetalEdge);
            int volPct = (int)MathF.Round(AudioHooks.MasterVolume * 100f);
            string volText = $"VOLUME: {volPct}% (STUB)";
            int volW = Raylib.MeasureText(volText, 10);
            Raylib.DrawText(volText, btnX + 52 + (btnW - 104 - volW) / 2, by + 130, 10, Palette.CrtAmberPrimary);
            DrawPauseButton(mouse, btnX + btnW - 48, by + 120, 48, 30, " + ", Palette.CrtAmberPrimary);

            string muteLabel = AudioHooks.IsMuted
                ? "AUDIO STUBS: MUTED"
                : "AUDIO STUBS: ACTIVE (NO SFX BUNDLED)";
            DrawPauseButton(mouse, btnX, by + 164, btnW, 30, muteLabel,
                AudioHooks.IsMuted ? Palette.AlertCrimson : Palette.CrtAmberDimmed);

            DrawPauseButton(mouse, btnX, by + 236, btnW, 32, "< BACK TO PAUSE MENU [ESC]", Palette.ClearanceTeal);
            const string hint = "F11 / ALT+ENTER ALSO TOGGLES FULLSCREEN";
            int hw = Raylib.MeasureText(hint, 10);
            Raylib.DrawText(hint, bx + (BoxW - hw) / 2, by + BoxH - 24, 10, Palette.CrtAmberDimmed);
        }
    }

    private static void DrawPauseButton(VirtualMouseState mouse, int x, int y, int w, int h, string label, Color accent)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Color fg = hovered ? Palette.CrtAmberPrimary : accent;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int tw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, x + Math.Max(6, (w - tw) / 2), y + (h - 10) / 2, 10, fg);
    }
}
