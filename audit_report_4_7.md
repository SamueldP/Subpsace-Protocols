# Pre-Prompt-5 Full Code Audit & Edge-Case Report (`Build 4.7`)

**Scope:** Strict read-only audit of the live Build 4.7 codebase (`Core/`, `Simulation/`, `Procedural/`, `UI/`, `data/`, `Program.cs`, `SubspaceProtocols.csproj`, `build_exe.ps1`) focusing on compounding risks across **Builds 4.5, 4.6, and 4.7** plus **Prompt 5 readiness** so we can execute a single surgical `Build 4.7.5` hardening pass before starting Prompt 5.

---

## Executive Summary & Subsystem Readiness Matrix

| # | Audit Area | Core Status | Key Risks / Action Required |
|---|---|---|---|
| 1 | **Font System & UI Legibility** (`Core/FontManager.cs`, `UI/UiText.cs`, `UI/*.cs`) | Zero raw `Raylib.DrawText`/`MeasureText` outside `FontManager`; zero GPU glyph-atlas leaks on preset/offset switch or `Shutdown()`; `U+2014` (`—`) is the only non-ASCII char in repo & is baked into `Codepoints`. | **A4** (`--smoke-test` / `--dump-frames` passes `headlessMode: isSmokeTest` even with window open); **B1** (bold presets / `uiFontSizeOffset >= +1` overflow & `12–13px` pitch collisions). |
| 2 | **Species Comms Audio** (`Core/AudioHooks.cs`, `data/voice_profiles.json`, `Simulation/CommsTypewriterQueue.cs`) | `AliasesPerClip = 3` round-robin; strict unload order (`UnloadSoundAlias` -> `UnloadSound` -> `CloseAudioDevice`); silent missing-`.wav`/headless fallback; `rawDeltaSeconds` cooldown decoupling; Zero-Tell Dopple (`dopple` -> `Human`, presented species only). | **B5** (`StopActiveCommsVoices` & click-to-snap do not stop `LoadedBaseSounds` / `"officer_protocols"` system clip). |
| 3 | **Encounter History, Pools & Saves** (`Core/GameState.cs`, `Simulation/EncounterHistoryQuery.cs`, `Simulation/FactionDialoguePool.cs`) | Duplicate-safe `RecordShipEncounterOnce`; exact `[currentDay - 3 .. currentDay - 1]` window; atomic `.tmp` + `File.Move` saves & null-safe legacy loads; `FactionDialoguePool` `DiskLoadCount == 1` & `[TODO...]` filtering. | **B4** (`[TODO: USER DIALOGUE PASS]` in `IntelEconomy.cs:223` & terminal JSON strings); **B6** (`JumpToDay(6/1)` & Morning Roster vs. Morning contract desync); **C1** (`tookBribe: false` awaiting Day 18). |
| 4 | **Auto-Shields / Auto-SPS, Protocol Review & Manual** (`UI/DeskScreen*.cs`, `Simulation/ShipGenerator.cs`, `Simulation/ValidationEngine.cs`) | Day 1+ Auto-Shields-Down cadence; `_autoSpsWeaponsOrderTriggered` debounce + spoofed `[!] HOT WEAPONS` lock; 2.35s fault trace auto-clear; `"MISMATCH ALREADY FOUND"`; Day 8–9 (2 tabs) vs. Day 10+ (3 tabs) Manual gating & Z-order/drag hitboxes. | **A1** (post-inspection Blank Slate unplug soft-locks `PROTOCOL REVIEW`); **A2** (Auto-SPS cools weapons before Officer speaks `"Power down weapons."`); **B2** (calm `[RE-TRANSMIT]` duplicates Captain upload line). |
| 5 | **Field Booklet, 5-Card Intro, Nixa & Renderers** (`UI/FieldBooklet.cs`, `UI/IntroScreen.cs`, `Procedural/*.cs`, `UI/ArtTestScreen*.cs`) | `IntroScreen` 5-card verbatim sequence + `F1` skip; `RenderRegeerNixaBuffer` (right-eye gold/crimson tassel) & `RenderKordaniSecurityOfficerBuffer`; `ShipRendererV2` (`280x120 @ 2x`) + `CorridorSpriteRenderer` (`16x32 @ 3x`) maintain `ActiveProceduralTextureCount == 0` on unload. | **B3** (`"884-D — MY NOTES"` Quarters/Desk desync & Quarters hover/tooltip bleed-through). |
| 6 | **Prompt 5 Readiness & Packaging** (`Core/Constants.cs`, `Core/ConfigFlags.cs`, `data/config.json`, `SubspaceProtocols.csproj`, `build_exe.ps1`) | All 11 unlock constants (`4/8/10/12/15/18/21/22/24/26/28`) & shift durations (`210/240/255/270/285`) align cleanly; `.csproj` & `build_exe.ps1` copy all `data\**\*.json` and `assets\**\*` + 10 comms folders. | **A3** (stale hardcoded `Day >= 3` Security Strikes gates & Day 3 Morning forum spoiler); **C2** (`DefaultDayUnlock*` & `$RequiredReleaseFiles` minor polish). |

