using Raylib_cs;
using SubspaceProtocols.Core;

namespace SubspaceProtocols.UI;

public readonly record struct DeskRect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;

    public Rectangle ToRaylib() => new(X, Y, W, H);

    public bool Intersects(DeskRect other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public bool Contains(DeskRect inner) =>
        inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;
}

/// <summary>
/// Single shared Booth 04 layout coordinate struct computed from the active DeskUpperLayoutMode
/// and B1/B2 portrait scale toggle each frame. Eliminates hardcoded upper/lower-right magic numbers.
/// Left region (x = 8..672) and Top HUD bar (y = 0..32) stay 100% identical across all three modes.
/// </summary>
public readonly record struct DeskLayoutRects
{
    public const int ScrollbarW = 16;
    public const int DataSlateW = 184;
    public const int DataSlateH = 216;
    public const int ClearanceHeaderH = 24;

    public DeskUpperLayoutMode Mode { get; init; }
    public bool Use3xPortrait { get; init; }
    public bool IsTransponderSeparatePanel { get; init; }
    public bool IsConduitStraightDrop { get; init; }

    // Fixed Top HUD Bar & Left Region (identical across A / B1 / B2)
    public DeskRect TopBar { get; init; }
    public DeskRect DetainLever { get; init; }
    public DeskRect Viewport { get; init; }
    public DeskRect SpsToggle { get; init; }
    public DeskRect SpsMini { get; init; }
    public DeskRect StokerDial { get; init; }
    public DeskRect WarpPrinter { get; init; }
    public DeskRect WarpRefSpot { get; init; }
    public DeskRect LapPocket { get; init; }

    // Dynamic Upper-Center / Upper-Right / Lower-Right / Slate Bus / Wafer Writer Regions
    public DeskRect Viewscreen { get; init; }
    public DeskRect Portrait { get; init; }
    public DeskRect FeedBox { get; init; }
    public DeskRect Transcript { get; init; }
    public int TranscriptVisibleRows { get; init; }
    public int TranscriptTextMaxPx { get; init; }
    public DeskRect CommsStrip { get; init; }
    public DeskRect NextHailBtn { get; init; }
    public DeskRect LowerShieldsBtn { get; init; }
    public DeskRect PwrDownWpnBtn { get; init; }
    public DeskRect ProtocolReviewBtn { get; init; }
    public DeskRect SlateBus { get; init; }
    public DeskRect SlateBusDockedSlate { get; init; }
    public DeskRect WaferWriter { get; init; }
    public DeskRect WaferFeedSlot { get; init; }

    // One-Way Armored Data Conduit Waypoints (Slate Bus -> Wafer Writer)
    public int ConduitStartX { get; init; }
    public int ConduitStartY { get; init; }
    public int ConduitGutterX { get; init; }
    public int ConduitBendY { get; init; }
    public int ConduitEndX { get; init; }
    public int ConduitEndY { get; init; }

    // Lower-Right Hardware (Clearance Upload Dock & Destroy Override)
    public DeskRect ClearanceDock { get; init; }
    public DeskRect ClearanceSlot { get; init; }
    public DeskRect ClearanceBar { get; init; }
    public DeskRect DestroyOverride { get; init; }

    // Free Desk Document Drag Bounds
    public DeskRect FreeDragBounds { get; init; }

    public int FreeDeskRestMinY => Mode == DeskUpperLayoutMode.OptionA_Classic ? 356 : FreeDragBounds.Y;
    public int SlateBusDockedX => SlateBusDockedSlate.X;
    public int SlateBusDockedY => SlateBusDockedSlate.Y;
    public int DeskSeamRightX => FreeDragBounds.Right;
    public int DeskSeamVerticalX => ClearanceDock.X - 4;

    public static DeskLayoutRects Compute(DeskUpperLayoutMode mode, bool use3xPortraitInLayoutB = true)
    {
        DeskRect topBar = new(0, 0, Constants.VirtualWidth, 32);
        DeskRect detainLever = new(8, 40, 96, 216);
        DeskRect viewport = new(112, 40, 448, 216);
        DeskRect spsToggle = new(568, 40, 104, 60);
        DeskRect spsMini = new(568, 108, 104, 148);
        DeskRect stokerDial = new(8, 264, 96, 88);
        DeskRect warpPrinter = new(112, 264, 320, 88);
        DeskRect warpRefSpot = new(440, 264, 232, 88);
        DeskRect lapPocket = new(8, 656, 280, 56);

        if (mode == DeskUpperLayoutMode.OptionA_Classic)
        {
            return new DeskLayoutRects
            {
                Mode = mode,
                Use3xPortrait = false,
                IsTransponderSeparatePanel = false,
                IsConduitStraightDrop = false,
                TopBar = topBar,
                DetainLever = detainLever,
                Viewport = viewport,
                SpsToggle = spsToggle,
                SpsMini = spsMini,
                StokerDial = stokerDial,
                WarpPrinter = warpPrinter,
                WarpRefSpot = warpRefSpot,
                LapPocket = lapPocket,
                Viewscreen = new DeskRect(680, 40, 220, 216),
                Portrait = new DeskRect(726, 48, 128, 128),
                FeedBox = new DeskRect(688, 184, 204, 64),
                Transcript = new DeskRect(908, 40, 364, 216),
                TranscriptVisibleRows = 14,
                TranscriptTextMaxPx = 364 - ScrollbarW - 14,
                CommsStrip = new DeskRect(908, 264, 364, 44),
                NextHailBtn = new DeskRect(908, 264, 116, 44),
                LowerShieldsBtn = new DeskRect(1032, 264, 116, 44),
                PwrDownWpnBtn = new DeskRect(1156, 264, 116, 44),
                ProtocolReviewBtn = new DeskRect(908, 316, 364, 36),
                SlateBus = new DeskRect(680, 264, 220, 44),
                SlateBusDockedSlate = new DeskRect(698, 308, DataSlateW, DataSlateH),
                WaferWriter = new DeskRect(680, 624, 220, 88),
                WaferFeedSlot = new DeskRect(692, 624, 196, 16),
                ConduitStartX = 900,
                ConduitStartY = 286,
                ConduitGutterX = 904,
                ConduitBendY = 604,
                ConduitEndX = 894,
                ConduitEndY = 624,
                ClearanceDock = new DeskRect(1072, 360, 200, 272),
                ClearanceSlot = new DeskRect(1080, 384, DataSlateW, DataSlateH),
                ClearanceBar = new DeskRect(1080, 604, DataSlateW, 20),
                DestroyOverride = new DeskRect(1072, 640, 200, 72),
                FreeDragBounds = new DeskRect(8, 308, 1056, 404)
            };
        }

        // OptionB1_CommsSwap & OptionB2_CommsSwapFlush share upper columns, Slate Bus, Wafer Writer, and straight conduit
        DeskRect viewscreenB = new(1052, 40, 220, 216);
        DeskRect portraitB = use3xPortraitInLayoutB
            ? new DeskRect(1066, 52, 192, 192)
            : new DeskRect(1098, 72, 128, 128);
        DeskRect feedBoxB = new(1052, 264, 220, 88);
        DeskRect transcriptB = new(680, 40, 364, 172);
        DeskRect commsStripB = new(680, 220, 364, 36);
        DeskRect nextHailB = new(680, 220, 116, 36);
        DeskRect lowerShieldsB = new(804, 220, 116, 36);
        DeskRect pwrDownWpnB = new(928, 220, 116, 36);
        DeskRect protocolReviewB = new(680, 264, 364, 36);
        DeskRect slateBusB = new(752, 308, 220, 44);
        DeskRect dockedSlateB = new(770, 352, DataSlateW, DataSlateH);
        DeskRect waferWriterB = new(752, 624, 220, 88);
        DeskRect waferFeedSlotB = new(764, 624, 196, 16);

        // Straight vertical drop along right shoulder x = 966 (clear of docked Data Slate x=770..954 & Wipe Feed Slot x=764..960)
        const int conduitStraightX = 966;

        bool isFlushB2 = mode == DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
        DeskRect clearanceDockB = isFlushB2
            ? new DeskRect(1052, 360, 220, 272)
            : new DeskRect(1072, 360, 200, 272);
        DeskRect clearanceSlotB = isFlushB2
            ? new DeskRect(1070, 384, DataSlateW, DataSlateH)
            : new DeskRect(1080, 384, DataSlateW, DataSlateH);
        DeskRect clearanceBarB = isFlushB2
            ? new DeskRect(1070, 604, DataSlateW, 20)
            : new DeskRect(1080, 604, DataSlateW, 20);
        DeskRect destroyOverrideB = isFlushB2
            ? new DeskRect(1052, 640, 220, 72)
            : new DeskRect(1072, 640, 200, 72);
        DeskRect freeDragBoundsB = isFlushB2
            ? new DeskRect(8, 352, 1036, 360)
            : new DeskRect(8, 352, 1056, 360);

        return new DeskLayoutRects
        {
            Mode = mode,
            Use3xPortrait = use3xPortraitInLayoutB,
            IsTransponderSeparatePanel = true,
            IsConduitStraightDrop = true,
            TopBar = topBar,
            DetainLever = detainLever,
            Viewport = viewport,
            SpsToggle = spsToggle,
            SpsMini = spsMini,
            StokerDial = stokerDial,
            WarpPrinter = warpPrinter,
            WarpRefSpot = warpRefSpot,
            LapPocket = lapPocket,
            Viewscreen = viewscreenB,
            Portrait = portraitB,
            FeedBox = feedBoxB,
            Transcript = transcriptB,
            TranscriptVisibleRows = 11,
            TranscriptTextMaxPx = 364 - ScrollbarW - 14,
            CommsStrip = commsStripB,
            NextHailBtn = nextHailB,
            LowerShieldsBtn = lowerShieldsB,
            PwrDownWpnBtn = pwrDownWpnB,
            ProtocolReviewBtn = protocolReviewB,
            SlateBus = slateBusB,
            SlateBusDockedSlate = dockedSlateB,
            WaferWriter = waferWriterB,
            WaferFeedSlot = waferFeedSlotB,
            ConduitStartX = conduitStraightX,
            ConduitStartY = slateBusB.Bottom,
            ConduitGutterX = conduitStraightX,
            ConduitBendY = 604,
            ConduitEndX = conduitStraightX,
            ConduitEndY = waferWriterB.Y,
            ClearanceDock = clearanceDockB,
            ClearanceSlot = clearanceSlotB,
            ClearanceBar = clearanceBarB,
            DestroyOverride = destroyOverrideB,
            FreeDragBounds = freeDragBoundsB
        };
    }

    /// <summary>
    /// Validates zero overlapping hardware hitboxes, exact containment of child bays (Portrait, ClearanceSlot,
    /// WaferFeedSlot, docked Data Slate), and exact 1280x720 virtual canvas bounds.
    /// </summary>
    public void ValidateGeometry()
    {
        DeskRect canvas = new(0, 0, Constants.VirtualWidth, Constants.VirtualHeight);

        List<(string Name, DeskRect Rect)> topLevelModules =
        [
            ("TopBar", TopBar),
            ("DetainLever", DetainLever),
            ("Viewport", Viewport),
            ("SpsToggle", SpsToggle),
            ("SpsMini", SpsMini),
            ("StokerDial", StokerDial),
            ("WarpPrinter", WarpPrinter),
            ("WarpRefSpot", WarpRefSpot),
            ("LapPocket", LapPocket),
            ("Viewscreen", Viewscreen),
            ("Transcript", Transcript),
            ("NextHailBtn", NextHailBtn),
            ("LowerShieldsBtn", LowerShieldsBtn),
            ("PwrDownWpnBtn", PwrDownWpnBtn),
            ("ProtocolReviewBtn", ProtocolReviewBtn),
            ("SlateBus", SlateBus),
            ("WaferWriter", WaferWriter),
            ("ClearanceDock", ClearanceDock),
            ("DestroyOverride", DestroyOverride)
        ];

        if (IsTransponderSeparatePanel)
        {
            topLevelModules.Add(("FeedBox", FeedBox));
        }

        foreach (var (name, rect) in topLevelModules)
        {
            if (!canvas.Contains(rect))
            {
                throw new InvalidOperationException($"Layout {Mode}: module '{name}' {rect} exceeds 1280x720 virtual canvas.");
            }
        }

        for (int i = 0; i < topLevelModules.Count; i++)
        {
            for (int j = i + 1; j < topLevelModules.Count; j++)
            {
                if (topLevelModules[i].Rect.Intersects(topLevelModules[j].Rect))
                {
                    throw new InvalidOperationException(
                        $"Layout {Mode}: hitbox overlap between '{topLevelModules[i].Name}' {topLevelModules[i].Rect} and '{topLevelModules[j].Name}' {topLevelModules[j].Rect}.");
                }
            }
        }

        if (!Viewscreen.Contains(Portrait))
            throw new InvalidOperationException($"Layout {Mode}: Portrait {Portrait} is not inside Viewscreen {Viewscreen}.");

        if (!IsTransponderSeparatePanel)
        {
            if (!Viewscreen.Contains(FeedBox))
                throw new InvalidOperationException($"Layout {Mode}: Classic FeedBox {FeedBox} is not inside Viewscreen {Viewscreen}.");
            if (Portrait.Intersects(FeedBox))
                throw new InvalidOperationException($"Layout {Mode}: Classic Portrait {Portrait} overlaps FeedBox {FeedBox}.");
        }

        if (!ClearanceDock.Contains(ClearanceSlot) || !ClearanceDock.Contains(ClearanceBar) || ClearanceSlot.Intersects(ClearanceBar))
            throw new InvalidOperationException($"Layout {Mode}: ClearanceDock child slots invalid.");

        if (!WaferWriter.Contains(WaferFeedSlot))
            throw new InvalidOperationException($"Layout {Mode}: WaferFeedSlot {WaferFeedSlot} is not inside WaferWriter {WaferWriter}.");

        if (!FreeDragBounds.Contains(SlateBusDockedSlate))
            throw new InvalidOperationException($"Layout {Mode}: Docked Data Slate {SlateBusDockedSlate} is outside FreeDragBounds {FreeDragBounds}.");

        if (SlateBusDockedSlate.Intersects(WaferWriter) || SlateBusDockedSlate.Intersects(ClearanceDock) || SlateBusDockedSlate.Intersects(LapPocket))
            throw new InvalidOperationException($"Layout {Mode}: Docked Data Slate {SlateBusDockedSlate} collides with lower desk hardware.");
    }
}
