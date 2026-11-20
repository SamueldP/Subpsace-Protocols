using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Procedural;
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
        GameState gameState = new();

        // Resizable desktop window defaulting to 1280x720 (2x integer scale), zero MSAA
        Raylib.SetConfigFlags(Raylib_cs.ConfigFlags.ResizableWindow | Raylib_cs.ConfigFlags.VSyncHint);
        Raylib.InitWindow(
            Constants.DefaultWindowWidth,
            Constants.DefaultWindowHeight,
            "Subspace Protocols — Void Gate 3 (Booth 04)");
        Raylib.SetWindowMinSize(Constants.VirtualWidth, Constants.VirtualHeight);
        Raylib.SetTargetFPS(60);

        // Ensure default Raylib font uses crisp point filtering (zero font smoothing)
        Font defaultFont = Raylib.GetFontDefault();
        if (defaultFont.Texture.Id != 0)
        {
            Raylib.SetTextureFilter(defaultFont.Texture, TextureFilter.Point);
        }

        using VirtualCanvas virtualCanvas = new();
        ScreenManager screenManager = new();

        screenManager.RegisterScreen(ScreenId.MainMenu, new MainMenuScreen(screenManager, gameState, config));
        screenManager.RegisterScreen(ScreenId.Corridor, new CorridorScreen(screenManager, gameState));
        screenManager.RegisterScreen(ScreenId.Desk, new DeskScreen(screenManager, gameState, config));
        screenManager.RegisterScreen(ScreenId.Quarters, new QuartersScreen(screenManager, gameState));
        screenManager.RegisterScreen(ScreenId.ArtTest, new ArtTestScreen(screenManager));

        screenManager.SwitchTo(isArtTestFlag ? ScreenId.ArtTest : ScreenId.MainMenu);
        DebugOverlay debugOverlay = new(screenManager, gameState, config);

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

            debugOverlay.HandleInput();
            screenManager.Update(dt, mouse);

            virtualCanvas.BeginVirtualDraw();
            screenManager.DrawVirtual(mouse);
            debugOverlay.DrawVirtual(mouse);
            virtualCanvas.EndVirtualDrawAndBlit();

            if (isSmokeTest)
            {
                if (smokeFrameCounter == 0 && !isArtTestFlag)
                {
                    RunProceduralEngineVerificationSuite();
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

            // Verify 4 different seeds of the same species produce distinct monochrome HOLO buffers
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

        // 4. Verify all 11 ship factions/orgs across allowed classes, Live/Manual/SPS modes, and GPU texture disposal
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
                    });
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
