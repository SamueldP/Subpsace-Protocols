using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

public sealed record FieldBookletPage(
    string Tag,
    string HeaderTitle,
    string BodyText,
    string ScreenshotAssetKey,
    string ScreenshotPlaceholderLabel);

/// <summary>
/// Draggable, minimizable warm pen-and-paper "884-D — MY NOTES" Field Booklet shared
/// across Booth 04 (Desk) and Berth 04 (Quarters). Zero top connector pins (visually distinct
/// from the dark-steel Data Slate and Brengop Reference Manual) and progressively unlocks
/// pages by day and narrative events without ever blocking gameplay.
/// </summary>
public sealed class FieldBookletWidget
{
    public const string CoverTitle = "884-D \u2014 MY NOTES";
    public const int ClosedW = 208;
    public const int ClosedH = 44;
    public const int OpenW = 316;
    public const int OpenH = 264;

    // Warm paper & ink palette (pen-and-paper physical notebook)
    private static readonly Color KraftCoverBg = Palette.FromHex("#bda27a");
    private static readonly Color KraftCoverInner = Palette.FromHex("#ccb38c");
    private static readonly Color KraftSpine = Palette.FromHex("#5d4732");
    private static readonly Color KraftBorder = Palette.FromHex("#463423");
    private static readonly Color PaperCream = Palette.FromHex("#ebdfc6");
    private static readonly Color PaperRuleLine = Palette.FromHex("#d3c2a4");
    private static readonly Color PaperMarginRule = Palette.FromHex("#b9736b");
    private static readonly Color InkDark = Palette.FromHex("#231c15");
    private static readonly Color InkDim = Palette.FromHex("#5c4c3b");
    private static readonly Color InkRedPen = Palette.FromHex("#8f2d24");

    public bool IsOpen { get; set; }
    public int PageIndex { get; set; }

    public int CurrentWidth => IsOpen ? OpenW : ClosedW;
    public int CurrentHeight => IsOpen ? OpenH : ClosedH;

    public bool ContainsPoint(int mx, int my, int x, int y) =>
        mx >= x && mx < x + CurrentWidth &&
        my >= y && my < y + CurrentHeight;

    public void SyncWithGameState(GameState state, Core.ConfigFlags config, bool jumpToNewestOnNewUnlock = true)
    {
        List<FieldBookletPage> pages = GetUnlockedPages(state, config);
        if (pages.Count == 0)
        {
            PageIndex = 0;
            state.ActiveBookletPageIndex = 0;
            return;
        }

        if (jumpToNewestOnNewUnlock && pages.Count > state.SeenBookletPageCount)
        {
            state.SeenBookletPageCount = pages.Count;
            state.ActiveBookletPageIndex = pages.Count - 1;
        }
        else if (state.SeenBookletPageCount <= 0)
        {
            state.SeenBookletPageCount = pages.Count;
        }

        PageIndex = Math.Clamp(state.ActiveBookletPageIndex, 0, pages.Count - 1);
        state.ActiveBookletPageIndex = PageIndex;
    }

