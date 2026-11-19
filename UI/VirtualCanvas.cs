using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Manages the 640x360 offscreen RenderTexture2D virtual framebuffer (TextureFilter.Point),
/// integer scaling blit with black letterboxing, and borderless fullscreen toggle (Alt+Enter / F11).
/// </summary>
public sealed class VirtualCanvas : IDisposable
{
    private RenderTexture2D _target;
    private bool _isDisposed;

    public ViewportTransform CurrentViewport { get; private set; }
    public bool IsBorderlessFullscreen { get; private set; }

    public VirtualCanvas()
    {
        _target = Raylib.LoadRenderTexture(Constants.VirtualWidth, Constants.VirtualHeight);
        Raylib.SetTextureFilter(_target.Texture, TextureFilter.Point);
        UpdateViewport();
    }

    public void UpdateViewport()
    {
        int winW = Raylib.GetScreenWidth();
        int winH = Raylib.GetScreenHeight();
        CurrentViewport = InputMapper.ComputeViewport(winW, winH);
    }

    public void HandleFullscreenShortcut()
    {
        bool altHeld = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
        bool altEnter = altHeld && Raylib.IsKeyPressed(KeyboardKey.Enter);
        bool f11 = Raylib.IsKeyPressed(KeyboardKey.F11);

        if (altEnter || f11)
        {
            Raylib.ToggleBorderlessWindowed();
            IsBorderlessFullscreen = !IsBorderlessFullscreen;
            AudioHooks.Play("ui_toggle_fullscreen");
            UpdateViewport();
        }
    }

    public void BeginVirtualDraw()
    {
        UpdateViewport();
        Raylib.BeginTextureMode(_target);
        Raylib.ClearBackground(Palette.OutlineVoid);
    }

    public void EndVirtualDrawAndBlit()
    {
        Raylib.EndTextureMode();

        Raylib.BeginDrawing();
        // Black letterboxing outside the integer-scaled 640x360 canvas
        Raylib.ClearBackground(Color.Black);

        // OpenGL RenderTexture2D is Y-flipped, so source height is negative
        Rectangle sourceRect = new(0, 0, Constants.VirtualWidth, -Constants.VirtualHeight);
        Rectangle destRect = CurrentViewport.DestinationRect;

        Raylib.DrawTexturePro(_target.Texture, sourceRect, destRect, Vector2.Zero, 0f, Color.White);
        Raylib.EndDrawing();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        if (_target.Id != 0)
        {
            Raylib.UnloadRenderTexture(_target);
        }
        _isDisposed = true;
    }
}
