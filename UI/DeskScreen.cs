using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

/// <summary>
/// 960x540 Booth 04 Desk Screen featuring:
/// - Clean >= 8px outer margins on left, right, and bottom edges
/// - Transparent ship rendering over wide viewport with full left-to-right 270s Brown Dwarf arc (held at t=0 until first hail)
/// - Slow 3.5s ease-out ship docking approach & 1.85s progressive subspace protocol download animation
/// - Scissor-clipped SPS inspection lens with tuned hunger/hypoxia sway & zero auto-hint spoilers
/// - Protocol Review (Claim vs. Reality only; Manual is strictly read-only) & always-unlocked Detain/Grant Access discretion
/// - Day 3+ Manual Warp Field Scan (2.8s sweep), slow 4.0s downward thermal strip extrusion, and click-and-drag tear-off paper strip
/// - Collapsible Expected Ship List, thematic scrollable CRT transcript, error-only amber citations
/// - Pure ASCII text throughout (zero unsupported Unicode glyphs)
/// </summary>
public sealed class DeskScreen : IScreen
{
    private enum EncounterPhase
    {
        BayEmpty = 0,
        ShipGlidingIn = 1,
        PreClearanceHailing = 2,
        DownloadingProtocols = 3,
        InspectingSlateAndShip = 4,
        UploadingClearance = 5,
        ResolvingDecision = 6
    }

    private enum PreShiftStage
    {
        None = 0,
        ShiftDirectiveModal = 1,
        Day3KordaniPreShiftHail = 2
    }

    private enum WarpPrinterPhase
    {
        Idle = 0,
        ScanningWarpField = 1,
        PrintingStrip = 2,
        AnchoredReadyToTear = 3,
        TornOffFreeDocument = 4
    }

    private enum RelayQueryPhase
    {
        Idle = 0,
        TypingQuery = 1,
        Completed = 2
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
        public bool WasConfronted { get; set; }
    }

    private enum DeskItemId
    {
        ExpectedList = 0,
        DataSlate = 1,
        ReferenceManual = 2,
        WarpStrip = 3
    }

    private enum ReviewTarget
    {
        None = 0,
        DataSlatePrefix = 1,
        DataSlateWeapons = 2,
        DataSlateOrigin = 3,
        DataSlateOrg = 4,
        DataSlateExpiry = 5,
        DataSlateSpecies = 6,
        DataSlateCargo = 7,
        TopHeaderCycle = 8,
        ViewportShipHull = 9,
        SpsLensConduit = 10,
        WarpStripPaper = 11,
        RelayAlertLog = 12
    }

    private sealed class DraggableDeskItem
    {
        public DeskItemId Id { get; init; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; init; }
        public int Height { get; set; }
        public int ExpandedHeight { get; init; }
        public int CollapsedHeight { get; init; }
        public bool IsCollapsed { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    // =========================================================================
    // 960x540 Orthographic Layout Bounds (Strict >= 8px Outer Margins)
    // =========================================================================
    private const int ViewportX = 10;
    private const int ViewportY = 28;
    private const int ViewportW = 530;
    private const int ViewportH = 224;
    private const int ViewportSkyTopY = ViewportY + 18;
    private const int ViewportSkyH = ViewportH - 20;

    // 120x56 ship sprite scaled 2x -> 240x112
    private const int ShipRenderW = Constants.ShipWidth * 2;  // 240
    private const int ShipRenderH = Constants.ShipHeight * 2; // 112
    private const int ShipAnchorX = ViewportX + 145;
    private const int ShipAnchorY = ViewportY + 86;

    // Top-Right Hailing CRT Viewscreen & Scrollable Transcript (Right edge: 546 + 404 = 950 -> 10px margin)
    private const int CrtX = 546;
    private const int CrtY = 28;
    private const int CrtW = 404;
    private const int CrtH = 224;

    private const int TranscriptX = CrtX + 142; // 688
    private const int TranscriptY = CrtY + 8;   // 36
    private const int TranscriptW = 254;        // 688..942
    private const int TranscriptH = 208;
    private const int TranscriptVisibleRows = 14;

    // Bottom Desk Surface (10px left/right margins, 8px bottom margin)
    private const int DeskSurfaceX = 10;
    private const int DeskSurfaceY = 286;
    private const int DeskSurfaceW = 940;
    private const int DeskSurfaceH = 246;

    private const int DragMinX = 16;
    private const int DragMaxX = 944;
    private const int DragMinY = 288;
    private const int DragMaxY = 530;

    // Left Hardware Column: Spectrometer & Protocol Review Log (16px left margin from screen edge)
    private const int LeftColX = 16;
    private const int LeftColW = 142;
    private const int SpecY = 292;
    private const int SpecH = 104;
    private const int PrinterSlotY = SpecY + SpecH - 12; // Paper mouth slot at y = 384

    // Tear-off Warp Thermal Strip dimensions & anchor
    private const int StripAnchorX = LeftColX + 7;  // 23
    private const int StripAnchorY = PrinterSlotY + 4; // 388
    private const int StripWidth = 128;
    private const int StripFullHeight = 76;
    private const float TearOffThresholdPx = 16.0f;

    // Top Console Header Station Cycle Panel (Clickable in Protocol Review for Expiry Checks)
    private const int TopCycleX = 650;
    private const int TopCycleY = 3;
    private const int TopCycleW = 108;
    private const int TopCycleH = 18;

    // Right Hardware Column: SPS Anomaly Mini-Screen (directly below SPS button), USB Clearance Dock & Detain Switch
    private const int SpsMiniX = 800;
    private const int SpsMiniY = 288;
    private const int SpsMiniW = 144;
    private const int SpsMiniH = 50;

    private const int DockPanelX = 800;
    private const int DockPanelY = 342;
    private const int DockPanelW = 144;
    private const int DockPanelH = 102;

    private const int DetainSwitchX = 800;
    private const int DetainSwitchY = 448;
    private const int DetainSwitchW = 144;
    private const int DetainSwitchH = 48;

    private const int DestroySwitchX = 800;
    private const int DestroySwitchY = 500;
    private const int DestroySwitchW = 144;
    private const int DestroySwitchH = 26;

    // SPS Scissor Inspection Lens Dimensions & Hold-to-Lock Threshold
    private const int SpsLensW = 74;
    private const int SpsLensH = 52;
    private const float SpsLockDurationSeconds = 0.50f;
    private const float SpsLockRadiusPx = 24.0f;

    // Pacing Durations
    private const float ShipApproachDurationSeconds = 3.5f;
    private const float ProtocolDownloadDurationSeconds = 1.85f;
    private const float ClearanceUploadDurationSeconds = 1.35f;
    private const float ShipDepartureDurationSeconds = 1.35f;
    private const float RelayTypingDurationSeconds = 1.50f;
    private const float WarpScanDurationSeconds = 2.8f;
    private const float WarpPrintDurationSeconds = 4.0f;

    // Canonical 12-prefix directory for Manual Tab 0 (exact match to ShipGenerator.GetFactionFullName)
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

    private readonly ShiftController _shiftController = new();
    private readonly ShipGenerator _shipGenerator = new();
    private readonly ValidationEngine _validationEngine = new();
    private readonly WorldSim _worldSim = new();

    private PreShiftStage _preShiftStage = PreShiftStage.None;
    private bool _directiveOpenedMidShift;
    private string[] _activeDirectiveLines = [];

    private EncounterPhase _phase = EncounterPhase.BayEmpty;
    private ShipSpec? _activeShip;
    private int _queueCounter;
    private uint _sessionEntropy = 101u;
    private bool _day2SuspectQuestioned;

    // Protocol Review State (Claim vs. Reality only; never locks Detain/Grant Access)
    private bool _protocolReviewModeActive;
    private ReviewTarget _reviewFirstSelection = ReviewTarget.None;
    private string _reviewStatusNote = string.Empty;
    private string _verifiedViolationSummary = string.Empty;

    // Tactile Pacing Timers: Approach (3.5s), Protocol Download (1.85s), Clearance Upload (1.35s)
    private float _shipApproachElapsed;
    private float _shipSlideOffset;
    private float _subspaceAutoTransferDelay;
    private float _protocolDownloadElapsed;
    private float _dataStreamAudioAccum;
    private float _clearanceUploadElapsed;

    // Day 1 Subspace Relay Ping State (Reference Manual Faction / Prefix Page)
    private RelayQueryPhase _relayPhase = RelayQueryPhase.Idle;
    private float _relayTypingElapsed;
    private int _relayLastTypedChars;
    private string _relayQueryFullText = string.Empty;
    private RelayPingStatus _relayResultStatus = RelayPingStatus.ActiveClear;
    private bool _relayTargetWasUnregistered;
    private bool _relayAlertConfronted;

    // Day 1 SPS Hold-to-Lock Anomaly Log State (Mini-Screen below SPS Toggle)
    private ShipHardpointLayout? _activeShipHardpoints;
    private readonly List<SpsAnomalyEntry> _spsLoggedAnomalies = [];
    private SpsAnomalyKind _spsLockTarget = SpsAnomalyKind.None;
    private float _spsLockProgress;

    // Day 3+ Manual Warp Field Scan & Physical Tear-Off Strip State
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

    // Animation & shift clocks
    private float _elapsedVisualTime;
    private int _animTick;
    private float _tickAccum;
    private int _lastShipAnimBucket = -1;
    private int _lastPortraitAnimBucket = -1;

    // Pre-clearance compliance timers
    private bool _pendingShieldsOrder;
    private bool _pendingWeaponsOrder;
    private float _complianceTimer;
    private bool _captainRefusedCompliance;

    // 1.5-second decision evaluation & Error-Only Instant Citation strip state
    private PlayerDecision _pendingDecision;
    private float _decisionEvalTimer;
    private bool _detainLeverPulled;
    private DecisionEvaluationResult? _activeCitationStrip;
    private float _citationSlideProgress;
    private float _citationDisplayTimer;

    // SPS Scanner & Hunger/Hypoxia Perlin/sine cursor sway
    private bool _spsEnabled;
    private Vector2 _currentSpsSwayOffset;
    private Rectangle _lastSpsLensRect;

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
    private bool _isDataSlateDocked;

    // Desk Reference Manual state (Tab 0: Prefixes, Tab 1: Amber Hull Charts, Tab 2: Star Systems & Waveforms)
    private int _manualActiveTab = 1;
    private int _manualFactionIndex = 0;
    private int _manualClassIndex = 0;

    // Cached Procedural GPU Textures (strictly unloaded when ship leaves, hail closes, or screen exits)
    private Texture2D _cachedShipLiveTex;
    private Texture2D _cachedShipSpsTex;
    private Texture2D _cachedLivePortraitTex;
    private Texture2D _cachedHoloPortraitTex;
    private Texture2D _cachedManualShipTex;
    private Texture2D _cachedPreShiftKordaniPortraitTex;

    // Strictly dark with yellow MAINTENANCE PENDING tag on Days 1-2; operational ONLY on Day 3+
    public bool IsSpectrometerOperational => _gameState.Day >= 3;

    private float ProtocolDownloadProgress =>
        _phase == EncounterPhase.DownloadingProtocols
            ? Math.Clamp(_protocolDownloadElapsed / ProtocolDownloadDurationSeconds, 0f, 1f)
            : 1.0f;

    public DeskScreen(
        ScreenManager screenManager,
        GameState gameState,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;

        InitializeDraggableDeskItems();
    }

    private void InitializeDraggableDeskItems()
    {
        _deskItems.Clear();
        _zOrder.Clear();

        // 1. Collapsible Expected Ship List Clipboard
        _deskItems[DeskItemId.ExpectedList] = new DraggableDeskItem
        {
            Id = DeskItemId.ExpectedList,
            X = 166,
            Y = 290,
            Width = 178,
            Height = 236,
            ExpandedHeight = 236,
            CollapsedHeight = 24,
            IsCollapsed = false,
            IsVisible = true
        };

        // 2. Movable 3:4 Data Slate (spawns when subspace protocol download begins)
        _deskItems[DeskItemId.DataSlate] = new DraggableDeskItem
        {
            Id = DeskItemId.DataSlate,
            X = 348,
            Y = 289,
            Width = 190,
            Height = 240,
            ExpandedHeight = 240,
            CollapsedHeight = 240,
            IsCollapsed = false,
            IsVisible = false
        };

        // 3. Movable Tabbed Desk Reference Manual (with Day 1 Subspace Relay Ping)
        _deskItems[DeskItemId.ReferenceManual] = new DraggableDeskItem
        {
            Id = DeskItemId.ReferenceManual,
            X = 542,
            Y = 289,
            Width = 246,
            Height = 240,
            ExpandedHeight = 240,
            CollapsedHeight = 240,
            IsCollapsed = false,
            IsVisible = true
        };

        // 4. Movable Torn-Off Thermal Warp Field Residue Strip (Day 3+, visible once torn free)
        _deskItems[DeskItemId.WarpStrip] = new DraggableDeskItem
        {
            Id = DeskItemId.WarpStrip,
            X = StripAnchorX,
            Y = StripAnchorY,
            Width = StripWidth,
            Height = StripFullHeight,
            ExpandedHeight = StripFullHeight,
            CollapsedHeight = StripFullHeight,
            IsCollapsed = false,
            IsVisible = false
        };

        _zOrder.Add(DeskItemId.ExpectedList);
        _zOrder.Add(DeskItemId.ReferenceManual);
        _zOrder.Add(DeskItemId.DataSlate);
        _zOrder.Add(DeskItemId.WarpStrip);
    }

    public void OnEnter()
    {
        UnloadCachedTextures();

        _spsEnabled = false;
        _phase = EncounterPhase.BayEmpty;
        _activeShip = null;
        _queueCounter = 0;
        _sessionEntropy += 37u;
        _lastShipAnimBucket = -1;
        _lastPortraitAnimBucket = -1;
        _isDataSlateDocked = false;
        _detainLeverPulled = false;
        _day2SuspectQuestioned = false;
        _activeCitationStrip = null;
        ResetProtocolReviewState();
        ResetWarpPrinterState();
        ResetRelayAndSpsAnomalyState();

        // Resets ClericalWarningsThisShift = 0 and holds timer paused at t = 0 until first Next Hail
        _shiftController.StartNewShift(_gameState);

        // Enforce strict Day 1-2 offline spectrometer vs Day 3+ repaired spectrometer
        _gameState.QuestFlags["Day3_SpectrometerRepaired"] = _gameState.Day >= 3;
        if (_gameState.Day >= 3 && _gameState.Day2SuspectDetained && !_gameState.Day3KordaniOvernightRepApplied)
        {
            _gameState.RepScore["KDF-"] = _gameState.GetRep("KDF-") + 2;
            _gameState.Day3KordaniOvernightRepApplied = true;
            _gameState.SaveToDisk();
        }

        _shipGenerator.InitializeDaySchedule(_gameState.Day, _sessionEntropy);
        InitializeDraggableDeskItems();

        _activeDirectiveLines = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
            _gameState.Day,
            _gameState.Day2SuspectDetained,
            _gameState.Day2SuspectRegistration,
            _gameState.Day2SuspectDeclaredOrigin);

        _transcriptLines.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        AddTranscriptLine($"BOOTH 04 TERMINAL READY // DAY {_gameState.Day:D2} WATCH.", Palette.CrtAmberPrimary);

        if (_gameState.Day == 3)
        {
            _preShiftStage = PreShiftStage.ShiftDirectiveModal;
            _directiveOpenedMidShift = false;
            _shiftController.IsPreShiftSequenceActive = true;
            AddTranscriptLine("MORNING SHIFT DIRECTIVE RECEIVED -- ACKNOWLEDGE TO PROCEED.", Palette.CrtAmberPrimary);
        }
        else
        {
            _preShiftStage = PreShiftStage.None;
            _directiveOpenedMidShift = false;
            _shiftController.IsPreShiftSequenceActive = false;
            AddTranscriptLine("WATCH TIMER PAUSED UNTIL FIRST HAIL. PRESS [NEXT HAIL] WHEN READY.", Palette.CrtAmberDimmed);
        }

        RebuildManualSilhouetteTexture();
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

    private void ResetRelayAndSpsAnomalyState()
    {
        _relayPhase = RelayQueryPhase.Idle;
        _relayTypingElapsed = 0f;
        _relayLastTypedChars = 0;
        _relayQueryFullText = string.Empty;
        _relayResultStatus = RelayPingStatus.ActiveClear;
        _relayTargetWasUnregistered = false;
        _relayAlertConfronted = false;

        _activeShipHardpoints = null;
        _spsLoggedAnomalies.Clear();
        _spsLockTarget = SpsAnomalyKind.None;
        _spsLockProgress = 0f;
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

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _elapsedVisualTime += deltaTime;

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

        _currentSpsSwayOffset = ComputeSpsSwayOffset(_elapsedVisualTime);
        UpdateSpsLensRect(mouse);

        // Update Day 1 Subspace Relay Ping typing animation & SPS Hold-to-Lock anomaly scanner
        UpdateRelayQueryState(deltaTime);
        UpdateSpsHoldToLock(deltaTime, mouse);

        // Update Day 3+ Manual Warp Field Scan & Downward Paper Extrusion
        UpdateWarpPrinterState(deltaTime);

        UpdateAnimatedEncounterTextures();

        // Update Error-Only Instant Citation Strip slide-out & flash timer
        if (_activeCitationStrip != null)
        {
            _citationSlideProgress = Math.Min(1.0f, _citationSlideProgress + deltaTime * 4.5f);
            _citationDisplayTimer -= deltaTime;
            if (_citationDisplayTimer <= 0f)
            {
                _activeCitationStrip = null;
            }
        }

        UpdateEncounterPhase(deltaTime);

        // Mousewheel scroll on the CRT Dialogue Transcript Box
        HandleTranscriptScrollInput(mouse);

        if (_preShiftStage != PreShiftStage.None)
        {
            HandlePreShiftStageInput(mouse);
            return;
        }

        // Handle Click-and-Drag Tear-Off pull on anchored thermal paper strip
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

        // 0. Dismiss active error citation strip if clicked
        if (_activeCitationStrip != null && mouse.IsHit(180, 152, 600, 74))
        {
            AudioHooks.Play("terminal_key_click");
            _activeCitationStrip = null;
            return;
        }

        // 1. Top Bar Navigation Buttons (10px right margin: 856 + 94 = 950)
        if (mouse.IsHit(764, 4, 86, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }
        if (mouse.IsHit(856, 4, 94, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
            return;
        }

        // Shift Complete proceed button inside viewport
        if (_shiftController.CanEndShift && mouse.IsHit(132, 154, 286, 26))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
            return;
        }

        // Thematic CRT Transcript Scroll Buttons
        if (TryHandleTranscriptScrollButtonClicks(mouse))
        {
            return;
        }

        // Day 3+ Physical [SCAN WARP] button mounted on the Spectrometer unit (x=22, y=354, w=130, h=22)
        if (IsSpectrometerOperational && mouse.IsHit(LeftColX + 6, SpecY + 62, LeftColW - 12, 22))
        {
            TryStartManualWarpScan();
            return;
        }

        // Left Console [VIEW SHIFT BRIEFING] button (x=22, y=506, w=130, h=18)
        if (mouse.IsHit(LeftColX + 6, 506, LeftColW - 12, 18))
        {
            AudioHooks.Play("terminal_key_click");
            _preShiftStage = PreShiftStage.ShiftDirectiveModal;
            _directiveOpenedMidShift = true;
            return;
        }

        // Day 2 Ghost Route Optional Route Pushback Button [QUESTION ROUTE]
        if (_activeShip != null &&
            _activeShip.GroundTruth.IsDay2GhostRouteSuspect &&
            _phase == EncounterPhase.InspectingSlateAndShip &&
            !_day2SuspectQuestioned &&
            mouse.IsHit(LeftColX + 6, 482, LeftColW - 12, 20))
        {
            _day2SuspectQuestioned = true;
            AudioHooks.Play("terminal_key_click");
            AddTranscriptLine($"> BOOTH 04: \"{_activeShip.SuspectRoutePushbackPromptLine}\"", Palette.CrtAmberPrimary);
            AddTranscriptLine($"CAPT: \"{_activeShip.SuspectRouteDeflectionLine}\"", Palette.CrtAmberPrimary);
            return;
        }

        // 2a. SPS Anomaly Log Mini-Screen Clickable Entries (works directly without toggling Review!)
        if (TryHandleSpsMiniScreenClick(mouse))
        {
            return;
        }

        // 2b. Hardware Control Strip & Right-Side Clearance Dock / Detain Switch
        if (TryHandleHardwarePanelClicks(mouse))
        {
            return;
        }

        // 3. Protocol Review Mode (Claim vs. Station Cycle / Reality / Relay Log)
        if (_protocolReviewModeActive && _activeShip != null &&
            (_phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.PreClearanceHailing))
        {
            if (TryHandleProtocolReviewClick(mouse))
            {
                return;
            }
        }

        // 4. Click on anchored thermal strip to begin pull-to-tear gesture (Day 3+)
        if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear &&
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

        // 5. Movable Bottom Desk Workspace Documents (Reverse Z-Order: topmost first)
        TryHandleDeskItemsMousePress(mouse);
    }

    private void ForceReseedAndSpawnNextShip()
    {
        _sessionEntropy += 113u;
        _shiftController.NotifyFirstHailTriggered();
        DebugSpawnValidityMode mode = (DebugSpawnValidityMode)_devDebug.ForceSpawnValidityMode;
        StartNextShipEncounter(mode);
    }

    // =========================================================================
    // Day 3+ Manual Warp Field Scan, Slow Extrusion & Tear-Off Strip Logic
    // =========================================================================
    private bool CanStartManualWarpScan() =>
        IsSpectrometerOperational &&
        _activeShip != null &&
        (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.DownloadingProtocols or EncounterPhase.InspectingSlateAndShip) &&
        !_activeShip.GroundTruth.ActualShieldsUp &&
        _warpPhase == WarpPrinterPhase.Idle;

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

    private void UpdateWarpPrinterState(float deltaTime)
    {
        if (_warpPhase == WarpPrinterPhase.ScanningWarpField)
        {
            _warpPhaseElapsed += deltaTime;
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
            _warpPhaseElapsed += deltaTime;
            _warpPrinterTickAccum += deltaTime;
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
                // Tear the paper strip cleanly free from the spectrometer printer mouth!
                _isPullingAnchoredStrip = false;
                _anchoredStripPullOffsetX = 0;
                _anchoredStripPullOffsetY = 0;
                _warpPhase = WarpPrinterPhase.TornOffFreeDocument;
                AudioHooks.Play("paper_tear");

                DraggableDeskItem strip = _deskItems[DeskItemId.WarpStrip];
                strip.IsVisible = true;
                strip.X = Math.Clamp(mouse.X - StripWidth / 2, DragMinX, DragMaxX - strip.Width);
                strip.Y = Math.Clamp(mouse.Y - StripFullHeight / 2, DragMinY, DragMaxY - strip.Height);
                BringDeskItemToFront(DeskItemId.WarpStrip);

                // Seamlessly continue dragging the freshly torn strip in the player's hand!
                _draggingItemId = DeskItemId.WarpStrip;
                _dragOffsetX = StripWidth / 2;
                _dragOffsetY = StripFullHeight / 2;
                return;
            }

            // Show subtle physical pull tension on the anchored strip before tear threshold
            _anchoredStripPullOffsetX = Math.Clamp(dx / 2, -6, 6);
            _anchoredStripPullOffsetY = Math.Clamp(dy / 2, 0, 8);
            return;
        }

        // Released before tearing free -> snap gently back to anchor
        _isPullingAnchoredStrip = false;
        _anchoredStripPullOffsetX = 0;
        _anchoredStripPullOffsetY = 0;
    }

    // =========================================================================
    // Day 1 Subspace Relay Ping & SPS Hold-to-Lock Anomaly Scanner Logic
    // =========================================================================
    private bool CanStartRelayPing() =>
        _activeShip != null &&
        _phase == EncounterPhase.InspectingSlateAndShip &&
        _relayPhase == RelayQueryPhase.Idle;

    private bool IsActiveRelayAlert() =>
        _activeShip != null &&
        _relayPhase == RelayQueryPhase.Completed &&
        (_relayResultStatus is RelayPingStatus.AlertReportedStolen or RelayPingStatus.AlertHullDecommissioned);

    private void StartSubspaceRelayPing()
    {
        if (!CanStartRelayPing() || _activeShip == null)
        {
            AudioHooks.Play("citation_buzz");
            return;
        }

        string factionTag = ShipGenerator.GetRelayFactionTag(_activeShip.Slate.DeclaredPrefix);
        string regCode = _activeShip.Slate.RegistrationCode;

        _relayPhase = RelayQueryPhase.TypingQuery;
        _relayTypingElapsed = 0f;
        _relayLastTypedChars = 0;
        _relayTargetWasUnregistered = _activeShip.Slate.DeclaredPrefix.Equals(Constants.UnregisteredPrefix, StringComparison.OrdinalIgnoreCase);
        _relayResultStatus = _activeShip.GroundTruth.RelayStatus;
        _relayAlertConfronted = false;
        _relayQueryFullText = $"QUERYING {factionTag} RELAY: {regCode}...";
        AudioHooks.Play("key_type");
    }

    private void UpdateRelayQueryState(float deltaTime)
    {
        if (_relayPhase != RelayQueryPhase.TypingQuery || _activeShip == null)
            return;

        _relayTypingElapsed += deltaTime;
        float progress = Math.Clamp(_relayTypingElapsed / RelayTypingDurationSeconds, 0f, 1f);
        int visibleChars = (int)MathF.Floor(progress * _relayQueryFullText.Length);
        if (visibleChars > _relayLastTypedChars)
        {
            _relayLastTypedChars = visibleChars;
            AudioHooks.Play("key_type");
        }

        if (_relayTypingElapsed >= RelayTypingDurationSeconds)
        {
            _relayPhase = RelayQueryPhase.Completed;
            _relayLastTypedChars = _relayQueryFullText.Length;
            AudioHooks.Play(IsActiveRelayAlert() ? "citation_buzz" : "terminal_key_confirm");
        }
    }

    private string GetRelayResultDisplayLine()
    {
        if (_relayTargetWasUnregistered)
            return "NO RELAY RECORD (UNREGISTERED)";

        return _relayResultStatus switch
        {
            RelayPingStatus.ActiveClear => "STATUS: REGISTRY ACTIVE / CLEAR",
            RelayPingStatus.NoResponseInterference => "NO RELAY RESPONSE (INTERFERENCE)",
            RelayPingStatus.AlertReportedStolen => "ALERT: VESSEL REPORTED STOLEN",
            RelayPingStatus.AlertHullDecommissioned => "ALERT: HULL DECOMMISSIONED 6 MO AGO",
            _ => "STATUS: REGISTRY ACTIVE / CLEAR"
        };
    }

    private void TriggerRelayAlertConfrontation()
    {
        if (!IsActiveRelayAlert() || _activeShip == null)
            return;

        _relayAlertConfronted = true;
        if (_activeShip.ProtocolReviewVerified)
        {
            AudioHooks.Play("terminal_key_click");
            return;
        }

        string summary = _relayResultStatus == RelayPingStatus.AlertReportedStolen
            ? "RELAY ALERT: VESSEL REPORTED STOLEN"
            : "RELAY ALERT: HULL DECOMMISSIONED 6 MO AGO";
        VerifyProtocolReviewDiscrepancy(ShipInfractionType.RelayRegistryAlert, summary);
    }

    private void GetRelayUiRects(DraggableDeskItem manual, out Rectangle pingBtnRect, out Rectangle readoutRect)
    {
        int mx = manual.X;
        int my = manual.Y;
        if (_manualActiveTab == 0)
        {
            pingBtnRect = new Rectangle(mx + 6, my + 197, 78, 16);
            readoutRect = new Rectangle(mx + 6, my + 215, manual.Width - 12, 20);
        }
        else
        {
            pingBtnRect = new Rectangle(mx + 6, my + 180, 82, 18);
            readoutRect = new Rectangle(mx + 6, my + 202, manual.Width - 12, 32);
        }
    }

    private void UpdateSpsHoldToLock(float deltaTime, VirtualMouseState mouse)
    {
        bool canScanHardpoints =
            _spsEnabled &&
            _preShiftStage == PreShiftStage.None &&
            _activeShip != null &&
            _activeShipHardpoints != null &&
            _phase == EncounterPhase.InspectingSlateAndShip &&
            !_activeShip.GroundTruth.ActualShieldsUp &&
            mouse.IsHit(ViewportX, ViewportSkyTopY, ViewportW, ViewportSkyH);

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

        // 1. Check anomalous hot forward weapon mount (when slate claims WPN: OFFLINE)
        bool hasUnloggedHotWeapons =
            _activeShip.GroundTruth.HasSpoofedWeapons &&
            _activeShip.GroundTruth.ActualWeaponsHot &&
            !_spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.HotWeaponMount);

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

        // 2. Check unexplained violet cargo power draw
        if (hoveredAnomaly == SpsAnomalyKind.None)
        {
            bool hasUnloggedCargoPower =
                _activeShip.GroundTruth.ActualCargoPowerDraw &&
                !_spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.VioletCargoPower);

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
                    _spsLoggedAnomalies.Add(new SpsAnomalyEntry
                    {
                        Kind = SpsAnomalyKind.HotWeaponMount,
                        Label = "[!] HOT WPN MOUNT",
                        AccentColor = Palette.SpsWeaponsHot
                    });
                }
                else if (hoveredAnomaly == SpsAnomalyKind.VioletCargoPower)
                {
                    _spsLoggedAnomalies.Add(new SpsAnomalyEntry
                    {
                        Kind = SpsAnomalyKind.VioletCargoPower,
                        Label = "[!] CARGO PWR DRAW",
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

    private bool TryHandleSpsMiniScreenClick(VirtualMouseState mouse)
    {
        if (_activeShip == null || _phase != EncounterPhase.InspectingSlateAndShip || _spsLoggedAnomalies.Count == 0)
            return false;

        int maxRows = Math.Min(2, _spsLoggedAnomalies.Count);
        for (int i = 0; i < maxRows; i++)
        {
            int ry = SpsMiniY + 15 + i * 17;
            if (!mouse.IsHit(SpsMiniX + 4, ry, SpsMiniW - 8, 15))
                continue;

            SpsAnomalyEntry entry = _spsLoggedAnomalies[i];
            entry.WasConfronted = true;

            if (_activeShip.ProtocolReviewVerified)
            {
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            if (entry.Kind == SpsAnomalyKind.HotWeaponMount)
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.SpoofedWeaponsHot,
                    "SPOOFED WEAPONS: SLATE 'OFFLINE' vs HOT SPS MOUNT");
            }
            else if (entry.Kind == SpsAnomalyKind.VioletCargoPower)
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.UnexplainedCargoPowerDraw,
                    "SPS VIOLET POWER DRAW IN DECLARED EMPTY HOLD");
            }

            return true;
        }

        return false;
    }

