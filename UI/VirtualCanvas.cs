using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

/// <summary>
/// Manages the 1280x720 offscreen RenderTexture2D virtual framebuffer,
/// monitor-aware automatic scaling, exact 16:9 black letterboxing,
/// crisp point filtering at integer scales, analytical sharp pixel-upscale shader
/// at non-integer scales (such as 1280x720 -> 1920x1080 1.5x without shimmer),
/// and borderless fullscreen toggle (Alt+Enter / F11).
/// </summary>
public sealed class VirtualCanvas : IDisposable
{
    private const string SharpPixelUpscaleFragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        out vec4 finalColor;
        void main()
        {
            vec2 texSize = vec2(textureSize(texture0, 0));
            vec2 texel = fragTexCoord * texSize;
            vec2 edge = floor(texel + 0.5);
            vec2 dist = texel - edge;
            vec2 width = max(fwidth(texel), vec2(0.0001));
            vec2 clamped = clamp(dist / width, -0.5, 0.5);
            vec2 uv = clamp((edge + clamped) / texSize, vec2(0.5) / texSize, (texSize - vec2(0.5)) / texSize);
            finalColor = texture(texture0, uv) * fragColor;
        }
        """;

    private RenderTexture2D _target;
    private Shader _sharpUpscaleShader;
    private bool _hasSharpShader;
    private TextureFilter _currentTargetFilter = TextureFilter.Point;
    private bool _isDisposed;

    public ViewportTransform CurrentViewport { get; private set; }
    public bool IsBorderlessFullscreen { get; private set; }

    public VirtualCanvas()
    {
        _target = Raylib.LoadRenderTexture(Constants.VirtualWidth, Constants.VirtualHeight);
        Raylib.SetTextureFilter(_target.Texture, TextureFilter.Point);
        _currentTargetFilter = TextureFilter.Point;

        try
        {
            _sharpUpscaleShader = Raylib.LoadShaderFromMemory(null, SharpPixelUpscaleFragmentShader);
            _hasSharpShader = _sharpUpscaleShader.Id != 0 && Raylib.IsShaderReady(_sharpUpscaleShader);
        }
        catch
        {
            _hasSharpShader = false;
        }

        UpdateViewport();
    }

    /// <summary>
    /// Detects the active monitor resolution and sizes the desktop window cleanly:
    /// - 1440p / 4K monitors: exact 2x/3x integer fit (borderless fullscreen if exact fill)
    /// - 1080p monitors: crisp 1:1 1280x720 centered window (F11 / Alt+Enter switches to sharp 1.5x 1080p fullscreen)
    /// - Smaller monitors (<= 720p): borderless fullscreen fit with zero titlebar clipping
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

        bool tightMonitorFit = targetW > monW - 16 || targetH > monH - 64;
        if (allowBorderlessFullscreen && tightMonitorFit && (bestScale >= 2 || monW <= Constants.VirtualWidth || monH <= Constants.VirtualHeight))
        {
            if (!IsBorderlessFullscreen)
            {
                Raylib.ToggleBorderlessWindowed();
                IsBorderlessFullscreen = true;
            }
        }
        else
        {
            if (bestScale > 1 && tightMonitorFit)
            {
                bestScale--;
                targetW = Constants.VirtualWidth * bestScale;
                targetH = Constants.VirtualHeight * bestScale;
            }

            Raylib.SetWindowSize(targetW, targetH);
            int posX = Math.Max(0, (monW - targetW) / 2);
            int posY = Math.Max(40, (monH - targetH) / 2);
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
            ToggleFullscreen();
        }
    }

    public void ToggleFullscreen()
    {
        Raylib.ToggleBorderlessWindowed();
        IsBorderlessFullscreen = !IsBorderlessFullscreen;
        if (!IsBorderlessFullscreen)
        {
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
        Raylib.ClearBackground(Color.Black);

        // OpenGL RenderTexture2D is Y-flipped, so source height is negative
        Rectangle sourceRect = new(0, 0, Constants.VirtualWidth, -Constants.VirtualHeight);
        Rectangle destRect = CurrentViewport.DestinationRect;

        bool useSharpShader = _hasSharpShader && !CurrentViewport.IsExactIntegerScale;
        TextureFilter desiredFilter = useSharpShader ? TextureFilter.Bilinear : TextureFilter.Point;
        if (_currentTargetFilter != desiredFilter)
        {
            Raylib.SetTextureFilter(_target.Texture, desiredFilter);
            _currentTargetFilter = desiredFilter;
        }

        if (useSharpShader)
        {
            Raylib.BeginShaderMode(_sharpUpscaleShader);
            Raylib.DrawTexturePro(_target.Texture, sourceRect, destRect, Vector2.Zero, 0f, Color.White);
            Raylib.EndShaderMode();
        }
        else
        {
            Raylib.DrawTexturePro(_target.Texture, sourceRect, destRect, Vector2.Zero, 0f, Color.White);
        }

        Raylib.EndDrawing();
    }

    public void ExportVirtualFramePng(string outputPath)
    {
        if (_target.Id == 0 || string.IsNullOrWhiteSpace(outputPath))
            return;

        Image img = Raylib.LoadImageFromTexture(_target.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ExportImage(img, outputPath);
        Raylib.UnloadImage(img);
    }

    internal Image CaptureVirtualFrameImage()
    {
        Image img = Raylib.LoadImageFromTexture(_target.Texture);
        Raylib.ImageFlipVertical(ref img);
        return img;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        if (_hasSharpShader && _sharpUpscaleShader.Id != 0)
        {
            Raylib.UnloadShader(_sharpUpscaleShader);
            _sharpUpscaleShader = default;
            _hasSharpShader = false;
        }

        if (_target.Id != 0)
        {
            Raylib.UnloadRenderTexture(_target);
            _target = default;
        }
        _isDisposed = true;
    }
}
