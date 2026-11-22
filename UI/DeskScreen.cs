using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

/// <summary>
/// 1280x720 Booth 04 Desk Screen (Prompt 3.6 Overhaul):
/// - Exact 8px-grid bounding boxes with zero overlaps and clean dark brushed-metal filler plates (ZERO placeholder text)
/// - Left-side physical Detain Lever with animated viewport docking clamps (~0.5s lock beat)
/// - Armored external glass viewport (real station window) with generous breathing room & 270s Brown Dwarf arc
/// - Day 1 simplified single-rule registration check (Raw Transponder ID vs. Data Slate REG CODE)
/// - Day 2 SPS Control Column + animated physical viewport sensor probe arm + LOWER SHIELDS / PWR DOWN WPN
/// - Day 3 Protocol Review + Security Strikes + Live Hardcoded Kordani Security Officer CRT Hail + L->R Warp Printer & Glowing Ref Spot
/// - 0.5s PROTOCOL FAULT beat (pulsing conflict highlights + CRT oscilloscope fault flash) + staggered officer/captain dialogue with viewscreen static flicker
/// - Live F1 animation speed multiplier scaling glide-in, download, clearance upload/transmit, and departure
/// </summary>
public sealed class DeskScreen : IScreen
{
    private enum EncounterPhase
    {
        BayEmpty = 0,
        ShipGlidingIn = 1,
        OpeningGreetingDelay = 2,
        AwaitingPortForDownload = 3,
        DownloadingProtocols = 4,
        DownloadAborted = 5,
        InspectingSlateAndShip = 6,
        UploadingClearance = 7,
        AwaitingClearanceReturnToTopPort = 8,
        TransmittingClearance = 9,
        DetainClamping = 10,
        ResolvingDecision = 11
    }

    private enum DataSlateLocation
    {
        SlateBus = 0,
        DeskFree = 1,
        ClearanceDock = 2
    }

    private enum DataSlateTelemetryState
    {
        Empty = 0,
        Downloading = 1,
        DownloadAborted = 2,
        Loaded = 3,
        UploadingClearance = 4,
        ClearedReadyToTransmit = 5,
        TransmittingClearance = 6
    }

    private enum PreShiftStage
    {
        None = 0,
        Day3KordaniLiveHail = 1
    }

    private enum FaultSequenceStage
    {
        None = 0,
        FaultBeat = 1,             // 0.50s pulsing conflict highlight + CRT oscilloscope PROTOCOL FAULT flash
        OfficerAccusationPause = 2 // 0.60s pause after officer line + portrait viewscreen static flicker before captain excuse
    }

    private enum WarpPrinterPhase
    {
        Idle = 0,
        ScanningWarpField = 1,
        PrintingStrip = 2,
        AnchoredReadyToTear = 3,
        TornOffFreeDocument = 4
    }

    private enum SpsAnomalyKind
    {
        None = 0,
        HotWeaponMount = 1,
        VioletCargoPower = 2
    }

    private sealed class SpsAnomalyEntry
    {
        public SpsAnomalyKind Kind { get; init; }
        public string Label { get; init; } = string.Empty;
        public Color AccentColor { get; init; }
        public bool IsCooled { get; set; }
        public bool WasVerified { get; set; }
    }

    private enum DeskItemId
    {
        DataSlate = 0,
        ReferenceManual = 1,
        WarpStrip = 2
    }

    private enum ReviewTarget
    {
        None = 0,
        DataSlatePrefix = 1,
        DataSlateClass = 2,
        DataSlateWeapons = 3,
        DataSlateOrigin = 4,
        DataSlateOrg = 5,
        DataSlateExpiry = 6,
        DataSlateSpecies = 7,
        DataSlateCargo = 8,
        VisualSignalTransponderId = 9,
        TopHeaderCycle = 10,
        ViewportShipHull = 11,
        SpsLensConduit = 12,
        WarpStripPaper = 13,
        SpsMiniScreenHotWeapons = 14,
        SpsMiniScreenCargoPower = 15
    }

    private sealed class DraggableDeskItem
    {
        public DeskItemId Id { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; init; }
        public int Height { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    // =========================================================================
    // EXACT 1280x720 BOOTH 04 MODULAR GRID (Strict 8px Outer Margin & 8px Gutters)
    // =========================================================================

    // 1. Top Status Bar: (0, 0, 1280, 32)
    private const int TopBarX = 0;
    private const int TopBarY = 0;
    private const int TopBarW = 1280;
    private const int TopBarH = 32;
    private const int TopCycleX = 236;
    private const int TopCycleY = 4;
    private const int TopCycleW = 124;
    private const int TopCycleH = 24;
    private const int PauseBtnX = 1236;
    private const int PauseBtnY = 4;
    private const int PauseBtnW = 36;
    private const int PauseBtnH = 24;

    // 2. Row 1 (y = 40..256, h = 216)
    // Detain Lever: (8, 40, 96, 216) -- flush with Stellar Stoker Mass Dial below
    private const int DetainLeverX = 8;
    private const int DetainLeverY = 40;
    private const int DetainLeverW = 96;
    private const int DetainLeverH = 216;

    // Armored External Space Viewport (Real Glass Window): (112, 40, 448, 216)
    private const int ViewportX = 112;
    private const int ViewportY = 40;
    private const int ViewportW = 448;
    private const int ViewportH = 216;
    private const int ViewportGlassInset = 6;
    private const int ViewportInnerX = ViewportX + ViewportGlassInset;
    private const int ViewportInnerY = ViewportY + ViewportGlassInset;
    private const int ViewportInnerW = ViewportW - ViewportGlassInset * 2;
    private const int ViewportInnerH = ViewportH - ViewportGlassInset * 2;

    // 120x56 ship sprite scaled 2x -> 240x112 centered inside 448x216 viewport
    private const int ShipRenderW = Constants.ShipWidth * 2;  // 240
    private const int ShipRenderH = Constants.ShipHeight * 2; // 112
    private const int ShipAnchorX = ViewportX + (ViewportW - ShipRenderW) / 2; // 216
    private const int ShipAnchorY = ViewportY + 72;                            // 112

    // Shift-End button inside viewport
    private const int ShiftEndBtnW = 288;
    private const int ShiftEndBtnH = 28;
    private const int ShiftEndBtnX = ViewportX + (ViewportW - ShiftEndBtnW) / 2;
    private const int ShiftEndBtnY = ViewportY + 148;

    // SPS Control Column: (568, 40, 104, 216) -- Toggle (568, 40, 104, 60) + 8px gap + Mini-Screen (568, 108, 104, 148)
    private const int SpsColX = 568;
    private const int SpsColFullY = 40;
    private const int SpsColW = 104;
    private const int SpsColFullH = 216;
    private const int SpsToggleX = 568;
    private const int SpsToggleY = 40;
    private const int SpsToggleW = 104;
    private const int SpsToggleH = 60;
    private const int SpsMiniX = 568;
    private const int SpsMiniY = 108;
    private const int SpsMiniW = 104;
    private const int SpsMiniH = 148;

    // Hailing Viewscreen & Signal ID: (680, 40, 220, 216)
    // 128x128 portrait centered at (726, 48), LIVE FEED + TRANSPONDER ID box at (688, 184, 204, 64)
    private const int ViewscreenX = 680;
    private const int ViewscreenY = 40;
    private const int ViewscreenW = 220;
    private const int ViewscreenH = 216;
    private const int PortraitBoxX = 726;
    private const int PortraitBoxY = 48;
    private const int PortraitSize = 128; // 64x64 scaled 2x
    private const int FeedBoxX = 688;
    private const int FeedBoxY = 184;
    private const int FeedBoxW = 204;
    private const int FeedBoxH = 64;

    // CRT Comms Dialogue Log: (908, 40, 364, 216) with amber pixel Up/Down scroll controls (w = 16)
    private const int TranscriptX = 908;
    private const int TranscriptY = 40;
    private const int TranscriptW = 364;
    private const int TranscriptH = 216;
    private const int ScrollbarW = 16;
    private const int TranscriptVisibleRows = 14;
    private const int TranscriptTextMaxPx = TranscriptW - ScrollbarW - 14; // 334px clean clearance

    // 3. Row 2 (y = 264..352, h = 88)
    // Stellar Stoker Mass Dial: (8, 264, 96, 88) -- aligns flush with Detain Lever (x = 8..104)
    private const int StokerDialX = 8;
    private const int StokerDialY = 264;
    private const int StokerDialW = 96;
    private const int StokerDialH = 88;

    // Warp Residue Printer + L->R Tray: (112, 264, 320, 88)
    // Left printer housing (112, 264, 88, 88) + Right extrusion tray (204, 276, 224, 64)
    private const int WarpPrinterX = 112;
    private const int WarpPrinterY = 264;
    private const int WarpPrinterW = 320;
    private const int WarpPrinterH = 88;
    private const int WarpHousingW = 88;
    private const int WarpTrayX = 204;
    private const int WarpTrayY = 276;
    private const int WarpTrayW = 224;
    private const int WarpTrayH = 64;

    // Glowing Warp Reference Spot: (440, 264, 232, 88) -- aligns flush with SPS right (x = 672)
    // < REF: SYSTEM > selector bar (448, 270, 216, 22) + backlit waveform glass (448, 294, 216, 52)
    private const int WarpRefSpotX = 440;
    private const int WarpRefSpotY = 264;
    private const int WarpRefSpotW = 232;
    private const int WarpRefSpotH = 88;
    private const int WarpSelectorX = 448;
    private const int WarpSelectorY = 270;
    private const int WarpSelectorW = 216;
    private const int WarpSelectorH = 22;
    private const int WarpGlassX = 448;
    private const int WarpGlassY = 294;
    private const int WarpGlassW = WarpSignatureGenerator.WaveformCanvasWidth;  // 216
    private const int WarpGlassH = WarpSignatureGenerator.WaveformCanvasHeight; // 52

    // Slate Bus (Top Two-Way Slot): (680, 264, 220, 44) -- aligns flush with Viewscreen (x = 680..900)
    private const int SlateBusX = 680;
    private const int SlateBusY = 264;
    private const int SlateBusW = 220;
    private const int SlateBusH = 44;

    // Comms Command Strip: (908, 264, 364, 44) -- three equal 116px buttons with 8px gaps
    private const int CommsStripX = 908;
    private const int CommsStripY = 264;
    private const int CommsStripW = 364;
    private const int CommsStripH = 44;
    private const int NextHailBtnX = 908;
    private const int NextHailBtnY = 264;
    private const int NextHailBtnW = 116;
    private const int NextHailBtnH = 44;
    private const int LowerShieldsBtnX = 1032;
    private const int LowerShieldsBtnY = 264;
    private const int LowerShieldsBtnW = 116;
    private const int LowerShieldsBtnH = 44;
    private const int PwrDownWpnBtnX = 1156;
    private const int PwrDownWpnBtnY = 264;
    private const int PwrDownWpnBtnW = 116;
    private const int PwrDownWpnBtnH = 44;

    // Row 2B (y = 316..352, h = 36, 8px below Command Strip)
    private const int ProtocolReviewBtnX = 908;
    private const int ProtocolReviewBtnY = 316;
    private const int ProtocolReviewBtnW = 156;
    private const int ProtocolReviewBtnH = 36;

    private const int BioScannerLedX = 1072;
    private const int BioScannerLedY = 316;
    private const int BioScannerLedW = 200;
    private const int BioScannerLedH = 36;

    // 4. Lower Desk (y = 360..712)
    private const int DragBoundsX = 8;
    private const int DragBoundsY = 308;
    private const int DragBoundsW = 1056; // x = 8..1064 (strict 8px gutter left of Clearance Dock at 1072)
    private const int DragBoundsH = 404;  // y = 308..712 (strict 8px bottom margin)

    // Primary Data Slate: exact size 184 x 216, spawns docked beneath Slate Bus at (698, 308, 184, 216)
    private const int DataSlateW = 184;
    private const int DataSlateH = 216;
    private const int SlateBusDockedX = 698;
    private const int SlateBusDockedY = 308;

    // Desk Reference Manual (Day 3+ only -- 1. PREFIXES & 2. HULLS only; tightened height 192px with zero dead space)
    private const int ManualDefaultX = 8;
    private const int ManualDefaultY = 360;
    private const int ManualW = 280;
    private const int ManualH = 192;

    // Printed Magnetic/Thermal Warp Strip (Day 3+ L->R extrusion):
    // Exact 1:1 216x52 waveform canvas + 2px side frames + 1px top border + 11px slim bottom grip edge = 220x64
    private const int StripWaveOffsetX = 2;
    private const int StripWaveOffsetY = 1;
    private const int StripWidth = WarpGlassW + StripWaveOffsetX * 2; // 220 (fits cleanly in 224px tray)
    private const int StripFullHeight = WarpGlassH + StripWaveOffsetY + 11; // 64 (exact match to 64px tray)
    private const int StripAnchorX = WarpTrayX + 2; // 206
    private const int StripAnchorY = WarpTrayY;     // 276
    private const int StripSnapX = WarpGlassX - StripWaveOffsetX; // 446 (aligns wave 1:1 with glass x = 448)
    private const int StripSnapY = WarpGlassY - StripWaveOffsetY; // 293 (aligns wave 1:1 with glass y = 294, leaving selector y=270..292 clear!)
    private const float TearOffThresholdPx = 16.0f;

    // Contraband Lap Pocket: (8, 656, 280, 56) -- subtle dark unlit beveled lip flush with x = 8 and y = 712
    private const int LapPocketX = 8;
    private const int LapPocketY = 656;
    private const int LapPocketW = 280;
    private const int LapPocketH = 56;

    // Wafer Writer (Bottom-Center): (680, 624, 220, 88) -- aligns flush with Slate Bus & Viewscreen (x = 680..900)
    // Visible Wafer Wipe Feed Slot along TOP edge: (692, 624, 196, 16)
    private const int WaferWriterX = 680;
    private const int WaferWriterY = 624;
    private const int WaferWriterW = 220;
    private const int WaferWriterH = 88;
    private const int WaferFeedSlotX = 692;
    private const int WaferFeedSlotY = 624;
    private const int WaferFeedSlotW = 196;
    private const int WaferFeedSlotH = 16;

    // Strictly One-Way Data Conduit Cable (Slate Bus right edge -> top of Wafer Writer):
    // Exits right edge of Slate Bus (900, 286) -> drops down 8px gutter x = 904 -> turns into Wafer Writer top shoulder (894, 624)
    private const int ConduitStartX = 900;
    private const int ConduitStartY = 286;
    private const int ConduitGutterX = 904;
    private const int ConduitBendY = 604;
    private const int ConduitEndX = 894;
    private const int ConduitEndY = 624;

    // Clearance Upload Dock: (1072, 360, 200, 272)
    // Header/pins (1072, 360, 200, 24) + Visible Docked Data Slate bay (1080, 384, 184, 216) + Progress Bar (1080, 604, 184, 20)
    private const int ClearanceDockX = 1072;
    private const int ClearanceDockY = 360;
    private const int ClearanceDockW = 200;
    private const int ClearanceDockH = 272;
    private const int ClearanceHeaderH = 24;
    private const int ClearanceSlotX = 1080;
    private const int ClearanceSlotY = 384;
    private const int ClearanceSlotW = DataSlateW; // 184
    private const int ClearanceSlotH = DataSlateH; // 216
    private const int ClearanceBarX = 1080;
    private const int ClearanceBarY = 604;
    private const int ClearanceBarW = 184;
    private const int ClearanceBarH = 20;

    // Destroy Ship Override: (1072, 640, 200, 72) -- aligns flush with Clearance Dock (x = 1072..1272) & bottom margin y = 712
    private const int DestroyOverrideX = 1072;
    private const int DestroyOverrideY = 640;
    private const int DestroyOverrideW = 200;
    private const int DestroyOverrideH = 72;

    // SPS Lens Dimensions & Hold-to-Lock
    private const int SpsLensW = 84;
    private const int SpsLensH = 58;
    private const float SpsLockDurationSeconds = 0.50f;
    private const float SpsLockRadiusPx = 26.0f;

    // Pacing Durations (download, clearance encryption, clearance TX, and detain clamp+wipe share exact StandardTransferDurationSeconds!)
    private const float ShipApproachDurationSeconds = 3.2f;
    private const float OpeningGreetingBeatSeconds = 0.35f;
    private const float ProtocolDownloadDurationSeconds = ShiftController.StandardTransferDurationSeconds;
    private const float ReTransmitAnnoyedDelaySeconds = 0.55f;
    private const float ClearanceUploadDurationSeconds = ShiftController.StandardTransferDurationSeconds;
    private const float ClearanceTransmitDurationSeconds = ShiftController.StandardTransferDurationSeconds;
    private const float DetainSequenceDurationSeconds = ShiftController.StandardTransferDurationSeconds;
    private const float ShipDepartureDurationSeconds = 1.25f;
    private const float FaultBeatDurationSeconds = 0.50f;
    private const float OfficerToCaptainPauseSeconds = 0.60f;
    private const float WarpScanDurationSeconds = 2.6f;
    private const float WarpPrintDurationSeconds = 3.6f;
    private const float UnplugMidDownloadDragThresholdPx = 10.0f;

    // Clearance Upload Dock & Slate Bus tactile micro-animation durations (Prompt 3.7.2, scaled by AnimSpeedMultiplier)
    private const float DockSettleDurationSeconds = 0.12f;
    private const float DockLatchClampInDurationSeconds = 0.12f;
    private const float DockLatchSnapOpenDurationSeconds = 0.06f;
    private const float DockReleaseKickDurationSeconds = 0.14f;
    private const float SlateBusSettleDurationSeconds = 0.12f;

    private static readonly (string Prefix, string OrgName, ShipFaction VisualFaction)[] ManualPrefixDirectory =
    [
        ("GUV-", "GALACTIC UNION", ShipFaction.GalacticUnion),
        ("KDF-", "KORDANI DEFENSE FLEET", ShipFaction.Kordani),
        ("BMC-", "BRENGOP CONSORTIUM", ShipFaction.Brengop),
        ("KRW-", "KRUSHVAK WAR-FLEET", ShipFaction.Krushvak),
        ("ORN-", "ORUN REGISTRY NETWORK", ShipFaction.Orun),
        ("TLV-", "TOLVAK TRANSIT LINE", ShipFaction.Tolvak),
        ("PRJ-", "PRUNJUN SALVAGE REG.", ShipFaction.Prunjun),
        ("FTV-", "INDEPENDENT FREE TRADERS", ShipFaction.FreeTraders),
        ("HBR-", "HUN-BORIAN FREE VESSEL", ShipFaction.FreeTraders),
        ("FTC-", "FAR-LINE TRANSIT CORP", ShipFaction.FarLineTransit),
        ("MNG-", "MINING GUILD SYNDICATE", ShipFaction.MiningGuild),
        ("ZNI-", "ZENITH INTERSTELLAR", ShipFaction.ZenithInterstellar)
    ];

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly DevDebugState _devDebug;
    private readonly Action? _onRequestPause;
    private readonly Action<int>? _onTriggerDayStartTutorial;

    private readonly ShiftController _shiftController = new();
    private readonly ShipGenerator _shipGenerator = new();
    private readonly ValidationEngine _validationEngine = new();
    private readonly WorldSim _worldSim = new();

    // Pre-Shift Live Kordani Hail state (Day 3)
    private PreShiftStage _preShiftStage = PreShiftStage.None;
    private string[] _kordaniHailLines = [];
    private int _kordaniHailLineIndex;
    private float _kordaniHailLineTimer;

    private EncounterPhase _phase = EncounterPhase.BayEmpty;
    private ShipSpec? _activeShip;
    private uint _debugReseedNonce;

    // Primary Dark-Steel Data Slate Physical Port & Telemetry State
    private DataSlateLocation _slateLocation = DataSlateLocation.SlateBus;
    private DataSlateTelemetryState _slateTelemetry = DataSlateTelemetryState.Empty;
    private float _slateWipeFlashTimer;
    private float _pendingReTransmitRestartTimer;
    private float _clearanceTransmitElapsed;

    // Protocol Review State (Unlocked Day 3+)
    private bool _protocolReviewModeActive;
    private ReviewTarget _reviewFirstSelection = ReviewTarget.None;
    private string _reviewStatusNote = string.Empty;
    private string _verifiedViolationSummary = string.Empty;

    // Tactile Inspection Weight: 0.5s PROTOCOL FAULT beat + Staggered Officer/Captain Exchange
    private FaultSequenceStage _faultStage = FaultSequenceStage.None;
    private float _faultStageTimer;
    private ReviewTarget _faultHighlightA = ReviewTarget.None;
    private ReviewTarget _faultHighlightB = ReviewTarget.None;
    private string _pendingFaultSummary = string.Empty;
    private string _pendingOfficerAccusation = string.Empty;
    private string _pendingCaptainReaction = string.Empty;
    private float _portraitStaticFlickerTimer;

    // Tactile Pacing Timers
    private float _shipApproachElapsed;
    private float _shipSlideOffset;
    private float _subspaceAutoTransferDelay;
    private float _protocolDownloadElapsed;
    private float _dataStreamAudioAccum;
    private float _clearanceUploadElapsed;

    // Clearance Upload Dock latches & tactile micro-settle animation state (Prompt 3.7.2)
    private float _dockSettleElapsed;
    private bool _dockSettleActive;
    private float _dockLatchProgress; // 0 = fully open, 1 = clamped inward over slate sides
    private float _dockLatchTarget;   // 0 = open, 1 = locked
    private float _dockReleaseKickElapsed;
    private bool _dockReleaseKickActive;
    private float _slateBusSettleElapsed;
    private bool _slateBusSettleActive;

    // Physical Detain Lever & Docking Clamp Animation State
    private bool _detainLeverPulled;
    private float _detainLeverVisualPos; // 0 = top armed, 1 = pulled down
    private float _detainClampElapsed;
    private bool _detainClampImpactPlayed;
    private bool _detainSlateWipeCompleted;
    private float _viewportImpactShakeTimer;

    // Day 2+ SPS Probe Arm & Hold-to-Lock Anomaly Scanner
    private bool _spsEnabled;
    private float _spsProbeExtend; // 0 = retracted, 1 = extended into right side of glass viewport
    private Vector2 _currentSpsSwayOffset;
    private Rectangle _lastSpsLensRect;
    private ShipHardpointLayout? _activeShipHardpoints;
    private readonly List<SpsAnomalyEntry> _spsLoggedAnomalies = [];
    private SpsAnomalyKind _spsLockTarget = SpsAnomalyKind.None;
    private float _spsLockProgress;

    // Day 3+ Manual Warp Field Scan, L->R Extrusion & Glowing Warp Reference Spot
    private WarpPrinterPhase _warpPhase = WarpPrinterPhase.Idle;
    private float _warpPhaseElapsed;
    private float _warpPrinterTickAccum;
    private string _warpStripActualOrigin = string.Empty;
    private string _warpStripShipReg = string.Empty;
    private bool _isPullingAnchoredStrip;
    private int _pullStartMouseX;
    private int _pullStartMouseY;
    private int _anchoredStripPullOffsetX;
    private int _anchoredStripPullOffsetY;
    private int _warpRefSpotSystemIndex;

    // Animation & shift clocks
    private float _elapsedVisualTime;
    private int _animTick;
    private float _tickAccum;
    private int _lastShipAnimBucket = -1;
    private int _lastPortraitAnimBucket = -1;

    // Compliance order timers (Day 2+)
    private bool _pendingShieldsOrder;
    private bool _pendingWeaponsOrder;
    private float _complianceTimer;
    private bool _captainRefusedCompliance;

    // Decision evaluation & Error-Only Instant Citation strip state
    private PlayerDecision _pendingDecision;
    private float _decisionEvalTimer;
    private DecisionEvaluationResult? _activeCitationStrip;
    private float _citationSlideProgress;
    private float _citationDisplayTimer;

    // Thematic Scrollable Dialogue Transcript Log
    private readonly List<(string Text, Color Color)> _transcriptLines = [];
    private int _transcriptScrollTopLine;
    private bool _transcriptAutoFollowBottom = true;

    // Draggable Desk Items & Z-Order list (last element = topmost)
    private readonly Dictionary<DeskItemId, DraggableDeskItem> _deskItems = [];
    private readonly List<DeskItemId> _zOrder = [];
    private DeskItemId? _draggingItemId;
    private int _dragOffsetX;
    private int _dragOffsetY;
    private int _dragStartMouseX;
    private int _dragStartMouseY;

    // Desk Reference Manual state (Day 3+)
    private int _manualActiveTab = 1;
    private int _manualFactionIndex = 0;
    private int _manualClassIndex = 0;

    // Cached Procedural GPU Textures (explicitly unloaded when ship leaves or screen exits)
    private Texture2D _cachedShipLiveTex;
    private Texture2D _cachedShipSpsTex;
    private Texture2D _cachedLivePortraitTex;
    private Texture2D _cachedHoloPortraitTex;
    private Texture2D _cachedManualShipTex;
    private Texture2D _cachedPreShiftKordaniPortraitTex;

    // Progressive Unlock Gates
    public bool IsSpsUnlocked => _gameState.Day >= 2;
    public bool AreCombatOrdersUnlocked => _gameState.Day >= 2;
    public bool IsProtocolReviewUnlocked => _gameState.Day >= 3;
    public bool AreSecurityStrikesUnlocked => _gameState.Day >= 3;
    public bool IsSpectrometerOperational => _gameState.Day >= 3;
    public bool IsManualUnlocked => _gameState.Day >= 3;
    public bool IsWaferWriterOperational => _gameState.Day >= 7;

    private bool IsDataSlateInSlateBus => _slateLocation == DataSlateLocation.SlateBus;
    private bool IsDataSlateInClearanceDock => _slateLocation == DataSlateLocation.ClearanceDock;
    private bool HasActiveClearanceSeal =>
        _slateTelemetry is DataSlateTelemetryState.ClearedReadyToTransmit
            or DataSlateTelemetryState.TransmittingClearance;
    private bool IsShipInspectableForReview =>
        _phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.AwaitingClearanceReturnToTopPort;
    private bool IsSlateReadableForReview =>
        _slateTelemetry is DataSlateTelemetryState.Loaded or DataSlateTelemetryState.ClearedReadyToTransmit;

    private float ProtocolDownloadProgress =>
        _slateTelemetry == DataSlateTelemetryState.Downloading
            ? Math.Clamp(_protocolDownloadElapsed / ProtocolDownloadDurationSeconds, 0f, 1f)
            : (_slateTelemetry is DataSlateTelemetryState.Loaded
                or DataSlateTelemetryState.UploadingClearance
                or DataSlateTelemetryState.ClearedReadyToTransmit
                or DataSlateTelemetryState.TransmittingClearance
                ? 1.0f
                : 0.0f);

    public DeskScreen(
        ScreenManager screenManager,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug,
        Action? onRequestPause = null,
        Action<int>? onTriggerDayStartTutorial = null)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;
        _onRequestPause = onRequestPause;
        _onTriggerDayStartTutorial = onTriggerDayStartTutorial;

        InitializeDraggableDeskItems();
    }

    private void InitializeDraggableDeskItems()
    {
        _deskItems.Clear();
        _zOrder.Clear();

        // 1. Primary Data Slate: (700, 304, 192, 240) docked in Slate Bus at shift start
        _deskItems[DeskItemId.DataSlate] = new DraggableDeskItem
        {
            Id = DeskItemId.DataSlate,
            X = SlateBusDockedX,
            Y = SlateBusDockedY,
            Width = DataSlateW,
            Height = DataSlateH,
            IsVisible = true
        };

        // 2. Desk Reference Manual: strictly hidden on Days 1-2; unlocked Day 3+
        _deskItems[DeskItemId.ReferenceManual] = new DraggableDeskItem
        {
            Id = DeskItemId.ReferenceManual,
            X = ManualDefaultX,
            Y = ManualDefaultY,
            Width = ManualW,
            Height = ManualH,
            IsVisible = IsManualUnlocked
        };

        // 3. Torn-Off Thermal Warp Field Residue Strip (Day 3+, visible once torn free)
        _deskItems[DeskItemId.WarpStrip] = new DraggableDeskItem
        {
            Id = DeskItemId.WarpStrip,
            X = StripAnchorX,
            Y = StripAnchorY,
            Width = StripWidth,
            Height = StripFullHeight,
            IsVisible = false
        };

        _zOrder.Add(DeskItemId.ReferenceManual);
        _zOrder.Add(DeskItemId.DataSlate);
        _zOrder.Add(DeskItemId.WarpStrip);
    }

    public void OnEnter()
    {
        UnloadCachedTextures();

        _spsEnabled = false;
        _spsProbeExtend = 0f;
        _phase = EncounterPhase.BayEmpty;
        _activeShip = null;
        _debugReseedNonce = 0;
        _lastShipAnimBucket = -1;
        _lastPortraitAnimBucket = -1;
        _slateLocation = DataSlateLocation.SlateBus;
        _slateTelemetry = DataSlateTelemetryState.Empty;
        _slateWipeFlashTimer = 0f;
        _pendingReTransmitRestartTimer = 0f;
        _clearanceTransmitElapsed = 0f;
        _draggingItemId = null;
        _detainLeverPulled = false;
        _detainLeverVisualPos = 0f;
        _detainClampElapsed = 0f;
        _detainClampImpactPlayed = false;
        _detainSlateWipeCompleted = false;
        _viewportImpactShakeTimer = 0f;
        _activeCitationStrip = null;
        _warpRefSpotSystemIndex = 0;

        ResetProtocolReviewState();
        ResetFaultSequenceState();
        ResetWarpPrinterState();
        ResetSpsAnomalyState();
        ResetSlateDockMicroAnimations();

        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;

        _shiftController.StartNewShift(_gameState);
        _shiftController.PrepareDailyQueue(_gameState, _shipGenerator);

        _gameState.QuestFlags["Day3_SpectrometerRepaired"] = _gameState.Day >= 3;
        if (_gameState.Day >= 3 && _gameState.Day2SuspectDetained && !_gameState.Day3KordaniOvernightRepApplied)
        {
            _gameState.RepScore["KDF-"] = _gameState.GetRep("KDF-") + 2;
            _gameState.Day3KordaniOvernightRepApplied = true;
            _gameState.SaveToDisk();
        }

        InitializeDraggableDeskItems();

        _transcriptLines.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        if (_gameState.Day == 3 && !_gameState.Day3PreShiftHailCompleted)
        {
            StartDay3PreShiftKordaniLiveHail();
        }
        else
        {
            _preShiftStage = PreShiftStage.None;
            _shiftController.IsPreShiftSequenceActive = false;
            AddTranscriptLine($"BOOTH 04 TERMINAL READY // DAY {_gameState.Day:D2} WATCH.", Palette.CrtAmberPrimary);
            AddTranscriptLine("PRIMARY DATA SLATE LINKED IN SLATE BUS. PRESS [NEXT HAIL] WHEN READY.", Palette.CrtAmberDimmed);
            _onTriggerDayStartTutorial?.Invoke(_gameState.Day);
        }

        if (IsManualUnlocked)
        {
            if (_manualActiveTab > 1)
                _manualActiveTab = 1;
            RebuildManualSilhouetteTexture();
        }
    }

    public void OnExit()
    {
        UnloadCachedTextures();
    }

    private void ResetProtocolReviewState()
    {
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _reviewStatusNote = string.Empty;
        _verifiedViolationSummary = string.Empty;
    }

    private void ResetFaultSequenceState()
    {
        _faultStage = FaultSequenceStage.None;
        _faultStageTimer = 0f;
        _faultHighlightA = ReviewTarget.None;
        _faultHighlightB = ReviewTarget.None;
        _pendingFaultSummary = string.Empty;
        _pendingOfficerAccusation = string.Empty;
        _pendingCaptainReaction = string.Empty;
        _portraitStaticFlickerTimer = 0f;
    }

    private void ResetSpsAnomalyState()
    {
        _activeShipHardpoints = null;
        _spsLoggedAnomalies.Clear();
        _spsLockTarget = SpsAnomalyKind.None;
        _spsLockProgress = 0f;
    }

    private void ResetSlateDockMicroAnimations()
    {
        _dockSettleElapsed = 0f;
        _dockSettleActive = false;
        _dockLatchProgress = 0f;
        _dockLatchTarget = 0f;
        _dockReleaseKickElapsed = 0f;
        _dockReleaseKickActive = false;
        _slateBusSettleElapsed = 0f;
        _slateBusSettleActive = false;
    }

    private void UpdateSlateDockMicroAnimations(float animDt)
    {
        if (_dockSettleActive)
        {
            _dockSettleElapsed += animDt;
            if (_dockSettleElapsed >= DockSettleDurationSeconds)
            {
                _dockSettleElapsed = 0f;
                _dockSettleActive = false;
            }
        }

        if (_dockReleaseKickActive)
        {
            _dockReleaseKickElapsed += animDt;
            if (_dockReleaseKickElapsed >= DockReleaseKickDurationSeconds)
            {
                _dockReleaseKickElapsed = 0f;
                _dockReleaseKickActive = false;
            }
        }

        if (_slateBusSettleActive)
        {
            _slateBusSettleElapsed += animDt;
            if (_slateBusSettleElapsed >= SlateBusSettleDurationSeconds)
            {
                _slateBusSettleElapsed = 0f;
                _slateBusSettleActive = false;
            }
        }

        float latchDuration = _dockLatchTarget > _dockLatchProgress
            ? DockLatchClampInDurationSeconds
            : DockLatchSnapOpenDurationSeconds;
        float maxLatchStep = latchDuration > 0.0001f ? (animDt / latchDuration) : 1.0f;
        _dockLatchProgress = MoveToward(_dockLatchProgress, _dockLatchTarget, maxLatchStep);
    }

    private int GetDataSlateVisualOffsetY()
    {
        if (IsDataSlateInClearanceDock)
        {
            if (_dockSettleActive)
            {
                float t = Math.Clamp(_dockSettleElapsed / DockSettleDurationSeconds, 0f, 1f);
                float u = 0.18f + 0.82f * t;
                return (int)MathF.Round(MathF.Sin(u * MathF.PI) * 2.0f);
            }

            if (_dockReleaseKickActive)
            {
                float t = Math.Clamp(_dockReleaseKickElapsed / DockReleaseKickDurationSeconds, 0f, 1f);
                float u = 0.20f + 0.80f * t;
                return -(int)MathF.Round(MathF.Sin(u * MathF.PI) * 2.0f);
            }
        }
        else if (IsDataSlateInSlateBus && _slateBusSettleActive)
        {
            float t = Math.Clamp(_slateBusSettleElapsed / SlateBusSettleDurationSeconds, 0f, 1f);
            float u = 0.18f + 0.82f * t;
            return (int)MathF.Round(MathF.Sin(u * MathF.PI) * 2.0f);
        }

        return 0;
    }

    private void ResetWarpPrinterState()
    {
        _warpPhase = WarpPrinterPhase.Idle;
        _warpPhaseElapsed = 0f;
        _warpPrinterTickAccum = 0f;
        _warpStripActualOrigin = string.Empty;
        _warpStripShipReg = string.Empty;
        _isPullingAnchoredStrip = false;
        _anchoredStripPullOffsetX = 0;
        _anchoredStripPullOffsetY = 0;

        if (_deskItems.TryGetValue(DeskItemId.WarpStrip, out DraggableDeskItem? stripItem))
        {
            stripItem.IsVisible = false;
            stripItem.X = StripAnchorX;
            stripItem.Y = StripAnchorY;
        }
    }

    // =========================================================================
    // Main Update Loop
    // =========================================================================
    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _elapsedVisualTime += deltaTime;
        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;

        float animSpeed = _devDebug.AnimationSpeedMultiplier;
        float animDt = deltaTime * animSpeed;

        // Sync Dev Debug Time Scale & One-Shot Commands
        _shiftController.TimeScale = _devDebug.ShiftTimeScale;
        if (_devDebug.PendingEndShiftNow)
        {
            _devDebug.PendingEndShiftNow = false;
            if (_preShiftStage == PreShiftStage.None)
            {
                _shiftController.ForceEndShiftNow();
                AddTranscriptLine("[DEV] SHIFT TIMER ADVANCED TO HORIZON SUNSET (270s).", Palette.CrtAmberDimmed);
            }
        }
        if (_devDebug.PendingForceReseedShip)
        {
            _devDebug.PendingForceReseedShip = false;
            if (_preShiftStage == PreShiftStage.None)
            {
                ForceReseedAndSpawnNextShip();
            }
        }

        _shiftController.Tick(deltaTime);

        _tickAccum += deltaTime * 60f;
        if (_tickAccum >= 1f)
        {
            int steps = (int)_tickAccum;
            _animTick += steps;
            _tickAccum -= steps;
        }