    // =========================================================================
    // Protocol Review Mode (Claim vs. Station Cycle / Reality / Relay Log)
    // =========================================================================
    private bool TryHandleProtocolReviewClick(VirtualMouseState mouse)
    {
        ReviewTarget hitTarget = DetectProtocolReviewTargetAtMouse(mouse);
        if (hitTarget == ReviewTarget.None)
            return false;

        if (_activeShip == null)
            return false;

        AudioHooks.Play("terminal_key_click");

        // Standalone valid violation 1: Clicking UNR- Registration Prefix or ORG on the Data Slate
        if ((hitTarget is ReviewTarget.DataSlatePrefix or ReviewTarget.DataSlateOrg) &&
            _activeShip.Slate.DeclaredPrefix.Equals(Constants.UnregisteredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            VerifyProtocolReviewDiscrepancy(
                ShipInfractionType.UnregisteredPrefix,
                $"UNREGISTERED TRANSPONDER ({_activeShip.Slate.RegistrationCode})");
            return true;
        }

        // Standalone valid violation 2: Clicking active Relay Alert in the Manual's Subspace Relay Log
        if (hitTarget == ReviewTarget.RelayAlertLog && IsActiveRelayAlert())
        {
            TriggerRelayAlertConfrontation();
            return true;
        }

        // Standalone valid violation 3: Clicking misspelled ORG or ORIG (spoofed packet tampering)
        if (hitTarget == ReviewTarget.DataSlateOrg &&
            _activeShip.GroundTruth.TamperedField == PacketTamperField.Organization)
        {
            VerifyProtocolReviewDiscrepancy(
                ShipInfractionType.PacketTamperingMisspelling,
                $"SPOOFED PACKET: MISSPELLED ORG '{_activeShip.Slate.DeclaredFactionName}'");
            return true;
        }

        if (hitTarget == ReviewTarget.DataSlateOrigin &&
            _activeShip.GroundTruth.TamperedField == PacketTamperField.Origin)
        {
            VerifyProtocolReviewDiscrepancy(
                ShipInfractionType.PacketTamperingMisspelling,
                $"SPOOFED PACKET: MISSPELLED ORIGIN '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'");
            return true;
        }

        // Standalone valid violation 4: Clicking Viewport Ship after captain outright refused compliance (Day 14+ / rep <= -9)
        if (hitTarget == ReviewTarget.ViewportShipHull && _captainRefusedCompliance)
        {
            VerifyProtocolReviewDiscrepancy(
                ShipInfractionType.RefusedShieldsOrWeaponsCompliance,
                "OUTRIGHT REFUSAL TO LOWER SHIELDS / POWER DOWN WEAPONS");
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

        // 0. Check Top Console Header Station Cycle (for Registration Expiry verification)
        if (mouse.IsHit(TopCycleX, TopCycleY, TopCycleW, TopCycleH))
        {
            return ReviewTarget.TopHeaderCycle;
        }

        // 1. Check SPS Swaying Lens Conduit when SPS is ON and mouse clicks inside the lens over the ship
        if (_spsEnabled && mouse.IsHit(_lastSpsLensRect))
        {
            Rectangle shipRect = new(ShipAnchorX + _shipSlideOffset, ShipAnchorY, ShipRenderW, ShipRenderH);
            bool lensOverlapsShip =
                _lastSpsLensRect.X < shipRect.X + shipRect.Width &&
                _lastSpsLensRect.X + _lastSpsLensRect.Width > shipRect.X &&
                _lastSpsLensRect.Y < shipRect.Y + shipRect.Height &&
                _lastSpsLensRect.Y + _lastSpsLensRect.Height > shipRect.Y;

            if (lensOverlapsShip)
            {
                return ReviewTarget.SpsLensConduit;
            }
        }

        // 2. Check Reality: Live Viewport Ship Hull
        if (mouse.IsHit(ViewportX + 4, ViewportSkyTopY, ViewportW - 8, ViewportSkyH))
        {
            return ReviewTarget.ViewportShipHull;
        }

        // 3. Check Reality: Anchored Thermal Warp Strip (Day 3+)
        if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear &&
            mouse.IsHit(StripAnchorX, StripAnchorY, StripWidth, StripFullHeight))
        {
            return ReviewTarget.WarpStripPaper;
        }

        // 4. Check topmost movable desk documents (Data Slate Claims, Torn-Off Warp Strip & Active Relay Alert)
        for (int i = _zOrder.Count - 1; i >= 0; i--)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible || !mouse.IsHit(item.X, item.Y, item.Width, item.Height))
                continue;

            if (id == DeskItemId.WarpStrip && _warpPhase == WarpPrinterPhase.TornOffFreeDocument)
            {
                return ReviewTarget.WarpStripPaper;
            }

            if (id == DeskItemId.DataSlate && _phase == EncounterPhase.InspectingSlateAndShip)
            {
                // Top-Right Claims: REG CODE / PREFIX & WPN: OFFLINE
                if (mouse.IsHit(item.X + 74, item.Y + 21, 112, 26))
                    return ReviewTarget.DataSlatePrefix;

                if (mouse.IsHit(item.X + 74, item.Y + 60, 112, 14))
                    return ReviewTarget.DataSlateWeapons;

                // Lower Table Claims: ORG, EXP, SPEC, ORIG, CARGO
                int rowW = item.Width - 8;
                if (mouse.IsHit(item.X + 4, item.Y + 106, rowW, 14))
                    return ReviewTarget.DataSlateOrg;

                if (mouse.IsHit(item.X + 4, item.Y + 121, rowW, 14))
                    return ReviewTarget.DataSlateExpiry;

                if (mouse.IsHit(item.X + 4, item.Y + 151, rowW, 14))
                    return ReviewTarget.DataSlateSpecies;

                if (mouse.IsHit(item.X + 4, item.Y + 166, rowW, 14))
                    return ReviewTarget.DataSlateOrigin;

                if (mouse.IsHit(item.X + 4, item.Y + 196, rowW, 28))
                    return ReviewTarget.DataSlateCargo;
            }
            else if (id == DeskItemId.ReferenceManual)
            {
                // Allow clicking an active Subspace Relay Alert on Tab 0 / Tab 1 inside Protocol Review;
                // all other Manual clicks return None so tab switching & PING RELAY still work seamlessly.
                if ((_manualActiveTab == 0 || _manualActiveTab == 1) && IsActiveRelayAlert())
                {
                    GetRelayUiRects(item, out _, out Rectangle readoutRect);
                    if (mouse.IsHit(readoutRect))
                    {
                        return ReviewTarget.RelayAlertLog;
                    }
                }
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

        // 1. Claim (Data Slate EXP) vs. Reality (Top Console Station Cycle)
        if (IsPair(ReviewTarget.DataSlateExpiry, ReviewTarget.TopHeaderCycle))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.ExpiredRegistration))
            {
                (int curY, int curM) = Constants.GetStationCycle(_gameState.Day);
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.ExpiredRegistration,
                    $"EXPIRED REG ({_activeShip.Slate.RegistrationExpiryDisplay} < CYC {Constants.FormatStationCycle(curY, curM)})");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 2. Claim (Data Slate Prefix) vs. Claim (Data Slate ORG) -- Mismatched Organization or Misspelled Org
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateOrg))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixOrganizationMismatch))
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.PrefixOrganizationMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs ORG '{_activeShip.Slate.DeclaredFactionName}'");
            }
            else if (truth.TamperedField == PacketTamperField.Organization)
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.PacketTamperingMisspelling,
                    $"SPOOFED PACKET: MISSPELLED ORG '{_activeShip.Slate.DeclaredFactionName}'");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 3. Claim (Data Slate Prefix or ORG) vs. Claim (Data Slate SPEC) -- Restricted Single-Species Prefix
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateSpecies) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.DataSlateSpecies))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixSpeciesMismatch))
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.PrefixSpeciesMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' PROHIBITS {_activeShip.Slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 4. Claim (Data Slate Prefix or ORG) vs. Reality (Live Viewport Ship Hull)
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.ViewportShipHull))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixHullSilhouetteMismatch))
            {
                string actualHullName = ShipGenerator.GetHullFactionReadableName(truth.ActualVisualFaction);
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.PrefixHullSilhouetteMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs {actualHullName} HULL");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 5. Claim (Data Slate WPN: OFFLINE) vs. Reality (SPS Live Conduit inside swaying lens)
        if (IsPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsLensConduit))
        {
            if (truth.HasSpoofedWeapons && truth.ActualWeaponsHot && !truth.ActualShieldsUp)
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.SpoofedWeaponsHot,
                    "SPOOFED WEAPONS: SLATE 'OFFLINE' vs HOT SPS CONDUIT");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 6. Claim (Data Slate CARGO) vs. Reality (SPS Violet Cargo Conduit)
        if (IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.SpsLensConduit))
        {
            if (truth.ActualCargoPowerDraw && !truth.ActualShieldsUp)
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.UnexplainedCargoPowerDraw,
                    "SPS VIOLET POWER DRAW IN DECLARED EMPTY HOLD");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 7. Claim (Data Slate Origin) vs. Reality (Physical Thermal Warp Field Strip on Day 3+)
        if (IsPair(ReviewTarget.DataSlateOrigin, ReviewTarget.WarpStripPaper))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform))
            {
                VerifyProtocolReviewDiscrepancy(
                    ShipInfractionType.FalsifiedOriginWaveform,
                    $"WARP STRIP '{truth.ActualOriginSystem.ToUpperInvariant()}' vs SLATE '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'");
            }
            else
            {
                LogNoDiscrepancyDetected();
            }
            return;
        }

        // 8. Claim (Data Slate Prefix or ORG) vs. Relay Alert Log
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.RelayAlertLog) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.RelayAlertLog))
        {
            if (IsActiveRelayAlert())
            {
                TriggerRelayAlertConfrontation();
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

    private void VerifyProtocolReviewDiscrepancy(ShipInfractionType violationType, string summary)
    {
        if (_activeShip == null)
            return;

        _activeShip.ProtocolReviewVerified = true;
        _verifiedViolationSummary = summary;
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _reviewStatusNote = "VERIFIED -- DETAIN AUTHORIZED.";
        AudioHooks.Play("terminal_key_confirm");

        string officerPrompt;
        string captainExcuse;

        switch (violationType)
        {
            case ShipInfractionType.UnregisteredPrefix:
                officerPrompt = $"Your registration '{_activeShip.Slate.RegistrationCode}' uses an unregistered UNR- prefix.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseUnregistered;
                break;

            case ShipInfractionType.ExpiredRegistration:
                officerPrompt = $"Your registration expired on {_activeShip.Slate.RegistrationExpiryDisplay}. Current cycle is {Constants.FormatStationCycle(Constants.GetStationCycle(_gameState.Day).Year, Constants.GetStationCycle(_gameState.Day).Month)}.";
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

            case ShipInfractionType.PacketTamperingMisspelling:
                officerPrompt = _activeShip.GroundTruth.TamperedField == PacketTamperField.Organization
                    ? $"Your Data Slate organization '{_activeShip.Slate.DeclaredFactionName}' is misspelled -- transponder packet tampering."
                    : $"Your Data Slate origin '{_activeShip.Slate.OriginSystem}' is misspelled -- transponder packet tampering.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcusePacketTamper;
                break;

            case ShipInfractionType.RelayRegistryAlert:
                officerPrompt = _activeShip.GroundTruth.RelayStatus == RelayPingStatus.AlertReportedStolen
                    ? $"Faction relay returned a STOLEN VESSEL alert on registration {_activeShip.Slate.RegistrationCode}."
                    : $"Faction relay reports hull {_activeShip.Slate.RegistrationCode} was DECOMMISSIONED 6 months ago.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseRelayAlert;
                break;

            case ShipInfractionType.PrefixHullSilhouetteMismatch:
                officerPrompt = $"Your Data Slate declares '{_activeShip.Slate.DeclaredPrefix}', which does not match your hull silhouette.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseHull;
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

        AddTranscriptLine($"[REVIEW VERIFIED] {summary}", Palette.ClearanceTeal);
        AddTranscriptLine($"> BOOTH 04: \"{officerPrompt}\"", Palette.ClearanceTeal);
        AddTranscriptLine($"CAPT: \"{captainExcuse}\"", Palette.CrtAmberPrimary);
    }

    private static string FormatReviewTargetLabel(ReviewTarget target) =>
        target switch
        {
            ReviewTarget.DataSlatePrefix => "SLATE REG/PREFIX",
            ReviewTarget.DataSlateWeapons => "SLATE WPN STATUS",
            ReviewTarget.DataSlateOrigin => "SLATE ORIGIN",
            ReviewTarget.DataSlateOrg => "SLATE ORG",
            ReviewTarget.DataSlateExpiry => "SLATE EXPIRY",
            ReviewTarget.DataSlateSpecies => "SLATE SPECIES",
            ReviewTarget.DataSlateCargo => "SLATE CARGO",
            ReviewTarget.TopHeaderCycle => "STATION CYCLE",
            ReviewTarget.ViewportShipHull => "VIEWPORT HULL",
            ReviewTarget.SpsLensConduit => "SPS LENS CONDUIT",
            ReviewTarget.WarpStripPaper => "WARP STRIP PAPER",
            ReviewTarget.RelayAlertLog => "RELAY ALERT LOG",
            _ => "NONE"
        };

    // =========================================================================
    // Pre-Shift Sequence (Day 3 Variant A/B Directive & Kordani Viewscreen Hail)
    // =========================================================================
    private void HandlePreShiftStageInput(VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(764, 4, 86, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.SwitchTo(ScreenId.MainMenu);
            return;
        }
        if (mouse.IsHit(856, 4, 94, 16))
        {
            AudioHooks.Play("terminal_key_confirm");
            _screenManager.RouteViaCorridor(ScreenId.Quarters);
            return;
        }

        if (_preShiftStage == PreShiftStage.ShiftDirectiveModal)
        {
            if (mouse.IsHit(340, 456, 280, 26))
            {
                AudioHooks.Play("terminal_key_confirm");

                if (!_directiveOpenedMidShift &&
                    _gameState.Day == 3 &&
                    _gameState.Day2SuspectDetained &&
                    !_gameState.Day3PreShiftHailCompleted)
                {
                    StartDay3PreShiftKordaniHail();
                }
                else
                {
                    _preShiftStage = PreShiftStage.None;
                    _shiftController.IsPreShiftSequenceActive = false;
                    if (!_directiveOpenedMidShift)
                    {
                        AddTranscriptLine($"DAY {_gameState.Day:D2} DIRECTIVE ACKNOWLEDGED. PRESS [NEXT HAIL] TO BEGIN WATCH.", Palette.CrtAmberPrimary);
                    }
                }
            }
            return;
        }

        if (_preShiftStage == PreShiftStage.Day3KordaniPreShiftHail)
        {
            if (mouse.IsHit(340, 444, 280, 26))
            {
                CloseDay3PreShiftKordaniHail();
            }
        }
    }

    private void StartDay3PreShiftKordaniHail()
    {
        _preShiftStage = PreShiftStage.Day3KordaniPreShiftHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer officerBuf = PortraitRenderer.RenderBuffer(new PortraitRenderOptions
        {
            SeedId = Day2And3ScriptedDialogue.Day3KordaniOfficerPortraitSeed,
            Species = PortraitSpecies.Kordani,
            Mode = PortraitRenderMode.Live,
            FactionPrefix = "KDF-"
        });
        _cachedPreShiftKordaniPortraitTex = officerBuf.ToTexture2D();

        AudioHooks.Play("hail_incoming_chime");

        _transcriptLines.Clear();
        AddTranscriptLine($"[INTERNAL COMMS] {Day2And3ScriptedDialogue.Day3KordaniOfficerName}", Palette.CrtAmberPrimary);
        foreach (string line in Day2And3ScriptedDialogue.Day3PreShiftKordaniHailLines)
        {
            AddTranscriptLine(line, Palette.CrtAmberPrimary);
        }
    }

    private void CloseDay3PreShiftKordaniHail()
    {
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);

        _gameState.Day3PreShiftHailCompleted = true;
        _gameState.SaveToDisk();

        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;

        AudioHooks.Play("terminal_key_confirm");
        AddTranscriptLine("INTERNAL HAIL CLOSED. PRESS [NEXT HAIL] TO BEGIN DAY 03 WATCH.", Palette.CrtAmberDimmed);
    }

    // =========================================================================
    // Encounter State Machine (3.5s Approach, Delayed Viewscreen, 1.85s Download, 1.35s Clearance Upload)
    // =========================================================================
    private void UpdateEncounterPhase(float deltaTime)
    {
        if (_activeShip == null)
            return;

        switch (_phase)
        {
            case EncounterPhase.ShipGlidingIn:
            {
                // Heavy, deliberate 3.5-second docking approach with smooth cubic ease-out deceleration.
                // Hailing Viewscreen remains dark/standby until docking completes.
                _shipApproachElapsed = Math.Min(ShipApproachDurationSeconds, _shipApproachElapsed + deltaTime);
                float t = _shipApproachElapsed / ShipApproachDurationSeconds;
                float eased = 1.0f - MathF.Pow(1.0f - t, 3.0f);
                _shipSlideOffset = (1.0f - eased) * 390.0f;

                if (_shipApproachElapsed >= ShipApproachDurationSeconds)
                {
                    _shipSlideOffset = 0f;
                    _phase = EncounterPhase.PreClearanceHailing;
                    _transcriptLines.Clear();
                    _transcriptScrollTopLine = 0;
                    _transcriptAutoFollowBottom = true;

                    // Open live/static portrait feed and play captain's opening hail ONLY now that ship finished docking
                    RebuildLivePortraitTexture();
                    AudioHooks.Play("hail_incoming_chime");
                    AddTranscriptLine($"HAIL [{_activeShip.Slate.RegistrationCode}]: {_activeShip.GreetingHailLine}", Palette.CrtAmberPrimary);

                    if (_activeShip.GroundTruth.IsPreClearanceSatisfied)
                    {
                        _subspaceAutoTransferDelay = 0.35f;
                    }
                }
                break;
            }

            case EncounterPhase.PreClearanceHailing:
            {
                if (_pendingShieldsOrder || _pendingWeaponsOrder)
                {
                    _complianceTimer -= deltaTime;
                    if (_complianceTimer <= 0f)
                    {
                        ResolvePendingComplianceOrders();
                    }
                }
                else if (_activeShip.GroundTruth.IsPreClearanceSatisfied && !_captainRefusedCompliance)
                {
                    _subspaceAutoTransferDelay -= deltaTime;
                    if (_subspaceAutoTransferDelay <= 0f)
                    {
                        BeginSubspaceProtocolsDownload();
                    }
                }
                break;
            }

            case EncounterPhase.DownloadingProtocols:
            {
                // 1.85-second animated subspace telemetry packet download onto the Data Slate
                _protocolDownloadElapsed += deltaTime;
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

            case EncounterPhase.InspectingSlateAndShip:
            {
                if (_pendingShieldsOrder || _pendingWeaponsOrder)
                {
                    _complianceTimer -= deltaTime;
                    if (_complianceTimer <= 0f)
                    {
                        ResolvePendingComplianceOrders();
                    }
                }
                break;
            }

            case EncounterPhase.UploadingClearance:
            {
                // Keep ship strictly stationary in the viewport until Clearance Dock upload hits 100%
                _shipSlideOffset = 0f;
                _clearanceUploadElapsed = Math.Min(ClearanceUploadDurationSeconds, _clearanceUploadElapsed + deltaTime);
                if (_clearanceUploadElapsed >= ClearanceUploadDurationSeconds)
                {
                    AudioHooks.Play("clearance_granted");
                    AddTranscriptLine("[DOCK LINK] CLEARANCE UPLOADED (100%) -- DEPARTURE AUTHORIZED.", Palette.ClearanceTeal);
                    _decisionEvalTimer = ShipDepartureDurationSeconds;
                    _phase = EncounterPhase.ResolvingDecision;
                }
                break;
            }

            case EncounterPhase.ResolvingDecision:
            {
                if (_pendingDecision == PlayerDecision.GrantAccess)
                {
                    // Smooth ease-in fly-away departure animation AFTER upload reached 100%
                    float depT = Math.Clamp(1.0f - (_decisionEvalTimer / ShipDepartureDurationSeconds), 0f, 1f);
                    _shipSlideOffset = -(depT * depT) * 430.0f;
                }

                _decisionEvalTimer -= deltaTime;
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
            if (!_activeShip.GroundTruth.ActualShieldsUp)
            {
                AddTranscriptLine("CAPT: \"Our shields are already down, Booth 04.\"", Palette.CrtAmberDimmed);
            }
            else if (_activeShip.GroundTruth.WillRefuseShieldsCompliance)
            {
                _captainRefusedCompliance = true;
                _activeShip.ProtocolReviewVerified = true;
                _verifiedViolationSummary = "REFUSED ORDER TO LOWER SHIELDS";
                AudioHooks.Play("citation_buzz");
                AddTranscriptLine($"CAPT: \"{_activeShip.RefusalLine}\"", Palette.CrtAmberPrimary);
                AddTranscriptLine("[SYS] PRE-CLEARANCE REFUSAL VERIFIED.", Palette.AlertCrimson);
            }
            else
            {
                _activeShip.GroundTruth.ActualShieldsUp = false;
                rebuildShip = true;
                AudioHooks.Play("terminal_key_confirm");
                AddTranscriptLine($"CAPT: \"{_activeShip.ShieldsLoweredLine}\"", Palette.CrtAmberPrimary);
            }
        }

        if (_pendingWeaponsOrder)
        {
            _pendingWeaponsOrder = false;
            if (!_activeShip.GroundTruth.ActualWeaponsHot)
            {
                AddTranscriptLine("CAPT: \"Our weapon mounts are already powered down.\"", Palette.CrtAmberDimmed);
            }
            else if (_activeShip.GroundTruth.HasSpoofedWeapons)
            {
                AddTranscriptLine($"CAPT: \"{_activeShip.SpoofedWeaponsDeflectionLine}\"", Palette.CrtAmberPrimary);
            }
            else if (_activeShip.GroundTruth.WillRefuseWeaponsCompliance)
            {
                _captainRefusedCompliance = true;
                _activeShip.ProtocolReviewVerified = true;
                _verifiedViolationSummary = "REFUSED ORDER TO POWER DOWN WEAPONS";
                AudioHooks.Play("citation_buzz");
                AddTranscriptLine($"CAPT: \"{_activeShip.RefusalLine}\"", Palette.CrtAmberPrimary);
                AddTranscriptLine("[SYS] PRE-CLEARANCE REFUSAL VERIFIED.", Palette.AlertCrimson);
            }
            else
            {
                _activeShip.GroundTruth.ActualWeaponsHot = false;
                rebuildShip = true;
                AudioHooks.Play("terminal_key_confirm");
                AddTranscriptLine($"CAPT: \"{_activeShip.WeaponsPoweredDownLine}\"", Palette.CrtAmberPrimary);
            }
        }

        if (rebuildShip)
        {
            RebuildActiveShipTextures();
        }

        if (_phase == EncounterPhase.PreClearanceHailing &&
            _activeShip.GroundTruth.IsPreClearanceSatisfied &&
            !_captainRefusedCompliance)
        {
            _subspaceAutoTransferDelay = 0.35f;
        }
    }

    private void BeginSubspaceProtocolsDownload()
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.DownloadingProtocols;
        _protocolDownloadElapsed = 0f;
        _dataStreamAudioAccum = 0f;

        AudioHooks.Play("data_stream");
        AddTranscriptLine("> BOOTH 04: \"Send subspace protocols.\"", Palette.ClearanceTeal);

        // Spawn the Data Slate onto the desk immediately in progressive streaming state
        DraggableDeskItem slateItem = _deskItems[DeskItemId.DataSlate];
        slateItem.X = 348;
        slateItem.Y = 289;
        slateItem.IsVisible = true;
        _isDataSlateDocked = false;
        BringDeskItemToFront(DeskItemId.DataSlate);

        RebuildHoloPortraitTexture();
    }

    private void CompleteSubspaceProtocolsDownload()
    {
        if (_activeShip == null)
            return;

        _phase = EncounterPhase.InspectingSlateAndShip;
        AudioHooks.Play("subspace_transfer");

        AddTranscriptLine($"CAPT: \"{_activeShip.SubspaceUploadLine}\"", Palette.CrtAmberPrimary);

        // Day 1 Rules Purity: Do NOT fire Day 2-3 Auto-Interrogation on Day 1
        if (_gameState.Day >= 2)
        {
            AddTranscriptLine("> BOOTH 04: \"Confirm route, purpose of visit, and cargo.\"", Palette.ClearanceTeal);
            AddTranscriptLine($"CAPT: \"{_activeShip.AutoInterrogationReplyLine}\"", Palette.CrtAmberPrimary);
        }
    }

    private bool TryHandleHardwarePanelClicks(VirtualMouseState mouse)
    {
        if (_preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive)
            return false;

        // 1. NEXT HAIL button (x=300, y=256, w=104, h=26)
        if (mouse.IsHit(300, 256, 104, 26))
        {
            if (_shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty)
            {
                _shiftController.NotifyFirstHailTriggered();
                DebugSpawnValidityMode mode = (DebugSpawnValidityMode)_devDebug.ForceSpawnValidityMode;
                StartNextShipEncounter(mode);
            }
            else
            {
                AudioHooks.Play("citation_buzz");
            }
            return true;
        }

        // 2. LOWER SHIELDS compliance button (x=410, y=256, w=122, h=26)
        if (mouse.IsHit(410, 256, 122, 26))
        {
            if (_activeShip != null &&
                (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.InspectingSlateAndShip) &&
                !_pendingShieldsOrder)
            {
                AudioHooks.Play("terminal_key_click");
                AddTranscriptLine("> BOOTH 04: \"Lower shields to standby.\"", Palette.ClearanceTeal);
                _pendingShieldsOrder = true;
                _complianceTimer = _activeShip.ComplianceDelaySeconds;
            }
            return true;
        }

        // 3. POWER DOWN WPN compliance button (x=538, y=256, w=130, h=26)
        if (mouse.IsHit(538, 256, 130, 26))
        {
            if (_activeShip != null &&
                (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.InspectingSlateAndShip) &&
                !_pendingWeaponsOrder)
            {
                AudioHooks.Play("terminal_key_click");
                AddTranscriptLine("> BOOTH 04: \"Power down forward weapon mounts.\"", Palette.ClearanceTeal);
                _pendingWeaponsOrder = true;
                _complianceTimer = _activeShip.ComplianceDelaySeconds;
            }
            return true;
        }

        // 4. PROTOCOL REVIEW Toggle button (x=674, y=256, w=120, h=26)
        if (mouse.IsHit(674, 256, 120, 26))
        {
            if (_activeShip != null &&
                (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.InspectingSlateAndShip))
            {
                _protocolReviewModeActive = !_protocolReviewModeActive;
                _reviewFirstSelection = ReviewTarget.None;
                _reviewStatusNote = _protocolReviewModeActive
                    ? "CLICK CLAIM vs CYCLE/HULL/RELAY."
                    : string.Empty;
                AudioHooks.Play("terminal_key_tab");
            }
            return true;
        }

        // 5. SPS TOGGLE button (x=800, y=256, w=144, h=26 -- directly above SPS Anomaly Mini-Screen!)
        if (mouse.IsHit(800, 256, 144, 26))
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

        // 6. GRANT ACCESS button inside Clearance Dock (x=808, y=DockPanelY+75, w=128, h=22)
        if (mouse.IsHit(DockPanelX + 8, DockPanelY + 75, DockPanelW - 16, 22))
        {
            if (_activeShip != null &&
                _phase == EncounterPhase.InspectingSlateAndShip &&
                _isDataSlateDocked)
            {
                AudioHooks.Play("clearance_upload");
                _protocolReviewModeActive = false;
                AddTranscriptLine("> BOOTH 04: [UPLOADING CLEARANCE...]", Palette.ClearanceTeal);
                _pendingDecision = PlayerDecision.GrantAccess;
                _clearanceUploadElapsed = 0f;
                _phase = EncounterPhase.UploadingClearance;
            }
            else
            {
                AudioHooks.Play("citation_buzz");
            }
            return true;
        }

        // 7. Physical Red DETAIN Switch (NEVER LOCKED once ship has hailed -- player has full discretion!)
        if (mouse.IsHit(DetainSwitchX, DetainSwitchY, DetainSwitchW, DetainSwitchH))
        {
            if (_activeShip != null &&
                (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.DownloadingProtocols or EncounterPhase.InspectingSlateAndShip))
            {
                AudioHooks.Play("detain_clamp");
                _protocolReviewModeActive = false;
                _detainLeverPulled = true;
                string tag = _activeShip.ProtocolReviewVerified
                    ? $"VERIFIED: {_verifiedViolationSummary}"
                    : "UNVERIFIED DISCRETIONARY CLAMP";
                AddTranscriptLine($"> BOOTH 04: [CLAMPS ENGAGED -- {tag}]", Palette.AlertCrimson);
                _pendingDecision = PlayerDecision.Detain;
                _decisionEvalTimer = 1.50f;
                _phase = EncounterPhase.ResolvingDecision;
            }
            return true;
        }

        return false;
    }

    private void StartNextShipEncounter(DebugSpawnValidityMode forceValidity = DebugSpawnValidityMode.Auto)
    {
        UnloadEncounterTextures();
        ResetProtocolReviewState();
        ResetWarpPrinterState();
        ResetRelayAndSpsAnomalyState();

        _activeShip = _shipGenerator.GenerateNextShip(_gameState, _queueCounter++, _sessionEntropy, forceValidity);
        _shiftController.HasActiveShipInBay = true;
        _phase = EncounterPhase.ShipGlidingIn;
        _shipApproachElapsed = 0f;
        _shipSlideOffset = 390f;
        _subspaceAutoTransferDelay = 0f;
        _clearanceUploadElapsed = 0f;
        _pendingShieldsOrder = false;
        _pendingWeaponsOrder = false;
        _captainRefusedCompliance = false;
        _day2SuspectQuestioned = false;
        _isDataSlateDocked = false;
        _detainLeverPulled = false;
        _deskItems[DeskItemId.DataSlate].IsVisible = false;

        // Keep Hailing Viewscreen dark/standby during docking approach;
        // portrait feed and hail chime trigger only once docking finishes.
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

        if (_activeShip.GroundTruth.IsExpectedShip &&
            _activeShip.GroundTruth.ExpectedShipSlotIndex >= 0 &&
            _activeShip.GroundTruth.ExpectedShipSlotIndex < _shipGenerator.ExpectedShipsToday.Count)
        {
            ExpectedShipEntry entry = _shipGenerator.ExpectedShipsToday[_activeShip.GroundTruth.ExpectedShipSlotIndex];
            entry.WasResolved = true;
            entry.ResolutionTag = _pendingDecision == PlayerDecision.GrantAccess ? "CLEAR" : "DETAIN";
        }

        // Error-Only Citations: ZERO popup/toast on accurate decisions
        if (result.IsCitation)
        {
            _activeCitationStrip = result;
            _citationSlideProgress = 0.0f;
            _citationDisplayTimer = 8.0f;
            AddTranscriptLine($"[CITATION] {result.ViolationReason}", Palette.AlertCrimson);
            AddTranscriptLine($"[PENALTY] {result.PenaltyText}", Palette.CrtAmberPrimary);
        }
        else
        {
            string quietLog = _pendingDecision == PlayerDecision.GrantAccess
                ? $"[DOCKED] Vessel {result.RegistrationCode} cleared (+15 SC)."
                : $"[DETAINED] Vessel {result.RegistrationCode} detained (+15 SC).";
            AddTranscriptLine(quietLog, Palette.CrtAmberDimmed);
        }

        UnloadEncounterTextures();
        ResetProtocolReviewState();
        ResetWarpPrinterState();
        ResetRelayAndSpsAnomalyState();

        _deskItems[DeskItemId.DataSlate].IsVisible = false;
        _isDataSlateDocked = false;
        _detainLeverPulled = false;
        _activeShip = null;
        _shiftController.HasActiveShipInBay = false;
        _phase = EncounterPhase.BayEmpty;

        if (_shiftController.IsShiftTimerExpired)
        {
            AddTranscriptLine("BROWN DWARF BELOW HORIZON -- WATCH COMPLETE. PROCEED TO QUARTERS.", Palette.CrtAmberPrimary);
        }
    }

    // =========================================================================
    // Thematic CRT Dialogue Transcript Scrolling
    // =========================================================================
    private void HandleTranscriptScrollInput(VirtualMouseState mouse)
    {
        if (MathF.Abs(mouse.WheelMove) < 0.01f)
            return;

        if (!mouse.IsHit(TranscriptX, TranscriptY, TranscriptW, TranscriptH))
            return;

        List<(string Line, Color Col)> wrapped = WrapTranscriptLines(maxCharsPerLine: 34);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);
        if (maxTop == 0)
            return;

        int delta = mouse.WheelMove > 0 ? -2 : 2;
        _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine + delta, 0, maxTop);
        _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
    }

    private bool TryHandleTranscriptScrollButtonClicks(VirtualMouseState mouse)
    {
        int barX = TranscriptX + TranscriptW - 16;
        int upBtnY = TranscriptY + 16;
        int downBtnY = TranscriptY + TranscriptH - 16;

        List<(string Line, Color Col)> wrapped = WrapTranscriptLines(maxCharsPerLine: 34);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);

        if (mouse.IsHit(barX, upBtnY, 14, 14))
        {
            _transcriptScrollTopLine = Math.Max(0, _transcriptScrollTopLine - 2);
            _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
            AudioHooks.Play("terminal_key_click");
            return true;
        }
        if (mouse.IsHit(barX, downBtnY, 14, 14))
        {
            _transcriptScrollTopLine = Math.Min(maxTop, _transcriptScrollTopLine + 2);
            _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
            AudioHooks.Play("terminal_key_click");
            return true;
        }
        return false;
    }

    // =========================================================================
    // Z-Ordered Draggable Desk Documents, Collapsible Expected List & Manual
    // =========================================================================
    private bool TryHandleDeskItemsMousePress(VirtualMouseState mouse)
    {
        for (int i = _zOrder.Count - 1; i >= 0; i--)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible)
                continue;

            if (!mouse.IsHit(item.X, item.Y, item.Width, item.Height))
                continue;

            BringDeskItemToFront(id);

            // Collapsible Expected Ship List toggle button [-] / [+] in top-right header
            if (id == DeskItemId.ExpectedList && mouse.IsHit(item.X + item.Width - 22, item.Y + 3, 18, 16))
            {
                item.IsCollapsed = !item.IsCollapsed;
                item.Height = item.IsCollapsed ? item.CollapsedHeight : item.ExpandedHeight;
                item.Y = Math.Clamp(item.Y, DragMinY, DragMaxY - item.Height);
                AudioHooks.Play("terminal_key_click");
                return true;
            }

            if (id == DeskItemId.ReferenceManual && TryHandleReferenceManualInternalClick(mouse, item))
            {
                return true;
            }

            // Lock Data Slate inside Clearance Dock while actively uploading clearance or departing
            if (id == DeskItemId.DataSlate && _isDataSlateDocked &&
                (_phase is EncounterPhase.UploadingClearance or EncounterPhase.ResolvingDecision))
            {
                AudioHooks.Play("citation_buzz");
                return true;
            }

            _draggingItemId = id;
            _dragOffsetX = mouse.X - item.X;
            _dragOffsetY = mouse.Y - item.Y;
            AudioHooks.Play("terminal_key_click");

            if (id == DeskItemId.DataSlate && _isDataSlateDocked)
            {
                _isDataSlateDocked = false;
            }
            return true;
        }

        return false;
    }

    private bool TryHandleReferenceManualInternalClick(VirtualMouseState mouse, DraggableDeskItem manual)
    {
        int mx = manual.X;
        int my = manual.Y;

        if (mouse.IsHit(mx + 6, my + 20, 76, 16))
        {
            _manualActiveTab = 0;
            AudioHooks.Play("terminal_key_tab");
            return true;
        }
        if (mouse.IsHit(mx + 85, my + 20, 76, 16))
        {
            _manualActiveTab = 1;
            AudioHooks.Play("terminal_key_tab");
            RebuildManualSilhouetteTexture();
            return true;
        }
        if (mouse.IsHit(mx + 164, my + 20, 76, 16))
        {
            _manualActiveTab = 2;
            AudioHooks.Play("terminal_key_tab");
            return true;
        }

        // Day 1 Subspace Relay Ping button & clickable Relay Alert box (available on Tab 0 Prefixes & Tab 1 Faction Page)
        if (_manualActiveTab == 0 || _manualActiveTab == 1)
        {
            GetRelayUiRects(manual, out Rectangle pingBtnRect, out Rectangle readoutRect);

            if (mouse.IsHit(pingBtnRect))
            {
                StartSubspaceRelayPing();
                return true;
            }

            if (IsActiveRelayAlert() && mouse.IsHit(readoutRect))
            {
                TriggerRelayAlertConfrontation();
                return true;
            }
        }

        if (_manualActiveTab == 0)
        {
            // Prefix-to-Hull jump across all 12 canonical prefixes
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
            if (mouse.IsHit(mx + 6, my + 40, 72, 16))
            {
                _manualFactionIndex = (_manualFactionIndex - 1 + factionCount) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (_devDebug.DevManualSyncEnabled && mouse.IsHit(mx + 84, my + 40, 78, 16))
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
            if (mouse.IsHit(mx + 168, my + 40, 72, 16))
            {
                _manualFactionIndex = (_manualFactionIndex + 1) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }

            ShipFaction currentFaction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowed = ShipRenderer.GetAllowedClasses(currentFaction);
            if (mouse.IsHit(mx + 6, my + 144, 58, 16))
            {
                _manualClassIndex = (_manualClassIndex - 1 + allowed.Count) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (mouse.IsHit(mx + 182, my + 144, 58, 16))
            {
                _manualClassIndex = (_manualClassIndex + 1) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
        }

        return false;
    }

    private void UpdateActiveDrag(VirtualMouseState mouse)
    {
        if (!_draggingItemId.HasValue)
            return;

        DraggableDeskItem item = _deskItems[_draggingItemId.Value];

        if (mouse.LeftDown)
        {
            item.X = Math.Clamp(mouse.X - _dragOffsetX, DragMinX, DragMaxX - item.Width);
            item.Y = Math.Clamp(mouse.Y - _dragOffsetY, DragMinY, DragMaxY - item.Height);
            return;
        }

        if (mouse.LeftReleased || !mouse.LeftDown)
        {
            if (item.Id == DeskItemId.DataSlate &&
                _preShiftStage == PreShiftStage.None &&
                _phase == EncounterPhase.InspectingSlateAndShip)
            {
                bool overlapsDock = item.X + item.Width >= DockPanelX - 8 &&
                                    item.Y + item.Height >= DockPanelY &&
                                    item.Y <= DockPanelY + DockPanelH;
                if (overlapsDock)
                {
                    // Snap flush against the Clearance Dock divider so it never covers the SPS Anomaly Log
                    item.X = DockPanelX - item.Width - 4;
                    item.Y = 289;
                    _isDataSlateDocked = true;
                    AudioHooks.Play("dock_lock");
                }
                else
                {
                    _isDataSlateDocked = false;
                }
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
    // Tuned SPS Hunger / Hypoxia Smooth Perlin/Sine Sway & Lens Positioning
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
            ampX = 38.0f;
            ampY = 28.0f;
            speed = 2.40f;
        }
        else if (_gameState.CurrentHungerTier == HungerTier.HighTierRations)
        {
            return Vector2.Zero;
        }
        else
        {
            ampX = 16.5f;
            ampY = 12.5f;
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
        bool mouseInViewport = mouse.IsHit(ViewportX, ViewportSkyTopY, ViewportW, ViewportSkyH);
        float targetX = mouseInViewport ? mouse.X : (ShipAnchorX + ShipRenderW * 0.5f);
        float targetY = mouseInViewport ? mouse.Y : (ShipAnchorY + ShipRenderH * 0.5f);

        int halfW = SpsLensW / 2;
        int halfH = SpsLensH / 2;
        int lensCx = Math.Clamp((int)MathF.Round(targetX + _currentSpsSwayOffset.X), ViewportX + halfW + 4, ViewportX + ViewportW - halfW - 4);
        int lensCy = Math.Clamp((int)MathF.Round(targetY + _currentSpsSwayOffset.Y), ViewportSkyTopY + halfH + 2, ViewportY + ViewportH - halfH - 4);

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
    // Procedural Texture Cache Management
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

        // Only animate static viewscreen portrait once docking finished and portrait feed opened
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
    // Virtual 960x540 Rendering Pipeline
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        Raylib.ClearBackground(Palette.WeatheredGunmetalMid);

        DrawTopBulkheadHeader(mouse);
        DrawExternalViewport(mouse);
        DrawHailingCrtViewscreen(mouse);
        DrawMiddleHardwareControlStrip(mouse);
        DrawBottomDeskWorkspace(mouse);
        DrawErrorOnlyCitationStrip();

        if (_preShiftStage == PreShiftStage.ShiftDirectiveModal)
        {
            DrawMorningShiftDirectiveModal(mouse);
        }
        else if (_preShiftStage == PreShiftStage.Day3KordaniPreShiftHail)
        {
            DrawDay3PreShiftKordaniHailOverlay(mouse);
        }
    }

    private void DrawTopBulkheadHeader(VirtualMouseState mouse)
    {
        // Zero numeric clock UI -- shift time is tracked exclusively via the Brown Dwarf arc
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, 24, Palette.OutlineVoid);
        Raylib.DrawLine(0, 23, Constants.VirtualWidth, 23, Palette.WeatheredGunmetalEdge);

        string headerText =
            $"VG-3 // BOOTH 04  |  DAY {_gameState.Day:D2}  |  SCRIP: {_gameState.PactScrip} SC  |  " +
            $"WARN: {_gameState.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift}  |  " +
            $"STRIKES: {_gameState.SecurityStrikes}/{_gameState.MaxSecurityStrikesThreshold}  |  SHIPS: {_gameState.ShipsProcessedToday}";

        Raylib.DrawText(headerText, 10, 7, 10, Palette.CrtAmberPrimary);

        // Current Station Date / Cycle panel (clickable in Protocol Review against Data Slate EXP)
        (int curYear, int curMonth) = Constants.GetStationCycle(_gameState.Day);
        string cycleLabel = $"CYCLE {Constants.FormatStationCycle(curYear, curMonth)}";
        bool cycleSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.TopHeaderCycle;
        bool cycleHovered = _protocolReviewModeActive && mouse.IsHit(TopCycleX, TopCycleY, TopCycleW, TopCycleH);
        Raylib.DrawRectangle(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleSelected ? Palette.ClearanceTeal : (cycleHovered ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn));
        if (_protocolReviewModeActive)
        {
            DrawReviewHotspotOutline(mouse, TopCycleX, TopCycleY, TopCycleW, TopCycleH, cycleSelected);
        }
        Raylib.DrawText(cycleLabel, TopCycleX + 10, TopCycleY + 4, 10,
            cycleSelected ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

        DrawSmallButton(mouse, 764, 4, 86, 16, "MAIN MENU");
        DrawSmallButton(mouse, 856, 4, 94, 16, "TO QUARTERS");
    }

    private void DrawExternalViewport(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_viewport_frame.png",
            ViewportX, ViewportY, ViewportW, ViewportH,
            Palette.DeepSpaceVoid,
            Palette.WeatheredGunmetalEdge,
            string.Empty);

        // Clean Dedicated Top Bezel Strip
        Raylib.DrawRectangle(ViewportX + 1, ViewportY + 1, ViewportW - 2, 16, Palette.OutlineVoid);
        Raylib.DrawLine(ViewportX + 1, ViewportY + 17, ViewportX + ViewportW - 1, ViewportY + 17, Palette.WeatheredGunmetalEdge);

        string leftHeader = _shiftController.IsShiftTimerExpired
            ? "VG-3 APPROACH VIEWPORT // WATCH ENDED (HORIZON SUNSET)"
            : (!_shiftController.HasShiftTimerStarted
                ? "VG-3 APPROACH VIEWPORT // STANDBY (AWAITING FIRST HAIL)"
                : "VG-3 EXTERNAL APPROACH VIEWPORT");
        Raylib.DrawText(leftHeader, ViewportX + 8, ViewportY + 4, 10,
            _shiftController.IsShiftTimerExpired ? Palette.AlertCrimson : Palette.CrtAmberDimmed);

        if (_spsEnabled)
        {
            Raylib.DrawText("SPS LENS ACTIVE", ViewportX + ViewportW - 108, ViewportY + 4, 10, Palette.SpsCommsActive);
        }

        Raylib.BeginScissorMode(ViewportX + 2, ViewportSkyTopY, ViewportW - 4, ViewportSkyH);

        // 1. Deep-Space Starfield & Full Left-to-Right 270-Second Brown Dwarf Sky Arc
        int horizonY = ViewportY + ViewportH - 22;
        Raylib.DrawRectangle(ViewportX + 2, horizonY - 26, ViewportW - 4, 26, Palette.BrownDwarfGlowDeep);

        for (int s = 0; s < 48; s++)
        {
            int sx = ViewportX + 12 + ((s * 97 + 23) % (ViewportW - 24));
            int sy = ViewportSkyTopY + 6 + ((s * 53 + 17) % (horizonY - ViewportSkyTopY - 14));
            Color starCol = (s % 4 == 0) ? Palette.BrownDwarfGlowRim : Palette.WeatheredGunmetalEdge;
            Raylib.DrawPixel(sx, sy, starCol);
        }

        float p = _shiftController.BrownDwarfArcProgress;
        int dwarfStartX = ViewportX + 46;
        int dwarfEndX = ViewportX + ViewportW - 46;
        int dwarfX = dwarfStartX + (int)MathF.Round(p * (dwarfEndX - dwarfStartX));
        float baseHorizonY = (horizonY - 10) + p * 24.0f;
        int dwarfY = (int)MathF.Round(baseHorizonY - MathF.Sin(p * MathF.PI) * 144.0f);

        Raylib.DrawCircle(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircle(dwarfX - 2, dwarfY - 2, 18, Palette.BrownDwarfGlowRim);
        Raylib.DrawRectangle(dwarfX - 20, dwarfY - 5, 40, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawRectangle(dwarfX - 18, dwarfY + 3, 36, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircleLines(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowRim);

        Raylib.DrawRectangle(ViewportX + 2, horizonY, ViewportW - 4, ViewportY + ViewportH - horizonY - 2, Palette.OutlineVoid);
        Raylib.DrawLine(ViewportX + 2, horizonY, ViewportX + ViewportW - 4, horizonY, Palette.BrownDwarfGlowRim);
        for (int gx = ViewportX + 18; gx < ViewportX + ViewportW - 18; gx += 46)
        {
            Raylib.DrawRectangle(gx, horizonY - 6, 12, 7, Palette.WeatheredGunmetalMid);
            Raylib.DrawPixel(gx + 5, horizonY - 4, Palette.CrtAmberDimmed);
        }

        // 2. Transparent Live Ship Sprite
        if (_preShiftStage == PreShiftStage.Day3KordaniPreShiftHail)
        {
            Raylib.DrawRectangle(136, 118, 280, 42, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(136, 118, 280, 42, Palette.CrtAmberDimmed);
            Raylib.DrawText("INTERNAL STATION HAIL ACTIVE", 184, 127, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText("APPROACH BAY LOCKED (PRE-SHIFT)", 174, 142, 10, Palette.CrtAmberDimmed);
        }
        else if (_activeShip != null && _cachedShipLiveTex.Id != 0)
        {
            int shipDrawX = ShipAnchorX + (int)MathF.Round(_shipSlideOffset);
            int shipDrawY = ShipAnchorY;

            Rectangle src = new(0, 0, Constants.ShipWidth, Constants.ShipHeight);
            Rectangle dst = new(shipDrawX, shipDrawY, ShipRenderW, ShipRenderH);

            Raylib.DrawTexturePro(_cachedShipLiveTex, src, dst, Vector2.Zero, 0f, Color.White);

            // Protocol Review Mode hover highlight on live viewport ship hull (Reality)
            if (_protocolReviewModeActive &&
                (_phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.PreClearanceHailing) &&
                mouse.IsHit(ViewportX + 4, ViewportSkyTopY, ViewportW - 8, ViewportSkyH) &&
                (!_spsEnabled || !mouse.IsHit(_lastSpsLensRect)))
            {
                Raylib.DrawRectangleLines(shipDrawX - 2, shipDrawY - 2, ShipRenderW + 4, ShipRenderH + 4, Palette.CrtAmberPrimary);
            }

            // Station Magnetic Detain Clamps visual effect during 1.5s detainment resolution
            if (_phase == EncounterPhase.ResolvingDecision && _pendingDecision == PlayerDecision.Detain)
            {
                int clampLeft = shipDrawX + 54;
                int clampRight = shipDrawX + 178;
                Raylib.DrawRectangle(clampLeft, ViewportSkyTopY, 10, 32, Palette.AlertCrimson);
                Raylib.DrawRectangle(clampRight, ViewportSkyTopY, 10, 32, Palette.AlertCrimson);
                Raylib.DrawRectangle(clampLeft, horizonY - 26, 10, 26, Palette.AlertCrimson);
                Raylib.DrawRectangle(clampRight, horizonY - 26, 10, 26, Palette.AlertCrimson);
                Raylib.DrawLine(clampLeft + 5, ViewportSkyTopY + 32, clampLeft + 5, horizonY - 26, Palette.AlertCrimson);
                Raylib.DrawLine(clampRight + 5, ViewportSkyTopY + 32, clampRight + 5, horizonY - 26, Palette.AlertCrimson);
            }

            // 3. STRICT SCISSOR-CLIPPED SPS INSPECTION LENS (Zero Spoiler Text)
            if (_spsEnabled && _cachedShipSpsTex.Id != 0)
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
                Raylib.BeginScissorMode(ViewportX + 2, ViewportSkyTopY, ViewportW - 4, ViewportSkyH);

                bool reviewLensHover = _protocolReviewModeActive && mouse.IsHit(_lastSpsLensRect);
                bool lockingActive = _spsLockProgress > 0.01f;
                Color reticleCol = reviewLensHover
                    ? Palette.CrtAmberPrimary
                    : (lockingActive ? Palette.CrtAmberPrimary : Palette.SpsCommsActive);
                Raylib.DrawRectangleLines(lx, ly, lw, lh, reticleCol);
                int cx = lx + lw / 2;
                int cy = ly + lh / 2;
                Raylib.DrawLine(cx - 5, cy, cx + 5, cy, reticleCol);
                Raylib.DrawLine(cx, cy - 5, cx, cy + 5, reticleCol);
                Raylib.DrawLine(lx + 1, ly + 1, lx + 8, ly + 1, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + 1, ly + 1, lx + 1, ly + 8, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + lw - 9, ly + lh - 2, lx + lw - 2, ly + lh - 2, Palette.SpsShieldArcIce);
                Raylib.DrawLine(lx + lw - 2, ly + lh - 9, lx + lw - 2, ly + lh - 2, Palette.SpsShieldArcIce);

                // Subtle hold-to-lock progress bar at bottom edge of SPS lens (zero spoiler text)
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
            Raylib.DrawRectangle(118, 108, 314, 84, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(118, 108, 314, 84, Palette.CrtAmberPrimary);
            Raylib.DrawText("BROWN DWARF HORIZON CONTACT", 180, 118, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText($"DAY {_gameState.Day:D2} WATCH SHIFT COMPLETE", 188, 134, 10, Palette.ClearanceTeal);
            DrawHardwareButton(mouse, 132, 154, 286, 26, "END WATCH -> WALK TO QUARTERS", enabled: true, highlight: true, Palette.ClearanceTeal);
        }
        else
        {
            Raylib.DrawRectangle(146, 130, 258, 28, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(146, 130, 258, 28, Palette.WeatheredGunmetalEdge);
            Raylib.DrawText("APPROACH LANE CLEAR -- AWAITING HAIL", 162, 139, 10, Palette.CrtAmberDimmed);
        }

        Raylib.EndScissorMode();

        Raylib.DrawRectangleLines(ViewportX, ViewportY, ViewportW, ViewportH,
            _spsEnabled ? Palette.SpsCommsActive : Palette.WeatheredGunmetalEdge);
    }

    private void DrawHailingCrtViewscreen(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_hailing_crt.png",
            CrtX, CrtY, CrtW, CrtH,
            Palette.OutlineVoid,
            Palette.OxidizedBronzeWorn,
            string.Empty);

        const int portraitBoxX = CrtX + 8;
        const int portraitBoxY = CrtY + 8;
        const int portraitSize = Constants.PortraitWidth * 2; // 128x128

        Raylib.DrawRectangle(portraitBoxX, portraitBoxY, portraitSize, portraitSize, Palette.DeepSpaceVoid);

        bool viewscreenOpen = _activeShip != null &&
                              _phase != EncounterPhase.ShipGlidingIn &&
                              _cachedLivePortraitTex.Id != 0;

        if (_preShiftStage == PreShiftStage.Day3KordaniPreShiftHail && _cachedPreShiftKordaniPortraitTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(portraitBoxX, portraitBoxY, portraitSize, portraitSize);
            Raylib.DrawTexturePro(_cachedPreShiftKordaniPortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
        }
        else if (viewscreenOpen)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = new(portraitBoxX, portraitBoxY, portraitSize, portraitSize);
            Raylib.DrawTexturePro(_cachedLivePortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            Raylib.DrawText("HAILING CRT", portraitBoxX + 30, portraitBoxY + 52, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("STANDBY", portraitBoxX + 40, portraitBoxY + 66, 10, Palette.WeatheredGunmetalEdge);
        }

        Raylib.DrawRectangleLines(portraitBoxX, portraitBoxY, portraitSize, portraitSize, Palette.CrtAmberDimmed);

        // Below Portrait: Neutral Signal Status Panel
        const int sigY = portraitBoxY + portraitSize + 8;
        Raylib.DrawRectangle(portraitBoxX, sigY, portraitSize, 72, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(portraitBoxX, sigY, portraitSize, 72, Palette.WeatheredGunmetalEdge);

        string feedLine = _preShiftStage == PreShiftStage.Day3KordaniPreShiftHail
            ? "INTERNAL: KDF-SEC"
            : (_activeShip == null
                ? "SIGNAL: IDLE"
                : (_phase == EncounterPhase.ShipGlidingIn
                    ? "SIGNAL: STANDBY"
                    : (_activeShip.GroundTruth.UsesStaticViewscreen ? "FEED: AUDIO ONLY" : "FEED: LIVE VIDEO")));
        Raylib.DrawText(feedLine, portraitBoxX + 6, sigY + 6, 10, Palette.CrtAmberPrimary);

        string regLine = (_activeShip != null && _phase != EncounterPhase.ShipGlidingIn)
            ? $"ID: {_activeShip.Slate.RegistrationCode}"
            : (_phase == EncounterPhase.ShipGlidingIn ? "ID: PENDING" : "CHANNEL: OPEN");
        Raylib.DrawText(Truncate(regLine, 19), portraitBoxX + 6, sigY + 22, 10, Palette.CrtAmberDimmed);

        string reviewBadge = (_activeShip != null && _activeShip.ProtocolReviewVerified)
            ? "REVIEW: VERIFIED"
            : (_protocolReviewModeActive ? "REVIEW: ACTIVE" : "REVIEW: STANDBY");
        Raylib.DrawText(reviewBadge, portraitBoxX + 6, sigY + 38, 10,
            (_activeShip != null && _activeShip.ProtocolReviewVerified) ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);

        string bayState = _activeShip == null
            ? "BAY: EMPTY"
            : _phase switch
            {
                EncounterPhase.ShipGlidingIn => "BAY: APPROACHING",
                EncounterPhase.UploadingClearance => "BAY: UPLOADING",
                EncounterPhase.ResolvingDecision when _pendingDecision == PlayerDecision.GrantAccess => "BAY: DEPARTING",
                _ => "BAY: DOCK LOCKED"
            };
        Raylib.DrawText(bayState, portraitBoxX + 6, sigY + 54, 10, Palette.CrtAmberDimmed);

        // Right: Thematic Scrollable CRT Dialogue Transcript Box
        Raylib.DrawRectangle(TranscriptX, TranscriptY, TranscriptW, TranscriptH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(TranscriptX, TranscriptY, TranscriptW, TranscriptH, Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(TranscriptX + 1, TranscriptY + 1, TranscriptW - 2, 14, Palette.OutlineVoid);
        Raylib.DrawLine(TranscriptX + 1, TranscriptY + 15, TranscriptX + TranscriptW - 1, TranscriptY + 15, Palette.WeatheredGunmetalEdge);
        string channelTitle = _preShiftStage == PreShiftStage.Day3KordaniPreShiftHail
            ? "INTERNAL COMMS // KORDANI SEC"
            : "SUBSPACE COMMS TRANSCRIPT";
        Raylib.DrawText(channelTitle, TranscriptX + 6, TranscriptY + 3, 10, Palette.CrtAmberPrimary);

        List<(string Line, Color Col)> wrapped = WrapTranscriptLines(maxCharsPerLine: 34);
        int maxTop = Math.Max(0, wrapped.Count - TranscriptVisibleRows);
        if (_transcriptAutoFollowBottom)
        {
            _transcriptScrollTopLine = maxTop;
        }
        else
        {
            _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine, 0, maxTop);
        }

        int drawY = TranscriptY + 19;
        int endIdx = Math.Min(wrapped.Count, _transcriptScrollTopLine + TranscriptVisibleRows);
        for (int i = _transcriptScrollTopLine; i < endIdx; i++)
        {
            Raylib.DrawText(wrapped[i].Line, TranscriptX + 6, drawY, 10, wrapped[i].Col);
            drawY += 13;
        }

        DrawThematicCrtScrollbar(mouse, wrapped.Count, maxTop);
    }

    private void DrawThematicCrtScrollbar(VirtualMouseState mouse, int totalLines, int maxTop)
    {
        int barX = TranscriptX + TranscriptW - 16;
        int upY = TranscriptY + 16;
        int downY = TranscriptY + TranscriptH - 16;
        int trackTopY = upY + 16;
        int trackH = (downY - 2) - trackTopY;

        // Top Up-Arrow pixel-art amber button
        bool upHover = mouse.IsHit(barX, upY, 14, 14);
        Raylib.DrawRectangle(barX, upY, 14, 14, upHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(barX, upY, 14, 14, upHover ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
        Color upCol = _transcriptScrollTopLine > 0 ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawPixel(barX + 7, upY + 5, upCol);
        Raylib.DrawLine(barX + 6, upY + 6, barX + 8, upY + 6, upCol);
        Raylib.DrawLine(barX + 5, upY + 7, barX + 9, upY + 7, upCol);
        Raylib.DrawLine(barX + 4, upY + 8, barX + 10, upY + 8, upCol);

        // Slim Segmented CRT Phosphor Track
        Raylib.DrawRectangle(barX + 2, trackTopY, 10, trackH, Palette.OutlineVoid);
        for (int sy = trackTopY + 2; sy < trackTopY + trackH - 2; sy += 4)
        {
            Raylib.DrawLine(barX + 4, sy, barX + 9, sy, Palette.OxidizedBronzeShadow);
        }

        if (totalLines > TranscriptVisibleRows && maxTop > 0)
        {
            float ratio = (float)TranscriptVisibleRows / totalLines;
            int thumbH = Math.Clamp((int)MathF.Round(trackH * ratio), 14, trackH - 4);
            float scrollFraction = (float)_transcriptScrollTopLine / maxTop;
            int thumbY = trackTopY + 2 + (int)MathF.Round(scrollFraction * (trackH - thumbH - 4));
            Raylib.DrawRectangle(barX + 3, thumbY, 8, thumbH, Palette.CrtAmberPrimary);
        }
        else
        {
            Raylib.DrawRectangle(barX + 4, trackTopY + 2, 6, trackH - 4, Palette.CrtAmberDimmed);
        }

        // Bottom Down-Arrow pixel-art amber button
        bool downHover = mouse.IsHit(barX, downY, 14, 14);
        Raylib.DrawRectangle(barX, downY, 14, 14, downHover ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(barX, downY, 14, 14, downHover ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
        Color downCol = _transcriptScrollTopLine < maxTop ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawLine(barX + 4, downY + 5, barX + 10, downY + 5, downCol);
        Raylib.DrawLine(barX + 5, downY + 6, barX + 9, downY + 6, downCol);
        Raylib.DrawLine(barX + 6, downY + 7, barX + 8, downY + 7, downCol);
        Raylib.DrawPixel(barX + 7, downY + 8, downCol);
    }

    private void DrawMiddleHardwareControlStrip(VirtualMouseState mouse)
    {
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;

        // Left Status Readout Console (x=10, y=256, w=284, h=26)
        Raylib.DrawRectangle(10, 256, 284, 26, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(10, 256, 284, 26, _protocolReviewModeActive ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);

        string statusText = lockedByPreShift
            ? "PRE-SHIFT HOLD -- CONTROLS LOCKED"
            : (_protocolReviewModeActive
                ? (!string.IsNullOrEmpty(_reviewStatusNote) ? _reviewStatusNote : "PROTOCOL REVIEW: CLICK CLAIM vs REALITY")
                : _phase switch
                {
                    EncounterPhase.BayEmpty => _shiftController.IsShiftTimerExpired
                        ? "WATCH ENDED -- PROCEED TO QUARTERS"
                        : "BAY STANDBY -- CLICK [NEXT HAIL]",
                    EncounterPhase.ShipGlidingIn => "INBOUND VESSEL DOCKING APPROACH...",
                    EncounterPhase.PreClearanceHailing => "PRE-CLEARANCE: CHECK BAY VISUALS",
                    EncounterPhase.DownloadingProtocols => "STREAMING SUBSPACE PROTOCOLS...",
                    EncounterPhase.InspectingSlateAndShip => _isDataSlateDocked
                        ? "SLATE DOCKED -> [GRANT ACCESS]"
                        : (!string.IsNullOrEmpty(_reviewStatusNote) ? _reviewStatusNote : "INSPECT VESSEL OR SLOT DATA SLATE"),
                    EncounterPhase.UploadingClearance => "UPLOADING CLEARANCE...",
                    EncounterPhase.ResolvingDecision => _pendingDecision == PlayerDecision.GrantAccess
                        ? "CLEARANCE CONFIRMED -- DEPARTING"
                        : "ENGAGING STATION CLAMPS...",
                    _ => string.Empty
                });

        Raylib.DrawText(TruncateByPixelWidth(statusText, 10, 272), 16, 264, 10, Palette.CrtAmberPrimary);

        // 1. NEXT HAIL (x=300, y=256, w=104, h=26)
        bool canHail = !lockedByPreShift && _shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty;
        string hailLabel = lockedByPreShift ? "LOCKED" : (_shiftController.IsShiftTimerExpired ? "WATCH ENDED" : "NEXT HAIL");
        DrawHardwareButton(mouse, 300, 256, 104, 26, hailLabel, enabled: canHail, highlight: canHail, Palette.CrtAmberPrimary);

        // 2. LOWER SHIELDS (x=410, y=256, w=122, h=26) -- Uniform neutral styling
        bool shipInteractable = !lockedByPreShift && _activeShip != null &&
                                (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.InspectingSlateAndShip);
        DrawHardwareButton(
            mouse, 410, 256, 122, 26,
            _pendingShieldsOrder ? "ORDERING..." : "LOWER SHIELDS",
            enabled: shipInteractable && !_pendingShieldsOrder,
            highlight: false,
            Palette.CrtAmberPrimary);

        // 3. POWER DOWN WPN (x=538, y=256, w=130, h=26) -- Uniform neutral styling
        DrawHardwareButton(
            mouse, 538, 256, 130, 26,
            _pendingWeaponsOrder ? "ORDERING..." : "POWER DOWN WPN",
            enabled: shipInteractable && !_pendingWeaponsOrder,
            highlight: false,
            Palette.CrtAmberPrimary);

        // 4. PROTOCOL REVIEW Toggle (x=674, y=256, w=120, h=26)
        bool canReview = !lockedByPreShift && _activeShip != null &&
                         (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.InspectingSlateAndShip);
        DrawHardwareButton(
            mouse, 674, 256, 120, 26,
            _protocolReviewModeActive ? "REVIEW: ON" : "REVIEW: OFF",
            enabled: canReview,
            highlight: _protocolReviewModeActive,
            Palette.CrtAmberPrimary);

        // 5. SPS SCAN TOGGLE (x=800, y=256, w=144, h=26 -- aligned directly above SPS Anomaly Mini-Screen!)
        DrawHardwareButton(
            mouse, 800, 256, 144, 26,
            _spsEnabled ? "SPS: ACTIVE" : "SPS: OFF",
            enabled: !lockedByPreShift,
            highlight: _spsEnabled && !lockedByPreShift,
            Palette.SpsCommsActive);
    }

    private void DrawBottomDeskWorkspace(VirtualMouseState mouse)
    {
        AssetManager.DrawImageOrPlaceholder(
            "locations/desk_surface.png",
            DeskSurfaceX, DeskSurfaceY, DeskSurfaceW, DeskSurfaceH,
            Palette.WeatheredGunmetalMid,
            Palette.WeatheredGunmetalEdge,
            string.Empty);

        Raylib.DrawLine(162, DeskSurfaceY + 2, 162, DeskSurfaceY + DeskSurfaceH - 2, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(794, DeskSurfaceY + 2, 794, DeskSurfaceY + DeskSurfaceH - 2, Palette.WeatheredGunmetalEdge);

        DrawLeftDeskHardwareProps(mouse);
        DrawRightSideClearanceDockAndDetainSwitch(mouse);

        // Render movable desk documents in Z-order (bottom to top)
        for (int i = 0; i < _zOrder.Count; i++)
        {
            DeskItemId id = _zOrder[i];
            DraggableDeskItem item = _deskItems[id];
            if (!item.IsVisible)
                continue;

            bool isTopmost = (i == _zOrder.Count - 1);
            switch (id)
            {
                case DeskItemId.ExpectedList:
                    DrawExpectedShipListClipboard(mouse, item, isTopmost);
                    break;
                case DeskItemId.DataSlate:
                    DrawMovableDataSlate(mouse, item, isTopmost);
                    break;
                case DeskItemId.ReferenceManual:
                    DrawDeskReferenceManual(mouse, item, isTopmost);
                    break;
                case DeskItemId.WarpStrip:
                    DrawFreeDraggableWarpStrip(mouse, item, isTopmost);
                    break;
            }
        }

        // If Day 3+ thermal paper strip is extruding or anchored at the spectrometer slot,
        // draw it cleanly emerging downward from the printer mouth!
        if (IsSpectrometerOperational &&
            (_warpPhase is WarpPrinterPhase.PrintingStrip or WarpPrinterPhase.AnchoredReadyToTear))
        {
            DrawExtrudingOrAnchoredWarpStrip(mouse);
        }
    }

    private void DrawLeftDeskHardwareProps(VirtualMouseState mouse)
    {
        bool operational = IsSpectrometerOperational;

        // 1. Warp Field Residue Spectrometer (x=16, y=292, w=142, h=104)
        Raylib.DrawRectangle(LeftColX, SpecY, LeftColW, SpecH, operational ? Palette.OutlineVoid : Palette.OxidizedBronzeShadow);
        Raylib.DrawRectangleLines(LeftColX, SpecY, LeftColW, SpecH, operational ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn);

        Raylib.DrawText("WARP RESIDUE", LeftColX + 8, SpecY + 6, 10, operational ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        Raylib.DrawText("SPECTROMETER", LeftColX + 8, SpecY + 18, 10, operational ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);

        if (!operational)
        {
            // Strictly dark with yellow MAINTENANCE PENDING tag on Days 1-2
            Raylib.DrawRectangle(LeftColX + 8, SpecY + 34, LeftColW - 16, 12, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(LeftColX + 8, SpecY + 34, LeftColW - 16, 12, Palette.WeatheredGunmetalEdge);

            Raylib.DrawRectangle(LeftColX + 6, SpecY + 52, LeftColW - 12, 40, Palette.CrtAmberPrimary);
            Raylib.DrawRectangleLines(LeftColX + 6, SpecY + 52, LeftColW - 12, 40, Palette.OutlineVoid);
            Raylib.DrawText("MAINTENANCE", LeftColX + 26, SpecY + 59, 10, Palette.OutlineVoid);
            Raylib.DrawText($"PENDING [DAY {_gameState.Day:D2}]", LeftColX + 18, SpecY + 73, 10, Palette.OutlineVoid);
        }
        else
        {
            // Day 3+: Status Readout + Physical [SCAN WARP] Button + Paper Extrusion Mouth
            Raylib.DrawRectangle(LeftColX + 6, SpecY + 32, LeftColW - 12, 24, Palette.DeepSpaceVoid);
            Raylib.DrawRectangleLines(LeftColX + 6, SpecY + 32, LeftColW - 12, 24, Palette.CrtAmberDimmed);

            if (_warpPhase == WarpPrinterPhase.ScanningWarpField)
            {
                float sweepRatio = Math.Clamp(_warpPhaseElapsed / WarpScanDurationSeconds, 0f, 1f);
                int barW = (int)MathF.Round((LeftColW - 20) * sweepRatio);
                Raylib.DrawRectangle(LeftColX + 10, SpecY + 36, barW, 6, Palette.ClearanceTeal);
                Raylib.DrawText("SCANNING WARP...", LeftColX + 10, SpecY + 45, 10, Palette.ClearanceTeal);
            }
            else if (_warpPhase == WarpPrinterPhase.PrintingStrip)
            {
                Raylib.DrawText("EXTRUDING THERMAL", LeftColX + 10, SpecY + 35, 10, Palette.CrtAmberPrimary);
                Raylib.DrawText("PAPER STRIP...", LeftColX + 10, SpecY + 45, 10, Palette.CrtAmberDimmed);
            }
            else if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear)
            {
                Raylib.DrawText("STRIP READY BELOW", LeftColX + 10, SpecY + 35, 10, Palette.ClearanceTeal);
                Raylib.DrawText("PULL TO TEAR FREE", LeftColX + 10, SpecY + 45, 10, Palette.CrtAmberPrimary);
            }
            else if (_warpPhase == WarpPrinterPhase.TornOffFreeDocument)
            {
                Raylib.DrawText("COIL ONLINE [HOT]", LeftColX + 10, SpecY + 35, 10, Palette.ClearanceTeal);
                Raylib.DrawText("STRIP ON DESK", LeftColX + 10, SpecY + 45, 10, Palette.CrtAmberDimmed);
            }
            else
            {
                Raylib.DrawText("COIL ONLINE [HOT]", LeftColX + 10, SpecY + 35, 10, Palette.ClearanceTeal);
                Raylib.DrawText("READY FOR SCAN", LeftColX + 10, SpecY + 45, 10, Palette.CrtAmberDimmed);
            }

            // Physical [SCAN WARP] button
            bool canScan = CanStartManualWarpScan();
            string scanLabel = _warpPhase switch
            {
                WarpPrinterPhase.ScanningWarpField => "SCANNING...",
                WarpPrinterPhase.PrintingStrip => "PRINTING...",
                WarpPrinterPhase.AnchoredReadyToTear => "TEAR STRIP",
                WarpPrinterPhase.TornOffFreeDocument => "SCAN DONE",
                _ => "SCAN WARP"
            };
            DrawHardwareButton(mouse, LeftColX + 6, SpecY + 62, LeftColW - 12, 22, scanLabel, canScan, canScan, Palette.ClearanceTeal);

            // Physical dark printer mouth slot along bottom edge of spectrometer
            Raylib.DrawRectangle(LeftColX + 6, PrinterSlotY, LeftColW - 12, 6, Palette.OutlineVoid);
            Raylib.DrawLine(LeftColX + 6, PrinterSlotY, LeftColX + LeftColW - 6, PrinterSlotY, Palette.WeatheredGunmetalEdge);
        }

        // 2. Protocol Review Log Panel (x=16, y=402, w=142, h=126)
        const int logY = 402;
        const int logH = 126;

        Raylib.DrawRectangle(LeftColX, logY, LeftColW, logH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(LeftColX, logY, LeftColW, logH,
            (_activeShip != null && _activeShip.ProtocolReviewVerified) ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("PROTOCOL REVIEW", LeftColX + 8, logY + 6, 10, Palette.CrtAmberPrimary);
        Raylib.DrawLine(LeftColX + 4, logY + 18, LeftColX + LeftColW - 4, logY + 18, Palette.WeatheredGunmetalEdge);

        if (_activeShip != null && _activeShip.ProtocolReviewVerified)
        {
            Raylib.DrawText("STATUS: VERIFIED", LeftColX + 6, logY + 23, 10, Palette.ClearanceTeal);
            int wy = logY + 37;
            foreach (string wLine in WrapSingleParagraph(_verifiedViolationSummary, 21))
            {
                if (wy > logY + 76) break;
                Raylib.DrawText(wLine, LeftColX + 6, wy, 10, Palette.CrtAmberPrimary);
                wy += 12;
            }
        }
        else if (_protocolReviewModeActive)
        {
            string stepLine = _reviewFirstSelection == ReviewTarget.None
                ? "CLICK CLAIM OR REALITY"
                : $"1/2: {FormatReviewTargetLabel(_reviewFirstSelection)}";
            Raylib.DrawText(Truncate(stepLine, 21), LeftColX + 6, logY + 24, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText("COMPARE SLATE vs", LeftColX + 6, logY + 40, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("CYCLE / HULL / RELAY", LeftColX + 6, logY + 52, 10, Palette.CrtAmberDimmed);
        }
        else if (!string.IsNullOrEmpty(_reviewStatusNote))
        {
            int wy = logY + 24;
            foreach (string wLine in WrapSingleParagraph(_reviewStatusNote, 21))
            {
                if (wy > logY + 72) break;
                Raylib.DrawText(wLine, LeftColX + 6, wy, 10, Palette.CrtAmberDimmed);
                wy += 12;
            }
        }
        else
        {
            Raylib.DrawText("NO REVIEW VERIFIED.", LeftColX + 6, logY + 24, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("USE [REVIEW: ON] TO", LeftColX + 6, logY + 40, 10, Palette.WeatheredGunmetalEdge);
            Raylib.DrawText("VERIFY VIOLATIONS", LeftColX + 6, logY + 52, 10, Palette.WeatheredGunmetalEdge);
            Raylib.DrawText("BEFORE DETAINING.", LeftColX + 6, logY + 64, 10, Palette.WeatheredGunmetalEdge);
        }

        // Day 2 Ghost Route Optional Pushback Button
        if (_activeShip != null &&
            _activeShip.GroundTruth.IsDay2GhostRouteSuspect &&
            _phase == EncounterPhase.InspectingSlateAndShip &&
            !_day2SuspectQuestioned)
        {
            DrawSmallButton(mouse, LeftColX + 6, 482, LeftColW - 12, 20, "QUESTION ROUTE", Palette.CrtAmberPrimary);
        }

        DrawSmallButton(mouse, LeftColX + 6, 506, LeftColW - 12, 18, "VIEW SHIFT BRIEFING");
    }

    private void DrawExtrudingOrAnchoredWarpStrip(VirtualMouseState mouse)
    {
        float progress = _warpPhase == WarpPrinterPhase.PrintingStrip
            ? Math.Clamp(_warpPhaseElapsed / WarpPrintDurationSeconds, 0f, 1f)
            : 1.0f;

        int visibleH = Math.Max(10, (int)MathF.Round(StripFullHeight * progress));
        int sx = StripAnchorX + _anchoredStripPullOffsetX;
        int sy = StripAnchorY + _anchoredStripPullOffsetY;

        // Thermal paper strip backing emerging smoothly downward from printer mouth
        Raylib.BeginScissorMode(sx - 2, PrinterSlotY + 2, StripWidth + 4, StripFullHeight + 16);
        DrawWarpPaperStripBody(mouse, sx, sy, StripWidth, visibleH, progress, isTornOff: false, isTopmost: true);
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

        Color borderCol = reviewSelected
            ? Palette.ClearanceTeal
            : (reviewHover || isTopmost ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);

        // Warm dark thermal paper background with crisp perforated top tear seam
        Raylib.DrawRectangle(x, y, w, h, new Color(24, 21, 16, 255));
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);

        // Perforated tear-off teeth along top edge
        for (int tx = x + 3; tx < x + w - 3; tx += 6)
        {
            Raylib.DrawLine(tx, y + 1, tx + 3, y + 1, Palette.CrtAmberDimmed);
        }

        if (h >= 18)
        {
            Raylib.DrawText($"WARP: {Truncate(_warpStripShipReg, 12)}", x + 6, y + 5, 10, Palette.CrtAmberPrimary);
        }

        // Progressive line-by-line analog waveform rendering inside the paper strip
        if (h >= 34 && !string.IsNullOrEmpty(_warpStripActualOrigin))
        {
            int waveBoxY = y + 19;
            int waveBoxH = Math.Min(36, h - 24);
            Raylib.DrawRectangle(x + 5, waveBoxY, w - 10, waveBoxH, Palette.OutlineVoid);
            Raylib.DrawRectangleLines(x + 5, waveBoxY, w - 10, waveBoxH, Palette.OxidizedBronzeShadow);

            int drawWaveW = (int)MathF.Round((w - 16) * printProgress);
            DrawAnalogWaveform(_warpStripActualOrigin, x + 8, waveBoxY + 3, w - 16, waveBoxH - 6, drawWaveW, Palette.CrtAmberPrimary);
        }

        if (h >= 68)
        {
            string footer = isTornOff
                ? "THERMAL STRIP (DRAG)"
                : (printProgress >= 1.0f ? "DRAG TO TEAR FREE" : "FEEDING PAPER...");
            Color footCol = (!isTornOff && printProgress >= 1.0f) ? Palette.ClearanceTeal : Palette.CrtAmberDimmed;
            Raylib.DrawText(footer, x + 8, y + h - 14, 10, footCol);
        }
    }

    private void DrawSpsMiniScreenAnomalyLog(VirtualMouseState mouse)
    {
        bool hasLogged = _spsLoggedAnomalies.Count > 0;
        bool locking = _spsLockProgress > 0.01f;
        Color frameCol = hasLogged
            ? Palette.CrtAmberPrimary
            : (_spsEnabled ? Palette.SpsCommsActive : Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, frameCol);

        Raylib.DrawRectangle(SpsMiniX + 1, SpsMiniY + 1, SpsMiniW - 2, 12, Palette.OutlineVoid);
        Raylib.DrawLine(SpsMiniX + 1, SpsMiniY + 13, SpsMiniX + SpsMiniW - 1, SpsMiniY + 13, Palette.WeatheredGunmetalEdge);
        Raylib.DrawText("SPS ANOMALY LOG", SpsMiniX + 6, SpsMiniY + 2, 10,
            _spsEnabled || hasLogged ? Palette.SpsCommsActive : Palette.CrtAmberDimmed);

        if (hasLogged)
        {
            int maxRows = Math.Min(2, _spsLoggedAnomalies.Count);
            for (int i = 0; i < maxRows; i++)
            {
                SpsAnomalyEntry entry = _spsLoggedAnomalies[i];
                int ry = SpsMiniY + 15 + i * 17;
                bool hovered = _phase == EncounterPhase.InspectingSlateAndShip &&
                               mouse.IsHit(SpsMiniX + 4, ry, SpsMiniW - 8, 15);
                Color rowBorder = entry.WasConfronted
                    ? Palette.ClearanceTeal
                    : (hovered ? Palette.CrtAmberPrimary : entry.AccentColor);
                Color rowBg = hovered ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid;
                Color rowFg = entry.WasConfronted ? Palette.ClearanceTeal : entry.AccentColor;
                string rowText = entry.WasConfronted ? entry.Label.Replace("[!]", "[OK]") : entry.Label;

                Raylib.DrawRectangle(SpsMiniX + 4, ry, SpsMiniW - 8, 15, rowBg);
                Raylib.DrawRectangleLines(SpsMiniX + 4, ry, SpsMiniW - 8, 15, rowBorder);
                Raylib.DrawText(TruncateByPixelWidth(rowText, 10, SpsMiniW - 16), SpsMiniX + 8, ry + 3, 10, rowFg);
            }
        }
        else if (locking)
        {
            Raylib.DrawText("LOCKING SIGNAL...", SpsMiniX + 8, SpsMiniY + 18, 10, Palette.CrtAmberPrimary);
            int maxBarW = SpsMiniW - 16;
            int fillW = Math.Clamp((int)MathF.Round(maxBarW * _spsLockProgress), 1, maxBarW);
            Raylib.DrawRectangle(SpsMiniX + 8, SpsMiniY + 33, maxBarW, 8, Palette.OutlineVoid);
            Raylib.DrawRectangle(SpsMiniX + 8, SpsMiniY + 33, fillW, 8, Palette.SpsCommsActive);
            Raylib.DrawRectangleLines(SpsMiniX + 8, SpsMiniY + 33, maxBarW, 8, Palette.SpsCommsActive);
        }
        else if (_spsEnabled)
        {
            if (_activeShip != null && _activeShip.GroundTruth.ActualShieldsUp)
            {
                Raylib.DrawText("SHIELD BLOCKED", SpsMiniX + 8, SpsMiniY + 18, 10, Palette.SpsShieldArcIce);
                Raylib.DrawText("LOWER SHIELDS", SpsMiniX + 8, SpsMiniY + 31, 10, Palette.CrtAmberDimmed);
            }
            else
            {
                Raylib.DrawText("HOLD LENS 0.5S", SpsMiniX + 8, SpsMiniY + 18, 10, Palette.SpsCommsActive);
                Raylib.DrawText("TO LOG ANOMALY", SpsMiniX + 8, SpsMiniY + 31, 10, Palette.WeatheredGunmetalEdge);
            }
        }
        else
        {
            Raylib.DrawText("SPS STANDBY", SpsMiniX + 8, SpsMiniY + 18, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("NO LOGGED SCAN", SpsMiniX + 8, SpsMiniY + 31, 10, Palette.WeatheredGunmetalEdge);
        }
    }

    private void DrawRightSideClearanceDockAndDetainSwitch(VirtualMouseState mouse)
    {
        DrawSpsMiniScreenAnomalyLog(mouse);

        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;

        bool draggingSlateNearDock = !lockedByPreShift &&
                                     _draggingItemId == DeskItemId.DataSlate &&
                                     _deskItems[DeskItemId.DataSlate].X + _deskItems[DeskItemId.DataSlate].Width >= DockPanelX - 16;

        bool isUploadingClearance = _phase == EncounterPhase.UploadingClearance && _pendingDecision == PlayerDecision.GrantAccess;
        bool isDepartingAfterUpload = _phase == EncounterPhase.ResolvingDecision && _pendingDecision == PlayerDecision.GrantAccess;

        Color dockBorder = lockedByPreShift
            ? Palette.WeatheredGunmetalEdge
            : (_isDataSlateDocked || isUploadingClearance || isDepartingAfterUpload
                ? Palette.ClearanceTeal
                : (draggingSlateNearDock ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        AssetManager.DrawImageOrPlaceholder(
            "ui/clearance_dock_port.png",
            DockPanelX, DockPanelY, DockPanelW, DockPanelH,
            Palette.OutlineVoid,
            dockBorder,
            string.Empty);

        Raylib.DrawText("CLEARANCE DOCK", DockPanelX + 16, DockPanelY + 5, 10,
            lockedByPreShift ? Palette.WeatheredGunmetalEdge : Palette.ClearanceTeal);

        // Glowing teal USB coupler latch connecting a docked Data Slate cleanly into the Clearance Dock
        if (_isDataSlateDocked || isUploadingClearance || isDepartingAfterUpload)
        {
            Raylib.DrawRectangle(DockPanelX - 4, DockPanelY + 28, 6, 14, Palette.ClearanceTeal);
            Raylib.DrawRectangle(DockPanelX - 3, DockPanelY + 31, 4, 8, Palette.SpsShieldArcIce);
        }

        Raylib.DrawRectangle(DockPanelX + 8, DockPanelY + 19, DockPanelW - 16, 34, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(DockPanelX + 8, DockPanelY + 19, DockPanelW - 16, 34, dockBorder);

        bool pulseLed = ((_animTick / 6) % 2) == 0;
        for (int pin = 0; pin < 7; pin++)
        {
            int px = DockPanelX + 16 + pin * 16;
            Color pinCol = isUploadingClearance
                ? (pulseLed ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                : (_isDataSlateDocked || isDepartingAfterUpload ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangle(px, DockPanelY + 24, 8, 4, pinCol);
        }

        if (isUploadingClearance)
        {
            float progress = Math.Clamp(_clearanceUploadElapsed / ClearanceUploadDurationSeconds, 0f, 1f);
            int barMaxW = DockPanelW - 24;
            int barW = (int)MathF.Round(barMaxW * progress);
            Raylib.DrawRectangle(DockPanelX + 12, DockPanelY + 34, barMaxW, 11, Palette.OutlineVoid);
            Raylib.DrawRectangle(DockPanelX + 12, DockPanelY + 34, barW, 11, Palette.ClearanceTeal);
            Raylib.DrawRectangleLines(DockPanelX + 12, DockPanelY + 34, barMaxW, 11, Palette.ClearanceTeal);

            int pct = (int)MathF.Round(progress * 100f);
            Raylib.DrawText($"UPLOADING {pct}%", DockPanelX + 14, DockPanelY + 58, 10, Palette.ClearanceTeal);
        }
        else if (isDepartingAfterUpload)
        {
            int barMaxW = DockPanelW - 24;
            Raylib.DrawRectangle(DockPanelX + 12, DockPanelY + 34, barMaxW, 11, Palette.ClearanceTeal);
            Raylib.DrawRectangleLines(DockPanelX + 12, DockPanelY + 34, barMaxW, 11, Palette.SpsShieldArcIce);
            Raylib.DrawText("UPLOAD COMPLETE!", DockPanelX + 14, DockPanelY + 58, 10, Palette.ClearanceTeal);
        }
        else
        {
            string slotLabel = lockedByPreShift
                ? "DOCK LOCKED"
                : (_isDataSlateDocked ? "SLATE LOCKED" : (draggingSlateNearDock ? ">> DROP SLATE <<" : "DRAG SLATE HERE"));
            Raylib.DrawText(slotLabel, DockPanelX + 14, DockPanelY + 35, 10,
                _isDataSlateDocked ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

            Color ledCol = _isDataSlateDocked ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge;
            Raylib.DrawCircle(DockPanelX + 18, DockPanelY + 63, 4, ledCol);
            Raylib.DrawText(_isDataSlateDocked ? "LINK: ACTIVE" : "LINK: IDLE", DockPanelX + 28, DockPanelY + 58, 10, ledCol);
        }

        bool canGrant = !lockedByPreShift &&
                        _activeShip != null &&
                        _phase == EncounterPhase.InspectingSlateAndShip &&
                        _isDataSlateDocked;
        string grantBtnLabel = isUploadingClearance
            ? "UPLOADING..."
            : (isDepartingAfterUpload ? "DEPARTING..." : (canGrant ? "GRANT ACCESS" : "SLOT DATA SLATE"));
        DrawHardwareButton(
            mouse, DockPanelX + 8, DockPanelY + 75, DockPanelW - 16, 22,
            grantBtnLabel,
            enabled: canGrant,
            highlight: canGrant || isUploadingClearance,
            activeColor: Palette.ClearanceTeal);

        // Physical Red DETAIN Switch -- ALWAYS UNLOCKED once a vessel has hailed!
        bool canDetain = !lockedByPreShift && _activeShip != null &&
                         (_phase is EncounterPhase.PreClearanceHailing or EncounterPhase.DownloadingProtocols or EncounterPhase.InspectingSlateAndShip);
        bool detainHovered = canDetain && mouse.IsHit(DetainSwitchX, DetainSwitchY, DetainSwitchW, DetainSwitchH);

        Color detainBg = !canDetain
            ? Palette.OutlineVoid
            : (_detainLeverPulled ? Palette.AlertCrimson : (detainHovered ? Palette.BrownDwarfGlowRim : Palette.BrownDwarfGlowDeep));
        Color detainBorder = canDetain ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge;

        Raylib.DrawRectangle(DetainSwitchX, DetainSwitchY, DetainSwitchW, DetainSwitchH, detainBg);
        Raylib.DrawRectangleLines(DetainSwitchX, DetainSwitchY, DetainSwitchW, DetainSwitchH, detainBorder);

        int leverY = _detainLeverPulled ? (DetainSwitchY + 28) : (DetainSwitchY + 6);
        Raylib.DrawRectangle(DetainSwitchX + 8, DetainSwitchY + 5, 14, 38, Palette.OutlineVoid);
        Raylib.DrawRectangle(DetainSwitchX + 9, leverY, 12, 13, detainBorder);

        Raylib.DrawText("DETAIN VESSEL", DetainSwitchX + 28, DetainSwitchY + 6, 10,
            _detainLeverPulled ? Palette.OutlineVoid : detainBorder);
        Raylib.DrawText("PULL TO CLAMP", DetainSwitchX + 28, DetainSwitchY + 19, 10,
            _detainLeverPulled ? Palette.OutlineVoid : (canDetain ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        string reviewSubTag = (_activeShip != null && _activeShip.ProtocolReviewVerified)
            ? "REVIEW: VERIFIED"
            : "DISCRETION ARMED";
        Raylib.DrawText(reviewSubTag, DetainSwitchX + 28, DetainSwitchY + 33, 10,
            _detainLeverPulled ? Palette.OutlineVoid : ((_activeShip != null && _activeShip.ProtocolReviewVerified) ? Palette.ClearanceTeal : Palette.CrtAmberDimmed));

        // Late-Game Destroy Override Switch (Day 14+ & KDF rep >= +8)
        bool destroyUnlocked = !lockedByPreShift &&
                               _gameState.GetRep("KDF-") >= Constants.RepKordaniDestroyUnlock &&
                               _gameState.Day >= 14;
        Raylib.DrawRectangle(DestroySwitchX, DestroySwitchY, DestroySwitchW, DestroySwitchH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(DestroySwitchX, DestroySwitchY, DestroySwitchW, DestroySwitchH,
            destroyUnlocked ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
        Raylib.DrawText(
            destroyUnlocked ? "DESTROY [ARMED]" : "DESTROY [LOCKED]",
            DestroySwitchX + 18, DestroySwitchY + 8, 10,
            destroyUnlocked ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
    }

    // =========================================================================
    // 1. Collapsible Expected Ship List Clipboard (Zero Automatic Hint Colors)
    // =========================================================================
    private void DrawExpectedShipListClipboard(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;

        Color borderCol = isTopmost ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn;

        AssetManager.DrawImageOrPlaceholder(
            "ui/expected_list_clipboard.png",
            x, y, w, h,
            Palette.OutlineVoid,
            borderCol,
            string.Empty);

        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 20, Palette.OxidizedBronzeShadow);
        Raylib.DrawText("EXPECTED SHIPS (10)", x + 8, y + 6, 10, Palette.CrtAmberPrimary);

        int toggleX = x + w - 22;
        int toggleY = y + 3;
        bool toggleHover = mouse.IsHit(toggleX, toggleY, 18, 16);
        Raylib.DrawRectangle(toggleX, toggleY, 18, 16, toggleHover ? Palette.WeatheredGunmetalEdge : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(toggleX, toggleY, 18, 16, Palette.CrtAmberPrimary);
        Raylib.DrawText(item.IsCollapsed ? "+" : "-", toggleX + 6, toggleY + 3, 10, Palette.CrtAmberPrimary);

        if (item.IsCollapsed)
            return;

        Raylib.DrawText("REG CODE    | STATUS", x + 8, y + 26, 10, Palette.CrtAmberDimmed);
        Raylib.DrawLine(x + 4, y + 38, x + w - 4, y + 38, Palette.WeatheredGunmetalEdge);

        IReadOnlyList<ExpectedShipEntry> list = _shipGenerator.ExpectedShipsToday;

        for (int i = 0; i < list.Count; i++)
        {
            ExpectedShipEntry entry = list[i];
            int rowY = y + 43 + i * 19;

            Color rowCol = entry.WasResolved ? Palette.CrtAmberDimmed : Palette.CrtAmberPrimary;
            string statusTag = entry.WasResolved ? entry.ResolutionTag : "SCHED";
            string lineText = $"{entry.SlotNumber:D2}. {entry.RegistrationCode,-10} [{statusTag}]";
            Raylib.DrawText(Truncate(lineText, 27), x + 6, rowY, 10, rowCol);
        }
    }

    // =========================================================================
    // 2. Movable 3:4 Data Slate (Animated Progressive Download + Review Claims)
    // =========================================================================
    private void DrawMovableDataSlate(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        if (_activeShip == null)
            return;

        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;

        DataSlateModel slate = _activeShip.Slate;
        float dl = ProtocolDownloadProgress;
        bool isDownloading = _phase == EncounterPhase.DownloadingProtocols;

        Color borderCol = _isDataSlateDocked
            ? Palette.ClearanceTeal
            : (isTopmost ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);

        AssetManager.DrawImageOrPlaceholder(
            "ui/data_slate_frame.png",
            x, y, w, h,
            Palette.OutlineVoid,
            borderCol,
            string.Empty);

        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 16, _isDataSlateDocked ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        string slateHeader = isDownloading
            ? $"RECEIVING PACKET {(int)(dl * 100)}%"
            : (_isDataSlateDocked ? "DATA SLATE [DOCKED]" : "SUBSPACE DATA SLATE");
        Raylib.DrawText(slateHeader, x + 8, y + 4, 10, isDownloading ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

        // Segmented Subspace Transfer Progress Bar during the 1.85s download animation
        if (isDownloading)
        {
            int totalSegs = 14;
            int filledSegs = (int)MathF.Floor(dl * totalSegs);
            for (int s = 0; s < totalSegs; s++)
            {
                int sx = x + 6 + s * 12;
                Color segCol = s <= filledSegs ? Palette.ClearanceTeal : Palette.WeatheredGunmetalMid;
                Raylib.DrawRectangle(sx, y + 18, 9, 3, segCol);
            }
        }

        // Top-Left: 64x64 Procedural Holo-Portrait (streams top-to-bottom during download)
        int holoX = x + 6;
        int holoY = y + 23;
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
        Raylib.DrawRectangleLines(holoX - 1, holoY - 1, Constants.PortraitWidth + 2, Constants.PortraitHeight + 2, Palette.CrtAmberDimmed);

        // Protocol Review Claim Hotspots (only when download is complete)
        if (_protocolReviewModeActive && !isDownloading)
        {
            int rowW = w - 8;
            DrawReviewHotspotOutline(mouse, x + 74, y + 21, 112, 26, _reviewFirstSelection == ReviewTarget.DataSlatePrefix);
            DrawReviewHotspotOutline(mouse, x + 74, y + 60, 112, 14, _reviewFirstSelection == ReviewTarget.DataSlateWeapons);
            DrawReviewHotspotOutline(mouse, x + 4, y + 106, rowW, 14, _reviewFirstSelection == ReviewTarget.DataSlateOrg);
            DrawReviewHotspotOutline(mouse, x + 4, y + 121, rowW, 14, _reviewFirstSelection == ReviewTarget.DataSlateExpiry);
            DrawReviewHotspotOutline(mouse, x + 4, y + 151, rowW, 14, _reviewFirstSelection == ReviewTarget.DataSlateSpecies);
            DrawReviewHotspotOutline(mouse, x + 4, y + 166, rowW, 14, _reviewFirstSelection == ReviewTarget.DataSlateOrigin);
            DrawReviewHotspotOutline(mouse, x + 4, y + 196, rowW, 28, _reviewFirstSelection == ReviewTarget.DataSlateCargo);
        }

        // Progressive Top-to-Bottom Field Population (Zero Auto-Hint Colors, Pixel-Accurate Width Clamping)
        int rightColX = x + 76;
        int rightColMaxPx = w - 82;
        if (dl >= 0.12f)
        {
            Raylib.DrawText("REG CODE:", rightColX, y + 23, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx), rightColX, y + 34, 10, Palette.CrtAmberPrimary);
        }
        if (dl >= 0.22f)
        {
            Raylib.DrawText($"CLASS: {ShortClass(slate.DeclaredShipClass)}", rightColX, y + 49, 10, Palette.CrtAmberPrimary);
        }
        if (dl >= 0.32f)
        {
            Raylib.DrawText($"WPN: {slate.DeclaredWeaponsStatus}", rightColX, y + 62, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText($"MASS: {slate.DeclaredTonnage} T", rightColX, y + 75, 10, Palette.CrtAmberDimmed);
        }

        int fieldX = x + 6;
        int fieldMaxPx = w - 12;
        int fieldY = y + 93;
        const int lineStep = 15;

        Raylib.DrawLine(x + 4, y + 90, x + w - 4, y + 90, Palette.WeatheredGunmetalEdge);
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
                Raylib.DrawText(cargoLines[c], fieldX, cargoBaseY + c * 13, 10, Palette.CrtAmberPrimary);
            }
        }
    }

    // =========================================================================
    // 3. Movable Tabbed Desk Reference Manual (with Day 1 Subspace Relay Ping)
    // =========================================================================
    private void DrawDeskReferenceManual(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;

        Color borderCol = isTopmost ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed;

        AssetManager.DrawImageOrPlaceholder(
            "ui/desk_manual_frame.png",
            x, y, w, h,
            Palette.OutlineVoid,
            borderCol,
            string.Empty);

        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 16, Palette.OxidizedBronzeShadow);
        Raylib.DrawText("DESK REFERENCE MANUAL", x + 8, y + 4, 10, Palette.CrtAmberPrimary);

        DrawManualTabButton(mouse, x + 6, y + 20, 76, 16, "1.PREFIXES", _manualActiveTab == 0);
        DrawManualTabButton(mouse, x + 85, y + 20, 76, 16, "2.HULLS", _manualActiveTab == 1);
        DrawManualTabButton(mouse, x + 164, y + 20, 76, 16, "3.STARS", _manualActiveTab == 2);

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

            Raylib.DrawText("UNR-  UNREGISTERED -> DETAIN!", x + 10, y + 184, 10, Palette.AlertCrimson);
            Raylib.DrawLine(x + 6, y + 195, x + w - 6, y + 195, Palette.WeatheredGunmetalEdge);
            DrawSubspaceRelayWidget(mouse, item);
        }
        else if (_manualActiveTab == 1)
        {
            ShipFaction faction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(faction);
            ShipClass selectedClass = allowedClasses[Math.Clamp(_manualClassIndex, 0, allowedClasses.Count - 1)];

            DrawSmallButton(mouse, x + 6, y + 40, 72, 16, "< FACTION");
            if (_devDebug.DevManualSyncEnabled)
            {
                DrawSmallButton(mouse, x + 84, y + 40, 78, 16, "DEV SYNC", Palette.ClearanceTeal);
            }
            DrawSmallButton(mouse, x + 168, y + 40, 72, 16, "FACTION >");

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

            DrawSmallButton(mouse, x + 6, y + 144, 58, 16, "< PREV");
            string classCaption = $"{ShortClass(selectedClass)} ({_manualClassIndex + 1}/{allowedClasses.Count})";
            int capW = Raylib.MeasureText(classCaption, 10);
            Raylib.DrawText(classCaption, x + (w - capW) / 2, y + 147, 10, Palette.ClearanceTeal);
            DrawSmallButton(mouse, x + 182, y + 144, 58, 16, "NEXT >");

            string allowedList = string.Join("/", allowedClasses.Select(ShortClass));
            string subNote = faction == ShipFaction.FreeTraders
                ? $"CLASSES: {allowedList} (SHARED FTV/HBR)"
                : $"ALLOWED CLASSES: {allowedList}";
            Raylib.DrawText(TruncateByPixelWidth(subNote, 10, w - 16), x + 8, y + 163, 10, Palette.CrtAmberDimmed);

            Raylib.DrawLine(x + 6, y + 176, x + w - 6, y + 176, Palette.WeatheredGunmetalEdge);
            DrawSubspaceRelayWidget(mouse, item);
        }
        else
        {
            Raylib.DrawText("SECTOR WARP SIGNATURE GUIDE:", x + 8, y + 42, 10, Palette.CrtAmberPrimary);
            (string Key, string Label)[] systems =
            [
                ("Veridia", "1. VERIDIA"),
                ("Korr-Vath", "2. KORR-VATH"),
                ("Dul-Krag", "3. DUL-KRAG"),
                ("Gharok", "4. GHAROK"),
                ("Oruun-4", "5. ORUUN-4"),
                ("Caldera Rim", "6. CALDERA RIM"),
                ("Solvane", "7. SOLVANE"),
                ("The Ashen Rift", "8. ASHEN RIFT*")
            ];
            for (int i = 0; i < systems.Length; i++)
            {
                int rowY = y + 58 + i * 20;
                bool isRestricted = (i == 7);
                Raylib.DrawText(systems[i].Label, x + 8, rowY + 3, 10,
                    isRestricted ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
                DrawAnalogWaveform(systems[i].Key, x + 128, rowY, 110, 16, 110,
                    isRestricted ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
            }
            Raylib.DrawText(
                IsSpectrometerOperational ? "SPECTROMETER: ONLINE (DAY 3+)" : "SPECTROMETER OFFLINE (DAYS 1-2)",
                x + 8, y + 221, 10,
                IsSpectrometerOperational ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        }
    }

    private void DrawSubspaceRelayWidget(VirtualMouseState mouse, DraggableDeskItem manual)
    {
        GetRelayUiRects(manual, out Rectangle pingBtnRect, out Rectangle readoutRect);

        bool canPing = CanStartRelayPing();
        string btnLabel = _relayPhase switch
        {
            RelayQueryPhase.TypingQuery => "PINGING...",
            RelayQueryPhase.Completed => "RELAY DONE",
            _ => "PING RELAY"
        };

        DrawHardwareButton(
            mouse,
            (int)pingBtnRect.X, (int)pingBtnRect.Y, (int)pingBtnRect.Width, (int)pingBtnRect.Height,
            btnLabel,
            enabled: canPing,
            highlight: canPing,
            activeColor: Palette.ClearanceTeal);

        bool isAlert = IsActiveRelayAlert();
        bool alertFlash = ((_animTick / 10) % 2) == 0;

        string sideHint = _relayPhase switch
        {
            RelayQueryPhase.TypingQuery => "TX SUBSPACE RELAY...",
            RelayQueryPhase.Completed when isAlert => _relayAlertConfronted ? "ALERT VERIFIED" : "[!] CLICK ALERT BELOW",
            RelayQueryPhase.Completed => "RELAY LOGGED",
            _ => _phase == EncounterPhase.InspectingSlateAndShip ? "FACTION RELAY READY" : "AWAITING DATA SLATE"
        };
        Color sideCol = isAlert
            ? (_relayAlertConfronted ? Palette.ClearanceTeal : (alertFlash ? Palette.AlertCrimson : Palette.CrtAmberPrimary))
            : (_relayPhase == RelayQueryPhase.TypingQuery ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        Raylib.DrawText(
            TruncateByPixelWidth(sideHint, 10, manual.Width - (int)pingBtnRect.Width - 20),
            (int)pingBtnRect.X + (int)pingBtnRect.Width + 6,
            (int)pingBtnRect.Y + ((int)pingBtnRect.Height - 10) / 2,
            10,
            sideCol);

        int rx = (int)readoutRect.X;
        int ry = (int)readoutRect.Y;
        int rw = (int)readoutRect.Width;
        int rh = (int)readoutRect.Height;
        bool alertHovered = isAlert && mouse.IsHit(readoutRect);

        Color boxBg = alertHovered ? Palette.BrownDwarfGlowDeep : Palette.DeepSpaceVoid;
        Color boxBorder = isAlert
            ? (_relayAlertConfronted ? Palette.ClearanceTeal : (alertHovered || alertFlash ? Palette.AlertCrimson : Palette.CrtAmberPrimary))
            : (_relayPhase == RelayQueryPhase.TypingQuery ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(rx, ry, rw, rh, boxBg);
        Raylib.DrawRectangleLines(rx, ry, rw, rh, boxBorder);

        if (_protocolReviewModeActive && isAlert)
        {
            DrawReviewHotspotOutline(mouse, rx, ry, rw, rh, _reviewFirstSelection == ReviewTarget.RelayAlertLog);
        }

        int textMaxPx = rw - 10;
        if (_relayPhase == RelayQueryPhase.Idle)
        {
            string idleMsg = _phase == EncounterPhase.InspectingSlateAndShip
                ? "READY: CLICK [PING RELAY] TO VERIFY REG"
                : "SUBSPACE RELAY STANDBY (LOAD SLATE)";
            Raylib.DrawText(TruncateByPixelWidth(idleMsg, 10, textMaxPx), rx + 5, ry + 5, 10, Palette.WeatheredGunmetalEdge);
        }
        else if (_relayPhase == RelayQueryPhase.TypingQuery)
        {
            int count = Math.Clamp(_relayLastTypedChars, 0, _relayQueryFullText.Length);
            string typed = _relayQueryFullText[..count] + (((_animTick / 8) % 2) == 0 ? "_" : "");
            Raylib.DrawText(TruncateByPixelWidth(typed, 10, textMaxPx), rx + 5, ry + 5, 10, Palette.ClearanceTeal);
            if (rh >= 28)
            {
                Raylib.DrawText("AWAITING FACTION RELAY REPLY...", rx + 5, ry + 18, 10, Palette.CrtAmberDimmed);
            }
        }
        else
        {
            string resLine = GetRelayResultDisplayLine();
            Color resCol = isAlert
                ? (_relayAlertConfronted ? Palette.ClearanceTeal : Palette.AlertCrimson)
                : (_relayTargetWasUnregistered
                    ? Palette.AlertCrimson
                    : (_relayResultStatus == RelayPingStatus.ActiveClear ? Palette.ClearanceTeal : Palette.CrtAmberDimmed));
            Raylib.DrawText(TruncateByPixelWidth(resLine, 10, textMaxPx), rx + 5, ry + 5, 10, resCol);

            if (rh >= 28)
            {
                string subLine = isAlert
                    ? (_relayAlertConfronted ? "CONFRONTATION LOGGED -- DETAIN AUTHORIZED" : ">> CLICK THIS ALERT TO CONFRONT CAPTAIN <<")
                    : (_relayResultStatus == RelayPingStatus.NoResponseInterference && !_relayTargetWasUnregistered
                        ? "RIM INTERFERENCE -- NOT A VIOLATION ALONE"
                        : (_relayTargetWasUnregistered ? "NO OFFICIAL PREFIX ON FILE (UNR-)" : "REGISTRY RECORD MATCHES ACTIVE VESSEL"));
                Color subCol = isAlert
                    ? (_relayAlertConfronted ? Palette.ClearanceTeal : Palette.CrtAmberPrimary)
                    : Palette.CrtAmberDimmed;
                Raylib.DrawText(TruncateByPixelWidth(subLine, 10, textMaxPx), rx + 5, ry + 18, 10, subCol);
            }
        }
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

    // =========================================================================
    // Error-Only Flashing & Sliding Instant Amber Citation Strip
    // =========================================================================
    private void DrawErrorOnlyCitationStrip()
    {
        if (_activeCitationStrip == null)
            return;

        const int cx = 180;
        const int targetY = 152;
        const int startY = 260;
        const int cw = 600;
        const int ch = 74;

        int cy = startY + (int)MathF.Round((targetY - startY) * _citationSlideProgress);
        bool flashBright = ((_animTick / 8) % 2) == 0;

        Color frameBorder = _activeCitationStrip.IsSecurityStrike
            ? (flashBright ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
            : (flashBright ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);

        Raylib.DrawRectangle(cx, cy, cw, ch, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(cx, cy, cw, ch, frameBorder);
        Raylib.DrawRectangleLines(cx + 2, cy + 2, cw - 4, ch - 4, Palette.OxidizedBronzeWorn);

        Raylib.DrawRectangle(cx + 3, cy + 3, cw - 6, 16,
            _activeCitationStrip.IsSecurityStrike ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
        Raylib.DrawText(_activeCitationStrip.HeaderText, cx + 10, cy + 6, 10, Palette.OutlineVoid);

        Raylib.DrawText(Truncate(_activeCitationStrip.ViolationReason, 94), cx + 10, cy + 25, 10, Palette.CrtAmberPrimary);
        Raylib.DrawText(Truncate(_activeCitationStrip.PenaltyText, 94), cx + 10, cy + 41, 10,
            _activeCitationStrip.IsSecurityStrike ? Palette.AlertCrimson : Palette.ClearanceTeal);
        Raylib.DrawText("[CLICK CITATION STRIP TO DISMISS]", cx + 10, cy + 57, 10, Palette.CrtAmberDimmed);
    }

    // =========================================================================
    // Day 3 Morning Shift Directive Modal (Variant A & Variant B)
    // =========================================================================
    private void DrawMorningShiftDirectiveModal(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(0, 24, Constants.VirtualWidth, Constants.VirtualHeight - 24, new Color(8, 11, 16, 215));

        const int mx = 140;
        const int my = 48;
        const int mw = 680;
        const int mh = 446;

        Raylib.DrawRectangle(mx, my, mw, mh, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(mx, my, mw, mh, Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(mx + 3, my + 3, mw - 6, mh - 6, Palette.OxidizedBronzeWorn);

        Raylib.DrawRectangle(mx + 4, my + 4, mw - 8, 18, Palette.CrtAmberPrimary);
        string variantTag = _gameState.Day == 3
            ? (_gameState.Day2SuspectDetained ? " [VARIANT B: KDF-SEC-09B]" : " [VARIANT A: KDF-SEC-09]")
            : string.Empty;
        Raylib.DrawText($"PACT PORT AUTHORITY // MORNING SHIFT DIRECTIVE{variantTag}", mx + 12, my + 8, 10, Palette.OutlineVoid);

        int cursorY = my + 32;
        foreach (string rawLine in _activeDirectiveLines)
        {
            if (string.IsNullOrEmpty(rawLine))
            {
                cursorY += 8;
                continue;
            }

            Color lineCol = rawLine.StartsWith("INCIDENT") || rawLine.StartsWith("ADMINISTRATIVE") || rawLine.StartsWith("NEW MANDATORY")
                ? Palette.ClearanceTeal
                : (rawLine.StartsWith("NOTE:") ? Palette.AlertCrimson : Palette.CrtAmberPrimary);

            foreach (string wrapped in WrapSingleParagraph(rawLine, 104))
            {
                if (cursorY > my + mh - 50)
                    break;
                Raylib.DrawText(wrapped, mx + 16, cursorY, 10, lineCol);
                cursorY += 14;
            }
        }

        string btnLabel = (!_directiveOpenedMidShift && _gameState.Day == 3 && _gameState.Day2SuspectDetained)
            ? "ACKNOWLEDGE DIRECTIVE (INCOMING HAIL)"
            : "ACKNOWLEDGE & CLOSE DIRECTIVE";
        DrawHardwareButton(mouse, 340, 456, 280, 26, btnLabel, enabled: true, highlight: true, Palette.ClearanceTeal);
    }

    // =========================================================================
    // Day 3 Pre-Shift Kordani Viewscreen Hail Overlay (Day2SuspectDetained == true)
    // =========================================================================
    private void DrawDay3PreShiftKordaniHailOverlay(VirtualMouseState mouse)
    {
        const int bx = 176;
        const int by = 294;
        const int bw = 608;
        const int bh = 190;

        Raylib.DrawRectangle(bx, by, bw, bh, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(bx, by, bw, bh, Palette.AlertCrimson);
        Raylib.DrawRectangleLines(bx + 2, by + 2, bw - 4, bh - 4, Palette.OxidizedBronzeWorn);

        Raylib.DrawRectangle(bx + 3, by + 3, bw - 6, 18, Palette.BrownDwarfGlowDeep);
        Raylib.DrawText($"PRIORITY INTERNAL COMMS // {Day2And3ScriptedDialogue.Day3KordaniOfficerName}", bx + 12, by + 7, 10, Palette.CrtAmberPrimary);

        int drawY = by + 30;
        foreach (string speechLine in Day2And3ScriptedDialogue.Day3PreShiftKordaniHailLines)
        {
            foreach (string wrapped in WrapSingleParagraph($"\"{speechLine}\"", 94))
            {
                Raylib.DrawText(wrapped, bx + 14, drawY, 10, Palette.CrtAmberPrimary);
                drawY += 14;
            }
            drawY += 6;
        }

        DrawHardwareButton(
            mouse, 340, 444, 280, 26,
            "ACKNOWLEDGE / CLOSE INTERNAL HAIL",
            enabled: true,
            highlight: true,
            activeColor: Palette.ClearanceTeal);
    }

    // =========================================================================
    // Analog Pixel-Art Warp Field Residue Waveform Renderer (8 Sector Star Systems)
    // =========================================================================
    private static void DrawAnalogWaveform(
        string originSystem,
        int x,
        int y,
        int width,
        int height,
        int visibleWidth,
        Color waveColor)
    {
        int midY = y + height / 2;
        int amp = Math.Max(3, (height / 2) - 2);
        int drawW = Math.Clamp(visibleWidth, 0, width);
        if (drawW <= 1)
            return;

        Raylib.DrawLine(x, midY, x + drawW, midY, Palette.WeatheredGunmetalEdge);

        int prevY = midY;
        for (int i = 0; i < drawW; i++)
        {
            float sample = SampleSystemWaveform(originSystem, i, width);
            int curY = Math.Clamp(midY - (int)MathF.Round(sample * amp), y + 1, y + height - 2);
            if (i > 0)
            {
                Raylib.DrawLine(x + i - 1, prevY, x + i, curY, waveColor);
            }
            prevY = curY;
        }
    }

    private static float SampleSystemWaveform(string system, int px, int totalWidth)
    {
        float period = Math.Max(14f, totalWidth / 4.0f);
        float phase = (px % period) / period;

        return system.ToLowerInvariant() switch
        {
            "veridia" => MathF.Sin(phase * MathF.PI * 2.0f),
            "korr-vath" => 1.0f - 2.0f * phase,
            "dul-krag" => phase < 0.5f ? 0.9f : -0.9f,
            "gharok" => phase < 0.25f
                ? (phase * 4.0f)
                : (phase < 0.75f ? 1.0f - (phase - 0.25f) * 4.0f : -1.0f + (phase - 0.75f) * 4.0f),
            "oruun-4" => phase < 0.22f
                ? MathF.Sin((phase / 0.22f) * MathF.PI)
                : (phase is >= 0.30f and < 0.52f ? MathF.Sin(((phase - 0.30f) / 0.22f) * MathF.PI) : -0.35f),
            "caldera rim" => phase < 0.45f
                ? MathF.Sin((phase / 0.45f) * MathF.PI) * 1.0f
                : (phase < 0.90f ? MathF.Sin(((phase - 0.45f) / 0.45f) * MathF.PI) * 0.45f : -0.25f),
            "solvane" => phase < 0.65f
                ? MathF.Sqrt(Math.Max(0f, 1.0f - MathF.Pow((phase - 0.325f) / 0.325f, 2.0f))) * 1.15f - 0.3f
                : -0.3f,
            _ => HashIntToSignedFloat(px * 13 + 7)
        };
    }

    // =========================================================================
    // UI Helpers
    // =========================================================================
    private void AddTranscriptLine(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _transcriptLines.Add((text, color));
        if (_transcriptLines.Count > 60)
        {
            _transcriptLines.RemoveAt(0);
        }
        _transcriptAutoFollowBottom = true;
    }

    private List<(string Line, Color Col)> WrapTranscriptLines(int maxCharsPerLine)
    {
        List<(string Line, Color Col)> output = [];
        foreach (var (text, col) in _transcriptLines)
        {
            foreach (string line in WrapSingleParagraph(text, maxCharsPerLine))
            {
                output.Add((line, col));
            }
        }
        return output;
    }

    private static List<string> WrapSingleParagraph(string text, int maxCharsPerLine)
    {
        List<string> lines = [];
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;
        foreach (string word in words)
        {
            if (current.Length == 0)
            {
                current = word;
            }
            else if (current.Length + 1 + word.Length <= maxCharsPerLine)
            {
                current += " " + word;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }
        if (current.Length > 0)
        {
            lines.Add(current);
        }
        return lines;
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

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            return value;
        return value[..maxChars];
    }

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
                lines.Add(current);
                if (lines.Count >= maxLines)
                    return lines;
                current = word;
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
        Raylib.DrawText(label, x + 6, y + (h - 10) / 2, 10, textCol);
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
        Raylib.DrawText(label, x + 6, y + (h - 10) / 2, 10, textOverride ?? Palette.CrtAmberPrimary);
    }
}
