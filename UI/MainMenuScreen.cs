using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// 1280x720 Main Menu featuring 3 persistent Save Slots (%APPDATA%/SubspaceProtocols/save_slot_1..3.json)
/// with START NEW (always Day 1), LOAD, and DELETE controls, plus direct gallery/quarters routing.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private const int ContentX = 300;
    private const int ContentW = 680;
    private const int SlotStartY = 240;
    private const int SlotStepY = 60;
    private const int SlotH = 48;

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

        for (int i = 0; i < _slotSummaries.Length; i++)
        {
            SaveSlotSummary slot = _slotSummaries[i];
            int rowY = SlotStartY + i * SlotStepY;

            if (!slot.Exists)
            {
                if (mouse.IsHit(ContentX + 428, rowY + 10, 236, 28))
                {
                    AudioHooks.Play("terminal_key_confirm");
                    _gameState.SuppressDiskPersistence = false;
                    _gameState.ResetForNewSave(slot.SlotNumber);
                    RefreshSlots();
                    // Prompt 4: New Game strictly starts in Day 1 Morning Quarters (not directly on Desk)
                    _screenManager.SwitchTo(ScreenId.Quarters);
                    return;
                }
            }
            else
            {
                if (mouse.IsHit(ContentX + 428, rowY + 10, 128, 28))
                {
                    AudioHooks.Play("terminal_key_confirm");
                    _gameState.SuppressDiskPersistence = false;
                    if (_gameState.LoadSlotIntoCurrent(slot.SlotNumber))
                    {
                        _screenManager.SwitchTo(ScreenId.Quarters);
                    }
                    return;
                }

                if (mouse.IsHit(ContentX + 564, rowY + 10, 100, 28))
                {
                    AudioHooks.Play("citation_buzz");
                    GameState.DeleteSlot(slot.SlotNumber);
                    RefreshSlots();
                    return;
                }
            }
        }

        // Bottom Utility Buttons (y=444)
        if (mouse.IsHit(ContentX, 444, 330, 32))
        {
            AudioHooks.Play("terminal_key_confirm");
            _gameState.StartSandboxedBerthPreview();
            _screenManager.SwitchTo(ScreenId.Quarters);
        }
        else if (mouse.IsHit(ContentX + 350, 444, 330, 32))
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
        Raylib.DrawRectangle(1, 32, Constants.VirtualWidth - 2, 144, Palette.BrownDwarfGlowDeep);
        Raylib.DrawLine(1, 176, Constants.VirtualWidth - 2, 176, Palette.BrownDwarfGlowRim);

        // Centered Title Header Panel (1280x720)
        const int titleX = 340;
        const int titleY = 56;
        const int titleW = 600;
        const int titleH = 116;
        Raylib.DrawRectangle(titleX, titleY, titleW, titleH, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(titleX, titleY, titleW, titleH, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(titleX + 4, titleY + 4, titleW - 8, titleH - 8, Palette.OutlineVoid);

        const string titleStr = "SUBSPACE PROTOCOLS";
        int titleTw = Raylib.MeasureText(titleStr, 20);
        Raylib.DrawText(titleStr, titleX + (titleW - titleTw) / 2, titleY + 20, 20, Palette.CrtAmberPrimary);

        const string sub1 = "VOID GATE 3 (VG-3) // BOOTH 04 CUSTOMS & DOCKING TERMINAL";
        int sub1W = Raylib.MeasureText(sub1, 10);
        Raylib.DrawText(sub1, titleX + (titleW - sub1W) / 2, titleY + 56, 10, Palette.CrtAmberDimmed);

        const string sub2 = "LEGACY FTC CONSORTIUM OS v4.08 -- BULWARK PACT OVERRIDE";
        int sub2W = Raylib.MeasureText(sub2, 10);
        Raylib.DrawText(sub2, titleX + (titleW - sub2W) / 2, titleY + 80, 10, Palette.ClearanceTeal);

        // Save Slot Panel Header
        Raylib.DrawText(
            "PORT OFFICER ASSIGNMENT PROFILES (3 SAVE SLOTS -- NEW SAVE STARTS ON DAY 01)",
            ContentX, 214, 10, Palette.CrtAmberPrimary);

        for (int i = 0; i < _slotSummaries.Length; i++)
        {
            SaveSlotSummary slot = _slotSummaries[i];
            int rowY = SlotStartY + i * SlotStepY;

            Raylib.DrawRectangle(ContentX, rowY, ContentW, SlotH, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(ContentX, rowY, ContentW, SlotH,
                slot.Exists ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalEdge);

            Raylib.DrawText($"SAVE SLOT {slot.SlotNumber:D2}", ContentX + 16, rowY + 9, 10, Palette.CrtAmberPrimary);

            if (!slot.Exists)
            {
                Raylib.DrawText("STATUS: [ EMPTY ]", ContentX + 16, rowY + 27, 10, Palette.WeatheredGunmetalEdge);
                DrawMenuButton(mouse, ContentX + 428, rowY + 10, 236, 28, "START NEW SAVE (DAY 01)", Palette.ClearanceTeal);
            }
            else
            {
                string phaseTag = slot.QuartersPhase == QuartersPhase.Morning ? "AM" : "PM";
                string strikesPart = slot.Day >= 3 ? $"  |  STRIKES: {slot.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}" : string.Empty;
                string summary = $"DAY {slot.Day:D2} ({phaseTag})  |  {slot.PactScrip} SC{strikesPart}";
                Raylib.DrawText(summary, ContentX + 16, rowY + 27, 10, Palette.ClearanceTeal);

                DrawMenuButton(mouse, ContentX + 428, rowY + 10, 128, 28, "LOAD SAVE", Palette.CrtAmberPrimary);
                DrawMenuButton(mouse, ContentX + 564, rowY + 10, 100, 28, "DELETE", Palette.AlertCrimson);
            }
        }

        // Secondary Dev/Preview Tools
        DrawMenuButton(mouse, ContentX, 444, 330, 32, "OFFICER QUARTERS (BERTH PREVIEW)", Palette.CrtAmberDimmed);
        DrawMenuButton(mouse, ContentX + 350, 444, 330, 32, "PROCEDURAL ART & PALETTE GALLERY", Palette.CrtAmberDimmed);

        // Footer bar (pure ASCII)
        Raylib.DrawRectangle(0, Constants.VirtualHeight - 28, Constants.VirtualWidth, 28, Palette.OutlineVoid);
        Raylib.DrawLine(0, Constants.VirtualHeight - 28, Constants.VirtualWidth, Constants.VirtualHeight - 28, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"ACTIVE SLOT: {_gameState.ActiveSaveSlot:D2}  |  DAY {_gameState.Day:D2}  |  1280x720 NATIVE  |  F1 / `: DEV DEBUG & DAY JUMP  |  F11 / ALT+ENTER: FULLSCREEN",
            20, Constants.VirtualHeight - 19, 10, Palette.CrtAmberDimmed);
    }

    private static void DrawMenuButton(VirtualMouseState mouse, int x, int y, int w, int h, string text, Color fg)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int tw = Raylib.MeasureText(text, 10);
        Raylib.DrawText(text, x + Math.Max(8, (w - tw) / 2), y + (h - 10) / 2, 10, hovered ? Palette.CrtAmberPrimary : fg);
    }

    public void Unload() { }
}
