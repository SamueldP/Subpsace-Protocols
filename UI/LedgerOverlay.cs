using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Clock-Out End-of-Shift Ledger centered at (360, 128, 560, 464) over the dimmed Booth 04 Desk.
/// Triggers right at clock-out after the deliberate 2.5s Booth 04 power-down delay,
/// displaying ships processed (reading live GameState.BasePayPerShipScrip), clerical fines,
/// Stellar Stoker overload structural-strain fee, Day 3+ persistent Kordani Security Strikes,
/// Clean-Audit Fine, auto-deducted Berth Lease (-30 SC with 1-night deferral / 2-night Debt Foreclosure),
/// and resulting Net Pact Scrip balance before entering Evening Quarters.
/// </summary>
public sealed class LedgerOverlay
{
    public const int BoxX = 360;
    public const int BoxY = 128;
    public const int BoxW = 560;
    public const int BoxH = 464;

    public const int FooterBtnX = 480;
    public const int FooterBtnY = 536;
    public const int FooterBtnW = 320;
    public const int FooterBtnH = 44;

    private ShiftLedgerSummary? _summary;
    private float _appearAnim;

    public bool IsOpen => _summary != null;
    public ShiftLedgerSummary? CurrentSummary => _summary;

    public void Open(ShiftLedgerSummary summary)
    {
        _summary = summary;
        _appearAnim = 0f;
        AudioHooks.Play("printer_tick");
    }

    public void Close()
    {
        _summary = null;
        _appearAnim = 0f;
    }

