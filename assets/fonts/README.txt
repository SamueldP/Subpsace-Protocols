SUBSPACE PROTOCOLS — CUSTOM UI FONTS
====================================

Drop any `.ttf` or `.otf` font file into this folder (`assets/fonts/`) to
auto-register it at startup by filename stem (for example, `DepartureMono.ttf`
registers as `"DepartureMono"`).

Registered custom fonts become live-selectable presets in:
- The F1 Dev Debug Overlay (Row 17 FONT cycler & Row 18 FONT SIZE offset `-2..+3`)
- `"uiFontPreset"` and `"uiFontSizeOffset"` in `data/config.json`

Built-in Windows system presets are also auto-detected when available:
- ConsolasBold (`consolab.ttf`)
- Consolas (`consola.ttf`)
- CascadiaMonoSemiBold / CascadiaMono (`CascadiaMono.ttf` / `CascadiaCode.ttf`)
- LucidaConsole (`lucon.ttf`)
- CourierNewBold (`courbd.ttf`) / CourierNew (`cour.ttf`)
- SegoeUISemibold (`seguisb.ttf`) / SegoeUIBold (`segoeuib.ttf`) / SegoeUI (`segoeui.ttf`)
- TahomaBold (`tahomabd.ttf`)
- VerdanaBold (`verdanab.ttf`)
- TrebuchetBold (`trebucbd.ttf`)
- Bahnschrift (`bahnschrift.ttf`)
- RaylibDefault (always-available crisp bitmap baseline)

Recommended free OFL / CC0 monospace & pixel fonts (not bundled here):
- Departure Mono (OFL)
- Pixel Operator Bold (CC0 / Public Domain)
- Silkscreen (OFL)
- VT323 (OFL)
- IBM Plex Mono SemiBold (OFL)
- JetBrains Mono SemiBold (OFL)

All TrueType/OpenType presets apply calibrated cap-height scaling (`1.22x-1.26x`)
and glyph-atlas alpha contrast boosting so strokes render crisp and full-brightness.
