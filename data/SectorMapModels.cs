using System.Text.Json.Serialization;

namespace SubspaceProtocols.Data;

public sealed class SectorMapCanvasSpec
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 960;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 540;

    [JsonPropertyName("aspect")]
    public string Aspect { get; set; } = "16:9";
}

public sealed class SectorMapPoint
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public sealed class SectorMapBorder
{
    public const string RemoveInProdMarkerName = "REMOVE IN PROD";
    public const string RemoveInProdMarkerId = "b134";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "faction";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#e8e0cc";

    [JsonPropertyName("fillAlpha")]
    public float FillAlpha { get; set; } = 0.10f;

    [JsonPropertyName("hatch")]
    public bool Hatch { get; set; }

    [JsonPropertyName("closed")]
    public bool Closed { get; set; } = true;

    [JsonPropertyName("smooth")]
    public bool Smooth { get; set; } = true;

    [JsonPropertyName("dashed")]
    public bool Dashed { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 2;

    [JsonPropertyName("points")]
    public List<SectorMapPoint> Points { get; set; } = [];

    [JsonPropertyName("renderPoints")]
    public List<SectorMapPoint> RenderPoints { get; set; } = [];

    [JsonIgnore]
    public bool IsRemoveInProdMarker =>
        Name.Trim().Equals(RemoveInProdMarkerName, StringComparison.OrdinalIgnoreCase) ||
        Id.Trim().Equals(RemoveInProdMarkerId, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public IReadOnlyList<SectorMapPoint> EffectiveGeometryPoints =>
        RenderPoints.Count >= 3 ? RenderPoints : Points;
}

public sealed class SectorMapNode
{
    public const string TodoFallback = "[TODO]";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "star";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#22c55e";

    [JsonPropertyName("labelPos")]
    public string LabelPos { get; set; } = "below";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsClickable { get; set; }

    [JsonIgnore]
    public string EffectiveFaction =>
        string.IsNullOrWhiteSpace(Faction) ? TodoFallback : Faction.Trim();

    [JsonIgnore]
    public string EffectiveTag =>
        string.IsNullOrWhiteSpace(Tag) ? TodoFallback : Tag.Trim();
}

public sealed class SectorMapRoute
{
    public const string TodoFallback = "[TODO]";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("export")]
    public string Export { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#d4a052";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsClickable { get; set; }

    [JsonIgnore]
    public string EffectiveExport =>
        string.IsNullOrWhiteSpace(Export) ? TodoFallback : Export.Trim();
}

public sealed class SectorMapLabel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; } = 18;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#ffb84d";

    [JsonPropertyName("spacing")]
    public int Spacing { get; set; } = 6;

    [JsonPropertyName("rotation")]
    public float Rotation { get; set; }

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 0.9f;
}

