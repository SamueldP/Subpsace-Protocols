# Playable Location: Personal Quarters

## Overview
- **Role:** The [[Main_Character]]'s living quarters on [[Void_Gate_3]], visited between shifts for nighttime survival, terminal requisitions, intel sales, and side-quest execution ([[Quarters_Management]]).
- **Scope:** Solo living only—there is **no family management**; the player manages only their own **Berth Lease (`30 SC/night`)**, **food intake**, and **quarters power (`5 SC/PU`)**.
- **No Tool Charging:** Work desk tools **never** require overnight charging in quarters, protecting the core shift loop from lockouts *(conflicts with legacy `Scratchpad.md`—see [[_Conflicts_and_Plot_Holes]])*.
- **Shift Transition ([[Procedural_Art_Bible_and_Asset_Spec]]):** Traveling between Quarters and [[Desk|Booth 04]] displays a `21:9` (`640 × 274`) non-interactive Walk-to-Work Corridor Strip loading screen.

## Room View & Point-and-Click Interaction (No Player Sprite)
- **Fixed Room View:** The player does **not** have a sprite of their own that they move around in Quarters. They only see the static room containing interactive objects: a **door on the side**, the **bed**, a **food replicator**, the **personal terminal**, and `[TODO: Fill in manually - specify any remaining quarters room props]`.
- **Personal Terminal (`4:3` Horizontal Screen):**
  - Clicking the terminal opens its interface, which includes **5 functional tabs** (with the original single Shop permanently replaced by two distinct requisition tabs):
    1. **Messages:** Where the player receives quests and unprompted faction recognitions ([[Helani_Courier_Warp_Signature]], [[Orun_Freighter_442]], [[Double Agent]]). **Does NOT consume power.**
    2. **News:** Station/sector news feed. **Does NOT consume power.**
    3. **Decrypt / Encrypt & Security Logs:** Powered covert terminal operations:
       - **Decrypt Secret Message:** **`3 PU — 15 SC`** (unlocks incoming faction side-quests and covert contacts).
       - **Encrypt & Send Data:** **`4 PU — 20 SC`** (encrypts stolen warp strips or copied Data Slates to complete contracts or sell to the 5 Multi-Buyer Intel factions).
       - **Scrub Security Logs:** **`5 PU — 25 SC`** (wipes unauthorized log entries from copied Data Slates or encrypted bribes before a 3-Strike [[Kordani]] audit).
    4. **Pact Requisition (Official — [[Shop]]):** Run by the [[Brengop]] Mercantile Consortium for official station consumables, rations, and legalized bribes (e.g., **Brengop "Audit Deferral" Fee [`50 SC`]**). **Does NOT consume terminal browsing power.**
    5. **Deck 4 Exchange (Grey Market — [[Shop]]):** Lower-deck human barter network for **Blank Data Slates (`15 SC`)**, **Shielded Pocket Lining (`90 SC`)**, contraband workarounds, unofficial goods, and commercial/independent intel sales. **Does NOT consume terminal browsing power.**
  - **Notification Icon:** A **small icon appears above the terminal** whenever there are unread messages, new news, or a completed decryption/encryption.
- **Bed:** Clicking the bed ends the day and puts the player to sleep (advancing to the next day / shift; sleeping ignores any active door knock).
- **Side Door & Corridor Viewport:**
  - When someone knocks on the door on the side, a **visual clue** appears and stays there until the player either sleeps (clicking the bed and ignoring it) or clicks the door to interact.
  - Clicking the door switches to looking through the **door's viewport into a corridor** where the player sees whoever they are interacting with ([[Scripted_Queue_Characters]]). Also breached by [[Kordani]] troopers in the **Security Detainment** failure ending.
- **Food Replicator:** Present inside the room alongside the bed, door, and terminal.
  - *(Confirmed Hybrid Food System:)* Uses the 3-tier food system (**No Food** after 2+ days = Max sway; **Nutrient Paste** [`4 PU — 20 SC`] = Minimal sway; **High-Tier Rations** [`6 PU — 30 SC`] = No sway; see [[Quarters_Management]], [[_State_Variables]]).
  - `[TODO: Fill in manually - clarify how clicking the in-room food replicator vs. Pact Requisition tab dispenses Nutrient Paste and High-Tier Rations]` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

## Additional Storage & Notes
- **Contraband & Blank Data Slates:** Stores **Blank Data Slates (`15 SC`)** to sneak into the [[Desk]] via the hidden pocket/contraband slot, as well as physical warp residue strips or copied telemetry snuck home from a shift. `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`

[TODO: Fill in manually - quarters visual art mockup and exact room-search inspection flow when 3 Kordani Security Strikes trigger]
