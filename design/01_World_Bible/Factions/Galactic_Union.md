# Galactic Union (GU)

## Overview
- **Ship Registration Prefix:** **`GUV-`** (**Galactic Union Vessel**).
- **Home Hub & Warp Waveform ([[Desk_Reference_Manual_Data]]):** **Veridia** (`GU Border Hub`) — **Smooth Sine Wave** (clean, symmetrical rolling curves with uniform spacing).
- **`power_score` Traffic Tier ([[_State_Variables]]):** **Rare Traffic** (`~4–8 ships/run`; Weakened `<= -2`, Dominant `>= +3`, Crisis `>= +5`).
- **Society:** A genuine post-scarcity society where citizens live comfortably and optimistically (does **not** use Pact Scrip natively; double-agent payouts are small operational slush funds).
- **Member Species (portrait & biometric profiles in [[Procedural_Art_Bible_and_Asset_Spec]] and [[Desk_Reference_Manual_Data]]):**
  - **Humans:** Diverse human races, 6 skin ramps, varied haircuts, optional facial hair, clean GU white/gunmetal collared jackets. *(Standard O2/N2 `1.0G`, Warm `37°C`.)*
  - **[[Helani]]:** Logical, Vulcan-inspired (changed from *Umani*); mostly human skin tones, distinguished by **protruding chins** and sharp angled brows. *(Standard O2/N2 `1.0G`, Warm-Cool `34°C`.)*
  - **Klaasari** (changed from *Klaasen*): Blue and green with **distinctive, beautiful patterned spots** in symmetrical ornamental clusters. *(High-Humidity O2 `1.0G`, Warm `38°C`, trace dermal bioluminescent pigment.)*
  - **Thanes:** Almost **titanium-white** skin, strictly **bald** head, and **short deep-black beards**. *(Cold, Dry O2 `1.1G`, Cool `31°C`, high skeletal mineral density.)*
  - **Veldor:** Require an **O2 + Sulfur Mix** (`1.0G`, Warm `36°C`) atmosphere, so they **always wear full head coverings / sealed helmets** (porcelain-white cowl, brass trim, amber faceplate visor, sulfur canisters, breather tube). *Mandatory sulfur-metabolizing respiratory trace; absence of sulfur triggers an instant RED Bio-Scanner flash.*
  - [TODO: Fill in manually - cultural, political, and lore details for Klaasari, Thanes, and Veldor]

## Starship Visuals, Traffic Rules & Decommissioned Hulls
*(From `Subspace Protocols - Game Design & Lore Document_3.pdf`, `gemini-code-1795085727104.md`, and [[Procedural_Art_Bible_and_Asset_Spec]])*
- **Rare Visitors & Action Conditions:**
  - Official GU (`GUV-`) ships are a **rare sight** at [[Void_Gate_3]], and they **never** offer Encrypted Subspace Bribes (`Core_Loop_Mechanics`).
  - `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` — `Document_3.pdf` Part 2, Section 3 states: *"Denying a GU vessel is even rarer and only happens due to petty Bulwark Pact red tape (like missing a new Transit Pass), post-war decommissioned GU hulls now operated by civilians with outdated/erroneous registry data, or impostors/Dopples forging GU credentials (the rarest case)."* *(Meanwhile, Part 1, Section 1 removes `"Deny / Turn Back"` in favor of binary **Grant Access vs. Detain**—an option the user is currently undecided on.)*
- **Allowed Classes & Visual Design (`120 × 56`):**
  - Official GU fleet operates **all 4 ship categories** (**Military**, **Civilian**, **Cargo / Freighter**, **Diplomatic / Explorer**).
  - Standard hulls: Clean white and gunmetal hulls, smooth curves, forward saucer/ovoid bow, tapered curved fuselage, and dual horizontal side-mounted **Warp Pylons** with `#2dd4bf` teal engine glow.
- **Decommissioned / Rundown Hull Variant (`isDecommissioned: true`):**
  - Weathered, rundown-looking GU hulls are not uncommon out on the rim, but they are **exclusively Civilian and Cargo/Freighter hulls**—**never official GU Military warships** (unless triggered by a special scripted story event, such as the weathered civilian GU cargo runner in Path A).
- **Non-GU Vessels:** Captains of Humans and GU member species also frequently operate ships belonging to multi-species [[Commercial_Organizations]].

## History & Relationship with the Krushvak
*(Preserving confirmed canon override alongside `Document_3.pdf`; see [[Concord_War_Timeline]].)*
- **Pre-War (`Phase 0`):** There were **decades of historical war** between the GU and [[Krushvak]], followed by roughly **40 years of tense pre-war cold peace** without open war prior to [[The_Concord]] arriving. `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *(Note: `Document_3.pdf` Part 2, Section 2 omitted the decades of historical war, listing only "longstanding rivalries without open war.")*
- **Phase 2 — Wartime Alliance:** When [[The_Concord]] struck the [[Krushvak]], the two powers set aside past rivalries; the GU stepped in from behind to back the Krushvak unconditionally, forging a permanently stable post-war alliance.
- **Phase 3 — Trapped Forces:** When the wormhole was destroyed mid-retreat, some GU, [[Krushvak]], and allied ships were tragically trapped on the far side ([[GU_Double_Agent_Objectives]]).

## Relationship to Void Gate 3
- [[Void_Gate_3]] was **not** a Galactic Union station; it was built independently by **[[Commercial_Organizations|Far-Line Transit Corp (`FTC-`)]]** and frontier colonists outside GU rules, and was later foreclosed on by the [[Brengop]].

## Role in Main Story, Intel Economy & Gameplay
- **Path A — The Extraction (*Across the Line*):** Escaping [[Void_Gate_3]] to reach the GU as a double agent (completing e.g. 3 of 5 covert tasks) is the baseline main path ([[Double Agent]], [[Endings_and_Story_Paths]]).
- **Multi-Buyer Intel Economy ([[Quarters_Management]]):** The **Galactic Union Handler** buys `KDF-`, `BMC-` (Pact rebuild intel), `HBR-`, or flagged Concord/Dopple routes for a **Covert Operational Stipend (`20–30 SC`) + GU `rep_score` (`+2`) + Hidden Double-Agent Trust**.
- **Contraband:** Desperate refugees carrying forged GU citizen papers vs. impostors/[[The_Concord|Dopples]] forging GU credentials ([[Data_Slate_and_Telemetry]]).
