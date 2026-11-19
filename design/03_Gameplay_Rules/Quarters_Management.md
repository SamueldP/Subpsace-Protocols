# Quarters Management (Nighttime Loop, Economy & Intel Sales)

## 1. Two Terminal Markets (Replacing the Generic "Shop")
- The original generic shop concept is permanently replaced by **two distinct tabs** on your [[Quarters]] Personal Computer Terminal ([[Shop]]):
  1. **Pact Requisition (Official):** Run by the [[Brengop]] Mercantile Consortium for rations, official consumables, and the Brengop "Audit Deferral" Fee.
  2. **Deck 4 Exchange (Grey Market):** A lower-deck human barter network for **Blank Data Slates**, contraband workarounds, espionage items, and non-Pact intel sales.
- **No Work Tool Charging:** Work desk tools **never** require overnight charging, protecting the core shift loop from lockouts *(conflicts with legacy `Scratchpad.md`—see [[_Conflicts_and_Plot_Holes]])*.
- **Design Rule — NO Mid-Game Permanent Upgrades:** Permanent hardware upgrades that speed up scanners, auto-highlight prefixes, or speed up the printer are intentionally excluded ([[Shop]]).

---

## 2. Shift Math, Currency (`Pact Scrip / SC`) & Penalties
- **Currency:** **Pact Scrip (`Scrip` / `SC`)** — company-town scrip issued by the [[Brengop]] with **zero value** inside the post-scarcity [[Galactic_Union]].
- **Shift Duration & Throughput:** **4 minutes 30 seconds (`270s`)** real time (tracked visually by the [[Brown_Dwarf_System|Brown Dwarf]] star arcing across the viewport sky); `~35–45s` per ship → **`6` ships/shift average** (`4` ships on a stalled shift; `8+` ships on a fast, high-reputation shift).
- **Base Pay:** **`15 SC`** per accurately processed vessel:
  - **Bad Shift (`4 ships`):** `60 SC` | **Average Shift (`6 ships`):** `90 SC` | **Strong Shift (`8 ships`):** `120 SC`
- **Power Rate:** Power is purchased nightly in **Power Units (`PU`)** at **`5 SC` per `PU`**. On an average `90 SC` shift, baseline survival leaves roughly **`25 SC` (`5 PU`)** of surplus.
- **Penalties ([[_State_Variables]]):**
  - Wrongfully detaining an **Expected Ship** issues a non-monetary **[[Kordani]] Security Strike** (**3 strikes** triggers a mandatory booth/quarters audit/search; see [[_Conflicts_and_Plot_Holes]] regarding maximum strikes for Security Detainment).
  - Standard clerical errors grant **`2` free warnings per shift**, followed by a **`-10 SC`** Brengop administrative fine per mistake. *(Waving through an Expected Ship with a minor discrepancy grants faction/Kordani `rep_score` and consumes `1` of the `2` free warnings before incurring `-10 SC` after warnings run out.)*

---

## 3. Nightly Living Expenses, 3-Tier Food System & Terminal Costs
*(Uses the active 3-tier food system paired with Food Synthesizer Scrip/PU pricing; `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`.)*

| Nightly Expense | Cost (`PU` / `SC`) | Gameplay Effect / Trade-Off |
| :--- | :--- | :--- |
| **Berth Lease (Rent)** | Fixed — **`30 SC`** | Mandatory. Missing **2 nights** triggers **Debt Foreclosure** (demotion to lower reactor decks; [[Endings_and_Story_Paths]]). |
| **Food: No Food** *(2+ days unfed)* | `0 PU — 0 SC` | Triggers **Max sway** on the Subspace Power-Grid Scanner ([[Inspection_Tools_and_Checks]]). `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` |
| **Food: Nutrient Paste** *(Medium Food)* | **`4 PU — 20 SC`** | **Minimal sway** on the SPS; baseline survival. |
| **Food: High-Tier Rations** *(Costly Good Food)* | **`6 PU — 30 SC`** | **No sway** on the SPS during the next shift *(does NOT grant artificial speed boosts and has ZERO effect on machine/printer speeds)*. |
| **Terminal: Decrypt Secret Message** | **`3 PU — 15 SC`** | Unlocks incoming faction side-quests and covert contacts. |
| **Terminal: Encrypt & Send Data** | **`4 PU — 20 SC`** | Encrypts stolen warp strips or copied Data Slates to complete contracts or sell intel. |
| **Terminal: Scrub Security Logs** | **`5 PU — 25 SC`** | Wipes unauthorized Data Slate copies and encrypted bribe logs before a 3-Strike Kordani booth/quarters audit. |