    public static List<FieldBookletPage> GetUnlockedPages(GameState state, Core.ConfigFlags config)
    {
        List<FieldBookletPage> pages =
        [
            new(
                "day1_shift",
                "DAY 01 // TRANSPONDER & CLEARANCE",
                "Check TRANSPONDER ID under the viewscreen against REG CODE on the Data Slate. If matching, upload encrypted clearance (Clearance Dock -> Slate Bus). If mismatching, enter Protocol Review to flag and execute Detain.",
                "booklet/day1_shift.png",
                "CRT REF // TRANSPONDER vs REG CODE")
        ];

        if (state.Day > 1 || (state.Day == 1 && state.QuartersPhase == QuartersPhase.Evening))
        {
            pages.Add(new(
                "day1_evening",
                "DAY 01 PM // LEASE & RATIONS",
                "Berth lease (-30 SC) deducts automatically at Clock-Out. Order nightly rations in PACT REQ before sleeping to prevent SPS sway.",
                "booklet/day1_quarters.png",
                "CRT REF // PACT REQ -> RATIONS"));
        }

        if (state.Day > 2 || (state.Day == 2 && state.Day2NixaHailCompleted))
        {
            pages.Add(new(
                "day2_sps",
                "DAY 02 // SPS WEAPONS SWEEP",
                "1. Press SPS button. 2. Sweep ship with SPS probe. 3. Hot weapons trigger mandatory order \"Power down weapons.\" If weapons remain powered, hold to lock [!] HOT WEAPONS on the mini-screen, flag in Protocol Review, and Detain.",
                "booklet/day2_sps.png",
                "CRT REF // SPS PROBE -> HOT WPN"));
        }

        if (state.Day > config.DayUnlockWarpScanner ||
            (state.Day == config.DayUnlockWarpScanner && state.Day4PreShiftHailCompleted))
        {
            pages.Add(new(
                "day4_warp",
                "DAY 04 // WARP CHECKER & STRIKES",
                "Warp Printer scan -> tear off strip -> align over Glowing Warp Checker (< REF: SYSTEM >). Red bleed or restricted Ashen Rift requires Detain. /3 Security Strikes system active.",
                "booklet/day4_warp.png",
                "CRT REF // < REF: SYSTEM > ALIGN"));
        }

        if (state.TerminalHackedUnlocked)
        {
            (string targetReg, _) = Simulation.ShipGenerator.EnsureDay6IntroTargetAssigned(state);
            pages.Add(new(
                "day6_crypto",
                "DAY 06 // BLANK SLATE & CRYPTO",
                $"TARGET RIG: {targetReg}\nSlot Blank Data Slate into Slate Bus before protocols hit -> capture packet -> re-transmit onto Primary Slate -> transmit in CRYPTOGRAPHY tab tonight and scrub logs before sleeping.",
                "booklet/day6_crypto.png",
                "CRT REF // CRYPTOGRAPHY TAB"));
        }

        if (state.Day > config.DayUnlockManualPart1 ||
            (state.Day == config.DayUnlockManualPart1 && state.Day8NixaHailCompleted))
        {
            pages.Add(new(
                "day8_manual",
                "DAY 08 // BRENGOP MANUAL",
                "Open Brengop Reference Manual -> check EXP: CYC against Station Cycle -> click prefix in SHIP REGISTRAR -> verify organization and species in SPECIES REGISTRAR.",
                "booklet/day8_manual.png",
                "CRT REF // SHIP & SPECIES REG"));
        }

        if (state.Day >= config.DayUnlockManualHulls)
        {
            pages.Add(new(
                "day10_hulls",
                "DAY 10 // HULL SILHOUETTES",
                "Open HULLS tab -> verify viewport hull silhouette matches declared ship class and prefix.",
                "booklet/day10_hulls.png",
                "CRT REF // HULLS SILHOUETTE"));
        }

        if (state.Day >= config.DayUnlockWaferCargo)
        {
            pages.Add(new(
                "day12_cargo",
                "DAY 12 // WAFERS & CARGO",
                "Wafer Writer prints a Cargo Manifest per ship. Check CARGO tab for the 5 banned items and sweep cargo bays on SPS for Violet [!] POWERED CARGO. Wipe used wafers in the top slot (3 blanks).",
                "booklet/day12_cargo.png",
                "CRT REF // WAFER & VIOLET CARGO"));
        }

        if (state.Day > config.DayUnlockStoker ||
            (state.Day == config.DayUnlockStoker && state.Day15ForemanHailCompleted))
        {
            pages.Add(new(
                "day15_stoker",
                "DAY 15 // STELLAR STOKER",
                "Press [ANCHOR] to check MEASURED tons against Data Slate MASS. Flag undeclared mass in Protocol Review and Detain. Keep the dial out of the red band to avoid overload fines and brownouts.",
                "booklet/day15_stoker.png",
                "CRT REF // [ANCHOR] & STRAIN DIAL"));
        }

        return pages;
    }

    public static string? GetUnlockTagForDay(int day, Core.ConfigFlags config)
    {
        if (day == 1) return "day1_shift";
        if (day == 2) return "day2_sps";
        if (day == config.DayUnlockWarpScanner) return "day4_warp";
        if (day == config.DayUnlockManualPart1) return "day8_manual";
        if (day == config.DayUnlockManualHulls) return "day10_hulls";
        if (day == config.DayUnlockWaferCargo) return "day12_cargo";
        if (day == config.DayUnlockStoker) return "day15_stoker";
        return null;
    }