---

## Section A — Critical / High-Severity Bugs & Soft-Locks (Fix Before Prompt 5)

### [A1] Day 6+ Post-Inspection Blank Data Slate Unplug Soft-Locks `PROTOCOL REVIEW`
* **Locations:**
  * `UI/DeskScreen.cs:591-593`, `UI/DeskScreen.cs:1952-1968` (`AbortBlankSlateDownloadMidStream`), `UI/DeskScreen.cs:2037-2046`
  * `UI/DeskScreen.DocumentsAndDrag.cs:736-749`
  * `Simulation/ValidationEngine.cs:206-215`
* **Root Cause:**
  1. If the player lets the Primary Data Slate finish downloading normally (`_phase == EncounterPhase.InspectingSlateAndShip`, `_slateTelemetry == DataSlateTelemetryState.Loaded`) and then inserts a Blank Data Slate into the Slate Bus, `DeskScreen.DocumentsAndDrag.cs:743-746` immediately triggers `BeginBlankSlateInterceptDownload(triggeredByReTransmit: true)`.
  2. Unlike `CompleteSubspaceProtocolsDownload()` (`DeskScreen.cs:2037-2046`), which branches on `_slateTelemetry != DataSlateTelemetryState.Loaded` vs. `Loaded`, pulling the Blank Data Slate out mid-stream calls `AbortBlankSlateDownloadMidStream()` (`DeskScreen.cs:1959`), which **unconditionally** sets `_phase = EncounterPhase.DownloadAborted`.
  3. On `Day >= 6`, lines `1961-1968` return early while leaving `_slateTelemetry == DataSlateTelemetryState.Loaded` and `_phase == EncounterPhase.DownloadAborted`.
  4. Because `IsShipInspectableForReview` (`DeskScreen.cs:591-592`) requires `_phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.AwaitingClearanceReturnToTopPort`, **the `PROTOCOL REVIEW` button becomes disabled and unclickable** for the rest of the encounter (unless the player knows to re-plug a slate and click `[RE-TRANSMIT]`), even though the Primary Data Slate is already 100% loaded on the desk. Detaining a guilty ship in this state triggers an `UNVERIFIED DETENTION` citation/fine (`ValidationEngine.cs:207`).
* **Recommended Fix:** In `AbortBlankSlateDownloadMidStream()` (`UI/DeskScreen.cs:1959`), restore `_phase` from `_slateTelemetry`:
  ```csharp
  _phase = _slateTelemetry switch
  {
      DataSlateTelemetryState.Loaded => EncounterPhase.InspectingSlateAndShip,
      DataSlateTelemetryState.ClearedReadyToTransmit => EncounterPhase.AwaitingClearanceReturnToTopPort,
      _ => EncounterPhase.DownloadAborted
  };
  ```