/// <summary>
/// Data-driven 0..1 normalized Sector Star Chart loaded from data/sector_map.json.
/// Strictly excludes any border named "REMOVE IN PROD" (b134) from RenderableBorders,
/// while using b134's enclosed polygon (or fallback core IDs) solely to mark clickable
/// star systems (VG-3 + 6 neighboring systems) and their connecting trade routes.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class SectorMapData
{
    private static readonly HashSet<string> FallbackClickableNodeIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "n11", // VG-3
        "n15", // Gharok
        "n16", // Veridia
        "n17", // Oruun-4
        "n19", // Ash
        "n20", // Korr-Vath
        "n21"  // Dul-Krag
    };

    private static readonly HashSet<string> FallbackClickableRouteIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "r47", "r48", "r49", "r50", "r51", "r52", "r64", "r111", "r112"
    };

    [JsonPropertyName("format")]
    public string Format { get; set; } = "star_chart_editor";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("canvas")]
    public SectorMapCanvasSpec Canvas { get; set; } = new();

    [JsonPropertyName("coordinateSystem")]
    public string CoordinateSystem { get; set; } = string.Empty;

    [JsonPropertyName("borders")]
    public List<SectorMapBorder> Borders { get; set; } = [];

    [JsonPropertyName("nodes")]
    public List<SectorMapNode> Nodes { get; set; } = [];

    [JsonPropertyName("routes")]
    public List<SectorMapRoute> Routes { get; set; } = [];

    [JsonPropertyName("labels")]
    public List<SectorMapLabel> Labels { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<SectorMapBorder> RenderableBorders { get; private set; } = [];

    [JsonIgnore]
    public IReadOnlyList<SectorMapNode> ClickableNodes { get; private set; } = [];

    [JsonIgnore]
    public IReadOnlyList<SectorMapRoute> ClickableRoutes { get; private set; } = [];

    [JsonIgnore]
    public IReadOnlyDictionary<string, SectorMapNode> NodesById { get; private set; } =
        new Dictionary<string, SectorMapNode>(StringComparer.OrdinalIgnoreCase);

    public static SectorMapData Load()
    {
        SectorMapData data = DataLoader.LoadJson<SectorMapData>("sector_map.json") ?? new SectorMapData();
        data.InitializeDerivedState();
        return data;
    }

    public void InitializeDerivedState()
    {
        // 1. Exclude REMOVE IN PROD (b134) from RenderableBorders so it is NEVER rendered in-game
        SectorMapBorder? markerBorder = Borders.FirstOrDefault(b => b.IsRemoveInProdMarker);
        RenderableBorders = Borders.Where(b => !b.IsRemoveInProdMarker).ToList();

        // 2. Build node lookup & mark clickable nodes using b134's polygon (or fallback core IDs)
        Dictionary<string, SectorMapNode> byId = new(StringComparer.OrdinalIgnoreCase);
        List<SectorMapNode> clickableNodes = [];
        IReadOnlyList<SectorMapPoint>? markerPoly = markerBorder?.EffectiveGeometryPoints;
        bool hasValidMarkerPoly = markerPoly != null && markerPoly.Count >= 3;

        foreach (SectorMapNode node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Faction))
                node.Faction = SectorMapNode.TodoFallback;
            if (string.IsNullOrWhiteSpace(node.Tag))
                node.Tag = SectorMapNode.TodoFallback;

            bool insideMarker = hasValidMarkerPoly
                ? IsPointInPolygon(node.X, node.Y, markerPoly!)
                : FallbackClickableNodeIds.Contains(node.Id);

            node.IsClickable = insideMarker;
            byId[node.Id] = node;
            if (node.IsClickable)
            {
                clickableNodes.Add(node);
            }
        }

        // 3. Mark clickable routes: any route connecting two clickable nodes inside b134 (or in FallbackClickableRouteIds)
        List<SectorMapRoute> clickableRoutes = [];
        foreach (SectorMapRoute route in Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Export))
                route.Export = SectorMapRoute.TodoFallback;

            bool endpointsClickable =
                byId.TryGetValue(route.From, out SectorMapNode? fromNode) && fromNode.IsClickable &&
                byId.TryGetValue(route.To, out SectorMapNode? toNode) && toNode.IsClickable;

            route.IsClickable = hasValidMarkerPoly
                ? endpointsClickable
                : (endpointsClickable || FallbackClickableRouteIds.Contains(route.Id));

            if (route.IsClickable)
            {
                clickableRoutes.Add(route);
            }
        }

        NodesById = byId;
        ClickableNodes = clickableNodes;
        ClickableRoutes = clickableRoutes;
    }

    public static bool IsPointInPolygon(float px, float py, IReadOnlyList<SectorMapPoint> polygon)
    {
        if (polygon.Count < 3)
            return false;

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            float xi = polygon[i].X;
            float yi = polygon[i].Y;
            float xj = polygon[j].X;
            float yj = polygon[j].Y;

            bool intersects = ((yi > py) != (yj > py)) &&
                              (px < (xj - xi) * (py - yi) / ((yj - yi) + 1e-7f) + xi);
            if (intersects)
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
