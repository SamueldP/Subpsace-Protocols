# Visual Direction & UI Layout

*For complete color hex ramps, pixel dimensions, rendering architecture, SPS viewport layer, and procedural geometry rules, see [[Procedural_Art_Bible_and_Asset_Spec]].*

## Theme & Aesthetic Direction
- **Aesthetic Reference:** Gritty **16-bit / late-1990s LucasArts DOS VGA** industrial aesthetic paired with oppressive, claustrophobic *Papers, Please* checkpoint framing and *Star Trek*-themed presentation (loosely inspired by post-Dominion War *Deep Space Nine*), plus quarters inspiration from *Death and Taxes* and *Not Tonight*.
- **Legally Safe Terminology:** "Subspace" and "warp fields / warp drives" (and "Warp Pylons" in ship geometry) are used; avoid *Star Trek*-specific phrasing like *"Warp Factor 9"* or *"Nacelles"* ([[FTL_and_Stellar_Stoker]]).
- **Visual Atmosphere:**
  - Dim, low-intensity infrared crimson / rust-red ambient wash (`#3b1518` – `#7a2b26`) cast by the [[Brown_Dwarf_System|Brown Dwarf]]—never bright white starlight.
  - Cold gunmetal steel interiors (`#262d36` / `#434e5c`), oxidized bronze framing (`#5c3a21` / `#8c5832`), scuffed bolted bulkheads stained with hydraulic grease and exposed ceiling conduits, contrasting with warm CRT amber screens (`#ffb84d` / `#b45309`), clearance teal (`#2dd4bf`), and alert crimson (`#dc2626`).
  - Dark [[Brengop]] administrative rings on [[Void_Gate_3]] (open suggestion).

## Native Grid & Hybrid Asset Architecture
- **Native Virtual Framebuffer Grid:** `640 × 360` (16:9 widescreen offscreen virtual framebuffer in a standalone native Windows `.exe` executable, integer-scaled with crisp nearest-neighbor point filtering and zero anti-aliasing, blur, or smooth glow filters; shading uses `4×4` Bayer dithering and `1px` `#080b10` outlines).
- **Static Assets:** Control booth bulkhead framing, `16:9` flat top-down [[Desk]] surface, USB-style Clearance Dock, [[Quarters]] interior, `21:9` (`640 × 274`) non-interactive **Walk-to-Work Corridor Strip** loading screen between Quarters and Booth, `16:9` (`640 × 360`) Main Menu banner, UI icons, and physical thermal printout templates.
- **100% Procedural Runtime Art:** All NPC hailing portraits (`64 × 64`), Data Slate ID holo-photos, approaching starships (`120 × 56`, flat 2D side profile facing Left), and Inspection Manual amber silhouettes are generated deterministically from seeds at runtime (with an optional `assets/images/scripted/` override hook only for unique [[Scripted_Queue_Characters|scripted story entities]]).

## Booth Screen Layout & Tactile UI ([[Desk|Booth 04]])
- **Horizontal Cockpit / Control-Tower Split:**
  - **Top:** Wide external Observation Viewport (`4:3` or `16:9` frame housing the `120 × 56` ship texture / virtual framebuffer + crimson space backdrop + **diegetic Brown Dwarf 4m 30s orbital arc timer**) + corner Main Hailing Viewscreen (`1:1` or `3:4` CRT bezel housing the `64 × 64` portrait texture / virtual framebuffer).
  - **Bottom:** Wide Console [[Desk]] (`16:9` flat top-down texture).
  - **No Clock UI:** Zero analog or digital shift clocks on the desk; time is read strictly from the Brown Dwarf sinking toward the dark planet's horizon / station superstructure.
