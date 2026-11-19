# Expected Ship List, Traffic Rules & Scripted Shift Briefings

## Expected Ship List (Daily Schedule — Built-In Double Bind)
*(From `Subspace Protocols - Game Design & Lore Document_3.pdf` Part 1, Section 1, with confirmed user override on warning/fine order)*
- **Daily Manifest List:** Issued daily by **[[Brengop]] Mercantile Operations**, listing **10 potential priority arrivals** for the shift at [[Desk|Booth 04]].
- **Shift Sampling:** Because a **4-minute 30-second (`270s`)** watch only processes **4 to 8 ships (`6` average)**, **2 to 3 of the first 5 ships** are drawn from this list, while the rest create queue uncertainty.
- **No Missing-Ship Reporting:** Because only 4 to 8 vessels arrive out of the 10 listed per shift, unarrived ships are expected and **do NOT need to be logged or reported** at shift end.
- **Random Compliance & Pact Friction Double-Bind:**
  - Whether a listed ship arrives legally compliant or with an infraction is rolled randomly.
  - **Detaining a Scheduled Ship Without Airtight Cause:** Triggers a non-monetary **[[Kordani]] Security Strike** (`3` strikes triggers an audit; maximum strikes triggers Security Detainment).
  - **Waving Through an Expected Ship with a Minor Discrepancy *(Confirmed User Override)*:** Grants reputation points (`+3 rep_score`, including with [[Kordani]] / the ship's faction) and **consumes `1` of the `2` free clerical warnings per shift**—incurring the **`-10 SC` Brengop administrative fine** only after the 2 free warnings are exhausted *(not an immediate fine)* while boosting the faction's `power_score` (`+1`).

---

## Day 2 Hardcoded Encounter — `"The Ghost Route"` (Mini-Dialogue Engine)
*(From `Subspace Protocols - Game Design & Lore Document_3.pdf` Part 1, Section 3, with canonical prefix **`FTC-`** applied)*
- **Eligible Pool:** Randomly rolled Faction and Species, strictly excluding [[Galactic_Union]] (`GUV-`) and [[The_Bulwark_Pact|Bulwark Pact]] (`KDF-`, `BMC-`). Eligible registries: `KRW-`, `HBR-`, `PRJ-`, `ORN-`, `TLV-`, `FTV-`, `FTC-`, `MNG-`, or `ZNI-`.
- **Inspection State:** Shields drop cleanly, weapons are completely cold on the SPS, cargo bay shows zero abnormal power draw, hull silhouette matches the registration prefix, and the [[Data_Slate_and_Telemetry|Data Slate]] is 100% spotless. *(Viewscreen is static/optional on Day 2, and the Warp Field Residue Printer is still offline.)*
- **Mini-Dialogue Engine Structure (Modular Slots):**
  - **Player Auto-Prompt:** `"Send subspace protocols."`
  - **Captain Upload Line (`[SLOT_GREETING]` by Faction Tone):**
    - `[[Krushvak]] (KRW-):` *"My protocols are clean, human. Do not waste a warrior's time staring at numbers."*
    - `[[The_Concord|Hun-Borian]] (HBR-):` *"Transmitting now. Keep it brief. I have no desire to linger under Kordani guns."*
    - `[[Independent_and_Minor_Species|Orun]] (ORN-):` *"Telemetry transmitted. Every checksum is verified to the fourth decimal. You will find no fault."*
    - `[[Independent_and_Minor_Species|Prunjun]] (PRJ-):` *"Sending the bright data-packet now! Very fast, very legal, no need to look too close!"*
    - `[[Independent_and_Minor_Species|Tolvak]] / [[Commercial_Organizations|Commercial Orgs]] (TLV-, FTV-, FTC-, MNG-, ZNI-):` *"Protocols sent. Look, my schedule is tight—stamp the slate and open the lane."*
  - **Player Auto-Interrogation:** `"Confirm route, purpose of visit, and cargo."`
  - **Captain Suspect Declaration (`[SLOT_ROUTE_SLIP] + [SLOT_CARGO_EXCUSE]`):**
    - *Base Script:* `"Inbound from [DECLARED_ORIGIN]—uh, I mean, departed [DECLARED_ORIGIN] four cycles ago. Heading to [DESTINATION]. Purpose is [SLOT_PURPOSE]. Hold is carrying [SLOT_CARGO]."`
    - *Faction-Specific `[SLOT_PURPOSE]` & `[SLOT_CARGO]` Inserts:*
      - **Krushvak:** `"routine patrol resupply"` / `"sealed spare reactor baffles. Standard war surplus—nothing that concerns a Pact clerk."`
      - **Hun-Borian:** `"private relocation"` / `"personal ballast and agricultural seed crates. Nothing military, if that is what your sensors are hunting for."`
      - **Orun:** `"scheduled archival delivery"` / `"unindexed mineral survey canisters. The manifest date? A... minor clerical recalculation on my departure port."` *(Extra suspect for an Orun.)*
      - **Prunjun:** `"buying shiny hull parts"` / `"totally normal scrap metal! Definitely did not stop in any scary red nebulae along the way!"`
      - **Mining Guild (`MNG-`):** `"ore transport"` / `"refined silicate slag. Why is the hull dust scorched? Just... standard asteroid grit. Nothing from the dead sectors."`
      - **Far-Line (`FTC-`) / Zenith (`ZNI-`):** `"charter transit"` / `"sealed passenger provisions. We took a slight detour for—navigation calibration. My slate says [DECLARED_ORIGIN], so that is where I came from."`
      - **Free Traders (`FTV-`) / Tolvak (`TLV-`):** `"independent trade"` / `"industrial lubricants and salvage filters. Look, if your route-scanner is dead, we are done here, right?"`
  - **Player Optional Pushback (`[INSPECT_ROUTE]`):** `"Your departure timing does not match the [DECLARED_ORIGIN] lane."`
  - **Captain Deflection:** `"Your station's own spectrometer is dark, Port Officer. My slate is green, my weapons are cold, and my papers are legal. Unless you want to explain to Kordani Command why you detained a clean vessel on a hunch, clear my dock."`
- **Outcome:** If cleared, the ship slips through—triggering the Day 3 security alert. *(If the player takes a gamble and detains them anyway without hard proof, Kordani Security still cites them for an unverified detainment on Day 2, only to quietly discover the ship's forged route overnight and repair the printer anyway.)*

---

## Day 3 Morning Console Briefing — Subspace Shift Directive
Replaces *Papers, Please*'s paper newspaper/bulletin with a scrolling amber CRT `PACT PORT AUTHORITY — SHIFT DIRECTIVE` printed/displayed at your console when you sit down:

```text
==================================================
VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE - DAY 03
CLASSIFICATION: BULWARK PACT INTERNAL
==================================================
INCIDENT REPORT [KDF-SEC-09]:
At 02:14 station standard, long-range perimeter relays flagged vessel
[DAY_2_SHIP_REG] departing Void Gate 3 on an unlogged vector. Post-transit
telemetry confirmed its declared origin ([DECLARED_ORIGIN]) was falsified.

ADMINISTRATIVE NOTICE [BMC-OPS]:
Maintenance request #4412 (Booth 04 Warp Field Residue Spectrometer - thermal
coil burnout from Brown Dwarf infrared surge) was previously deferred under
Brengop cost-containment policy. Following Kordani Security intervention, a
replacement coil has been pulled from Deck 4 salvage and wired into your
console.

NEW MANDATORY PROTOCOL:
Effective immediately, ALL incoming vessels must undergo a Warp Field Residue
print-check. Compare the printed waveform strip against your Sector Signature
Guide before slotting any Data Slate into the Clearance Dock.

NOTE: Salvaged coil runs hot. Print speed is suboptimal. Do not tamper with
housing. Brengop Management will bill replacement costs directly to your Berth
Lease.
==================================================
```

---

## Daily Queue Structure, Prefixes & Traffic Tiers
- **Queue Mix:** Standard procedurally generated ships (deterministically seeded via `hashSeed` / `Mulberry32`) mixed with [[Scripted_Queue_Characters|scripted loop interruptions]].
- **Traffic Tiers (`power_score` — [[_State_Variables]]):**
  - **High Traffic (`~18–25 ships/run`):** `KDF-` ([[Kordani]]), `BMC-` ([[Brengop]]), `FTV-` ([[Commercial_Organizations|Free Traders]]), `MNG-` ([[Commercial_Organizations|Mining Guild]]).
  - **Mid Traffic (`~8–15 ships/run`):** `KRW-` ([[Krushvak]]), `FTC-` ([[Commercial_Organizations|Far-Line Transit Corp]]), `ZNI-` ([[Commercial_Organizations|Zenith Interstellar]]), `TLV-` ([[Independent_and_Minor_Species|Tolvak]]), `ORN-` ([[Independent_and_Minor_Species|Orun]]), `PRJ-` ([[Independent_and_Minor_Species|Prunjun]]).
  - **Rare Traffic (`~4–8 ships/run`):** `GUV-` ([[Galactic_Union]] — official ships rare; decommissioned `isDecommissioned: true` civilian/cargo hulls appear out on the rim), `HBR-` ([[The_Concord|Hun-Borians]] — uses `FTV-` schematic).
  - **Unregistered / Missing Transponder (`UNR-`):** `[TODO: Fill in manually - define traffic handling and hull pool for UNR- vessels]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
- **Downstream Spawns & Hull States (`Core_Loop_Mechanics`):** Crossing `Dominant` or `Weakened` `power_score` thresholds alters traffic 2–3 shifts later (e.g., `MNG-` crossing `+8` spawns more armed frigates than ore rigs and causes `FTV-` ships to arrive with visible hull scorch marks; `KDF-` crossing `+8` increases military transport frequency).

[TODO: Fill in manually - day-by-day Expected Ship List templates beyond Day 1–3]
