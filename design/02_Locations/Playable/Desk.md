# Playable Location: The Desk (Booth 04 — Customs & Docking Control)

## Overview
- **Location:** Upper-deck Port Officer customs and docking control booth (**Booth 04**) on [[Void_Gate_3]].
- **Architecture:** Docking and sensor consoles run on **legacy FTC software** (aging hardware/software from [[Commercial_Organizations|Far-Line Transit Corp]]; see [[Station_Atmosphere_and_Society]]).
- **Role:** Primary workspace during the **4-minute 30-second (`270s`)** daytime shift loop ([[Core_Loop_Mechanics]]), serving as the player's lens for the sector's politics, trade, and paranoia.
- **Visual Spec ([[Procedural_Art_Bible_and_Asset_Spec]]):** Gritty 16-bit / late-1990s LucasArts DOS VGA industrial booth with cold gunmetal steel bulkheads, oxidized bronze framing, warm amber-gold CRT screens, and `0°` flat orthographic perspective tilt for all desktop items.

## Console & UI Elements
- **External Observation Viewport (`4:3` or `16:9` Top Wide) — Diegetic Timer (No Clock UI):**
  - Houses the `120 × 56` procedural ship texture / virtual framebuffer, visual shields/weapons tells, and the [[Inspection_Tools_and_Checks|Subspace Power-Grid Scanner (SPS)]] overlay.
  - **No Analog or Digital Clock UI:** Shift time (`4m 30s`) is tracked diegetically as the dim, rust-red [[Brown_Dwarf_System|Brown Dwarf]] star slowly arcs across the viewport sky toward the dark planet's horizon / station superstructure ([[Visual_Direction_and_UI_Layout]]).
- **Morning Shift Directive Readout:** Scrolling amber CRT `PACT PORT AUTHORITY — SHIFT DIRECTIVE` displayed at the console at shift start (replacing *Papers, Please*'s paper newspaper; see [[Expected_Ship_List_and_Traffic]]).
- **Main Hailing Comms Viewscreen (`1:1` or `3:4` CRT Bezel):** Houses the `64 × 64` procedural portrait texture / virtual framebuffer displaying the hailing captain (or Bayer-dithered CRT static with `"NO VISUAL SIGNAL"` scanline bars; audio-only allowed on Days 1–5, mandatory matching + SPS Comms-Array Audit on Day 6+).
- **Inspection Desk Surface (`16:9` Flat Top-Down):** Houses physicalized inspection tools, movable slates, manuals, thermal printouts, citation strips, and desk trinkets.
- **"Next Hail" Control:** Triggered by the player to bring the next approaching ship in the queue into the viewport and initiate hailing.
- **Movable [[Data_Slate_and_Telemetry|Data Slate]] (`3:4` Vertical):** Receives downloaded ship telemetry, manifest details, monochrome ID holo-photo, and (on Day 6+) a **Visual Waiver Flag** when authorized, plus Encrypted Subspace Bribe prompts.
- **Sector Signature Guide & Inspection Manual (`3:4` Vertical):** Houses CRT Amber (`#ffb84d` / `#b45309`) procedural ship silhouettes, the **8 Star System Warp Waveforms** (Day 3+), and the **Species Biometric Reference Chart** (Day 6+; see [[Desk_Reference_Manual_Data]]).
- **The Clearance Dock (Replacing the Paper Stamp):** A USB-style hardware port on the right side of the screen/desk terminal. Drag the [[Data_Slate_and_Telemetry|Data Slate]] here and select **Grant Access** (`Clear to Dock`) to transmit encrypted docking clearance back to the ship.
  - `[TODO: Fill in manually - decide whether to keep "Deny / Turn Back" alongside Detain or use binary Grant Access vs. Detain]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
- **Detain Control:** A separate physical desk control/switch in Alert Crimson (`#dc2626`), not the Data Slate clearance upload port. Engages station clamps and calls [[Kordani]] security boarding teams.
- **Late-Game "Destroy Ship" Override Switch:** Manual station-defense override switch unlocked by Kordani Command if (and ONLY if) **`Kordani rep_score >= +8`** on Day 14+ or high sector tension, allowing the player to **Destroy** defiant vessels right in the viewport ([[Core_Loop_Mechanics]]).

## Inspection Devices & Instant Feedback
- **[[Inspection_Tools_and_Checks|Subspace Power-Grid Scanner (SPS)]]:** Available from Day 1. Blocked by active shields; draws live internal power conduits to Forward Weapon Mounts, Warp Pylons, Bridge Comms Array, and Cargo Bay. Sway is controlled by the 3 food states in [[Quarters]] ([[_State_Variables]]).
- **[[Inspection_Tools_and_Checks|Warp Field Residue Spectrometer / Printer]]:**
  - *Days 1–2:* Physically bolted to the desk, sitting dark with a yellow **`MAINTENANCE PENDING`** tag over the thermal slot.
  - *Day 3+:* Repaired with a salvaged Deck 4 coil (billed to Berth Lease); runs hot and slowly feeds physical analog waveform strips matching the 8 systems in [[Desk_Reference_Manual_Data]].
  - *Design Rule:* **NO Mid-Game Permanent Upgrades** — faster thermal printers are intentionally excluded ([[Shop]]).
- **Analog Gravity-Strain Gauge (Day 4–5) `[UNDER REVIEW: Potential pacing cut]`:** Console gauge displaying the ship's real physical mass (tonnage) once locked onto the beacon ([[Inspection_Tools_and_Checks]]).
- **Automatic Bio-Scanner (Passive 5–10s Load — Day 6+ Bridge & Day 10+ Cargo):** Zero-click background scanner whose indicator LED flashes **GREEN** (clear) or **RED** (species mismatch / Dopple instability on Day 6+, or living cargo bio-signature on Day 10+; see [[Inspection_Tools_and_Checks]]).
- **Immediate Error Feedback (Instant Citations):** Whenever a protocol mistake occurs, the console delivers instant feedback seconds after the ship clears or is detained—buzzing audibly and printing/flashing an amber citation strip detailing the exact violation and any fine or Security Strike.
- **[[Expected_Ship_List_and_Traffic|Expected Ship List]]:** Daily schedule of 10 potential priority arrivals issued by [[Brengop]] Mercantile Operations.

## Hidden Pocket & Desk Trinkets
- **Hidden Pocket / Contraband Slot:** Built into the work desk to sneak physical items between the booth and [[Quarters]] (e.g., pocketing a physical warp residue printout strip, or bringing a **Blank Data Slate** [`15 SC` — Deck 4 Exchange] from quarters to copy a target ship's telemetry).
  - **Shielded Pocket Lining (`90 SC` — Deck 4 Exchange):** Optional espionage item that purely conceals **1 smuggled item** in your desk contraband slot during [[Kordani]] 3-Strike sweeps (does not ease inspection checks; see [[Shop]]).
- **Faction Desk Trinkets *(Under Consideration)*:** Physical faction items placed on the desk where a hailing captain can see them on the viewscreen to make captains from that faction more lenient.
- **[[Kordani]] Military Insignia *(Under Consideration)*:** Kept under consideration by the user (`Shop.md`, `_State_Variables.md`). `[TODO: Fill in manually - decide whether to keep Kordani Military Insignia and Faction Desk Trinkets and define their exact pricing and stat effects]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

[TODO: Fill in manually - exact desk layout coordinates and trinket slot capacity]
