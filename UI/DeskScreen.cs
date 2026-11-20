using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;

namespace SubspaceProtocols.UI;

/// <summary>
/// Booth 04 (Desk) Horizontal Cockpit / Control-Tower Split Layout
/// defined in /design/08_Physical_Look/Visual_Direction_and_UI_Layout.md.
/// Renders cached procedural starships (120x56 -> 240x112), live hailing portraits (64x64 -> 128x128),
/// Data Slate holo-photos (64x64), and Reference Manual amber silhouettes (120x56).
/// </summary>
public sealed class DeskScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;

    private bool _spsEnabled;
    private float _simulatedShiftSeconds;
    private int _encounterIndex;
    private int _animTick;
    private float _tickAccum;

    private Texture2D _cachedShipTex;
    private Texture2D _cachedManualShipTex;
    private Texture2D _cachedLivePortraitTex;
    private Texture2D _cachedHoloPortraitTex;
    private int _lastAnimBucket = -1;

    private static readonly (string ShipSeed, ShipFaction Faction, ShipClass Class, PortraitSpecies Species, bool ShieldsUp, bool WeaponsHot, bool IsDecomm)[] SampleEncounters =
    [
        ("GUV-771-VERIDIA", ShipFaction.GalacticUnion, ShipClass.Civilian, PortraitSpecies.Human, false, false, true),
        ("KDF-104-KORRVATH", ShipFaction.Kordani, ShipClass.Military, PortraitSpecies.Kordani, false, false, false),
        ("BMC-882-DULKRAG", ShipFaction.Brengop, ShipClass.Cargo, PortraitSpecies.Brengop, false, false, false),
        ("ORN-442-LITHOS", ShipFaction.Orun, ShipClass.Cargo, PortraitSpecies.Orun, false, false, false),
        ("PRJ-009-HACKJOB", ShipFaction.Prunjun, ShipClass.HackJob, PortraitSpecies.Prunjun, false, true, false),
        ("FTV-612-CALDERA", ShipFaction.FreeTraders, ShipClass.Cargo, PortraitSpecies.HunBorian, false, false, false),
        ("FLT-205-ENDURANCE", ShipFaction.FarLineTransit, ShipClass.Civilian, PortraitSpecies.Klaasari, false, false, false),
        ("MNG-990-ORUUN4", ShipFaction.MiningGuild, ShipClass.Cargo, PortraitSpecies.Krushvak, true, false, false),
        ("ZNI-800-SOLVANE", ShipFaction.ZenithInterstellar, ShipClass.Diplomatic, PortraitSpecies.Helani, false, false, false)
    ];

    public DeskScreen(ScreenManager screenManager, GameState gameState, Core.ConfigFlags config)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
    }

    public void OnEnter()
    {
        _spsEnabled = false;
        _simulatedShiftSeconds = 0f;
        _lastAnimBucket = -1;
        RebuildEncounterTextures(forceAll: true);
    }

    public void OnExit()
    {
        UnloadCachedTextures();
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _simulatedShiftSeconds = Math.Min(Constants.ShiftDurationSeconds, _simulatedShiftSeconds + deltaTime);
        _tickAccum += deltaTime * 60f;
        if (_tickAccum >= 1f)
        {
            int steps = (int)_tickAccum;
            _animTick += steps;
            _tickAccum -= steps;
        }

        int currentBucket = _animTick / 9;
        if (currentBucket != _lastAnimBucket)
        {
            RebuildEncounterTextures(forceAll: false);
        }

        if (!mouse.LeftPressed)
            return;

        // Top navigation bar buttons
        if (mouse.IsHit(548, 4, 86, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
        }
        else if (mouse.IsHit(468, 4, 76, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
        }
        // SPS Toggle button
        else if (mouse.IsHit(14, 148, 108, 18))
        {
            _spsEnabled = !_spsEnabled;
            AudioHooks.Play(_spsEnabled ? "sps_scanner_on" : "sps_scanner_off");
            RebuildEncounterTextures(forceAll: false);
        }
        // Next Hail stub button
        else if (mouse.IsHit(128, 148, 96, 18))
        {
            _encounterIndex = (_encounterIndex + 1) % SampleEncounters.Length;
            AudioHooks.Play("hail_incoming_chime");
            RebuildEncounterTextures(forceAll: true);
        }
        // Clearance Dock (Grant Access)
        else if (mouse.IsHit(526, 200, 102, 74))
        {
            AudioHooks.Play("dock_lock_grant_access");
        }
        // Physical Detain Switch
        else if (mouse.IsHit(526, 282, 102, 30))
        {
            AudioHooks.Play("detain_alarm_clamps");
        }
    }

    private void RebuildEncounterTextures(bool forceAll)
    {
        var enc = SampleEncounters[_encounterIndex];
        int currentBucket = _animTick / 9;
        _lastAnimBucket = currentBucket;

        // Live / SPS Ship texture
        PixelBuffer.UnloadTexture(ref _cachedShipTex);
        PixelBuffer shipBuf = ShipRenderer.RenderBuffer(new ShipRenderOptions
        {
            SeedId = enc.ShipSeed,
            Faction = enc.Faction,
            RequestedClass = enc.Class,
            Mode = ShipRenderMode.Live,
            SpsEnabled = _spsEnabled,
            ShieldsUp = enc.ShieldsUp,
            WeaponsHot = enc.WeaponsHot,
            IsDecommissioned = enc.IsDecomm,
            AnimTick = _animTick
        });
        _cachedShipTex = shipBuf.ToTexture2D();

        if (forceAll || _cachedLivePortraitTex.Id == 0)
        {
            PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
            PixelBuffer manualBuf = ShipRenderer.RenderBuffer(new ShipRenderOptions
            {
                SeedId = enc.ShipSeed,
                Faction = enc.Faction,
                RequestedClass = enc.Class,
                Mode = ShipRenderMode.Manual
            });
            _cachedManualShipTex = manualBuf.ToTexture2D();

            string prefix = ShipRenderer.GetFactionPrefix(enc.Faction);
            PixelBuffer.UnloadTexture(ref _cachedLivePortraitTex);
            PixelBuffer livePortraitBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
            {
                SeedId = enc.ShipSeed,
                Species = enc.Species,
                Mode = PortraitRenderMode.Live,
                FactionPrefix = prefix
            });
            _cachedLivePortraitTex = livePortraitBuf.ToTexture2D();

            PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
            PixelBuffer holoPortraitBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
            {
                SeedId = enc.ShipSeed,
                Species = enc.Species,
                Mode = PortraitRenderMode.Holo,
                FactionPrefix = prefix
            });
            _cachedHoloPortraitTex = holoPortraitBuf.ToTexture2D();
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.WeatheredGunmetalMid);
        var enc = SampleEncounters[_encounterIndex];

        // Top Bar: Booth 04 Bulkhead Header (zero clock UI — Brown Dwarf arc tracks time)
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 23, Constants.VirtualWidth, 23, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            $"VG-3 // BOOTH 04 CUSTOMS & DOCKING CONTROL | DAY {_gameState.Day:D2} | SCRIP: {_gameState.PactScrip} SC",
            8, 7, 10, Palette.CrtAmberPrimary);

        DrawSmallButton(mouse, 468, 4, 76, 16, "MAIN MENU");
        DrawSmallButton(mouse, 548, 4, 86, 16, "END -> BERTH");

        // =====================================================================
        // TOP HALF: Horizontal Cockpit Split
        // 1. Wide External Observation Viewport (Left/Center: 420 x 142)
        // 2. Corner Main Hailing Viewscreen CRT (Right: 196 x 142)
        // =====================================================================
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_viewport_frame.png",
            8, 28, 420, 142,
            Palette.DeepSpaceVoid,
            Palette.WeatheredGunmetalEdge,
            "VIEWPORT (420x142) - DIEGETIC BROWN DWARF ARC");

        // Diegetic Brown Dwarf orbital arc (4m 30s / 270s shift timer, zero clock UI)
        float progress = _simulatedShiftSeconds / Constants.ShiftDurationSeconds;
        int dwarfX = 48 + (int)(progress * 330);
        int dwarfY = 48 + (int)(MathF.Sin(progress * MathF.PI) * -14f + progress * 55f);
        Raylib.DrawCircle(dwarfX, dwarfY, 20, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircleLines(dwarfX, dwarfY, 20, Palette.BrownDwarfGlowRim);

        // 120 x 56 Procedural Ship Sprite slot (scaled 2x -> 240 x 112 in viewport)
        const int shipBoxX = 98;
        const int shipBoxY = 34;
        if (_cachedShipTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.ShipWidth, Constants.ShipHeight);
            Rectangle dst = new(shipBoxX, shipBoxY, Constants.ShipWidth * 2, Constants.ShipHeight * 2);
            Raylib.DrawTexturePro(_cachedShipTex, src, dst, Vector2.Zero, 0f, Color.White);
            Color shipBorder = _spsEnabled ? Palette.SpsCommsActive : Palette.BrownDwarfGlowRim;
            Raylib.DrawRectangleLines(shipBoxX - 1, shipBoxY - 1, Constants.ShipWidth * 2 + 2, Constants.ShipHeight * 2 + 2, shipBorder);
        }

        // Viewport Controls Overlay (SPS Toggle & Next Hail)
        DrawSmallButton(mouse, 14, 148, 108, 18, _spsEnabled ? "SPS SCAN: ACTIVE" : "SPS SCAN: OFF",
            _spsEnabled ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
        DrawSmallButton(mouse, 128, 148, 96, 18, "NEXT HAIL");

        // Corner Main Hailing Viewscreen Frame (CRT Bezel housing 64x64 portrait)
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_hailing_crt.png",
            436, 28, 196, 142,
            Palette.OutlineVoid,
            Palette.OxidizedBronzeWorn,
            "HAILING CRT");

        if (_cachedLivePortraitTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(470, 36, Constants.PortraitWidth * 2, Constants.PortraitHeight * 2);
            Raylib.DrawTexturePro(_cachedLivePortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
            Raylib.DrawRectangleLines(469, 35, 130, 130, Palette.CrtAmberDimmed);
        }

        // =====================================================================
        // BOTTOM HALF: Flat Top-Down Inspection Desk Surface (0 deg orthographic)
        // =====================================================================
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_surface.png",
            8, 174, 624, 180,
            Palette.WeatheredGunmetalMid,
            Palette.WeatheredGunmetalEdge,
            "STATIC ASSET: locations/desk_surface.png (TOP-DOWN 0 DEG DESK SURFACE)");

        // 1. Morning Shift Directive & Bio-Scanner Indicator LED + Optional Gravity Gauge
        Raylib.DrawRectangle(16, 196, 138, 66, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(16, 196, 138, 66, Palette.CrtAmberDimmed);
        Raylib.DrawText("SHIFT DIRECTIVE", 22, 202, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText("BIO-SCAN LED: [GREEN]", 22, 220, 10, Palette.ClearanceTeal);

        if (_config.EnableGravityGauge)
        {
            Raylib.DrawText("GRAV-GAUGE: 0420 T", 22, 238, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            Raylib.DrawText("GRAV-GAUGE: [CUT]", 22, 238, 10, Palette.WeatheredGunmetalEdge);
        }

        // 2. Warp Field Residue Spectrometer (with yellow MAINTENANCE PENDING tag on Days 1-2)
        Raylib.DrawRectangle(16, 268, 138, 52, Palette.OxidizedBronzeShadow);
        Raylib.DrawRectangleLines(16, 268, 138, 52, Palette.OxidizedBronzeWorn);
        Raylib.DrawText("WARP SPECTROMETER", 22, 274, 10, Palette.CrtAmberPrimary);
        if (_gameState.Day <= 2)
        {
            Raylib.DrawRectangle(20, 290, 130, 22, Palette.CrtAmberPrimary);
            Raylib.DrawText("MAINTENANCE PENDING", 24, 296, 10, Palette.OutlineVoid);
        }
        else
        {
            Raylib.DrawText("THERMAL SLOT READY", 22, 296, 10, Palette.ClearanceTeal);
        }

        // 3. Contraband / Hidden Pocket Slot
        Raylib.DrawRectangle(16, 324, 138, 24, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(16, 324, 138, 24, Palette.WeatheredGunmetalEdge);
        string pocketText = _gameState.ContrabandPocket != null
            ? $"POCKET: {_gameState.ContrabandPocket.ItemType}"
            : "POCKET SLOT: [EMPTY]";
        Raylib.DrawText(pocketText, 22, 331, 10, Palette.CrtAmberDimmed);

        // 4. Movable Data Slate (3:4 Vertical, 0 deg orthographic) with 64x64 Holo-Photo
        AssetManager.DrawImageOrPlaceholder(
            "ui/data_slate_frame.png",
            168, 196, 164, 152,
            Palette.OutlineVoid,
            Palette.ClearanceTeal,
            "DATA SLATE (3:4)");
        Raylib.DrawText($"ID: {enc.ShipSeed}", 176, 214, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText($"SPECIES: {enc.Species}", 176, 228, 10, Palette.ClearanceTeal);
        if (_cachedHoloPortraitTex.Id != 0)
        {
            Raylib.DrawTexture(_cachedHoloPortraitTex, 178, 244, Color.White);
            Raylib.DrawRectangleLines(177, 243, 66, 66, Palette.ClearanceTeal);
        }

        // 5. Desk Reference Manual (3:4 Vertical, 0 deg orthographic) with CRT Amber Ship Silhouette
        AssetManager.DrawImageOrPlaceholder(
            "ui/desk_manual_frame.png",
            344, 196, 168, 152,
            Palette.OutlineVoid,
            Palette.CrtAmberDimmed,
            "REFERENCE MANUAL (3:4)");
        Raylib.DrawText($"CHART: {enc.Faction} ({enc.Class})", 352, 214, 10, Palette.CrtAmberPrimary);
        if (_cachedManualShipTex.Id != 0)
        {
            Raylib.DrawTexture(_cachedManualShipTex, 366, 246, Color.White);
            Raylib.DrawRectangleLines(365, 245, Constants.ShipWidth + 2, Constants.ShipHeight + 2, Palette.CrtAmberDimmed);
        }

        // 6. Right-Side Controls: USB-Style Clearance Dock (Grant Access), Detain Switch, Conditional Destroy
        bool dockHovered = mouse.IsHit(526, 200, 102, 74);
        Raylib.DrawRectangle(526, 200, 102, 74, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(526, 200, 102, 74, dockHovered ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        Raylib.DrawText("CLEARANCE DOCK", 532, 210, 10, Palette.ClearanceTeal);
        Raylib.DrawText("[GRANT ACCESS]", 534, 228, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText("SLOT DATA SLATE", 532, 246, 10, Palette.CrtAmberDimmed);

        bool detainHovered = mouse.IsHit(526, 282, 102, 30);
        Raylib.DrawRectangle(526, 282, 102, 30, detainHovered ? Palette.BrownDwarfGlowRim : Palette.BrownDwarfGlowDeep);
        Raylib.DrawRectangleLines(526, 282, 102, 30, Palette.AlertCrimson);
        Raylib.DrawText("DETAIN VESSEL", 538, 292, 10, Palette.AlertCrimson);

        // Conditional Late-Game Kordani Destroy Ship Override Switch
        bool destroyUnlocked = _gameState.GetRep("KDF-") >= Constants.RepKordaniDestroyUnlock && _gameState.Day >= 14;
        Raylib.DrawRectangle(526, 318, 102, 28, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(526, 318, 102, 28, destroyUnlocked ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            destroyUnlocked ? "DESTROY [ARMED]" : "DESTROY [LOCKED]",
            532, 327, 10, destroyUnlocked ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
    }

    private static void DrawSmallButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string label,
        Color? textOverride = null)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(label, x + 6, y + 3, 10, textOverride ?? Palette.CrtAmberPrimary);
    }

    private void UnloadCachedTextures()
    {
        PixelBuffer.UnloadTexture(ref _cachedShipTex);
        PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
        PixelBuffer.UnloadTexture(ref _cachedLivePortraitTex);
        PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
    }

    public void Unload()
    {
        UnloadCachedTextures();
    }
}
