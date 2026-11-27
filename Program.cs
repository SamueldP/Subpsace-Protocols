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
        AssetManager.TryApplyWindowIcon();
        Raylib.SetExitKey(KeyboardKey.Null); // ESC toggles the CRT Pause Menu, never closes the window
        Raylib.SetWindowMinSize(640, 360);
        Raylib.SetTargetFPS(60);

        // Ensure default Raylib font uses crisp point filtering (zero font smoothing)
        Font defaultFont = Raylib.GetFontDefault();
        if (defaultFont.Texture.Id != 0)
        {
            Raylib.SetTextureFilter(defaultFont.Texture, TextureFilter.Point);
        }

        FontManager.Initialize(config.UiFontPreset, headlessMode: !Raylib.IsWindowReady());
        FontManager.SetFontSizeOffset(config.UiFontSizeOffset);
        AudioHooks.Initialize(headlessMode: isSmokeTest);
        FactionDialoguePool.EnsureLoaded();

        using VirtualCanvas virtualCanvas = new();
        virtualCanvas.ApplyOptimalMonitorScaling(allowBorderlessFullscreen: !isSmokeTest);

        ScreenManager screenManager = new();
        TutorialOverlay tutorialOverlay = new(gameState, config);
        Action openContextRules = () => tutorialOverlay.OpenSummaryForScreen(screenManager.CurrentScreenId);
        PauseMenuOverlay pauseMenu = new(screenManager, gameState, virtualCanvas, openContextRules);
        DebugOverlay debugOverlay = new(screenManager, gameState, config, devDebug, openContextRules);

        IntroScreen introScreen = new(screenManager, devDebug);
        CorridorScreen corridorScreen = new(screenManager, gameState);
        screenManager.RegisterScreen(ScreenId.MainMenu, new MainMenuScreen(screenManager, gameState, config, devDebug));
        screenManager.RegisterScreen(ScreenId.Intro, introScreen);
        screenManager.RegisterScreen(ScreenId.Corridor, corridorScreen);
        DeskScreen deskScreen = new(
            screenManager,
            gameState,
            config,
            devDebug,
            pauseMenu.Toggle,
            day => tutorialOverlay.TriggerDayStartHook(day));
        screenManager.RegisterScreen(ScreenId.Desk, deskScreen);
        QuartersScreen quartersScreen = new(
            screenManager,
            gameState,
            config,
            pauseMenu.Toggle,
            () => tutorialOverlay.OnFirstMorningQuarters(),
            () => tutorialOverlay.OnFirstEveningQuarters(),
            devDebug);
        screenManager.RegisterScreen(ScreenId.Quarters, quartersScreen);
        ArtTestScreen artTestScreen = new(screenManager, config);
        screenManager.RegisterScreen(ScreenId.ArtTest, artTestScreen);

        screenManager.SwitchTo(isArtTestFlag ? ScreenId.ArtTest : ScreenId.MainMenu);

        int smokeFrameCounter = 0;
        ScreenId[] smokeRouteOrder =
        [
            ScreenId.MainMenu,
            ScreenId.Intro,
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

            // Priority ESC routing:
            // 1. Tutorial Card dismisses cleanly if open
            // 2. If Quarters 5-Tab Terminal / Modal / Chute / Vent popup is open (and Pause Menu is NOT open), ESC closes that first!
            // 3. Otherwise ESC toggles the in-game Pause Menu on Desk, Ledger, Corridor, Evening/Morning Quarters (never closing app)
            bool tutorialConsumed = !pauseMenu.IsOpen && tutorialOverlay.HandleInput(mouse);
            bool quartersEscapeConsumed = !tutorialConsumed && !pauseMenu.IsOpen &&
                                          screenManager.CurrentScreenId == ScreenId.Quarters &&
                                          Raylib.IsKeyPressed(KeyboardKey.Escape) &&
                                          quartersScreen.TryConsumeEscape();
            bool paused = !tutorialConsumed && !quartersEscapeConsumed && pauseMenu.HandleInput(mouse);
            bool debugConsumed = !paused && !tutorialConsumed && !quartersEscapeConsumed && debugOverlay.HandleInput(mouse);

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
                    RunPrompt4QuartersAndEspionageVerificationSuite(config);

                    // Run interactive DeskScreen Prompt 3.7.1 & Prompt 4 espionage checks on Day 2 and Day 3
                    gameState.ResetForNewSave(1);
                    gameState.AdvanceToDay(2);
                    screenManager.SwitchTo(ScreenId.Desk);
                    tutorialOverlay.ForceCloseSilent();
                    deskScreen.SmokeVerifyPrompt371Interactions();

                    gameState.Day4PreShiftHailCompleted = true;
                    gameState.AdvanceToDay(config.DayUnlockWarpScanner);
                    screenManager.SwitchTo(ScreenId.Desk);
                    tutorialOverlay.ForceCloseSilent();
                    deskScreen.SmokeVerifyPrompt371Interactions();
                    deskScreen.SmokeVerifyPrompt4DeskEspionage();
                    deskScreen.SmokeVerifyBuild43DeskFeatures();
                    deskScreen.SmokeVerifyBuild431DeskFeatures();
                    RunBuild432VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);

                    // Run interactive Quarters Coat Pocket <-> Service Chute <-> Vent Stash click transfer verification
                    // plus Build 4.3.1 3.0s Sleep shutter, blank-on-open MESSAGES + LED sync, hidden Days 1-5 tabs & Day 6 Hack Reveal
                    // plus Build 4.3.3 Quarters Wall-Mounted Interactive Star Chart & data/sector_map.json verification
                    screenManager.SwitchTo(ScreenId.Quarters);
                    tutorialOverlay.ForceCloseSilent();
                    quartersScreen.SmokeVerifyCoatPocketAndVentTransfers();
                    RunBuild431InteractiveQuartersVerificationSuite(quartersScreen, gameState, config, devDebug);
                    RunBuild433StarChartVerificationSuite(
                        virtualCanvas,
                        screenManager,
                        quartersScreen,
                        pauseMenu,
                        gameState);
                    RunBuild434VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild435VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild436VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunShipRendererV2VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        artTestScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config);
                    RunBuild45VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        artTestScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config);
                    RunBuild46VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config);
                    RunBuild47VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        introScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild475VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild51VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild52VerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        corridorScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config,
                        devDebug);
                    RunBuild521Day6TargetSyncVerificationSuite(
                        virtualCanvas,
                        screenManager,
                        deskScreen,
                        quartersScreen,
                        tutorialOverlay,
                        pauseMenu,
                        debugOverlay,
                        gameState,
                        config);

                    if (!string.IsNullOrWhiteSpace(dumpFramesDir))
                    {
                        CaptureVisualVerificationFrames(
                            virtualCanvas,
                            screenManager,
                            deskScreen,
                            quartersScreen,
                            artTestScreen,
                            tutorialOverlay,
                            pauseMenu,
                            debugOverlay,
                            gameState,
                            config,
                            devDebug,
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
        FontManager.Shutdown();
        AudioHooks.Shutdown();
        Raylib.CloseWindow();

        if (isSmokeTest)
        {
            if (PixelBuffer.ActiveProceduralTextureCount != 0)
            {
                throw new InvalidOperationException(
                    $"VRAM texture leak detected: {PixelBuffer.ActiveProceduralTextureCount} procedural textures still active.");
            }
            if (FontManager.ActiveGpuFontCount != 0)
            {
                throw new InvalidOperationException(
                    $"GPU font leak detected: {FontManager.ActiveGpuFontCount} fonts still active.");
            }
            if (AudioHooks.ActiveSoundAliasCount != 0)
            {
                throw new InvalidOperationException(
                    $"Audio sound alias leak detected: {AudioHooks.ActiveSoundAliasCount} aliases still active.");
            }
            if (AudioHooks.ActiveSoundCount != 0)
            {
                throw new InvalidOperationException(
                    $"Audio sound leak detected: {AudioHooks.ActiveSoundCount} sounds still active.");
            }
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

        // 1. Verify Day 1 Simplified Single Rule (ONLY TransponderIdMismatch, shields may spawn UP on Day 1+, weapons MUST stay cold)
        int d1ShieldedCount = 0;
        for (int i = 0; i < 60; i++)
        {
            ShipSpec day1Ship = generator.GenerateShipForSlot(simState, i);
            if (day1Ship.GroundTruth.ActualWeaponsHot)
                throw new InvalidOperationException("Day 1 ship must spawn with weapons cold.");
            if (day1Ship.GroundTruth.ActualShieldsUp)
                d1ShieldedCount++;

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
        if (d1ShieldedCount == 0)
            throw new InvalidOperationException("Day 1 ships should spawn shields-up frequently under Build 4.5 ShieldsUpBaseChance.");

        // Day 1 Detain on invalid ship without Protocol Review triggers Brengop Clerical Warning;
        // confirming via Protocol Review first succeeds accurately (+10 SC)
        {
            GameState d1State = new() { Day = 1, PactScrip = 45, SuppressDiskPersistence = true };
            ShipSpec d1Inv = generator.GenerateShipForSlot(d1State, 0, forceValidity: DebugSpawnValidityMode.ForceInvalid);
            d1Inv.ProtocolReviewVerified = false;
            DecisionEvaluationResult d1Unverified = validator.EvaluateDecision(d1State, d1Inv, PlayerDecision.Detain, worldSim);
            if (d1Unverified.IsAccurate || !d1Unverified.IsCitation || d1Unverified.IsSecurityStrike ||
                d1Unverified.ViolationReason != ValidationEngine.UnverifiedDetentionReason)
            {
                throw new InvalidOperationException("Day 1 unverified Detain did not trigger Brengop Clerical Warning.");
            }

            d1Inv.ProtocolReviewVerified = true;
            DecisionEvaluationResult d1Verified = validator.EvaluateDecision(d1State, d1Inv, PlayerDecision.Detain, worldSim);
            if (!d1Verified.IsAccurate || d1Verified.IsCitation || d1Verified.ScripDelta != 10)
            {
                throw new InvalidOperationException("Day 1 verified Detain on invalid ship should succeed (+10 SC).");
            }
        }

        // 2. Verify Day 2 Escalation (ONLY TransponderIdMismatch + SpoofedWeaponsHot, NO Ghost Route on Day 2)
        GameState d2State = new() { Day = 2, PactScrip = 45, RunSeed = 20260410, SuppressDiskPersistence = true };
        HashSet<ShipInfractionType> seenD2 = [];
        for (int i = 0; i < 60; i++)
        {
            ShipSpec s2 = generator.GenerateShipForSlot(d2State, i, reseedNonce: 0u, DebugSpawnValidityMode.Auto);
            if (s2.GroundTruth.IsDay2GhostRouteSuspect)
                throw new InvalidOperationException("Ghost Route suspect moved to Day 3 breather and must NOT spawn on Day 2.");
            ShipSpec inv2 = generator.GenerateShipForSlot(d2State, i, reseedNonce: 4u, DebugSpawnValidityMode.ForceInvalid);
            ShipInfractionType t = inv2.GroundTruth.ActiveInfractions[0].Type;
            seenD2.Add(t);
            if (t is not (ShipInfractionType.TransponderIdMismatch or ShipInfractionType.SpoofedWeaponsHot))
                throw new InvalidOperationException($"Day 2 generated disallowed infraction: {t}");
        }
        if (!seenD2.Contains(ShipInfractionType.TransponderIdMismatch) || !seenD2.Contains(ShipInfractionType.SpoofedWeaponsHot))
            throw new InvalidOperationException("Day 2 must generate both TransponderIdMismatch and SpoofedWeaponsHot.");

        // Day 3 Breather spawns hardcoded Ghost Route suspect (slot 1 or 2 deterministic from RunSeed)
        GameState ghostDay3State = new() { Day = Constants.DayGhostRouteSuspect, PactScrip = 45, RunSeed = 20260410, SuppressDiskPersistence = true };
        int ghostSlot = ShipGenerator.GetGhostRouteQueueSlot(ghostDay3State);
        if (ghostSlot is not (1 or 2) || ShipGenerator.GetDay2GhostRouteQueueSlot(ghostDay3State.RunSeed) != ghostSlot)
            throw new InvalidOperationException($"Day 3 Ghost Route slot must deterministically be 1 or 2, got {ghostSlot}.");
        ShipSpec ghostSuspect = generator.GenerateShipForSlot(ghostDay3State, ghostSlot);
        if (!ghostSuspect.GroundTruth.IsDay2GhostRouteSuspect)
            throw new InvalidOperationException($"Day 3 Ghost Route suspect must spawn on its deterministic slot ({ghostSlot}).");
        HashSet<int> reachableGhostSlots = [];
        for (int seed = 1; seed <= 32; seed++)
        {
            reachableGhostSlots.Add(ShipGenerator.GetGhostRouteQueueSlot(seed));
        }
        if (!reachableGhostSlots.Contains(1) || !reachableGhostSlots.Contains(2) || reachableGhostSlots.Count != 2)
            throw new InvalidOperationException("Day 3 Ghost Route slot randomization must reach both slot 1 and slot 2 across seeds.");

        // 2b. Verify Build 4.5 28-Day Stagger:
        //   Day 3 (Breather): TransponderIdMismatch + SpoofedWeaponsHot ONLY
        //   Days 4-7: Adds FalsifiedOriginWaveform ONLY (Days 5 & 7 are breathers)
        //   Days 8-9: Adds ExpiredRegistration + PrefixOrganizationMismatch + PrefixSpeciesMismatch (NO Hulls, NO Cargo)
        //   Days 10-11: Adds PrefixHullSilhouetteMismatch (NO Cargo)
        //   Day 12+: Adds UnexplainedCargoPowerDraw (SPS Violet Cargo Bay)
        {
            GameState d3PoolState = new() { Day = 3, PactScrip = 45, SuppressDiskPersistence = true };
            for (int i = 0; i < 80; i++)
            {
                ShipSpec s = generator.GenerateShipForSlot(d3PoolState, i, reseedNonce: 7u, DebugSpawnValidityMode.ForceInvalid);
                ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                if (t is not (ShipInfractionType.TransponderIdMismatch or ShipInfractionType.SpoofedWeaponsHot))
                    throw new InvalidOperationException($"Day 3 breather generated disallowed post-Day-3 infraction: {t}");
            }

            for (int day = 4; day <= 7; day++)
            {
                GameState d47PoolState = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD47 = [];
                for (int i = 0; i < 120; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d47PoolState, i, reseedNonce: 7u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD47.Add(t);
                    if (t is not (ShipInfractionType.TransponderIdMismatch
                        or ShipInfractionType.SpoofedWeaponsHot
                        or ShipInfractionType.FalsifiedOriginWaveform
                        or ShipInfractionType.SpokenRouteDiscrepancy))
                    {
                        throw new InvalidOperationException($"Day {day} generated disallowed post-Day-7 infraction: {t}");
                    }
                }
                if (!seenD47.Contains(ShipInfractionType.FalsifiedOriginWaveform) ||
                    !seenD47.Contains(ShipInfractionType.SpokenRouteDiscrepancy))
                {
                    throw new InvalidOperationException($"Day {day} must generate FalsifiedOriginWaveform and SpokenRouteDiscrepancy.");
                }
            }

            for (int day = 8; day <= 9; day++)
            {
                GameState d89PoolState = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD89 = [];
                for (int i = 0; i < 180; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d89PoolState, i, reseedNonce: 9u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD89.Add(t);
                    if (t is ShipInfractionType.PrefixHullSilhouetteMismatch
                        or ShipInfractionType.UnexplainedCargoPowerDraw
                        or ShipInfractionType.BannedCargoItem
                        or ShipInfractionType.SpokenCargoDiscrepancy
                        or ShipInfractionType.CargoManifestDiscrepancy
                        or ShipInfractionType.WithheldIllicitCargoManifest
                        or ShipInfractionType.StokerMassMismatch)
                    {
                        throw new InvalidOperationException($"Day {day} generated disallowed infraction: {t}");
                    }
                }
                if (!seenD89.Contains(ShipInfractionType.ExpiredRegistration) ||
                    !seenD89.Contains(ShipInfractionType.PrefixOrganizationMismatch) ||
                    !seenD89.Contains(ShipInfractionType.PrefixSpeciesMismatch))
                {
                    throw new InvalidOperationException($"Day {day} must generate ExpiredRegistration, PrefixOrganizationMismatch, and PrefixSpeciesMismatch.");
                }
            }

            for (int day = 10; day <= 11; day++)
            {
                GameState d1011State = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD1011 = [];
                for (int i = 0; i < 180; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d1011State, i, reseedNonce: 13u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD1011.Add(t);
                    if (t is ShipInfractionType.UnexplainedCargoPowerDraw
                        or ShipInfractionType.BannedCargoItem
                        or ShipInfractionType.SpokenCargoDiscrepancy
                        or ShipInfractionType.CargoManifestDiscrepancy
                        or ShipInfractionType.WithheldIllicitCargoManifest
                        or ShipInfractionType.StokerMassMismatch)
                    {
                        throw new InvalidOperationException($"Day {day} generated disallowed Day 12+ cargo infraction: {t}.");
                    }
                }
                if (!seenD1011.Contains(ShipInfractionType.PrefixHullSilhouetteMismatch))
                    throw new InvalidOperationException($"Day {day} must generate PrefixHullSilhouetteMismatch.");
            }

            for (int day = 12; day <= 14; day++)
            {
                GameState d12State = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD12 = [];
                for (int i = 0; i < 260; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d12State, i, reseedNonce: 17u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    if (t == ShipInfractionType.StokerMassMismatch)
                        throw new InvalidOperationException($"Day {day} generated disallowed Day 15+ StokerMassMismatch.");
                    seenD12.Add(t);
                }
                if (day == 12 &&
                    (!seenD12.Contains(ShipInfractionType.UnexplainedCargoPowerDraw) ||
                     !seenD12.Contains(ShipInfractionType.BannedCargoItem)))
                {
                    throw new InvalidOperationException("Day 12 must unlock UnexplainedCargoPowerDraw and BannedCargoItem.");
                }
            }

            GameState d15PoolState = new() { Day = Constants.DefaultDayUnlockStoker, PactScrip = 90, SuppressDiskPersistence = true };
            HashSet<ShipInfractionType> seenD15 = [];
            bool sawSlagDenseOverweight = false;
            for (int i = 0; i < 320; i++)
            {
                ShipSpec s = generator.GenerateShipForSlot(d15PoolState, i, reseedNonce: 23u, DebugSpawnValidityMode.ForceInvalid);
                foreach (ShipInfraction inf in s.GroundTruth.ActiveInfractions)
                {
                    seenD15.Add(inf.Type);
                }
                if (string.Equals(s.GroundTruth.WaferCargoManifestItem, "Raw Slag Isotope", StringComparison.OrdinalIgnoreCase))
                {
                    if (s.GroundTruth.ActualMass <= s.Slate.DeclaredTonnage ||
                        !s.GroundTruth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.StokerMassMismatch))
                    {
                        throw new InvalidOperationException("Day 15+ Raw Slag Isotope (DenseMass) failed to roll overweight physical mass.");
                    }
                    sawSlagDenseOverweight = true;
                }
            }
            if (!seenD15.Contains(ShipInfractionType.StokerMassMismatch) || !sawSlagDenseOverweight)
            {
                throw new InvalidOperationException("Day 15 must unlock StokerMassMismatch and pair Raw Slag Isotope with overweight physical mass.");
            }
        }

        // 2c. Verify Security Strikes unified strictly to /3 (no silent mid-shift 5-strike game over)
        {
            GameState strikeState = new() { Day = 4, SecurityStrikesActive = true, PactScrip = 45, SuppressDiskPersistence = true };
            for (int i = 0; i < 5; i++)
            {
                validator.AddSecurityStrike(strikeState, 1);
            }
            if (strikeState.EndingFlags.TryGetValue("Fail_SecurityDetainment", out bool midShiftFail) && midShiftFail)
                throw new InvalidOperationException("AddSecurityStrike must NOT trigger silent mid-shift 5-strike Fail_SecurityDetainment; 3/3 triggers Sleep Kordani Berth Audit.");
        }

        // 3. Verify Day 4 Escalation (Protocol Review required, Security Strikes active, full pool without typos)
        GameState d4State = new() { Day = 4, SecurityStrikesActive = true, PactScrip = 45, SuppressDiskPersistence = true };
        ShipSpec d4Valid = generator.GenerateShipForSlot(d4State, 0, forceValidity: DebugSpawnValidityMode.ForceValid);
        d4Valid.GroundTruth.ActualShieldsUp = false;
        d4Valid.GroundTruth.ActualWeaponsHot = false;
        ShipSpec d4Invalid = generator.GenerateShipForSlot(d4State, 1, forceValidity: DebugSpawnValidityMode.ForceInvalid);

        // Case 1: Valid + Grant Access -> +10 SC
        DecisionEvaluationResult c1 = validator.EvaluateDecision(d4State, d4Valid, PlayerDecision.GrantAccess, worldSim);
        if (!c1.IsAccurate || c1.IsCitation || c1.ScripDelta != 10)
            throw new InvalidOperationException("Day 4 Case 1 (Valid + GrantAccess) failed.");

        // Case 4: Day 4 Invalid + Detain + ProtocolReviewVerified == false -> Citation!
        d4Invalid.ProtocolReviewVerified = false;
        DecisionEvaluationResult c4 = validator.EvaluateDecision(d4State, d4Invalid, PlayerDecision.Detain, worldSim);
        if (c4.IsAccurate || !c4.IsCitation || c4.ViolationReason != ValidationEngine.UnverifiedDetentionReason)
            throw new InvalidOperationException("Day 4 Case 4 (Unverified Detain) failed.");

        // Case 5: Day 4 Invalid + Detain + ProtocolReviewVerified == true -> Accurate (+10 SC)
        d4Invalid.ProtocolReviewVerified = true;
        DecisionEvaluationResult c5 = validator.EvaluateDecision(d4State, d4Invalid, PlayerDecision.Detain, worldSim);
        if (!c5.IsAccurate || c5.IsCitation || c5.ScripDelta != 10)
            throw new InvalidOperationException("Day 4 Case 5 (Verified Detain) failed.");

        // Verify live Pay Per Ship tuner changes payout immediately
        d4State.BasePayPerShipScrip = 15;
        DecisionEvaluationResult cPay15 = validator.EvaluateDecision(d4State, d4Valid, PlayerDecision.GrantAccess, worldSim);
        if (!cPay15.IsAccurate || cPay15.ScripDelta != 15)
            throw new InvalidOperationException("Live BasePayPerShipScrip tuner (15 SC) did not affect ValidationEngine payout.");

        // Verify Day 4 Live Kordani Hail lines for BOTH Branch A and Branch B
        string[] branchA = Day2And3ScriptedDialogue.BuildDay4KordaniHailLines(true, "ORN-442-K", "Oruun-4");
        string[] branchB = Day2And3ScriptedDialogue.BuildDay4KordaniHailLines(false, "ORN-442-K", "Oruun-4");
        if (branchA.Length < 4 || branchB.Length < 4 ||
            !branchA[0].Contains("Our boarding teams investigated the vessel you detained yesterday", StringComparison.Ordinal))
            throw new InvalidOperationException("Day 4 Kordani Live Hail lines missing or diverged for Branch A or Branch B.");

        // Verify Hold Timer Until First Hail & variable Day 1 (210s) shift_horn trigger
        ShiftController timerShift = new();
        timerShift.StartNewShift(simState);
        timerShift.Tick(30.0f);
        if (timerShift.ElapsedShiftSeconds > 0.001f)
            throw new InvalidOperationException("ShiftController must hold timer at t = 0 until first Next Hail.");
        timerShift.NotifyFirstHailTriggered();
        timerShift.Tick(209.0f);
        if (timerShift.IsShiftTimerExpired)
            throw new InvalidOperationException("Day 1 ShiftController expired early before 210s.");
        timerShift.Tick(1.0f);
        if (!timerShift.IsShiftTimerExpired || !string.Equals(AudioHooks.LastPlayedSoundId, "shift_horn", StringComparison.Ordinal))
            throw new InvalidOperationException("ShiftController failed to fire 'shift_horn' at Day 1 t = 210s.");

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
            spoofedLiars.WeaponsPowerDownOrdersIssued = 0;

            if (!ShiftController.ShouldSpsDetectHotWeapons(spoofedLiars))
                throw new InvalidOperationException("SPS failed to detect hot weapons on a spoofed vessel.");

            // 1st PWR DOWN WPN -> bluff compliance while staying visibly hot in SPS
            ComplianceOrderOutcome spoofFirst = ShiftController.ResolveWeaponsOrder(spsState, spoofedLiars);
            if (spoofFirst != ComplianceOrderOutcome.SpoofedBluff ||
                !spoofedLiars.GroundTruth.ActualWeaponsHot ||
                !ShiftController.ShouldSpsDetectHotWeapons(spoofedLiars))
            {
                throw new InvalidOperationException("1st PWR DOWN WPN on spoofed vessel failed to bluff compliance while staying hot in SPS.");
            }

            // 2nd PWR DOWN WPN -> defensive refusal/excuse while staying visibly hot in SPS
            ComplianceOrderOutcome spoofSecond = ShiftController.ResolveWeaponsOrder(spsState, spoofedLiars);
            if (spoofSecond != ComplianceOrderOutcome.SpoofedDeflection ||
                !spoofedLiars.GroundTruth.ActualWeaponsHot ||
                !ShiftController.ShouldSpsDetectHotWeapons(spoofedLiars))
            {
                throw new InvalidOperationException("2nd PWR DOWN WPN on spoofed vessel failed to trigger defensive refusal while staying hot in SPS.");
            }
        }
    }

    private static void RunPrompt4QuartersAndEspionageVerificationSuite(Core.ConfigFlags config)
    {
        GameState state = new() { SuppressDiskPersistence = true };
        EconomyEngine economy = new();
        IntelEconomy intel = new();
        RaidController raidController = new();
        ShipGenerator generator = new();
        WorldSim worldSim = new();

        // 1. New Game starts in Day 1 Morning Quarters with 1 mandatory unread Brengop email (blinking LED), PocketCapacity = 1
        state.ResetForNewSave(1);
        if (state.Day != 1 ||
            state.QuartersPhase != QuartersPhase.Morning ||
            state.UnreadMessageCount != 1 ||
            !state.HasUnreadEmails ||
            state.Day1BrengopEmailRead ||
            state.Day1VorstHailCompleted ||
            state.PocketCapacity != 1)
        {
            throw new InvalidOperationException("New Game must start at Day 1 Morning Quarters with 1 unread Brengop email and PocketCapacity == 1.");
        }

        // 2. Clock-Out Ledger: PayPerShip live tuning, auto-deducted Berth Lease (-30 SC), 1st deferral warning vs 2nd Debt Foreclosure
        state.BasePayPerShipScrip = 15;
        state.ShipsProcessedToday = 4;
        state.ShiftGrossPayEarned = 60;
        state.PactScrip = 45 + 60; // 105 SC
        ShiftLedgerSummary ledger = economy.SettleShiftClockOut(state, config);
        if (ledger.PayPerShipScrip != 15 || ledger.GrossShiftPay != 60 || !ledger.BerthLeasePaid ||
            ledger.ResultingNetScrip != 105 - config.BerthLeaseCost || state.QuartersPhase != QuartersPhase.Evening)
        {
            throw new InvalidOperationException("Clock-Out Ledger settlement math or Evening Quarters transition failed.");
        }

        // Verify 1st night lease deferral & 2nd night Debt Foreclosure
        GameState leaseTest = new() { SuppressDiskPersistence = true };
        leaseTest.ResetForNewSave(1);
        leaseTest.PactScrip = 10;
        ShiftLedgerSummary defer1 = economy.SettleShiftClockOut(leaseTest, config);
        if (!defer1.BerthLeaseDeferred || defer1.DebtForeclosureTriggered || leaseTest.MissedBerthLeaseCount != 1)
            throw new InvalidOperationException("1st missed Berth Lease night did not defer with warning flag.");
        ShiftLedgerSummary defer2 = economy.SettleShiftClockOut(leaseTest, config);
        if (!defer2.DebtForeclosureTriggered || !leaseTest.EndingFlags["Fail_DebtForeclosure"])
            throw new InvalidOperationException("2nd missed Berth Lease night did not trigger Debt Foreclosure.");

        // 3. Evening -> PACT REQ / DECK 4 -> Sleep -> Morning Quarters (NO double Berth Lease charge, 0500 chute drop, Day 2 Morning Ship Roster!)
        int scripBeforeNight = state.PactScrip;
        var (okFood, _) = economy.TryOrderNightlyRation(state, NightlyRationOrder.HighTierRations, config);
        var (okBlank, _) = economy.TryOrderBlankDataSlate(state, config);
        if (!okFood || !okBlank || !state.ReplicatorDispensedTonight || state.PendingChuteDeliveries.Count != 1)
            throw new InvalidOperationException("Evening Quarters PACT REQ / DECK 4 orders failed.");

        int scripBeforeSleep = state.PactScrip;
        economy.CompleteSleepToMorning(state, config, generator);
        if (state.Day != 2 || state.QuartersPhase != QuartersPhase.Morning ||
            state.PactScrip != scripBeforeSleep || // Berth Lease MUST NOT be charged a second time!
            state.ServiceChuteItems.Count != 1 ||
            state.CurrentHungerTier != HungerTier.HighTierRations ||
            !state.HasUnreadEmails)
        {
            throw new InvalidOperationException("Evening -> Sleep -> Day 2 Morning Quarters resolution failed.");
        }

        // Verify Day 2 Morning Ship Roster: exact 10 ships, 1-3 real arrivals in deterministic 16-ship queue, Ghost Route excluded
        if (state.TodayMorningRoster.Count != 10)
            throw new InvalidOperationException($"Expected 10 Morning Ship Roster vessels, got {state.TodayMorningRoster.Count}.");
        int realCount = state.TodayMorningRoster.Count(r => r.IsRealArrival);
        if (realCount is < 1 or > 3)
            throw new InvalidOperationException($"Expected 1-3 real arrivals on Morning Ship Roster, got {realCount}.");
        List<ShipSpec> day2Queue = generator.GenerateDailyQueue(state, Constants.PreGeneratedQueueSize);
        foreach (MorningRosterEntry r in state.TodayMorningRoster.Where(x => x.IsRealArrival))
        {
            if (r.RealQueueSlot < 0 || r.RealQueueSlot >= 16 ||
                day2Queue[r.RealQueueSlot].GroundTruth.IsDay2GhostRouteSuspect ||
                !day2Queue[r.RealQueueSlot].Slate.RegistrationCode.Equals(r.RegistrationCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Morning Ship Roster real arrival failed alignment with 16-ship pre-generated queue.");
            }
        }

        // 4. Build 4.5: Day 6/15 Pocket Capacity Upgrade Gates, Day 12+ Covert Delivery Missions, Day 6 Hack Drop & Guaranteed MNG- Target,
        //    Missions-Only CRYPTO / LOGS Direct [TRANSMIT] Dispatch & Option A DECK 4 50 SC Strike Clearing
        if (config.PocketUpgrade2MinDay != 6 || config.PocketUpgrade3MinDay != 15)
            throw new InvalidOperationException($"Expected Day 6 / Day 15 Pocket Capacity upgrade gates, found Day {config.PocketUpgrade2MinDay} / Day {config.PocketUpgrade3MinDay}.");
        if (config.ClockOutPowerDownDelaySeconds < 3.8f || config.ClockOutPowerDownDelaySeconds > 4.2f)
            throw new InvalidOperationException($"Expected ClockOutPowerDownDelaySeconds in [3.8, 4.2], found {config.ClockOutPowerDownDelaySeconds}.");

        // Verify Pocket Upgrade Day 6 (60 SC) and Day 15 (120 SC) locks
        state.PocketCapacity = 1;
        state.PactScrip = 300;
        state.Day = 5;
        if (economy.GetNextPocketUpgradeInfo(state, config).CanUpgrade)
            throw new InvalidOperationException("Pocket Capacity 1->2 upgrade must be locked on Day 5.");
        state.Day = 6;
        if (!economy.TryPurchasePocketCapacityUpgrade(state, config).Success || state.PocketCapacity != 2)
            throw new InvalidOperationException("Pocket Capacity 1->2 upgrade failed on Day 6.");
        state.Day = 14;
        if (economy.GetNextPocketUpgradeInfo(state, config).CanUpgrade)
            throw new InvalidOperationException("Pocket Capacity 2->3 upgrade must be locked on Day 14.");
        state.Day = 15;
        if (!economy.TryPurchasePocketCapacityUpgrade(state, config).Success || state.PocketCapacity != 3)
            throw new InvalidOperationException("Pocket Capacity 2->3 upgrade failed on Day 15.");

        // Verify NO covert delivery emails trigger on Days 1-11; OP-01 / OP-02 live on Day 12+
        CovertMissionDatabase missionDb = CovertMissionDatabase.Load();
        foreach (CovertMissionSpec spec in missionDb.Missions)
        {
            if (spec.MinAvailableDay < 6)
                throw new InvalidOperationException($"Covert mission {spec.MissionId} illegally available before Day 6.");
            if (spec.IsDeliveryMission && spec.MinAvailableDay < 12)
                throw new InvalidOperationException($"Covert delivery mission {spec.MissionId} illegally available before Day 12.");
            if (spec.IsDeliveryMission && spec.TriggerEmailDay is > 0 and < 12)
                throw new InvalidOperationException($"Covert delivery email {spec.MissionId} illegally triggers before Day 12.");
        }

        GameState d111State = new() { SuppressDiskPersistence = true };
        d111State.ResetForNewSave(1);
        for (int d = 1; d <= 11; d++)
        {
            economy.DeliverScheduledEmailsForPhase(d111State, d, QuartersPhase.Morning, config);
            economy.DeliverScheduledEmailsForPhase(d111State, d, QuartersPhase.Evening, config);
        }
        if (d111State.Emails.Any(e => e.IsEncrypted))
            throw new InvalidOperationException("Encrypted covert delivery emails must NEVER trigger on Days 1-11.");

        // Day 6 Morning unsolicited drop (free Blank Data Slate in 0500 chute + CHECK YOUR CHUTE email) & guaranteed MNG- in queue + Morning Roster
        GameState d6State = new() { SuppressDiskPersistence = true };
        d6State.ResetForNewSave(1);
        d6State.AdvanceToDay(6);
        d6State.QuartersPhase = QuartersPhase.Morning;
        economy.EnsureMorningRosterGenerated(d6State, generator);
        bool dropped = economy.EnsureDay6MorningUnsolicitedDrop(d6State);
        if (!dropped ||
            d6State.TerminalHackedUnlocked ||
            !d6State.ServiceChuteItems.Any(i => i.ItemType == ContrabandPocketItem.TypeBlankDataSlate) ||
            !d6State.Emails.Any(e => e.IsTerminalHackEmail && e.Sender == "unknown" && e.Subject == "Check your chute" && e.CustomStatusLine == "asf.as\u2026tje" && !e.IsRead))
        {
            throw new InvalidOperationException("Day 6 Morning unsolicited 0500 Blank Data Slate drop or Check your chute email failed.");
        }

        List<ShipSpec> day6Queue = generator.GenerateDailyQueue(d6State, Constants.PreGeneratedQueueSize);
        if (!day6Queue.Any(s => s.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)) ||
            !d6State.TodayMorningRoster.Any(r => r.IsRealArrival && r.Prefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Day 6 failed to guarantee a Mining Guild (MNG-) vessel inside both the 16-ship queue and Morning Ship Roster.");
        }

        // Day 6 Hack unlock auto-activates MNG- intro intercept job -> Missions-Only CRYPTO / LOGS direct [TRANSMIT] (+45 SC, logs [AUDIT_FLAG])
        economy.ActivateDay6IntroHackMission(d6State);
        if (!d6State.TerminalHackedUnlocked || !d6State.ActiveInterceptMissionIds.Contains(EconomyEngine.Day6IntroMissionId))
            throw new InvalidOperationException("ActivateDay6IntroHackMission failed to unlock terminal or activate Day 6 MNG- intercept contract.");

        ContrabandPocketItem mngCaptured = new()
        {
            ItemId = "test_mng_cap_1",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = $"STOLEN SLATE: {d6State.Day6IntroTargetReg}",
            TargetShipPrefix = "MNG-",
            TargetShipRegistration = d6State.Day6IntroTargetReg,
            OriginStarSystem = "Oruun-4"
        };
        d6State.VentStashItems.Add(mngCaptured);
        List<MissionDispatchMatch> dispatches = intel.GetActiveInterceptDispatches(d6State, missionDb);
        if (dispatches.Count != 1 || !dispatches[0].CanTransmit || !dispatches[0].SlateIsInVentStash)
            throw new InvalidOperationException("CRYPTO / LOGS Mission Dispatch failed to match captured MNG- slate in Vent Stash.");

        int d6ScripPre = d6State.PactScrip;
        int d6LogsPre = d6State.UnscrubbedLogCount;
        MissionTransmitResult mngTx = intel.ExecuteMissionInterceptTransmit(d6State, worldSim, EconomyEngine.Day6IntroMissionId, mngCaptured, slateIsInVentStash: true, missionDb);
        if (!mngTx.Success || mngTx.ScripEarned != 45 || d6State.PactScrip != d6ScripPre + 45 ||
            d6State.UnscrubbedLogCount <= d6LogsPre ||
            !d6State.SecurityLogs.Any(l => l.Tag == "[AUDIT_FLAG]") ||
            d6State.GetPower("MNG-") != -1)
        {
            throw new InvalidOperationException("Day 6 MNG- direct [TRANSMIT] failed to credit +45 SC, log [AUDIT_FLAG], or decrement MNG- power.");
        }

        // Day 12+ Covert Delivery Missions: Accept Type 1 Delivery (queues sealed Mission Slate for 0500) + Type 2 Intercept + DECRYPT [0 SC]
        state.AdvanceToDay(12);
        state.TerminalHackedUnlocked = true;
        state.QuartersPhase = QuartersPhase.Evening;
        economy.DeliverScheduledEmailsForPhase(state, 12, QuartersPhase.Evening, config);
        int scripPreDecrypt = state.PactScrip;
        int logsPreDecrypt = state.UnscrubbedLogCount;
        var (decOk, _, _) = economy.TryDecryptNextMessage(state);
        if (!decOk || state.PactScrip != scripPreDecrypt || state.UnscrubbedLogCount <= logsPreDecrypt ||
            !state.PendingChuteDeliveries.Any(i => i.ItemType == ContrabandPocketItem.TypeMissionDataSlate && i.MissionId == "slate_bus_mission_01"))
        {
            throw new InvalidOperationException("Day 12 DECRYPT MESSAGE (0 SC) failed to decrypt OP-01 free, log [UNAUTH_IO], or queue sealed Mission Slate.");
        }

        var (intAccOk, _) = economy.TryAcceptCovertContract(state, "intercept_op_01", missionDb);
        if (!intAccOk || !state.ActiveInterceptMissionIds.Contains("intercept_op_01"))
            throw new InvalidOperationException("DECK 4 failed to accept Day 7+ Intercept contract intercept_op_01.");

        ContrabandPocketItem bmcCaptured = new()
        {
            ItemId = "test_bmc_cap_1",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            TargetShipPrefix = "BMC-",
            TargetShipRegistration = "BMC-505-K",
            OriginStarSystem = "Dul-Krag"
        };
        state.PocketItems.Add(bmcCaptured);
        int guTrustBefore = state.DoubleAgentTrust;
        MissionTransmitResult bmcTx = intel.ExecuteMissionInterceptTransmit(state, worldSim, "intercept_op_01", bmcCaptured, slateIsInVentStash: false, missionDb);
        if (!bmcTx.Success || state.DoubleAgentTrust != guTrustBefore + 1 || state.GetRep("GUV-") != 2 || state.GetPower("BMC-") != -1)
            throw new InvalidOperationException("Day 7+ GU Intercept direct [TRANSMIT] failed to apply DoubleAgentTrust, +2 GUV rep, and -1 BMC power.");

        // Option A Strike Clearing: DECK 4 Audit Deferral Fee [50 SC -> -1 STRIKE] is the sole regular strike clearer
        state.SecurityStrikes = 2;
        state.PactScrip = 100;
        var (deferOk, _) = economy.TryPurchaseAuditDeferralFee(state, config);
        if (!deferOk || state.SecurityStrikes != 1 || state.PactScrip != 100 - config.AuditDeferralFeeCost)
            throw new InvalidOperationException("Option A DECK 4 Audit Deferral Fee (50 SC -> -1 Strike) failed.");

        // 5. Two-Tier Sleep-Triggered Kordani Berth Raids (RaidController):
        //    5a. SCRUB LOGS costs flat 15 SC -> Clean Logs + Contraband in Vent Stash -> Standard Sweep ONLY (Vent Stash SAFE!)
        state.AdvanceToDay(4);
        state.SecurityStrikes = config.MaxStrikesBeforeAudit;
        state.PactScrip = 100;
        var (scrubOk, _, _) = economy.TryScrubSecurityLogs(state, config);
        if (!scrubOk || state.UnscrubbedLogCount != 0 || state.PactScrip != 100 - config.ScrubSecurityLogsCost)
            throw new InvalidOperationException("SCRUB LOGS (15 SC) failed to deduct 15 SC or purge security hex logs.");

        state.PocketItems.Clear();
        state.VentStashItems.Clear();
        state.VentStashItems.Add(new ContrabandPocketItem
        {
            ItemId = "vent_safe_item",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = "STOLEN SLATE: BMC-101-A"
        });
        int scripBeforeCleanAudit = state.PactScrip;
        int kdfRepPreAudit = state.GetRep("KDF-");
        BerthAuditResolution cleanAudit = raidController.ResolveBerthAudit(state, config, worldSim, BerthAuditKind.ThreeStrikeBerthAudit);
        if (cleanAudit.WasCaught || cleanAudit.DeepStructuralSweepTriggered ||
            state.VentStashItems.Count != 1 || // Vent Stash MUST stay undiscovered when logs are clean!
            state.SecurityStrikes != 0 ||
            cleanAudit.CleanAuditFineCharged != config.CleanAuditFine ||
            state.PactScrip != scripBeforeCleanAudit - config.CleanAuditFine ||
            state.GetRep("KDF-") != kdfRepPreAudit + config.CleanAuditKordaniRepDrop)
        {
            throw new InvalidOperationException("Two-Tier Clean-Log 3-Strike Raid failed to keep Vent Stash safe, reset strikes, charge fine, or drop KDF rep.");
        }

        //    5b. Tier 1 Coat Frisk with Shielded Pocket Lining (1 item concealed) + Clean Logs -> Passes cleanly!
        state.PendingStokerBerthSearch = true;
        state.HasShieldedPocketLining = true;
        state.PocketItems.Add(new ContrabandPocketItem
        {
            ItemId = "shielded_blank",
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "BLANK DATA SLATE"
        });
        BerthAuditResolution stokerAudit = raidController.ResolveBerthAudit(state, config, worldSim, BerthAuditKind.StokerSabotageAudit);
        if (stokerAudit.WasCaught || stokerAudit.PocketContrabandConfiscated != 0 || state.PocketItems.Count != 1 || state.PendingStokerBerthSearch)
            throw new InvalidOperationException("Stoker Sabotage Raid with Shielded Pocket Lining (1 item) failed.");

        //    5c. Tier 1 Coat Frisk with Unshielded Contraband (2nd item beyond lining) -> Confiscated & Caught!
        state.PocketItems.Add(new ContrabandPocketItem
        {
            ItemId = "unshielded_blank",
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "UNSHIELDED SLATE"
        });
        BerthAuditResolution coatCaught = raidController.ResolveBerthAudit(state, config, worldSim, BerthAuditKind.ThreeStrikeBerthAudit);
        if (!coatCaught.WasCaught || coatCaught.PocketContrabandConfiscated != 1 || state.PocketItems.Count != 1 ||
            !state.EndingFlags["Fail_SecurityDetainment"])
        {
            throw new InvalidOperationException("Coat Frisk failed to confiscate unshielded contraband beyond Shielded Lining capacity.");
        }

        //    5d. Tier 2 Dirty Terminal Logs -> Deep Structural Sweep breaches & confiscates Under-Desk Vent Stash!
        state.EndingFlags["Fail_SecurityDetainment"] = false;
        state.PocketItems.Clear();
        state.AddSecurityLog("[UNAUTH_IO]", "UNSCRUBBED TEST", isSuspicious: true);
        BerthAuditResolution deepSweepCaught = raidController.ResolveBerthAudit(state, config, worldSim, BerthAuditKind.ThreeStrikeBerthAudit);
        if (!deepSweepCaught.WasCaught || !deepSweepCaught.DeepStructuralSweepTriggered ||
            deepSweepCaught.VentContrabandConfiscated != 1 || state.VentStashItems.Count != 0 ||
            !state.EndingFlags["Fail_SecurityDetainment"])
        {
            throw new InvalidOperationException("Dirty terminal logs failed to trigger Deep Structural Sweep & confiscate Under-Desk Vent Stash.");
        }

        // 6. Verify /data/forum.json loads and filters stoker_overload + berth_search + power_score triggers cleanly
        ForumDatabase forumDb = ForumDatabase.Load();
        state.StokerOverloadedToday = true;
        List<ForumThreadModel> threads = economy.GetVisibleForumThreads(state, forumDb);
        if (forumDb.Threads.Count < 8 || !threads.Any(t => t.Id == "evt_stoker_overload_brownout"))
            throw new InvalidOperationException("data/forum.json failed to load or trigger stoker_overload thread.");

        // 7. Verify GameState CopyFrom preserves Prompt 4 fields & TutorialFlags quarters hooks
        state.NextDayPowerBrownout = true;
        state.PendingStokerBerthSearch = true;
        state.TerminalHackedUnlocked = true;
        state.TutorialFlags.MarkSeen(TutorialEventHook.OnFirstMorningQuarters);
        GameState clone = new() { SuppressDiskPersistence = true };
        clone.CopyFrom(state);
        if (!clone.NextDayPowerBrownout || !clone.PendingStokerBerthSearch ||
            !clone.TerminalHackedUnlocked ||
            !clone.TutorialFlags.HasSeen(TutorialEventHook.OnFirstMorningQuarters) ||
            clone.TutorialFlags.HasSeen(TutorialEventHook.OnFirstEveningQuarters))
        {
            throw new InvalidOperationException("GameState.CopyFrom failed to preserve Prompt 4 / Build 4.3.1 fields.");
        }

        // 8. Verify Build 4.3 / 4.3.4 Quarters, Terminal Tab Order, Path E live PactScrip 1,000 SC check, Berth Preview sandbox, and Price Scale
        if (config.IndependentPassageFundTarget != 1000)
            throw new InvalidOperationException($"Expected default IndependentPassageFundTarget == 1000, got {config.IndependentPassageFundTarget}.");
        state.PactScrip = 999;
        if (state.IsPathEScripRequirementMet(config))
            throw new InvalidOperationException("Path E 1,000 SC requirement must fail at 999 SC.");
        state.PactScrip = 1000;
        if (!state.IsPathEScripRequirementMet(config))
            throw new InvalidOperationException("Path E 1,000 SC requirement must pass at 1,000 SC live PactScrip.");

        if (TerminalOverlay.TabSpecs.Length != 5 ||
            TerminalOverlay.TabSpecs[0].Label != "MESSAGES" || TerminalOverlay.TabSpecs[0].IsUnofficial ||
            TerminalOverlay.TabSpecs[1].Label != "FORUM" || TerminalOverlay.TabSpecs[1].IsUnofficial ||
            TerminalOverlay.TabSpecs[2].Label != "PACT REQ" || TerminalOverlay.TabSpecs[2].IsUnofficial ||
            TerminalOverlay.TabSpecs[3].Label != "CRYPTOGRAPHY" || !TerminalOverlay.TabSpecs[3].IsUnofficial ||
            TerminalOverlay.TabSpecs[4].Label != "DECK 4" || !TerminalOverlay.TabSpecs[4].IsUnofficial)
        {
            throw new InvalidOperationException("TerminalOverlay 5-tab order or Official/Unofficial split mismatch.");
        }

        GameState previewTest = new() { SuppressDiskPersistence = false };
        previewTest.StartSandboxedBerthPreview();
        if (!previewTest.SuppressDiskPersistence || previewTest.ActiveSaveSlot != 0)
            throw new InvalidOperationException("StartSandboxedBerthPreview must set SuppressDiskPersistence = true and ActiveSaveSlot = 0.");

        DevDebugState priceDebug = new();
        int origBerth = config.BerthLeaseCost;
        priceDebug.StepPriceScale(2, config); // 1.0x -> 1.5x
        if (config.BerthLeaseCost <= origBerth)
            throw new InvalidOperationException("DevDebugState.StepPriceScale failed to scale ConfigFlags costs.");
        priceDebug.StepPriceScale(-2, config); // back to 1.0x
        if (config.BerthLeaseCost != origBerth)
            throw new InvalidOperationException("DevDebugState.StepPriceScale failed to restore 1.0x ConfigFlags costs.");
    }

    private static void RunBuild431InteractiveQuartersVerificationSuite(
        QuartersScreen quartersScreen,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        VirtualMouseState idle = new() { X = 100, Y = 100, IsInsideViewport = true, Scale = 1.0f };
        int prevAnimIdx = devDebug.AnimationSpeedPresetIndex;
        devDebug.AnimationSpeedPresetIndex = 1; // 1.0x

        // 1. Days 1-5: Unofficial tabs (CRYPTO / LOGS & DECK 4) completely hidden & unclickable; Day 1 starts with 1 unread Brengop email & blinking LED
        gameState.ResetForNewSave(1);
        quartersScreen.OnEnter();
        if (gameState.TerminalHackedUnlocked ||
            gameState.UnreadMessageCount != 1 ||
            !quartersScreen.IsPhysicalTerminalUnreadLedActive)
        {
            throw new InvalidOperationException("Day 1 Morning must start with TerminalHackedUnlocked == false, 1 unread Brengop email, and active blinking Unread LED.");
        }

        quartersScreen.Terminal.Open(initialTab: 0);
        if (quartersScreen.Terminal.IsTabAccessible(3) || quartersScreen.Terminal.IsTabAccessible(4))
            throw new InvalidOperationException("Days 1-5 unofficial tabs (CRYPTO / LOGS, DECK 4) must not be accessible while TerminalHackedUnlocked == false.");

        // Attempt clicking where CRYPTO / LOGS and DECK 4 sit on top tab bar -> must ignore clicks!
        quartersScreen.Update(0.016f, new VirtualMouseState { X = 800, Y = 72, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        quartersScreen.Update(0.016f, new VirtualMouseState { X = 1000, Y = 72, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (quartersScreen.Terminal.ActiveTab != 0)
            throw new InvalidOperationException("Hidden unofficial tabs responded to clicks while TerminalHackedUnlocked == false.");
        quartersScreen.Terminal.Close();

        // 2. Weighty 3.0s 3-Stage Eyelid Sleep Shutter (Day 5 Evening -> Day 6 Morning):
        //    0.8s Close -> 1.4s Dark Hold (no skip; Day 6 generates mid-hold!) -> 0.8s Open
        gameState.AdvanceToDay(5);
        gameState.QuartersPhase = QuartersPhase.Evening;
        gameState.SecurityStrikes = 0;
        gameState.PendingStokerBerthSearch = false;
        quartersScreen.OnEnter();
        quartersScreen.SmokeTriggerEveningSleep();

        // Click during Close (0.4s) & Dark Hold (1.2s) -> must NOT instant-skip!
        quartersScreen.Update(0.40f, new VirtualMouseState { X = 640, Y = 360, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (!quartersScreen.IsSleepTransitionActive || gameState.Day != 5)
            throw new InvalidOperationException("Sleep shutter prematurely skipped or advanced day during Close Eyes stage (0.4s).");

        quartersScreen.Update(0.80f, new VirtualMouseState { X = 640, Y = 360, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f }); // 1.2s (early Dark Hold)
        if (!quartersScreen.IsSleepTransitionActive || gameState.Day != 5)
            throw new InvalidOperationException("Sleep shutter prematurely skipped during early Dark Hold (1.2s).");

        // Advance past midpoint (1.50s -> 1.80s): Day 6 Morning + Roster + Free Chute Blank Slate + CHECK YOUR CHUTE generated while eyes remain closed!
        quartersScreen.Update(0.60f, idle); // 1.8s (still Dark Hold)
        if (!quartersScreen.IsSleepTransitionActive || gameState.Day != 6 || gameState.QuartersPhase != QuartersPhase.Morning)
            throw new InvalidOperationException("Sleep shutter failed to generate Day 6 Morning during Dark Hold midpoint.");

        // Still animating Open Eyes at 2.6s -> finishes at 3.0s!
        quartersScreen.Update(0.80f, idle); // 2.6s (mid Open Eyes)
        if (!quartersScreen.IsSleepTransitionActive)
            throw new InvalidOperationException("Sleep shutter finished before 3.0s Open Eyes completed.");

        quartersScreen.Update(0.50f, idle); // 3.1s -> complete!
        if (quartersScreen.IsSleepTransitionActive)
            throw new InvalidOperationException("Sleep shutter did not finish cleanly after 3.0s.");

        // 3. Blank-on-Open MESSAGES Reader + Unread/Read LED Sync + Day 6 CHECK YOUR CHUTE Hack Glitch Reveal (~1.0s)
        if (!quartersScreen.IsPhysicalTerminalUnreadLedActive || gameState.UnreadMessageCount < 2)
            throw new InvalidOperationException("Day 6 Morning must have >= 2 unread messages (Roster + CHECK YOUR CHUTE) and active physical Terminal LED.");

        quartersScreen.Terminal.Open(initialTab: 0);
        if (quartersScreen.Terminal.HasSelectedEmail || gameState.UnreadMessageCount < 2)
            throw new InvalidOperationException("Opening Personal Terminal MUST leave MESSAGES reader pane blank and MUST NOT auto-mark emails read!");

        // Switch to FORUM and back to MESSAGES -> reader must STILL remain blank!
        quartersScreen.Terminal.SwitchTabForSmoke(1);
        quartersScreen.Terminal.SwitchTabForSmoke(0);
        if (quartersScreen.Terminal.HasSelectedEmail)
            throw new InvalidOperationException("Switching to MESSAGES tab MUST leave reader pane blank until explicit click!");

        // Unread rows sort at the top; explicitly click Morning Roster first -> 1 unread remains (CHECK YOUR CHUTE), LED STILL ON!
        string rosterId = $"morning_roster_day_{gameState.Day:D2}";
        if (!quartersScreen.Terminal.SmokeClickEmailById(rosterId) ||
            !quartersScreen.IsPhysicalTerminalUnreadLedActive ||
            gameState.TerminalHackedUnlocked)
        {
            throw new InvalidOperationException("Reading Morning Roster prematurely disabled Unread LED or unlocked hacked tabs.");
        }

        // Explicitly click CHECK YOUR CHUTE -> triggers ~1.0s glitch reveal, turns Unread LED OFF (all mail read!), unlocks tabs at ~1.0s!
        if (!quartersScreen.Terminal.SmokeClickEmailById(EconomyEngine.Day6HackEmailId) ||
            !quartersScreen.Terminal.IsHackGlitchActive ||
            quartersScreen.IsPhysicalTerminalUnreadLedActive)
        {
            throw new InvalidOperationException("Clicking CHECK YOUR CHUTE failed to trigger hack glitch animation or sync physical Unread LED off.");
        }

        // Mid-glitch (0.50s): still glitching, not finalized yet
        quartersScreen.Update(0.50f, idle);
        if (!quartersScreen.Terminal.IsHackGlitchActive || gameState.TerminalHackedUnlocked)
            throw new InvalidOperationException("Day 6 Terminal Hack glitch settled prematurely at 0.50s.");

        // Complete glitch (1.05s): TerminalHackedUnlocked == true, tabs 3 & 4 accessible, MNG- intro contract auto-activated!
        quartersScreen.Update(0.55f, idle);
        if (quartersScreen.Terminal.IsHackGlitchActive ||
            !gameState.TerminalHackedUnlocked ||
            !quartersScreen.Terminal.IsTabAccessible(3) ||
            !quartersScreen.Terminal.IsTabAccessible(4) ||
            !gameState.ActiveInterceptMissionIds.Contains(EconomyEngine.Day6IntroMissionId))
        {
            throw new InvalidOperationException("Day 6 Terminal Hack glitch failed to unlock CRYPTO/LOGS + DECK 4 or auto-activate MNG- intro contract.");
        }

        quartersScreen.Terminal.Close();
        devDebug.AnimationSpeedPresetIndex = prevAnimIdx;
    }

    private static void RunBuild432VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        // 1. Verify data/config.json defaults: OptionB2_CommsSwapFlush & 65 chars/sec & 3x B portrait default
        if (config.DeskUpperLayoutMode != DeskUpperLayoutMode.OptionB2_CommsSwapFlush)
            throw new InvalidOperationException($"Expected default DeskUpperLayoutMode == OptionB2_CommsSwapFlush, got {config.DeskUpperLayoutMode}.");
        if (MathF.Abs(config.DialogueTypewriterCharsPerSec - 65.0f) > 0.01f)
            throw new InvalidOperationException($"Expected default DialogueTypewriterCharsPerSec == 65, got {config.DialogueTypewriterCharsPerSec}.");
        if (!devDebug.Use3xPortraitInLayoutB)
            throw new InvalidOperationException("Expected DevDebugState.Use3xPortraitInLayoutB default == true (3x / 192px).");

        // 2. Standalone CommsTypewriterQueue verification: 65 chars/sec, punctuation micro-pauses, [RECEIVING...],
        //    click-to-finish, AnimationSpeedMultiplier scaling, and comms_type_officer / comms_type_captain stubs
        CommsTypewriterQueue q = new();
        string plain100 = new('A', 100);
        q.EnqueueSpokenLine(plain100, CommsSpeakerKind.Officer, CommsTextTone.ClearanceTeal);
        q.Update(1.0f, config.DialogueTypewriterCharsPerSec);
        if (q.ActiveVisibleCharCount is < 64 or > 66)
            throw new InvalidOperationException($"Typewriter 65 chars/sec rate mismatch after 1.0s: got {q.ActiveVisibleCharCount} chars.");
        if (!AudioHooks.LoggedEvents.Contains("comms_type_officer"))
            throw new InvalidOperationException("Typewriter failed to emit 'comms_type_officer' audio stub.");

        // Punctuation micro-pauses (, +0.06s, .?! +0.15s, ... +0.25s, decimal ignored)
        if (MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Hold, check.", 4) - 0.06f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Hold, check.", 11) - 0.15f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Ready? Go!", 5) - 0.15f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Ready? Go!", 9) - 0.15f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Wait... ok", 4) - 0.0f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Wait... ok", 5) - 0.0f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("Wait... ok", 6) - 0.25f) > 0.0001f ||
            MathF.Abs(CommsTypewriterQueue.ComputePunctuationPause("CYC 2409.04", 8) - 0.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Typewriter punctuation micro-pause timing check failed.");
        }

        // [RECEIVING...] subspace lag (0.55-0.65s, bumped +0.20s) between Officer finishing & Captain starting + comms_type_captain stub
        q.Clear();
        q.EnqueueSpokenLine("> BOOTH 04: \"State cargo.\"", CommsSpeakerKind.Officer, CommsTextTone.ClearanceTeal, instantPrefixLength: 12);
        q.EnqueueSpokenLine("CAPT: \"Refined alloy ingots for Void Gate Three.\"", CommsSpeakerKind.Captain, CommsTextTone.AmberPrimary, instantPrefixLength: 6);
        q.Update(0.05f, 65.0f);
        if (!q.IsTypingActiveLine || q.ActiveSpeaker != CommsSpeakerKind.Officer)
            throw new InvalidOperationException("Officer line failed to start typing.");

        // Click-to-finish active Officer line -> commits Officer 100%, preserves Captain line in [RECEIVING...] beat!
        if (!q.FinishActiveLineImmediately() ||
            q.CommittedLines.Count != 1 ||
            !q.IsWaitingReceiving ||
            q.ReceivingSecondsRemaining is < 0.55f or > 0.65f)
        {
            throw new InvalidOperationException($"Click-to-finish did not arm 0.55-0.65s [RECEIVING...] beat cleanly (remaining={q.ReceivingSecondsRemaining:0.00}).");
        }

        // Mid-lag (0.35s): still waiting in [RECEIVING...]
        q.Update(0.35f, 65.0f);
        if (!q.IsWaitingReceiving || q.IsTypingActiveLine)
            throw new InvalidOperationException("[RECEIVING...] subspace lag ended prematurely at 0.35s.");

        // Past lag (+0.35s): Captain now typing and emits comms_type_captain!
        q.Update(0.35f, 65.0f);
        if (q.IsWaitingReceiving || !q.IsTypingActiveLine || q.ActiveSpeaker != CommsSpeakerKind.Captain ||
            !AudioHooks.LoggedEvents.Contains("comms_type_captain"))
        {
            throw new InvalidOperationException("Captain reply failed to begin typing or emit 'comms_type_captain' after [RECEIVING...] lag.");
        }

        // 3. Interactive DeskScreen Layout Switcher (A / B1 / B2), 2x/3x portrait rendering, dynamic trace endpoints & typewriter
        void RenderLiveFrame(VirtualMouseState m)
        {
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(m);
            canvas.EndVirtualDrawAndBlit();
        }
        deskScreen.SmokeVerifyBuild432LayoutAndTypewriter(RenderLiveFrame);

        // 4. Pause Menu Continuity: ESC / Pause cleanly freezes active typewriter & [RECEIVING...] timers without dropping lines
        DeskUpperLayoutMode savedMode = config.DeskUpperLayoutMode;
        bool saved3x = devDebug.Use3xPortraitInLayoutB;
        try
        {
            deskScreen.SmokeStageTypewriterReceivingBeat();
            if (!deskScreen.CommsQueue.IsWaitingReceiving)
                throw new InvalidOperationException("Failed to stage [RECEIVING...] beat for Pause Menu continuity check.");

            float receivingBeforePause = deskScreen.CommsQueue.ReceivingSecondsRemaining;
            int committedBeforePause = deskScreen.CommsQueue.CommittedLines.Count;

            pauseMenu.Open();
            VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            for (int f = 0; f < 20; f++)
            {
                bool paused = pauseMenu.IsOpen;
                if (!paused)
                {
                    screenManager.Update(0.05f, idle);
                }
                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(idle);
                pauseMenu.DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
            }

            if (!deskScreen.CommsQueue.IsWaitingReceiving ||
                MathF.Abs(deskScreen.CommsQueue.ReceivingSecondsRemaining - receivingBeforePause) > 0.0001f ||
                deskScreen.CommsQueue.CommittedLines.Count != committedBeforePause)
            {
                throw new InvalidOperationException("Pause Menu failed to freeze [RECEIVING...] timer without corrupting queued lines.");
            }

            // Unpause -> advance past [RECEIVING...] into active Captain typing, then pause AGAIN mid-sentence!
            pauseMenu.Close();
            deskScreen.Update(ShiftController.SubspaceReceivingLagSeconds + 0.12f, idle);
            if (!deskScreen.CommsQueue.IsTypingActiveLine || deskScreen.CommsQueue.ActiveSpeaker != CommsSpeakerKind.Captain)
                throw new InvalidOperationException("Typewriter did not resume smoothly after closing Pause Menu.");

            int charsBeforePause = deskScreen.CommsQueue.ActiveVisibleCharCount;
            pauseMenu.Open();
            for (int f = 0; f < 15; f++)
            {
                if (!pauseMenu.IsOpen)
                {
                    deskScreen.Update(0.05f, idle);
                }
            }
            if (!deskScreen.CommsQueue.IsTypingActiveLine || deskScreen.CommsQueue.ActiveVisibleCharCount != charsBeforePause)
                throw new InvalidOperationException("Pause Menu failed to freeze active typewriter character counter.");
            pauseMenu.Close();

            // 5. F1 DebugOverlay Live Switcher Clicks (Row 14: A / B1 / B2 & PORTRAIT (B) 3x/2x)
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 20, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.DeskUpperLayoutMode != DeskUpperLayoutMode.OptionA_Classic)
                throw new InvalidOperationException("F1 DebugOverlay [DESK: A] button did not switch to OptionA_Classic.");

            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 100, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.DeskUpperLayoutMode != DeskUpperLayoutMode.OptionB1_CommsSwap)
                throw new InvalidOperationException("F1 DebugOverlay [DESK: B1] button did not switch to OptionB1_CommsSwap.");

            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 180, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.DeskUpperLayoutMode != DeskUpperLayoutMode.OptionB2_CommsSwapFlush)
                throw new InvalidOperationException("F1 DebugOverlay [DESK: B2] button did not switch to OptionB2_CommsSwapFlush.");

            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 280, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (devDebug.Use3xPortraitInLayoutB)
                throw new InvalidOperationException("F1 DebugOverlay [PORTRAIT (B)] toggle did not switch from 3x to 2x.");

            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 280, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (!devDebug.Use3xPortraitInLayoutB)
                throw new InvalidOperationException("F1 DebugOverlay [PORTRAIT (B)] toggle did not switch back from 2x to 3x.");
            debugOverlay.IsVisible = false;
        }
        finally
        {
            pauseMenu.Close();
            debugOverlay.IsVisible = false;
            config.DeskUpperLayoutMode = savedMode;
            devDebug.Use3xPortraitInLayoutB = saved3x;
            deskScreen.SyncLayoutFromConfig();
            _ = gameState;
        }
    }

    private static void RunBuild433StarChartVerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        QuartersScreen quartersScreen,
        PauseMenuOverlay pauseMenu,
        GameState gameState)
    {
        // 1. Verify data/sector_map.json parses cleanly & b134 ("REMOVE IN PROD") is strictly excluded from RenderableBorders
        SectorMapData map = SectorMapData.Load();
        if (map.Borders.Count < 9 || map.Nodes.Count < 40 || map.Routes.Count < 45 || map.Labels.Count < 9)
        {
            throw new InvalidOperationException(
                $"data/sector_map.json failed count check: borders={map.Borders.Count}, nodes={map.Nodes.Count}, routes={map.Routes.Count}, labels={map.Labels.Count}.");
        }

        if (map.RenderableBorders.Any(b =>
                b.IsRemoveInProdMarker ||
                b.Id.Equals("b134", StringComparison.OrdinalIgnoreCase) ||
                b.Name.Equals("REMOVE IN PROD", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Border b134 ('REMOVE IN PROD') MUST NEVER be included in RenderableBorders.");
        }

        if (map.RenderableBorders.Count != map.Borders.Count - 1)
        {
            throw new InvalidOperationException($"Expected RenderableBorders.Count == {map.Borders.Count - 1}, got {map.RenderableBorders.Count}.");
        }

        // 2. Verify b134 encloses exact 7 clickable nodes (VG-3 + 6 named neighbors) and 9 clickable connecting trade routes
        (string Id, string Name, string ExpectedFaction)[] expectedClickableNodes =
        [
            ("n11", "VG-3", "[TODO]"),
            ("n15", "Gharok", "Krushvak"),
            ("n16", "Veridia", "GU"),
            ("n17", "Oruun-4", "Independent"),
            ("n19", "Ash", "Unclaimed"),
            ("n20", "Korr-Vath", "Kordani"),
            ("n21", "Dul-Krag", "Brengop")
        ];

        if (map.ClickableNodes.Count != expectedClickableNodes.Length)
        {
            throw new InvalidOperationException($"Expected {expectedClickableNodes.Length} clickable nodes enclosed by b134, got {map.ClickableNodes.Count}.");
        }

        foreach (var (id, name, expectedFaction) in expectedClickableNodes)
        {
            if (!map.NodesById.TryGetValue(id, out SectorMapNode? node) ||
                !node.IsClickable ||
                !node.Name.Equals(name, StringComparison.Ordinal) ||
                !node.EffectiveFaction.Equals(expectedFaction, StringComparison.Ordinal) ||
                !node.EffectiveTag.Equals("[TODO]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Clickable node verification failed for {id} ({name}).");
            }
        }

        string[] expectedClickableRoutes = ["r47", "r48", "r49", "r50", "r51", "r52", "r64", "r111", "r112"];
        if (map.ClickableRoutes.Count != expectedClickableRoutes.Length)
        {
            throw new InvalidOperationException($"Expected {expectedClickableRoutes.Length} clickable trade routes enclosed by b134, got {map.ClickableRoutes.Count}.");
        }
        foreach (string rId in expectedClickableRoutes)
        {
            if (!map.ClickableRoutes.Any(r => r.Id.Equals(rId, StringComparison.OrdinalIgnoreCase) && r.EffectiveExport == "[TODO]"))
            {
                throw new InvalidOperationException($"Expected clickable trade route {rId} with EffectiveExport == '[TODO]'.");
            }
        }

        // Verify outer nodes (including WORMHOLE) remain non-clickable background context
        if (!map.NodesById.TryGetValue("n99", out SectorMapNode? wormhole) ||
            !wormhole.Name.Equals("WORMHOLE", StringComparison.Ordinal) ||
            wormhole.IsClickable)
        {
            throw new InvalidOperationException("WORMHOLE (n99) must exist as non-clickable background context.");
        }

        // 3. Verify empty string fallback to [TODO] when user leaves a field blank
        SectorMapNode blankTestNode = new() { Id = "test", Name = "VG-3", Faction = "   ", Tag = "" };
        SectorMapRoute blankTestRoute = new() { Id = "r_test", From = "n15", To = "n11", Export = "" };
        if (blankTestNode.EffectiveFaction != "[TODO]" ||
            blankTestNode.EffectiveTag != "[TODO]" ||
            blankTestRoute.EffectiveExport != "[TODO]")
        {
            throw new InvalidOperationException("Blank faction, tag, or export field failed to fall back to '[TODO]'.");
        }

        // 4. Interactive Quarters Wall Fixture, Modal Clicking (Nodes + Routes), & ESC Priority Before Pause Menu
        gameState.ResetForNewSave(1);
        screenManager.SwitchTo(ScreenId.Quarters);
        quartersScreen.SmokeCloseSubOverlays();
        pauseMenu.Close();

        // Clicking dead broken right-wall appliance (1136, 344, 120, 152) must NOT open Star Chart
        quartersScreen.Update(0.016f, new VirtualMouseState
        {
            X = 1136 + 60,
            Y = 344 + 60,
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        });
        if (quartersScreen.StarChart.IsOpen || quartersScreen.IsAnySubOverlayOpen)
        {
            throw new InvalidOperationException("Dead broken right-wall appliance illegally opened an overlay on click.");
        }

        // Clicking glowing Star Chart wall unit (1136, 176, 120, 144) opens StarChartModal
        quartersScreen.Update(0.016f, new VirtualMouseState
        {
            X = QuartersScreen.StarChartX + QuartersScreen.StarChartW / 2,
            Y = QuartersScreen.StarChartY + QuartersScreen.StarChartH / 2,
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        });
        if (!quartersScreen.StarChart.IsOpen || !quartersScreen.IsAnySubOverlayOpen)
        {
            throw new InvalidOperationException("Clicking Quarters Star Chart wall fixture did not open StarChartModal.");
        }

        // Click each of the 7 clickable nodes (VG-3 + 6 neighboring systems) via virtual mouse coords
        foreach (var (id, name, expectedFaction) in expectedClickableNodes)
        {
            var (nx, ny) = quartersScreen.StarChart.GetNodeScreenPosition(id);
            quartersScreen.Update(0.016f, new VirtualMouseState
            {
                X = nx,
                Y = ny,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });

            if (!string.Equals(quartersScreen.StarChart.SelectedNodeId, id, StringComparison.OrdinalIgnoreCase) ||
                quartersScreen.StarChart.SelectedRouteId != null ||
                quartersScreen.StarChart.ReadoutPrimaryText != $"SYSTEM: {name}" ||
                quartersScreen.StarChart.ReadoutSecondaryText != $"CONTROLLING FACTION: {expectedFaction}" ||
                quartersScreen.StarChart.ReadoutTertiaryText != "SYSTEM TAG: [TODO]")
            {
                throw new InvalidOperationException(
                    $"Clicking node {id} ({name}) produced unexpected readout: '{quartersScreen.StarChart.ReadoutCombinedLine}'.");
            }
        }

        // Click each of the 9 clickable trade routes at their screen midpoints via point-to-segment distance check
        foreach (string rId in expectedClickableRoutes)
        {
            var (mx, my) = quartersScreen.StarChart.GetRouteMidpointScreen(rId);
            quartersScreen.Update(0.016f, new VirtualMouseState
            {
                X = mx,
                Y = my,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });

            if (!string.Equals(quartersScreen.StarChart.SelectedRouteId, rId, StringComparison.OrdinalIgnoreCase) ||
                quartersScreen.StarChart.SelectedNodeId != null ||
                !quartersScreen.StarChart.ReadoutPrimaryText.StartsWith("TRADE ROUTE: ", StringComparison.Ordinal) ||
                !quartersScreen.StarChart.ReadoutPrimaryText.Contains(" <-> ", StringComparison.Ordinal) ||
                quartersScreen.StarChart.ReadoutSecondaryText != "PRIMARY EXPORT: [TODO]")
            {
                throw new InvalidOperationException(
                    $"Clicking trade route {rId} at ({mx},{my}) failed readout check: '{quartersScreen.StarChart.ReadoutCombinedLine}'.");
            }
        }

        // Clicking outer non-clickable node WORMHOLE (n99) must NOT select it
        string? prevRoute = quartersScreen.StarChart.SelectedRouteId;
        var (wx, wy) = quartersScreen.StarChart.GetNodeScreenPosition("n99");
        quartersScreen.Update(0.016f, new VirtualMouseState
        {
            X = wx,
            Y = wy,
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        });
        if (quartersScreen.StarChart.SelectedNodeId != null || quartersScreen.StarChart.SelectedRouteId != prevRoute)
        {
            throw new InvalidOperationException("Outer non-clickable WORMHOLE node responded to selection click.");
        }

        // Render live frame of open Star Chart modal to verify GPU drawing pipeline
        VirtualMouseState hoverVg3 = new()
        {
            X = quartersScreen.StarChart.GetNodeScreenPosition("n11").X,
            Y = quartersScreen.StarChart.GetNodeScreenPosition("n11").Y,
            IsInsideViewport = true,
            Scale = 1.0f
        };
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(hoverVg3);
        canvas.EndVirtualDrawAndBlit();

        // Verify ESC priority: TryConsumeEscape() closes StarChartModal first before Pause Menu can open
        if (!quartersScreen.TryConsumeEscape() || quartersScreen.StarChart.IsOpen || pauseMenu.IsOpen)
        {
            throw new InvalidOperationException("ESC did not close StarChartModal cleanly before Pause Menu.");
        }

        // Re-open and verify top-right [X] close button
        quartersScreen.StarChart.Open();
        quartersScreen.Update(0.016f, new VirtualMouseState
        {
            X = StarChartModal.CloseBtnX + 10,
            Y = StarChartModal.CloseBtnY + 10,
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        });
        if (quartersScreen.StarChart.IsOpen)
        {
            throw new InvalidOperationException("StarChartModal top-right [X] button did not close the modal.");
        }
    }

    private static void RunBuild434VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
        TalkingHeadAdvanceMode savedAdvanceMode = config.TalkingHeadAdvanceMode;
        int savedBuyoutTarget = config.IndependentPassageFundTarget;
        int savedAnimIdx = devDebug.AnimationSpeedPresetIndex;

        try
        {
            config.TalkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance;
            devDebug.AnimationSpeedPresetIndex = 1; // 1.0x

            // 1. Day 1 Morning Quarters: Mandatory Brengop Email Gate & Verbatim Contract #884-D
            gameState.ResetForNewSave(1);
            screenManager.SwitchTo(ScreenId.Quarters);
            tutorialOverlay.ForceCloseSilent();

            if (gameState.Day != 1 ||
                gameState.QuartersPhase != QuartersPhase.Morning ||
                gameState.Day1BrengopEmailRead ||
                gameState.Day1VorstHailCompleted ||
                gameState.UnreadMessageCount != 1 ||
                !quartersScreen.IsPhysicalTerminalUnreadLedActive ||
                !quartersScreen.IsDoorGatedByDay1Email)
            {
                throw new InvalidOperationException("Day 1 Morning Quarters did not initialize with unread Brengop email gate & blinking LED.");
            }

            // Clicking Quarters Door while Day1BrengopEmailRead == false must block transition and show diegetic toast
            quartersScreen.Update(0.016f, new VirtualMouseState
            {
                X = 16 + 40,
                Y = 360 + 100,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (screenManager.CurrentScreenId != ScreenId.Quarters ||
                quartersScreen.ActiveNudgeText != "Should probably read my emails first.")
            {
                throw new InvalidOperationException(
                    $"Quarters Door failed to block Day 1 exit before reading email (screen={screenManager.CurrentScreenId}, nudge='{quartersScreen.ActiveNudgeText}').");
            }

            // Opening Terminal MESSAGES must keep reader pane blank and keep LED blinking until explicit click
            quartersScreen.Terminal.Open(initialTab: 0);
            if (quartersScreen.Terminal.HasSelectedEmail ||
                gameState.UnreadMessageCount != 1 ||
                !quartersScreen.IsPhysicalTerminalUnreadLedActive ||
                gameState.Day1BrengopEmailRead)
            {
                throw new InvalidOperationException("Opening MESSAGES must leave reader pane blank and email unread until explicitly clicked.");
            }

            // Explicitly open Brengop Contract #884-D email -> verify verbatim copy, read state, LED off, Door unlocked
            if (!quartersScreen.Terminal.SmokeClickEmailById(TerminalEmail.Day1BrengopEmailId))
                throw new InvalidOperationException("Failed to locate and click Day 1 Brengop Contract #884-D email.");

            TerminalEmail expectedMail = TerminalEmail.CreateDay1BrengopReassignmentEmail();
            TerminalEmail? actualMail = gameState.Emails.FirstOrDefault(
                e => e.EmailId.Equals(TerminalEmail.Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase));
            if (actualMail == null ||
                actualMail.Sender != "Brengop Mercantile Consortium // VG3 Debt & Asset Management" ||
                actualMail.Recipient != "Contractor #884-D (Subspace Gate 3, Booth 04)" ||
                actualMail.Subject != "Congratulations on Your Upward Mobility [CONTRACT #884-D]" ||
                actualMail.Body != expectedMail.Body ||
                !actualMail.Body.StartsWith("Dear Contractor #884-D,", StringComparison.Ordinal) ||
                !actualMail.Body.Contains("forty-two minutes\u2014a regrettable", StringComparison.Ordinal) ||
                !actualMail.Body.Contains("- Transit Debt Buyout (Off-Station Visa): 1,000 SC (Lump sum)", StringComparison.Ordinal) ||
                !actualMail.Body.Contains("perusal of this notice constitutes constructive assent", StringComparison.Ordinal) ||
                !actualMail.Body.EndsWith("\u2014 Brengop Mercantile Consortium", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Day 1 Brengop email copy diverged from locked authored copy.");
            }

            if (gameState.UnreadMessageCount != 0 ||
                quartersScreen.IsPhysicalTerminalUnreadLedActive ||
                !gameState.Day1BrengopEmailRead ||
                quartersScreen.IsDoorGatedByDay1Email)
            {
                throw new InvalidOperationException("Opening Day 1 Brengop email did not clear unread LED or unlock Quarters Door.");
            }

            // Render live GPU frame of open Brengop email (exercises PixelText em-dash path)
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            quartersScreen.Terminal.Close();

            // Clicking Quarters Door now routes cleanly via Corridor -> Desk
            quartersScreen.Update(0.016f, new VirtualMouseState
            {
                X = 16 + 40,
                Y = 360 + 100,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (screenManager.CurrentScreenId != ScreenId.Corridor ||
                screenManager.NextCorridorDestination != ScreenId.Desk)
            {
                throw new InvalidOperationException("Quarters Door failed to transition to Corridor -> Desk after reading Brengop email.");
            }

            // 2. Day >= 2 backward compatibility: CopyFrom & AdvanceToDay(2) default Day1BrengopEmailRead & Day1VorstHailCompleted to true
            GameState legacyDay2 = new() { Day = 2, Day1BrengopEmailRead = false, Day1VorstHailCompleted = false, SuppressDiskPersistence = true };
            GameState loadedCopy = new() { SuppressDiskPersistence = true };
            loadedCopy.CopyFrom(legacyDay2);
            if (!loadedCopy.Day1BrengopEmailRead || !loadedCopy.Day1VorstHailCompleted)
                throw new InvalidOperationException("CopyFrom on Day >= 2 save failed to default Day1BrengopEmailRead / Day1VorstHailCompleted to true.");

            GameState jumpState = new() { SuppressDiskPersistence = true };
            jumpState.ResetForNewSave(1);
            jumpState.AdvanceToDay(2);
            if (!jumpState.Day1BrengopEmailRead || !jumpState.Day1VorstHailCompleted || jumpState.UnreadMessageCount != 0)
                throw new InvalidOperationException("AdvanceToDay(2) failed to clear Day 1 Brengop email gate & unread LED.");

            // 3. Day 1 Desk Arrival: Commander Vorst Live Viewscreen Hail (verbatim 8 lines + 32 CPS talking-head pacing)
            if (MathF.Abs(config.TalkingHeadCharsPerSec - 32.0f) > 0.01f ||
                MathF.Abs(config.TalkingHeadLinePauseSeconds - 0.95f) > 0.01f ||
                savedAdvanceMode != TalkingHeadAdvanceMode.AutoAdvance)
            {
                throw new InvalidOperationException(
                    $"Expected TalkingHead config defaults (32 CPS, 0.95s pause, AutoAdvance), got ({config.TalkingHeadCharsPerSec}, {config.TalkingHeadLinePauseSeconds}, {savedAdvanceMode}).");
            }

            gameState.ResetForNewSave(1);
            gameState.Day1BrengopEmailRead = true;
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();

            if (!deskScreen.IsDay1VorstHailActive ||
                !deskScreen.HasPreShiftPortraitTextureLoaded ||
                deskScreen.CanTriggerNextHailNow ||
                deskScreen.ShiftElapsedSeconds > 0.0001f)
            {
                throw new InvalidOperationException("Day 1 Desk arrival did not trigger Commander Vorst hail before shift timer & NEXT HAIL.");
            }

            string[] queuedVorstLines = deskScreen.CommsQueue.EnumerateAllTextIncludingPending().Skip(1).ToArray();
            if (queuedVorstLines.Length != Day2And3ScriptedDialogue.Day1CommanderVorstHailLines.Length)
                throw new InvalidOperationException($"Expected {Day2And3ScriptedDialogue.Day1CommanderVorstHailLines.Length} Commander Vorst lines, got {queuedVorstLines.Length}.");
            for (int i = 0; i < queuedVorstLines.Length; i++)
            {
                if (!string.Equals(queuedVorstLines[i], Day2And3ScriptedDialogue.Day1CommanderVorstHailLines[i], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Commander Vorst line {i + 1} diverged from locked verbatim copy.");
                }
            }

            // Advance 0.25s pre-delay + 1.00s typing -> slower 32 CPS pacing (line 1 still typing, not rushed at 65 CPS)
            deskScreen.Update(0.25f, idle);
            deskScreen.Update(1.00f, idle);
            if (!deskScreen.CommsQueue.IsTypingActiveLine ||
                deskScreen.CommsQueue.ActiveFullText != Day2And3ScriptedDialogue.Day1CommanderVorstHailLines[0] ||
                deskScreen.CommsQueue.ActiveVisibleCharCount is < 24 or > 40)
            {
                throw new InvalidOperationException(
                    $"Talking-head 32 CPS pacing check failed after 1.0s (typing={deskScreen.CommsQueue.IsTypingActiveLine}, visible={deskScreen.CommsQueue.ActiveVisibleCharCount}).");
            }

            // ESC Pause freezes talking-head typewriter cleanly
            int charsBeforePause = deskScreen.CommsQueue.ActiveVisibleCharCount;
            pauseMenu.Open();
            for (int f = 0; f < 15; f++)
            {
                if (!pauseMenu.IsOpen)
                    deskScreen.Update(0.05f, idle);
            }
            if (!deskScreen.CommsQueue.IsTypingActiveLine || deskScreen.CommsQueue.ActiveVisibleCharCount != charsBeforePause)
                throw new InvalidOperationException("ESC Pause Menu failed to freeze active talking-head typewriter.");
            pauseMenu.Close();

            // Finish line 1 -> verify ~0.95s AutoAdvance inter-line pause before line 2 starts
            deskScreen.Update(0.75f, idle);
            if (!deskScreen.CommsQueue.IsWaitingTalkingHeadAutoPause ||
                deskScreen.CommsQueue.TalkingHeadAutoPauseRemaining <= 0.10f ||
                deskScreen.CommsQueue.TalkingHeadAutoPauseRemaining > 0.95f)
            {
                throw new InvalidOperationException(
                    $"TalkingHead AutoAdvance failed to enter ~0.95s inter-line pause (waiting={deskScreen.CommsQueue.IsWaitingTalkingHeadAutoPause}, rem={deskScreen.CommsQueue.TalkingHeadAutoPauseRemaining:0.00}).");
            }

            // Render live Vorst frame (landscape + portrait + transcript)
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            // Advance past 0.95s pause -> line 2 begins typing automatically
            deskScreen.Update(1.00f, idle);
            if (!deskScreen.CommsQueue.IsTypingActiveLine ||
                deskScreen.CommsQueue.ActiveFullText != Day2And3ScriptedDialogue.Day1CommanderVorstHailLines[1])
            {
                throw new InvalidOperationException("TalkingHead AutoAdvance did not advance automatically to Vorst line 2 after pause.");
            }

            // 4. F1 Live Toggle: AutoAdvance -> ClickToAdvance + 2-click line advance + Space/Enter advance
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 40, Y = 40 + 446, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.TalkingHeadAdvanceMode != TalkingHeadAdvanceMode.ClickToAdvance)
                throw new InvalidOperationException("F1 DebugOverlay [TALKING HEADS] button did not toggle AutoAdvance -> ClickToAdvance.");

            // Click inside CRT Comms Log while line 2 is typing -> snaps line 2 to 100% and holds on [CLICK / SPACE]
            DeskRect tr = deskScreen.CurrentLayout.Transcript;
            deskScreen.Update(0.016f, new VirtualMouseState { X = tr.X + 40, Y = tr.Y + 40, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (!deskScreen.CommsQueue.IsWaitingTalkingHeadClickAdvance || deskScreen.CommsQueue.IsTypingActiveLine)
                throw new InvalidOperationException("ClickToAdvance first click did not snap line 2 and wait for click/space advance.");

            // Render frame showing pulsing [CLICK / SPACE]
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            // Must stay paused indefinitely until clicked or Space/Enter pressed
            deskScreen.Update(2.00f, idle);
            if (!deskScreen.CommsQueue.IsWaitingTalkingHeadClickAdvance)
                throw new InvalidOperationException("ClickToAdvance illegally auto-advanced without user input.");

            // Second click inside CRT Comms Log advances to line 3
            deskScreen.Update(0.016f, new VirtualMouseState { X = tr.X + 40, Y = tr.Y + 40, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (deskScreen.CommsQueue.IsWaitingTalkingHeadClickAdvance ||
                !deskScreen.CommsQueue.IsTypingActiveLine ||
                deskScreen.CommsQueue.ActiveFullText != Day2And3ScriptedDialogue.Day1CommanderVorstHailLines[2])
            {
                throw new InvalidOperationException("ClickToAdvance second click did not advance to Vorst line 3.");
            }

            // Keyboard Space/Enter path snaps line 3 -> holds -> advances to line 4 (with em-dash)
            deskScreen.SmokeTriggerPreShiftAdvanceKey();
            if (!deskScreen.CommsQueue.IsWaitingTalkingHeadClickAdvance)
                throw new InvalidOperationException("Space/Enter key while typing did not snap line 3 to click-hold.");
            deskScreen.SmokeTriggerPreShiftAdvanceKey();
            if (!deskScreen.CommsQueue.IsTypingActiveLine ||
                deskScreen.CommsQueue.ActiveFullText != Day2And3ScriptedDialogue.Day1CommanderVorstHailLines[3])
            {
                throw new InvalidOperationException("Space/Enter key on click-hold did not advance to Vorst line 4.");
            }

            // Render line 4 (contains '\u2014' em-dash) on GPU
            deskScreen.SmokeTriggerPreShiftAdvanceKey();
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            // Flush remaining Vorst lines & acknowledge via CommsStrip button -> unlocks NEXT HAIL & unloads portrait texture
            VirtualMouseState stripBtnClick = new()
            {
                X = deskScreen.CurrentLayout.CommsStrip.X + 40,
                Y = deskScreen.CurrentLayout.CommsStrip.Y + 12,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            };
            deskScreen.Update(0.016f, stripBtnClick);
            if (deskScreen.CommsQueue.HasPendingOrActive ||
                !Day2And3ScriptedDialogue.Day1CommanderVorstHailLines.All(
                    line => deskScreen.CommsQueue.CommittedLines.Any(c => c.Text == line)))
            {
                throw new InvalidOperationException("Vorst hail skip/flush did not commit all 8 verbatim lines.");
            }

            deskScreen.Update(0.016f, stripBtnClick);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.IsDay1VorstHailActive ||
                !gameState.Day1VorstHailCompleted ||
                deskScreen.HasPreShiftPortraitTextureLoaded ||
                !deskScreen.CanTriggerNextHailNow ||
                deskScreen.ShiftElapsedSeconds > 0.0001f)
            {
                throw new InvalidOperationException("Acknowledging Commander Vorst hail failed to unload portrait or unlock NEXT HAIL.");
            }

            // 5. Verify Day 4 Kordani Officer hail also streams with TalkingHead pacing & unloads cleanly
            config.TalkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance;
            gameState.GhostRouteSuspectDetained = true;
            gameState.Day4PreShiftHailCompleted = false;
            gameState.AdvanceToDay(4);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsDay3KordaniHailActive || !deskScreen.HasPreShiftPortraitTextureLoaded)
                throw new InvalidOperationException("Day 4 Kordani Live Hail did not trigger.");
            deskScreen.Update(0.25f, idle);
            deskScreen.Update(0.50f, idle);
            if (!deskScreen.CommsQueue.IsTypingActiveLine || deskScreen.CommsQueue.ActiveVisibleCharCount > 32)
                throw new InvalidOperationException("Day 4 Kordani Live Hail did not stream at slower talking-head CPS.");
            deskScreen.Update(0.016f, stripBtnClick);
            deskScreen.Update(0.016f, stripBtnClick);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.HasPreShiftPortraitTextureLoaded || !deskScreen.CanTriggerNextHailNow)
                throw new InvalidOperationException("Day 4 Kordani Live Hail failed to unload portrait cleanly on acknowledge.");

            // 6. Verify 1,000 SC IndependentPassageFundTarget config default & live F1 Dev Economy Stepper
            if (Constants.IndependentPassageFundTarget != 1000 || savedBuyoutTarget != 1000)
                throw new InvalidOperationException($"Expected IndependentPassageFundTarget == 1000, got constant={Constants.IndependentPassageFundTarget}, config={savedBuyoutTarget}.");

            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 244, Y = 40 + 446, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.IndependentPassageFundTarget != 900)
                throw new InvalidOperationException($"F1 Dev Buyout '<' stepper expected 900 SC, got {config.IndependentPassageFundTarget}.");
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 440, Y = 40 + 446, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.IndependentPassageFundTarget != 1000)
                throw new InvalidOperationException($"F1 Dev Buyout '>' stepper expected 1000 SC, got {config.IndependentPassageFundTarget}.");
            debugOverlay.IsVisible = false;
        }
        finally
        {
            pauseMenu.Close();
            debugOverlay.IsVisible = false;
            tutorialOverlay.ForceCloseSilent();
            config.TalkingHeadAdvanceMode = savedAdvanceMode;
            config.IndependentPassageFundTarget = savedBuyoutTarget;
            devDebug.AnimationSpeedPresetIndex = savedAnimIdx;
        }
    }

    private static void RunBuild435VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
        string? prevSaveDir = GameState.SaveDirectoryOverride;
        bool prevSuppressDisk = gameState.SuppressDiskPersistence;
        string tempSaveDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke435_{Guid.NewGuid():N}");

        try
        {
            // 1. DeskScreen Build 4.3.5 hardening checks (OnExit contraband recovery, 1-99% Blank Slate abort + Re-Transmit,
            // topmost document click priority over Row 2, OptionA FreeDeskRestMinY clamping, Clock-Out drag cleanup,
            // and premature Day 3 Protocol Review target gating)
            deskScreen.SmokeVerifyBuild435DeskHardening(screenManager);

            // 2. CovertMissionDatabase in-memory caching (zero per-frame disk reads on CRYPTO / LOGS and DECK 4)
            _ = CovertMissionDatabase.Load();
            int loadsBefore = CovertMissionDatabase.DiskLoadCount;
            gameState.AdvanceToDay(7);
            if (!gameState.TerminalHackedUnlocked)
                throw new InvalidOperationException("AdvanceToDay(7) failed to backfill TerminalHackedUnlocked = true.");

            screenManager.SwitchTo(ScreenId.Quarters);
            tutorialOverlay.ForceCloseSilent();
            quartersScreen.SmokeStageTerminal(tabIndex: 3);
            for (int f = 0; f < 20; f++)
            {
                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
            }

            quartersScreen.SmokeStageTerminal(tabIndex: 4);
            for (int f = 0; f < 20; f++)
            {
                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
            }

            if (CovertMissionDatabase.DiskLoadCount != loadsBefore)
            {
                throw new InvalidOperationException(
                    $"CovertMissionDatabase read from disk {CovertMissionDatabase.DiskLoadCount - loadsBefore} extra time(s) during terminal rendering.");
            }

            _ = CovertMissionDatabase.Reload();
            if (CovertMissionDatabase.DiskLoadCount != loadsBefore + 1)
                throw new InvalidOperationException("CovertMissionDatabase.Reload() did not increment DiskLoadCount by exactly 1.");

            quartersScreen.SmokeCloseSubOverlays();

            // 3. Atomic SaveToDisk() + F2-F5 DebugOverlay & MainMenu save-slot protection (isolated temp save dir)
            Directory.CreateDirectory(tempSaveDir);
            GameState.SaveDirectoryOverride = tempSaveDir;
            gameState.SuppressDiskPersistence = false;
            gameState.ResetForNewSave(1);
            gameState.PactScrip = 435;
            gameState.SaveToDisk();

            string slot1Path = GameState.GetSlotFilePath(1);
            if (!File.Exists(slot1Path) || Directory.GetFiles(tempSaveDir, "*.tmp").Length != 0)
                throw new InvalidOperationException("Initial atomic SaveToDisk() did not produce clean save_slot_1.json without leftover .tmp files.");

            gameState.PactScrip = 777;
            gameState.SaveToDisk();
            if (Directory.GetFiles(tempSaveDir, "*.tmp").Length != 0)
                throw new InvalidOperationException("Atomic SaveToDisk() overwrite left .tmp file(s) behind.");
            if (!gameState.LoadSlotIntoCurrent(1) || gameState.PactScrip != 777)
                throw new InvalidOperationException("Atomic SaveToDisk() overwrite failed round-trip LoadSlotIntoCurrent(1).");

            string slot1SnapshotBeforeMainMenu = File.ReadAllText(slot1Path);

            screenManager.SwitchTo(ScreenId.MainMenu);
            debugOverlay.IsVisible = false;
            if (debugOverlay.TryHandleScreenJumpKey(KeyboardKey.F3) || screenManager.CurrentScreenId != ScreenId.MainMenu)
                throw new InvalidOperationException("F3 screen jump fired while DebugOverlay.IsVisible == false.");

            debugOverlay.IsVisible = true;
            if (!debugOverlay.TryHandleScreenJumpKey(KeyboardKey.F3) || screenManager.CurrentScreenId != ScreenId.Desk)
                throw new InvalidOperationException("F3 screen jump did not navigate to Desk when DebugOverlay.IsVisible == true.");
            if (!gameState.SuppressDiskPersistence || gameState.ActiveSaveSlot != 0 || gameState.HasActivePlayableSlot)
                throw new InvalidOperationException("Jumping from MainMenu via F3 did not sandbox GameState against save overwrite.");

            gameState.PactScrip = 9999;
            gameState.SaveToDisk();
            if (File.ReadAllText(slot1Path) != slot1SnapshotBeforeMainMenu)
                throw new InvalidOperationException("Sandboxed jump from MainMenu mutated real save_slot_1.json on disk!");

            debugOverlay.IsVisible = false;
            GameState.SaveDirectoryOverride = prevSaveDir;
            gameState.SuppressDiskPersistence = true;

            // 4. Day 6 CHECK YOUR CHUTE repeat LOAD DAY 06 AM, unread-chute door gate & Day 6 security log fail-safe
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(6);
            if (gameState.TerminalHackedUnlocked)
                throw new InvalidOperationException("AdvanceToDay(6) must NOT pre-unlock TerminalHackedUnlocked.");

            gameState.AdvanceToDay(7);
            if (!gameState.TerminalHackedUnlocked)
                throw new InvalidOperationException("AdvanceToDay(7) must backfill TerminalHackedUnlocked = true.");

            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 260, Y = 40 + 400, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            debugOverlay.IsVisible = false;
            tutorialOverlay.ForceCloseSilent();

            quartersScreen.SmokeStageTerminal(tabIndex: 0);
            _ = quartersScreen.Terminal.SmokeClickEmailById("morning_roster_day_06");
            if (!quartersScreen.Terminal.SmokeClickEmailById(EconomyEngine.Day6HackEmailId))
                throw new InvalidOperationException("First LOAD DAY 06 AM failed to deliver CHECK YOUR CHUTE email.");
            quartersScreen.Update(1.10f, idle);
            quartersScreen.SmokeCloseSubOverlays();
            if (!gameState.TerminalHackedUnlocked || quartersScreen.IsPhysicalTerminalUnreadLedActive)
                throw new InvalidOperationException("Reading first CHECK YOUR CHUTE did not unlock hacked tabs / clear unread LED.");

            // Repeat LOAD DAY 06 AM must clear previous read hack email & chute drop and re-trigger unread email + LED + chute slate
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 260, Y = 40 + 400, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            debugOverlay.IsVisible = false;
            tutorialOverlay.ForceCloseSilent();
            if (gameState.TerminalHackedUnlocked ||
                !quartersScreen.IsPhysicalTerminalUnreadLedActive ||
                gameState.ServiceChuteItems.Count != 1)
            {
                throw new InvalidOperationException("Repeat LOAD DAY 06 AM failed to reset TerminalHackedUnlocked and re-seed unread CHECK YOUR CHUTE + Chute item.");
            }

            // Taking the Day 6 chute Blank Data Slate without reading "Check your chute" blocks door exit with unread email nudge
            ContrabandPocketItem d6Blank = gameState.ServiceChuteItems[0];
            gameState.ServiceChuteItems.Clear();
            gameState.PocketItems.Add(d6Blank);
            if (!quartersScreen.IsDoorGatedByDay6UnreadChuteDrop)
                throw new InvalidOperationException("IsDoorGatedByDay6UnreadChuteDrop did not trigger when Day 6 hack email was unread.");

            quartersScreen.Update(0.016f, new VirtualMouseState { X = 80, Y = 500, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (screenManager.CurrentScreenId != ScreenId.Quarters ||
                quartersScreen.ActiveNudgeText != "Should probably read my emails first.")
            {
                throw new InvalidOperationException($"Quarters door did not block Day 6 unread email with expected prompt, got '{quartersScreen.ActiveNudgeText}'.");
            }

            // Fail-safe auto-unlock if a suspicious security log is recorded on Day 6+ before TerminalHackedUnlocked
            gameState.AddSecurityLog("[UNAUTH_IO]", "SMOKE_PORT_SWAP_FAILSAFE", isSuspicious: true);
            if (!gameState.TerminalHackedUnlocked)
                throw new InvalidOperationException("Day 6+ AddSecurityLog(isSuspicious: true) fail-safe did not unlock TerminalHackedUnlocked.");

            // 5. Centralized ConfigFlags unlock days (28-day schedule) & custom ShipGenerator(customConfig) gating
            if (config.DayUnlockWarpScanner != 4 ||
                config.DayUnlockManualPart1 != 8 ||
                config.DayUnlockManualHulls != 10 ||
                config.DayUnlockWaferCargo != 12 ||
                config.DayUnlockStoker != 15 ||
                config.DayUnlockFaceBribes != 18 ||
                config.DayUnlockCargoBioScan != 21 ||
                config.DayUnlockHostilityDestroy != 22 ||
                config.DayUnlockTransitPass != 24 ||
                config.DayPactClimax != 26 ||
                config.CampaignFinalDay != 28)
            {
                throw new InvalidOperationException("ConfigFlags default DayUnlock thresholds do not match Build 4.5 28-day spec.");
            }

            Core.ConfigFlags customCfg = new()
            {
                DayUnlockManualPart1 = 10,
                DayUnlockManualHulls = 10,
                DayUnlockWaferCargo = 10
            };
            ShipGenerator customGen = new(customCfg);
            GameState day5ProbeState = new() { Day = 5, RunSeed = 435 };
            for (int q = 0; q < 24; q++)
            {
                ShipSpec spec = customGen.GenerateShipForSlot(
                    day5ProbeState,
                    q,
                    reseedNonce: (uint)q,
                    forceValidity: DebugSpawnValidityMode.ForceInvalid,
                    config: customCfg);
                if (spec.GroundTruth.ActiveInfractions.Any(inf => inf.Type is
                    ShipInfractionType.PrefixOrganizationMismatch or
                    ShipInfractionType.ExpiredRegistration or
                    ShipInfractionType.PrefixSpeciesMismatch or
                    ShipInfractionType.PrefixHullSilhouetteMismatch or
                    ShipInfractionType.UnexplainedCargoPowerDraw))
                {
                    throw new InvalidOperationException(
                        $"Custom ConfigFlags DayUnlock thresholds leaked a locked Day 10 infraction on Day 5 (q={q}).");
                }
            }

            // 6. Data hygiene: forum.json & placeholders_todo.json
            ForumDatabase forumDb = ForumDatabase.Load();
            if (forumDb.Threads.Any(t =>
                t.Title.Contains("Stim-Caff", StringComparison.OrdinalIgnoreCase) ||
                t.Posts.Any(p => p.Body.Contains("Stim-Caff", StringComparison.OrdinalIgnoreCase))))
            {
                throw new InvalidOperationException("data/forum.json still contains stale Stim-Caff reference.");
            }
            ForumThreadModel? deck4Rumor = forumDb.Threads.FirstOrDefault(t => t.Id == "d2_lower_deck_rumors");
            if (deck4Rumor == null || deck4Rumor.MinDay != 6)
                throw new InvalidOperationException($"Expected d2_lower_deck_rumors MinDay == 6, got {deck4Rumor?.MinDay}.");

            string todoJson = File.ReadAllText(Path.Combine(DataLoader.GetDataDirectory(), "placeholders_todo.json"));
            if (!todoJson.Contains("1,000 SC", StringComparison.Ordinal) || todoJson.Contains("600 SC", StringComparison.Ordinal))
                throw new InvalidOperationException("data/placeholders_todo.json did not update 600 SC -> 1,000 SC cleanly.");

            // 7. Latent CrudeCrayon Holo-Portrait ordering fix + balanced GPU texture disposal
            DataSlateModel cleanSlate = new()
            {
                PortraitSeedId = "smoke435-crayon-holo",
                HoloPortraitSpecies = PortraitSpecies.Prunjun,
                DeclaredPrefix = "FTV-",
                CrudeCrayon = false
            };
            DataSlateModel crayonSlate = new()
            {
                PortraitSeedId = "smoke435-crayon-holo",
                HoloPortraitSpecies = PortraitSpecies.Prunjun,
                DeclaredPrefix = "FTV-",
                CrudeCrayon = true
            };
            PixelBuffer cleanHolo = PortraitRenderer.RenderBuffer(cleanSlate.BuildHoloPortraitOptions());
            PixelBuffer crayonHolo = PortraitRenderer.RenderBuffer(crayonSlate.BuildHoloPortraitOptions());
            if (CountDifferingPixels(cleanHolo, crayonHolo) < 20)
                throw new InvalidOperationException("PortraitRenderer did not apply CrudeCrayon overlay in PortraitRenderMode.Holo.");

            Texture2D holoTex = crayonHolo.ToTexture2D();
            PixelBuffer.UnloadTexture(ref holoTex);
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            GameState.SaveDirectoryOverride = prevSaveDir;
            gameState.SuppressDiskPersistence = prevSuppressDisk;
            if (Directory.Exists(tempSaveDir))
            {
                try { Directory.Delete(tempSaveDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild436VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
        bool prevSuppress = gameState.SuppressDiskPersistence;
        gameState.SuppressDiskPersistence = true;

        try
        {
            // 1. Verify CovertMissionDatabase & ConfigFlags loaded from data/config.json match Build 4.5 defaults
            CovertMissionDatabase missionDb = CovertMissionDatabase.Reload();
            Core.ConfigFlags diskConfig = Core.ConfigFlags.Load();
            if (diskConfig.ShiftDurationDay1 != 210 ||
                diskConfig.ShiftDurationDays2To5 != 240 ||
                diskConfig.ShiftDurationDays6To11 != 255 ||
                diskConfig.ShiftDurationDays12To17 != 270 ||
                diskConfig.ShiftDurationDays18Plus != 285 ||
                MathF.Abs(diskConfig.ShieldsUpBaseChance - 0.38f) > 0.001f ||
                MathF.Abs(diskConfig.InvalidShipRateEarly - 0.38f) > 0.001f ||
                MathF.Abs(diskConfig.InvalidShipRateMid - 0.45f) > 0.001f ||
                diskConfig.DayUnlockWarpScanner != 4 ||
                diskConfig.DayUnlockManualPart1 != 8 ||
                diskConfig.DayUnlockManualHulls != 10 ||
                diskConfig.DayUnlockWaferCargo != 12 ||
                diskConfig.DayUnlockStoker != 15 ||
                diskConfig.DayUnlockFaceBribes != 18 ||
                diskConfig.DayUnlockCargoBioScan != 21 ||
                diskConfig.DayUnlockHostilityDestroy != 22 ||
                diskConfig.DayUnlockTransitPass != 24 ||
                diskConfig.DayPactClimax != 26 ||
                diskConfig.CampaignFinalDay != 28 ||
                diskConfig.PocketUpgrade2MinDay != 6 ||
                diskConfig.PocketUpgrade2Cost != 60 ||
                diskConfig.PocketUpgrade3MinDay != 15 ||
                diskConfig.PocketUpgrade3Cost != 120)
            {
                throw new InvalidOperationException("data/config.json did not load Build 4.5 variable shift clock, rates & 28-day spaced defaults.");
            }

            // 2. Assert ConfigFlags.GetShiftDurationSeconds across all 5 campaign stages
            if (config.GetShiftDurationSeconds(1) != 210 ||
                config.GetShiftDurationSeconds(2) != 240 ||
                config.GetShiftDurationSeconds(3) != 240 ||
                config.GetShiftDurationSeconds(5) != 240 ||
                config.GetShiftDurationSeconds(6) != 255 ||
                config.GetShiftDurationSeconds(9) != 255 ||
                config.GetShiftDurationSeconds(11) != 255 ||
                config.GetShiftDurationSeconds(12) != 270 ||
                config.GetShiftDurationSeconds(17) != 270 ||
                config.GetShiftDurationSeconds(18) != 285 ||
                config.GetShiftDurationSeconds(26) != 285 ||
                config.GetShiftDurationSeconds(28) != 285)
            {
                throw new InvalidOperationException("ConfigFlags.GetShiftDurationSeconds returned incorrect Build 4.5 stage duration(s).");
            }

            // 3. Assert ShiftController & DeskScreen active duration + BrownDwarfArcProgress scaling (Day 1 = 210s, Day 26 = 285s)
            gameState.ResetForNewSave(1);
            gameState.Day1BrengopEmailRead = true;
            gameState.Day1VorstHailCompleted = true;
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (MathF.Abs(deskScreen.ActiveShiftDurationSeconds - 210f) > 0.01f)
                throw new InvalidOperationException($"Expected Day 1 DeskScreen ActiveShiftDurationSeconds == 210, got {deskScreen.ActiveShiftDurationSeconds}.");

            deskScreen.SmokeSetShiftElapsedSeconds(105f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 0.5f) > 0.001f || deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 1 BrownDwarfArcProgress expected 0.50 (unexpired) at 105s / 210s.");
            deskScreen.SmokeSetShiftElapsedSeconds(210f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 1.0f) > 0.001f || !deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 1 BrownDwarfArcProgress expected 1.00 (expired) at 210s / 210s.");

            gameState.AdvanceToDay(26);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (MathF.Abs(deskScreen.ActiveShiftDurationSeconds - 285f) > 0.01f)
                throw new InvalidOperationException($"Expected Day 26 DeskScreen ActiveShiftDurationSeconds == 285, got {deskScreen.ActiveShiftDurationSeconds}.");

            deskScreen.SmokeSetShiftElapsedSeconds(142.5f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 0.5f) > 0.001f || deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 26 BrownDwarfArcProgress expected 0.50 (unexpired) at 142.5s / 285s.");
            deskScreen.SmokeSetShiftElapsedSeconds(285f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 1.0f) > 0.001f || !deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 26 BrownDwarfArcProgress expected 1.00 (expired) at 285s / 285s.");

            // 4. Assert Days 5 & 7 Breathers (zero Manual infractions, Manual locked, zero REFERENCE MANUAL text in directive)
            ShipGenerator gen = new(config);
            gameState.ResetForNewSave(1);
            foreach (int breatherDay in new[] { 5, 7 })
            {
                gameState.AdvanceToDay(breatherDay);
                screenManager.SwitchTo(ScreenId.Desk);
                tutorialOverlay.ForceCloseSilent();
                if (deskScreen.IsManualUnlocked)
                    throw new InvalidOperationException($"Day {breatherDay} breather shift must NOT unlock Kordani Reference Manual Tablet.");
                string[] breatherDir = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                    breatherDay, gameState.GhostRouteSuspectDetained, gameState.Day2SuspectRegistration, gameState.Day2SuspectDeclaredOrigin, config);
                if (breatherDir.Any(l => l.Contains("REFERENCE MANUAL", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Day {breatherDay} Shift Directive must not announce Kordani Reference Manual.");

                for (int i = 0; i < 80; i++)
                {
                    ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: (uint)(430 + breatherDay), DebugSpawnValidityMode.ForceInvalid, config: config);
                    if (s.GroundTruth.ActiveInfractions.Any(inf => inf.Type is
                        ShipInfractionType.ExpiredRegistration or
                        ShipInfractionType.PrefixOrganizationMismatch or
                        ShipInfractionType.PrefixSpeciesMismatch or
                        ShipInfractionType.PrefixHullSilhouetteMismatch or
                        ShipInfractionType.UnexplainedCargoPowerDraw))
                    {
                        throw new InvalidOperationException($"Day {breatherDay} breather generated disallowed Manual/Cargo infraction: {s.GroundTruth.ActiveInfractions[0].Type}");
                    }
                }
            }

            // 5. Assert Day 8 Manual Part 1 (Expired, Org, Species; NO Hull Silhouettes)
            gameState.AdvanceToDay(8);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsManualUnlocked || deskScreen.IsManualHullsEnforced)
                throw new InvalidOperationException("Day 8 must unlock Manual Part 1 without enforcing Manual Part 2 hulls.");
            HashSet<ShipInfractionType> seenD8 = [];
            for (int i = 0; i < 180; i++)
            {
                ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: 808u, DebugSpawnValidityMode.ForceInvalid, config: config);
                ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                seenD8.Add(t);
                if (t is ShipInfractionType.PrefixHullSilhouetteMismatch or ShipInfractionType.UnexplainedCargoPowerDraw)
                    throw new InvalidOperationException($"Day 8 generated disallowed post-Day-8 infraction: {t}");
            }
            if (!seenD8.Contains(ShipInfractionType.ExpiredRegistration) ||
                !seenD8.Contains(ShipInfractionType.PrefixOrganizationMismatch) ||
                !seenD8.Contains(ShipInfractionType.PrefixSpeciesMismatch))
            {
                throw new InvalidOperationException("Day 8 failed to generate Manual Part 1 infractions.");
            }

            // 6. Assert Day 6 Pinned Hack + Blank Data Slate + MNG- Intro + Pocket Upgrade 2 (60 SC) + No Hulls/Manual Yet
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(6);
            gameState.QuartersPhase = QuartersPhase.Morning;
            EconomyEngine econ = new();
            econ.EnsureMorningRosterGenerated(gameState, gen, config);
            econ.EnsureDay6MorningUnsolicitedDrop(gameState);
            if (!gameState.Emails.Any(e => e.IsTerminalHackEmail && e.Sender == "unknown" && e.Subject == "Check your chute" && !e.IsRead) ||
                !gameState.ServiceChuteItems.Any(i => i.ItemType == ContrabandPocketItem.TypeBlankDataSlate))
            {
                throw new InvalidOperationException("Day 6 pinned Check your chute email or free 0500 Blank Data Slate drop failed.");
            }

            screenManager.SwitchTo(ScreenId.Quarters);
            tutorialOverlay.ForceCloseSilent();
            quartersScreen.Terminal.Open(0);
            if (!quartersScreen.Terminal.SmokeClickEmailById(EconomyEngine.Day6HackEmailId))
                throw new InvalidOperationException("Failed to open CHECK YOUR CHUTE on Day 6 Morning.");
            quartersScreen.Update(1.10f, idle);
            if (!gameState.TerminalHackedUnlocked ||
                !quartersScreen.Terminal.IsTabAccessible(3) ||
                !quartersScreen.Terminal.IsTabAccessible(4) ||
                !gameState.ActiveInterceptMissionIds.Contains(EconomyEngine.Day6IntroMissionId))
            {
                throw new InvalidOperationException("Day 6 pinned hack reveal failed to unlock CRYPTO/LOGS, DECK 4, or day6_mng_intro.");
            }
            quartersScreen.SmokeCloseSubOverlays();

            gameState.PactScrip = 100;
            if (!econ.TryPurchasePocketCapacityUpgrade(state: gameState, config: config).Success || gameState.PocketCapacity != 2)
                throw new InvalidOperationException("Day 6 Pocket Upgrade 2 (60 SC) failed.");

            List<ShipSpec> d6Queue = gen.GenerateDailyQueue(gameState, Constants.PreGeneratedQueueSize, config: config);
            if (!d6Queue.Any(s => s.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)) ||
                !gameState.TodayMorningRoster.Any(r => r.IsRealArrival && r.Prefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Day 6 failed to guarantee MNG- in queue and Morning Ship Roster.");
            }

            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.IsManualUnlocked || deskScreen.IsManualHullsEnforced)
                throw new InvalidOperationException("Day 6 must NOT unlock Manual Part 1 or Manual Part 2 Hull Silhouettes.");

            // 7. Assert Day 10 Manual Part 2 (Hull Silhouette + Ship Class; no Cargo Power Draw yet)
            gameState.AdvanceToDay(10);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsManualHullsEnforced || deskScreen.IsCargoPowerUnlocked)
                throw new InvalidOperationException("Day 10 must enforce Manual Part 2 Hull Silhouettes without unlocking Day 12 Cargo.");
            HashSet<ShipInfractionType> seenD10 = [];
            for (int i = 0; i < 180; i++)
            {
                ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: 1010u, DebugSpawnValidityMode.ForceInvalid, config: config);
                ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                seenD10.Add(t);
                if (t == ShipInfractionType.UnexplainedCargoPowerDraw)
                    throw new InvalidOperationException("Day 10 leaked Day 12+ UnexplainedCargoPowerDraw.");
            }
            if (!seenD10.Contains(ShipInfractionType.PrefixHullSilhouetteMismatch))
                throw new InvalidOperationException("Day 10 failed to generate Manual Part 2 PrefixHullSilhouetteMismatch.");

            // 8. Verify Covert Delivery missions shifted to Day 12+
            CovertMissionSpec? op01 = missionDb.Missions.FirstOrDefault(m => m.MissionId == "slate_bus_mission_01");
            CovertMissionSpec? op02 = missionDb.Missions.FirstOrDefault(m => m.MissionId == "slate_bus_mission_02");
            if (op01 == null || op01.MinAvailableDay < 12 || op01.TriggerEmailDay < 12 ||
                op02 == null || op02.MinAvailableDay < 12 || op02.TriggerEmailDay < 12)
            {
                throw new InvalidOperationException("OP-01 / OP-02 Covert Delivery missions must trigger starting Day 12+.");
            }

            // 9. Assert F1 DebugOverlay Day Jump Range 1..28 & Telemetry Bar Duration Readout
            gameState.AdvanceToDay(1);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();

            debugOverlay.IsVisible = false;
            debugOverlay.IsVisible = true;
            // Click '< DAY' to ensure clamping at 1, then click 'DAY >' 35 times to verify 28 ceiling
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 20, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            for (int step = 0; step < 35; step++)
            {
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            }
            if (debugOverlay.SelectedJumpDay != 28)
                throw new InvalidOperationException($"F1 DebugOverlay DAY > jumper expected ceiling 28, got {debugOverlay.SelectedJumpDay}.");

            // Click 'LOAD DAY 28' -> jumps cleanly to Day 28 with 285s shift duration
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 150, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            tutorialOverlay.ForceCloseSilent();
            if (gameState.Day != 28 || MathF.Abs(deskScreen.ActiveShiftDurationSeconds - 285f) > 0.01f)
                throw new InvalidOperationException($"LOAD DAY 28 failed (day={gameState.Day}, shift={deskScreen.ActiveShiftDurationSeconds}s).");

            string telemetryLine = debugOverlay.BuildPrimaryTelemetryLine(idle);
            if (!telemetryLine.Contains("D28", StringComparison.Ordinal) ||
                !telemetryLine.Contains("285s/4m45s", StringComparison.Ordinal) ||
                UiText.MeasureWidth(telemetryLine, 10) > 452)
            {
                throw new InvalidOperationException($"F1 DebugOverlay telemetry bar failed duration or width check: '{telemetryLine}'.");
            }

            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            debugOverlay.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            debugOverlay.IsVisible = false;
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            screenManager.UnloadAllScreens();
            gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    private static void CaptureVisualVerificationFrames(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        ArtTestScreen artTestScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug,
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

        // 2. Day 1 Pre-Shift Commander Vorst Live Hail & Day 1 Tutorial Popup (triggered after acknowledging Vorst hail)
        gameState.ResetForNewSave(1);
        gameState.Day1BrengopEmailRead = true;
        screenManager.SwitchTo(ScreenId.Desk);
        for (int i = 0; i < 6; i++)
        {
            deskScreen.Update(1.0f, idleMouse);
        }
        RenderAndSave("02_day1_vorst_live_hail.png");

        VirtualMouseState commsStripClick = new()
        {
            X = deskScreen.CurrentLayout.CommsStrip.X + 40,
            Y = deskScreen.CurrentLayout.CommsStrip.Y + 12,
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        };
        if (deskScreen.CommsQueue.HasPendingOrActive)
        {
            deskScreen.Update(0.016f, commsStripClick);
        }
        deskScreen.Update(0.016f, commsStripClick);
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

        // 7. Day 4 Live Kordani Officer Viewscreen Hail
        gameState.GhostRouteSuspectDetained = true;
        gameState.Day4PreShiftHailCompleted = false;
        gameState.AdvanceToDay(4);
        screenManager.SwitchTo(ScreenId.Desk);
        for (int i = 0; i < 6; i++)
        {
            deskScreen.Update(1.0f, idleMouse);
        }
        RenderAndSave("06_day3_kordani_live_hail.png");

        // 8. Tightened Manual (Tab 1: 2. HULLS, zero PING RELAY) + Warp Checker RED MISMATCH BLEED
        gameState.AdvanceToDay(config.DayUnlockManualPart1);
        screenManager.SwitchTo(ScreenId.Desk);
        tutorialOverlay.ForceCloseSilent();
        deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false);
        deskScreen.SmokeSetManualTab(1);
        deskScreen.SmokeStageWarpStripOnChecker(refSystemIndex: 0, forcedActualOrigin: "Ashen Rift"); // Veridia ref vs Ashen Rift strip -> vivid RED bleed!
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("07_day3_manual_hulls_and_warp_mismatch.png");

        // 9. Day 4 Tightened Manual (Tab 0: 1. PREFIXES, zero UNR- & zero PING RELAY) + Warp Checker MATCH
        deskScreen.SmokeSetManualTab(0);
        deskScreen.SmokeStageWarpStripOnChecker(refSystemIndex: 5, forcedActualOrigin: "Ashen Rift"); // Ashen Rift ref vs Ashen Rift strip -> warm amber/white glow!
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("08_day3_manual_prefixes_and_warp_match.png");

        // 10. Day 4 Tutorial Popup
        gameState.AdvanceToDay(4);
        tutorialOverlay.OnDay3Start(force: true);
        RenderAndSave("09_day4_tutorial_popup.png");
        tutorialOverlay.ForceCloseSilent();

        // =====================================================================
        // Prompt 4 Visual Verification Captures (1280x720 Exact 8px Grid)
        // =====================================================================

        // 11. Day 1 Morning Quarters (Crimson Brown Dwarf, blinking terminal LED, clean hover-only labels, bottom Coat Pocket tray)
        gameState.ResetForNewSave(1);
        screenManager.SwitchTo(ScreenId.Quarters);
        tutorialOverlay.ForceCloseSilent();
        quartersScreen.Update(0.10f, idleMouse);
        RenderAndSave("10_day1_morning_quarters.png");

        // 11a. Day 1 Mandatory Brengop Contract #884-D email opened in MESSAGES
        quartersScreen.Terminal.Open(initialTab: 0);
        quartersScreen.Terminal.SmokeClickEmailById(TerminalEmail.Day1BrengopEmailId);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("10a_day1_brengop_email.png");
        quartersScreen.SmokeCloseSubOverlays();

        // 11b. Hovering Personal Terminal hotspot shows concise hover-only tooltip & clean highlight
        VirtualMouseState termHoverMouse = new() { X = 320, Y = 400, IsInsideViewport = true, Scale = 1.0f };
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(termHoverMouse);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "10b_morning_quarters_hover_terminal.png"));

        // 12. Clock-Out End-of-Shift Ledger centered (360, 128, 560, 464) over dimmed Desk
        gameState.AdvanceToDay(4);
        gameState.Day4PreShiftHailCompleted = true;
        screenManager.SwitchTo(ScreenId.Desk);
        tutorialOverlay.ForceCloseSilent();
        gameState.ShipsProcessedToday = 6;
        gameState.ShiftGrossPayEarned = 60;
        gameState.ClericalWarningsThisShift = 3;
        gameState.ShiftClericalFinesPaid = 10;
        gameState.SecurityStrikes = 1;
        gameState.PactScrip = 85;
        deskScreen.SmokeStageClockOutLedger();
        deskScreen.Update(0.5f, idleMouse);
        RenderAndSave("11_clock_out_ledger.png");

        // 13. Evening Quarters (Day 7+ with Hack Unlocked for visual capture of all 5 terminal tabs)
        gameState.AdvanceToDay(7);
        gameState.TerminalHackedUnlocked = true;
        gameState.QuartersPhase = QuartersPhase.Evening;
        gameState.TonightRationOrder = NightlyRationOrder.HighTierRations;
        gameState.StokerOverloadedToday = true;
        gameState.NextDayPowerBrownout = true;
        gameState.PactScrip = 140;
        gameState.PocketCapacity = 2;
        gameState.ActiveInterceptMissionIds.Add(EconomyEngine.Day6IntroMissionId);
        gameState.ServiceChuteItems.Add(new ContrabandPocketItem
        {
            ItemId = "vis_blank_1",
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "UNMARKED BLACK DATA SLATE"
        });
        (string d6VisTargetReg, _) = ShipGenerator.EnsureDay6IntroTargetAssigned(gameState);
        gameState.PocketItems.Add(new ContrabandPocketItem
        {
            ItemId = "vis_mng_stolen",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = $"STOLEN SLATE: {d6VisTargetReg}",
            TargetShipPrefix = "MNG-",
            TargetShipRegistration = d6VisTargetReg,
            OriginStarSystem = "Oruun-4"
        });
        gameState.VentStashItems.Add(new ContrabandPocketItem
        {
            ItemId = "vis_copied_slate_2",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = "STOLEN SLATE: ORN-442-K",
            TargetShipPrefix = "ORN-",
            TargetShipRegistration = "ORN-442-K",
            OriginStarSystem = "Ashen Rift",
            IsConcordOrDoppleFlagged = true
        });
        gameState.AddSecurityLog("[UNAUTH_IO]", $"SLATE_BUS PORT SWAP: {d6VisTargetReg} (MNG-)", isSuspicious: true);
        gameState.AddSecurityLog("[AUDIT_FLAG]", $"MISSION TX -> RELAY_D4: {d6VisTargetReg}", isSuspicious: true);

        EconomyEngine visEcon = new();
        ShipGenerator visGen = new();
        visEcon.EnsureMorningRosterGenerated(gameState, visGen);
        visEcon.DeliverScheduledEmailsForPhase(gameState, 7, QuartersPhase.Evening);

        screenManager.SwitchTo(ScreenId.Quarters);
        tutorialOverlay.ForceCloseSilent();
        quartersScreen.Update(0.25f, idleMouse);
        RenderAndSave("12_evening_quarters_stoker_flicker.png");

        // 14. Personal Terminal: Tab 0 MESSAGES (blank standby on open, then explicitly selected)
        quartersScreen.SmokeStageTerminal(tabIndex: 0, selectFirstEmail: false);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("13_terminal_messages_blank_standby.png");

        // 15. Personal Terminal: Tab 1 FORUM (90s/2000s web forum with Stoker overload thread)
        quartersScreen.SmokeStageTerminal(tabIndex: 1);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("14_terminal_forum.png");

        // 16. Personal Terminal: Tab 3 CRYPTO / LOGS (Missions-Only Direct [TRANSMIT] Dispatch)
        quartersScreen.SmokeStageTerminal(tabIndex: 3);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("15_terminal_crypto_logs_missions.png");

        // 17. Personal Terminal: Tab 2 PACT REQ (Brengop yellow-on-dark corporate SKU ledger)
        quartersScreen.SmokeStageTerminal(tabIndex: 2);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("16_terminal_pact_req.png");

        // 18. Personal Terminal: Tab 4 DECK 4 (Contracts & Drops + Option A 50 SC Strike Clearing)
        quartersScreen.SmokeStageTerminal(tabIndex: 4);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("17_terminal_deck4.png");

        // 19. Sleep-Triggered Kordani Berth Search Audit Modal
        gameState.SecurityStrikes = config.MaxStrikesBeforeAudit;
        quartersScreen.SmokeStageBerthAudit(BerthAuditKind.ThreeStrikeBerthAudit);
        quartersScreen.Update(0.05f, idleMouse);
        RenderAndSave("18_kordani_berth_audit.png");
        quartersScreen.SmokeCloseSubOverlays();

        // 20. Dynamic 21:9 Walk-to-Work Corridor (with Stoker Brownout + Kordani Patrol visual variations)
        gameState.EndingFlags["Fail_SecurityDetainment"] = false;
        gameState.SecurityStrikes = 2;
        gameState.NextDayPowerBrownout = true;
        screenManager.NextCorridorDestination = ScreenId.Desk;
        screenManager.SwitchTo(ScreenId.Corridor);
        screenManager.Update(0.80f, idleMouse);
        RenderAndSave("19_corridor_stoker_brownout.png");

        // 21. Desk with Contraband Lap Pocket peeking items & Blank Data Slate Port-Swap in Slate Bus
        gameState.Day3PreShiftHailCompleted = true;
        screenManager.SwitchTo(ScreenId.Desk);
        tutorialOverlay.ForceCloseSilent();
        deskScreen.SmokeStageBlankSlatePortSwap();
        deskScreen.Update(0.05f, idleMouse);
        RenderAndSave("20_desk_blank_slate_port_swap.png");

        // 22. Build 4.3.1: Blank Data Slate mid-1.80s download animation (RX PACKET 55%, no premature red flash!)
        deskScreen.SmokeStageBlankSlateDownloading(0.55f);
        RenderAndSave("21_desk_blank_slate_downloading.png");

        // 23. Build 4.3.1: Steady crimson right-angle circuit trace line on PROTOCOL VIOLATED
        deskScreen.SmokeStageCrimsonFaultTrace();
        RenderAndSave("22_desk_crimson_fault_trace.png");

        // 24. Build 4.3.1: Burnt / Cracked single-use Covert Mission Slate in Slate Bus
        deskScreen.SmokeStageBurntCovertSlate();
        RenderAndSave("23_desk_burnt_covert_slate.png");

        // 25. Build 4.3.1: Quiet powered-down Booth 04 (POWER LED 0%, crimson Brown Dwarf wash, plain-text leave prompt)
        deskScreen.SmokeStageClockOutQuietLeavePrompt();
        RenderAndSave("24_desk_shutdown_leave_prompt.png");

        // 26. Build 4.3.1: Days 1-5 clean 3-tab Official Terminal (CRYPTO/LOGS & DECK 4 completely hidden)
        gameState.ResetForNewSave(1);
        gameState.AdvanceToDay(3);
        gameState.QuartersPhase = QuartersPhase.Morning;
        screenManager.SwitchTo(ScreenId.Quarters);
        tutorialOverlay.ForceCloseSilent();
        quartersScreen.SmokeStageTerminal(tabIndex: 0, selectFirstEmail: false);
        RenderAndSave("25_terminal_days1_5_three_tabs.png");

        // 27. Build 4.3.1: Day 6 CHECK YOUR CHUTE Glitch / Hijack Reveal mid-animation
        gameState.ResetForNewSave(1);
        gameState.AdvanceToDay(6);
        gameState.QuartersPhase = QuartersPhase.Morning;
        screenManager.SwitchTo(ScreenId.Quarters);
        tutorialOverlay.ForceCloseSilent();
        quartersScreen.Terminal.Open(0);
        quartersScreen.Terminal.SmokeClickEmailById(EconomyEngine.Day6HackEmailId);
        quartersScreen.Update(0.62f, idleMouse);
        RenderAndSave("26_terminal_day6_hack_glitch.png");
        quartersScreen.SmokeCloseSubOverlays();

        // =====================================================================
        // Build 4.3.2 Visual Verification Captures: A / B1 / B2 + 3x/2x Portrait + Typewriter [RECEIVING...]
        // =====================================================================
        DeskUpperLayoutMode prevMode = config.DeskUpperLayoutMode;
        bool prev3x = devDebug.Use3xPortraitInLayoutB;
        gameState.ResetForNewSave(1);
        gameState.AdvanceToDay(3);
        gameState.Day3PreShiftHailCompleted = true;
        screenManager.SwitchTo(ScreenId.Desk);
        tutorialOverlay.ForceCloseSilent();

        // 27. Option A (Classic) with docked slate + crimson fault trace
        config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
        deskScreen.SyncLayoutFromConfig();
        deskScreen.SmokeStageCrimsonFaultTrace();
        RenderAndSave("27_desk_layout_A_classic.png");

        // 28. Option B1 (Comms Swap, Classic Lower-Right) with 3x (192px) portrait
        config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB1_CommsSwap;
        devDebug.Use3xPortraitInLayoutB = true;
        deskScreen.SyncLayoutFromConfig();
        deskScreen.SmokeStageDockedClearanceSlate(0.60f);
        RenderAndSave("28_desk_layout_B1_3x.png");

        // 29a. Option B2 (Comms Swap + Widened Flush Lower-Right, Default) with 3x (192px) portrait & clamped Clearance Dock
        config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
        devDebug.Use3xPortraitInLayoutB = true;
        deskScreen.SyncLayoutFromConfig();
        deskScreen.SmokeStageDockedClearanceSlate(0.65f);
        RenderAndSave("29a_desk_layout_B2_flush_3x.png");

        // 29b. Option B2 with 2x (128px) framed viewscreen bezel + LIVE FEED status bar
        devDebug.Use3xPortraitInLayoutB = false;
        deskScreen.SyncLayoutFromConfig();
        RenderAndSave("29b_desk_layout_B2_flush_2x.png");

        // 30a. Option B2 with crimson PROTOCOL VIOLATED trace anchored to (1052, 264, 220, 88)
        devDebug.Use3xPortraitInLayoutB = true;
        deskScreen.SyncLayoutFromConfig();
        deskScreen.SmokeStageCrimsonFaultTrace();
        RenderAndSave("30a_desk_layout_B2_crimson_trace.png");

        // 30b. Option B2 with active [RECEIVING...] typewriter beat
        deskScreen.SmokeStageTypewriterReceivingBeat();
        RenderAndSave("30b_desk_layout_B2_receiving_typewriter.png");

        // 31. F1 Dev Debug Overlay showing live DESK: A / B1 / B2 & PORTRAIT (B): 3x (192px) controls
        debugOverlay.IsVisible = true;
        RenderAndSave("31_f1_debug_layout_switcher.png");
        debugOverlay.IsVisible = false;

        // 32. Build 4.3.3: Quarters Room with Glowing Right-Wall Star Chart Unit & Hover Tooltip
        screenManager.SwitchTo(ScreenId.Quarters);
        tutorialOverlay.ForceCloseSilent();
        quartersScreen.SmokeCloseSubOverlays();
        VirtualMouseState starChartHoverMouse = new()
        {
            X = QuartersScreen.StarChartX + QuartersScreen.StarChartW / 2,
            Y = QuartersScreen.StarChartY + QuartersScreen.StarChartH / 2,
            IsInsideViewport = true,
            Scale = 1.0f
        };
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(starChartHoverMouse);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "32_quarters_star_chart_wall_unit.png"));

        // 33a. Build 4.3.3: Interactive Star Chart Modal with Gharok (Krushvak) selected & VG-3 hovered
        quartersScreen.SmokeStageStarChart(selectedNodeId: "n15", selectedRouteId: null);
        var (vg3X, vg3Y) = quartersScreen.StarChart.GetNodeScreenPosition("n11");
        VirtualMouseState modalNodeHover = new() { X = vg3X, Y = vg3Y, IsInsideViewport = true, Scale = 1.0f };
        quartersScreen.Update(0.05f, modalNodeHover);
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(modalNodeHover);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "33a_star_chart_modal_node_selected.png"));

        // 33b. Build 4.3.3: Interactive Star Chart Modal with Trade Route (Gharok <-> VG-3) selected
        quartersScreen.SmokeStageStarChart(selectedNodeId: null, selectedRouteId: "r47");
        var (rMidX, rMidY) = quartersScreen.StarChart.GetRouteMidpointScreen("r50");
        VirtualMouseState modalRouteHover = new() { X = rMidX, Y = rMidY, IsInsideViewport = true, Scale = 1.0f };
        quartersScreen.Update(0.05f, modalRouteHover);
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(modalRouteHover);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "33b_star_chart_modal_route_selected.png"));
        quartersScreen.SmokeCloseSubOverlays();

        // 34. ShipRendererV2 Visual Verification Captures (Desk V2 Live/SPS/Clamps + F1 Switcher + ArtTest V2 Single & 4-Seed Compare)
        ShipArtVersion prevArt = config.ShipArtVersion;
        config.ShipArtVersion = ShipArtVersion.V2_HiBit;
        gameState.ResetForNewSave(1);
        gameState.AdvanceToDay(10);
        screenManager.SwitchTo(ScreenId.Desk);
        tutorialOverlay.ForceCloseSilent();

        (int v2WpnX, int v2WpnY) = deskScreen.SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
        debugOverlay.IsVisible = true;
        canvas.BeginVirtualDraw();
        screenManager.DrawVirtual(new VirtualMouseState { X = v2WpnX, Y = v2WpnY, IsInsideViewport = true, Scale = 1.0f });
        debugOverlay.DrawVirtual(idleMouse);
        canvas.EndVirtualDrawAndBlit();
        canvas.ExportVirtualFramePng(Path.Combine(outDir, "34a_desk_v2_sps_and_f1.png"));
        debugOverlay.IsVisible = false;

        deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false, triggerDetainClamps: true);
        deskScreen.Update(0.02f, idleMouse);
        RenderAndSave("34b_desk_v2_detain_clamps.png");

        screenManager.SwitchTo(ScreenId.ArtTest);
        artTestScreen.SmokeSetFourSeedCompare(false);
        RenderAndSave("35a_art_test_v2_single.png");
        artTestScreen.SmokeSetFourSeedCompare(true);
        RenderAndSave("35b_art_test_v2_4seed_compare.png");
        artTestScreen.SmokeSetFourSeedCompare(false);
        config.ShipArtVersion = prevArt;

        config.DeskUpperLayoutMode = prevMode;
        devDebug.Use3xPortraitInLayoutB = prev3x;
        deskScreen.SyncLayoutFromConfig();
    }

    private static void RunShipRendererV2VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        ArtTestScreen artTestScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config)
    {
        ShipArtVersion savedArtVersion = config.ShipArtVersion;
        bool prevSuppress = gameState.SuppressDiskPersistence;
        gameState.SuppressDiskPersistence = true;
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };

        try
        {
            // 1. Default config must stay V2_HiBit
            Core.ConfigFlags diskConfig = Core.ConfigFlags.Load();
            if (diskConfig.ShipArtVersion != ShipArtVersion.V2_HiBit)
            {
                throw new InvalidOperationException($"Expected default data/config.json shipArtVersion == V2_HiBit, got {diskConfig.ShipArtVersion}.");
            }

            // 2. Verify ShipRendererV2 across all 11 factions & every allowed class (Live, SPS, Shielded SPS, Manual, Manual Tablet)
            foreach (ShipFaction faction in ShipRendererV2.AllFactions)
            {
                foreach (ShipClass allowedClass in ShipRendererV2.GetAllowedClasses(faction))
                {
                    ShipRenderOptions[] cases =
                    [
                        new ShipRenderOptions
                        {
                            SeedId = $"verify-v2-seedA-{faction}-{allowedClass}",
                            Faction = faction,
                            RequestedClass = allowedClass,
                            Mode = ShipRenderMode.Live,
                            SpsEnabled = false,
                            WeaponsHot = true,
                            IsDecommissioned = true,
                            HasDownstreamHullScorch = true
                        },
                        new ShipRenderOptions
                        {
                            SeedId = $"verify-v2-seedA-{faction}-{allowedClass}",
                            Faction = faction,
                            RequestedClass = allowedClass,
                            Mode = ShipRenderMode.Live,
                            SpsEnabled = true,
                            WeaponsHot = true,
                            CargoHighDraw = true,
                            UncannyWarpPylonPulse = true,
                            ShieldsUp = false
                        },
                        new ShipRenderOptions
                        {
                            SeedId = $"verify-v2-seedA-{faction}-{allowedClass}",
                            Faction = faction,
                            RequestedClass = allowedClass,
                            Mode = ShipRenderMode.Live,
                            SpsEnabled = true,
                            ShieldsUp = true
                        },
                        new ShipRenderOptions
                        {
                            SeedId = $"verify-v2-seedA-{faction}-{allowedClass}",
                            Faction = faction,
                            RequestedClass = allowedClass,
                            Mode = ShipRenderMode.Manual
                        }
                    ];

                    PixelBuffer? liveSeedABuf = null;
                    ShipHardpointLayoutV2 liveSeedAHp = default;

                    for (int c = 0; c < cases.Length; c++)
                    {
                        PixelBuffer buf = ShipRendererV2.RenderBuffer(cases[c], out ShipHardpointLayoutV2 hp);
                        if (buf.Width != ShipRendererV2.OutputWidth || buf.Height != ShipRendererV2.OutputHeight)
                            throw new InvalidOperationException($"ShipRendererV2 expected 560x240 buffer, got {buf.Width}x{buf.Height}.");

                        int solidCount = CountNonNullPixels(buf);
                        if (solidCount < 1800)
                            throw new InvalidOperationException($"ShipRendererV2 produced near-empty buffer ({solidCount} px) for {faction} {allowedClass} case {c}.");

                        if (cases[c].Mode != ShipRenderMode.Manual && (buf.HasPixel(0, 0) || buf.HasPixel(buf.Width - 1, 0)))
                            throw new InvalidOperationException($"ShipRendererV2 leaked non-transparent corner pixel for {faction} {allowedClass}.");

                        VerifyExact2x2Uniformity(buf, $"{faction} {allowedClass} case {c}");
                        VerifyV2HardpointsInViewportBounds(hp, $"{faction} {allowedClass}");

                        Texture2D tex = buf.ToTexture2D();
                        PixelBuffer.UnloadTexture(ref tex);

                        if (c == 0)
                        {
                            liveSeedABuf = buf;
                            liveSeedAHp = hp;
                        }
                    }

                    // Distinct seeds of same class must differ visibly while preserving macro silhouette dimensions
                    PixelBuffer liveSeedBBuf = ShipRendererV2.RenderBuffer(new ShipRenderOptions
                    {
                        SeedId = $"verify-v2-seedB7-{faction}-{allowedClass}",
                        Faction = faction,
                        RequestedClass = allowedClass,
                        Mode = ShipRenderMode.Live
                    }, out ShipHardpointLayoutV2 liveSeedBHp);

                    VerifyExact2x2Uniformity(liveSeedBBuf, $"{faction} {allowedClass} seedB7");
                    int diffPx = CountDifferingPixels(liveSeedABuf!, liveSeedBBuf);
                    if (diffPx < 80)
                        throw new InvalidOperationException($"ShipRendererV2 seed variety too low for {faction} {allowedClass}: {diffPx} differing px.");

                    if (Math.Abs(liveSeedAHp.HullBounds.W - liveSeedBHp.HullBounds.W) > 18 ||
                        Math.Abs(liveSeedAHp.HullBounds.H - liveSeedBHp.HullBounds.H) > 18)
                    {
                        throw new InvalidOperationException($"ShipRendererV2 macro bounds drifted across seeds for {faction} {allowedClass}.");
                    }

                    // Manual tablet 120x56 sampling
                    PixelBuffer tabletBuf = ShipRendererV2.RenderManualTablet120x56Buffer(faction, allowedClass, $"manual-chart-{faction}-{allowedClass}");
                    if (tabletBuf.Width != Constants.ShipWidth || tabletBuf.Height != Constants.ShipHeight)
                        throw new InvalidOperationException($"RenderManualTablet120x56Buffer expected 120x56, got {tabletBuf.Width}x{tabletBuf.Height}.");
                    Texture2D tabTex = tabletBuf.ToTexture2D();
                    PixelBuffer.UnloadTexture(ref tabTex);
                }
            }

            // 3. Live mid-encounter V1 <-> V2 toggle in DeskScreen (Day 12 Cargo unlock) via F1 DebugOverlay + SPS & Detain verification
            config.ShipArtVersion = ShipArtVersion.V1_Classic;
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(config.DayUnlockWaferCargo);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();

            deskScreen.SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
            var v1Sizes = deskScreen.SmokeGetActiveShipTextureSizes();
            if (v1Sizes != (120, 56, 120, 56))
                throw new InvalidOperationException($"Expected V1 DeskScreen ship textures 120x56, got {v1Sizes}.");

            int activeTexBeforeToggle = PixelBuffer.ActiveProceduralTextureCount;

            // Click F1 DebugOverlay [SHIPS: V1/V2] button at (16 + 400, 40 + 422)
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 400, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.ShipArtVersion != ShipArtVersion.V2_HiBit)
                throw new InvalidOperationException("F1 DebugOverlay [SHIPS] button did not switch V1_Classic -> V2_HiBit.");
            debugOverlay.IsVisible = false;

            deskScreen.Update(0.016f, idle);
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            var v2Sizes = deskScreen.SmokeGetActiveShipTextureSizes();
            if (v2Sizes != (560, 240, 560, 240))
                throw new InvalidOperationException($"Live toggle V1->V2 did not regenerate 560x240 ship textures: got {v2Sizes}.");
            if (PixelBuffer.ActiveProceduralTextureCount != activeTexBeforeToggle)
                throw new InvalidOperationException($"Texture leak or mismatch during live V1->V2 toggle: before={activeTexBeforeToggle}, after={PixelBuffer.ActiveProceduralTextureCount}.");

            // Verify SPS hold-to-lock on V2 Spoofed Hot Weapons & Violet Cargo Draw
            (int wpnX, int wpnY) = deskScreen.SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: true);
            if (!deskScreen.SmokeHasWeaponsHotSpsLog() || wpnX is < 162 or > 510 || wpnY is < 77 or > 219)
                throw new InvalidOperationException($"V2 SPS weapons lock failed or landed outside reachable lens bounds: ({wpnX},{wpnY}).");

            if (!deskScreen.SmokeStageAndLockV2CargoPower(out int cargoX, out int cargoY) || cargoX is < 162 or > 510 || cargoY is < 77 or > 219)
                throw new InvalidOperationException($"V2 SPS cargo lock failed or landed outside reachable lens bounds: ({cargoX},{cargoY}).");

            // Render V2 Detain docking clamps & Manual Tab 2 (HULLS)
            deskScreen.SmokeStageShipLoadedAndInspecting(enableSps: false, triggerDetainClamps: true);
            deskScreen.SmokeSetManualTab(2);
            if (deskScreen.SmokeGetManualTextureSize() != (120, 56))
                throw new InvalidOperationException($"Expected V2 Manual Tab 2 texture 120x56, got {deskScreen.SmokeGetManualTextureSize()}.");
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            // Toggle V2 -> V1 back mid-encounter cleanly
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 400, Y = 40 + 422, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.ShipArtVersion != ShipArtVersion.V1_Classic)
                throw new InvalidOperationException("F1 DebugOverlay [SHIPS] button did not switch V2_HiBit -> V1_Classic.");
            debugOverlay.IsVisible = false;
            deskScreen.Update(0.016f, idle);
            if (deskScreen.SmokeGetActiveShipTextureSizes() != (120, 56, 120, 56))
                throw new InvalidOperationException("Live toggle V2->V1 did not restore 120x56 ship textures.");

            // 4. Verify ArtTestScreen V1/V2 switcher & 4-seed ship compare grid cleanup
            screenManager.SwitchTo(ScreenId.ArtTest);
            artTestScreen.SmokeToggleShipArtVersion(); // V1 -> V2
            var singleV2 = artTestScreen.SmokeGetShipTextureMetrics();
            if (singleV2.SingleW != 560 || singleV2.SingleH != 240)
                throw new InvalidOperationException($"ArtTestScreen V2 single ship expected 560x240, got {singleV2.SingleW}x{singleV2.SingleH}.");

            artTestScreen.SmokeSetFourSeedCompare(true);
            var cmpV2 = artTestScreen.SmokeGetShipTextureMetrics();
            if (cmpV2.CompareShipCount != 4 || cmpV2.CompareShipW != 280 || cmpV2.CompareShipH != 120)
                throw new InvalidOperationException($"ArtTestScreen V2 4-seed compare expected 4x 280x120, got {cmpV2}.");

            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            artTestScreen.SmokeToggleShipArtVersion(); // V2 -> V1
            var cmpV1 = artTestScreen.SmokeGetShipTextureMetrics();
            if (cmpV1.CompareShipCount != 4 || cmpV1.CompareShipW != 120 || cmpV1.CompareShipH != 56)
                throw new InvalidOperationException($"ArtTestScreen V1 4-seed compare expected 4x 120x56, got {cmpV1}.");

            artTestScreen.SmokeSetFourSeedCompare(false);
        }
        finally
        {
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            config.ShipArtVersion = savedArtVersion;
            screenManager.UnloadAllScreens();
            gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    private static int CountNonNullPixels(PixelBuffer buf)
    {
        int count = 0;
        for (int y = 0; y < buf.Height; y++)
        {
            for (int x = 0; x < buf.Width; x++)
            {
                if (buf.HasPixel(x, y))
                    count++;
            }
        }
        return count;
    }

    private static void VerifyExact2x2Uniformity(PixelBuffer buf, string label)
    {
        for (int y = 0; y < buf.Height; y += 2)
        {
            for (int x = 0; x < buf.Width; x += 2)
            {
                Color? c00 = buf.GetRgba(x, y);
                Color? c10 = buf.GetRgba(x + 1, y);
                Color? c01 = buf.GetRgba(x, y + 1);
                Color? c11 = buf.GetRgba(x + 1, y + 1);
                if (!AreRgbaEqual(c00, c10) || !AreRgbaEqual(c00, c01) || !AreRgbaEqual(c00, c11))
                {
                    throw new InvalidOperationException($"Mixel detected at ({x},{y}) in {label}.");
                }
            }
        }
    }

    private static bool AreRgbaEqual(Color? a, Color? b)
    {
        if (!a.HasValue && !b.HasValue)
            return true;
        if (!a.HasValue || !b.HasValue)
            return false;
        Color ca = a.Value;
        Color cb = b.Value;
        return ca.R == cb.R && ca.G == cb.G && ca.B == cb.B && ca.A == cb.A;
    }

    private static void VerifyV2HardpointsInViewportBounds(ShipHardpointLayoutV2 hp, string label)
    {
        HardpointRect[] rects =
        [
            hp.WeaponsBayViewport560,
            hp.CargoBayViewport560,
            hp.BridgeCommsViewport560,
            hp.UpperWarpPylonViewport560,
            hp.HullBoundsViewport560
        ];

        foreach (HardpointRect r in rects)
        {
            if (r.IsEmpty || r.X < 0 || r.Y < 0 || r.Right > ShipRendererV2.OutputWidth || r.Bottom > ShipRendererV2.OutputHeight)
            {
                throw new InvalidOperationException($"Out-of-bounds 560x240 V2 rect {r} for {label}.");
            }
        }

        // Verify SPS lock centers sit inside DeskScreen's reachable swaying reticle center window (162..510, 77..219) when anchored at (56, 22)
        (int X, int Y)[] lockCenters =
        [
            (56 + hp.WeaponsLockCenter560.X, 22 + hp.WeaponsLockCenter560.Y),
            (56 + hp.CargoLockCenter560.X, 22 + hp.CargoLockCenter560.Y),
            (56 + hp.BridgeCommsViewport560.CenterX, 22 + hp.BridgeCommsViewport560.CenterY),
            (56 + hp.UpperWarpPylonViewport560.CenterX, 22 + hp.UpperWarpPylonViewport560.CenterY)
        ];

        foreach (var (sx, sy) in lockCenters)
        {
            if (sx is < 162 or > 510 || sy is < 77 or > 219)
            {
                throw new InvalidOperationException($"V2 SPS lock center ({sx},{sy}) falls outside DeskScreen reachable reticle window for {label}.");
            }
        }

        ClampGrabZone fore560 = hp.ForeClampViewport560;
        ClampGrabZone aft560 = hp.AftClampViewport560;
        if (fore560.X < 0 || fore560.X >= ShipRendererV2.OutputWidth ||
            aft560.X < 0 || aft560.X >= ShipRendererV2.OutputWidth ||
            fore560.TopY < 0 || fore560.BottomY >= ShipRendererV2.OutputHeight ||
            aft560.TopY < 0 || aft560.BottomY >= ShipRendererV2.OutputHeight)
        {
            throw new InvalidOperationException($"Out-of-bounds V2 clamp grab zone for {label}.");
        }
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

    private static void RunBuild45VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        ArtTestScreen artTestScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config)
    {
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
        bool prevSuppress = gameState.SuppressDiskPersistence;
        gameState.SuppressDiskPersistence = true;
        string tempDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke45_{Guid.NewGuid():N}");

        try
        {
            // 1. DeskLayout across OptionA, OptionB1, OptionB2: no LowerShields/PwrDownWpn, NextHail spans CommsStrip (364px)
            foreach (DeskUpperLayoutMode mode in Enum.GetValues<DeskUpperLayoutMode>())
            {
                DeskLayoutRects lr = DeskLayoutRects.Compute(mode, use3xPortraitInLayoutB: true);
                lr.ValidateGeometry();
                if (lr.NextHailBtn != lr.CommsStrip || lr.NextHailBtn.W != 364)
                {
                    throw new InvalidOperationException($"Layout {mode} NextHailBtn did not widen to full CommsStrip (364px).");
                }
            }

            // 2. DeskScreen Auto-Shields-Down (Day 1+) & Auto-SPS Weapons Sweep Compliance (Day 2+)
            deskScreen.SmokeVerifyBuild45AutoShieldsAndWeapons();

            // 3. GameState SecurityStrikesActive, GhostRouteSuspectDetained save compat, and Day 4 authored directives
            GameState s4 = new() { SuppressDiskPersistence = true };
            s4.ResetForNewSave(1);
            if (s4.SecurityStrikesActive)
                throw new InvalidOperationException("Day 1 New Save must initialize SecurityStrikesActive == false.");
            s4.GhostRouteSuspectDetained = true;
            if (!s4.Day2SuspectDetained)
                throw new InvalidOperationException("GhostRouteSuspectDetained / Day2SuspectDetained compatibility sync failed.");
            s4.AdvanceToDay(4);
            if (!s4.SecurityStrikesActive || !s4.QuestFlags["Day4_SpectrometerRepaired"])
                throw new InvalidOperationException("AdvanceToDay(4) failed to set SecurityStrikesActive and Day4_SpectrometerRepaired.");

            string[] varA = Day2And3ScriptedDialogue.BuildDay4ShiftDirectiveLines(false, "ORN-442-K", "Oruun-4");
            string[] varB = Day2And3ScriptedDialogue.BuildDay4ShiftDirectiveLines(true, "ORN-442-K", "Oruun-4");
            if (!varA.Any(l => l.Contains("departing Void Gate 3 on an unlogged vector", StringComparison.Ordinal)) ||
                !varB.Any(l => l.Contains("Kordani boarding teams on detained vessel", StringComparison.Ordinal)) ||
                !varA.Any(l => l.Contains("Maintenance request #4412", StringComparison.Ordinal)) ||
                !varB.Any(l => l.Contains("Maintenance request #4412", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Day 4 Shift Directive Variants A/B diverged from authored copy.");
            }

            // 4. Tunable Invalid-Ship Roll Rates (~38% Days 1-5, ~45% Days 6+) & Day 1+ ShieldsUpBaseChance + RunSeed Parity
            ShipGenerator gen = new(config);
            int earlyInvalidCount = 0;
            int earlyTotal = 0;
            int day1ShieldsUpCount = 0;
            int day1Total = 0;
            int midInvalidCount = 0;
            int midTotal = 0;

            for (int seed = 100; seed < 125; seed++)
            {
                GameState d1Sample = new() { Day = 1, RunSeed = seed, SuppressDiskPersistence = true };
                foreach (ShipSpec s in gen.GenerateDailyQueue(d1Sample, config: config))
                {
                    day1Total++;
                    if (s.GroundTruth.ActualShieldsUp)
                        day1ShieldsUpCount++;
                }

                GameState d2Sample = new() { Day = 2, RunSeed = seed, SuppressDiskPersistence = true };
                foreach (ShipSpec s in gen.GenerateDailyQueue(d2Sample, config: config))
                {
                    earlyTotal++;
                    if (!s.IsValid)
                        earlyInvalidCount++;
                }

                GameState d8Sample = new() { Day = 8, RunSeed = seed, SuppressDiskPersistence = true };
                foreach (ShipSpec s in gen.GenerateDailyQueue(d8Sample, config: config))
                {
                    midTotal++;
                    if (!s.IsValid)
                        midInvalidCount++;
                }
            }

            float earlyRate = earlyInvalidCount / (float)earlyTotal;
            float midRate = midInvalidCount / (float)midTotal;
            float d1ShieldRate = day1ShieldsUpCount / (float)day1Total;
            if (earlyRate < 0.28f || earlyRate > 0.48f)
                throw new InvalidOperationException($"Early (Days 1-5) invalid rate {earlyRate:0.00} outside expected ~0.38 window.");
            if (midRate < 0.35f || midRate > 0.55f)
                throw new InvalidOperationException($"Mid (Days 6+) invalid rate {midRate:0.00} outside expected ~0.45 window.");
            if (d1ShieldRate < 0.24f || d1ShieldRate > 0.52f)
                throw new InvalidOperationException($"Day 1 shields-up rate {d1ShieldRate:0.00} outside expected ~0.38 window.");

            // Deterministic RunSeed parity check
            GameState parityA = new() { Day = 5, RunSeed = 77742, SuppressDiskPersistence = true };
            GameState parityB = new() { Day = 5, RunSeed = 77742, SuppressDiskPersistence = true };
            List<ShipSpec> qA = gen.GenerateDailyQueue(parityA, config: config);
            List<ShipSpec> qB = gen.GenerateDailyQueue(parityB, config: config);
            for (int i = 0; i < qA.Count; i++)
            {
                if (qA[i].Slate.RegistrationCode != qB[i].Slate.RegistrationCode || qA[i].IsValid != qB[i].IsValid)
                    throw new InvalidOperationException($"Deterministic RunSeed queue parity failed at slot {i}.");
            }

            // 5. CorridorSpriteRenderer & data/corridor_sprites.json verification
            CorridorSpritesConfig diskCorridorCfg = CorridorSpritesConfig.Load();
            if (diskCorridorCfg.LockedPlayerCharacter.Seed <= 0 ||
                !diskCorridorCfg.NpcProceduralConfig.ProceduralEnabled ||
                diskCorridorCfg.NpcProceduralConfig.RoleWeights.Count == 0 ||
                diskCorridorCfg.NpcProceduralConfig.AllowedSpecies.Count < 10)
            {
                throw new InvalidOperationException("data/corridor_sprites.json failed default structure check.");
            }

            Directory.CreateDirectory(tempDir);
            string tempJsonPath = Path.Combine(tempDir, "corridor_sprites_test.json");
            CorridorSpritesConfig testCfg = CorridorSpritesConfig.Load();
            testCfg.LockedPlayerCharacter.Seed = 9182;
            if (!testCfg.SaveAtomic(tempJsonPath))
                throw new InvalidOperationException("CorridorSpritesConfig.SaveAtomic failed.");
            if (Directory.GetFiles(tempDir, "*.tmp.*").Length != 0)
                throw new InvalidOperationException("CorridorSpritesConfig.SaveAtomic left lingering .tmp files.");
            CorridorSpritesConfig reloaded = CorridorSpritesConfig.Load(tempJsonPath);
            if (reloaded.LockedPlayerCharacter.Seed != 9182)
                throw new InvalidOperationException($"CorridorSpritesConfig atomic round-trip mismatch: expected 9182, got {reloaded.LockedPlayerCharacter.Seed}.");

            // Check CorridorSpriteRenderer across all 5 roles & 12 species + Idle vs Walk frames + Left/Right facing
            foreach (CorridorSpriteRole role in Enum.GetValues<CorridorSpriteRole>())
            {
                PortraitSpecies roleSpecies = role switch
                {
                    CorridorSpriteRole.KordaniSentry => PortraitSpecies.Kordani,
                    CorridorSpriteRole.BrengopClerk => PortraitSpecies.Brengop,
                    _ => PortraitSpecies.Human
                };

                PixelBuffer idleR = CorridorSpriteRenderer.RenderBuffer(new CorridorSpriteRenderOptions
                {
                    Seed = 4041,
                    Species = roleSpecies,
                    Role = role,
                    Facing = CorridorFacing.Right,
                    AnimMode = CorridorAnimMode.Idle
                });
                PixelBuffer walk0R = CorridorSpriteRenderer.RenderBuffer(new CorridorSpriteRenderOptions
                {
                    Seed = 4041,
                    Species = roleSpecies,
                    Role = role,
                    Facing = CorridorFacing.Right,
                    AnimMode = CorridorAnimMode.Walk,
                    WalkFrame = 0
                });
                PixelBuffer walk2R = CorridorSpriteRenderer.RenderBuffer(new CorridorSpriteRenderOptions
                {
                    Seed = 4041,
                    Species = roleSpecies,
                    Role = role,
                    Facing = CorridorFacing.Right,
                    AnimMode = CorridorAnimMode.Walk,
                    WalkFrame = 2
                });
                PixelBuffer idleL = CorridorSpriteRenderer.RenderBuffer(new CorridorSpriteRenderOptions
                {
                    Seed = 4041,
                    Species = roleSpecies,
                    Role = role,
                    Facing = CorridorFacing.Left,
                    AnimMode = CorridorAnimMode.Idle
                });

                if (idleR.Width != Constants.CorridorSpriteWidth || idleR.Height != Constants.CorridorSpriteHeight ||
                    CountNonNullPixels(idleR) < 60 ||
                    CountDifferingPixels(idleR, walk0R) < 6 ||
                    CountDifferingPixels(walk0R, walk2R) < 6 ||
                    CountDifferingPixels(idleR, idleL) < 10)
                {
                    throw new InvalidOperationException($"CorridorSpriteRenderer frame/facing check failed for role {role}.");
                }
            }

            foreach (PortraitSpecies sp in PortraitRenderer.AllActiveSpecies)
            {
                PixelBuffer spBuf = CorridorSpriteRenderer.RenderBuffer(new CorridorSpriteRenderOptions
                {
                    Seed = 5150,
                    Species = sp,
                    Role = CorridorSpriteRole.OffDutyCaptain,
                    Facing = CorridorFacing.Right,
                    AnimMode = CorridorAnimMode.Idle
                });
                if (CountNonNullPixels(spBuf) < 55)
                    throw new InvalidOperationException($"CorridorSpriteRenderer produced sparse buffer for species {sp}.");
                Texture2D spTex = spBuf.ToTexture2D();
                PixelBuffer.UnloadTexture(ref spTex);
            }

            // 6. ArtTestScreen [CORRIDOR] Tab render & strict VRAM unload check
            screenManager.SwitchTo(ScreenId.ArtTest);
            artTestScreen.SmokeSwitchToCorridorTab();
            artTestScreen.SmokeStepPlayerCandidate(1);
            if (!artTestScreen.SmokeSaveLockedPlayerToPath(tempJsonPath))
                throw new InvalidOperationException("ArtTestScreen [CORRIDOR] tab SaveAtomic failed.");

            var cMetrics = artTestScreen.SmokeGetCorridorTabMetrics();
            if (!cMetrics.HasPlayerTex || cMetrics.NpcCount != 6 || cMetrics.LoadedNpcSprites != 6)
                throw new InvalidOperationException($"ArtTestScreen [CORRIDOR] tab texture metrics mismatch: {cMetrics}.");

            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();

            screenManager.UnloadAllScreens();
            if (PixelBuffer.ActiveProceduralTextureCount != 0)
                throw new InvalidOperationException($"Expected 0 active procedural textures after unloading ArtTestScreen [CORRIDOR], found {PixelBuffer.ActiveProceduralTextureCount}.");
        }
        finally
        {
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            screenManager.UnloadAllScreens();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild46VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        string savedUiFontPreset = config.UiFontPreset;
        string? prevSaveDirOverride = GameState.SaveDirectoryOverride;
        string tempDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke46_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            gameState.SuppressDiskPersistence = true;

            // =================================================================
            // 1. FontManager discovery, preset switching, ConfigFlags.UiFontPreset atomic persistence,
            //    missing-preset fallback, DebugOverlay Row 17 cycler click, headless zero-GPU-font behavior
            // =================================================================
            if (FontManager.IsHeadlessMode != !Raylib.IsWindowReady())
                throw new InvalidOperationException($"FontManager.IsHeadlessMode mismatch with Raylib.IsWindowReady() ({FontManager.IsHeadlessMode}).");

            if (!FontManager.PresetNames.Contains("RaylibDefault") || FontManager.PresetCount < 1)
                throw new InvalidOperationException("FontManager missing RaylibDefault fallback preset.");

            if (FontManager.SnapToBucket(9) != 10 ||
                FontManager.SnapToBucket(11) != 10 ||
                FontManager.SnapToBucket(13) != 12 ||
                FontManager.SnapToBucket(22) != 20 ||
                FontManager.SnapToBucket(40) != 32)
            {
                throw new InvalidOperationException("FontManager.SnapToBucket downward tie-break check failed.");
            }

            // Custom fonts discovery + missing preset fallback in isolated temp dir
            string tempFontsDir = Path.Combine(tempDir, "fonts");
            Directory.CreateDirectory(tempFontsDir);
            string winFontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string consolaSrc = Path.Combine(winFontsDir, "consola.ttf");
            if (File.Exists(consolaSrc))
            {
                File.Copy(consolaSrc, Path.Combine(tempFontsDir, "AlphaCustomMono.ttf"), overwrite: true);
            }

            FontManager.Initialize("NonExistentPreset_XYZ", headlessMode: true, customFontsDirOverride: tempFontsDir);
            if (FontManager.ActiveGpuFontCount != 0)
                throw new InvalidOperationException("Headless FontManager.Initialize loaded GPU fonts.");
            if (File.Exists(consolaSrc) &&
                (!FontManager.PresetNames.Contains("AlphaCustomMono") ||
                 !string.Equals(FontManager.ActivePresetName, "AlphaCustomMono", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Missing-preset fallback should select first custom preset AlphaCustomMono, got {FontManager.ActivePresetName}.");
            }

            if (!FontManager.SetActivePreset("RaylibDefault") || FontManager.ActivePresetName != "RaylibDefault")
                throw new InvalidOperationException("FontManager.SetActivePreset(RaylibDefault) failed.");
            if (FontManager.SetActivePreset("NoSuchFontPreset_404"))
                throw new InvalidOperationException("FontManager.SetActivePreset should return false for unknown presets.");

            // Atomic UiFontPreset persistence on temp config copy
            string tempConfigPath = Path.Combine(tempDir, "config_test.json");
            File.WriteAllText(tempConfigPath, "{\n  \"enableDebugMenu\": true,\n  \"uiFontPreset\": \"Consolas\"\n}\n");
            config.UiFontPreset = "RaylibDefault";
            if (!config.TryPersistUiFontPresetToPath(tempConfigPath))
                throw new InvalidOperationException("ConfigFlags.TryPersistUiFontPresetToPath failed.");
            if (File.Exists(tempConfigPath + ".tmp"))
                throw new InvalidOperationException("TryPersistUiFontPresetToPath left lingering .tmp file.");
            string persistedCfg = File.ReadAllText(tempConfigPath);
            if (!persistedCfg.Contains("\"uiFontPreset\": \"RaylibDefault\"", StringComparison.Ordinal))
                throw new InvalidOperationException($"Atomic config persistence did not update uiFontPreset: {persistedCfg}");

            // Re-initialize normal assets/fonts preset & test DebugOverlay Row 17 click
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            config.UiFontPreset = FontManager.ActivePresetName;

            if (FontManager.PresetCount > 1)
            {
                string beforePreset = FontManager.ActivePresetName;
                debugOverlay.IsVisible = true;
                // Click '>' button at (16 + 440, 40 + 494)
                bool handledNext = debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 440, Y = 40 + 494, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                string afterNext = FontManager.ActivePresetName;
                if (!handledNext || afterNext == beforePreset || config.UiFontPreset != afterNext)
                    throw new InvalidOperationException($"DebugOverlay Row 17 '>' cycler failed: before={beforePreset}, after={afterNext}, cfg={config.UiFontPreset}.");

                // Click center readout at (16 + 200, 40 + 494)
                bool handledCenter = debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 200, Y = 40 + 494, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (!handledCenter || config.UiFontPreset != FontManager.ActivePresetName)
                    throw new InvalidOperationException("DebugOverlay Row 17 center readout click failed.");

                // Click '<' button twice at (16 + 20, 40 + 494) -> returns to beforePreset
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 20, Y = 40 + 494, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 20, Y = 40 + 494, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (FontManager.ActivePresetName != beforePreset)
                    throw new InvalidOperationException($"DebugOverlay Row 17 '<' cycler did not restore {beforePreset}, got {FontManager.ActivePresetName}.");

                // Draw DebugOverlay with Row 17 + VOICE + HISTORY readouts cleanly
                VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(idle);
                debugOverlay.DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
                debugOverlay.IsVisible = false;
            }

            // =================================================================
            // 2. voice_profiles.json all 12 species, AudioHooks, & CommsTypewriterQueue
            // =================================================================
            AudioHooks.Initialize(headlessMode: true);
            if (AudioHooks.IsAudioDeviceInitialized || AudioHooks.ActiveSoundCount != 0 || AudioHooks.ActiveSoundAliasCount != 0)
                throw new InvalidOperationException("Headless AudioHooks.Initialize must leave audio device uninitialized with 0 active sounds/aliases.");
            if (AudioHooks.LoadedSpeciesProfileCount != 12)
                throw new InvalidOperationException($"Expected 12 species profiles loaded from data/voice_profiles.json, found {AudioHooks.LoadedSpeciesProfileCount}.");

            string[] all12Species =
            [
                "Human", "Helani", "Klaasari", "Thane", "Veldor", "Kordani",
                "Brengop", "Krushvak", "HunBorian", "Oruun", "Prunjun", "Tolvak"
            ];
            foreach (string sp in all12Species)
            {
                SpeciesVoiceProfile prof = AudioHooks.GetSpeciesProfile(sp);
                if (string.IsNullOrWhiteSpace(prof.Bank) || prof.PitchMin <= 0f || prof.PitchMax < prof.PitchMin)
                    throw new InvalidOperationException($"Invalid voice profile for species {sp}.");
            }

            SpeciesVoiceProfile helani = AudioHooks.GetSpeciesProfile("Helani");
            if (helani.Bank != "Human_GU" || helani.CooldownMs is < 115 or > 125 || MathF.Abs(helani.Jitter - 0.015f) > 0.002f)
                throw new InvalidOperationException($"Helani voice profile mismatch: bank={helani.Bank}, cooldown={helani.CooldownMs}, jitter={helani.Jitter}.");

            SpeciesVoiceProfile klaasari = AudioHooks.GetSpeciesProfile("Klaasari");
            SpeciesVoiceProfile thane = AudioHooks.GetSpeciesProfile("Thane");
            SpeciesVoiceProfile veldor = AudioHooks.GetSpeciesProfile("Veldor");
            if (klaasari.Bank != "GU_Alien" || thane.Bank != "GU_Alien" || veldor.Bank != "GU_Alien" ||
                !(thane.PitchMax < veldor.PitchMin && veldor.PitchMax < klaasari.PitchMin))
            {
                throw new InvalidOperationException("Klaasari/Thane/Veldor GU_Alien distinct pitch ranges check failed.");
            }

            // Zero-Tell Dopple rule & deterministic encounter base pitch
            AudioHooks.BeginEncounterVoice("Dopple", 4242, useRadioSquelch: true);
            if (AudioHooks.ActivePresentedSpecies != "Human" || AudioHooks.ActiveVoiceBank != "Human_GU")
                throw new InvalidOperationException($"Zero-Tell Dopple rule leaked species: {AudioHooks.ActivePresentedSpecies}/{AudioHooks.ActiveVoiceBank}.");

            AudioHooks.BeginEncounterVoice("Prunjun", 778899, useRadioSquelch: true);
            SpeciesVoiceProfile prunjun = AudioHooks.GetSpeciesProfile("Prunjun");
            float pitch1 = AudioHooks.ActiveEncounterBasePitch;
            AudioHooks.BeginEncounterVoice("Prunjun", 778899, useRadioSquelch: true);
            float pitch2 = AudioHooks.ActiveEncounterBasePitch;
            if (MathF.Abs(pitch1 - pitch2) > 1e-6f || pitch1 < prunjun.PitchMin || pitch1 > prunjun.PitchMax)
                throw new InvalidOperationException($"Encounter base pitch determinism/bounds failed: {pitch1} vs {pitch2}.");

            // Silence on spaces & punctuation (!char.IsLetterOrDigit)
            if (AudioHooks.OnCommsCharRevealed(' ', isOfficer: false, isCaptain: true) ||
                AudioHooks.OnCommsCharRevealed('.', isOfficer: false, isCaptain: true) ||
                AudioHooks.OnCommsCharRevealed('-', isOfficer: true, isCaptain: false))
            {
                throw new InvalidOperationException("AudioHooks.OnCommsCharRevealed must remain silent on spaces and punctuation.");
            }

            // rawDeltaSeconds decoupling from high AnimSpeedMultiplier (10x speed still ticks cooldown at real-time rawDeltaSeconds)
            AudioHooks.BeginEncounterVoice("Human", 555, useRadioSquelch: true);
            AudioHooks.ClearEventHistory();
            CommsTypewriterQueue speedQueue = new();
            speedQueue.EnqueueSpokenLine(new string('A', 200), CommsSpeakerKind.Captain, CommsTextTone.AmberPrimary, suppressAutoReceivingLag: true);
            for (int f = 0; f < 10; f++)
            {
                speedQueue.Update(scaledDeltaSeconds: 0.016f * 10f, charsPerSec: 65f, rawDeltaSeconds: 0.016f);
            }
            int captainBlips = AudioHooks.CommsEvents.Count(e => e.StartsWith("voice_blip:", StringComparison.Ordinal));
            if (captainBlips is < 2 or > 4)
                throw new InvalidOperationException($"Expected real-time ~68ms cooldown (~3 blips in 160ms raw time at 10x anim speed), got {captainBlips}.");

            // Anchor suppression on "Send subspace protocols." and clearing afterward
            AudioHooks.ClearEventHistory();
            CommsTypewriterQueue anchorQueue = new();
            anchorQueue.EnqueueSpokenLine("> BOOTH 04: \"Send subspace protocols.\"", CommsSpeakerKind.Officer, CommsTextTone.ClearanceTeal, instantPrefixLength: 12, suppressAutoReceivingLag: true);
            anchorQueue.EnqueueSpokenLine("> BOOTH 04: \"Hold position.\"", CommsSpeakerKind.Officer, CommsTextTone.ClearanceTeal, instantPrefixLength: 12, suppressAutoReceivingLag: true);
            anchorQueue.Update(1.0f, 65f, rawDeltaSeconds: 1.0f);
            if (!AudioHooks.CommsEvents.Contains("officer_protocols") ||
                AudioHooks.CommsEvents.Count(e => e == "officer_console_blip") == 0 ||
                AudioHooks.IsAnchorBlipSuppressed)
            {
                throw new InvalidOperationException("Officer anchor suppression failed to emit officer_protocols or resume blips on subsequent officer line.");
            }
            // Also verify direct suppression during the anchor line itself
            AudioHooks.OnCommsLineStart(isOfficer: true, isCaptain: false, "> BOOTH 04: \"Send subspace protocols.\"");
            if (!AudioHooks.IsAnchorBlipSuppressed || AudioHooks.OnCommsCharRevealed('S', isOfficer: true, isCaptain: false))
                throw new InvalidOperationException("Officer anchor line failed to suppress officer_console_blip while active.");
            AudioHooks.OnCommsLineEnd(isCaptain: false);
            if (AudioHooks.IsAnchorBlipSuppressed)
                throw new InvalidOperationException("AudioHooks.OnCommsLineEnd failed to clear IsAnchorBlipSuppressed.");

            // squelch_open / squelch_close on Captain lines (including click-to-snap) vs silent squelch on Kordani live hail
            AudioHooks.BeginEncounterVoice("Brengop", 101, useRadioSquelch: true);
            AudioHooks.ClearEventHistory();
            CommsTypewriterQueue squelchQueue = new();
            squelchQueue.EnqueueSpokenLine("CAPT: \"Testing radio squelch open and close.\"", CommsSpeakerKind.Captain, CommsTextTone.AmberPrimary, instantPrefixLength: 6, suppressAutoReceivingLag: true);
            squelchQueue.Update(0.05f, 65f, rawDeltaSeconds: 0.05f);
            squelchQueue.FinishActiveLineImmediately();
            if (!AudioHooks.CommsEvents.Contains("squelch_open") || !AudioHooks.CommsEvents.Contains("squelch_close"))
                throw new InvalidOperationException("Captain radio line click-to-snap failed to emit both squelch_open and squelch_close.");

            AudioHooks.BeginEncounterVoice("Kordani", 202, useRadioSquelch: false);
            AudioHooks.ClearEventHistory();
            AudioHooks.OnCommsLineStart(isOfficer: false, isCaptain: true, "Vorst: \"Hail.\"");
            AudioHooks.OnCommsLineEnd(isCaptain: true);
            if (AudioHooks.CommsEvents.Contains("squelch_open") || AudioHooks.CommsEvents.Contains("squelch_close"))
                throw new InvalidOperationException("useRadioSquelch: false illegally emitted squelch_open/squelch_close.");

            // PickNonRepeatingClipIndex & missing-.wav/headless safety
            Random clipRng = new(42);
            int lastIdx = 1;
            for (int i = 0; i < 40; i++)
            {
                int nextIdx = AudioHooks.PickNonRepeatingClipIndex(4, lastIdx, clipRng);
                if (nextIdx < 0 || nextIdx >= 4 || nextIdx == lastIdx)
                    throw new InvalidOperationException($"PickNonRepeatingClipIndex repeated {lastIdx} or went out of range ({nextIdx}).");
                lastIdx = nextIdx;
            }
            if (AudioHooks.PickNonRepeatingClipIndex(1, 0, clipRng) != 0 ||
                AudioHooks.PickNonRepeatingClipIndex(0, -1, clipRng) != -1)
            {
                throw new InvalidOperationException("PickNonRepeatingClipIndex edge case (0 or 1 clip) failed.");
            }

            // =================================================================
            // 3. EncounterHistory append-once, Day 3 "ghost_route", Day 6 "day6_mng_intro",
            //    atomic save_slot_1.json round-trip, legacy save missing encounterHistory
            // =================================================================
            GameState.SaveDirectoryOverride = tempDir;
            GameState histState = new() { SuppressDiskPersistence = false };
            histState.ResetForNewSave(1);
            ShipGenerator histGen = new(config);
            ValidationEngine histVal = new();
            WorldSim histWorld = new();

            // Day 3 Ghost Route -> Detained -> StoryTag "ghost_route", append-once!
            histState.AdvanceToDay(3);
            int ghostSlot = ShipGenerator.GetGhostRouteQueueSlot(histState);
            ShipSpec ghostShip = histGen.GenerateShipForSlot(histState, ghostSlot, config: config);
            histVal.EvaluateDecision(histState, ghostShip, PlayerDecision.Detain, histWorld, config);
            int countAfterFirstEval = histState.EncounterHistory.Count;
            histVal.EvaluateDecision(histState, ghostShip, PlayerDecision.Detain, histWorld, config);
            if (countAfterFirstEval != 1 || histState.EncounterHistory.Count != 1 ||
                histState.EncounterHistory[0].StoryTag != "ghost_route" ||
                histState.EncounterHistory[0].Verdict != "Detained")
            {
                throw new InvalidOperationException("Day 3 Ghost Route EncounterHistory append-once or StoryTag 'ghost_route' failed.");
            }

            // Day 6 MNG- intro ship -> Cleared -> StoryTag "day6_mng_intro"
            histState.AdvanceToDay(6);
            List<ShipSpec> d6Ships = histGen.GenerateDailyQueue(histState, config: config);
            ShipSpec? mngIntro = d6Ships.FirstOrDefault(s => s.StoryTag == "day6_mng_intro");
            if (mngIntro == null)
                throw new InvalidOperationException("Day 6 daily queue missing StoryTag 'day6_mng_intro' on MNG- vessel.");
            mngIntro.GroundTruth.ActualShieldsUp = false;
            mngIntro.GroundTruth.ActualWeaponsHot = false;
            mngIntro.GroundTruth.ActiveInfractions.Clear();
            histVal.EvaluateDecision(histState, mngIntro, PlayerDecision.GrantAccess, histWorld, config);
            if (histState.EncounterHistory.Count != 2 ||
                histState.EncounterHistory[1].StoryTag != "day6_mng_intro" ||
                histState.EncounterHistory[1].Verdict != "Cleared")
            {
                throw new InvalidOperationException("Day 6 MNG- intro EncounterHistory recording failed.");
            }

            // Atomic save_slot_1.json round-trip
            string slot1Path = GameState.GetSlotFilePath(1);
            if (!File.Exists(slot1Path) || File.Exists(slot1Path + ".tmp"))
                throw new InvalidOperationException("Atomic save_slot_1.json missing or left .tmp file.");
            GameState reloadedHist = new() { SuppressDiskPersistence = true };
            if (!reloadedHist.LoadSlotIntoCurrent(1) || reloadedHist.EncounterHistory.Count != 2 ||
                reloadedHist.EncounterHistory[0].StoryTag != "ghost_route" ||
                reloadedHist.EncounterHistory[1].StoryTag != "day6_mng_intro")
            {
                throw new InvalidOperationException("save_slot_1.json EncounterHistory round-trip failed.");
            }

            // Legacy save missing encounterHistory field loads safely
            string slot2Path = GameState.GetSlotFilePath(2);
            File.WriteAllText(slot2Path, "{\"activeSaveSlot\":2,\"runSeed\":12345,\"day\":2,\"pactScrip\":60}");
            GameState legacyLoaded = new() { SuppressDiskPersistence = true };
            if (!legacyLoaded.LoadSlotIntoCurrent(2) || legacyLoaded.EncounterHistory == null || legacyLoaded.EncounterHistory.Count != 0)
                throw new InvalidOperationException("Legacy save without 'encounterHistory' failed safe load.");

            // =================================================================
            // 4. EncounterHistoryQuery strict [currentDay - 3 .. currentDay - 1] window vs full-campaign StoryTag
            // =================================================================
            List<EncounterRecord> queryRecords =
            [
                new EncounterRecord { Day = 3, Faction = "ORN-", Verdict = "Detained", HadViolation = false, StoryTag = "ghost_route" },
                new EncounterRecord { Day = 6, Faction = "FTV-", Verdict = "Detained", HadViolation = true }, // Day 10 - 4 -> outside window!
                new EncounterRecord { Day = 7, Faction = "FTV-", Verdict = "Detained", HadViolation = true }, // Day 10 - 3 -> inside!
                new EncounterRecord { Day = 8, Faction = "FTV-", Verdict = "Detained", HadViolation = true }, // Day 10 - 2 -> inside!
                new EncounterRecord { Day = 9, Faction = "MNG-", Verdict = "Cleared", HadViolation = true, TookBribe = true }, // Day 10 - 1 -> inside!
                new EncounterRecord { Day = 10, Faction = "FTV-", Verdict = "Detained", HadViolation = true } // currentDay -> excluded!
            ];

            if (EncounterHistoryQuery.CountRecentDetentions(queryRecords, currentDay: 10, "FTV-") != 2 ||
                EncounterHistoryQuery.CountRecentClearedViolations(queryRecords, currentDay: 10, "MNG-") != 1 ||
                !EncounterHistoryQuery.HasRecentBribe(queryRecords, currentDay: 10, "MNG-") ||
                EncounterHistoryQuery.HasRecentBribe(queryRecords, currentDay: 10, "FTV-") ||
                EncounterHistoryQuery.GetMostDetainedRecentFaction(queryRecords, currentDay: 10) != "FTV-" ||
                EncounterHistoryQuery.GetRecentFactionPattern(queryRecords, currentDay: 10, "FTV-") != RecentFactionCallbackKind.RepeatedDetentions ||
                EncounterHistoryQuery.GetRecentFactionPattern(queryRecords, currentDay: 10, "MNG-") != RecentFactionCallbackKind.TookBribe)
            {
                throw new InvalidOperationException("EncounterHistoryQuery strict [currentDay - 3 .. currentDay - 1] rolling window check failed.");
            }

            if (EncounterHistoryQuery.FindByStoryTag(queryRecords, "ghost_route")?.Day != 3 ||
                !EncounterHistoryQuery.HasStoryTagVerdict(queryRecords, "ghost_route", "Detained") ||
                EncounterHistoryQuery.HasStoryTagVerdict(queryRecords, "ghost_route", "Cleared"))
            {
                throw new InvalidOperationException("EncounterHistoryQuery full-campaign StoryTag lookup failed.");
            }

            // =================================================================
            // 5. FactionDialoguePool DiskLoadCount == 1, [TODO] skipping, non-repeat picks, recentCallbacks weaving
            // =================================================================
            FactionDialoguePool.EnsureLoaded();
            FactionDialoguePool.EnsureLoaded();
            if (FactionDialoguePool.DiskLoadCount != 1)
                throw new InvalidOperationException($"Expected FactionDialoguePool.DiskLoadCount == 1, got {FactionDialoguePool.DiskLoadCount}.");

            if (FactionDialoguePool.IsPlayableLine("[TODO: test]") || !FactionDialoguePool.IsPlayableLine("Valid greeting."))
                throw new InvalidOperationException("FactionDialoguePool.IsPlayableLine failed [TODO] skip logic.");

            FactionDialoguePool.ResetSelectionMemory();
            ShipSpec dummyFtvA = new() { EncounterSeedId = "seed-ftv-a", Slate = new DataSlateModel { ShipName = "DRIFTER", RegistrationCode = "FTV-101-A", OriginSystem = "Veridia", DestinationSystem = "Oruun-4" } };
            ShipSpec dummyFtvB = new() { EncounterSeedId = "seed-ftv-a", Slate = new DataSlateModel { ShipName = "DRIFTER", RegistrationCode = "FTV-101-A", OriginSystem = "Veridia", DestinationSystem = "Oruun-4" } };
            string? ftvGreet1 = FactionDialoguePool.TryPickGreeting("FTV-", dummyFtvA);
            string? ftvGreet2 = FactionDialoguePool.TryPickGreeting("FTV-", dummyFtvB);
            if (string.IsNullOrWhiteSpace(ftvGreet1) || string.IsNullOrWhiteSpace(ftvGreet2) || ftvGreet1 == ftvGreet2)
                throw new InvalidOperationException($"FTV- greeting non-repeat failed: '{ftvGreet1}' vs '{ftvGreet2}'.");

            string? mngDetain1 = FactionDialoguePool.TryPickDetentionReaction("MNG-", dummyFtvA);
            string? mngDetain2 = FactionDialoguePool.TryPickDetentionReaction("MNG-", dummyFtvB);
            if (string.IsNullOrWhiteSpace(mngDetain1) || string.IsNullOrWhiteSpace(mngDetain2) || mngDetain1 == mngDetain2)
                throw new InvalidOperationException($"MNG- detention reaction non-repeat failed: '{mngDetain1}' vs '{mngDetain2}'.");

            // Zero [TODO] lines leaked into generated ShipSpec across all prefixes
            GameState todoCheckState = new() { Day = 10, RunSeed = 9001, SuppressDiskPersistence = true };
            for (int slot = 0; slot < 16; slot++)
            {
                ShipSpec generated = histGen.GenerateShipForSlot(todoCheckState, slot, config: config);
                string[] linesToCheck =
                [
                    generated.GreetingHailLine,
                    generated.DetentionReactionLine,
                    generated.ConfrontationGuiltyExcuseHull,
                    generated.ConfrontationGuiltyExcuseOrigin,
                    generated.ConfrontationGuiltyExcuseTransponderId,
                    generated.ConfrontationGuiltyExcuseWeapons
                ];
                foreach (string line in linesToCheck)
                {
                    if (!string.IsNullOrEmpty(line) && line.Contains("[TODO", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"[TODO] placeholder leaked into generated ShipSpec dialogue: '{line}'.");
                }
            }

            // recentCallbacks greeting weaving
            GameState callbackState = new() { Day = 6, RunSeed = 4242, SuppressDiskPersistence = true };
            callbackState.EncounterHistory.Add(new EncounterRecord
            {
                Day = 5,
                Faction = "MNG-",
                HadViolation = true,
                Verdict = "Cleared"
            });
            (_, int d6CbSlot) = ShipGenerator.EnsureDay6IntroTargetAssigned(callbackState);
            ShipSpec callbackShip = histGen.GenerateShipForSlot(
                callbackState,
                d6CbSlot,
                config: config);
            if (!string.Equals(callbackShip.Slate.DeclaredPrefix, "MNG-", StringComparison.OrdinalIgnoreCase) ||
                (!callbackShip.GreetingHailLine.Contains("Syndicate dispatch", StringComparison.OrdinalIgnoreCase) &&
                 !callbackShip.GreetingHailLine.Contains("Oruun-4 haulers", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Expected MNG- ClearedViolation recentCallback greeting weaving, got '{callbackShip.GreetingHailLine}'.");
            }
        }
        finally
        {
            GameState.SaveDirectoryOverride = prevSaveDirOverride;
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            screenManager.UnloadAllScreens();
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            config.UiFontPreset = savedUiFontPreset;
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild47VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        IntroScreen introScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        string savedUiFontPreset = config.UiFontPreset;
        int savedUiFontSizeOffset = config.UiFontSizeOffset;
        bool savedSkipIntro = devDebug.SkipIntroSequence;
        string tempDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke47_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            gameState.SuppressDiskPersistence = true;

            // 1. FontManager cap-height scaling, alpha contrast curve, Windows bold presets & offset persistence
            FontManager.Initialize("ConsolasBold", headlessMode: true);
            if (!FontManager.PresetNames.Contains("ConsolasBold") ||
                !FontManager.PresetNames.Contains("RaylibDefault") ||
                FontManager.PresetCount < 6)
            {
                throw new InvalidOperationException($"Expected expanded FontManager presets with ConsolasBold, got count={FontManager.PresetCount}.");
            }

            if (MathF.Abs(FontManager.ActiveScaleMultiplier - 1.25f) > 0.05f)
                throw new InvalidOperationException($"Expected ConsolasBold ScaleMultiplier ~1.25f, got {FontManager.ActiveScaleMultiplier}.");

            byte boostedMid = FontManager.BoostGlyphAlpha(128);
            if (boostedMid <= 180)
                throw new InvalidOperationException($"BoostGlyphAlpha failed to brighten semi-transparent edge pixels (128 -> {boostedMid}).");

            FontManager.SetFontSizeOffset(99);
            if (FontManager.FontSizeOffset != FontManager.MaxFontSizeOffset)
                throw new InvalidOperationException($"FontSizeOffset clamp upper failed: {FontManager.FontSizeOffset}.");
            FontManager.SetFontSizeOffset(-99);
            if (FontManager.FontSizeOffset != FontManager.MinFontSizeOffset)
                throw new InvalidOperationException($"FontSizeOffset clamp lower failed: {FontManager.FontSizeOffset}.");
            FontManager.SetFontSizeOffset(savedUiFontSizeOffset);

            string tempCfgPath = Path.Combine(tempDir, "config47.json");
            File.WriteAllText(tempCfgPath, "{\n  \"uiFontSizeOffset\": 0\n}\n");
            config.UiFontSizeOffset = 2;
            if (!config.TryPersistUiFontSizeOffsetToPath(tempCfgPath) ||
                !File.ReadAllText(tempCfgPath).Contains("\"uiFontSizeOffset\": 2", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("TryPersistUiFontSizeOffsetToPath failed atomic write.");
            }
            config.UiFontSizeOffset = savedUiFontSizeOffset;

            // 2. Live GPU font atlas load/measure/unload test with non-headless FontManager & clean shutdown
            FontManager.Initialize("ConsolasBold", headlessMode: false);
            int wBold = UiText.MeasureWidth("VOID GATE 3", 10);
            FontManager.SetActivePreset("RaylibDefault");
            int wDefault = UiText.MeasureWidth("VOID GATE 3", 10);
            if (wBold <= 0 || wDefault <= 0)
                throw new InvalidOperationException("UiText.MeasureWidth failed across live FontManager presets.");
            FontManager.Shutdown();
            if (FontManager.ActiveGpuFontCount != 0)
                throw new InvalidOperationException($"FontManager.Shutdown leaked GPU fonts: {FontManager.ActiveGpuFontCount}.");
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            FontManager.SetFontSizeOffset(savedUiFontSizeOffset);

            // 3. 5-card IntroScreen sequence & F1 Skip toggle
            if (IntroScreen.VerbatimCards.Count != 5 ||
                !IntroScreen.VerbatimCards[0].Contains("Three years after the Concord War", StringComparison.Ordinal) ||
                IntroScreen.VerbatimCards[4] != "Congratulations")
            {
                throw new InvalidOperationException("IntroScreen 5-card verbatim text check failed.");
            }

            devDebug.SkipIntroSequence = false;
            screenManager.SwitchTo(ScreenId.Intro);
            if (screenManager.CurrentScreenId != ScreenId.Intro || introScreen.CurrentCardIndex != 0)
                throw new InvalidOperationException("IntroScreen failed to start on Card 0.");
            for (int c = 0; c < 4; c++) introScreen.SmokeAdvanceCard();
            if (introScreen.CurrentCardIndex != 4)
                throw new InvalidOperationException($"IntroScreen failed to advance to Card 4, got {introScreen.CurrentCardIndex}.");
            introScreen.SmokeAdvanceCard();
            introScreen.Update(0.50f, default);
            if (screenManager.CurrentScreenId != ScreenId.Quarters)
                throw new InvalidOperationException($"IntroScreen failed to fade into Quarters after Card 5, stayed on {screenManager.CurrentScreenId}.");

            devDebug.SkipIntroSequence = true;
            screenManager.SwitchTo(ScreenId.Intro);
            if (screenManager.CurrentScreenId != ScreenId.Quarters)
                throw new InvalidOperationException("SkipIntroSequence F1 toggle failed to skip IntroScreen straight to Quarters.");
            devDebug.SkipIntroSequence = savedSkipIntro;

            // 4. Determinism of Regeer Nixa 64x64 portrait, Day 2/8 hails, Vorst Day 4 hails
            PixelBuffer nixaA = PortraitRenderer.RenderRegeerNixaBuffer();
            PixelBuffer nixaB = PortraitRenderer.RenderRegeerNixaBuffer();
            if (nixaA.Width != 64 || nixaA.Height != 64 || !ArePixelBuffersIdentical(nixaA, nixaB))
                throw new InvalidOperationException("PortraitRenderer.RenderRegeerNixaBuffer() was not deterministic 64x64.");
            if (Day2And3ScriptedDialogue.Day2RegeerNixaHailLines.Length != 5 ||
                Day2And3ScriptedDialogue.Day8RegeerNixaHailLines.Length != 7 ||
                !Day2And3ScriptedDialogue.Day8RegeerNixaHailLines[5].Contains("Dopples", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Regeer Nixa Day 2/Day 8 verbatim hail arrays mismatch.");
            }
            string[] vorstD4A = Day2And3ScriptedDialogue.BuildDay4KordaniHailLines(true, "ORN-442-K", "Oruun-4");
            string[] vorstD4B = Day2And3ScriptedDialogue.BuildDay4KordaniHailLines(false, "ORN-442-K", "Oruun-4");
            if (!vorstD4A[0].StartsWith("Vorst:", StringComparison.Ordinal) ||
                !vorstD4B[0].StartsWith("Vorst:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Day 4 Kordani hails must be spoken by Commander Vorst.");
            }

            // 5. No blocking Day 1 tutorial popup, Field Booklet Quarters + Day 6 page unlock, Day 1 Evening ration cue
            gameState.ResetForNewSave(1);
            screenManager.SwitchTo(ScreenId.Quarters);
            if (tutorialOverlay.IsOpen)
                throw new InvalidOperationException("Blocking Day 1 Morning tutorial modal opened on Quarters entry.");

            // Day 1 Evening ration cue active until ration ordered
            gameState.QuartersPhase = QuartersPhase.Evening;
            gameState.TonightRationOrder = NightlyRationOrder.None;
            screenManager.SwitchTo(ScreenId.Quarters);
            if (tutorialOverlay.IsOpen || !quartersScreen.IsDay1EveningRationCueActive)
                throw new InvalidOperationException("Day 1 Evening ration cue failed or blocking end-of-day popup opened.");

            // Quarters Field Booklet accessible & Day 6 page unlocks only after reading "Check your chute"
            List<FieldBookletPage> d1Pages = FieldBookletWidget.GetUnlockedPages(gameState, config);
            if (!d1Pages.Any(p => p.Tag == "day1_evening") || d1Pages.Any(p => p.Tag == "day6_crypto"))
                throw new InvalidOperationException("Field Booklet initial Quarters/Day 6 unlock gating failed.");

            gameState.AdvanceToDay(6);
            gameState.TerminalHackedUnlocked = true;
            List<FieldBookletPage> d6Pages = FieldBookletWidget.GetUnlockedPages(gameState, config);
            if (!d6Pages.Any(p => p.Tag == "day6_crypto"))
                throw new InvalidOperationException("Field Booklet day6_crypto Cryptography page did not unlock after TerminalHackedUnlocked.");

            // Day 6 Morning chute door prompt ("I see there is something in my chute, let me check it out")
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(6);
            gameState.QuartersPhase = QuartersPhase.Morning;
            EconomyEngine econ = new();
            ShipGenerator gen = new(config);
            econ.EnsureMorningRosterGenerated(gameState, gen, config);
            econ.EnsureDay6MorningUnsolicitedDrop(gameState);
            TerminalEmail chuteEmail = gameState.Emails.First(e => e.IsTerminalHackEmail);
            chuteEmail.IsRead = true;
            screenManager.SwitchTo(ScreenId.Quarters);
            quartersScreen.Update(0.016f, new VirtualMouseState { X = 80, Y = 500, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (quartersScreen.ActiveNudgeText != "I see there is something in my chute, let me check it out")
                throw new InvalidOperationException($"Expected Day 6 chute collection nudge, got '{quartersScreen.ActiveNudgeText}'.");

            // 6. DeskScreen Build 4.7 verification suite (Protocol Review auto-clear/duplicate, Reference Manual tabs/hitboxes, hails, booklet, silent capture)
            deskScreen.SmokeVerifyBuild47DeskFeatures(canvas, screenManager, config);

            Console.WriteLine("[SMOKE 4.7] PASS: Font scaling/alpha/presets, V2 default, Protocol Review auto-clear, Brengop Manual, Field Booklet, 5-card Intro, Regeer Nixa hails & silent capture verified.");
        }
        finally
        {
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            screenManager.UnloadAllScreens();
            devDebug.SkipIntroSequence = savedSkipIntro;
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            FontManager.SetFontSizeOffset(savedUiFontSizeOffset);
            config.UiFontPreset = savedUiFontPreset;
            config.UiFontSizeOffset = savedUiFontSizeOffset;
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild475VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        string savedUiFontPreset = config.UiFontPreset;
        int savedUiFontSizeOffset = config.UiFontSizeOffset;
        string tempDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke475_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            gameState.SuppressDiskPersistence = true;

            // [A4] & Pixel-Grid Font Sharpness / Fallback
            if (Raylib.IsWindowReady() && FontManager.IsHeadlessMode)
                throw new InvalidOperationException("[A4] Windowed --smoke-test initialized FontManager in headlessMode.");

            FontManager.Initialize("PixelOperatorMono-Bold", headlessMode: !Raylib.IsWindowReady());
            if (!FontManager.PresetNames.Contains("PixelOperatorMono-Bold") ||
                !FontManager.IsActivePresetPixelFont ||
                !string.Equals(FontManager.ActivePresetName, "PixelOperatorMono-Bold", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected active pixel font PixelOperatorMono-Bold, got {FontManager.ActivePresetName} (isPixel={FontManager.IsActivePresetPixelFont}).");
            }
            if (Raylib.IsWindowReady())
            {
                int pxW = UiText.MeasureWidth("SHIP REGISTRAR", 10, denseRow: true);
                if (pxW != 112 || FontManager.ActiveGpuFontCount <= 0)
                    throw new InvalidOperationException($"Expected native 16px PixelOperatorMono-Bold atlas measure 112px, got {pxW} (gpuFonts={FontManager.ActiveGpuFontCount}).");
            }
            if (FontManager.SnapPixelGlyphAlpha(100) != 0 || FontManager.SnapPixelGlyphAlpha(200) != 255)
                throw new InvalidOperationException("FontManager.SnapPixelGlyphAlpha crisp threshold check failed.");

            // Fallback chain when assets/fonts dir is empty -> ConsolasBold -> RaylibDefault
            string emptyFontsDir = Path.Combine(tempDir, "empty_fonts");
            Directory.CreateDirectory(emptyFontsDir);
            FontManager.Initialize("PixelOperatorMono-Bold", headlessMode: true, customFontsDirOverride: emptyFontsDir);
            if (FontManager.ActivePresetName != "ConsolasBold" && FontManager.ActivePresetName != "RaylibDefault")
                throw new InvalidOperationException($"Empty-dir fallback failed: {FontManager.ActivePresetName}.");
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            FontManager.SetFontSizeOffset(savedUiFontSizeOffset);

            // Regeer Nixa Brengop Species Head check: drooping wide Brengop ears (x <= 12 at y=30..38) + cap + right-eye tassel
            PixelBuffer nixaBuf = PortraitRenderer.RenderRegeerNixaBuffer();
            bool hasLeftEar = false;
            bool hasRightEar = false;
            for (int y = 30; y <= 38; y++)
            {
                string? leftPx = nixaBuf.Get(12, y);
                string? rightPx = nixaBuf.Get(51, y);
                if (!string.IsNullOrEmpty(leftPx) && !leftPx.StartsWith("#121", StringComparison.OrdinalIgnoreCase))
                    hasLeftEar = true;
                if (!string.IsNullOrEmpty(rightPx) && !rightPx.StartsWith("#121", StringComparison.OrdinalIgnoreCase))
                    hasRightEar = true;
            }
            string? tasselPx = nixaBuf.Get(38, 22);
            if (!hasLeftEar || !hasRightEar || string.IsNullOrEmpty(tasselPx))
                throw new InvalidOperationException($"Regeer Nixa Brengop geometry check failed (leftEar={hasLeftEar}, rightEar={hasRightEar}).");

            // [A3] Day 3 vs Day 4 Security Strikes & Forum day2_* gating
            EconomyEngine econ = new();
            ForumDatabase forumDb = ForumDatabase.Load();
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(3);
            gameState.Day2SuspectEncountered = true;
            gameState.Day2SuspectDetained = false;
            List<ForumThreadModel> d3Threads = econ.GetVisibleForumThreads(gameState, forumDb, config);
            if (d3Threads.Any(t => t.Id.StartsWith("d3_coil_repaired", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("[A3] d3_coil_repaired_* leaked onto Day 3 FORUM.");
            var (deferD3Ok, deferD3Msg) = econ.TryPurchaseAuditDeferralFee(gameState, config);
            if (deferD3Ok || !deferD3Msg.Contains("DAY 04", StringComparison.Ordinal))
                throw new InvalidOperationException($"[A3] Day 3 Audit Deferral error mismatch: '{deferD3Msg}'.");

            gameState.AdvanceToDay(config.DayUnlockWarpScanner);
            List<ForumThreadModel> d4Threads = econ.GetVisibleForumThreads(gameState, forumDb, config);
            if (!d4Threads.Any(t => t.Id == "d3_coil_repaired_cleared"))
                throw new InvalidOperationException("[A3] d3_coil_repaired_cleared failed to unlock on Day 4.");

            // [B3] Quarters booklet hover masking + Day 1 PM / Day 6 AM newest page sync
            gameState.ResetForNewSave(1);
            gameState.QuartersPhase = QuartersPhase.Evening;
            screenManager.SwitchTo(ScreenId.Quarters);
            quartersScreen.Booklet.IsOpen = true;
            VirtualMouseState overBookletAndDoor = new() { X = 60, Y = 500, IsInsideViewport = true, Scale = 1.0f };
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(overBookletAndDoor);
            canvas.EndVirtualDrawAndBlit();
            if (!string.IsNullOrEmpty(quartersScreen.ActiveHoverTooltipText))
                throw new InvalidOperationException($"[B3] Room tooltip leaked behind open FieldBooklet: '{quartersScreen.ActiveHoverTooltipText}'.");
            if (quartersScreen.Booklet.CurrentPage(gameState, config).Tag != "day1_evening")
                throw new InvalidOperationException($"[B3] Day 1 Evening booklet did not sync to day1_evening, got {quartersScreen.Booklet.CurrentPage(gameState, config).Tag}.");

            VirtualMouseState overDoorOnly = new() { X = 60, Y = 370, IsInsideViewport = true, Scale = 1.0f };
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(overDoorOnly);
            canvas.EndVirtualDrawAndBlit();
            if (string.IsNullOrEmpty(quartersScreen.ActiveHoverTooltipText))
                throw new InvalidOperationException("[B3] Door tooltip did not render when hovering Door outside booklet.");

            // [B4] Todo tag sanitizer & CONTRACT SETTLED cleanup
            string cleaned = TerminalOverlay.StripAuthoringTodoTags("Line one [TODO: USER DIALOGUE PASS]");
            if (cleaned != "Line one" || cleaned.Contains("[TODO", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"[B4] StripAuthoringTodoTags failed: '{cleaned}'.");

            IntelEconomy intelEcon = new();
            WorldSim worldSim = new();
            gameState.AdvanceToDay(6);
            gameState.ActiveInterceptMissionIds.Add(EconomyEngine.Day6IntroMissionId);
            (string d6B4Reg, _) = ShipGenerator.EnsureDay6IntroTargetAssigned(gameState);
            ContrabandPocketItem mngSlate = new()
            {
                ItemId = "smoke_b4_mng",
                ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
                DisplayName = $"STOLEN SLATE: {d6B4Reg}",
                TargetShipPrefix = "MNG-",
                TargetShipRegistration = d6B4Reg,
                OriginStarSystem = "Kestrel Drift"
            };
            gameState.PocketItems.Add(mngSlate);
            MissionTransmitResult txRes = intelEcon.ExecuteMissionInterceptTransmit(gameState, worldSim, EconomyEngine.Day6IntroMissionId, mngSlate, slateIsInVentStash: false, CovertMissionDatabase.Load(), config);
            if (!txRes.Success)
                throw new InvalidOperationException($"[B4] ExecuteMissionInterceptTransmit failed: {txRes.Message}");
            TerminalEmail settledEmail = gameState.Emails.Last(e => e.Subject.StartsWith("CONTRACT SETTLED", StringComparison.Ordinal));
            if (settledEmail.Body.Contains("[TODO", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"[B4] Leaked [TODO] tag into CONTRACT SETTLED email: '{settledEmail.Body}'.");

            // [B6] Day 1 email reset, Day 6 F1 JumpToDay backfill, & Morning Roster contract sync
            gameState.Day1BrengopEmailRead = true;
            gameState.AdvanceToDay(1);
            TerminalEmail d1Mail = gameState.Emails.First(e => e.IsDay1BrengopEmail);
            if (gameState.Day1BrengopEmailRead || d1Mail.IsRead)
                throw new InvalidOperationException("[B6] AdvanceToDay(1) did not reset Day 1 Brengop Contract email unread state.");

            gameState.ResetForNewSave(1);
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 300, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f }); // DAY 1 (resets SelectedJumpDay=1)
            for (int step = 0; step < 5; step++)
            {
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f }); // -> DAY 6
            }
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 150, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f }); // LOAD DAY 06
            debugOverlay.IsVisible = false;
            tutorialOverlay.ForceCloseSilent();
            if (gameState.Day != 6 ||
                !gameState.TerminalHackedUnlocked ||
                !gameState.ActiveInterceptMissionIds.Contains(EconomyEngine.Day6IntroMissionId) ||
                !gameState.PocketItems.Any(i => i.ItemType == ContrabandPocketItem.TypeBlankDataSlate))
            {
                throw new InvalidOperationException("[B6] DebugOverlay JumpToDay(6) failed Day 6 Desk state backfill.");
            }

            // Morning Roster contract sync when accepting an intercept contract in Morning Quarters
            gameState.AdvanceToDay(7);
            gameState.QuartersPhase = QuartersPhase.Morning;
            ShipGenerator gen = new(config);
            econ.EnsureMorningRosterGenerated(gameState, gen, config);
            CovertMissionSpec? mngOrFtcSpec = CovertMissionDatabase.Load().Missions.FirstOrDefault(m => !m.IsDeliveryMission && m.MinAvailableDay <= 7 && !m.AutoActivateOnHack);
            if (mngOrFtcSpec != null)
            {
                var (accOk, _) = econ.TryAcceptCovertContract(gameState, mngOrFtcSpec.MissionId, CovertMissionDatabase.Load(), config);
                if (!accOk || !gameState.TodayMorningRoster.Any(r => r.RegistrationCode.StartsWith(mngOrFtcSpec.TargetPrefix, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"[B6] Accepting Morning Quarters contract {mngOrFtcSpec.MissionId} did not sync TodayMorningRoster.");
            }

            // Desk-specific Build 4.7.5 checks (A1, A2, B1, B2, B5)
            deskScreen.SmokeVerifyBuild475DeskFeatures(canvas, screenManager, config);

            Console.WriteLine("[SMOKE 4.7.5] PASS: A1-A4, B1-B6, Regeer Nixa Brengop portrait & crisp PixelOperatorMono-Bold verified.");
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            screenManager.UnloadAllScreens();
            FontManager.Initialize(savedUiFontPreset, headlessMode: !Raylib.IsWindowReady());
            FontManager.SetFontSizeOffset(savedUiFontSizeOffset);
            config.UiFontPreset = savedUiFontPreset;
            config.UiFontSizeOffset = savedUiFontSizeOffset;
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild51VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        WaferScoopMode savedScoopMode = config.WaferScoopMode;
        gameState.SuppressDiskPersistence = true;

        try
        {
            // 1. Verify data/cargo_catalog.json 3-Stage Cargo/Bio Progression pools & 5-item Banned List
            CargoCatalog catalog = CargoCatalog.Load(forceReload: true);
            if (!File.Exists(CargoCatalog.ResolveCatalogPath()) ||
                catalog.LegalInertPool.Count != 8 ||
                catalog.ControlledLegalBioPool.Count != 6 ||
                catalog.BannedCargoList.Count != 5 ||
                !catalog.IsBannedCargo("Disruptor Cells", out BannedCargoEntry? disEntry) || disEntry == null || !disEntry.SpsViolet ||
                !catalog.IsBannedCargo("Unshielded Coils", out BannedCargoEntry? coilEntry) || coilEntry == null || !coilEntry.SpsViolet ||
                !catalog.IsBannedCargo("Raw Slag Isotope", out BannedCargoEntry? slagEntry) || slagEntry == null || !slagEntry.DenseMass ||
                !catalog.IsBannedCargo("Ashen Spores", out BannedCargoEntry? sporeEntry) || sporeEntry == null || !sporeEntry.BannedBio ||
                !catalog.IsBannedCargo("Silt Worms", out BannedCargoEntry? wormEntry) || wormEntry == null || !wormEntry.BannedBio)
            {
                throw new InvalidOperationException("CargoCatalog failed to load or validate data/cargo_catalog.json.");
            }

            // 2. Verify ShipRendererV2 ~90% centered hull footprint & non-overlapping >= 3px hull-interior SPS hardpoints across all 11 factions x classes
            foreach (ShipFaction faction in ShipRendererV2.AllFactions)
            {
                foreach (ShipClass allowedClass in ShipRendererV2.GetAllowedClasses(faction))
                {
                    PixelBuffer nativeLive = ShipRendererV2.RenderNative280x120Buffer(new ShipRenderOptions
                    {
                        SeedId = $"verify-51-footprint-{faction}-{allowedClass}",
                        Faction = faction,
                        RequestedClass = allowedClass,
                        Mode = ShipRenderMode.Live
                    }, out ShipHardpointLayoutV2 hp);

                    if (hp.HullBounds.W > 170 || hp.HullBounds.H > 72 ||
                        hp.HullBounds.X < 50 || hp.HullBounds.Right > 230 ||
                        hp.HullBounds.Y < 20 || hp.HullBounds.Bottom > 100)
                    {
                        throw new InvalidOperationException(
                            $"ShipRendererV2 hull footprint exceeded ~90% centered bounds for {faction} {allowedClass}: {hp.HullBounds}.");
                    }

                    bool[] hullMask = nativeLive.CaptureNonNullMask(excludeOutline: true);
                    HardpointRect[] activeModules = new[]
                    {
                        hp.ReactorBounds,
                        hp.WeaponsBay,
                        hp.SecondaryWeaponsBay,
                        hp.BridgeComms,
                        hp.CargoBay,
                        hp.UpperWarpPylon,
                        hp.LowerWarpPylon
                    }.Where(r => !r.IsEmpty).ToArray();

                    for (int i = 0; i < activeModules.Length; i++)
                    {
                        if (!ShipRendererV2.IsRectFullyInsideMask(hullMask, activeModules[i]))
                        {
                            throw new InvalidOperationException(
                                $"SPS subsystem {i} ({activeModules[i]}) bled outside hull mask for {faction} {allowedClass}.");
                        }

                        for (int j = i + 1; j < activeModules.Length; j++)
                        {
                            if (!ShipRendererV2.AreRectsSeparatedByAtLeast(activeModules[i], activeModules[j], ShipRendererV2.MinSpsSubsystemClearancePx))
                            {
                                throw new InvalidOperationException(
                                    $"SPS subsystems {i} ({activeModules[i]}) and {j} ({activeModules[j]}) violated >= 3px clearance for {faction} {allowedClass}.");
                            }
                        }
                    }

                    // Also exercise SPS render with Violet CargoHighDraw, Bio-Green hook, and MilitaryCommsEncryption hook
                    PixelBuffer sps560 = ShipRendererV2.RenderBuffer(new ShipRenderOptions
                    {
                        SeedId = $"verify-51-sps-{faction}-{allowedClass}",
                        Faction = faction,
                        RequestedClass = allowedClass,
                        Mode = ShipRenderMode.Live,
                        SpsEnabled = true,
                        WeaponsHot = true,
                        CargoHighDraw = true,
                        MilitaryCommsEncryption = true
                    });
                    VerifyExact2x2Uniformity(sps560, $"{faction} {allowedClass} 5.1 SPS");
                }
            }

            // 3. Verify Day 12+ Type 1 Covert Delivery contracts unlock on DECK 4 & queue 0500 Chute Mission Slate
            EconomyEngine econ = new();
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(config.DayUnlockWaferCargo);
            gameState.TerminalHackedUnlocked = true;
            var listings = econ.GetDeck4ContractListings(gameState);
            var firstDelivery = listings.FirstOrDefault(l => l.Mission.IsDeliveryMission);
            if (firstDelivery == null)
                throw new InvalidOperationException("Day 12 DECK 4 listings did not expose Type 1 Covert Delivery contracts.");
            var (accOk, _) = econ.TryAcceptCovertContract(gameState, firstDelivery.Mission.MissionId, config: config);
            if (!accOk || !gameState.PendingChuteDeliveries.Any(i => i.IsMissionSlate))
                throw new InvalidOperationException("Accepting Day 12 Covert Delivery contract did not queue 0500 Chute Mission Slate.");

            // 4. Verify F1 DebugOverlay Row 19 controls (WaferScoopMode cycler, Force Empty Hopper, Spawn Test Mission Slate / Chute Drop)
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            config.WaferScoopMode = WaferScoopMode.PostVerdictAutoScoop;
            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 40, Y = 40 + 544, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (config.WaferScoopMode != WaferScoopMode.CenterSwipeScoop)
                throw new InvalidOperationException($"F1 SCOOP cycler failed: got {config.WaferScoopMode}.");
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 210, Y = 40 + 544, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (!devDebug.PendingForceEmptyWaferHopper)
                throw new InvalidOperationException("F1 EMPTY HOPPER button did not arm PendingForceEmptyWaferHopper.");
            debugOverlay.IsVisible = false;
            deskScreen.Update(0.016f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
            if (deskScreen.WaferHopperCount != 0)
                throw new InvalidOperationException($"ForceEmptyWaferHopper failed to empty DeskScreen hopper (got {deskScreen.WaferHopperCount}).");

            debugOverlay.IsVisible = true;
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 360, Y = 40 + 544, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            debugOverlay.IsVisible = false;
            if (!gameState.ServiceChuteItems.Any(i => i.IsMissionSlate) || !gameState.PocketItems.Any(i => i.IsMissionSlate))
                throw new InvalidOperationException("F1 TEST CHUTE SLATE button failed to spawn test mission slate.");

            // 5. Full DeskScreen Build 5.1 verification (CommsTranscriptStatement, 4th CARGO tab, 3-wafer hopper, one-way conduit, left-fan, missing wafer, scoop modes, SPS Violet)
            deskScreen.SmokeVerifyBuild51DeskFeatures(canvas, screenManager, config);

            Console.WriteLine("[SMOKE 5.1] PASS: ~90% V2 footprint, non-overlapping SPS X-Ray schematics, clickable CommsTranscriptStatement, 280px 4-tab Manual CARGO, 3-wafer hopper, scoop modes & SPS Violet [!] POWERED CARGO verified.");
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            config.WaferScoopMode = savedScoopMode;
            screenManager.UnloadAllScreens();
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    private static void RunBuild52VerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        CorridorScreen corridorScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        bool savedEnableStoker = config.EnableStokerDial;
        float savedSafeCap = config.StokerSafeLoadCap;
        float savedShock = config.StokerUncalibratedShock;
        DebugBrownoutForceMode savedBrownoutForce = devDebug.BrownoutForceMode;
        string? savedSaveDirOverride = GameState.SaveDirectoryOverride;
        string tempSaveDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke52_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSaveDir);
        gameState.SuppressDiskPersistence = true;

        try
        {
            EconomyEngine econ = new();
            RaidController raid = new();
            WorldSim worldSim = new();
            ShipGenerator gen = new(config);

            // 1. Shared ValidationEngine.EvaluateStokerMassCalibration Math:
            //    15% idle baseline, 16+ valid ships + overflow cap strictly <= 65% (never overloading),
            //    1 uncalibrated clearance <= 98%, 2 uncalibrated clearances > 100% even on a slow 3-ship shift (~113.85%)
            {
                GameState mState = new() { Day = config.DayUnlockStoker, PactScrip = 100, SuppressDiskPersistence = true };
                float safe = config.StokerIdleBaseline;
                float shock = 0f;
                for (int i = 0; i < 24; i++) // 16-ship queue + 8 random overflow valid hulls
                {
                    ShipSpec validShip = gen.GenerateShipForSlot(mState, i, forceValidity: DebugSpawnValidityMode.ForceValid);
                    StokerCalibrationResult r = ValidationEngine.EvaluateStokerMassCalibration(validShip, mState.Day, config, safe, shock);
                    if (!r.IsCalibrated || r.OverloadsOnClearance || r.PreviewShockDelta > 0.001f)
                        throw new InvalidOperationException($"Valid ship {i} reported uncalibrated or overloaded Stoker.");
                    safe = r.NextSafeLoadOnClearance;
                    shock = r.NextShockOnClearance;
                }
                if (safe > config.StokerSafeLoadCap + 0.001f || safe < 64.9f || shock > 0.001f)
                    throw new InvalidOperationException($"24 valid ships did not converge cleanly to 65% SafeLoad cap (got {safe}%).");

                ShipSpec uncal = gen.GenerateShipForSlot(mState, 0, forceValidity: DebugSpawnValidityMode.ForceValid);
                uncal.Slate.DeclaredTonnage = 1000;
                uncal.GroundTruth.ActualMass = 1950;
                uncal.GroundTruth.CargoManifestAccountedExtraMass = 0;

                StokerCalibrationResult oneSlipAtCap = ValidationEngine.EvaluateStokerMassCalibration(uncal, mState.Day, config, safe, shock);
                if (oneSlipAtCap.IsCalibrated || oneSlipAtCap.OverloadsOnClearance || oneSlipAtCap.PostClearanceTotalStrain > 98.01f)
                    throw new InvalidOperationException($"1 uncalibrated slip at 65% cap must stay <= 98% without overloading (got {oneSlipAtCap.PostClearanceTotalStrain}%).");

                // Slow 3-ship shift + 2 uncalibrated slips -> crosses > 100% (~113.85%)
                float slowSafe = config.StokerIdleBaseline;
                float slowShock = 0f;
                ShipSpec calShip = gen.GenerateShipForSlot(mState, 1, forceValidity: DebugSpawnValidityMode.ForceValid);
                var cStep = ValidationEngine.EvaluateStokerMassCalibration(calShip, mState.Day, config, slowSafe, slowShock);
                slowSafe = cStep.NextSafeLoadOnClearance;
                var uStep1 = ValidationEngine.EvaluateStokerMassCalibration(uncal, mState.Day, config, slowSafe, slowShock);
                slowSafe = uStep1.NextSafeLoadOnClearance;
                slowShock = uStep1.NextShockOnClearance;
                var uStep2 = ValidationEngine.EvaluateStokerMassCalibration(uncal, mState.Day, config, slowSafe, slowShock);
                if (!uStep2.OverloadsOnClearance || uStep2.PostClearanceTotalStrain <= 100.0f)
                    throw new InvalidOperationException($"2 uncalibrated slips on slow 3-ship shift failed to cross > 100% (got {uStep2.PostClearanceTotalStrain}%).");
            }

            // 2. Day 15 Overload Settlement (-35 SC fine, PendingStokerBerthSearch, +0 Security Strikes Sabotage Audit, NextDayPowerBrownout persistence & FORUM flag)
            {
                gameState.ResetForNewSave(1);
                gameState.AdvanceToDay(config.DayUnlockStoker);
                gameState.PactScrip = 200;
                gameState.SecurityStrikes = 1;
                gameState.StokerOverloadedToday = true;

                ShiftLedgerSummary summary = econ.SettleShiftClockOut(gameState, config);
                if (!summary.StokerOverloaded ||
                    summary.StokerOverloadFine != config.StokerOverloadFine ||
                    !gameState.PendingStokerBerthSearch ||
                    !gameState.NextDayPowerBrownout ||
                    !gameState.LastShiftStokerOverload ||
                    gameState.SecurityStrikes != 1)
                {
                    throw new InvalidOperationException("Stoker overload Clock-Out settlement failed (-35 SC fine, Sabotage Audit flag, +0 Security Strikes, or NextDayPowerBrownout).");
                }

                // Atomic save/load round-trip preserves NextDayPowerBrownout & LastShiftStokerOverload
                GameState.SaveDirectoryOverride = tempSaveDir;
                gameState.ActiveSaveSlot = 1;
                gameState.SuppressDiskPersistence = false;
                gameState.SaveToDisk();
                gameState.SuppressDiskPersistence = true;
                GameState reloaded = new() { SuppressDiskPersistence = true };
                if (!reloaded.LoadSlotIntoCurrent(1) || !reloaded.NextDayPowerBrownout || !reloaded.LastShiftStokerOverload)
                    throw new InvalidOperationException("LoadSlotIntoCurrent did not preserve NextDayPowerBrownout / LastShiftStokerOverload.");
                GameState.SaveDirectoryOverride = savedSaveDirOverride;

                // Reactive low-power FORUM chatter unlocked
                ForumDatabase forumDb = ForumDatabase.Load();
                if (!econ.GetVisibleForumThreads(gameState, forumDb, config)
                        .Any(t => t.RequiredFlags.Contains("stoker_overload", StringComparer.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Stoker overload did not unlock reactive low-power FORUM thread.");
                }

                // Sleep Kordani Sabotage Audit adds +0 Security Strikes when berth is clean
                BerthAuditKind pendingKind = raid.CheckPendingSleepAudit(gameState, config);
                if (pendingKind != BerthAuditKind.StokerSabotageAudit)
                    throw new InvalidOperationException($"Expected StokerSabotageAudit on Sleep, got {pendingKind}.");
                BerthAuditResolution auditRes = raid.ResolveBerthAudit(gameState, config, worldSim, pendingKind);
                if (auditRes.WasCaught || gameState.SecurityStrikes != 1)
                    throw new InvalidOperationException("Sleep Kordani Stoker Sabotage Audit illegally altered Security Strikes.");
            }

            // 3. Pocket Upgrade 3 (120 SC on Day 15) & 3-Slate Clock-Out Preservation
            {
                gameState.ResetForNewSave(1);
                gameState.PocketCapacity = 2;
                gameState.PactScrip = 300;
                gameState.AdvanceToDay(config.PocketUpgrade3MinDay - 1);
                if (econ.GetNextPocketUpgradeInfo(gameState, config).CanUpgrade)
                    throw new InvalidOperationException("Pocket Upgrade 3 unlocked prematurely before Day 15.");

                gameState.AdvanceToDay(config.PocketUpgrade3MinDay);
                var up3Info = econ.GetNextPocketUpgradeInfo(gameState, config);
                if (!up3Info.CanUpgrade || up3Info.NextCap != 3 || up3Info.Cost != 120)
                    throw new InvalidOperationException($"Day 15 Pocket Upgrade 3 info mismatch: {up3Info}.");
                var (up3Ok, _) = econ.TryPurchasePocketCapacityUpgrade(gameState, config);
                if (!up3Ok || gameState.PocketCapacity != 3 || gameState.PactScrip != 180)
                    throw new InvalidOperationException("Purchasing Day 15 Pocket Upgrade 3 (120 SC) failed.");

                gameState.PocketItems.Clear();
                for (int s = 0; s < 3; s++)
                {
                    gameState.PocketItems.Add(new ContrabandPocketItem
                    {
                        ItemId = $"p3_slate_{s}",
                        ItemType = ContrabandPocketItem.TypeBlankDataSlate,
                        DisplayName = $"BLANK SLATE {s + 1}"
                    });
                }
                gameState.Day15ForemanHailCompleted = true;
                screenManager.SwitchTo(ScreenId.Desk);
                // Pull 1 of the 3 slates onto the desk and run Clock-Out recovery -> all 3 must be preserved in PocketItems!
                deskScreen.Update(0.016f, new VirtualMouseState { X = 40, Y = 680, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                deskScreen.Update(0.016f, new VirtualMouseState { X = 480, Y = 500, IsInsideViewport = true, LeftReleased = true, Scale = 1.0f });
                deskScreen.TryAutoRecoverLooseContrabandAtClockOut();
                if (gameState.PocketItems.Count != 3)
                    throw new InvalidOperationException($"Expected all 3 Contraband Slates preserved at Clock-Out, got {gameState.PocketItems.Count}.");
            }

            // 4. App Logo (app_icon.jpg) & Day 15 Regeer Nixa Same-Face Portrait + Zero-% Dialogue Check
            {
                if (string.IsNullOrWhiteSpace(AssetManager.ResolveAppIconPath()) ||
                    !AssetManager.TryGetAppLogoTexture(out Texture2D appLogoTex) ||
                    appLogoTex.Width != 64 || appLogoTex.Height != 64)
                {
                    throw new InvalidOperationException("AssetManager failed to load crisp 64x64 app_icon.jpg logo texture.");
                }

                PixelBuffer foremanA = PortraitRenderer.RenderBrengopLogisticsForemanBuffer();
                PixelBuffer nixa = PortraitRenderer.RenderRegeerNixaBuffer();
                if (!ArePixelBuffersIdentical(foremanA, nixa))
                    throw new InvalidOperationException("Day 15 Brengop hail portrait must match Regeer Nixa's exact face.");
                Texture2D fTex = foremanA.ToTexture2D();
                PixelBuffer.UnloadTexture(ref fTex);

                foreach (string line in Day2And3ScriptedDialogue.Day15BrengopForemanHailLines)
                {
                    if (line.Contains('%'))
                        throw new InvalidOperationException($"Day 15 Regeer Nixa hail dialogue leaked raw '%' tooltip number: '{line}'.");
                }
            }

            // 5. Corridor Organic Irregular Power-Sag Dips (0.8s-1.8s gaps + 0.15s-0.45s bursts)
            {
                gameState.NextDayPowerBrownout = true;
                screenManager.SwitchTo(ScreenId.Corridor);
                VirtualMouseState corrMouse = new() { X = 640, Y = 360, IsInsideViewport = true, Scale = 1.0f };
                bool sawSagBurst = false;
                for (int step = 0; step < 30 && screenManager.CurrentScreenId == ScreenId.Corridor; step++)
                {
                    corridorScreen.Update(0.08f, corrMouse);
                    if (corridorScreen.LastSampledSagGapSeconds is < 0.799f or > 1.801f)
                        throw new InvalidOperationException($"Corridor sag gap out of [0.8, 1.8]s range: {corridorScreen.LastSampledSagGapSeconds}.");
                    if (corridorScreen.IsPowerSagBurstActive)
                    {
                        sawSagBurst = true;
                        if (corridorScreen.LastSampledSagBurstDurationSeconds is < 0.149f or > 0.451f)
                            throw new InvalidOperationException($"Corridor sag burst out of [0.15, 0.45]s range: {corridorScreen.LastSampledSagBurstDurationSeconds}.");
                    }
                    canvas.BeginVirtualDraw();
                    corridorScreen.DrawVirtual(corrMouse);
                    canvas.EndVirtualDrawAndBlit();
                }
                if (!sawSagBurst)
                    throw new InvalidOperationException("Corridor organic power-sag state machine never triggered a burst within 2.4s.");
            }

            // 6. Day 15+ Pause [?] Guide Exact Percentages & F1 Dev Controls (Rows 20-23)
            {
                gameState.AdvanceToDay(config.DayUnlockStoker);
                gameState.Day15ForemanHailCompleted = true;
                screenManager.SwitchTo(ScreenId.Desk);
                tutorialOverlay.OpenSummaryForScreen(ScreenId.Desk);
                string guideText = tutorialOverlay.GetActiveSummaryPlainText();
                tutorialOverlay.ForceCloseSilent();
                if (!guideText.Contains("15%", StringComparison.Ordinal) ||
                    !guideText.Contains("65%", StringComparison.Ordinal) ||
                    !guideText.Contains("+33%", StringComparison.Ordinal) ||
                    !guideText.Contains("0 Security Strikes", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Day 15+ Pause [?] summary missing exact Stoker percentages or 0 Security Strikes: '{guideText}'.");
                }

                debugOverlay.IsVisible = true;
                // Row 20: CAP+ at (16+210, 40+586) -> 70%, SHK+ at (16+440, 40+586) -> 34%
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 210, Y = 40 + 586, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 440, Y = 40 + 586, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (MathF.Abs(config.StokerSafeLoadCap - 70f) > 0.01f || MathF.Abs(config.StokerUncalibratedShock - 34f) > 0.01f)
                    throw new InvalidOperationException("F1 Row 20 StokerSafeLoadCap / StokerUncalibratedShock steppers failed.");
                config.StokerSafeLoadCap = 65f;
                config.StokerUncalibratedShock = 33f;

                // Row 21: FORCE STOKER OVL at (16+300, 40+608) & RESET STRAIN at (16+80, 40+608)
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 300, Y = 40 + 608, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (!devDebug.PendingForceStokerOverload || !gameState.StokerOverloadedToday)
                    throw new InvalidOperationException("F1 Row 21 FORCE STOKER OVL button failed.");
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 608, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (!devDebug.PendingResetStokerStrain || gameState.StokerOverloadedToday)
                    throw new InvalidOperationException("F1 Row 21 RESET STRAIN button failed.");
                devDebug.PendingForceStokerOverload = false;
                devDebug.PendingResetStokerStrain = false;

                // Row 22: BRWN FORCE DIM at (16+280, 40+628)
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 280, Y = 40 + 628, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (devDebug.BrownoutForceMode != DebugBrownoutForceMode.ForceDim)
                    throw new InvalidOperationException($"F1 Row 22 ForceDim button failed (got {devDebug.BrownoutForceMode}).");
                devDebug.BrownoutForceMode = DebugBrownoutForceMode.Auto;

                // Row 23: STOKER DIAL toggle at (16+80, 40+650) & POCKET + at (16+440, 40+650)
                gameState.PocketCapacity = 1;
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 650, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 440, Y = 40 + 650, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
                if (config.EnableStokerDial || gameState.PocketCapacity != 2)
                    throw new InvalidOperationException("F1 Row 23 EnableStokerDial toggle or POCKET CAP stepper failed.");
                config.EnableStokerDial = true;

                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(new VirtualMouseState { X = 640, Y = 360, IsInsideViewport = true, Scale = 1.0f });
                debugOverlay.DrawVirtual(new VirtualMouseState { X = 640, Y = 360, IsInsideViewport = true, Scale = 1.0f });
                canvas.EndVirtualDrawAndBlit();
                debugOverlay.IsVisible = false;
            }

            // 7. Full DeskScreen Build 5.2 interactive verification (Premature Wafer Wipe [RE-TRANSMIT], Day 15 Foreman Hail, Stoker LED/Dial/Review, 3-State Brownouts & GPU pixel proof)
            deskScreen.SmokeVerifyBuild52DeskFeatures(canvas, screenManager, pauseMenu, config, devDebug);

            Console.WriteLine("[SMOKE 5.2] PASS: Premature Wafer Wipe [RE-TRANSMIT], Day 15 Stellar Stoker Dual-Readout & Shared Calibration, Foreman Hail, Overload +0-Strike Audit, 3-State Brownouts, Organic Corridor Sag & Pocket Upgrade 3 verified.");
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            config.EnableStokerDial = savedEnableStoker;
            config.StokerSafeLoadCap = savedSafeCap;
            config.StokerUncalibratedShock = savedShock;
            devDebug.BrownoutForceMode = savedBrownoutForce;
            GameState.SaveDirectoryOverride = savedSaveDirOverride;
            screenManager.UnloadAllScreens();
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempSaveDir))
            {
                try { Directory.Delete(tempSaveDir, recursive: true); } catch { }
            }
        }
    }

    private static void RunBuild521Day6TargetSyncVerificationSuite(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        DeskScreen deskScreen,
        QuartersScreen quartersScreen,
        TutorialOverlay tutorialOverlay,
        PauseMenuOverlay pauseMenu,
        DebugOverlay debugOverlay,
        GameState gameState,
        Core.ConfigFlags config)
    {
        bool prevSuppress = gameState.SuppressDiskPersistence;
        string? savedSaveDirOverride = GameState.SaveDirectoryOverride;
        string tempSaveDir = Path.Combine(Path.GetTempPath(), $"SubspaceProtocols_Smoke521_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSaveDir);
        GameState.SaveDirectoryOverride = tempSaveDir;

        try
        {
            gameState.SuppressDiskPersistence = false;

            // 1. Determinism across RunSeeds, slot index in 1..3 (Ship 2..4), save round-trip & legacy save backfill
            HashSet<int> observedSlots = [];
            int[] sampleSeeds = [101, 202, 303, 4242, 9999];
            foreach (int seed in sampleSeeds)
            {
                GameState seedStateA = new() { SuppressDiskPersistence = true, RunSeed = seed };
                GameState seedStateB = new() { SuppressDiskPersistence = true, RunSeed = seed };
                (string regA, int slotA) = ShipGenerator.EnsureDay6IntroTargetAssigned(seedStateA);
                (string regB, int slotB) = ShipGenerator.EnsureDay6IntroTargetAssigned(seedStateB);

                if (!regA.StartsWith("MNG-", StringComparison.Ordinal) ||
                    regA != regB ||
                    slotA is < 1 or > 3 ||
                    slotA != slotB)
                {
                    throw new InvalidOperationException($"[5.2.1] Determinism or slot range failed for seed {seed}: ({regA}, {slotA}) vs ({regB}, {slotB}).");
                }
                observedSlots.Add(slotA);
            }
            if (observedSlots.Count < 2)
                throw new InvalidOperationException("[5.2.1] Expected Day 6 target slot to vary across RunSeeds within 1..3.");

            // Legacy save without Day6IntroTargetReg / Day6IntroTargetSlot deserializes safely and backfills deterministically
            string legacySlotPath = GameState.GetSlotFilePath(2);
            File.WriteAllText(legacySlotPath, "{\"activeSaveSlot\":2,\"runSeed\":4242,\"day\":6,\"pactScrip\":80}");
            GameState legacyState = new() { SuppressDiskPersistence = true };
            if (!legacyState.LoadSlotIntoCurrent(2) ||
                !string.IsNullOrEmpty(legacyState.Day6IntroTargetReg) ||
                legacyState.Day6IntroTargetSlot != -1)
            {
                throw new InvalidOperationException("[5.2.1] Legacy save default initialization failed.");
            }
            (string legacyReg, int legacySlot) = ShipGenerator.EnsureDay6IntroTargetAssigned(legacyState);
            if (!legacyReg.StartsWith("MNG-", StringComparison.Ordinal) || legacySlot is < 1 or > 3)
                throw new InvalidOperationException($"[5.2.1] Legacy save Day 6 backfill failed: ({legacyReg}, {legacySlot}).");

            // 2. Email verbatim wording + exact reg, Morning Roster sync, queue uniqueness & Transponder parity
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(6);
            gameState.QuartersPhase = QuartersPhase.Morning;
            EconomyEngine econ = new();
            ShipGenerator gen = new(config);
            econ.EnsureMorningRosterGenerated(gameState, gen, config);
            bool dropped = econ.EnsureDay6MorningUnsolicitedDrop(gameState);
            string targetReg = gameState.Day6IntroTargetReg;
            int targetSlot = gameState.Day6IntroTargetSlot;

            if (!dropped || !targetReg.StartsWith("MNG-", StringComparison.Ordinal) || targetSlot is < 1 or > 3)
                throw new InvalidOperationException($"[5.2.1] Day 6 assignment failed: dropped={dropped}, reg='{targetReg}', slot={targetSlot}.");

            TerminalEmail chuteEmail = gameState.Emails.FirstOrDefault(e => e.EmailId == EconomyEngine.Day6HackEmailId)
                ?? throw new InvalidOperationException("[5.2.1] Missing Day 6 Check your chute email.");
            string expectedLine = $"There is a MNG- rig ({targetReg}) on today\u2019s roster carrying telemetry someone is willing to pay for. Slot in the blank slate into the Bus before protocols hit.";
            if (!chuteEmail.Body.Contains(expectedLine, StringComparison.Ordinal) ||
                !chuteEmail.Body.Contains("Check your Chute next to your door.", StringComparison.Ordinal) ||
                !chuteEmail.Body.Contains("You can transmit the slate tonight via the Cryptography tab", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"[5.2.1] Check your chute email mismatch:\n{chuteEmail.Body}");
            }

            TerminalEmail rosterEmail = gameState.Emails.FirstOrDefault(e => e.EmailId == "morning_roster_day_06")
                ?? throw new InvalidOperationException("[5.2.1] Missing Day 6 Morning Ship Roster email.");
            if (!gameState.TodayMorningRoster.Any(r => r.IsRealArrival && r.RegistrationCode.Equals(targetReg, StringComparison.Ordinal)) ||
                !rosterEmail.Body.Contains(targetReg, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"[5.2.1] Day 6 Morning Ship Roster missing target rig '{targetReg}'.");
            }

            List<ShipSpec> day6Queue = gen.GenerateDailyQueue(gameState, Constants.PreGeneratedQueueSize, config: config);
            ShipSpec injectedShip = day6Queue[targetSlot];
            if (!injectedShip.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.Ordinal) ||
                !injectedShip.Slate.RegistrationCode.Equals(targetReg, StringComparison.Ordinal) ||
                !injectedShip.GroundTruth.RawTransponderId.Equals(targetReg, StringComparison.Ordinal) ||
                injectedShip.StoryTag != EconomyEngine.Day6IntroMissionId)
            {
                throw new InvalidOperationException($"[5.2.1] Day 6 queue slot {targetSlot} mismatch: prefix={injectedShip.Slate.DeclaredPrefix}, reg={injectedShip.Slate.RegistrationCode}, raw={injectedShip.GroundTruth.RawTransponderId}, tag={injectedShip.StoryTag}.");
            }

            HashSet<string> uniqueQueueRegs = new(StringComparer.OrdinalIgnoreCase);
            foreach (ShipSpec s in day6Queue)
            {
                if (!uniqueQueueRegs.Add(s.Slate.RegistrationCode))
                    throw new InvalidOperationException($"[5.2.1] Duplicate registration '{s.Slate.RegistrationCode}' found in Day 6 queue.");
            }
            if (day6Queue.Count(s => s.Slate.RegistrationCode.Equals(targetReg, StringComparison.OrdinalIgnoreCase)) != 1)
                throw new InvalidOperationException($"[5.2.1] Target reg '{targetReg}' appeared != 1 time in Day 6 queue.");

            // Save & reload persistence round-trip
            GameState reloaded = new() { SuppressDiskPersistence = true };
            if (!reloaded.LoadSlotIntoCurrent(1) ||
                reloaded.Day6IntroTargetReg != targetReg ||
                reloaded.Day6IntroTargetSlot != targetSlot)
            {
                throw new InvalidOperationException("[5.2.1] Day6IntroTargetReg / Day6IntroTargetSlot save round-trip failed.");
            }

            // 3. Field Booklet auto-jot gated on TerminalHackedUnlocked + exact Mission Validation
            if (FieldBookletWidget.GetUnlockedPages(gameState, config).Any(p => p.Tag == "day6_crypto"))
                throw new InvalidOperationException("[5.2.1] day6_crypto booklet page unlocked prematurely before reading Check your chute.");

            econ.ActivateDay6IntroHackMission(gameState);
            FieldBookletPage d6NotePage = FieldBookletWidget.GetUnlockedPages(gameState, config)
                .FirstOrDefault(p => p.Tag == "day6_crypto")
                ?? throw new InvalidOperationException("[5.2.1] day6_crypto booklet page missing after TerminalHackedUnlocked.");
            if (!d6NotePage.BodyText.Contains($"TARGET RIG: {targetReg}", StringComparison.Ordinal))
                throw new InvalidOperationException($"[5.2.1] Field Booklet Day 6 note missing 'TARGET RIG: {targetReg}': '{d6NotePage.BodyText}'.");

            IntelEconomy intel = new();
            WorldSim worldSim = new();
            CovertMissionDatabase missionDb = CovertMissionDatabase.Load();

            string wrongMngReg = targetReg.Equals("MNG-999-Z", StringComparison.OrdinalIgnoreCase) ? "MNG-998-Y" : "MNG-999-Z";
            ContrabandPocketItem wrongMngSlate = new()
            {
                ItemId = "smoke_521_wrong_mng",
                ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
                DisplayName = $"STOLEN SLATE: {wrongMngReg}",
                TargetShipPrefix = "MNG-",
                TargetShipRegistration = wrongMngReg,
                OriginStarSystem = "Oruun-4"
            };
            gameState.PocketItems.Add(wrongMngSlate);

            List<MissionDispatchMatch> wrongMatches = intel.GetActiveInterceptDispatches(gameState, missionDb);
            if (wrongMatches.Count != 1 || wrongMatches[0].CanTransmit || wrongMatches[0].EffectiveTargetRegistration != targetReg)
                throw new InvalidOperationException($"[5.2.1] Wrong MNG- rig '{wrongMngReg}' falsely matched Day 6 intro contract.");

            MissionTransmitResult wrongTx = intel.ExecuteMissionInterceptTransmit(gameState, worldSim, EconomyEngine.Day6IntroMissionId, wrongMngSlate, slateIsInVentStash: false, missionDb, config);
            if (wrongTx.Success)
                throw new InvalidOperationException($"[5.2.1] Wrong MNG- rig '{wrongMngReg}' falsely transmitted for Day 6 intro contract.");

            ContrabandPocketItem exactMngSlate = new()
            {
                ItemId = "smoke_521_exact_mng",
                ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
                DisplayName = $"STOLEN SLATE: {targetReg}",
                TargetShipPrefix = "MNG-",
                TargetShipRegistration = targetReg,
                OriginStarSystem = "Oruun-4"
            };
            gameState.VentStashItems.Add(exactMngSlate);

            List<MissionDispatchMatch> exactMatches = intel.GetActiveInterceptDispatches(gameState, missionDb);
            if (exactMatches.Count != 1 || !exactMatches[0].CanTransmit || exactMatches[0].MatchingSlate?.ItemId != exactMngSlate.ItemId)
                throw new InvalidOperationException($"[5.2.1] Exact MNG- rig '{targetReg}' failed to match Day 6 intro contract.");

            MissionTransmitResult exactTx = intel.ExecuteMissionInterceptTransmit(gameState, worldSim, EconomyEngine.Day6IntroMissionId, exactMngSlate, slateIsInVentStash: true, missionDb, config);
            if (!exactTx.Success || exactTx.ScripEarned != 45)
                throw new InvalidOperationException($"[5.2.1] Exact MNG- rig '{targetReg}' transmit failed: {exactTx.Message}");

            // 4. F1 DebugOverlay JumpToDay(6) & LOAD DAY 06 AM (HACK DROP) parity
            gameState.SuppressDiskPersistence = true;
            gameState.ResetForNewSave(1);
            debugOverlay.IsVisible = true;
            // Row 6: D1 then +5 steps -> D6 -> LOAD DAY 06
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 300, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            for (int s = 0; s < 5; s++)
            {
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            }
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 150, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            tutorialOverlay.ForceCloseSilent();
            if (gameState.Day != 6 ||
                !gameState.Day6IntroTargetReg.StartsWith("MNG-", StringComparison.Ordinal) ||
                gameState.Day6IntroTargetSlot is < 1 or > 3)
            {
                throw new InvalidOperationException("[5.2.1] F1 JumpToDay(6) failed Day6IntroTargetReg / Day6IntroTargetSlot sync.");
            }
            string jumpReg = gameState.Day6IntroTargetReg;
            int jumpSlot = gameState.Day6IntroTargetSlot;
            List<ShipSpec> jumpQueue = gen.GenerateDailyQueue(gameState, Constants.PreGeneratedQueueSize, config: config);
            if (jumpQueue[jumpSlot].Slate.RegistrationCode != jumpReg)
                throw new InvalidOperationException("[5.2.1] F1 JumpToDay(6) queue slot mismatch.");

            // Row 13: LOAD DAY 06 AM (HACK DROP)
            gameState.ResetForNewSave(1);
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 300, Y = 40 + 400, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            tutorialOverlay.ForceCloseSilent();
            debugOverlay.IsVisible = false;
            string hackDropReg = gameState.Day6IntroTargetReg;
            int hackDropSlot = gameState.Day6IntroTargetSlot;
            TerminalEmail? hackDropEmail = gameState.Emails.FirstOrDefault(e => e.EmailId == EconomyEngine.Day6HackEmailId);
            TerminalEmail? hackDropRoster = gameState.Emails.FirstOrDefault(e => e.EmailId == "morning_roster_day_06");
            List<ShipSpec> hackDropQueue = gen.GenerateDailyQueue(gameState, Constants.PreGeneratedQueueSize, config: config);
            if (gameState.Day != 6 ||
                hackDropEmail == null || !hackDropEmail.Body.Contains($"({hackDropReg})", StringComparison.Ordinal) ||
                hackDropRoster == null || !hackDropRoster.Body.Contains(hackDropReg, StringComparison.Ordinal) ||
                hackDropQueue[hackDropSlot].Slate.RegistrationCode != hackDropReg)
            {
                throw new InvalidOperationException("[5.2.1] F1 LOAD DAY 06 AM (HACK DROP) failed email/roster/queue sync.");
            }

            // Render Field Booklet with Day 6 note to verify crisp UI text rendering path
            gameState.TerminalHackedUnlocked = true;
            quartersScreen.Booklet.IsOpen = true;
            quartersScreen.Booklet.SelectPageByTag("day6_crypto", gameState, config);
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(new VirtualMouseState { X = 640, Y = 360, IsInsideViewport = true, Scale = 1.0f });
            canvas.EndVirtualDrawAndBlit();
            quartersScreen.Booklet.IsOpen = false;

            Console.WriteLine("[SMOKE 5.2.1] PASS: Deterministic Day 6 MNG- target reg/slot, verbatim email, Morning Roster, queue uniqueness, exact intercept validation, Field Booklet auto-jot & F1 jump parity verified.");
        }
        finally
        {
            quartersScreen.SmokeCloseSubOverlays();
            debugOverlay.IsVisible = false;
            pauseMenu.Close();
            tutorialOverlay.ForceCloseSilent();
            GameState.SaveDirectoryOverride = savedSaveDirOverride;
            screenManager.UnloadAllScreens();
            AudioHooks.Initialize(headlessMode: true);
            AudioHooks.ClearEventHistory();
            gameState.SuppressDiskPersistence = prevSuppress;
            if (Directory.Exists(tempSaveDir))
            {
                try { Directory.Delete(tempSaveDir, recursive: true); } catch { }
            }
        }
    }

    private static bool ArePixelBuffersIdentical(PixelBuffer a, PixelBuffer b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                if (!string.Equals(a.Get(x, y), b.Get(x, y), StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }
        return true;
    }
}
