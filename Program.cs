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

        Core.ConfigFlags config = Core.ConfigFlags.Load();
        GameState gameState = new() { SuppressDiskPersistence = isSmokeTest };
        DevDebugState devDebug = new();

        // Resizable desktop window initialized at 960x540 (zero MSAA), then auto-scaled to largest clean integer fit on monitor (e.g., 2x = 1920x1080)
        Raylib.SetConfigFlags(Raylib_cs.ConfigFlags.ResizableWindow | Raylib_cs.ConfigFlags.VSyncHint);
        Raylib.InitWindow(
            Constants.VirtualWidth,
            Constants.VirtualHeight,
            "Subspace Protocols -- Void Gate 3 (Booth 04)");
        Raylib.SetWindowMinSize(Constants.VirtualWidth, Constants.VirtualHeight);
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

        screenManager.RegisterScreen(ScreenId.MainMenu, new MainMenuScreen(screenManager, gameState, config));
        screenManager.RegisterScreen(ScreenId.Corridor, new CorridorScreen(screenManager, gameState));
        screenManager.RegisterScreen(ScreenId.Desk, new DeskScreen(screenManager, gameState, config, devDebug));
        screenManager.RegisterScreen(ScreenId.Quarters, new QuartersScreen(screenManager, gameState));
        screenManager.RegisterScreen(ScreenId.ArtTest, new ArtTestScreen(screenManager));

        screenManager.SwitchTo(isArtTestFlag ? ScreenId.ArtTest : ScreenId.MainMenu);
        DebugOverlay debugOverlay = new(screenManager, gameState, config, devDebug);

        int smokeFrameCounter = 0;
        ScreenId[] smokeRouteOrder =
        [
            ScreenId.MainMenu,
            ScreenId.Corridor,
            ScreenId.Desk,
            ScreenId.Quarters,
            ScreenId.ArtTest
        ];

        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();

            virtualCanvas.HandleFullscreenShortcut();
            virtualCanvas.UpdateViewport();

            VirtualMouseState mouse = InputMapper.SampleMouse(virtualCanvas.CurrentViewport);

            bool debugConsumed = debugOverlay.HandleInput(mouse);
            VirtualMouseState screenMouse = debugConsumed
                ? mouse with { LeftPressed = false, LeftDown = false, RightPressed = false }
                : mouse;

            screenManager.Update(dt, screenMouse);

            virtualCanvas.BeginVirtualDraw();
            screenManager.DrawVirtual(screenMouse);
            debugOverlay.DrawVirtual(mouse);
            virtualCanvas.EndVirtualDrawAndBlit();

            if (isSmokeTest)
            {
                if (smokeFrameCounter == 0 && !isArtTestFlag)
                {
                    RunProceduralEngineVerificationSuite();
                    RunWorkShiftAndBranchingVerificationSuite();
                }

                if (!isArtTestFlag && smokeFrameCounter < smokeRouteOrder.Length)
                {
                    screenManager.SwitchTo(smokeRouteOrder[smokeFrameCounter]);
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
        // 1. Verify DeterministicRng FNV-1a & Mulberry32 repeatability
        DeterministicRng rngA = new("capt-vek-409");
        DeterministicRng rngB = new("capt-vek-409");
        if (rngA.InitialHash != rngB.InitialHash || MathF.Abs(rngA.NextFloat() - rngB.NextFloat()) > 1e-7f)
        {
            throw new InvalidOperationException("DeterministicRng failed repeatability check.");
        }

        // 2. Verify all 13 active species & 4 portrait render modes + within-species HOLO seed distinctiveness + GPU texture lifecycle
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
            {
                throw new InvalidOperationException(
                    $"Within-species HOLO seed variation too low for {species}: only {diffPixels} differing pixels.");
            }
        }

        // 3. Verify GU decommissioned rule (Civilian & Cargo ONLY; never unscripted Military)
        if (ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Military, true, false))
        {
            throw new InvalidOperationException("GU Military ship illegally allowed decommissioned weathering.");
        }
        if (!ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Civilian, true, false) ||
            !ShipRenderer.IsDecommissionedEffective(ShipFaction.GalacticUnion, ShipClass.Cargo, true, false))
        {
            throw new InvalidOperationException("GU Civilian/Cargo decommissioned weathering check failed.");
        }

        // 4. Verify all 11 ship factions/orgs across allowed classes, transparent Live/SPS backgrounds, Manual mode, and GPU disposal
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
                        throw new InvalidOperationException(
                            $"Out-of-bounds ship hardpoint metadata for {faction} {allowedClass}.");
                    }

                    // Verify transparent background outside the hull outline
                    if (sBuf.HasPixel(0, 0) || sBuf.HasPixel(Constants.ShipWidth - 1, 0))
                    {
                        throw new InvalidOperationException(
                            $"Expected transparent background pixel on Live/SPS ship buffer for {faction} {allowedClass}.");
                    }

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

        // 5. Verify Day 3 Pre-Shift Kordani Officer live portrait renders & unloads cleanly
        PixelBuffer kdfOfficerBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
        {
            SeedId = Day2And3ScriptedDialogue.Day3KordaniOfficerPortraitSeed,
            Species = PortraitSpecies.Kordani,
            Mode = PortraitRenderMode.Live,
            FactionPrefix = "KDF-"
        });
        Texture2D kdfTex = kdfOfficerBuf.ToTexture2D();
        PixelBuffer.UnloadTexture(ref kdfTex);
    }

    private static void RunWorkShiftAndBranchingVerificationSuite()
    {
        GameState simState = new() { SuppressDiskPersistence = true };
        ShipGenerator generator = new();
        ValidationEngine validator = new();
        WorldSim worldSim = new();
        ShiftController shift = new();

        // 0. Verify New Save Slot Reset initializes strictly on Day 1 with offline spectrometer
        simState.Day = 7;
        simState.PactScrip = 190;
        simState.ClericalWarningsThisShift = 2;
        simState.SecurityStrikes = 2;
        simState.QuestFlags["Day3_SpectrometerRepaired"] = true;
        simState.ResetForNewSave(1);
        if (simState.Day != 1 || simState.PactScrip != 45 || simState.ClericalWarningsThisShift != 0 ||
            simState.SecurityStrikes != 0 || simState.QuestFlags["Day3_SpectrometerRepaired"])
        {
            throw new InvalidOperationException("ResetForNewSave(1) failed strict Day 1 initialization check.");
        }

        shift.StartNewShift(simState);
        IReadOnlyList<ExpectedShipEntry> expectedList = generator.InitializeDaySchedule(1, 777u);
        if (expectedList.Count != Constants.ExpectedShipsPerDay)
        {
            throw new InvalidOperationException($"Expected 10 priority arrivals on Expected Ship List, got {expectedList.Count}.");
        }

        // 1. Verify Day 1 rules purity (NO shield/weapon compliance refusals on Day 1) & 2-3 Expected Ships in first 5
        int expectedInFirstFive = 0;
        int validCount = 0;
        const int totalSample = 60;
        (int curYear, int curMonth) = Constants.GetStationCycle(1);

        for (int i = 0; i < totalSample; i++)
        {
            ShipSpec spec = generator.GenerateNextShip(simState, i, 777u);
            if (string.IsNullOrWhiteSpace(spec.Slate.ShipName) ||
                string.IsNullOrWhiteSpace(spec.Slate.RegistrationCode) ||
                string.IsNullOrWhiteSpace(spec.Slate.DeclaredCaptainName) ||
                string.IsNullOrWhiteSpace(spec.Slate.OriginSystem) ||
                string.IsNullOrWhiteSpace(spec.Slate.DestinationSystem) ||
                string.IsNullOrWhiteSpace(spec.Slate.CargoManifest) ||
                !spec.Slate.RegistrationExpiryDisplay.StartsWith("CYC ") ||
                spec.Slate.DeclaredTonnage <= 0)
            {
                throw new InvalidOperationException("DataSlateModel missing required inspectable fields.");
            }

            if (spec.GroundTruth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.RefusedShieldsOrWeaponsCompliance))
            {
                throw new InvalidOperationException("Day 1 ship must NEVER generate RefusedShieldsOrWeaponsCompliance (locked to Day 14+ / rep <= -9).");
            }

            if (spec.GroundTruth.IsValid)
            {
                if (Constants.CompareCycle(spec.Slate.RegistrationExpiryYear, spec.Slate.RegistrationExpiryMonth, curYear, curMonth) <= 0)
                    throw new InvalidOperationException("Valid Day 1 ship spawned with expired registration.");
                if (!ShipGenerator.IsSpeciesValidForPrefix(spec.Slate.DeclaredPrefix, spec.Slate.DeclaredCaptainSpecies))
                    throw new InvalidOperationException("Valid Day 1 ship spawned with invalid species for restricted prefix.");
                if (spec.GroundTruth.RelayStatus is RelayPingStatus.AlertReportedStolen or RelayPingStatus.AlertHullDecommissioned)
                    throw new InvalidOperationException("Valid Day 1 ship spawned with a Relay Alert flag.");
                if (spec.GroundTruth.TamperedField != PacketTamperField.None || spec.GroundTruth.ActualCargoPowerDraw)
                    throw new InvalidOperationException("Valid Day 1 ship spawned with tampering or unexplained cargo power.");
            }

            if (i < 5 && spec.GroundTruth.IsExpectedShip)
                expectedInFirstFive++;

            if (spec.GroundTruth.IsValid)
                validCount++;
        }

        // 1b. Verify all expanded Day 1 procedural infraction types generate cleanly with ground truth & citation text
        HashSet<ShipInfractionType> seenDay1Infractions = [];
        bool seenTamperedOrg = false;
        bool seenTamperedOrigin = false;
        bool seenRelayStolen = false;
        bool seenRelayDecomm = false;
        ShipGenerator day1InfGen = new();
        day1InfGen.InitializeDaySchedule(1, 909u);
        for (int i = 0; i < 160; i++)
        {
            ShipSpec inv = day1InfGen.GenerateNextShip(simState, i, 909u, DebugSpawnValidityMode.ForceInvalid);
            if (inv.GroundTruth.ActiveInfractions.Count == 0 || string.IsNullOrWhiteSpace(inv.GroundTruth.ActiveInfractions[0].CitationReason))
                throw new InvalidOperationException("ForceInvalid ship missing active infraction or citation reason.");

            ShipInfractionType t = inv.GroundTruth.ActiveInfractions[0].Type;
            seenDay1Infractions.Add(t);
            if (t == ShipInfractionType.PacketTamperingMisspelling)
            {
                if (inv.GroundTruth.TamperedField == PacketTamperField.Organization) seenTamperedOrg = true;
                if (inv.GroundTruth.TamperedField == PacketTamperField.Origin) seenTamperedOrigin = true;
            }
            if (t == ShipInfractionType.RelayRegistryAlert)
            {
                if (inv.GroundTruth.RelayStatus == RelayPingStatus.AlertReportedStolen) seenRelayStolen = true;
                if (inv.GroundTruth.RelayStatus == RelayPingStatus.AlertHullDecommissioned) seenRelayDecomm = true;
            }
        }

        ShipInfractionType[] requiredDay1Infractions =
        [
            ShipInfractionType.ExpiredRegistration,
            ShipInfractionType.PrefixOrganizationMismatch,
            ShipInfractionType.PrefixSpeciesMismatch,
            ShipInfractionType.PacketTamperingMisspelling,
            ShipInfractionType.PrefixHullSilhouetteMismatch,
            ShipInfractionType.UnregisteredPrefix,
            ShipInfractionType.SpoofedWeaponsHot,
            ShipInfractionType.UnexplainedCargoPowerDraw,
            ShipInfractionType.RelayRegistryAlert
        ];
        foreach (ShipInfractionType req in requiredDay1Infractions)
        {
            if (!seenDay1Infractions.Contains(req))
                throw new InvalidOperationException($"Expected Day 1 infraction '{req}' did not appear in 160 invalid samples.");
        }
        if (!seenTamperedOrg || !seenTamperedOrigin || !seenRelayStolen || !seenRelayDecomm)
        {
            throw new InvalidOperationException("Expected all Day 1 Packet Tampering (Org/Origin) and Relay Alert (Stolen/Decomm) sub-variants to generate.");
        }

        if (expectedInFirstFive is < 2 or > 3)
            throw new InvalidOperationException($"Expected 2-3 priority ships in first 5 arrivals, found {expectedInFirstFive}.");

        if (validCount is < 27 or > 50)
            throw new InvalidOperationException($"Unexpected Day 1 valid ship ratio: {validCount}/{totalSample}.");

        // 2. Verify Hold Timer Until First Hail & 270s shift_horn trigger
        shift.Tick(30.0f);
        if (shift.ElapsedShiftSeconds > 0.001f)
            throw new InvalidOperationException("ShiftController must hold timer at t = 0 until first Next Hail.");

        shift.NotifyFirstHailTriggered();
        shift.Tick(270.0f);
        if (!shift.IsShiftTimerExpired || !string.Equals(AudioHooks.LastPlayedSoundId, "shift_horn", StringComparison.Ordinal))
            throw new InvalidOperationException("ShiftController failed to fire 'shift_horn' at t = 270s.");

        // 3. Verify Strict 5-Case ValidationEngine Truth Table (IsValid x Decision x ProtocolReviewVerified)
        {
            GameState ttState = new() { Day = 1, PactScrip = 45, SuppressDiskPersistence = true };
            ShipGenerator ttGen = new();
            ttGen.InitializeDaySchedule(1, 555u);

            ShipSpec validShip = ttGen.GenerateNextShip(ttState, 0, 555u, DebugSpawnValidityMode.ForceValid);
            validShip.GroundTruth.IsExpectedShip = false;
            ShipSpec invalidShip = ttGen.GenerateNextShip(ttState, 1, 555u, DebugSpawnValidityMode.ForceInvalid);
            invalidShip.GroundTruth.IsExpectedShip = false;

            // Case 1: IsValid == true + Grant Access -> Accurate (+15 SC, no citation)
            DecisionEvaluationResult c1 = validator.EvaluateDecision(ttState, validShip, PlayerDecision.GrantAccess, worldSim);
            if (!c1.IsAccurate || c1.IsCitation || c1.ScripDelta != Constants.BasePayPerShipScrip)
                throw new InvalidOperationException("Truth Table Case 1 (Valid + GrantAccess) failed.");

            // Case 2: IsValid == true + Detain -> Mistake ("UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT")
            DecisionEvaluationResult c2 = validator.EvaluateDecision(ttState, validShip, PlayerDecision.Detain, worldSim);
            if (c2.IsAccurate || !c2.IsCitation || !c2.ViolationReason.Contains("UNJUSTIFIED DETAINMENT -- VESSEL WAS COMPLIANT"))
                throw new InvalidOperationException("Truth Table Case 2 (Valid + Detain) failed.");

            // Case 3: IsValid == false + Grant Access -> Mistake (exact missed violation)
            DecisionEvaluationResult c3 = validator.EvaluateDecision(ttState, invalidShip, PlayerDecision.GrantAccess, worldSim);
            if (c3.IsAccurate || !c3.IsCitation || !c3.ViolationReason.Contains("CLEARED INVALID VESSEL --"))
                throw new InvalidOperationException("Truth Table Case 3 (Invalid + GrantAccess) failed.");

            // Case 4: IsValid == false + Detain + ProtocolReviewVerified == false -> Mistake ("UNVERIFIED DETAINMENT -- PROTOCOL REVIEW NOT COMPLETED", no +15 SC)
            invalidShip.ProtocolReviewVerified = false;
            DecisionEvaluationResult c4 = validator.EvaluateDecision(ttState, invalidShip, PlayerDecision.Detain, worldSim);
            if (c4.IsAccurate || !c4.IsCitation || c4.ScripDelta > 0 ||
                c4.ViolationReason != "UNVERIFIED DETAINMENT -- PROTOCOL REVIEW NOT COMPLETED")
                throw new InvalidOperationException("Truth Table Case 4 (Invalid + Detain + Unverified) failed.");

            // Case 5: IsValid == false + Detain + ProtocolReviewVerified == true -> Accurate (+15 SC, no citation)
            invalidShip.ProtocolReviewVerified = true;
            DecisionEvaluationResult c5 = validator.EvaluateDecision(ttState, invalidShip, PlayerDecision.Detain, worldSim);
            if (!c5.IsAccurate || c5.IsCitation || c5.ScripDelta != Constants.BasePayPerShipScrip)
                throw new InvalidOperationException("Truth Table Case 5 (Invalid + Detain + Verified) failed.");
        }

        // 4. Verify ValidationEngine economy & Day 2 Ghost Route suspect branching
        GameState evalState = new() { Day = 2, PactScrip = 45, SuppressDiskPersistence = true };
        ShipGenerator day2Gen = new();
        IReadOnlyList<ExpectedShipEntry> day2Expected = day2Gen.InitializeDaySchedule(2, 888u);
        ShipSpec ghostSuspect = day2Gen.GenerateNextShip(evalState, ShipGenerator.Day2GhostRouteQueueSlot, 888u);

        if (!ghostSuspect.GroundTruth.IsDay2GhostRouteSuspect || ghostSuspect.GroundTruth.IsExpectedShip)
            throw new InvalidOperationException("Day 2 Ghost Route suspect must be flagged and excluded from the Expected Ship List.");

        if (day2Expected.Any(e => e.RegistrationCode.Equals(ghostSuspect.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Day 2 Ghost Route suspect registration collided with Expected Ship List.");

        // Detaining the Day 2 suspect -> clerical warning 1/2, 0 SC payout, -2 suspect faction rep, 0 strikes
        string suspectPrefix = ghostSuspect.Slate.DeclaredPrefix;
        DecisionEvaluationResult d2Detain = validator.EvaluateDecision(evalState, ghostSuspect, PlayerDecision.Detain, worldSim);
        if (d2Detain.IsAccurate || !d2Detain.IsCitation || d2Detain.IsSecurityStrike ||
            !evalState.Day2SuspectDetained || evalState.PactScrip != 45 ||
            evalState.ClericalWarningsThisShift != 1 || evalState.SecurityStrikes != 0 ||
            evalState.GetRep(suspectPrefix) != -2)
        {
            throw new InvalidOperationException("Day 2 Ghost Route suspect Detain evaluation failed required rules.");
        }

        // Advance overnight to Day 3 -> resets clerical warnings to 0, activates spectrometer, silently adds +2 KDF rep
        int kdfRepBefore = evalState.GetRep("KDF-");
        evalState.AdvanceToDay(3);
        if (evalState.Day != 3 || evalState.ClericalWarningsThisShift != 0 ||
            !evalState.QuestFlags["Day3_SpectrometerRepaired"] ||
            evalState.GetRep("KDF-") != kdfRepBefore + 2)
        {
            throw new InvalidOperationException("Overnight transition to Day 3 after Day 2 Detain failed.");
        }

        // Verify both Day 3 Shift Directive variants format cleanly
        string[] varA = Day2And3ScriptedDialogue.BuildDay3ShiftDirectiveLines(false, evalState.Day2SuspectRegistration, evalState.Day2SuspectDeclaredOrigin);
        string[] varB = Day2And3ScriptedDialogue.BuildDay3ShiftDirectiveLines(true, evalState.Day2SuspectRegistration, evalState.Day2SuspectDeclaredOrigin);
        if (!string.Join("\n", varA).Contains("[KDF-SEC-09]") || !string.Join("\n", varB).Contains("[KDF-SEC-09B]"))
        {
            throw new InvalidOperationException("Day 3 Shift Directive Variant A / Variant B text verification failed.");
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
}