### [A2] Day 2+ Auto-SPS Weapons Sweep Cools Weapons Seconds *Before* Officer Speaks `"Power down weapons."`
* **Locations:**
  * `UI/DeskScreen.HardwareModules.cs:148-158` (`UpdateSpsHoldToLock`)
  * `UI/DeskScreen.cs:1461-1474` (`UpdateEncounterPhase`), `UI/DeskScreen.cs:1738-1784` (`ResolvePendingComplianceOrders`)
* **Root Cause:** When the SPS probe sweeps a ship's hot forward weapon mounts on Day 2+, `DeskScreen.HardwareModules.cs:154-157` appends `EnqueueOfficerLine("Power down weapons.", ...)` to the **end** of `_commsQueue` (which frequently still has 4–8 seconds of opening hail, upload, and auto-interrogation dialogue queued ahead of it) while simultaneously arming `_pendingWeaponsOrder = true` and `_complianceTimer = _activeShip.ComplianceDelaySeconds` (`0.20–0.55s`). Because `UpdateEncounterPhase` (`DeskScreen.cs:1469`) counts down `_complianceTimer` immediately every frame regardless of `_commsQueue`, honest armed ships extinguish their viewport weapon glow, mark `WEAPONS: OFFLINE`, and enqueue their compliance reply ~0.3s after the sweep—**seconds before Officer 884-D ever speaks `"Power down weapons."`** (and spoofed ships enqueue their bluff prematurely).
* **Recommended Fix:** Capture `ShipSpec targetShip = _activeShip;` and arm `_pendingWeaponsOrder = true; _complianceTimer = targetShip.ComplianceDelaySeconds;` inside the `onComplete` callback of `EnqueueOfficerLine("Power down weapons.", CommsTextTone.ClearanceTeal, onComplete: ...)`, guarded by `if (_activeShip == targetShip && !_isDetainingShip)`.

### [A3] Stale Hardcoded `Day >= 3` Security Strikes Gates & Day 3 Morning Forum Spoiler
* **Locations:**
  * `Simulation/EconomyEngine.cs:809-810` (`EvaluateForumFlag`)
  * `data/forum.json:66-107` (`d3_coil_repaired_cleared`, `d3_coil_repaired_detained`)
  * `UI/QuartersScreen.cs:1115` (`DrawTopCompactStatusStrip`)
  * `UI/TerminalOverlay.cs:1759-1763` (`DrawDeck4Tab`)
  * `UI/TutorialOverlay.cs:328`
* **Root Cause:** Build 4.5 moved the Ghost Route suspect to Day 3 (`Constants.DayGhostRouteSuspect = 3`) and Warp Scanner + Security Strikes activation to Day 4 (`Constants.DefaultDayUnlockWarpScanner = 4`), but four UI/simulation gates still hardcode `Day >= 3`:
  1. `Simulation/EconomyEngine.cs:809-810` checks `"day2_cleared" => state.Day >= 3 && !state.Day2SuspectDetained`. On **Day 3 Morning** (before the Day 3 shift has started), `state.Day == 3` and `Day2SuspectDetained == false`, prematurely unlocking thread `d3_coil_repaired_cleared` (`data/forum.json:66-85`), which spoils the upcoming Day 3 Ghost Route outcome, the Day 4 Warp coil repair, and Security Strikes activation 24 hours early.
  2. `UI/QuartersScreen.cs:1115` checks `if (_gameState.Day >= 3)` to draw `SECURITY STRIKES: 0/3` in the Quarters top bar on Day 3, contradicting `DeskScreen.cs` and `MainMenuScreen.cs` (which activate strikes on Day 4).
  3. `UI/TerminalOverlay.cs:1759-1763` sets `bool strikesOnline = _gameState.Day >= 3;` and `"LOCKED -- SECURITY STRIKES ACTIVATE DAY 03"`, while `EconomyEngine.TryPurchaseAuditDeferralFee` enforces `DayUnlockWarpScanner` (Day 4).
  4. `UI/TutorialOverlay.cs:328` still tells players `"At 3/3 Security Strikes (Day 3+)"` instead of Day 4+.
