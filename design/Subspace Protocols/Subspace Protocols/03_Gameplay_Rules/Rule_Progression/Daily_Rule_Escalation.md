# Daily Rule Escalation & Sector Events

## Progressive Check Escalation (Day 1–17 Rollout)
*(From `Subspace Protocols - Game Design & Lore Document_3.pdf` Part 1, Section 2, with confirmed user overrides applied)*

- **Day 1 (Core Loop Foundation):**
  - Hail → Verify Shields Down & Weapons Off (live viewport + [[Inspection_Tools_and_Checks|Subspace Power-Grid Scanner (SPS)]]) → `"Send subspace protocols."` → **Auto-Interrogation** (`"Confirm route, purpose of visit, and cargo."` — **enabled from Day 1 per confirmed user override**) → Cross-check Registration Prefix and Visual Ship Silhouette/Faction (using [[Data_Slate_and_Telemetry|Data Slate]] & Inspection Manual) → Track [[Expected_Ship_List_and_Traffic|Expected Ship List]] → **Grant Access** (Clearance Dock) or **Detain**.
  - *Spectrometer Tag:* The Warp Field Residue Spectrometer is physically bolted to the desk on Days 1–2, sitting dark with a yellow **`MAINTENANCE PENDING`** tag over the thermal slot.
  - *Viewscreen Rule (Days 1–5):* Unreliable consortium comms hardware + Brown Dwarf infrared interference; audio-only + Data Slate processing is allowed when a captain's camera shows static.
- **Day 2 (Hardcoded Suspect Encounter — `"The Ghost Route"`):**
  - Standard checks continue, plus a mandatory hardcoded scripted vessel whose dialogue is deeply suspicious while every available scan and document checks out cleanly ([[Expected_Ship_List_and_Traffic]]). Because the Warp Field Residue Printer is offline, the player has no hard evidence to detain them—setting up the Day 3 incident.
- **Day 3 (Morning Shift Directive & Warp Field Residue Printer):**
  - Scrolling amber CRT Morning Shift Directive (`VOID GATE 3 // PORT AUTHORITY SHIFT DIRECTIVE — DAY 03`) announces the Day 2 vessel falsified its departure origin; [[Kordani]] Security forces a lower-deck mechanic to wire in a salvaged Deck 4 coil.
  - Pair Auto-Interrogation cross-checks with the **Warp Field Residue Printer**, slowly feeding physical analog waveform strips matched against the **8 Sector Star Systems** in [[Desk_Reference_Manual_Data]] (`Veridia`, `Korr-Vath`, `Dul-Krag`, `Gharok`, `Oruun-4`, `Caldera Rim`, `Solvane`, and red-flag `The Ashen Rift`).
- **Day 4–5 (Stellar Stoker Mass Calibration) `[UNDER REVIEW: Potential pacing cut]`:**
  - Introduce the analog **Gravity-Strain Gauge** to cross-check declared cargo/hull tonnage against actual gravitational mass ([[Inspection_Tools_and_Checks]]).
- **Day 6 (Mandatory Viewscreen & Bridge Bio-Scan — Kordani Security Directive 06):**
  - Viewscreen identity matching becomes mandatory under **Kordani Security Directive 06** (station receiver repaired).
  - The passive 5–10s **Automatic Bio-Scanner** activates bridge-cabin species verification (flashing **GREEN** or **RED** against the 12-Species Biometric Reference Chart in [[Desk_Reference_Manual_Data]]).
  - For static viewscreens, activate the **Two-Step Broken Camera Protocol**: Step 1 (**SPS Comms-Array Audit**) → Step 2 (**Automatic Bio-Scanner** GREEN flash authorizes a **Visual Waiver Flag** on the Data Slate). `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
- **Day 7+ (Kordani Internal Affairs Compliance Stings):**
  - Roughly **1 in 8** Encrypted Subspace Bribe offers (`~12%` on invalid vessels) is secretly a **Kordani Internal Affairs compliance sting** carrying 1–2 subtle tells; accepting triggers **`+2 Kordani Security Strikes`** and confiscates the bribe ([[Core_Loop_Mechanics]]).
- **Day 10 (Automatic Cargo Bio-Scans):**
  - The **Automatic Bio-Scanner** expands to sweep cargo holds during its passive 5–10s cycle. A **RED** cargo bio-flash flags living signatures (legitimate food/livestock/agricultural cargo if declared on the manifest, or undeclared stowaways/smuggled specimens requiring **Detain**). Combines passively with SPS & Mass `[UNDER REVIEW: Potential pacing cut]` for Contraband Deductions ([[Inspection_Tools_and_Checks]]).
- **Day 14+ (Extreme Reputation Thresholds & Station-Defense Override):**
  - At **`rep_score <= -9`** on Day 14+, hostile captains begin refusing to drop shields or power down weapons outright.
  - If **`Kordani rep_score >= +8`** (Day 14+ or high sector tension), Kordani Command unlocks the manual **Destroy Ship** station-defense override switch ([[_State_Variables]]).
- **Day 15 ([[Brengop]] Transit Pass):**
  - The [[Brengop]] institute a paid **Transit Pass** for additional revenue and security. Ships must provide a valid Transit Pass matching their **ship name**, **starting location**, **end location**, and **registration number**.
- **Day 17 (Conflicting Pact Directives — Placeholder Milestone):**
  - Placeholder milestone where [[Kordani]] Security and [[Brengop]] Mercantile Administration issue directly contradictory morning shift memos, forcing the player to choose which Pact boss to satisfy.
  - `[TODO: Fill in manually - finalize the exact Day 17 conflicting Kordani Security vs. Brengop Mercantile morning shift directive scenario]`

- `[TODO: Fill in manually - specify any rules or events for intermediate days (Days 7–9, 11–13, 16) and post-Day 17 endgame schedule]`

---

## Suzerain-Inspired Diegetic World Reactivity & Earlier Open Suggestions
- **Delayed Cause-and-Effect Ripples (2–3 Shift Lag):** Crossing `Weakened`, `Dominant`, or `Crisis` thresholds in hidden `power_score` updates **Morning Shift Directives** and downstream ship spawns/hull states 2 to 3 shifts later ([[Core_Loop_Mechanics]], [[_State_Variables]]).
- **Earlier Open Suggestions:** [[The_Concord|Hun-Borian]] "awakening" riots & peaceful refugees, [[The_Concord|Dopple]] paranoia lockdowns (random Kordani desk audits), [[Krushvak]] border skirmishes, and [[FTL_and_Stellar_Stoker|Stellar Stoker]] power surges distorting warp-residue strips.
