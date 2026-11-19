# Core Gameplay Loop & Desk Mechanics

## 1. The Work Shift (Desk & Viewport Loop)
- **Game Style:** A *Papers, Please*-inspired, single-area bureaucratic inspection and management game where the customs and docking control [[Desk|desk (Booth 04)]] on [[Void_Gate_3]] serves as the primary lens for the sector's politics, trade, and paranoia.
- **Role & Perspective:** First-person, unnamed human [[Main_Character|Port Officer]].
- **Shift Transition:** Traveling between [[Quarters]] and the [[Desk|Booth]] shows a `21:9` (`640 × 274`) non-interactive Walk-to-Work Corridor Strip loading screen ([[Procedural_Art_Bible_and_Asset_Spec]]).
- **Passage of Time (No Clock UI) & Time Runout Rule (`4m 30s / 270s`):**
  - There is **no analog or digital clock** on the desk. Instead, shift time is tracked diegetically through the external viewport: the dim, rust-red [[Brown_Dwarf_System|Brown Dwarf]] star slowly arcs across the sky from shift start to shift end, sinking toward the dark planet's horizon / station superstructure as the **4-minute 30-second (`270s`)** watch winds down.
  - **Time Runout Rule:** When the Brown Dwarf reaches the horizon and the shift timer ends, no new hails can be triggered, **but you are always allowed to finish inspecting and resolving the vessel currently in your bay**.
- **Hailing & Pre-Clearance Compliance (`Subspace Protocols - Game Design & Lore Document_3.pdf`):**
  1. **Next Hail:** Trigger the `"Next Hail"` control. The approaching ship pulls into view in the external observation viewport (`120 × 56` procedural ship sprite facing Left) and hails your booth.
  2. **Shields & Weapons Check:** Every ship must approach with shields down and weapons powered off (visually checked in the viewport; active shields block the SPS and hot weapons glow `#dc2626` on mounts). If either is active, order the captain to lower shields and/or power down weapons. Hostile (low-reputation) captains may stall, argue, or claim system faults to waste shift time; high-reputation captains arrive compliant or comply immediately. If a captain refuses outright, you must **Detain** them (or **Destroy** them later in the game if tensions are high and Kordani authorization is unlocked).
  3. **Subspace Transfer (Auto-Dialogue):** Once shields and weapons are addressed, request their packet via subspace (short-range encrypted data transfer) with the fixed prompt: **`"Send subspace protocols."`** The ship's telemetry, manifest, and monochrome ID holo-photo load onto your movable [[Data_Slate_and_Telemetry|Data Slate]].
  4. **Auto-Interrogation (Enabled from Day 1 — Confirmed User Override):** Once the Data Slate loads, your character automatically issues the standard inquiry: **`"Confirm route, purpose of visit, and cargo."`** *(Confirmed active from Day 1 rather than waiting until Day 3.)*
- **Queue Composition:** Standard procedurally generated ships mixed with [[Scripted_Queue_Characters|scripted encounters]] (such as the Day 2 `"Ghost Route"` suspect) and the daily [[Expected_Ship_List_and_Traffic|Expected Ship List]] (`10` priority arrivals issued by Brengop Mercantile Operations; ~`4–8` ships processed per watch).
- **Ship & Captain Inspection ([[Inspection_Tools_and_Checks]], [[Daily_Rule_Escalation]]):**
  - Cross-check Registration Prefix, Visual Ship Silhouette/Faction, and (from Day 3+) the **Warp Field Residue Printer** strip against [[Desk_Reference_Manual_Data]].
  - Toggle the **Subspace Power-Grid Scanner (SPS)** (Day 1+, blocked if shields are active) over the viewport to inspect live internal power conduits to Forward Weapon Mounts, Warp Pylons, Bridge Comms Array, and Cargo Bay.
  - Monitor the **Gravity-Strain Gauge** (Day 4–5+) `[UNDER REVIEW: Potential pacing cut]`, the passive 5–10s **Automatic Bio-Scanner** (Day 6+ bridge & Day 10+ cargo), passive **Cross-Referenced Contraband Deductions**, **Day 6+ Viewscreen Facial Matching & Two-Step Broken Camera Protocol**, and **Day 15 Brengop Transit Pass**.
