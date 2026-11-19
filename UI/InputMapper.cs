using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

public readonly struct ViewportTransform
{
    public int WindowWidth { get; init; }
    public int WindowHeight { get; init; }
    public int Scale { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int ScaledWidth { get; init; }
    public int ScaledHeight { get; init; }

    public Rectangle DestinationRect => new(OffsetX, OffsetY, ScaledWidth, ScaledHeight);
}

public readonly struct VirtualMouseState
{
    public int ScreenX { get; init; }
    public int ScreenY { get; init; }
    public int X { get; init; } // Clamped 0..639
    public int Y { get; init; } // Clamped 0..359
    public bool IsInsideViewport { get; init; }
    public bool LeftPressed { get; init; }
    public bool LeftDown { get; init; }
    public bool LeftReleased { get; init; }
    public bool RightPressed { get; init; }
    public int Scale { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }

    public Vector2 Position => new(X, Y);

    public bool IsHit(int rx, int ry, int rw, int rh) =>
        IsInsideViewport && X >= rx && X < rx + rw && Y >= ry && Y < ry + rh;

    public bool IsHit(Rectangle rect) =>
        IsHit((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
}

/// <summary>
/// Computes integer scaling and black letterbox offsets, and converts desktop window
/// mouse coordinates accurately into 0..639 x 0..359 virtual coordinates.
/// </summary>
public static class InputMapper
{
    public static ViewportTransform ComputeViewport(int windowWidth, int windowHeight)
    {
        int safeW = Math.Max(1, windowWidth);
        int safeH = Math.Max(1, windowHeight);

        int scaleX = safeW / Constants.VirtualWidth;
        int scaleY = safeH / Constants.VirtualHeight;
        int scale = Math.Max(1, Math.Min(scaleX, scaleY));

        int scaledW = Constants.VirtualWidth * scale;
        int scaledH = Constants.VirtualHeight * scale;

        int offsetX = (safeW - scaledW) / 2;
        int offsetY = (safeH - scaledH) / 2;

        return new ViewportTransform
        {
            WindowWidth = safeW,
            WindowHeight = safeH,
            Scale = scale,
            OffsetX = offsetX,
            OffsetY = offsetY,
            ScaledWidth = scaledW,
            ScaledHeight = scaledH
        };
    }

    public static (int VirtualX, int VirtualY, bool InsideViewport) ScreenToVirtual(
        int screenX,
        int screenY,
        ViewportTransform viewport)
    {
        int relX = screenX - viewport.OffsetX;
        int relY = screenY - viewport.OffsetY;

        bool inside = relX >= 0 && relX < viewport.ScaledWidth &&
                      relY >= 0 && relY < viewport.ScaledHeight;

        int rawX = (int)Math.Floor((double)relX / viewport.Scale);
        int rawY = (int)Math.Floor((double)relY / viewport.Scale);

        int clampedX = Math.Clamp(rawX, 0, Constants.VirtualWidth - 1);
        int clampedY = Math.Clamp(rawY, 0, Constants.VirtualHeight - 1);

        return (clampedX, clampedY, inside);
    }

    public static VirtualMouseState SampleMouse(ViewportTransform viewport)
    {
        int screenX = Raylib.GetMouseX();
        int screenY = Raylib.GetMouseY();
        (int vx, int vy, bool inside) = ScreenToVirtual(screenX, screenY, viewport);

        return new VirtualMouseState
        {
            ScreenX = screenX,
            ScreenY = screenY,
            X = vx,
            Y = vy,
            IsInsideViewport = inside,
            LeftPressed = inside && Raylib.IsMouseButtonPressed(MouseButton.Left),
            LeftDown = inside && Raylib.IsMouseButtonDown(MouseButton.Left),
            LeftReleased = Raylib.IsMouseButtonReleased(MouseButton.Left),
            RightPressed = inside && Raylib.IsMouseButtonPressed(MouseButton.Right),
            Scale = viewport.Scale,
            OffsetX = viewport.OffsetX,
            OffsetY = viewport.OffsetY
        };
    }
}