        // Smooth physical SPS probe arm extension (Day 2+)
        float targetProbe = (IsSpsUnlocked && _spsEnabled) ? 1.0f : 0.0f;
        _spsProbeExtend = MoveToward(_spsProbeExtend, targetProbe, deltaTime * 4.5f);

        // Smooth physical Detain lever position
        float targetLever = _detainLeverPulled ? 1.0f : 0.0f;
        _detainLeverVisualPos = MoveToward(_detainLeverVisualPos, targetLever, deltaTime * 6.5f);

        if (_viewportImpactShakeTimer > 0f)
        {
            _viewportImpactShakeTimer = Math.Max(0f, _viewportImpactShakeTimer - deltaTime);
        }

        if (_portraitStaticFlickerTimer > 0f)
        {
            _portraitStaticFlickerTimer = Math.Max(0f, _portraitStaticFlickerTimer - deltaTime);
        }

        if (_slateWipeFlashTimer > 0f)
        {
            _slateWipeFlashTimer = Math.Max(0f, _slateWipeFlashTimer - deltaTime);
        }

        // Update Day 3 Pre-Shift Live Kordani Hail line-by-line delivery in CRT Log
        UpdatePreShiftKordaniHail(deltaTime);

        // Update Tactile 0.5s PROTOCOL FAULT beat & staggered dialogue exchange
        UpdateFaultSequence(deltaTime);

        // SPS Sway, Lens & Hold-to-Lock (Day 2+)
        _currentSpsSwayOffset = ComputeSpsSwayOffset(_elapsedVisualTime);
        UpdateSpsLensRect(mouse);
        if (IsSpsUnlocked)
        {
            UpdateSpsHoldToLock(deltaTime, mouse);
        }

        // Day 3+ Warp Residue Printer
        if (IsSpectrometerOperational)
        {
            UpdateWarpPrinterState(animDt);
        }

        UpdateAnimatedEncounterTextures();

        // Update Error-Only Instant Citation Strip slide-out & display timer
        if (_activeCitationStrip != null)
        {
            _citationSlideProgress = Math.Min(1.0f, _citationSlideProgress + deltaTime * 5.0f);
            _citationDisplayTimer -= deltaTime;
            if (_citationDisplayTimer <= 0f)
            {
                _activeCitationStrip = null;
            }
        }

        UpdateEncounterPhase(deltaTime, animDt);
        UpdateSlateDockMicroAnimations(animDt);

        // Mousewheel scroll on the CRT Comms Dialogue Log
        HandleTranscriptScrollInput(mouse);

        // Pause Button (1236, 4, 36, 24) works even during pre-shift hail
        if (mouse.LeftPressed && mouse.IsHit(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH))
        {
            _onRequestPause?.Invoke();
            return;
        }

        // Transcript scrollbar buttons always responsive
        if (mouse.LeftPressed && TryHandleTranscriptScrollButtonClicks(mouse))
        {
            return;
        }

        if (_preShiftStage != PreShiftStage.None)
        {
            HandlePreShiftStageInput(mouse);
            return;
        }

        // Handle Click-and-Drag Tear-Off pull on anchored thermal paper strip (Day 3+)
        if (_isPullingAnchoredStrip)
        {
            UpdateAnchoredStripTearPull(mouse);
            return;
        }

        // Active document dragging
        if (_draggingItemId.HasValue)
        {
            UpdateActiveDrag(mouse);
            return;
        }

        if (!mouse.LeftPressed)
            return;

        // Dismiss active transient error citation slip if clicked
        if (_activeCitationStrip != null)
        {
            Rectangle citRect = GetActiveCitationPopupRect();
            if (mouse.IsHit(citRect))
            {
                AudioHooks.Play("terminal_key_click");
                _activeCitationStrip = null;
                return;
            }
        }

        // Shift Complete proceed button inside viewport
        if (_shiftController.CanEndShift && mouse.IsHit(ShiftEndBtnX, ShiftEndBtnY, ShiftEndBtnW, ShiftEndBtnH))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
            return;
        }

        // Day 3+ Warp Residue Printer & Glowing Warp Reference Spot
        if (IsSpectrometerOperational && TryHandleWarpHardwareClicks(mouse))
        {
            return;
        }

        // Hardware Controls: Detain Lever, Comms Command Strip, Protocol Review, SPS Toggle, Slate Bus Re-Transmit, Clearance Dock pickup
        if (TryHandleHardwarePanelClicks(mouse))
        {
            return;
        }

        // Day 3+ Protocol Review Mode Pairing (includes SPS Anomaly Mini-Screen evidence selection)
        if (IsProtocolReviewUnlocked && _protocolReviewModeActive && _activeShip != null &&
            IsShipInspectableForReview)
        {
            if (TryHandleProtocolReviewClick(mouse))
            {
                return;
            }
        }

        // Day 3+ Click on anchored thermal strip in L->R tray to begin tear-off
        if (IsSpectrometerOperational && _warpPhase == WarpPrinterPhase.AnchoredReadyToTear &&
            mouse.IsHit(StripAnchorX, StripAnchorY, StripWidth, StripFullHeight))
        {
            _isPullingAnchoredStrip = true;
            _pullStartMouseX = mouse.X;
            _pullStartMouseY = mouse.Y;
            _anchoredStripPullOffsetX = 0;
            _anchoredStripPullOffsetY = 0;
            AudioHooks.Play("terminal_key_click");
            return;
        }

