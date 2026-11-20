using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Manages the 960x540 offscreen RenderTexture2D virtual framebuffer (TextureFilter.Point),
/// monitor-aware automatic integer window scaling (2x = 1920x1080), black letterboxing,
/// and borderless fullscreen toggle (Alt+Enter / F11).
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

    /// <summary>
    /// Detects the active monitor resolution and automatically sizes the desktop window
    /// to the largest clean integer scale of 960x540 that fits (using borderless windowed
    /// at exact 2x 1920x1080 on 1080p monitors so zero pixels are clipped by titlebars).
    /// </summary>
    public void ApplyOptimalMonitorScaling(bool allowBorderlessFullscreen = true)
    {
        int monitor = Raylib.GetCurrentMonitor();
        int monW = Raylib.GetMonitorWidth(monitor);
        int monH = Raylib.GetMonitorHeight(monitor);

        if (monW <= 0 || monH <= 0)
        {
            UpdateViewport();
            return;
        }

        int maxScaleX = monW / Constants.VirtualWidth;
        int maxScaleY = monH / Constants.VirtualHeight;
        int bestScale = Math.Max(1, Math.Min(maxScaleX, maxScaleY));

        int targetW = Constants.VirtualWidth * bestScale;
        int targetH = Constants.VirtualHeight * bestScale;

        // If the largest clean integer scale exactly fills the monitor (e.g., 1920x1080 = 2x of 960x540),
        // launch in borderless windowed mode so the entire 2x integer canvas fits without titlebar clipping.
        bool exactMonitorFill = targetW >= monW || targetH >= monH;
        if (allowBorderlessFullscreen && exactMonitorFill && bestScale >= 2)
        {
            if (!IsBorderlessFullscreen)
            {
                Raylib.ToggleBorderlessWindowed();
                IsBorderlessFullscreen = true;
            }
        }
        else
        {
            // Leave room for window chrome if a bordered window would exceed monitor height
            if (bestScale > 1 && (targetW > monW - 16 || targetH > monH - 64))
            {
                bestScale--;
                targetW = Constants.VirtualWidth * bestScale;
                targetH = Constants.VirtualHeight * bestScale;
            }

            Raylib.SetWindowSize(targetW, targetH);
            int posX = Math.Max(0, (monW - targetW) / 2);
            int posY = Math.Max(28, (monH - targetH) / 2);
            Raylib.SetWindowPosition(posX, posY);
        }

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
            if (!IsBorderlessFullscreen)
            {
                // Restore clean 1x centered window when leaving borderless fullscreen on 1080p
                int monitor = Raylib.GetCurrentMonitor();
                int monW = Raylib.GetMonitorWidth(monitor);
                int monH = Raylib.GetMonitorHeight(monitor);
                Raylib.SetWindowSize(Constants.VirtualWidth, Constants.VirtualHeight);
                if (monW > Constants.VirtualWidth && monH > Constants.VirtualHeight)
                {
                    Raylib.SetWindowPosition((monW - Constants.VirtualWidth) / 2, (monH - Constants.VirtualHeight) / 2);
                }
            }
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
        // Black letterboxing outside the integer-scaled 960x540 canvas
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
