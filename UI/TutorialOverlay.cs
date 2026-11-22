using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Reusable Playtest Mini Tutorial Overlay built on explicit day-start event hooks
/// (OnDay1Start, OnDay2Start, OnDay3Start). Renders compact, dismissible CRT briefing cards
/// centered in 1280x720 while freezing the shift timer and desk inputs.
/// Designed so these temporary playtest cards can be swapped cleanly into in-character
/// comms dialogue in Milestone 4 without rewriting trigger call sites.
/// </summary>
public sealed class TutorialOverlay
{
    private const int CardW = 648;
    private const int CardH = 384;
    private static int CardX => (Constants.VirtualWidth - CardW) / 2;
    private static int CardY => (Constants.VirtualHeight - CardH) / 2;

    private sealed record TutorialSection(string Heading, string Body, Color HeadingColor);

    private sealed record TutorialCardContent(
        string HeaderTitle,
        string SubHeader,
        TutorialSection[] Sections,
        string FooterTip);

    private readonly GameState _gameState;

    public bool IsOpen { get; private set; }
    public TutorialEventHook ActiveHook { get; private set; } = TutorialEventHook.None;
    public bool OpenedFromPauseSummary { get; private set; }

    public TutorialOverlay(GameState gameState)
    {
        _gameState = gameState;
    }

