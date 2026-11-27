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
    private readonly Core.ConfigFlags _config;

    public bool IsOpen { get; private set; }
    public TutorialEventHook ActiveHook { get; private set; } = TutorialEventHook.None;
    public bool OpenedFromPauseSummary { get; private set; }

    public TutorialOverlay(GameState gameState, Core.ConfigFlags? config = null)
    {
        _gameState = gameState;
        _config = config ?? new Core.ConfigFlags();
    }

    // =========================================================================
    // Reusable Shift-Start Event Hooks (Swap-Ready for Milestone 4 Comms)
    // =========================================================================
    public void OnDay1Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay1Start, force);

    public void OnDay2Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay2Start, force);

    public void OnDay3Start(bool force = false) => TryOpenHook(TutorialEventHook.OnDay3Start, force);

    public void OnFirstMorningQuarters(bool force = false) => TryOpenHook(TutorialEventHook.OnFirstMorningQuarters, force);

    public void OnFirstEveningQuarters(bool force = false) => TryOpenHook(TutorialEventHook.OnFirstEveningQuarters, force);

    public void TriggerDayStartHook(int day, bool force = false)
    {
        switch (TutorialFlags.GetHookForDay(day, _config.DayUnlockWarpScanner))
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
    /// Re-opens the context-appropriate rules &amp; controls card at any time (e.g., from ESC Pause Menu [?]).
    /// </summary>
    public void OpenCurrentDaySummary() => OpenSummaryForScreen(ScreenId.Desk);

    public void OpenSummaryForScreen(ScreenId currentScreen)
    {
        TutorialEventHook hook = currentScreen == ScreenId.Quarters
            ? (_gameState.QuartersPhase == QuartersPhase.Morning
                ? TutorialEventHook.OnFirstMorningQuarters
                : TutorialEventHook.OnFirstEveningQuarters)
            : TutorialFlags.GetHookForDay(_gameState.Day, _config.DayUnlockWarpScanner);
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

        TutorialCardContent card = BuildContent(ActiveHook, _gameState.Day, _gameState.TerminalHackedUnlocked, _config);

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
        UiText.Draw(card.HeaderTitle, bx + 14, by + 11, 10, Palette.CrtAmberPrimary);

        // Top-right [X] close button
        int closeX = bx + CardW - 28;
        int closeY = by + 6;
        bool closeHover = mouse.IsHit(closeX, closeY, 20, 20);
        Raylib.DrawRectangle(closeX, closeY, 20, 20, closeHover ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(closeX, closeY, 20, 20, closeHover ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        UiText.Draw("X", closeX + 7, closeY + 5, 10, closeHover ? Palette.OutlineVoid : Palette.CrtAmberPrimary);

        // Subheader Banner
        UiText.Draw(card.SubHeader, bx + 16, by + 36, 10, Palette.ClearanceTeal);
        Raylib.DrawLine(bx + 12, by + 52, bx + CardW - 12, by + 52, Palette.WeatheredGunmetalEdge);

        // Numbered Briefing Sections
        int curY = by + 62;
        int textMaxPx = CardW - 44;
        for (int i = 0; i < card.Sections.Length; i++)
        {
            TutorialSection sec = card.Sections[i];
            Raylib.DrawRectangle(bx + 14, curY - 2, CardW - 28, 14, new Color(20, 26, 34, 255));
            UiText.Draw(sec.Heading, bx + 20, curY, 10, sec.HeadingColor);
            curY += 16;

            List<string> wrapped = WrapTextByPixelWidth(sec.Body, 10, textMaxPx);
            foreach (string line in wrapped)
            {
                UiText.Draw(line, bx + 22, curY, 10, new Color(216, 204, 178, 255));
                curY += 13;
            }
            curY += 8;
        }

        // Bottom Tip + Acknowledge Button
        int dividerY = by + CardH - 80;
        Raylib.DrawLine(bx + 12, dividerY, bx + CardW - 12, dividerY, Palette.WeatheredGunmetalEdge);
        UiText.Draw(card.FooterTip, bx + 16, dividerY + 6, 10, Palette.CrtAmberDimmed);

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
        int bw = UiText.MeasureWidth(btnLabel, 10);
        UiText.Draw(btnLabel, btnX + Math.Max(8, (btnW - bw) / 2), btnY + 10, 10,
            btnHover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);

        const string escNote = "RE-OPEN ANYTIME FROM [ESC] PAUSE MENU -> [?] RULES / CONTROLS";
        int nw = UiText.MeasureWidth(escNote, 10);
        UiText.Draw(escNote, bx + (CardW - nw) / 2, by + CardH - 18, 10, Palette.WeatheredGunmetalEdge);
    }

    internal string GetActiveSummaryPlainText()
    {
        TutorialCardContent card = BuildContent(ActiveHook, _gameState.Day, _gameState.TerminalHackedUnlocked, _config);
        return string.Join("\n", card.Sections.Select(s => $"{s.Heading}: {s.Body}")) + "\n" + card.FooterTip;
    }

    private static TutorialCardContent BuildContent(TutorialEventHook hook, int currentDay, bool terminalHackedUnlocked, Core.ConfigFlags config)
    {
        int manualPart1 = config.DayUnlockManualPart1;
        int manualHulls = config.DayUnlockManualHulls;
        int stokerDay = config.DayUnlockStoker;

        if (hook == TutorialEventHook.OnDay3Start && currentDay >= stokerDay)
        {
            return new TutorialCardContent(
                $"DAY {currentDay:D2} RULES & CONTROLS // STELLAR STOKER & ACTIVE CHECKS [?]",
                $"STOKER ONLINE: {config.StokerIdleBaseline:0}% IDLE  |  {config.StokerSafeLoadCap:0}% SAFE CAP  |  +{config.StokerUncalibratedShock:0}% UNCALIBRATED SHOCK",
                [
                    new TutorialSection(
                        "1. STELLAR STOKER -- [ANCHOR] MASS & EXACT STRAIN MATH",
                        $"Shift starts at {config.StokerIdleBaseline:0}% idle SafeLoad. Calibrated clearances approach a {config.StokerSafeLoadCap:0}% safe cap (never overloading). Uncalibrated overweight hulls add +{config.StokerUncalibratedShock:0}% Shock; pair Data Slate MASS vs MEASURED LED in [PROTOCOL REVIEW] and Detain.",
                        Palette.FromHex("#bdb040")),
                    new TutorialSection(
                        "2. STOKER OVERLOAD (> 100%) -- 0 STRIKES + BROWNOUT CASCADE",
                        $"Crossing 100% strain assesses a -{config.StokerOverloadFine} SC structural fine, triggers a Sleep Sabotage Audit (0 Security Strikes added), and causes next-day 3-state Booth 04 power brownouts.",
                        Palette.AlertCrimson),
                    new TutorialSection(
                        "3. ALL PRIOR STATION PROTOCOLS REMAIN ENFORCED",
                        "Verify Transponder ID vs REG CODE, SPS Hot Weapons / Violet Cargo, Warp Strip origin, Manual Expiry / Org / Species / Hulls, and Cargo Manifest Cert-Wafers (3-wafer hopper).",
                        Palette.ClearanceTeal)
                ],
                "TIP: Pulling DETAIN drops any live +33% [ANCHOR] preview spike cleanly to 0%.");
        }

        return hook switch
        {
            TutorialEventHook.OnDay1Start => new TutorialCardContent(
                "DAY 01 RULES & CONTROLS // BOOTH 04 ORIENTATION [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  DAY 01 CHECK: REGISTRATION MATCH + PROTOCOL REVIEW",
                [
                    new TutorialSection(
                        "1. DAY 01 RULE -- TRANSPONDER ID vs. DATA SLATE REG CODE",
                        "Compare the raw TRANSPONDER ID shown below the Hailing Viewscreen against the REG CODE printed on the Primary Data Slate.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "2. AUTO-SHIELDS & GRANTING ACCESS (IF IDS MATCH)",
                        "Click [NEXT HAIL] to dock a vessel. Shielded ships automatically drop shields during hail before telemetry downloads. If IDs match, drag the Data Slate into CLEARANCE DOCK, wait for encryption, then return it to SLATE BUS.",
                        Palette.ClearanceTeal),
                    new TutorialSection(
                        "3. PROTOCOL REVIEW BEFORE DETAINING (IF IDS CONFLICT)",
                        "If TRANSPONDER ID and REG CODE conflict, click [PROTOCOL REVIEW], select both conflicting fields to confirm the fault, then pull the red DETAIN lever. Pulling DETAIN without confirming via PROTOCOL REVIEW first triggers a Brengop Clerical Warning.",
                        Palette.AlertCrimson)
                ],
                "TIP: 2 free clerical warnings per shift; mistakes beyond 2 deduct 10 SC."),

            TutorialEventHook.OnDay2Start => new TutorialCardContent(
                "DAY 02-03 RULES & CONTROLS // SPS PROBE & AUTO-WEAPONS [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  NEW HARDWARE: SPS SCANNER & AUTO-WEAPONS COMPLIANCE",
                [
                    new TutorialSection(
                        "1. SPS PROBE ARM & SWAYING RETICLE",
                        "Click [SPS PROBE] beside the viewport to extend the sensor arm. Shields automatically drop on hail so you can sweep the swaying reticle across internal weapon mounts and power conduits.",
                        Palette.SpsCommsActive),
                    new TutorialSection(
                        "2. AUTOMATIC WEAPONS COMPLIANCE ON SWEEP",
                        "Sweeping over hot weapon mounts automatically fires 'Power down weapons.' Honest armed ships reply and visibly cool inside the SPS lens, clearing any concern with zero manual command buttons.",
                        Palette.ClearanceTeal),
                    new TutorialSection(
                        "3. CATCHING SPOOFED HOT WEAPONS LIARS",
                        "Dishonest ships claiming WPN: OFFLINE stall while mounts stay orange-hot in SPS. Hold over the hot mount for 0.5s to lock [!] HOT WEAPONS onto the SPS LOG, then pair it vs WPN: OFFLINE in [PROTOCOL REVIEW] before detaining.",
                        Palette.AlertCrimson)
                ],
                "TIP: Day 03 is a breather watch before the Day 04 Warp Checker rollout."),

            TutorialEventHook.OnFirstMorningQuarters => new TutorialCardContent(
                "MORNING QUARTERS // PRE-SHIFT PREPARATION [PLAYTEST]",
                "SHIFT CLOCK OFF  |  PREPARE BEFORE WALKING CORRIDOR TO BOOTH 04",
                [
                    new TutorialSection(
                        "1. PERSONAL TERMINAL & DAY 2+ MORNING SHIP ROSTER",
                        "Click your Personal Terminal on the back-left desk. On Day 1 Morning the amber Unread LED blinks with your Brengop reassignment contract; from Day 2 Morning onward, check MESSAGES for the 10-ship Morning Ship Roster (1-3 actually arrive in your shift; rostered ships can still carry violations!).",
                        Palette.ClearanceTeal),
                    new TutorialSection(
                        "2. 0500 SERVICE CHUTE, COAT POCKET & UNDER-DESK VENT STASH",
                        "Overnight carryable orders arrive in the left-wall Service Chute at 0500. Click items in the Chute to pack your bottom Coat Pocket tray (synced with Booth 04's Lap Pocket), or click Coat Pocket items to hide contraband inside the Under-Desk Vent Stash.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "3. LEFT-WALL DOOR -> WALK TO BOOTH 04",
                        "Hover room objects for clean highlights and tooltips. When ready for your shift, click the Left-Wall Door to walk the Habitat Ring corridor into Booth 04.",
                        Palette.CrtAmberPrimary)
                ],
                "TIP: Press [ESC] anytime to close the Terminal or open the Pause Menu."),

            TutorialEventHook.OnFirstEveningQuarters => new TutorialCardContent(
                "EVENING QUARTERS // SURVIVAL, INTEL & SLEEP [PLAYTEST]",
                "BERTH LEASE AUTO-SETTLED AT CLOCK-OUT  |  PURE PACT SCRIP (SC) ECONOMY",
                [
                    new TutorialSection(
                        "1. PACT REQ (FLAT-SC RATIONS & LIVE SCRIP BALANCE)",
                        "Open your Personal Terminal -> PACT REQ to order tonight's rations using Pact Scrip (Dry Bricks 10 SC, Nutrient Paste 20 SC, High-Tier 30 SC). Food rations are the sole driver of next-shift SPS cursor sway.",
                        Palette.FromHex("#bdb040")),
                    new TutorialSection(
                        terminalHackedUnlocked
                            ? "2. CRYPTO / LOGS & DECK 4 (COVERT CONTRACTS & LOG SCRUBBING)"
                            : "2. MESSAGES, FORUM & STATION STANDING",
                        terminalHackedUnlocked
                            ? "Accept Delivery or Intercept contracts and order gear on DECK 4 (Audit Deferral clears 1 Strike for 50 SC). In CRYPTO / LOGS, DECRYPT [0 SC] and mission [TRANSMIT] write hex traces -- run SCRUB LOGS (15 SC) to keep your Vent Stash safe!"
                            : "Check MESSAGES for incoming transmissions and browse the Station FORUM for sector rumors and traffic developments. All official station tabs operate on your live Pact Scrip balance.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        "3. BED -> SLEEP & TWO-TIER KORDANI BERTH RAID",
                        $"Click the Bed to sleep into Morning Quarters. At 3/3 Security Strikes (Day {config.DayUnlockWarpScanner}+), Kordani raids your berth: Coat Frisk confiscates unshielded coat contraband (1 item safe with Lining); clean logs keep the Under-Desk Vent safe, while dirty logs trigger a Deep Sweep!",
                        Palette.AlertCrimson)
                ],
                "TIP: Hide contraband slates in the Vent Stash and scrub hex logs before sleeping at 3/3 Strikes."),

            _ => new TutorialCardContent(
                $"DAY {config.DayUnlockWarpScanner:D2} RULES & CONTROLS // STRIKES & GLOWING WARP CHECKER [PLAYTEST]",
                "SHIFT TIMER FROZEN  |  WARP SPECTROMETER & SECURITY STRIKES (0/3) ONLINE",
                [
                    new TutorialSection(
                        "1. KORDANI SECURITY STRIKES (0/3) & PROTOCOL REVIEW",
                        "Security Strikes activate today (3/3 triggers a Kordani Berth Audit on sleep). Continue confirming every fault via [PROTOCOL REVIEW] before detaining: Transponder ID, [!] HOT WEAPONS, or Falsified Origin Waveform.",
                        Palette.AlertCrimson),
                    new TutorialSection(
                        "2. WARP PRINTER & TRANSLUCENT GLOWING WARP CHECKER",
                        "After shields drop, click [SCAN WARP], drag to tear off the magnetic strip, and lay it 1:1 over the Glowing Warp Checker glass. Cycle < REF: SYSTEM >: matching curves glow bright amber/white; deviating segments bleed RED.",
                        Palette.CrtAmberPrimary),
                    new TutorialSection(
                        $"3. STAGGERED MANUAL ROLLOUT (DAYS {manualPart1:D2} & {manualHulls:D2})",
                        $"Days 05 & 07 are breather watches around the Day 06 Relay reveal. The Kordani Reference Manual unlocks on Day {manualPart1:D2} (Expiry & Prefix vs Org/Species), followed by Hull Silhouettes & Class on Day {manualHulls:D2}.",
                        Palette.ClearanceTeal)
                ],
                "TIP: Unverified detainments issue Brengop Clerical Warnings -- confirm faults first.")
        };
    }

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
            if (UiText.MeasureWidth(candidate, fontSize) <= maxPixelWidth)
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