- **Zero Perspective Tilt (`0°`):** All desktop items ([[Data_Slate_and_Telemetry|Data Slates]], manuals, warp-residue strips, citation strips, Clearance Dock) are strictly flat orthographic 2D top-down or front-facing so they stack, slide, and slot cleanly.
- **Physicalizing Digital Data & Console Gauges:**
  - `3:4` vertical [[Data_Slate_and_Telemetry|Data Slates]] & [[Desk_Reference_Manual_Data|Inspection Manual]] (Data Slates include monochrome cyan/amber ID holo-photos and Day 6+ **Visual Waiver Flag**; manual displays CRT amber ship silhouettes, 8 system waveforms, and 12-species biometrics).
  - **Morning Shift Directive:** Scrolling amber CRT `PACT PORT AUTHORITY — SHIFT DIRECTIVE` printed/displayed at the console at shift start.
  - **Warp Field Residue Spectrometer:** Physically bolted to the desk on Days 1–2 sitting dark with a **yellow `MAINTENANCE PENDING` tag** over the thermal slot; repaired Day 3 to slowly print analog waveform strips.
  - **Analog Gravity-Strain Gauge (Day 4–5+)** `[UNDER REVIEW: Potential pacing cut]`: Analog console gauge displaying actual physical ship tonnage.
  - **Automatic Bio-Scanner Indicator LED (Day 6+):** Passive background scanner whose indicator LED flashes **GREEN** or **RED** after 5–10 seconds (zero extra UI buttons).
  - **Instant Citation Strips:** Audible console buzzer + amber citation strip printing/flashing seconds after clearing or detaining on a mistake.
  - **Visual Violation Tells & SPS Viewport Render Layer:** `weaponsHot: true` displays pulsing `#dc2626` crimson weapon-mount glow, and `shieldsUp: true` displays an animated `1px` dithered teal/cyan oval shimmer perimeter. Toggling **SPS** (when shields are down) dims hull pixels to `#0c141d`–`#1c2b3a` monochrome and draws a pulsing **Reactor Core** + `1px` color-coded conduits to Forward Weapon Mounts (`#ef4444` / `#1e293b`), Warp Pylons (`#f59e0b` / `#115e59`), Bridge Comms Array (`#38bdf8` / `#475569`), and Cargo Bay (`#a855f7` / `#1e293b`).
  - **Downstream Hull States (`gemini-code` / `Document_3.pdf`):** Decommissioned GU Civilian/Cargo ships (`isDecommissioned: true`) show weathered primer hulls, patches, and scorch streaks; when `MNG-` is Dominant, `FTV-` Free Traders arrive with visible hull scorch marks.
  - **Broken Camera Viewscreen (`mode: 'static'`):** Renders animated Bayer-dithered CRT snow with `"NO VISUAL SIGNAL"` scanline bars.
  - **Clearance Dock, Detain & Destroy Controls:** USB-style hardware port on the right side for **Grant Access** *(on-the-fence note on keeping `"Deny / Turn Back"` — `[TODO: Fill in manually - decide whether to keep "Deny / Turn Back" alongside Detain]`, `[CONFLICT: See _Conflicts_and_Plot_Holes.md]`)*; separate physical **Detain** switch; and late-game Kordani **Destroy Ship** manual station-defense override switch.
- **Visible Desk Trinkets *(Under Consideration)*:** Physical faction items (such as the [[Kordani]] Military Insignia) sitting on the [[Desk]] in view of the hailing captain (`[TODO: Fill in manually - decide whether to keep]`).

## Quarters Room Screen & UI ([[Quarters]])
- **Fixed Room View (No Player Sprite):** The player has **no movable sprite** in [[Quarters]]; they see only the room containing a **door on the side**, the **bed**, a **food replicator**, the **personal terminal** (`4:3` horizontal screen when opened), and `[TODO: Fill in manually - specify any remaining room props]`.
- **Interactive Clues & Viewport:**
  - Clicking the **Personal Terminal** opens its 5-tab interface (**Messages**, **News**, **Decrypt / Encrypt & Scrub Logs**, **Pact Requisition**, and **Deck 4 Exchange**). A **small icon above the terminal** indicates unread messages, new news, or a completed decryption/encryption.
  - Clicking the **Bed** ends the day and sleeps.
  - When someone knocks on the **Side Door**, a **visual clue** stays visible until the player either sleeps (ignoring it) or clicks the door to look through the **door's viewport into a corridor** and see whoever is there.

[TODO: Fill in manually - font choices and exact static UI layout mockups]
