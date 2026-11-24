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
public sealed partial class DeskScreen : IScreen
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
        Day1VorstLiveHail = 1,
        Day3KordaniLiveHail = 2
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
        WarpStrip = 2,
        ContrabandSlate = 3
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
    private const int PowerModuleX = 1172;
    private const int PowerModuleY = 2;
    private const int PowerModuleW = 56;
    private const int PowerModuleH = 28;
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

    // Dynamic Upper/Lower-Right Region dimensions are routed strictly through DeskLayoutRects (_layout)
    // computed from ConfigFlags.DeskUpperLayoutMode (OptionA_Classic / OptionB1_CommsSwap / OptionB2_CommsSwapFlush).
    private const int ScrollbarW = DeskLayoutRects.ScrollbarW;
    private const int DataSlateW = DeskLayoutRects.DataSlateW;
    private const int DataSlateH = DeskLayoutRects.DataSlateH;
    private const int ClearanceHeaderH = DeskLayoutRects.ClearanceHeaderH;

    // 3. Row 2 Left Region (y = 264..352, h = 88, x = 8..672 -- identical across A / B1 / B2)
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

    // Desk Reference Manual (Day 4+ only -- 1. PREFIXES & 2. HULLS only; tightened height 192px with zero dead space)
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
    private const float FaultBeatDurationSeconds = ShiftController.ProtocolFaultBeatSeconds;
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

    private readonly ShiftController _shiftController;
    private readonly ShipGenerator _shipGenerator;
    private readonly ValidationEngine _validationEngine = new();
    private readonly WorldSim _worldSim = new();
    private readonly EconomyEngine _economyEngine = new();
    private readonly LedgerOverlay _ledgerOverlay = new();
    private readonly CommsTypewriterQueue _commsQueue = new();

    // Shared Booth 04 layout state computed each frame from ConfigFlags.DeskUpperLayoutMode & DevDebugState.Use3xPortraitInLayoutB
    private DeskLayoutRects _layout;
    private DeskUpperLayoutMode _lastAppliedLayoutMode;
    public DeskLayoutRects CurrentLayout => _layout;
    public CommsTypewriterQueue CommsQueue => _commsQueue;

    // Clock-Out ~4.0s Sequential Booth 04 Power-Down, Plain-Text Leave Prompt & End-of-Shift Ledger state
    private const string ClockOutLeavePromptText = "Watch complete. Click to head back to quarters.";
    private bool _clockOutPowerDownActive;
    private float _clockOutPowerDownElapsed;
    private bool _clockOutAwaitingLeavePrompt;
    private bool _clockOutSettled;
    private int _clockOutRelaySoundStage;

    /// <summary>
    /// Diegetic Booth 04 POWER indicator brightness (0.0 = dark/off, 1.0 = steady full power).
    /// Smoothly fades to 0% during nightly shutdown and supports future brownout dimming/flickering.
    /// </summary>
    public float PowerIndicatorBrightness { get; private set; } = 1.0f;
    public bool IsClockOutPowerDownActive => _clockOutPowerDownActive;
    public bool IsAwaitingClockOutLeavePrompt => _clockOutAwaitingLeavePrompt;

    // Prompt 4 / Build 4.3.1: Contraband Lap Pocket (1->3), Blank Data Slate Port-Swap & Burnt Covert Slates
    private ContrabandPocketItem? _activeDeskContrabandItem;
    private readonly List<(int X, int Y)> _parkedBurntSlatesOnDesk = [];
    private bool _contrabandSlateInSlateBus;
    private bool _isDownloadingToBlankSlate;
    private bool _isUploadingCovertMissionSlate;
    private float _covertUploadElapsed;
    private const float CovertUploadDurationSeconds = ShiftController.StandardTransferDurationSeconds * 0.75f;
    private const int ContrabandSlateW = 160;
    private const int ContrabandSlateH = 188;

    public bool IsLedgerOpen => _ledgerOverlay.IsOpen;
    public bool IsBlankSlateDownloading =>
        _isDownloadingToBlankSlate && _phase == EncounterPhase.DownloadingProtocols;
    public float BlankSlateDownloadProgress =>
        IsBlankSlateDownloading
            ? Math.Clamp(_protocolDownloadElapsed / ProtocolDownloadDurationSeconds, 0f, 1f)
            : 0f;
    public bool HasActiveProtocolFaultTrace =>
        _activeShip != null &&
        (_faultStage != FaultSequenceStage.None || _activeShip.ProtocolReviewVerified) &&
        _faultHighlightA != ReviewTarget.None &&
        _faultHighlightB != ReviewTarget.None;

    // Pre-Shift Live Kordani Hail state (Day 1 Commander Vorst & Day 3 Kordani Security Officer)
    private PreShiftStage _preShiftStage = PreShiftStage.None;
    private string[] _kordaniHailLines = [];

    private bool IsPreShiftTalkingHeadHailActive =>
        _preShiftStage is PreShiftStage.Day1VorstLiveHail or PreShiftStage.Day3KordaniLiveHail;

    public bool IsPreShiftHailActive => IsPreShiftTalkingHeadHailActive;
    public bool IsDay1VorstHailActive => _preShiftStage == PreShiftStage.Day1VorstLiveHail;
    public bool IsDay3KordaniHailActive => _preShiftStage == PreShiftStage.Day3KordaniLiveHail;
    public bool HasPreShiftPortraitTextureLoaded => _cachedPreShiftKordaniPortraitTex.Id != 0;
    public bool CanTriggerNextHailNow =>
        _shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty && _preShiftStage == PreShiftStage.None;
    public float ShiftElapsedSeconds => _shiftController.ElapsedShiftSeconds;
    public float ActiveShiftDurationSeconds => _shiftController.ActiveShiftDurationSeconds;
    public float BrownDwarfArcProgress => _shiftController.BrownDwarfArcProgress;
    public bool IsShiftTimerExpired => _shiftController.IsShiftTimerExpired;

    private EncounterPhase _phase = EncounterPhase.BayEmpty;
    private ShipSpec? _activeShip;
    private uint _debugReseedNonce;

    // Primary Dark-Steel Data Slate Physical Port & Telemetry State
    private DataSlateLocation _slateLocation = DataSlateLocation.SlateBus;
    private DataSlateTelemetryState _slateTelemetry = DataSlateTelemetryState.Empty;
    private float _slateWipeFlashTimer;
    private float _pendingReTransmitRestartTimer;
    private float _clearanceTransmitElapsed;

    // Protocol Review State (Unlocked Day 1, Ship 1)
    private const float NoFaultTraceDurationSeconds = 0.60f;
    private bool _protocolReviewModeActive;
    private ReviewTarget _reviewFirstSelection = ReviewTarget.None;
    private string _reviewStatusNote = string.Empty;
    private string _verifiedViolationSummary = string.Empty;
    private float _noFaultTraceTimer;
    private ReviewTarget _noFaultTargetA = ReviewTarget.None;
    private ReviewTarget _noFaultTargetB = ReviewTarget.None;

    // Day 2 "Ghost Route" Suspense Tweaks
    private bool _ghostRoutePushbackUsed;
    private float _ghostRouteTransponderFlickerTimer;

    // Tactile Inspection Weight: 0.5s PROTOCOL FAULT beat integrated cleanly into CommsTypewriterQueue
    private FaultSequenceStage _faultStage = FaultSequenceStage.None;
    private ReviewTarget _faultHighlightA = ReviewTarget.None;
    private ReviewTarget _faultHighlightB = ReviewTarget.None;
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
    private string _warpStripShipPrefix = string.Empty;
    private string _warpStripShipName = string.Empty;
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

    // Thematic Scrollable Dialogue Transcript Log scroll state (text & streaming owned by _commsQueue)
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

    // Desk Reference Manual state (Day 4+)
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

    // Progressive Unlock Gates (configurable via /data/config.json -> ConfigFlags)
    public bool IsSpsUnlocked => _gameState.Day >= Constants.DayUnlockSpsAndCombat;
    public bool AreCombatOrdersUnlocked => _gameState.Day >= Constants.DayUnlockSpsAndCombat;
    public bool IsProtocolReviewUnlocked => true; // Active starting Day 1, Ship 1
    public bool AreSecurityStrikesUnlocked => _gameState.Day >= Constants.DayUnlockWarpAndStrikes;
    public bool IsSpectrometerOperational => _gameState.Day >= Constants.DayUnlockWarpAndStrikes;
    public bool IsManualUnlocked => _gameState.Day >= _config.DayUnlockManualPart1;
    public bool IsManualHullsEnforced => _gameState.Day >= _config.DayUnlockManualHulls;
    public bool IsWaferWriterOperational => _gameState.Day >= _config.DayUnlockWaferCargo;
    public bool IsCargoPowerUnlocked => _gameState.Day >= _config.DayUnlockWaferCargo;

    private float ClockOutPowerDownProgress
    {
        get
        {
            if (_clockOutSettled || _clockOutAwaitingLeavePrompt)
                return 1.0f;
            if (!_clockOutPowerDownActive)
                return 0.0f;
            float delaySec = Math.Clamp(_config.ClockOutPowerDownDelaySeconds, 3.8f, 4.2f);
            return Math.Clamp(_clockOutPowerDownElapsed / delaySec, 0f, 1f);
        }
    }

    private bool CanTriggerGhostRoutePushback =>
        _preShiftStage == PreShiftStage.None &&
        _activeShip != null &&
        _activeShip.GroundTruth.IsDay2GhostRouteSuspect &&
        !_ghostRoutePushbackUsed &&
        _phase is EncounterPhase.InspectingSlateAndShip
            or EncounterPhase.UploadingClearance
            or EncounterPhase.AwaitingClearanceReturnToTopPort &&
        _slateTelemetry is DataSlateTelemetryState.Loaded
            or DataSlateTelemetryState.UploadingClearance
            or DataSlateTelemetryState.ClearedReadyToTransmit;

    private bool IsDataSlateInSlateBus => _slateLocation == DataSlateLocation.SlateBus;
    private bool IsDataSlateInClearanceDock => _slateLocation == DataSlateLocation.ClearanceDock;
    private bool HasValidBlankSlateInSlateBus =>
        _contrabandSlateInSlateBus &&
        _activeDeskContrabandItem != null &&
        !_activeDeskContrabandItem.IsBurnt &&
        _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBlankDataSlate;
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

        _shiftController = new ShiftController(config);
        _shipGenerator = new ShipGenerator(config);

        _layout = DeskLayoutRects.Compute(_config.DeskUpperLayoutMode, _devDebug.Use3xPortraitInLayoutB);
        _lastAppliedLayoutMode = _layout.Mode;

        InitializeDraggableDeskItems();
    }

    public void SyncLayoutFromConfig()
    {
        DeskLayoutRects next = DeskLayoutRects.Compute(_config.DeskUpperLayoutMode, _devDebug.Use3xPortraitInLayoutB);
        bool modeChanged = next.Mode != _lastAppliedLayoutMode;
        _layout = next;

        if (modeChanged)
        {
            ReconcileItemsForLayoutChange(next);
            _lastAppliedLayoutMode = next.Mode;
        }
    }

    private void ReconcileItemsForLayoutChange(DeskLayoutRects layout)
    {
        DeskRect bounds = layout.FreeDragBounds;

        // 1. Primary Data Slate: re-anchor if docked in Slate Bus or Clearance Dock; leave mid-drag under cursor; clamp loose
        if (_deskItems.TryGetValue(DeskItemId.DataSlate, out DraggableDeskItem? slate) &&
            _draggingItemId != DeskItemId.DataSlate)
        {
            if (IsDataSlateInSlateBus)
            {
                slate.X = layout.SlateBusDockedX;
                slate.Y = layout.SlateBusDockedY;
            }
            else if (IsDataSlateInClearanceDock)
            {
                slate.X = layout.ClearanceSlot.X;
                slate.Y = layout.ClearanceSlot.Y;
            }
            else
            {
                slate.X = Math.Clamp(slate.X, bounds.X, bounds.Right - slate.Width);
                slate.Y = Math.Clamp(slate.Y, layout.FreeDeskRestMinY, bounds.Bottom - slate.Height);
            }
        }

        // 2. Contraband / Blank / Mission Data Slate: re-anchor if docked in Slate Bus; leave mid-drag under cursor; clamp loose
        if (_deskItems.TryGetValue(DeskItemId.ContrabandSlate, out DraggableDeskItem? cItem) &&
            _draggingItemId != DeskItemId.ContrabandSlate)
        {
            if (_contrabandSlateInSlateBus)
            {
                cItem.X = layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
                cItem.Y = layout.SlateBusDockedY;
            }
            else if (cItem.IsVisible)
            {
                cItem.X = Math.Clamp(cItem.X, bounds.X, bounds.Right - cItem.Width);
                cItem.Y = Math.Clamp(cItem.Y, layout.FreeDeskRestMinY, bounds.Bottom - cItem.Height);
            }
        }

        // 3. Parked burnt slates on desk: clamp cleanly inside updated FreeDragBounds
        for (int i = 0; i < _parkedBurntSlatesOnDesk.Count; i++)
        {
            var (bx, by) = _parkedBurntSlatesOnDesk[i];
            int clampedX = Math.Clamp(bx, bounds.X, bounds.Right - ContrabandSlateW);
            int clampedY = Math.Clamp(by, layout.FreeDeskRestMinY, bounds.Bottom - ContrabandSlateH);
            _parkedBurntSlatesOnDesk[i] = (clampedX, clampedY);
        }

        // 4. Desk Reference Manual: clamp cleanly inside updated FreeDragBounds (unless mid-drag)
        if (_deskItems.TryGetValue(DeskItemId.ReferenceManual, out DraggableDeskItem? manual) &&
            _draggingItemId != DeskItemId.ReferenceManual)
        {
            manual.X = Math.Clamp(manual.X, bounds.X, bounds.Right - manual.Width);
            manual.Y = Math.Clamp(manual.Y, layout.FreeDeskRestMinY, bounds.Bottom - manual.Height);
        }

        // 5. Torn-off Warp Strip: keep snapped over Glowing Warp Checker, otherwise clamp inside updated drag bounds
        if (_deskItems.TryGetValue(DeskItemId.WarpStrip, out DraggableDeskItem? strip) &&
            _draggingItemId != DeskItemId.WarpStrip &&
            _warpPhase == WarpPrinterPhase.TornOffFreeDocument &&
            !(strip.X == StripSnapX && strip.Y == StripSnapY))
        {
            strip.X = Math.Clamp(strip.X, bounds.X, bounds.Right - strip.Width);
            strip.Y = Math.Clamp(strip.Y, WarpRefSpotY, bounds.Bottom - strip.Height);
        }

        // 6. Keep transcript scroll within valid range for the new visible row count
        if (!_transcriptAutoFollowBottom)
        {
            List<WrappedTranscriptRow> wrapped = WrapTranscriptForDisplay(layout.TranscriptTextMaxPx);
            int maxTop = Math.Max(0, wrapped.Count - layout.TranscriptVisibleRows);
            _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine, 0, maxTop);
            if (_transcriptScrollTopLine >= maxTop)
            {
                _transcriptAutoFollowBottom = true;
            }
        }
    }

    private void InitializeDraggableDeskItems()
    {
        _deskItems.Clear();
        _zOrder.Clear();

        // 1. Primary Data Slate: exact 184x216 docked in active Slate Bus slot at shift start
        _deskItems[DeskItemId.DataSlate] = new DraggableDeskItem
        {
            Id = DeskItemId.DataSlate,
            X = _layout.SlateBusDockedX,
            Y = _layout.SlateBusDockedY,
            Width = DataSlateW,
            Height = DataSlateH,
            IsVisible = true
        };

        // 2. Desk Reference Manual: strictly hidden on Days 1-3; unlocked Day 4+
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

        // 4. Grey-Market Blank / Stolen / Covert Mission Data Slate pulled from Contraband Lap Pocket
        _deskItems[DeskItemId.ContrabandSlate] = new DraggableDeskItem
        {
            Id = DeskItemId.ContrabandSlate,
            X = LapPocketX + 36,
            Y = LapPocketY - ContrabandSlateH - 8,
            Width = ContrabandSlateW,
            Height = ContrabandSlateH,
            IsVisible = false
        };

        _zOrder.Add(DeskItemId.ReferenceManual);
        _zOrder.Add(DeskItemId.DataSlate);
        _zOrder.Add(DeskItemId.ContrabandSlate);
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

        _clockOutPowerDownActive = false;
        _clockOutPowerDownElapsed = 0f;
        _clockOutAwaitingLeavePrompt = false;
        _clockOutSettled = false;
        _clockOutRelaySoundStage = 0;
        PowerIndicatorBrightness = 1.0f;
        _ledgerOverlay.Close();

        _activeDeskContrabandItem = null;
        _parkedBurntSlatesOnDesk.Clear();
        _contrabandSlateInSlateBus = false;
        _isDownloadingToBlankSlate = false;
        _isUploadingCovertMissionSlate = false;
        _covertUploadElapsed = 0f;
        _ghostRoutePushbackUsed = false;
        _ghostRouteTransponderFlickerTimer = 0f;

        ResetProtocolReviewState();
        ResetFaultSequenceState();
        ResetWarpPrinterState();
        ResetSpsAnomalyState();
        ResetSlateDockMicroAnimations();

        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;

        _shiftController.StartNewShift(_gameState, _config);
        _shiftController.PrepareDailyQueue(_gameState, _shipGenerator, config: _config);

        _gameState.QuestFlags["Day3_SpectrometerRepaired"] = IsSpectrometerOperational;
        if (_gameState.Day >= Constants.DayUnlockWarpAndStrikes && _gameState.Day2SuspectDetained && !_gameState.Day3KordaniOvernightRepApplied)
        {
            _gameState.RepScore["KDF-"] = _gameState.GetRep("KDF-") + 2;
            _gameState.Day3KordaniOvernightRepApplied = true;
            _gameState.SaveToDisk();
        }

        _layout = DeskLayoutRects.Compute(_config.DeskUpperLayoutMode, _devDebug.Use3xPortraitInLayoutB);
        _lastAppliedLayoutMode = _layout.Mode;
        InitializeDraggableDeskItems();

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        if (_gameState.Day == 1 && !_gameState.Day1VorstHailCompleted)
        {
            StartDay1PreShiftVorstLiveHail();
        }
        else if (_gameState.Day == 3 && !_gameState.Day3PreShiftHailCompleted)
        {
            StartDay3PreShiftKordaniLiveHail();
        }
        else
        {
            _preShiftStage = PreShiftStage.None;
            _shiftController.IsPreShiftSequenceActive = false;
            string[] directiveLines = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                _gameState.Day,
                _gameState.Day2SuspectDetained,
                _gameState.Day2SuspectRegistration,
                _gameState.Day2SuspectDeclaredOrigin,
                _config);
            for (int d = 0; d < directiveLines.Length; d++)
            {
                AddSystemLogLine(directiveLines[d], d == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
            }
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
        _draggingItemId = null;
        _isPullingAnchoredStrip = false;
        _anchoredStripPullOffsetX = 0;
        _anchoredStripPullOffsetY = 0;
        TryAutoRecoverLooseContrabandAtClockOut();
        UnloadCachedTextures();
    }

    private void ResetProtocolReviewState()
    {
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _reviewStatusNote = string.Empty;
        _verifiedViolationSummary = string.Empty;
        _noFaultTraceTimer = 0f;
        _noFaultTargetA = ReviewTarget.None;
        _noFaultTargetB = ReviewTarget.None;
    }

    private void ResetFaultSequenceState()
    {
        _faultStage = FaultSequenceStage.None;
        _faultHighlightA = ReviewTarget.None;
        _faultHighlightB = ReviewTarget.None;
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
        _warpStripShipPrefix = string.Empty;
        _warpStripShipName = string.Empty;
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
        SyncLayoutFromConfig();

        _elapsedVisualTime += deltaTime;
        _gameState.BasePayPerShipScrip = _devDebug.PayPerShipScrip;

        // Pause Button (1236, 4, 36, 24) works on Clock-Out Ledger & during pre-shift hail
        if (mouse.LeftPressed && mouse.IsHit(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH))
        {
            _onRequestPause?.Invoke();
            return;
        }

        // Clock-Out End-of-Shift Ledger Overlay takes focus once Booth 04 powers down
        if (_ledgerOverlay.IsOpen)
        {
            if (_ledgerOverlay.UpdateAndCheckProceed(deltaTime, mouse))
            {
                _ledgerOverlay.Close();
                _screenManager.SwitchTo(ScreenId.Quarters);
            }
            return;
        }

        // Hold on quiet powered-down Booth 04 until player explicitly clicks the plain-text leave prompt (or Space/Enter)
        if (_clockOutAwaitingLeavePrompt)
        {
            PowerIndicatorBrightness = 0.0f;
            Rectangle promptRect = GetClockOutLeavePromptRect();
            if ((mouse.LeftPressed && mouse.IsHit(promptRect)) ||
                Raylib.IsKeyPressed(KeyboardKey.Space) ||
                Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                _clockOutAwaitingLeavePrompt = false;
                _clockOutSettled = true;
                AudioHooks.Play("terminal_key_confirm");
                ShiftLedgerSummary summary = _economyEngine.SettleShiftClockOut(_gameState, _config);
                _ledgerOverlay.Open(summary);
            }
            return;
        }

        float animSpeed = _devDebug.AnimationSpeedMultiplier;
        float animDt = deltaTime * animSpeed;

        // Slower sequential nightly Booth 04 shutdown (~3.8-4.2s, scaled by AnimSpeedMultiplier):
        // 1. CRT Comms Log & Hailing Viewscreen collapse into horizontal flash-lines and power off
        // 2. Desk LEDs, Slate Bus pins, and Glowing Warp Checker backlight click off sequentially
        // 3. Top-right POWER light slowly fades to 0% while ambient stays dim-on and crimson Brown Dwarf glow washes desk
        // Holds on quiet booth with clickable plain-text leave prompt (does NOT auto-pop LedgerOverlay)
        if (_shiftController.CanEndShift && !_clockOutSettled && _phase == EncounterPhase.BayEmpty && _activeShip == null)
        {
            if (!_clockOutPowerDownActive)
            {
                _clockOutPowerDownActive = true;
                _clockOutPowerDownElapsed = 0f;
                _clockOutRelaySoundStage = 0;
                _draggingItemId = null;
                _isPullingAnchoredStrip = false;
                _anchoredStripPullOffsetX = 0;
                _anchoredStripPullOffsetY = 0;
                ResetProtocolReviewState();
                if (_deskItems.TryGetValue(DeskItemId.DataSlate, out DraggableDeskItem? looseSlate) &&
                    !IsDataSlateInSlateBus &&
                    !IsDataSlateInClearanceDock)
                {
                    looseSlate.Y = Math.Clamp(looseSlate.Y, _layout.FreeDeskRestMinY, _layout.FreeDragBounds.Bottom - looseSlate.Height);
                }
                if (_deskItems.TryGetValue(DeskItemId.ReferenceManual, out DraggableDeskItem? looseManual))
                {
                    looseManual.Y = Math.Clamp(looseManual.Y, _layout.FreeDeskRestMinY, _layout.FreeDragBounds.Bottom - looseManual.Height);
                }
                // Auto-recover any loose valid Contraband Slate and discard any burnt single-use slates at Clock-Out
                TryAutoRecoverLooseContrabandAtClockOut();
                AudioHooks.Play("terminal_power_off");
                AddSystemLogLine("[BOOTH 04] SHIFT COMPLETE -- POWERING DOWN CONSOLES...", CommsTextTone.AmberPrimary);
            }

            _clockOutPowerDownElapsed += animDt;
            float delaySec = Math.Clamp(_config.ClockOutPowerDownDelaySeconds, 3.8f, 4.2f);
            float p = Math.Clamp(_clockOutPowerDownElapsed / delaySec, 0f, 1f);

            // Sequential hardware relay click-offs during Stage 2
            if (_clockOutRelaySoundStage == 0 && p >= 0.36f)
            {
                _clockOutRelaySoundStage = 1;
                AudioHooks.Play("terminal_key_click");
            }
            else if (_clockOutRelaySoundStage == 1 && p >= 0.50f)
            {
                _clockOutRelaySoundStage = 2;
                AudioHooks.Play("terminal_key_click");
            }
            else if (_clockOutRelaySoundStage == 2 && p >= 0.58f)
            {
                _clockOutRelaySoundStage = 3;
                AudioHooks.Play("terminal_key_click");
            }
            else if (_clockOutRelaySoundStage == 3 && p >= 0.66f)
            {
                _clockOutRelaySoundStage = 4;
                AudioHooks.Play("terminal_key_click");
            }

            // Stage 3 (0.65 -> 1.00): smooth fade of top-right POWER indicator LED down to 0%
            PowerIndicatorBrightness = p <= 0.65f
                ? 1.0f
                : Math.Clamp(1.0f - (p - 0.65f) / 0.35f, 0f, 1f);

            if (_clockOutPowerDownElapsed >= delaySec)
            {
                _clockOutPowerDownActive = false;
                _clockOutAwaitingLeavePrompt = true;
                PowerIndicatorBrightness = 0.0f;
            }
            return;
        }

        // Steady Booth 04 power state during normal active watch
        PowerIndicatorBrightness = 1.0f;

        // Sync Dev Debug Time Scale & One-Shot Commands
        _shiftController.TimeScale = _devDebug.ShiftTimeScale;
        if (_devDebug.PendingEndShiftNow)
        {
            _devDebug.PendingEndShiftNow = false;
            if (_preShiftStage == PreShiftStage.None)
            {
                _shiftController.ForceEndShiftNow();
                AddSystemLogLine(
                    $"[DEV] SHIFT TIMER ADVANCED TO HORIZON SUNSET ({(int)MathF.Round(_shiftController.ActiveShiftDurationSeconds)}s).",
                    CommsTextTone.AmberDimmed);
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

        if (_ghostRouteTransponderFlickerTimer > 0f)
        {
            _ghostRouteTransponderFlickerTimer = Math.Max(0f, _ghostRouteTransponderFlickerTimer - deltaTime);
        }

        if (_noFaultTraceTimer > 0f)
        {
            _noFaultTraceTimer = Math.Max(0f, _noFaultTraceTimer - deltaTime);
            if (_noFaultTraceTimer <= 0f)
            {
                _noFaultTargetA = ReviewTarget.None;
                _noFaultTargetB = ReviewTarget.None;
            }
        }

        if (_slateWipeFlashTimer > 0f)
        {
            _slateWipeFlashTimer = Math.Max(0f, _slateWipeFlashTimer - deltaTime);
        }

        // Non-blocking linear typewriter queue (65 chars/sec routine, 32 chars/sec talking-head hails,
        // scaled by AnimationSpeedMultiplier). Automatically freezes when Pause Menu or Tutorial Card is open.
        _commsQueue.Update(
            animDt,
            _config.DialogueTypewriterCharsPerSec,
            _config.TalkingHeadCharsPerSec,
            _config.TalkingHeadLinePauseSeconds,
            _config.TalkingHeadAdvanceMode);

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
        UpdateCovertSlateBusUpload(deltaTime, animDt);
        UpdateSlateDockMicroAnimations(animDt);

        // Mousewheel scroll on the CRT Comms Dialogue Log
        HandleTranscriptScrollInput(mouse);

        // Transcript scrollbar buttons always responsive
        if (mouse.LeftPressed && TryHandleTranscriptScrollButtonClicks(mouse))
        {
            return;
        }

        // Clicking inside CRT Comms Log while a line is actively typing snaps ONLY that line to 100%
        if (mouse.LeftPressed && TryHandleTranscriptClickToFinish(mouse))
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

        // Day 1+ Protocol Review Mode Pairing runs first when Review mode is actively selecting fields
        if (IsProtocolReviewUnlocked && _protocolReviewModeActive && _activeShip != null &&
            IsShipInspectableForReview)
        {
            if (TryHandleProtocolReviewClick(mouse))
            {
                return;
            }
        }

        // Movable Lower Desk Documents (Reverse Z-Order: topmost first) take priority over buried Row 2 hardware buttons
        if (TryHandleDeskItemsMousePress(mouse))
        {
            return;
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

        // Contraband Lap Pocket (8, 656, 280, 56): pull out peeking Blank/Stolen/Mission slates
        TryHandleLapPocketClick(mouse);
    }

    private void ForceReseedAndSpawnNextShip()
    {
        _debugReseedNonce++;
        _shiftController.NotifyFirstHailTriggered();
        DebugSpawnValidityMode mode = (DebugSpawnValidityMode)_devDebug.ForceSpawnValidityMode;
        StartNextShipEncounter(mode, _debugReseedNonce);
    }

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
                    _commsQueue.Clear();
                    _transcriptScrollTopLine = 0;
                    _transcriptAutoFollowBottom = true;

                    if (_activeShip.GroundTruth.IsDay2GhostRouteSuspect)
                    {
                        _ghostRouteTransponderFlickerTimer = 0.50f;
                    }

                    RebuildLivePortraitTexture();
                    AudioHooks.Play("hail_incoming_chime");
                    EnqueueCaptainLine($"HAIL [{_activeShip.Slate.RegistrationCode}]: {_activeShip.GreetingHailLine}", CommsTextTone.AmberPrimary);
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
                else if (_contrabandSlateInSlateBus &&
                         _activeDeskContrabandItem?.ItemType == ContrabandPocketItem.TypeBlankDataSlate)
                {
                    BeginBlankSlateInterceptDownload(triggeredByReTransmit: false);
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
                        else if (HasValidBlankSlateInSlateBus)
                        {
                            BeginBlankSlateInterceptDownload(triggeredByReTransmit: true, enqueueDialogue: false);
                        }
                        else
                        {
                            AddSystemLogLine("[SLATE BUS] PLUG DATA SLATE INTO SLATE BUS & CLICK [RE-TRANSMIT].", CommsTextTone.AlertCrimson);
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
                    AddSystemLogLine("[CLEARANCE DOCK] ENCRYPTION 100% -- RETURN DATA SLATE TO SLATE BUS TO TRANSMIT.", CommsTextTone.ClearanceTeal);
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
                    AddSystemLogLine("[SLATE BUS] CLEARANCE TRANSMITTED -- DEPARTURE AUTHORIZED.", CommsTextTone.ClearanceTeal);
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
                        slate.X = _layout.SlateBusDockedX;
                        slate.Y = _layout.SlateBusDockedY;
                    }

                    AddSystemLogLine("[DATA SLATE] ACTIVE TELEMETRY WIPED.", CommsTextTone.AmberDimmed);
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
            ComplianceOrderOutcome outcome = ShiftController.ResolveShieldsOrder(_gameState, _activeShip, _config);
            switch (outcome)
            {
                case ComplianceOrderOutcome.AlreadyCompliant:
                    EnqueueCaptainLine("CAPT: \"Our shields are already down, Booth 04.\"", CommsTextTone.AmberDimmed);
                    break;
                case ComplianceOrderOutcome.RefusedOutright:
                    _captainRefusedCompliance = true;
                    _activeShip.ProtocolReviewVerified = true;
                    _verifiedViolationSummary = "REFUSED ORDER TO LOWER SHIELDS";
                    AudioHooks.Play("citation_buzz");
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.RefusalLine}\"", CommsTextTone.AmberPrimary);
                    AddSystemLogLine("[SYS] COMPLIANCE REFUSAL VERIFIED.", CommsTextTone.AlertCrimson);
                    break;
                case ComplianceOrderOutcome.Complied:
                    rebuildShip = true;
                    AudioHooks.Play("terminal_key_confirm");
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.ShieldsLoweredLine}\"", CommsTextTone.AmberPrimary);
                    break;
            }
        }

        if (_pendingWeaponsOrder)
        {
            _pendingWeaponsOrder = false;
            ComplianceOrderOutcome outcome = ShiftController.ResolveWeaponsOrder(_gameState, _activeShip, _config);
            switch (outcome)
            {
                case ComplianceOrderOutcome.AlreadyCompliant:
                    EnqueueCaptainLine("CAPT: \"Our weapon mounts are already powered down.\"", CommsTextTone.AmberDimmed);
                    break;
                case ComplianceOrderOutcome.SpoofedBluff:
                    // 1st PWR DOWN WPN on deceptive spoofed ship: captain bluffs compliance while mounts stay visibly hot in SPS!
                    AudioHooks.Play("terminal_key_confirm");
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.SpoofedWeaponsBluffLine}\"", CommsTextTone.AmberPrimary);
                    break;
                case ComplianceOrderOutcome.SpoofedDeflection:
                    // 2nd+ PWR DOWN WPN while weapons are still hot: defensive refusal/excuse allowing clean Protocol Review
                    AudioHooks.Play("terminal_key_click");
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.SpoofedWeaponsDeflectionLine}\"", CommsTextTone.AmberPrimary);
                    break;
                case ComplianceOrderOutcome.RefusedOutright:
                    _captainRefusedCompliance = true;
                    _activeShip.ProtocolReviewVerified = true;
                    _verifiedViolationSummary = "REFUSED ORDER TO POWER DOWN WEAPONS";
                    AudioHooks.Play("citation_buzz");
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.RefusalLine}\"", CommsTextTone.AmberPrimary);
                    AddSystemLogLine("[SYS] COMPLIANCE REFUSAL VERIFIED.", CommsTextTone.AlertCrimson);
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
                    EnqueueCaptainLine($"CAPT: \"{_activeShip.WeaponsPoweredDownLine}\"", CommsTextTone.AmberPrimary);
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

        // Enqueue non-blocking officer request and trigger the 1.80s RX PACKET download IMMEDIATELY (zero shift-clock stall!)
        EnqueueOfficerLine("Send subspace protocols.", CommsTextTone.ClearanceTeal, preDelaySeconds: 0.10f);

        if (IsDataSlateInSlateBus)
        {
            BeginSubspaceProtocolsDownload(isReTransmit: false);
        }
        else if (_contrabandSlateInSlateBus &&
                 _activeDeskContrabandItem?.ItemType == ContrabandPocketItem.TypeBlankDataSlate)
        {
            BeginBlankSlateInterceptDownload(triggeredByReTransmit: false);
        }
        else
        {
            _phase = EncounterPhase.AwaitingPortForDownload;
            _slateTelemetry = DataSlateTelemetryState.Empty;
            AudioHooks.Play("citation_buzz");
            AddSystemLogLine("[SLATE BUS] NO SLATE DETECTED -- PLUG DATA SLATE INTO SLATE BUS.", CommsTextTone.AmberPrimary);
        }
    }

    private void BeginBlankSlateInterceptDownload(bool triggeredByReTransmit, bool enqueueDialogue = true)
    {
        if (_activeShip == null || _activeDeskContrabandItem == null ||
            _activeDeskContrabandItem.ItemType != ContrabandPocketItem.TypeBlankDataSlate)
            return;

        _phase = EncounterPhase.DownloadingProtocols;
        _isDownloadingToBlankSlate = true;
        _contrabandSlateInSlateBus = true;
        _protocolDownloadElapsed = 0f;
        _dataStreamAudioAccum = 0f;
        _pendingReTransmitRestartTimer = 0f;

        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
        cItem.Y = _layout.SlateBusDockedY;
        cItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.ContrabandSlate);

        AudioHooks.Play("data_stream");
        if (triggeredByReTransmit)
        {
            if (enqueueDialogue)
            {
                EnqueueOfficerLine("Carrier hiccup -- re-transmit subspace protocols.", CommsTextTone.ClearanceTeal);
                EnqueueCaptainLine($"CAPT: \"{_activeShip.ReTransmitAnnoyedLine}\"", CommsTextTone.AmberPrimary);
            }
            AddSystemLogLine("[SLATE BUS] RE-SYNCING SUBSPACE PROTOCOL PACKET...", CommsTextTone.ClearanceTeal);
        }
        else
        {
            // Routine, unsuspicious comms traffic only (never announce a blank/fake slate in Comms Log)
            AddSystemLogLine("[SLATE BUS] RECEIVING SUBSPACE PROTOCOL PACKET...", CommsTextTone.ClearanceTeal);
        }
    }

    private void BeginSubspaceProtocolsDownload(bool isReTransmit)
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.DownloadingProtocols;
        _isDownloadingToBlankSlate = false;
        _slateLocation = DataSlateLocation.SlateBus;
        _slateTelemetry = DataSlateTelemetryState.Downloading;
        _protocolDownloadElapsed = 0f;
        _dataStreamAudioAccum = 0f;
        _pendingReTransmitRestartTimer = 0f;

        DraggableDeskItem slateItem = _deskItems[DeskItemId.DataSlate];
        slateItem.X = _layout.SlateBusDockedX;
        slateItem.Y = _layout.SlateBusDockedY;
        slateItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.DataSlate);

        AudioHooks.Play("data_stream");
        if (isReTransmit)
        {
            AddSystemLogLine("[SLATE BUS] RE-SYNCING SUBSPACE PROTOCOL PACKET...", CommsTextTone.ClearanceTeal);
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
        AddSystemLogLine("[PORT ERROR] DATA SLATE UNPLUGGED MID-DOWNLOAD -- TRANSMISSION ABORTED.", CommsTextTone.AlertCrimson);
        AddSystemLogLine("[SLATE BUS] RE-PLUG SLATE INTO SLATE BUS AND CLICK [RE-TRANSMIT].", CommsTextTone.AmberPrimary);
    }

    private void AbortBlankSlateDownloadMidStream()
    {
        _isDownloadingToBlankSlate = false;
        _contrabandSlateInSlateBus = false;
        _protocolDownloadElapsed = 0f;
        _pendingReTransmitRestartTimer = 0f;
        _phase = EncounterPhase.DownloadAborted;
        if (_slateTelemetry != DataSlateTelemetryState.Loaded)
        {
            _slateTelemetry = DataSlateTelemetryState.DownloadAborted;
        }

        AudioHooks.Play("citation_buzz");
        AddSystemLogLine("[PORT ERROR] BLANK SLATE UNPLUGGED MID-DOWNLOAD -- TRANSMISSION ABORTED.", CommsTextTone.AlertCrimson);
        AddSystemLogLine("[SLATE BUS] RE-PLUG SLATE INTO SLATE BUS AND CLICK [RE-TRANSMIT].", CommsTextTone.AmberPrimary);
    }

    private void TryTriggerReTransmit()
    {
        if (_activeShip == null || _phase != EncounterPhase.DownloadAborted)
            return;

        if (_pendingReTransmitRestartTimer > 0f)
            return;

        if (!IsDataSlateInSlateBus && !HasValidBlankSlateInSlateBus)
        {
            AudioHooks.Play("citation_buzz");
            AddSystemLogLine("[SLATE BUS] PLUG DATA SLATE INTO SLATE BUS FIRST BEFORE RE-TRANSMIT.", CommsTextTone.AlertCrimson);
            return;
        }

        AudioHooks.Play("terminal_key_confirm");
        EnqueueOfficerLine("Link dropped. Re-transmit subspace protocols.", CommsTextTone.ClearanceTeal);
        EnqueueCaptainLine($"CAPT: \"{_activeShip.ReTransmitAnnoyedLine}\"", CommsTextTone.AmberPrimary);
        _pendingReTransmitRestartTimer = ReTransmitAnnoyedDelaySeconds;
    }

    private void CompleteSubspaceProtocolsDownload()
    {
        if (_activeShip == null)
            return;

        // Blank Data Slate Port-Swap Intercept completed!
        if (_isDownloadingToBlankSlate && _activeDeskContrabandItem != null)
        {
            _isDownloadingToBlankSlate = false;
            string reg = _activeShip.Slate.RegistrationCode;
            string pfx = _activeShip.Slate.DeclaredPrefix;
            bool concordFlag = WarpSignatureGenerator.IsRestrictedOrigin(_activeShip.GroundTruth.ActualOriginSystem) ||
                               pfx.Equals("HBR-", StringComparison.OrdinalIgnoreCase);

            _activeDeskContrabandItem.ItemType = ContrabandPocketItem.TypeCopiedDataSlate;
            _activeDeskContrabandItem.DisplayName = $"STOLEN SLATE: {reg}";
            _activeDeskContrabandItem.TargetShipPrefix = pfx;
            _activeDeskContrabandItem.TargetShipRegistration = reg;
            _activeDeskContrabandItem.TargetShipName = _activeShip.Slate.ShipName;
            _activeDeskContrabandItem.OriginStarSystem = _activeShip.GroundTruth.ActualOriginSystem;
            _activeDeskContrabandItem.IsConcordOrDoppleFlagged = concordFlag;

            _gameState.AddSecurityLog("[UNAUTH_IO]", $"SLATE_BUS PORT SWAP: {reg} ({pfx})", isSuspicious: true);
            _gameState.SaveToDisk();

            // Silent Blank Data Slate intercept: print ONLY routine, unsuspicious comms traffic in CRT Comms Log
            AudioHooks.Play("subspace_transfer");
            if (_slateTelemetry != DataSlateTelemetryState.Loaded)
            {
                _phase = EncounterPhase.DownloadAborted;
                EnqueueCaptainLine($"CAPT: \"{_activeShip.SubspaceUploadLine}\"", CommsTextTone.AmberPrimary);
                AddSystemLogLine("[SLATE BUS] PRIMARY BUFFER UNSYNCED -- PLUG PRIMARY SLATE & CLICK [RE-TRANSMIT].", CommsTextTone.AmberDimmed);
            }
            else
            {
                _phase = EncounterPhase.InspectingSlateAndShip;
                AddSystemLogLine("[SLATE BUS] SUBSPACE TELEMETRY SYNC COMPLETE.", CommsTextTone.ClearanceTeal);
            }
            return;
        }

        _phase = EncounterPhase.InspectingSlateAndShip;
        _slateTelemetry = DataSlateTelemetryState.Loaded;
        AudioHooks.Play("subspace_transfer");

        EnqueueCaptainLine($"CAPT: \"{_activeShip.SubspaceUploadLine}\"", CommsTextTone.AmberPrimary);

        // Fire standard Auto-Interrogation line starting on Day 1 (Days 1+) right after Data Slate finishes downloading
        if (_gameState.Day >= 1)
        {
            EnqueueOfficerLine("Confirm route, purpose of visit, and cargo.", CommsTextTone.ClearanceTeal, preDelaySeconds: 0.12f);
            EnqueueCaptainLine($"CAPT: \"{_activeShip.AutoInterrogationReplyLine}\"", CommsTextTone.AmberPrimary);
        }
    }

    private void TriggerGhostRoutePushback()
    {
        if (_activeShip == null || _ghostRoutePushbackUsed)
            return;

        _ghostRoutePushbackUsed = true;
        AudioHooks.Play("terminal_key_confirm");
        ShipSpec targetShip = _activeShip;
        EnqueueOfficerLine(
            _activeShip.SuspectRoutePushbackPromptLine,
            CommsTextTone.ClearanceTeal,
            onComplete: () =>
            {
                if (_activeShip == targetShip)
                {
                    _portraitStaticFlickerTimer = 0.28f;
                }
            });
        EnqueueCaptainLine($"CAPT: \"{_activeShip.SuspectRouteDeflectionLine}\"", CommsTextTone.AmberPrimary);
    }

    // =========================================================================
    // Prompt 4: Two-Way Slate Bus Covert Mission Uploads & Contraband Lap Pocket
    // =========================================================================
    private void BeginCovertMissionSlateUpload()
    {
        if (_activeShip == null || _activeDeskContrabandItem == null || !_activeDeskContrabandItem.IsMissionSlate)
            return;

        _isUploadingCovertMissionSlate = true;
        _covertUploadElapsed = 0f;
        _contrabandSlateInSlateBus = true;

        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
        cItem.Y = _layout.SlateBusDockedY;
        cItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.ContrabandSlate);

        AudioHooks.Play("data_stream");
        AddSystemLogLine($"[SLATE BUS // TX] UPLOADING COVERT MISSION PAYLOAD TO {_activeShip.Slate.RegistrationCode}...", CommsTextTone.AmberPrimary);
    }

    private void UpdateCovertSlateBusUpload(float deltaTime, float animDt)
    {
        _ = deltaTime;
        if (!_isUploadingCovertMissionSlate)
            return;

        if (_activeShip == null || _activeDeskContrabandItem == null)
        {
            _isUploadingCovertMissionSlate = false;
            _covertUploadElapsed = 0f;
            return;
        }

        _covertUploadElapsed += animDt;
        if (_covertUploadElapsed >= CovertUploadDurationSeconds)
        {
            CompleteCovertMissionSlateUpload();
        }
    }

    private void CompleteCovertMissionSlateUpload()
    {
        _isUploadingCovertMissionSlate = false;
        _covertUploadElapsed = 0f;

        if (_activeShip == null || _activeDeskContrabandItem == null)
            return;

        ContrabandPocketItem slate = _activeDeskContrabandItem;
        string dockedReg = _activeShip.Slate.RegistrationCode;
        string dockedPrefix = _activeShip.Slate.DeclaredPrefix;

        bool isIntendedRecipient =
            (_activeShip.GroundTruth.IsCovertMissionTarget &&
             _activeShip.GroundTruth.CovertMissionId.Equals(slate.MissionId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(slate.MissionTargetReg) &&
             dockedReg.Equals(slate.MissionTargetReg, StringComparison.OrdinalIgnoreCase));

        bool isPactCruiser =
            dockedPrefix.Equals("KDF-", StringComparison.OrdinalIgnoreCase) ||
            dockedPrefix.Equals("BMC-", StringComparison.OrdinalIgnoreCase);

        if (isIntendedRecipient)
        {
            // Outcome 1: Intended Mission Recipient -- surprised at first, then quiet guarded "thank you", mission advances!
            AudioHooks.Play("subspace_transfer");
            EnqueueCaptainLine(
                "CAPT: \"Wait -- what is this encrypted burst coming straight off your Slate Bus?!\"",
                CommsTextTone.AmberPrimary,
                suppressAutoReceivingLag: true);
            EnqueueCaptainLine(
                "CAPT: \"...Understood, Booth 04. Thank you. Forget this channel ever pulsed.\"",
                CommsTextTone.ClearanceTeal,
                preDelaySeconds: 0.45f,
                suppressAutoReceivingLag: true,
                onStart: () => AudioHooks.Play("comms_reply_blip"));

            string mId = string.IsNullOrWhiteSpace(slate.MissionId) ? "covert_delivery" : slate.MissionId;
            _gameState.QuestFlags[$"{mId}_completed"] = true;
            if (!_gameState.DeliveredCovertMissionIds.Contains(mId))
                _gameState.DeliveredCovertMissionIds.Add(mId);
            if (_gameState.ActiveCovertMissionId.Equals(mId, StringComparison.OrdinalIgnoreCase))
            {
                _gameState.ActiveCovertMissionId = string.Empty;
                _gameState.ActiveCovertTargetReg = string.Empty;
            }
            _gameState.CovertTasksCompleted++;

            // Apply covert mission payout & confirmation message from /data/covert_missions.json
            CovertMissionDatabase db = CovertMissionDatabase.Load();
            CovertMissionSpec? spec = db.Missions.FirstOrDefault(m => m.MissionId.Equals(mId, StringComparison.OrdinalIgnoreCase));
            int rewardSc = spec?.RewardScrip ?? 45;
            _gameState.PactScrip += rewardSc;
            if (spec != null && !string.IsNullOrWhiteSpace(spec.RewardFactionPrefix))
            {
                _worldSim.ModifyReputation(_gameState, spec.RewardFactionPrefix, spec.RewardRepDelta);
            }

            (int cy, int cm) = Constants.GetStationCycle(_gameState.Day);
            _gameState.AddEmail(new TerminalEmail
            {
                EmailId = $"covert_done_{mId}",
                DayReceived = _gameState.Day,
                PhaseReceived = "EVENING",
                CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // 20:05",
                Sender = spec?.AssigningFaction ?? "COVERT HANDLER",
                Subject = $"DROP CONFIRMED // {dockedReg}",
                Body = $"TELEMETRY UPLOAD TO {dockedReg} VERIFIED.\n\nSETTLEMENT: +{rewardSc} SC ROUTED TO YOUR BERTH ACCOUNT.\nSCRUB YOUR SECURITY LOGS BEFORE SLEEP.",
                IsRead = false
            });

            _gameState.AddSecurityLog("[COVERT_TX]", $"SLATE_BUS MISSION DROP: {dockedReg}", isSuspicious: true);
            _gameState.SaveToDisk();

            // Transition single-use covert mission slate into burnt / cracked glass (do NOT vanish/delete it!)
            // Player physically unplugs the burnt slate and sets it aside on the desk to plug Primary Slate back in.
            slate.ItemType = ContrabandPocketItem.TypeBurntMissionSlate;
            slate.IsBurnt = true;
            slate.IsMissionSlate = false;
            slate.DisplayName = "BURNT MISSION SLATE";
            _activeDeskContrabandItem = slate;
            _contrabandSlateInSlateBus = true;
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
            AddSystemLogLine($"[COVERT MISSION COMPLETE] PAYLOAD DELIVERED TO {dockedReg} (+{rewardSc} SC). CRYSTAL BURNT -- UNPLUG SLATE.", CommsTextTone.ClearanceTeal);
            return;
        }

        if (isPactCruiser)
        {
            // Outcome 3: Accidental Upload to a Pact Cruiser (KDF- or BMC-) -> Immediate hostile reaction + +1 Security Strike (Day 3+)!
            AudioHooks.Play("detain_alarm");
            string hostileLine = dockedPrefix.Equals("KDF-", StringComparison.OrdinalIgnoreCase)
                ? "CAPT: \"Unauthorized telemetry injection on a Kordani Defense Fleet bus?! Security Command is logging Booth 04 immediately!\""
                : "CAPT: \"Illegal non-clearance burst onto a Brengop Consortium hull?! Factor Audit is flagging your booth right now!\"";
            EnqueueCaptainLine(hostileLine, CommsTextTone.AlertCrimson, suppressAutoReceivingLag: true);

            _gameState.AddSecurityLog("[AUDIT_FLAG]", $"HOSTILE COVERT TX -> PACT HULL {dockedReg}", isSuspicious: true);

            if (AreSecurityStrikesUnlocked)
            {
                _validationEngine.AddSecurityStrike(_gameState, 1, _config);
                _activeCitationStrip = new DecisionEvaluationResult
                {
                    RegistrationCode = dockedReg,
                    ShipName = _activeShip.Slate.ShipName,
                    IsCitation = true,
                    IsSecurityStrike = true,
                    HeaderText = "KORDANI SECURITY COMMAND // SECURITY STRIKE ISSUED",
                    ViolationReason = $"UNAUTHORIZED COVERT SLATE BUS UPLOAD TO PACT HULL ({dockedReg})",
                    PenaltyText = $"KORDANI SECURITY STRIKE +1 ({_gameState.SecurityStrikes}/{_config.MaxStrikesBeforeAudit})"
                };
            }
            else
            {
                int fine = _validationEngine.ApplyClericalMistake(_gameState);
                _activeCitationStrip = new DecisionEvaluationResult
                {
                    RegistrationCode = dockedReg,
                    ShipName = _activeShip.Slate.ShipName,
                    IsCitation = true,
                    IsSecurityStrike = false,
                    HeaderText = "BRENGOP MERCANTILE OPS // PROTOCOL CITATION",
                    ViolationReason = $"UNAUTHORIZED SLATE BUS UPLOAD TO PACT HULL ({dockedReg})",
                    PenaltyText = fine == 0
                        ? $"CLERICAL WARNING {_gameState.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift} (0 SC)"
                        : $"BRENGOP ADMINISTRATIVE FINE: -{fine} SC"
                };
            }

            _citationSlideProgress = 0f;
            _citationDisplayTimer = 8.5f;
            _gameState.SaveToDisk();

            // Eject mission slate back onto desk cleanly within active FreeDragBounds
            _contrabandSlateInSlateBus = false;
            DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
            cItem.X = Math.Clamp(480, _layout.FreeDragBounds.X, _layout.FreeDragBounds.Right - cItem.Width);
            cItem.Y = Math.Clamp(420, _layout.FreeDeskRestMinY, _layout.FreeDragBounds.Bottom - cItem.Height);
            return;
        }

        // Outcome 2: Confused Wrong Civilian / Non-Mission Ship -> Confused reaction, no mission progress, eject cleanly!
        AudioHooks.Play("citation_buzz");
        EnqueueCaptainLine(
            "CAPT: \"Uh, Booth 04? You just dumped raw junk telemetry onto my helm buffer. Keep your Slate Bus clean!\"",
            CommsTextTone.AmberPrimary,
            suppressAutoReceivingLag: true);
        AddSystemLogLine("[SLATE BUS] WRONG RECIPIENT -- COVERT MISSION SLATE EJECTED TO DESK.", CommsTextTone.AmberDimmed);
        _contrabandSlateInSlateBus = false;
        DraggableDeskItem ejectItem = _deskItems[DeskItemId.ContrabandSlate];
        ejectItem.X = Math.Clamp(480, _layout.FreeDragBounds.X, _layout.FreeDragBounds.Right - ejectItem.Width);
        ejectItem.Y = Math.Clamp(420, _layout.FreeDeskRestMinY, _layout.FreeDragBounds.Bottom - ejectItem.Height);
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

        // Snap the actual 184x216 Data Slate visibly into the active Clearance Dock bay
        DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
        slate.X = _layout.ClearanceSlot.X;
        slate.Y = _layout.ClearanceSlot.Y;
        slate.IsVisible = true;
        BringDeskItemToFront(DeskItemId.DataSlate);

        AddSystemLogLine("> BOOTH 04: [ENCRYPTING CLEARANCE IN DOCK...]", CommsTextTone.ClearanceTeal);
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
        AddSystemLogLine("[CLEARANCE DOCK] ENCRYPTION ABORTED -- SLATE UNPLUGGED (0%).", CommsTextTone.AmberPrimary);
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
        AddSystemLogLine("[SLATE BUS] TRANSMITTING ENCRYPTED CLEARANCE TO VESSEL...", CommsTextTone.ClearanceTeal);
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
        AddSystemLogLine("[SLATE BUS] TX CLEARANCE ABORTED -- CLEARED SLATE UNPLUGGED (0%).", CommsTextTone.AmberPrimary);
    }

    private void TriggerDetainLeverClampSequence()
    {
        if (_activeShip == null)
            return;

        ResetFaultSequenceState();
        _commsQueue.SnapActiveAndDropPendingQueue();
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
        AddSystemLogLine($"> BOOTH 04: [DETAIN LEVER PULLED -- {tag}]", CommsTextTone.AlertCrimson);

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

        _ghostRoutePushbackUsed = false;
        _ghostRouteTransponderFlickerTimer = 0f;

        _activeShip = _shiftController.TakeNextShip(_gameState, _shipGenerator, forceValidity, reseedNonce, _config);

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
        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;
        AddSystemLogLine("APPROACH VECTOR LOCKED -- VESSEL GLIDING INTO BAY...", CommsTextTone.AmberDimmed);

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
            _worldSim,
            _config);

        if (result.IsCitation)
        {
            _activeCitationStrip = result;
            _citationSlideProgress = 0.0f;
            _citationDisplayTimer = 8.5f;
            AddSystemLogLine($"[CITATION] {result.ViolationReason}", CommsTextTone.AlertCrimson);
            AddSystemLogLine($"[PENALTY] {result.PenaltyText}", CommsTextTone.AmberPrimary);
        }
        else
        {
            string quietLog = _pendingDecision == PlayerDecision.GrantAccess
                ? $"[DOCKED] Vessel {result.RegistrationCode} cleared (+{result.ScripDelta} SC)."
                : $"[DETAINED] Vessel {result.RegistrationCode} detained (+{result.ScripDelta} SC).";
            AddSystemLogLine(quietLog, CommsTextTone.AmberDimmed);
        }

        UnloadEncounterTextures();
        ResetProtocolReviewState();
        ResetFaultSequenceState();
        ResetWarpPrinterState();
        ResetSpsAnomalyState();
        ResetSlateDockMicroAnimations();

        _ghostRoutePushbackUsed = false;
        _ghostRouteTransponderFlickerTimer = 0f;
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
            AddSystemLogLine("BROWN DWARF BELOW HORIZON -- WATCH COMPLETE. PROCEED TO QUARTERS.", CommsTextTone.AmberPrimary);
        }
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

    // =========================================================================
    // Virtual 1280x720 Rendering Pipeline
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        SyncLayoutFromConfig();

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
        DrawHailingViewscreen(mouse);
        DrawCrtCommsDialogueLog(mouse);

        // 4. Lower Console Desk Surface, Contraband Lap Pocket & One-Way Conduit (y = 262..720)
        DrawLowerDeskSurfaceAndConduit();

        // 5. Row 2 & Row 2B Hardware (y = 264..352)
        DrawInactiveHardwareBay(StokerDialX, StokerDialY, StokerDialW, StokerDialH, "STOKER DIAL", null);
        DrawWarpResiduePrinterAndRefSpot(mouse);
        DrawTransponderIdPanel(mouse);
        DrawSlateBusSocket();
        DrawCommsCommandStrip(mouse);
        DrawProtocolReviewButton(mouse);

        // 6. Lower Desk Hardware (Wafer Writer with top Feed Slot, Clearance Dock, Destroy Override)
        DrawWaferWriterModule();
        DrawClearanceUploadDock(mouse);
        DeskRect destroyRect = _layout.DestroyOverride;
        DrawInactiveHardwareBay(destroyRect.X, destroyRect.Y, destroyRect.W, destroyRect.H, "DESTROY OVERRIDE", null);

        // 6b. Any parked burnt single-use covert slates set aside on the desk until Clock-Out
        for (int b = 0; b < _parkedBurntSlatesOnDesk.Count; b++)
        {
            var (bx, by) = _parkedBurntSlatesOnDesk[b];
            DrawBurntContrabandSlateBody(bx, by, ContrabandSlateW, ContrabandSlateH, isTopmost: false, inBus: false);
        }

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
                case DeskItemId.ContrabandSlate:
                    if (_activeDeskContrabandItem != null)
                    {
                        DrawContrabandDeskSlate(mouse, item, isTopmost);
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

        // Protocol Review right-angle circuit trace links:
        // - Confirmed fault (PROTOCOL VIOLATED): steady crimson-red trace line with square endpoint nodes
        // - Matching pair (PROTOCOLS MATCH // NO FAULT): crisp 0.6s teal trace line
        if (HasActiveProtocolFaultTrace)
        {
            DrawCircuitTraceOverlay(_faultHighlightA, _faultHighlightB, Palette.AlertCrimson);
        }
        else if (_noFaultTraceTimer > 0f)
        {
            DrawCircuitTraceOverlay(_noFaultTargetA, _noFaultTargetB, Palette.ClearanceTeal);
        }

        // Error-Only Citation Strip: transient slide-out overlay rendered ONLY when an error actually occurs (ZERO permanent box!)
        DrawErrorOnlyCitationPopup();

        // Sequential CRT & Brown Dwarf Shift-End Power-Down (~4.0s):
        // Booth 04 ambient room lighting remains dim-on (never blacking out the screen) while a noticeably stronger
        // distinct crimson-red glow from the setting Brown Dwarf washes across the quiet desk from the armored viewport
        float pwrDownP = ClockOutPowerDownProgress;
        if (pwrDownP > 0.001f)
        {
            DrawClockOutBrownDwarfCrimsonWash(pwrDownP);
        }

        // Quiet Powered-Down Booth Leave Prompt (understated clickable plain text -- NOT a framed console button!)
        if (_clockOutAwaitingLeavePrompt && !_ledgerOverlay.IsOpen)
        {
            DrawClockOutLeavePrompt(mouse);
        }

        // Clock-Out End-of-Shift Ledger Overlay (360, 128, 560, 464) centered over Desk
        if (_ledgerOverlay.IsOpen)
        {
            _ledgerOverlay.DrawVirtual(mouse);
        }
    }

    private static Color BlendRgb(Color a, Color b, float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        byte r = (byte)Math.Clamp((int)MathF.Round(a.R + (b.R - a.R) * k), 0, 255);
        byte g = (byte)Math.Clamp((int)MathF.Round(a.G + (b.G - a.G) * k), 0, 255);
        byte bl = (byte)Math.Clamp((int)MathF.Round(a.B + (b.B - a.B) * k), 0, 255);
        return new Color(r, g, bl, (byte)255);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
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

        if (PixelText.MeasureText(value, fontSize) <= maxPixelWidth)
            return value;

        int len = value.Length;
        while (len > 1 && PixelText.MeasureText(value[..len], fontSize) > maxPixelWidth)
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

            if (PixelText.MeasureText(candidate, fontSize) <= maxPixelWidth)
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
                current = PixelText.MeasureText(word, fontSize) <= maxPixelWidth
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

}
