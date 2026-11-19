---
tags:
- gamedev
- art-bible
- pixel-art
- visual-direction
- procedural-art
- antigravity-spec
---

# Procedural Art Bible & Asset Spec

*Sources: `_Inbox_Sources/subspace_protocols_art_bible_procedural_asset_spec.md`, `_Inbox_Sources/gemini-code-1795085727104.md`, `_Inbox_Sources/Subspace Protocols - Game Design & Lore Document_3.pdf`, and confirmed canon overrides (cross-linked with [[Visual_Direction_and_UI_Layout]]).*

## 1. Visual Direction & Hybrid Asset Architecture

*Subspace Protocols* combines a gritty **16-bit / late-1990s LucasArts DOS VGA** industrial aesthetic with oppressive, claustrophobic *Papers, Please* checkpoint framing.

### Asset Split (Static vs. 100% Procedural)
- **Static / Environmental Art (Hand-Drawn Artist or AI Placeholder Initially):** Control booth bulkhead framing ([[Desk]]), top-down desk surface, USB-style Clearance Dock, **Gravity-Strain Gauge** `[UNDER REVIEW: Potential pacing cut]`, **Automatic Bio-Scanner** LED indicator, yellow `MAINTENANCE PENDING` tag (Days 1–2), [[Quarters]] interior, walk-to-work corridor loading strip, UI icons, and physical thermal printout / amber citation strip templates ([[Inspection_Tools_and_Checks]]).
- **100% Procedural Runtime Art (Zero External Images):** **All NPC hailing portraits, ID holo-photos, approaching starships, and manual silhouettes** are generated procedurally at runtime into an in-memory RGBA `PixelBuffer` uploaded to a cached GPU texture with Point/Nearest filtering and composited onto the offscreen virtual framebuffer using `4×4` Bayer ordered dithering.
- **Scripted Story Override Hook (Exceptions Only):** Standard traffic is always randomly/deterministically generated from seeds ([[Expected_Ship_List_and_Traffic]]). Both `PortraitRenderer` and `ShipRenderer` check an optional `assets/images/scripted/` folder *only* if a character or ship is flagged as a unique scripted story entity ([[Scripted_Queue_Characters]]), falling back to the procedural renderer otherwise.

### Rendering & Pixel Rules
- **Strict Pixel Crispness:** Always upload in-memory RGBA `PixelBuffer` data to cached GPU textures and composite onto the offscreen virtual framebuffer using strict nearest-neighbor (Point) filtering and integer scaling.
- **Native Virtual Framebuffer Grid:** `640 × 360` (16:9 widescreen offscreen virtual framebuffer, scales cleanly by integer multiples to 720p, 1080p, 1440p, and 4K with nearest-neighbor point filtering).
- **Procedural Buffer Grids:**
  - **Portraits (`PortraitRenderer`):** `64 × 64` native pixels (gives maximum 16-bit charm while keeping geometric shapes crisp via `1px` outlines and Bayer dithering).
  - **Starships (`ShipRenderer`):** `120 × 56` native pixels, strictly flat **2D side profile facing Left** (Bow on left, Warp Pylons/Stern on right).
- **Strict Prohibitions:** Zero soft anti-aliasing, Gaussian blur, outer glow filters, or smooth digital airbrushing. All shading transitions must use `4×4` Bayer dithering and hard `1px` dark outlines (`#080b10`).

---

## 2. Environmental Tone, Lighting & Static UI Specs

### Atmosphere & Brown Dwarf Diegetic Shift Arc
- **Setting:** An aging, overcrowded orbital border station ([[Void_Gate_3]]) now run by [[The_Bulwark_Pact|the Bulwark Pact]]. Bulkheads are scuffed, bolted together, stained with hydraulic grease, and wired with exposed ceiling conduits.
- **Dim Infrared Crimson Glow & 4m 30s Shift Timer:** [[Void_Gate_3]] orbits a [[Brown_Dwarf_System|Brown Dwarf]] (failed star). The external space viewport is bathed in a **dim, low-intensity infrared crimson / rust-red ambient wash**—never bright white starlight. With no clock UI on the desk, the Brown Dwarf slowly arcs across the viewport sky toward the dark planet's horizon / station superstructure over **4m 30s (`270s`)**.
- **Color Temperature Contrast:** Cold gunmetal steel interiors and dim crimson-rust space outside contrast sharply against warm amber-gold CRT screens inside the cramped control booth ([[Desk]]).
- **Zero Perspective Tilt (`0°`):** All desktop items ([[Data_Slate_and_Telemetry|Data Slates]], manuals, warp-residue strips, citation strips, Clearance Dock) must be strictly flat orthographic 2D top-down or front-facing so they stack, slide, and slot cleanly.

### Core Station & UI Color Palette

> **Visual Separation Rule:** The Brown Dwarf's muted infrared rust-red (`#3b1518` – `#7a2b26`) stays dark and desaturated so it never conflicts with bright Alert Crimson (`#dc2626`) used for UI warnings, hot weapons alerts, or Detain/Destroy controls.

