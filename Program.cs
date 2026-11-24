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
        TutorialOverlay tutorialOverlay = new(gameState, config);
        Action openContextRules = () => tutorialOverlay.OpenSummaryForScreen(screenManager.CurrentScreenId);
        PauseMenuOverlay pauseMenu = new(screenManager, gameState, virtualCanvas, openContextRules);
        DebugOverlay debugOverlay = new(screenManager, gameState, config, devDebug, openContextRules);

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
        QuartersScreen quartersScreen = new(
            screenManager,
            gameState,
            config,
            pauseMenu.Toggle,
            () => tutorialOverlay.OnFirstMorningQuarters(),
            () => tutorialOverlay.OnFirstEveningQuarters(),
            devDebug);
        screenManager.RegisterScreen(ScreenId.Quarters, quartersScreen);
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

                    gameState.Day3PreShiftHailCompleted = true;
                    gameState.AdvanceToDay(3);
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

                    if (!string.IsNullOrWhiteSpace(dumpFramesDir))
                    {
                        CaptureVisualVerificationFrames(
                            virtualCanvas,
                            screenManager,
                            deskScreen,
                            quartersScreen,
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

        // 2. Verify Day 2 Escalation (ONLY TransponderIdMismatch + SpoofedWeaponsHot, plus Ghost Route slot 1 or 2 deterministic from RunSeed)
        GameState d2State = new() { Day = 2, PactScrip = 45, RunSeed = 20260410, SuppressDiskPersistence = true };
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

        int ghostSlot = ShipGenerator.GetDay2GhostRouteQueueSlot(d2State);
        if (ghostSlot is not (1 or 2) || ShipGenerator.GetDay2GhostRouteQueueSlot(d2State.RunSeed) != ghostSlot)
            throw new InvalidOperationException($"Day 2 Ghost Route slot must deterministically be 1 or 2, got {ghostSlot}.");
        ShipSpec ghostSuspect = generator.GenerateShipForSlot(d2State, ghostSlot);
        if (!ghostSuspect.GroundTruth.IsDay2GhostRouteSuspect)
            throw new InvalidOperationException($"Day 2 Ghost Route suspect must spawn on its deterministic slot ({ghostSlot}).");
        HashSet<int> reachableGhostSlots = [];
        for (int seed = 1; seed <= 32; seed++)
        {
            reachableGhostSlots.Add(ShipGenerator.GetDay2GhostRouteQueueSlot(seed));
        }
        if (!reachableGhostSlots.Contains(1) || !reachableGhostSlots.Contains(2) || reachableGhostSlots.Count != 2)
            throw new InvalidOperationException("Day 2 Ghost Route slot randomization must reach both slot 1 and slot 2 across seeds.");

        // 2b. Verify Build 4.3.6 25-Day Stagger:
        //   Days 3-4: TransponderIdMismatch + SpoofedWeaponsHot + FalsifiedOriginWaveform ONLY (Day 4 is a breather shift)
        //   Days 5-6: Adds ExpiredRegistration + PrefixOrganizationMismatch + PrefixSpeciesMismatch (NO PrefixHullSilhouetteMismatch, NO UnexplainedCargoPowerDraw)
        //   Days 7-9: Adds PrefixHullSilhouetteMismatch (NO UnexplainedCargoPowerDraw)
        //   Day 10+:  Adds UnexplainedCargoPowerDraw (SPS Violet Cargo Bay)
        {
            for (int day = 3; day <= 4; day++)
            {
                GameState d34PoolState = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD34 = [];
                for (int i = 0; i < 120; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d34PoolState, i, reseedNonce: 7u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD34.Add(t);
                    if (t is not (ShipInfractionType.TransponderIdMismatch or ShipInfractionType.SpoofedWeaponsHot or ShipInfractionType.FalsifiedOriginWaveform))
                        throw new InvalidOperationException($"Day {day} generated disallowed post-Day-4 infraction: {t}");
                }
                if (!seenD34.Contains(ShipInfractionType.FalsifiedOriginWaveform))
                    throw new InvalidOperationException($"Day {day} must generate FalsifiedOriginWaveform.");
            }

            for (int day = 5; day <= 6; day++)
            {
                GameState d56PoolState = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD56 = [];
                for (int i = 0; i < 180; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d56PoolState, i, reseedNonce: 9u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD56.Add(t);
                    if (t is ShipInfractionType.PrefixHullSilhouetteMismatch or ShipInfractionType.UnexplainedCargoPowerDraw)
                        throw new InvalidOperationException($"Day {day} generated disallowed infraction: {t}");
                }
                if (!seenD56.Contains(ShipInfractionType.ExpiredRegistration) ||
                    !seenD56.Contains(ShipInfractionType.PrefixOrganizationMismatch) ||
                    !seenD56.Contains(ShipInfractionType.PrefixSpeciesMismatch))
                {
                    throw new InvalidOperationException($"Day {day} must generate ExpiredRegistration, PrefixOrganizationMismatch, and PrefixSpeciesMismatch.");
                }
            }

            for (int day = 7; day <= 9; day++)
            {
                GameState d79State = new() { Day = day, PactScrip = 45, SuppressDiskPersistence = true };
                HashSet<ShipInfractionType> seenD79 = [];
                for (int i = 0; i < 180; i++)
                {
                    ShipSpec s = generator.GenerateShipForSlot(d79State, i, reseedNonce: 13u, DebugSpawnValidityMode.ForceInvalid);
                    ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                    seenD79.Add(t);
                    if (t == ShipInfractionType.UnexplainedCargoPowerDraw)
                        throw new InvalidOperationException($"Day {day} generated disallowed Day 10+ UnexplainedCargoPowerDraw.");
                }
                if (!seenD79.Contains(ShipInfractionType.PrefixHullSilhouetteMismatch))
                    throw new InvalidOperationException($"Day {day} must generate PrefixHullSilhouetteMismatch.");
            }

            GameState d10State = new() { Day = 10, PactScrip = 45, SuppressDiskPersistence = true };
            HashSet<ShipInfractionType> seenD10 = [];
            for (int i = 0; i < 200; i++)
            {
                ShipSpec s = generator.GenerateShipForSlot(d10State, i, reseedNonce: 17u, DebugSpawnValidityMode.ForceInvalid);
                seenD10.Add(s.GroundTruth.ActiveInfractions[0].Type);
            }
            if (!seenD10.Contains(ShipInfractionType.UnexplainedCargoPowerDraw))
                throw new InvalidOperationException("Day 10 must unlock UnexplainedCargoPowerDraw (SPS Violet Cargo Bay).");
        }

        // 2c. Verify Security Strikes unified strictly to /3 (no silent mid-shift 5-strike game over)
        {
            GameState strikeState = new() { Day = 3, PactScrip = 45, SuppressDiskPersistence = true };
            for (int i = 0; i < 5; i++)
            {
                validator.AddSecurityStrike(strikeState, 1);
            }
            if (strikeState.EndingFlags.TryGetValue("Fail_SecurityDetainment", out bool midShiftFail) && midShiftFail)
                throw new InvalidOperationException("AddSecurityStrike must NOT trigger silent mid-shift 5-strike Fail_SecurityDetainment; 3/3 triggers Sleep Kordani Berth Audit.");
        }

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
        if (c4.IsAccurate || !c4.IsCitation || c4.ViolationReason != ValidationEngine.UnverifiedDetentionReason)
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

        // 4. Build 4.3.6: Day 6/13 Pocket Capacity Upgrade Gates, Day 10+ Covert Delivery Missions, Day 6 Hack Drop & Guaranteed MNG- Target,
        //    Missions-Only CRYPTO / LOGS Direct [TRANSMIT] Dispatch & Option A DECK 4 50 SC Strike Clearing
        if (config.PocketUpgrade2MinDay != 6 || config.PocketUpgrade3MinDay != 13)
            throw new InvalidOperationException($"Expected Day 6 / Day 13 Pocket Capacity upgrade gates, found Day {config.PocketUpgrade2MinDay} / Day {config.PocketUpgrade3MinDay}.");
        if (config.ClockOutPowerDownDelaySeconds < 3.8f || config.ClockOutPowerDownDelaySeconds > 4.2f)
            throw new InvalidOperationException($"Expected ClockOutPowerDownDelaySeconds in [3.8, 4.2], found {config.ClockOutPowerDownDelaySeconds}.");

        // Verify Pocket Upgrade Day 6 (60 SC) and Day 13 (120 SC) locks
        state.PocketCapacity = 1;
        state.PactScrip = 300;
        state.Day = 5;
        if (economy.GetNextPocketUpgradeInfo(state, config).CanUpgrade)
            throw new InvalidOperationException("Pocket Capacity 1->2 upgrade must be locked on Day 5.");
        state.Day = 6;
        if (!economy.TryPurchasePocketCapacityUpgrade(state, config).Success || state.PocketCapacity != 2)
            throw new InvalidOperationException("Pocket Capacity 1->2 upgrade failed on Day 6.");
        state.Day = 12;
        if (economy.GetNextPocketUpgradeInfo(state, config).CanUpgrade)
            throw new InvalidOperationException("Pocket Capacity 2->3 upgrade must be locked on Day 12.");
        state.Day = 13;
        if (!economy.TryPurchasePocketCapacityUpgrade(state, config).Success || state.PocketCapacity != 3)
            throw new InvalidOperationException("Pocket Capacity 2->3 upgrade failed on Day 13.");

        // Verify NO covert delivery emails trigger on Days 1-9; OP-01 / OP-02 live on Day 10+
        CovertMissionDatabase missionDb = CovertMissionDatabase.Load();
        foreach (CovertMissionSpec spec in missionDb.Missions)
        {
            if (spec.MinAvailableDay < 6)
                throw new InvalidOperationException($"Covert mission {spec.MissionId} illegally available before Day 6.");
            if (spec.IsDeliveryMission && spec.MinAvailableDay < 10)
                throw new InvalidOperationException($"Covert delivery mission {spec.MissionId} illegally available before Day 10.");
            if (spec.IsDeliveryMission && spec.TriggerEmailDay is > 0 and < 10)
                throw new InvalidOperationException($"Covert delivery email {spec.MissionId} illegally triggers before Day 10.");
        }

        GameState d19State = new() { SuppressDiskPersistence = true };
        d19State.ResetForNewSave(1);
        for (int d = 1; d <= 9; d++)
        {
            economy.DeliverScheduledEmailsForPhase(d19State, d, QuartersPhase.Morning, config);
            economy.DeliverScheduledEmailsForPhase(d19State, d, QuartersPhase.Evening, config);
        }
        if (d19State.Emails.Any(e => e.IsEncrypted))
            throw new InvalidOperationException("Encrypted covert delivery emails must NEVER trigger on Days 1-9.");

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
            !d6State.Emails.Any(e => e.IsTerminalHackEmail && e.Sender == "UNKNOWN // RELAY_D4" && e.Subject == "CHECK YOUR CHUTE" && !e.IsRead))
        {
            throw new InvalidOperationException("Day 6 Morning unsolicited 0500 Blank Data Slate drop or CHECK YOUR CHUTE email failed.");
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
            DisplayName = "STOLEN SLATE: MNG-402-B",
            TargetShipPrefix = "MNG-",
            TargetShipRegistration = "MNG-402-B",
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

        // Day 10+ Covert Delivery Missions: Accept Type 1 Delivery (queues sealed Mission Slate for 0500) + Type 2 Intercept + DECRYPT [0 SC]
        state.AdvanceToDay(10);
        state.TerminalHackedUnlocked = true;
        state.QuartersPhase = QuartersPhase.Evening;
        economy.DeliverScheduledEmailsForPhase(state, 10, QuartersPhase.Evening, config);
        int scripPreDecrypt = state.PactScrip;
        int logsPreDecrypt = state.UnscrubbedLogCount;
        var (decOk, _, _) = economy.TryDecryptNextMessage(state);
        if (!decOk || state.PactScrip != scripPreDecrypt || state.UnscrubbedLogCount <= logsPreDecrypt ||
            !state.PendingChuteDeliveries.Any(i => i.ItemType == ContrabandPocketItem.TypeMissionDataSlate && i.MissionId == "slate_bus_mission_01"))
        {
            throw new InvalidOperationException("Day 10 DECRYPT MESSAGE (0 SC) failed to decrypt OP-01 free, log [UNAUTH_IO], or queue sealed Mission Slate.");
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
        state.AdvanceToDay(3);
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
            TerminalOverlay.TabSpecs[3].Label != "CRYPTO / LOGS" || !TerminalOverlay.TabSpecs[3].IsUnofficial ||
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

            // 5. Verify Day 3 Kordani Officer hail also streams with TalkingHead pacing & unloads cleanly
            config.TalkingHeadAdvanceMode = TalkingHeadAdvanceMode.AutoAdvance;
            gameState.Day2SuspectDetained = true;
            gameState.Day3PreShiftHailCompleted = false;
            gameState.AdvanceToDay(3);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsDay3KordaniHailActive || !deskScreen.HasPreShiftPortraitTextureLoaded)
                throw new InvalidOperationException("Day 3 Kordani Live Hail did not trigger.");
            deskScreen.Update(0.25f, idle);
            deskScreen.Update(0.50f, idle);
            if (!deskScreen.CommsQueue.IsTypingActiveLine || deskScreen.CommsQueue.ActiveVisibleCharCount > 32)
                throw new InvalidOperationException("Day 3 Kordani Live Hail did not stream at slower talking-head CPS.");
            deskScreen.Update(0.016f, stripBtnClick);
            deskScreen.Update(0.016f, stripBtnClick);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.HasPreShiftPortraitTextureLoaded || !deskScreen.CanTriggerNextHailNow)
                throw new InvalidOperationException("Day 3 Kordani Live Hail failed to unload portrait cleanly on acknowledge.");

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

            // Taking the Day 6 chute Blank Data Slate without reading CHECK YOUR CHUTE blocks door exit with MESSAGES nudge
            ContrabandPocketItem d6Blank = gameState.ServiceChuteItems[0];
            gameState.ServiceChuteItems.Clear();
            gameState.PocketItems.Add(d6Blank);
            if (!quartersScreen.IsDoorGatedByDay6UnreadChuteDrop)
                throw new InvalidOperationException("IsDoorGatedByDay6UnreadChuteDrop did not trigger when Day 6 chute slate was collected before reading hack email.");

            quartersScreen.Update(0.016f, new VirtualMouseState { X = 80, Y = 500, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            if (screenManager.CurrentScreenId != ScreenId.Quarters ||
                !quartersScreen.ActiveNudgeText.Contains("MESSAGES", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Quarters door did not block Day 6 unread-chute trap with a [MESSAGES] nudge.");
            }

            // Fail-safe auto-unlock if a suspicious security log is recorded on Day 6+ before TerminalHackedUnlocked
            gameState.AddSecurityLog("[UNAUTH_IO]", "SMOKE_PORT_SWAP_FAILSAFE", isSuspicious: true);
            if (!gameState.TerminalHackedUnlocked)
                throw new InvalidOperationException("Day 6+ AddSecurityLog(isSuspicious: true) fail-safe did not unlock TerminalHackedUnlocked.");

            // 5. Centralized ConfigFlags unlock days & custom ShipGenerator(customConfig) gating
            if (config.DayUnlockManualPart1 != 5 ||
                config.DayUnlockManualHulls != 7 ||
                config.DayUnlockWaferCargo != 10 ||
                config.DayUnlockStoker != 13 ||
                config.DayUnlockFaceBribes != 16 ||
                config.DayUnlockCargoBioScan != 19 ||
                config.DayUnlockHostilityDestroy != 20 ||
                config.DayUnlockTransitPass != 22 ||
                config.DayPactClimax != 23)
            {
                throw new InvalidOperationException("ConfigFlags default DayUnlock thresholds do not match Build 4.3.6 25-day spec.");
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
            // 1. Verify CovertMissionDatabase & ConfigFlags loaded from data/config.json match Build 4.3.6 defaults
            CovertMissionDatabase missionDb = CovertMissionDatabase.Reload();
            Core.ConfigFlags diskConfig = Core.ConfigFlags.Load();
            if (diskConfig.ShiftDurationDay1 != 210 ||
                diskConfig.ShiftDurationDays2To4 != 240 ||
                diskConfig.ShiftDurationDays5To9 != 255 ||
                diskConfig.ShiftDurationDays10To15 != 270 ||
                diskConfig.ShiftDurationDays16Plus != 285 ||
                diskConfig.DayUnlockManualPart1 != 5 ||
                diskConfig.DayUnlockManualHulls != 7 ||
                diskConfig.DayUnlockWaferCargo != 10 ||
                diskConfig.DayUnlockStoker != 13 ||
                diskConfig.DayUnlockFaceBribes != 16 ||
                diskConfig.DayUnlockCargoBioScan != 19 ||
                diskConfig.DayUnlockHostilityDestroy != 20 ||
                diskConfig.DayUnlockTransitPass != 22 ||
                diskConfig.DayPactClimax != 23 ||
                diskConfig.PocketUpgrade2MinDay != 6 ||
                diskConfig.PocketUpgrade2Cost != 60 ||
                diskConfig.PocketUpgrade3MinDay != 13 ||
                diskConfig.PocketUpgrade3Cost != 120)
            {
                throw new InvalidOperationException("data/config.json did not load Build 4.3.6 variable shift clock & 25-day spaced defaults.");
            }

            // 2. Assert ConfigFlags.GetShiftDurationSeconds across all 5 campaign stages
            if (config.GetShiftDurationSeconds(1) != 210 ||
                config.GetShiftDurationSeconds(2) != 240 ||
                config.GetShiftDurationSeconds(3) != 240 ||
                config.GetShiftDurationSeconds(4) != 240 ||
                config.GetShiftDurationSeconds(5) != 255 ||
                config.GetShiftDurationSeconds(6) != 255 ||
                config.GetShiftDurationSeconds(9) != 255 ||
                config.GetShiftDurationSeconds(10) != 270 ||
                config.GetShiftDurationSeconds(15) != 270 ||
                config.GetShiftDurationSeconds(16) != 285 ||
                config.GetShiftDurationSeconds(23) != 285 ||
                config.GetShiftDurationSeconds(25) != 285)
            {
                throw new InvalidOperationException("ConfigFlags.GetShiftDurationSeconds returned incorrect stage duration(s).");
            }

            // 3. Assert ShiftController & DeskScreen active duration + BrownDwarfArcProgress scaling (Day 1 = 210s, Day 23 = 285s)
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

            gameState.AdvanceToDay(23);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (MathF.Abs(deskScreen.ActiveShiftDurationSeconds - 285f) > 0.01f)
                throw new InvalidOperationException($"Expected Day 23 DeskScreen ActiveShiftDurationSeconds == 285, got {deskScreen.ActiveShiftDurationSeconds}.");

            deskScreen.SmokeSetShiftElapsedSeconds(142.5f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 0.5f) > 0.001f || deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 23 BrownDwarfArcProgress expected 0.50 (unexpired) at 142.5s / 285s.");
            deskScreen.SmokeSetShiftElapsedSeconds(285f);
            if (MathF.Abs(deskScreen.BrownDwarfArcProgress - 1.0f) > 0.001f || !deskScreen.IsShiftTimerExpired)
                throw new InvalidOperationException("Day 23 BrownDwarfArcProgress expected 1.00 (expired) at 285s / 285s.");

            // 4. Assert Day 4 Breather (zero Manual infractions, Manual locked, zero REFERENCE MANUAL text in directive)
            ShipGenerator gen = new(config);
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(4);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.IsManualUnlocked)
                throw new InvalidOperationException("Day 4 breather shift must NOT unlock Kordani Reference Manual Tablet.");
            string[] day4Dir = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                4, gameState.Day2SuspectDetained, gameState.Day2SuspectRegistration, gameState.Day2SuspectDeclaredOrigin, config);
            if (day4Dir.Any(l => l.Contains("REFERENCE MANUAL", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Day 4 Shift Directive must not announce Kordani Reference Manual.");

            for (int i = 0; i < 120; i++)
            {
                ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: 436u, DebugSpawnValidityMode.ForceInvalid, config: config);
                if (s.GroundTruth.ActiveInfractions.Any(inf => inf.Type is
                    ShipInfractionType.ExpiredRegistration or
                    ShipInfractionType.PrefixOrganizationMismatch or
                    ShipInfractionType.PrefixSpeciesMismatch or
                    ShipInfractionType.PrefixHullSilhouetteMismatch or
                    ShipInfractionType.UnexplainedCargoPowerDraw))
                {
                    throw new InvalidOperationException($"Day 4 breather generated disallowed Manual/Cargo infraction: {s.GroundTruth.ActiveInfractions[0].Type}");
                }
            }

            // 5. Assert Day 5 Manual Part 1 (Expired, Org, Species; NO Hull Silhouettes)
            gameState.AdvanceToDay(5);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsManualUnlocked || deskScreen.IsManualHullsEnforced)
                throw new InvalidOperationException("Day 5 must unlock Manual Part 1 without enforcing Manual Part 2 hulls.");
            HashSet<ShipInfractionType> seenD5 = [];
            for (int i = 0; i < 180; i++)
            {
                ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: 505u, DebugSpawnValidityMode.ForceInvalid, config: config);
                ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                seenD5.Add(t);
                if (t is ShipInfractionType.PrefixHullSilhouetteMismatch or ShipInfractionType.UnexplainedCargoPowerDraw)
                    throw new InvalidOperationException($"Day 5 generated disallowed post-Day-5 infraction: {t}");
            }
            if (!seenD5.Contains(ShipInfractionType.ExpiredRegistration) ||
                !seenD5.Contains(ShipInfractionType.PrefixOrganizationMismatch) ||
                !seenD5.Contains(ShipInfractionType.PrefixSpeciesMismatch))
            {
                throw new InvalidOperationException("Day 5 failed to generate Manual Part 1 infractions.");
            }

            // 6. Assert Day 6 Pinned Hack + Blank Data Slate + MNG- Intro + Pocket Upgrade 2 (60 SC) + No Hulls Yet
            gameState.ResetForNewSave(1);
            gameState.AdvanceToDay(6);
            gameState.QuartersPhase = QuartersPhase.Morning;
            EconomyEngine econ = new();
            econ.EnsureMorningRosterGenerated(gameState, gen, config);
            econ.EnsureDay6MorningUnsolicitedDrop(gameState);
            if (!gameState.Emails.Any(e => e.IsTerminalHackEmail && e.Subject == "CHECK YOUR CHUTE" && !e.IsRead) ||
                !gameState.ServiceChuteItems.Any(i => i.ItemType == ContrabandPocketItem.TypeBlankDataSlate))
            {
                throw new InvalidOperationException("Day 6 pinned CHECK YOUR CHUTE email or free 0500 Blank Data Slate drop failed.");
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
            if (!econ.TryPurchasePocketCapacityUpgrade(gameState, config).Success || gameState.PocketCapacity != 2)
                throw new InvalidOperationException("Day 6 Pocket Upgrade 2 (60 SC) failed.");

            List<ShipSpec> d6Queue = gen.GenerateDailyQueue(gameState, Constants.PreGeneratedQueueSize, config: config);
            if (!d6Queue.Any(s => s.Slate.DeclaredPrefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)) ||
                !gameState.TodayMorningRoster.Any(r => r.IsRealArrival && r.Prefix.Equals("MNG-", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Day 6 failed to guarantee MNG- in queue and Morning Ship Roster.");
            }

            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (deskScreen.IsManualHullsEnforced)
                throw new InvalidOperationException("Day 6 must NOT enforce Manual Part 2 Hull Silhouettes.");

            // 7. Assert Day 7 Manual Part 2 (Hull Silhouette + Ship Class; no Cargo Power Draw yet)
            gameState.AdvanceToDay(7);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();
            if (!deskScreen.IsManualHullsEnforced || deskScreen.IsCargoPowerUnlocked)
                throw new InvalidOperationException("Day 7 must enforce Manual Part 2 Hull Silhouettes without unlocking Day 10 Cargo.");
            HashSet<ShipInfractionType> seenD7 = [];
            for (int i = 0; i < 180; i++)
            {
                ShipSpec s = gen.GenerateShipForSlot(gameState, i, reseedNonce: 707u, DebugSpawnValidityMode.ForceInvalid, config: config);
                ShipInfractionType t = s.GroundTruth.ActiveInfractions[0].Type;
                seenD7.Add(t);
                if (t == ShipInfractionType.UnexplainedCargoPowerDraw)
                    throw new InvalidOperationException("Day 7 leaked Day 10+ UnexplainedCargoPowerDraw.");
            }
            if (!seenD7.Contains(ShipInfractionType.PrefixHullSilhouetteMismatch))
                throw new InvalidOperationException("Day 7 failed to generate Manual Part 2 PrefixHullSilhouetteMismatch.");

            // 8. Verify placeholder Covert Delivery missions shifted to Day 10+
            CovertMissionSpec? op01 = missionDb.Missions.FirstOrDefault(m => m.MissionId == "slate_bus_mission_01");
            CovertMissionSpec? op02 = missionDb.Missions.FirstOrDefault(m => m.MissionId == "slate_bus_mission_02");
            if (op01 == null || op01.MinAvailableDay < 10 || op01.TriggerEmailDay < 10 ||
                op02 == null || op02.MinAvailableDay < 10 || op02.TriggerEmailDay < 10)
            {
                throw new InvalidOperationException("OP-01 / OP-02 Covert Delivery missions must trigger starting Day 10+.");
            }

            // 9. Assert F1 DebugOverlay Day Jump Range 1..25 & Telemetry Bar Duration Readout
            gameState.AdvanceToDay(1);
            screenManager.SwitchTo(ScreenId.Desk);
            tutorialOverlay.ForceCloseSilent();

            debugOverlay.IsVisible = false;
            debugOverlay.IsVisible = true;
            // Click '< DAY' to ensure clamping at 1, then click 'DAY >' 30 times to verify 25 ceiling
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 20, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            for (int step = 0; step < 30; step++)
            {
                debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 80, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            }
            if (debugOverlay.SelectedJumpDay != 25)
                throw new InvalidOperationException($"F1 DebugOverlay DAY > jumper expected ceiling 25, got {debugOverlay.SelectedJumpDay}.");

            // Click 'LOAD DAY 25' -> jumps cleanly to Day 25 with 285s shift duration
            debugOverlay.HandleInput(new VirtualMouseState { X = 16 + 150, Y = 40 + 220, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
            tutorialOverlay.ForceCloseSilent();
            if (gameState.Day != 25 || MathF.Abs(deskScreen.ActiveShiftDurationSeconds - 285f) > 0.01f)
                throw new InvalidOperationException($"LOAD DAY 25 failed (day={gameState.Day}, shift={deskScreen.ActiveShiftDurationSeconds}s).");

            string telemetryLine = debugOverlay.BuildPrimaryTelemetryLine(idle);
            if (!telemetryLine.Contains("D25", StringComparison.Ordinal) ||
                !telemetryLine.Contains("285s/4m45s", StringComparison.Ordinal) ||
                Raylib.MeasureText(telemetryLine, 10) > 452)
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

        // 10. Day 3 Tutorial Popup
        gameState.AdvanceToDay(3);
        tutorialOverlay.OnDay3Start(force: true);
        RenderAndSave("09_day3_tutorial_popup.png");
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
        gameState.AdvanceToDay(3);
        gameState.Day3PreShiftHailCompleted = true;
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
        gameState.PocketItems.Add(new ContrabandPocketItem
        {
            ItemId = "vis_mng_stolen",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = "STOLEN SLATE: MNG-304-V",
            TargetShipPrefix = "MNG-",
            TargetShipRegistration = "MNG-304-V",
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
        gameState.AddSecurityLog("[UNAUTH_IO]", "SLATE_BUS PORT SWAP: MNG-304-V (MNG-)", isSuspicious: true);
        gameState.AddSecurityLog("[AUDIT_FLAG]", "MISSION TX -> RELAY_D4: MNG-304-V", isSuspicious: true);

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

        config.DeskUpperLayoutMode = prevMode;
        devDebug.Use3xPortraitInLayoutB = prev3x;
        deskScreen.SyncLayoutFromConfig();
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
