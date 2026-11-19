# Kordani

## Overview
- **Ship Registration Prefix:** **`KDF-`** (**Kordani Defense Fleet**).
- **Home Sector & Warp Waveform ([[Desk_Reference_Manual_Data]]):** **Korr-Vath** (`Kordani Garrison Sector`) — **Sharp Sawtooth Wave** (steep vertical rises with slanted downward drops; crisp and rigid).
- **`power_score` Traffic Tier ([[_State_Variables]]):** **High Traffic** (`~18–25 ships/run`; Weakened `<= -4`, Dominant `>= +8`, Crisis `>= +14`).
- **Disposition:** Militaristic, authoritarian faction.
- **Pre-War (`Phase 0`):** Maintained a hostile standoff against the entire galaxy because they disliked everyone ([[Concord_War_Timeline]]).

## Physical Appearance, Biometrics & Starship Visuals
*(From [[Procedural_Art_Bible_and_Asset_Spec]] and [[Desk_Reference_Manual_Data]])*
- **Portrait Appearance (`64 × 64`):** **Lizard-like**, with **cold blue, slate grey, or rare light purple** skin colors (where **light purple is seen as royalty** / high rank). Sharp angular cheekbones and tapering chin, subtle horizontal/angled scale-ridge pixels along cheeks and neck, narrow slit-pupiled eyes, no hair, and rigid high-collared military armor.
- **Biometric Profile (Day 6+ Automatic Bio-Scanner):** **Warm, Arid O2 (`1.0G`)**, **Cool (`29°C`)**, scaled exothermic thermal profile, slow metabolic rate.
- **Ship Classes & Silhouettes (`120 × 56`):**
  - **Allowed Classes:** **Military** and **Diplomatic / Command only** (**no Cargo or Civilian ships**).
  - **Visual Design:** Deep blue-green primary hulls (`#0a2730`–`#2e8b8f`) and dark slate secondary hulls (`#0f172a`–`#475569`) with `#f59e0b` amber engine glow. Sharp dagger-like forward prow, jagged swept-forward wing blades, serrated dorsal spine, angular recessed engine slits, and zero rounded curves.

## Role in the Concord War (`Phase 3`) & The Bulwark Pact
- Entered the war late with an arrogant, all-out assault that shattered [[The_Concord]] offensive, at the cost of crippling their own population and destroying most of their warships.
- *After* the war, the crippled Kordani and wealthy [[Brengop]] formed [[The_Bulwark_Pact]] ("The Pact")—a loose, transactional coalition where the Brengop finance the Kordani rebuild in exchange for warships and military training.
- On [[Void_Gate_3]], Kordani run the military garrison and security administration, drafting a small number of literate humans into upper-deck booths because consoles run on **legacy FTC software** ([[Station_Atmosphere_and_Society]]).

## Gameplay, Security Enforcement & Story Path
- **Kordani Security Strikes & Audits ([[_State_Variables]]):**
  - Triggered by wrongfully detaining an [[Expected_Ship_List_and_Traffic|Expected Ship]], granting access to a mismatched Stoker mass (`[UNDER REVIEW: Potential pacing cut]`), or accepting a Day 7+ sting bribe (`+2 Strikes`).
  - **3 Strikes:** Triggers a mandatory booth/quarters audit/search (exposing un-scrubbed unauthorized Data Slate copies or encrypted bribes unless **Scrub Security Logs [`5 PU / 25 SC`]** was paid). Expunge strikes via **Brengop "Audit Deferral" Fee (`50 SC`)** or selling intel to **Kordani Security**.
  - **Maximum Strikes (`Security Detainment` Failure Ending):** Reaching maximum Kordani Security Strikes, failing a contraband raid, or being exposed as a spy leads to Kordani troopers breaching your [[Quarters]] airlock ([[Endings_and_Story_Paths]]). `[TODO: Fill in manually - define exact maximum Kordani Security Strikes threshold]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
- **Day 6 — Kordani Security Directive 06:** After the Day 2 "Ghost Route" breach, Kordani Security repairs the station receiver and enforces mandatory viewscreen facial matching + **Automatic Bio-Scanner** checks ([[Inspection_Tools_and_Checks]], [[Daily_Rule_Escalation]]).
- **Day 7+ — Internal Affairs Compliance Stings:** Roughly 1 in 8 bribe offers (`~12%`) is secretly a Kordani Internal Affairs sting with 1–2 subtle tells; accepting triggers **`+2 Kordani Security Strikes`** and confiscates the bribe ([[Core_Loop_Mechanics]]).
- **Late-Game "Destroy Ship" Override (`Kordani rep_score >= +8`):** On Day 14+ or high sector tension, if (and ONLY if) the player maintains **`Kordani rep_score >= +8`**, Kordani Command unlocks a manual station-defense override switch allowing the player to **Destroy** defiant vessels (`-10 Target rep_score / +3 Kordani rep_score`).
- **Multi-Buyer Intel Economy ([[Quarters_Management]]):** **Kordani Security** buys `BMC-` (Brengop), `KRW-` (Krushvak), `GUV-` (GU), and `HBR-` (Hun-Borians) telemetry for **Modest Scrip (`25 SC`) OR Expunge `1` Security Strike + Kordani `rep_score` (`+2`)**.
- **Path B — The Kordani (*"The Iron Collar"* / Ending: *The Kordani Coup*):** Side with Kordani Internal Security to sideline Brengop influence and enforce martial law, becoming Chief Port Overseer with unlimited power and rations, despised by lower-deck humans ([[Endings_and_Story_Paths]]). `[TODO: Fill in manually - design exact 5 Kordani quests]`
- **Under Consideration — Kordani Military Insignia:** Kept under consideration by the user (`Shop.md`, `Desk.md`). `[TODO: Fill in manually - decide whether to keep Kordani Military Insignia and define exact cost and hidden stat effect]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
- **Day 17 Conflicting Pact Directives Placeholder:** `[TODO: Fill in manually - finalize Day 17 conflicting Kordani Security vs. Brengop Mercantile morning shift directive scenario]`