| Role | Hex Code | Visual Usage |
| :--- | :--- | :--- |
| **Deep Space / Outline Void** | `#080b10` – `#0f1319` | `applyOutline()` sprite borders, deep shadows, void backdrop |
| **Brown Dwarf Glow (Deep)** | `#3b1518` | Outer infrared haze, deep viewport tint |
| **Brown Dwarf Glow (Rim)** | `#7a2b26` | Dim crimson rim-light along station mullions & ship undersides |
| **Weathered Gunmetal (Mid)** | `#262d36` | Primary steel bulkheads, desk plating, console casings |
| **Weathered Gunmetal (Edge)** | `#434e5c` | Metallic bevel highlights, scratched edges, rivets |
| **Oxidized Bronze (Shadow)** | `#5c3a21` | Recessed trim, aged consortium hardware |
| **Oxidized Bronze (Worn)** | `#8c5832` | Heavy station doors, Pact authority framing |
| **CRT Amber (Primary)** | `#ffb84d` | Active screen text, headers, Morning Shift Directives, citation strips, manual silhouettes |
| **CRT Amber (Dimmed)** | `#b45309` | Scanlines, inactive gauges, secondary text |
| **Clearance / Uplink Teal** | `#2dd4bf` | Approved transmissions, docked Data Slate LED, GU warp glow |
| **Denial / Alert Crimson** | `#dc2626` | Hot weapons alerts, Detain switch *(older label also mentions "Denied clearances"* `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *)* |

### Static Asset Aspect Ratios (Non-Procedural UI & Screens)
- **Main Menu Banner:** `16:9` Widescreen Landscape (`640 × 360`)
- **Walk-to-Work Corridor Strip (Loading Screen):** `21:9` Ultrawide Panoramic Cutaway (`640 × 274`) — non-interactive transition screen between [[Quarters]] and [[Desk|Booth 04]].
- **Inspection [[Desk]] Surface:** `16:9` Flat Top-Down Texture (bottom workspace)
- **Observation Viewport Frame:** `4:3` or `16:9` Landscape (houses the `120 × 56` scaled ship texture / virtual framebuffer + crimson space backdrop + Brown Dwarf arc)
- **Main Hailing Viewscreen Frame:** `1:1` or `3:4` CRT Bezel (houses the `64 × 64` scaled portrait texture / virtual framebuffer)
- **[[Data_Slate_and_Telemetry|Data Slates]] & [[Desk_Reference_Manual_Data|Manuals]]:** `3:4` Vertical (uniform outer dimensions for clean text & holo-photo overlays)
- **[[Quarters]] Personal Terminal:** `4:3` Horizontal

### AI Image Prompt Suffix (For Static UI/Background Placeholders Only)
> 16-bit 1990s DOS LucasArts adventure game pixel art style, gritty orbital frontier station industrial aesthetic, dystopian border checkpoint framing, dim infrared crimson and rust-red ambient light from a brown dwarf star outside, weathered brushed titanium and oxidized bronze bulkheads, heavy manual Bayer dithering, stark shadows, crisp hard pixel edges, subtle amber and slate-blue console accents, strictly flat orthographic 2D plane with no perspective tilt.

---

## 3. Procedural Engine Architecture (Antigravity Build Spec)

Build the procedural renderer as a self-contained procedural art module inside the standalone native Windows desktop executable (`.exe`) (persisting state and seeds via a local disk JSON save file) using these five core blocks:

### Block 1: `PixelBuffer(w, h)` Rasterizer
Stores pixels in a flat `Array(w * h)` of hex strings (or `null` for transparent):
- `set(x, y, col)` / `fillRect(x, y, w, h, col)` — Integer-snapped pixel writes.
- `drawLine(x0, y0, x1, y1, col)` — Classic integer Bresenham line algorithm for 1px seams, ribs, tubes, conduits, and insignia.
- `fillPoly(pts, shaderFn)` — Scanline polygon filler accepting `shaderFn(px, py)` for directional Bayer-dithered shading.
- `fillEllipse(cx, cy, rx, ry, colFn)` — Normalized-distance ellipse filler passing `(px, py, dx, dy, dist)` for contoured heads, bubble cockpits, spots, reactor cores, and visors.
- `fillClippedEllipse(cx, cy, rx, ry, maskFn, colFn)` — Only writes pixels where `maskFn(px, py)` is already non-null (essential for clipping **Klaasari spots**, **Tolvak molt patches**, and **Prunjun flame decals** cleanly inside head/hull boundaries).
- `applyOutline(outlineCol = '#080b10')` — 4-neighbor pass adding a crisp 1px dark outline around non-null pixels.
- `applyRimLight(rimCol = '#7a2b26', edge = 'bottom')` — Optional 1px dim infrared Brown Dwarf rim highlight along the lower/facing silhouette edge of ships in the viewport.

### Block 2: `4×4` Bayer Ordered Dithering (`bayerDither` & `shadeRamp`)
Use a standard `4×4` Bayer threshold matrix to dither across **4-step material ramps** `[highlight, base, mid, shadow]`:
```text
CONSTANT BAYER_4X4 = [
  [ 0,  8,  2, 10],
  [12,  4, 14,  6],
  [ 3, 11,  1,  9],
  [15,  7, 13,  5]
]

FUNCTION bayerDither(x, y, colorDark, colorLight, ratio = 0.5):
  bx = ((round(x) MOD 4) + 4) MOD 4
  by = ((round(y) MOD 4) + 4) MOD 4
  threshold = (BAYER_4X4[by][bx] + 0.5) / 16.0
  RETURN (ratio > threshold) ? colorLight : colorDark

// Helper: maps normalized light intensity t in [0..1] across a 4-color ramp [shadow, mid, base, highlight]
FUNCTION sampleRamp(x, y, ramp, t):
  clamped = max(0.0, min(0.999, t))
  scaled  = clamped * (length(ramp) - 1)
  idx     = floor(scaled)
  frac    = scaled - idx
  IF idx >= length(ramp) - 1 THEN RETURN ramp[length(ramp) - 1]
  RETURN bayerDither(x, y, ramp[idx], ramp[idx + 1], frac)
```

### Block 3: Deterministic Seeding (`hashSeed` / `Mulberry32`)
- Hash any NPC or Ship ID string (e.g., `'capt-vek-409'`) via FNV-1a into a deterministic PRNG (`Mulberry32`).
- Every trait (skin ramp, head shape, spot coordinates, beard cut, uniform color, ship sub-parts, damage scratches) is derived strictly from the seed so characters stay 100% consistent across hailing screens, ID Data Slates, and repeat visits without saving images.

### Block 4: Multi-Mode Output Renderers
- **`PortraitRenderer.render(targetBuffer, seedObj, options)`:** (Renders into an in-memory RGBA `PixelBuffer`, uploaded to a cached GPU texture with Point/Nearest filtering.)
  - **Live Viewscreen (`mode: 'live'`):** Draws faction-tinted bridge background behind the `64 × 64` buffer, applies subtle CRT scanlines.
  - **Broken Camera (`mode: 'static'`):** Renders animated Bayer-dithered CRT static/snow with `"NO VISUAL SIGNAL"` scanline bars when a captain's camera is out ([[Inspection_Tools_and_Checks]]).
  - **[[Data_Slate_and_Telemetry|Data Slate]] Holo-Photo (`mode: 'holo'`):** Reuses the exact `64 × 64` buffer, converts pixels to luminance, and quantizes to a 5-step cyan/amber monochrome ramp with alternating scanline dimming and corner brackets.
  - **Forged / Bad Fake ID (`crudeCrayon: true`):** Intentionally jitters vertices, flattens dithering, or uses mismatched seed traits for sloppy forgeries.
- **`ShipRenderer.render(targetBuffer, shipSpec, tick, options)`:** (Renders into an in-memory RGBA `PixelBuffer`, uploaded to a cached GPU texture with Point/Nearest filtering.)
  - **Live Viewport (`mode: 'live'`):** Draws deep space + subtle Brown Dwarf crimson infrared gradient backdrop -> renders `120 × 56` ship buffer -> applies `tick`-driven animations:
    - Blinking navigation strobes (`floor(tick / 18) % 2`).
    - Pulsing **Warp Pylon** engine glow (`floor(tick / 9) % 2`).
    - **`weaponsHot: true`:** Pulsing `#dc2626` crimson glow pixels on dorsal/bow weapon mounts (visual violation tell). `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *(`#dc2626` here vs. `#ef4444` in SPS conduit mode below.)*
    - **`shieldsUp: true`:** Animated `1px` dithered teal/cyan oval shimmer perimeter around the hull (visual violation tell — shields are a visual check in the viewport; see [[Inspection_Tools_and_Checks]]). `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *(teal/cyan shimmer here vs. `#38bdf8` / `#e0f2fe` static arcs in SPS mode.)*
    - *Downstream Hull Scorch States:* When `MNG-` crosses `Dominant` (`>= +8` `power_score`), `FTV-` Free Traders spawn with visible hull scorch marks ([[Core_Loop_Mechanics]]).
  - **Inspection Manual Chart (`mode: 'manual'`):** Reuses the exact same `buildShipBuffer` output, recoloring all non-null pixels to glowing CRT Amber (`#ffb84d` / `#b45309`) so recognition charts always match live silhouettes.

### Block 5: Subspace Power-Grid Scanner (SPS) — Viewport Render Layer
*(From `gemini-code-1795085727104.md`)*
- **Trigger Condition:** Only renders when the player toggles `SPS` AND the ship's `shieldsUp === false`. If `shieldsUp === true`, draw animated static-interference arcs across the shield bubble (`#38bdf8` / `#e0f2fe`) blocking internal visibility.
- **Render Pass (On `120 × 56` Ship Buffer):**
  1. Dim all standard hull pixels by shifting them down into dark navy/slate monochrome (`#0c141d` – `#1c2b3a`).
  2. Draw a pulsing **Reactor Core** ellipse (`fillEllipse`) at the hull's central anchor.
  3. Draw `1px` Bresenham conduit lines (`drawLine`) with animated `tick` pulse pips from the Reactor Core to four fixed ship hardpoints:
     - **Forward Weapon Mounts:** `#ef4444` Alert Red if `weaponsHot === true`; dark `#1e293b` if offline.
     - **Warp Pylons:** `#f59e0b` Hot Amber if primed/spooled; dim teal `#115e59` if idle.
     - **Bridge Comms Array:** `#38bdf8` Active Cyan if powered; dark/flickering `#475569` if genuinely broken.
     - **Cargo Bay:** `#a855f7` High-Draw Violet if unexplained active power is inside an "empty" hold; dim `#1e293b` if standard unpowered cargo.

---

## 4. Starship Visual Language & Procedural Geometry (`120 × 56` Buffer)

All ships are drawn facing **Left** (`bow` toward `x = 8..35`, `core` at `x = 35..85`, `stern / Warp Pylons` at `x = 75..114`).

### Faction & Organization Ship Class Availability Matrix
The 4 standard ship categories are: **Military**, **Civilian**, **Cargo / Freighter**, and **Diplomatic / Explorer (Default — fancier non-military vessels)**.

| Faction / Organization (Prefix) | Military | Civilian | Cargo / Freighter | Diplomatic / Explorer | Special Pool / Notes |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **[[Galactic_Union]] (`GUV-`)** | Yes | Yes | Yes | Yes | `isDecommissioned: true` on Civilian & Cargo/Freighter ONLY |
| **[[Krushvak]] (`KRW-`)** | Yes | No | Yes | No | Military & Armored Cargo/Supply only |
| **[[Kordani]] (`KDF-`)** | Yes | No | No | Yes | Military & Diplomatic/Command only |
| **[[Brengop]] (`BMC-`)** | Yes | Yes | Yes | Yes | Military uses blunted Kordani hull template |
| **Hun-Borian (`HBR-`, [[The_Concord]])** | Yes | Yes | Yes | Yes | **Uses the `FTV-` Independent Free Traders schematic** |
| **Prunjun (`PRJ-`, [[Independent_and_Minor_Species]])** | No | No | No | No | **`hack_job` (Rigid Amalgam)** |
| **Orun (`ORN-`, [[Independent_and_Minor_Species]])** | No | Yes | Yes | Yes | No fighter / military hulls |
| **Tolvak (`TLV-`, [[Independent_and_Minor_Species]])** | No | Yes | Yes | No | Civilian & Cargo only |
| **Independent Free Traders (`FTV-`, [[Commercial_Organizations]])** | Yes | Yes | Yes | Yes | All 4 formats; shared with `HBR-` |
| **Far-Line Transit Corp (`FTC-`, [[Commercial_Organizations]])** | No | Yes | Yes | No | Cargo/Freighter & Civilian only |
| **Mining Guild (`MNG-`, [[Commercial_Organizations]])** | Yes | No | Yes | No | Cargo/Freighter & Military/War only |
| **Zenith Interstellar (`ZNI-`, [[Commercial_Organizations]])** | No | No | Yes | Yes | Diplomatic/Explorer & Cargo/Freighter only |

### Faction Hull Ramps (`[shadow, mid, base, highlight]`) & Geometry Specs

- **[[Galactic_Union]] (`GUV-`)**
  - *Visual Description:* Clean white and gunmetal hulls, smooth curves, unmistakable side-mounted **Warp Pylons** (outriggers). Operates all 4 ship types (rare visitors at VG-3).
  - *Primary Ramp (White):* `['#64748b', '#94a3b8', '#cbd5e1', '#f1f5f9']` `[CONFLICT: See _Conflicts_and_Plot_Holes.md]` *(standard white highlight is `#f1f5f9` here vs. `#e6eef5` referenced in `gemini-code-1795085727104.md`)*
  - *Secondary Ramp (Gunmetal):* `['#1e293b', '#334155', '#475569', '#64748b']`
  - *Engine Glow:* `#2dd4bf` (Teal)
  - *Geometry Rules:* Smooth forward saucer/ovoid `fillEllipse` bow, tapered curved fuselage `fillPoly`, and dual long horizontal **Warp Pylons** mounted above and below the stern on swept diagonal struts (`drawLine` + `fillRect`). Diplomatic/Explorer variants elongate the saucer and add sleek fins; Cargo adds clean smooth ventral pods.
  - **Decommissioned / Rundown Hull Variant (`isDecommissioned: true`):**
    - *Allowed Classes:* **Civilian** and **Cargo/Freighter** ONLY (**strict prohibition:** official GU Military warships never spawn with rundown shaders unless triggered by a scripted story event).
    - *Procedural Weathering Pass:* Swap standard clean white ramp to tarnished off-white/primer (`['#4e565e', '#78828a', '#a3acb3', '#c8cfd4']`); overlay 2–4 seeded dark rectangular hull-patch polygons (`#3d444d`) with `1px` rivet lines (`#1c2026`) and Bayer-dithered carbon-scorch streaks behind the warp pylons; dim warp pylon glow from vibrant cyan (`#2dd4bf`) to flickering pale teal (`#5b8c85`).

- **[[Kordani]] (`KDF-`)**
  - *Visual Description:* Deep blue-green hulls, sharp angular points, aggressive militaristic silhouettes. **No cargo or civilian ships** (Military & Diplomatic/Command only).
  - *Primary Ramp (Deep Blue-Green):* `['#0a2730', '#0f4c5c', '#1b6b73', '#2e8b8f']`
  - *Secondary Ramp (Dark Slate):* `['#0f172a', '#1e293b', '#334155', '#475569']`
  - *Engine Glow:* `#f59e0b` (Amber)
  - *Geometry Rules:* Sharp dagger-like forward prow (`fillPoly` with acute nose vertices), jagged swept-forward wing blades, serrated dorsal spine, and angular recessed engine slits. Zero rounded curves.

- **[[Brengop]] (`BMC-`)**
  - *Visual Description:* Practical, utilitarian style ships in a **gross yellowish color**. Their **Military** ships look very similar to [[Kordani]] silhouettes (reflecting [[The_Bulwark_Pact|Pact]] ship-sharing), but noticeably chunkier/more practical and painted gross yellow. Operates all 4 types.
  - *Primary Ramp (Gross Industrial Yellow):* `['#423c0e', '#695f18', '#968826', '#bdb040']`
  - *Secondary Ramp (Grimy Brass):* `['#2e2414', '#4a3a1e', '#6b5324', '#8a6d33']`
  - *Engine Glow:* `#d97706` (Dirty Orange)
  - *Geometry Rules:*
    - *Civilian / Cargo / Diplomatic:* Boxy, blunt brick-like barges (`fillRect` + beveled corners) with bolted ore hoppers and exposed thruster nozzles.
    - *Military:* Imports the Kordani angular hull polygon template, widens the midsection/nose vertices by `+2–4px` to blunt the sleekness into a chunky practical frame, adds bolted armor strips (`drawLine`), and renders using the Brengop gross yellow ramp.

- **[[Krushvak]] (`KRW-`)**
  - *Visual Description:* Military-focused ships in **lighter green and brown colors**. **No civilian or diplomatic/explorer ships** (Military & Armored Cargo/Supply only).
  - *Primary Ramp (Lighter Olive/Moss Green):* `['#2f4526', '#4a693b', '#688c51', '#8ab06c']`
  - *Secondary Ramp (Earth Brown):* `['#362418', '#523826', '#704e36', '#8c6549']`
  - *Engine Glow:* `#ef4444` (Red-Orange)
  - *Geometry Rules:* Heavy, forward-weighted armored prows with notched ramming noses, thick exposed dorsal armor ribbing (`drawLine`), bulky box-shaped **Warp Pylons**, and heavy ventral weapon/supply pods.

- **Prunjun (`PRJ-` — Rigid Procedural "Hack-Job" Assembly)**
  - *Visual Description:* Weird hack-job amalgamations of different races' ship parts bolted together; hard to confirm in-lore, so the generator uses **rigid rules** with two unmistakable defining features: an **always-visible round cockpit** and **painted flames** (40k Ork style, because they think flames make them go faster). Uses no standard ship categories.
  - *Flame Ramp:* `['#b91c1c', '#ef4444', '#f97316', '#facc15']`
  - *Cockpit Glass Ramp:* `['#1e3a5f', '#38bdf8', '#7dd3fc', '#e0f2fe']`
  - *Rigid Generator Logic:*
    1. Seed picks `core_hull` polygon + ramp from Random Faction A (GU, Kordani, Brengop, Krushvak, or Orun).
    2. Seed picks `bow_mount` polygon + ramp from Random Faction B (`B !== A`).
    3. Seed picks `stern_pylons` polygon + ramp from Random Faction C (`C !== A`).
    4. **Mandatory Lock 1 (Round Cockpit):** Draws a prominent circular bubble dome (`fillEllipse(cx, cy, 7, 7)`) protruding clearly from the upper-forward hull with bright glass highlight pixels.
    5. **Mandatory Lock 2 (Painted Flames):** Uses `fillClippedEllipse` / clipped `fillPoly` to paint jagged multi-tongue red-to-yellow flame decals streaking backward from the bow across the mismatched hull.

- **Orun (`ORN-`)**
  - *Visual Description:* Mostly large, practical freighters with unmistakable **clear cargo containers**. **No fighter / military ships** (Cargo, Civilian, Diplomatic only).
  - *Primary Ramp (Granite Grey):* `['#334155', '#64748b', '#94a3b8', '#cbd5e1']`
  - *Glass Container Ramp:* `['#1e293b', '#475569', '#94a3b8', '#e2e8f0']` (with `1px` specular diagonal shine lines)
  - *Geometry Rules:* Long, strictly symmetrical heavy girder spine with 2–4 large rectangular **clear/transparent cargo bays** along the belly/midsection where small colorful stacked crate rects are visibly rendered inside the glass outline. Zero weapon turrets.

- **Tolvak (`TLV-`)**
  - *Visual Description:* Terracotta/sand ramps (`['#4a251d', '#8c3b27', '#b45338', '#d6c29f']`). Low, flat-bellied desert-skiff silhouettes with long swept dorsal radiator fins (Civilian & Cargo only).

### Multi-Species [[Commercial_Organizations]] Ship Visual Profiles (`120 × 56`, Facing Left)

- **Independent Free Traders (`FTV-`) & Hun-Borian Free Vessels (`HBR-`)**
  - *Note:* Per confirmed canon override, **Hun-Borian Free Vessels (`HBR-`)** use the **`FTV-` Independent Free Traders** ship schematic (they are essentially the same).
  - *Description:* Loose, open-registry club for unaligned mercenaries, freelancers, and small-time traders. Practical, asymmetrical, heavily customized hulls.
  - *4-Shade Hull Ramp (Weathered Slate & Rust Trim):*
    - Primary Hull: `['#23272e', '#3e4552', '#5f697a', '#8591a6']`
    - Trim / Stripes: `['#6b2f1a', '#9c4827', '#cc6b35', '#eb9654']` (Burnt Orange / Rust)
    - Engine Glow: `#f59e0b` (Warm Amber)
  - *Allowed Ship Formats (All 4):*
    - **Military (Mercenary Gunboat):** Compact, snub-nosed wedge hull (`fillPoly`), oversized twin forward gun barrels (`fillRect`) jutting past the bow, heavy armored cockpit bulge, stubby dorsal fin, twin closely-tucked rear warp pylons.
    - **Civilian (Tramp Runner / Shuttle):** Small, boxy-oval light hull with an asymmetrical offset bridge pod on the top-left, external antenna mast (`drawLine`), and a single underslung rear warp pylon.
    - **Cargo / Freighter (Modular Tramp Hauler):** Long central spine (`fillRect`) carrying 2–3 mismatched, differently shaded rectangular cargo crates clamped underneath, forward cab with tiny amber viewport windows, wide twin engine thrusters at the stern.
    - **Diplomatic / Explorer (Refitted Private Yacht):** Lean, dart-shaped vintage hull with swept-back mid-wings, a long sensor nosecone, and visible aftermarket paint stripes across the fuselage.

- **Far-Line Transit Corp (`FTC-`)**
  - *Description:* Long-haul deep-space logistics and colony transport specialists. Built for endurance and extreme distances—long, cylindrical, heavily tanked designs.
  - *4-Shade Hull Ramp (Industrial Cream & Deep Navy):*
    - Primary Hull: `['#545147', '#827d6e', '#b5afa0', '#dcd7c9']` (Utilitarian Off-White / Bone)
    - Accent Band: `['#10233d', '#1c3b66', '#2a568f', '#4678b8']` (Deep Space Navy)
    - Engine Glow: `#38bdf8` (Cold Ion Blue)
  - *Allowed Ship Formats (2):*
    - **Cargo / Freighter (Deep-Space Colony Ark):** Massive horizontal cigar-shaped fuselage spanning nearly the full `120px` width; flanked by 3–4 huge spherical or ribbed fuel/cryo-pods (`fillEllipse`) strapped along the mid-section, reinforced blunt nose shield, and heavy quad warp pylons at the rear.
    - **Civilian (Long-Range Passenger Sleeper):** Sleek, elongated multi-deck tube with a continuous horizontal row of tiny lit cabin windows (`1px` amber dots), a prominent forward navigational dish/beacon, and long, thin endurance warp pylons extending well past the tail.

- **Mining Guild (`MNG-`)**
  - *Description:* Heavy industrial resource-extraction syndicate operating armored ore rigs and claim-protection escorts. Brutal, girder-heavy, hazard-striped construction silhouettes.
  - *4-Shade Hull Ramp (Dark Charcoal Steel & Hazard Yellow):*
    - Primary Hull: `['#181a1c', '#2c3036', '#464c54', '#666f7a']` (Heavy Cast Iron)
    - Trim / Stripes: `['#6e5009', '#a67b11', '#dba61f', '#facc15']` (Construction Hazard Yellow—sharper and more saturated than Brengop sickly yellow)
    - Engine Glow: `#fb923c` (Furnace Orange)
  - *Allowed Ship Formats (2):*
    - **Cargo / Freighter (Industrial Ore Rig):** Tall, blocky, asymmetrical industrial silhouette featuring open truss-frame scaffolding (`drawLine` cross-braces), massive exposed raw-ore hoppers/drills mounted on the lower bow, hazard-striped bumper plates, and thick vertical stabilizer thrusters.
    - **Military / War (Armored Claim-Guard Frigate):** Slab-sided, rectangular flying brick with thick beveled armor plates (`fillPoly`), twin recessed dorsal/ventral heavy turret blocks, reinforced ramming prow with diagonal yellow-and-black hazard stripes, and shielded boxy warp pylons.

- **Zenith Interstellar (`ZNI-`)**
  - *Description:* High-end interstellar cruise and hospitality line for wealthy passengers. Clean, opulent, sweeping horizontal curves with panoramic observation decks.
  - *4-Shade Hull Ramp (Deep Burgundy/Plum, Pearl & Gold Trim):*
    - Primary Hull: `['#361424', '#5c223e', '#853459', '#ad4b77']` *(or Pearl White `['#737a82', '#a6aeb8', '#d4dce6', '#f2f6fa']` with Burgundy belly)*
    - Trim / Accent: `['#785616', '#b38424', '#e0b043', '#fce08b']` (Polished Gold)
    - Engine Glow: `#a78bfa` (Soft Luxury Lavender) or `#2dd4bf` (Teal)
  - *Allowed Ship Formats (2):*
    - **Diplomatic / Explorer (Flagship Starliner / Luxury Cruise):** Graceful, sweeping whale-like or yacht-tiered silhouette (`fillPoly` with smooth stepped curves), a prominent curved panoramic glass promenade along the upper bow (`#93c5fd` dithered reflection), gold racing pinstripes (`drawLine`), and recessed, elegantly faired warp pylons.
    - **Cargo / Freighter (Hospitality & Provision Tender):** Noticeably cleaner and rounder than standard haulers—smooth teardrop bow leading into streamlined, enclosed white-and-burgundy supply pods (never exposed rusty crates) with gold corporate striping along the spine.

---

## 5. Species Portrait Visual Guide & Draw Specs (`64 × 64` Buffer)

### Standard `64 × 64` Portrait Pipeline (Bottom to Top)
1. **Neck & Uniform/Collar (`y = 44..63`):** Faction-colored shoulders/collar via `fillPoly` -> `applyOutline()`.
2. **Head Base (`cx = 32, cy = 28`):** Species skull via `fillEllipse` / `fillPoly` shaded radially with `sampleRamp(x, y, skinRamp, radialLight)`.
3. **Clipped Skin Markings:** Spots, scale ridges, or molt patches drawn via `fillClippedEllipse` / `set`.
4. **Facial Features (`y = 24..42`):** Eyes (`y = 26`), nose (`y = 32`), mouth (`y = 38`), species jaw/chin/brow ridges.
5. **Hair / Facial Hair / Spikes:** Species-specific hair, beards, sideburns, or crown spikes.
6. **Full Headgear / Optics:** Sealed helmets, hoses, visors, or goggles (when applicable).
7. **Final Pass:** `applyOutline('#080b10')`.

### [[Galactic_Union]] (GU) Species

- **Humans**
  - *Description:* Look human; includes full diversity of all human races, skin tones, and hairstyles.
  - *Skin Ramps (Seed picks from 6 diverse human tones):*
    - Deep: `['#2b1810', '#4a2a1c', '#6b3e2b', '#87543e']`
    - Rich Brown: `['#422416', '#6e3e27', '#94573a', '#b37150']`
    - Olive/Tan: `['#5c3d28', '#8c6142', '#b8855f', '#d4a37d']`
    - Warm Medium: `['#6e4732', '#a36e50', '#cc9474', '#e3b296']`
    - Light: `['#7a5444', '#b3836d', '#dba88f', '#f2cab6']`
    - Fair: `['#825e52', '#bd9182', '#e3b9aa', '#f7d8cc']`
  - *Geometry Rules:* Standard ovoid head (`rx = 12, ry = 15`), varied haircuts, optional facial hair, clean GU white/gunmetal collared jackets.

- **[[Helani]]**
  - *Description:* Clean features, look mostly human with diverse human-like skin tones, distinguished by **protruding chins** (plus sharp, composed brows).
  - *Skin Ramps:* Reuses the full Human skin ramp array.
  - *Geometry Rules:* Slightly narrower upper head (`rx = 11, ry = 14`), **mandatory elongated protruding chin polygon** extending lower (`y = 40..46`, squared/sculpted tip), crisp angled eyebrows (`drawLine`), neat geometric hairlines.

- **Klaasari**
  - *Description:* Blue and green with **distinctive, beautiful patterned spots** (either mostly blue with green spots, or mostly green with blue spots).
  - *Ramps:*
    - Ocean Blue: `['#113859', '#1d5d8f', '#2e86c1', '#5dade2']`
    - Emerald Green: `['#114d3a', '#1d7a5b', '#2eb083', '#5ee0b0']`
  - *Geometry Rules:* Seed flips a boolean `invertColors` (50% Blue base + Green spots, 50% Green base + Blue spots). After filling the smooth head ellipse, generate **symmetrical ornamental spot clusters** along both cheekbones, temples, and forehead (`x = 32 ± dx`) using `fillClippedEllipse(rx = 1..2, ry = 1..2)` in the secondary ramp so the spots form unmistakable crisp patterns.

- **Thanes**
  - *Description:* Almost **titanium-white** skin, **no hair on head**, and **short deep-black beards** as their distinctive feature.
  - *Skin Ramp (Titanium White):* `['#7c8a99', '#aebac7', '#d8e2eb', '#f1f5f9']`
  - *Beard Ramp (Deep Black):* `['#05070a', '#0f131a', '#1e2633', '#2d3748']`
  - *Geometry Rules:* Strictly bald head (`hairLayer = null`). **Mandatory sharp, neatly trimmed short black beard** (`fillPoly` hugging the lower jawline from ear to ear, `y = 34..45`, with crisp lip cutout) creating unmistakable white-and-black contrast.

- **Veldor**
  - *Description:* Require an **oxygen and sulfur** atmosphere, so they **always wear full head coverings / sealed helmets**.
  - *Helmet Ramp (Porcelain White & Brass):* White `['#64748b', '#94a3b8', '#cbd5e1', '#f8fafc']`, Brass trim `['#5c3a21', '#8c5832', '#b45309', '#d97706']`, Sulfur Visor `['#3b2b0a', '#785712', '#b5841d', '#e8b93c']`.
  - *Geometry Rules:* Skips organic face layers completely. Renders a sleek, rounded full-head GU-style sealed cowl (`fillEllipse(32, 28, 15, 17)`), dark amber-tinted oval faceplate visor (`fillEllipse(32, 27, 10, 7)` with diagonal 1px white glare), twin brass cheek-mounted sulfur scrubber canisters (`fillRect`), and a thin pale-yellow breather tube (`drawLine`) down to the collar.

### [[The_Bulwark_Pact|Bulwark Pact]] Species

- **[[Brengop]]**
  - *Description:* **Troll-like**, **little to no pigment**, intentionally gross-looking subterranean dwellers.
  - *Skin Ramps (Pigmentless Putty / Cave Ash):*
    - Pale Putty: `['#5c5c54', '#85857a', '#abab9f', '#c9c9be']`
    - Clammy Grey-Pink: `['#595052', '#827477', '#a8999c', '#c7b8bb']`
  - *Geometry Rules:* Wide, squat, lumpy head silhouette (`rx = 14, ry = 13` with heavy sagging lower jowl ellipses at `y = 35`), heavy low brow ridge overhanging sunken beady eyes, wide bulbous nose (`fillEllipse(32, 31, 4, 3)`), wide downturned mouth, and drooping pointed troll ears protruding at `x = 15` and `x = 49`.

- **[[Kordani]]**
  - *Description:* **Lizard-like**, with **blue, grey, or light purple** skin colors (where **light purple is seen as royalty**).
  - *Skin Ramps:*
    - Cold Blue: `['#1e3a4c', '#335c75', '#4b7c9b', '#6ea2c2']`
    - Slate Grey: `['#2c3540', '#475569', '#64748b', '#94a3b8']`
    - Royal Light Purple (Rare / High Rank): `['#4c356e', '#6d4c9c', '#9170c4', '#b89be6']`
  - *Geometry Rules:* Sharp angular cheekbones and tapering chin (`fillPoly`), subtle horizontal/angled scale-ridge pixels (`drawLine` along cheeks and neck using `shadow`/`highlight`), narrow slit-pupiled eyes, no hair, rigid high-collared military armor.

### GU Allies

- **[[Krushvak]]**
  - *Description:* **Deep brown, black, or deep green** skin color, **unique forehead ridge**, and **large sideburns** (explicitly distinct from Klingons).
  - *Skin Ramps:*
    - Deep Brown: `['#26150b', '#422615', '#5c3a21', '#784e30']`
    - Charcoal Black: `['#121214', '#222226', '#333338', '#47474f']`
    - Deep Forest Green: `['#132418', '#1f3b27', '#2d5438', '#3f704d']`
  - *Geometry Rules:* Broad, heavy skull (`rx = 13, ry = 15`). **Mandatory unique forehead ridge:** a vertical twin-crest or V-shaped brow armor ridge (`y = 16..24`, drawn with crisp highlight/shadow pixels—never horizontal waves). **Mandatory large mutton-chop sideburns:** thick dark jagged `fillPoly` patches sweeping down both cheeks from temples (`y = 22`) to the outer jaw (`y = 40`), leaving the chin clear.

### [[The_Concord]] (Defeated Invaders)

- **The Masters ("Dopples")**
  - *Description:* In true/unshifted form, they look **plain—like taking a human or any race and removing every feature into a smooth blob**. Usually encountered shapeshifted to look like other species.
  - *True Form Ramp (Waxy Clay-Tan / Blank Amber):* `['#5c4a38', '#8a7259', '#b5997b', '#d4ba9c']`
  - *Geometry Rules:*
    - *Disguised:* Generates using any target species' normal pipeline (allowing gameplay tells to come from documents/scanners or subtle intentional anomalies).
    - *True / Unstable Form:* Draws a smooth, slightly melting ovoid head (`fillEllipse`), **omitting** eyebrows, hair, ears, nostrils, and distinct lips—leaving only faint, recessed dark-dithered hollows where eyes and mouth would be.

- **Hun-Borians**
  - *Description:* **Stocky, large creatures** with **pale blue eyes** (to look kind), **spikes on their head instead of hair**, **yellow and brown skin** (warm and natural—very different from Brengop gross yellow), and **strong jawlines**.
  - *Skin Ramps (Warm Ochre-Yellow & Rich Earth-Brown):*
    - Warm Ochre-Yellow: `['#6b4710', '#9e6c19', '#cf9325', '#e8b343']`
    - Rich Earth-Brown: `['#3d2410', '#633c1b', '#875428', '#a86e3b']`
  - *Kind Eyes:* `#93c5fd` (Pale Sky Blue) with `#eff6ff` specular dot.
  - *Geometry Rules:* Massive bull-neck filling wide shoulder span (`x = 8..56`), heavy squared **strong jawline** (`fillPoly` lower face wider than upper temples), gentle wide-set **pale blue eyes**, and **5–7 short triangular bony spikes** (`fillPoly`, `y = 8..18`) crowning the top of the scalp instead of hair.

### [[Independent_and_Minor_Species|Independent & Minor Species]]

- **Prunjun**
  - *Description:* Heavy-set, thick-necked, lumpy scavengers with simple blunt expressions and salvaged gear.
  - *Skin Ramps:* Olive-Tan `['#4f4a36', '#787153', '#9c9470', '#b8b08c']` or Dull Grey-Pink `['#524444', '#7a6666', '#9e8686', '#bda4a4']`.
  - *Geometry Rules:* Wide, bottom-heavy pear-shaped head, grease smudge pixels on cheeks, crooked salvaged flight cap, and **oversized asymmetrical welding goggles** pushed up onto the forehead (`fillEllipse` lenses).

- **Tolvak**
  - *Description:* Desert reptilian freighter crews who periodically shed facial scales.
  - *Skin Ramps:* Warm Desert Terracotta/Sand `['#4a251d', '#7d3f2b', '#b45338', '#d6c29f']` (warm palette, distinct from Kordani cold blues).
  - *Geometry Rules:* Narrow reptilian head with visible lighter-colored **molting scale patches** (`fillClippedEllipse` in `#e8dcc5`) along the cheek/jaw when `isMolting: true`.

- **Orun**
  - *Description:* Heavy-boned, stone-skinned lithoid bureaucrats and archivists.
  - *Skin Ramp (Calcified Granite):* `['#334155', '#64748b', '#94a3b8', '#cbd5e1']`
  - *Geometry Rules:* Strictly angular faceted polygonal skull (`fillPoly`, zero soft ellipses), flat chiseled cheekbones, deeply recessed stoic eyes, crisp geometric high-collared robes with brass trim (`#d4af37`).