* **Recommended Fix:**
  * In `EconomyEngine.EvaluateForumFlag` (`Simulation/EconomyEngine.cs:809-810`), pass `config` (or check `Constants.DefaultDayUnlockWarpScanner`) so `"day2_detained"` / `"day2_cleared"` require `state.Day >= warpUnlockDay && state.Day2SuspectEncountered` (falling back safely on `state.Day >= warpUnlockDay` if jumped via `F1`), and bump `minDay` in `data/forum.json:69, 92` to `4`.
  * In `UI/QuartersScreen.cs:1115` and `UI/TerminalOverlay.cs:1759-1763`, gate on `_gameState.SecurityStrikesActive || _gameState.Day >= _config.DayUnlockWarpScanner` and format `"LOCKED -- SECURITY STRIKES ACTIVATE DAY {_config.DayUnlockWarpScanner:D2}"`.
  * Update `UI/TutorialOverlay.cs:328` to `"Day {config.DayUnlockWarpScanner}+"`.

### [A4] `--smoke-test` / `--dump-frames` Headless `FontManager` Flag Bypasses TrueType Atlases
* **Locations:**
  * `Program.cs:60-64, 76, 91, 3675, 3993, 4073, 4172`
  * `Core/FontManager.cs:45-48, 193-202, 224-235, 253-261`
* **Root Cause:** `Program.cs:76` opens a real Raylib OpenGL window even when `--smoke-test` or `--dump-frames=...` is passed, yet `Program.cs:91` calls `FontManager.Initialize(config.UiFontPreset, headlessMode: isSmokeTest)`. Inside `FontManager`, `ResolveEffectivePixelSize(10)` still applies `ConsolasBold`'s `ScaleMultiplier = 1.25f` (`effectivePx = 12`), and then `DrawText` (`line 225`) / `MeasureWidth` (`line 253`) short-circuit on `if (_isHeadlessMode)` into `DrawDefaultFont` / `MeasureDefaultFontWidth`. As a result:
  1. Visual verification screenshots emitted by `--smoke-test --dump-frames=...` never render TrueType fonts (`ConsolasBold` or custom `assets/fonts/`), rendering stretched 12px Raylib default bitmap text instead.
  2. `UiText.MeasureWidth` layout/overflow assertions inside `--smoke-test` measure the wide default bitmap font instead of the active TrueType preset.
* **Recommended Fix:** In `Program.cs:91` (and when restoring `FontManager.Initialize` at the end of `RunBuild46VerificationSuite` / `RunBuild47VerificationSuite`), pass `headlessMode: !Raylib.IsWindowReady()` (keeping explicit `headlessMode: true` only for the isolated unit-test sub-checks that specifically assert headless fallback behavior).

---

## Section B — Medium / Edge-Case & Layout Polish (Bundled into Build 4.7.5)

