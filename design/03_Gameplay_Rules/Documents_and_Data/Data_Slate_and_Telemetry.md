# Documents, Data Slates & Telemetry

## Movable Data Slate (`3:4` Vertical)
- **Download via Subspace:** After shields and weapons are addressed, the player requests the ship's packet via subspace (short-range encrypted data transfer) with the fixed prompt **`"Send subspace protocols."`**, loading telemetry and manifest data onto a movable **Data Slate** on the [[Desk]] (strictly flat `0°` orthographic orientation per [[Procedural_Art_Bible_and_Asset_Spec]]).
- **Inspection Contents:**
  - **ID Holo-Photo (`PortraitRenderer` `mode: 'holo'`):** Reuses the captain's deterministic `64 × 64` portrait buffer quantized to a 5-step cyan/amber monochrome ramp with alternating scanline dimming and corner brackets.
  - Captain and crew details (cross-checked against registered manifests and viewscreen facial matching from Day 6+).
  - Cargo details (plus **Automatic Cargo Bio-Scans** starting Day 10).
  - **Ship Registration Prefixes:** Ships carry inspectable registration prefixes (example: **`ORN-442xxx`** on [[Orun_Freighter_442]], where `xxx` is randomized in-game).
  - **Weapon & Power Status:** Downloaded telemetry can be spoofed (for example, claiming weapons are offline while the [[Inspection_Tools_and_Checks|Subspace Power-Grid Scanner (SPS)]] reveals power still feeding forward weapon mounts).
  - **Visual Waiver Flag (Day 6+):** Authorized onto the Data Slate before docking when a captain's viewscreen camera is genuinely broken (verified via Step 1 SPS Comms-Array Audit and Step 2 Automatic Bio-Scanner **GREEN** flash; see [[Inspection_Tools_and_Checks]]).
  - **Encrypted Subspace Bribes:** When an invalid ship's Data Slate loads (or slots into the Clearance Dock), a quiet secondary prompt may ping over the encrypted channel offering **`30–50 SC`** (max 1/shift, ~15% on invalid non-`GUV-` ships; ~1 in 8 on Day 7+ are Kordani Internal Affairs stings). Accepting creates an unauthorized log entry unless cleared with **Scrub Security Logs (`5 PU / 25 SC`)**.
- **Clearance Dock Submission:** Drag the Data Slate to the right side of the screen and slot it into the USB-style Clearance Dock to select **Grant Access** (`Clear to Dock`). (**Detain** is triggered via a separate physical desk control, and **Destroy Ship** via a late-game Kordani override switch.)
  - `[TODO: Fill in manually - decide whether to keep "Deny / Turn Back" alongside Detain or use binary Grant Access vs. Detain]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

---

## Ship Registration Prefix Quick Reference

| Prefix | Organization / Faction | Linked Note |
| :--- | :--- | :--- |
| **`GUV-`** | Galactic Union Vessel | [[Galactic_Union]] |
| **`KRW-`** | Krushvak War-Vessel | [[Krushvak]] |
| **`KDF-`** | Kordani Defense Fleet | [[Kordani]] |
| **`BMC-`** | Brengop Mercantile Consortium | [[Brengop]] |
| **`HBR-`** | Hun-Borian Free Vessel *(uses `FTV-` schematic)* | [[The_Concord]] |
| **`PRJ-`** | Prunjun Salvage Registry | [[Independent_and_Minor_Species]] |
| **`ORN-`** | Orun Registry Network | [[Independent_and_Minor_Species]] |
| **`TLV-`** | Tolvak Transit Line | [[Independent_and_Minor_Species]] |
| **`FTV-`** | Independent Free Traders | [[Commercial_Organizations]] |
| **`FTC-`** | Far-Line Transit Corp | [[Commercial_Organizations]] |
| **`MNG-`** | Mining Guild | [[Commercial_Organizations]] |
| **`ZNI-`** | Zenith Interstellar | [[Commercial_Organizations]] |
| **`UNR-`** | Unregistered / Missing Transponder `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` | [[Daily_Rule_Escalation]] |

- [TODO: Fill in manually - define inspection/detainment rules for `UNR-` (Unregistered / Missing Transponder) vessels]

---

## Day 15+ Paid Brengop Transit Pass
- **Introduced Day 15 ([[Daily_Rule_Escalation]]):** The [[Brengop]] institute a paid **Transit Pass** for additional revenue and security.
- **Required Matching Fields:** Ships must provide a valid Transit Pass matching their:
  1. **Ship name**
  2. **Starting location**
  3. **End location**
  4. **Registration number**

---

## Desk Reference Manual (`3:4` Vertical) & Physical Printouts
- **[[Desk_Reference_Manual_Data]] (`3:4` Vertical):** Houses CRT Amber (`#ffb84d` / `#b45309`) ship recognition silhouettes (`ShipRenderer` `mode: 'manual'`), the **8 Sector Star Systems & Warp Field Residue Waveforms (Day 3+)**, and the **12-Species Biometric Reference Chart (Day 6+)**.
- **Warp Field Residue Strip (Day 3+):** Slow physical analog waveform strip printed by the [[Inspection_Tools_and_Checks|Warp Field Residue Printer]]. Can be slipped into the [[Desk]] hidden pocket to sneak into [[Quarters]] for side quests or Multi-Buyer Intel sales.
- **Instant Citation Strip:** Amber strip buzzing and printing/flashing seconds after clearing or detaining a vessel whenever a protocol mistake occurs ([[Core_Loop_Mechanics]]).

---

## Blank Data Slates, Forged IDs & Contraband
- **Blank Data Slates (`15 SC` — Deck 4 Exchange):** Brought from [[Quarters]] via the [[Desk]] hidden pocket/contraband slot to copy *any* passing ship's telemetry during a shift (used in side quests like [[Helani_Courier_Warp_Signature]], [[Double Agent]] tasks, and the [[Quarters_Management|Multi-Buyer Intel Economy]]). Creates an **unauthorized log entry** until cleared with **Scrub Security Logs (`5 PU / 25 SC`)**.
- **Forged / Bad Fake IDs (`crudeCrayon: true`):** Per [[Procedural_Art_Bible_and_Asset_Spec]], sloppy forgeries intentionally jitter portrait vertices, flatten Bayer dithering, or use mismatched seed traits on the Data Slate holo-photo.
- **Contraband Trends:** Dense inert contraband (undeclared ore, black-market weapons, smuggled armor), hidden living passengers (lower-deck human escapees, fleeing Hun-Borians, smuggled specimens), low-mass powered hardware (active Concord cloaking coils, illegal medical tech, hidden surveillance relays), forged [[Galactic_Union]] citizen papers, and encrypted [[The_Bulwark_Pact|Pact]] ledgers.

[TODO: Fill in manually - exact Data Slate UI text fields, Transit Pass UI format, and any additional manual rule pages]
