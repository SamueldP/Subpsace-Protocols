# State Variables & Tracked Systems

All currencies, nightly expenses, penalties, hidden reputation/power counters, and progression states explicitly described in `_Inbox_Sources` and confirmed user overrides are documented below.

---

## 1. Currency (`Pact Scrip / SC`), Shift Pay & Living Fees
- **Currency:** **Pact Scrip (`Scrip` / `SC`)** — company-town scrip issued by the [[Brengop]] with **zero value** inside the post-scarcity [[Galactic_Union]].
- **Power Conversion:** Nightly power is purchased in **Power Units (`PU`)** at **`5 SC` per `PU`**.
- **Base Shift Pay:** **`15 SC`** per accurately processed vessel (`4m 30s` / `270s` shift at `~35–45s/ship`):
  - **Bad Shift (`4 ships`):** `60 SC`
  - **Average Shift (`6 ships`):** `90 SC` *(baseline survival leaves roughly `25 SC` / `5 PU` surplus)*
  - **Strong Shift (`8+ ships`):** `120 SC`
- **Mandatory Berth Lease (Rent):** Fixed **`30 SC`** per night. Missing **2 nights** triggers the **Debt Foreclosure** failure ending ([[Endings_and_Story_Paths]]).
- **Additional Income & Savings:**
  - **Encrypted Subspace Bribes (`30–50 SC`):** Routed straight to your Berth account (max 1/shift on invalid non-`GUV-` ships; confiscated if caught in a Day 7+ sting).
  - **Multi-Buyer Intel Sales (`20–65 SC`):** Selling copied [[Data_Slate_and_Telemetry|Data Slates]] or Warp Field Residue Strips at night ([[Quarters_Management]]).
  - **Independent Passage Fund (`600 SC`):** Savings target for Path E (*Out into the Dark*, [[Endings_and_Story_Paths]]).
  - *Under Consideration:* **Kordani Audit Bonus** from placing a [[Kordani]] Military Insignia on the [[Desk]]. `[TODO: Fill in manually - decide whether to keep Kordani Military Insignia and define exact audit payout bonus]`

---