- **Final Decision — Grant Access vs. Detain *(Plus On-the-Fence Deny Note)* & Conditional Destroy:**
  - **Grant Access (Clearance Dock):** Physically drag the [[Data_Slate_and_Telemetry|Data Slate]] to the right side of the screen and slot it into the USB-style hardware port on your desk terminal to transmit encrypted docking clearance back to the ship.
  - **Detain (Separate Desk Control):** Triggered via a separate physical control/switch on the desk rather than the Data Slate clearance port, engaging station clamps and calling [[Kordani]] security boarding teams.
  - *Deny / Turn Back Status (`[TODO]`):* `Document_3.pdf` Part 1, Section 1 replaces `"Deny / Turn Back"` with binary **Grant Access vs. Detain**, while Part 2, Section 3 still mentions denying a vessel and the creator is currently deciding between the two options. `[TODO: Fill in manually - decide whether to keep "Deny / Turn Back" alongside Detain or use binary Grant Access vs. Detain]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
  - **Destroy Ship (Late-Game Conditional Override):** Later in the game (Day 14+ or high sector tension), hostile captains may refuse outright to drop shields or power down weapons (`rep_score <= -9`). If (and ONLY if) **`Kordani rep_score >= +8`**, Kordani Command unlocks a manual station-defense override switch allowing you to **Destroy** the defiant vessel right in the viewport (`-10 Target rep_score / +3 Kordani rep_score`).
- **Immediate Error Feedback (Instant Citations):** Whenever you make a protocol mistake (clerical error, missed discrepancy, or unjustified detainment), the console delivers instant feedback seconds after the ship clears or is detained—buzzing audibly and printing/flashing an amber citation strip detailing the exact violation and any fine (`2` free clerical warnings/shift, then `-10 SC`) or Kordani Security Strike, rather than waiting for an end-of-shift summary.
- **Mechanics Under Consideration (Not Yet Locked In):**
  - *Subspace Frequency Tuning:* Rotary dial to tune static hails and potentially overhear 2-second unencrypted chatter bursts *(note: may add too much friction; evaluate during prototyping)*.

---

## 2. Encrypted Subspace Bribes & Day 7+ Internal Affairs Stings
- **Bribe Delivery:** Routed through the same short-range encrypted subspace channel used for [[Data_Slate_and_Telemetry|Data Slate]] transfers. When an invalid ship's Data Slate loads (or slots into the Clearance Dock), a quiet secondary prompt may ping offering **`30–50 SC`** straight to your Berth account to ignore the discrepancy and **Grant Access**.
- **Frequency Cap:** Maximum of **1 bribe per shift** (roughly a **15%** roll on invalid vessels; **never** offered by official [[Galactic_Union]] `GUV-` ships).
- **Audit Risk & Power Tie-In:** Because the channel is encrypted, [[Kordani]] Security does not detect bribes immediately—unless **3 Security Strikes** trigger a booth/quarters audit and you have not paid **`5 PU (25 SC)`** on your quarters terminal to **Scrub Security Logs** ([[Quarters_Management]]).
- **Internal Affairs Stings (Starting Day 7+):** Roughly **1 in 8** bribe offers (**~12%** chance on invalid ships) is secretly a **Kordani Internal Affairs compliance sting** rather than a real smuggler. Stings carry **1–2 subtle tells**:
  - Unusually stiff, formal Pact phrasing in the encrypted offer.
  - A pristine hull claiming to be a battered tramp hauler.
  - **SPS** comms conduits routing through military-grade encryption on a civilian vessel.
  - *Penalty:* Accepting a sting bribe triggers an immediate **`+2 Kordani Security Strikes`** (and confiscates the bribe) at shift end.

---

## 3. The Pocket / Contraband Mechanic (Shift-to-Quarters Bridge)
- **Sneaking Items In & Out:** The work [[Desk]] includes a hidden pocket / contraband slot to sneak physical items between the shift booth and [[Quarters]].
- **Theft & Recording:** Smuggle a **Blank Data Slate (`15 SC` — Deck 4 Exchange)** into the booth to copy target telemetry from *any* passing ship, or slide a printed **Warp Field Residue Strip** into your pocket for side quests ([[Helani_Courier_Warp_Signature]], [[Double Agent]]) or nighttime **Multi-Buyer Intel Economy** sales ([[Quarters_Management]]).
- **Shielded Pocket Lining (`90 SC` — Deck 4 Exchange):** Purely conceals **1 smuggled item** in your desk contraband slot during Kordani 3-Strike sweeps.

---

## 4. Hidden Reputation (`rep_score`), Galactic Power (`power_score`) & Diegetic Reactivity
*(All reputation and galactic power numbers are strictly hidden under the hood—zero visible UI meters; full tables in [[_State_Variables]].)*
- **Counter A — Hidden Faction Reputation (`rep_score`, starts at `0`):**
  - Controls the 3-tier dialogue engine (**Low `<= -5`**, **Mid `-4 to +4`**, **High `>= +6`**), Day 14+ `<= -9` shield/weapon refusal, and Kordani `>= +8` **Destroy Ship** unlock.
  - Waving through an [[Expected_Ship_List_and_Traffic|Expected Ship]] with a minor discrepancy grants faction reputation (including `+3` with [[Kordani]] / the ship's faction) and consumes 1 of 2 free shift warnings before any `-10 SC` fine.
- **Counter B — Hidden Galactic Power (`power_score`, starts at `0`):**
  - Tracks sector strength strictly by throughput (`Grant Access: +1`, `Detain/Destroy: -1`, `Intel Sale Compromise: -1`) across **High**, **Mid**, and **Rare** Traffic Tiers (`Weakened`, `Dominant`, `Crisis`).
- **Suzerain-Inspired Diegetic World Reactivity:**
  1. **Delayed Cause-and-Effect Ripples (2–3 Shift Lag):** Crossing a `Weakened`, `Dominant`, or `Crisis` threshold surfaces 2 to 3 shifts later via **Morning Shift Directives** (e.g., `MNG-` claiming asteroid belts, `KRW-` pulling back) and **Downstream Ship Spawns & Hull States** (e.g., if `MNG-` is Dominant, `FTV-` Free Traders arrive with visible hull scorch marks complaining about Guild escorts; if `KDF-` is Dominant, military transport frequency rises and non-Pact captains arrive more nervous).
  2. **Unprompted Recognition (Organic Faction Reactions):** Nighttime Personal Terminal contacts and recurring captains silently read `rep_score`, `power_score`, and unannounced behavioral flags—generating **Unsolicited Recognition** (e.g., a Brengop Factor offering an unprompted Berth Lease discount), **Quiet Warnings** (a Kordani officer casually remarking on leniency toward `HBR-`/`FTV-` before a formal strike), and **Captain Memory** (*"Word on the rim is Booth 04 doesn't hassle Free Traders"* or *"Heard Kordani's pet human is seizing every Krushvak hull that drops out of warp"*).
- **6 Rule-Breaking Levers (Why Break the Rules?):**
  1. *Suppressing a Rising Threat (Detaining Clean Ships)* to balance dominant factions (`KDF-`, `MNG-`, `KRW-`).
  2. *Feeding an Ally / Unlocking Leverage (Clearing Flawed Ships)* for Kordani `Destroy` (`+8`) or Path E `FTV-`/`FTC-` (`+6`).
  3. *Humanitarian & Moral Dilemmas* (awakening peaceful [[The_Concord|Hun-Borians]], lower-deck human mechanics hiding in tramp hauler bays, refugees with forged GU papers vs. Dopples).
  4. *The Expected Ship Trap vs. Gut Instinct* (clean papers vs. suspicious dialogue/Deck 4 tip-off).
  5. *Economic Desperation & Bribes* (`30–50 SC` bribe vs. `5 PU / 25 SC` log scrub risk).
  6. *Personal Grudges & Reputation Loops*.
- **Under Consideration — Faction Desk Trinkets & [[Kordani]] Military Insignia:** Kept under consideration by the user alongside `rep_score` (`Desk.md`, `Shop.md`, `_State_Variables.md`). `[TODO: Fill in manually - decide whether to keep Kordani Military Insignia and Faction Desk Trinkets and define exact stat effects]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`
