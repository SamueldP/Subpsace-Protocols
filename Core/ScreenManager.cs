using SubspaceProtocols.UI;

namespace SubspaceProtocols.Core;

public enum ScreenId
{
    MainMenu = 0,
    Corridor = 1,
    Desk = 2,
    Quarters = 3,
    ArtTest = 4
}

/// <summary>
/// Routes between MainMenu, Corridor, Desk, Quarters, and ArtTest screens,
/// ensuring clean OnExit / OnEnter lifecycle transitions and VRAM cache cleanup.
/// </summary>
public sealed class ScreenManager
{
    private readonly Dictionary<ScreenId, IScreen> _screens = [];
    private IScreen? _activeScreen;

    public ScreenId CurrentScreenId { get; private set; } = ScreenId.MainMenu;
    public ScreenId NextCorridorDestination { get; set; } = ScreenId.Desk;

    public void RegisterScreen(ScreenId id, IScreen screen)
    {
        _screens[id] = screen;
    }

    public void SwitchTo(ScreenId targetId)
    {
        if (!_screens.TryGetValue(targetId, out IScreen? nextScreen))
            return;

        if (_activeScreen != null)
        {
            _activeScreen.OnExit();
        }

        CurrentScreenId = targetId;
        _activeScreen = nextScreen;
        AudioHooks.Play($"screen_enter_{targetId.ToString().ToLowerInvariant()}");
        _activeScreen.OnEnter();
    }

    public void RouteViaCorridor(ScreenId destination)
    {
        NextCorridorDestination = destination;
        SwitchTo(ScreenId.Corridor);
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _activeScreen?.Update(deltaTime, mouse);
    }

    public void DrawVirtual(VirtualMouseState mouse)
    {
        _activeScreen?.DrawVirtual(mouse);
    }

    public void UnloadAllScreens()
    {
        _activeScreen?.OnExit();
        foreach (IScreen screen in _screens.Values)
        {
            screen.Unload();
        }
    }
}
