# Project Overview — Subspace Protocols

## Locked-In Identity
- **Game Title:** *Subspace Protocols* (chosen as a legally safe title; "subspace" is a generic sci-fi term).
- **Target Platform & Rendering Pipeline:** Standalone native Windows desktop executable (`.exe`) rendering to a fixed `640 × 360` offscreen virtual framebuffer with integer scaling, nearest-neighbor (point) texture filtering, and local disk JSON save files.
- **Core Concept:** A *Papers, Please*-inspired, single-area bureaucratic inspection and management game set in an original sci-fi universe.
  - Aesthetic & setting inspiration: *Star Trek*-themed presentation, loosely inspired by post-Dominion War *Deep Space Nine (DS9)*.
  - Quarters & management inspiration: *Death and Taxes* and *Not Tonight*.
  - World-state reactivity inspiration: *Suzerain*-inspired delayed ripples and unprompted faction recognition.
- **Role & Perspective:** First-person, unnamed human [[Main_Character|Port Officer]] working customs and docking control at **Booth 04** on [[Void_Gate_3]], serving as the primary lens for the sector's politics, trade, and paranoia.
- **Primary Location:** [[Void_Gate_3]] (station code: `VG-3`; formerly tested names: *Abyss Station* and *Outer Reach*). There are 4 Void Gates in total across the galaxy.
- **Original Builders / Consortium:** **[[Commercial_Organizations|Far-Line Transit Corp (`FTC-`)]]** and frontier colonists (independent human engineering consortium outside [[Galactic_Union]] rules; station consoles run on **legacy FTC software**).
- **Storylines & Endings ([[Endings_and_Story_Paths]]):** Features **5 Main Storyline Paths** (**Path A: Galactic Union / *Across the Line***, **Path B: Kordani / *The Kordani Coup***, **Path C: Brengop / *The Buyout***, **Path D: Masters & Dopples / *The Quiet Return***, and **Path E: Independent Escape / *Out into the Dark***) plus **2 Failure Endings** (*Security Detainment* and *Debt Foreclosure*).

## Undecided / Open Naming Items
- **Universe Name:** Needs a short, punchy setting name (like *Sajuuk*), avoiding "Subspace Protocols universe" or "Void Gate universe".
  - *Suggested Options (from `Subpsace protocols info.pdf`):* The Vaelen Universe/Cluster, The Tarkis Universe/Galaxy, The Oruun Expanse, The Sovak Sector, The Kaelis Reach, The Verdan Drift, The Verge, The Pale Sector, The Sundered Reach, The Rimward March.
  - [TODO: Fill in manually - select final Universe Name]

---

## Vault Index

### System Index (`00_System_Index`)
- [[_Project_Overview]]
- [[_State_Variables]]
- [[_Conflicts_and_Plot_Holes]] (Vault root)

### World Bible (`01_World_Bible`)
- **Factions & Organizations:** [[Kordani]], [[Brengop]], [[The_Bulwark_Pact]], [[Galactic_Union]], [[Krushvak]], [[The_Concord]], [[Helani]], [[Independent_and_Minor_Species]], [[Commercial_Organizations]]
- **Setting & History:** [[FTL_and_Stellar_Stoker]], [[Station_Atmosphere_and_Society]]
- **Timeline:** [[Concord_War_Timeline]]

### Locations (`02_Locations`)
- **Playable:** [[Desk]], [[Quarters]], [[Shop]]
- **World Locations:** [[Void_Gate_3]], [[Brown_Dwarf_System]]

### Gameplay Rules (`03_Gameplay_Rules`)
- **Core & Quarters:** [[Core_Loop_Mechanics]], [[Quarters_Management]]
- **Documents & Data:** [[Data_Slate_and_Telemetry]], [[Inspection_Tools_and_Checks]], [[Desk_Reference_Manual_Data]]
- **Rule Progression:** [[Daily_Rule_Escalation]]

### Characters (`04_Characters`)
- [[Main_Character]]
- **Scripted NPCs:** [[Scripted_Queue_Characters]]

### Main Story (`05_Main_Story`)
- **Endings:** [[Endings_and_Story_Paths]], [[Double Agent]]
- **Overarching Beats:** [[GU_Double_Agent_Objectives]]

### Side Quests (`06_Side_Quests`)
- [[Helani_Courier_Warp_Signature]]
- [[Orun_Freighter_442]]

### Daily Schedule (`07_Daily_Schedule`)
- [[Expected_Ship_List_and_Traffic]]

### Physical Look (`08_Physical_Look`)
- [[Visual_Direction_and_UI_Layout]]
- [[Procedural_Art_Bible_and_Asset_Spec]]
