using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

namespace SubspaceProtocols.UI;

/// <summary>
/// Quarters Wall-Mounted Interactive Sector Star Chart Modal (1280x720 virtual canvas).
/// Centered CRT/holo modal at (80, 48, 1120, 624) with an inner 16:9 normalized 0..1 map viewport (176, 86, 928, 522)
/// and a bottom Info Readout Bar (96, 614, 1088, 50).
/// Strictly skips rendering any border named "REMOVE IN PROD" (b134), while using b134's enclosed nodes
/// (VG-3 + 6 neighboring systems) and connecting trade routes to determine clickability.
/// Zero patrol clusters, zero live ship/fleet tracking, zero military telemetry, zero shift-time checks.
/// </summary>
public sealed class StarChartModal
{
    // Centered Modal Frame (strict 8px grid)
    public const int ModalX = 80;
    public const int ModalY = 48;
    public const int ModalW = 1120;
    public const int ModalH = 624;

    public const int HeaderH = 32;
    public const int CloseBtnX = ModalX + ModalW - 36; // 1164
    public const int CloseBtnY = ModalY + 4;           // 52
    public const int CloseBtnW = 28;
    public const int CloseBtnH = 24;

    // Inner 16:9 Map Viewport: 928x522 (58*16 by 58*9, exact 16:9 aspect ratio centered in upper modal)
    public const int MapX = 176;
    public const int MapY = 86;
    public const int MapW = 928;
    public const int MapH = 522;

    // Bottom Info Readout Bar
    public const int ReadoutX = 96;
    public const int ReadoutY = 614;
    public const int ReadoutW = 1088;
    public const int ReadoutH = 50;

    // Hit-test thresholds in virtual 1280x720 pixels
    private const float ClickableNodeHitRadiusPx = 15.0f;
    private const float ClickableRouteHitDistancePx = 7.5f;

    private SectorMapData _mapData;
    private float _elapsedTime;
    private string? _hoveredNodeId;
    private string? _hoveredRouteId;
    private string? _selectedNodeId;
    private string? _selectedRouteId;

    public bool IsOpen { get; private set; }
    public SectorMapData MapData => _mapData;
    public string? HoveredNodeId => _hoveredNodeId;
    public string? HoveredRouteId => _hoveredRouteId;
    public string? SelectedNodeId => _selectedNodeId;
    public string? SelectedRouteId => _selectedRouteId;

    public string ReadoutPrimaryText { get; private set; } = string.Empty;
    public string ReadoutSecondaryText { get; private set; } = string.Empty;
    public string ReadoutTertiaryText { get; private set; } = string.Empty;

    public string ReadoutCombinedLine =>
        string.IsNullOrEmpty(ReadoutTertiaryText)
            ? $"{ReadoutPrimaryText}  //  {ReadoutSecondaryText}"
            : $"{ReadoutPrimaryText}  //  {ReadoutSecondaryText}  //  {ReadoutTertiaryText}";

    public StarChartModal(SectorMapData? mapData = null)
    {
        _mapData = mapData ?? SectorMapData.Load();
        SelectNodeById("n11", playAudio: false);
    }

    public void ReloadMapData(SectorMapData? customData = null)
    {
        _mapData = customData ?? SectorMapData.Load();
        _hoveredNodeId = null;
        _hoveredRouteId = null;
        _selectedNodeId = null;
        _selectedRouteId = null;
        SelectNodeById("n11", playAudio: false);
    }

    public void Open()
    {
        IsOpen = true;
        _hoveredNodeId = null;
        _hoveredRouteId = null;
        if (string.IsNullOrEmpty(_selectedNodeId) && string.IsNullOrEmpty(_selectedRouteId))
        {
            SelectNodeById("n11", playAudio: false);
        }
        AudioHooks.Play("terminal_key_confirm");
    }