### [B1] Bold Presets & `uiFontSizeOffset` (`+1..+3`) Text Overflow / Vertical Overlap Hotspots at `1280x720`
* **Confirmed Hygiene Baseline:** Zero raw `Raylib.DrawText` / `Raylib.MeasureText` calls remain outside `Core/FontManager.cs` (`lines 259, 449`); `FontManager.UnloadCachedGpuFonts()` (`Core/FontManager.cs:454-469`) unloads every cached GPU font texture on `Initialize`, `SetActivePreset`, `SetFontSizeOffset`, `CyclePreset`, and `Shutdown`. Five leftover `PixelText.DrawText` facade calls remain in `UI/TerminalOverlay.cs:1171, 1173, 1175, 1177, 1220` (safe because `PixelText` forwards to `UiText.Draw`, though inconsistent).
* **Specific Overflow & Overlap Hotspots (especially at `ConsolasBold +2/+3` -> `effectivePx = 15..16` or wide proportional presets `VerdanaBold` / `TahomaBold` / `TrebuchetBold`):**
  1. **Brengop Reference Manual Tab Labels (`UI/DeskScreen.DocumentsAndDrag.cs:1519-1527, 1690`):** `DrawManualTabButton` renders `"SHIP REGISTRAR"` (`120px` tab) and `"SPECIES REGISTRAR"` (`132px` tab) at `x + 6` with **no truncation**, overflowing tab borders on wide bold presets at offset `0` and on `ConsolasBold` at offset `>= +1`.
  2. **12px Vertical Pitch in Dense Tables (`UI/DeskScreen.DocumentsAndDrag.cs:1368, 1545`; `UI/DeskScreen.ProtocolsAndComms.cs:1279-1290`):** Data Slate Day 4+ manifest rows (`lineStep = 12`) and Manual Tab 0 prefix rows (`ry += 12`) collide vertically when `effectivePx >= 15` (`uiFontSizeOffset >= +2`), while Protocol Review hitboxes remain 12px high.
  3. **`"884-D — MY NOTES"` Body Clipping & 13px Pitch (`UI/FieldBooklet.cs:262-275`):** Ruled writing zone uses 13px row spacing (`textY + i * 13`) and hard-caps `WrapToWidth(..., maxLines: 7)`. On `uiFontSizeOffset >= +2` or wide bold presets, dense pages (e.g., `day1_shift` 214 chars, `day2_sps` 218 chars) overlap rows vertically and silently truncate trailing sentences beyond line 7.
  4. **`TerminalOverlay` Footer, SKU Subtitles & Mail Headers (`UI/TerminalOverlay.cs:1182-1195, 1380-1400, 1801-1808, 1826`):** `DrawStatusFooter` draws 69-char `leftStats` untruncated against right-aligned `_statusMessage` (up to `460px` inside `1104px`); `DrawPactSkuRow` draws 76-char subtitles untruncated across `PactBtnX` (`ORDER`); `DrawMessagesTab` omits `TruncateText` on `FROM:` / `TIME:` / `STATUS:`.
  5. **`QuartersScreen` Service Chute Tray (`UI/QuartersScreen.cs:1983, 2016-2020`):** Header string and item `DisplayName` / `desc` lack `TruncateToWidth` before the right-hand `[-> COAT POCKET]` / `[STASH IN VENT]` buttons (`rowW - 288`).
* **Recommended Fix:** Apply `TruncateToWidth` in `DrawManualTabButton`, `DrawStatusFooter`, `DrawPactSkuRow`, `DrawMessagesTab` (also replacing the 5 `PixelText.DrawText` calls with `UiText.Draw`), and `DrawChutePickupTray`; clamp effective pixel size on dense 12–13px widgets (Data Slate fields, Manual directory, `"884-D — MY NOTES"` body) or cap `maxFontSizeOffset` / scale pitch dynamically when `effectivePx > 13`.

### [B2] Day 6+ Calm `[RE-TRANSMIT]` Enqueues Duplicate Captain `SubspaceUploadLine` (+ Dead Code)
* **Locations:** `UI/DeskScreen.cs:1873, 1889-1898, 1996-2000, 2055`
* **Root Cause:** After silent Blank Data Slate capture sets `_silentBlankSlateCaptureMode = true`, clicking `[RE-TRANSMIT]` with the Primary Data Slate plugged in causes `TryTriggerReTransmit()` (`lines 1996-2000`) to enqueue `CAPT: "{_activeShip.SubspaceUploadLine}"`, and `2.35s` later `CompleteSubspaceProtocolsDownload()` (`line 2055`) enqueues the exact same `CAPT: "{_activeShip.SubspaceUploadLine}"` a second time back-to-back. Separately, `BeginBlankSlateInterceptDownload` sets `_silentBlankSlateCaptureMode = true` unconditionally at `line 1873`, making the `if (enqueueDialogue && !_silentBlankSlateCaptureMode)` branch at `lines 1889-1893` unreachable dead code.
* **Recommended Fix:** Remove `EnqueueCaptainLine(_activeShip.SubspaceUploadLine)` from `TryTriggerReTransmit()` (`lines 1998-2000`) so the Officer requests `"Send subspace protocols."` on click and the Captain replies once when download completes (`line 2055`); remove the dead `!_silentBlankSlateCaptureMode` branch at `lines 1889-1893`.

