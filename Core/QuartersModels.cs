namespace SubspaceProtocols.Core;

public enum QuartersPhase
{
    Morning = 0,
    Evening = 1
}

public enum NightlyRationOrder
{
    None = 0,
    DryRationBricks = 1,
    NutrientPaste = 2,
    HighTierRations = 3
}

public enum BerthAuditKind
{
    None = 0,
    ThreeStrikeBerthAudit = 1,
    StokerSabotageAudit = 2
}

public sealed class ShiftLedgerSummary
{
    public int ShiftDay { get; set; } = 1;
    public int ShipsProcessedAccurate { get; set; } = 0;
    public int PayPerShipScrip { get; set; } = Constants.BasePayPerShipScrip;
    public int GrossShiftPay { get; set; } = 0;
    public int ClericalWarnings { get; set; } = 0;
    public int ClericalFinesTotal { get; set; } = 0;
    public bool StokerOverloaded { get; set; } = false;
    public int StokerOverloadFine { get; set; } = 0;
    public int SecurityStrikesEnd { get; set; } = 0;
    public int MaxStrikesBeforeAudit { get; set; } = Constants.SecurityStrikesAuditThreshold;
    public int CleanAuditFineApplied { get; set; } = 0;
    public int BerthLeaseCost { get; set; } = Constants.BerthLeaseNightlyScrip;
    public bool BerthLeasePaid { get; set; } = true;
    public bool BerthLeaseDeferred { get; set; } = false;
    public bool DebtForeclosureTriggered { get; set; } = false;
    public int MissedLeaseCountAfter { get; set; } = 0;
    public int ResultingNetScrip { get; set; } = 0;
    public bool PendingStokerSabotageAudit { get; set; } = false;

    public ShiftLedgerSummary Clone() => new()
    {
        ShiftDay = ShiftDay,
        ShipsProcessedAccurate = ShipsProcessedAccurate,
        PayPerShipScrip = PayPerShipScrip,
        GrossShiftPay = GrossShiftPay,
        ClericalWarnings = ClericalWarnings,
        ClericalFinesTotal = ClericalFinesTotal,
        StokerOverloaded = StokerOverloaded,
        StokerOverloadFine = StokerOverloadFine,
        SecurityStrikesEnd = SecurityStrikesEnd,
        MaxStrikesBeforeAudit = MaxStrikesBeforeAudit,
        CleanAuditFineApplied = CleanAuditFineApplied,
        BerthLeaseCost = BerthLeaseCost,
        BerthLeasePaid = BerthLeasePaid,
        BerthLeaseDeferred = BerthLeaseDeferred,
        DebtForeclosureTriggered = DebtForeclosureTriggered,
        MissedLeaseCountAfter = MissedLeaseCountAfter,
        ResultingNetScrip = ResultingNetScrip,
        PendingStokerSabotageAudit = PendingStokerSabotageAudit
    };
}

public sealed class TerminalEmail
{
    public const string Day1BrengopEmailId = "day1_brengop_contract_884d";