    public void Close(bool playAudio = true)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _hoveredNodeId = null;
        _hoveredRouteId = null;
        if (playAudio)
        {
            AudioHooks.Play("terminal_key_click");
        }
    }

    public bool TryConsumeEscape()
    {
        if (!IsOpen)
            return false;

        Close(playAudio: true);
        return true;
    }

    public static (int X, int Y) ProjectToScreen(float normX, float normY)
    {
        int sx = MapX + (int)MathF.Round(normX * MapW);
        int sy = MapY + (int)MathF.Round(normY * MapH);
        return (sx, sy);
    }

    public (int X, int Y) GetNodeScreenPosition(string nodeId)
    {
        if (_mapData.NodesById.TryGetValue(nodeId, out SectorMapNode? node))
        {
            return ProjectToScreen(node.X, node.Y);
        }
        return (MapX + MapW / 2, MapY + MapH / 2);
    }

    public (int X, int Y) GetRouteMidpointScreen(string routeId)
    {
        SectorMapRoute? route = _mapData.Routes.FirstOrDefault(r => r.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase));
        if (route != null &&
            _mapData.NodesById.TryGetValue(route.From, out SectorMapNode? fromNode) &&
            _mapData.NodesById.TryGetValue(route.To, out SectorMapNode? toNode))
        {
            float mx = (fromNode.X + toNode.X) * 0.5f;
            float my = (fromNode.Y + toNode.Y) * 0.5f;
            return ProjectToScreen(mx, my);
        }
        return (MapX + MapW / 2, MapY + MapH / 2);
    }

    public static float PointToSegmentDistance(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        float lenSq = dx * dx + dy * dy;
        if (lenSq <= 1e-5f)
        {
            float ex = px - ax;
            float ey = py - ay;
            return MathF.Sqrt(ex * ex + ey * ey);
        }

        float t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / lenSq, 0f, 1f);
        float projX = ax + t * dx;
        float projY = ay + t * dy;
        float rx = px - projX;
        float ry = py - projY;
        return MathF.Sqrt(rx * rx + ry * ry);
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!IsOpen)
            return;

        _elapsedTime += deltaTime;
        _hoveredNodeId = null;
        _hoveredRouteId = null;

        // Top-right [X] close button
        if (mouse.LeftPressed && mouse.IsHit(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH))
        {
            Close(playAudio: true);
            return;
        }

        if (!mouse.IsInsideViewport || !mouse.IsHit(MapX, MapY, MapW, MapH))
            return;

        // 1. Hit-test clickable nodes first (VG-3 + 6 neighboring systems enclosed by b134)
        SectorMapNode? bestNode = null;
        float bestNodeDist = ClickableNodeHitRadiusPx;

        foreach (SectorMapNode node in _mapData.ClickableNodes)
        {
            var (nx, ny) = ProjectToScreen(node.X, node.Y);
            float dx = mouse.X - nx;
            float dy = mouse.Y - ny;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist <= bestNodeDist)
            {
                bestNodeDist = dist;
                bestNode = node;
            }
        }

        if (bestNode != null)
        {
            _hoveredNodeId = bestNode.Id;
            if (mouse.LeftPressed)
            {
                SelectNode(bestNode, playAudio: true);
            }
            return;
        }

        // 2. Hit-test clickable trade routes via point-to-segment distance check
        SectorMapRoute? bestRoute = null;
        float bestRouteDist = ClickableRouteHitDistancePx;

        foreach (SectorMapRoute route in _mapData.ClickableRoutes)
        {
            if (!_mapData.NodesById.TryGetValue(route.From, out SectorMapNode? fromNode) ||
                !_mapData.NodesById.TryGetValue(route.To, out SectorMapNode? toNode))
            {
                continue;
            }

            var (ax, ay) = ProjectToScreen(fromNode.X, fromNode.Y);
            var (bx, by) = ProjectToScreen(toNode.X, toNode.Y);

            float dist = PointToSegmentDistance(mouse.X, mouse.Y, ax, ay, bx, by);
            if (dist <= bestRouteDist)
            {
                bestRouteDist = dist;
                bestRoute = route;
            }
        }

        if (bestRoute != null)
        {
            _hoveredRouteId = bestRoute.Id;
            if (mouse.LeftPressed)
            {
                SelectRoute(bestRoute, playAudio: true);
            }
        }
    }

    public bool SelectNodeById(string nodeId, bool playAudio = true)
    {
        if (!_mapData.NodesById.TryGetValue(nodeId, out SectorMapNode? node) || !node.IsClickable)
            return false;

        SelectNode(node, playAudio);
        return true;
    }

    public bool SelectRouteById(string routeId, bool playAudio = true)
    {
        SectorMapRoute? route = _mapData.ClickableRoutes.FirstOrDefault(
            r => r.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase));
        if (route == null)
            return false;

        SelectRoute(route, playAudio);
        return true;
    }

    private void SelectNode(SectorMapNode node, bool playAudio)
    {
        _selectedNodeId = node.Id;
        _selectedRouteId = null;

        ReadoutPrimaryText = $"SYSTEM: {node.Name}";
        ReadoutSecondaryText = $"CONTROLLING FACTION: {node.EffectiveFaction}";
        ReadoutTertiaryText = $"SYSTEM TAG: {node.EffectiveTag}";

        if (playAudio)
        {
            AudioHooks.Play("terminal_key_click");
        }
    }

    private void SelectRoute(SectorMapRoute route, bool playAudio)
    {
        _selectedRouteId = route.Id;
        _selectedNodeId = null;

        string fromName = _mapData.NodesById.TryGetValue(route.From, out SectorMapNode? fn) ? fn.Name : route.From;
        string toName = _mapData.NodesById.TryGetValue(route.To, out SectorMapNode? tn) ? tn.Name : route.To;

        ReadoutPrimaryText = $"TRADE ROUTE: {fromName} <-> {toName}";
        ReadoutSecondaryText = $"PRIMARY EXPORT: {route.EffectiveExport}";
        ReadoutTertiaryText = string.Empty;

        if (playAudio)
        {
            AudioHooks.Play("terminal_key_click");
        }
    }

    // =========================================================================
    // RENDERING (Centered CRT/Holo Modal + 16:9 Normalized Projection + Readout)
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!IsOpen)
            return;

        // 1. Dimmed background veil over live Quarters room
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(6, 9, 14, 188));

        // 2. Centered CRT/Holo Modal Shell (80, 48, 1120, 624)
        Raylib.DrawRectangle(ModalX, ModalY, ModalW, ModalH, new Color(11, 16, 22, 255));
        Raylib.DrawRectangleLines(ModalX, ModalY, ModalW, ModalH, Palette.ClearanceTeal);
        Raylib.DrawRectangleLines(ModalX + 2, ModalY + 2, ModalW - 4, ModalH - 4, Palette.OutlineVoid);

        // 3. Top Header Bar (80, 48, 1120, 32)
        Raylib.DrawRectangle(ModalX + 2, ModalY + 2, ModalW - 4, HeaderH - 2, new Color(16, 28, 34, 255));
        Raylib.DrawLine(ModalX + 2, ModalY + HeaderH, ModalX + ModalW - 2, ModalY + HeaderH, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("VOID GATE 3 // SECTOR ASTROGATION & TRADE CORRIDOR CHART", ModalX + 14, ModalY + 11, 10, Palette.ClearanceTeal);
        Raylib.DrawText("CLICK INNER SYSTEM OR TRADE ROUTE FOR TELEMETRY  |  [ESC] CLOSE", ModalX + 684, ModalY + 11, 10, Palette.CrtAmberDimmed);

        bool closeHov = mouse.IsHit(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH);
        Raylib.DrawRectangle(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH, closeHov ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH, Palette.AlertCrimson);
        Raylib.DrawText("X", CloseBtnX + 10, CloseBtnY + 7, 10, closeHov ? Palette.OutlineVoid : Palette.AlertCrimson);

        // 4. Side Astrogation Frame Trim flanking the 16:9 Map Viewport
        DrawSideAstrogationTrim();

        // 5. Inner 16:9 Map Viewport (176, 86, 928, 522)
        Raylib.DrawRectangle(MapX - 2, MapY - 2, MapW + 4, MapH + 4, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(MapX, MapY, MapW, MapH, new Color(7, 11, 16, 255));

        Raylib.BeginScissorMode(MapX, MapY, MapW, MapH);

        DrawMapCoordinateGrid();
        DrawRenderableBorders();
        DrawRegionLabels();
        DrawTradeRoutes();
        DrawNodes();

        Raylib.EndScissorMode();

        Raylib.DrawRectangleLines(MapX, MapY, MapW, MapH, Palette.ClearanceTeal);

        // 6. Bottom Info Readout Bar (96, 614, 1088, 50)
        DrawBottomInfoReadoutBar();
    }

    private static void DrawSideAstrogationTrim()
    {
        // Left & right recessed telemetry rails (x = 96..168 and x = 1112..1184)
        int leftRailX = 96;
        int rightRailX = 1112;
        int railW = 68;

        Raylib.DrawRectangle(leftRailX, MapY, railW, MapH, new Color(9, 13, 18, 255));
        Raylib.DrawRectangleLines(leftRailX, MapY, railW, MapH, Palette.WeatheredGunmetalMid);
        Raylib.DrawText("LAT", leftRailX + 8, MapY + 8, 10, Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(rightRailX, MapY, railW, MapH, new Color(9, 13, 18, 255));
        Raylib.DrawRectangleLines(rightRailX, MapY, railW, MapH, Palette.WeatheredGunmetalMid);
        Raylib.DrawText("KEY", rightRailX + 8, MapY + 8, 10, Palette.WeatheredGunmetalEdge);

        for (int i = 1; i <= 9; i++)
        {
            int ty = MapY + (MapH * i) / 10;
            Raylib.DrawLine(leftRailX + 6, ty, leftRailX + railW - 6, ty, new Color(22, 30, 38, 255));
            Raylib.DrawText($"0.{i}", leftRailX + 20, ty - 12, 10, new Color(56, 74, 86, 255));
        }

        // Compact legend inside right rail
        int ky = MapY + 36;
        Raylib.DrawCircle(rightRailX + 14, ky + 4, 4, Palette.ClearanceTeal);
        Raylib.DrawText("GATE", rightRailX + 24, ky, 10, Palette.ClearanceTeal);
        ky += 28;
        Raylib.DrawCircle(rightRailX + 14, ky + 4, 4, Palette.FromHex("#22c55e"));
        Raylib.DrawText("CORE", rightRailX + 24, ky, 10, Palette.FromHex("#22c55e"));
        ky += 28;
        Raylib.DrawCircle(rightRailX + 14, ky + 4, 4, Palette.AlertCrimson);
        Raylib.DrawText("RIFT", rightRailX + 24, ky, 10, Palette.AlertCrimson);
        ky += 28;
        Raylib.DrawLine(rightRailX + 8, ky + 5, rightRailX + 20, ky + 5, Palette.FromHex("#d4a052"));
        Raylib.DrawText("LANE", rightRailX + 24, ky, 10, Palette.CrtAmberPrimary);
        ky += 28;
        Raylib.DrawCircle(rightRailX + 14, ky + 4, 3, new Color(180, 134, 62, 150));
        Raylib.DrawText("OUTER", rightRailX + 24, ky, 10, Palette.WeatheredGunmetalEdge);
    }

    private static void DrawMapCoordinateGrid()
    {
        Color gridCol = new(14, 22, 30, 255);
        for (int i = 1; i < 10; i++)
        {
            int gx = MapX + (MapW * i) / 10;
            int gy = MapY + (MapH * i) / 10;
            Raylib.DrawLine(gx, MapY, gx, MapY + MapH, gridCol);
            Raylib.DrawLine(MapX, gy, MapX + MapW, gy, gridCol);
        }

        // Faint deterministic background star dust
        for (int s = 0; s < 56; s++)
        {
            int sx = MapX + ((s * 173 + 29) % MapW);
            int sy = MapY + ((s * 97 + 41) % MapH);
            Raylib.DrawPixel(sx, sy, (s % 3 == 0) ? new Color(36, 54, 66, 255) : new Color(22, 32, 42, 255));
        }
    }

    private void DrawRenderableBorders()
    {
        // RenderableBorders strictly excludes any border named "REMOVE IN PROD" (b134)
        foreach (SectorMapBorder border in _mapData.RenderableBorders)
        {
            if (border.IsRemoveInProdMarker)
                continue;

            IReadOnlyList<SectorMapPoint> pts = border.EffectiveGeometryPoints;
            if (pts.Count < 3)
                continue;

            Color baseCol = ParseHexColor(border.Color, Palette.CrtAmberPrimary);
            byte fillA = (byte)Math.Clamp((int)MathF.Round(border.FillAlpha * 255f), 8, 140);
            Color fillCol = new(baseCol.R, baseCol.G, baseCol.B, fillA);

            List<Vector2> screenPts = new(pts.Count);
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float sx = MapX + pts[i].X * MapW;
                float sy = MapY + pts[i].Y * MapH;
                screenPts.Add(new Vector2(sx, sy));
                if (sy < minY) minY = sy;
                if (sy > maxY) maxY = sy;
            }

            // 1. Even-odd scanline polygon fill (handles arbitrary concave curves cleanly)
            int startY = Math.Max(MapY, (int)MathF.Floor(minY));
            int endY = Math.Min(MapY + MapH - 1, (int)MathF.Ceiling(maxY));
            DrawPolygonScanlineFill(screenPts, startY, endY, fillCol);

            // 2. Diagonal 45-degree hazard hatching (e.g. Ashen Rift red hazard b6)
            if (border.Hatch)
            {
                Color hatchCol = new(baseCol.R, baseCol.G, baseCol.B, (byte)96);
                DrawPolygonDiagonalHatch(screenPts, startY, endY, hatchCol, spacingPx: 10);
            }

            // 3. Border outline (solid or dashed)
            Color strokeCol = new(baseCol.R, baseCol.G, baseCol.B, (byte)205);
            DrawPolygonOutline(screenPts, border.Closed, border.Dashed, Math.Clamp(border.Width, 1, 3), strokeCol);
        }
    }

    private static void DrawPolygonScanlineFill(List<Vector2> poly, int startY, int endY, Color fillCol)
    {
        List<float> xHits = new(16);
        int count = poly.Count;

        for (int y = startY; y <= endY; y += 2)
        {
            float scanY = y + 0.5f;
            xHits.Clear();

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = poly[j];
                Vector2 b = poly[i];
                if ((a.Y <= scanY && b.Y > scanY) || (b.Y <= scanY && a.Y > scanY))
                {
                    float t = (scanY - a.Y) / (b.Y - a.Y);
                    xHits.Add(a.X + t * (b.X - a.X));
                }
            }

            xHits.Sort();
            for (int k = 0; k + 1 < xHits.Count; k += 2)
            {
                int x0 = Math.Max(MapX, (int)MathF.Round(xHits[k]));
                int x1 = Math.Min(MapX + MapW, (int)MathF.Round(xHits[k + 1]));
                if (x1 > x0)
                {
                    Raylib.DrawRectangle(x0, y, x1 - x0, 2, fillCol);
                }
            }
        }
    }

    private static void DrawPolygonDiagonalHatch(List<Vector2> poly, int startY, int endY, Color hatchCol, int spacingPx)
    {
        List<float> xHits = new(16);
        int count = poly.Count;

        for (int y = startY; y <= endY; y += 2)
        {
            float scanY = y + 0.5f;
            xHits.Clear();

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = poly[j];
                Vector2 b = poly[i];
                if ((a.Y <= scanY && b.Y > scanY) || (b.Y <= scanY && a.Y > scanY))
                {
                    float t = (scanY - a.Y) / (b.Y - a.Y);
                    xHits.Add(a.X + t * (b.X - a.X));
                }
            }

            xHits.Sort();
            for (int k = 0; k + 1 < xHits.Count; k += 2)
            {
                int x0 = Math.Max(MapX, (int)MathF.Round(xHits[k]));
                int x1 = Math.Min(MapX + MapW - 1, (int)MathF.Round(xHits[k + 1]));
                for (int x = x0; x <= x1; x++)
                {
                    if (((x + y) % spacingPx) == 0)
                    {
                        Raylib.DrawRectangle(x, y, 2, 2, hatchCol);
                    }
                }
            }
        }
    }

    private static void DrawPolygonOutline(List<Vector2> poly, bool closed, bool dashed, int width, Color col)
    {
        int segCount = closed ? poly.Count : poly.Count - 1;
        float dashCarry = 0f;
        const float dashLen = 8.0f;
        const float gapLen = 5.0f;
        const float period = dashLen + gapLen;

        for (int i = 0; i < segCount; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Count];
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float segLen = MathF.Sqrt(dx * dx + dy * dy);
            if (segLen < 0.5f)
                continue;

            if (!dashed)
            {
                Raylib.DrawLineEx(a, b, width, col);
                continue;
            }

            float ux = dx / segLen;
            float uy = dy / segLen;
            float walked = 0f;
            while (walked < segLen)
            {
                float cyclePos = (dashCarry + walked) % period;
                bool isOn = cyclePos < dashLen;
                float step = isOn ? (dashLen - cyclePos) : (period - cyclePos);
                float nextWalked = MathF.Min(segLen, walked + step);
                if (isOn && nextWalked > walked)
                {
                    Vector2 p0 = new(a.X + ux * walked, a.Y + uy * walked);
                    Vector2 p1 = new(a.X + ux * nextWalked, a.Y + uy * nextWalked);
                    Raylib.DrawLineEx(p0, p1, width, col);
                }
                walked = nextWalked;
            }
            dashCarry = (dashCarry + segLen) % period;
        }
    }

    private void DrawRegionLabels()
    {
        foreach (SectorMapLabel label in _mapData.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Text))
                continue;

            var (lx, ly) = ProjectToScreen(label.X, label.Y);
            Color baseCol = ParseHexColor(label.Color, Palette.CrtAmberPrimary);
            byte alpha = (byte)Math.Clamp((int)MathF.Round(label.Opacity * 180f), 60, 210);
            Color col = new(baseCol.R, baseCol.G, baseCol.B, alpha);

            string text = label.Text.Trim().ToUpperInvariant();
            int fontSize = 10;
            int charSpacing = Math.Clamp(label.Spacing / 2, 2, 5);
            int totalW = MeasureSpacedText(text, fontSize, charSpacing);
            DrawSpacedText(text, lx - totalW / 2, ly - 5, fontSize, charSpacing, col);
        }
    }

    private void DrawTradeRoutes()
    {
        // Pass 1: Outer non-clickable routes (visual background context)
        foreach (SectorMapRoute route in _mapData.Routes)
        {
            if (route.IsClickable)
                continue;

            if (!_mapData.NodesById.TryGetValue(route.From, out SectorMapNode? fromNode) ||
                !_mapData.NodesById.TryGetValue(route.To, out SectorMapNode? toNode))
            {
                continue;
            }

            var (ax, ay) = ProjectToScreen(fromNode.X, fromNode.Y);
            var (bx, by) = ProjectToScreen(toNode.X, toNode.Y);
            Color c = ParseHexColor(route.Color, Palette.CrtAmberDimmed);
            Raylib.DrawLine(ax, ay, bx, by, new Color(c.R, c.G, c.B, (byte)95));
        }

        // Pass 2: Inner clickable trade routes (r47..r52, r64, r111, r112)
        foreach (SectorMapRoute route in _mapData.ClickableRoutes)
        {
            if (!_mapData.NodesById.TryGetValue(route.From, out SectorMapNode? fromNode) ||
                !_mapData.NodesById.TryGetValue(route.To, out SectorMapNode? toNode))
            {
                continue;
            }

            var (ax, ay) = ProjectToScreen(fromNode.X, fromNode.Y);
            var (bx, by) = ProjectToScreen(toNode.X, toNode.Y);

            bool isSelected = string.Equals(_selectedRouteId, route.Id, StringComparison.OrdinalIgnoreCase);
            bool isHovered = string.Equals(_hoveredRouteId, route.Id, StringComparison.OrdinalIgnoreCase);

            Color baseCol = ParseHexColor(route.Color, Palette.CrtAmberPrimary);
            Vector2 pA = new(ax, ay);
            Vector2 pB = new(bx, by);

            if (isSelected)
            {
                Raylib.DrawLineEx(pA, pB, 4.0f, new Color(45, 212, 191, 95));
                Raylib.DrawLineEx(pA, pB, 2.5f, Palette.CrtAmberPrimary);
                int mx = (ax + bx) / 2;
                int my = (ay + by) / 2;
                Raylib.DrawRectangle(mx - 4, my - 4, 8, 8, Palette.OutlineVoid);
                Raylib.DrawRectangleLines(mx - 4, my - 4, 8, 8, Palette.CrtAmberPrimary);
            }
            else if (isHovered)
            {
                Raylib.DrawLineEx(pA, pB, 3.0f, Palette.ClearanceTeal);
                int mx = (ax + bx) / 2;
                int my = (ay + by) / 2;
                Raylib.DrawRectangle(mx - 3, my - 3, 6, 6, Palette.ClearanceTeal);
            }
            else
            {
                Raylib.DrawLineEx(pA, pB, 2.0f, new Color(baseCol.R, baseCol.G, baseCol.B, (byte)205));
            }
        }
    }

    private void DrawNodes()
    {
        float pulse = 0.5f + 0.5f * MathF.Sin(_elapsedTime * 4.2f);

        // Pass 1: Outer non-clickable nodes (K1..K8, G1..G10, I1..I3, KD1..KD7, B1..B7, M1..M4, WORMHOLE)
        foreach (SectorMapNode node in _mapData.Nodes)
        {
            if (node.IsClickable)
                continue;

            var (nx, ny) = ProjectToScreen(node.X, node.Y);
            Color baseCol = ParseHexColor(node.Color, Palette.CrtAmberDimmed);

            if (node.Name.Equals("WORMHOLE", StringComparison.OrdinalIgnoreCase))
            {
                // Visual-context Wormhole icon in upper Ashen Rift
                Color whCol = new(baseCol.R, baseCol.G, baseCol.B, (byte)190);
                Raylib.DrawCircleLines(nx, ny, 7, whCol);
                Raylib.DrawCircleLines(nx, ny, 4, Palette.AlertCrimson);
                Raylib.DrawPixel(nx, ny, whCol);
                int ww = Raylib.MeasureText("WORMHOLE", 10);
                Raylib.DrawText("WORMHOLE", nx - ww / 2, ny + 10, 10, whCol);
                continue;
            }

            Color dotCol = new(baseCol.R, baseCol.G, baseCol.B, (byte)135);
            Raylib.DrawCircle(nx, ny, 3.0f, dotCol);
            Raylib.DrawPixel(nx, ny, new Color(255, 236, 190, 180));

            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                int tw = Raylib.MeasureText(node.Name, 10);
                Raylib.DrawText(node.Name, nx - tw / 2, ny + 6, 10, new Color(baseCol.R, baseCol.G, baseCol.B, (byte)125));
            }
        }

        // Pass 2: Inner 7 clickable systems (VG-3, Gharok, Veridia, Oruun-4, Ash, Korr-Vath, Dul-Krag)
        foreach (SectorMapNode node in _mapData.ClickableNodes)
        {
            var (nx, ny) = ProjectToScreen(node.X, node.Y);
            bool isSelected = string.Equals(_selectedNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
            bool isHovered = string.Equals(_hoveredNodeId, node.Id, StringComparison.OrdinalIgnoreCase);

            Color nodeCol = ParseHexColor(node.Color, Palette.ClearanceTeal);
            bool isStation = node.Type.Equals("station", StringComparison.OrdinalIgnoreCase);
            bool isHazard = node.Type.Equals("hazard", StringComparison.OrdinalIgnoreCase);

            // Outer interactive halo ring
            byte ringAlpha = (byte)(isSelected ? 235 : (isHovered ? 210 : (int)(110 + pulse * 55)));
            Color ringCol = isSelected
                ? Palette.CrtAmberPrimary
                : (isHovered ? Palette.ClearanceTeal : new Color(nodeCol.R, nodeCol.G, nodeCol.B, ringAlpha));
            Raylib.DrawCircleLines(nx, ny, isStation ? 10 : 8, ringCol);

            if (isStation)
            {
                // VG-3 Station diamond & crosshairs
                Raylib.DrawRectangle(nx - 4, ny - 4, 8, 8, nodeCol);
                Raylib.DrawRectangleLines(nx - 6, ny - 6, 12, 12, ringCol);
                Raylib.DrawLine(nx - 12, ny, nx - 7, ny, ringCol);
                Raylib.DrawLine(nx + 7, ny, nx + 12, ny, ringCol);
                Raylib.DrawLine(nx, ny - 12, nx, ny - 7, ringCol);
                Raylib.DrawLine(nx, ny + 7, nx, ny + 12, ringCol);
            }
            else if (isHazard)
            {
                // Ash hazard node
                Raylib.DrawCircle(nx, ny, 4.5f, nodeCol);
                Raylib.DrawCircleLines(nx, ny, 6.0f, Palette.AlertCrimson);
            }
            else
            {
                // Named neighboring star system
                Raylib.DrawCircle(nx, ny, 4.5f, nodeCol);
                Raylib.DrawPixel(nx - 1, ny - 1, new Color(230, 255, 240, 255));
            }

            if (isSelected || isHovered)
            {
                Color bracketCol = isSelected ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;
                DrawSelectionBrackets(nx, ny, radius: 13, bracketCol);
            }

            // Crisp system name plate below node
            string label = node.Name;
            int tw = Raylib.MeasureText(label, 10);
            int lx = nx - tw / 2;
            int ly = ny + 12;
            Raylib.DrawRectangle(lx - 3, ly - 1, tw + 6, 12, new Color(7, 11, 16, 220));
            Color textCol = isSelected
                ? Palette.CrtAmberPrimary
                : (isHovered ? Palette.ClearanceTeal : nodeCol);
            Raylib.DrawText(label, lx, ly, 10, textCol);
        }
    }

    private static void DrawSelectionBrackets(int cx, int cy, int radius, Color col)
    {
        int x0 = cx - radius;
        int y0 = cy - radius;
        int x1 = cx + radius;
        int y1 = cy + radius;
        const int arm = 4;

        // Top-Left
        Raylib.DrawLine(x0, y0, x0 + arm, y0, col);
        Raylib.DrawLine(x0, y0, x0, y0 + arm, col);
        // Top-Right
        Raylib.DrawLine(x1 - arm, y0, x1, y0, col);
        Raylib.DrawLine(x1, y0, x1, y0 + arm, col);
        // Bottom-Left
        Raylib.DrawLine(x0, y1 - arm, x0, y1, col);
        Raylib.DrawLine(x0, y1, x0 + arm, y1, col);
        // Bottom-Right
        Raylib.DrawLine(x1 - arm, y1, x1, y1, col);
        Raylib.DrawLine(x1, y1 - arm, x1, y1, col);
    }

    private void DrawBottomInfoReadoutBar()
    {
        Raylib.DrawRectangle(ReadoutX, ReadoutY, ReadoutW, ReadoutH, new Color(8, 13, 19, 255));
        Raylib.DrawRectangleLines(ReadoutX, ReadoutY, ReadoutW, ReadoutH, Palette.ClearanceTeal);
        Raylib.DrawRectangleLines(ReadoutX + 2, ReadoutY + 2, ReadoutW - 4, ReadoutH - 4, Palette.OutlineVoid);

        Raylib.DrawText("ASTROGATION TELEMETRY READOUT", ReadoutX + 14, ReadoutY + 9, 10, Palette.WeatheredGunmetalEdge);

        if (!string.IsNullOrEmpty(_selectedNodeId))
        {
            Raylib.DrawText(ReadoutPrimaryText, ReadoutX + 14, ReadoutY + 27, 10, Palette.ClearanceTeal);
            Raylib.DrawText(ReadoutSecondaryText, ReadoutX + 284, ReadoutY + 27, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText(ReadoutTertiaryText, ReadoutX + 684, ReadoutY + 27, 10, Palette.CrtAmberDimmed);
        }
        else if (!string.IsNullOrEmpty(_selectedRouteId))
        {
            Raylib.DrawText(ReadoutPrimaryText, ReadoutX + 14, ReadoutY + 27, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText(ReadoutSecondaryText, ReadoutX + 448, ReadoutY + 27, 10, Palette.ClearanceTeal);
        }
        else
        {
            Raylib.DrawText("SELECT AN INNER SECTOR SYSTEM (VG-3 + 6 NEIGHBORS) OR TRADE CORRIDOR LINE.", ReadoutX + 14, ReadoutY + 27, 10, Palette.CrtAmberDimmed);
        }
    }

    private static int MeasureSpacedText(string text, int fontSize, int extraSpacing)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int total = 0;
        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            total += Raylib.MeasureText(ch, fontSize);
            if (i + 1 < text.Length)
                total += extraSpacing;
        }
        return total;
    }

    private static void DrawSpacedText(string text, int x, int y, int fontSize, int extraSpacing, Color col)
    {
        int cx = x;
        for (int i = 0; i < text.Length; i++)
        {
            string ch = text[i].ToString();
            Raylib.DrawText(ch, cx, y, fontSize, col);
            cx += Raylib.MeasureText(ch, fontSize) + extraSpacing;
        }
    }

    private static Color ParseHexColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        try
        {
            return Palette.FromHex(hex.Trim());
        }
        catch
        {
            return fallback;
        }
    }
}
