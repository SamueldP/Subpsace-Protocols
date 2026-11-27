using SubspaceProtocols.Core;

namespace SubspaceProtocols.Data;

/// <summary>
/// Editable scripted dialogue & Morning Shift Directive blocks for the Day 2 ("The Ghost Route")
/// and Day 3 branching opening sequence (Live Kordani Security Officer Viewscreen Hail).
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public static class Day2And3ScriptedDialogue
{
    public const string Day1VorstOfficerName = "COMMANDER VORST // KORDANI SECURITY COMMAND";
    public const string Day1VorstTransponder = "KDF-CMD-01";

    // Strictly verbatim Day 1 Commander Vorst live viewscreen hail (Scripted_Events_and_Directives.md)
    public static readonly string[] Day1CommanderVorstHailLines =
    [
        "Vorst: \"884-D. Commander Vorst, Kordani Security.\"",
        "Vorst: \"Another human face. I see they are still scraping Deck 4.\"",
        "Vorst: \"The Brengop treat this gate like a tollbooth. The Kordani will hold it like a border.\"",
        "Vorst: \"I spent years on the Concord front\u2014I know the cost of one breach.\"",
        "Vorst: \"If the registries do not match, detain them. Do not ask for an explanation. My men will handle it.\"",
        "Vorst: \"The last clerk got greedy. It took four minutes to clear his desk. Replacing him was not difficult.\"",
        "Vorst: \"...\"",
        "Vorst: \"Hail the first ship.\""
    ];

    public const string RegeerNixaOfficerName = "REGEER NIXA // CONSORTIUM LOGISTICS";
    public const string RegeerNixaTransponder = "BMC-LOG-01";

    public const string Day15BrengopForemanName = RegeerNixaOfficerName;
    public const string Day15BrengopForemanTransponder = RegeerNixaTransponder;

    // Day 15 Pre-Shift Regeer Nixa (Consortium Logistics) Live Viewscreen Hail (zero raw % tooltip recitation!)
    // TODO: USER DIALOGUE PASS
    public static readonly string[] Day15BrengopForemanHailLines =
    [
        "Regeer Nixa: \"Morning, 884-D. Do forgive the grease on your console\u2014I had our riggers bolt a Stellar Stoker mass-anchor gauge beneath your Detain Lever before dawn.\"", // TODO: USER DIALOGUE PASS
        "Regeer Nixa: \"Press ANCHOR on every hull that docks and match its measured weight against the mass declared on their slate.\"", // TODO: USER DIALOGUE PASS
        "Regeer Nixa: \"Keep that strain needle out of the red band. One heavy uncalibrated freighter will rattle the ring, and two will cook the coils.\"", // TODO: USER DIALOGUE PASS
        "Regeer Nixa: \"Do try not to fry Consortium hardware, 884-D. Billing will dock your pay, and those paranoid Kordani will tear your berth apart hunting for sabotage.\"" // TODO: USER DIALOGUE PASS
    ];

    // Verbatim Day 2 Regeer Nixa live viewscreen hail
    public static readonly string[] Day2RegeerNixaHailLines =
    [
        "Regeer Nixa: \"Morning, 884-D. I am Regeer Nixa, Consortium Logistics. It is... good to meet you.\"",
        "Regeer Nixa: \"I see your shift yesterday went smoothly. Keep this up and we\u2014I\u2014will make sure your berth stays warm.\"",
        "Regeer Nixa: \"I had your former associates on Deck 4 restore the SPS probe last night.\"",
        "Regeer Nixa: \"Scan every ship and make sure their shields and weapons are actually off before you let them through. Pilots lie, and those Kordani thugs don't care how much a station costs when they get their scales on a trigger.\"",
        "Regeer Nixa: \"Keep the lane moving, 884-D.\""
    ];

    // Verbatim Day 8 Regeer Nixa live viewscreen hail
    public static readonly string[] Day8RegeerNixaHailLines =
    [
        "Regeer Nixa: \"Morning, 884-D. Still clearing lanes I see. I do love a reliable investment.\"",
        "Regeer Nixa: \"I pulled a few strings with Consortium Compliance and had a Registry Tablet sent up for your desk. Brand-new glass. Try not to grease it up with those fingers.\"",
        "Regeer Nixa: \"Check every vessel's registration expiry with the registrar and match its prefix against whatever they like to call themselves. Wrong flag or wrong crew, mark the discrepancy and detain them. Let the lizards deal with it.\"",
        "Regeer Nixa: \"They wanted to run armed boarding sweeps on every second freighter over those ridiculous... Dopple rumours. Imagine what that would do to our docking turnaround.\"",
        "Regeer Nixa: \"...\"",
        "Regeer Nixa: \"Shapeshifting Concord leftovers. Pure gossip, of course. Nothing left of the Dopples after the Ashen collapse wiped them all to dust.\"",
        "Regeer Nixa: \"Not that it is anything for a booth clerk to count dust over. Leave the paranoia to the Kordani. You just cross-check the registries and keep my ledger clean, 884-D.\""
    ];

    public const string Day3KordaniOfficerPortraitSeed = "kdf-sec-day3-officer";
    public const string Day3KordaniOfficerName = "COMMANDER VORST // KORDANI SECURITY COMMAND";
    public const string Day3KordaniOfficerTransponder = "KDF-CMD-01";

    // Branch A (GhostRouteSuspectDetained == true) & Branch B (false): Live CRT viewscreen hail on Day 4 by Commander Vorst
    public static string[] BuildDay4KordaniHailLines(
        bool ghostRouteSuspectDetained,
        string ghostShipReg,
        string declaredOrigin)
    {
        string safeReg = string.IsNullOrWhiteSpace(ghostShipReg) ? "ORN-442-K" : ghostShipReg;
        string safeOrigin = string.IsNullOrWhiteSpace(declaredOrigin) ? "Oruun-4" : declaredOrigin;

        if (ghostRouteSuspectDetained)
        {
            return
            [
                $"Vorst: \"884-D. Our boarding teams investigated the vessel you detained yesterday ({safeReg}). Their departure logs from {safeOrigin} were falsified.\"",
                "Vorst: \"You trusted your gut, and it paid off. Thank you for the catch.\"",
                "Vorst: \"That said, Booth 04 does not run on instinct. I pulled Deck 4 salvage to restore your Warp Spectrometer and Glowing Warp Checker.\"",
                "Vorst: \"Effective immediately, Kordani Security Strikes (/3 cap) are active on your console. Print and verify every origin waveform before you pull Access or Detain.\""
            ];
        }

        return
        [
            $"Vorst: \"884-D. Perimeter relays flagged vessel {safeReg} slipping out of Void Gate 3 yesterday on an unlogged vector. Its {safeOrigin} declaration was falsified.\"",
            "Vorst: \"You followed active Day 03 rules, yet a recon hull still crossed my border while Brengop management sat on your broken spectrometer.\"",
            "Vorst: \"I bypassed their ledger and had Deck 4 salvage wired into your Warp Residue Printer and Glowing Warp Checker.\"",
            "Vorst: \"Effective immediately, Kordani Security Strikes (/3 cap) are active at Booth 04. Verify every origin strip before you clear a lane.\""
        ];
    }

    public static string[] BuildDay3KordaniHailLines(
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin) =>
        BuildDay4KordaniHailLines(day2SuspectDetained, day2ShipReg, declaredOrigin);

    // Verbatim authored copy from /design/07_Daily_Schedule/Daily_Traffic_and_Scripted_Briefings.md (updated to Day 04 / Day 3 Ghost Route)
    public static string[] BuildDay4ShiftDirectiveLines(
        bool ghostRouteSuspectDetained,
        string ghostShipReg,
        string declaredOrigin)
    {
        string safeReg = string.IsNullOrWhiteSpace(ghostShipReg) ? "ORN-442-K" : ghostShipReg;
        string safeOrigin = string.IsNullOrWhiteSpace(declaredOrigin) ? "Oruun-4" : declaredOrigin;

        if (!ghostRouteSuspectDetained)
        {
            return
            [
                "==================================================",
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 04",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "==================================================",
                "INCIDENT REPORT [KDF-SEC-09]:",
                $"At 02:14 station standard, long-range perimeter relays flagged vessel {safeReg} departing Void Gate 3 on an unlogged vector. Post-transit telemetry confirmed its declared origin ({safeOrigin}) was falsified.",
                "",
                "ADMINISTRATIVE NOTICE [BMC-OPS]:",
                "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer - thermal coil burnout from Brown Dwarf infrared surge) was previously deferred under Brengop cost-containment policy. Following Kordani Security intervention, a replacement coil has been pulled from Deck 4 salvage and wired into your console.",
                "",
                "NEW MANDATORY PROTOCOL:",
                "Effective immediately, ALL incoming vessels must undergo a Warp Field Residue print-check. Overlap the printed magnetic waveform strip directly onto your backlit Glowing Warp Checker before slotting any Data Slate into the Clearance Dock.",
                "",
                "NOTE: Salvaged coil runs hot. Print speed is suboptimal. Do not tamper with housing. Brengop Management will bill replacement costs directly to your Berth Lease.",
                "=================================================="
            ];
        }

        return
        [
            "==================================================",
            "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 04",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "==================================================",
            "INCIDENT REPORT [KDF-SEC-09]:",
            $"Overnight investigation by Kordani boarding teams on detained vessel {safeReg} confirmed its declared origin ({safeOrigin}) was falsified. Because Booth 04 only caught the breach on a hunch while the spectrometer was offline, Kordani Security has forced an immediate spectrometer repair anyway.",
            "",
            "ADMINISTRATIVE NOTICE [BMC-OPS]:",
            "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer - thermal coil burnout from Brown Dwarf infrared surge) was previously deferred under Brengop cost-containment policy. Following Kordani Security intervention, a replacement coil has been pulled from Deck 4 salvage and wired into your console.",
            "",
            "NEW MANDATORY PROTOCOL:",
            "Effective immediately, ALL incoming vessels must undergo a Warp Field Residue print-check. Overlap the printed magnetic waveform strip directly onto your backlit Glowing Warp Checker before slotting any Data Slate into the Clearance Dock.",
            "",
            "NOTE: Salvaged coil runs hot. Print speed is suboptimal. Do not tamper with housing. Brengop Management will bill replacement costs directly to your Berth Lease.",
            "=================================================="
        ];
    }

    public static string[] BuildDay3ShiftDirectiveLines(
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin) =>
        BuildDay4ShiftDirectiveLines(day2SuspectDetained, day2ShipReg, declaredOrigin);

    public static string[] BuildDailyShiftDirectiveLines(
        int day,
        bool ghostRouteSuspectDetained,
        string ghostShipReg,
        string declaredOrigin,
        ConfigFlags? config = null)
    {
        int warpUnlockDay = config?.DayUnlockWarpScanner ?? Constants.DefaultDayUnlockWarpScanner;
        int manualPart1Day = config?.DayUnlockManualPart1 ?? Constants.DefaultDayUnlockManualPart1;
        int manualHullsDay = config?.DayUnlockManualHulls ?? Constants.DefaultDayUnlockManualHulls;
        int waferCargoDay = config?.DayUnlockWaferCargo ?? Constants.DefaultDayUnlockWaferCargo;
        int stokerDay = config?.DayUnlockStoker ?? Constants.DefaultDayUnlockStoker;

        if (day == 1)
        {
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 01",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "ACTIVE PROTOCOL CHECKS:",
                "1. On hail, shielded ships receive an automatic shields-down order before telemetry transfer.",
                "2. Cross-check Raw Transponder ID (under Viewscreen) vs. Data Slate REG CODE.",
                "3. Matching ID: Slot Data Slate in Clearance Dock, then return to Slate Bus.",
                "4. Mismatched ID: Confirm conflict via PROTOCOL REVIEW first, then pull Detain Lever."
            ];
        }

        if (day == 2)
        {
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 02",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "NEW HARDWARE ONLINE: SPS PROBE (AUTO-WEAPONS COMPLIANCE)",
                "ACTIVE PROTOCOL CHECKS:",
                "1. Cross-check Raw Transponder ID vs. Data Slate REG CODE.",
                "2. Sweep armed weapon mounts with the SPS Probe: comms will auto-issue 'Power down weapons.'",
                "3. Honest armed ships cool visibly inside the lens. Hold-to-lock [!] HOT WEAPONS on liars."
            ];
        }

        if (day == Constants.DayGhostRouteSuspect)
        {
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "ROUTINE WATCH NOTICE: BREATHER SHIFT",
                "ACTIVE PROTOCOL CHECKS:",
                "1. No new desk equipment today (Warp Residue Printer remains offline awaiting KDF tech pass).",
                "2. Cross-check Raw Transponder ID vs. Data Slate REG CODE.",
                "3. Sweep weapon mounts with the SPS Probe and flag any spoofed [!] HOT WEAPONS via Protocol Review."
            ];
        }

        if (day == warpUnlockDay)
        {
            return BuildDay4ShiftDirectiveLines(ghostRouteSuspectDetained, ghostShipReg, declaredOrigin);
        }

        if (day > warpUnlockDay && day < manualPart1Day)
        {
            string breatherLabel = day is 5 or 7
                ? "ROUTINE WATCH NOTICE: BREATHER SHIFT (NO NEW HARDWARE)"
                : "ROUTINE WATCH NOTICE: STANDARD QUEUE TRAFFIC";
            return
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                breatherLabel,
                "ACTIVE PROTOCOL CHECKS:",
                "1. Cross-check Raw Transponder ID vs. Data Slate REG CODE.",
                "2. Sweep armed hulls with the SPS Probe for spoofed [!] HOT WEAPONS.",
                "3. Print Warp Strip and align on Glowing Warp Checker. Security Strikes (/3) active."
            ];
        }

        if (day >= manualPart1Day && day < manualHullsDay)
        {
            List<string> lines =
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------"
            ];
            if (day == manualPart1Day)
            {
                lines.Add("NEW HARDWARE ISSUED: BRENGOP REFERENCE MANUAL TABLET (SHIP REGISTRAR | SPECIES REGISTRAR)");
            }
            lines.Add("ACTIVE PROTOCOL CHECKS:");
            lines.Add("1. Enforce Registration Expiry Date (Data Slate EXP vs. Top Bar Station Cycle).");
            lines.Add("2. Enforce Prefix vs. Declared Organization and Permitted Captain Species (SHIP / SPECIES REGISTRAR).");
            lines.Add("3. Continue Transponder ID, SPS Hot Weapons, and Glowing Warp Checker origin checks.");
            return [.. lines];
        }

        if (day >= manualHullsDay && day < waferCargoDay)
        {
            return
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                day == manualHullsDay
                    ? "NEW PROTOCOL ACTIVE: HULL SILHOUETTES & SHIP CLASS (BRENGOP MANUAL -> HULLS TAB)"
                    : "ACTIVE PROTOCOL CHECKS:",
                "1. Enforce Hull Silhouette & Ship Class schematics via Brengop Reference Manual HULLS tab.",
                "2. Enforce Registration Expiry Date and Prefix vs. Organization / Captain Species.",
                "3. Continue Transponder ID, SPS Hot Weapons, and Glowing Warp Checker origin checks."
            ];
        }

        if (day >= waferCargoDay && day < stokerDay)
        {
            return
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                day == waferCargoDay
                    ? "NEW HARDWARE ONLINE: WAFER WRITER DEMUX & MANUAL CARGO TAB"
                    : "ACTIVE PROTOCOL CHECKS:",
                "1. Inspect printed Cargo Manifest Cert-Wafers against the 5-item Banned List on the Manual CARGO tab.",
                "2. Sweep cargo bays on SPS for Violet [!] POWERED CARGO and wipe used Cert-Wafers in the top slot (3 blanks).",
                "3. Continue Transponder ID, SPS Hot Weapons, Warp Checker, Expiry, and Hull/Species checks."
            ];
        }

        return
        [
            $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            day == stokerDay
                ? "NEW HARDWARE ONLINE: STELLAR STOKER DUAL-READOUT MASS ANCHOR"
                : "ACTIVE PROTOCOL CHECKS:",
            "1. Press [ANCHOR] on the Stellar Stoker module to measure hull tonnage vs. Data Slate MASS.",
            "2. Flag undeclared overweight hulls (or dense Raw Slag Isotope loads) via Protocol Review and Detain.",
            "3. Keep the Stoker strain needle out of the red band to prevent coil overload fines and station brownouts."
        ];
    }
}
