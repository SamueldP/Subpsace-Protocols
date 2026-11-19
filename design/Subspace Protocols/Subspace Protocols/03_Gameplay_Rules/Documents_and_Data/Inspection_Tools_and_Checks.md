# Inspection Tools & Checks

## 1. Pre-Clearance Shields & Weapons Check
- **Rule:** Every ship must approach with **shields down** and **weapons powered off**.
- **Visual Violation Tells ([[Procedural_Art_Bible_and_Asset_Spec]]):**
  - **`shieldsUp: true`:** Animated `1px` dithered teal/cyan oval shimmer perimeter around the ship hull in the live viewport (shields are checked visually).
  - **`weaponsHot: true`:** Pulsing `#dc2626` crimson glow pixels on dorsal/bow weapon mounts in the live viewport.
- **Compliance & Late-Game Destroy Override:** If either is active, order the captain to lower shields and/or power down weapons before requesting `"Send subspace protocols."` Hostile captains (`rep_score <= -5`) stall and waste shift time; at `<= -9` on Day 14+, captains refuse outright. Outright refusal requires **Detain**—or **Destroy Ship** if `Kordani rep_score >= +8` on Day 14+ / high tension ([[Core_Loop_Mechanics]]).

---

## 2. Subspace Power-Grid Scanner (SPS — Day 1+)
*(Replaces the old Hull Scanner and manual shield scanner; visual layer specified in `gemini-code-1795085727104.md`.)*
- **Availability:** Available from **Day 1**.
- **Technical Shield-Blocking Rule:** Active shields scatter sensor sweeps and block the SPS—giving a hard technical reason why shields must be dropped before inspection. When `SPS` is toggled on while `shieldsUp === true`, animated static-interference arcs (`#38bdf8` / `#e0f2fe`) render across the shield bubble.
- **Viewport Render Layer (`shieldsUp === false`):**
  1. Dims standard hull pixels into dark navy/slate monochrome (`#0c141d` – `#1c2b3a`) and draws a pulsing **Reactor Core** ellipse.
  2. Draws `1px` animated conduit lines to **four fixed ship hardpoints**:
     - **Forward Weapon Mounts:** `#ef4444` Alert Red if `weaponsHot === true`; dark `#1e293b` if offline. *(Catches spoofed Data Slates claiming weapons are offline.)*
     - **Warp Pylons:** `#f59e0b` Hot Amber if primed/spooled (flags hot pylons primed to bolt during detainment); dim teal `#115e59` if idle.
     - **Bridge Comms Array:** `#38bdf8` Active Cyan if powered; dark/flickering `#475569` if genuinely broken. *(Used on Day 6+ to detect deliberately jammed viewscreens, and on Day 7+ to spot civilian comms routing through military-grade encryption during Internal Affairs stings.)*
     - **Cargo Bay:** `#a855f7` High-Draw Violet if unexplained active power is inside an "empty" hold (life support, active contraband, or cloaking hardware); dim `#1e293b` if standard unpowered cargo.
- **3-Tier Food Sway Mechanic ([[Quarters_Management]], [[_State_Variables]]):**
  1. **No Food (`0 PU — 0 SC`):** **Max sway** (after **2+ days without food**). `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
  2. **Nutrient Paste (`4 PU — 20 SC`):** **Minimal sway**.
  3. **High-Tier Rations (`6 PU — 30 SC`):** **No sway** *(zero effect on machine/printer speeds)*.

---

## 3. Warp Field Residue Spectrometer & Printer (Days 1–2 Offline → Day 3+ Active)
- **Days 1–2 Status:** Physically bolted to the [[Desk]], sitting dark with a yellow **`MAINTENANCE PENDING`** tag over the thermal slot (thermal coil burnout from Brown Dwarf infrared surge).
- **Day 3+ Activation:** After the Day 2 `"Ghost Route"` incident, Kordani Security forces a lower-deck mechanic to wire in a salvaged Deck 4 coil. The salvaged coil runs hot and feeds physical analog waveform strips at a **deliberately tense, slightly slow speed** to compare against the **8 Star Systems** in [[Desk_Reference_Manual_Data]] (`Veridia`, `Korr-Vath`, `Dul-Krag`, `Gharok`, `Oruun-4`, `Caldera Rim`, `Solvane`, and red-flag `The Ashen Rift`).
- **Design Rule:** **NO Mid-Game Permanent Upgrades** — faster thermal printers are intentionally excluded ([[Shop]]).
- **Contraband Use:** Physical warp residue strips can be pocketed using the [[Desk]] hidden pocket and sold at night in the Multi-Buyer Intel Economy.

---

## 4. Stellar Stoker Mass Calibration — Gravity-Strain Gauge (Day 4–5+) `[UNDER REVIEW: Potential pacing cut]`
- **Function `[UNDER REVIEW: Potential pacing cut]`:** Because [[Void_Gate_3]] stabilizes the Brown Dwarf's gravity well, an analog **Gravity-Strain Gauge** on your console displays the ship's real physical **Hull Mass (Tonnage)** once locked onto the beacon.
- **Discrepancy Example `[UNDER REVIEW: Potential pacing cut]`:** If a freighter declares `1,200` tons (`"empty hold"`) while the gauge pins at `3,800` tons, they are concealing dense undeclared ore, heavy military armor, or high-mass Hun-Borian passengers. Granting access to a mismatched mass destabilizes the Stoker and incurs a [[Kordani]] Security Strike.

---

## 5. Automatic Bio-Scanner (Passive 5–10s Load — Day 6+ Bridge & Day 10+ Cargo)
- **Zero-Click Background Operation:** Starts automatically in the background once a ship drops its shields and transmits its [[Data_Slate_and_Telemetry|Data Slate]]—requiring **zero extra clicks or manual mini-games**. Completes over **5 to 10 seconds** while you inspect the Data Slate, check silhouettes, or wait for the Warp Field Residue Printer.
- **Indicator LED Output:**
  - **GREEN Flash:** Biometrics clear and match declared species profile ([[Desk_Reference_Manual_Data]]) and manifest (on Day 10+, cargo holds contain no undeclared life forms).
  - **RED Flash:**
    - *Day 6+ (Bridge/Captain):* Species biometric mismatch (impostor, forged species ID, or Dopple fluctuating cellular baseline).
    - *Day 10+ (Cargo Hold):* Living bio-signature detected in the cargo hold (may be legitimate food, livestock, or agricultural cargo if declared on the manifest, or undeclared stowaways/smuggled specimens requiring **Detain**).
    - `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *(Day 10 rollout note in Part 1, Section 2 also mentions "mandatory secondary inspection" for positive cargo bio-scans without defining a separate mechanic beyond verifying against the manifest / Passive Cross-Reference.)*