- **Approved Consumables & Sinks ([[Shop]]):** **Blank Data Slate (`15 SC` — Deck 4 Exchange)**, **Shielded Pocket Lining (`90 SC` — Deck 4 Exchange)**, **Brengop "Audit Deferral" Fee (`50 SC` — Pact Requisition)**, and **Independent Passage Fund (`600 SC`)**.
- `[TODO: Fill in manually - clarify how clicking the in-room Food Replicator interacts with buying Nutrient Paste / High-Tier Rations on Pact Requisition, and define SPS sway after 1 day without food]`

---

## 4. Point-and-Click Room Gameplay & Terminal Tabs (No Player Sprite)
- **No Movable Sprite:** In [[Quarters]], you see only the fixed room view containing a **door on the side**, the **bed**, a **food replicator**, the **personal terminal**, and `[TODO: Fill in manually - specify any remaining room props]`.
- **Interactive Objects:**
  - **Personal Terminal (`4:3` Horizontal):** Houses **Messages** (`0 PU`), **News** (`0 PU`), **Decrypt / Encrypt & Scrub Logs** (`3–5 PU`), **Pact Requisition** (`0 PU`), and **Deck 4 Exchange** (`0 PU`). A **small icon above the terminal** signals unread messages, new news, or completed decryption/encryption.
  - **Bed:** Click to end the day and sleep (ignores any active door knock).
  - **Side Door:** Knocking creates a persistent visual clue until you sleep or click the door to view the corridor through the **door's viewport** and interact with visitors ([[Scripted_Queue_Characters]]).

---

## 5. Multi-Buyer Intel Economy (Nighttime Terminal)
Using a **Blank Data Slate (`15 SC` on Deck 4 Exchange)** smuggled into [[Desk|Booth 04]] via your contraband pocket—or a pocketed physical **Warp Field Residue Strip**—you can freely copy telemetry from *any* passing ship during a shift, even outside scripted quests. Back in [[Quarters]], paying **`4 PU (20 SC)`** to encrypt and transmit that data lets you choose which faction buys it:
- **No Self-Buying:** A faction will never purchase its own telemetry (e.g., Brengop Factors will not buy a `BMC-` Data Slate; Kordani will not buy a `KDF-` slate).
- **Targeted Demand:** Each buyer only accepts telemetry from factions they are actively monitoring or rivaling, and pays out in a currency/reward matching their lore.
- **Hidden Consequences:** Selling a ship's telemetry grants **`+2 rep_score`** with the buyer, **`-1 power_score`** to the compromised ship's faction (their routes/manifests are exposed), and creates an **unauthorized log entry** that should be cleared with **Scrub Security Logs (`5 PU`)** before a 3-Strike Kordani audit.

| Buyer | Telemetry They Will Buy | Payout / Reward |
| :--- | :--- | :--- |
| **[[Brengop]] Factors** | `KDF-` (Kordani), `MNG-` (Mining Guild), `FTC-`, `ZNI-`, `ORN-` | **High Pact Scrip (`45–65 SC`) + Brengop `rep_score` (`+2`)** |
| **[[Kordani]] Security** | `BMC-` (Brengop), `KRW-` (Krushvak), `GUV-` (GU), `HBR-` (Hun-Borians) | **Modest Scrip (`25 SC`) OR Expunge `1` Security Strike + Kordani `rep_score` (`+2`)** |
| **[[Galactic_Union]] Handler** | `KDF-`, `BMC-` (Pact rebuild intel), `HBR-`, or flagged [[The_Concord]] / Dopple routes | **Covert Operational Stipend (`20–30 SC`) + GU `rep_score` (`+2`) + Hidden Double-Agent Trust** *(post-scarcity GU does not use Scrip natively, so payouts are small slush funds)* |
| **[[Krushvak]] Liaison** | `KDF-` (Kordani), `HBR-` (Hun-Borian), or suspect [[The_Concord]]-route strips | **Salvage Scrip (`30–40 SC`) + Krushvak `rep_score` (`+2`)** |
| **Deck 4 Exchange (Brokers)** | *Any* non-Pact commercial or independent slate (`FTV-`, `FTC-`, `MNG-`, `ZNI-`, `PRJ-`, `TLV-`) | **Flat `35 SC`** *(no major faction `rep_score` change; lower audit heat than selling military slates)* |