    // =========================================================================
    // Reusable Shift-Start Event Hooks (Swap-Ready for Milestone 4 Comms)
    // =========================================================================
    public void OnDay1Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay1Start, force);

    public void OnDay2Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay2Start, force);

    public void OnDay3Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay3Start, force);

    public void TriggerDayStartHook(int day, bool force = false)
    {
        switch (TutorialFlags.GetHookForDay(day))
        {
            case TutorialEventHook.OnDay1Start:
                OnDay1Start(force);
                break;
            case TutorialEventHook.OnDay2Start:
                OnDay2Start(force);
                break;
            case TutorialEventHook.OnDay3Start:
                OnDay3Start(force);
                break;
        }
    }

    /// <summary>
    /// Re-opens the current day's rules & controls card at any time (e.g., from ESC Pause Menu [?]).
    /// </summary>
    public void OpenCurrentDaySummary()
    {
        TutorialEventHook hook = TutorialFlags.GetHookForDay(_gameState.Day);
        ActiveHook = hook;
        OpenedFromPauseSummary = true;
        IsOpen = true;
        AudioHooks.Play("terminal_key_tab");
    }

    private void TryOpenHook(TutorialEventHook hook, bool force)
    {
        _gameState.TutorialFlags ??= new TutorialFlags();
        if (!force)
        {
            if (!_gameState.TutorialFlags.TutorialsEnabled)
                return;
            if (_gameState.TutorialFlags.HasSeen(hook))
                return;
        }

        ActiveHook = hook;
        OpenedFromPauseSummary = false;
        IsOpen = true;
        AudioHooks.Play("terminal_key_tab");
    }

    public void Dismiss(bool markSeen = true)
    {
        if (!IsOpen)
            return;

        if (markSeen && ActiveHook != TutorialEventHook.None)
        {
            _gameState.TutorialFlags ??= new TutorialFlags();
            _gameState.TutorialFlags.MarkSeen(ActiveHook);
            _gameState.SaveToDisk();
        }

        IsOpen = false;
        OpenedFromPauseSummary = false;
        AudioHooks.Play("terminal_key_confirm");
    }

    public void ForceCloseSilent()
    {
        IsOpen = false;
        OpenedFromPauseSummary = false;
    }

    /// <summary>
    /// Consumes input while open so active gameplay updates and desk interactions remain frozen.
    /// </summary>
    public bool HandleInput(VirtualMouseState mouse)
    {
        if (!IsOpen)
            return false;

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) ||
            Raylib.IsKeyPressed(KeyboardKey.Space) ||
            Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Dismiss(markSeen: true);
            return true;
        }

        if (mouse.LeftPressed)
        {
            int bx = CardX;
            int by = CardY;
            int btnW = 280;
            int btnH = 30;
            int btnX = bx + (CardW - btnW) / 2;
            int btnY = by + CardH - 58;

            if (mouse.IsHit(btnX, btnY, btnW, btnH) ||
                mouse.IsHit(bx + CardW - 28, by + 6, 20, 20))
            {
                Dismiss(markSeen: true);
                return true;
            }
        }

        return true;
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!IsOpen || ActiveHook == TutorialEventHook.None)
            return;

        TutorialCardContent card = BuildContent(ActiveHook);

        // Dim CRT backdrop with subtle scanlines
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 198));
        for (int sy = 0; sy < Constants.VirtualHeight; sy += 4)
        {
            Raylib.DrawLine(0, sy, Constants.VirtualWidth, sy, new Color(15, 19, 25, 84));
        }

        int bx = CardX;
        int by = CardY;

        // Industrial CRT Card Frame
        Raylib.DrawRectangle(bx, by, CardW, CardH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(bx, by, CardW, CardH, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(bx + 3, by + 3, CardW - 6, CardH - 6, Palette.OxidizedBronzeWorn);

        // Header Bar
        Raylib.DrawRectangle(bx + 4, by + 4, CardW - 8, 24, Palette.OxidizedBronzeShadow);
        Raylib.DrawText(card.HeaderTitle, bx + 14, by + 11, 10, Palette.CrtAmberPrimary);

        // Top-right [X] close button
        int closeX = bx + CardW - 28;
        int closeY = by + 6;
        bool closeHover = mouse.IsHit(closeX, closeY, 20, 20);
        Raylib.DrawRectangle(closeX, closeY, 20, 20, closeHover ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(closeX, closeY, 20, 20, closeHover ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("X", closeX + 7, closeY + 5, 10, closeHover ? Palette.OutlineVoid : Palette.CrtAmberPrimary);

        // Subheader Banner
        Raylib.DrawText(card.SubHeader, bx + 16, by + 36, 10, Palette.ClearanceTeal);
        Raylib.DrawLine(bx + 12, by + 52, bx + CardW - 12, by + 52, Palette.WeatheredGunmetalEdge);

        // Numbered Briefing Sections
        int curY = by + 62;
        int textMaxPx = CardW - 44;
        for (int i = 0; i < card.Sections.Length; i++)
        {
            TutorialSection sec = card.Sections[i];
            Raylib.DrawRectangle(bx + 14, curY - 2, CardW - 28, 14, new Color(20, 26, 34, 255));
            Raylib.DrawText(sec.Heading, bx + 20, curY, 10, sec.HeadingColor);
            curY += 16;

            List<string> wrapped = WrapTextByPixelWidth(sec.Body, 10, textMaxPx);
            foreach (string line in wrapped)
            {
                Raylib.DrawText(line, bx + 22, curY, 10, new Color(216, 204, 178, 255));
                curY += 13;
            }
            curY += 8;
        }

        // Bottom Tip + Acknowledge Button
        int dividerY = by + CardH - 80;
        Raylib.DrawLine(bx + 12, dividerY, bx + CardW - 12, dividerY, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(card.FooterTip, bx + 16, dividerY + 6, 10, Palette.CrtAmberDimmed);

        int btnW = 280;
        int btnH = 30;
        int btnX = bx + (CardW - btnW) / 2;
        int btnY = by + CardH - 58;
        bool btnHover = mouse.IsHit(btnX, btnY, btnW, btnH);

        Raylib.DrawRectangle(btnX, btnY, btnW, btnH, btnHover ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(btnX, btnY, btnW, btnH, btnHover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        string btnLabel = OpenedFromPauseSummary
            ? "CLOSE BRIEFING [SPACE / ENTER]"
            : "UNDERSTOOD -- BEGIN WATCH [SPACE]";
        int bw = Raylib.MeasureText(btnLabel, 10);
        Raylib.DrawText(btnLabel, btnX + Math.Max(8, (btnW - bw) / 2), btnY + 10, 10,
            btnHover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);

        const string escNote = "RE-OPEN ANYTIME FROM [ESC] PAUSE MENU -> [?] RULES / CONTROLS";
        int nw = Raylib.MeasureText(escNote, 10);
        Raylib.DrawText(escNote, bx + (CardW - nw) / 2, by + CardH - 18, 10, Palette.WeatheredGunmetalEdge);
    }

    private static TutorialCardContent BuildContent(TutorialEventHook hook) =>
        hook switch
        {
            TutorialEventHook.OnDay1Start => new TutorialCardContent(
                "DAY 01 RULES & CONTROLS // BOOTH 04 ORIENTATION [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  SINGLE DAY 01 CHECK: REGISTRATION MATCH",
                [
                    new TutorialSection(
                        "1. DAY 01 RULE -- TRANSPONDER ID vs. DATA SLATE REG CODE",
                        "Compare the raw TRANSPONDER ID shown below the Hailing Viewscreen against the REG CODE printed on the Primary Data Slate.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "2. CALLING SHIPS & GRANTING ACCESS (IF IDS MATCH)",
                        "Click [NEXT HAIL] to bring a vessel into the bay and download its packet onto the Data Slate. If IDs match, drag the Data Slate into the right-side CLEARANCE DOCK, wait for encryption, then drag the cleared slate back up to the SLATE BUS to transmit clearance.",
                        Palette.ClearanceTeal),
                    new TutorialSection(
                        "3. DETAINING VIOLATORS (IF IDS DO NOT MATCH)",
                        "If TRANSPONDER ID and REG CODE conflict, pull the red DETAIN lever on the far left of the viewport to lock station clamps and wipe the Data Slate.",
                        Palette.AlertCrimson)
                ],
                "TIP: 2 free clerical warnings per shift; mistakes beyond 2 deduct 10 SC."),

            TutorialEventHook.OnDay2Start => new TutorialCardContent(
                "DAY 02 RULES & CONTROLS // COMBAT ORDERS & SPS PROBE [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  NEW HARDWARE: SHIELDS, WEAPONS & SPS SCANNER",
                [
                    new TutorialSection(
                        "1. LOWER SHIELDS & PWR DOWN WPN",
                        "Ships may now arrive with active shields or armed weapon mounts. Order [LOWER SHIELDS] and [PWR DOWN WPN] in the Comms Strip before granting clearance. Shields must be down to scan inside a hull.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "2. SPS PROBE ARM & SWAYING RETICLE",
                        "Click [SPS PROBE] beside the viewport to extend the sensor arm. Move the swaying reticle across the hull to inspect internal power conduits.",
                        Palette.SpsCommsActive),
                    new TutorialSection(
                        "3. DETECTING HOT WEAPONS & CATCHING LIARS WITH SPS",
                        "All armed ships show glowing hot orange weapon mounts inside the swaying SPS lens. Hover 0.5s to log [!] HOT WPN in the SPS LOG, then press [PWR DOWN WPN] under Comms. Honest captains cool down immediately; if a liar claiming WPN: OFFLINE stays hot, pull DETAIN.",
                        Palette.AlertCrimson)
                ],
                "TIP: Day 01 Transponder ID checks remain active alongside Day 02 SPS checks."),

            _ => new TutorialCardContent(
                "DAY 03 RULES & CONTROLS // PROTOCOL REVIEW, STRIKES & WARP CHECKER [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  FULL INSPECTION SUITE & SECURITY STRIKES ONLINE",
                [
                    new TutorialSection(
                        "1. MANDATORY PROTOCOL REVIEW & KORDANI SECURITY STRIKES",
                        "Before pulling DETAIN, click [PROTOCOL REVIEW] and pair the conflicting Claim (Data Slate field) with Reality (Transponder ID, Viewport Hull, Station Cycle, SPS LOG [!] HOT WPN / Cargo Draw, or Warp Strip). Security Strikes are active (3 = audit; 5 = detainment).",
                        Palette.AlertCrimson),
                    new TutorialSection(
                        "2. WARP PRINTER & TRANSLUCENT GLOWING WARP CHECKER",
                        "After shields drop, click [SCAN WARP], drag to tear off the magnetic strip, and lay it 1:1 over the Glowing Warp Checker glass. Cycle < REF: SYSTEM >: matching curves glow bright amber/white; deviating segments bleed RED.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "3. DESK REFERENCE MANUAL (1. PREFIXES & 2. HULLS)",
                        "Use the Reference Manual to cross-check Prefix/Organization pairings, permitted Captain Species, and Faction Hull & Class silhouettes against the Data Slate and Space Viewport.",
                        Palette.ClearanceTeal)
                ],
                "TIP: Unverified detainments issue clerical citations -- always run Protocol Review first.")
        };

    private static List<string> WrapTextByPixelWidth(string text, int fontSize, int maxPixelWidth)
    {
        List<string> lines = [];
        if (string.IsNullOrWhiteSpace(text))
            return lines;

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;

        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : $"{current} {word}";
            if (Raylib.MeasureText(candidate, fontSize) <= maxPixelWidth)
            {
                current = candidate;
            }
            else
            {
                if (current.Length > 0)
                    lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }
}