    public bool SelectPageByTag(string tag, GameState state, Core.ConfigFlags config)
    {
        List<FieldBookletPage> pages = GetUnlockedPages(state, config);
        state.SeenBookletPageCount = Math.Max(state.SeenBookletPageCount, pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i].Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                PageIndex = i;
                state.ActiveBookletPageIndex = i;
                return true;
            }
        }
        return false;
    }

    public FieldBookletPage CurrentPage(GameState state, Core.ConfigFlags config)
    {
        List<FieldBookletPage> pages = GetUnlockedPages(state, config);
        int idx = Math.Clamp(PageIndex, 0, Math.Max(0, pages.Count - 1));
        return pages[idx];
    }

    /// <summary>
    /// Handles internal button clicks (minimize [-], page nav &lt; / &gt;, or minimized cover open).
    /// Returns true if an internal control consumed the click; returns false so drags can start on the booklet body.
    /// </summary>
    public bool HandleInternalClick(VirtualMouseState mouse, int x, int y, GameState state, Core.ConfigFlags config)
    {
        List<FieldBookletPage> pages = GetUnlockedPages(state, config);
        SyncWithGameState(state, config, jumpToNewestOnNewUnlock: true);

        if (!IsOpen)
        {
            if (mouse.IsHit(x + ClosedW - 44, y + 10, 36, 24))
            {
                IsOpen = true;
                if (pages.Count > 0 && state.SeenBookletPageCount <= pages.Count)
                {
                    state.SeenBookletPageCount = pages.Count;
                }
                PageIndex = Math.Clamp(state.ActiveBookletPageIndex, 0, Math.Max(0, pages.Count - 1));
                state.ActiveBookletPageIndex = PageIndex;
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            return false;
        }

        // Minimize [-] button in top-right corner of header
        if (mouse.IsHit(x + OpenW - 28, y + 3, 22, 16))
        {
            IsOpen = false;
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        // Prev / Next page buttons in top page strip (y + 24 .. y + 40)
        if (mouse.IsHit(x + 16, y + 24, 24, 16))
        {
            if (pages.Count > 1)
            {
                PageIndex = (PageIndex - 1 + pages.Count) % pages.Count;
                state.ActiveBookletPageIndex = PageIndex;
                AudioHooks.Play("terminal_key_tab");
            }
            return true;
        }

        if (mouse.IsHit(x + OpenW - 30, y + 24, 24, 16))
        {
            if (pages.Count > 1)
            {
                PageIndex = (PageIndex + 1) % pages.Count;
                state.ActiveBookletPageIndex = PageIndex;
                AudioHooks.Play("terminal_key_tab");
            }
            return true;
        }

        return false;
    }

    public void Draw(VirtualMouseState mouse, int x, int y, GameState state, Core.ConfigFlags config, bool isTopmost)
    {
        List<FieldBookletPage> pages = GetUnlockedPages(state, config);
        SyncWithGameState(state, config, jumpToNewestOnNewUnlock: true);

        if (!IsOpen)
        {
            DrawMinimizedCover(mouse, x, y, pages.Count, isTopmost);
            return;
        }

        FieldBookletPage page = pages[PageIndex];

        // Drop shadow + warm paper notebook shell (zero top connector pins)
        Raylib.DrawRectangle(x + 3, y + 3, OpenW, OpenH, new Color(6, 8, 12, 175));
        Raylib.DrawRectangle(x, y, OpenW, OpenH, KraftCoverBg);
        Raylib.DrawRectangle(x + 12, y + 4, OpenW - 16, OpenH - 8, PaperCream);
        Raylib.DrawRectangle(x, y, 12, OpenH, KraftSpine);

        // Stitched notebook binding marks along left spine
        for (int sy = y + 10; sy < y + OpenH - 10; sy += 16)
        {
            Raylib.DrawRectangle(x + 3, sy, 6, 4, KraftCoverInner);
            Raylib.DrawRectangleLines(x + 2, sy - 1, 8, 6, KraftBorder);
        }

        Raylib.DrawRectangleLines(x, y, OpenW, OpenH, isTopmost ? Palette.CrtAmberPrimary : KraftBorder);

        // Header bar ("884-D — MY NOTES" + [-] minimize button)
        Raylib.DrawRectangle(x + 12, y + 2, OpenW - 14, 18, KraftCoverInner);
        Raylib.DrawLine(x + 12, y + 20, x + OpenW - 2, y + 20, KraftBorder);
        UiText.Draw(CoverTitle, x + 18, y + 6, 10, InkDark, denseRow: true);

        bool minHov = mouse.IsHit(x + OpenW - 28, y + 3, 22, 16);
        Raylib.DrawRectangle(x + OpenW - 28, y + 3, 22, 16, minHov ? KraftSpine : KraftCoverBg);
        Raylib.DrawRectangleLines(x + OpenW - 28, y + 3, 22, 16, KraftBorder);
        UiText.Draw("[-]", x + OpenW - 25, y + 6, 10, minHov ? PaperCream : InkDark, denseRow: true);

        // Page navigation strip (< PAGE X/Y >)
        bool prevHov = mouse.IsHit(x + 16, y + 24, 24, 16);
        bool nextHov = mouse.IsHit(x + OpenW - 30, y + 24, 24, 16);
        Raylib.DrawRectangle(x + 16, y + 24, 24, 16, prevHov ? KraftCoverInner : PaperCream);
        Raylib.DrawRectangleLines(x + 16, y + 24, 24, 16, KraftBorder);
        UiText.Draw("<", x + 25, y + 27, 10, InkDark, denseRow: true);

        Raylib.DrawRectangle(x + OpenW - 30, y + 24, 24, 16, nextHov ? KraftCoverInner : PaperCream);
        Raylib.DrawRectangleLines(x + OpenW - 30, y + 24, 24, 16, KraftBorder);
        UiText.Draw(">", x + OpenW - 21, y + 27, 10, InkDark, denseRow: true);

        string pageCountLabel = $"PAGE {PageIndex + 1}/{pages.Count}";
        int pW = UiText.MeasureWidth(pageCountLabel, 10, denseRow: true);
        UiText.Draw(pageCountLabel, x + 16 + (OpenW - 24 - pW) / 2, y + 27, 10, InkRedPen, denseRow: true);

        // Ruled notebook lines inside writing zone (12px dense pitch for zero cutoff on long pages)
        int contentX = x + 18;
        int contentMaxW = OpenW - 26;
        for (int ly = y + 54; ly <= y + 162; ly += 12)
        {
            Raylib.DrawLine(x + 13, ly, x + OpenW - 5, ly, PaperRuleLine);
        }
        Raylib.DrawLine(x + 24, y + 43, x + 24, y + 174, PaperMarginRule);

        // Handwritten pen section title + wrapped notes
        UiText.Draw(TruncateToWidth(page.HeaderTitle, 10, contentMaxW - 10, denseRow: true), contentX + 8, y + 44, 10, InkRedPen, denseRow: true);
        List<string> lines = WrapToWidth(page.BodyText, 10, contentMaxW - 10, maxLines: 9, denseRow: true);
        int textY = y + 56;
        for (int i = 0; i < lines.Count; i++)
        {
            Color lineColor = lines[i].StartsWith("TARGET RIG:", StringComparison.OrdinalIgnoreCase) ? InkRedPen : InkDark;
            UiText.Draw(lines[i], contentX + 8, textY + i * 12, 10, lineColor, denseRow: true);
        }

        // CRT-style screenshot placeholder box (AssetManager PNG hook) at bottom of page
        int boxX = x + 18;
        int boxY = y + OpenH - 82;
        int boxW = OpenW - 26;
        const int boxH = 70;

        Raylib.DrawRectangle(boxX - 2, boxY - 2, boxW + 4, boxH + 4, KraftSpine);
        AssetManager.DrawImageOrPlaceholder(
            page.ScreenshotAssetKey,
            boxX, boxY, boxW, boxH,
            Palette.DeepSpaceVoid,
            InkDark,
            page.ScreenshotPlaceholderLabel);
        for (int sy = boxY + 2; sy < boxY + boxH - 1; sy += 4)
        {
            Raylib.DrawLine(boxX + 1, sy, boxX + boxW - 2, sy, new Color(8, 14, 18, 56));
        }
        Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, InkDark);
        UiText.Draw(TruncateToWidth(page.ScreenshotPlaceholderLabel, 10, boxW - 10, denseRow: true), boxX + 5, boxY + boxH - 13, 10, Palette.CrtAmberDimmed, denseRow: true);
    }

    private static void DrawMinimizedCover(VirtualMouseState mouse, int x, int y, int unlockedCount, bool isTopmost)
    {
        bool hov = mouse.IsHit(x, y, ClosedW, ClosedH);
        Raylib.DrawRectangle(x + 2, y + 2, ClosedW, ClosedH, new Color(6, 8, 12, 165));
        Raylib.DrawRectangle(x, y, ClosedW, ClosedH, hov ? KraftCoverInner : KraftCoverBg);
        Raylib.DrawRectangle(x, y, 10, ClosedH, KraftSpine);
        for (int sy = y + 6; sy < y + ClosedH - 6; sy += 12)
        {
            Raylib.DrawRectangle(x + 2, sy, 6, 4, KraftCoverInner);
            Raylib.DrawRectangleLines(x + 1, sy - 1, 8, 6, KraftBorder);
        }

        Color border = isTopmost || hov ? Palette.CrtAmberPrimary : KraftBorder;
        Raylib.DrawRectangleLines(x, y, ClosedW, ClosedH, border);
        Raylib.DrawRectangleLines(x + 12, y + 3, ClosedW - 15, ClosedH - 6, InkDim);

        UiText.Draw(TruncateToWidth(CoverTitle, 10, ClosedW - 62, denseRow: true), x + 16, y + 8, 10, InkDark, denseRow: true);
        UiText.Draw($"NOTES ({unlockedCount} PG)", x + 16, y + 24, 10, InkRedPen, denseRow: true);

        bool btnHov = mouse.IsHit(x + ClosedW - 44, y + 10, 36, 24);
        Raylib.DrawRectangle(x + ClosedW - 44, y + 10, 36, 24, btnHov ? KraftSpine : KraftCoverInner);
        Raylib.DrawRectangleLines(x + ClosedW - 44, y + 10, 36, 24, KraftBorder);
        UiText.Draw("OPEN", x + ClosedW - 40, y + 17, 10, btnHov ? PaperCream : InkDark, denseRow: true);
    }

    private static string TruncateToWidth(string value, int fontSize, int maxPx, bool denseRow = false)
    {
        if (string.IsNullOrEmpty(value) || maxPx <= 0)
            return string.Empty;
        if (UiText.MeasureWidth(value, fontSize, denseRow) <= maxPx)
            return value;
        int len = value.Length;
        while (len > 1 && UiText.MeasureWidth(value[..len], fontSize, denseRow) > maxPx)
            len--;
        return value[..len];
    }

    private static List<string> WrapToWidth(string text, int fontSize, int maxPx, int maxLines, bool denseRow = false)
    {
        List<string> result = [];
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
            return result;

        string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
        foreach (string paragraph in paragraphs)
        {
            if (result.Count >= maxLines)
                break;

            string[] words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                continue;

            string current = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                string cand = current.Length == 0 ? w : $"{current} {w}";
                if (UiText.MeasureWidth(cand, fontSize, denseRow) <= maxPx)
                {
                    current = cand;
                }
                else if (current.Length == 0)
                {
                    result.Add(TruncateToWidth(w, fontSize, maxPx, denseRow));
                    if (result.Count >= maxLines)
                        return result;
                }
                else
                {
                    result.Add(TruncateToWidth(current, fontSize, maxPx, denseRow));
                    if (result.Count >= maxLines)
                        return result;
                    current = TruncateToWidth(w, fontSize, maxPx, denseRow);
                }
            }

            if (current.Length > 0 && result.Count < maxLines)
            {
                result.Add(TruncateToWidth(current, fontSize, maxPx, denseRow));
            }
        }

        return result;
    }
}