    public string EmailId { get; set; } = string.Empty;
    public int DayReceived { get; set; } = 1;
    public string PhaseReceived { get; set; } = "MORNING";
    public string CycleTimestamp { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public bool IsEncrypted { get; set; } = false;
    public string EncryptedPreview { get; set; } = string.Empty;
    public string DecryptedBody { get; set; } = string.Empty;
    public string? LinkedMissionId { get; set; } = null;
    public bool IsMorningRoster { get; set; } = false;
    public bool IsTerminalHackEmail { get; set; } = false;
    public bool IsDay1BrengopEmail { get; set; } = false;

    public TerminalEmail Clone() => new()
    {
        EmailId = EmailId,
        DayReceived = DayReceived,
        PhaseReceived = PhaseReceived,
        CycleTimestamp = CycleTimestamp,
        Sender = Sender,
        Recipient = Recipient,
        Subject = Subject,
        Body = Body,
        IsRead = IsRead,
        IsEncrypted = IsEncrypted,
        EncryptedPreview = EncryptedPreview,
        DecryptedBody = DecryptedBody,
        LinkedMissionId = LinkedMissionId,
        IsMorningRoster = IsMorningRoster,
        IsTerminalHackEmail = IsTerminalHackEmail,
        IsDay1BrengopEmail = IsDay1BrengopEmail
    };

    /// <summary>
    /// Creates the Day 1 Morning mandatory Brengop reassignment email (Contract #884-D)
    /// using the strictly verbatim locked authored copy from Scripted_Events_and_Directives.md.
    /// </summary>
    public static TerminalEmail CreateDay1BrengopReassignmentEmail()
    {
        (int cy, int cm) = Constants.GetStationCycle(1);
        return new TerminalEmail
        {
            EmailId = Day1BrengopEmailId,
            DayReceived = 1,
            PhaseReceived = "MORNING",
            CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // 06:00",
            Sender = "Brengop Mercantile Consortium // VG3 Debt & Asset Management",
            Recipient = "Contractor #884-D (Subspace Gate 3, Booth 04)",
            Subject = "Congratulations on Your Upward Mobility [CONTRACT #884-D]",
            Body =
                "Dear Contractor #884-D,\n\n" +
                "Congratulations. Effective 06:00 station time, the Consortium is pleased to\n" +
                "elevate you from Deck 4 Scrap & Maintenance to Provisional Port Inspector at\n" +
                "Subspace Gate Booth 04.\n\n" +
                "Your predecessor vacated his post unexpectedly mid-shift, leaving the lane idle\n" +
                "for forty-two minutes\u2014a regrettable interruption to tariff collection that the\n" +
                "BMC cannot afford to repeat. We trust you will display greater fiscal discipline.\n\n" +
                "To reflect your newly elevated earning potential, your existing quarters have\n" +
                "been upgraded to our Tier-3 Solo billing rate. Your revised daily terms follow:\n\n" +
                "DAILY LEDGER (EFFECTIVE DAY 1):\n" +
                "--------------------------------------------------------------------------------\n" +
                "- Clearing Commission (Compliant Vessel):   +10 SC / ship\n" +
                "- Mandatory Berth Lease (Tier-3 Solo):      -30 SC / night (Auto-debited)\n" +
                "- Transit Debt Buyout (Off-Station Visa): 1,000 SC (Lump sum)\n" +
                "--------------------------------------------------------------------------------\n\n" +
                "FIDUCIARY OBLIGATIONS:\n" +
                "You are expected to clear a minimum of 6 vessels per shift. Unregistered hulls\n" +
                "represent uncollected tariffs; hesitation costs the Consortium throughput.\n\n" +
                "Booth 04 instrumentation represents considerable BMC capital. Hardware damage,\n" +
                "shift delays, or excessive wear on the mechanical toggles will be assessed and\n" +
                "billed to your personal ledger at close of shift.\n\n" +
                "Should Kordani Security fine, detain, or liquidate you for procedural breaches,\n" +
                "the Consortium assumes no liability. Please note that berth rent continues to\n" +
                "accrue during periods of detainment.\n\n" +
                "NOTICE OF EXECUTION:\n" +
                "As a courtesy\u2014and to spare your ledger our standard 2 SC notarization fee\u2014\n" +
                "perusal of this notice constitutes constructive assent, carrying the full legal\n" +
                "force and effect of a wet-ink signature.\n\n" +
                "We value your continued solvency.\n" +
                "\u2014 Brengop Mercantile Consortium",
            IsRead = false,
            IsEncrypted = false,
            IsDay1BrengopEmail = true
        };
    }
}

public sealed class SecurityLogEntry
{
    public string HexAddr { get; set; } = "0x9E:1A2";
    public int Day { get; set; } = 1;
    public string Tag { get; set; } = "[UNAUTH_IO]";
    public string Description { get; set; } = string.Empty;
    public bool IsSuspicious { get; set; } = true;
    public bool IsPurged { get; set; } = false;

    public SecurityLogEntry Clone() => new()
    {
        HexAddr = HexAddr,
        Day = Day,
        Tag = Tag,
        Description = Description,
        IsSuspicious = IsSuspicious,
        IsPurged = IsPurged
    };
}

public sealed class MorningRosterEntry
{
    public string RegistrationCode { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string ShipName { get; set; } = string.Empty;
    public string ShipClassCode { get; set; } = "CIV";
    public bool IsRealArrival { get; set; } = false;
    public int RealQueueSlot { get; set; } = -1;
    public bool IsMissionTarget { get; set; } = false;

    public MorningRosterEntry Clone() => new()
    {
        RegistrationCode = RegistrationCode,
        Prefix = Prefix,
        OrganizationName = OrganizationName,
        ShipName = ShipName,
        ShipClassCode = ShipClassCode,
        IsRealArrival = IsRealArrival,
        RealQueueSlot = RealQueueSlot,
        IsMissionTarget = IsMissionTarget
    };
}

public sealed class BerthAuditResolution
{
    public BerthAuditKind Kind { get; init; }
    public bool WasCaught { get; init; }
    public string CaughtReason { get; init; } = string.Empty;
    public bool DeepStructuralSweepTriggered { get; init; }
    public int UnscrubbedLogsFound { get; init; }
    public int PocketContrabandTotal { get; init; }
    public int PocketContrabandConcealedByLining { get; init; }
    public int PocketContrabandConfiscated { get; init; }
    public int VentContrabandExposed { get; init; }
    public int VentContrabandConfiscated { get; init; }
    public int CleanAuditFineCharged { get; init; }
    public int KordaniRepDelta { get; init; }
    public bool StrikesResetToZero { get; init; }
    public List<string> InspectionLogLines { get; init; } = [];
}
