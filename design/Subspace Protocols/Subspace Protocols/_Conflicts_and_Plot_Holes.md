# Contradictions, Outdated References & Plot Holes Audit

This log is the result of a full-vault consistency audit across `_Inbox_Sources` (`Chat_Additions.txt`, `Subpsace protocols info.pdf`, `Subspace Protocols - Game Design & Lore Document.pdf`, `subspace_protocols_art_bible_procedural_asset_spec.md`, `Subspace Protocols - Game Design & Lore Document_2.pdf`, `gemini-code-1795085727104.md`, and `Subspace Protocols - Game Design & Lore Document_3.pdf`), root `Scratchpad.md`, and all organized notes (`00_System_Index` through `08_Physical_Look`).

Confirmed canon overrides have been applied across all organized vault notes (see **Resolved & Confirmed Overrides** below). All remaining unresolved contradictions, cut/under-review items, outdated source references, orphaned state variables, unstandardized terms, and plot holes are logged below by category with `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` markers placed in the affected notes.

---

## Resolved & Confirmed Canon Overrides (Applied Across Organized Vault Notes)
1. **Void Gate 3 Origin, Transit & Legacy Software:** Built independently by **Far-Line Transit Corp (`FTC-`)** and foreclosed on by the [[Brengop]] via loan collateral loophole (FTC is a large company that only defaulted on VG-3 due to short-sightedness about post-war traffic). Ships can detour around VG-3 at high cost, making VG-3 a practical shortcut despite steep fees. Booth consoles run **legacy FTC software**.
2. **Concord War History:** Decades of historical war between [[Galactic_Union]] and [[Krushvak]], followed by **~40 years of pre-war cold peace** without open war; strictly non-military [[Brengop]] profiteers; mid-retreat wormhole collapse.
3. **Alliance, Species & Removed Elements:** Canonical alliance is **[[The_Bulwark_Pact]]**; 5th GU species is **Veldor**; `GUV-` ships are confirmed rare visitors; **Hun-Borian Free Vessels (`HBR-`)** use the **`FTV-`** ship schematic. `Zulak`, `Vesh`, and `Prenn` are completely removed (replaced by [[Commercial_Organizations]]), and all `Rebels` / `rebel ion fields` / `rebel ion spikes` references are removed.
4. **Scanner Consolidation, Upgrades & Terminal Markets:** **Subspace Power-Grid Scanner (SPS)** replaces the old `Hull Scanner`, manual shield scanner, and automated shield upgrade. Generic `Shop` is replaced by **Pact Requisition (Official)** and **Deck 4 Exchange (Grey Market)** terminal tabs. **NO Mid-Game Permanent Upgrades** is confirmed.
5. **Food, Life Support, Auto-Interrogation & Expected Ship Fines:** `Life Support / Air Scrubber`, `Scrubber Filter Cartridge`, `Stim-Caff Ampoule`, and `Dry Ration Brick` are excluded; the old 3-tier food system (`No Food 2+ days = Max sway`, `Nutrient Paste = Minimal sway`, `High-Tier Rations = No sway`) is preserved using the new pricing (`0 SC`, `4 PU / 20 SC`, `6 PU / 30 SC`). Missing-ship reporting is removed. Waving through an Expected Ship with a minor discrepancy awards faction/Kordani `rep_score` (`+3`) and consumes `1` of the `2` free shift warnings before triggering a `-10 SC` fine. **Auto-Interrogation** is enabled from **Day 1**.
6. **Cut & Under-Review Features:** **Docking Drone Hull-Cam Sweep** is cut for initial playtesting. All references to the Day 4–5 **Gravity-Strain Gauge (tonnage check)** are tagged `[UNDER REVIEW: Potential pacing cut]`.
7. **Standardized Terms:** `Data Slate`, `The Bulwark Pact`, `Veldor`, `FTC-`, `legacy FTC software`, and `ORN-442xxx`.
8. **Target Platform & Rendering Pipeline:** Organized notes target a standalone native Windows desktop executable (`.exe`) rendering to a fixed `640 × 360` offscreen virtual framebuffer with integer scaling, nearest-neighbor (point) texture filtering, and local disk JSON save files. Legacy web/HTML5/JavaScript references in `_Inbox_Sources/subspace_protocols_art_bible_procedural_asset_spec.md` (`HTML5 <canvas>`, `ctx.imageSmoothingEnabled = false`, CSS `image-rendering: pixelated`, `js/procedural_art.js`, JS snippets) are superseded across all organized vault files.