    /// <summary>
    /// Updates slip reveal animation and handles footer button click.
    /// Returns true if the player clicked the footer button to proceed.
    /// </summary>
    public bool UpdateAndCheckProceed(float deltaTime, VirtualMouseState mouse)
    {
        if (_summary == null)
            return false;

        _appearAnim = Math.Min(1.0f, _appearAnim + deltaTime * 5.0f);

        if (mouse.LeftPressed && mouse.IsHit(FooterBtnX, FooterBtnY, FooterBtnW, FooterBtnH))
        {
            AudioHooks.Play("terminal_key_confirm");
            return true;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            AudioHooks.Play("terminal_key_confirm");
            return true;
        }

        return false;
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (_summary == null)
            return;

        // Dim Booth 04 Desk backdrop underneath
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 196));
        for (int sy = 0; sy < Constants.VirtualHeight; sy += 4)
        {
            Raylib.DrawLine(0, sy, Constants.VirtualWidth, sy, new Color(12, 15, 22, 80));
        }

        int yOffset = (int)MathF.Round((1.0f - _appearAnim) * 18f);
        int bx = BoxX;
        int by = BoxY + yOffset;

        // Brengop Corporate Yellow-on-Dark Settlement Slip Frame
        Color brengopBg = new(18, 18, 13, 255);
        Color brengopDarkPanel = new(12, 12, 9, 255);
        Color brengopYellow = Palette.FromHex("#bdb040");
        Color brengopBrass = Palette.FromHex("#8a6d33");
        Color brengopMuted = Palette.FromHex("#968826");

        // Drop shadow + outer frame
        Raylib.DrawRectangle(bx + 4, by + 4, BoxW, BoxH, new Color(4, 6, 8, 220));
        Raylib.DrawRectangle(bx, by, BoxW, BoxH, brengopBg);
        Raylib.DrawRectangleLines(bx, by, BoxW, BoxH, brengopYellow);
        Raylib.DrawRectangleLines(bx + 3, by + 3, BoxW - 6, BoxH - 6, brengopBrass);

        // Perforated slip notches along top & bottom edges
        for (int px = bx + 16; px < bx + BoxW - 16; px += 16)
        {
            Raylib.DrawRectangle(px, by + 1, 6, 2, Palette.OutlineVoid);
            Raylib.DrawRectangle(px, by + BoxH - 3, 6, 2, Palette.OutlineVoid);
        }

        // Corporate Header Band
        Raylib.DrawRectangle(bx + 8, by + 8, BoxW - 16, 44, new Color(34, 31, 14, 255));
        Raylib.DrawRectangleLines(bx + 8, by + 8, BoxW - 16, 44, brengopMuted);
        string title = $"BRENGOP PORT AUTHORITY // SHIFT {_summary.ShiftDay:D2} SETTLEMENT";
        int titleW = UiText.MeasureWidth(title, 10);
        UiText.Draw(title, bx + (BoxW - titleW) / 2, by + 16, 10, brengopYellow);

        (int cy, int cm) = Constants.GetStationCycle(_summary.ShiftDay);
        string sub = $"VOID GATE 3  |  BOOTH 04 CLOCK-OUT SLIP  |  CYC {Constants.FormatStationCycle(cy, cm)}";
        int subW = UiText.MeasureWidth(sub, 10);
        UiText.Draw(sub, bx + (BoxW - subW) / 2, by + 34, 10, brengopMuted);

        // Itemized Ledger Container
        int bodyX = bx + 16;
        int bodyY = by + 64;
        int bodyW = BoxW - 32;
        int bodyH = 330;
        Raylib.DrawRectangle(bodyX, bodyY, bodyW, bodyH, brengopDarkPanel);
        Raylib.DrawRectangleLines(bodyX, bodyY, bodyW, bodyH, brengopBrass);

        int rowX = bodyX + 16;
        int valRightX = bodyX + bodyW - 16;
        int curY = bodyY + 14;
        const int rowStep = 26;

        // 1. Ships Processed (Live GameState.BasePayPerShipScrip)
        DrawLedgerRow(
            rowX, valRightX, curY,
            $"SHIPS PROCESSED ({_summary.ShipsProcessedAccurate} x {_summary.PayPerShipScrip} SC)",
            $"+{_summary.GrossShiftPay} SC",
            Palette.CrtAmberPrimary,
            Palette.ClearanceTeal);
        curY += rowStep;

        // 2. Clerical Warnings & Fines (reset daily; -10 SC after 2 warnings)
        string warnTag = _summary.ClericalWarnings <= Constants.FreeClericalWarningsPerShift
            ? $"CLERICAL WARNINGS ({_summary.ClericalWarnings}/{Constants.FreeClericalWarningsPerShift} FREE)"
            : $"CLERICAL FINES ({_summary.ClericalWarnings} MISTAKES, -10 SC >2)";
        DrawLedgerRow(
            rowX, valRightX, curY,
            warnTag,
            _summary.ClericalFinesTotal > 0 ? $"-{_summary.ClericalFinesTotal} SC" : "0 SC",
            Palette.CrtAmberPrimary,
            _summary.ClericalFinesTotal > 0 ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
        curY += rowStep;

        // 3. Stellar Stoker Overload Structural-Strain Penalty (if triggered this shift)
        if (_summary.StokerOverloaded)
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "STELLAR STOKER OVERLOAD STRAIN FEE (+0 STRIKES)",
                $"-{_summary.StokerOverloadFine} SC",
                Palette.AlertCrimson,
                Palette.AlertCrimson);
            curY += rowStep;

            UiText.Draw(
                "  >> NOTICE: KORDANI SABOTAGE AUDIT QUEUED FOR SLEEP CYCLE",
                rowX, curY - 8, 10, Palette.CrtAmberPrimary);
            curY += 16;
        }
        else
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "STELLAR STOKER STRUCTURAL STRAIN",
                "NOMINAL (0 SC)",
                brengopMuted,
                Palette.WeatheredGunmetalEdge);
            curY += rowStep;
        }

        // 4. Persistent Kordani Security Strikes (displayed Day 4+ once activated)
        if (_summary.ShiftDay >= Constants.DefaultDayUnlockWarpScanner)
        {
            Color strikeCol = _summary.SecurityStrikesEnd >= _summary.MaxStrikesBeforeAudit
                ? Palette.AlertCrimson
                : (_summary.SecurityStrikesEnd > 0 ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
            string strikeRight = _summary.SecurityStrikesEnd >= _summary.MaxStrikesBeforeAudit
                ? $"{_summary.SecurityStrikesEnd}/{_summary.MaxStrikesBeforeAudit} [AUDIT ON SLEEP!]"
                : $"{_summary.SecurityStrikesEnd}/{_summary.MaxStrikesBeforeAudit} PERSISTENT";
            DrawLedgerRow(
                rowX, valRightX, curY,
                "KORDANI SECURITY STRIKES (PERSISTENT)",
                strikeRight,
                Palette.CrtAmberPrimary,
                strikeCol);
            curY += rowStep;
        }
        else
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "KORDANI SECURITY STRIKES",
                $"INACTIVE (DAYS 01-{(Constants.DefaultDayUnlockWarpScanner - 1):D2})",
                Palette.WeatheredGunmetalEdge,
                Palette.WeatheredGunmetalEdge);
            curY += rowStep;
        }

        // 5. Clean-Audit Fine (if applied during the prior night's 3-Strike Audit)
        if (_summary.CleanAuditFineApplied > 0)
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "KORDANI CLEAN-AUDIT ADMIN FEE (PRIOR AUDIT)",
                $"-{_summary.CleanAuditFineApplied} SC",
                Palette.CrtAmberPrimary,
                Palette.AlertCrimson);
            curY += rowStep;
        }

        // Divider before Mandatory Auto-Deducted Berth Lease
        Raylib.DrawLine(rowX, curY + 2, valRightX, curY + 2, brengopBrass);
        curY += 12;

        // 6. Auto-Deducted Berth Lease (-30 SC)
        if (_summary.BerthLeasePaid)
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "AUTO-DEDUCTED BERTH LEASE (NIGHTLY RENT)",
                $"-{_summary.BerthLeaseCost} SC",
                brengopYellow,
                Palette.CrtAmberPrimary);
        }
        else if (_summary.BerthLeaseDeferred)
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "BERTH LEASE DEFERRED -- 1 NIGHT GRACE WARNING!",
                $"0 SC (MISSED {_summary.MissedLeaseCountAfter}/2)",
                Palette.AlertCrimson,
                Palette.AlertCrimson);
        }
        else
        {
            DrawLedgerRow(
                rowX, valRightX, curY,
                "BERTH LEASE DEFAULT -- 2 NIGHTS MISSED!",
                "DEBT FORECLOSURE",
                Palette.AlertCrimson,
                Palette.AlertCrimson);
        }
        curY += rowStep + 4;

        // Warning banner if Lease Deferred or Foreclosure
        if (_summary.BerthLeaseDeferred)
        {
            Raylib.DrawRectangle(rowX, curY - 4, bodyW - 32, 24, new Color(54, 18, 18, 255));
            Raylib.DrawRectangleLines(rowX, curY - 4, bodyW - 32, 24, Palette.AlertCrimson);
            UiText.Draw(
                "WARNING: MISSING 1 MORE BERTH LEASE TRIGGERS DEBT FORECLOSURE!",
                rowX + 10, curY + 3, 10, Palette.CrtAmberPrimary);
            curY += 28;
        }
        else if (_summary.DebtForeclosureTriggered)
        {
            Raylib.DrawRectangle(rowX, curY - 4, bodyW - 32, 24, new Color(72, 16, 16, 255));
            Raylib.DrawRectangleLines(rowX, curY - 4, bodyW - 32, 24, Palette.AlertCrimson);
            UiText.Draw(
                "BADGE REVOKED: PERMANENT REASSIGNMENT TO LOWER REACTOR DECKS.",
                rowX + 10, curY + 3, 10, Palette.AlertCrimson);
            curY += 28;
        }

        // Bottom Net Balance Summary Box inside body
        int netBoxY = bodyY + bodyH - 54;
        Raylib.DrawRectangle(rowX, netBoxY, bodyW - 32, 42, new Color(24, 24, 15, 255));
        Raylib.DrawRectangleLines(rowX, netBoxY, bodyW - 32, 42, brengopYellow);
        UiText.Draw("NET PACT SCRIP AVAILABLE (EVENING QUARTERS)", rowX + 12, netBoxY + 9, 10, brengopMuted);
        string netVal = $"{_summary.ResultingNetScrip} SC";
        int netW = UiText.MeasureWidth(netVal, 20);
        UiText.Draw(netVal, valRightX - netW - 10, netBoxY + 11, 20,
            _summary.ResultingNetScrip >= 20 ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
        UiText.Draw("ALL DEDUCTIONS FINAL. BERTH LEASE AUTO-SETTLED.", rowX + 12, netBoxY + 25, 10, Palette.WeatheredGunmetalEdge);

        // Footer Action Button: exact (480, 536, 320, 44)
        int btnY = FooterBtnY + yOffset;
        bool hover = mouse.IsHit(FooterBtnX, btnY, FooterBtnW, FooterBtnH);
        Color btnBorder = _summary.DebtForeclosureTriggered
            ? Palette.AlertCrimson
            : (hover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        Color btnBg = hover ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Raylib.DrawRectangle(FooterBtnX, btnY, FooterBtnW, FooterBtnH, btnBg);
        Raylib.DrawRectangleLines(FooterBtnX, btnY, FooterBtnW, FooterBtnH, btnBorder);
        Raylib.DrawRectangleLines(FooterBtnX + 2, btnY + 2, FooterBtnW - 4, FooterBtnH - 4, Palette.OutlineVoid);

        string btnLabel = _summary.DebtForeclosureTriggered
            ? "[ ACCEPT DEBT FORECLOSURE ]"
            : "[ ENTER EVENING QUARTERS ]";
        int bw = UiText.MeasureWidth(btnLabel, 10);
        UiText.Draw(
            btnLabel,
            FooterBtnX + (FooterBtnW - bw) / 2,
            btnY + (FooterBtnH - 10) / 2,
            10,
            _summary.DebtForeclosureTriggered
                ? Palette.AlertCrimson
                : (hover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal));
    }

    private static void DrawLedgerRow(
        int leftX,
        int rightX,
        int y,
        string label,
        string value,
        Color labelCol,
        Color valueCol)
    {
        UiText.Draw(label, leftX, y, 10, labelCol);
        int vw = UiText.MeasureWidth(value, 10);
        int lw = UiText.MeasureWidth(label, 10);
        int dotsStart = leftX + lw + 8;
        int dotsEnd = rightX - vw - 8;
        for (int dx = dotsStart; dx < dotsEnd; dx += 6)
        {
            Raylib.DrawPixel(dx, y + 7, new Color(72, 66, 36, 255));
        }
        UiText.Draw(value, rightX - vw, y, 10, valueCol);
    }
}