### [B3] `"884-D — MY NOTES"` Quarters/Desk State Desync & Quarters Hover/Tooltip Bleed-Through
* **Locations:**
  * `UI/DeskScreen.cs:325` (`_fieldBooklet`)
  * `UI/QuartersScreen.cs:128, 544-552, 964-1005` (`_quartersBooklet`)
  * `UI/FieldBooklet.cs:162-172`
* **Root Cause:**
  1. **Quarters hover/tooltip bleed-through (`QuartersScreen.cs:964-1005`):** While `Update()` gives `_quartersBooklet` input priority over room furniture, `DrawVirtual()` passes unmodified `roomMouse` to `DrawRoomInterior`, `DrawCoatPocketTray`, and `DrawActiveHoverTooltip` **before** `_quartersBooklet.Draw`. At its default unfolded coordinates `(16, 388, 288, 252)`—or anywhere dragged—hovering the booklet highlights underlying props (`Door`, `Desk`, `Vent`) and renders floating room tooltips around the booklet.
  2. **Page sync & stale Page 1 open (`QuartersScreen.cs:544-552`):** `DeskScreen` and `QuartersScreen` own separate `FieldBookletWidget` instances, and clicking the minimized cover in Quarters clamps `PageIndex` (staying on `0` = Day 1 Morning) instead of jumping to `pages.Count - 1` like `FieldBookletWidget.HandleInternalClick` (`FieldBooklet.cs:167`), so opening notes on Day 1 Evening / Day 6 Morning shows stale Page 1 instead of the newly unlocked page.
* **Recommended Fix:** In `QuartersScreen.DrawVirtual` (`lines 964-978`), suppress `roomMouse` (`IsInsideViewport = false`) when `!IsAnySubOverlayOpen && _quartersBooklet.ContainsPoint(mouse.X, mouse.Y, bx, by)` so room props/tooltips never bleed through the booklet. Sync `PageIndex` via `GameState` (or jump to `pages.Count - 1` when newly unlocked pages arrive).

### [B4] `[TODO: USER DIALOGUE PASS]` Leaked into Live `CONTRACT SETTLED` Inbox Email & Terminal Views
* **Locations:**
  * `Simulation/IntelEconomy.cs:223` (`ExecuteMissionInterceptTransmit`)
  * `data/covert_missions.json:18, 31, 43, 54, 64, 77, 89, 100, 110`
  * `data/forum.json:17, 39, 77, 99, 121, 143, 165, 181, 197, 214, 231, 248, 265`
* **Root Cause:** `FactionDialoguePool.IsPlayableLine` cleanly filters `[TODO...]` placeholders from Comms Log dialogue, and `SectorMapModels.cs` intentionally uses `[TODO]` for unauthored star nodes (`Program.cs:1586-1698`). However, `IntelEconomy.cs:223` hardcodes literal `[TODO: USER DIALOGUE PASS]` (and `Constants.ScrubSecurityLogsCostScrip` instead of `config.ScrubSecurityLogsCost`) directly into a live `MESSAGES` email body, and `TerminalOverlay` renders `[TODO: USER DIALOGUE PASS]` / `[TODO]` suffixes verbatim from `covert_missions.json` and `forum.json`.
* **Recommended Fix:** Remove `[TODO: USER DIALOGUE PASS]` from `Simulation/IntelEconomy.cs:223` (and pass `config.ScrubSecurityLogsCost`), and strip trailing `[TODO: ...]` authoring tags in `TerminalOverlay` display helpers (preserving the JSON tags cleanly on disk for later writing).

### [B5] `AudioHooks.StopActiveCommsVoices()` Misses `"officer_protocols"` & `LoadedBaseSounds` Fallback on Snap
* **Locations:**
  * `Core/AudioHooks.cs:286-320, 376-410, 613-640`
  * `Simulation/CommsTypewriterQueue.cs:89-110, 500-526`
