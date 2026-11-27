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
    private readonly DevDebugState? _devDebug;
    private SaveSlotSummary[] _slotSummaries = [];

    public MainMenuScreen(ScreenManager screenManager, GameState gameState, Core.ConfigFlags config, DevDebugState? devDebug = null)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;
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
                    // New save opens the 5-card intro before fading into Day 1 Morning Quarters (unless skipped in F1)
                    _screenManager.SwitchTo(_devDebug?.SkipIntroSequence == true ? ScreenId.Quarters : ScreenId.Intro);
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

        // Centered Title Header Panel (1280x720) with crisp 2x (128x128) app_icon.jpg Station Logo Emblem
        const int titleX = ContentX; // 300
        const int titleY = 38;
        const int titleW = ContentW; // 680
        const int titleH = 148;
        Raylib.DrawRectangle(titleX, titleY, titleW, titleH, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(titleX, titleY, titleW, titleH, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(titleX + 4, titleY + 4, titleW - 8, titleH - 8, Palette.OutlineVoid);

        int textPaneX = titleX + 12;
        int textPaneW = titleW - 24;

        if (AssetManager.TryGetAppLogoTexture(out Texture2D logoTex))
        {
            const int logoSize = 128; // Crisp 2x integer scale of 64x64 pixel-art logo
            int logoX = titleX + 12;
            int logoY = titleY + (titleH - logoSize) / 2;
            Raylib.DrawRectangle(logoX - 2, logoY - 2, logoSize + 4, logoSize + 4, Palette.OutlineVoid);
            Raylib.DrawTexturePro(
                logoTex,
                new System.Numerics.Vector2(0, 0) is var _ ? new Rectangle(0, 0, logoTex.Width, logoTex.Height) : default,
                new Rectangle(logoX, logoY, logoSize, logoSize),
                System.Numerics.Vector2.Zero,
                0f,
                Color.White);
            Raylib.DrawRectangleLines(logoX - 1, logoY - 1, logoSize + 2, logoSize + 2, Palette.CrtAmberDimmed);
            Raylib.DrawRectangleLines(logoX - 2, logoY - 2, logoSize + 4, logoSize + 4, Palette.OxidizedBronzeWorn);

            textPaneX = logoX + logoSize + 14;
            textPaneW = (titleX + titleW - 10) - textPaneX;
        }

        string titleStr = UiText.TruncateToWidth("SUBSPACE PROTOCOLS", 20, textPaneW - 8);
        int titleTw = UiText.MeasureWidth(titleStr, 20);
        UiText.Draw(titleStr, textPaneX + Math.Max(4, (textPaneW - titleTw) / 2), titleY + 28, 20, Palette.CrtAmberPrimary);

        string sub1 = UiText.TruncateToWidth("VOID GATE 3 (VG-3) // BOOTH 04 CUSTOMS & DOCKING TERMINAL", 10, textPaneW - 8, denseRow: true);
        int sub1W = UiText.MeasureWidth(sub1, 10, denseRow: true);
        UiText.Draw(sub1, textPaneX + Math.Max(4, (textPaneW - sub1W) / 2), titleY + 70, 10, Palette.CrtAmberDimmed, denseRow: true);

        string sub2 = UiText.TruncateToWidth("LEGACY FTC CONSORTIUM OS v4.08 -- BULWARK PACT OVERRIDE", 10, textPaneW - 8, denseRow: true);
        int sub2W = UiText.MeasureWidth(sub2, 10, denseRow: true);
        UiText.Draw(sub2, textPaneX + Math.Max(4, (textPaneW - sub2W) / 2), titleY + 96, 10, Palette.ClearanceTeal, denseRow: true);

        // Save Slot Panel Header
        UiText.Draw(
            "PORT OFFICER ASSIGNMENT PROFILES (3 SAVE SLOTS -- NEW SAVE STARTS ON DAY 01)",
            ContentX, 214, 10, Palette.CrtAmberPrimary);

        for (int i = 0; i < _slotSummaries.Length; i++)
        {
            SaveSlotSummary slot = _slotSummaries[i];
            int rowY = SlotStartY + i * SlotStepY;

            Raylib.DrawRectangle(ContentX, rowY, ContentW, SlotH, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(ContentX, rowY, ContentW, SlotH,
                slot.Exists ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalEdge);

            UiText.Draw($"SAVE SLOT {slot.SlotNumber:D2}", ContentX + 16, rowY + 9, 10, Palette.CrtAmberPrimary);

            if (!slot.Exists)
            {
                UiText.Draw("STATUS: [ EMPTY ]", ContentX + 16, rowY + 27, 10, Palette.WeatheredGunmetalEdge);
                DrawMenuButton(mouse, ContentX + 428, rowY + 10, 236, 28, "START NEW SAVE (DAY 01)", Palette.ClearanceTeal);
            }
            else
            {
                string phaseTag = slot.QuartersPhase == QuartersPhase.Morning ? "AM" : "PM";
                string strikesPart = (slot.SecurityStrikesActive || slot.Day >= _config.DayUnlockWarpScanner)
                    ? $"  |  STRIKES: {slot.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}"
                    : string.Empty;
                string summary = $"DAY {slot.Day:D2} ({phaseTag})  |  {slot.PactScrip} SC{strikesPart}";
                UiText.Draw(summary, ContentX + 16, rowY + 27, 10, Palette.ClearanceTeal);

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
        UiText.Draw(
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
        int tw = UiText.MeasureWidth(text, 10);
        UiText.Draw(text, x + Math.Max(8, (w - tw) / 2), y + (h - 10) / 2, 10, hovered ? Palette.CrtAmberPrimary : fg);
    }

    public void Unload() { }
}