---

## 1. Lore / Timeline
1. **Phase 0 GU–Krushvak War Omission in `Document_3.pdf`:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 2, Section 2 — Phase 0), `Concord_War_Timeline.md`, `Galactic_Union.md`, `Krushvak.md`.
   - *Issue:* Part 2, Section 2 of `Document_3.pdf` states only that Krushvak and GU had *"longstanding rivalries without open war"*. Per confirmed user override, vault notes preserve **decades of historical war, followed by roughly 40 years of pre-war cold peace without open war**.
2. **Earth / Obulon Nebula Residue Example vs. 8 Sector Star Systems:**
   - *Files:* `Subpsace protocols info.pdf` (Section 2), `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 3, Section 1), `Desk_Reference_Manual_Data.md`, `FTL_and_Stellar_Stoker.md`, `Brown_Dwarf_System.md`.
   - *Issue:* `Subpsace protocols info.pdf` gives *Earth → Obulon Nebula* as a warp-residue rule, whereas `Document_3.pdf` Part 3, Section 1 defines the 8 official Day 3+ star systems (`Veridia`, `Korr-Vath`, `Dul-Krag`, `Gharok`, `Oruun-4`, `Caldera Rim`, `Solvane`, and `The Ashen Rift`), with Earth omitted from the 8-system chart.
3. **Leftover `"rebel ion spikes"` in `Document_3.pdf` Day 3 Notes:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 2 — Day 3, p. 4 vs. Part 3, Section 1, p. 18), `Daily_Rule_Escalation.md`, `Desk_Reference_Manual_Data.md`.
   - *Issue:* Page 4 of `Document_3.pdf` still mentions `(e.g., zigzag vs. sine-like patterns, rebel ion spikes)` parenthetically, whereas Part 3, Section 1 replaces rebel ion fields with **The Ashen Rift** (*Restricted Concord War-Graveyard / Ion Nebula*, Chaotic Static Spikes). Per user override, all `rebel ion spikes` references are removed from organized vault notes.
4. **Undecided Universe Name & Undefined Geography / War Dates:**
   - *Files:* `Subpsace protocols info.pdf` (Section 1), `_Project_Overview.md`, `Brown_Dwarf_System.md`, `Void_Gate_3.md`, `Concord_War_Timeline.md`.
   - *Issue:* Universe Name remains undecided (`[TODO]`), calendar dates/duration of the Concord War are missing, and names/map locations for the Brown Dwarf, dark planet, outer gas giants, Hun-Borian homeworld, and the other 3 Void Gates remain undefined.

---

## 2. Factions / Species
1. **Unregistered / Missing Transponder (`UNR-`) Prefix Has No Rules or Hull Spec:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 2, Section 5 in prior pass / `Document_2.pdf`), `Data_Slate_and_Telemetry.md`, `Expected_Ship_List_and_Traffic.md`, `Daily_Rule_Escalation.md`.
   - *Issue:* `UNR-` is tracked in `Data_Slate_and_Telemetry.md` and `Expected_Ship_List_and_Traffic.md`, but no Day 1–17 rule specifies how `UNR-` vessels are resolved or which hull schematics they use.
2. **`Krushvak` Conceptual Inspiration vs. Visual Design Rule:**
   - *Files:* `Subpsace protocols info.pdf` (Sections 4–5), `subspace_protocols_art_bible_procedural_asset_spec.md` (Section 5), `Krushvak.md`, `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Older lore docs call Krushvak *"Klingon-inspired"*, whereas the Art Bible mandates they are visually *"explicitly distinct from Klingons"* (vertical twin-crest or V-shaped brow ridge and mutton-chop sideburns).
3. **Incomplete Main Questlines (`Paths B, C, D`) & Missing Cultural/Dialogue Details:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 7), `Endings_and_Story_Paths.md`, `Kordani.md`, `Brengop.md`, `The_Concord.md`, `Galactic_Union.md`, `Helani.md`, `Independent_and_Minor_Species.md`, `Commercial_Organizations.md`.
   - *Issue:* Creator placeholders remain open for **Path B** (exact 5 Kordani quests), **Path C** (exact Brengop quests), **Path D** (exact Masters/Dopple quests & discovery triggers), Dopple booth detection tells beyond `crudeCrayon: true` and Bio-Scanner drift, and cultural/dialogue quirks for GU/minor/commercial species.