## 2. Clerical Warnings, Fines & Kordani Security Strikes
- **Clerical Warning Counter:** Standard clerical errors grant **`2` free warnings per shift**, followed by a **`-10 SC` Brengop administrative fine** per mistake.
  - *Expected Ship Discrepancy Rule (Confirmed User Override):* Waving through an [[Expected_Ship_List_and_Traffic|Expected Ship]] with a minor discrepancy grants reputation points (including with the [[Kordani]] / ship's faction, `+3 rep_score`) and **consumes `1` of the `2` free warnings** (incurring the `-10 SC` fine only after the 2 free warnings are exhausted, not an immediate fine).
- **Kordani Security Strikes:**
  - *Sources of Strikes:* Wrongfully detaining an Expected Ship; granting access to a mismatched Stoker mass (`[UNDER REVIEW: Potential pacing cut]`); accepting a Day 7+ Internal Affairs sting bribe (`+2 Strikes`).
  - **Audit Trigger (`3 Strikes`):** **`3` Security Strikes** trigger a mandatory booth/quarters audit/search (exposing un-scrubbed unauthorized Data Slate copies or encrypted bribes unless **Scrub Security Logs [`5 PU / 25 SC`]** was paid).
  - **Expunging Strikes:** Can be removed via **Brengop "Audit Deferral" Fee (`50 SC`)** on Pact Requisition, or by selling intel to **Kordani Security** ([[Quarters_Management]]).
  - **Maximum Strikes Failure (`Security Detainment`):** Reaching *"maximum Kordani Security Strikes"* triggers the **Security Detainment** failure ending ([[Endings_and_Story_Paths]]).
  - [TODO: Fill in manually - define the exact numerical threshold for "maximum Kordani Security Strikes" (e.g., whether 3 strikes only triggers an audit while a higher count triggers Detainment)] `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

---

## 3. Quarters Power (`PU`), Hunger Tiers & Subspace Power-Grid Scanner (SPS) Sway
- **No Work Tool Charging:** Basic work desk tools **never** require overnight charging (`Quarters_Management`).
- **3-Tier Food System & Scanner Sway (Confirmed Hybrid Canon Rule):**
  *(Preserves the old 3-tier food system paired with Food Synthesizer Scrip/PU pricing; `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`.)*

| Tier | Food State | Nightly Cost | Effect on SPS Cursor Sway |
| :--- | :--- | :--- | :--- |
| **Low** | **No Food** (triggered after **2+ days without food**) | `0 PU — 0 SC` | **Max sway** `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` |
| **Medium** | **Nutrient Paste** | `4 PU — 20 SC` | **Minimal sway** |
| **High** | **High-Tier Rations** | `6 PU — 30 SC` | **No sway** *(zero effect on machine/printer speeds)* |

- **Nightly Terminal Power Costs (`4:3` Personal Terminal):**
  - **Pact Requisition**, **Deck 4 Exchange**, **Messages**, and **News** tabs consume **0 PU**.
  - **Terminal: Decrypt Secret Message:** **`3 PU — 15 SC`** (unlocks faction side-quests and covert contacts).
  - **Terminal: Encrypt & Send Data:** **`4 PU — 20 SC`** (encrypts stolen warp strips or copied Data Slates to complete contracts or sell to multi-buyers).
  - **Terminal: Scrub Security Logs:** **`5 PU — 25 SC`** (wipes unauthorized log entries created by copying Data Slates or taking encrypted bribes before a 3-Strike Kordani audit).
- [TODO: Fill in manually - define SPS sway state after only 1 day without food and confirm whether optional Fuse Box power allocation remains planned]

---

## 4. Counter A: Hidden Faction Reputation (`rep_score`, starts at `0`)
All reputation numbers are strictly hidden under the hood. Controls the 3-tier dialogue engine and hidden faction unlocks (`Subspace Protocols - Game Design & Lore Document_3.pdf` Part 1, Section 6):

| Player Action | `rep_score` Change | Design Purpose |
| :--- | :--- | :--- |
| **Valid Ship → Grant Access** | **`+1`** | Expected service; slow positive drift. |
| **Invalid Ship → Detain** | **`-2`** | Even when legal, factions resent VG-3 seizing their vessels. |
| **Invalid Ship → Grant Access** *(Waiver / Bribe / Mercy)* | **`+3`** | Major favor; the faction knows you stuck your neck out. |
| **Valid Ship → Detain** *(Sabotage / Spite / False Positive)* | **`-4`** | Severe provocation; rapid hostility. |
| **Destroy Ship** *(Late-Game Kordani Override)* | **`-10`** (Target) / **`+3`** (Kordani) | Turns target faction permanently hostile while cementing Pact loyalty. |
| **Sell Telemetry in Multi-Buyer Intel Economy** | **`+2`** (Buyer) | Rewards covert espionage with the purchasing faction. |

### Universal Reputation Thresholds & 3-Tier Dialogue Engine
- **Low Reputation (`<= -5`):** Hostile/Stalling — captains stall compliance on shields/weapons and documents, wasting shift time. *(At **`<= -9` on Day 14+**, captains begin refusing to drop shields or power down weapons outright.)*
- **Mid Reputation (`-4` to `+4`):** Neutral, strictly professional interactions.
- **High Reputation (`>= +6`):** Cooperative / Fast Queue — captains arrive compliant (or comply immediately) and transmit subspace protocols quickly. *(Required with `FTV-` or `FTC-` for Path E.)*
- **Kordani "Destroy Ship" Unlock:** **`Kordani rep_score >= +8`** (plus **Day 14+ or high sector tension**).

---

## 5. Counter B: Hidden Galactic Power (`power_score`, starts at `0`)
Tracks how strong each species or organization becomes across the wider sector based strictly on throughput—whether ships make it through [[Void_Gate_3]] regardless of paperwork validity:
- **Grant Access (Any Ship):** **`+1`** Galactic Power
- **Detain or Destroy (Any Ship):** **`-1`** Galactic Power
- **Compromised via Multi-Buyer Intel Sale:** **`-1`** Galactic Power to the compromised ship's faction

### Traffic Tiers & World-Shift Thresholds
Crossing a threshold triggers indirect **Delayed Cause-and-Effect Ripples (2–3 Shift Lag)** via morning Shift Directives and downstream ship spawns/hull states ([[Expected_Ship_List_and_Traffic]]):

| Traffic Tier | Registries | Weakened *(Pullbacks / Shortages)* | Dominant *(Expansion / News Alerts)* | Crisis *(Major World Shift)* |
| :--- | :--- | :--- | :--- | :--- |
| **High Traffic** *(~18–25 ships/run)* | `KDF-`, `BMC-`, `FTV-`, `MNG-` | **`<= -4`** | **`>= +8`** | **`>= +14`** |
| **Mid Traffic** *(~8–15 ships/run)* | `KRW-`, `FTC-`, `ZNI-`, `TLV-`, `ORN-`, `PRJ-` | **`<= -3`** | **`>= +5`** | **`>= +9`** |
| **Rare Traffic** *(~4–8 ships/run)* | `GUV-`, `HBR-` | **`<= -2`** | **`>= +3`** | **`>= +5`** |

---

## 6. Other Tracked & Under-Consideration States
- **Unauthorized Log Entry Flag:** Created whenever you copy a ship's telemetry or accept an encrypted subspace bribe; exposed during a **3-Strike Kordani audit** unless cleared with **Scrub Security Logs (`5 PU / 25 SC`)**.
- **Hidden Double-Agent Trust & Covert Task Counter:** Gained by selling intel to the **Galactic Union Handler** and completing covert double-agent tasks (e.g., **`3 of 5`** threshold) for Path A (*Across the Line*, [[Endings_and_Story_Paths]]).
- **Orphaned / Under-Consideration Older Variables `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`:**
  - *Brengop vs. Kordani Tension Meter:* Older variable partially superseded by per-faction `rep_score` / `power_score` and Paths B & C.
  - *Kordani Military Insignia & Faction Desk Trinkets:* Kept under consideration by user. `[TODO: Fill in manually - decide whether to keep Kordani Military Insignia and Faction Desk Trinkets and define which hidden stat/multiplier they modify]`
