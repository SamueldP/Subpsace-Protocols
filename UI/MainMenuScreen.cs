using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 960x540 Main Menu featuring 3 persistent Save Slots (%APPDATA%/SubspaceProtocols/save_slot_1..3.json)
/// with START NEW (always Day 1), LOAD, and DELETE controls, plus direct gallery/quarters routing.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private SaveSlotSummary[] _slotSummaries = [];

    public MainMenuScreen(ScreenManager screenManager, GameState gameState, Core.ConfigFlags config)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
    }

    public void OnEnter()
    {
        RefreshSlots();
    }

    public void OnExit() { }

    private void RefreshSlots()
    {
        _slotSummaries = GameState.GetAllSlotSummaries();
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        // 3 Save Slot Rows (x=180..780, y=196, 250, 304, height=44)
        for (int i = 0; i < _slotSummaries.Length; i++)
        {
            SaveSlotSummary slot = _slotSummaries[i];
            int rowY = 196 + i * 54;

            if (!slot.Exists)
            {
                // [START NEW (DAY 01)] button (x=548, y=rowY+9, w=216, h=26)
                if (mouse.IsHit(548, rowY + 9, 216, 26))
                {
                    AudioHooks.Play("terminal_key_confirm");
                    _gameState.ResetForNewSave(slot.SlotNumber);
                    RefreshSlots();
                    _screenManager.RouteViaCorridor(ScreenId.Desk);
                    return;
                }
            }
            else
            {
                // [LOAD] button (x=548, y=rowY+9, w=116, h=26)
                if (mouse.IsHit(548, rowY + 9, 116, 26))
                {
                    AudioHooks.Play("terminal_key_confirm");
                    if (_gameState.LoadSlotIntoCurrent(slot.SlotNumber))
                    {
                        _screenManager.RouteViaCorridor(ScreenId.Desk);
                    }
                    return;
                }

                // [DELETE] button (x=672, y=rowY+9, w=92, h=26)
                if (mouse.IsHit(672, rowY + 9, 92, 26))
                {
                    AudioHooks.Play("citation_buzz");
                    GameState.DeleteSlot(slot.SlotNumber);
                    RefreshSlots();
                    return;
                }
            }
        }

        // Bottom Utility Buttons (y=382, 422)
        if (mouse.IsHit(180, 384, 290, 28))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.Quarters);
        }
        else if (mouse.IsHit(490, 384, 290, 28))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.ArtTest);
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "ui/main_menu_banner.png",
            0, 0, Constants.VirtualWidth, Constants.VirtualHeight,
            Palette.DeepSpaceVoid,
            Palette.WeatheredGunmetalEdge,
            string.Empty);

        // Dim Brown Dwarf horizon wash across top backdrop
        Raylib.DrawRectangle(1, 24, Constants.VirtualWidth - 2, 118, Palette.BrownDwarfGlowDeep);
        Raylib.DrawLine(1, 142, Constants.VirtualWidth - 2, 142, Palette.BrownDwarfGlowRim);

        // Centered Title Header Panel (960x540)
        Raylib.DrawRectangle(220, 40, 520, 108, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(220, 40, 520, 108, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(224, 44, 512, 100, Palette.OutlineVoid);

        Raylib.DrawText("SUBSPACE PROTOCOLS", 342, 58, 20, Palette.CrtAmberPrimary);
        Raylib.DrawText("VOID GATE 3 (VG-3) // BOOTH 04 CUSTOMS & DOCKING TERMINAL", 282, 90, 10, Palette.CrtAmberDimmed);
        Raylib.DrawText("LEGACY FTC CONSORTIUM OS v4.08 -- BULWARK PACT OVERRIDE", 286, 112, 10, Palette.ClearanceTeal);

        // Save Slot Panel Header
        Raylib.DrawText("PORT OFFICER ASSIGNMENT PROFILES (3 SAVE SLOTS -- NEW SAVE STARTS ON DAY 01)", 180, 174, 10, Palette.CrtAmberPrimary);

        for (int i = 0; i < _slotSummaries.Length; i++)
        {
            SaveSlotSummary slot = _slotSummaries[i];
            int rowY = 196 + i * 54;

            Raylib.DrawRectangle(180, rowY, 600, 44, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(180, rowY, 600, 44, slot.Exists ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalEdge);

            string slotHeader = $"SAVE SLOT {slot.SlotNumber:D2}";
            Raylib.DrawText(slotHeader, 196, rowY + 8, 10, Palette.CrtAmberPrimary);

            if (!slot.Exists)
            {
                Raylib.DrawText("STATUS: [ EMPTY ]", 196, rowY + 24, 10, Palette.WeatheredGunmetalEdge);
                DrawMenuButton(mouse, 548, rowY + 9, 216, 26, "START NEW SAVE (DAY 01)", Palette.ClearanceTeal);
            }
            else
            {
                string summary = $"DAY {slot.Day:D2}  |  {slot.PactScrip} SC  |  STRIKES: {slot.SecurityStrikes}";
                Raylib.DrawText(summary, 196, rowY + 24, 10, Palette.ClearanceTeal);

                DrawMenuButton(mouse, 548, rowY + 9, 116, 26, "LOAD SAVE", Palette.CrtAmberPrimary);
                DrawMenuButton(mouse, 672, rowY + 9, 92, 26, "DELETE", Palette.AlertCrimson);
            }
        }

        // Secondary Dev/Preview Tools
        DrawMenuButton(mouse, 180, 384, 290, 28, "OFFICER QUARTERS (BERTH PREVIEW)", Palette.CrtAmberDimmed);
        DrawMenuButton(mouse, 490, 384, 290, 28, "PROCEDURAL ART & PALETTE GALLERY", Palette.CrtAmberDimmed);

        // Footer bar (pure ASCII)
        Raylib.DrawRectangle(0, Constants.VirtualHeight - 26, Constants.VirtualWidth, 26, Palette.OutlineVoid);
        Raylib.DrawLine(0, Constants.VirtualHeight - 26, Constants.VirtualWidth, Constants.VirtualHeight - 26, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"ACTIVE SLOT: {_gameState.ActiveSaveSlot:D2}  |  DAY {_gameState.Day:D2}  |  960x540 NATIVE  |  F1 / `: DEV DEBUG & DAY JUMP  |  F11 / ALT+ENTER: FULLSCREEN",
            16, Constants.VirtualHeight - 17, 10, Palette.CrtAmberDimmed);
    }

    private static void DrawMenuButton(VirtualMouseState mouse, int x, int y, int w, int h, string text, Color fg)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        Raylib.DrawText(text, x + 12, y + (h - 10) / 2, 10, hovered ? Palette.CrtAmberPrimary : fg);
    }

    public void Unload() { }
}