---

## 3. Gameplay Mechanics
1. **Removal of `"Deny / Turn Back"` vs. Binary `Grant Access / Detain` (`On the Fence`) & Leftover PDF Phrasing:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 1 vs. Part 2, Section 3, p. 16), `Core_Loop_Mechanics.md`, `Desk.md`, `Data_Slate_and_Telemetry.md`, `Galactic_Union.md`, `Visual_Direction_and_UI_Layout.md`.
   - *Conflict:* `Document_3.pdf` Part 1, Section 1 removes `"Deny / Turn Back"` in favor of binary **Grant Access vs. Detain** (+ conditional **Destroy Ship**), yet Part 2, Section 3 (p. 16, under *Galactic Union*) still reads: *"Denying a GU vessel is even rarer and only happens due to petty Bulwark Pact red tape..."*. Additionally, the user is currently on the fence about whether to keep `"Deny / Turn Back"` alongside `Detain` (`[TODO]` placed in `Desk.md`, `Core_Loop_Mechanics.md`, `Data_Slate_and_Telemetry.md`, and `Visual_Direction_and_UI_Layout.md`).
2. **Replacement of Generic `02_Locations/Playable/Shop` & Status of Older Shop Items (`Kordani Military Insignia`, `Faction Desk Trinkets`, `Faster Thermal Printers`):**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 5), `Chat_Additions.txt` (lines 7, 27–29), `Scratchpad.md` (lines 23–28), `Shop.md`, `Quarters.md`, `Desk.md`, `Kordani.md`, `Core_Loop_Mechanics.md`, `_State_Variables.md`.
   - *Conflicts:*
     - The single `Shop` tab/room is permanently replaced by **Pact Requisition (Official)** and **Deck 4 Exchange (Grey Market)** on the Quarters terminal.
     - **NO Mid-Game Permanent Upgrades** explicitly excludes older suggestions like **Faster Thermal Printers** (`Subpsace protocols info.pdf` Section 3).
     - **Kordani Military Insignia** and **Faction Desk Trinkets** do not appear in `Document_3.pdf`'s Approved Purchases list (and overlap with the **50 SC Brengop "Audit Deferral" Fee** and `rep_score` mechanics), but remain flagged `[TODO]` as under user consideration.
3. **Hybrid Food System vs. `Document_3.pdf` Food Synthesizer / Dry Ration Brick / Life Support / Stim-Caff:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 5), `Chat_Additions.txt` (lines 20–25), `Quarters.md`, `Quarters_Management.md`, `Shop.md`, `_State_Variables.md`, `Inspection_Tools_and_Checks.md`.
   - *Conflicts & Gaps:*
     - `Document_3.pdf` introduced `Life Support / Air Scrubber (3 PU — 15 SC)`, `Scrubber Filter Cartridge (20 SC)`, `Stim-Caff Ampoule (15 SC)`, `Dry Ration Brick (0 PU — 10 SC)`, and `Food Synthesizer (Standard 4 PU — 20 SC / High-Grade 6 PU — 30 SC)`. Per user overrides, Life Support, Scrubber Filters, Stim-Caff, and Dry Ration Bricks are removed, while the old 3-tier food system is retained at the new prices (`No Food = 0 SC`, `Nutrient Paste = 4 PU / 20 SC`, `High-Tier Rations = 6 PU / 30 SC`).
     - Still undefined: SPS sway after **only 1 day without food** (since Max sway triggers after 2+ days), whether High-Tier Rations' zero sway lasts the whole shift (`Document_3.pdf`) or *"a few seconds"* (`Chat_Additions.txt`), and how clicking the in-room **Food Replicator** in `Quarters.md` interacts with **Pact Requisition**.
4. **Expected Ship List — Removal of Missing-Ship Reporting & Clerical Warning Rule:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Sections 1 & 5), `Subspace Protocols - Game Design & Lore Document_2.pdf` (Part 1, Section 1), `Expected_Ship_List_and_Traffic.md`, `_State_Variables.md`.
   - *Issue:* Missing-ship reporting from `Document_2.pdf` is explicitly removed in `Document_3.pdf`. Also, `Document_3.pdf` Part 1, Section 1 wrote that waving through a minor discrepancy *"risks a -10 SC Brengop clerical fine"*, whereas the user clarified it grants faction/Kordani `rep_score` and **consumes `1` of the `2` free shift warnings** before incurring `-10 SC` after warnings run out.
