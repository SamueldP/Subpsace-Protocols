# Workspace Rules — Subspace Protocols (Native Windows .exe)

## Platform & Tech Stack
- Build a standalone native Windows 64-bit desktop executable (`.exe`) using **C# (.NET 8)** and **Raylib-cs** with `System.Text.Json`.
- NEVER introduce HTML, CSS, JavaScript, WebView, Electron, or browser dependencies.
- Keep game simulation logic (`Core/`, `Data/`, `Simulation/`) strictly decoupled from Raylib rendering and input (`Rendering/`, `UI/`).

## Source of Truth & Scope Control
- `/design` is the read-only design bible. NEVER modify, delete, or reorganize files inside `/design`.
- Implement strictly the lore, species, commercial organizations, prefixes, visual specs, economy numbers, and mechanics documented in `/design`. Do NOT invent new factions, upgrades, currencies, or narrative beats.
- Where `/design` contains `[TODO]` placeholders (such as unfinished faction quest arcs or Day 17 directives), implement clean data-driven JSON structures in `/data/` with clearly labeled placeholder entries so the creator can author them later without changing C# code.

## Pixel-Art & Memory Discipline
- Render the entire game to a single `640x360` `RenderTexture2D` virtual framebuffer (`TextureFilter.Point`), then blit it to the desktop window using strict integer scaling and black letterboxing.
- Transform all mouse screen coordinates cleanly into virtual `640x360` coordinates before UI hit-testing.
- Zero anti-aliasing, zero bilinear filtering, zero Gaussian blur, and zero font smoothing (use crisp pixel-aligned bitmap/point-filtered rendering).
- All runtime procedural textures (`PortraitRenderer`, `ShipRenderer`, thermal warp strips) MUST be cached per active ship/entity and explicitly unloaded (`Raylib.UnloadTexture`) when the ship leaves or the screen changes so VRAM never leaks.

## Audio & External Assets
- Do not generate or bundle audio files yet. Place explicit stub calls (`AudioHooks.Play("event_id")`) at every gameplay feedback point (citations, hailing, printer ticks, dock lock, detain alarm, terminal keys).
- Resolve `/data` and `/assets` relative to `AppContext.BaseDirectory` so paths work identically in development and inside the published `.exe` folder.