using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

/// <summary>
/// Dynamic 21:9 Ultrawide Panoramic Walk-to-Work Corridor Strip (1280x548 letterboxed inside 1280x720)
/// transitioning between Morning Quarters and Booth 04 (Desk) at the start of each shift.
/// NOT static: visually changes each day based on GameState variables (Stoker brownout flickering
/// lights, Kordani patrol security markers, Brengop debt notices, Mining Guild dust/grit, Day 3+
/// salvaged Deck 4 conduit runs). Skippable on click/Space/Enter after a brief fade-in.
/// [TODO: Fill in manually - exact 21:9 visual state variations]
/// </summary>
public sealed class CorridorScreen : IScreen
{
    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private float _elapsedSeconds;

    // Organic irregular power-sag state machine when NextDayPowerBrownout is active
    // (mostly dim-steady illumination with unpredictable 0.15s-0.45s flicker bursts spaced randomly 0.8s-1.8s apart)
    private Random _sagRng = new(1504);
    private float _sagNextGapRemaining;
    private float _sagBurstRemaining;
    private float _sagBurstTotalDuration;
    private int _sagBurstIndex;

    internal bool IsPowerSagBurstActive => _sagBurstRemaining > 0f;
    internal float LastSampledSagGapSeconds { get; private set; }
    internal float LastSampledSagBurstDurationSeconds { get; private set; }

    private const float FadeInDurationSeconds = 0.35f;
    private const float TransitionDurationSeconds = 2.6f;

    public CorridorScreen(ScreenManager screenManager, GameState gameState)
    {
        _screenManager = screenManager;
        _gameState = gameState;
    }

    public void OnEnter()
    {
        _elapsedSeconds = 0f;
        int seed = (_gameState.RunSeed != 0 ? _gameState.RunSeed : 2026) ^ (_gameState.Day * 7919);
        _sagRng = new Random(seed);
        _sagBurstIndex = 0;
        _sagBurstRemaining = 0f;
        _sagBurstTotalDuration = 0f;
        _sagNextGapRemaining = SampleRandomRange(0.80f, 1.80f);
        LastSampledSagGapSeconds = _sagNextGapRemaining;
        AudioHooks.Play("corridor_footsteps_loop");
    }

    private float SampleRandomRange(float min, float max) =>
        min + (float)_sagRng.NextDouble() * (max - min);

    public void OnExit() { }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _elapsedSeconds += deltaTime;
        UpdatePowerSagState(deltaTime);

        bool canSkip = _elapsedSeconds >= FadeInDurationSeconds;
        bool skipInput = mouse.LeftPressed ||
                         Raylib.IsKeyPressed(KeyboardKey.Space) ||
                         Raylib.IsKeyPressed(KeyboardKey.Enter);

