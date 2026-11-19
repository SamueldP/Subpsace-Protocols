using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.UI;

namespace SubspaceProtocols;

public static class Program
{
    public static int Main(string[] args)
    {
        bool isSmokeTest = args.Any(a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));

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

        screenManager.SwitchTo(ScreenId.MainMenu);
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
                if (smokeFrameCounter < smokeRouteOrder.Length)
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

        return 0;
    }
}
