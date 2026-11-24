using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

/// <summary>
/// Full interactive debug gallery for /design/08_Physical_Look accessible from Main Menu
/// and via --art-test CLI flag. Allows cycling every active species, portrait mode,
/// 4-SEED COMPARE side-by-side verification (LIVE + HOLO), faction/commercial organization,
/// ship class (enforcing the Availability Matrix), random seed, and live toggling
/// shieldsUp, weaponsHot, isDecommissioned, isMolting, and SPS while displaying active
/// VRAM texture count to verify zero memory leaks.
/// </summary>
public sealed class ArtTestScreen : IScreen
{
    private readonly ScreenManager _screenManager;

    private Texture2D _cachedPortraitTex;
    private PortraitRenderOptions? _lastPortraitOptions;

    private readonly Texture2D[] _compareLiveTextures = new Texture2D[4];
    private readonly Texture2D[] _compareHoloTextures = new Texture2D[4];
    private PortraitRenderOptions? _lastCompareBaseOptions;

    private Texture2D _cachedShipTex;
    private ShipRenderOptions? _lastShipOptions;

    // Gallery state
    private int _speciesUiIndex = 0;
    private int _portraitModeIndex = 0;
    private int _factionIndex = 0;
    private ShipClass _selectedShipClass = ShipClass.Civilian;
    private ShipRenderMode _shipRenderMode = ShipRenderMode.Live;

    private int _seedPresetIndex = 0;
    private string _currentSeed = "capt-vek-409";
    private uint _randomCounter = 1001;

    // Live interactive flags
    private bool _fourSeedCompare = false;
    private bool _shieldsUp = false;
    private bool _weaponsHot = false;
    private bool _isDecommissioned = false;
    private bool _isMolting = true;
    private bool _spsEnabled = false;
    private bool _crudeCrayonToggle = false;
    private bool _cargoHighDraw = false;
    private bool _commsBroken = false;
    private bool _animPaused = false;
    private int _animTick = 0;
    private float _tickAccumulator = 0f;

    private static readonly string[] PresetSeeds =
    [
        "capt-vek-409",
        "GUV-771-VERIDIA",
        "KDF-104-KORRVATH",
        "BMC-882-DULKRAG",
        "KRW-519-GHAROK",
        "ORN-442-LITHOS",
        "TLV-308-SOLARIS",
        "PRJ-009-HACKJOB",
        "FTV-612-CALDERA",
        "FTC-205-ENDURANCE",
        "MNG-990-ORUUN4",
        "ZNI-800-SOLVANE"
    ];

    private static readonly string[] CompareSeedSuffixes = ["", "-B7", "-C3", "-D9"];

    // 14 entries covering all 13 active species + both Dopple forms (True Form & Disguised)
    private static readonly (PortraitSpecies Species, bool DoppleTrueForm, string Label, string Note)[] SpeciesCatalog =
    [
        (PortraitSpecies.Human, false, "HUMAN (GU)", "10 hair cuts, 6 skin ramps, 6 beard/eye/nose pools"),
        (PortraitSpecies.Helani, false, "HELANI (GU)", "Protruding chin, angled brows, 10 hair cuts"),
        (PortraitSpecies.Klaasari, false, "KLAASARI (GU)", "Blue/green inverted, 4 symmetrical spot archetypes"),
        (PortraitSpecies.Thane, false, "THANE (GU)", "Bald white skull + 5 distinct black beard cuts"),
        (PortraitSpecies.Veldor, false, "VELDOR (GU)", "4 sealed cowls, 4 sulfur visors, 4 tube/stripe sets"),
        (PortraitSpecies.Brengop, false, "BRENGOP (PACT)", "Pigmentless troll, 4 ear angles, warts, tusks"),
        (PortraitSpecies.Kordani, false, "KORDANI (PACT)", "Lizard blue/grey/purple, 4 horn/frill crests"),
        (PortraitSpecies.Krushvak, false, "KRUSHVAK (ALLY)", "4 V/twin brow ridges, 4 sideburns, 4 topknots"),
        (PortraitSpecies.Dopple, true, "DOPPLE (TRUE FORM)", "Smooth melting featureless waxy blob"),
        (PortraitSpecies.Dopple, false, "DOPPLE (DISGUISED)", "Shapeshifted into target species"),
        (PortraitSpecies.HunBorian, false, "HUN-BORIAN (CONCORD)", "5-7 varied crown spikes, pale blue eyes, broad jaw"),
        (PortraitSpecies.Prunjun, false, "PRUNJUN (INDEP)", "Pear head, 4 cap tilts, swapped asym goggles"),
        (PortraitSpecies.Tolvak, false, "TOLVAK (INDEP)", "Terracotta reptilian + seeded isMolting patches"),
        (PortraitSpecies.Orun, false, "ORUN (INDEP)", "4 faceted polygonal granite skulls + brass inlays")
    ];