        if ((canSkip && skipInput) || _elapsedSeconds >= TransitionDurationSeconds)
        {
            AudioHooks.Play("bulkhead_door_lock");
            _screenManager.SwitchTo(_screenManager.NextCorridorDestination);
        }
    }

    private void UpdatePowerSagState(float deltaTime)
    {
        if (!(_gameState.NextDayPowerBrownout || _gameState.StokerOverloadedToday) || deltaTime <= 0f)
        {
            _sagBurstRemaining = 0f;
            return;
        }

        float rem = deltaTime;
        while (rem > 0f)
        {
            if (_sagBurstRemaining > 0f)
            {
                float step = Math.Min(rem, _sagBurstRemaining);
                _sagBurstRemaining -= step;
                rem -= step;
                if (_sagBurstRemaining <= 0.0001f)
                {
                    _sagBurstRemaining = 0f;
                    _sagNextGapRemaining = SampleRandomRange(0.80f, 1.80f);
                    LastSampledSagGapSeconds = _sagNextGapRemaining;
                }
            }
            else
            {
                float step = Math.Min(rem, _sagNextGapRemaining);
                _sagNextGapRemaining -= step;
                rem -= step;
                if (_sagNextGapRemaining <= 0.0001f)
                {
                    _sagBurstIndex++;
                    _sagBurstTotalDuration = SampleRandomRange(0.15f, 0.45f);
                    _sagBurstRemaining = _sagBurstTotalDuration;
                    LastSampledSagBurstDurationSeconds = _sagBurstTotalDuration;
                }
            }
        }
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.OutlineVoid);

        int stripY = (Constants.VirtualHeight - Constants.CorridorHeight) / 2; // y = 86 (exact 21:9 framing)
        int stripH = Constants.CorridorHeight; // 548

        // Dynamic game-state environmental modifiers
        // [TODO: Fill in manually - exact 21:9 visual state variations]
        bool stokerBrownout = _gameState.NextDayPowerBrownout || _gameState.StokerOverloadedToday;
        bool kordaniHeavyPresence = _gameState.SecurityStrikes >= 1 ||
            WorldSim.EvaluateTrafficPowerState("KDF-", _gameState.GetPower("KDF-")) is TrafficPowerState.Dominant or TrafficPowerState.Crisis;
        bool brengopDebtNotice = _gameState.MissedBerthLeaseCount >= 1 ||
            WorldSim.EvaluateTrafficPowerState("BMC-", _gameState.GetPower("BMC-")) is TrafficPowerState.Dominant or TrafficPowerState.Crisis;
        bool mngGrit = WorldSim.EvaluateTrafficPowerState("MNG-", _gameState.GetPower("MNG-")) is TrafficPowerState.Dominant or TrafficPowerState.Crisis;
        bool salvagedCoilInstalled = _gameState.Day >= Constants.DefaultDayUnlockWarpScanner;

        // Organic irregular power-sag dips when NextDayPowerBrownout is active:
        // mostly dim-steady corridor illumination with unpredictable 0.15s-0.45s flicker bursts spaced 0.8s-1.8s apart.
        bool lightsFlickerOff = false;
        if (stokerBrownout && _sagBurstRemaining > 0f && _sagBurstTotalDuration > 0.001f)
        {
            float burstFrac = 1.0f - (_sagBurstRemaining / _sagBurstTotalDuration);
            // Irregular twin-dip sag envelope inside the 0.15s-0.45s burst (dips out, catches dimly, dips out again)
            lightsFlickerOff = (_sagBurstIndex % 2 == 0)
                ? (burstFrac < 0.42f || burstFrac > 0.64f)
                : (burstFrac < 0.58f || burstFrac > 0.78f);
        }

        // Subtle walking parallax & footstep bob
        float walkProgress = Math.Clamp(_elapsedSeconds / TransitionDurationSeconds, 0f, 1f);
        int scrollShift = (int)MathF.Round(walkProgress * 96f);
        int stepBobY = (int)MathF.Round(MathF.Sin(_elapsedSeconds * 9.5f) * 2.0f);

        // Clip strictly to the 21:9 letterbox strip (0, 86, 1280, 548)
        Raylib.BeginScissorMode(0, stripY, Constants.CorridorWidth, stripH);

        // Backdrop (dim-steady baseline during brownout; deeper sag during irregular flicker bursts)
        Color wallBase = lightsFlickerOff
            ? new Color(13, 15, 19, 255)
            : (stokerBrownout ? new Color(21, 26, 33, 255) : new Color(28, 34, 42, 255));
        AssetManager.DrawImageOrPlaceholder(
            "locations/corridor_strip.png",
            0, stripY, Constants.CorridorWidth, stripH,
            wallBase,
            Palette.OxidizedBronzeWorn,
            string.Empty);

        // Ceiling conduit bay + industrial floor decking
        int ceilH = 84;
        int floorY = stripY + stripH - 128;
        Color ceilCol = lightsFlickerOff
            ? new Color(10, 12, 16, 255)
            : (stokerBrownout ? new Color(16, 19, 24, 255) : new Color(20, 24, 30, 255));
        Color floorCol = lightsFlickerOff
            ? new Color(11, 13, 17, 255)
            : (stokerBrownout ? new Color(17, 21, 26, 255) : new Color(22, 26, 32, 255));
        Raylib.DrawRectangle(0, stripY, Constants.CorridorWidth, ceilH, ceilCol);
        Raylib.DrawLine(0, stripY + ceilH, Constants.CorridorWidth, stripY + ceilH, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(0, floorY, Constants.CorridorWidth, 128, floorCol);
        Raylib.DrawLine(0, floorY, Constants.CorridorWidth, floorY, Palette.OxidizedBronzeShadow);

        // Horizontal floor grate stripes
        for (int fy = floorY + 12; fy < stripY + stripH; fy += 14)
        {
            Raylib.DrawLine(0, fy, Constants.CorridorWidth, fy, new Color(16, 20, 26, 255));
        }

        // Day 3+: Salvaged Deck 4 heavy amber/copper conduit pipes strapped along upper corridor wall
        if (salvagedCoilInstalled)
        {
            int pipeY = stripY + ceilH + 14 + stepBobY;
            Raylib.DrawRectangle(0, pipeY, Constants.CorridorWidth, 8, Palette.OxidizedBronzeShadow);
            Raylib.DrawLine(0, pipeY, Constants.CorridorWidth, pipeY, Palette.OxidizedBronzeWorn);
            Raylib.DrawLine(0, pipeY + 8, Constants.CorridorWidth, pipeY + 8, Palette.OutlineVoid);
        }

        // Repeating corridor bays: stanchions, Brown Dwarf portholes, overhead amber strip lights, dynamic props
        for (int i = -1; i < 11; i++)
        {
            int bx = 32 + i * 136 - (scrollShift % 136);
            int bayTop = stripY + ceilH + stepBobY;
            int bayH = floorY - bayTop;

            // Vertical steel bulkhead rib
            Color ribCol = lightsFlickerOff ? new Color(22, 27, 34, 255) : Palette.WeatheredGunmetalEdge;
            Raylib.DrawRectangle(bx, bayTop, 22, bayH, ribCol);
            Raylib.DrawRectangleLines(bx, bayTop, 22, bayH, Palette.OutlineVoid);

            // Overhead corridor light strip (flickers during Stoker brownout!)
            Color lampCol = lightsFlickerOff
                ? new Color(40, 26, 12, 255)
                : (stokerBrownout ? Palette.CrtAmberDimmed : Palette.CrtAmberPrimary);
            Raylib.DrawRectangle(bx + 34, stripY + ceilH - 12, 80, 6, lampCol);
            if (!lightsFlickerOff)
            {
                // Subtle downward pixel light cone
                Raylib.DrawRectangle(bx + 42, stripY + ceilH + 1, 64, 34, new Color(255, 184, 77, 16));
            }

            // Brown Dwarf external observation porthole
            int portX = bx + 36;
            int portY = bayTop + 42;
            int portW = 76;
            int portH = 148;
            Raylib.DrawRectangle(portX, portY, portW, portH, Palette.DeepSpaceVoid);
            // Warm rust-red Brown Dwarf horizon bleed through porthole (shifts subtly with Day)
            int glowH = 68 + ((_gameState.Day * 7) % 32);
            Raylib.DrawRectangle(portX + 2, portY + portH - glowH, portW - 4, glowH - 2, Palette.BrownDwarfGlowDeep);
            Raylib.DrawLine(portX + 2, portY + portH - glowH, portX + portW - 3, portY + portH - glowH, Palette.BrownDwarfGlowRim);
            // Faint deterministic stars inside porthole
            Raylib.DrawPixel(portX + 18, portY + 24, new Color(200, 210, 224, 255));
            Raylib.DrawPixel(portX + 52, portY + 38, new Color(255, 210, 168, 255));
            Raylib.DrawRectangleLines(portX, portY, portW, portH, Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangleLines(portX - 2, portY - 2, portW + 4, portH + 4, Palette.OutlineVoid);

            // Dynamic State Variation 1: Kordani security wall cameras & patrol notices
            if (kordaniHeavyPresence && (i % 2 == 0))
            {
                int camX = bx + 4;
                int camY = bayTop + 14;
                Raylib.DrawRectangle(camX, camY, 14, 8, Palette.OutlineVoid);
                bool camBlink = ((int)(_elapsedSeconds * 4f) + i) % 2 == 0;
                Raylib.DrawRectangle(camX + 9, camY + 2, 3, 3, camBlink ? Palette.AlertCrimson : Palette.BrownDwarfGlowRim);
            }

            // Dynamic State Variation 2: Brengop corporate debt / quota yellow posters on lower stanchion
            if (brengopDebtNotice && (i % 3 == 1))
            {
                int postX = bx + 38;
                int postY = bayTop + 206;
                Raylib.DrawRectangle(postX, postY, 72, 48, new Color(58, 52, 18, 255));
                Raylib.DrawRectangleLines(postX, postY, 72, 48, Palette.FromHex("#bdb040"));
                UiText.Draw("BMC NOTICE", postX + 6, postY + 8, 10, Palette.FromHex("#bdb040"));
                UiText.Draw("LEASE DUE", postX + 8, postY + 24, 10, Palette.AlertCrimson);
            }

            // Dynamic State Variation 3: Mining Guild hazard stripe / ore grit along base skirting
            if (mngGrit)
            {
                for (int hx = bx + 24; hx < bx + 132; hx += 16)
                {
                    Raylib.DrawRectangle(hx, floorY - 8, 8, 6, Palette.FromHex("#a67b11"));
                }
            }
        }

        // Centered Transit Status Banner inside bottom of 21:9 strip
        const int boxW = 540;
        const int boxH = 74;
        int boxX = (Constants.VirtualWidth - boxW) / 2;
        int boxY = stripY + stripH - 104;
        const int barMaxW = 500;
        int barW = (int)(barMaxW * walkProgress);

        Raylib.DrawRectangle(boxX, boxY, boxW, boxH, new Color(8, 11, 16, 236));
        Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, stokerBrownout ? Palette.AlertCrimson : Palette.CrtAmberDimmed);

        string destLabel = _screenManager.NextCorridorDestination == ScreenId.Desk
            ? $"WALK TO WORK // BERTH 04 -> CUSTOMS BOOTH 04 (DAY {_gameState.Day:D2})"
            : $"SECTOR CORRIDOR // -> {_screenManager.NextCorridorDestination.ToString().ToUpperInvariant()}";

        int labelW = UiText.MeasureWidth(destLabel, 10);
        UiText.Draw(destLabel, boxX + (boxW - labelW) / 2, boxY + 10, 10, Palette.CrtAmberPrimary);

        string statusSub = stokerBrownout
            ? "ALERT: STELLAR STOKER GRID UNSTABLE -- CORRIDOR LIGHTS FLICKERING"
            : (kordaniHeavyPresence
                ? "SECURITY NOTICE: KORDANI GARRISON PATROLS ACTIVE ON RING B"
                : (brengopDebtNotice
                    ? "BRENGOP NOTICE: HABITAT LEASE AUDIT IN EFFECT"
                    : "VOID GATE 3 // HABITAT RING B ACCESS CORRIDOR"));
        int subW = UiText.MeasureWidth(statusSub, 10);
        UiText.Draw(statusSub, boxX + (boxW - subW) / 2, boxY + 26, 10,
            stokerBrownout ? Palette.AlertCrimson : Palette.ClearanceTeal);

        Raylib.DrawRectangle(boxX + 20, boxY + 46, barMaxW, 14, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(boxX + 20, boxY + 46, barW, 14, stokerBrownout ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(boxX + 20, boxY + 46, barMaxW, 14, Palette.CrtAmberDimmed);

        // Brief fade-in veil over the 21:9 strip
        if (_elapsedSeconds < FadeInDurationSeconds)
        {
            float alpha = 1.0f - (_elapsedSeconds / FadeInDurationSeconds);
            Raylib.DrawRectangle(0, stripY, Constants.CorridorWidth, stripH, new Color((byte)8, (byte)11, (byte)16, (byte)(alpha * 255f)));
        }

        Raylib.EndScissorMode();

        // Crisp 21:9 Letterbox Border Lines & Footer Text
        Raylib.DrawLine(0, stripY, Constants.VirtualWidth, stripY, Palette.OxidizedBronzeWorn);
        Raylib.DrawLine(0, stripY + stripH, Constants.VirtualWidth, stripY + stripH, Palette.OxidizedBronzeWorn);

        string topText = $"DAY {_gameState.Day:D2} // MORNING TRANSIT  |  SCRIP: {_gameState.PactScrip} SC  |  POCKET: {_gameState.PocketItems.Count}/{_gameState.PocketCapacity}";
        UiText.Draw(topText, 16, 36, 10, Palette.CrtAmberDimmed);

        string footer = _elapsedSeconds >= FadeInDurationSeconds
            ? "CLICK OR PRESS [SPACE] TO ENTER BOOTH 04  |  [ESC] PAUSE"
            : "TRANSITING HABITAT RING B...";
        int footerW = UiText.MeasureWidth(footer, 10);
        UiText.Draw(footer, (Constants.VirtualWidth - footerW) / 2, Constants.VirtualHeight - 44, 10, Palette.CrtAmberDimmed);
    }

    public void Unload() { }
}