        // Movable Lower Desk Documents (Reverse Z-Order: topmost first)
        TryHandleDeskItemsMousePress(mouse);
    }

    private void ForceReseedAndSpawnNextShip()
    {
        _debugReseedNonce++;
        _shiftController.NotifyFirstHailTriggered();
        DebugSpawnValidityMode mode = (DebugSpawnValidityMode)_devDebug.ForceSpawnValidityMode;
        StartNextShipEncounter(mode, _debugReseedNonce);
    }

    // =========================================================================
    // Day 3 Pre-Shift Live Kordani Security Officer Viewscreen Hail (CRT Log)
    // =========================================================================
    private void StartDay3PreShiftKordaniLiveHail()
    {
        _preShiftStage = PreShiftStage.Day3KordaniLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer officerBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
        {
            SeedId = Day2And3ScriptedDialogue.Day3KordaniOfficerPortraitSeed,
            Species = PortraitSpecies.Kordani,
            Mode = PortraitRenderMode.Live,
            FactionPrefix = "KDF-",
            IsKordaniSecurityOfficer = true
        });
        _cachedPreShiftKordaniPortraitTex = officerBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.BuildDay3KordaniHailLines(
            _gameState.Day2SuspectDetained,
            _gameState.Day2SuspectRegistration,
            _gameState.Day2SuspectDeclaredOrigin);
        _kordaniHailLineIndex = 0;
        _kordaniHailLineTimer = 0.25f;

        AudioHooks.Play("hail_incoming_chime");

        _transcriptLines.Clear();
        AddTranscriptLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.Day3KordaniOfficerName}", Palette.AlertCrimson);
    }

    private void UpdatePreShiftKordaniHail(float deltaTime)
    {
        if (_preShiftStage != PreShiftStage.Day3KordaniLiveHail)
            return;

        if (_kordaniHailLineIndex < _kordaniHailLines.Length)
        {
            _kordaniHailLineTimer -= deltaTime;
            if (_kordaniHailLineTimer <= 0f)
            {
                AddTranscriptLine($"CMDR: \"{_kordaniHailLines[_kordaniHailLineIndex]}\"", Palette.CrtAmberPrimary);
                AudioHooks.Play("comms_reply_blip");
                _kordaniHailLineIndex++;
                _kordaniHailLineTimer = 1.10f;
            }
        }
    }

    private void HandlePreShiftStageInput(VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        if (_preShiftStage == PreShiftStage.Day3KordaniLiveHail &&
            mouse.IsHit(CommsStripX, CommsStripY, CommsStripW, CommsStripH))
        {
            // If lines are still printing, reveal all immediately; otherwise acknowledge and start Day 03 watch
            if (_kordaniHailLineIndex < _kordaniHailLines.Length)
            {
                while (_kordaniHailLineIndex < _kordaniHailLines.Length)
                {
                    AddTranscriptLine($"CMDR: \"{_kordaniHailLines[_kordaniHailLineIndex]}\"", Palette.CrtAmberPrimary);
                    _kordaniHailLineIndex++;
                }
                AudioHooks.Play("terminal_key_click");
                return;
            }

            CloseDay3PreShiftKordaniLiveHail();
        }
    }

    private void CloseDay3PreShiftKordaniLiveHail()
    {
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);

        _gameState.Day3PreShiftHailCompleted = true;
        _gameState.SaveToDisk();

        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;

        AudioHooks.Play("terminal_key_confirm");
        AddTranscriptLine("[SYS] INTERNAL HAIL CLOSED -- PROTOCOL REVIEW & SECURITY STRIKES ONLINE.", Palette.ClearanceTeal);
        AddTranscriptLine("PRESS [NEXT HAIL] TO BEGIN DAY 03 WATCH.", Palette.CrtAmberDimmed);
        _onTriggerDayStartTutorial?.Invoke(_gameState.Day);
    }

    // =========================================================================
    // Tactile Inspection Weight: 0.5s PROTOCOL FAULT Beat + Staggered Exchange
    // =========================================================================
    private void BeginProtocolFaultSequence(
        ShipInfractionType violationType,
        string summary,
        ReviewTarget highlightA,
        ReviewTarget highlightB)
    {
        if (_activeShip == null)
            return;

        _activeShip.ProtocolReviewVerified = true;
        _verifiedViolationSummary = summary;
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _reviewStatusNote = "VERIFIED -- DETAIN AUTHORIZED.";

        BuildConfrontationLines(violationType, out string officerPrompt, out string captainExcuse);

        _faultStage = FaultSequenceStage.FaultBeat;
        _faultStageTimer = FaultBeatDurationSeconds;
        _faultHighlightA = highlightA;
        _faultHighlightB = highlightB;
        _pendingFaultSummary = summary;
        _pendingOfficerAccusation = officerPrompt;
        _pendingCaptainReaction = captainExcuse;

        AudioHooks.Play("protocol_fault_pulse");
    }

    private void UpdateFaultSequence(float deltaTime)
    {
        if (_faultStage == FaultSequenceStage.None)
            return;

        if (_activeShip == null)
        {
            ResetFaultSequenceState();
            return;
        }

        _faultStageTimer -= deltaTime;
        if (_faultStage == FaultSequenceStage.FaultBeat && _faultStageTimer <= 0f)
        {
            // Fault beat done -> print Officer accusation FIRST, trigger brief static flicker on captain portrait,
            // then wait 0.60s before printing captain's reaction!
            string tag = IsProtocolReviewUnlocked ? "[PROTOCOL FAULT]" : "[SPS FAULT]";
            AddTranscriptLine($"{tag} {_pendingFaultSummary}", Palette.AlertCrimson);
            AddTranscriptLine($"> BOOTH 04: \"{_pendingOfficerAccusation}\"", Palette.ClearanceTeal);
            AudioHooks.Play("terminal_key_confirm");

            _portraitStaticFlickerTimer = 0.28f;
            _faultStage = FaultSequenceStage.OfficerAccusationPause;
            _faultStageTimer = OfficerToCaptainPauseSeconds;
        }
        else if (_faultStage == FaultSequenceStage.OfficerAccusationPause && _faultStageTimer <= 0f)
        {
            // Staggered captain reaction arrives after pause + portrait static flicker
            AddTranscriptLine($"CAPT: \"{_pendingCaptainReaction}\"", Palette.CrtAmberPrimary);
            AudioHooks.Play("comms_reply_blip");

            _faultStage = FaultSequenceStage.None;
            _faultHighlightA = ReviewTarget.None;
            _faultHighlightB = ReviewTarget.None;
        }
    }

    private void BuildConfrontationLines(ShipInfractionType violationType, out string officerPrompt, out string captainExcuse)
    {
        if (_activeShip == null)
        {
            officerPrompt = string.Empty;
            captainExcuse = string.Empty;
            return;
        }

        switch (violationType)
        {
            case ShipInfractionType.TransponderIdMismatch:
                officerPrompt = $"Our raw transponder reads '{_activeShip.GroundTruth.RawTransponderId}', which does not match your Data Slate code '{_activeShip.Slate.RegistrationCode}'.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseTransponderId;
                break;

            case ShipInfractionType.ExpiredRegistration:
                (int cy, int cm) = Constants.GetStationCycle(_gameState.Day);
                officerPrompt = $"Your registration expired on {_activeShip.Slate.RegistrationExpiryDisplay}. Current cycle is {Constants.FormatStationCycle(cy, cm)}.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseExpired;
                break;

            case ShipInfractionType.PrefixOrganizationMismatch:
                officerPrompt = $"Prefix '{_activeShip.Slate.DeclaredPrefix}' does not belong to '{_activeShip.Slate.DeclaredFactionName}'.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseOrgMismatch;
                break;

            case ShipInfractionType.PrefixSpeciesMismatch:
                officerPrompt = $"Prefix '{_activeShip.Slate.DeclaredPrefix}' is restricted and does not permit a {_activeShip.Slate.DeclaredCaptainSpecies} captain.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseSpeciesMismatch;
                break;

            case ShipInfractionType.PrefixHullSilhouetteMismatch:
                if (_activeShip.GroundTruth.IsClassSilhouetteMismatch)
                {
                    officerPrompt = $"Your Data Slate declares a {ShortClass(_activeShip.Slate.DeclaredShipClass)} vessel, yet sensors show a {ShortClass(_activeShip.GroundTruth.ActualShipClass)} hull silhouette.";
                    captainExcuse = _activeShip.ConfrontationGuiltyExcuseClassMismatch;
                }
                else
                {
                    officerPrompt = $"Your Data Slate declares '{_activeShip.Slate.DeclaredPrefix}', which does not match your hull silhouette.";
                    captainExcuse = _activeShip.ConfrontationGuiltyExcuseHull;
                }
                break;

            case ShipInfractionType.SpoofedWeaponsHot:
                officerPrompt = "Your Data Slate claims weapons OFFLINE, yet SPS shows live power feeding forward mounts.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseWeapons;
                break;

            case ShipInfractionType.UnexplainedCargoPowerDraw:
                officerPrompt = "Your manifest declares an empty unpowered hold, yet SPS shows high-draw violet power in your cargo bay.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseCargoPower;
                break;

            case ShipInfractionType.FalsifiedOriginWaveform:
                officerPrompt = $"Your thermal warp strip does not match your declared departure from {_activeShip.Slate.OriginSystem}.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseOrigin;
                break;

            default:
                officerPrompt = "Protocol discrepancy verified on your vessel.";
                captainExcuse = _activeShip.RefusalLine;
                break;
        }
    }

    // =========================================================================
    // Day 3+ Manual Warp Field Scan, L->R Extrusion & Glowing Warp Ref Spot
    // =========================================================================
    private bool CanStartManualWarpScan() =>
        IsSpectrometerOperational &&
        _activeShip != null &&
        (_phase is EncounterPhase.OpeningGreetingDelay
            or EncounterPhase.AwaitingPortForDownload
            or EncounterPhase.DownloadingProtocols
            or EncounterPhase.DownloadAborted
            or EncounterPhase.InspectingSlateAndShip
            or EncounterPhase.AwaitingClearanceReturnToTopPort) &&
        !_activeShip.GroundTruth.ActualShieldsUp &&
        _warpPhase == WarpPrinterPhase.Idle;

    private bool TryHandleWarpHardwareClicks(VirtualMouseState mouse)
    {
        // [SCAN WARP] button on left housing of Warp Residue Printer (112, 264, 88, 88)
        if (mouse.IsHit(WarpPrinterX + 4, WarpPrinterY + 56, WarpHousingW - 8, 26))
        {
            TryStartManualWarpScan();
            return true;
        }

        // Glowing Warp Reference Spot selector bar (448, 270, 216, 22) [<] and [>] system cycle buttons
        int sysCount = WarpSignatureGenerator.SectorSystems.Length;
        if (mouse.IsHit(WarpSelectorX, WarpSelectorY, 24, WarpSelectorH))
        {
            _warpRefSpotSystemIndex = (_warpRefSpotSystemIndex - 1 + sysCount) % sysCount;
            AudioHooks.Play("terminal_key_tab");
            return true;
        }
        if (mouse.IsHit(WarpSelectorX + WarpSelectorW - 24, WarpSelectorY, 24, WarpSelectorH))
        {
            _warpRefSpotSystemIndex = (_warpRefSpotSystemIndex + 1) % sysCount;
            AudioHooks.Play("terminal_key_tab");
            return true;
        }

        return false;
    }

    private void TryStartManualWarpScan()
    {
        if (!CanStartManualWarpScan() || _activeShip == null)
        {
            AudioHooks.Play("citation_buzz");
            return;
        }

        _warpPhase = WarpPrinterPhase.ScanningWarpField;
        _warpPhaseElapsed = 0f;
        _warpPrinterTickAccum = 0f;
        _warpStripActualOrigin = _activeShip.GroundTruth.ActualOriginSystem;
        _warpStripShipReg = _activeShip.Slate.RegistrationCode;
        AudioHooks.Play("terminal_key_confirm");
    }

    private void UpdateWarpPrinterState(float animDt)
    {
        if (_warpPhase == WarpPrinterPhase.ScanningWarpField)
        {
            _warpPhaseElapsed += animDt;
            if (_warpPhaseElapsed >= WarpScanDurationSeconds)
            {
                _warpPhase = WarpPrinterPhase.PrintingStrip;
                _warpPhaseElapsed = 0f;
                _warpPrinterTickAccum = 0f;
                AudioHooks.Play("printer_tick");
            }
        }
        else if (_warpPhase == WarpPrinterPhase.PrintingStrip)
        {
            _warpPhaseElapsed += animDt;
            _warpPrinterTickAccum += animDt;
            if (_warpPrinterTickAccum >= 0.34f)
            {
                _warpPrinterTickAccum = 0f;
                AudioHooks.Play("printer_tick");
            }

            if (_warpPhaseElapsed >= WarpPrintDurationSeconds)
            {
                _warpPhaseElapsed = WarpPrintDurationSeconds;
                _warpPhase = WarpPrinterPhase.AnchoredReadyToTear;
                AudioHooks.Play("printer_tick");
            }
        }
    }

    private void UpdateAnchoredStripTearPull(VirtualMouseState mouse)
    {
        if (mouse.LeftDown)
        {
            int dx = mouse.X - _pullStartMouseX;
            int dy = mouse.Y - _pullStartMouseY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist >= TearOffThresholdPx)
            {
                _isPullingAnchoredStrip = false;
                _anchoredStripPullOffsetX = 0;
                _anchoredStripPullOffsetY = 0;
                _warpPhase = WarpPrinterPhase.TornOffFreeDocument;
                AudioHooks.Play("paper_tear");

                DraggableDeskItem strip = _deskItems[DeskItemId.WarpStrip];
                strip.IsVisible = true;
                strip.X = Math.Clamp(mouse.X - StripWidth / 2, DragBoundsX, DragBoundsX + DragBoundsW - strip.Width);
                strip.Y = Math.Clamp(mouse.Y - StripFullHeight / 2, WarpRefSpotY, DragBoundsY + DragBoundsH - strip.Height);
                BringDeskItemToFront(DeskItemId.WarpStrip);

                _draggingItemId = DeskItemId.WarpStrip;
                _dragOffsetX = StripWidth / 2;
                _dragOffsetY = StripFullHeight / 2;
                return;
            }

            _anchoredStripPullOffsetX = Math.Clamp(dx / 2, 0, 8);
            _anchoredStripPullOffsetY = Math.Clamp(dy / 2, -6, 6);
            return;
        }

        _isPullingAnchoredStrip = false;
        _anchoredStripPullOffsetX = 0;
        _anchoredStripPullOffsetY = 0;
    }

    // =========================================================================
    // Day 2+ SPS Hold-to-Lock Anomaly Scanner
    // (Detects ALL hot weapons -- both honest armed AND spoofed -- plus violet cargo draw;
    //  SPS Mini-Screen is strictly a diagnostic log on Day 2 and Protocol Review evidence on Day 3+)
    // =========================================================================
    private void UpdateSpsHoldToLock(float deltaTime, VirtualMouseState mouse)
    {
        bool shipInInspectableBayState =
            _phase is EncounterPhase.OpeningGreetingDelay
                or EncounterPhase.AwaitingPortForDownload
                or EncounterPhase.DownloadingProtocols
                or EncounterPhase.DownloadAborted
                or EncounterPhase.InspectingSlateAndShip
                or EncounterPhase.AwaitingClearanceReturnToTopPort;

        bool canScanHardpoints =
            IsSpsUnlocked &&
            _spsEnabled &&
            _preShiftStage == PreShiftStage.None &&
            _activeShip != null &&
            _activeShipHardpoints != null &&
            shipInInspectableBayState &&
            !_activeShip.GroundTruth.ActualShieldsUp &&
            mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH);

        if (!canScanHardpoints || _activeShip == null || _activeShipHardpoints == null)
        {
            _spsLockProgress = Math.Max(0f, _spsLockProgress - deltaTime * 2.0f);
            if (_spsLockProgress <= 0f)
            {
                _spsLockTarget = SpsAnomalyKind.None;
            }
            return;
        }

        float reticleCx = _lastSpsLensRect.X + _lastSpsLensRect.Width * 0.5f;
        float reticleCy = _lastSpsLensRect.Y + _lastSpsLensRect.Height * 0.5f;
        float shipBaseX = ShipAnchorX + _shipSlideOffset;
        float shipBaseY = ShipAnchorY;

        SpsAnomalyKind hoveredAnomaly = SpsAnomalyKind.None;

        // Detect ALL currently hot weapon mounts (both openly declared honest armed ships AND spoofed liars)
        bool hasUnloggedHotWeapons =
            ShiftController.ShouldSpsDetectHotWeapons(_activeShip) &&
            !_spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.HotWeaponMount && !e.IsCooled);

        if (hasUnloggedHotWeapons)
        {
            float wx = shipBaseX + _activeShipHardpoints.ForwardWeapons.X * 2.0f + 1.0f;
            float wy = shipBaseY + _activeShipHardpoints.ForwardWeapons.Y * 2.0f + 1.0f;
            float dx = reticleCx - wx;
            float dy = reticleCy - wy;
            if (dx * dx + dy * dy <= SpsLockRadiusPx * SpsLockRadiusPx)
            {
                hoveredAnomaly = SpsAnomalyKind.HotWeaponMount;
            }
        }

        if (hoveredAnomaly == SpsAnomalyKind.None)
        {
            bool hasUnloggedCargoPower =
                _activeShip.GroundTruth.ActualCargoPowerDraw &&
                !_spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.VioletCargoPower && !e.IsCooled);

            if (hasUnloggedCargoPower)
            {
                float cx = shipBaseX + _activeShipHardpoints.CargoBay.X * 2.0f + 1.0f;
                float cy = shipBaseY + _activeShipHardpoints.CargoBay.Y * 2.0f + 1.0f;
                float dx = reticleCx - cx;
                float dy = reticleCy - cy;
                if (dx * dx + dy * dy <= SpsLockRadiusPx * SpsLockRadiusPx)
                {
                    hoveredAnomaly = SpsAnomalyKind.VioletCargoPower;
                }
            }
        }

        if (hoveredAnomaly != SpsAnomalyKind.None)
        {
            if (_spsLockTarget != hoveredAnomaly)
            {
                _spsLockTarget = hoveredAnomaly;
                _spsLockProgress = 0f;
            }

            _spsLockProgress = Math.Min(1.0f, _spsLockProgress + deltaTime / SpsLockDurationSeconds);
            if (_spsLockProgress >= 1.0f)
            {
                if (hoveredAnomaly == SpsAnomalyKind.HotWeaponMount)
                {
                    _spsLoggedAnomalies.RemoveAll(e => e.Kind == SpsAnomalyKind.HotWeaponMount);
                    _spsLoggedAnomalies.Add(new SpsAnomalyEntry
                    {
                        Kind = SpsAnomalyKind.HotWeaponMount,
                        Label = "MOUNT HOT",
                        AccentColor = Palette.SpsWeaponsHotOrange
                    });
                }
                else if (hoveredAnomaly == SpsAnomalyKind.VioletCargoPower)
                {
                    _spsLoggedAnomalies.RemoveAll(e => e.Kind == SpsAnomalyKind.VioletCargoPower);
                    _spsLoggedAnomalies.Add(new SpsAnomalyEntry
                    {
                        Kind = SpsAnomalyKind.VioletCargoPower,
                        Label = "VIOLET DRAW",
                        AccentColor = Palette.SpsCargoHighDraw
                    });
                }

                AudioHooks.Play("sps_lock");
                _spsLockTarget = SpsAnomalyKind.None;
                _spsLockProgress = 0f;
            }
        }
        else
        {
            _spsLockProgress = Math.Max(0f, _spsLockProgress - deltaTime * 1.5f);
            if (_spsLockProgress <= 0f)
            {
                _spsLockTarget = SpsAnomalyKind.None;
            }
        }
    }

    // =========================================================================
    // Day 3+ Protocol Review Mode (Claim vs. Reality)
    // =========================================================================
    private bool TryHandleProtocolReviewClick(VirtualMouseState mouse)
    {
        if (_faultStage != FaultSequenceStage.None)
            return false;

        ReviewTarget hitTarget = DetectProtocolReviewTargetAtMouse(mouse);
        if (hitTarget == ReviewTarget.None || _activeShip == null)
            return false;

        AudioHooks.Play("terminal_key_click");

        // Standalone violation: Day 14+ compliance refusal
        if (hitTarget == ReviewTarget.ViewportShipHull && _captainRefusedCompliance)
        {
            BeginProtocolFaultSequence(
                ShipInfractionType.RefusedShieldsOrWeaponsCompliance,
                "OUTRIGHT REFUSAL TO LOWER SHIELDS / POWER DOWN WEAPONS",
                ReviewTarget.ViewportShipHull,
                ReviewTarget.None);
            return true;
        }

        if (_reviewFirstSelection == ReviewTarget.None)
        {
            _reviewFirstSelection = hitTarget;
            _reviewStatusNote = $"REVIEW 1/2: {FormatReviewTargetLabel(hitTarget)}";
            return true;
        }

        if (_reviewFirstSelection == hitTarget)
        {
            _reviewFirstSelection = ReviewTarget.None;
            _reviewStatusNote = "SELECTION CLEARED.";
            return true;
        }

        EvaluateProtocolReviewPair(_reviewFirstSelection, hitTarget);
        _reviewFirstSelection = ReviewTarget.None;
        return true;
    }

    private ReviewTarget DetectProtocolReviewTargetAtMouse(VirtualMouseState mouse)
    {
        if (_activeShip == null)
            return ReviewTarget.None;

        if (mouse.IsHit(TopCycleX, TopCycleY, TopCycleW, TopCycleH))
            return ReviewTarget.TopHeaderCycle;

        if (mouse.IsHit(FeedBoxX, FeedBoxY, FeedBoxW, FeedBoxH))
            return ReviewTarget.VisualSignalTransponderId;

        // Day 3+ SPS Anomaly Mini-Screen: strictly selectable evidence source inside Protocol Review mode
        if (IsSpsUnlocked && _spsLoggedAnomalies.Count > 0)
        {
            int maxRows = Math.Min(3, _spsLoggedAnomalies.Count);
            for (int i = 0; i < maxRows; i++)
            {
                SpsAnomalyEntry entry = _spsLoggedAnomalies[i];
                if (entry.IsCooled)
                    continue;

                int ry = SpsMiniY + 24 + i * 36;
                if (mouse.IsHit(SpsMiniX + 4, ry, SpsMiniW - 8, 30))
                {
                    return entry.Kind == SpsAnomalyKind.HotWeaponMount
                        ? ReviewTarget.SpsMiniScreenHotWeapons
                        : ReviewTarget.SpsMiniScreenCargoPower;
                }
            }
        }

        if (IsSpsUnlocked && _spsEnabled && mouse.IsHit(_lastSpsLensRect))
        {
            Rectangle shipRect = new(ShipAnchorX + _shipSlideOffset, ShipAnchorY, ShipRenderW, ShipRenderH);
            bool lensOverlapsShip =
                _lastSpsLensRect.X < shipRect.X + shipRect.Width &&
                _lastSpsLensRect.X + _lastSpsLensRect.Width > shipRect.X &&
                _lastSpsLensRect.Y < shipRect.Y + shipRect.Height &&
                _lastSpsLensRect.Y + _lastSpsLensRect.Height > shipRect.Y;

            if (lensOverlapsShip)
                return ReviewTarget.SpsLensConduit;
        }

        if (mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH))
            return ReviewTarget.ViewportShipHull;

        if (IsSpectrometerOperational && _warpPhase == WarpPrinterPhase.AnchoredReadyToTear &&
            mouse.IsHit(StripAnchorX, StripAnchorY, StripWidth, StripFullHeight))
        {
            return ReviewTarget.WarpStripPaper;
        }

        for (int i = _zOrder.Count - 1; i >= 0; i--)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible)
                continue;

            if (id == DeskItemId.DataSlate && IsDataSlateInClearanceDock && _phase == EncounterPhase.UploadingClearance)
                continue;

            if (!mouse.IsHit(item.X, item.Y, item.Width, item.Height))
                continue;

            if (id == DeskItemId.WarpStrip && _warpPhase == WarpPrinterPhase.TornOffFreeDocument)
                return ReviewTarget.WarpStripPaper;

            if (id == DeskItemId.DataSlate && IsShipInspectableForReview && IsSlateReadableForReview)
            {
                int rightHotW = item.Width - 78; // 106px
                if (mouse.IsHit(item.X + 73, item.Y + 21, rightHotW, 22))
                    return ReviewTarget.DataSlatePrefix;

                if (mouse.IsHit(item.X + 73, item.Y + 44, rightHotW, 13))
                    return ReviewTarget.DataSlateClass;

                if (mouse.IsHit(item.X + 73, item.Y + 58, rightHotW, 13))
                    return ReviewTarget.DataSlateWeapons;

                int rowW = item.Width - 8;
                if (mouse.IsHit(item.X + 4, item.Y + 103, rowW, 12))
                    return ReviewTarget.DataSlateOrg;

                if (mouse.IsHit(item.X + 4, item.Y + 115, rowW, 12))
                    return ReviewTarget.DataSlateExpiry;

                if (mouse.IsHit(item.X + 4, item.Y + 139, rowW, 12))
                    return ReviewTarget.DataSlateSpecies;

                if (mouse.IsHit(item.X + 4, item.Y + 151, rowW, 12))
                    return ReviewTarget.DataSlateOrigin;

                if (mouse.IsHit(item.X + 4, item.Y + 175, rowW, 24))
                    return ReviewTarget.DataSlateCargo;
            }
            else if (id == DeskItemId.ReferenceManual)
            {
                return ReviewTarget.None;
            }

            return ReviewTarget.None;
        }

        return ReviewTarget.None;
    }

    private void EvaluateProtocolReviewPair(ReviewTarget a, ReviewTarget b)
    {
        if (_activeShip == null)
            return;

        bool IsPair(ReviewTarget x, ReviewTarget y) => (a == x && b == y) || (a == y && b == x);
        ShipGroundTruth truth = _activeShip.GroundTruth;

        // 0. Data Slate REG CODE vs. Raw Transponder ID
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.TransponderIdMismatch) ||
                !string.Equals(truth.RawTransponderId, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
            {
                BeginProtocolFaultSequence(
                    ShipInfractionType.TransponderIdMismatch,
                    $"RAW ID '{truth.RawTransponderId}' vs SLATE '{_activeShip.Slate.RegistrationCode}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 1. Data Slate EXP vs. Top Console Station Cycle
        if (IsPair(ReviewTarget.DataSlateExpiry, ReviewTarget.TopHeaderCycle))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.ExpiredRegistration))
            {
                (int curY, int curM) = Constants.GetStationCycle(_gameState.Day);
                BeginProtocolFaultSequence(
                    ShipInfractionType.ExpiredRegistration,
                    $"EXPIRED REG ({_activeShip.Slate.RegistrationExpiryDisplay} < CYC {Constants.FormatStationCycle(curY, curM)})",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 2. Data Slate Prefix vs. Data Slate ORG
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateOrg))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixOrganizationMismatch))
            {
                BeginProtocolFaultSequence(
                    ShipInfractionType.PrefixOrganizationMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs ORG '{_activeShip.Slate.DeclaredFactionName}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 3. Data Slate Prefix/ORG vs. Data Slate SPEC
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateSpecies) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.DataSlateSpecies))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixSpeciesMismatch))
            {
                BeginProtocolFaultSequence(
                    ShipInfractionType.PrefixSpeciesMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' PROHIBITS {_activeShip.Slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 4. Data Slate Prefix/ORG/CLASS vs. Live Viewport Ship Hull
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.DataSlateClass, ReviewTarget.ViewportShipHull))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixHullSilhouetteMismatch))
            {
                string summary = truth.IsClassSilhouetteMismatch
                    ? $"CLASS '{ShortClass(_activeShip.Slate.DeclaredShipClass)}' vs {ShortClass(truth.ActualShipClass)} HULL"
                    : $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs {ShipGenerator.GetHullFactionReadableName(truth.ActualVisualFaction)} HULL";
                BeginProtocolFaultSequence(ShipInfractionType.PrefixHullSilhouetteMismatch, summary, a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 5. Data Slate WPN: OFFLINE vs. SPS Anomaly Mini-Screen [!] HOT WPN or SPS Lens Conduit
        //    (Honest armed ships declaring WPN: ARMED have HasSpoofedWeapons == false -> NO DISCREPANCY!)
        if (IsPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons) ||
            IsPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsLensConduit))
        {
            if (truth.HasSpoofedWeapons && truth.ActualWeaponsHot && !truth.ActualShieldsUp)
            {
                foreach (SpsAnomalyEntry entry in _spsLoggedAnomalies)
                {
                    if (entry.Kind == SpsAnomalyKind.HotWeaponMount && !entry.IsCooled)
                    {
                        entry.WasVerified = true;
                    }
                }

                BeginProtocolFaultSequence(
                    ShipInfractionType.SpoofedWeaponsHot,
                    "SPOOFED WEAPONS: SLATE 'OFFLINE' vs HOT SPS MOUNT",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 6. Data Slate CARGO vs. SPS Anomaly Mini-Screen Violet Cargo Draw or SPS Lens Conduit
        if (IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.SpsMiniScreenCargoPower) ||
            IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.SpsLensConduit))
        {
            if (truth.ActualCargoPowerDraw && !truth.ActualShieldsUp)
            {
                foreach (SpsAnomalyEntry entry in _spsLoggedAnomalies)
                {
                    if (entry.Kind == SpsAnomalyKind.VioletCargoPower && !entry.IsCooled)
                    {
                        entry.WasVerified = true;
                    }
                }

                BeginProtocolFaultSequence(
                    ShipInfractionType.UnexplainedCargoPowerDraw,
                    "SPS VIOLET POWER DRAW IN DECLARED EMPTY HOLD",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 7. Data Slate Origin vs. Thermal Warp Strip
        if (IsPair(ReviewTarget.DataSlateOrigin, ReviewTarget.WarpStripPaper))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform))
            {
                BeginProtocolFaultSequence(
                    ShipInfractionType.FalsifiedOriginWaveform,
                    $"WARP STRIP '{truth.ActualOriginSystem.ToUpperInvariant()}' vs SLATE '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        LogNoDiscrepancyDetected();
    }

    private void LogNoDiscrepancyDetected()
    {
        _reviewStatusNote = "NO DISCREPANCY DETECTED.";
        AddTranscriptLine("[SYS] PROTOCOL REVIEW: NO DISCREPANCY DETECTED.", Palette.CrtAmberDimmed);
        AudioHooks.Play("terminal_key_click");
    }

    private static string FormatReviewTargetLabel(ReviewTarget target) =>
        target switch
        {
            ReviewTarget.DataSlatePrefix => "SLATE REG CODE",
            ReviewTarget.DataSlateClass => "SLATE CLASS",
            ReviewTarget.DataSlateWeapons => "SLATE WPN STATUS",
            ReviewTarget.DataSlateOrigin => "SLATE ORIGIN",
            ReviewTarget.DataSlateOrg => "SLATE ORG",
            ReviewTarget.DataSlateExpiry => "SLATE EXPIRY",
            ReviewTarget.DataSlateSpecies => "SLATE SPECIES",
            ReviewTarget.DataSlateCargo => "SLATE CARGO",
            ReviewTarget.VisualSignalTransponderId => "RAW TRANSPONDER ID",
            ReviewTarget.TopHeaderCycle => "STATION CYCLE",
            ReviewTarget.ViewportShipHull => "VIEWPORT HULL",
            ReviewTarget.SpsLensConduit => "SPS LENS CONDUIT",
            ReviewTarget.WarpStripPaper => "WARP STRIP PAPER",
            ReviewTarget.SpsMiniScreenHotWeapons => "SPS LOG: HOT WPN",
            ReviewTarget.SpsMiniScreenCargoPower => "SPS LOG: CARGO PWR",
            _ => "NONE"
        };

    // =========================================================================
    // Encounter State Machine (Scaled cleanly by AnimationSpeedMultiplier)
    // =========================================================================
    private void UpdateEncounterPhase(float deltaTime, float animDt)
    {
        if (_activeShip == null)
            return;

        // Compliance orders (Day 2+)
        if ((_pendingShieldsOrder || _pendingWeaponsOrder) &&
            _phase is EncounterPhase.OpeningGreetingDelay
                or EncounterPhase.AwaitingPortForDownload
                or EncounterPhase.DownloadingProtocols
                or EncounterPhase.DownloadAborted
                or EncounterPhase.InspectingSlateAndShip
                or EncounterPhase.AwaitingClearanceReturnToTopPort)
        {
            _complianceTimer -= deltaTime;
            if (_complianceTimer <= 0f)
            {
                ResolvePendingComplianceOrders();
            }
        }

        switch (_phase)
        {
            case EncounterPhase.ShipGlidingIn:
            {
                _shipApproachElapsed = Math.Min(ShipApproachDurationSeconds, _shipApproachElapsed + animDt);
                float t = _shipApproachElapsed / ShipApproachDurationSeconds;
                float eased = 1.0f - MathF.Pow(1.0f - t, 3.0f);
                _shipSlideOffset = (1.0f - eased) * 380.0f;

                if (_shipApproachElapsed >= ShipApproachDurationSeconds)
                {
                    _shipSlideOffset = 0f;
                    _phase = EncounterPhase.OpeningGreetingDelay;
                    _subspaceAutoTransferDelay = OpeningGreetingBeatSeconds;
                    _transcriptLines.Clear();
                    _transcriptScrollTopLine = 0;
                    _transcriptAutoFollowBottom = true;

                    RebuildLivePortraitTexture();
                    AudioHooks.Play("hail_incoming_chime");
                    AddTranscriptLine($"HAIL [{_activeShip.Slate.RegistrationCode}]: {_activeShip.GreetingHailLine}", Palette.CrtAmberPrimary);
                }
                break;
            }

            case EncounterPhase.OpeningGreetingDelay:
            {
                _subspaceAutoTransferDelay -= animDt;
                if (_subspaceAutoTransferDelay <= 0f)
                {
                    InitiateSubspaceProtocolRequest();
                }
                break;
            }

            case EncounterPhase.AwaitingPortForDownload:
            {
                if (IsDataSlateInSlateBus)
                {
                    BeginSubspaceProtocolsDownload(isReTransmit: false);
                }
                break;
            }

            case EncounterPhase.DownloadingProtocols:
            {
                _protocolDownloadElapsed += animDt;
                _dataStreamAudioAccum += deltaTime;
                if (_dataStreamAudioAccum >= 0.45f)
                {
                    _dataStreamAudioAccum = 0f;
                    AudioHooks.Play("data_stream");
                }

                if (_protocolDownloadElapsed >= ProtocolDownloadDurationSeconds)
                {
                    CompleteSubspaceProtocolsDownload();
                }
                break;
            }

            case EncounterPhase.DownloadAborted:
            {
                if (_pendingReTransmitRestartTimer > 0f)
                {
                    _pendingReTransmitRestartTimer -= deltaTime;
                    if (_pendingReTransmitRestartTimer <= 0f)
                    {
                        if (IsDataSlateInSlateBus)
                        {
                            BeginSubspaceProtocolsDownload(isReTransmit: true);
                        }
                        else
                        {
                            AddTranscriptLine("[SLATE BUS] PLUG DATA SLATE INTO SLATE BUS & CLICK [RE-TRANSMIT].", Palette.AlertCrimson);
                        }
                    }
                }
                break;
            }

            case EncounterPhase.InspectingSlateAndShip:
            {
                break;
            }

            case EncounterPhase.UploadingClearance:
            {
                _shipSlideOffset = 0f;
                _clearanceUploadElapsed = Math.Min(ClearanceUploadDurationSeconds, _clearanceUploadElapsed + animDt);
                if (_clearanceUploadElapsed >= ClearanceUploadDurationSeconds)
                {
                    _slateTelemetry = DataSlateTelemetryState.ClearedReadyToTransmit;
                    _phase = EncounterPhase.AwaitingClearanceReturnToTopPort;
                    // Snap side clamp latches open with a crisp 2px upward release kick on the docked slate
                    _dockSettleActive = false;
                    _dockSettleElapsed = 0f;
                    _dockLatchTarget = 0f;
                    _dockReleaseKickActive = true;
                    _dockReleaseKickElapsed = 0f;
                    AudioHooks.Play("terminal_key_confirm");
                    AddTranscriptLine("[CLEARANCE DOCK] ENCRYPTION 100% -- RETURN DATA SLATE TO SLATE BUS TO TRANSMIT.", Palette.ClearanceTeal);
                }
                break;
            }

            case EncounterPhase.AwaitingClearanceReturnToTopPort:
            {
                _shipSlideOffset = 0f;
                break;
            }

            case EncounterPhase.TransmittingClearance:
            {
                _shipSlideOffset = 0f;
                _clearanceTransmitElapsed = Math.Min(ClearanceTransmitDurationSeconds, _clearanceTransmitElapsed + animDt);
                _dataStreamAudioAccum += deltaTime;
                if (_dataStreamAudioAccum >= 0.35f)
                {
                    _dataStreamAudioAccum = 0f;
                    AudioHooks.Play("data_stream");
                }

                if (_clearanceTransmitElapsed >= ClearanceTransmitDurationSeconds)
                {
                    _slateTelemetry = DataSlateTelemetryState.Empty;
                    ResetSlateDockMicroAnimations();
                    PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
                    AudioHooks.Play("clearance_granted");
                    AddTranscriptLine("[SLATE BUS] CLEARANCE TRANSMITTED -- DEPARTURE AUTHORIZED.", Palette.ClearanceTeal);
                    _pendingDecision = PlayerDecision.GrantAccess;
                    _decisionEvalTimer = ShipDepartureDurationSeconds;
                    _phase = EncounterPhase.ResolvingDecision;
                }
                break;
            }

            case EncounterPhase.DetainClamping:
            {
                // Deliberate Detain clamp-lock + slate-wipe sequence sharing exact StandardTransferDurationSeconds
                // with protocol download, clearance encryption, and clearance transmission (uniformly scaled by animDt).
                _shipSlideOffset = 0f;
                _detainClampElapsed = Math.Min(DetainSequenceDurationSeconds, _detainClampElapsed + animDt);
                float impactTime = DetainSequenceDurationSeconds * ShiftController.DetainClampImpactFraction;
                float wipeTime = DetainSequenceDurationSeconds * ShiftController.DetainSlateWipeFraction;

                if (!_detainClampImpactPlayed && _detainClampElapsed >= impactTime)
                {
                    _detainClampImpactPlayed = true;
                    _viewportImpactShakeTimer = 0.16f;
                    AudioHooks.Play("detain_clamp_lock");
                }

                if (!_detainSlateWipeCompleted && _detainClampElapsed >= wipeTime)
                {
                    _detainSlateWipeCompleted = true;
                    _slateTelemetry = DataSlateTelemetryState.Empty;
                    _slateWipeFlashTimer = 1.15f;
                    ResetSlateDockMicroAnimations();
                    PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
                    AudioHooks.Play("slate_wipe");

                    if (IsDataSlateInClearanceDock)
                    {
                        _slateLocation = DataSlateLocation.SlateBus;
                        DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
                        slate.X = SlateBusDockedX;
                        slate.Y = SlateBusDockedY;
                    }

                    AddTranscriptLine("[DATA SLATE] ACTIVE TELEMETRY WIPED.", Palette.CrtAmberDimmed);
                }

                if (_detainClampElapsed >= DetainSequenceDurationSeconds)
                {
                    _pendingDecision = PlayerDecision.Detain;
                    FinalizeShipResolution();
                }
                break;
            }

            case EncounterPhase.ResolvingDecision:
            {
                if (_pendingDecision == PlayerDecision.GrantAccess)
                {
                    float depT = Math.Clamp(1.0f - (_decisionEvalTimer / ShipDepartureDurationSeconds), 0f, 1f);
                    _shipSlideOffset = -(depT * depT) * 420.0f;
                    _decisionEvalTimer -= animDt;
                }
                else
                {
                    _decisionEvalTimer -= animDt;
                }

                if (_decisionEvalTimer <= 0f)
                {
                    FinalizeShipResolution();
                }
                break;
            }
        }
    }

    private void ResolvePendingComplianceOrders()
    {
        if (_activeShip == null)
            return;

        bool rebuildShip = false;

        if (_pendingShieldsOrder)
        {
            _pendingShieldsOrder = false;
            ComplianceOrderOutcome outcome = ShiftController.ResolveShieldsOrder(_gameState, _activeShip);
            switch (outcome)
            {
                case ComplianceOrderOutcome.AlreadyCompliant:
                    AddTranscriptLine("CAPT: \"Our shields are already down, Booth 04.\"", Palette.CrtAmberDimmed);
                    break;
                case ComplianceOrderOutcome.RefusedOutright:
                    _captainRefusedCompliance = true;
                    _activeShip.ProtocolReviewVerified = true;
                    _verifiedViolationSummary = "REFUSED ORDER TO LOWER SHIELDS";
                    AudioHooks.Play("citation_buzz");
                    AddTranscriptLine($"CAPT: \"{_activeShip.RefusalLine}\"", Palette.CrtAmberPrimary);
                    AddTranscriptLine("[SYS] COMPLIANCE REFUSAL VERIFIED.", Palette.AlertCrimson);
                    break;
                case ComplianceOrderOutcome.Complied:
                    rebuildShip = true;
                    AudioHooks.Play("terminal_key_confirm");
                    AddTranscriptLine($"CAPT: \"{_activeShip.ShieldsLoweredLine}\"", Palette.CrtAmberPrimary);
                    break;
            }
        }

        if (_pendingWeaponsOrder)
        {
            _pendingWeaponsOrder = false;
            ComplianceOrderOutcome outcome = ShiftController.ResolveWeaponsOrder(_gameState, _activeShip);
            switch (outcome)
            {
                case ComplianceOrderOutcome.AlreadyCompliant:
                    AddTranscriptLine("CAPT: \"Our weapon mounts are already powered down.\"", Palette.CrtAmberDimmed);
                    break;
                case ComplianceOrderOutcome.SpoofedDeflection:
                    AudioHooks.Play("terminal_key_click");
                    AddTranscriptLine($"CAPT: \"{_activeShip.SpoofedWeaponsDeflectionLine}\"", Palette.CrtAmberPrimary);
                    break;
                case ComplianceOrderOutcome.RefusedOutright:
                    _captainRefusedCompliance = true;
                    _activeShip.ProtocolReviewVerified = true;
                    _verifiedViolationSummary = "REFUSED ORDER TO POWER DOWN WEAPONS";
                    AudioHooks.Play("citation_buzz");
                    AddTranscriptLine($"CAPT: \"{_activeShip.RefusalLine}\"", Palette.CrtAmberPrimary);
                    AddTranscriptLine("[SYS] COMPLIANCE REFUSAL VERIFIED.", Palette.AlertCrimson);
                    break;
                case ComplianceOrderOutcome.Complied:
                    rebuildShip = true;
                    // Immediately cool/clear active hot-weapon readout in SPS Anomaly Mini-Screen to match visual cooling
                    foreach (SpsAnomalyEntry entry in _spsLoggedAnomalies)
                    {
                        if (entry.Kind == SpsAnomalyKind.HotWeaponMount)
                        {
                            entry.IsCooled = true;
                        }
                    }
                    if (_spsLockTarget == SpsAnomalyKind.HotWeaponMount)
                    {
                        _spsLockTarget = SpsAnomalyKind.None;
                        _spsLockProgress = 0f;
                    }
                    AudioHooks.Play("terminal_key_confirm");
                    AddTranscriptLine($"CAPT: \"{_activeShip.WeaponsPoweredDownLine}\"", Palette.CrtAmberPrimary);
                    break;
            }
        }

        if (rebuildShip)
        {
            RebuildActiveShipTextures();
        }
    }

    private void InitiateSubspaceProtocolRequest()
    {
        if (_activeShip == null)
            return;

        AddTranscriptLine("> BOOTH 04: \"Send subspace protocols.\"", Palette.ClearanceTeal);

        if (IsDataSlateInSlateBus)
        {
            BeginSubspaceProtocolsDownload(isReTransmit: false);
        }
        else
        {
            _phase = EncounterPhase.AwaitingPortForDownload;
            _slateTelemetry = DataSlateTelemetryState.Empty;
            AudioHooks.Play("citation_buzz");
            AddTranscriptLine("[SLATE BUS] NO SLATE DETECTED -- PLUG PRIMARY DATA SLATE INTO SLATE BUS.", Palette.CrtAmberPrimary);
        }
    }

    private void BeginSubspaceProtocolsDownload(bool isReTransmit)
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.DownloadingProtocols;
        _slateLocation = DataSlateLocation.SlateBus;
        _slateTelemetry = DataSlateTelemetryState.Downloading;
        _protocolDownloadElapsed = 0f;
        _dataStreamAudioAccum = 0f;
        _pendingReTransmitRestartTimer = 0f;

        DraggableDeskItem slateItem = _deskItems[DeskItemId.DataSlate];
        slateItem.X = SlateBusDockedX;
        slateItem.Y = SlateBusDockedY;
        slateItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.DataSlate);

        AudioHooks.Play("data_stream");
        if (isReTransmit)
        {
            AddTranscriptLine("[SLATE BUS] RE-SYNCING SUBSPACE PROTOCOL PACKET...", Palette.ClearanceTeal);
        }

        RebuildHoloPortraitTexture();
    }

    private void AbortProtocolDownloadMidStream()
    {
        _phase = EncounterPhase.DownloadAborted;
        _slateLocation = DataSlateLocation.DeskFree;
        _slateTelemetry = DataSlateTelemetryState.DownloadAborted;
        _protocolDownloadElapsed = 0f;
        _pendingReTransmitRestartTimer = 0f;
        PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);

        AudioHooks.Play("citation_buzz");
        AddTranscriptLine("[PORT ERROR] DATA SLATE UNPLUGGED MID-DOWNLOAD -- TRANSMISSION ABORTED.", Palette.AlertCrimson);
        AddTranscriptLine("[SLATE BUS] RE-PLUG SLATE INTO SLATE BUS AND CLICK [RE-TRANSMIT].", Palette.CrtAmberPrimary);
    }

    private void TryTriggerReTransmit()
    {
        if (_activeShip == null || _phase != EncounterPhase.DownloadAborted)
            return;

        if (_pendingReTransmitRestartTimer > 0f)
            return;

        if (!IsDataSlateInSlateBus)
        {
            AudioHooks.Play("citation_buzz");
            AddTranscriptLine("[SLATE BUS] PLUG DATA SLATE INTO SLATE BUS FIRST BEFORE RE-TRANSMIT.", Palette.AlertCrimson);
            return;
        }

        AudioHooks.Play("terminal_key_confirm");
        AddTranscriptLine("> BOOTH 04: \"Link dropped. Re-transmit subspace protocols.\"", Palette.ClearanceTeal);
        AddTranscriptLine($"CAPT: \"{_activeShip.ReTransmitAnnoyedLine}\"", Palette.CrtAmberPrimary);
        _pendingReTransmitRestartTimer = ReTransmitAnnoyedDelaySeconds;
    }

    private void CompleteSubspaceProtocolsDownload()
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.InspectingSlateAndShip;
        _slateTelemetry = DataSlateTelemetryState.Loaded;
        AudioHooks.Play("subspace_transfer");

        AddTranscriptLine($"CAPT: \"{_activeShip.SubspaceUploadLine}\"", Palette.CrtAmberPrimary);

        // Day 1 stays clean and quiet; Day 2+ triggers standard route/cargo declaration
        if (_gameState.Day >= 2)
        {
            AddTranscriptLine("> BOOTH 04: \"Confirm route, purpose of visit, and cargo.\"", Palette.ClearanceTeal);
            AddTranscriptLine($"CAPT: \"{_activeShip.AutoInterrogationReplyLine}\"", Palette.CrtAmberPrimary);
        }
    }

    private void BeginClearanceUploadInDock()
    {
        if (_activeShip == null)
            return;

        AudioHooks.Play("clearance_upload");
        _protocolReviewModeActive = false;
        _slateLocation = DataSlateLocation.ClearanceDock;
        _slateTelemetry = DataSlateTelemetryState.UploadingClearance;
        _clearanceUploadElapsed = 0f;
        _pendingDecision = PlayerDecision.GrantAccess;
        _phase = EncounterPhase.UploadingClearance;

        // Crisp ~0.12s downward micro-settle bounce against top connector pins + inward mechanical clamp latches
        _dockSettleActive = true;
        _dockSettleElapsed = 0f;
        _dockReleaseKickActive = false;
        _dockReleaseKickElapsed = 0f;
        _dockLatchProgress = 0f;
        _dockLatchTarget = 1f;
        _slateBusSettleActive = false;
        _slateBusSettleElapsed = 0f;

        // Snap the actual 184x216 Data Slate visibly into the Clearance Dock bay (1080, 384, 184, 216)
        DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
        slate.X = ClearanceSlotX;
        slate.Y = ClearanceSlotY;
        slate.IsVisible = true;
        BringDeskItemToFront(DeskItemId.DataSlate);

        AddTranscriptLine("> BOOTH 04: [ENCRYPTING CLEARANCE IN DOCK...]", Palette.ClearanceTeal);
    }

    private void AbortClearanceUploadMidEncryption()
    {
        _clearanceUploadElapsed = 0f;
        _phase = EncounterPhase.InspectingSlateAndShip;
        _slateTelemetry = DataSlateTelemetryState.Loaded;
        _slateLocation = DataSlateLocation.DeskFree;
        // Immediately snap dock latches open and cancel any settle/release micro-animations
        _dockLatchProgress = 0f;
        _dockLatchTarget = 0f;
        _dockSettleActive = false;
        _dockSettleElapsed = 0f;
        _dockReleaseKickActive = false;
        _dockReleaseKickElapsed = 0f;
        AudioHooks.Play("terminal_key_click");
        AddTranscriptLine("[CLEARANCE DOCK] ENCRYPTION ABORTED -- SLATE UNPLUGGED (0%).", Palette.CrtAmberPrimary);
    }

    private void BeginClearanceTransmissionToShip()
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.TransmittingClearance;
        _slateLocation = DataSlateLocation.SlateBus;
        _slateTelemetry = DataSlateTelemetryState.TransmittingClearance;
        _clearanceTransmitElapsed = 0f;
        _dataStreamAudioAccum = 0f;

        // Subtle matching 2px seat tick when snapping cleared Data Slate back into Slate Bus
        _slateBusSettleActive = true;
        _slateBusSettleElapsed = 0f;
        _dockSettleActive = false;
        _dockSettleElapsed = 0f;
        _dockReleaseKickActive = false;
        _dockReleaseKickElapsed = 0f;

        AudioHooks.Play("data_stream");
        AddTranscriptLine("[SLATE BUS] TRANSMITTING ENCRYPTED CLEARANCE TO VESSEL...", Palette.ClearanceTeal);
    }

    private void AbortClearanceTransmissionMidStream()
    {
        _clearanceTransmitElapsed = 0f;
        _dataStreamAudioAccum = 0f;
        _phase = EncounterPhase.AwaitingClearanceReturnToTopPort;
        _slateTelemetry = DataSlateTelemetryState.ClearedReadyToTransmit;
        _slateLocation = DataSlateLocation.DeskFree;
        _slateBusSettleActive = false;
        _slateBusSettleElapsed = 0f;
        AudioHooks.Play("terminal_key_click");
        AddTranscriptLine("[SLATE BUS] TX CLEARANCE ABORTED -- CLEARED SLATE UNPLUGGED (0%).", Palette.CrtAmberPrimary);
    }

    private bool TryHandleHardwarePanelClicks(VirtualMouseState mouse)
    {
        if (_preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive)
            return false;

        // 0. RE-TRANSMIT Click on Slate Bus (688, 264, 216, 44) or Next Hail slot when DownloadAborted
        if (_phase == EncounterPhase.DownloadAborted &&
            (mouse.IsHit(SlateBusX, SlateBusY, SlateBusW, SlateBusH) ||
             mouse.IsHit(NextHailBtnX, NextHailBtnY, NextHailBtnW, NextHailBtnH)))
        {
            TryTriggerReTransmit();
            return true;
        }

        // 1. Physical Left-Side DETAIN LEVER: (8, 40, 64, 216)
        if (mouse.IsHit(DetainLeverX, DetainLeverY, DetainLeverW, DetainLeverH))
        {
            bool canDetain = _activeShip != null &&
                _phase is EncounterPhase.OpeningGreetingDelay
                    or EncounterPhase.AwaitingPortForDownload
                    or EncounterPhase.DownloadingProtocols
                    or EncounterPhase.DownloadAborted
                    or EncounterPhase.InspectingSlateAndShip
                    or EncounterPhase.AwaitingClearanceReturnToTopPort;

            if (canDetain && _activeShip != null)
            {
                TriggerDetainLeverClampSequence();
            }
            else
            {
                AudioHooks.Play("citation_buzz");
            }
            return true;
        }

        // 2. NEXT HAIL button: (918, 270, 112, 36)
        if (mouse.IsHit(NextHailBtnX, NextHailBtnY, NextHailBtnW, NextHailBtnH))
        {
            if (_shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty)
            {
                _shiftController.NotifyFirstHailTriggered();
                DebugSpawnValidityMode mode = (DebugSpawnValidityMode)_devDebug.ForceSpawnValidityMode;
                StartNextShipEncounter(mode, reseedNonce: 0);
            }
            else
            {
                AudioHooks.Play("citation_buzz");
            }
            return true;
        }

        bool canIssueComplianceOrder = AreCombatOrdersUnlocked &&
            _activeShip != null &&
            _phase is EncounterPhase.OpeningGreetingDelay
                or EncounterPhase.AwaitingPortForDownload
                or EncounterPhase.DownloadingProtocols
                or EncounterPhase.DownloadAborted
                or EncounterPhase.InspectingSlateAndShip
                or EncounterPhase.AwaitingClearanceReturnToTopPort;

        // 3. LOWER SHIELDS (Day 2+ only): (1032, 264, 116, 44)
        if (AreCombatOrdersUnlocked && mouse.IsHit(LowerShieldsBtnX, LowerShieldsBtnY, LowerShieldsBtnW, LowerShieldsBtnH))
        {
            if (canIssueComplianceOrder && !_pendingShieldsOrder && _activeShip != null)
            {
                AudioHooks.Play("terminal_key_click");
                AddTranscriptLine("> BOOTH 04: \"Lower shields to standby.\"", Palette.ClearanceTeal);
                _pendingShieldsOrder = true;
                _complianceTimer = _activeShip.ComplianceDelaySeconds;
            }
            return true;
        }

        // 4. PWR DOWN WPN (Day 2+ only): (1156, 264, 116, 44)
        if (AreCombatOrdersUnlocked && mouse.IsHit(PwrDownWpnBtnX, PwrDownWpnBtnY, PwrDownWpnBtnW, PwrDownWpnBtnH))
        {
            if (canIssueComplianceOrder && !_pendingWeaponsOrder && _activeShip != null)
            {
                AudioHooks.Play("terminal_key_click");
                AddTranscriptLine("> BOOTH 04: \"Power down forward weapon mounts.\"", Palette.ClearanceTeal);
                _pendingWeaponsOrder = true;
                _complianceTimer = _activeShip.ComplianceDelaySeconds;
            }
            return true;
        }

        // 5. PROTOCOL REVIEW Toggle (Day 3+ only): (908, 316, 156, 36)
        if (IsProtocolReviewUnlocked && mouse.IsHit(ProtocolReviewBtnX, ProtocolReviewBtnY, ProtocolReviewBtnW, ProtocolReviewBtnH))
        {
            if (_activeShip != null && IsShipInspectableForReview && _faultStage == FaultSequenceStage.None)
            {
                _protocolReviewModeActive = !_protocolReviewModeActive;
                _reviewFirstSelection = ReviewTarget.None;
                _reviewStatusNote = _protocolReviewModeActive
                    ? "CLICK CLAIM vs REALITY."
                    : string.Empty;
                AudioHooks.Play("terminal_key_tab");
            }
            return true;
        }

        // 6. SPS TOGGLE Button (Day 2+ only): (568, 40, 104, 60)
        if (IsSpsUnlocked && mouse.IsHit(SpsToggleX, SpsToggleY, SpsToggleW, SpsToggleH))
        {
            _spsEnabled = !_spsEnabled;
            if (!_spsEnabled)
            {
                _spsLockTarget = SpsAnomalyKind.None;
                _spsLockProgress = 0f;
            }
            AudioHooks.Play(_spsEnabled ? "sps_scanner_on" : "sps_scanner_off");
            return true;
        }

        // 7. Grab and drag the visible docked 184x216 Data Slate directly out of Clearance Upload Dock (1072, 360, 200, 272)
        //    Works BOTH mid-encryption (1%-99%, cleanly aborting/resetting to 0% once pulled >= 8px) AND after 100% cleared!
        if (IsDataSlateInClearanceDock &&
            _phase != EncounterPhase.DetainClamping &&
            mouse.IsHit(ClearanceDockX, ClearanceDockY, ClearanceDockW, ClearanceDockH))
        {
            // When Protocol Review is active on a 100%-cleared docked slate, let Protocol Review handle clicks on slate fields
            if (_protocolReviewModeActive && _phase == EncounterPhase.AwaitingClearanceReturnToTopPort)
            {
                return false;
            }

            DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
            if (_phase != EncounterPhase.UploadingClearance)
            {
                _slateLocation = DataSlateLocation.DeskFree;
                _dockSettleActive = false;
                _dockSettleElapsed = 0f;
                _dockReleaseKickActive = false;
                _dockReleaseKickElapsed = 0f;
            }
            BringDeskItemToFront(DeskItemId.DataSlate);

            _draggingItemId = DeskItemId.DataSlate;
            _dragOffsetX = Math.Clamp(mouse.X - slate.X, 12, slate.Width - 12);
            _dragOffsetY = Math.Clamp(mouse.Y - slate.Y, 12, slate.Height - 12);
            _dragStartMouseX = mouse.X;
            _dragStartMouseY = mouse.Y;
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        return false;
    }

    private void TriggerDetainLeverClampSequence()
    {
        if (_activeShip == null)
            return;

        ResetFaultSequenceState();
        _protocolReviewModeActive = false;
        _detainLeverPulled = true;
        _detainClampElapsed = 0f;
        _detainClampImpactPlayed = false;
        _detainSlateWipeCompleted = false;
        _pendingReTransmitRestartTimer = 0f;

        AudioHooks.Play("detain_lever_pull");

        string tag = IsProtocolReviewUnlocked
            ? (_activeShip.ProtocolReviewVerified ? $"VERIFIED: {_verifiedViolationSummary}" : "UNVERIFIED CLAMP")
            : "STATION CLAMPS ENGAGED";
        AddTranscriptLine($"> BOOTH 04: [DETAIN LEVER PULLED -- {tag}]", Palette.AlertCrimson);

        _phase = EncounterPhase.DetainClamping;
    }

    private void StartNextShipEncounter(DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto, uint reseedNonce = 0)
    {
        UnloadEncounterTextures();
        ResetProtocolReviewState();
        ResetFaultSequenceState();
        ResetWarpPrinterState();
        ResetSpsAnomalyState();
        ResetSlateDockMicroAnimations();

        _activeShip = _shiftController.TakeNextShip(_gameState, _shipGenerator, forceValidity, reseedNonce);

        if (_activeShip.GroundTruth.IsDay2GhostRouteSuspect)
        {
            _gameState.Day2SuspectEncountered = true;
            _gameState.Day2SuspectRegistration = _activeShip.Slate.RegistrationCode;
            _gameState.Day2SuspectDeclaredOrigin = _activeShip.Slate.OriginSystem;
            _gameState.Day2SuspectActualOrigin = _activeShip.GroundTruth.ActualOriginSystem;
            _gameState.Day2SuspectPrefix = _activeShip.Slate.DeclaredPrefix;
            _gameState.Day2SuspectShipName = _activeShip.Slate.ShipName;
            _gameState.QuestFlags["Day2_GhostRouteEncountered"] = true;
            _gameState.SaveToDisk();
        }

        _shiftController.HasActiveShipInBay = true;
        _phase = EncounterPhase.ShipGlidingIn;
        _shipApproachElapsed = 0f;
        _shipSlideOffset = 380f;
        _subspaceAutoTransferDelay = 0f;
        _clearanceUploadElapsed = 0f;
        _clearanceTransmitElapsed = 0f;
        _pendingReTransmitRestartTimer = 0f;
        _pendingShieldsOrder = false;
        _pendingWeaponsOrder = false;
        _captainRefusedCompliance = false;
        _detainLeverPulled = false;
        _detainClampElapsed = 0f;
        _detainClampImpactPlayed = false;
        _detainSlateWipeCompleted = false;

        _slateTelemetry = DataSlateTelemetryState.Empty;
        _deskItems[DeskItemId.DataSlate].IsVisible = true;

        AudioHooks.Play("terminal_key_confirm");
        _transcriptLines.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;
        AddTranscriptLine("APPROACH VECTOR LOCKED -- VESSEL GLIDING INTO BAY...", Palette.CrtAmberDimmed);

        _lastShipAnimBucket = -1;
        _lastPortraitAnimBucket = -1;
        RebuildActiveShipTextures();
    }

    private void FinalizeShipResolution()
    {
        if (_activeShip == null)
            return;

        DecisionEvaluationResult result = _validationEngine.EvaluateDecision(
            _gameState,
            _activeShip,
            _pendingDecision,
            _worldSim);

        if (result.IsCitation)
        {
            _activeCitationStrip = result;
            _citationSlideProgress = 0.0f;
            _citationDisplayTimer = 8.5f;
            AddTranscriptLine($"[CITATION] {result.ViolationReason}", Palette.AlertCrimson);
            AddTranscriptLine($"[PENALTY] {result.PenaltyText}", Palette.CrtAmberPrimary);
        }
        else
        {
            string quietLog = _pendingDecision == PlayerDecision.GrantAccess
                ? $"[DOCKED] Vessel {result.RegistrationCode} cleared (+{result.ScripDelta} SC)."
                : $"[DETAINED] Vessel {result.RegistrationCode} detained (+{result.ScripDelta} SC).";
            AddTranscriptLine(quietLog, Palette.CrtAmberDimmed);
        }

        UnloadEncounterTextures();
        ResetProtocolReviewState();
        ResetFaultSequenceState();
        ResetWarpPrinterState();
        ResetSpsAnomalyState();
        ResetSlateDockMicroAnimations();

        _slateTelemetry = DataSlateTelemetryState.Empty;
        _detainLeverPulled = false;
        _detainClampElapsed = 0f;
        _detainClampImpactPlayed = false;
        _detainSlateWipeCompleted = false;
        _pendingReTransmitRestartTimer = 0f;
        _activeShip = null;
        _shiftController.HasActiveShipInBay = false;
        _phase = EncounterPhase.BayEmpty;

        if (_shiftController.IsShiftTimerExpired)
        {
            AddTranscriptLine("BROWN DWARF BELOW HORIZON -- WATCH COMPLETE. PROCEED TO QUARTERS.", Palette.CrtAmberPrimary);
        }
    }

    // =========================================================================
    // Thematic CRT Dialogue Transcript Scrolling (w = 16 amber pixel scrollbar)
    // =========================================================================
    private void HandleTranscriptScrollInput(VirtualMouseState mouse)
    {
        if (MathF.Abs(mouse.WheelMove) < 0.01f)
            return;

        if (!mouse.IsHit(TranscriptX, TranscriptY, TranscriptW, TranscriptH))
            return;

        List<(string Line, Color Col)> wrapped = WrapTranscriptLinesByPixelWidth(TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);
        if (maxTop == 0)
            return;

        int delta = mouse.WheelMove > 0 ? -2 : 2;
        _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine + delta, 0, maxTop);
        _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
    }

    private bool TryHandleTranscriptScrollButtonClicks(VirtualMouseState mouse)
    {
        int barX = TranscriptX + TranscriptW - ScrollbarW - 2;
        int upBtnY = TranscriptY + 18;
        int downBtnY = TranscriptY + TranscriptH - 18;

        List<(string Line, Color Col)> wrapped = WrapTranscriptLinesByPixelWidth(TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);

        if (mouse.IsHit(barX, upBtnY, ScrollbarW, 16))
        {
            _transcriptScrollTopLine = Math.Max(0, _transcriptScrollTopLine - 2);
            _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
            AudioHooks.Play("terminal_key_click");
            return true;
        }
        if (mouse.IsHit(barX, downBtnY, ScrollbarW, 16))
        {
            _transcriptScrollTopLine = Math.Min(maxTop, _transcriptScrollTopLine + 2);
            _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
            AudioHooks.Play("terminal_key_click");
            return true;
        }
        return false;
    }

    // =========================================================================
    // Z-Ordered Draggable Desk Documents, Slate Bus & Clearance Dock Snapping
    // =========================================================================
    private bool TryHandleDeskItemsMousePress(VirtualMouseState mouse)
    {
        for (int i = _zOrder.Count - 1; i >= 0; i--)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible)
                continue;

            // When Data Slate is seated inside the Clearance Upload Dock, it is picked up via the dock click handler
            if (id == DeskItemId.DataSlate && IsDataSlateInClearanceDock)
                continue;

            if (!mouse.IsHit(item.X, item.Y, item.Width, item.Height))
                continue;

            BringDeskItemToFront(id);

            if (id == DeskItemId.ReferenceManual && TryHandleReferenceManualInternalClick(mouse, item))
                return true;

            if (id == DeskItemId.DataSlate && _phase == EncounterPhase.DetainClamping)
            {
                AudioHooks.Play("citation_buzz");
                return true;
            }

            _draggingItemId = id;
            _dragOffsetX = mouse.X - item.X;
            _dragOffsetY = mouse.Y - item.Y;
            _dragStartMouseX = mouse.X;
            _dragStartMouseY = mouse.Y;
            AudioHooks.Play("terminal_key_click");

            if (id == DeskItemId.DataSlate &&
                _phase is not (EncounterPhase.DownloadingProtocols or EncounterPhase.TransmittingClearance))
            {
                if (_slateLocation == DataSlateLocation.SlateBus)
                {
                    _slateLocation = DataSlateLocation.DeskFree;
                }
            }
            return true;
        }

        return false;
    }

    private bool TryHandleReferenceManualInternalClick(VirtualMouseState mouse, DraggableDeskItem manual)
    {
        int mx = manual.X;
        int my = manual.Y;

        // Only 2 tabs: 1. PREFIXES & 2. HULLS (PING RELAY & 3. STARS completely removed!)
        if (mouse.IsHit(mx + 6, my + 20, 132, 16))
        {
            _manualActiveTab = 0;
            AudioHooks.Play("terminal_key_tab");
            return true;
        }
        if (mouse.IsHit(mx + 142, my + 20, 132, 16))
        {
            _manualActiveTab = 1;
            AudioHooks.Play("terminal_key_tab");
            RebuildManualSilhouetteTexture();
            return true;
        }

        if (_manualActiveTab == 0)
        {
            int rowStartY = my + 39;
            if (mouse.X >= mx + 6 && mouse.X <= mx + manual.Width - 6 &&
                mouse.Y >= rowStartY && mouse.Y < rowStartY + ManualPrefixDirectory.Length * 12)
            {
                int clickedIdx = (mouse.Y - rowStartY) / 12;
                if (clickedIdx >= 0 && clickedIdx < ManualPrefixDirectory.Length)
                {
                    ShipFaction targetFaction = ManualPrefixDirectory[clickedIdx].VisualFaction;
                    for (int f = 0; f < ShipRenderer.AllFactions.Count; f++)
                    {
                        if (ShipRenderer.AllFactions[f] == targetFaction)
                        {
                            _manualFactionIndex = f;
                            break;
                        }
                    }
                    _manualClassIndex = 0;
                    _manualActiveTab = 1;
                    AudioHooks.Play("terminal_key_click");
                    RebuildManualSilhouetteTexture();
                    return true;
                }
            }
        }
        else if (_manualActiveTab == 1)
        {
            int factionCount = ShipRenderer.AllFactions.Count;
            if (mouse.IsHit(mx + 6, my + 40, 76, 16))
            {
                _manualFactionIndex = (_manualFactionIndex - 1 + factionCount) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (_devDebug.DevManualSyncEnabled && mouse.IsHit(mx + 102, my + 40, 76, 16))
            {
                if (_activeShip != null)
                {
                    ShipFaction targetFaction = _activeShip.Slate.DeclaredVisualFaction;
                    for (int f = 0; f < factionCount; f++)
                    {
                        if (ShipRenderer.AllFactions[f] == targetFaction)
                        {
                            _manualFactionIndex = f;
                            break;
                        }
                    }
                    IReadOnlyList<ShipClass> classes = ShipRenderer.GetAllowedClasses(ShipRenderer.AllFactions[_manualFactionIndex]);
                    _manualClassIndex = 0;
                    for (int c = 0; c < classes.Count; c++)
                    {
                        if (classes[c] == _activeShip.Slate.DeclaredShipClass)
                        {
                            _manualClassIndex = c;
                            break;
                        }
                    }
                    AudioHooks.Play("terminal_key_confirm");
                    RebuildManualSilhouetteTexture();
                }
                return true;
            }
            if (mouse.IsHit(mx + 198, my + 40, 76, 16))
            {
                _manualFactionIndex = (_manualFactionIndex + 1) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }

            ShipFaction currentFaction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowed = ShipRenderer.GetAllowedClasses(currentFaction);
            if (mouse.IsHit(mx + 6, my + 144, 62, 16))
            {
                _manualClassIndex = (_manualClassIndex - 1 + allowed.Count) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (mouse.IsHit(mx + 212, my + 144, 62, 16))
            {
                _manualClassIndex = (_manualClassIndex + 1) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
        }

        return false;
    }

    private bool IsDraggingDataSlateOverClearanceDock(VirtualMouseState mouse, DraggableDeskItem slate)
    {
        if (mouse.IsHit(ClearanceDockX, ClearanceDockY, ClearanceDockW, ClearanceDockH))
            return true;

        return slate.X + slate.Width >= ClearanceDockX - 18 &&
               slate.Y + slate.Height >= ClearanceDockY &&
               slate.Y <= ClearanceDockY + ClearanceDockH;
    }

    private bool IsDraggingDataSlateOverSlateBus(VirtualMouseState mouse, DraggableDeskItem slate)
    {
        if (mouse.IsHit(SlateBusX - 12, SlateBusY - 8, SlateBusW + 24, SlateBusH + 36))
            return true;

        return slate.X < SlateBusX + SlateBusW + 16 &&
               slate.X + slate.Width > SlateBusX - 16 &&
               slate.Y <= SlateBusY + SlateBusH + 24;
    }

    private bool IsStripOverGlowingWarpChecker(VirtualMouseState mouse, DraggableDeskItem strip)
    {
        if (mouse.IsHit(WarpRefSpotX - 8, WarpRefSpotY - 4, WarpRefSpotW + 16, WarpRefSpotH + 16))
            return true;

        int stripWaveX = strip.X + StripWaveOffsetX;
        int stripWaveY = strip.Y + StripWaveOffsetY;
        int ovLeft = Math.Max(stripWaveX, WarpGlassX);
        int ovRight = Math.Min(stripWaveX + WarpGlassW, WarpGlassX + WarpGlassW);
        int ovTop = Math.Max(stripWaveY, WarpGlassY);
        int ovBottom = Math.Min(stripWaveY + WarpGlassH, WarpGlassY + WarpGlassH);
        if (ovRight <= ovLeft || ovBottom <= ovTop)
            return false;

        int overlapArea = (ovRight - ovLeft) * (ovBottom - ovTop);
        return overlapArea >= (WarpGlassW * WarpGlassH) / 3;
    }

    private void UpdateActiveDrag(VirtualMouseState mouse)
    {
        if (!_draggingItemId.HasValue)
            return;

        DraggableDeskItem item = _deskItems[_draggingItemId.Value];

        if (item.Id == DeskItemId.DataSlate &&
            _phase is EncounterPhase.ResolvingDecision or EncounterPhase.DetainClamping)
        {
            _draggingItemId = null;
            return;
        }

        if (mouse.LeftDown)
        {
            if (item.Id == DeskItemId.DataSlate &&
                _phase == EncounterPhase.DownloadingProtocols &&
                IsDataSlateInSlateBus)
            {
                int pullDx = mouse.X - _dragStartMouseX;
                int pullDy = mouse.Y - _dragStartMouseY;
                float pullDist = MathF.Sqrt(pullDx * pullDx + pullDy * pullDy);
                if (pullDist < UnplugMidDownloadDragThresholdPx)
                    return;

                AbortProtocolDownloadMidStream();
            }
            else if (item.Id == DeskItemId.DataSlate &&
                     _phase == EncounterPhase.UploadingClearance &&
                     IsDataSlateInClearanceDock)
            {
                int pullDx = mouse.X - _dragStartMouseX;
                int pullDy = mouse.Y - _dragStartMouseY;
                float pullDist = MathF.Sqrt(pullDx * pullDx + pullDy * pullDy);
                if (pullDist < ShiftController.UnplugMidTransferDragThresholdPx)
                    return;

                if (ShiftController.CanInterruptTransfer(_clearanceUploadElapsed, ClearanceUploadDurationSeconds))
                {
                    AbortClearanceUploadMidEncryption();
                }
            }
            else if (item.Id == DeskItemId.DataSlate &&
                     _phase == EncounterPhase.AwaitingClearanceReturnToTopPort &&
                     IsDataSlateInClearanceDock)
            {
                // Encryption hit 100% while the player held the slate -- detach cleanly into hand
                _slateLocation = DataSlateLocation.DeskFree;
                _dockSettleActive = false;
                _dockSettleElapsed = 0f;
                _dockReleaseKickActive = false;
                _dockReleaseKickElapsed = 0f;
            }
            else if (item.Id == DeskItemId.DataSlate &&
                     _phase == EncounterPhase.TransmittingClearance &&
                     IsDataSlateInSlateBus)
            {
                int pullDx = mouse.X - _dragStartMouseX;
                int pullDy = mouse.Y - _dragStartMouseY;
                float pullDist = MathF.Sqrt(pullDx * pullDx + pullDy * pullDy);
                if (pullDist < ShiftController.UnplugMidTransferDragThresholdPx)
                    return;

                if (ShiftController.CanInterruptTransfer(_clearanceTransmitElapsed, ClearanceTransmitDurationSeconds))
                {
                    AbortClearanceTransmissionMidStream();
                }
            }

            // Allow Data Slate to reach directly over Clearance Dock, and allow WarpStrip to move up over
            // the Glowing Warp Checker glass (y >= 264) while keeping resting desk bounds crisp.
            int minDragY = item.Id == DeskItemId.WarpStrip ? WarpRefSpotY : DragBoundsY;
            int maxDragX = item.Id == DeskItemId.DataSlate
                ? (ClearanceDockX + ClearanceDockW - item.Width)
                : (DragBoundsX + DragBoundsW - item.Width);
            int maxDragY = DragBoundsY + DragBoundsH - item.Height;

            item.X = Math.Clamp(mouse.X - _dragOffsetX, DragBoundsX, maxDragX);
            item.Y = Math.Clamp(mouse.Y - _dragOffsetY, minDragY, maxDragY);
            return;
        }

        if (mouse.LeftReleased || !mouse.LeftDown)
        {
            if (item.Id == DeskItemId.DataSlate && _preShiftStage == PreShiftStage.None)
            {
                if (_phase == EncounterPhase.DownloadingProtocols && IsDataSlateInSlateBus)
                {
                    item.X = SlateBusDockedX;
                    item.Y = SlateBusDockedY;
                    _draggingItemId = null;
                    return;
                }

                if (_phase == EncounterPhase.UploadingClearance && IsDataSlateInClearanceDock)
                {
                    item.X = ClearanceSlotX;
                    item.Y = ClearanceSlotY;
                    _draggingItemId = null;
                    return;
                }

                if (_phase == EncounterPhase.TransmittingClearance && IsDataSlateInSlateBus)
                {
                    item.X = SlateBusDockedX;
                    item.Y = SlateBusDockedY;
                    _draggingItemId = null;
                    return;
                }

                bool overSlateBus = IsDraggingDataSlateOverSlateBus(mouse, item);
                bool canSlotInClearanceDock = _phase == EncounterPhase.InspectingSlateAndShip &&
                                              _slateTelemetry == DataSlateTelemetryState.Loaded;
                bool canReSeatClearedSlate = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort &&
                                             _slateTelemetry == DataSlateTelemetryState.ClearedReadyToTransmit;
                bool overClearanceDock = IsDraggingDataSlateOverClearanceDock(mouse, item);

                if (overSlateBus)
                {
                    item.X = SlateBusDockedX;
                    item.Y = SlateBusDockedY;
                    _slateLocation = DataSlateLocation.SlateBus;
                    AudioHooks.Play("dock_lock");

                    if (_phase == EncounterPhase.AwaitingPortForDownload)
                    {
                        BeginSubspaceProtocolsDownload(isReTransmit: false);
                    }
                    else if (_phase == EncounterPhase.AwaitingClearanceReturnToTopPort &&
                             _slateTelemetry == DataSlateTelemetryState.ClearedReadyToTransmit)
                    {
                        BeginClearanceTransmissionToShip();
                    }
                }
                else if (overClearanceDock && canSlotInClearanceDock)
                {
                    // Snap visibly into Clearance Upload Dock bay (1080, 384, 184, 216) and start encryption!
                    AudioHooks.Play("dock_lock");
                    BeginClearanceUploadInDock();
                }
                else if (overClearanceDock && canReSeatClearedSlate)
                {
                    // Re-seat already cleared slate cleanly in the dock bay without re-running encryption
                    item.X = ClearanceSlotX;
                    item.Y = ClearanceSlotY;
                    _slateLocation = DataSlateLocation.ClearanceDock;
                    _dockSettleActive = true;
                    _dockSettleElapsed = 0f;
                    _dockReleaseKickActive = false;
                    _dockReleaseKickElapsed = 0f;
                    _dockLatchTarget = 0f;
                    AudioHooks.Play("dock_lock");
                }
                else
                {
                    if (overClearanceDock && !canSlotInClearanceDock && !canReSeatClearedSlate)
                    {
                        AudioHooks.Play("citation_buzz");
                    }
                    item.X = Math.Clamp(item.X, DragBoundsX, DragBoundsX + DragBoundsW - item.Width);
                    item.Y = Math.Clamp(item.Y, DragBoundsY, DragBoundsY + DragBoundsH - item.Height);
                    _slateLocation = DataSlateLocation.DeskFree;
                }
            }
            else if (item.Id == DeskItemId.WarpStrip && IsSpectrometerOperational && IsStripOverGlowingWarpChecker(mouse, item))
            {
                // Snap the 216x52 magnetic warp film cleanly 1:1 over the Glowing Warp Checker glass (448, 294, 216, 52)
                item.X = StripSnapX;
                item.Y = StripSnapY;
                AudioHooks.Play("dock_lock");
            }
            else
            {
                int minRestY = item.Id == DeskItemId.WarpStrip ? WarpRefSpotY : DragBoundsY;
                item.X = Math.Clamp(item.X, DragBoundsX, DragBoundsX + DragBoundsW - item.Width);
                item.Y = Math.Clamp(item.Y, minRestY, DragBoundsY + DragBoundsH - item.Height);
            }

            _draggingItemId = null;
        }
    }

    private void BringDeskItemToFront(DeskItemId id)
    {
        _zOrder.Remove(id);
        _zOrder.Add(id);
    }

    // =========================================================================
    // SPS Sway & Lens Positioning (Day 2+)
    // =========================================================================
    private Vector2 ComputeSpsSwayOffset(float t)
    {
        float ampX;
        float ampY;
        float speed;

        if (_gameState.IsHypoxic ||
            _gameState.CurrentHungerTier == HungerTier.LowNoFood ||
            _gameState.DaysWithoutFood >= 2)
        {
            ampX = 36.0f;
            ampY = 26.0f;
            speed = 2.40f;
        }
        else if (_gameState.CurrentHungerTier == HungerTier.HighTierRations)
        {
            return Vector2.Zero;
        }
        else
        {
            ampX = 16.0f;
            ampY = 12.0f;
            speed = 1.45f;
        }

        float nx = SmoothValueNoise1D(t * speed * 0.92f, 17.3f) * 0.62f +
                   MathF.Sin(t * speed * 1.71f) * 0.38f;
        float ny = SmoothValueNoise1D(t * speed * 0.87f, 91.7f) * 0.62f +
                   MathF.Cos(t * speed * 1.39f + 0.8f) * 0.38f;

        return new Vector2(nx * ampX, ny * ampY);
    }

    private void UpdateSpsLensRect(VirtualMouseState mouse)
    {
        bool mouseInViewport = mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH);
        float targetX = mouseInViewport ? mouse.X : (ShipAnchorX + ShipRenderW * 0.5f);
        float targetY = mouseInViewport ? mouse.Y : (ShipAnchorY + ShipRenderH * 0.5f);

        int halfW = SpsLensW / 2;
        int halfH = SpsLensH / 2;
        int lensCx = Math.Clamp((int)MathF.Round(targetX + _currentSpsSwayOffset.X), ViewportInnerX + halfW + 2, ViewportInnerX + ViewportInnerW - halfW - 2);
        int lensCy = Math.Clamp((int)MathF.Round(targetY + _currentSpsSwayOffset.Y), ViewportInnerY + halfH + 2, ViewportInnerY + ViewportInnerH - halfH - 2);

        _lastSpsLensRect = new Rectangle(lensCx - halfW, lensCy - halfH, SpsLensW, SpsLensH);
    }

    private static float SmoothValueNoise1D(float x, float seedOffset)
    {
        float p = x + seedOffset;
        int i0 = (int)MathF.Floor(p);
        int i1 = i0 + 1;
        float frac = p - i0;
        float u = frac * frac * frac * (frac * (frac * 6f - 15f) + 10f);

        float v0 = HashIntToSignedFloat(i0);
        float v1 = HashIntToSignedFloat(i1);
        return v0 + u * (v1 - v0);
    }

    private static float HashIntToSignedFloat(int n)
    {
        uint h = (uint)n * 0x9E3779B1u;
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        return ((h & 0xFFFFu) / 32767.5f) - 1.0f;
    }

    // =========================================================================
    // Procedural Texture Cache Management (Strict VRAM Discipline)
    // =========================================================================
    private void UpdateAnimatedEncounterTextures()
    {
        if (_activeShip == null)
            return;

        int shipBucket = _animTick / 9;
        if (shipBucket != _lastShipAnimBucket)
        {
            RebuildActiveShipTextures();
        }

        if (_phase != EncounterPhase.ShipGlidingIn &&
            _phase != EncounterPhase.BayEmpty &&
            _activeShip.GroundTruth.UsesStaticViewscreen &&
            _cachedLivePortraitTex.Id != 0)
        {
            int portraitBucket = _animTick / 6;
            if (portraitBucket != _lastPortraitAnimBucket)
            {
                RebuildLivePortraitTexture();
            }
        }
    }

    private void RebuildActiveShipTextures()
    {
        PixelBuffer.UnloadTexture(ref _cachedShipLiveTex);
        PixelBuffer.UnloadTexture(ref _cachedShipSpsTex);
        if (_activeShip == null)
        {
            _activeShipHardpoints = null;
            return;
        }

        _lastShipAnimBucket = _animTick / 9;

        PixelBuffer liveBuf = ShipRenderer.RenderBuffer(
            _activeShip.BuildLiveShipRenderOptions(spsEnabled: false, _animTick),
            out ShipHardpointLayout hardpoints);
        _activeShipHardpoints = hardpoints;
        _cachedShipLiveTex = liveBuf.ToTexture2D();

        PixelBuffer spsBuf = ShipRenderer.RenderBuffer(_activeShip.BuildLiveShipRenderOptions(spsEnabled: true, _animTick));
        _cachedShipSpsTex = spsBuf.ToTexture2D();
    }

    private void RebuildLivePortraitTexture()
    {
        PixelBuffer.UnloadTexture(ref _cachedLivePortraitTex);
        if (_activeShip == null)
            return;

        _lastPortraitAnimBucket = _animTick / 6;
        PixelBuffer liveBuf = PortraitRenderer.RenderBuffer(_activeShip.BuildLivePortraitRenderOptions(_animTick));
        _cachedLivePortraitTex = liveBuf.ToTexture2D();
    }

    private void RebuildHoloPortraitTexture()
    {
        PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
        if (_activeShip == null)
            return;

        PixelBuffer holoBuf = PortraitRenderer.RenderBuffer(_activeShip.Slate.BuildHoloPortraitOptions());
        _cachedHoloPortraitTex = holoBuf.ToTexture2D();
    }

    private void RebuildManualSilhouetteTexture()
    {
        PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
        ShipFaction faction = ShipRenderer.AllFactions[_manualFactionIndex];
        IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(faction);
        ShipClass selectedClass = allowedClasses[Math.Clamp(_manualClassIndex, 0, allowedClasses.Count - 1)];

        PixelBuffer manualBuf = ShipRenderer.RenderBuffer(new ShipRenderOptions
        {
            SeedId = $"manual-chart-{faction}-{selectedClass}",
            Faction = faction,
            RequestedClass = selectedClass,
            Mode = ShipRenderMode.Manual
        });
        _cachedManualShipTex = manualBuf.ToTexture2D();
    }

    private void UnloadEncounterTextures()
    {
        PixelBuffer.UnloadTexture(ref _cachedShipLiveTex);
        PixelBuffer.UnloadTexture(ref _cachedShipSpsTex);
        PixelBuffer.UnloadTexture(ref _cachedLivePortraitTex);
        PixelBuffer.UnloadTexture(ref _cachedHoloPortraitTex);
    }

    private void UnloadCachedTextures()
    {
        UnloadEncounterTextures();
        PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
    }

    public void Unload()
    {
        UnloadCachedTextures();
    }

    // Smoke-test deterministic visual staging helpers (never invoked in normal gameplay)
    internal void SmokeStageShipLoadedAndInspecting(bool enableSps = false, bool triggerDetainClamps = false, bool triggerFaultBeat = false)
    {
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;
        _shiftController.NotifyFirstHailTriggered();
        StartNextShipEncounter(DebugSpawnValidityMode.ForceInvalid, reseedNonce: 1u);
        _shipApproachElapsed = ShipApproachDurationSeconds;
        _shipSlideOffset = 0f;
        RebuildLivePortraitTexture();
        BeginSubspaceProtocolsDownload(isReTransmit: false);
        _protocolDownloadElapsed = ProtocolDownloadDurationSeconds;
        CompleteSubspaceProtocolsDownload();
        _spsEnabled = IsSpsUnlocked && enableSps;
        _spsProbeExtend = _spsEnabled ? 1.0f : 0.0f;
        if (_activeShip != null && _spsEnabled)
        {
            _activeShip.GroundTruth.ActualShieldsUp = false;
            RebuildActiveShipTextures();
        }
        if (triggerDetainClamps)
        {
            TriggerDetainLeverClampSequence();
            _detainLeverVisualPos = 1.0f;
            _detainClampElapsed = DetainSequenceDurationSeconds * 0.45f;
        }
        if (triggerFaultBeat && _activeShip != null)
        {
            BeginProtocolFaultSequence(
                ShipInfractionType.TransponderIdMismatch,
                $"RAW ID '{_activeShip.GroundTruth.RawTransponderId}' vs SLATE '{_activeShip.Slate.RegistrationCode}'",
                ReviewTarget.DataSlatePrefix,
                ReviewTarget.VisualSignalTransponderId);
        }
    }

    internal void SmokeStageDockedClearanceSlate(float encryptionProgress = 0.62f)
    {
        if (_activeShip == null)
        {
            SmokeStageShipLoadedAndInspecting();
        }
        BeginClearanceUploadInDock();
        _clearanceUploadElapsed = ClearanceUploadDurationSeconds * Math.Clamp(encryptionProgress, 0f, 1f);
        _dockSettleActive = false;
        _dockSettleElapsed = 0f;
        _dockReleaseKickActive = false;
        _dockReleaseKickElapsed = 0f;
        if (encryptionProgress >= 1.0f)
        {
            _slateTelemetry = DataSlateTelemetryState.ClearedReadyToTransmit;
            _phase = EncounterPhase.AwaitingClearanceReturnToTopPort;
            _dockLatchProgress = 0f;
            _dockLatchTarget = 0f;
        }
        else
        {
            _dockLatchProgress = 1f;
            _dockLatchTarget = 1f;
        }
    }

    internal void SmokeStageWarpStripOnChecker(int refSystemIndex, string? forcedActualOrigin = null)
    {
        if (_activeShip == null)
        {
            SmokeStageShipLoadedAndInspecting();
        }
        if (_activeShip == null)
            return;

        _activeShip.GroundTruth.ActualShieldsUp = false;
        if (!string.IsNullOrWhiteSpace(forcedActualOrigin))
        {
            _activeShip.GroundTruth.ActualOriginSystem = forcedActualOrigin;
        }
        _warpStripActualOrigin = WarpSignatureGenerator.NormalizeSystemKey(_activeShip.GroundTruth.ActualOriginSystem);
        _warpStripShipReg = _activeShip.Slate.RegistrationCode;
        _warpPhase = WarpPrinterPhase.TornOffFreeDocument;
        _warpRefSpotSystemIndex = Math.Clamp(refSystemIndex, 0, WarpSignatureGenerator.SectorSystems.Length - 1);

        DraggableDeskItem strip = _deskItems[DeskItemId.WarpStrip];
        strip.IsVisible = true;
        strip.X = StripSnapX;
        strip.Y = StripSnapY;
        BringDeskItemToFront(DeskItemId.WarpStrip);
    }

    internal void SmokeSetManualTab(int tab)
    {
        _manualActiveTab = tab == 0 ? 0 : 1;
        if (_manualActiveTab == 1 && IsManualUnlocked)
        {
            RebuildManualSilhouetteTexture();
        }
    }

    internal (int WeaponScreenX, int WeaponScreenY) SmokeStageHonestArmedShipWithSps(bool applyWeaponsPowerDown = false)
    {
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;
        _shiftController.NotifyFirstHailTriggered();
        StartNextShipEncounter(DebugSpawnValidityMode.ForceValid, reseedNonce: 7u);
        _shipApproachElapsed = ShipApproachDurationSeconds;
        _shipSlideOffset = 0f;
        RebuildLivePortraitTexture();
        BeginSubspaceProtocolsDownload(isReTransmit: false);
        _protocolDownloadElapsed = ProtocolDownloadDurationSeconds;
        CompleteSubspaceProtocolsDownload();

        if (_activeShip == null)
            return (310, 165);

        _activeShip.GroundTruth.ActualShieldsUp = false;
        _activeShip.GroundTruth.ActualWeaponsHot = true;
        _activeShip.GroundTruth.HasSpoofedWeapons = false;
        _activeShip.Slate.DeclaredShieldsStatus = "DOWN";
        _activeShip.Slate.DeclaredWeaponsStatus = "ARMED";
        RebuildActiveShipTextures();

        _spsEnabled = IsSpsUnlocked;
        _spsProbeExtend = _spsEnabled ? 1.0f : 0.0f;

        int wx = 310;
        int wy = 165;
        if (_activeShipHardpoints != null)
        {
            // Center swaying lens cleanly over forward weapon mount and lock via UpdateSpsHoldToLock!
            wx = ShipAnchorX + _activeShipHardpoints.ForwardWeapons.X * 2 + 1;
            wy = ShipAnchorY + _activeShipHardpoints.ForwardWeapons.Y * 2 + 1;
            _currentSpsSwayOffset = Vector2.Zero;
            VirtualMouseState aimMouse = new() { X = wx, Y = wy, IsInsideViewport = true, Scale = 1.0f };
            UpdateSpsLensRect(aimMouse);
            UpdateSpsHoldToLock(SpsLockDurationSeconds + 0.05f, aimMouse);
        }

        if (applyWeaponsPowerDown)
        {
            _pendingWeaponsOrder = true;
            _complianceTimer = 0f;
            ResolvePendingComplianceOrders();
        }

        return (wx, wy);
    }

    internal void SmokeVerifyPrompt371Interactions()
    {
        // 1. Verify Clearance Upload Dock mid-encryption pull aborts & resets to 0% (plus 3.7.2 latch snap & no seal)
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        SmokeStageDockedClearanceSlate(encryptionProgress: 0.55f);
        if (_phase != EncounterPhase.UploadingClearance || !IsDataSlateInClearanceDock ||
            _dockLatchProgress < 0.99f || HasActiveClearanceSeal)
            throw new InvalidOperationException("Failed to stage UploadingClearance with clamped latches and no premature seal.");

        int dockPressX = ClearanceSlotX + 60;
        int dockPressY = ClearanceSlotY + 60;
        Update(0.016f, new VirtualMouseState { X = dockPressX, Y = dockPressY, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
        // Drag 40px left -> must abort encryption, immediately snap latches open, cancel settle, and keep no seal!
        Update(0.016f, new VirtualMouseState { X = dockPressX - 40, Y = dockPressY, IsInsideViewport = true, LeftDown = true, Scale = 1.0f });
        if (_phase != EncounterPhase.InspectingSlateAndShip ||
            _slateTelemetry != DataSlateTelemetryState.Loaded ||
            _clearanceUploadElapsed > 0.001f ||
            IsDataSlateInClearanceDock ||
            _dockLatchProgress > 0.001f ||
            _dockLatchTarget > 0.001f ||
            _dockSettleActive ||
            HasActiveClearanceSeal)
        {
            throw new InvalidOperationException("Pulling Data Slate mid-encryption did not abort, snap latches open, & reset Clearance Dock to 0%.");
        }
        // Release on desk, then re-slot into Clearance Dock -> verify 0.12s downward micro-settle & inward clamp animation
        Update(0.016f, new VirtualMouseState { X = 600, Y = 450, IsInsideViewport = true, LeftReleased = true, Scale = 1.0f });
        BeginClearanceUploadInDock();
        if (!_dockSettleActive || _dockLatchTarget < 0.99f || GetDataSlateVisualOffsetY() <= 0)
            throw new InvalidOperationException("Dropping Data Slate into Clearance Dock did not start downward micro-settle and latch clamp-in.");

        Update(0.06f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (_dockLatchProgress <= 0.10f || GetDataSlateVisualOffsetY() <= 0 || HasActiveClearanceSeal)
            throw new InvalidOperationException("Clearance Dock clamp-in or micro-settle failed mid-animation.");

        // Advance to just before 100%, then cross 100% -> verify upward release kick, open latches, and CLEARED seal!
        Update(ClearanceUploadDurationSeconds - 0.10f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (_dockLatchProgress < 0.99f || HasActiveClearanceSeal)
            throw new InvalidOperationException("Clearance Dock latches failed to stay locked prior to 100% completion.");

        Update(0.06f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (_phase != EncounterPhase.AwaitingClearanceReturnToTopPort ||
            _slateTelemetry != DataSlateTelemetryState.ClearedReadyToTransmit ||
            !HasActiveClearanceSeal ||
            _dockLatchTarget > 0.001f ||
            !_dockReleaseKickActive ||
            GetDataSlateVisualOffsetY() >= 0)
        {
            throw new InvalidOperationException("Completing encryption at 100% did not apply CLEARED seal, snap latches open, and trigger upward release kick.");
        }

        Update(0.20f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (_dockLatchProgress > 0.001f || _dockReleaseKickActive || GetDataSlateVisualOffsetY() != 0)
            throw new InvalidOperationException("Clearance Dock latches or release kick failed to finish cleanly.");

        // 2. Verify Slate Bus TX CLEARANCE seat tick, CLEARED seal persistence, and mid-stream pull abort
        DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
        slate.X = SlateBusDockedX;
        slate.Y = SlateBusDockedY;
        BeginClearanceTransmissionToShip();
        if (!_slateBusSettleActive || GetDataSlateVisualOffsetY() <= 0 || !HasActiveClearanceSeal)
            throw new InvalidOperationException("Snapping cleared Data Slate into Slate Bus did not trigger matching seat tick or keep CLEARED seal.");

        _clearanceTransmitElapsed = ClearanceTransmitDurationSeconds * 0.50f;
        int busPressX = SlateBusDockedX + 60;
        int busPressY = SlateBusDockedY + 60;
        Update(0.016f, new VirtualMouseState { X = busPressX, Y = busPressY, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
        Update(0.016f, new VirtualMouseState { X = busPressX, Y = busPressY + 40, IsInsideViewport = true, LeftDown = true, Scale = 1.0f });
        if (_phase != EncounterPhase.AwaitingClearanceReturnToTopPort ||
            _slateTelemetry != DataSlateTelemetryState.ClearedReadyToTransmit ||
            _clearanceTransmitElapsed > 0.001f ||
            IsDataSlateInSlateBus ||
            _slateBusSettleActive ||
            !HasActiveClearanceSeal)
        {
            throw new InvalidOperationException("Pulling cleared Data Slate mid-TX CLEARANCE did not abort & reset TX cleanly while preserving CLEARED seal.");
        }
        Update(0.016f, new VirtualMouseState { X = 600, Y = 450, IsInsideViewport = true, LeftReleased = true, Scale = 1.0f });

        // Verify TX CLEARANCE completion clears the CLEARED seal cleanly
        slate.X = SlateBusDockedX;
        slate.Y = SlateBusDockedY;
        BeginClearanceTransmissionToShip();
        Update(ClearanceTransmitDurationSeconds + 0.05f, new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (HasActiveClearanceSeal || _slateTelemetry != DataSlateTelemetryState.Empty)
            throw new InvalidOperationException("Completing TX CLEARANCE did not clear the CLEARED seal and reset the Data Slate.");

        // Verify DETAIN lever wipe also removes CLEARED seal & resets dock micro-animations
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        SmokeStageDockedClearanceSlate(encryptionProgress: 1.0f);
        if (!HasActiveClearanceSeal)
            throw new InvalidOperationException("Expected CLEARED seal at 100% prior to Detain wipe test.");
        TriggerDetainLeverClampSequence();
        Update(DetainSequenceDurationSeconds * ShiftController.DetainSlateWipeFraction + 0.05f,
            new VirtualMouseState { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f });
        if (HasActiveClearanceSeal || _slateTelemetry != DataSlateTelemetryState.Empty)
            throw new InvalidOperationException("DETAIN lever wipe did not remove the CLEARED seal cleanly.");

        // 3. Verify Honest Armed Ship SPS detection, non-clickable SPS mini-screen outside Protocol Review, and cooling upon PWR DOWN WPN
        if (IsSpsUnlocked)
        {
            SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
            if (_spsLoggedAnomalies.Count != 1 ||
                _spsLoggedAnomalies[0].Kind != SpsAnomalyKind.HotWeaponMount ||
                _spsLoggedAnomalies[0].IsCooled)
            {
                throw new InvalidOperationException("SPS failed to lock and log hot weapons on an honest armed vessel.");
            }

            // Clicking the SPS Anomaly Mini-Screen directly MUST NOT power down weapons or trigger a fault!
            int miniClickX = SpsMiniX + 40;
            int miniClickY = SpsMiniY + 36;
            Update(0.016f, new VirtualMouseState { X = miniClickX, Y = miniClickY, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            if (_faultStage != FaultSequenceStage.None || _pendingWeaponsOrder || _activeShip == null || !_activeShip.GroundTruth.ActualWeaponsHot)
            {
                throw new InvalidOperationException("Clicking SPS Anomaly Mini-Screen illegally triggered a command or accusation.");
            }

            // Day 3+ Protocol Review pairing WPN: ARMED (honest) with SPS LOG [!] HOT WPN must report NO DISCREPANCY
            if (IsProtocolReviewUnlocked)
            {
                _protocolReviewModeActive = true;
                EvaluateProtocolReviewPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons);
                if (_faultStage != FaultSequenceStage.None || _activeShip.ProtocolReviewVerified)
                {
                    throw new InvalidOperationException("Honest armed ship falsely triggered Protocol Fault when pairing WPN: ARMED with SPS LOG.");
                }
                _protocolReviewModeActive = false;
            }

            // Now press PWR DOWN WPN -> honest captain complies -> SPS visual cools and SPS LOG marks [OK] COOLED
            _pendingWeaponsOrder = true;
            _complianceTimer = 0f;
            ResolvePendingComplianceOrders();
            if (_activeShip.GroundTruth.ActualWeaponsHot || !_spsLoggedAnomalies[0].IsCooled)
            {
                throw new InvalidOperationException("PWR DOWN WPN compliance failed to cool weapons & mark SPS LOG cooled.");
            }

            // 4. Verify Day 3+ Spoofed Weapons lying captain CAN be caught via Protocol Review using SPS Anomaly Mini-Screen
            if (IsProtocolReviewUnlocked)
            {
                SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
                if (_activeShip != null)
                {
                    _activeShip.GroundTruth.HasSpoofedWeapons = true;
                    _activeShip.Slate.DeclaredWeaponsStatus = "OFFLINE";
                    _protocolReviewModeActive = true;
                    EvaluateProtocolReviewPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons);
                    if (_faultStage != FaultSequenceStage.FaultBeat || !_activeShip.ProtocolReviewVerified)
                    {
                        throw new InvalidOperationException("Day 3+ Protocol Review failed to verify spoofed weapons via SPS Anomaly Mini-Screen.");
                    }
                    ResetFaultSequenceState();
                }
            }
        }
    }

    // =========================================================================
    // Virtual 1280x720 Rendering Pipeline
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        // Industrial Booth 04 dark steel console base
        Raylib.ClearBackground(new Color(22, 26, 32, 255));

        // 1. Top Status Bar (0, 0, 1280, 32)
        DrawTopStatusBar(mouse);

        // 2. Upper Bulkhead Panel Backing (y = 32..260)
        Raylib.DrawRectangle(0, 32, Constants.VirtualWidth, 228, new Color(28, 34, 42, 255));
        Raylib.DrawLine(0, 260, Constants.VirtualWidth, 260, Palette.OutlineVoid);
        Raylib.DrawLine(0, 261, Constants.VirtualWidth, 261, Palette.WeatheredGunmetalEdge);

        // 3. Row 1 Hardware (y = 40..256, strict 8px gutters)
        DrawLeftDetainLever(mouse);
        DrawArmoredExternalViewport(mouse);
        DrawSpsControlColumn(mouse);
        DrawHailingViewscreenAndTransponder(mouse);
        DrawCrtCommsDialogueLog(mouse);

        // 4. Lower Console Desk Surface, Contraband Lap Pocket & One-Way Conduit (y = 262..720)
        DrawLowerDeskSurfaceAndConduit();

        // 5. Row 2 & Row 2B Hardware (y = 264..352)
        DrawInactiveHardwareBay(StokerDialX, StokerDialY, StokerDialW, StokerDialH, "STOKER DIAL", null);
        DrawWarpResiduePrinterAndRefSpot(mouse);
        DrawSlateBusSocket();
        DrawCommsCommandStrip(mouse);
        DrawProtocolReviewAndBioZone(mouse);

        // 6. Lower Desk Hardware (Wafer Writer with top Feed Slot, Clearance Dock, Destroy Override)
        DrawWaferWriterModule();
        DrawClearanceUploadDock(mouse);
        DrawInactiveHardwareBay(DestroyOverrideX, DestroyOverrideY, DestroyOverrideW, DestroyOverrideH, "DESTROY OVERRIDE", null);

        // 7. Movable Desk Documents (Z-Order bottom to top)
        // NOTE: DataSlate ALWAYS renders via DrawMovableDataSlate even when docked inside Clearance Upload Dock (1080, 384, 184, 216)!
        for (int i = 0; i < _zOrder.Count; i++)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible)
                continue;

            bool isTopmost = (i == _zOrder.Count - 1);
            switch (id)
            {
                case DeskItemId.DataSlate:
                    DrawMovableDataSlate(mouse, item, isTopmost);
                    if (IsDataSlateInClearanceDock)
                    {
                        // Mechanical metal clamp latches animate inward OVER the docked slate's sides
                        DrawClearanceDockLatches(isEncrypting: _phase == EncounterPhase.UploadingClearance);
                    }
                    break;
                case DeskItemId.ReferenceManual:
                    if (IsManualUnlocked)
                    {
                        DrawDeskReferenceManual(mouse, item, isTopmost);
                    }
                    break;
                case DeskItemId.WarpStrip:
                    if (IsSpectrometerOperational)
                    {
                        DrawFreeDraggableWarpStrip(mouse, item, isTopmost);
                    }
                    break;
            }
        }

        // Day 3+ L->R extruding or anchored magnetic strip inside Warp Printer tray
        if (IsSpectrometerOperational &&
            (_warpPhase is WarpPrinterPhase.PrintingStrip or WarpPrinterPhase.AnchoredReadyToTear))
        {
            DrawExtrudingOrAnchoredWarpStrip(mouse);
        }

        // Error-Only Citation Strip: transient slide-out overlay rendered ONLY when an error actually occurs (ZERO permanent box!)
        DrawErrorOnlyCitationPopup();
    }

    // =========================================================================
    // 1. Top Status Bar: (0, 0, 1280, 32) -- vertically centered, zero top-edge clipping
    // =========================================================================
    private void DrawTopStatusBar(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(TopBarX, TopBarY, TopBarW, TopBarH, Palette.OutlineVoid);
        Raylib.DrawLine(0, TopBarH - 1, TopBarW, TopBarH - 1, Palette.WeatheredGunmetalEdge);

        // Clean vertical centering inside y = 0..32 (fontSize = 10 -> y = 11 gives 11px top / 10px bottom before y = 31 divider)
        const int textY = 11;

        // Left: Booth ID + Day (aligned cleanly to 8px left margin)
        Raylib.DrawText("VOID GATE 3 // BOOTH 04", 8, textY, 10, Palette.CrtAmberDimmed);
        Raylib.DrawText($"DAY {_gameState.Day:D2}", 170, textY, 10, Palette.CrtAmberPrimary);

        // Station Cycle box (236, 4, 124, 24) -- clickable in Protocol Review on Day 3+
        (int curYear, int curMonth) = Constants.GetStationCycle(_gameState.Day);
        string cycleLabel = $"CYC {Constants.FormatStationCycle(curYear, curMonth)}";
        bool cycleSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.TopHeaderCycle;
        bool cycleFault = IsTargetFaultHighlighted(ReviewTarget.TopHeaderCycle);
        Raylib.DrawRectangle(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleFault ? Palette.BrownDwarfGlowDeep : (cycleSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid));
        Raylib.DrawRectangleLines(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleFault ? GetFaultPulseColor() : (cycleSelected ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn));
        if (_protocolReviewModeActive)
        {
            DrawReviewHotspotOutline(mouse, TopCycleX, TopCycleY, TopCycleW, TopCycleH, cycleSelected);
        }
        int cycW = Raylib.MeasureText(cycleLabel, 10);
        Raylib.DrawText(cycleLabel, TopCycleX + (TopCycleW - cycW) / 2, TopCycleY + (TopCycleH - 10) / 2, 10,
            cycleSelected ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

        // Pact Scrip, Daily Warnings, Security Strikes (Day 3+)
        Raylib.DrawText($"PACT SCRIP: {_gameState.PactScrip} SC", 384, textY, 10, Palette.ClearanceTeal);
        Raylib.DrawText(
            $"WARNINGS: {_gameState.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift}",
            556, textY, 10,
            _gameState.ClericalWarningsThisShift >= Constants.FreeClericalWarningsPerShift ? Palette.AlertCrimson : Palette.CrtAmberPrimary);

        if (AreSecurityStrikesUnlocked)
        {
            Raylib.DrawText(
                $"SECURITY STRIKES: {_gameState.SecurityStrikes}/{_gameState.MaxSecurityStrikesThreshold}",
                704, textY, 10,
                _gameState.SecurityStrikes > 0 ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
        }

        Raylib.DrawText($"PROCESSED: {_gameState.ShipsProcessedToday}", 916, textY, 10, Palette.CrtAmberDimmed);

        // Pause Button: (1236, 4, 36, 24) -- 4px top/bottom padding, 8px right outer margin
        bool pauseHover = mouse.IsHit(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH);
        Raylib.DrawRectangle(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH,
            pauseHover ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH,
            pauseHover ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        Color barCol = pauseHover ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;
        Raylib.DrawRectangle(PauseBtnX + 12, PauseBtnY + 6, 4, 12, barCol);
        Raylib.DrawRectangle(PauseBtnX + 20, PauseBtnY + 6, 4, 12, barCol);
    }

    // =========================================================================
    // 2. Physical Left-Side Detain Lever: (8, 40, 96, 216)
    // =========================================================================
    private void DrawLeftDetainLever(VirtualMouseState mouse)
    {
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;
        bool canDetain = !lockedByPreShift && _activeShip != null &&
                         (_phase is EncounterPhase.OpeningGreetingDelay
                             or EncounterPhase.AwaitingPortForDownload
                             or EncounterPhase.DownloadingProtocols
                             or EncounterPhase.DownloadAborted
                             or EncounterPhase.InspectingSlateAndShip
                             or EncounterPhase.AwaitingClearanceReturnToTopPort);
        bool hovered = canDetain && mouse.IsHit(DetainLeverX, DetainLeverY, DetainLeverW, DetainLeverH);

        // Armored bulkhead housing
        Raylib.DrawRectangle(DetainLeverX, DetainLeverY, DetainLeverW, DetainLeverH, new Color(26, 30, 37, 255));
        Raylib.DrawRectangleLines(DetainLeverX, DetainLeverY, DetainLeverW, DetainLeverH,
            _detainLeverPulled ? Palette.AlertCrimson : (hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        // Top engraved plate "DETAIN"
        Raylib.DrawRectangle(DetainLeverX + 6, DetainLeverY + 6, DetainLeverW - 12, 22,
            _detainLeverPulled ? Palette.AlertCrimson : Palette.BrownDwarfGlowDeep);
        Raylib.DrawRectangleLines(DetainLeverX + 6, DetainLeverY + 6, DetainLeverW - 12, 22,
            canDetain || _detainLeverPulled ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
        const string label = "DETAIN";
        int lw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, DetainLeverX + (DetainLeverW - lw) / 2, DetainLeverY + 12, 10,
            _detainLeverPulled ? Palette.OutlineVoid : (canDetain ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        // Vertical hazard stripes flanking the mechanical track
        for (int sy = DetainLeverY + 36; sy < DetainLeverY + DetainLeverH - 28; sy += 12)
        {
            Color stripeCol = canDetain || _detainLeverPulled ? Palette.BrownDwarfGlowRim : Palette.WeatheredGunmetalMid;
            Raylib.DrawRectangle(DetainLeverX + 6, sy, 6, 6, stripeCol);
            Raylib.DrawRectangle(DetainLeverX + DetainLeverW - 12, sy, 6, 6, stripeCol);
        }

        // Recessed mechanical steel track slot (centered inside DetainLeverW = 96)
        const int trackW = 28;
        int trackX = DetainLeverX + (DetainLeverW - trackW) / 2;
        int trackY = DetainLeverY + 36;
        int trackH = DetainLeverH - 68;
        Raylib.DrawRectangle(trackX, trackY, trackW, trackH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(trackX, trackY, trackW, trackH, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(trackX + (trackW - 6) / 2, trackY + 6, 6, trackH - 12, new Color(18, 22, 28, 255));

        // Articulated mechanical lever arm & heavy red grip handle
        int topHandleY = trackY + 10;
        int botHandleY = trackY + trackH - 36;
        int handleY = topHandleY + (int)MathF.Round((botHandleY - topHandleY) * _detainLeverVisualPos);

        // Metal linkage shaft connecting track center to grip
        int midTrackY = trackY + trackH / 2;
        int shaftTop = Math.Min(midTrackY, handleY + 12);
        int shaftBot = Math.Max(midTrackY, handleY + 12);
        Raylib.DrawRectangle(DetainLeverX + (DetainLeverW - 6) / 2, shaftTop, 6, Math.Max(4, shaftBot - shaftTop), Palette.WeatheredGunmetalEdge);

        // Heavy red cylindrical T-bar grip handle
        int gripX = DetainLeverX + 16;
        int gripW = DetainLeverW - 32;
        int gripH = 26;
        Color gripFill = _detainLeverPulled
            ? Palette.AlertCrimson
            : (canDetain ? (hovered ? Palette.AlertCrimson : new Color(168, 32, 32, 255)) : new Color(68, 34, 34, 255));
        Color gripTop = _detainLeverPulled || hovered ? new Color(248, 113, 113, 255) : Palette.BrownDwarfGlowRim;

        Raylib.DrawRectangle(gripX, handleY, gripW, gripH, gripFill);
        Raylib.DrawRectangle(gripX + 2, handleY + 2, gripW - 4, 4, gripTop);
        Raylib.DrawLine(gripX + 4, handleY + 12, gripX + gripW - 4, handleY + 12, Palette.OutlineVoid);
        Raylib.DrawLine(gripX + 4, handleY + 17, gripX + gripW - 4, handleY + 17, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(gripX, handleY, gripW, gripH, Palette.OutlineVoid);

        // Bottom indicator lamp
        bool blink = ((_animTick / 8) % 2) == 0;
        Color ledCol = _detainLeverPulled
            ? (blink ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
            : (canDetain ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(DetainLeverX + 14, DetainLeverY + DetainLeverH - 22, DetainLeverW - 28, 14, Palette.OutlineVoid);
        Raylib.DrawRectangle(DetainLeverX + 16, DetainLeverY + DetainLeverH - 20, DetainLeverW - 32, 10, ledCol);
        Raylib.DrawRectangleLines(DetainLeverX + 14, DetainLeverY + DetainLeverH - 22, DetainLeverW - 28, 14, Palette.WeatheredGunmetalEdge);
    }

    // =========================================================================
    // 3. Armored External Space Viewport (Real Glass Window): (80, 40, 480, 216)
    // =========================================================================
    private void DrawArmoredExternalViewport(VirtualMouseState mouse)
    {
        // Outer reinforced armored station window casing (real glass window, zero CRT chrome)
        Raylib.DrawRectangle(ViewportX, ViewportY, ViewportW, ViewportH, new Color(36, 42, 50, 255));
        Raylib.DrawRectangleLines(ViewportX, ViewportY, ViewportW, ViewportH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ViewportX + 1, ViewportY + 1, ViewportW - 2, ViewportH - 2, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangleLines(ViewportX + 5, ViewportY + 5, ViewportW - 10, ViewportH - 10, Palette.OutlineVoid);

        // Heavy corner armor bolts on the window frame
        int[] boltX = [ViewportX + 3, ViewportX + ViewportW - 5];
        int[] boltY = [ViewportY + 3, ViewportY + ViewportH - 5];
        foreach (int bx in boltX)
        {
            foreach (int by in boltY)
            {
                Raylib.DrawRectangle(bx, by, 2, 2, Palette.OxidizedBronzeWorn);
            }
        }

        int shakeX = 0;
        int shakeY = 0;
        if (_viewportImpactShakeTimer > 0f)
        {
            shakeX = ((_animTick % 2) == 0 ? 2 : -2);
            shakeY = (((_animTick / 2) % 2) == 0 ? 1 : -1);
        }

        Raylib.BeginScissorMode(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH);

        // Deep Space backdrop inside glass window
        Raylib.DrawRectangle(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH, Palette.DeepSpaceVoid);

        int horizonY = ViewportInnerY + ViewportInnerH - 18;
        Raylib.DrawRectangle(ViewportInnerX, horizonY - 26, ViewportInnerW, 26, Palette.BrownDwarfGlowDeep);

        // Subtle starfield
        for (int s = 0; s < 52; s++)
        {
            int sx = ViewportInnerX + 10 + ((s * 97 + 23) % (ViewportInnerW - 20));
            int sy = ViewportInnerY + 6 + ((s * 53 + 17) % (horizonY - ViewportInnerY - 12));
            Color starCol = (s % 4 == 0) ? Palette.BrownDwarfGlowRim : Palette.WeatheredGunmetalEdge;
            Raylib.DrawPixel(sx, sy, starCol);
        }

        // Diegetic 270-second Brown Dwarf arc (low-left -> high-center -> low-right)
        float p = _shiftController.BrownDwarfArcProgress;
        int dwarfStartX = ViewportInnerX + 36;
        int dwarfEndX = ViewportInnerX + ViewportInnerW - 36;
        int dwarfX = dwarfStartX + (int)MathF.Round(p * (dwarfEndX - dwarfStartX));
        float baseHorizonY = (horizonY - 8) + p * 22.0f;
        int dwarfY = (int)MathF.Round(baseHorizonY - MathF.Sin(p * MathF.PI) * 142.0f);

        Raylib.DrawCircle(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircle(dwarfX - 2, dwarfY - 2, 18, Palette.BrownDwarfGlowRim);
        Raylib.DrawRectangle(dwarfX - 20, dwarfY - 5, 40, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawRectangle(dwarfX - 18, dwarfY + 3, 36, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircleLines(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowRim);

        // Exterior docking bay sill along lower edge of window
        Raylib.DrawRectangle(ViewportInnerX, horizonY, ViewportInnerW, ViewportInnerY + ViewportInnerH - horizonY, Palette.OutlineVoid);
        Raylib.DrawLine(ViewportInnerX, horizonY, ViewportInnerX + ViewportInnerW, horizonY, Palette.BrownDwarfGlowRim);
        for (int gx = ViewportInnerX + 20; gx < ViewportInnerX + ViewportInnerW - 18; gx += 48)
        {
            Raylib.DrawRectangle(gx, horizonY - 5, 12, 6, Palette.WeatheredGunmetalMid);
            Raylib.DrawPixel(gx + 5, horizonY - 3, Palette.CrtAmberDimmed);
        }

        // Docked / Approaching Ship Sprite
        if (_preShiftStage == PreShiftStage.Day3KordaniLiveHail)
        {
            // Keep viewport cleanly empty during pre-shift internal Kordani hail
        }
        else if (_activeShip != null && _cachedShipLiveTex.Id != 0)
        {
            int shipDrawX = ShipAnchorX + (int)MathF.Round(_shipSlideOffset) + shakeX;
            int shipDrawY = ShipAnchorY + shakeY;

            Rectangle src = new(0, 0, Constants.ShipWidth, Constants.ShipHeight);
            Rectangle dst = new(shipDrawX, shipDrawY, ShipRenderW, ShipRenderH);
            Raylib.DrawTexturePro(_cachedShipLiveTex, src, dst, Vector2.Zero, 0f, Color.White);

            // Protocol Review hover or Fault highlight on live ship hull
            bool hullFault = IsTargetFaultHighlighted(ReviewTarget.ViewportShipHull);
            bool hullReviewHover = _protocolReviewModeActive &&
                                   IsShipInspectableForReview &&
                                   mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH) &&
                                   (!_spsEnabled || !mouse.IsHit(_lastSpsLensRect));
            if (hullFault || hullReviewHover)
            {
                Color hiCol = hullFault ? GetFaultPulseColor() : Palette.CrtAmberPrimary;
                Raylib.DrawRectangleLines(shipDrawX - 3, shipDrawY - 3, ShipRenderW + 6, ShipRenderH + 6, hiCol);
                if (hullFault)
                {
                    Raylib.DrawRectangleLines(shipDrawX - 4, shipDrawY - 4, ShipRenderW + 8, ShipRenderH + 8, hiCol);
                }
            }

            // Animated Physical Docking Clamps snapping onto the ship inside the glass viewport!
            if (_phase == EncounterPhase.DetainClamping ||
                (_phase == EncounterPhase.ResolvingDecision && _pendingDecision == PlayerDecision.Detain))
            {
                DrawAnimatedDetainDockingClamps(shipDrawX, shipDrawY, horizonY);
            }

            // Day 2+ Scissor-Clipped SPS Reticle Lens
            if (IsSpsUnlocked && _spsEnabled && _cachedShipSpsTex.Id != 0)
            {
                int lx = (int)_lastSpsLensRect.X;
                int ly = (int)_lastSpsLensRect.Y;
                int lw = (int)_lastSpsLensRect.Width;
                int lh = (int)_lastSpsLensRect.Height;

                Raylib.EndScissorMode();
                Raylib.BeginScissorMode(lx, ly, lw, lh);

                Raylib.DrawRectangle(lx, ly, lw, lh, new Color(8, 16, 26, 232));
                for (int sy = ly + 4; sy < ly + lh; sy += 6)
                {
                    Raylib.DrawLine(lx, sy, lx + lw, sy, new Color(18, 42, 60, 160));
                }

                Raylib.DrawTexturePro(_cachedShipSpsTex, src, dst, Vector2.Zero, 0f, Color.White);

                Raylib.EndScissorMode();
                Raylib.BeginScissorMode(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH);

                bool lensFault = IsTargetFaultHighlighted(ReviewTarget.SpsLensConduit);
                bool reviewLensHover = _protocolReviewModeActive && mouse.IsHit(_lastSpsLensRect);
                bool lockingActive = _spsLockProgress > 0.01f;
                Color reticleCol = lensFault
                    ? GetFaultPulseColor()
                    : (reviewLensHover || lockingActive ? Palette.CrtAmberPrimary : Palette.SpsCommsActive);

                Raylib.DrawRectangleLines(lx, ly, lw, lh, reticleCol);
                int cx = lx + lw / 2;
                int cy = ly + lh / 2;
                Raylib.DrawLine(cx - 5, cy, cx + 5, cy, reticleCol);
                Raylib.DrawLine(cx, cy - 5, cx, cy + 5, reticleCol);
                Raylib.DrawLine(lx + 1, ly + 1, lx + 8, ly + 1, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + 1, ly + 1, lx + 1, ly + 8, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + lw - 9, ly + lh - 2, lx + lw - 2, ly + lh - 2, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + lw - 2, ly + lh - 9, lx + lw - 2, ly + lh - 2, Palette.SpsShieldArcIce);

                if (lockingActive)
                {
                    int lockMaxW = lw - 10;
                    int lockFillW = Math.Clamp((int)MathF.Round(lockMaxW * _spsLockProgress), 1, lockMaxW);
                    Raylib.DrawRectangle(lx + 5, ly + lh - 6, lockMaxW, 3, Palette.OutlineVoid);
                    Raylib.DrawRectangle(lx + 5, ly + lh - 6, lockFillW, 3, Palette.CrtAmberPrimary);
                }
            }
        }
        else if (_shiftController.CanEndShift)
        {
            const int boxW = 316;
            int bx = ViewportX + (ViewportW - boxW) / 2;
            int by = ViewportY + 96;
            Raylib.DrawRectangle(bx, by, boxW, 88, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(bx, by, boxW, 88, Palette.CrtAmberPrimary);
            const string h1 = "BROWN DWARF HORIZON CONTACT";
            string h2 = $"DAY {_gameState.Day:D2} WATCH SHIFT COMPLETE";
            Raylib.DrawText(h1, bx + (boxW - Raylib.MeasureText(h1, 10)) / 2, by + 12, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText(h2, bx + (boxW - Raylib.MeasureText(h2, 10)) / 2, by + 28, 10, Palette.ClearanceTeal);
            DrawHardwareButton(mouse, ShiftEndBtnX, ShiftEndBtnY, ShiftEndBtnW, ShiftEndBtnH, "END WATCH -> WALK TO QUARTERS", enabled: true, highlight: true, Palette.ClearanceTeal);
        }

        // Day 2+ Physical Articulated SPS Camera/Sensor Probe Arm extending from right bulkhead into viewport
        if (IsSpsUnlocked && _spsProbeExtend > 0.01f)
        {
            DrawPhysicalSpsProbeArm();
        }

        // Subtle armored station glass diagonal glare streaks (real glass feel, zero blur)
        Color glassGlint = new(148, 163, 184, 18);
        Raylib.DrawLine(ViewportInnerX + 18, ViewportInnerY + 6, ViewportInnerX + 62, ViewportInnerY + 50, glassGlint);
        Raylib.DrawLine(ViewportInnerX + 26, ViewportInnerY + 6, ViewportInnerX + 54, ViewportInnerY + 34, glassGlint);
        Raylib.DrawLine(ViewportInnerX + ViewportInnerW - 70, ViewportInnerY + ViewportInnerH - 22,
                        ViewportInnerX + ViewportInnerW - 22, ViewportInnerY + ViewportInnerH - 70, glassGlint);

        Raylib.EndScissorMode();
    }

    private void DrawAnimatedDetainDockingClamps(int shipDrawX, int shipDrawY, int horizonY)
    {
        float impactTime = DetainSequenceDurationSeconds * ShiftController.DetainClampImpactFraction;
        float rawT = _phase == EncounterPhase.DetainClamping
            ? Math.Clamp(_detainClampElapsed / impactTime, 0f, 1f)
            : 1.0f;
        // Snappy mechanical ease-out slam
        float clampT = 1.0f - MathF.Pow(1.0f - rawT, 3.0f);

        int foreX = shipDrawX + 52;
        int aftX = shipDrawX + ShipRenderW - 64;

        int topTargetY = shipDrawY + 28;
        int botTargetY = shipDrawY + ShipRenderH - 28;

        int topJawY = ViewportInnerY + (int)MathF.Round((topTargetY - ViewportInnerY) * clampT);
        int botJawY = horizonY - (int)MathF.Round((horizonY - botTargetY) * clampT);

        bool locked = rawT >= 0.99f;
        bool flash = ((_animTick / 5) % 2) == 0;
        Color strobeCol = locked && flash ? Palette.CrtAmberPrimary : Palette.AlertCrimson;

        int[] clampXs = [foreX, aftX];
        foreach (int cx in clampXs)
        {
            // Top hydraulic gantry column + locking pincer claw
            int topLen = Math.Max(4, topJawY - ViewportInnerY);
            Raylib.DrawRectangle(cx, ViewportInnerY, 12, topLen, Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(cx, ViewportInnerY, 12, topLen, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx + 4, ViewportInnerY, 4, topLen, Palette.WeatheredGunmetalEdge);
            // Pincer jaw head
            Raylib.DrawRectangle(cx - 5, topJawY - 8, 22, 10, Palette.OxidizedBronzeShadow);
            Raylib.DrawRectangleLines(cx - 5, topJawY - 8, 22, 10, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx - 3, topJawY, 6, 6, strobeCol);
            Raylib.DrawRectangle(cx + 9, topJawY, 6, 6, strobeCol);

            // Bottom hydraulic gantry column + locking pincer claw
            int botLen = Math.Max(4, horizonY - botJawY);
            Raylib.DrawRectangle(cx, botJawY, 12, botLen, Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(cx, botJawY, 12, botLen, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx + 4, botJawY, 4, botLen, Palette.WeatheredGunmetalEdge);
            // Pincer jaw head
            Raylib.DrawRectangle(cx - 5, botJawY - 2, 22, 10, Palette.OxidizedBronzeShadow);
            Raylib.DrawRectangleLines(cx - 5, botJawY - 2, 22, 10, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx - 3, botJawY - 6, 6, 6, strobeCol);
            Raylib.DrawRectangle(cx + 9, botJawY - 6, 6, 6, strobeCol);

            // Magnetic lock beam connecting upper & lower jaws once snapped shut
            if (locked)
            {
                Raylib.DrawLine(cx + 6, topJawY + 4, cx + 6, botJawY - 4, strobeCol);
                if (_viewportImpactShakeTimer > 0f)
                {
                    // Brief contact spark pixels
                    Raylib.DrawPixel(cx - 7, topJawY + 2, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx + 18, topJawY + 3, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx - 6, botJawY - 2, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx + 17, botJawY - 3, Palette.CrtAmberPrimary);
                }
            }
        }
    }

    private void DrawPhysicalSpsProbeArm()
    {
        float ext = _spsProbeExtend;
        int mountX = ViewportInnerX + ViewportInnerW; // right glass wall (554)
        int mountY = ViewportInnerY + 44;             // aligned with SPS toggle height

        // Subtle swivel aiming toward the swaying SPS reticle
        float targetY = _lastSpsLensRect.Y + _lastSpsLensRect.Height * 0.5f;
        int elbowX = mountX - (int)MathF.Round(24 * ext);
        int elbowY = mountY + (int)MathF.Round(Math.Clamp((targetY - mountY) * 0.20f, -12f, 24f) * ext);

        int headX = mountX - (int)MathF.Round(54 * ext);
        int headY = elbowY + (int)MathF.Round(Math.Clamp((targetY - elbowY) * 0.35f, -18f, 36f) * ext);

        // Bulkhead wall housing flange
        Raylib.DrawRectangle(mountX - 8, mountY - 12, 8, 24, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(mountX - 8, mountY - 12, 8, 24, Palette.OutlineVoid);
        Raylib.DrawRectangle(mountX - 6, mountY - 8, 4, 16, Palette.OxidizedBronzeWorn);

        // Upper & lower articulated steel boom segments
        DrawThickLine(mountX - 6, mountY, elbowX, elbowY, 5, Palette.OutlineVoid);
        DrawThickLine(mountX - 6, mountY, elbowX, elbowY, 3, Palette.WeatheredGunmetalEdge);
        DrawThickLine(elbowX, elbowY, headX, headY, 4, Palette.OutlineVoid);
        DrawThickLine(elbowX, elbowY, headX, headY, 2, Palette.WeatheredGunmetalEdge);

        // Mechanical elbow joint rivet
        Raylib.DrawRectangle(elbowX - 3, elbowY - 3, 6, 6, Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(elbowX - 3, elbowY - 3, 6, 6, Palette.OutlineVoid);

        // Camera / Sensor optical pod head
        Raylib.DrawRectangle(headX - 8, headY - 6, 16, 12, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(headX - 8, headY - 6, 16, 12, Palette.OutlineVoid);
        Raylib.DrawRectangle(headX - 10, headY - 4, 4, 8, Palette.SpsCommsActive);
        Raylib.DrawPixel(headX - 9, headY - 2, Palette.SpsShieldArcIce);
        Raylib.DrawPixel(headX + 4, headY - 4, Palette.CrtAmberPrimary);

        // Faint cyan sensor scanline beam connecting probe lens tip to swaying reticle when fully deployed
        if (ext >= 0.85f && _spsEnabled)
        {
            int rx = (int)(_lastSpsLensRect.X + _lastSpsLensRect.Width);
            int ry = (int)(_lastSpsLensRect.Y + _lastSpsLensRect.Height * 0.5f);
            Color beamCol = new(56, 189, 248, 70);
            Raylib.DrawLine(headX - 10, headY, rx, (int)_lastSpsLensRect.Y, beamCol);
            Raylib.DrawLine(headX - 10, headY, rx, (int)(_lastSpsLensRect.Y + _lastSpsLensRect.Height), beamCol);
        }
    }

    // =========================================================================
    // 4. SPS Control Column: Toggle (568, 40, 104, 60) + 8px gap + Mini-Screen (568, 108, 104, 148)
    // =========================================================================
    private void DrawSpsControlColumn(VirtualMouseState mouse)
    {
        if (!IsSpsUnlocked)
        {
            // Day 1: Recessed inactive bay with subtle low-contrast [PLAYTEST] stencil
            DrawInactiveHardwareBay(SpsColX, SpsColFullY, SpsColW, SpsColFullH, "SPS SCANNER", "DAY 02");
            return;
        }

        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;

        // Top: SPS Toggle Button (568, 40, 104, 60)
        bool spsHover = !lockedByPreShift && mouse.IsHit(SpsToggleX, SpsToggleY, SpsToggleW, SpsToggleH);
        Color btnBg = lockedByPreShift
            ? Palette.OutlineVoid
            : (_spsEnabled ? new Color(14, 44, 62, 255) : (spsHover ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid));
        Color btnBorder = lockedByPreShift
            ? Palette.WeatheredGunmetalEdge
            : (_spsEnabled ? Palette.SpsCommsActive : (spsHover ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        Raylib.DrawRectangle(SpsToggleX, SpsToggleY, SpsToggleW, SpsToggleH, btnBg);
        Raylib.DrawRectangleLines(SpsToggleX, SpsToggleY, SpsToggleW, SpsToggleH, btnBorder);
        Raylib.DrawRectangleLines(SpsToggleX + 2, SpsToggleY + 2, SpsToggleW - 4, SpsToggleH - 4, Palette.OutlineVoid);

        // Illuminated indicator bar on toggle
        Color ledCol = _spsEnabled ? Palette.SpsCommsActive : Palette.WeatheredGunmetalEdge;
        Raylib.DrawRectangle(SpsToggleX + 12, SpsToggleY + 9, SpsToggleW - 24, 5, ledCol);

        const string spsTitle = "SPS PROBE";
        int tw = Raylib.MeasureText(spsTitle, 10);
        Raylib.DrawText(spsTitle, SpsToggleX + (SpsToggleW - tw) / 2, SpsToggleY + 22, 10,
            _spsEnabled ? Palette.SpsShieldArcIce : Palette.CrtAmberPrimary);

        string spsState = _spsEnabled ? "[ ACTIVE ]" : "[ STANDBY ]";
        int sw = Raylib.MeasureText(spsState, 10);
        Raylib.DrawText(spsState, SpsToggleX + (SpsToggleW - sw) / 2, SpsToggleY + 38, 10,
            _spsEnabled ? Palette.SpsCommsActive : Palette.CrtAmberDimmed);

        // Bottom: SPS Anomaly Mini-Screen (568, 108, 104, 148)
        // Diagnostic log on Day 2; strictly selectable Protocol Review evidence on Day 3+ (never a command trigger!)
        bool hasLogged = _spsLoggedAnomalies.Count > 0;
        bool hasActiveAnomaly = _spsLoggedAnomalies.Any(e => !e.IsCooled);
        bool locking = _spsLockProgress > 0.01f;
        bool miniFault = IsTargetFaultHighlighted(ReviewTarget.SpsMiniScreenHotWeapons) ||
                         IsTargetFaultHighlighted(ReviewTarget.SpsMiniScreenCargoPower);

        Color frameCol = miniFault
            ? GetFaultPulseColor()
            : (hasActiveAnomaly ? Palette.SpsWeaponsHotOrange : (_spsEnabled ? Palette.SpsCommsActive : Palette.WeatheredGunmetalEdge));

        Raylib.DrawRectangle(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, frameCol);
        if (miniFault)
        {
            Raylib.DrawRectangleLines(SpsMiniX - 1, SpsMiniY - 1, SpsMiniW + 2, SpsMiniH + 2, frameCol);
        }

        Raylib.DrawRectangle(SpsMiniX + 1, SpsMiniY + 1, SpsMiniW - 2, 16, Palette.OutlineVoid);
        Raylib.DrawLine(SpsMiniX + 1, SpsMiniY + 17, SpsMiniX + SpsMiniW - 1, SpsMiniY + 17, Palette.WeatheredGunmetalEdge);
        const string logHeader = "SPS LOG";
        Raylib.DrawText(logHeader, SpsMiniX + (SpsMiniW - Raylib.MeasureText(logHeader, 10)) / 2, SpsMiniY + 4, 10,
            _spsEnabled || hasLogged ? Palette.SpsCommsActive : Palette.CrtAmberDimmed);

        if (hasLogged)
        {
            bool inReviewMode = IsProtocolReviewUnlocked && _protocolReviewModeActive && IsShipInspectableForReview;
            int maxRows = Math.Min(3, _spsLoggedAnomalies.Count);
            for (int i = 0; i < maxRows; i++)
            {
                SpsAnomalyEntry entry = _spsLoggedAnomalies[i];
                int ry = SpsMiniY + 24 + i * 36;
                int rx = SpsMiniX + 4;
                int rw = SpsMiniW - 8;
                const int rh = 30;

                if (entry.IsCooled)
                {
                    Raylib.DrawRectangle(rx, ry, rw, rh, Palette.OutlineVoid);
                    Raylib.DrawRectangleLines(rx, ry, rw, rh, Palette.WeatheredGunmetalEdge);
                    Raylib.DrawText("[OK] COOLED", rx + 6, ry + 4, 10, Palette.ClearanceTeal);
                    Raylib.DrawText("WPN OFFLINE", rx + 6, ry + 16, 10, Palette.WeatheredGunmetalEdge);
                    continue;
                }

                ReviewTarget rowTarget = entry.Kind == SpsAnomalyKind.HotWeaponMount
                    ? ReviewTarget.SpsMiniScreenHotWeapons
                    : ReviewTarget.SpsMiniScreenCargoPower;
                bool rowFault = IsTargetFaultHighlighted(rowTarget);
                bool rowSelected = inReviewMode && _reviewFirstSelection == rowTarget;

                Color rowBg = rowFault
                    ? Palette.BrownDwarfGlowDeep
                    : (rowSelected ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
                Color rowBorder = rowFault
                    ? GetFaultPulseColor()
                    : (entry.WasVerified ? Palette.ClearanceTeal : (rowSelected ? Palette.ClearanceTeal : entry.AccentColor));
                Color topFg = rowFault
                    ? GetFaultPulseColor()
                    : (entry.WasVerified ? Palette.ClearanceTeal : entry.AccentColor);

                string topLabel = entry.WasVerified
                    ? "[VERIFIED]"
                    : (entry.Kind == SpsAnomalyKind.HotWeaponMount ? "[!] HOT WPN" : "[!] CARGO");

                Raylib.DrawRectangle(rx, ry, rw, rh, rowBg);
                Raylib.DrawRectangleLines(rx, ry, rw, rh, rowBorder);
                if (inReviewMode && !rowFault)
                {
                    DrawReviewHotspotOutline(mouse, rx, ry, rw, rh, rowSelected);
                }

                Raylib.DrawText(TruncateByPixelWidth(topLabel, 10, rw - 8), rx + 5, ry + 4, 10, topFg);
                Raylib.DrawText(TruncateByPixelWidth(entry.Label, 10, rw - 8), rx + 5, ry + 16, 10, Palette.CrtAmberPrimary);
            }

            if (locking)
            {
                int maxBarW = SpsMiniW - 12;
                int fillW = Math.Clamp((int)MathF.Round(maxBarW * _spsLockProgress), 1, maxBarW);
                Raylib.DrawRectangle(SpsMiniX + 6, SpsMiniY + SpsMiniH - 12, maxBarW, 6, Palette.OutlineVoid);
                Raylib.DrawRectangle(SpsMiniX + 6, SpsMiniY + SpsMiniH - 12, fillW, 6, Palette.SpsCommsActive);
                Raylib.DrawRectangleLines(SpsMiniX + 6, SpsMiniY + SpsMiniH - 12, maxBarW, 6, Palette.SpsCommsActive);
            }
        }
        else if (locking)
        {
            const string lockTxt = "LOCKING...";
            Raylib.DrawText(lockTxt, SpsMiniX + (SpsMiniW - Raylib.MeasureText(lockTxt, 10)) / 2, SpsMiniY + 46, 10, Palette.CrtAmberPrimary);
            int maxBarW = SpsMiniW - 16;
            int fillW = Math.Clamp((int)MathF.Round(maxBarW * _spsLockProgress), 1, maxBarW);
            Raylib.DrawRectangle(SpsMiniX + 8, SpsMiniY + 64, maxBarW, 10, Palette.OutlineVoid);
            Raylib.DrawRectangle(SpsMiniX + 8, SpsMiniY + 64, fillW, 10, Palette.SpsCommsActive);
            Raylib.DrawRectangleLines(SpsMiniX + 8, SpsMiniY + 64, maxBarW, 10, Palette.SpsCommsActive);
        }
        else if (_spsEnabled)
        {
            if (_activeShip != null && _activeShip.GroundTruth.ActualShieldsUp)
            {
                const string s1 = "SHIELD";
                const string s2 = "BLOCKED";
                Raylib.DrawText(s1, SpsMiniX + (SpsMiniW - Raylib.MeasureText(s1, 10)) / 2, SpsMiniY + 48, 10, Palette.SpsShieldArcIce);
                Raylib.DrawText(s2, SpsMiniX + (SpsMiniW - Raylib.MeasureText(s2, 10)) / 2, SpsMiniY + 62, 10, Palette.SpsShieldArcIce);
            }
            else
            {
                const string c1 = "SCANNING";
                const string c2 = "HOLD 0.5S";
                const string c3 = "ON SIGNAL";
                Raylib.DrawText(c1, SpsMiniX + (SpsMiniW - Raylib.MeasureText(c1, 10)) / 2, SpsMiniY + 48, 10, Palette.SpsCommsActive);
                Raylib.DrawText(c2, SpsMiniX + (SpsMiniW - Raylib.MeasureText(c2, 10)) / 2, SpsMiniY + 64, 10, Palette.CrtAmberDimmed);
                Raylib.DrawText(c3, SpsMiniX + (SpsMiniW - Raylib.MeasureText(c3, 10)) / 2, SpsMiniY + 78, 10, Palette.CrtAmberDimmed);
            }
        }
        else
        {
            const string st = "STANDBY";
            Raylib.DrawText(st, SpsMiniX + (SpsMiniW - Raylib.MeasureText(st, 10)) / 2, SpsMiniY + 68, 10, Palette.WeatheredGunmetalEdge);
        }
    }

    // =========================================================================
    // 5. Hailing Viewscreen & Transponder ID: (688, 40, 216, 216)
    // =========================================================================
    private void DrawHailingViewscreenAndTransponder(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(ViewscreenX, ViewscreenY, ViewscreenW, ViewscreenH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ViewscreenX, ViewscreenY, ViewscreenW, ViewscreenH, Palette.OxidizedBronzeWorn);

        // Portrait Frame: 128x128 at (732, 48)
        Raylib.DrawRectangle(PortraitBoxX, PortraitBoxY, PortraitSize, PortraitSize, Palette.DeepSpaceVoid);

        bool viewscreenOpen = _activeShip != null &&
                              _phase != EncounterPhase.ShipGlidingIn &&
                              _cachedLivePortraitTex.Id != 0;

        if (_preShiftStage == PreShiftStage.Day3KordaniLiveHail && _cachedPreShiftKordaniPortraitTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(PortraitBoxX, PortraitBoxY, PortraitSize, PortraitSize);
            Raylib.DrawTexturePro(_cachedPreShiftKordaniPortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
        }
        else if (viewscreenOpen)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(PortraitBoxX, PortraitBoxY, PortraitSize, PortraitSize);
            Raylib.DrawTexturePro(_cachedLivePortraitTex, src, dst, Vector2.Zero, 0f, Color.White);

            // Brief static flicker on captain's viewscreen during staggered confrontation reaction pause
            if (_portraitStaticFlickerTimer > 0f)
            {
                DrawPortraitStaticFlickerOverlay(PortraitBoxX, PortraitBoxY, PortraitSize, PortraitSize);
            }
        }
        else
        {
            const string s1 = "VIEWSCREEN";
            const string s2 = "STANDBY";
            Raylib.DrawText(s1, PortraitBoxX + (PortraitSize - Raylib.MeasureText(s1, 10)) / 2, PortraitBoxY + 52, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText(s2, PortraitBoxX + (PortraitSize - Raylib.MeasureText(s2, 10)) / 2, PortraitBoxY + 68, 10, Palette.WeatheredGunmetalEdge);
        }

        Raylib.DrawRectangleLines(PortraitBoxX - 1, PortraitBoxY - 1, PortraitSize + 2, PortraitSize + 2,
            _preShiftStage == PreShiftStage.Day3KordaniLiveHail ? Palette.AlertCrimson : Palette.CrtAmberDimmed);

        // LIVE FEED / AUDIO ONLY & Raw TRANSPONDER ID Box: (696, 184, 200, 64)
        bool idFault = IsTargetFaultHighlighted(ReviewTarget.VisualSignalTransponderId);
        bool idSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.VisualSignalTransponderId;

        Raylib.DrawRectangle(FeedBoxX, FeedBoxY, FeedBoxW, FeedBoxH,
            idFault ? Palette.BrownDwarfGlowDeep : (idSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid));
        Raylib.DrawRectangleLines(FeedBoxX, FeedBoxY, FeedBoxW, FeedBoxH,
            idFault ? GetFaultPulseColor() : (idSelected ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge));

        if (idFault)
        {
            Raylib.DrawRectangleLines(FeedBoxX - 1, FeedBoxY - 1, FeedBoxW + 2, FeedBoxH + 2, GetFaultPulseColor());
        }
        else if (_protocolReviewModeActive && _activeShip != null && IsShipInspectableForReview)
        {
            DrawReviewHotspotOutline(mouse, FeedBoxX, FeedBoxY, FeedBoxW, FeedBoxH, idSelected);
        }

        // Feed status row
        string feedLine;
        Color feedDotCol;
        if (_preShiftStage == PreShiftStage.Day3KordaniLiveHail)
        {
            feedLine = "LIVE FEED // KDF SECURITY";
            feedDotCol = Palette.AlertCrimson;
        }
        else if (_activeShip == null || _phase == EncounterPhase.ShipGlidingIn)
        {
            feedLine = "SIGNAL: STANDBY";
            feedDotCol = Palette.WeatheredGunmetalEdge;
        }
        else if (_activeShip.GroundTruth.UsesStaticViewscreen)
        {
            feedLine = "AUDIO ONLY";
            feedDotCol = Palette.CrtAmberDimmed;
        }
        else
        {
            feedLine = "LIVE FEED";
            feedDotCol = Palette.ClearanceTeal;
        }

        Raylib.DrawRectangle(FeedBoxX + 8, FeedBoxY + 8, 6, 6, feedDotCol);
        Raylib.DrawText(TruncateByPixelWidth(feedLine, 10, FeedBoxW - 24), FeedBoxX + 18, FeedBoxY + 6, 10,
            _preShiftStage == PreShiftStage.Day3KordaniLiveHail ? Palette.AlertCrimson : Palette.CrtAmberDimmed);
        Raylib.DrawLine(FeedBoxX + 4, FeedBoxY + 21, FeedBoxX + FeedBoxW - 4, FeedBoxY + 21, Palette.WeatheredGunmetalEdge);

        // Raw Local Sensor Transponder ID (Prominent & crisp!)
        Raylib.DrawText("TRANSPONDER ID", FeedBoxX + 8, FeedBoxY + 25, 10, Palette.WeatheredGunmetalEdge);

        string rawIdText = _preShiftStage == PreShiftStage.Day3KordaniLiveHail
            ? Day2And3ScriptedDialogue.Day3KordaniOfficerTransponder
            : (_activeShip != null && _phase != EncounterPhase.ShipGlidingIn
                ? _activeShip.GroundTruth.RawTransponderId
                : "---");

        Color idCol = idFault
            ? GetFaultPulseColor()
            : (idSelected ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

        int idFontSize = Raylib.MeasureText(rawIdText, 20) <= FeedBoxW - 16 ? 20 : 10;
        int idDrawY = idFontSize == 20 ? FeedBoxY + 39 : FeedBoxY + 44;
        Raylib.DrawText(rawIdText, FeedBoxX + 8, idDrawY, idFontSize, idCol);
    }

    private void DrawPortraitStaticFlickerOverlay(int x, int y, int w, int h)
    {
        for (int sy = y; sy < y + h; sy += 4)
        {
            uint hash = DeterministicRng.HashSeedFnv1a($"flicker_{_animTick}_{sy}");
            if ((hash % 3) == 0)
            {
                int barLen = (int)(20 + (hash % (uint)(w - 24)));
                int barX = x + (int)((hash >> 8) % (uint)Math.Max(1, w - barLen));
                Color c = (hash % 2 == 0) ? new Color(226, 232, 240, 150) : new Color(255, 184, 77, 130);
                Raylib.DrawRectangle(barX, sy, barLen, 2, c);
            }
        }
    }

    // =========================================================================
    // 6. CRT Comms Dialogue Log: (912, 40, 360, 216) + 0.5s PROTOCOL FAULT Flash
    // =========================================================================
    private void DrawCrtCommsDialogueLog(VirtualMouseState mouse)
    {
        bool faultBeatActive = _faultStage == FaultSequenceStage.FaultBeat;

        Raylib.DrawRectangle(TranscriptX, TranscriptY, TranscriptW, TranscriptH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(TranscriptX, TranscriptY, TranscriptW, TranscriptH,
            faultBeatActive ? GetFaultPulseColor() : Palette.OxidizedBronzeWorn);

        // Header bar
        Raylib.DrawRectangle(TranscriptX + 1, TranscriptY + 1, TranscriptW - 2, 16, Palette.OutlineVoid);
        Raylib.DrawLine(TranscriptX + 1, TranscriptY + 17, TranscriptX + TranscriptW - 1, TranscriptY + 17, Palette.WeatheredGunmetalEdge);
        string channelTitle = _preShiftStage == PreShiftStage.Day3KordaniLiveHail
            ? "INTERNAL COMMS // KORDANI SECURITY"
            : "CRT COMMS DIALOGUE LOG";
        Raylib.DrawText(TruncateByPixelWidth(channelTitle, 10, TranscriptW - 14), TranscriptX + 8, TranscriptY + 4, 10,
            _preShiftStage == PreShiftStage.Day3KordaniLiveHail ? Palette.AlertCrimson : Palette.CrtAmberPrimary);

        List<(string Line, Color Col)> wrapped = WrapTranscriptLinesByPixelWidth(TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);
        if (_transcriptAutoFollowBottom)
        {
            _transcriptScrollTopLine = maxTop;
        }
        else
        {
            _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine, 0, maxTop);
        }

        int drawY = TranscriptY + 22;
        int endIdx = Math.Min(wrapped.Count, _transcriptScrollTopLine + TranscriptVisibleRows);
        for (int i = _transcriptScrollTopLine; i < endIdx; i++)
        {
            Raylib.DrawText(wrapped[i].Line, TranscriptX + 8, drawY, 10, wrapped[i].Col);
            drawY += 13;
        }

        DrawThematicCrtScrollbar(mouse, wrapped.Count, maxTop);

        // 0.5-Second PROTOCOL FAULT CRT Oscilloscope Flash Overlay
        if (faultBeatActive)
        {
            DrawProtocolFaultOscilloscopeFlash();
        }
    }

    private void DrawProtocolFaultOscilloscopeFlash()
    {
        int ox = TranscriptX + 12;
        int oy = TranscriptY + 72;
        int ow = TranscriptW - ScrollbarW - 24;
        int oh = 74;
        Color pulseCol = GetFaultPulseColor();

        Raylib.DrawRectangle(ox, oy, ow, oh, new Color(16, 10, 12, 238));
        Raylib.DrawRectangleLines(ox, oy, ow, oh, pulseCol);
        Raylib.DrawRectangleLines(ox + 2, oy + 2, ow - 4, oh - 4, Palette.AlertCrimson);

        // Jagged CRT Oscilloscope fault waveform
        int midY = oy + 46;
        int prevY = midY;
        for (int px = 0; px < ow - 16; px += 4)
        {
            float spike = MathF.Sin((px + _animTick * 11) * 0.25f) * 14f +
                          HashIntToSignedFloat(px + _animTick * 7) * 9f;
            int curY = Math.Clamp(midY + (int)MathF.Round(spike), oy + 26, oy + oh - 6);
            if (px > 0)
            {
                Raylib.DrawLine(ox + 8 + px - 4, prevY, ox + 8 + px, curY, pulseCol);
            }
            prevY = curY;
        }

        // Bold crisp "PROTOCOL FAULT" flash header
        Raylib.DrawRectangle(ox + 4, oy + 4, ow - 8, 20, pulseCol);
        const string faultStr = "PROTOCOL FAULT";
        int fw = Raylib.MeasureText(faultStr, 10);
        Raylib.DrawText(faultStr, ox + (ow - fw) / 2, oy + 9, 10, Palette.OutlineVoid);
    }

    private void DrawThematicCrtScrollbar(VirtualMouseState mouse, int totalLines, int maxTop)
    {
        int barX = TranscriptX + TranscriptW - ScrollbarW - 2;
        int upY = TranscriptY + 18;
        int downY = TranscriptY + TranscriptH - 18;
        int trackTopY = upY + 18;
        int trackH = (downY - 2) - trackTopY;

        // Top Up-Arrow pixel-art amber button (w = 16)
        bool upHover = mouse.IsHit(barX, upY, ScrollbarW, 16);
        Raylib.DrawRectangle(barX, upY, ScrollbarW, 16, upHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(barX, upY, ScrollbarW, 16, upHover ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
        Color upCol = _transcriptScrollTopLine > 0 ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawPixel(barX + 8, upY + 5, upCol);
        Raylib.DrawLine(barX + 7, upY + 6, barX + 9, upY + 6, upCol);
        Raylib.DrawLine(barX + 6, upY + 7, barX + 10, upY + 7, upCol);
        Raylib.DrawLine(barX + 5, upY + 8, barX + 11, upY + 8, upCol);
        Raylib.DrawLine(barX + 4, upY + 9, barX + 12, upY + 9, upCol);

        // Segmented CRT phosphor track
        Raylib.DrawRectangle(barX + 2, trackTopY, ScrollbarW - 4, trackH, Palette.OutlineVoid);
        for (int sy = trackTopY + 2; sy < trackTopY + trackH - 2; sy += 4)
        {
            Raylib.DrawLine(barX + 4, sy, barX + ScrollbarW - 5, sy, Palette.OxidizedBronzeShadow);
        }

        if (totalLines > TranscriptVisibleRows && maxTop > 0)
        {
            float ratio = (float)TranscriptVisibleRows / totalLines;
            int thumbH = Math.Clamp((int)MathF.Round(trackH * ratio), 14, trackH - 4);
            float scrollFraction = (float)_transcriptScrollTopLine / maxTop;
            int thumbY = trackTopY + 2 + (int)MathF.Round(scrollFraction * (trackH - thumbH - 4));
            Raylib.DrawRectangle(barX + 3, thumbY, ScrollbarW - 6, thumbH, Palette.CrtAmberPrimary);
        }
        else
        {
            Raylib.DrawRectangle(barX + 4, trackTopY + 2, ScrollbarW - 8, trackH - 4, Palette.CrtAmberDimmed);
        }

        // Bottom Down-Arrow pixel-art amber button (w = 16)
        bool downHover = mouse.IsHit(barX, downY, ScrollbarW, 16);
        Raylib.DrawRectangle(barX, downY, ScrollbarW, 16, downHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(barX, downY, ScrollbarW, 16, downHover ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
        Color downCol = _transcriptScrollTopLine < maxTop ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawLine(barX + 4, downY + 6, barX + 12, downY + 6, downCol);
        Raylib.DrawLine(barX + 5, downY + 7, barX + 11, downY + 7, downCol);
        Raylib.DrawLine(barX + 6, downY + 8, barX + 10, downY + 8, downCol);
        Raylib.DrawLine(barX + 7, downY + 9, barX + 9, downY + 9, downCol);
        Raylib.DrawPixel(barX + 8, downY + 10, downCol);
    }

    // =========================================================================
    // 7. Lower Desk Surface, One-Way Data Conduit Cable & Subtle Lap Pocket
    // =========================================================================
    private void DrawLowerDeskSurfaceAndConduit()
    {
        // Flat top-down dark industrial gunmetal desk mat
        Raylib.DrawRectangle(0, 262, Constants.VirtualWidth, Constants.VirtualHeight - 262, new Color(30, 36, 44, 255));

        // Subtle recessed desk mat seams aligned with strict 8px gutters (y = 356, x = 1068)
        Raylib.DrawLine(8, 356, 1064, 356, new Color(22, 27, 34, 255));
        Raylib.DrawLine(1068, 356, 1068, 712, new Color(22, 27, 34, 255));

        // Contraband Lap Pocket: (8, 656, 280, 56) -- subtle dark unlit beveled lip flush with x = 8 and y = 712
        Raylib.DrawRectangle(LapPocketX, LapPocketY, LapPocketW, LapPocketH, new Color(18, 22, 28, 255));
        Raylib.DrawRectangle(LapPocketX + 3, LapPocketY + 6, LapPocketW - 6, LapPocketH - 6, new Color(12, 15, 20, 255));
        Raylib.DrawRectangle(LapPocketX + 6, LapPocketY + 14, LapPocketW - 12, LapPocketH - 14, Palette.OutlineVoid);
        // Subtle unlit beveled top & side lip
        Raylib.DrawLine(LapPocketX, LapPocketY, LapPocketX + LapPocketW - 1, LapPocketY, new Color(46, 54, 65, 255));
        Raylib.DrawLine(LapPocketX, LapPocketY + 1, LapPocketX + LapPocketW - 1, LapPocketY + 1, Palette.OutlineVoid);
        Raylib.DrawLine(LapPocketX, LapPocketY, LapPocketX, LapPocketY + LapPocketH - 1, new Color(36, 43, 52, 255));
        Raylib.DrawLine(LapPocketX + LapPocketW - 1, LapPocketY, LapPocketX + LapPocketW - 1, LapPocketY + LapPocketH - 1, new Color(14, 17, 22, 255));
        Raylib.DrawLine(LapPocketX, LapPocketY + LapPocketH - 1, LapPocketX + LapPocketW - 1, LapPocketY + LapPocketH - 1, new Color(14, 17, 22, 255));

        // Strictly One-Way Armored Data Conduit Cable: Slate Bus right edge (900, 286) -> top of Wafer Writer (894, 624)
        DrawArmoredDataConduitCable();
    }

    private void DrawArmoredDataConduitCable()
    {
        // Strictly One-Way Receive-Only Conduit (Slate Bus -> Wafer Writer):
        // - Inactive State (Days 1-6 / Wafer Writer offline): completely dark & inert, NEVER animates pulses!
        // - Active State (Day 7+): pulses ONLY downward during incoming protocol download (RX PACKET),
        //   NEVER during Clearance Dock encryption, Slate Bus TX CLEARANCE upload, or Detaining.
        bool waferOnline = IsWaferWriterOperational;
        bool pulseRxActive = waferOnline && _phase == EncounterPhase.DownloadingProtocols;

        // 1. Outer dark armored housing (6px wide) routed cleanly through 8px gutter x = 904
        // Stub out of Slate Bus right edge (900..904 at y = 286)
        Raylib.DrawRectangle(ConduitStartX, ConduitStartY - 3, ConduitGutterX - ConduitStartX + 3, 6, Palette.OutlineVoid);
        // Vertical drop along x = 904 (y = 286..604)
        Raylib.DrawRectangle(ConduitGutterX - 3, ConduitStartY - 3, 6, ConduitBendY - ConduitStartY + 6, Palette.OutlineVoid);
        // Horizontal jog left at y = 604 (x = 894..904)
        Raylib.DrawRectangle(ConduitEndX - 3, ConduitBendY - 3, ConduitGutterX - ConduitEndX + 6, 6, Palette.OutlineVoid);
        // Short vertical entry into top-right shoulder of Wafer Writer (x = 894, y = 604..624)
        Raylib.DrawRectangle(ConduitEndX - 3, ConduitBendY - 3, 6, ConduitEndY - ConduitBendY + 3, Palette.OutlineVoid);

        // 2. Inner weathered steel braiding (4px wide)
        Color sheathCol = waferOnline ? new Color(46, 55, 66, 255) : new Color(32, 38, 46, 255);
        Raylib.DrawRectangle(ConduitStartX, ConduitStartY - 2, ConduitGutterX - ConduitStartX + 2, 4, sheathCol);
        Raylib.DrawRectangle(ConduitGutterX - 2, ConduitStartY - 2, 4, ConduitBendY - ConduitStartY + 4, sheathCol);
        Raylib.DrawRectangle(ConduitEndX - 2, ConduitBendY - 2, ConduitGutterX - ConduitEndX + 4, 4, sheathCol);
        Raylib.DrawRectangle(ConduitEndX - 2, ConduitBendY - 2, 4, ConduitEndY - ConduitBendY + 2, sheathCol);

        // 3. Center optical channel (2px) -- completely dark/inert when Wafer Writer is offline (Days 1-6)
        Color fiberCore = waferOnline ? new Color(48, 36, 22, 255) : new Color(16, 20, 26, 255);
        Raylib.DrawRectangle(ConduitStartX, ConduitStartY - 1, ConduitGutterX - ConduitStartX + 1, 2, fiberCore);
        Raylib.DrawRectangle(ConduitGutterX - 1, ConduitStartY - 1, 2, ConduitBendY - ConduitStartY + 2, fiberCore);
        Raylib.DrawRectangle(ConduitEndX - 1, ConduitBendY - 1, ConduitGutterX - ConduitEndX + 2, 2, fiberCore);
        Raylib.DrawRectangle(ConduitEndX - 1, ConduitBendY - 1, 2, ConduitEndY - ConduitBendY + 1, fiberCore);

        if (pulseRxActive)
        {
            // One-way downward RX PACKET pulses flowing strictly from Slate Bus -> Wafer Writer
            int segOffset = (_animTick * 3) % 24;
            Raylib.DrawRectangle(ConduitStartX, ConduitStartY - 1, ConduitGutterX - ConduitStartX + 1, 2, Palette.CrtAmberPrimary);
            for (int y = ConduitStartY + segOffset; y < ConduitBendY; y += 24)
            {
                int h = Math.Min(10, ConduitBendY - y);
                Raylib.DrawRectangle(ConduitGutterX - 1, y, 2, h, Palette.CrtAmberPrimary);
            }
            if ((segOffset % 24) < 12)
            {
                Raylib.DrawRectangle(ConduitEndX - 1, ConduitBendY - 1, ConduitGutterX - ConduitEndX + 2, 2, Palette.CrtAmberPrimary);
            }
            for (int y = ConduitBendY + (segOffset % 14); y < ConduitEndY; y += 14)
            {
                int h = Math.Min(7, ConduitEndY - y);
                Raylib.DrawRectangle(ConduitEndX - 1, y, 2, h, Palette.CrtAmberPrimary);
            }
        }

        // Desk surface steel mounting clamps & intake grommets
        Color clampCol = waferOnline ? Palette.WeatheredGunmetalEdge : new Color(46, 54, 64, 255);
        Raylib.DrawRectangle(ConduitStartX, ConduitStartY - 4, 3, 8, clampCol);
        Raylib.DrawRectangle(ConduitEndX - 4, ConduitEndY - 4, 8, 4, clampCol);
        for (int cy = 368; cy < ConduitBendY - 24; cy += 68)
        {
            Raylib.DrawRectangle(ConduitGutterX - 4, cy, 8, 4, clampCol);
            Raylib.DrawRectangleLines(ConduitGutterX - 4, cy, 8, 4, Palette.OutlineVoid);
        }
    }

    // =========================================================================
    // 7b. Wafer Writer (Bottom-Center): (680, 624, 220, 88)
    //     Visible Wafer Wipe Feed Slot along TOP edge: (692, 624, 196, 16)
    // =========================================================================
    private void DrawWaferWriterModule()
    {
        if (!IsWaferWriterOperational)
        {
            DrawBrushedMetalFillerPlate(WaferWriterX, WaferWriterY, WaferWriterW, WaferWriterH);
        }
        else
        {
            Raylib.DrawRectangle(WaferWriterX, WaferWriterY, WaferWriterW, WaferWriterH, Palette.OutlineVoid);
            Raylib.DrawRectangle(WaferWriterX + 1, WaferWriterY + 1, WaferWriterW - 2, WaferWriterH - 2, Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(WaferWriterX, WaferWriterY, WaferWriterW, WaferWriterH, Palette.OxidizedBronzeWorn);
        }

        // Visible Wafer Wipe Feed Slot along TOP edge: (692, 624, 196, 16)
        Raylib.DrawRectangle(WaferFeedSlotX, WaferFeedSlotY, WaferFeedSlotW, WaferFeedSlotH, new Color(18, 22, 28, 255));
        Raylib.DrawRectangleLines(WaferFeedSlotX, WaferFeedSlotY, WaferFeedSlotW, WaferFeedSlotH,
            IsWaferWriterOperational ? Palette.OxidizedBronzeWorn : new Color(48, 56, 68, 255));
        // Recessed physical wipe intake slit + side bevel guides
        Raylib.DrawRectangle(WaferFeedSlotX + 10, WaferFeedSlotY + 5, WaferFeedSlotW - 20, 6, Palette.OutlineVoid);
        Raylib.DrawLine(WaferFeedSlotX + 10, WaferFeedSlotY + 11, WaferFeedSlotX + WaferFeedSlotW - 11, WaferFeedSlotY + 11,
            IsWaferWriterOperational ? Palette.CrtAmberDimmed : new Color(42, 49, 60, 255));
        Raylib.DrawRectangle(WaferFeedSlotX + 4, WaferFeedSlotY + 6, 3, 4, new Color(48, 56, 68, 255));
        Raylib.DrawRectangle(WaferFeedSlotX + WaferFeedSlotW - 7, WaferFeedSlotY + 6, 3, 4, new Color(48, 56, 68, 255));

        if (!IsWaferWriterOperational)
        {
            // Subtle stencil label + [PLAYTEST] badge cleanly centered below the top Wafer Wipe Feed Slot
            DrawInactiveStencilText(WaferWriterX, WaferWriterY + WaferFeedSlotH, WaferWriterW, WaferWriterH - WaferFeedSlotH,
                "WAFER WRITER", null);
        }
        else
        {
            bool rxActive = _phase == EncounterPhase.DownloadingProtocols;
            const string title = "WAFER WRITER // RX DEMUX";
            Raylib.DrawText(title, WaferWriterX + (WaferWriterW - Raylib.MeasureText(title, 10)) / 2, WaferWriterY + 28, 10, Palette.CrtAmberPrimary);
            string st = rxActive ? "DEMUXING CERT-WAFERS..." : "WIPE FEED READY";
            Raylib.DrawText(st, WaferWriterX + (WaferWriterW - Raylib.MeasureText(st, 10)) / 2, WaferWriterY + 48, 10,
                rxActive ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        }
    }

    // =========================================================================
    // 8. Row 2 Hardware: Warp Printer (112, 264, 320, 88) & Glowing Warp Checker (440, 264, 232, 88)
    // =========================================================================
    private void DrawWarpResiduePrinterAndRefSpot(VirtualMouseState mouse)
    {
        if (!IsSpectrometerOperational)
        {
            // Days 1-2: Recessed inactive bays with subtle low-contrast [PLAYTEST] stencils
            DrawInactiveHardwareBay(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH, "WARP PRINTER", "DAY 03");
            DrawInactiveHardwareBay(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH, "WARP CHECKER", "DAY 03");
            return;
        }

        // Day 3+: Warp Residue Printer + L->R Tray: (112, 264, 320, 88)
        Raylib.DrawRectangle(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH, Palette.ClearanceTeal);

        // Left Printer Housing: (112, 264, 88, 88)
        Raylib.DrawRectangle(WarpPrinterX + 1, WarpPrinterY + 1, WarpHousingW - 2, WarpPrinterH - 2, Palette.WeatheredGunmetalMid);
        Raylib.DrawLine(WarpPrinterX + WarpHousingW - 1, WarpPrinterY + 1, WarpPrinterX + WarpHousingW - 1, WarpPrinterY + WarpPrinterH - 2, Palette.OutlineVoid);
        const string prnTitle = "WARP COIL";
        Raylib.DrawText(prnTitle, WarpPrinterX + (WarpHousingW - Raylib.MeasureText(prnTitle, 10)) / 2, WarpPrinterY + 5, 10, Palette.ClearanceTeal);

        // Compact status screen inside left housing (80x32)
        int scrX = WarpPrinterX + 4;
        int scrY = WarpPrinterY + 18;
        int scrW = WarpHousingW - 8;
        const int scrH = 34;
        Raylib.DrawRectangle(scrX, scrY, scrW, scrH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(scrX, scrY, scrW, scrH, Palette.CrtAmberDimmed);

        if (_warpPhase == WarpPrinterPhase.ScanningWarpField)
        {
            float sweep = Math.Clamp(_warpPhaseElapsed / WarpScanDurationSeconds, 0f, 1f);
            int barW = Math.Max(1, (int)MathF.Round((scrW - 8) * sweep));
            Raylib.DrawRectangle(scrX + 4, scrY + 6, barW, 6, Palette.ClearanceTeal);
            Raylib.DrawText("SCANNING", scrX + 12, scrY + 18, 10, Palette.ClearanceTeal);
        }
        else if (_warpPhase == WarpPrinterPhase.PrintingStrip)
        {
            Raylib.DrawText("PRINTING", scrX + 12, scrY + 6, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText("L -> R", scrX + 20, scrY + 19, 10, Palette.CrtAmberDimmed);
        }
        else if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear)
        {
            Raylib.DrawText("READY", scrX + 20, scrY + 6, 10, Palette.ClearanceTeal);
            Raylib.DrawText("TEAR ->", scrX + 14, scrY + 19, 10, Palette.CrtAmberPrimary);
        }
        else if (_warpPhase == WarpPrinterPhase.TornOffFreeDocument)
        {
            Raylib.DrawText("COIL HOT", scrX + 12, scrY + 6, 10, Palette.ClearanceTeal);
            Raylib.DrawText("STRIP OUT", scrX + 8, scrY + 19, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            Raylib.DrawText("ONLINE", scrX + 18, scrY + 6, 10, Palette.ClearanceTeal);
            Raylib.DrawText("READY", scrX + 20, scrY + 19, 10, Palette.CrtAmberDimmed);
        }

        bool canScan = CanStartManualWarpScan();
        string scanLabel = _warpPhase switch
        {
            WarpPrinterPhase.ScanningWarpField => "SCAN...",
            WarpPrinterPhase.PrintingStrip => "PRINT...",
            WarpPrinterPhase.AnchoredReadyToTear => "TEAR ->",
            WarpPrinterPhase.TornOffFreeDocument => "SCANNED",
            _ => "SCAN WARP"
        };
        DrawHardwareButton(mouse, WarpPrinterX + 4, WarpPrinterY + 56, WarpHousingW - 8, 26, scanLabel, canScan, canScan, Palette.ClearanceTeal);

        // Right L->R Extrusion Tray: (204, 276, 224, 64)
        Raylib.DrawRectangle(WarpTrayX, WarpTrayY, WarpTrayW, WarpTrayH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(WarpTrayX, WarpTrayY, WarpTrayW, WarpTrayH, Palette.WeatheredGunmetalEdge);
        // Vertical extrusion mouth slit on left side of tray
        Raylib.DrawRectangle(WarpTrayX - 3, WarpTrayY + 2, 4, WarpTrayH - 4, Palette.OutlineVoid);
        Raylib.DrawLine(WarpTrayX + 1, WarpTrayY + 2, WarpTrayX + 1, WarpTrayY + WarpTrayH - 3, Palette.OxidizedBronzeWorn);

        // Day 3+: Glowing Warp Reference Spot (Checker): (440, 264, 232, 88)
        Raylib.DrawRectangle(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH, new Color(24, 20, 14, 255));
        Raylib.DrawRectangleLines(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(WarpRefSpotX + 2, WarpRefSpotY + 2, WarpRefSpotW - 4, WarpRefSpotH - 4, Palette.OxidizedBronzeWorn);

        // Selector Bar: (448, 270, 216, 22)
        StarSystemSignatureInfo sys = WarpSignatureGenerator.SectorSystems[
            Math.Clamp(_warpRefSpotSystemIndex, 0, WarpSignatureGenerator.SectorSystems.Length - 1)];
        DrawSmallButton(mouse, WarpSelectorX, WarpSelectorY, 24, WarpSelectorH, "<");
        Raylib.DrawRectangle(WarpSelectorX + 26, WarpSelectorY, WarpSelectorW - 52, WarpSelectorH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(WarpSelectorX + 26, WarpSelectorY, WarpSelectorW - 52, WarpSelectorH, Palette.OxidizedBronzeShadow);
        string sysTitle = $"REF: {sys.DisplayLabel}";
        int stw = Raylib.MeasureText(sysTitle, 10);
        Raylib.DrawText(sysTitle, WarpSelectorX + (WarpSelectorW - stw) / 2, WarpSelectorY + (WarpSelectorH - 10) / 2, 10,
            sys.IsRestricted ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
        DrawSmallButton(mouse, WarpSelectorX + WarpSelectorW - 24, WarpSelectorY, 24, WarpSelectorH, ">");

        // Backlit Waveform Glass Window: EXACT (448, 294, 216, 52)
        Raylib.DrawRectangle(WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH, new Color(50, 36, 15, 255));
        // Subtle vertical calibration reticle lines on backlit glass
        Color gridCol = new(74, 54, 22, 255);
        Raylib.DrawLine(WarpGlassX + 1, WarpGlassY + WarpGlassH / 2, WarpGlassX + WarpGlassW - 2, WarpGlassY + WarpGlassH / 2, gridCol);
        for (int gx = WarpGlassX + 36; gx < WarpGlassX + WarpGlassW; gx += 36)
        {
            Raylib.DrawLine(gx, WarpGlassY + 2, gx, WarpGlassY + WarpGlassH - 3, new Color(64, 46, 18, 255));
        }

        // Bright glowing reference waveform across the full 216x52 backlit glass
        Color refWaveCol = sys.IsRestricted ? new Color(255, 132, 84, 255) : Palette.CrtAmberPrimary;
        Color refHaloCol = sys.IsRestricted ? new Color(190, 56, 40, 120) : new Color(255, 184, 77, 100);
        DrawHarmonicWaveform(sys.Key, WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH, WarpGlassW, refWaveCol, refHaloCol);
        Raylib.DrawRectangleLines(WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH, Palette.CrtAmberDimmed);
    }

    private void DrawSlateBusSocket()
    {
        bool pulse = ((_animTick / 10) % 2) == 0;
        bool draggingSlateNearBus = _draggingItemId == DeskItemId.DataSlate &&
                                    _deskItems[DeskItemId.DataSlate].X < SlateBusX + SlateBusW + 16 &&
                                    _deskItems[DeskItemId.DataSlate].X + _deskItems[DeskItemId.DataSlate].Width > SlateBusX - 16 &&
                                    _deskItems[DeskItemId.DataSlate].Y <= SlateBusY + SlateBusH + 24;

        bool needsSlateForDownload = _phase == EncounterPhase.AwaitingPortForDownload;
        bool needsSlateForClearance = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort;
        bool isTransmittingClearance = _phase == EncounterPhase.TransmittingClearance;
        bool isDownloading = _phase == EncounterPhase.DownloadingProtocols;
        bool isAborted = _phase == EncounterPhase.DownloadAborted;

        Color borderCol = isAborted
            ? (pulse ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
            : (isTransmittingClearance || needsSlateForClearance
                ? (pulse ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                : (needsSlateForDownload || draggingSlateNearBus
                    ? Palette.CrtAmberPrimary
                    : (IsDataSlateInSlateBus ? (isDownloading ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn) : Palette.WeatheredGunmetalEdge)));

        Raylib.DrawRectangle(SlateBusX, SlateBusY, SlateBusW, SlateBusH, Palette.OutlineVoid);
        Raylib.DrawRectangle(SlateBusX + 2, SlateBusY + 2, SlateBusW - 4, SlateBusH - 6, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(SlateBusX, SlateBusY, SlateBusW, SlateBusH, borderCol);

        // Corner bolts
        Raylib.DrawRectangle(SlateBusX + 4, SlateBusY + 4, 2, 2, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(SlateBusX + SlateBusW - 6, SlateBusY + 4, 2, 2, Palette.WeatheredGunmetalEdge);

        // 6 physical contact pins along bottom lip mating cleanly with docked Data Slate (698..882)
        for (int p = 0; p < 6; p++)
        {
            int px = SlateBusDockedX + 14 + p * 28;
            Color pinCol = isAborted
                ? Palette.AlertCrimson
                : (isTransmittingClearance || isDownloading
                    ? (pulse ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                    : (IsDataSlateInSlateBus ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn));
            Raylib.DrawRectangle(px, SlateBusY + SlateBusH - 4, 12, 4, pinCol);
        }

        // Status LED
        Color ledCol = isAborted
            ? Palette.AlertCrimson
            : (isTransmittingClearance || isDownloading || needsSlateForClearance
                ? Palette.ClearanceTeal
                : (IsDataSlateInSlateBus ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));
        Raylib.DrawCircle(SlateBusX + 14, SlateBusY + 18, 4, ledCol);

        if (isTransmittingClearance)
        {
            float txProg = Math.Clamp(_clearanceTransmitElapsed / ClearanceTransmitDurationSeconds, 0f, 1f);
            int barX = SlateBusX + 26;
            int barY = SlateBusY + 10;
            int barW = SlateBusW - 36;
            const int barH = 18;
            int fillW = Math.Clamp((int)MathF.Round(barW * txProg), 1, barW);
            Raylib.DrawRectangle(barX, barY, barW, barH, Palette.DeepSpaceVoid);
            Raylib.DrawRectangle(barX, barY, fillW, barH, Palette.ClearanceTeal);
            Raylib.DrawRectangleLines(barX, barY, barW, barH, Palette.ClearanceTeal);
            string txTxt = $"TX CLEARANCE {(int)MathF.Round(txProg * 100f)}%";
            int txW = Raylib.MeasureText(txTxt, 10);
            int txtX = barX + (barW - txW) / 2;
            int txtY = barY + 4;
            Raylib.DrawText(txTxt, txtX, txtY, 10, Palette.ClearanceTeal);
            Raylib.BeginScissorMode(barX, barY, fillW, barH);
            Raylib.DrawText(txTxt, txtX, txtY, 10, Palette.OutlineVoid);
            Raylib.EndScissorMode();
            return;
        }

        string line1 = "SLATE BUS";
        string line2;
        Color line2Col;

        if (isAborted)
        {
            line2 = IsDataSlateInSlateBus ? "CLICK [RE-TRANSMIT]" : "RE-PLUG DATA SLATE";
            line2Col = IsDataSlateInSlateBus ? Palette.ClearanceTeal : Palette.AlertCrimson;
        }
        else if (needsSlateForClearance)
        {
            line2 = ">> DROP CLEARED SLATE <<";
            line2Col = Palette.ClearanceTeal;
        }
        else if (needsSlateForDownload)
        {
            line2 = ">> PLUG SLATE TO RX <<";
            line2Col = Palette.CrtAmberPrimary;
        }
        else if (isDownloading)
        {
            line2 = $"RX PACKET {(int)MathF.Round(ProtocolDownloadProgress * 100f)}%";
            line2Col = Palette.ClearanceTeal;
        }
        else if (draggingSlateNearBus)
        {
            line2 = ">> DROP TO DOCK <<";
            line2Col = Palette.CrtAmberPrimary;
        }
        else if (IsDataSlateInSlateBus)
        {
            line2 = "STATUS: SLATE LINKED";
            line2Col = Palette.CrtAmberPrimary;
        }
        else
        {
            line2 = "STATUS: UNPLUGGED";
            line2Col = Palette.CrtAmberDimmed;
        }

        Raylib.DrawText(line1, SlateBusX + 26, SlateBusY + 7, 10, Palette.CrtAmberDimmed);
        Raylib.DrawText(TruncateByPixelWidth(line2, 10, SlateBusW - 34), SlateBusX + 26, SlateBusY + 21, 10, line2Col);
    }

    private void DrawCommsCommandStrip(VirtualMouseState mouse)
    {
        // During Day 3 Live Kordani Pre-Shift Hail: dedicated single acknowledge button spanning (908, 264, 364, 44)
        if (_preShiftStage == PreShiftStage.Day3KordaniLiveHail)
        {
            bool finishedLines = _kordaniHailLineIndex >= _kordaniHailLines.Length;
            string btnTxt = finishedLines
                ? "ACKNOWLEDGE HAIL -- BEGIN DAY 03 WATCH"
                : "SKIP TRANSMISSION TEXT >>";
            DrawHardwareButton(
                mouse,
                CommsStripX, CommsStripY, CommsStripW, CommsStripH,
                btnTxt,
                enabled: true,
                highlight: true,
                activeColor: finishedLines ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
            return;
        }

        // 1. NEXT HAIL (or RE-TRANSMIT when DownloadAborted): (908, 264, 116, 44)
        if (_phase == EncounterPhase.DownloadAborted)
        {
            bool canReTx = IsDataSlateInSlateBus && _pendingReTransmitRestartTimer <= 0f;
            DrawHardwareButton(
                mouse, NextHailBtnX, NextHailBtnY, NextHailBtnW, NextHailBtnH,
                _pendingReTransmitRestartTimer > 0f ? "WAIT..." : "RE-TRANSMIT",
                enabled: canReTx,
                highlight: canReTx,
                activeColor: Palette.ClearanceTeal);
        }
        else
        {
            bool canHail = _shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty;
            string hailLabel = _shiftController.IsShiftTimerExpired ? "WATCH ENDED" : "NEXT HAIL";
            DrawHardwareButton(
                mouse, NextHailBtnX, NextHailBtnY, NextHailBtnW, NextHailBtnH,
                hailLabel,
                enabled: canHail,
                highlight: canHail,
                activeColor: Palette.CrtAmberPrimary);
        }

        // 2 & 3. LOWER SHIELDS (1032, 264, 116, 44) & PWR DOWN WPN (1156, 264, 116, 44):
        // Recessed inactive bays with subtle [PLAYTEST] labels on Day 1; active Day 2+
        if (!AreCombatOrdersUnlocked)
        {
            DrawInactiveHardwareBay(LowerShieldsBtnX, LowerShieldsBtnY, LowerShieldsBtnW, LowerShieldsBtnH, "SHIELDS (D02)", null);
            DrawInactiveHardwareBay(PwrDownWpnBtnX, PwrDownWpnBtnY, PwrDownWpnBtnW, PwrDownWpnBtnH, "WEAPONS (D02)", null);
            return;
        }

        bool shipInteractable = _activeShip != null &&
                                (_phase is EncounterPhase.OpeningGreetingDelay
                                    or EncounterPhase.AwaitingPortForDownload
                                    or EncounterPhase.DownloadingProtocols
                                    or EncounterPhase.DownloadAborted
                                    or EncounterPhase.InspectingSlateAndShip
                                    or EncounterPhase.AwaitingClearanceReturnToTopPort);

        DrawHardwareButton(
            mouse, LowerShieldsBtnX, LowerShieldsBtnY, LowerShieldsBtnW, LowerShieldsBtnH,
            _pendingShieldsOrder ? "ORDERING..." : "LOWER SHIELDS",
            enabled: shipInteractable && !_pendingShieldsOrder,
            highlight: false,
            activeColor: Palette.CrtAmberPrimary);

        DrawHardwareButton(
            mouse, PwrDownWpnBtnX, PwrDownWpnBtnY, PwrDownWpnBtnW, PwrDownWpnBtnH,
            _pendingWeaponsOrder ? "ORDERING..." : "PWR DOWN WPN",
            enabled: shipInteractable && !_pendingWeaponsOrder,
            highlight: false,
            activeColor: Palette.CrtAmberPrimary);
    }

    private void DrawProtocolReviewAndBioZone(VirtualMouseState mouse)
    {
        // Row 2B: PROTOCOL REVIEW (908, 316, 156, 36) -- inactive [PLAYTEST] bay Days 1-2; active Day 3+
        if (!IsProtocolReviewUnlocked)
        {
            DrawInactiveHardwareBay(ProtocolReviewBtnX, ProtocolReviewBtnY, ProtocolReviewBtnW, ProtocolReviewBtnH, "PROTOCOL REVIEW", "DAY 03");
        }
        else
        {
            bool locked = _preShiftStage != PreShiftStage.None || _faultStage != FaultSequenceStage.None;
            bool canReview = !locked && _activeShip != null && IsShipInspectableForReview;
            bool verified = _activeShip != null && _activeShip.ProtocolReviewVerified;

            string label = _protocolReviewModeActive
                ? (_reviewFirstSelection == ReviewTarget.None ? "REVIEW: 1/2" : "REVIEW: 2/2")
                : (verified ? "REVIEW: VERIFIED" : "PROTOCOL REVIEW");
            Color activeCol = verified ? Palette.ClearanceTeal : Palette.CrtAmberPrimary;

            DrawHardwareButton(
                mouse, ProtocolReviewBtnX, ProtocolReviewBtnY, ProtocolReviewBtnW, ProtocolReviewBtnH,
                label,
                enabled: canReview,
                highlight: _protocolReviewModeActive || verified,
                activeColor: activeCol);
        }

        // Row 2B: BIO-SCANNER LED (1072, 316, 200, 36) -- aligns flush with Clearance Dock (x = 1072..1272)
        DrawInactiveHardwareBay(BioScannerLedX, BioScannerLedY, BioScannerLedW, BioScannerLedH, "BIO-SCANNER", null);
    }

    // =========================================================================
    // 9. Clearance Upload Dock: (1072, 360, 200, 272)
    //    Header/Pins: (1072, 360, 200, 24)
    //    Visible Docked Data Slate Bay: (1080, 384, 184, 216)
    //    Encryption Progress Bar: (1080, 604, 184, 20)
    // =========================================================================
    private void DrawClearanceUploadDock(VirtualMouseState mouse)
    {
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;
        bool canSlotForClearance = !lockedByPreShift &&
                                   _phase == EncounterPhase.InspectingSlateAndShip &&
                                   _slateTelemetry == DataSlateTelemetryState.Loaded;
        bool draggingSlateOverDock = canSlotForClearance &&
                                     _draggingItemId == DeskItemId.DataSlate &&
                                     IsDraggingDataSlateOverClearanceDock(mouse, _deskItems[DeskItemId.DataSlate]);

        bool isUploading = _phase == EncounterPhase.UploadingClearance;
        bool awaitingReturn = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort;
        bool isTransmitting = _phase == EncounterPhase.TransmittingClearance;
        bool pulse = ((_animTick / 8) % 2) == 0;
        bool amberPulse = ((_animTick / 6) % 2) == 0;
        bool isClearedLed = awaitingReturn || isTransmitting;

        Color dockBorder = (IsDataSlateInClearanceDock || isUploading || awaitingReturn || isTransmitting)
            ? Palette.ClearanceTeal
            : (draggingSlateOverDock ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);

        // Outer Dock Chassis: (1072, 360, 200, 272)
        Raylib.DrawRectangle(ClearanceDockX, ClearanceDockY, ClearanceDockW, ClearanceDockH, Palette.OutlineVoid);
        Raylib.DrawRectangle(ClearanceDockX + 2, ClearanceDockY + 2, ClearanceDockW - 4, ClearanceDockH - 4, new Color(24, 29, 36, 255));
        Raylib.DrawRectangleLines(ClearanceDockX, ClearanceDockY, ClearanceDockW, ClearanceDockH, dockBorder);

        // Top Header + Connector Pins: (1072, 360, 200, 24)
        Raylib.DrawRectangle(ClearanceDockX + 2, ClearanceDockY + 2, ClearanceDockW - 4, ClearanceHeaderH - 2, Palette.WeatheredGunmetalMid);

        // Dock Status Indicator LED (left side of header): pulsing amber while encrypting (1%-99%), solid cyan/green at 100% cleared
        int ledX = ClearanceDockX + 8;
        int ledY = ClearanceDockY + 5;
        Color ledBezel = lockedByPreShift
            ? Palette.WeatheredGunmetalEdge
            : (isUploading
                ? (amberPulse ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn)
                : (isClearedLed
                    ? Palette.ClearanceTeal
                    : (draggingSlateOverDock ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge)));
        Color ledCore = lockedByPreShift
            ? new Color(28, 34, 42, 255)
            : (isUploading
                ? (amberPulse ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed)
                : (isClearedLed
                    ? Palette.ClearanceTeal
                    : (draggingSlateOverDock ? Palette.CrtAmberDimmed : new Color(38, 46, 56, 255))));

        Raylib.DrawRectangle(ledX, ledY, 10, 10, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ledX, ledY, 10, 10, ledBezel);
        Raylib.DrawRectangle(ledX + 2, ledY + 2, 6, 6, ledCore);
        if (!lockedByPreShift && (isUploading || isClearedLed))
        {
            Color ledPip = isUploading
                ? (amberPulse ? new Color(255, 236, 179, 255) : Palette.CrtAmberPrimary)
                : new Color(186, 255, 245, 255);
            Raylib.DrawRectangle(ledX + 3, ledY + 3, 2, 2, ledPip);
        }

        const string title = "CLEARANCE DOCK";
        int titleW = Raylib.MeasureText(title, 10);
        Raylib.DrawText(title, ClearanceDockX + (ClearanceDockW - titleW) / 2, ClearanceDockY + 5, 10,
            lockedByPreShift ? Palette.WeatheredGunmetalEdge : Palette.ClearanceTeal);

        // 6 Connector Pins along bottom edge of header (y = 380..384) mating directly with the docked 184x216 Data Slate!
        for (int pin = 0; pin < 6; pin++)
        {
            int px = ClearanceSlotX + 14 + pin * 28;
            Color pinCol = isUploading
                ? (amberPulse ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed)
                : (isClearedLed || IsDataSlateInClearanceDock
                    ? (pulse && isClearedLed ? Palette.SpsShieldArcIce : Palette.ClearanceTeal)
                    : Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangle(px, ClearanceDockY + ClearanceHeaderH - 4, 12, 4, pinCol);
        }

        // Visible Docked Data Slate Bay: (1080, 384, 184, 216) -- exact size of Primary Data Slate!
        Color slotBg = draggingSlateOverDock ? new Color(16, 42, 46, 255) : Palette.DeepSpaceVoid;
        Raylib.DrawRectangle(ClearanceSlotX, ClearanceSlotY, ClearanceSlotW, ClearanceSlotH, slotBg);
        Raylib.DrawRectangleLines(ClearanceSlotX, ClearanceSlotY, ClearanceSlotW, ClearanceSlotH, dockBorder);

        // Recessed left/right clamp latch guide tracks machined into the dock's side walls
        const int latchTrackY = ClearanceSlotY + (ClearanceSlotH / 2) - 18;
        const int latchTrackH = 36;
        Raylib.DrawRectangle(ClearanceDockX + 2, latchTrackY, 6, latchTrackH, Palette.OutlineVoid);
        Raylib.DrawLine(ClearanceDockX + 2, latchTrackY, ClearanceDockX + 7, latchTrackY, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(ClearanceDockX + 2, latchTrackY + latchTrackH - 1, ClearanceDockX + 7, latchTrackY + latchTrackH - 1, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(ClearanceSlotX + ClearanceSlotW, latchTrackY, 6, latchTrackH, Palette.OutlineVoid);
        Raylib.DrawLine(ClearanceSlotX + ClearanceSlotW, latchTrackY, ClearanceSlotX + ClearanceSlotW + 5, latchTrackY, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(ClearanceSlotX + ClearanceSlotW, latchTrackY + latchTrackH - 1, ClearanceSlotX + ClearanceSlotW + 5, latchTrackY + latchTrackH - 1, Palette.WeatheredGunmetalEdge);

        // When no slate is seated inside the bay, render the open/retracting latches behind dragged items;
        // when seated, DrawVirtual renders them OVER the docked Data Slate so they bite over its sides.
        if (!IsDataSlateInClearanceDock)
        {
            DrawClearanceDockLatches(isEncrypting: isUploading);
        }

        // Only draw empty-bay instructions when Data Slate is NOT seated inside;
        // when seated, the REAL 184x216 Data Slate renders right on top at (1080, 384, 184, 216)!
        if (!IsDataSlateInClearanceDock)
        {
            if (draggingSlateOverDock)
            {
                const string dropTxt = ">> DROP TO ENCRYPT <<";
                int dw = Raylib.MeasureText(dropTxt, 10);
                Raylib.DrawText(dropTxt, ClearanceSlotX + (ClearanceSlotW - dw) / 2, ClearanceSlotY + 100, 10, Palette.CrtAmberPrimary);
            }
            else if (isTransmitting)
            {
                const string txTxt = "TX VIA SLATE BUS";
                int tw = Raylib.MeasureText(txTxt, 10);
                Raylib.DrawText(txTxt, ClearanceSlotX + (ClearanceSlotW - tw) / 2, ClearanceSlotY + 100, 10, Palette.ClearanceTeal);
            }
            else
            {
                const string idle1 = "DROP LOADED";
                const string idle2 = "DATA SLATE HERE";
                Raylib.DrawText(idle1, ClearanceSlotX + (ClearanceSlotW - Raylib.MeasureText(idle1, 10)) / 2, ClearanceSlotY + 90, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(idle2, ClearanceSlotX + (ClearanceSlotW - Raylib.MeasureText(idle2, 10)) / 2, ClearanceSlotY + 106, 10, Palette.WeatheredGunmetalEdge);
            }
        }

        // Encryption Progress Bar: (1080, 604, 184, 20) underneath the visible docked Data Slate
        Raylib.DrawRectangle(ClearanceBarX, ClearanceBarY, ClearanceBarW, ClearanceBarH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(ClearanceBarX, ClearanceBarY, ClearanceBarW, ClearanceBarH,
            isUploading || awaitingReturn ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);

        float prog = isUploading
            ? Math.Clamp(_clearanceUploadElapsed / ClearanceUploadDurationSeconds, 0f, 1f)
            : (awaitingReturn || isTransmitting ? 1.0f : 0.0f);

        if (prog > 0.001f)
        {
            int innerW = ClearanceBarW - 4;
            int fillW = Math.Clamp((int)MathF.Round(innerW * prog), 1, innerW);
            Raylib.DrawRectangle(ClearanceBarX + 2, ClearanceBarY + 2, fillW, ClearanceBarH - 4, Palette.ClearanceTeal);

            string pctStr = prog >= 0.999f ? "CLEARED 100%" : $"ENCRYPTING {(int)MathF.Round(prog * 100f)}%";
            int pw = Raylib.MeasureText(pctStr, 10);
            int txtX = ClearanceBarX + (ClearanceBarW - pw) / 2;
            int txtY = ClearanceBarY + 5;
            // Inverted-color retro CRT bar readout: teal on dark unfilled track, dark void on filled teal bar
            Raylib.DrawText(pctStr, txtX, txtY, 10, Palette.ClearanceTeal);
            Raylib.BeginScissorMode(ClearanceBarX + 2, ClearanceBarY + 2, fillW, ClearanceBarH - 4);
            Raylib.DrawText(pctStr, txtX, txtY, 10, Palette.OutlineVoid);
            Raylib.EndScissorMode();
        }
        else
        {
            const string barIdle = "ENCRYPTION IDLE";
            int iw = Raylib.MeasureText(barIdle, 10);
            Raylib.DrawText(barIdle, ClearanceBarX + (ClearanceBarW - iw) / 2, ClearanceBarY + 5, 10, Palette.WeatheredGunmetalEdge);
        }
    }

    private void DrawClearanceDockLatches(bool isEncrypting)
    {
        const int latchCenterY = ClearanceSlotY + ClearanceSlotH / 2;
        const int baseY = latchCenterY - 14;
        const int baseH = 28;
        const int jawY = latchCenterY - 12;
        const int jawH = 24;

        float clampedP = Math.Clamp(_dockLatchProgress, 0f, 1f);
        // Snappy mechanical ease-out quad as the clamp jaws slide inward over the slate edges
        float easedP = 1.0f - (1.0f - clampedP) * (1.0f - clampedP);
        int travelPx = (int)MathF.Round(6.0f * easedP);

        bool amberPulse = ((_animTick / 6) % 2) == 0;
        Color pipCol = isEncrypting
            ? (amberPulse ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed)
            : Palette.OxidizedBronzeWorn;
        Color jawFill = Palette.WeatheredGunmetalEdge;
        Color jawShade = Palette.WeatheredGunmetalMid;
        Color jawHighlight = new(120, 134, 152, 255);
        Color jawTipEdge = new(184, 198, 214, 255);
        Color dropShadow = new(8, 11, 16, 210);

        // Left mechanical clamp latch (slides rightward from left bay wall x = 1074..1079 -> up to x = 1085)
        int leftWallX = ClearanceDockX + 2;
        int leftJawW = 6 + travelPx;
        if (travelPx > 0)
        {
            Raylib.DrawRectangle(ClearanceSlotX + travelPx, jawY + 1, 1, jawH, dropShadow);
            Raylib.DrawRectangle(ClearanceSlotX, jawY + jawH, travelPx + 1, 1, dropShadow);
        }
        Raylib.DrawRectangle(leftWallX, baseY, 6, baseH, Palette.OutlineVoid);
        Raylib.DrawRectangle(leftWallX, jawY, leftJawW, jawH, Palette.OutlineVoid);
        Raylib.DrawRectangle(leftWallX + 1, baseY + 1, 4, baseH - 2, jawShade);
        Raylib.DrawRectangle(leftWallX + 1, baseY + 1, 4, 2, jawHighlight);
        Raylib.DrawRectangle(leftWallX + 1, jawY + 1, leftJawW - 2, jawH - 2, jawFill);
        Raylib.DrawLine(leftWallX + 1, jawY + 1, leftWallX + leftJawW - 2, jawY + 1, jawHighlight);
        Raylib.DrawLine(leftWallX + 1, jawY + jawH - 2, leftWallX + leftJawW - 2, jawY + jawH - 2, jawShade);
        Raylib.DrawRectangle(leftWallX + leftJawW - 2, jawY + 2, 1, jawH - 4, jawTipEdge);
        if (travelPx >= 2)
        {
            Raylib.DrawLine(ClearanceSlotX, jawY + 6, leftWallX + leftJawW - 3, jawY + 6, jawShade);
            Raylib.DrawLine(ClearanceSlotX, jawY + 12, leftWallX + leftJawW - 3, jawY + 12, jawShade);
            Raylib.DrawLine(ClearanceSlotX, jawY + 18, leftWallX + leftJawW - 3, jawY + 18, jawShade);
        }
        Raylib.DrawRectangle(leftWallX + 2, latchCenterY - 4, 2, 8, pipCol);

        // Right mechanical clamp latch (mirrored: slides leftward from right bay wall x = 1264..1269 -> down to x = 1258)
        int rightWallX = ClearanceSlotX + ClearanceSlotW;
        int rightJawX = rightWallX - travelPx;
        int rightJawW = 6 + travelPx;
        if (travelPx > 0)
        {
            Raylib.DrawRectangle(rightJawX - 1, jawY + 1, 1, jawH, dropShadow);
            Raylib.DrawRectangle(rightJawX - 1, jawY + jawH, travelPx + 1, 1, dropShadow);
        }
        Raylib.DrawRectangle(rightWallX, baseY, 6, baseH, Palette.OutlineVoid);
        Raylib.DrawRectangle(rightJawX, jawY, rightJawW, jawH, Palette.OutlineVoid);
        Raylib.DrawRectangle(rightWallX + 1, baseY + 1, 4, baseH - 2, jawShade);
        Raylib.DrawRectangle(rightWallX + 1, baseY + 1, 4, 2, jawHighlight);
        Raylib.DrawRectangle(rightJawX + 1, jawY + 1, rightJawW - 2, jawH - 2, jawFill);
        Raylib.DrawLine(rightJawX + 1, jawY + 1, rightJawX + rightJawW - 2, jawY + 1, jawHighlight);
        Raylib.DrawLine(rightJawX + 1, jawY + jawH - 2, rightJawX + rightJawW - 2, jawY + jawH - 2, jawShade);
        Raylib.DrawRectangle(rightJawX + 1, jawY + 2, 1, jawH - 4, jawTipEdge);
        if (travelPx >= 2)
        {
            Raylib.DrawLine(rightJawX + 2, jawY + 6, rightWallX - 1, jawY + 6, jawShade);
            Raylib.DrawLine(rightJawX + 2, jawY + 12, rightWallX - 1, jawY + 12, jawShade);
            Raylib.DrawLine(rightJawX + 2, jawY + 18, rightWallX - 1, jawY + 18, jawShade);
        }
        Raylib.DrawRectangle(rightWallX + 2, latchCenterY - 4, 2, 8, pipCol);
    }

    // =========================================================================
    // 10. Error-Only Transient Citation Slide-Out Overlay (ZERO permanent grey box!)
    // =========================================================================
    private Rectangle GetActiveCitationPopupRect()
    {
        const int cw = 480;
        const int ch = 88;
        int cx = Constants.VirtualWidth - 8 - cw; // right-aligned to 8px margin (792..1272)
        int startY = Constants.VirtualHeight;
        int targetY = Constants.VirtualHeight - 8 - ch; // rests at y = 624..712 with 8px bottom margin
        int cy = startY + (int)MathF.Round((targetY - startY) * _citationSlideProgress);
        return new Rectangle(cx, cy, cw, ch);
    }

    private void DrawErrorOnlyCitationPopup()
    {
        if (_activeCitationStrip == null)
            return;

        Rectangle rect = GetActiveCitationPopupRect();
        int cx = (int)rect.X;
        int cy = (int)rect.Y;
        int cw = (int)rect.Width;
        int ch = (int)rect.Height;

        bool flashBright = ((_animTick / 8) % 2) == 0;
        Color frameBorder = _activeCitationStrip.IsSecurityStrike
            ? (flashBright ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
            : (flashBright ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);

        Raylib.DrawRectangle(cx, cy, cw, ch, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(cx, cy, cw, ch, frameBorder);
        Raylib.DrawRectangleLines(cx + 2, cy + 2, cw - 4, ch - 4, Palette.OxidizedBronzeWorn);

        Raylib.DrawRectangle(cx + 3, cy + 3, cw - 6, 18,
            _activeCitationStrip.IsSecurityStrike ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
        Raylib.DrawText(TruncateByPixelWidth(_activeCitationStrip.HeaderText, 10, cw - 16), cx + 10, cy + 7, 10, Palette.OutlineVoid);

        Raylib.DrawText(TruncateByPixelWidth(_activeCitationStrip.ViolationReason, 10, cw - 20), cx + 10, cy + 28, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(TruncateByPixelWidth(_activeCitationStrip.PenaltyText, 10, cw - 20), cx + 10, cy + 46, 10,
            _activeCitationStrip.IsSecurityStrike ? Palette.AlertCrimson : Palette.ClearanceTeal);
        Raylib.DrawText("[CLICK TO DISMISS CITATION]", cx + 10, cy + 66, 10, Palette.CrtAmberDimmed);
    }

    // =========================================================================
    // 11. Primary Data Slate: exact 184 x 216
    //     Visibly docks beneath Slate Bus (698, 308) AND inside Clearance Dock (1080, 384)
    //     Day 1 = Clean Basic Registration | Day 2 = +WPN/SHLD | Day 3+ = Full
    // =========================================================================
    private void DrawMovableDataSlate(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        int x = item.X;
        int y = item.Y + GetDataSlateVisualOffsetY();
        int w = item.Width;
        int h = item.Height;

        bool isDownloading = _slateTelemetry == DataSlateTelemetryState.Downloading;
        bool isAborted = _slateTelemetry == DataSlateTelemetryState.DownloadAborted;
        bool isUploading = _slateTelemetry == DataSlateTelemetryState.UploadingClearance;
        bool isCleared = _slateTelemetry == DataSlateTelemetryState.ClearedReadyToTransmit;
        bool isTransmitting = _slateTelemetry == DataSlateTelemetryState.TransmittingClearance;
        bool isDetainWiping = _phase == EncounterPhase.DetainClamping && !_detainSlateWipeCompleted;
        bool hasShipData = _activeShip != null &&
                           _slateTelemetry is DataSlateTelemetryState.Downloading
                               or DataSlateTelemetryState.Loaded
                               or DataSlateTelemetryState.UploadingClearance
                               or DataSlateTelemetryState.ClearedReadyToTransmit
                               or DataSlateTelemetryState.TransmittingClearance;

        bool pulse = ((_animTick / 8) % 2) == 0;
        Color borderCol = isAborted || isDetainWiping
            ? Palette.AlertCrimson
            : (isUploading || isCleared || isTransmitting
                ? (pulse ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                : (isTopmost ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        // Heavy Dark-Steel Chassis (184 x 216)
        Raylib.DrawRectangle(x, y, w, h, new Color(22, 27, 34, 255));
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);
        Raylib.DrawRectangleLines(x + 1, y + 1, w - 2, h - 2, Palette.WeatheredGunmetalMid);

        // 6 top contact tabs mating with Slate Bus OR Clearance Dock connector pins
        bool dockedToConnector = IsDataSlateInSlateBus || IsDataSlateInClearanceDock;
        for (int pin = 0; pin < 6; pin++)
        {
            int px = x + 14 + pin * 28;
            Color tabCol = dockedToConnector
                ? ((isDownloading || isUploading || isTransmitting) && pulse ? Palette.SpsShieldArcIce : Palette.ClearanceTeal)
                : Palette.OxidizedBronzeWorn;
            Raylib.DrawRectangle(px, y - 1, 12, 2, tabCol);
        }

        // Top Header Bar (y + 2 .. y + 17)
        Color headerBg = isAborted || isDetainWiping
            ? Palette.BrownDwarfGlowDeep
            : (isUploading || isCleared || isTransmitting ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(x + 2, y + 2, w - 4, 16, headerBg);

        float dl = ProtocolDownloadProgress;
        float upProg = Math.Clamp(_clearanceUploadElapsed / ClearanceUploadDurationSeconds, 0f, 1f);
        float txProg = Math.Clamp(_clearanceTransmitElapsed / ClearanceTransmitDurationSeconds, 0f, 1f);

        string slateHeader = isDetainWiping
            ? "CLAMP LOCK -- WIPING"
            : _slateTelemetry switch
            {
                DataSlateTelemetryState.Downloading => $"RX PACKET {(int)MathF.Round(dl * 100f)}%",
                DataSlateTelemetryState.DownloadAborted => "LINK LOST -- ABORTED",
                DataSlateTelemetryState.UploadingClearance => $"ENCRYPTING {(int)MathF.Round(upProg * 100f)}%",
                DataSlateTelemetryState.ClearedReadyToTransmit => "CLEARED -> SLATE BUS",
                DataSlateTelemetryState.TransmittingClearance => $"TX CLEAR {(int)MathF.Round(txProg * 100f)}%",
                DataSlateTelemetryState.Loaded when IsDataSlateInSlateBus => "DATA SLATE [LINKED]",
                DataSlateTelemetryState.Loaded when IsDataSlateInClearanceDock => "DATA SLATE [DOCKED]",
                DataSlateTelemetryState.Loaded => "PRIMARY DATA SLATE",
                _ => IsDataSlateInSlateBus ? "DATA SLATE [LINKED]" : "DATA SLATE [FREE]"
            };
        Color headerFg = isAborted || isDetainWiping
            ? Palette.AlertCrimson
            : (isDownloading || isUploading || isCleared || isTransmitting ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
        Raylib.DrawText(TruncateByPixelWidth(slateHeader, 10, w - 12), x + 6, y + 5, 10, headerFg);

        // Empty / Wiped / Aborted Screen
        if (!hasShipData || _activeShip == null)
        {
            Raylib.DrawRectangle(x + 8, y + 24, w - 16, h - 36, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(x + 8, y + 24, w - 16, h - 36,
                isAborted ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);

            if (isAborted)
            {
                const string a1 = "LINK SEVERED";
                const string a2 = "RE-PLUG IN SLATE BUS";
                const string a3 = "CLICK [RE-TRANSMIT]";
                Raylib.DrawText(a1, x + (w - Raylib.MeasureText(a1, 10)) / 2, y + 66, 10, Palette.AlertCrimson);
                Raylib.DrawText(a2, x + (w - Raylib.MeasureText(a2, 10)) / 2, y + 92, 10, Palette.ClearanceTeal);
                Raylib.DrawText(a3, x + (w - Raylib.MeasureText(a3, 10)) / 2, y + 110, 10, Palette.ClearanceTeal);
            }
            else if (_slateWipeFlashTimer > 0f)
            {
                const string w1 = "CLAMPS ENGAGED";
                const string w2 = "TELEMETRY WIPED";
                const string w3 = "BUFFER CLEAN";
                Raylib.DrawText(w1, x + (w - Raylib.MeasureText(w1, 10)) / 2, y + 74, 10, Palette.AlertCrimson);
                Raylib.DrawText(w2, x + (w - Raylib.MeasureText(w2, 10)) / 2, y + 94, 10, Palette.CrtAmberPrimary);
                Raylib.DrawText(w3, x + (w - Raylib.MeasureText(w3, 10)) / 2, y + 114, 10, Palette.ClearanceTeal);
            }
            else
            {
                const string e1 = "PRIMARY DATA SLATE";
                const string e2 = "BUFFER: EMPTY";
                string hint = IsDataSlateInSlateBus ? "READY IN SLATE BUS" : "PLUG INTO SLATE BUS";
                Raylib.DrawText(e1, x + (w - Raylib.MeasureText(e1, 10)) / 2, y + 74, 10, Palette.CrtAmberDimmed);
                Raylib.DrawText(e2, x + (w - Raylib.MeasureText(e2, 10)) / 2, y + 96, 10, Palette.CrtAmberPrimary);
                Raylib.DrawText(hint, x + (w - Raylib.MeasureText(hint, 10)) / 2, y + 118, 10,
                    IsDataSlateInSlateBus ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
            }

            return;
        }

        DataSlateModel slate = _activeShip.Slate;

        // Progressive Transfer Segmented Bar (y + 19) for RX Download / Dock Encryption / TX Clearance
        if (isDownloading || isUploading || isTransmitting)
        {
            float segProg = isDownloading ? dl : (isUploading ? upProg : txProg);
            const int segs = 13;
            int filled = (int)MathF.Floor(segProg * segs);
            for (int s = 0; s < segs; s++)
            {
                Raylib.DrawRectangle(x + 7 + s * 13, y + 19, 10, 2, s <= filled ? Palette.ClearanceTeal : Palette.WeatheredGunmetalMid);
            }
        }

        // Top-Left: 64x64 Procedural Holo-Portrait at (x + 6, y + 22)
        int holoX = x + 6;
        int holoY = y + 22;
        Raylib.DrawRectangle(holoX, holoY, Constants.PortraitWidth, Constants.PortraitHeight, Palette.DeepSpaceVoid);
        if (_cachedHoloPortraitTex.Id != 0)
        {
            float holoReveal = Math.Clamp(dl / 0.55f, 0f, 1f);
            int revealH = (int)MathF.Round(Constants.PortraitHeight * holoReveal);
            if (revealH > 0)
            {
                Rectangle src = new(0, 0, Constants.PortraitWidth, revealH);
                Rectangle dst = new(holoX, holoY, Constants.PortraitWidth, revealH);
                Raylib.DrawTexturePro(_cachedHoloPortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
                if (isDownloading && revealH < Constants.PortraitHeight)
                {
                    Raylib.DrawLine(holoX, holoY + revealH, holoX + Constants.PortraitWidth, holoY + revealH, Palette.ClearanceTeal);
                }
            }
        }
        Raylib.DrawRectangleLines(holoX - 1, holoY - 1, Constants.PortraitWidth + 2, Constants.PortraitHeight + 2,
            isUploading || isCleared ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);

        int rightColX = x + 75;
        int rightColMaxPx = w - 80; // 104px
        int fieldX = x + 7;
        int fieldMaxPx = w - 14;    // 170px

        if (_gameState.Day == 1)
        {
            // =================================================================
            // DAY 1 CLEAN MINIMAL DATA SLATE (Basic Registration Fields Only!)
            // =================================================================
            if (dl >= 0.15f)
            {
                Raylib.DrawRectangle(x + 73, y + 22, w - 78, 30, Palette.OutlineVoid);
                Raylib.DrawRectangleLines(x + 73, y + 22, w - 78, 30, Palette.OxidizedBronzeWorn);
                Raylib.DrawText("REG CODE", rightColX + 1, y + 25, 10, Palette.CrtAmberDimmed);
                Raylib.DrawText(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx - 4), rightColX + 1, y + 38, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.35f)
            {
                Raylib.DrawText("PREFIX:", rightColX, y + 57, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(slate.DeclaredPrefix, rightColX, y + 69, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawLine(x + 6, y + 89, x + w - 6, y + 89, Palette.WeatheredGunmetalEdge);

            if (dl >= 0.45f)
            {
                Raylib.DrawText("VESSEL", fieldX, y + 94, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(TruncateByPixelWidth(slate.ShipName, 10, fieldMaxPx), fieldX, y + 105, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.60f)
            {
                Raylib.DrawText("REGISTRY", fieldX, y + 120, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(TruncateByPixelWidth(slate.DeclaredFactionName, 10, fieldMaxPx), fieldX, y + 131, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.75f)
            {
                Raylib.DrawText("CAPTAIN", fieldX, y + 146, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(TruncateByPixelWidth(slate.DeclaredCaptainName, 10, fieldMaxPx), fieldX, y + 157, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.88f)
            {
                Raylib.DrawText("ROUTE", fieldX, y + 172, 10, Palette.WeatheredGunmetalEdge);
                Raylib.DrawText(TruncateByPixelWidth($"{slate.OriginSystem} -> {slate.DestinationSystem}", 10, fieldMaxPx),
                    fieldX, y + 183, 10, Palette.CrtAmberDimmed);
            }
        }
        else if (_gameState.Day == 2)
        {
            // =================================================================
            // DAY 2 DATA SLATE (Basic Registration + WPN & SHLD Telemetry for SPS)
            // =================================================================
            bool wpnFault = IsTargetFaultHighlighted(ReviewTarget.DataSlateWeapons);
            if (wpnFault)
            {
                Raylib.DrawRectangle(x + 73, y + 50, w - 78, 14, Palette.BrownDwarfGlowDeep);
                Raylib.DrawRectangleLines(x + 73, y + 50, w - 78, 14, GetFaultPulseColor());
            }

            if (dl >= 0.15f)
            {
                Raylib.DrawText("REG CODE:", rightColX, y + 22, 10, Palette.CrtAmberDimmed);
                Raylib.DrawText(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx), rightColX, y + 34, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.30f)
            {
                Raylib.DrawText(TruncateByPixelWidth($"WPN: {slate.DeclaredWeaponsStatus}", 10, rightColMaxPx), rightColX, y + 52, 10,
                    wpnFault ? GetFaultPulseColor() : Palette.CrtAmberPrimary);
                Raylib.DrawText(TruncateByPixelWidth($"SHLD: {slate.DeclaredShieldsStatus}", 10, rightColMaxPx), rightColX, y + 68, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawLine(x + 6, y + 89, x + w - 6, y + 89, Palette.WeatheredGunmetalEdge);

            if (dl >= 0.45f) Raylib.DrawText(TruncateByPixelWidth($"SHIP: {slate.ShipName}", 10, fieldMaxPx), fieldX, y + 94, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.58f) Raylib.DrawText(TruncateByPixelWidth($"ORG:  {slate.DeclaredFactionName}", 10, fieldMaxPx), fieldX, y + 108, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.70f) Raylib.DrawText(TruncateByPixelWidth($"CAPT: {slate.DeclaredCaptainName}", 10, fieldMaxPx), fieldX, y + 122, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.80f) Raylib.DrawText(TruncateByPixelWidth($"ORIG: {slate.OriginSystem}", 10, fieldMaxPx), fieldX, y + 136, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.88f) Raylib.DrawText(TruncateByPixelWidth($"DEST: {slate.DestinationSystem}", 10, fieldMaxPx), fieldX, y + 150, 10, Palette.CrtAmberDimmed);
            if (dl >= 0.94f)
            {
                List<string> cargoLines = WrapTextByPixelWidth($"CARGO: {slate.CargoManifest}", 10, fieldMaxPx, maxLines: 2);
                for (int c = 0; c < cargoLines.Count; c++)
                {
                    Raylib.DrawText(cargoLines[c], fieldX, y + 164 + c * 13, 10, Palette.CrtAmberDimmed);
                }
            }
        }
        else
        {
            // =================================================================
            // DAY 3+ FULL INSPECTION DATA SLATE (Exact 184x216 Protocol Review Fields)
            // =================================================================
            int rightHotW = w - 78; // 106px
            int rowW = w - 8;

            DrawSlateFieldHighlight(mouse, x + 73, y + 21, rightHotW, 22, ReviewTarget.DataSlatePrefix);
            DrawSlateFieldHighlight(mouse, x + 73, y + 44, rightHotW, 13, ReviewTarget.DataSlateClass);
            DrawSlateFieldHighlight(mouse, x + 73, y + 58, rightHotW, 13, ReviewTarget.DataSlateWeapons);
            DrawSlateFieldHighlight(mouse, x + 4, y + 103, rowW, 12, ReviewTarget.DataSlateOrg);
            DrawSlateFieldHighlight(mouse, x + 4, y + 115, rowW, 12, ReviewTarget.DataSlateExpiry);
            DrawSlateFieldHighlight(mouse, x + 4, y + 139, rowW, 12, ReviewTarget.DataSlateSpecies);
            DrawSlateFieldHighlight(mouse, x + 4, y + 151, rowW, 12, ReviewTarget.DataSlateOrigin);
            DrawSlateFieldHighlight(mouse, x + 4, y + 175, rowW, 24, ReviewTarget.DataSlateCargo);

            if (dl >= 0.12f)
            {
                Raylib.DrawText("REG CODE:", rightColX, y + 22, 10, Palette.CrtAmberDimmed);
                Raylib.DrawText(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx), rightColX, y + 32, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.22f)
            {
                Raylib.DrawText(TruncateByPixelWidth($"CLASS: {ShortClass(slate.DeclaredShipClass)}", 10, rightColMaxPx), rightColX, y + 45, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.32f)
            {
                Raylib.DrawText(TruncateByPixelWidth($"WPN: {slate.DeclaredWeaponsStatus}", 10, rightColMaxPx), rightColX, y + 59, 10, Palette.CrtAmberPrimary);
                Raylib.DrawText(TruncateByPixelWidth($"MASS: {slate.DeclaredTonnage} T", 10, rightColMaxPx), rightColX, y + 73, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawLine(x + 4, y + 89, x + w - 4, y + 89, Palette.WeatheredGunmetalEdge);
            int fieldY = y + 92;
            const int lineStep = 12;

            if (dl >= 0.42f) Raylib.DrawText(TruncateByPixelWidth($"SHIP: {slate.ShipName}", 10, fieldMaxPx), fieldX, fieldY, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.50f) Raylib.DrawText(TruncateByPixelWidth($"ORG:  {slate.DeclaredFactionName}", 10, fieldMaxPx), fieldX, fieldY + lineStep, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.58f) Raylib.DrawText(TruncateByPixelWidth($"EXP:  {slate.RegistrationExpiryDisplay}", 10, fieldMaxPx), fieldX, fieldY + lineStep * 2, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.66f) Raylib.DrawText(TruncateByPixelWidth($"CAPT: {slate.DeclaredCaptainName}", 10, fieldMaxPx), fieldX, fieldY + lineStep * 3, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.74f) Raylib.DrawText(TruncateByPixelWidth($"SPEC: {slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}", 10, fieldMaxPx), fieldX, fieldY + lineStep * 4, 10, Palette.CrtAmberDimmed);
            if (dl >= 0.82f) Raylib.DrawText(TruncateByPixelWidth($"ORIG: {slate.OriginSystem}", 10, fieldMaxPx), fieldX, fieldY + lineStep * 5, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.88f) Raylib.DrawText(TruncateByPixelWidth($"DEST: {slate.DestinationSystem}", 10, fieldMaxPx), fieldX, fieldY + lineStep * 6, 10, Palette.CrtAmberDimmed);
            if (dl >= 0.94f)
            {
                List<string> cargoLines = WrapTextByPixelWidth($"CARGO: {slate.CargoManifest}", 10, fieldMaxPx, maxLines: 2);
                int cargoBaseY = fieldY + lineStep * 7;
                for (int c = 0; c < cargoLines.Count; c++)
                {
                    Raylib.DrawText(cargoLines[c], fieldX, cargoBaseY + c * 12, 10, Palette.CrtAmberPrimary);
                }
            }
        }

        // Bottom Status Banner (y + 201 .. y + 214 -- zero overlap with lower fields)
        if (isDetainWiping)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.AlertCrimson);
            const string msg = "WIPING TELEMETRY...";
            Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isUploading)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.OxidizedBronzeShadow);
            const string msg = "DRAG OUT TO ABORT";
            Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.ClearanceTeal);
        }
        else if (isCleared)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.ClearanceTeal);
            const string msg = ">> DRAG TO SLATE BUS <<";
            Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isTransmitting)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.ClearanceTeal);
            const string msg = "TX -- DRAG TO ABORT";
            Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isDownloading)
        {
            const string msg = "HOLD IN SLATE BUS";
            Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.ClearanceTeal);
        }

        // Stamp bold framed CLEARED text seal directly over the face of the Data Slate from 100% encryption through Slate Bus TX
        if (HasActiveClearanceSeal)
        {
            DrawClearanceSeal(new Rectangle(x, y, w, h));
        }
    }

    /// <summary>
    /// Standalone text-based clearance stamp overlay drawn over a cleared Data Slate.
    /// Art-ready placeholder: swap this framed text stamp for pixel-art stamp artwork in Milestone 5.
    /// </summary>
    private static void DrawClearanceSeal(Rectangle slateRect)
    {
        int sx = (int)slateRect.X;
        int sy = (int)slateRect.Y;
        int sw = (int)slateRect.Width;

        const int sealW = 156;
        const int sealH = 50;
        int boxX = sx + (sw - sealW) / 2;
        int boxY = sy + 94;

        // Crisp 2px pixel drop-shadow so the seal floats clearly above the slate's underlying text/fields
        Raylib.DrawRectangle(boxX + 2, boxY + 2, sealW, sealH, new Color(4, 8, 12, 175));

        // Semi-opaque dark backing plate for high legibility whether docked, dragged, or in Slate Bus
        Raylib.DrawRectangle(boxX, boxY, sealW, sealH, new Color(8, 24, 28, 224));

        // Crisp double-line border with corner rivets
        Raylib.DrawRectangleLines(boxX, boxY, sealW, sealH, Palette.ClearanceTeal);
        Raylib.DrawRectangleLines(boxX + 3, boxY + 3, sealW - 6, sealH - 6, Palette.ClearanceTeal);
        Raylib.DrawRectangle(boxX + 1, boxY + 1, 2, 2, Palette.SpsShieldArcIce);
        Raylib.DrawRectangle(boxX + sealW - 3, boxY + 1, 2, 2, Palette.SpsShieldArcIce);
        Raylib.DrawRectangle(boxX + 1, boxY + sealH - 3, 2, 2, Palette.SpsShieldArcIce);
        Raylib.DrawRectangle(boxX + sealW - 3, boxY + sealH - 3, 2, 2, Palette.SpsShieldArcIce);

        // Bold 2x integer-pixel-scale "CLEARED" stamp + crisp "TO DOCK" sub-label
        const string primaryText = "CLEARED";
        int primaryW = Raylib.MeasureText(primaryText, 20);
        int primaryX = boxX + (sealW - primaryW) / 2;
        int primaryY = boxY + 8;
        Raylib.DrawText(primaryText, primaryX + 1, primaryY + 1, 20, Palette.OutlineVoid);
        Raylib.DrawText(primaryText, primaryX, primaryY, 20, Palette.ClearanceTeal);

        Raylib.DrawLine(boxX + 12, boxY + 30, boxX + sealW - 13, boxY + 30, new Color(45, 212, 191, 150));

        const string subText = "TO DOCK";
        int subW = Raylib.MeasureText(subText, 10);
        int subX = boxX + (sealW - subW) / 2;
        int subY = boxY + 33;
        Raylib.DrawText(subText, subX + 1, subY + 1, 10, Palette.OutlineVoid);
        Raylib.DrawText(subText, subX, subY, 10, Palette.SpsShieldArcIce);
    }

    private void DrawSlateFieldHighlight(VirtualMouseState mouse, int rx, int ry, int rw, int rh, ReviewTarget target)
    {
        bool isFault = IsTargetFaultHighlighted(target);
        if (isFault)
        {
            Color col = GetFaultPulseColor();
            Raylib.DrawRectangle(rx, ry, rw, rh, Palette.BrownDwarfGlowDeep);
            Raylib.DrawRectangleLines(rx, ry, rw, rh, col);
            Raylib.DrawRectangleLines(rx - 1, ry - 1, rw + 2, rh + 2, col);
            return;
        }

        if (_protocolReviewModeActive && IsShipInspectableForReview && IsSlateReadableForReview)
        {
            DrawReviewHotspotOutline(mouse, rx, ry, rw, rh, _reviewFirstSelection == target);
        }
    }

    // =========================================================================
    // 12. Day 3+ Movable Desk Reference Manual (1. PREFIXES & 2. HULLS ONLY; tightened 280x192 layout)
    // =========================================================================
    private void DrawDeskReferenceManual(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;

        if (_manualActiveTab != 0)
            _manualActiveTab = 1;

        Color borderCol = isTopmost ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed;
        Raylib.DrawRectangle(x, y, w, h, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);

        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 16, Palette.OxidizedBronzeShadow);
        Raylib.DrawText("DESK REFERENCE MANUAL", x + 8, y + 4, 10, Palette.CrtAmberPrimary);

        DrawManualTabButton(mouse, x + 6, y + 20, 132, 16, "1. PREFIXES", _manualActiveTab == 0);
        DrawManualTabButton(mouse, x + 142, y + 20, 132, 16, "2. HULLS", _manualActiveTab == 1);

        if (_manualActiveTab == 0)
        {
            int ry = y + 39;
            for (int i = 0; i < ManualPrefixDirectory.Length; i++)
            {
                var entry = ManualPrefixDirectory[i];
                bool rowHovered = mouse.IsHit(x + 6, ry, w - 12, 12);
                if (rowHovered)
                {
                    Raylib.DrawRectangle(x + 6, ry, w - 12, 12, Palette.OxidizedBronzeShadow);
                }
                string rowText = $"{entry.Prefix}  {entry.OrgName}";
                Raylib.DrawText(TruncateByPixelWidth(rowText, 10, w - 18), x + 10, ry + 1, 10,
                    rowHovered ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
                ry += 12;
            }
        }
        else
        {
            ShipFaction faction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(faction);
            ShipClass selectedClass = allowedClasses[Math.Clamp(_manualClassIndex, 0, allowedClasses.Count - 1)];

            DrawSmallButton(mouse, x + 6, y + 40, 76, 16, "< FACTION");
            if (_devDebug.DevManualSyncEnabled)
            {
                DrawSmallButton(mouse, x + 102, y + 40, 76, 16, "DEV SYNC", Palette.ClearanceTeal);
            }
            DrawSmallButton(mouse, x + 198, y + 40, 76, 16, "FACTION >");

            string factionTitle = ShipRenderer.GetFactionDisplayName(faction);
            Raylib.DrawText(TruncateByPixelWidth(factionTitle, 10, w - 16), x + 8, y + 59, 10, Palette.CrtAmberPrimary);

            string crewLine = ShipGenerator.GetAllowedCrewSummary(faction);
            Raylib.DrawText(TruncateByPixelWidth(crewLine, 10, w - 16), x + 8, y + 71, 10, Palette.ClearanceTeal);

            int chartX = x + (w - Constants.ShipWidth) / 2;
            int chartY = y + 84;
            if (_cachedManualShipTex.Id != 0)
            {
                Raylib.DrawTexture(_cachedManualShipTex, chartX, chartY, Color.White);
            }
            Raylib.DrawRectangleLines(chartX - 1, chartY - 1, Constants.ShipWidth + 2, Constants.ShipHeight + 2, Palette.CrtAmberDimmed);

            DrawSmallButton(mouse, x + 6, y + 144, 62, 16, "< PREV");
            string classCaption = $"{ShortClass(selectedClass)} ({_manualClassIndex + 1}/{allowedClasses.Count})";
            int capW = Raylib.MeasureText(classCaption, 10);
            Raylib.DrawText(classCaption, x + (w - capW) / 2, y + 147, 10, Palette.ClearanceTeal);
            DrawSmallButton(mouse, x + 212, y + 144, 62, 16, "NEXT >");

            string allowedList = string.Join("/", allowedClasses.Select(ShortClass));
            string subNote = faction == ShipFaction.FreeTraders
                ? $"CLASSES: {allowedList} (SHARED FTV/HBR)"
                : $"ALLOWED CLASSES: {allowedList}";
            Raylib.DrawText(TruncateByPixelWidth(subNote, 10, w - 16), x + 8, y + 168, 10, Palette.CrtAmberDimmed);
        }
    }

    private void DrawExtrudingOrAnchoredWarpStrip(VirtualMouseState mouse)
    {
        // L -> R Extrusion inside Warp Printer Tray (204, 276, 224, 64)
        float progress = _warpPhase == WarpPrinterPhase.PrintingStrip
            ? Math.Clamp(_warpPhaseElapsed / WarpPrintDurationSeconds, 0f, 1f)
            : 1.0f;

        int visibleW = Math.Max(14, (int)MathF.Round(StripWidth * progress));
        int sx = StripAnchorX + _anchoredStripPullOffsetX;
        int sy = StripAnchorY + _anchoredStripPullOffsetY;

        Raylib.BeginScissorMode(WarpTrayX + 1, WarpTrayY, WarpTrayW - 2, WarpTrayH);
        DrawWarpPaperStripBody(mouse, sx, sy, visibleW, StripFullHeight, progress, isTornOff: false, isTopmost: true);
        Raylib.EndScissorMode();
    }

    private void DrawFreeDraggableWarpStrip(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        DrawWarpPaperStripBody(mouse, item.X, item.Y, item.Width, item.Height, 1.0f, isTornOff: true, isTopmost: isTopmost);
    }

    private void DrawWarpPaperStripBody(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        float printProgress,
        bool isTornOff,
        bool isTopmost)
    {
        bool reviewSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.WarpStripPaper;
        bool reviewHover = _protocolReviewModeActive && mouse.IsHit(x, y, w, h);
        bool stripFault = IsTargetFaultHighlighted(ReviewTarget.WarpStripPaper);

        // Exact 216x52 magnetic waveform canvas inside the 220x64 strip (with slim 11px bottom grip edge)
        int waveX = x + StripWaveOffsetX;
        int waveY = y + StripWaveOffsetY;
        int visibleWaveW = Math.Clamp(w - StripWaveOffsetX * 2, 0, WarpGlassW);
        int gripY = waveY + WarpGlassH;
        int gripH = Math.Max(8, y + h - gripY);

        // Check overlap with Glowing Warp Checker's backlit glass (448, 294, 216, 52)
        int ovLeft = Math.Max(waveX, WarpGlassX);
        int ovRight = Math.Min(waveX + visibleWaveW, WarpGlassX + WarpGlassW);
        int ovTop = Math.Max(waveY, WarpGlassY);
        int ovBottom = Math.Min(waveY + WarpGlassH, WarpGlassY + WarpGlassH);
        bool hasBacklightOverlap = isTornOff && IsSpectrometerOperational &&
                                   !string.IsNullOrEmpty(_warpStripActualOrigin) &&
                                   ovRight > ovLeft && ovBottom > ovTop;

        bool hasOverlapMismatch = false;
        bool hasSubstantialOverlap = hasBacklightOverlap && (ovRight - ovLeft) >= 96 && (ovBottom - ovTop) >= 28;

        if (!hasBacklightOverlap)
        {
            // Opaque smoked magnetic/thermal film when resting on desk or inside printer tray
            Raylib.DrawRectangle(x, y, w, h, new Color(24, 21, 16, 246));
            if (visibleWaveW > 2)
            {
                Raylib.DrawRectangle(waveX, waveY, visibleWaveW, WarpGlassH, new Color(14, 13, 11, 246));
                Raylib.DrawLine(waveX, waveY + WarpGlassH / 2, waveX + visibleWaveW - 1, waveY + WarpGlassH / 2, new Color(46, 42, 34, 255));
                for (int gx = 36; gx < visibleWaveW; gx += 36)
                {
                    Raylib.DrawLine(waveX + gx, waveY + 2, waveX + gx, waveY + WarpGlassH - 3, new Color(34, 30, 24, 255));
                }
                if (!string.IsNullOrEmpty(_warpStripActualOrigin))
                {
                    int drawWaveW = Math.Clamp((int)MathF.Round(WarpGlassW * printProgress), 2, visibleWaveW);
                    DrawHarmonicWaveform(_warpStripActualOrigin, waveX, waveY, WarpGlassW, WarpGlassH, drawWaveW,
                        Palette.CrtAmberPrimary, new Color(180, 110, 32, 95));
                }
            }
        }
        else
        {
            // Semi-translucent magnetic film over Glowing Warp Checker backlit glass:
            // 1. Opaque frame rails + non-overlapping canvas areas
            Raylib.DrawRectangle(x, y, w, StripWaveOffsetY, new Color(32, 27, 20, 245));
            Raylib.DrawRectangle(x, waveY, StripWaveOffsetX, WarpGlassH, new Color(32, 27, 20, 245));
            Raylib.DrawRectangle(x + w - StripWaveOffsetX, waveY, StripWaveOffsetX, WarpGlassH, new Color(32, 27, 20, 245));

            // Draw non-overlapping left/right/top/bottom portions of film canvas opaquely if partially overhanging
            if (ovLeft > waveX)
                Raylib.DrawRectangle(waveX, waveY, ovLeft - waveX, WarpGlassH, new Color(14, 13, 11, 244));
            if (ovRight < waveX + visibleWaveW)
                Raylib.DrawRectangle(ovRight, waveY, (waveX + visibleWaveW) - ovRight, WarpGlassH, new Color(14, 13, 11, 244));
            if (ovTop > waveY)
                Raylib.DrawRectangle(ovLeft, waveY, ovRight - ovLeft, ovTop - waveY, new Color(14, 13, 11, 244));
            if (ovBottom < waveY + WarpGlassH)
                Raylib.DrawRectangle(ovLeft, ovBottom, ovRight - ovLeft, (waveY + WarpGlassH) - ovBottom, new Color(14, 13, 11, 244));

            // 2. Semi-translucent magnetic film tint across the backlit glass overlap so reference glows through!
            Raylib.DrawRectangle(ovLeft, ovTop, ovRight - ovLeft, ovBottom - ovTop, new Color(30, 22, 12, 136));

            // Draw any non-overlapping portion of the strip's waveform normally
            if (ovLeft > waveX)
            {
                Raylib.BeginScissorMode(waveX, waveY, ovLeft - waveX, WarpGlassH);
                DrawHarmonicWaveform(_warpStripActualOrigin, waveX, waveY, WarpGlassW, WarpGlassH, visibleWaveW,
                    Palette.CrtAmberPrimary, new Color(180, 110, 32, 85));
                Raylib.EndScissorMode();
            }
            if (ovRight < waveX + visibleWaveW)
            {
                Raylib.BeginScissorMode(ovRight, waveY, (waveX + visibleWaveW) - ovRight, WarpGlassH);
                DrawHarmonicWaveform(_warpStripActualOrigin, waveX, waveY, WarpGlassW, WarpGlassH, visibleWaveW,
                    Palette.CrtAmberPrimary, new Color(180, 110, 32, 85));
                Raylib.EndScissorMode();
            }

            // 3. Point-by-point comparison along the backlit overlap:
            //    - Matching segments align cleanly in a warm bright amber/white glow
            //    - Mismatching segments bleed/glow distinctly RED wherever the underlying reference deviates!
            StarSystemSignatureInfo refSys = WarpSignatureGenerator.SectorSystems[
                Math.Clamp(_warpRefSpotSystemIndex, 0, WarpSignatureGenerator.SectorSystems.Length - 1)];

            Color matchCore = new(255, 246, 214, 255);
            Color matchHalo = new(255, 198, 92, 185);
            Color mismatchBleed = new(225, 34, 34, 118);
            Color mismatchRefRed = new(255, 48, 48, 255);
            Color mismatchRefGlow = new(220, 28, 28, 175);
            Color mismatchStripCol = new(255, 136, 84, 255);

            int mismatchCols = 0;
            Raylib.BeginScissorMode(ovLeft, ovTop, ovRight - ovLeft, ovBottom - ovTop);

            int prevStripY = 0;
            int prevRefY = 0;
            bool hasPrev = false;

            for (int sx = ovLeft; sx < ovRight; sx++)
            {
                int stripLocalX = sx - waveX;
                int refLocalX = sx - WarpGlassX;
                int stripScreenY = waveY + WarpSignatureGenerator.SamplePixelY(_warpStripActualOrigin, stripLocalX, WarpGlassW, WarpGlassH);
                int refScreenY = WarpGlassY + WarpSignatureGenerator.SamplePixelY(refSys.Key, refLocalX, WarpGlassW, WarpGlassH);
                int dy = Math.Abs(stripScreenY - refScreenY);
                bool isMatch = dy <= 2;

                if (!isMatch)
                {
                    mismatchCols++;
                    int yMin = Math.Min(stripScreenY, refScreenY);
                    int yMax = Math.Max(stripScreenY, refScreenY);
                    // Distinct RED mismatch bleed corridor between deviating curves
                    Raylib.DrawLine(sx, yMin, sx, yMax, mismatchBleed);
                    // Subtle red aura around underlying reference wave
                    Raylib.DrawPixel(sx, refScreenY - 1, mismatchRefGlow);
                    Raylib.DrawPixel(sx, refScreenY + 1, mismatchRefGlow);
                }
                else
                {
                    // Warm bright amber/white backlit halo where curves align cleanly
                    int midY = (stripScreenY + refScreenY) / 2;
                    Raylib.DrawPixel(sx, midY - 1, matchHalo);
                    Raylib.DrawPixel(sx, midY + 1, matchHalo);
                }

                if (hasPrev)
                {
                    if (isMatch)
                    {
                        int prevMid = (prevStripY + prevRefY) / 2;
                        int curMid = (stripScreenY + refScreenY) / 2;
                        Raylib.DrawLine(sx - 1, prevMid, sx, curMid, matchCore);
                    }
                    else
                    {
                        // Underlying glowing reference line bleeds vivid RED
                        Raylib.DrawLine(sx - 1, prevRefY, sx, refScreenY, mismatchRefRed);
                        Raylib.DrawLine(sx - 1, prevRefY + 1, sx, refScreenY + 1, mismatchRefGlow);
                        // Printed strip trace in warm red-amber
                        Raylib.DrawLine(sx - 1, prevStripY, sx, stripScreenY, mismatchStripCol);
                    }
                }

                prevStripY = stripScreenY;
                prevRefY = refScreenY;
                hasPrev = true;
            }

            Raylib.EndScissorMode();
            hasOverlapMismatch = mismatchCols > 6;
        }

        // Slim Bottom Grip / Label Edge (11px tall below the 216x52 waveform canvas -- keeps < REF: SYSTEM > 100% uncovered!)
        Raylib.DrawRectangle(x + 1, gripY, Math.Max(0, w - 2), Math.Max(1, gripH - 1), new Color(32, 28, 22, 252));
        Raylib.DrawLine(x + 1, gripY, x + w - 2, gripY, Palette.OxidizedBronzeWorn);

        if (w >= 64)
        {
            string gripLeft = $"WARP: {_warpStripShipReg}";
            Raylib.DrawText(TruncateByPixelWidth(gripLeft, 10, Math.Max(20, w - 76)), x + 6, gripY + 1, 10, Palette.CrtAmberPrimary);
        }
        if (w >= 156)
        {
            string gripRight = !isTornOff
                ? (printProgress >= 1.0f ? "DRAG TO TEAR" : "PRINTING...")
                : (hasSubstantialOverlap ? "BACKLIT FILM" : "DRAG TO CHECKER");
            Color rightCol = !isTornOff
                ? (printProgress >= 1.0f ? Palette.ClearanceTeal : Palette.CrtAmberDimmed)
                : (hasSubstantialOverlap ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            int rw = Raylib.MeasureText(gripRight, 10);
            Raylib.DrawText(gripRight, x + w - rw - 5, gripY + 1, 10, rightCol);
        }

        // Outer Frame Border (shows subtle optical rim feedback when aligned/mismatching over checker)
        Color borderCol = stripFault
            ? GetFaultPulseColor()
            : (reviewSelected
                ? Palette.ClearanceTeal
                : (hasSubstantialOverlap
                    ? (hasOverlapMismatch ? Palette.AlertCrimson : new Color(255, 236, 184, 255))
                    : (reviewHover || isTopmost ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn)));
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);

        // Perforated left tear-off seam (L -> R extrusion)
        for (int ty = y + 3; ty < y + h - 3; ty += 6)
        {
            Raylib.DrawLine(x + 1, ty, x + 1, ty + 3, Palette.CrtAmberDimmed);
        }
    }

    // =========================================================================
    // Recessed Inactive Hardware Bays with Light Low-Contrast [PLAYTEST] Stencils
    // =========================================================================
    private static void DrawInactiveHardwareBay(int x, int y, int w, int h, string moduleName, string? unlockTag)
    {
        DrawBrushedMetalFillerPlate(x, y, w, h);
        DrawInactiveStencilText(x, y, w, h, moduleName, unlockTag);
    }

    private static void DrawInactiveStencilText(int x, int y, int w, int h, string moduleName, string? unlockTag)
    {
        Color stencilNameCol = new(74, 84, 98, 255);
        Color playtestBadgeCol = new(96, 80, 52, 255);
        int maxTextPx = Math.Max(20, w - 14);

        string inlineCandidate = string.IsNullOrEmpty(unlockTag)
            ? $"{moduleName} -- [PLAYTEST]"
            : $"{moduleName} -- {unlockTag} [PLAYTEST]";

        // If bay is wide and short (or fits cleanly on 1 centered line with generous padding), print single stencil line
        if (h <= 40 && Raylib.MeasureText(inlineCandidate, 10) <= maxTextPx)
        {
            int tw = Raylib.MeasureText(inlineCandidate, 10);
            int tx = x + (w - tw) / 2;
            int ty = y + (h - 10) / 2;
            string prefix = string.IsNullOrEmpty(unlockTag) ? $"{moduleName} -- " : $"{moduleName} -- {unlockTag} ";
            Raylib.DrawText(prefix, tx, ty, 10, stencilNameCol);
            Raylib.DrawText("[PLAYTEST]", tx + Raylib.MeasureText(prefix, 10), ty, 10, playtestBadgeCol);
            return;
        }

        // Tall / compact bays: clean 2- or 3-line centered stencil
        if (h >= 72 && !string.IsNullOrEmpty(unlockTag) && Raylib.MeasureText(moduleName, 10) <= maxTextPx)
        {
            string line1 = TruncateByPixelWidth(moduleName, 10, maxTextPx);
            string line2 = unlockTag;
            const string line3 = "[PLAYTEST]";
            int startY = y + (h - 36) / 2;
            Raylib.DrawText(line1, x + (w - Raylib.MeasureText(line1, 10)) / 2, startY, 10, stencilNameCol);
            Raylib.DrawText(line2, x + (w - Raylib.MeasureText(line2, 10)) / 2, startY + 13, 10, stencilNameCol);
            Raylib.DrawText(line3, x + (w - Raylib.MeasureText(line3, 10)) / 2, startY + 26, 10, playtestBadgeCol);
            return;
        }

        string topText = string.IsNullOrEmpty(unlockTag)
            ? TruncateByPixelWidth(moduleName, 10, maxTextPx)
            : TruncateByPixelWidth(Raylib.MeasureText($"{moduleName} -- {unlockTag}", 10) <= maxTextPx
                ? $"{moduleName} -- {unlockTag}"
                : moduleName, 10, maxTextPx);
        string botText = !string.IsNullOrEmpty(unlockTag) && topText == moduleName
            ? $"{unlockTag} [PLAYTEST]"
            : "[PLAYTEST]";
        botText = TruncateByPixelWidth(botText, 10, maxTextPx);

        int lineGap = h <= 44 ? 12 : 14;
        int blockH = 10 + lineGap;
        int topY = y + Math.Max(4, (h - blockH) / 2);
        Raylib.DrawText(topText, x + (w - Raylib.MeasureText(topText, 10)) / 2, topY, 10, stencilNameCol);
        Raylib.DrawText(botText, x + (w - Raylib.MeasureText(botText, 10)) / 2, topY + lineGap, 10, playtestBadgeCol);
    }

    private static void DrawBrushedMetalFillerPlate(int x, int y, int w, int h)
    {
        // Outer recessed gunmetal seam
        Raylib.DrawRectangle(x, y, w, h, Palette.OutlineVoid);
        // Dark brushed metal base fill
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, h - 2, new Color(25, 30, 37, 255));

        // Subtle horizontal brushed-steel grain lines (strictly pixel-aligned)
        Color grainLight = new(30, 36, 44, 255);
        Color grainDark = new(21, 26, 32, 255);
        for (int py = y + 3; py < y + h - 3; py += 4)
        {
            Raylib.DrawLine(x + 3, py, x + w - 4, py, ((py / 4) % 2 == 0) ? grainLight : grainDark);
        }

        // Crisp 1px top-left specular bevel & bottom-right shadow bevel
        Raylib.DrawLine(x + 1, y + 1, x + w - 2, y + 1, new Color(45, 53, 64, 255));
        Raylib.DrawLine(x + 1, y + 1, x + 1, y + h - 2, new Color(40, 48, 58, 255));
        Raylib.DrawLine(x + 1, y + h - 2, x + w - 2, y + h - 2, new Color(14, 17, 22, 255));
        Raylib.DrawLine(x + w - 2, y + 1, x + w - 2, y + h - 2, new Color(14, 17, 22, 255));

        // 4 subtle recessed corner hex screws (only when panel is large enough)
        if (w >= 28 && h >= 24)
        {
            int[] rx = [x + 5, x + w - 7];
            int[] ry = [y + 5, y + h - 7];
            foreach (int bx in rx)
            {
                foreach (int by in ry)
                {
                    Raylib.DrawRectangle(bx, by, 2, 2, Palette.OutlineVoid);
                    Raylib.DrawPixel(bx, by, new Color(54, 62, 74, 255));
                }
            }
        }
    }

    // =========================================================================
    // Harmonic Waveforms & UI Helpers
    // =========================================================================
    private bool IsTargetFaultHighlighted(ReviewTarget target) =>
        _faultStage == FaultSequenceStage.FaultBeat &&
        target != ReviewTarget.None &&
        (_faultHighlightA == target || _faultHighlightB == target);

    private Color GetFaultPulseColor() =>
        ((_animTick / 4) % 2) == 0 ? Palette.AlertCrimson : Palette.CrtAmberPrimary;

    private static void DrawThickLine(int x0, int y0, int x1, int y1, int thickness, Color col)
    {
        Raylib.DrawLineEx(new Vector2(x0, y0), new Vector2(x1, y1), thickness, col);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    private static void DrawReviewHotspotOutline(VirtualMouseState mouse, int x, int y, int w, int h, bool selected)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color col = selected ? Palette.ClearanceTeal : (hovered ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);
        Raylib.DrawRectangleLines(x, y, w, h, col);
        if (selected || hovered)
        {
            Raylib.DrawRectangleLines(x - 1, y - 1, w + 2, h + 2, col);
        }
    }

    private static void DrawHarmonicWaveform(
        string originSystem,
        int x,
        int y,
        int width,
        int height,
        int visibleWidth,
        Color waveColor,
        Color haloColor)
    {
        int drawW = Math.Clamp(visibleWidth, 0, width);
        if (drawW <= 1)
            return;

        int prevY = y + WarpSignatureGenerator.SamplePixelY(originSystem, 0, width, height);
        for (int i = 1; i < drawW; i++)
        {
            int curY = y + WarpSignatureGenerator.SamplePixelY(originSystem, i, width, height);
            Raylib.DrawLine(x + i - 1, prevY - 1, x + i, curY - 1, haloColor);
            Raylib.DrawLine(x + i - 1, prevY + 1, x + i, curY + 1, haloColor);
            Raylib.DrawLine(x + i - 1, prevY, x + i, curY, waveColor);
            prevY = curY;
        }
    }

    private void AddTranscriptLine(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _transcriptLines.Add((text, color));
        if (_transcriptLines.Count > 64)
        {
            _transcriptLines.RemoveAt(0);
        }
        _transcriptAutoFollowBottom = true;
    }

    private List<(string Line, Color Col)> WrapTranscriptLinesByPixelWidth(int maxPixelWidth)
    {
        List<(string Line, Color Col)> output = [];
        foreach (var (text, col) in _transcriptLines)
        {
            foreach (string line in WrapTextByPixelWidth(text, fontSize: 10, maxPixelWidth, maxLines: 64))
            {
                output.Add((line, col));
            }
        }
        return output;
    }

    private static string ShortClass(ShipClass shipClass) =>
        shipClass switch
        {
            ShipClass.Military => "MIL",
            ShipClass.Civilian => "CIV",
            ShipClass.Cargo => "CRG",
            ShipClass.Diplomatic => "DIP",
            ShipClass.HackJob => "HCK",
            _ => "CIV"
        };

    private static string TruncateByPixelWidth(string value, int fontSize, int maxPixelWidth)
    {
        if (string.IsNullOrEmpty(value) || maxPixelWidth <= 0)
            return string.Empty;

        if (Raylib.MeasureText(value, fontSize) <= maxPixelWidth)
            return value;

        int len = value.Length;
        while (len > 1 && Raylib.MeasureText(value[..len], fontSize) > maxPixelWidth)
        {
            len--;
        }
        return value[..len];
    }

    private static List<string> WrapTextByPixelWidth(string text, int fontSize, int maxPixelWidth, int maxLines)
    {
        List<string> lines = [];
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
            return lines;

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            string candidate = current.Length == 0 ? word : $"{current} {word}";

            if (Raylib.MeasureText(candidate, fontSize) <= maxPixelWidth)
            {
                current = candidate;
            }
            else if (current.Length == 0)
            {
                lines.Add(TruncateByPixelWidth(word, fontSize, maxPixelWidth));
                if (lines.Count >= maxLines)
                    return lines;
            }
            else
            {
                lines.Add(TruncateByPixelWidth(current, fontSize, maxPixelWidth));
                if (lines.Count >= maxLines)
                    return lines;
                current = Raylib.MeasureText(word, fontSize) <= maxPixelWidth
                    ? word
                    : TruncateByPixelWidth(word, fontSize, maxPixelWidth);
            }
        }

        if (current.Length > 0 && lines.Count < maxLines)
        {
            lines.Add(TruncateByPixelWidth(current, fontSize, maxPixelWidth));
        }

        return lines;
    }

    private static void DrawManualTabButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string label,
        bool active)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Color bg = active ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid;
        Color border = (active || hovered) ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Color fg = active ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        Raylib.DrawText(label, x + 6, y + 4, 10, fg);
    }

    private static void DrawHardwareButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string label,
        bool enabled,
        bool highlight,
        Color activeColor)
    {
        bool hovered = enabled && mouse.IsHit(x, y, w, h);
        Color bg = !enabled
            ? Palette.OutlineVoid
            : (hovered || highlight ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Color border = !enabled
            ? Palette.WeatheredGunmetalEdge
            : (hovered || highlight ? activeColor : Palette.CrtAmberDimmed);
        Color textCol = !enabled
            ? Palette.WeatheredGunmetalEdge
            : (highlight || hovered ? activeColor : Palette.CrtAmberPrimary);

        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int tw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, x + Math.Max(4, (w - tw) / 2), y + (h - 10) / 2, 10, textCol);
    }

    private static void DrawSmallButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string label,
        Color? textOverride = null)
    {
        bool hovered = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hovered ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        int tw = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, x + Math.Max(4, (w - tw) / 2), y + (h - 10) / 2, 10, textOverride ?? Palette.CrtAmberPrimary);
    }
}
