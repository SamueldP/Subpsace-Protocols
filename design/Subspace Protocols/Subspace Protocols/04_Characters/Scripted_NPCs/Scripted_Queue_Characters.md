# Scripted Queue Characters & NPC Roles

## Loop Integration & Rendering Overrides
- Main story beats ([[GU_Double_Agent_Objectives]], [[Endings_and_Story_Paths]]) and side quests ([[Expected_Ship_List_and_Traffic|Day 2 "Ghost Route"]], [[Helani_Courier_Warp_Signature]], [[Orun_Freighter_442]]) inject specific scripted characters directly into the daily shift queue at [[Desk|Booth 04]] alongside standard procedurally generated ships.
- **Asset Override Hook ([[Procedural_Art_Bible_and_Asset_Spec]]):** While standard traffic portraits and starships are 100% procedurally generated from deterministic seeds, `PortraitRenderer` and `ShipRenderer` check an optional `assets/images/scripted/` folder *only* if a character or ship is flagged as a unique scripted story entity, falling back to procedural rendering otherwise.

---

## Explicitly Referenced Scripted Roles, Stings & Nighttime Contacts

### 1. Day 2 Hardcoded Suspect — `"The Ghost Route"`
- **Trigger:** Mandatory hardcoded scripted vessel on **Day 2** ([[Expected_Ship_List_and_Traffic]], [[Daily_Rule_Escalation]]).
- **Eligible Pool:** Randomly rolled from `KRW-`, `HBR-`, `PRJ-`, `ORN-`, `TLV-`, `FTV-`, `FTC-`, `MNG-`, or `ZNI-` (strictly excluding `GUV-`, `KDF-`, and `BMC-`).
- **Behavior:** Spotless paperwork and cold SPS readings paired with deeply suspicious dialogue excuses (`"Your station's own spectrometer is dark, Port Officer..."`). Triggers the Day 3 incident alert (`[KDF-SEC-09]`) and spectrometer repair.

### 2. Day 7+ Kordani Internal Affairs Sting Captains
- **Role:** Starting Day 7+, roughly **1 in 8** Encrypted Subspace Bribe offers (`~12%` on invalid vessels) is secretly a **Kordani Internal Affairs compliance sting** rather than a real smuggler ([[Core_Loop_Mechanics]]).
- **Tells:** Carries 1–2 subtle tells (stiff/formal Pact phrasing in the encrypted offer, a pristine hull claiming to be a battered tramp hauler, or SPS comms conduits routing through military-grade encryption on a civilian vessel).

### 3. Main Storyline Handlers & Faction Contacts (Nighttime Personal Terminal)
- **[[Galactic_Union]] Handler (Path A — *The Extraction*):** Recruits the player inside customs, assigns covert double-agent tasks (e.g., 3 of 5 threshold), buys `KDF-`, `BMC-`, `HBR-`, and flagged Concord/Dopple intel (`20–30 SC + 2 GU rep + Hidden Double-Agent Trust`), and arranges the final weathered civilian GU cargo runner escape ([[Double Agent]]).
- **[[Kordani]] Internal Security / Desk Auditors (Path B — *The Iron Collar*):** Audits booth/quarters after **3 Security Strikes** (and breaches airlock on Security Detainment failure); quietly remarks on `HBR-`/`FTV-` leniency before formal strikes; buys `BMC-`, `KRW-`, `GUV-`, `HBR-` intel (`25 SC` OR expunge `1` Strike `+ 2 Kordani rep`). `[TODO: Fill in manually - Kordani handler names and exact 5 Path B quests]`
- **Senior [[Brengop]] Factors (Path C — *The Company Man*):** Sends unsolicited recognition and unprompted Berth Lease discounts on high `BMC-` throughput; buys `KDF-`, `MNG-`, `FTC-`, `ZNI-`, `ORN-` intel (`45–65 SC + 2 Brengop rep`). `[TODO: Fill in manually - Brengop Factor names and exact Path C quests]`
- **[[Krushvak]] Liaison (Intel Buyer):** Buys `KDF-`, `HBR-`, and suspect Concord-route strips (`30–40 SC + 2 Krushvak rep`).
- **Deck 4 Exchange Brokers (Grey Market):** Lower-deck human barter network buying any non-Pact commercial/independent slate (`FTV-`, `FTC-`, `MNG-`, `ZNI-`, `PRJ-`, `TLV-`) for a flat `35 SC` (lower audit heat).
- **Stranded [[The_Concord|Concord Masters ("Dopples")]] (Path D — *The Hollow Gate*):** Hidden contacts who seek to silently replace VG-3's Kordani and Brengop leadership. `[TODO: Fill in manually - Dopple contact names, quests, and discovery triggers]`
- **Independent Captain (Path E — *Out into the Dark*):** A fairly treated `FTV-` or `FTC-` captain who offers a quiet outer-rim berth once the player accumulates `600 SC` and `>= +6` High Rep.

### 4. Helani Courier Captain (Draft Side Quest)
- **Quest Link:** [[Helani_Courier_Warp_Signature]] ([[Helani]]).
- **Role:** Pilots the Helani courier ship arriving during the shift whose warp signature the player logs and copies onto a **Blank Data Slate** (`15 SC`).
- [TODO: Fill in manually - captain name, ship name, arrival day, and dialogue]

### 5. Orun Freighter `ORN-442xxx` Captain (Draft Side Quest)
- **Quest Link:** [[Orun_Freighter_442]] ([[Independent_and_Minor_Species|Orun]]).
- **Role:** Pilots an Orun freighter carrying Ship Registration **`ORN-442xxx`** (where `xxx` is randomized in-game) that must be cleared to dock even though its inspection details are invalid.
- [TODO: Fill in manually - captain name, ship name, arrival day, invalid details, and dialogue]

### 6. Nighttime Quarters Visitors (Side Door)
- **Role:** Visitors knocking at the **side door** of [[Quarters]] at night. When there is a knock, a visual clue stays visible until the player either clicks the bed to sleep (ignoring it) or clicks the door to interact—switching to looking through the **door's viewport into a corridor** to see whoever they are interacting with.
- [TODO: Fill in manually - specific visitor characters, factions, and nighttime door events]