    private static readonly (PortraitRenderMode Mode, bool HoloAmber, string Label)[] PortraitModes =
    [
        (PortraitRenderMode.Live, false, "LIVE (BRIDGE + SCANLINES)"),
        (PortraitRenderMode.Static, false, "STATIC (NO VISUAL SIGNAL)"),
        (PortraitRenderMode.Holo, false, "HOLO (5-STEP CYAN SLATE)"),
        (PortraitRenderMode.Holo, true, "HOLO (5-STEP AMBER SLATE)"),
        (PortraitRenderMode.CrudeCrayon, false, "CRUDE CRAYON (FORGED ID)")
    ];

    public ArtTestScreen(ScreenManager screenManager)
    {
        _screenManager = screenManager;
    }

    public void OnEnter()
    {
        EnsureCachedTexturesUpToDate(forceRebuild: true);
    }

    public void OnExit()
    {
        UnloadCachedTextures();
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!_animPaused)
        {
            _tickAccumulator += deltaTime * 60.0f;
            if (_tickAccumulator >= 1.0f)
            {
                int steps = (int)_tickAccumulator;
                _animTick += steps;
                _tickAccumulator -= steps;
            }
        }

        HandleKeyboardShortcuts();
        HandleMouseInput(mouse);

        EnsureCachedTexturesUpToDate(forceRebuild: false);
    }

    private void HandleKeyboardShortcuts()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.S))
        {
            CycleSpecies(1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.M))
        {
            CyclePortraitMode(1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.V) || Raylib.IsKeyPressed(KeyboardKey.Six))
        {
            ToggleFourSeedCompare();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            CycleFaction(1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            CycleShipClass(1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            GenerateRandomSeed();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.One))
        {
            _shieldsUp = !_shieldsUp;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Two))
        {
            _weaponsHot = !_weaponsHot;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Three))
        {
            _isDecommissioned = !_isDecommissioned;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Four))
        {
            _isMolting = !_isMolting;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Five))
        {
            _spsEnabled = !_spsEnabled;
            AudioHooks.Play(_spsEnabled ? "sps_scanner_on" : "sps_scanner_off");
        }
    }

    private void HandleMouseInput(VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        // Top Header: Main Menu (1100, 6, 164, 28)
        if (mouse.IsHit(1100, 6, 164, 28))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }

        // Seed & Animation Bar (y = 52..88)
        if (mouse.IsHit(12, 52, 104, 36))
        {
            _seedPresetIndex = (_seedPresetIndex - 1 + PresetSeeds.Length) % PresetSeeds.Length;
            _currentSeed = PresetSeeds[_seedPresetIndex];
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(120, 52, 104, 36))
        {
            _seedPresetIndex = (_seedPresetIndex + 1) % PresetSeeds.Length;
            _currentSeed = PresetSeeds[_seedPresetIndex];
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(228, 52, 176, 36))
        {
            GenerateRandomSeed();
        }
        else if (mouse.IsHit(1048, 52, 220, 36))
        {
            _animPaused = !_animPaused;
            AudioHooks.Play("terminal_key_tab");
        }

        // Left Column: Portrait Controls (x = 12..556)
        else if (mouse.IsHit(24, 108, 44, 36))
        {
            CycleSpecies(-1);
        }
        else if (mouse.IsHit(72, 108, 428, 36) || mouse.IsHit(504, 108, 44, 36))
        {
            CycleSpecies(1);
        }
        else if (mouse.IsHit(24, 152, 44, 36))
        {
            CyclePortraitMode(-1);
        }
        else if (mouse.IsHit(72, 152, 428, 36) || mouse.IsHit(504, 152, 44, 36))
        {
            CyclePortraitMode(1);
        }
        // Row y = 196: [isMolting] [crudeCrayon] [4-SEED COMPARE]
        else if (mouse.IsHit(24, 196, 152, 36))
        {
            _isMolting = !_isMolting;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(182, 196, 152, 36))
        {
            _crudeCrayonToggle = !_crudeCrayonToggle;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(340, 196, 208, 36))
        {
            ToggleFourSeedCompare();
        }
        // Quick Portrait Mode buttons (y = 532)
        else if (mouse.IsHit(24, 532, 124, 36))
        {
            _portraitModeIndex = 0;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(154, 532, 124, 36))
        {
            _portraitModeIndex = 1;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(284, 532, 124, 36))
        {
            _portraitModeIndex = _portraitModeIndex == 2 ? 3 : 2;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(414, 532, 134, 36))
        {
            _portraitModeIndex = 4;
            AudioHooks.Play("terminal_key_tab");
        }

        // Right Column: Ship Controls (x = 564..1268)
        else if (mouse.IsHit(576, 108, 44, 36))
        {
            CycleFaction(-1);
        }
        else if (mouse.IsHit(624, 108, 580, 36) || mouse.IsHit(1208, 108, 44, 36))
        {
            CycleFaction(1);
        }
        else if (mouse.IsHit(576, 152, 44, 36))
        {
            CycleShipClass(-1);
        }
        else if (mouse.IsHit(624, 152, 304, 36) || mouse.IsHit(932, 152, 44, 36))
        {
            CycleShipClass(1);
        }
        else if (mouse.IsHit(984, 152, 268, 36))
        {
            _shipRenderMode = _shipRenderMode == ShipRenderMode.Live
                ? ShipRenderMode.Manual
                : ShipRenderMode.Live;
            AudioHooks.Play("terminal_key_tab");
        }
        // Direct Ship Class Availability Matrix buttons (y = 196)
        else if (mouse.IsHit(576, 196, 128, 36))
        {
            TrySelectShipClass(ShipClass.Military);
        }
        else if (mouse.IsHit(712, 196, 128, 36))
        {
            TrySelectShipClass(ShipClass.Civilian);
        }
        else if (mouse.IsHit(848, 196, 128, 36))
        {
            TrySelectShipClass(ShipClass.Cargo);
        }
        else if (mouse.IsHit(984, 196, 128, 36))
        {
            TrySelectShipClass(ShipClass.Diplomatic);
        }
        else if (mouse.IsHit(1120, 196, 132, 36))
        {
            TrySelectShipClass(ShipClass.HackJob);
        }
        // Ship Live Toggles Row 1 (y = 240)
        else if (mouse.IsHit(576, 240, 216, 36))
        {
            _shieldsUp = !_shieldsUp;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(800, 240, 224, 36))
        {
            _weaponsHot = !_weaponsHot;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(1032, 240, 220, 36))
        {
            _spsEnabled = !_spsEnabled;
            AudioHooks.Play(_spsEnabled ? "sps_scanner_on" : "sps_scanner_off");
        }
        // Ship Live Toggles Row 2 (y = 284)
        else if (mouse.IsHit(576, 284, 332, 36))
        {
            _isDecommissioned = !_isDecommissioned;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(916, 284, 168, 36))
        {
            _cargoHighDraw = !_cargoHighDraw;
            AudioHooks.Play("terminal_key_tab");
        }
        else if (mouse.IsHit(1092, 284, 160, 36))
        {
            _commsBroken = !_commsBroken;
            AudioHooks.Play("terminal_key_tab");
        }
    }

    private void ToggleFourSeedCompare()
    {
        _fourSeedCompare = !_fourSeedCompare;
        AudioHooks.Play("terminal_key_confirm");
        if (_fourSeedCompare)
        {
            // Unload single portrait texture while in 4-seed compare mode
            PixelBuffer.UnloadTexture(ref _cachedPortraitTex);
            _lastPortraitOptions = null;
        }
        else
        {
            // Cleanly unload all 8 compare textures when returning to single view
            UnloadCompareGridTextures();
        }
    }

    private void CycleSpecies(int delta)
    {
        _speciesUiIndex = (_speciesUiIndex + delta + SpeciesCatalog.Length) % SpeciesCatalog.Length;
        AudioHooks.Play("terminal_key_tab");
    }

    private void CyclePortraitMode(int delta)
    {
        _portraitModeIndex = (_portraitModeIndex + delta + PortraitModes.Length) % PortraitModes.Length;
        AudioHooks.Play("terminal_key_tab");
    }

    private void CycleFaction(int delta)
    {
        _factionIndex = (_factionIndex + delta + ShipRenderer.AllFactions.Count) % ShipRenderer.AllFactions.Count;
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        _selectedShipClass = ShipRenderer.NormalizeShipClass(faction, _selectedShipClass);
        AudioHooks.Play("terminal_key_tab");
    }

    private void CycleShipClass(int delta)
    {
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        IReadOnlyList<ShipClass> allowed = ShipRenderer.GetAllowedClasses(faction);
        int idx = 0;
        for (int i = 0; i < allowed.Count; i++)
        {
            if (allowed[i] == _selectedShipClass)
            {
                idx = i;
                break;
            }
        }
        int nextIdx = (idx + delta + allowed.Count) % allowed.Count;
        _selectedShipClass = allowed[nextIdx];
        AudioHooks.Play("terminal_key_tab");
    }

    private void TrySelectShipClass(ShipClass targetClass)
    {
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        if (ShipRenderer.IsClassAllowed(faction, targetClass))
        {
            _selectedShipClass = targetClass;
            AudioHooks.Play("terminal_key_tab");
        }
        else
        {
            AudioHooks.Play("detain_alarm_clamps");
        }
    }

    private void GenerateRandomSeed()
    {
        _randomCounter++;
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        string prefix = ShipRenderer.GetFactionPrefix(faction);
        uint hash = DeterministicRng.HashSeedFnv1a($"rnd_{_randomCounter}_{_animTick}");
        _currentSeed = $"{prefix}{(hash % 900) + 100:D3}-{(hash >> 8) & 0xFFFF:X4}";
        AudioHooks.Play("terminal_key_confirm");
    }

    private PortraitRenderOptions BuildCurrentPortraitOptions()
    {
        var spEntry = SpeciesCatalog[_speciesUiIndex];
        var modeEntry = PortraitModes[_portraitModeIndex];
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];

        return new PortraitRenderOptions
        {
            SeedId = _currentSeed,
            Species = spEntry.Species,
            DoppleInTrueForm = spEntry.DoppleTrueForm,
            Mode = modeEntry.Mode,
            HoloAmber = modeEntry.HoloAmber,
            FactionPrefix = ShipRenderer.GetFactionPrefix(faction),
            IsMolting = _isMolting,
            CrudeCrayon = _crudeCrayonToggle || modeEntry.Mode == PortraitRenderMode.CrudeCrayon,
            AnimTick = _animTick
        };
    }

    private ShipRenderOptions BuildCurrentShipOptions()
    {
        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        ShipClass effectiveClass = ShipRenderer.NormalizeShipClass(faction, _selectedShipClass);

        return new ShipRenderOptions
        {
            SeedId = _currentSeed,
            Faction = faction,
            RequestedClass = effectiveClass,
            Mode = _shipRenderMode,
            SpsEnabled = _spsEnabled,
            ShieldsUp = _shieldsUp,
            WeaponsHot = _weaponsHot,
            IsDecommissioned = _isDecommissioned,
            CargoHighDraw = _cargoHighDraw,
            CommsBroken = _commsBroken,
            AnimTick = _animTick
        };
    }

    /// <summary>
    /// Enforces strict caching: regenerates and re-uploads GPU Texture2D ONLY when the seed,
    /// species/faction, toggles, or quantized animation bucket actually changes.
    /// Explicitly unloads the previous GPU textures before uploading replacements.
    /// </summary>
    private void EnsureCachedTexturesUpToDate(bool forceRebuild)
    {
        PortraitRenderOptions nextPortrait = BuildCurrentPortraitOptions();

        if (_fourSeedCompare)
        {
            bool compareDirty = forceRebuild ||
                _compareLiveTextures[0].Id == 0 ||
                _lastCompareBaseOptions == null ||
                _lastCompareBaseOptions.SeedId != nextPortrait.SeedId ||
                _lastCompareBaseOptions.Species != nextPortrait.Species ||
                _lastCompareBaseOptions.DoppleInTrueForm != nextPortrait.DoppleInTrueForm ||
                _lastCompareBaseOptions.HoloAmber != nextPortrait.HoloAmber ||
                _lastCompareBaseOptions.FactionPrefix != nextPortrait.FactionPrefix ||
                _lastCompareBaseOptions.IsMolting != nextPortrait.IsMolting ||
                _lastCompareBaseOptions.CrudeCrayon != nextPortrait.CrudeCrayon;

            if (compareDirty)
            {
                UnloadCompareGridTextures();
                for (int i = 0; i < 4; i++)
                {
                    string subSeed = $"{nextPortrait.SeedId}{CompareSeedSuffixes[i]}";

                    PortraitRenderOptions liveOpt = nextPortrait with
                    {
                        SeedId = subSeed,
                        Mode = nextPortrait.CrudeCrayon ? PortraitRenderMode.CrudeCrayon : PortraitRenderMode.Live
                    };
                    PixelBuffer liveBuf = PortraitRenderer.RenderBuffer(liveOpt);
                    _compareLiveTextures[i] = liveBuf.ToTexture2D();

                    PortraitRenderOptions holoOpt = nextPortrait with
                    {
                        SeedId = subSeed,
                        Mode = PortraitRenderMode.Holo
                    };
                    PixelBuffer holoBuf = PortraitRenderer.RenderBuffer(holoOpt);
                    _compareHoloTextures[i] = holoBuf.ToTexture2D();
                }
                _lastCompareBaseOptions = nextPortrait;
            }
        }
        else
        {
            bool portraitDirty = forceRebuild ||
                _cachedPortraitTex.Id == 0 ||
                _lastPortraitOptions == null ||
                _lastPortraitOptions.SeedId != nextPortrait.SeedId ||
                _lastPortraitOptions.Species != nextPortrait.Species ||
                _lastPortraitOptions.DoppleInTrueForm != nextPortrait.DoppleInTrueForm ||
                _lastPortraitOptions.Mode != nextPortrait.Mode ||
                _lastPortraitOptions.HoloAmber != nextPortrait.HoloAmber ||
                _lastPortraitOptions.FactionPrefix != nextPortrait.FactionPrefix ||
                _lastPortraitOptions.IsMolting != nextPortrait.IsMolting ||
                _lastPortraitOptions.CrudeCrayon != nextPortrait.CrudeCrayon ||
                _lastPortraitOptions.EffectiveAnimBucket != nextPortrait.EffectiveAnimBucket;

            if (portraitDirty)
            {
                PixelBuffer.UnloadTexture(ref _cachedPortraitTex);
                PixelBuffer pBuf = PortraitRenderer.RenderBuffer(nextPortrait);
                _cachedPortraitTex = pBuf.ToTexture2D();
                _lastPortraitOptions = nextPortrait;
            }
        }

        ShipRenderOptions nextShip = BuildCurrentShipOptions();
        bool shipDirty = forceRebuild ||
            _cachedShipTex.Id == 0 ||
            _lastShipOptions == null ||
            _lastShipOptions.SeedId != nextShip.SeedId ||
            _lastShipOptions.Faction != nextShip.Faction ||
            _lastShipOptions.RequestedClass != nextShip.RequestedClass ||
            _lastShipOptions.Mode != nextShip.Mode ||
            _lastShipOptions.SpsEnabled != nextShip.SpsEnabled ||
            _lastShipOptions.ShieldsUp != nextShip.ShieldsUp ||
            _lastShipOptions.WeaponsHot != nextShip.WeaponsHot ||
            _lastShipOptions.IsDecommissioned != nextShip.IsDecommissioned ||
            _lastShipOptions.CargoHighDraw != nextShip.CargoHighDraw ||
            _lastShipOptions.CommsBroken != nextShip.CommsBroken ||
            _lastShipOptions.EffectiveAnimBucket != nextShip.EffectiveAnimBucket;

        if (shipDirty)
        {
            PixelBuffer.UnloadTexture(ref _cachedShipTex);
            PixelBuffer sBuf = ShipRenderer.RenderBuffer(nextShip);
            _cachedShipTex = sBuf.ToTexture2D();
            _lastShipOptions = nextShip;
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.DeepSpaceVoid);

        // =====================================================================
        // 1. TOP HEADER & LIVE VRAM TEXTURE TELEMETRY (1280x720 Native)
        // =====================================================================
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 40, Palette.OutlineVoid);
        Raylib.DrawLine(0, 40, Constants.VirtualWidth, 40, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("08_PHYSICAL_LOOK // PROCEDURAL ART GALLERY (1280x720 NATIVE)", 12, 15, 10, Palette.CrtAmberPrimary);

        string vramText =
            $"VRAM TEX: {PixelBuffer.TotalActiveVramTextures} (PROC:{PixelBuffer.ActiveProceduralTextureCount} GEN:{PixelBuffer.TotalProceduralTexturesCreated} FREE:{PixelBuffer.TotalProceduralTexturesUnloaded})";
        Raylib.DrawText(vramText, 512, 15, 10, Palette.ClearanceTeal);

        DrawButton(mouse, 1100, 6, 164, 28, "MAIN MENU");

        // =====================================================================
        // 2. SEED & ANIMATION BAR (y = 52..88)
        // =====================================================================
        DrawButton(mouse, 12, 52, 104, 36, "< SEED");
        DrawButton(mouse, 120, 52, 104, 36, "SEED >");
        DrawButton(mouse, 228, 52, 176, 36, "RANDOM [R]");

        uint fnvHash = DeterministicRng.HashSeedFnv1a(_currentSeed);
        Raylib.DrawText($"SEED: {_currentSeed} (FNV1a: 0x{fnvHash:X8})", 418, 65, 10, Palette.CrtAmberPrimary);

        DrawToggleButton(mouse, 1048, 52, 220, 36,
            _animPaused ? "ANIM: PAUSED" : "ANIM: LIVE",
            !_animPaused, Palette.ClearanceTeal);

        // =====================================================================
        // 3. LEFT PANEL: 64x64 PORTRAIT RENDERER & 4-SEED COMPARE (x = 12..556)
        // =====================================================================
        Raylib.DrawRectangle(12, 96, 544, 572, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(12, 96, 544, 572, Palette.WeatheredGunmetalEdge);

        var spEntry = SpeciesCatalog[_speciesUiIndex];
        DrawButton(mouse, 24, 108, 44, 36, "<");
        DrawButton(mouse, 72, 108, 428, 36, $"{_speciesUiIndex + 1}/{SpeciesCatalog.Length}: {spEntry.Label}");
        DrawButton(mouse, 504, 108, 44, 36, ">");

        var modeEntry = PortraitModes[_portraitModeIndex];
        DrawButton(mouse, 24, 152, 44, 36, "<");
        DrawButton(mouse, 72, 152, 428, 36, modeEntry.Label);
        DrawButton(mouse, 504, 152, 44, 36, ">");

        DrawToggleButton(mouse, 24, 196, 152, 36,
            _isMolting ? "MOLT: ON" : "MOLT: OFF",
            _isMolting, Palette.CrtAmberPrimary);

        bool crayonActive = _crudeCrayonToggle || modeEntry.Mode == PortraitRenderMode.CrudeCrayon;
        DrawToggleButton(mouse, 182, 196, 152, 36,
            crayonActive ? "CRYN: ON" : "CRYN: OFF",
            crayonActive, Palette.AlertCrimson);

        DrawToggleButton(mouse, 340, 196, 208, 36,
            _fourSeedCompare ? "4-SEED: ON [V]" : "4-SEED CMP [V]",
            _fourSeedCompare, Palette.ClearanceTeal);

        if (_fourSeedCompare)
        {
            // 4-SEED COMPARE GRID: 4 Seeds Side-by-Side at 2x scale (128x128)
            for (int i = 0; i < 4; i++)
            {
                int px = 20 + i * 132;
                Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
                if (_compareLiveTextures[i].Id != 0)
                {
                    Raylib.DrawTexturePro(_compareLiveTextures[i], src, new Rectangle(px, 244, 128, 128), Vector2.Zero, 0f, Color.White);
                    Raylib.DrawRectangleLines(px - 1, 243, 130, 130, Palette.CrtAmberDimmed);
                }
                if (_compareHoloTextures[i].Id != 0)
                {
                    Raylib.DrawTexturePro(_compareHoloTextures[i], src, new Rectangle(px, 384, 128, 128), Vector2.Zero, 0f, Color.White);
                    Raylib.DrawRectangleLines(px - 1, 383, 130, 130, Palette.ClearanceTeal);
                }
            }
            Raylib.DrawText("TOP: 4 SEEDS [LIVE 128x128]   |   BOT: 4 SEEDS [HOLO 128x128]", 28, 516, 10, Palette.ClearanceTeal);
        }
        else
        {
            // 4x Scaled Portrait (256x256), 2x Scaled Portrait (128x128), & 1x Native Portrait (64x64)
            if (_cachedPortraitTex.Id != 0)
            {
                Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
                Rectangle dst4x = new(28, 248, Constants.PortraitWidth * 4, Constants.PortraitHeight * 4);
                Raylib.DrawTexturePro(_cachedPortraitTex, src, dst4x, Vector2.Zero, 0f, Color.White);
                Raylib.DrawRectangleLines(27, 247, 258, 258, Palette.CrtAmberDimmed);

                Rectangle dst2x = new(304, 248, Constants.PortraitWidth * 2, Constants.PortraitHeight * 2);
                Raylib.DrawTexturePro(_cachedPortraitTex, src, dst2x, Vector2.Zero, 0f, Color.White);
                Raylib.DrawRectangleLines(303, 247, 130, 130, Palette.WeatheredGunmetalEdge);

                Raylib.DrawTexture(_cachedPortraitTex, 452, 248, Color.White);
                Raylib.DrawRectangleLines(451, 247, 66, 66, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText("64x64 NATIVE", 448, 322, 10, Palette.CrtAmberPrimary);
                Raylib.DrawText("128x128 (2X)", 306, 384, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawText("7-LAYER PIPELINE OK", 304, 412, 10, Palette.ClearanceTeal);
            Raylib.DrawText("BAYER 4x4 + #080b10", 304, 432, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("NO ZULAK/VESH/PRENN", 304, 452, 10, Palette.ClearanceTeal);
            Raylib.DrawText("4X CRT (256x256)", 48, 512, 10, Palette.CrtAmberDimmed);
        }

        // Quick 1-click Portrait Mode row (y = 532)
        DrawToggleButton(mouse, 24, 532, 124, 36, "LIVE", _portraitModeIndex == 0, Palette.ClearanceTeal);
        DrawToggleButton(mouse, 154, 532, 124, 36, "STATIC", _portraitModeIndex == 1, Palette.CrtAmberPrimary);
        DrawToggleButton(mouse, 284, 532, 124, 36, "HOLO", _portraitModeIndex is 2 or 3, Palette.ClearanceTeal);
        DrawToggleButton(mouse, 414, 532, 134, 36, "CRAYON", _portraitModeIndex == 4, Palette.AlertCrimson);

        Raylib.DrawText(spEntry.Note, 24, 584, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText("KEYS: [S]PECIES [M]ODE [V] 4-SEED COMPARE [4]MOLT", 24, 616, 10, Palette.CrtAmberDimmed);

        // =====================================================================
        // 4. RIGHT PANEL: 120x56 LEFT-FACING STARSHIP RENDERER & SPS LAYER (x = 564..1268)
        // =====================================================================
        Raylib.DrawRectangle(564, 96, 704, 572, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(564, 96, 704, 572, Palette.WeatheredGunmetalEdge);

        ShipFaction faction = ShipRenderer.AllFactions[_factionIndex];
        ShipClass effectiveClass = ShipRenderer.NormalizeShipClass(faction, _selectedShipClass);
        bool effectiveDecomm = ShipRenderer.IsDecommissionedEffective(faction, effectiveClass, _isDecommissioned, false);

        DrawButton(mouse, 576, 108, 44, 36, "<");
        DrawButton(mouse, 624, 108, 580, 36,
            $"{_factionIndex + 1}/{ShipRenderer.AllFactions.Count}: {ShipRenderer.GetFactionDisplayName(faction)}");
        DrawButton(mouse, 1208, 108, 44, 36, ">");

        DrawButton(mouse, 576, 152, 44, 36, "<");
        DrawButton(mouse, 624, 152, 304, 36, $"CLASS: {effectiveClass.ToString().ToUpperInvariant()}");
        DrawButton(mouse, 932, 152, 44, 36, ">");

        DrawToggleButton(mouse, 984, 152, 268, 36,
            _shipRenderMode == ShipRenderMode.Manual ? "CHART: MANUAL" : "VIEW: LIVE",
            _shipRenderMode == ShipRenderMode.Manual, Palette.CrtAmberPrimary);

        // Ship Class Availability Matrix Pills (y = 196)
        DrawClassMatrixPill(mouse, 576, 196, 128, 36, "MIL", faction, ShipClass.Military, effectiveClass);
        DrawClassMatrixPill(mouse, 712, 196, 128, 36, "CIV", faction, ShipClass.Civilian, effectiveClass);
        DrawClassMatrixPill(mouse, 848, 196, 128, 36, "CRG", faction, ShipClass.Cargo, effectiveClass);
        DrawClassMatrixPill(mouse, 984, 196, 128, 36, "DIP", faction, ShipClass.Diplomatic, effectiveClass);
        DrawClassMatrixPill(mouse, 1120, 196, 132, 36, "HACK", faction, ShipClass.HackJob, effectiveClass);

        // Live Toggles Row 1 (y = 240)
        DrawToggleButton(mouse, 576, 240, 216, 36,
            _shieldsUp ? "shieldsUp: ON" : "shieldsUp: OFF",
            _shieldsUp, Palette.ClearanceTeal);

        DrawToggleButton(mouse, 800, 240, 224, 36,
            _weaponsHot ? "weaponsHot: ON" : "weaponsHot: OFF",
            _weaponsHot, Palette.AlertCrimson);

        DrawToggleButton(mouse, 1032, 240, 220, 36,
            _spsEnabled ? "SPS LAYER: ON" : "SPS LAYER: OFF",
            _spsEnabled, Palette.SpsCommsActive);

        // Live Toggles Row 2 (y = 284)
        string decommLabel = !_isDecommissioned
            ? "isDecomm: OFF [3]"
            : (effectiveDecomm ? "isDecomm: ACTIVE" : "isDecomm: GU CIV/CRG!");
        DrawToggleButton(mouse, 576, 284, 332, 36,
            decommLabel,
            effectiveDecomm,
            _isDecommissioned && !effectiveDecomm ? Palette.AlertCrimson : Palette.CrtAmberPrimary);

        DrawToggleButton(mouse, 916, 284, 168, 36,
            _cargoHighDraw ? "CARGO:HOT" : "CARGO:STD",
            _cargoHighDraw, Palette.SpsCargoHighDraw);

        DrawToggleButton(mouse, 1092, 284, 160, 36,
            _commsBroken ? "COMMS:BRK" : "COMMS:OK",
            !_commsBroken, Palette.SpsCommsActive);

        // 120x56 Left-Facing Starship Viewport (4x scaled -> 480x224)
        Raylib.DrawText("<-- BOW (LEFT: x=8..35)      STERN/PYLONS (RIGHT: x=75..114) -->", 580, 334, 10, Palette.CrtAmberDimmed);
        if (_cachedShipTex.Id != 0)
        {
            Raylib.DrawRectangle(580, 360, Constants.ShipWidth * 4, Constants.ShipHeight * 4, Palette.DeepSpaceVoid);
            Rectangle src = new(0, 0, Constants.ShipWidth, Constants.ShipHeight);
            Rectangle dst4x = new(580, 360, Constants.ShipWidth * 4, Constants.ShipHeight * 4);
            Raylib.DrawTexturePro(_cachedShipTex, src, dst4x, Vector2.Zero, 0f, Color.White);
            Color borderCol = _spsEnabled ? Palette.SpsCommsActive : Palette.BrownDwarfGlowRim;
            Raylib.DrawRectangleLines(579, 359, Constants.ShipWidth * 4 + 2, Constants.ShipHeight * 4 + 2, borderCol);
        }

        // Right-side SPS & Hull Telemetry Legend Box (x = 1072..1252, y = 360..584)
        Raylib.DrawRectangle(1072, 360, 180, 224, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(1072, 360, 180, 224, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("SPS HARDPOINTS", 1084, 374, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(_weaponsHot ? "WPN: #ef4444" : "WPN: #1e293b", 1084, 404, 10,
            _weaponsHot ? Palette.SpsWeaponsHot : Palette.CrtAmberDimmed);
        Raylib.DrawText("WRP: #f59e0b", 1084, 428, 10, Palette.SpsWarpPrimed);
        Raylib.DrawText(!_commsBroken ? "COM: #38bdf8" : "COM: #475569", 1084, 452, 10,
            !_commsBroken ? Palette.SpsCommsActive : Palette.CrtAmberDimmed);
        Raylib.DrawText(_cargoHighDraw ? "CRG: #a855f7" : "CRG: #1e293b", 1084, 476, 10,
            _cargoHighDraw ? Palette.SpsCargoHighDraw : Palette.CrtAmberDimmed);
        Raylib.DrawText("GRID: 120x56 (4X)", 1084, 512, 10, Palette.ClearanceTeal);
        Raylib.DrawText("FACE: LEFT", 1084, 536, 10, Palette.CrtAmberPrimary);

        string shipRuleNote = effectiveClass switch
        {
            ShipClass.Civilian => "CIV SILHOUETTE: Compact ~68px shuttle/runabout profile with dynamic SPS anchors.",
            ShipClass.Cargo => "CRG SILHOUETTE: Stretched ~108px heavy hauler spine/pods with dynamic SPS anchors.",
            ShipClass.Military => "MIL SILHOUETTE: Compact wide aggressive combat hull with tucked-in armored pylons.",
            ShipClass.Diplomatic => "DIP SILHOUETTE: Stretched sleek cruiser/command yacht with dorsal fins/domes.",
            _ => "PRUNJUN HACK-JOB: Rigid Factions A+B+C + Round Bubble Cockpit + Clipped Flames."
        };
        Raylib.DrawText(shipRuleNote, 580, 604, 10, Palette.ClearanceTeal);
        Raylib.DrawText("SPS RULE: Dynamic anchors per class; blocked by static arcs if shieldsUp.", 580, 632, 10, Palette.CrtAmberDimmed);

        // =====================================================================
        // 5. FOOTER STATUS BAR (y = 680..720)
        // =====================================================================
        Raylib.DrawRectangle(0, 680, Constants.VirtualWidth, 40, Palette.OutlineVoid);
        Raylib.DrawLine(0, 680, Constants.VirtualWidth, 680, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            "HOTKEYS: [S]PECIES [M]ODE [V] 4-SEED [F]ACTION [C]LASS [R]ANDOM [1]SHIELD [2]WPN [3]DECOMM [4]MOLT [5]SPS",
            16, 695, 10, Palette.CrtAmberDimmed);
    }

    private void DrawClassMatrixPill(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string shortName,
        ShipFaction faction,
        ShipClass targetClass,
        ShipClass activeClass)
    {
        bool allowed = ShipRenderer.IsClassAllowed(faction, targetClass);
        bool selected = activeClass == targetClass;
        bool hovered = mouse.IsHit(x, y, w, h);

        Color bg = selected
            ? Palette.OxidizedBronzeShadow
            : (allowed ? Palette.WeatheredGunmetalMid : Palette.OutlineVoid);
        Color border = selected
            ? Palette.CrtAmberPrimary
            : (allowed ? (hovered ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge) : Palette.WeatheredGunmetalMid);
        Color textCol = selected
            ? Palette.CrtAmberPrimary
            : (allowed ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        string label = allowed ? $"[{shortName}]" : $"x{shortName}x";
        int lw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, x + Math.Max(6, (w - lw) / 2), y + (h - 10) / 2, 10, textCol);
    }

    private static void DrawButton(VirtualMouseState mouse, int x, int y, int w, int h, string text)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        int tw = Raylib.MeasureText(text, 10);
        Raylib.DrawText(text, x + Math.Max(6, (w - tw) / 2), y + (h - 10) / 2, 10, Palette.CrtAmberPrimary);
    }

    private static void DrawToggleButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string text,
        bool isActive,
        Color activeAccent)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = isActive ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = hovered ? Palette.CrtAmberPrimary : (isActive ? activeAccent : Palette.WeatheredGunmetalEdge);
        Color txtCol = isActive ? activeAccent : Palette.CrtAmberDimmed;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int tw = Raylib.MeasureText(text, 10);
        Raylib.DrawText(text, x + Math.Max(5, (w - tw) / 2), y + (h - 10) / 2, 10, txtCol);
    }

    private void UnloadCompareGridTextures()
    {
        for (int i = 0; i < 4; i++)
        {
            PixelBuffer.UnloadTexture(ref _compareLiveTextures[i]);
            PixelBuffer.UnloadTexture(ref _compareHoloTextures[i]);
        }
        _lastCompareBaseOptions = null;
    }

    private void UnloadCachedTextures()
    {
        PixelBuffer.UnloadTexture(ref _cachedPortraitTex);
        PixelBuffer.UnloadTexture(ref _cachedShipTex);
        UnloadCompareGridTextures();
        _lastPortraitOptions = null;
        _lastShipOptions = null;
    }

    public void Unload()
    {
        UnloadCachedTextures();
    }
}