5. **Undefined Failure Threshold: `3 Security Strikes` Audit vs. `"Maximum Kordani Security Strikes"` Game Over:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Sections 1, 5, 7), `_State_Variables.md`, `Endings_and_Story_Paths.md`, `Kordani.md`.
   - *Gap:* **3 Kordani Security Strikes** triggers a mandatory booth/quarters audit/search, while **Security Detainment** Game Over triggers at *"maximum Kordani Security Strikes"* without defining the exact numerical cap (`[TODO]` placed in `_State_Variables.md`, `Kordani.md`, `Endings_and_Story_Paths.md`).
6. **Cut `Docking Drone Hull-Cam Sweep` & Day 6+ Broken Camera Step 2:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (Part 1, Section 4, p. 8), `Inspection_Tools_and_Checks.md`, `Daily_Rule_Escalation.md`, `Desk.md`, `Visual_Direction_and_UI_Layout.md`.
   - *Issue:* `Document_3.pdf` framed Step 2 of the Day 6+ Broken Camera Protocol as a trade-off between **Automatic Bio-Scanner** and **Docking Drone Hull-Cam Sweep** (which cost extra shift time). Because the user cut the Docking Drone Hull-Cam Sweep for initial playtesting, Step 2 relies solely on the passive Automatic Bio-Scanner (`GREEN` = Visual Waiver Flag, `RED` = mismatch) without an alternative manual fallback.
7. **`Gravity-Strain Gauge` `[UNDER REVIEW: Potential pacing cut]`:**
   - *Files:* `Document_3.pdf` (Part 1, Sections 1–2), `Inspection_Tools_and_Checks.md`, `Desk.md`, `Core_Loop_Mechanics.md`, `Daily_Rule_Escalation.md`, `FTL_and_Stellar_Stoker.md`, `_State_Variables.md`, `Visual_Direction_and_UI_Layout.md`, `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Day 4–5 **Gravity-Strain Gauge** (and its rows in the Cross-Referenced Contraband Deductions table) is flagged across all files for potential removal.
8. **Day 2 vs. Day 3 Auto-Interrogation, Day 10 Bio-Scan Secondary Check & Intermediate Days:**
   - *Files:* `Document_3.pdf` (Part 1, Sections 2–3), `Chat_Additions.txt`, `Daily_Rule_Escalation.md`, `Inspection_Tools_and_Checks.md`.
   - *Issues:* `Document_3.pdf` listed Auto-Interrogation under Day 3 while using it in the Day 2 encounter (resolved by user override enabling it on **Day 1**). Day 10 Cargo Bio-Scans (`Document_2.pdf` phrased this as *"mandatory secondary inspection"*; `Document_3.pdf` simply checks against manifest / Passive Cross-Reference). Days 7–9, 11–13, 16, and exact Day 17 conflicting Pact directives remain `[TODO]`.
9. **Orphaned Older Mechanics, Variables & Side Quest Blanks:**
   - *Files:* `Scratchpad.md`, `Inspection_Tools_and_Checks.md`, `_State_Variables.md`, `Quarters.md`, `Helani_Courier_Warp_Signature.md`, `Orun_Freighter_442.md`.
   - *Issues:*
     - *Overnight Tool Charging:* Root `Scratchpad.md` (lines 7–8, 21) contradicts all PDFs forbidding overnight work-tool charging.
     - *Orphaned Exterior Hull-Damage / Registry Check:* `Subpsace protocols info.pdf` Section 2 open suggestion has no dedicated reticle tool since SPS replaced Hull Scanner.
     - *Orphaned Tension Meter:* Older `Brengop/Kordani Tension Meter` (`_State_Variables.md`) lacks integration rules with new per-faction `rep_score` / `power_score`.
     - *Side Quest & Room Props Blanks:* `Helani_Courier_Warp_Signature.md` recipient faction; `Orun_Freighter_442.md` (`ORN-442xxx`) quest giver, invalid fields, and bonus reward; and remaining `Quarters.md` props (`"and whatever else"`).

---

## 4. Art / UI Spec
1. **Standard Clean GU Primary White Hex Mismatch (`#f1f5f9` vs. `#e6eef5`):**
   - *Files:* `subspace_protocols_art_bible_procedural_asset_spec.md` (Section 4), `gemini-code-1795085727104.md`, `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Art Bible defines GU Primary White as `['#64748b', '#94a3b8', '#cbd5e1', '#f1f5f9']`, whereas `gemini-code-1795085727104.md` refers to swapping `#e6eef5`.