* **Root Cause:**
  1. `StopActiveCommsVoices()` (`AudioHooks.cs:391-410`) stops all `LoadedAliases`, but not `LoadedBaseSounds` (which `PlayClipSlot` uses as fallback at `lines 626-629` if alias creation fails).
  2. Clicking the Comms Strip to snap `"Send subspace protocols."` calls `OnCommsLineEnd`, which clears `IsAnchorBlipSuppressed` cleanly (`line 378`) but never stops the pre-recorded `"officer_protocols"` system clip (`PlaySystemClip` at `line 298`), allowing it to keep playing over the Captain's voice.
  3. `CommsTypewriterQueue.Clear()` (`CommsTypewriterQueue.cs:89-110`) skips `AudioHooks.OnCommsLineEnd(false)` if `_activeLine` had not yet finished `_preDelayRemaining`, and `SnapActiveAndDropPendingQueue()` (`lines 500-526`) skips `OnCommsLineEnd` under the same pre-delay guard.
* **Recommended Fix:** Stop `LoadedBaseSounds` in `StopActiveCommsVoices()`, stop `"officer_protocols"` aliases inside `OnCommsLineEnd` when `IsAnchorBlipSuppressed` was true, and reset `AudioHooks.IsAnchorBlipSuppressed` / call `OnCommsLineEnd` unconditionally on queue reset.

### [B6] `F1` Day Jump (`JumpToDay(6)` / `JumpToDay(1)`) & Morning Roster vs. Morning Contract Mutation
* **Locations:**
  * `UI/DebugOverlay.cs:642-679` (`JumpToDay`)
  * `Core/GameState.cs:792-855` (`AdvanceToDay`)
  * `Simulation/EconomyEngine.cs:633-640, 651-670` (`EnsureMorningRosterGenerated`)
  * `Simulation/ShipGenerator.cs:176-195`
* **Root Cause:**
  1. Jumping to **Day 6** via `DebugOverlay` Row 6 `LOAD DAY 06` (`JumpToDay(6)`) switches straight to `ScreenId.Desk` (`line 678`) without invoking `EnsureDay6MorningUnsolicitedDrop`, leaving `TerminalHackedUnlocked == false` and zero Blank Data Slate in `PocketItems` on Day 6 Desk (unlike `LOAD DAY 06 AM (HACK DROP)`). Conversely, jumping backward to **Day 1** (`JumpToDay(1)`) resets `Day1VorstHailCompleted` (`line 652`), but `AdvanceToDay(1)` never resets `Day1BrengopEmailRead = false` or `IsRead = false` on the Day 1 email.
  2. `EnsureMorningRosterGenerated` (`EconomyEngine.cs:666-667`) calls `generator.GenerateDailyQueue(state, Constants.PreGeneratedQueueSize)` without passing `config`, and runs at 05:00 wake-up (`line 634`) *before* Morning Quarters actions (accepting a `DECK 4` Intercept contract or decrypting a Delivery brief), which can alter slot 2/3 in `DeskScreen.PrepareDailyQueue` without updating `TodayMorningRoster`.
* **Recommended Fix:** Forward `config` in `EnsureMorningRosterGenerated` and refresh `TodayMorningRoster` (or append the newly targeted ship cleanly) when a Morning Quarters action adds a contract; reset `Day1BrengopEmailRead = false` in `AdvanceToDay(1)`; backfill `TerminalHackedUnlocked = true`, `Day6IntroMissionId`, and a Blank Data Slate in `DebugOverlay.JumpToDay(6)`.

---

## Section C — Low-Priority / Deferred Until Prompt 5–7

### [C1] `ValidationEngine` Hardcodes `tookBribe: false` Across All 7 Verdict Paths
* **Location:** `Simulation/ValidationEngine.cs:79, 110, 154, 184, 213, 258, 309, 389`
* **Note:** Ready for Prompt 5 (`dayUnlockFaceBribes: 18`). `EncounterRecord.TookBribe`, `EncounterHistoryQuery.HasRecentBribe`, and `FactionDialoguePool` `TookBribe` weaving are already implemented and smoke-tested.

