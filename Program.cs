using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;
using SubspaceProtocols.UI;

namespace SubspaceProtocols;

public static class Program
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            return RunApplication(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            return 1;
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog(ex);
        }
        else
        {
            WriteCrashLog(new Exception($"Unhandled non-Exception object: {e.ExceptionObject}"));
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            string crashPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
            string report =
                $"[{DateTime.UtcNow:O}] UNHANDLED CRASH IN SUBSPACE PROTOCOLS{Environment.NewLine}" +
                $"BaseDirectory: {AppContext.BaseDirectory}{Environment.NewLine}" +
                $"Exception: {ex}{Environment.NewLine}" +
                $"--------------------------------------------------------------------------------{Environment.NewLine}";
            File.WriteAllText(crashPath, report);
            Console.Error.WriteLine(report);
        }
        catch
        {
            // Swallow secondary logging failures
        }
    }

    private static int RunApplication(string[] args)
    {
        bool isSmokeTest = args.Any(a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
        bool isArtTestFlag = args.Any(a => a.Equals("--art-test", StringComparison.OrdinalIgnoreCase));
        string? dumpFramesDir = args
            .FirstOrDefault(a => a.StartsWith("--dump-frames=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("--dump-frames=".Length);
        if (!string.IsNullOrWhiteSpace(dumpFramesDir))
        {
            Directory.CreateDirectory(dumpFramesDir);
        }

        Core.ConfigFlags config = Core.ConfigFlags.Load();
        GameState gameState = new() { SuppressDiskPersistence = isSmokeTest };
        DevDebugState devDebug = new();

        // Resizable desktop window initialized at 1280x720 (exact 16:9, zero MSAA)
        Raylib.SetConfigFlags(Raylib_cs.ConfigFlags.ResizableWindow | Raylib_cs.ConfigFlags.VSyncHint);
        Raylib.InitWindow(
            Constants.VirtualWidth,
            Constants.VirtualHeight,
            "Subspace Protocols -- Void Gate 3 (Booth 04)");
        Raylib.SetExitKey(KeyboardKey.Null); // ESC toggles the CRT Pause Menu, never closes the window
        Raylib.SetWindowMinSize(640, 360);
        Raylib.SetTargetFPS(60);

        // Ensure default Raylib font uses crisp point filtering (zero font smoothing)
        Font defaultFont = Raylib.GetFontDefault();
        if (defaultFont.Texture.Id != 0)
        {
            Raylib.SetTextureFilter(defaultFont.Texture, TextureFilter.Point);
        }

        using VirtualCanvas virtualCanvas = new();
        virtualCanvas.ApplyOptimalMonitorScaling(allowBorderlessFullscreen: !isSmokeTest);

        ScreenManager screenManager = new();
        TutorialOverlay tutorialOverlay = new(gameState);
        PauseMenuOverlay pauseMenu = new(screenManager, gameState, virtualCanvas, tutorialOverlay.OpenCurrentDaySummary);
        DebugOverlay debugOverlay = new(screenManager, gameState, config, devDebug, tutorialOverlay.OpenCurrentDaySummary);

        screenManager.RegisterScreen(ScreenId.MainMenu, new MainMenuScreen(screenManager, gameState, config));
        screenManager.RegisterScreen(ScreenId.Corridor, new CorridorScreen(screenManager, gameState));
        DeskScreen deskScreen = new(
            screenManager,
            gameState,
            config,
            devDebug,
            pauseMenu.Toggle,
            day => tutorialOverlay.TriggerDayStartHook(day));
        screenManager.RegisterScreen(ScreenId.Desk, deskScreen);
        screenManager.RegisterScreen(ScreenId.Quarters, new QuartersScreen(screenManager, gameState));
        screenManager.RegisterScreen(ScreenId.ArtTest, new ArtTestScreen(screenManager));

        screenManager.SwitchTo(isArtTestFlag ? ScreenId.ArtTest : ScreenId.MainMenu);

        int smokeFrameCounter = 0;
        ScreenId[] smokeRouteOrder =
        [
            ScreenId.MainMenu,
            ScreenId.Corridor,
            ScreenId.Desk,
            ScreenId.Quarters,
            ScreenId.ArtTest
        ];

        while (!Raylib.WindowShouldClose() && !pauseMenu.ShouldQuitApplication)
        {
            float dt = Raylib.GetFrameTime();

            virtualCanvas.HandleFullscreenShortcut();
            virtualCanvas.UpdateViewport();

            VirtualMouseState mouse = InputMapper.SampleMouse(virtualCanvas.CurrentViewport);

            if (screenManager.CurrentScreenId == ScreenId.MainMenu && tutorialOverlay.IsOpen)
            {
                tutorialOverlay.ForceCloseSilent();
            }

            // If Tutorial Card is open, ESC dismisses the tutorial card cleanly instead of stacking Pause on top.
            bool tutorialConsumed = !pauseMenu.IsOpen && tutorialOverlay.HandleInput(mouse);
            bool paused = !tutorialConsumed && pauseMenu.HandleInput(mouse);
            bool debugConsumed = !paused && !tutorialConsumed && debugOverlay.HandleInput(mouse);

            VirtualMouseState screenMouse = (paused || tutorialConsumed || debugConsumed)
                ? mouse with { IsInsideViewport = false, LeftPressed = false, LeftDown = false, LeftReleased = false, RightPressed = false, WheelMove = 0f }
                : mouse;

            // Freeze the 270-second shift timer and all active animations while the ESC Pause Menu OR Tutorial Card is open
            if (!paused && !tutorialConsumed)
            {
                screenManager.Update(dt, screenMouse);
            }

            virtualCanvas.BeginVirtualDraw();
            screenManager.DrawVirtual(screenMouse);
            tutorialOverlay.DrawVirtual(paused ? screenMouse : mouse);
            debugOverlay.DrawVirtual(paused || tutorialConsumed ? screenMouse : mouse);
            pauseMenu.DrawVirtual(mouse);
            virtualCanvas.EndVirtualDrawAndBlit();

            if (isSmokeTest)
            {
                if (smokeFrameCounter == 0 && !isArtTestFlag)
                {
                    RunProceduralEngineVerificationSuite();
                    RunWorkShiftAndBranchingVerificationSuite();

                    // Run interactive DeskScreen Prompt 3.7.1 checks on Day 2 and Day 3 even without --dump-frames
                    gameState.ResetForNewSave(1);
                    gameState.AdvanceToDay(2);
                    screenManager.SwitchTo(ScreenId.Desk);
                    tutorialOverlay.ForceCloseSilent();
                    deskScreen.SmokeVerifyPrompt371Interactions();

                    gameState.Day3PreShiftHailCompleted = true;
                    gameState.AdvanceToDay(3);
                    screenManager.SwitchTo(ScreenId.Desk);
                    tutorialOverlay.ForceCloseSilent();
                    deskScreen.SmokeVerifyPrompt371Interactions();

                    if (!string.IsNullOrWhiteSpace(dumpFramesDir))
                    {
                        CaptureVisualVerificationFrames(
                            virtualCanvas,
                            screenManager,
                            deskScreen,
                            tutorialOverlay,
                            pauseMenu,
                            debugOverlay,
                            gameState,
                            dumpFramesDir);
                    }
                }

                if (!isArtTestFlag && smokeFrameCounter < smokeRouteOrder.Length)
                {
                    tutorialOverlay.ForceCloseSilent();
                    screenManager.SwitchTo(smokeRouteOrder[smokeFrameCounter]);
                    tutorialOverlay.ForceCloseSilent();
                }
                smokeFrameCounter++;
                if (smokeFrameCounter > smokeRouteOrder.Length + 2)
                {
                    break;
                }
            }
        }

        // Strict VRAM & resource cleanup
        screenManager.UnloadAllScreens();
        AssetManager.UnloadAll();
        virtualCanvas.Dispose();
        Raylib.CloseWindow();

        if (isSmokeTest && PixelBuffer.ActiveProceduralTextureCount != 0)
        {
            throw new InvalidOperationException(
                $"VRAM texture leak detected: {PixelBuffer.ActiveProceduralTextureCount} procedural textures still active.");
        }

        return 0;
    }

    private static void RunProceduralEngineVerificationSuite()
    {
        // 0. Verify 1280x720 virtual framebuffer & aspect-ratio mouse projection (including 1080p 1.5x & letterboxed resize)
        if (Constants.VirtualWidth != 1280 || Constants.VirtualHeight != 720)
            throw new InvalidOperationException("Virtual framebuffer must be 1280x720.");

        ViewportTransform vp1080 = InputMapper.ComputeViewport(1920, 1080);
        if (vp1080.ScaledWidth != 1920 || vp1080.ScaledHeight != 1080 || vp1080.OffsetX != 0 || vp1080.OffsetY != 0)
            throw new InvalidOperationException("1080p viewport scaling mismatch.");

        var (vxMid, vyMid, inMid) = InputMapper.ScreenToVirtual(960, 540, vp1080);
        if (!inMid || vxMid != 640 || vyMid != 360)
            throw new InvalidOperationException($"1080p mouse center projection failed: ({vxMid},{vyMid})");

        ViewportTransform vpUltra = InputMapper.ComputeViewport(2560, 1080);
        if (vpUltra.ScaledWidth != 1920 || vpUltra.ScaledHeight != 1080 || vpUltra.OffsetX != 320 || vpUltra.OffsetY != 0)
            throw new InvalidOperationException("Ultrawide aspect-ratio letterboxing mismatch.");

        // 1. Verify DeterministicRng FNV-1a & Mulberry32 repeatability
        DeterministicRng rngA = new("capt-vek-409");
        DeterministicRng rngB = new("capt-vek-409");
        if (rngA.InitialHash != rngB.InitialHash || MathF.Abs(rngA.NextFloat() - rngB.NextFloat()) > 1e-7f)
            throw new InvalidOperationException("DeterministicRng failed repeatability check.");

        // 2. Verify all 13 active species & 4 portrait render modes + GPU texture lifecycle
        PortraitRenderMode[] modes =
        [
            PortraitRenderMode.Live,
            PortraitRenderMode.Static,
            PortraitRenderMode.Holo,
            PortraitRenderMode.CrudeCrayon
        ];

        foreach (PortraitSpecies species in PortraitRenderer.AllActiveSpecies)
        {
            foreach (PortraitRenderMode mode in modes)
            {
                PixelBuffer pBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
                {
                    SeedId = $"verify-{species}-{mode}",
                    Species = species,
                    Mode = mode,
                    IsMolting = species == PortraitSpecies.Tolvak,
                    DoppleInTrueForm = true
                });
                Texture2D tex = pBuf.ToTexture2D();
                PixelBuffer.UnloadTexture(ref tex);
            }

            PixelBuffer holoA = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
            {
                SeedId = $"cmp-{species}-seedA",
                Species = species,
                Mode = PortraitRenderMode.Holo,
                DoppleInTrueForm = true
            });
            PixelBuffer holoB = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
            {
                SeedId = $"cmp-{species}-seedB7",
                Species = species,
                Mode = PortraitRenderMode.Holo,
                DoppleInTrueForm = true
            });
            int diffPixels = CountDifferingPixels(holoA, holoB);
            if (diffPixels < 25)
                throw new InvalidOperationException($"Within-species HOLO variation too low for {species}: {diffPixels}");
        }

        // 3. Verify GU decommissioned rule & all 11 ship factions
        if (ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Military, true, false))
            throw new InvalidOperationException("GU Military ship illegally allowed decommissioned weathering.");
        if (!ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Civilian, true, false) ||
            !ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Cargo, true, false))
            throw new InvalidOperationException("GU Civilian/Cargo decommissioned weathering check failed.");

        foreach (ShipFaction faction in ShipRenderer.AllFactions)
        {
            foreach (ShipClass allowedClass in ShipRenderer.GetAllowedClasses(faction))
            {
                foreach (bool sps in new[] { false, true })
                {
                    PixelBuffer sBuf = ShipRenderer.RenderBuffer(new ShipRenderOptions
                    {
                        SeedId = $"verify-{faction}-{allowedClass}",
                        Faction = faction,
                        RequestedClass = allowedClass,
                        Mode = ShipRenderMode.Live,
                        SpsEnabled = sps,
                        ShieldsUp = false,
                        WeaponsHot = true,
                        IsDecommissioned = true
                    }, out ShipHardpointLayout hp);

                    if (hp.ForwardWeapons.X is < 0 or >= Constants.ShipWidth ||
                        hp.ForwardWeapons.Y is < 0 or >= Constants.ShipHeight ||
                        hp.CargoBay.X is < 0 or >= Constants.ShipWidth ||
                        hp.CargoBay.Y is < 0 or >= Constants.ShipHeight)
                    {
                        throw new InvalidOperationException($"Out-of-bounds hardpoint metadata for {faction} {allowedClass}.");
                    }

                    if (sBuf.HasPixel(0, 0) || sBuf.HasPixel(Constants.ShipWidth - 1, 0))
                        throw new InvalidOperationException($"Expected transparent background on ship buffer for {faction} {allowedClass}.");

                    Texture2D tex = sBuf.ToTexture2D();
                    PixelBuffer.UnloadTexture(ref tex);
                }

                PixelBuffer manualBuf = ShipRenderer.RenderBuffer(new ShipRenderOptions
                {
                    SeedId = $"verify-{faction}-{allowedClass}",
                    Faction = faction,
                    RequestedClass = allowedClass,
                    Mode = ShipRenderMode.Manual
                });
                Texture2D mTex = manualBuf.ToTexture2D();
                PixelBuffer.UnloadTexture(ref mTex);
            }
        }

        // 4. Verify Dedicated Hardcoded Day 3 Kordani Security Officer portrait (identical across seeds, zero RNG)
        PixelBuffer kdfOfficerA = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
        {
            SeedId = "seed-one",
            Species = PortraitSpecies.Kordani,
            Mode = PortraitRenderMode.Live,
            FactionPrefix = "KDF-",
            IsKordaniSecurityOfficer = true
        });
        PixelBuffer kdfOfficerB = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
        {
            SeedId = "seed-completely-different",
            Species = PortraitSpecies.Kordani,
            Mode = PortraitRenderMode.Live,
            FactionPrefix = "KDF-",
            IsKordaniSecurityOfficer = true
        });
        if (CountDifferingPixels(kdfOfficerA, kdfOfficerB) != 0)
            throw new InvalidOperationException("Hardcoded Kordani Security Officer portrait must have zero RNG variation.");

        Texture2D kdfTex = kdfOfficerA.ToTexture2D();
        PixelBuffer.UnloadTexture(ref kdfTex);
    }

    private static void RunWorkShiftAndBranchingVerificationSuite()
    {
        if (Constants.BasePayPerShipScrip != 10)
            throw new InvalidOperationException($"Expected default BasePayPerShipScrip == 10, found {Constants.BasePayPerShipScrip}.");

        // Verify DevDebugState live tuners (Pay Per Ship + Animation Speed Multiplier)
        DevDebugState dbg = new();
        if (dbg.PayPerShipScrip != 10 || MathF.Abs(dbg.AnimationSpeedMultiplier - 1.0f) > 0.001f)
            throw new InvalidOperationException("DevDebugState default pay (10 SC) or anim speed (1.0x) mismatch.");
        dbg.StepPayPerShip(1);
        dbg.StepAnimationSpeed(2);
        if (dbg.PayPerShipScrip != 15 || MathF.Abs(dbg.AnimationSpeedMultiplier - 1.5f) > 0.001f)
            throw new InvalidOperationException("DevDebugState pay/speed steppers failed.");

        GameState simState = new() { SuppressDiskPersistence = true };
        ShipGenerator generator = new();
        ValidationEngine validator = new();
        WorldSim worldSim = new();
        ShiftController shift = new();

        simState.ResetForNewSave(1);
        int runSeedA = simState.RunSeed;
        if (simState.Day != 1 || simState.PactScrip != 45 || simState.BasePayPerShipScrip != 10 || runSeedA <= 0)
            throw new InvalidOperationException("ResetForNewSave(1) failed strict Day 1 initialization check.");

        // Pre-generated 16 queue determinism + random overflow beyond 16
        shift.StartNewShift(simState);
        IReadOnlyList<ShipSpec> queueA1 = shift.PrepareDailyQueue(simState, generator);
        ShiftController shiftReload = new();
        shiftReload.StartNewShift(simState);
        IReadOnlyList<ShipSpec> queueA2 = shiftReload.PrepareDailyQueue(simState, generator);
        if (queueA1.Count != 16 || queueA2.Count != 16)
            throw new InvalidOperationException($"Expected 16 pre-generated daily ships, got {queueA1.Count}.");

        for (int i = 0; i < 16; i++)
        {
            if (queueA1[i].Slate.RegistrationCode != queueA2[i].Slate.RegistrationCode)
                throw new InvalidOperationException("Pre-generated 16-vessel queue was not deterministic for same (RunSeed, Day).");
        }

        // Pull all 16 + 3 overflow ships
        for (int i = 0; i < 19; i++)
        {
            ShipSpec s = shift.TakeNextShip(simState, generator);
            if (string.IsNullOrWhiteSpace(s.Slate.RegistrationCode))
                throw new InvalidOperationException("Overflow ship missing registration code.");
        }
        if (!shift.IsInRandomOverflow)
            throw new InvalidOperationException("ShiftController did not enter random overflow after 16 ships.");

        // 1. Verify Day 1 Simplified Single Rule (ONLY TransponderIdMismatch, NO shields/weapons, NO typos)
        for (int i = 0; i < 60; i++)
        {
            ShipSpec day1Ship = generator.GenerateShipForSlot(simState, i);
            if (day1Ship.GroundTruth.ActualShieldsUp || day1Ship.GroundTruth.ActualWeaponsHot)
                throw new InvalidOperationException("Day 1 ship must spawn with shields down & weapons cold.");

            if (!day1Ship.IsValid)
            {
                if (day1Ship.GroundTruth.ActiveInfractions.Count != 1 ||
                    day1Ship.GroundTruth.ActiveInfractions[0].Type != ShipInfractionType.TransponderIdMismatch)
                {
                    throw new InvalidOperationException(
                        $"Day 1 generated disallowed infraction: {day1Ship.GroundTruth.ActiveInfractions[0].Type}");
                }
                if (string.Equals(day1Ship.GroundTruth.RawTransponderId, day1Ship.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Day 1 TransponderIdMismatch had matching IDs.");
            }
        }

        // Day 1 Detain on invalid ship succeeds WITHOUT requiring Protocol Review (+10 SC)
        {
            GameState d1State = new() { Day = 1, PactScrip = 45, SuppressDiskPersistence = true };
            ShipSpec d1Inv = generator.GenerateShipForSlot(d1State, 0, forceValidity: DebugSpawnValidityMode.ForceInvalid);
            d1Inv.ProtocolReviewVerified = false;
            DecisionEvaluationResult d1Detain = validator.EvaluateDecision(d1State, d1Inv, PlayerDecision.Detain, worldSim);
            if (!d1Detain.IsAccurate || d1Detain.IsCitation || d1Detain.ScripDelta != 10)
                throw new InvalidOperationException("Day 1 Detain on invalid ship should succeed (+10 SC) without Protocol Review.");
        }

        // 2. Verify Day 2 Escalation (ONLY TransponderIdMismatch + SpoofedWeaponsHot, plus Ghost Route slot 1)
        GameState d2State = new() { Day = 2, PactScrip = 45, SuppressDiskPersistence = true };
        HashSet<ShipInfractionType> seenD2 = [];
        for (int i = 0; i < 60; i++)
        {
            ShipSpec inv2 = generator.GenerateShipForSlot(d2State, i, reseedNonce: 4u, DebugSpawnValidityMode.ForceInvalid);
            ShipInfractionType t = inv2.GroundTruth.ActiveInfractions[0].Type;
            seenD2.Add(t);
            if (t is not (ShipInfractionType.TransponderIdMismatch or ShipInfractionType.SpoofedWeaponsHot))
                throw new InvalidOperationException($"Day 2 generated disallowed infraction: {t}");
        }
        if (!seenD2.Contains(ShipInfractionType.TransponderIdMismatch) || !seenD2.Contains(ShipInfractionType.SpoofedWeaponsHot))
            throw new InvalidOperationException("Day 2 must generate both TransponderIdMismatch and SpoofedWeaponsHot.");

        ShipSpec ghostSuspect = generator.GenerateShipForSlot(d2State, ShipGenerator.Day2GhostRouteQueueSlot);
        if (!ghostSuspect.GroundTruth.IsDay2GhostRouteSuspect)
            throw new InvalidOperationException("Day 2 Ghost Route suspect must spawn on slot 1.");

        // 3. Verify Day 3 Escalation (Protocol Review required, Security Strikes active, full pool without typos)
        GameState d3State = new() { Day = 3, PactScrip = 45, SuppressDiskPersistence = true };
        ShipSpec d3Valid = generator.GenerateShipForSlot(d3State, 0, forceValidity: DebugSpawnValidityMode.ForceValid);
        d3Valid.GroundTruth.ActualShieldsUp = false;
        d3Valid.GroundTruth.ActualWeaponsHot = false;
        ShipSpec d3Invalid = generator.GenerateShipForSlot(d3State, 1, forceValidity: DebugSpawnValidityMode.ForceInvalid);

        // Case 1: Valid + Grant Access -> +10 SC
        DecisionEvaluationResult c1 = validator.EvaluateDecision(d3State, d3Valid, PlayerDecision.GrantAccess, worldSim);
        if (!c1.IsAccurate || c1.IsCitation || c1.ScripDelta != 10)
            throw new InvalidOperationException("Day 3 Case 1 (Valid + GrantAccess) failed.");

        // Case 4: Day 3 Invalid + Detain + ProtocolReviewVerified == false -> Citation!
        d3Invalid.ProtocolReviewVerified = false;
        DecisionEvaluationResult c4 = validator.EvaluateDecision(d3State, d3Invalid, PlayerDecision.Detain, worldSim);
        if (c4.IsAccurate || !c4.IsCitation || c4.ViolationReason != "UNVERIFIED DETAINMENT -- PROTOCOL REVIEW NOT COMPLETED")
            throw new InvalidOperationException("Day 3 Case 4 (Unverified Detain) failed.");

        // Case 5: Day 3 Invalid + Detain + ProtocolReviewVerified == true -> Accurate (+10 SC)
        d3Invalid.ProtocolReviewVerified = true;
        DecisionEvaluationResult c5 = validator.EvaluateDecision(d3State, d3Invalid, PlayerDecision.Detain, worldSim);
        if (!c5.IsAccurate || c5.IsCitation || c5.ScripDelta != 10)
            throw new InvalidOperationException("Day 3 Case 5 (Verified Detain) failed.");

        // Verify live Pay Per Ship tuner changes payout immediately
        d3State.BasePayPerShipScrip = 15;
        DecisionEvaluationResult cPay15 = validator.EvaluateDecision(d3State, d3Valid, PlayerDecision.GrantAccess, worldSim);
        if (!cPay15.IsAccurate || cPay15.ScripDelta != 15)
            throw new InvalidOperationException("Live BasePayPerShipScrip tuner (15 SC) did not affect ValidationEngine payout.");

        // Verify Day 3 Live Kordani Hail lines for BOTH Branch A and Branch B
        string[] branchA = Day2And3ScriptedDialogue.BuildDay3KordaniHailLines(true, "ORN-442-K", "Oruun-4");
        string[] branchB = Day2And3ScriptedDialogue.BuildDay3KordaniHailLines(false, "ORN-442-K", "Oruun-4");
        if (branchA.Length < 3 || branchB.Length < 3)
            throw new InvalidOperationException("Day 3 Kordani Live Hail lines missing for Branch A or Branch B.");

        // Verify Hold Timer Until First Hail & 270s shift_horn trigger
        ShiftController timerShift = new();
        timerShift.StartNewShift(simState);
        timerShift.Tick(30.0f);
        if (timerShift.ElapsedShiftSeconds > 0.001f)
            throw new InvalidOperationException("ShiftController must hold timer at t = 0 until first Next Hail.");
        timerShift.NotifyFirstHailTriggered();
        timerShift.Tick(270.0f);
        if (!timerShift.IsShiftTimerExpired || !string.Equals(AudioHooks.LastPlayedSoundId, "shift_horn", StringComparison.Ordinal))
            throw new InvalidOperationException("ShiftController failed to fire 'shift_horn' at t = 270s.");

        // Verify Prompt 3.7: Unified transfer duration constant
        if (MathF.Abs(ShiftController.StandardTransferDurationSeconds - 1.80f) > 0.001f)
            throw new InvalidOperationException("ShiftController.StandardTransferDurationSeconds mismatch.");

        // Verify Prompt 3.7: 6-system smooth harmonic WarpSignatureGenerator
        if (WarpSignatureGenerator.SectorSystems.Length != 6)
            throw new InvalidOperationException($"Expected 6 warp star systems, found {WarpSignatureGenerator.SectorSystems.Length}.");
        if (!WarpSignatureGenerator.IsRestrictedOrigin("The Ashen Rift") || !WarpSignatureGenerator.IsRestrictedOrigin("Ashen Rift"))
            throw new InvalidOperationException("WarpSignatureGenerator failed restricted Ashen Rift normalization.");

        for (int a = 0; a < WarpSignatureGenerator.SectorSystems.Length; a++)
        {
            string keyA = WarpSignatureGenerator.SectorSystems[a].Key;
            float selfDev = WarpSignatureGenerator.ComputeMeanPixelDeviation(keyA, keyA);
            if (selfDev > 0.001f)
                throw new InvalidOperationException($"WarpSignatureGenerator self-deviation must be 0 for {keyA}.");

            for (int b = a + 1; b < WarpSignatureGenerator.SectorSystems.Length; b++)
            {
                string keyB = WarpSignatureGenerator.SectorSystems[b].Key;
                float dev = WarpSignatureGenerator.ComputeMeanPixelDeviation(keyA, keyB);
                if (dev < 3.0f)
                    throw new InvalidOperationException($"Warp waveforms for {keyA} and {keyB} are too similar (mean dev {dev:0.00}px).");
            }
        }

        // Verify Prompt 3.7: TutorialFlags lifecycle & persistence clone
        simState.ResetForNewSave(1);
        if (!simState.TutorialFlags.TutorialsEnabled ||
            simState.TutorialFlags.HasSeen(TutorialEventHook.OnDay1Start) ||
            simState.TutorialFlags.HasSeen(TutorialEventHook.OnDay2Start) ||
            simState.TutorialFlags.HasSeen(TutorialEventHook.OnDay3Start))
        {
            throw new InvalidOperationException("TutorialFlags did not reset cleanly on ResetForNewSave.");
        }
        simState.TutorialFlags.MarkSeen(TutorialEventHook.OnDay1Start);
        GameState clonedState = new() { SuppressDiskPersistence = true };
        clonedState.CopyFrom(simState);
        if (!clonedState.TutorialFlags.HasSeen(TutorialEventHook.OnDay1Start) ||
            clonedState.TutorialFlags.HasSeen(TutorialEventHook.OnDay2Start))
        {
            throw new InvalidOperationException("TutorialFlags did not copy cleanly in GameState.CopyFrom.");
        }

        // Verify Prompt 3.7.1: ZERO UNR- vessels across Days 1-3 (240 generated ships)
        HashSet<string> validPrefixes = new(Constants.AllTrackedPrefixes, StringComparer.OrdinalIgnoreCase);
        for (int day = 1; day <= 3; day++)
        {
            GameState prefixState = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
            for (int slot = 0; slot < 80; slot++)
            {
                DebugSpawnValidityMode mode = (slot % 2 == 0) ? DebugSpawnValidityMode.ForceInvalid : DebugSpawnValidityMode.Auto;
                ShipSpec s = generator.GenerateShipForSlot(prefixState, slot, reseedNonce: 11u, forceValidity: mode);
                if (s.Slate.DeclaredPrefix.Equals("UNR-", StringComparison.OrdinalIgnoreCase) ||
                    s.Slate.RegistrationCode.StartsWith("UNR-", StringComparison.OrdinalIgnoreCase) ||
                    s.GroundTruth.RawTransponderId.StartsWith("UNR-", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Disallowed UNR- vessel generated on Day {day}, slot {slot}: {s.Slate.RegistrationCode}");
                }
                if (!validPrefixes.Contains(s.Slate.DeclaredPrefix))
                {
                    throw new InvalidOperationException($"Unexpected non-standard prefix '{s.Slate.DeclaredPrefix}' on Day {day}.");
                }
            }
        }

        // Verify Prompt 3.7.1: Mid-transfer interrupt window (1%-99% true, 100% false)
        if (!ShiftController.CanInterruptTransfer(0.50f) ||
            !ShiftController.CanInterruptTransfer(ShiftController.StandardTransferDurationSeconds - 0.01f) ||
            ShiftController.CanInterruptTransfer(ShiftController.StandardTransferDurationSeconds))
        {
            throw new InvalidOperationException("ShiftController.CanInterruptTransfer boundary check failed.");
        }

        // Verify Prompt 3.7.1: SPS detects BOTH honest armed ships AND spoofed weapon ships, and cools on compliance
        {
            GameState spsState = new() { Day = 2, PactScrip = 45, SuppressDiskPersistence = true };
            ShipSpec honestArmed = generator.GenerateShipForSlot(spsState, 0, forceValidity: DebugSpawnValidityMode.ForceValid);
            honestArmed.GroundTruth.ActualShieldsUp = false;
            honestArmed.GroundTruth.ActualWeaponsHot = true;
            honestArmed.GroundTruth.HasSpoofedWeapons = false;
            honestArmed.Slate.DeclaredWeaponsStatus = "ARMED";

            if (!ShiftController.ShouldSpsDetectHotWeapons(honestArmed))
                throw new InvalidOperationException("SPS failed to detect hot weapons on an honest armed vessel.");

            ComplianceOrderOutcome honestOutcome = ShiftController.ResolveWeaponsOrder(spsState, honestArmed);
            if (honestOutcome != ComplianceOrderOutcome.Complied ||
                honestArmed.GroundTruth.ActualWeaponsHot ||
                ShiftController.ShouldSpsDetectHotWeapons(honestArmed))
            {
                throw new InvalidOperationException("Honest armed vessel failed to power down & cool SPS weapons on PWR DOWN WPN.");
            }

            ShipSpec spoofedLiars = generator.GenerateShipForSlot(spsState, 1, forceValidity: DebugSpawnValidityMode.ForceInvalid);
            spoofedLiars.GroundTruth.ActualShieldsUp = false;
            spoofedLiars.GroundTruth.ActualWeaponsHot = true;
            spoofedLiars.GroundTruth.HasSpoofedWeapons = true;
            spoofedLiars.Slate.DeclaredWeaponsStatus = "OFFLINE";

            if (!ShiftController.ShouldSpsDetectHotWeapons(spoofedLiars))
                throw new InvalidOperationException("SPS failed to detect hot weapons on a spoofed vessel.");

            ComplianceOrderOutcome spoofOutcome = ShiftController.ResolveWeaponsOrder(spsState, spoofedLiars);
            if (spoofOutcome != ComplianceOrderOutcome.SpoofedDeflection ||
                !spoofedLiars.GroundTruth.ActualWeaponsHot ||
                !ShiftController.ShouldSpsDetectHotWeapons(spoofedLiars))
            {
                throw new InvalidOperationException("Spoofed weapon vessel failed to stay hot in SPS after verbal deflection.");
            }
        }
    }

    private static void CaptureVisualVerificationFrames(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        string outDir)
    {
        VirtualMouseState idleMouse = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };

        void RenderAndSave(string filename)
        {
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idleMouse);
            tutorialOverlay.DrawVirtual(idleMouse);
            debugOverlay.DrawVirtual(idleMouse);
            pauseMenu.DrawVirtual(idleMouse);
            canvas.EndVirtualDrawAndBlit();
            canvas.ExportVirtualFramePng(Path.Combine(outDir, filename));
        }

        // 1. Main Menu (1280x720)
        tutorialOverlay.ForceCloseSilent();
        pauseMenu.Close();
        screenManager.SwitchTo(ScreenId.MainMenu);
        RenderAndSave("01_main_menu.png");

        // 2. Day 1 Tutorial Popup (triggered cleanly by OnDay1Start hook)
        gameState.ResetForNewSave(1);
        screenManager.SwitchTo(ScreenId.Desk);
        RenderAndSave("02a_day1_tutorial_popup.png");
        tutorialOverlay.Dismiss(markSeen: true);

        // 3. Day 1 Desk: Exact 8px-gutter grid, [PLAYTEST] inactive labels, inert conduit, visible Wafer Wipe Feed Slot
        deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false);
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("02b_day1_desk_ship_loaded.png");

        // 4. Day 1 Visible Docked 184x216 Data Slate inside Clearance Upload Dock (1080, 384, 184, 216) + encryption bar
        deskScreen.SmokeStageDockedClearanceSlate(encryptionProgress: 0.65f);
        deskScreen.Update(0.02f, idleMouse);
        RenderAndSave("02c_day1_docked_clearance_slate.png");

        // 5. Day 1 Physical Detain Clamps Snapped onto Ship
        deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false, triggerDetainClamps: true);
        deskScreen.Update(0.02f, idleMouse);
        RenderAndSave("03_day1_detain_clamps.png");

        // 6. Day 2 Desk: Honest Armed Ship with Hot Orange SPS Mount Logged + Cooled After PWR DOWN WPN
        gameState.AdvanceToDay(2);
        screenManager.SwitchTo(ScreenId.Desk);
        RenderAndSave("04a_day2_tutorial_popup.png");
        tutorialOverlay.Dismiss(markSeen: true);

        (int wpnX, int wpnY) = deskScreen.SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
        VirtualMouseState spsAimMouse = new() { X = wpnX, Y = wpnY, IsInsideViewport = true, Scale = 1.0f };
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(spsAimMouse);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "04b_day2_sps_honest_armed_hot.png"));

        deskScreen.SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: true);
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(spsAimMouse);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "04c_day2_sps_honest_armed_cooled.png"));

        pauseMenu.Open();
        RenderAndSave("04d_pause_menu_rules_btn.png");
        pauseMenu.Close();

        debugOverlay.IsVisible = true;
        RenderAndSave("05_f1_debug_tuners.png");
        debugOverlay.IsVisible = false;

        // 7. Day 3 Live Kordani Officer Viewscreen Hail
        gameState.Day2SuspectDetained = true;
        gameState.Day3PreShiftHailCompleted = false;
        gameState.AdvanceToDay(3);
        screenManager.SwitchTo(ScreenId.Desk);
        for (int i = 0; i < 6; i++)
        {
            deskScreen.Update(1.0f, idleMouse);
        }
        RenderAndSave("06_day3_kordani_live_hail.png");

        // 8. Day 3 Tightened Manual (Tab 1: 2. HULLS, zero PING RELAY) + Warp Checker RED MISMATCH BLEED
        deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false);
        deskScreen.SmokeSetManualTab(1);
        deskScreen.SmokeStageWarpStripOnChecker(refSystemIndex: 0, forcedActualOrigin: "Ashen Rift"); // Veridia ref vs Ashen Rift strip -> vivid RED bleed!
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("07_day3_manual_hulls_and_warp_mismatch.png");

        // 9. Day 3 Tightened Manual (Tab 0: 1. PREFIXES, zero UNR- & zero PING RELAY) + Warp Checker MATCH
        deskScreen.SmokeSetManualTab(0);
        deskScreen.SmokeStageWarpStripOnChecker(refSystemIndex: 5, forcedActualOrigin: "Ashen Rift"); // Ashen Rift ref vs Ashen Rift strip -> warm amber/white glow!
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("08_day3_manual_prefixes_and_warp_match.png");

        // 10. Day 3 Tutorial Popup
        tutorialOverlay.OnDay3Start(force: true);
        RenderAndSave("09_day3_tutorial_popup.png");
        tutorialOverlay.ForceCloseSilent();
    }

    private static int CountDifferingPixels(PixelBuffer a, PixelBuffer b)
    {
        int diffs = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                if (!string.Equals(a.Get(x, y), b.Get(x, y), StringComparison.OrdinalIgnoreCase))
                {
                    diffs++;
                }
            }
        }
        return diffs;
    }
}
