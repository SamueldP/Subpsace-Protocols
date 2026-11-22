namespace SubspaceProtocols.Data;

/// <summary>
/// Editable scripted dialogue & Morning Shift Directive blocks for the Day 2 ("The Ghost Route")
/// and Day 3 branching opening sequence (Live Kordani Security Officer Viewscreen Hail).
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public static class Day2And3ScriptedDialogue
{
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
                "That said, Booth 04 does not run on instinct. Your Warp Residue Spectrometer is operational again, and Protocol Review verification is now mandatory before engaging clamps.",
                "Effective this watch, Kordani Security Strikes are active. Verify every violation on console before detaining. Do not disappoint Command."
            ];
        }

        // Branch B (Day2SuspectDetained == false): Breach slipped through -> tightened security & Security Strikes
        return
        [
            $"Port Officer. Perimeter relays flagged vessel {safeReg} departing Void Gate 3 on an unlogged vector. Its declared origin ({safeOrigin}) was falsified.",
            "You followed active Day 02 protocols, yet a major breach still slipped through Booth 04.",
            "Kordani Security has forced immediate repair of your Warp Residue Spectrometer and enabled mandatory Protocol Review verification prior to detainment.",
            "Effective this watch, Kordani Security Strikes are active. Three strikes trigger a berth audit. Hold the gate cleanly."
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
                "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer) has been fulfilled via Deck 4 salvage following Kordani Security intervention.",
                "",
                "NEW MANDATORY PROTOCOL:",
                "Effective immediately, Protocol Review verification and Kordani Security Strikes are ACTIVE. Verify all discrepancies before detaining."
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
            "Because Booth 04 caught the breach on a hunch while the spectrometer was offline, Kordani Security forced immediate maintenance compliance anyway.",
            "",
            "NEW MANDATORY PROTOCOL:",
            "Effective immediately, Protocol Review verification and Kordani Security Strikes are ACTIVE. Verify all discrepancies before detaining."
        ];
    }

    public static string[] BuildDailyShiftDirectiveLines(int day, bool day2SuspectDetained, string day2ShipReg, string declaredOrigin)
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

        return
        [
            $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            "ACTIVE PROTOCOL CHECKS:",
            "1. Cross-check Raw Transponder ID (under Viewscreen) vs. Data Slate REG CODE.",
            "2. Matching ID: Slot Data Slate in Clearance Dock, then return to Slate Bus.",
            "3. Mismatched ID: Pull the left-side Detain Lever to engage station clamps."
        ];
    }
}