### [C2] Minor `Constants.DefaultDayUnlock*` Coupling & `build_exe.ps1` Post-Publish Sentinel List
* **Locations:** `Core/GameState.cs:721, 782, 803, 835, 841, 845-847, 896`; `UI/LedgerOverlay.cs:181, 202`; `UI/CorridorScreen.cs:69`; `build_exe.ps1:76-82`
* **Note:** Under default `data/config.json`, `Constants.DefaultDayUnlock*` (`4/8/10`) match `ConfigFlags`; wiring `ConfigFlags` ensures full moddability if unlock days change. In `build_exe.ps1:76-82`, `$RequiredReleaseFiles` already copies `config.json` and `corridor_sprites.json` via line `39-47`, and can optionally assert both explicitly alongside `voice_profiles.json` and `dialogue\faction_pools.json`.

### [C3] Dead `_pendingShieldsOrder` & `WeaponsOrderOutcome.SpoofedDeflection` Leftovers
* **Locations:** `UI/DeskScreen.cs:506, 1461-1467, 1713-1736`; `Simulation/ShiftController.cs:205-210`
* **Note:** Leftover from removing manual `LOWER SHIELDS` / `PWR DOWN WPN` buttons in Build 4.5 (`AwaitingAutoShieldsDrop` and `_autoSpsWeaponsOrderTriggered` now own both flows). Harmless; clean out during Prompt 5 Desk additions.

### [C4] Data-Driven Narrative `[TODO]` Placeholders Ready for Content Authoring
* **Locations:** `data/placeholders_todo.json:1-16`; `data/dialogue/faction_pools.json:14-823`
* **Note:** Intentional JSON-only placeholders matching `GEMINI.md` scope rules (`Day 17` directives, faction quest paths, unauthored `faction_pools.json` variants skipped safely by `FactionDialoguePool.IsPlayableLine`).

---

## Surgical `Build 4.7.5` Pre-Prompt-5 Hardening Checklist
1. **[A1]** Fix post-inspection Blank Data Slate unplug `_phase` restoration in `AbortBlankSlateDownloadMidStream()` (`UI/DeskScreen.cs:1959`).
2. **[A2]** Delay Auto-SPS `_pendingWeaponsOrder` timer until `"Power down weapons."` finishes speaking via `onComplete` (`UI/DeskScreen.HardwareModules.cs:154-157`).
3. **[A3]** Replace stale `Day >= 3` gates with `DayUnlockWarpScanner` / `SecurityStrikesActive` (`Simulation/EconomyEngine.cs:809-810`, `data/forum.json:69,92`, `UI/QuartersScreen.cs:1115`, `UI/TerminalOverlay.cs:1759-1763`, `UI/TutorialOverlay.cs:328`).
4. **[A4]** Pass `headlessMode: !Raylib.IsWindowReady()` in `Program.cs:91` (and test restoration lines) so `--smoke-test` / `--dump-frames` exercise real TrueType metrics and PNG output.
5. **[B1–B6]** Bundle truncation/pitch guards (`DrawManualTabButton`, `FieldBookletWidget`, `TerminalOverlay`, `QuartersScreen`), calm `[RE-TRANSMIT]` dedup, Quarters booklet hover masking, `IntelEconomy.cs:223` `[TODO]` removal + `[TODO]` UI stripping, `AudioHooks` `"officer_protocols"` snap stop, and `JumpToDay(6/1)` + Morning Roster sync fixes.
6. Expand `--smoke-test` assertions to cover **A1** (mid-stream Blank Slate abort after primary load -> `Protocol Review` still works), **A2** (weapons stay hot until Officer finishes `"Power down weapons."`), and **A3** (Day 3 Morning Quarters hides `SECURITY STRIKES` & `d3_coil_repaired_cleared`).
