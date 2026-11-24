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

    public const string Day3KordaniOfficerPortraitSeed = "kdf-sec-day3-officer";
    public const string Day3KordaniOfficerName = "CMDR. VEX-KORR // KORDANI SECURITY";
    public const string Day3KordaniOfficerTransponder = "KDF-SEC-09";

    // Branch A (Day2SuspectDetained == true): Live CRT viewscreen hail
    public static string[] BuildDay3KordaniHailLines(
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin)
    {
        string safeReg = string.IsNullOrWhiteSpace(day2ShipReg) ? "ORN-442-K" : day2ShipReg;
        string safeOrigin = string.IsNullOrWhiteSpace(declaredOrigin) ? "Oruun-4" : declaredOrigin;

        if (day2SuspectDetained)
        {
            return
            [
                $"Port Officer. Our boarding teams investigated detained vessel {safeReg}. Their declared origin ({safeOrigin}) was falsified.",
                "You trusted your gut, and it paid off. Thank you for the catch.",
                "That said, Booth 04 does not run on instinct. Your Warp Residue Spectrometer is operational again so every route fault can be verified via Protocol Review.",
                "Effective this watch, Kordani Security Strikes are active. Verify every violation on console before detaining. Do not disappoint Command."
            ];
        }

        // Branch B (Day2SuspectDetained == false): Breach slipped through -> tightened security & Security Strikes
        return
        [
            $"Port Officer. Perimeter relays flagged vessel {safeReg} departing Void Gate 3 on an unlogged vector. Its declared origin ({safeOrigin}) was falsified.",
            "You followed active Day 02 protocols, yet a major breach still slipped through Booth 04.",
            "Kordani Security has forced immediate repair of your Warp Residue Spectrometer and installed the Glowing Warp Checker.",
            "Effective this watch, Kordani Security Strikes (0/3) are active. Three strikes trigger a berth audit. Hold the gate cleanly."
        ];
    }

    public static string[] BuildDay3ShiftDirectiveLines(
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin)
    {
        string safeReg = string.IsNullOrWhiteSpace(day2ShipReg) ? "ORN-442-K" : day2ShipReg;
        string safeOrigin = string.IsNullOrWhiteSpace(declaredOrigin) ? "Oruun-4" : declaredOrigin;

        if (!day2SuspectDetained)
        {
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "INCIDENT REPORT [KDF-SEC-09]:",
                $"At 02:14 station standard, long-range perimeter relays flagged vessel {safeReg} departing Void Gate 3 on an unlogged vector. Post-transit telemetry confirmed its declared origin ({safeOrigin}) was falsified.",
                "",
                "ADMINISTRATIVE NOTICE [BMC-OPS]:",
                "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer & Glowing Warp Checker) has been fulfilled via Deck 4 salvage following Kordani Security intervention.",
                "",
                "NEW MANDATORY PROTOCOL:",
                "1. Scan warp residue and lay the magnetic strip over the Glowing Warp Checker to verify declared origin waveforms and flag restricted Ashen Rift traffic.",
                "2. Effective immediately, Kordani Security Strikes (0/3) are ACTIVE. Verify all discrepancies via Protocol Review before detaining."
            ];
        }

        return
        [
            "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            "INCIDENT REPORT [KDF-SEC-09B]:",
            $"Overnight, Kordani Security boarding teams investigated detained vessel {safeReg} and confirmed its declared origin ({safeOrigin}) was falsified.",
            "",
            "ADMINISTRATIVE NOTICE [BMC-OPS]:",
            "Because Booth 04 caught the breach on a hunch while the spectrometer was offline, Kordani Security forced immediate repair of your Warp Printer and Glowing Warp Checker anyway.",
            "",
            "NEW MANDATORY PROTOCOL:",
            "1. Scan warp residue and lay the magnetic strip over the Glowing Warp Checker to verify declared origin waveforms and flag restricted Ashen Rift traffic.",
            "2. Effective immediately, Kordani Security Strikes (0/3) are ACTIVE. Verify all discrepancies via Protocol Review before detaining."
        ];
    }

    public static string[] BuildDailyShiftDirectiveLines(
        int day,
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin,
        ConfigFlags? config = null)
    {
        if (day == 3)
        {
            return BuildDay3ShiftDirectiveLines(day2SuspectDetained, day2ShipReg, declaredOrigin);
        }

        if (day == 2)
        {
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 02",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "ACTIVE PROTOCOL CHECKS:",
                "1. Cross-check Raw Transponder ID vs. Data Slate REG CODE.",
                "2. Order active shields down and honest weapons offline before granting access.",
                "3. Scan hulls with the SPS Probe for spoofed hot weapons claiming OFFLINE."
            ];
        }

        int manualPart1Day = config?.DayUnlockManualPart1 ?? Constants.DefaultDayUnlockManualPart1;
        int manualHullsDay = config?.DayUnlockManualHulls ?? Constants.DefaultDayUnlockManualHulls;
        int waferCargoDay = config?.DayUnlockWaferCargo ?? Constants.DefaultDayUnlockWaferCargo;

        if (day > 3 && day < manualPart1Day)
        {
            return
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "ROUTINE WATCH NOTICE: STANDARD QUEUE TRAFFIC",
                "ACTIVE PROTOCOL CHECKS:",
                "1. Cross-check Raw Transponder ID vs. Data Slate REG CODE.",
                "2. Order active shields down / weapons offline and scan with SPS Probe for spoofed hot weapons.",
                "3. Scan warp residue and overlap onto Glowing Warp Checker. Security Strikes (0/3) active."
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
                lines.Add("NEW HARDWARE ISSUED: KORDANI REFERENCE MANUAL TABLET (1. PREFIXES | 2. HULLS)");
            }
            lines.Add("ACTIVE PROTOCOL CHECKS:");
            lines.Add("1. Enforce Registration Expiry Date (Data Slate EXP vs. Top Bar Station Cycle).");
            lines.Add("2. Enforce Prefix vs. Declared Organization and Permitted Captain Species (Tab 1. PREFIXES).");
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
                "ACTIVE PROTOCOL CHECKS:",
                "1. Enforce Hull Silhouette & Ship Class schematics via Reference Manual Tab 2. HULLS.",
                "2. Enforce Registration Expiry Date and Prefix vs. Organization / Captain Species.",
                "3. Continue Transponder ID, SPS Hot Weapons, and Glowing Warp Checker origin checks."
            ];
        }

        if (day >= waferCargoDay)
        {
            return
            [
                $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "ACTIVE PROTOCOL CHECKS:",
                "1. Scan cargo bays with the SPS Probe for unexplained Violet Cargo Power Draw in empty holds.",
                "2. Enforce Hull Silhouette & Class, Registration Expiry, and Prefix vs. Org / Species.",
                "3. Continue Transponder ID, SPS Hot Weapons, and Glowing Warp Checker origin checks."
            ];
        }

        return
        [
            $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            "ACTIVE PROTOCOL CHECKS:",
            "1. Cross-check Raw Transponder ID (under Viewscreen) vs. Data Slate REG CODE.",
            "2. Matching ID: Slot Data Slate in Clearance Dock, then return to Slate Bus.",
            "3. Mismatched ID: Confirm conflict via PROTOCOL REVIEW first, then pull Detain Lever."
        ];
    }
}