---

## 6. Cross-Referenced Contraband Deductions (Passive Layer — No Extra UI Buttons)
Existing tools (Day 1 **SPS**, Day 4–5 **Gravity-Strain Gauge** `[UNDER REVIEW: Potential pacing cut]`, and Day 10 **Automatic Bio-Scanner**) combine passively to reveal *what* a ship is concealing without extra click friction or contraband forms *(any single discrepancy is enough to hit **Detain**)*:

| Tool Combination | Concealed Contraband Type | Examples |
| :--- | :--- | :--- |
| **Heavy Mass** `[UNDER REVIEW: Potential pacing cut]` + **Dark Cargo SPS** + **No Bio-Scan** | **Dense inert contraband** | Undeclared ore, black-market weapons, smuggled military hull armor |
| **Heavy Mass** `[UNDER REVIEW: Potential pacing cut]` + **Violet Cargo SPS** + **Positive Bio-Scan** | **Powered life support for hidden living passengers** | Lower-deck human escapees, fleeing Hun-Borians, smuggled specimens |
| **Normal Mass** `[UNDER REVIEW: Potential pacing cut]` + **Violet Cargo SPS** + **No Bio-Scan** | **Low-mass powered hardware** | Active Concord cloaking coils, illegal medical tech, hidden surveillance relays |

---

## 7. Viewscreen & Broken Camera Protocol (Days 1–5 vs. Day 6+)
- **Days 1–5 (Unreliable Hardware, Audio-Only Allowed):** Because VG-3's comms array runs on aging consortium hardware and Brown Dwarf infrared radiation degrades video signals, the viewscreen often shows static (`mode: 'static'`). Pact rules do **not** require visual confirmation on Days 1–5; process audio-only ships via voice and [[Data_Slate_and_Telemetry|Data Slate]] alone.
- **Day 6+ (Mandatory Visual ID — Kordani Security Directive 06 & Two-Step Workaround):** After the security breach, [[Kordani]] Security repairs the station receiver and enforces mandatory facial matching against the Data Slate Holo-Photo. Because honest freighters still suffer legitimate camera failures (and detaining an Expected Ship incurs a penalty), resolve static viewscreens in two steps:
  1. **Step 1 — SPS Comms-Array Audit:** Toggle the **SPS** over the ship's bridge/comms array.
     - *Powered (`#38bdf8`):* Captain is deliberately jamming or hiding their face → press them to restore feed, or **Detain**.
     - *Dead / Flickering (`#475569`):* Fault is genuine → proceed to Step 2.
  2. **Step 2 — Automatic Bio-Scanner Verification:** Cannot clear on audio alone—rely on the passive **5–10s Automatic Bio-Scanner** verifying cabin atmosphere, body heat, and gravity/mass against the declared species ([[Desk_Reference_Manual_Data]]). A **GREEN** flash authorizes a **Visual Waiver Flag** on the Data Slate; a **RED** flash exposes an impostor/Dopple. `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

---

## 8. Day 15+ Brengop Transit Pass & Earlier Unassigned Suggestions
- **Day 15+ Brengop Transit Pass:** Cross-check paid Transit Pass against **ship name**, **starting location**, **end location**, and **registration number** ([[Daily_Rule_Escalation]]).
- **Desk Reference Manual (`3:4` Vertical):** CRT Amber procedural ship silhouettes (`mode: 'manual'`), 8 Star System waveforms, and 12-Species Biometrics ([[Desk_Reference_Manual_Data]]).
- **Orphaned Earlier Suggestion `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`:** Live viewport scanning for *"physical hull damage / mismatched registry numbers"* (`Subpsace protocols info.pdf` Section 2) has no dedicated reticle tool since the old Hull Scanner was replaced by the internal-conduit SPS.