2. **Weapons-Hot Alert Red Hex Difference (`#dc2626` Live Glow vs. `#ef4444` SPS Conduit):**
   - *Files:* `subspace_protocols_art_bible_procedural_asset_spec.md` (Sections 2–3), `gemini-code-1795085727104.md`, `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Live viewport `weaponsHot` mount glow uses `#dc2626`, whereas SPS Forward Weapon Mount conduit lines use `#ef4444`.
3. **Shield-Up Palette Difference (`1px` Teal/Cyan Shimmer vs. `#38bdf8` / `#e0f2fe` Static Arcs):**
   - *Files:* `subspace_protocols_art_bible_procedural_asset_spec.md` (Section 3), `gemini-code-1795085727104.md`, `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Normal live mode uses un-hexed teal/cyan shimmer, whereas SPS mode draws `#38bdf8` / `#e0f2fe` static-interference arcs.
4. **`#dc2626` Palette Label (`"Denied clearances"`) vs. Removed `"Deny / Turn Back"`:**
   - *Files:* `subspace_protocols_art_bible_procedural_asset_spec.md` (Section 2), `Procedural_Art_Bible_and_Asset_Spec.md`.
   - *Issue:* Palette table still mentions *"Denied clearances"* under `#dc2626` while `"Deny / Turn Back"` was dropped in `Document_3.pdf` Part 1, Section 1 (and remains on the fence for the creator).
5. **Missing Pixel/Layout Specs for New `Document_3.pdf` Booth Widgets:**
   - *Files:* `Visual_Direction_and_UI_Layout.md`, `Procedural_Art_Bible_and_Asset_Spec.md`, `Desk.md`.
   - *Issue:* Exact pixel dimensions/UI mockups are not yet defined in the Art Bible for the scrolling **Morning Shift Directive**, **Instant Citation** amber strip/buzzer, **Automatic Bio-Scanner** LED indicator, yellow **`MAINTENANCE PENDING`** tag, **Gravity-Strain Gauge** `[UNDER REVIEW: Potential pacing cut]`, **Destroy Ship** override switch, **Shielded Pocket Lining** visual indicator, or **Subspace Frequency Tuning** rotary dial.

---

## 5. Terminology
1. **`FLT-` vs. Canonical `FTC-` and `"legacy human consortium software"` vs. `"legacy FTC software"`:**
   - *Files:* `Subspace Protocols - Game Design & Lore Document_3.pdf` (pages 5, 6, 10, 12, 14, 15, 17, 18), `Subspace Protocols - Game Design & Lore Document_2.pdf`, `gemini-code-1795085727104.md`.
   - *Issue:* The PDFs still write `FLT-` and `"legacy human consortium software"`, which have been standardized across all organized vault notes (`00_System_Index`–`08_Physical_Look`) to **`FTC-`** and **`legacy FTC software`** per user override.
2. **Legacy / Outdated Terms Remaining in Root `Scratchpad.md` and Older `_Inbox_Sources`:**
   - *Files:* `Scratchpad.md` (untouched), `Chat_Additions.txt`, `Subpsace protocols info.pdf`, `Subspace Protocols - Game Design & Lore Document.pdf`, `subspace_protocols_art_bible_procedural_asset_spec.md`.
   - *Issue:* While all organized vault notes (`00_System_Index`–`08_Physical_Look`) are clean and standardized, root `Scratchpad.md` still contains `Charger`, `Hull Scanner`, automated shield upgrade, `blank slate`, and `Registry prefix 442`, and older `_Inbox_Sources` files still contain `Data Pad`, `Vrotes`, `Kordani-Brengop Coalition`, `Coalition`, `Zulak`, `Vesh`, `Prenn`, `rebel ion fields`, `rebel ion spikes`, `Docking Drone Hull-Cam Sweep`, `Life Support / Air Scrubber`, `Stim-Caff Ampoule`, and `Dry Ration Brick`.
