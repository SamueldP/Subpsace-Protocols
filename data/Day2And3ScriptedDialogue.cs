namespace SubspaceProtocols.Data;

/// <summary>
/// Editable scripted dialogue & Morning Shift Directive blocks for the Day 2 ("The Ghost Route")
/// and Day 3 branching opening sequence.
/// Strictly decoupled from Raylib rendering and input.
/// </summary>
public static class Day2And3ScriptedDialogue
{
    public const string Day3KordaniOfficerPortraitSeed = "kdf-sec-day3-officer";
    public const string Day3KordaniOfficerName = "CMDR. VEX-KORR // KORDANI SECURITY";

    // TODO: USER DIALOGUE PASS
    public static readonly string[] Day3PreShiftKordaniHailLines =
    [
        "Port Officer. Our boarding teams investigated the vessel you detained yesterday. Their departure logs were falsified.",
        "You trusted your gut, and it paid off. Thank you for the catch.",
        "That said, Booth 04 does not run on instinct. Your spectrometer is operational again. In the future, follow the rules."
    ];

    /// <summary>
    /// Builds the Day 3 Morning Shift Directive lines for Variant A (Day2SuspectDetained == false)
    /// or Variant B (Day2SuspectDetained == true). Both branches confirm the Warp Field Residue
    /// Spectrometer is repaired and active for Day 3.
    /// </summary>
    public static string[] BuildDay3ShiftDirectiveLines(
        bool day2SuspectDetained,
        string day2ShipReg,
        string declaredOrigin)
    {
        string safeReg = string.IsNullOrWhiteSpace(day2ShipReg) ? "ORN-442-K" : day2ShipReg;
        string safeOrigin = string.IsNullOrWhiteSpace(declaredOrigin) ? "Oruun-4" : declaredOrigin;

        if (!day2SuspectDetained)
        {
            // Variant A (Day2SuspectDetained == false): Existing [KDF-SEC-09] briefing
            return
            [
                "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03",
                "CLASSIFICATION: BULWARK PACT INTERNAL",
                "----------------------------------------------------------",
                "INCIDENT REPORT [KDF-SEC-09]:",
                $"At 02:14 station standard, long-range perimeter relays flagged vessel {safeReg} departing Void Gate 3 on an unlogged vector. Post-transit telemetry confirmed its declared origin ({safeOrigin}) was falsified.",
                "",
                "ADMINISTRATIVE NOTICE [BMC-OPS]:",
                "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer - thermal coil burnout from Brown Dwarf infrared surge) was previously deferred under Brengop cost-containment policy. Following Kordani Security intervention, a replacement coil has been pulled from Deck 4 salvage and wired into your console.",
                "",
                "NEW MANDATORY PROTOCOL:",
                "Effective immediately, ALL incoming vessels must undergo a Warp Field Residue print-check. Compare the printed waveform strip against your Sector Signature Guide before slotting any Data Slate into the Clearance Dock.",
                "",
                "NOTE: Salvaged coil runs hot. Print speed is suboptimal. Do not tamper with housing. Brengop Management will bill replacement costs directly to your Berth Lease."
            ];
        }

        // Variant B (Day2SuspectDetained == true): Boarding team confirmed falsified logs overnight
        return
        [
            "VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            "INCIDENT REPORT [KDF-SEC-09B]:",
            $"Overnight, Kordani Security boarding teams investigated detained vessel {safeReg} and confirmed its declared origin ({safeOrigin}) was falsified.",
            "",
            "ADMINISTRATIVE NOTICE [BMC-OPS]:",
            "Because Booth 04 only caught the breach on a hunch while the Warp Field Residue Spectrometer was offline, Kordani Security has forced immediate maintenance compliance anyway. A replacement thermal coil has been pulled from Deck 4 salvage and wired into your console.",
            "",
            "NEW MANDATORY PROTOCOL:",
            "Effective immediately, ALL incoming vessels must undergo a Warp Field Residue print-check. Compare the printed waveform strip against your Sector Signature Guide before slotting any Data Slate into the Clearance Dock.",
            "",
            "NOTE: Salvaged coil runs hot. Print speed is suboptimal. Do not tamper with housing. Brengop Management will bill replacement costs directly to your Berth Lease."
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
                "ADMINISTRATIVE NOTICE [BMC-OPS]:",
                "Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer) remains DEFERRED pending salvage allocation. Continue standard Day 01 verification protocols.",
                "",
                "ACTIVE PROTOCOL CHECKS:",
                "1. Enforce Shields Down & Weapons Cold (check SPS for spoofed mounts).",
                "2. Verify Registration Prefix against physical hull silhouette.",
                "3. Detain all UNR- unregistered transponders.",
                "4. Track Expected Ship List priority arrivals (wrongful detainment of valid Expected Ships incurs a Kordani Security Strike)."
            ];
        }

        return
        [
            $"VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY {day:D2}",
            "CLASSIFICATION: BULWARK PACT INTERNAL",
            "----------------------------------------------------------",
            "ACTIVE PROTOCOL CHECKS:",
            "1. Enforce Shields Down & Weapons Cold (check SPS for spoofed mounts).",
            "2. Verify Registration Prefix against physical hull silhouette.",
            "3. Detain all UNR- unregistered transponders.",
            day >= 3
                ? "4. Verify printed Warp Field Residue waveform strip against Sector Signature Guide (Tab 3)."
                : "4. Warp Field Residue Spectrometer offline (MAINTENANCE PENDING)."
        ];
    }
}
