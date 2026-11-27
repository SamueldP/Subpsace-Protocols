using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

/// <summary>
/// Solo Officer Quarters Room View (1280x720, strict 8px grid, NO movable player sprite,
/// NO family management, NO Air Scrubber, pure Pact Scrip SC economy).
/// Features Booth 04-matched crimson/red-brown banded Brown Dwarf windows with soft directional wash,
/// clean clutter-free furniture with hover-only tooltips for the 5 interactive hotspots
/// (Door, Service Chute, Personal Terminal, Vent Stash, Bed), visual-only Food Replicator,
/// dead unclickable right-wall appliance, bottom Coat Pocket tray synced with Booth 04's Lap Pocket,
/// centered 5-Tab Terminal overlay, and Two-Tier Sleep-Triggered Kordani Berth Audits via RaidController.
/// </summary>
public sealed class QuartersScreen : IScreen
{
    // Top Status Bar & Pause Button
    private const int TopBarH = 32;
    private const int PauseBtnX = 1236;
    private const int PauseBtnY = 4;
    private const int PauseBtnW = 36;
    private const int PauseBtnH = 24;

    // 3 Back-Wall Windows (y = 88..216, h = 128)
    private const int WinY = 88;
    private const int WinW = 224;
    private const int WinH = 128;
    private const int LeftWinX = 224;
    private const int CenterWinX = 528;
    private const int RightWinX = 832;

    // Corner Stellar Stoker Amber Strips
    private const int LeftStokerX = 160;
    private const int RightStokerX = 1104;
    private const int StokerY = 88;
    private const int StokerW = 16;
    private const int StokerH = 448;

    // 5 Interactive Room Hotspots + 2 Non-Interactive Visual Props
    private const int DoorX = 16;
    private const int DoorY = 360;
    private const int DoorW = 128;
    private const int DoorH = 280;

    private const int ChuteX = 40;
    private const int ChuteY = 240;
    private const int ChuteW = 88;
    private const int ChuteH = 96;

    private const int DeskX = 216;
    private const int DeskY = 336;
    private const int DeskW = 248;
    private const int DeskH = 136;

    private const int UnreadLedX = 356;
    private const int UnreadLedY = 396;
    private const int UnreadLedSize = 12;

    private const int VentX = 256;
    private const int VentY = 480;
    private const int VentW = 136;
    private const int VentH = 72;

    // Visual-only Food Replicator (no click/hover clutter)
    private const int ReplicatorX = 560;
    private const int ReplicatorY = 352;
    private const int ReplicatorW = 160;
    private const int ReplicatorH = 144;

    private const int BedX = 776;
    private const int BedY = 400;
    private const int BedW = 296;
    private const int BedH = 168;

    // Glowing right-wall Interactive Sector Star Chart display unit (strict 8px grid, mounted directly above Broken Appliance)
    public const int StarChartX = 1136;
    public const int StarChartY = 176;
    public const int StarChartW = 120;
    public const int StarChartH = 144;

    // Visibly dead & unclickable right-wall appliance
    private const int BrokenAppX = 1136;
    private const int BrokenAppY = 344;
    private const int BrokenAppW = 120;
    private const int BrokenAppH = 152;

    // Bottom Coat Pocket Strip (synced with Booth 04 Lap Pocket, 1->3 slots, 8px bottom margin)
    private const int CoatTrayX = 344;
    private const int CoatTrayY = 648;
    private const int CoatTrayW = 592;
    private const int CoatTrayH = 64;
    private const int CoatSlotStartX = 480;
    private const int CoatSlotY = 656;
    private const int CoatSlotW = 144;
    private const int CoatSlotH = 48;
    private const int CoatSlotGap = 8;

    // Compact Popups (Service Chute Pickup Tray, Vent Stash Transfer Drawer, Kordani Audit Raid)
    private const int ChuteTrayX = 296;
    private const int ChuteTrayY = 168;
    private const int ChuteTrayW = 688;
    private const int ChuteTrayH = 384;

    private const int VentDrawerX = 240;
    private const int VentDrawerY = 160;
    private const int VentDrawerW = 800;
    private const int VentDrawerH = 400;

    private const int AuditModalX = 224;
    private const int AuditModalY = 104;
    private const int AuditModalW = 832;
    private const int AuditModalH = 512;

    private readonly ScreenManager _screenManager;
    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly DevDebugState? _devDebug;
    private readonly EconomyEngine _economy;
    private readonly IntelEconomy _intelEconomy;
    private readonly RaidController _raidController;
    private readonly ShipGenerator _shipGenerator;
    private readonly WorldSim _worldSim;
    private readonly Action? _onRequestPause;

    private readonly TerminalOverlay _terminalOverlay;
    private readonly StarChartModal _starChartModal;
    private readonly FieldBookletWidget _quartersBooklet = new();

    private int _quartersBookletOpenX = 16;
    private int _quartersBookletOpenY = 388;
    private bool _draggingQuartersBooklet;
    private int _bookletDragOffsetX;
    private int _bookletDragOffsetY;
    private int _bookletDragStartX;
    private int _bookletDragStartY;

    internal FieldBookletWidget Booklet => _quartersBooklet;
    internal bool IsBookletOpen => _quartersBooklet.IsOpen;
    internal string? ActiveHoverTooltipText { get; private set; }
    internal bool IsDay1EveningRationCueActive =>
        _gameState.Day == 1 &&
        _gameState.QuartersPhase == QuartersPhase.Evening &&
        _gameState.TonightRationOrder == NightlyRationOrder.None;

    public const float SleepCloseDurationSeconds = 0.80f;
    public const float SleepDarkHoldDurationSeconds = 1.40f;
    public const float SleepOpenDurationSeconds = 0.80f;
    public const float SleepTransitionDurationSeconds =
        SleepCloseDurationSeconds + SleepDarkHoldDurationSeconds + SleepOpenDurationSeconds; // 3.00s

    private float _elapsedTime;
    private bool _chuteTrayOpen;
    private bool _ventDrawerOpen;
    private int _chuteScroll;
    private int _ventPocketScroll;
    private int _ventStashScroll;
    private bool _isSleepTransitionActive;
    private float _sleepTransitionElapsed;
    private bool _sleepTransitionMidpointResolved;
    private bool _sleepAuditAlarmPlayed;
    private BerthAuditResolution? _activeAuditResolution;
    private bool _showingFailureScreen;

    // Brief diegetic room nudge toast
    private string _nudgeText = string.Empty;
    private Color _nudgeColor = Palette.CrtAmberPrimary;
    private float _nudgeTimer;

    public TerminalOverlay Terminal => _terminalOverlay;
    public StarChartModal StarChart => _starChartModal;
    public bool IsSleepTransitionActive => _isSleepTransitionActive;
    public float SleepTransitionElapsed => _sleepTransitionElapsed;
    public bool IsPhysicalTerminalUnreadLedActive => _gameState.UnreadMessageCount > 0;
    public string ActiveNudgeText => _nudgeText;
    public bool IsDoorGatedByDay1Email =>
        _gameState.Day == 1 &&
        _gameState.QuartersPhase == QuartersPhase.Morning &&
        !_gameState.Day1BrengopEmailRead;
    public bool IsDoorGatedByDay6UnreadEmail =>
        _gameState.Day == 6 &&
        _gameState.QuartersPhase == QuartersPhase.Morning &&
        !_gameState.TerminalHackedUnlocked &&
        _gameState.Emails.Any(e => e.IsTerminalHackEmail && !e.IsRead);
    public bool IsDoorGatedByDay6UncollectedChute =>
        _gameState.Day == 6 &&
        _gameState.QuartersPhase == QuartersPhase.Morning &&
        _gameState.ServiceChuteItems.Any(i => i.ItemId.StartsWith("d6_chute_blank", StringComparison.OrdinalIgnoreCase));
    public bool IsDoorGatedByDay6UnreadChuteDrop =>
        IsDoorGatedByDay6UnreadEmail;
    public bool IsAnySubOverlayOpen =>
        _terminalOverlay.IsOpen || _starChartModal.IsOpen || _chuteTrayOpen || _ventDrawerOpen || _activeAuditResolution != null || _showingFailureScreen || _isSleepTransitionActive;

    public QuartersScreen(
        ScreenManager screenManager,
        GameState gameState,
        Core.ConfigFlags config,
        Action? onRequestPause = null,
        Action? onFirstMorningQuarters = null,
        Action? onFirstEveningQuarters = null,
        DevDebugState? devDebug = null)
    {
        _screenManager = screenManager;
        _gameState = gameState;
        _config = config;
        _devDebug = devDebug;
        _onRequestPause = onRequestPause;
        _ = onFirstMorningQuarters;
        _ = onFirstEveningQuarters;

        _economy = new EconomyEngine();
        _intelEconomy = new IntelEconomy();
        _raidController = new RaidController();
        _shipGenerator = new ShipGenerator(_config);
        _worldSim = new WorldSim();

        _terminalOverlay = new TerminalOverlay(_gameState, _config, _economy, _intelEconomy, _worldSim);
        _starChartModal = new StarChartModal();
    }

    public void OnEnter()
    {
        _chuteTrayOpen = false;
        _ventDrawerOpen = false;
        _chuteScroll = 0;
        _ventPocketScroll = 0;
        _ventStashScroll = 0;
        _isSleepTransitionActive = false;
        _sleepTransitionElapsed = 0f;
        _sleepTransitionMidpointResolved = false;
        _sleepAuditAlarmPlayed = false;
        _activeAuditResolution = null;
        _showingFailureScreen = false;
        _quartersBooklet.IsOpen = false;
        _draggingQuartersBooklet = false;
        _quartersBookletOpenX = 16;
        _quartersBookletOpenY = 388;
        _terminalOverlay.Close();
        _starChartModal.Close(playAudio: false);
        _nudgeText = string.Empty;
        _nudgeTimer = 0f;

        if ((_gameState.EndingFlags.TryGetValue("Fail_DebtForeclosure", out bool debtFail) && debtFail) ||
            (_gameState.EndingFlags.TryGetValue("Fail_SecurityDetainment", out bool secFail) && secFail))
        {
            _showingFailureScreen = true;
            return;
        }

        // Guarantee Day 1 Brengop reassignment email is seeded on Day 1 Morning if unread,
        // and guarantee Day 2+ Morning Ship Roster exists in Morning Quarters (NEVER on Day 1 Morning!)
        if (_gameState.QuartersPhase == QuartersPhase.Morning)
        {
            if (_gameState.Day == 1 && !_gameState.Day1BrengopEmailRead)
            {
                _economy.EnsureDay1BrengopEmailSeeded(_gameState);
            }
            else if (_gameState.Day >= 2)
            {
                _economy.EnsureMorningRosterGenerated(_gameState, _shipGenerator, _config);
                if (_gameState.Day == 6)
                {
                    _economy.EnsureDay6MorningUnsolicitedDrop(_gameState);
                }
            }
        }
    }

    public void OnExit()
    {
        _quartersBooklet.IsOpen = false;
        _draggingQuartersBooklet = false;
        _terminalOverlay.Close();
        _starChartModal.Close(playAudio: false);
        _chuteTrayOpen = false;
        _ventDrawerOpen = false;
        _isSleepTransitionActive = false;
    }

    /// <summary>
    /// Allows ESC to close Terminal (or Star Chart modal, Encrypt modal, Chute tray, Vent drawer) cleanly before opening Pause Menu.
    /// </summary>
    public bool TryConsumeEscape()
    {
        if (_terminalOverlay.TryConsumeEscape())
            return true;

        if (_starChartModal.TryConsumeEscape())
            return true;

        if (_quartersBooklet.IsOpen)
        {
            _quartersBooklet.IsOpen = false;
            _draggingQuartersBooklet = false;
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        if (_chuteTrayOpen)
        {
            _chuteTrayOpen = false;
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        if (_ventDrawerOpen)
        {
            _ventDrawerOpen = false;
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        return false;
    }

    // Smoke test helpers for visual frame captures & interactive checks
    internal void SmokeStageTerminal(int tabIndex, bool selectFirstEmail = false)
    {
        _chuteTrayOpen = false;
        _ventDrawerOpen = false;
        _starChartModal.Close(playAudio: false);
        _isSleepTransitionActive = false;
        _activeAuditResolution = null;
        _showingFailureScreen = false;
        _terminalOverlay.SwitchTabForSmoke(tabIndex, selectFirstEmail);
    }

    internal void SmokeStageStarChart(string? selectedNodeId = "n11", string? selectedRouteId = null)
    {
        SmokeCloseSubOverlays();
        _starChartModal.Open();
        if (!string.IsNullOrEmpty(selectedRouteId))
        {
            _starChartModal.SelectRouteById(selectedRouteId, playAudio: false);
        }
        else if (!string.IsNullOrEmpty(selectedNodeId))
        {
            _starChartModal.SelectNodeById(selectedNodeId, playAudio: false);
        }
    }

    internal void SmokeTriggerEveningSleep()
    {
        SmokeCloseSubOverlays();
        TriggerEveningSleep();
    }

    internal void SmokeCloseSubOverlays()
    {
        _terminalOverlay.Close();
        _starChartModal.Close(playAudio: false);
        _chuteTrayOpen = false;
        _ventDrawerOpen = false;
        _chuteScroll = 0;
        _ventPocketScroll = 0;
        _ventStashScroll = 0;
        _isSleepTransitionActive = false;
        _sleepTransitionElapsed = 0f;
        _sleepTransitionMidpointResolved = false;
        _activeAuditResolution = null;
        _showingFailureScreen = false;
    }

    internal void SmokeStageBerthAudit(BerthAuditKind kind)
    {
        SmokeCloseSubOverlays();
        _activeAuditResolution = _raidController.ResolveBerthAudit(_gameState, _config, _worldSim, kind);
    }

    internal void SmokeVerifyCoatPocketAndVentTransfers()
    {
        SmokeCloseSubOverlays();
        _gameState.PocketCapacity = 2;
        _gameState.PocketItems.Clear();
        _gameState.VentStashItems.Clear();
        _gameState.ServiceChuteItems.Clear();

        _gameState.ServiceChuteItems.Add(new ContrabandPocketItem
        {
            ItemId = "smoke_chute_1",
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "BLANK DATA SLATE (UNREG)"
        });

        // 1. Click Service Chute hotspot -> opens Chute popup, then click item row -> moves to Coat Pocket
        Update(0.016f, new VirtualMouseState { X = ChuteX + 20, Y = ChuteY + 20, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (!_chuteTrayOpen)
            throw new InvalidOperationException("Clicking Service Chute did not open Chute pickup tray.");

        Update(0.016f, new VirtualMouseState { X = ChuteTrayX + 80, Y = ChuteTrayY + 80, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (_gameState.ServiceChuteItems.Count != 0 || _gameState.PocketItems.Count != 1)
            throw new InvalidOperationException("Clicking Service Chute item did not move it into the Coat Pocket.");
        _chuteTrayOpen = false;

        // 2. Click Coat Pocket slot 0 along bottom edge -> moves directly into Under-Desk Vent Stash
        Update(0.016f, new VirtualMouseState { X = CoatSlotStartX + 20, Y = CoatSlotY + 20, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (_gameState.PocketItems.Count != 0 || _gameState.VentStashItems.Count != 1)
            throw new InvalidOperationException("Clicking Coat Pocket item did not stash it into the Under-Desk Vent Stash.");

        // 3. Click Under-Desk Vent Stash hotspot -> opens Vent popup, then click Vent item row -> retrieves back into Coat Pocket
        Update(0.016f, new VirtualMouseState { X = VentX + 40, Y = VentY + 20, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (!_ventDrawerOpen)
            throw new InvalidOperationException("Clicking Vent Stash did not open Vent Stash popup.");

        Update(0.016f, new VirtualMouseState { X = VentDrawerX + 480, Y = VentDrawerY + 90, IsInsideViewport = true, LeftPressed = true, Scale = 1.0f });
        if (_gameState.VentStashItems.Count != 0 || _gameState.PocketItems.Count != 1)
            throw new InvalidOperationException("Clicking Vent Stash item did not retrieve it back into the Coat Pocket.");

        SmokeCloseSubOverlays();
    }

    private void ShowNudge(string text, Color? col = null)
    {
        _nudgeText = text;
        _nudgeColor = col ?? Palette.CrtAmberPrimary;
        _nudgeTimer = 3.8f;
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        _elapsedTime += deltaTime;
        if (_nudgeTimer > 0f)
        {
            _nudgeTimer = Math.Max(0f, _nudgeTimer - deltaTime);
            if (_nudgeTimer <= 0f)
                _nudgeText = string.Empty;
        }

        // Top Pause [||] button works at any time
        if (mouse.LeftPressed && mouse.IsHit(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH))
        {
            _onRequestPause?.Invoke();
            return;
        }

        if (_isSleepTransitionActive)
        {
            // Weighty 3.0s 3-stage eyelid shutter animation (scaled by AnimationSpeedMultiplier):
            // 1. Close Eyes (0.8s) -> 2. Dark Hold (1.4s, no instant-skip) -> 3. Open Eyes (0.8s)
            float speedMul = Math.Max(0.1f, _devDebug?.AnimationSpeedMultiplier ?? 1.0f);
            _sleepTransitionElapsed += deltaTime * speedMul;

            float holdMidpoint = SleepCloseDurationSeconds + SleepDarkHoldDurationSeconds * 0.50f;
            float openStart = SleepCloseDurationSeconds + SleepDarkHoldDurationSeconds;

            if (!_sleepTransitionMidpointResolved && _sleepTransitionElapsed >= holdMidpoint)
            {
                _sleepTransitionMidpointResolved = true;
                BerthAuditKind auditKind = _raidController.CheckPendingSleepAudit(_gameState, _config);
                if (auditKind != BerthAuditKind.None)
                {
                    // Resolve raid during quiet dark hold so eyelids open directly into the triggered Kordani Berth Audit
                    _activeAuditResolution = _raidController.ResolveBerthAudit(_gameState, _config, _worldSim, auditKind);
                }
                else
                {
                    _economy.CompleteSleepToMorning(_gameState, _config, _shipGenerator);
                    _terminalOverlay.RefreshForumThreads();
                }
            }

            if (_activeAuditResolution != null && !_sleepAuditAlarmPlayed && _sleepTransitionElapsed >= openStart)
            {
                _sleepAuditAlarmPlayed = true;
                AudioHooks.Play("detain_alarm");
            }

            if (_sleepTransitionElapsed >= SleepTransitionDurationSeconds)
            {
                _isSleepTransitionActive = false;
                if (_activeAuditResolution == null)
                {
                    ShowNudge($"MORNING CYCLE -- DAY {_gameState.Day:D2}. CHECK TERMINAL & 0500 SERVICE CHUTE.", Palette.ClearanceTeal);
                }
            }
            return;
        }

        if (_showingFailureScreen)
        {
            HandleFailureScreenInput(mouse);
            return;
        }

        if (_activeAuditResolution != null)
        {
            HandleAuditModalInput(mouse);
            return;
        }

        if (_terminalOverlay.IsOpen)
        {
            _terminalOverlay.Update(deltaTime, mouse);
            return;
        }

        if (_starChartModal.IsOpen)
        {
            _starChartModal.Update(deltaTime, mouse);
            return;
        }

        if (_chuteTrayOpen)
        {
            HandleChuteTrayInput(mouse);
            return;
        }

        if (_ventDrawerOpen)
        {
            HandleVentDrawerInput(mouse);
            return;
        }

        // Field Booklet ("884-D — MY NOTES") dragging & open/minimize handling in Quarters
        if (_draggingQuartersBooklet)
        {
            if (mouse.LeftDown)
            {
                _quartersBookletOpenX = Math.Clamp(mouse.X - _bookletDragOffsetX, 8, Constants.VirtualWidth - FieldBookletWidget.OpenW - 8);
                _quartersBookletOpenY = Math.Clamp(mouse.Y - _bookletDragOffsetY, TopBarH + 8, Constants.VirtualHeight - FieldBookletWidget.OpenH - 8);
                return;
            }
            _draggingQuartersBooklet = false;
            return;
        }

        if (mouse.LeftPressed)
        {
            if (_quartersBooklet.IsOpen &&
                mouse.IsHit(_quartersBookletOpenX, _quartersBookletOpenY, FieldBookletWidget.OpenW, FieldBookletWidget.OpenH))
            {
                if (_quartersBooklet.HandleInternalClick(mouse, _quartersBookletOpenX, _quartersBookletOpenY, _gameState, _config))
                    return;

                _draggingQuartersBooklet = true;
                _bookletDragOffsetX = mouse.X - _quartersBookletOpenX;
                _bookletDragOffsetY = mouse.Y - _quartersBookletOpenY;
                _bookletDragStartX = mouse.X;
                _bookletDragStartY = mouse.Y;
                AudioHooks.Play("terminal_key_click");
                return;
            }

            if (!_quartersBooklet.IsOpen &&
                mouse.IsHit(16, 654, FieldBookletWidget.ClosedW, FieldBookletWidget.ClosedH))
            {
                _quartersBooklet.IsOpen = true;
                _quartersBooklet.SyncWithGameState(_gameState, _config, jumpToNewestOnNewUnlock: true);
                AudioHooks.Play("terminal_key_tab");
                return;
            }
        }

        if (!mouse.LeftPressed)
            return;

        // 0. Bottom Coat Pocket Strip: click a carried item to stash directly into Under-Desk Vent Stash
        if (TryHandleCoatPocketTrayClick(mouse))
            return;

        // 1. Left Wall -- Door (16, 360, 128, 280)
        if (mouse.IsHit(DoorX, DoorY, DoorW, DoorH))
        {
            if (_gameState.QuartersPhase == QuartersPhase.Morning)
            {
                if (IsDoorGatedByDay1Email || IsDoorGatedByDay6UnreadEmail)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge("Should probably read my emails first.", Palette.CrtAmberPrimary);
                    return;
                }

                if (IsDoorGatedByDay6UncollectedChute)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge("I see there is something in my chute, let me check it out", Palette.CrtAmberPrimary);
                    return;
                }

                AudioHooks.Play("bulkhead_door_lock");
                _screenManager.RouteViaCorridor(ScreenId.Desk);
            }
            else
            {
                AudioHooks.Play("citation_buzz");
                ShowNudge("SHIFT COMPLETE -- REST BEFORE NEXT WATCH", Palette.CrtAmberPrimary);
            }
            return;
        }

        // 2. Left Wall -- Service Chute (40, 240, 88, 96)
        if (mouse.IsHit(ChuteX, ChuteY, ChuteW, ChuteH))
        {
            _chuteTrayOpen = true;
            _chuteScroll = 0;
            AudioHooks.Play("door_viewport_shutter");
            return;
        }

        // 3. Back-Left -- Personal Terminal Desk (216, 336, 248, 136)
        if (mouse.IsHit(DeskX, DeskY, DeskW, DeskH))
        {
            _terminalOverlay.Open(initialTab: 0);
            return;
        }

        // 4. Under the Desk -- Contraband Vent Stash (256, 480, 136, 72)
        if (mouse.IsHit(VentX, VentY, VentW, VentH))
        {
            _ventDrawerOpen = true;
            _ventPocketScroll = 0;
            _ventStashScroll = 0;
            AudioHooks.Play("terminal_key_click");
            return;
        }

        // 5. Back-Right -- Bed (776, 400, 296, 168) -- Sleep Only
        if (mouse.IsHit(BedX, BedY, BedW, BedH))
        {
            if (_gameState.QuartersPhase == QuartersPhase.Evening)
            {
                TriggerEveningSleep();
            }
            else
            {
                AudioHooks.Play("terminal_key_click");
                ShowNudge("HEAD TO DOOR FOR WATCH", Palette.ClearanceTeal);
            }
            return;
        }

        // 6. Right Wall -- Glowing Interactive Sector Star Chart Display Unit (1136, 176, 120, 144)
        if (mouse.IsHit(StarChartX, StarChartY, StarChartW, StarChartH))
        {
            _starChartModal.Open();
            return;
        }

        // Note: Food Replicator (visual-only) and right-wall Broken Appliance (dead/unclickable) intentionally ignore clicks.
    }

    private bool TryHandleCoatPocketTrayClick(VirtualMouseState mouse)
    {
        if (!mouse.IsHit(CoatTrayX, CoatTrayY, CoatTrayW, CoatTrayH))
            return false;

        int cap = Math.Clamp(_gameState.PocketCapacity, 1, 3);
        for (int i = 0; i < cap && i < _gameState.PocketItems.Count; i++)
        {
            int sx = CoatSlotStartX + i * (CoatSlotW + CoatSlotGap);
            if (!mouse.IsHit(sx, CoatSlotY, CoatSlotW, CoatSlotH))
                continue;

            ContrabandPocketItem item = _gameState.PocketItems[i];

            // Clicking a carried contraband item in the Coat Pocket stashes it directly into Under-Desk Vent Stash
            if (_gameState.VentStashItems.Count >= _config.VentStashCapacity)
            {
                AudioHooks.Play("citation_buzz");
                ShowNudge($"UNDER-DESK VENT STASH FULL ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity}).", Palette.AlertCrimson);
                return true;
            }

            _gameState.VentStashItems.Add(item);
            _gameState.PocketItems.RemoveAt(i);
            _gameState.SaveToDisk();
            AudioHooks.Play("terminal_key_confirm");
            ShowNudge($"STASHED {item.DisplayName} INTO UNDER-DESK VENT ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity}).", Palette.CrtAmberPrimary);
            return true;
        }

        return true;
    }

    private void TriggerEveningSleep()
    {
        AudioHooks.Play("bed_sleep_transition");
        _isSleepTransitionActive = true;
        _sleepTransitionElapsed = 0f;
        _sleepTransitionMidpointResolved = false;
        _sleepAuditAlarmPlayed = false;
        _nudgeText = string.Empty;
        _nudgeTimer = 0f;
    }

    private void FinishSleepIntoMorning()
    {
        _economy.CompleteSleepToMorning(_gameState, _config, _shipGenerator);
        _terminalOverlay.RefreshForumThreads();
        _activeAuditResolution = null;
        ShowNudge($"MORNING CYCLE -- DAY {_gameState.Day:D2}. CHECK TERMINAL & 0500 SERVICE CHUTE.", Palette.ClearanceTeal);
    }

    // =========================================================================
    // SUB-OVERLAY INPUT HANDLERS
    // =========================================================================
    private void HandleChuteTrayInput(VirtualMouseState mouse)
    {
        const int visibleRows = 6;
        int maxScroll = Math.Max(0, _gameState.ServiceChuteItems.Count - visibleRows);
        _chuteScroll = Math.Clamp(_chuteScroll, 0, maxScroll);

        if (mouse.WheelMove != 0f && mouse.IsHit(ChuteTrayX, ChuteTrayY, ChuteTrayW, ChuteTrayH))
        {
            int delta = mouse.WheelMove > 0f ? -1 : 1;
            _chuteScroll = Math.Clamp(_chuteScroll + delta, 0, maxScroll);
        }

        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(ChuteTrayX + ChuteTrayW - 36, ChuteTrayY + 8, 28, 24) ||
            mouse.IsHit(ChuteTrayX + ChuteTrayW - 156, ChuteTrayY + ChuteTrayH - 44, 140, 32))
        {
            _chuteTrayOpen = false;
            AudioHooks.Play("door_viewport_shutter");
            return;
        }

        int sbX = ChuteTrayX + ChuteTrayW - 24;
        if (maxScroll > 0)
        {
            if (mouse.IsHit(sbX, ChuteTrayY + 66, 14, 18))
            {
                _chuteScroll = Math.Max(0, _chuteScroll - 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
            if (mouse.IsHit(sbX, ChuteTrayY + 66 + visibleRows * 44 - 24, 14, 18))
            {
                _chuteScroll = Math.Min(maxScroll, _chuteScroll + 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int rowW = maxScroll > 0 ? (ChuteTrayW - 44) : (ChuteTrayW - 32);
        for (int r = 0; r < visibleRows; r++)
        {
            int idx = _chuteScroll + r;
            if (idx >= _gameState.ServiceChuteItems.Count)
                break;

            ContrabandPocketItem item = _gameState.ServiceChuteItems[idx];
            int ry = ChuteTrayY + 66 + r * 44;

            // Secondary button checked FIRST: [STASH IN VENT]
            if (item.IsContraband && mouse.IsHit(ChuteTrayX + 16 + rowW - 144, ry + 4, 136, 30))
            {
                if (_gameState.VentStashItems.Count >= _config.VentStashCapacity)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge($"VENT STASH FULL ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity}).", Palette.AlertCrimson);
                    return;
                }
                _gameState.VentStashItems.Add(item);
                _gameState.ServiceChuteItems.RemoveAt(idx);
                _chuteScroll = Math.Clamp(_chuteScroll, 0, Math.Max(0, _gameState.ServiceChuteItems.Count - visibleRows));
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                ShowNudge($"STASHED {item.DisplayName} INTO UNDER-DESK VENT.", Palette.CrtAmberPrimary);
                return;
            }

            // Clicking anywhere on the item row (or [-> COAT POCKET] / [INSTALL LINING]) moves item into Coat Pocket!
            if (mouse.IsHit(ChuteTrayX + 16, ry, rowW, 38))
            {
                if (item.ItemType == ContrabandPocketItem.TypeShieldedPocketLining)
                {
                    _gameState.HasShieldedPocketLining = true;
                    _gameState.ShieldedLiningOrdered = false;
                    _gameState.ServiceChuteItems.RemoveAt(idx);
                    _chuteScroll = Math.Clamp(_chuteScroll, 0, Math.Max(0, _gameState.ServiceChuteItems.Count - visibleRows));
                    _gameState.SaveToDisk();
                    AudioHooks.Play("terminal_key_confirm");
                    ShowNudge("SHIELDED POCKET LINING INSTALLED IN OFFICER COAT!", Palette.ClearanceTeal);
                    return;
                }

                if (_gameState.PocketItems.Count >= _gameState.PocketCapacity)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge($"COAT POCKET FULL ({_gameState.PocketItems.Count}/{_gameState.PocketCapacity}) -- STASH IN VENT FIRST.", Palette.AlertCrimson);
                    return;
                }

                _gameState.PocketItems.Add(item);
                _gameState.ServiceChuteItems.RemoveAt(idx);
                _chuteScroll = Math.Clamp(_chuteScroll, 0, Math.Max(0, _gameState.ServiceChuteItems.Count - visibleRows));
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                ShowNudge($"MOVED {item.DisplayName} INTO COAT POCKET.", Palette.ClearanceTeal);
                return;
            }
        }
    }

    private void HandleVentDrawerInput(VirtualMouseState mouse)
    {
        const int visibleRows = 5;
        int maxPocketScroll = Math.Max(0, _gameState.PocketItems.Count - visibleRows);
        int maxVentScroll = Math.Max(0, _gameState.VentStashItems.Count - visibleRows);
        _ventPocketScroll = Math.Clamp(_ventPocketScroll, 0, maxPocketScroll);
        _ventStashScroll = Math.Clamp(_ventStashScroll, 0, maxVentScroll);

        if (mouse.WheelMove != 0f)
        {
            int delta = mouse.WheelMove > 0f ? -1 : 1;
            if (mouse.IsHit(VentDrawerX + 16, VentDrawerY + 68, 372, 268))
            {
                _ventPocketScroll = Math.Clamp(_ventPocketScroll + delta, 0, maxPocketScroll);
            }
            else if (mouse.IsHit(VentDrawerX + 412, VentDrawerY + 68, 372, 268))
            {
                _ventStashScroll = Math.Clamp(_ventStashScroll + delta, 0, maxVentScroll);
            }
        }

        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(VentDrawerX + VentDrawerW - 36, VentDrawerY + 8, 28, 24) ||
            mouse.IsHit(VentDrawerX + VentDrawerW - 156, VentDrawerY + VentDrawerH - 44, 140, 32))
        {
            _ventDrawerOpen = false;
            AudioHooks.Play("terminal_key_click");
            return;
        }

        // Scrollbar buttons for Left Column (Coat Pocket)
        if (maxPocketScroll > 0)
        {
            int sbLeftX = VentDrawerX + 16 + 372 - 16;
            if (mouse.IsHit(sbLeftX, VentDrawerY + 72, 14, 18))
            {
                _ventPocketScroll = Math.Max(0, _ventPocketScroll - 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
            if (mouse.IsHit(sbLeftX, VentDrawerY + 68 + 268 - 22, 14, 18))
            {
                _ventPocketScroll = Math.Min(maxPocketScroll, _ventPocketScroll + 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        // Scrollbar buttons for Right Column (Vent Stash)
        if (maxVentScroll > 0)
        {
            int sbRightX = VentDrawerX + 412 + 372 - 16;
            if (mouse.IsHit(sbRightX, VentDrawerY + 72, 14, 18))
            {
                _ventStashScroll = Math.Max(0, _ventStashScroll - 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
            if (mouse.IsHit(sbRightX, VentDrawerY + 68 + 268 - 22, 14, 18))
            {
                _ventStashScroll = Math.Min(maxVentScroll, _ventStashScroll + 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int leftRowW = maxPocketScroll > 0 ? 344 : 360;
        // Left Column: Clicking a Coat Pocket item stashes it into Under-Desk Vent Stash
        for (int r = 0; r < visibleRows; r++)
        {
            int idx = _ventPocketScroll + r;
            if (idx >= _gameState.PocketItems.Count)
                break;

            ContrabandPocketItem item = _gameState.PocketItems[idx];
            int ry = VentDrawerY + 76 + r * 48;
            if (mouse.IsHit(VentDrawerX + 22, ry, leftRowW, 42))
            {
                if (_gameState.VentStashItems.Count >= _config.VentStashCapacity)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge($"VENT STASH FULL ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity}).", Palette.AlertCrimson);
                    return;
                }
                _gameState.VentStashItems.Add(item);
                _gameState.PocketItems.RemoveAt(idx);
                _ventPocketScroll = Math.Clamp(_ventPocketScroll, 0, Math.Max(0, _gameState.PocketItems.Count - visibleRows));
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return;
            }
        }

        int rightRowW = maxVentScroll > 0 ? 344 : 360;
        // Right Column: Clicking a Vent Stash item retrieves it back into the Coat Pocket
        for (int r = 0; r < visibleRows; r++)
        {
            int idx = _ventStashScroll + r;
            if (idx >= _gameState.VentStashItems.Count)
                break;

            ContrabandPocketItem item = _gameState.VentStashItems[idx];
            int ry = VentDrawerY + 76 + r * 48;
            if (mouse.IsHit(VentDrawerX + 418, ry, rightRowW, 42))
            {
                if (_gameState.PocketItems.Count >= _gameState.PocketCapacity)
                {
                    AudioHooks.Play("citation_buzz");
                    ShowNudge($"COAT POCKET FULL ({_gameState.PocketItems.Count}/{_gameState.PocketCapacity}).", Palette.AlertCrimson);
                    return;
                }
                _gameState.PocketItems.Add(item);
                _gameState.VentStashItems.RemoveAt(idx);
                _ventStashScroll = Math.Clamp(_ventStashScroll, 0, Math.Max(0, _gameState.VentStashItems.Count - visibleRows));
                _gameState.SaveToDisk();
                AudioHooks.Play("terminal_key_confirm");
                return;
            }
        }
    }

    private void HandleAuditModalInput(VirtualMouseState mouse)
    {
        if (_activeAuditResolution == null)
            return;

        if (!mouse.LeftPressed && !Raylib.IsKeyPressed(KeyboardKey.Enter) && !Raylib.IsKeyPressed(KeyboardKey.Space))
            return;

        bool btnHit = mouse.LeftPressed && mouse.IsHit(AuditModalX + (AuditModalW - 380) / 2, AuditModalY + AuditModalH - 58, 380, 42);
        if (!btnHit && !Raylib.IsKeyPressed(KeyboardKey.Enter) && !Raylib.IsKeyPressed(KeyboardKey.Space))
            return;

        AudioHooks.Play("terminal_key_confirm");
        if (_activeAuditResolution.WasCaught)
        {
            _activeAuditResolution = null;
            _showingFailureScreen = true;
        }
        else
        {
            FinishSleepIntoMorning();
        }
    }

    private void HandleFailureScreenInput(VirtualMouseState mouse)
    {
        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(460, 500, 360, 44))
        {
            AudioHooks.Play("terminal_key_confirm");
            _showingFailureScreen = false;
            _screenManager.SwitchTo(ScreenId.MainMenu);
        }
    }

    // =========================================================================
    // ROOM RENDERING (Crimson Brown Dwarf, Soft Directional Wash, Clean Hotspots)
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        ActiveHoverTooltipText = null;
        int bx = _quartersBooklet.IsOpen ? _quartersBookletOpenX : 16;
        int by = _quartersBooklet.IsOpen ? _quartersBookletOpenY : 654;
        if (!IsAnySubOverlayOpen)
        {
            _quartersBooklet.SyncWithGameState(_gameState, _config, jumpToNewestOnNewUnlock: true);
        }

        bool overBooklet = !IsAnySubOverlayOpen && _quartersBooklet.ContainsPoint(mouse.X, mouse.Y, bx, by);
        bool roomInteractive = !IsAnySubOverlayOpen && !overBooklet;
        VirtualMouseState roomMouse = roomInteractive
            ? mouse
            : mouse with { IsInsideViewport = false, LeftPressed = false };

        DrawRoomInterior(roomMouse);
        DrawCoatPocketTray(roomMouse);
        DrawTopCompactStatusStrip(mouse);

        // Concise hover-only tooltip for interactive hotspots & Coat Pocket slots
        if (roomInteractive)
        {
            DrawActiveHoverTooltip(roomMouse);
        }

        // Diegetic bottom-center nudge banner positioned cleanly ABOVE the Coat Pocket tray
        // (or Day 1 Evening ration order reminder until rations are ordered)
        string? bottomCaption = !string.IsNullOrEmpty(_nudgeText)
            ? _nudgeText
            : (IsDay1EveningRationCueActive
                ? "Long first shift. Better order rations on the terminal before sleeping."
                : null);
        Color bottomCol = !string.IsNullOrEmpty(_nudgeText) ? _nudgeColor : Palette.CrtAmberPrimary;

        if (!string.IsNullOrEmpty(bottomCaption) && !IsAnySubOverlayOpen)
        {
            int nw = Math.Min(860, UiText.MeasureWidth(bottomCaption, 10) + 40);
            int nx = (Constants.VirtualWidth - nw) / 2;
            const int ny = 604;
            Raylib.DrawRectangle(nx, ny, nw, 32, new Color(10, 14, 20, 240));
            Raylib.DrawRectangleLines(nx, ny, nw, 32, bottomCol);
            int tw = UiText.MeasureWidth(bottomCaption, 10);
            UiText.Draw(bottomCaption, nx + (nw - tw) / 2, ny + 11, 10, bottomCol);
        }

        // Physical "884-D — MY NOTES" Field Booklet (bottom-left minimized cover or unfolded draggable notes)
        if (!IsAnySubOverlayOpen)
        {
            _quartersBooklet.Draw(mouse, bx, by, _gameState, _config, isTopmost: _quartersBooklet.IsOpen);
        }

        if (_chuteTrayOpen)
        {
            DrawChutePickupTray(mouse);
        }

        if (_ventDrawerOpen)
        {
            DrawVentStashDrawer(mouse);
        }

        if (_terminalOverlay.IsOpen)
        {
            _terminalOverlay.DrawVirtual(mouse);
        }

        if (_starChartModal.IsOpen)
        {
            _starChartModal.DrawVirtual(mouse);
        }

        if (_activeAuditResolution != null)
        {
            DrawBerthAuditModal(mouse, _activeAuditResolution);
        }

        if (_showingFailureScreen)
        {
            DrawFailureEndingScreen(mouse);
        }

        if (_isSleepTransitionActive)
        {
            DrawSleepTransitionOverlay();
        }
    }

    private void DrawSleepTransitionOverlay()
    {
        // Weighty 3-stage eyelid shutter animation (~3.0s total):
        // Stage 1 (0.0..0.8s): Smooth top/bottom eyelid shutters close to full black.
        // Stage 2 (0.8..2.2s): Quiet complete darkness while next day / roster / forum generates.
        // Stage 3 (2.2..3.0s): Smooth eyelid shutters open into Morning Quarters (or Kordani Berth Audit).
        float t = Math.Clamp(_sleepTransitionElapsed, 0f, SleepTransitionDurationSeconds);
        float openStart = SleepCloseDurationSeconds + SleepDarkHoldDurationSeconds;

        float closedness;
        if (t < SleepCloseDurationSeconds)
        {
            float c = Math.Clamp(t / SleepCloseDurationSeconds, 0f, 1f);
            closedness = c * c * (3f - 2f * c);
        }
        else if (t <= openStart)
        {
            closedness = 1.0f;
        }
        else
        {
            float o = Math.Clamp((t - openStart) / SleepOpenDurationSeconds, 0f, 1f);
            float easeOut = o * o * (3f - 2f * o);
            closedness = 1.0f - easeOut;
        }

        if (closedness >= 0.999f)
        {
            // Quiet complete darkness during the 1.4s Dark Hold
            Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(4, 5, 8, 255));
            return;
        }

        int halfH = (int)MathF.Round((Constants.VirtualHeight * 0.5f) * closedness);
        int veilAlpha = Math.Clamp((int)MathF.Round(closedness * 210f), 0, 255);
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(4, 5, 8, veilAlpha));

        if (halfH > 0)
        {
            // Top eyelid shutter closing downward from y = 0
            Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, halfH, new Color(4, 5, 8, 255));
            Raylib.DrawRectangle(0, halfH, Constants.VirtualWidth, 6, new Color(6, 8, 12, 210));
            Raylib.DrawRectangle(0, halfH + 6, Constants.VirtualWidth, 4, new Color(8, 10, 14, 140));

            // Bottom eyelid shutter closing upward from y = 720
            int botY = Constants.VirtualHeight - halfH;
            Raylib.DrawRectangle(0, botY, Constants.VirtualWidth, halfH, new Color(4, 5, 8, 255));
            Raylib.DrawRectangle(0, botY - 6, Constants.VirtualWidth, 6, new Color(6, 8, 12, 210));
            Raylib.DrawRectangle(0, botY - 10, Constants.VirtualWidth, 4, new Color(8, 10, 14, 140));
        }
    }

    private void DrawTopCompactStatusStrip(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, TopBarH, Palette.OutlineVoid);
        Raylib.DrawLine(0, TopBarH - 1, Constants.VirtualWidth, TopBarH - 1, Palette.WeatheredGunmetalEdge);

        string phaseLabel = _gameState.QuartersPhase == QuartersPhase.Morning
            ? $"DAY {_gameState.Day:D2} // MORNING QUARTERS"
            : $"DAY {_gameState.Day:D2} // EVENING QUARTERS";

        Color phaseCol = _gameState.QuartersPhase == QuartersPhase.Morning
            ? Palette.ClearanceTeal
            : Palette.CrtAmberPrimary;

        UiText.Draw(phaseLabel, 12, 11, 10, phaseCol);

        // Pure Pact Scrip (SC) HUD -- ZERO Quarters Power (PU)
        int sx = Math.Max(248, 12 + UiText.MeasureWidth(phaseLabel, 10) + 20);
        string scripText = $"PACT SCRIP: {_gameState.PactScrip} SC";
        UiText.Draw(scripText, sx, 11, 10, Palette.CrtAmberPrimary);
        sx += Math.Max(168, UiText.MeasureWidth(scripText, 10) + 18);

        if (_gameState.SecurityStrikesActive || _gameState.Day >= _config.DayUnlockWarpScanner)
        {
            Color strkCol = _gameState.SecurityStrikes >= _config.MaxStrikesBeforeAudit
                ? Palette.AlertCrimson
                : (_gameState.SecurityStrikes > 0 ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
            string strikesText = $"SECURITY STRIKES: {_gameState.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}";
            UiText.Draw(strikesText, sx, 11, 10, strkCol);
            sx += Math.Max(188, UiText.MeasureWidth(strikesText, 10) + 18);
        }

        string pocketText = $"POCKET: {_gameState.PocketItems.Count}/{_gameState.PocketCapacity}";
        UiText.Draw(pocketText, sx, 11, 10, Palette.ClearanceTeal);
        sx += Math.Max(116, UiText.MeasureWidth(pocketText, 10) + 18);

        if (_gameState.HasShieldedPocketLining && sx < PauseBtnX - 24)
        {
            string shieldedText = UiText.TruncateToWidth("[SHIELDED]", 10, PauseBtnX - 8 - sx);
            UiText.Draw(shieldedText, sx, 11, 10, Palette.ClearanceTeal);
        }

        bool pHov = mouse.IsHit(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH);
        Raylib.DrawRectangle(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH, pHov ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(PauseBtnX, PauseBtnY, PauseBtnW, PauseBtnH, pHov ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);
        UiText.Draw("||", PauseBtnX + 13, PauseBtnY + 7, 10, Palette.CrtAmberPrimary);
    }

    private void DrawRoomInterior(VirtualMouseState mouse)
    {
        bool isMorning = _gameState.QuartersPhase == QuartersPhase.Morning;
        bool stokerFlicker = _gameState.IsStokerCornerFlickerActive;
        bool stokerBlinkDim = stokerFlicker && (((int)(_elapsedTime * 11f) % 9) is 1 or 2 or 6);

        // 1. Base Industrial Berth Shell (Dark weathered gunmetal walls & floor, 100% opaque RGB)
        Color backWallBase = new(21, 24, 30, 255);
        Color sideWallBase = new(17, 20, 25, 255);
        Color floorBase = new(14, 17, 22, 255);

        Raylib.DrawRectangle(0, TopBarH, Constants.VirtualWidth, Constants.VirtualHeight - TopBarH, sideWallBase);
        Raylib.DrawRectangle(160, 48, 960, 496, backWallBase);
        Raylib.DrawRectangle(0, 544, Constants.VirtualWidth, Constants.VirtualHeight - 544, floorBase);

        // 2. Subtle, Soft Reddish-Amber Directional Brown Dwarf Wash (100% opaque pre-blended RGB -- zero alpha artifacts, zero harsh beige block!)
        DrawBrownDwarfDirectionalWash(isMorning, backWallBase, floorBase);

        // Subtle architectural wall panel seams & perspective corners (drawn cleanly over the soft light wash)
        for (int wx = 320; wx < 1120; wx += 160)
        {
            Raylib.DrawLine(wx, 48, wx, 544, new Color(15, 18, 23, 255));
        }
        Raylib.DrawLine(0, 48, Constants.VirtualWidth, 48, Palette.OutlineVoid);
        Raylib.DrawLine(160, 48, 160, 544, Palette.OutlineVoid);
        Raylib.DrawLine(1120, 48, 1120, 544, Palette.OutlineVoid);
        Raylib.DrawLine(160, 544, 1120, 544, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(160, 544, 0, 720, Palette.OutlineVoid);
        Raylib.DrawLine(1120, 544, 1280, 720, Palette.OutlineVoid);

        for (int fy = 564; fy < Constants.VirtualHeight; fy += 24)
        {
            Raylib.DrawLine(0, fy, Constants.VirtualWidth, fy, new Color(11, 14, 18, 255));
        }

        // 3. Three Back-Wall Windows (y = 88..216, h = 128) featuring Booth 04's exact Crimson Brown Dwarf
        DrawBackWindow(LeftWinX, WinY, WinW, WinH, isMorning ? WindowSunMode.MorningBrownDwarf : WindowSunMode.DeepSpaceVoid, seed: 11);
        DrawBackWindow(CenterWinX, WinY, WinW, WinH, WindowSunMode.DeepSpaceVoid, seed: 23);
        DrawBackWindow(RightWinX, WinY, WinW, WinH, !isMorning ? WindowSunMode.EveningBrownDwarf : WindowSunMode.DeepSpaceVoid, seed: 37);

        // 4. Corner Stellar Stoker Amber Strips
        DrawCornerStokerStrip(LeftStokerX, StokerY, StokerW, StokerH, stokerFlicker, stokerBlinkDim);
        DrawCornerStokerStrip(RightStokerX, StokerY, StokerW, StokerH, stokerFlicker, stokerBlinkDim);

        // 5. Interactive Hotspots & Visual Props (clean artwork, ZERO permanent clutter text)
        DrawServiceChuteProp(mouse);
        DrawDoorProp(mouse, isMorning, IsDoorGatedByDay1Email || IsDoorGatedByDay6UnreadEmail || IsDoorGatedByDay6UncollectedChute);
        DrawTerminalDeskProp(mouse);
        DrawVentStashProp(mouse);
        DrawFoodReplicatorProp();
        DrawBedProp(mouse, isMorning);
        DrawStarChartWallProp(mouse);
        DrawBrokenApplianceProp();
    }

    private static Color BlendRgb(Color a, Color b, float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        byte r = (byte)Math.Clamp((int)MathF.Round(a.R + (b.R - a.R) * k), 0, 255);
        byte g = (byte)Math.Clamp((int)MathF.Round(a.G + (b.G - a.G) * k), 0, 255);
        byte bl = (byte)Math.Clamp((int)MathF.Round(a.B + (b.B - a.B) * k), 0, 255);
        return new Color(r, g, bl, (byte)255);
    }

    private static void DrawBrownDwarfDirectionalWash(bool isMorning, Color backWallBase, Color floorBase)
    {
        // Soft, subtle reddish-amber directional wash coming smoothly from Left Window (Morning) or Right Window (Evening).
        // 100% opaque pre-blended RGB so the RenderTexture2D alpha channel stays crisp and uniform with zero grey artifacts.
        Color warmGlowTint = isMorning
            ? new Color(86, 38, 32, 255)  // Soft crimson-amber Morning Brown Dwarf wash
            : new Color(92, 34, 30, 255); // Deep crimson-rust Evening Brown Dwarf wash
        Color coolShadowBase = new(16, 19, 25, 255);

        int activeWinX = isMorning ? LeftWinX : RightWinX;
        float lightOriginX = activeWinX + WinW * 0.5f;
        float lightOriginY = WinY + WinH * 0.85f;

        // Smooth 8px horizontal x 8px vertical dither-free subtle light field across back wall (x = 160..1120, y = 48..544)
        const int cellW = 16;
        const int cellH = 8;
        for (int y = 48; y < 544; y += cellH)
        {
            float dy = (y + cellH * 0.5f) - lightOriginY;
            float downBias = dy < 0f ? Math.Abs(dy) * 2.2f : dy * 0.92f;

            for (int x = 160; x < 1120; x += cellW)
            {
                float cx = x + cellW * 0.5f;
                // Gentle diagonal beam drift inward from active window
                float beamShift = isMorning ? Math.Max(0f, dy) * 0.22f : -Math.Max(0f, dy) * 0.22f;
                float dx = (cx - (lightOriginX + beamShift)) * 0.85f;
                float dist = MathF.Sqrt(dx * dx + downBias * downBias);

                float falloff = Math.Clamp(1.0f - dist / 460.0f, 0f, 1f);
                float smooth = falloff * falloff * (3.0f - 2.0f * falloff); // Cubic smoothstep

                // Subtle cool darkening on the far unlit half of the back wall
                float farSideFactor = isMorning
                    ? Math.Clamp((cx - 640f) / 480f, 0f, 1f) * 0.55f
                    : Math.Clamp((640f - cx) / 480f, 0f, 1f) * 0.55f;

                Color baseCol = BlendRgb(backWallBase, coolShadowBase, farSideFactor);
                Color litCol = BlendRgb(baseCol, warmGlowTint, smooth * 0.52f);
                Raylib.DrawRectangle(x, y, cellW, cellH, litCol);
            }
        }

        // Subtle crimson-amber bounce highlight right along the active window sill
        Raylib.DrawRectangle(activeWinX - 4, WinY + WinH + 4, WinW + 8, 2, BlendRgb(backWallBase, Palette.BrownDwarfGlowRim, 0.65f));
        Raylib.DrawRectangle(activeWinX + 6, WinY + WinH + 6, WinW - 12, 3, BlendRgb(backWallBase, warmGlowTint, 0.55f));

        // Gentle directional crimson-amber floor wash beneath the illuminated side (y = 544..720)
        for (int fy = 544; fy < Constants.VirtualHeight; fy += 8)
        {
            float fDistY = (fy - 544f) / 176f;
            float beamX = isMorning ? (lightOriginX + 42f + fDistY * 26f) : (lightOriginX - 42f - fDistY * 26f);
            for (int fx = 0; fx < Constants.VirtualWidth; fx += 16)
            {
                float fdx = Math.Abs((fx + 8f) - beamX);
                float radial = Math.Clamp(1.0f - fdx / (310f + fDistY * 90f), 0f, 1f);
                float verticalFade = Math.Clamp(1.0f - fDistY * 0.78f, 0f, 1f);
                float strength = radial * radial * verticalFade * 0.34f;
                if (strength > 0.01f)
                {
                    Raylib.DrawRectangle(fx, fy, 16, 8, BlendRgb(floorBase, warmGlowTint, strength));
                }
            }
        }
    }

    private enum WindowSunMode { DeepSpaceVoid, MorningBrownDwarf, EveningBrownDwarf }

    private static void DrawBackWindow(int x, int y, int w, int h, WindowSunMode mode, int seed)
    {
        // Outer reinforced bulkhead frame
        Raylib.DrawRectangle(x - 4, y - 4, w + 8, h + 8, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangleLines(x - 4, y - 4, w + 8, h + 8, Palette.OutlineVoid);
        Raylib.DrawRectangle(x, y, w, h, Palette.DeepSpaceVoid);

        Raylib.BeginScissorMode(x, y, w, h);

        // Subtle crimson horizon wash in the active Brown Dwarf window (matching Booth 04's exact viewport)
        if (mode != WindowSunMode.DeepSpaceVoid)
        {
            Raylib.DrawRectangle(x, y + h - 22, w, 22, Palette.BrownDwarfGlowDeep);
            Raylib.DrawLine(x, y + h - 22, x + w, y + h - 22, Palette.BrownDwarfGlowRim);
        }

        // Faint deterministic background stars (matching Booth 04 starfield colors)
        for (int s = 0; s < 16; s++)
        {
            int sx = x + 10 + ((seed * 37 + s * 53) % (w - 20));
            int sy = y + 10 + ((seed * 19 + s * 41) % (h - 24));
            Color starCol = (s % 4 == 0) ? Palette.BrownDwarfGlowRim : Palette.WeatheredGunmetalEdge;
            Raylib.DrawPixel(sx, sy, starCol);
        }

        // Exact Booth 04 Crimson / Red-Brown Banded Brown Dwarf Palette (100% opaque pre-blended RGB -- zero alpha artifacts!)
        if (mode is WindowSunMode.MorningBrownDwarf or WindowSunMode.EveningBrownDwarf)
        {
            bool morning = mode == WindowSunMode.MorningBrownDwarf;
            int cx = morning ? (x + 76) : (x + w - 76);
            int cy = morning ? (y + 72) : (y + 76);
            const int radius = 34;

            // Subtle 100% opaque crimson outer atmospheric halos pre-blended against DeepSpaceVoid
            Raylib.DrawCircle(cx, cy, radius + 12, BlendRgb(Palette.DeepSpaceVoid, Palette.BrownDwarfGlowDeep, 0.32f));
            Raylib.DrawCircle(cx, cy, radius + 5, BlendRgb(Palette.DeepSpaceVoid, Palette.BrownDwarfGlowDeep, 0.68f));

            // Core Brown Dwarf sphere in exact Booth 04 crimson/red-brown palette
            Raylib.DrawCircle(cx, cy, radius, Palette.BrownDwarfGlowDeep);               // #3b1518
            Raylib.DrawCircle(cx - 3, cy - 3, radius - 7, Palette.BrownDwarfGlowRim);   // #7a2b26
            Raylib.DrawCircle(cx - 5, cy - 5, radius - 16, new Color(142, 54, 42, 255)); // Muted rust-crimson ember

            // Crisp dark-crimson atmospheric cloud bands clipped strictly inside sphere radius
            DrawSphereBand(cx, cy, radius - 2, -16, 3, Palette.BrownDwarfGlowDeep);
            DrawSphereBand(cx, cy, radius - 2, -7, 4, new Color(46, 16, 19, 255));
            DrawSphereBand(cx, cy, radius - 2, 3, 3, Palette.BrownDwarfGlowDeep);
            DrawSphereBand(cx, cy, radius - 2, 12, 4, new Color(42, 14, 17, 255));
            DrawSphereBand(cx, cy, radius - 2, 21, 3, new Color(34, 12, 15, 255));

            // Crisp crimson rim outline matching Booth 04 viewport
            Raylib.DrawCircleLines(cx, cy, radius, Palette.BrownDwarfGlowRim);
        }

        // Window mullion & crisp glass reflection line (100% opaque)
        Raylib.DrawLine(x + w / 2, y, x + w / 2, y + h, new Color(28, 34, 42, 255));
        Raylib.DrawLine(x + 12, y + 6, x + 34, y + 28, new Color(46, 56, 68, 255));

        Raylib.EndScissorMode();
        Raylib.DrawRectangleLines(x, y, w, h, Palette.OutlineVoid);
    }

    private static void DrawSphereBand(int cx, int cy, int radius, int yOff, int height, Color col)
    {
        for (int dy = 0; dy < height; dy++)
        {
            int py = yOff + dy;
            int distSq = radius * radius - py * py;
            if (distSq <= 0)
                continue;
            int halfW = (int)MathF.Sqrt(distSq);
            Raylib.DrawLine(cx - halfW, cy + py, cx + halfW, cy + py, col);
        }
    }

    private static void DrawCornerStokerStrip(int x, int y, int w, int h, bool flickering, bool blinkDim)
    {
        Raylib.DrawRectangle(x, y, w, h, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, Palette.WeatheredGunmetalEdge);

        Color stripCore = blinkDim
            ? new Color(46, 24, 10, 255)
            : (flickering ? Palette.CrtAmberDimmed : new Color(186, 114, 34, 255));

        Raylib.DrawRectangle(x + 4, y + 6, w - 8, h - 12, stripCore);
        for (int sy = y + 36; sy < y + h - 12; sy += 44)
        {
            Raylib.DrawRectangle(x + 2, sy, w - 4, 4, Palette.WeatheredGunmetalMid);
        }

        int spillX = x < 640 ? x + w : x - 4;
        Color spillCol = blinkDim ? new Color(24, 20, 18, 255) : new Color(54, 38, 24, 255);
        Raylib.DrawRectangle(spillX, y + 8, 4, h - 16, spillCol);
    }

    private void DrawServiceChuteProp(VirtualMouseState mouse)
    {
        bool hov = mouse.IsHit(ChuteX, ChuteY, ChuteW, ChuteH);
        bool hasItems = _gameState.ServiceChuteItems.Count > 0;
        bool pulse = ((int)(_elapsedTime * 3.0f) % 2) == 0;

        Color border = hov
            ? Palette.CrtAmberPrimary
            : (hasItems ? (pulse ? Palette.ClearanceTeal : Palette.CrtAmberPrimary) : Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(ChuteX, ChuteY, ChuteW, ChuteH, new Color(24, 29, 36, 255));
        Raylib.DrawRectangleLines(ChuteX, ChuteY, ChuteW, ChuteH, border);
        if (hov)
            Raylib.DrawRectangleLines(ChuteX - 1, ChuteY - 1, ChuteW + 2, ChuteH + 2, Palette.CrtAmberPrimary);

        // Upper pneumatic intake louver + status LED
        Raylib.DrawRectangle(ChuteX + 10, ChuteY + 8, ChuteW - 34, 8, Palette.OutlineVoid);
        Raylib.DrawRectangle(ChuteX + ChuteW - 18, ChuteY + 8, 8, 8,
            hasItems ? (pulse ? Palette.ClearanceTeal : Palette.CrtAmberPrimary) : new Color(28, 34, 42, 255));

        // Chute flap mouth
        Raylib.DrawRectangle(ChuteX + 10, ChuteY + 24, ChuteW - 20, 54, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ChuteX + 10, ChuteY + 24, ChuteW - 20, 54, Palette.OxidizedBronzeWorn);
        // Inner chute baffle bevel
        Raylib.DrawLine(ChuteX + 12, ChuteY + 32, ChuteX + ChuteW - 13, ChuteY + 32, new Color(30, 36, 44, 255));

        if (hasItems)
        {
            // Visible package silhouette peeking cleanly inside chute tray
            Raylib.DrawRectangle(ChuteX + 18, ChuteY + 48, ChuteW - 36, 24, Palette.OxidizedBronzeShadow);
            Raylib.DrawRectangleLines(ChuteX + 18, ChuteY + 48, ChuteW - 36, 24, Palette.CrtAmberPrimary);
            Raylib.DrawLine(ChuteX + 24, ChuteY + 58, ChuteX + ChuteW - 25, ChuteY + 58, Palette.ClearanceTeal);
        }

        // Bottom latch handle
        Raylib.DrawRectangle(ChuteX + 26, ChuteY + 84, ChuteW - 52, 4, Palette.WeatheredGunmetalEdge);
    }

    private static void DrawDoorProp(VirtualMouseState mouse, bool isMorning, bool isDoorGated)
    {
        bool hov = mouse.IsHit(DoorX, DoorY, DoorW, DoorH);
        Color border = hov
            ? (isMorning && !isDoorGated ? Palette.ClearanceTeal : Palette.CrtAmberPrimary)
            : Palette.OxidizedBronzeWorn;

        Raylib.DrawRectangle(DoorX, DoorY, DoorW, DoorH, new Color(30, 36, 44, 255));
        Raylib.DrawRectangleLines(DoorX, DoorY, DoorW, DoorH, border);
        if (hov)
            Raylib.DrawRectangleLines(DoorX - 1, DoorY - 1, DoorW + 2, DoorH + 2, border);
        Raylib.DrawRectangleLines(DoorX + 5, DoorY + 5, DoorW - 10, DoorH - 10, Palette.OutlineVoid);

        // Recessed upper bulkhead panel & corridor viewport slit
        Raylib.DrawRectangle(DoorX + 22, DoorY + 28, DoorW - 44, 42, Palette.DeepSpaceVoid);
        Raylib.DrawRectangle(DoorX + 26, DoorY + 44, DoorW - 52, 14, Palette.BrownDwarfGlowDeep);
        Raylib.DrawLine(DoorX + 26, DoorY + 44, DoorX + DoorW - 27, DoorY + 44, Palette.BrownDwarfGlowRim);
        Raylib.DrawRectangleLines(DoorX + 22, DoorY + 28, DoorW - 44, 42, Palette.WeatheredGunmetalEdge);

        // Middle structural seam & lower kickplate
        Raylib.DrawLine(DoorX + 10, DoorY + 114, DoorX + DoorW - 11, DoorY + 114, Palette.OutlineVoid);
        Raylib.DrawRectangle(DoorX + 14, DoorY + 186, DoorW - 28, 72, new Color(24, 29, 36, 255));
        Raylib.DrawRectangleLines(DoorX + 14, DoorY + 186, DoorW - 28, 72, new Color(38, 46, 56, 255));

        // Electronic lock pad & status LED (teal = watch ready; dim amber = off watch or Day 1 unread email gate)
        Raylib.DrawRectangle(DoorX + DoorW - 28, DoorY + 128, 16, 40, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(DoorX + DoorW - 28, DoorY + 128, 16, 40, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(DoorX + DoorW - 24, DoorY + 134, 8, 8,
            (isMorning && !isDoorGated) ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);
        Raylib.DrawRectangle(DoorX + DoorW - 24, DoorY + 148, 8, 14, Palette.WeatheredGunmetalMid);
    }

    private void DrawTerminalDeskProp(VirtualMouseState mouse)
    {
        bool hov = mouse.IsHit(DeskX, DeskY, DeskW, DeskH);
        Color border = hov ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn;

        // Desk tabletop spanning (216, 336, 248, 136)
        Raylib.DrawRectangle(DeskX, DeskY + 86, DeskW, 50, new Color(34, 40, 48, 255));
        Raylib.DrawRectangleLines(DeskX, DeskY + 86, DeskW, 50, border);
        if (hov)
            Raylib.DrawRectangleLines(DeskX - 1, DeskY + 85, DeskW + 2, 52, Palette.CrtAmberPrimary);

        // Desk legs framing the under-desk Vent Stash below (y = 472..556)
        Raylib.DrawRectangle(DeskX + 8, DeskY + 136, 16, 84, new Color(24, 29, 36, 255));
        Raylib.DrawRectangle(DeskX + DeskW - 24, DeskY + 136, 16, 84, new Color(24, 29, 36, 255));

        // Monitor chassis seated on desk
        int monX = DeskX + 36;
        int monY = DeskY + 8;
        int monW = 176;
        int monH = 82;
        Raylib.DrawRectangle(monX, monY, monW, monH, new Color(26, 32, 40, 255));
        Raylib.DrawRectangleLines(monX, monY, monW, monH, border);
        if (hov)
            Raylib.DrawRectangleLines(monX - 1, monY - 1, monW + 2, monH + 2, Palette.CrtAmberPrimary);

        // Clean visual-only CRT display lines (ZERO clutter text!)
        int scrX = monX + 10;
        int scrY = monY + 8;
        int scrW = monW - 20;
        int scrH = 40;
        Raylib.DrawRectangle(scrX, scrY, scrW, scrH, new Color(10, 18, 22, 255));
        Raylib.DrawRectangleLines(scrX, scrY, scrW, scrH, hov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        // Subtle miniature CRT scanlines & data bars (100% opaque)
        Raylib.DrawRectangle(scrX + 8, scrY + 8, 56, 4, Palette.ClearanceTeal);
        Raylib.DrawRectangle(scrX + 8, scrY + 16, 108, 3, new Color(28, 96, 92, 255));
        Raylib.DrawRectangle(scrX + 8, scrY + 23, 84, 3, _gameState.HasUnreadEmails ? Palette.CrtAmberPrimary : new Color(110, 62, 22, 255));
        Raylib.DrawRectangle(scrX + 8, scrY + 30, 64, 3, new Color(22, 66, 68, 255));

        // Physical Unread Indicator LED at EXACT (356, 396, 12, 12):
        // Blinks amber on Day 1 Morning (1 unread Brengop email) and any time unread mail exists; turns dark when all mail is read
        bool hasUnread = _gameState.HasUnreadEmails;
        bool ledOn = hasUnread && ((_elapsedTime % 1.2f) < 0.6f);
        Raylib.DrawRectangle(UnreadLedX - 1, UnreadLedY - 1, UnreadLedSize + 2, UnreadLedSize + 2, Palette.OutlineVoid);
        Color ledCol = ledOn
            ? Palette.CrtAmberPrimary
            : (hasUnread ? new Color(84, 46, 12, 255) : new Color(18, 22, 26, 255));
        Raylib.DrawRectangle(UnreadLedX, UnreadLedY, UnreadLedSize, UnreadLedSize, ledCol);
        if (ledOn)
        {
            Raylib.DrawRectangle(UnreadLedX + 3, UnreadLedY + 3, 4, 4, new Color(255, 240, 190, 255));
        }

        // Physical pixel keyboard deck (no instructional text!)
        int kbX = DeskX + 48;
        int kbY = DeskY + 100;
        int kbW = 152;
        int kbH = 24;
        Raylib.DrawRectangle(kbX, kbY, kbW, kbH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(kbX, kbY, kbW, kbH, hov ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalEdge);
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 14; col++)
            {
                Raylib.DrawRectangle(kbX + 6 + col * 10, kbY + 4 + row * 6, 7, 4, new Color(44, 52, 62, 255));
            }
        }
    }

    private void DrawVentStashProp(VirtualMouseState mouse)
    {
        bool hov = mouse.IsHit(VentX, VentY, VentW, VentH);
        int count = _gameState.VentStashItems.Count;
        Color border = hov
            ? Palette.CrtAmberPrimary
            : (count > 0 ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge);

        Raylib.DrawRectangle(VentX, VentY, VentW, VentH, new Color(14, 17, 22, 255));
        Raylib.DrawRectangleLines(VentX, VentY, VentW, VentH, border);
        if (hov)
            Raylib.DrawRectangleLines(VentX - 1, VentY - 1, VentW + 2, VentH + 2, Palette.CrtAmberPrimary);

        // Slatted concealed vent louvers (clean artwork, ZERO permanent label)
        for (int vy = VentY + 8; vy < VentY + VentH - 8; vy += 9)
        {
            Raylib.DrawRectangle(VentX + 8, vy, VentW - 16, 4, Palette.OutlineVoid);
            Raylib.DrawLine(VentX + 8, vy + 4, VentX + VentW - 9, vy + 4, new Color(36, 44, 54, 255));
        }

        // Subtle physical visual cue when contraband items are tucked behind louvers
        if (count > 0)
        {
            for (int c = 0; c < Math.Min(3, count); c++)
            {
                Raylib.DrawRectangle(VentX + 16 + c * 14, VentY + VentH - 12, 8, 4, Palette.OxidizedBronzeWorn);
            }
        }
    }

    /// <summary>
    /// Visual-only Food Replicator prop (zero permanent labels, zero hover/click clutter).
    /// Displays subtle tray + rising steam indicator when rations are ordered for tonight.
    /// </summary>
    private void DrawFoodReplicatorProp()
    {
        bool dispensed = _gameState.QuartersPhase == QuartersPhase.Evening && _gameState.ReplicatorDispensedTonight;
        bool cuePulse = IsDay1EveningRationCueActive;
        float pulseT = cuePulse ? (0.5f + 0.5f * MathF.Sin(_elapsedTime * 3.0f)) : 0f;

        Color border = dispensed
            ? new Color(64, 118, 114, 255)
            : (cuePulse
                ? BlendRgb(Palette.OxidizedBronzeShadow, Palette.CrtAmberPrimary, 0.45f + pulseT * 0.55f)
                : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(ReplicatorX, ReplicatorY, ReplicatorW, ReplicatorH, new Color(26, 31, 38, 255));
        Raylib.DrawRectangleLines(ReplicatorX, ReplicatorY, ReplicatorW, ReplicatorH, border);

        // Subtle upper dispenser chassis trim + status diode
        Raylib.DrawRectangle(ReplicatorX + 16, ReplicatorY + 10, ReplicatorW - 44, 6, new Color(18, 22, 28, 255));
        Raylib.DrawRectangle(ReplicatorX + ReplicatorW - 22, ReplicatorY + 10, 6, 6,
            dispensed
                ? Palette.ClearanceTeal
                : (cuePulse
                    ? BlendRgb(Palette.CrtAmberDimmed, Palette.CrtAmberPrimary, pulseT)
                    : new Color(32, 38, 46, 255)));

        // Dispenser alcove
        int alcX = ReplicatorX + 18;
        int alcY = ReplicatorY + 28;
        int alcW = ReplicatorW - 36;
        int alcH = 88;
        Raylib.DrawRectangle(alcX, alcY, alcW, alcH, dispensed ? new Color(42, 34, 20, 255) : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(alcX, alcY, alcW, alcH,
            dispensed
                ? Palette.OxidizedBronzeWorn
                : (cuePulse ? border : Palette.WeatheredGunmetalMid));

        if (dispensed)
        {
            // Overhead warm synthesizer strip + dispensed meal tray + steam wisps
            Raylib.DrawRectangle(alcX + 16, alcY + 3, alcW - 32, 4, Palette.CrtAmberPrimary);
            Raylib.DrawRectangle(alcX + 16, alcY + 66, alcW - 32, 12, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangleLines(alcX + 16, alcY + 66, alcW - 32, 12, Palette.OutlineVoid);

            if (_gameState.TonightRationOrder == NightlyRationOrder.DryRationBricks)
            {
                Raylib.DrawRectangle(alcX + 30, alcY + 50, 26, 16, Palette.OxidizedBronzeWorn);
                Raylib.DrawRectangle(alcX + 60, alcY + 52, 26, 14, Palette.OxidizedBronzeShadow);
            }
            else if (_gameState.TonightRationOrder == NightlyRationOrder.NutrientPaste)
            {
                Raylib.DrawRectangle(alcX + 32, alcY + 48, 58, 18, Palette.FromHex("#bdb040"));
                Raylib.DrawRectangleLines(alcX + 32, alcY + 48, 58, 18, Palette.OutlineVoid);
                int steamOff = ((int)(_elapsedTime * 3f)) % 6;
                Raylib.DrawRectangle(alcX + 54, alcY + 34 - steamOff, 2, 6, new Color(156, 174, 184, 255));
            }
            else
            {
                // High-Tier Rations: warm glowing multi-compartment meal + gentle rising steam
                Raylib.DrawRectangle(alcX + 24, alcY + 48, 36, 18, Palette.ClearanceTeal);
                Raylib.DrawRectangle(alcX + 64, alcY + 50, 32, 16, Palette.CrtAmberPrimary);
                int steamA = ((int)(_elapsedTime * 4f)) % 10;
                int steamB = ((int)(_elapsedTime * 4f + 5)) % 10;
                Raylib.DrawRectangle(alcX + 38, alcY + 38 - steamA, 2, 6, new Color(182, 202, 214, 255));
                Raylib.DrawRectangle(alcX + 76, alcY + 40 - steamB, 2, 6, new Color(182, 202, 214, 255));
            }
        }
        else
        {
            // Dark recessed drip grate at bottom of idle alcove
            Raylib.DrawRectangle(alcX + 14, alcY + alcH - 10, alcW - 28, 6, new Color(18, 22, 28, 255));
        }

        // Lower cabinet vent slats
        Raylib.DrawLine(ReplicatorX + 20, ReplicatorY + 126, ReplicatorX + ReplicatorW - 20, ReplicatorY + 126, Palette.OutlineVoid);
        Raylib.DrawLine(ReplicatorX + 20, ReplicatorY + 132, ReplicatorX + ReplicatorW - 20, ReplicatorY + 132, Palette.OutlineVoid);
    }

    private void DrawBedProp(VirtualMouseState mouse, bool isMorning)
    {
        bool hov = mouse.IsHit(BedX, BedY, BedW, BedH);
        bool sleepReady = !isMorning;
        Color border = hov
            ? (sleepReady ? Palette.CrtAmberPrimary : Palette.ClearanceTeal)
            : (sleepReady ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge);

        // Metal bunk frame (ZERO permanent clutter text)
        Raylib.DrawRectangle(BedX, BedY, BedW, BedH, new Color(30, 35, 42, 255));
        Raylib.DrawRectangleLines(BedX, BedY, BedW, BedH, border);
        if (hov)
            Raylib.DrawRectangleLines(BedX - 1, BedY - 1, BedW + 2, BedH + 2, border);

        // Headboard + mattress + pillow + folded blanket
        Raylib.DrawRectangle(BedX + 6, BedY + 10, 22, BedH - 20, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(BedX + 30, BedY + 18, BedW - 40, BedH - 36, new Color(46, 54, 64, 255));
        Raylib.DrawRectangleLines(BedX + 30, BedY + 18, BedW - 40, BedH - 36, Palette.OutlineVoid);

        // Pillow
        Raylib.DrawRectangle(BedX + 38, BedY + 28, 52, 62, new Color(142, 150, 160, 255));
        Raylib.DrawRectangleLines(BedX + 38, BedY + 28, 52, 62, Palette.OutlineVoid);

        // Blanket (subtle crimson-brown tint in Evening Brown Dwarf light; cool slate in Morning)
        Color blanketCol = sleepReady ? new Color(84, 46, 40, 255) : new Color(40, 52, 66, 255);
        Raylib.DrawRectangle(BedX + 100, BedY + 22, BedW - 114, BedH - 44, blanketCol);
        Raylib.DrawRectangleLines(BedX + 100, BedY + 22, BedW - 114, BedH - 44, Palette.OutlineVoid);
        // Fold seam across blanket
        Raylib.DrawLine(BedX + 114, BedY + 23, BedX + 114, BedY + BedH - 24, Palette.OutlineVoid);

        // Subtle red warning diode on bunk frame edge if a Sleep Raid is pending in Evening Quarters
        if (sleepReady && _raidController.CheckPendingSleepAudit(_gameState, _config) != BerthAuditKind.None)
        {
            bool blink = ((int)(_elapsedTime * 3.5f) % 2) == 0;
            Raylib.DrawRectangle(BedX + 13, BedY + 18, 8, 8, blink ? Palette.AlertCrimson : Palette.BrownDwarfGlowDeep);
        }
    }

    /// <summary>
    /// Glowing Wall-Mounted Interactive Sector Star Chart display unit on the right wall at (1136, 176, 120, 144),
    /// mounted directly above the dark, cracked, unclickable broken appliance at (1136, 344, 120, 152).
    /// Visual language ("Glowing = Interactable"): backlit teal/amber map glow with tiny star dots and route lines.
    /// </summary>
    private void DrawStarChartWallProp(VirtualMouseState mouse)
    {
        bool hov = mouse.IsHit(StarChartX, StarChartY, StarChartW, StarChartH);
        float pulse = 0.5f + 0.5f * MathF.Sin(_elapsedTime * 3.2f);

        // Soft 100% opaque pre-blended backlit wall spill around the unit to reinforce "Glowing = Interactable"
        Color spillOuter = BlendRgb(new Color(17, 20, 25, 255), Palette.ClearanceTeal, hov ? 0.24f : (0.12f + pulse * 0.04f));
        Color spillInner = BlendRgb(new Color(17, 20, 25, 255), Palette.ClearanceTeal, hov ? 0.36f : (0.20f + pulse * 0.06f));
        Raylib.DrawRectangle(StarChartX - 4, StarChartY - 4, StarChartW + 8, StarChartH + 8, spillOuter);
        Raylib.DrawRectangle(StarChartX - 2, StarChartY - 2, StarChartW + 4, StarChartH + 4, spillInner);

        // Heavy wall-mount chassis & brackets
        Color border = hov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal;
        Raylib.DrawRectangle(StarChartX, StarChartY, StarChartW, StarChartH, new Color(24, 30, 38, 255));
        Raylib.DrawRectangleLines(StarChartX, StarChartY, StarChartW, StarChartH, border);
        if (hov)
        {
            Raylib.DrawRectangleLines(StarChartX - 1, StarChartY - 1, StarChartW + 2, StarChartH + 2, Palette.CrtAmberPrimary);
        }

        // Top & bottom mounting lugs
        Raylib.DrawRectangle(StarChartX + 16, StarChartY - 3, 16, 3, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(StarChartX + StarChartW - 32, StarChartY - 3, 16, 3, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(StarChartX + 16, StarChartY + StarChartH, 16, 3, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(StarChartX + StarChartW - 32, StarChartY + StarChartH, 16, 3, Palette.WeatheredGunmetalEdge);

        // Backlit CRT/holo map glass pane (1144, 186, 104, 108)
        int scrX = StarChartX + 8;
        int scrY = StarChartY + 10;
        int scrW = StarChartW - 16; // 104
        int scrH = 108;

        Color glassBg = BlendRgb(new Color(8, 20, 26, 255), new Color(16, 44, 52, 255), 0.35f + pulse * 0.25f);
        Raylib.DrawRectangle(scrX, scrY, scrW, scrH, glassBg);

        // Subtle miniature astrogation grid lines
        Color miniGrid = new(16, 40, 48, 255);
        for (int gx = scrX + 20; gx < scrX + scrW; gx += 20)
            Raylib.DrawLine(gx, scrY + 1, gx, scrY + scrH - 2, miniGrid);
        for (int gy = scrY + 22; gy < scrY + scrH; gy += 22)
            Raylib.DrawLine(scrX + 1, gy, scrX + scrW - 2, gy, miniGrid);

        // Faint crimson Ashen Rift hazard zone wedge at top-center
        Raylib.DrawRectangle(scrX + 34, scrY + 4, 34, 14, new Color(48, 18, 22, 255));
        Raylib.DrawLine(scrX + 34, scrY + 18, scrX + 50, scrY + 30, new Color(148, 42, 42, 255));
        Raylib.DrawLine(scrX + 68, scrY + 18, scrX + 50, scrY + 30, new Color(148, 42, 42, 255));

        // Miniature glowing sector trade route lines radiating from VG-3 center (vgX, vgY)
        int vgX = scrX + 44;
        int vgY = scrY + 58;
        (int X, int Y, Color Col)[] miniNeighbors =
        [
            (scrX + 26, scrY + 42, Palette.FromHex("#22c55e")), // Gharok
            (scrX + 20, scrY + 60, Palette.FromHex("#22c55e")), // Veridia
            (scrX + 28, scrY + 80, Palette.FromHex("#22c55e")), // Oruun-4
            (scrX + 56, scrY + 38, Palette.AlertCrimson),       // Ash
            (scrX + 70, scrY + 58, Palette.FromHex("#22c55e")), // Korr-Vath
            (scrX + 66, scrY + 82, Palette.FromHex("#22c55e"))  // Dul-Krag
        ];

        Color routeGlow = hov ? Palette.CrtAmberPrimary : new Color(212, 160, 82, 255);
        foreach (var nb in miniNeighbors)
        {
            Raylib.DrawLine(vgX, vgY, nb.X, nb.Y, routeGlow);
        }
        // Connecting outer rim segments
        Raylib.DrawLine(miniNeighbors[0].X, miniNeighbors[0].Y, miniNeighbors[1].X, miniNeighbors[1].Y, new Color(148, 110, 54, 255));
        Raylib.DrawLine(miniNeighbors[1].X, miniNeighbors[1].Y, miniNeighbors[2].X, miniNeighbors[2].Y, new Color(148, 110, 54, 255));
        Raylib.DrawLine(miniNeighbors[4].X, miniNeighbors[4].Y, miniNeighbors[5].X, miniNeighbors[5].Y, new Color(148, 110, 54, 255));

        // Tiny outer cluster star dots
        (int X, int Y)[] outerStars =
        [
            (scrX + 12, scrY + 26), (scrX + 10, scrY + 48), (scrX + 12, scrY + 76),
            (scrX + 86, scrY + 34), (scrX + 90, scrY + 56), (scrX + 86, scrY + 84),
            (scrX + 46, scrY + 12)
        ];
        foreach (var os in outerStars)
        {
            Raylib.DrawRectangle(os.X, os.Y, 2, 2, Palette.CrtAmberDimmed);
        }

        // Neighboring star nodes
        foreach (var nb in miniNeighbors)
        {
            Raylib.DrawRectangle(nb.X - 1, nb.Y - 1, 3, 3, nb.Col);
        }

        // Central VG-3 glowing teal station beacon + radar ring
        Raylib.DrawCircleLines(vgX, vgY, hov ? 7 : 6, hov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        Raylib.DrawRectangle(vgX - 2, vgY - 2, 5, 5, Palette.ClearanceTeal);
        Raylib.DrawPixel(vgX, vgY, new Color(230, 255, 250, 255));

        Raylib.DrawRectangleLines(scrX, scrY, scrW, scrH, hov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);

        // Lower backlit telemetry status strip (1144, 300, 104, 12) -- active lit power LED + glowing data bars
        int stripY = StarChartY + StarChartH - 20;
        Raylib.DrawRectangle(scrX, stripY, scrW, 12, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(scrX, stripY, scrW, 12, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(scrX + 4, stripY + 3, 6, 6, Palette.ClearanceTeal);
        Raylib.DrawRectangle(scrX + 16, stripY + 4, 34, 4, hov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        Raylib.DrawRectangle(scrX + 56, stripY + 4, 24, 4, Palette.CrtAmberPrimary);
        Raylib.DrawRectangle(scrX + 84, stripY + 4, 14, 4, Palette.FromHex("#22c55e"));
    }

    /// <summary>
    /// Visibly dead &amp; unclickable weathered right-wall appliance (zero labels, zero hover/click response).
    /// </summary>
    private static void DrawBrokenApplianceProp()
    {
        Raylib.DrawRectangle(BrokenAppX, BrokenAppY, BrokenAppW, BrokenAppH, new Color(22, 25, 30, 255));
        Raylib.DrawRectangleLines(BrokenAppX, BrokenAppY, BrokenAppW, BrokenAppH, new Color(36, 42, 50, 255));

        // Cracked dark dead display pane
        Raylib.DrawRectangle(BrokenAppX + 12, BrokenAppY + 16, BrokenAppW - 24, 46, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(BrokenAppX + 12, BrokenAppY + 16, BrokenAppW - 24, 46, new Color(32, 38, 46, 255));
        Raylib.DrawLine(BrokenAppX + 16, BrokenAppY + 22, BrokenAppX + 48, BrokenAppY + 54, new Color(56, 64, 74, 255));
        Raylib.DrawLine(BrokenAppX + 34, BrokenAppY + 34, BrokenAppX + 62, BrokenAppY + 46, new Color(44, 52, 62, 255));

        // Dead dark power LED & missing dial socket
        Raylib.DrawRectangle(BrokenAppX + 16, BrokenAppY + 74, 8, 8, new Color(14, 16, 20, 255));
        Raylib.DrawCircle(BrokenAppX + BrokenAppW - 26, BrokenAppY + 78, 7, Palette.OutlineVoid);

        // Loose sagging panel & dangling dead wire pixels
        Raylib.DrawRectangle(BrokenAppX + 14, BrokenAppY + 96, BrokenAppW - 28, 34, new Color(16, 19, 23, 255));
        Raylib.DrawRectangleLines(BrokenAppX + 14, BrokenAppY + 96, BrokenAppW - 28, 34, new Color(46, 36, 28, 255));
        Raylib.DrawLine(BrokenAppX + 32, BrokenAppY + 128, BrokenAppX + 36, BrokenAppY + 146, new Color(72, 46, 28, 255));
        Raylib.DrawLine(BrokenAppX + 44, BrokenAppY + 128, BrokenAppX + 40, BrokenAppY + 142, new Color(54, 62, 72, 255));
    }

    // =========================================================================
    // BOTTOM COAT POCKET STRIP (Synced 1->3 Slots with Booth 04 Lap Pocket)
    // =========================================================================
    private void DrawCoatPocketTray(VirtualMouseState mouse)
    {
        int cap = Math.Clamp(_gameState.PocketCapacity, 1, 3);
        bool trayHov = mouse.IsHit(CoatTrayX, CoatTrayY, CoatTrayW, CoatTrayH);

        Raylib.DrawRectangle(CoatTrayX, CoatTrayY, CoatTrayW, CoatTrayH, new Color(14, 18, 24, 255));
        Raylib.DrawRectangleLines(CoatTrayX, CoatTrayY, CoatTrayW, CoatTrayH, trayHov ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangleLines(CoatTrayX + 2, CoatTrayY + 2, CoatTrayW - 4, CoatTrayH - 4, Palette.OutlineVoid);

        // Compact Left Label Column
        UiText.Draw("COAT POCKET", CoatTrayX + 12, CoatTrayY + 14, 10, Palette.ClearanceTeal);
        UiText.Draw($"{_gameState.PocketItems.Count} / {cap} SLOTS", CoatTrayX + 12, CoatTrayY + 30, 10, Palette.CrtAmberPrimary);
        if (_gameState.HasShieldedPocketLining)
        {
            UiText.Draw("LINING: 1 SAFE", CoatTrayX + 12, CoatTrayY + 46, 10, Palette.ClearanceTeal);
        }
        else
        {
            UiText.Draw("CLICK -> VENT", CoatTrayX + 12, CoatTrayY + 46, 10, Palette.WeatheredGunmetalEdge);
        }

        // 3 Pocket Slots (1..cap unlocked, remaining dimmed)
        for (int i = 0; i < 3; i++)
        {
            int sx = CoatSlotStartX + i * (CoatSlotW + CoatSlotGap);
            bool unlocked = i < cap;
            bool hasItem = unlocked && i < _gameState.PocketItems.Count;
            bool slotHov = unlocked && mouse.IsHit(sx, CoatSlotY, CoatSlotW, CoatSlotH);

            if (!unlocked)
            {
                Raylib.DrawRectangle(sx, CoatSlotY, CoatSlotW, CoatSlotH, new Color(10, 13, 17, 255));
                Raylib.DrawRectangleLines(sx, CoatSlotY, CoatSlotW, CoatSlotH, new Color(28, 34, 42, 255));
                const string lockTxt = "LOCKED";
                int lw = UiText.MeasureWidth(lockTxt, 10);
                UiText.Draw(lockTxt, sx + (CoatSlotW - lw) / 2, CoatSlotY + 19, 10, new Color(46, 54, 64, 255));
                continue;
            }

            if (!hasItem)
            {
                Raylib.DrawRectangle(sx, CoatSlotY, CoatSlotW, CoatSlotH, Palette.OutlineVoid);
                Raylib.DrawRectangleLines(sx, CoatSlotY, CoatSlotW, CoatSlotH, Palette.WeatheredGunmetalMid);
                const string emptyTxt = "EMPTY";
                int ew = UiText.MeasureWidth(emptyTxt, 10);
                UiText.Draw(emptyTxt, sx + (CoatSlotW - ew) / 2, CoatSlotY + 19, 10, Palette.WeatheredGunmetalEdge);
                continue;
            }

            ContrabandPocketItem item = _gameState.PocketItems[i];
            Color bodyCol = item.ItemType switch
            {
                ContrabandPocketItem.TypeBlankDataSlate => new Color(44, 42, 34, 255),
                ContrabandPocketItem.TypeCopiedDataSlate => new Color(56, 44, 24, 255),
                ContrabandPocketItem.TypeMissionDataSlate => new Color(36, 46, 30, 255),
                _ => Palette.WeatheredGunmetalMid
            };
            Color rimCol = slotHov
                ? Palette.CrtAmberPrimary
                : item.ItemType switch
                {
                    ContrabandPocketItem.TypeCopiedDataSlate => Palette.CrtAmberPrimary,
                    ContrabandPocketItem.TypeMissionDataSlate => Palette.AlertCrimson,
                    _ => Palette.OxidizedBronzeWorn
                };

            Raylib.DrawRectangle(sx, CoatSlotY, CoatSlotW, CoatSlotH, bodyCol);
            Raylib.DrawRectangleLines(sx, CoatSlotY, CoatSlotW, CoatSlotH, rimCol);

            string title = item.ItemType switch
            {
                ContrabandPocketItem.TypeBlankDataSlate => "BLANK SLATE",
                ContrabandPocketItem.TypeCopiedDataSlate => "STOLEN SLATE",
                ContrabandPocketItem.TypeMissionDataSlate => "COVERT SLATE",
                _ => "ITEM"
            };
            string sub = !string.IsNullOrEmpty(item.TargetShipRegistration)
                ? item.TargetShipRegistration
                : "UNREG";

            UiText.Draw(TruncateText(title, 10, CoatSlotW - 12), sx + 6, CoatSlotY + 9, 10, rimCol);
            UiText.Draw(TruncateText(sub, 10, CoatSlotW - 12), sx + 6, CoatSlotY + 26, 10, new Color(204, 212, 222, 255));
        }
    }

    // =========================================================================
    // CONCISE HOVER-ONLY TOOLTIP RENDERER (5 Interactive Hotspots + Coat Pocket)
    // =========================================================================
    private void DrawActiveHoverTooltip(VirtualMouseState mouse)
    {
        if (!mouse.IsInsideViewport)
            return;

        string? tip = null;
        Color accent = Palette.CrtAmberPrimary;
        int anchorX = mouse.X;
        int anchorY = mouse.Y - 30;

        bool isMorning = _gameState.QuartersPhase == QuartersPhase.Morning;

        if (mouse.IsHit(DoorX, DoorY, DoorW, DoorH))
        {
            tip = isMorning ? "DOOR // WALK TO BOOTH 04" : "DOOR // OFF WATCH";
            accent = isMorning ? Palette.ClearanceTeal : Palette.CrtAmberPrimary;
            anchorX = DoorX + DoorW / 2;
            anchorY = DoorY - 30;
        }
        else if (mouse.IsHit(ChuteX, ChuteY, ChuteW, ChuteH))
        {
            int c = _gameState.ServiceChuteItems.Count;
            tip = c > 0 ? $"SERVICE CHUTE ({c} DROP)" : "SERVICE CHUTE (EMPTY)";
            accent = c > 0 ? Palette.ClearanceTeal : Palette.CrtAmberPrimary;
            anchorX = ChuteX + ChuteW / 2;
            anchorY = ChuteY - 30;
        }
        else if (mouse.IsHit(DeskX, DeskY, DeskW, DeskH))
        {
            tip = _gameState.HasUnreadEmails
                ? $"PERSONAL TERMINAL ({_gameState.UnreadEmailCount} UNREAD)"
                : "PERSONAL TERMINAL";
            accent = Palette.ClearanceTeal;
            anchorX = DeskX + DeskW / 2;
            anchorY = DeskY - 30;
        }
        else if (mouse.IsHit(VentX, VentY, VentW, VentH))
        {
            tip = $"VENT STASH ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity})";
            accent = Palette.CrtAmberPrimary;
            anchorX = VentX + VentW / 2;
            anchorY = VentY - 30;
        }
        else if (mouse.IsHit(BedX, BedY, BedW, BedH))
        {
            if (isMorning)
            {
                tip = "BED // SLEEP (EVENING ONLY)";
                accent = Palette.WeatheredGunmetalEdge;
            }
            else
            {
                bool auditRisk = _raidController.CheckPendingSleepAudit(_gameState, _config) != BerthAuditKind.None;
                tip = auditRisk ? "BED // SLEEP [RAID WARRANT PENDING!]" : "BED // SLEEP";
                accent = auditRisk ? Palette.AlertCrimson : Palette.ClearanceTeal;
            }
            anchorX = BedX + BedW / 2;
            anchorY = BedY - 30;
        }
        else if (mouse.IsHit(StarChartX, StarChartY, StarChartW, StarChartH))
        {
            tip = "SECTOR STAR CHART";
            accent = Palette.ClearanceTeal;
            anchorX = StarChartX + StarChartW / 2;
            anchorY = StarChartY - 30;
        }
        else if (mouse.IsHit(CoatTrayX, CoatTrayY, CoatTrayW, CoatTrayH))
        {
            int cap = Math.Clamp(_gameState.PocketCapacity, 1, 3);
            for (int i = 0; i < cap && i < _gameState.PocketItems.Count; i++)
            {
                int sx = CoatSlotStartX + i * (CoatSlotW + CoatSlotGap);
                if (mouse.IsHit(sx, CoatSlotY, CoatSlotW, CoatSlotH))
                {
                    ContrabandPocketItem item = _gameState.PocketItems[i];
                    tip = $"{item.DisplayName} // CLICK TO STASH IN VENT";
                    accent = Palette.CrtAmberPrimary;
                    anchorX = sx + CoatSlotW / 2;
                    anchorY = CoatTrayY - 30;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(tip))
            return;

        ActiveHoverTooltipText = tip;
        int tw = UiText.MeasureWidth(tip, 10);
        int boxW = tw + 20;
        const int boxH = 24;
        int bx = Math.Clamp(anchorX - boxW / 2, 12, Constants.VirtualWidth - boxW - 12);
        int by = Math.Clamp(anchorY, TopBarH + 8, Constants.VirtualHeight - boxH - 8);

        Raylib.DrawRectangle(bx, by, boxW, boxH, new Color(8, 11, 16, 255));
        Raylib.DrawRectangleLines(bx, by, boxW, boxH, accent);
        UiText.Draw(tip, bx + 10, by + 7, 10, accent);
    }

    // =========================================================================
    // SERVICE CHUTE PICKUP TRAY, VENT STASH DRAWER, TWO-TIER RAID & FAILURE MODALS
    // =========================================================================
    private void DrawChutePickupTray(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 148));
        Raylib.DrawRectangle(ChuteTrayX, ChuteTrayY, ChuteTrayW, ChuteTrayH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ChuteTrayX, ChuteTrayY, ChuteTrayW, ChuteTrayH, Palette.ClearanceTeal);

        Raylib.DrawRectangle(ChuteTrayX + 2, ChuteTrayY + 2, ChuteTrayW - 4, 34, new Color(18, 32, 38, 255));
        string chuteHdr = $"0500 SERVICE CHUTE // CLICK ITEM -> COAT POCKET ({_gameState.PocketItems.Count}/{_gameState.PocketCapacity}  |  VENT: {_gameState.VentStashItems.Count}/{_config.VentStashCapacity})";
        UiText.Draw(UiText.TruncateToWidth(chuteHdr, 10, ChuteTrayW - 56),
            ChuteTrayX + 14, ChuteTrayY + 14, 10, Palette.ClearanceTeal);
        DrawModalCloseBtn(mouse, ChuteTrayX + ChuteTrayW - 36, ChuteTrayY + 8);

        if (_gameState.ServiceChuteItems.Count == 0)
        {
            UiText.Draw("SERVICE CHUTE TRAY IS EMPTY.", ChuteTrayX + 240, ChuteTrayY + 160, 10, Palette.WeatheredGunmetalEdge);
            int pending = _gameState.PendingChuteDeliveries.Count;
            string sub = pending > 0
                ? $"{pending} ITEM(S) QUEUED FOR NEXT 0500 OVERNIGHT DROP (AFTER SLEEP)."
                : "CARRYABLE GEAR ORDERED ON DECK 4 ARRIVES HERE AT 0500.";
            int sw = UiText.MeasureWidth(sub, 10);
            UiText.Draw(sub, ChuteTrayX + (ChuteTrayW - sw) / 2, ChuteTrayY + 184, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            const int visibleRows = 6;
            int maxScroll = Math.Max(0, _gameState.ServiceChuteItems.Count - visibleRows);
            _chuteScroll = Math.Clamp(_chuteScroll, 0, maxScroll);
            int rowW = maxScroll > 0 ? (ChuteTrayW - 44) : (ChuteTrayW - 32);

            for (int r = 0; r < visibleRows; r++)
            {
                int idx = _chuteScroll + r;
                if (idx >= _gameState.ServiceChuteItems.Count)
                    break;

                ContrabandPocketItem item = _gameState.ServiceChuteItems[idx];
                int ry = ChuteTrayY + 66 + r * 44;
                bool rowHov = mouse.IsHit(ChuteTrayX + 16, ry, rowW, 38);
                Raylib.DrawRectangle(ChuteTrayX + 16, ry, rowW, 38, rowHov ? new Color(24, 34, 42, 255) : new Color(18, 24, 30, 255));
                Raylib.DrawRectangleLines(ChuteTrayX + 16, ry, rowW, 38, rowHov ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn);

                int textMaxW = Math.Max(40, rowW - 312);
                UiText.Draw(UiText.TruncateToWidth(item.DisplayName, 10, textMaxW), ChuteTrayX + 26, ry + 7, 10, Palette.CrtAmberPrimary);
                string desc = item.IsMissionSlate
                    ? $"TARGET: {item.MissionTargetReg} ({item.TargetShipName})"
                    : "CLICK TO MOVE INTO COAT POCKET";
                UiText.Draw(UiText.TruncateToWidth(desc, 10, textMaxW), ChuteTrayX + 26, ry + 21, 10, Palette.WeatheredGunmetalEdge);

                string primaryLbl = item.ItemType == ContrabandPocketItem.TypeShieldedPocketLining
                    ? "INSTALL LINING"
                    : "-> COAT POCKET";
                int primaryX = ChuteTrayX + 16 + rowW - 288;
                DrawSmallModalBtn(mouse, primaryX, ry + 4, 136, 30, primaryLbl, Palette.ClearanceTeal);

                if (item.IsContraband)
                {
                    DrawSmallModalBtn(mouse, ChuteTrayX + 16 + rowW - 144, ry + 4, 136, 30, "STASH IN VENT", Palette.CrtAmberPrimary);
                }
            }

            if (maxScroll > 0)
            {
                DrawModalScrollbar(mouse, ChuteTrayX + ChuteTrayW - 24, ChuteTrayY + 66, visibleRows * 44 - 6, _chuteScroll, maxScroll);
            }
        }

        DrawSmallModalBtn(mouse, ChuteTrayX + ChuteTrayW - 156, ChuteTrayY + ChuteTrayH - 44, 140, 32, "CLOSE [ESC]", Palette.CrtAmberPrimary);
    }

    private void DrawVentStashDrawer(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 148));
        Raylib.DrawRectangle(VentDrawerX, VentDrawerY, VentDrawerW, VentDrawerH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(VentDrawerX, VentDrawerY, VentDrawerW, VentDrawerH, Palette.CrtAmberPrimary);

        Raylib.DrawRectangle(VentDrawerX + 2, VentDrawerY + 2, VentDrawerW - 4, 34, new Color(26, 22, 16, 255));
        string shieldNote = _gameState.HasShieldedPocketLining ? " | SHIELDED LINING: ACTIVE (CONCEALS 1 COAT ITEM)" : string.Empty;
        UiText.Draw($"UNDER-DESK VENT STASH // CLICK ITEMS TO TRANSFER{shieldNote}", VentDrawerX + 14, VentDrawerY + 14, 10, Palette.CrtAmberPrimary);
        DrawModalCloseBtn(mouse, VentDrawerX + VentDrawerW - 36, VentDrawerY + 8);

        const int visibleRows = 5;
        int maxPocketScroll = Math.Max(0, _gameState.PocketItems.Count - visibleRows);
        int maxVentScroll = Math.Max(0, _gameState.VentStashItems.Count - visibleRows);
        _ventPocketScroll = Math.Clamp(_ventPocketScroll, 0, maxPocketScroll);
        _ventStashScroll = Math.Clamp(_ventStashScroll, 0, maxVentScroll);

        // Left Column: Coat Pocket (carried to Booth 04; frisked during sleep raid)
        UiText.Draw($"COAT POCKET ({_gameState.PocketItems.Count}/{_gameState.PocketCapacity}) -- CLICK TO STASH IN VENT",
            VentDrawerX + 16, VentDrawerY + 50, 10, Palette.ClearanceTeal);
        Raylib.DrawRectangle(VentDrawerX + 16, VentDrawerY + 68, 372, 268, new Color(12, 16, 22, 255));
        Raylib.DrawRectangleLines(VentDrawerX + 16, VentDrawerY + 68, 372, 268, Palette.WeatheredGunmetalEdge);

        int leftRowW = maxPocketScroll > 0 ? 344 : 360;
        for (int r = 0; r < visibleRows; r++)
        {
            int idx = _ventPocketScroll + r;
            if (idx >= _gameState.PocketItems.Count)
                break;

            ContrabandPocketItem item = _gameState.PocketItems[idx];
            int ry = VentDrawerY + 76 + r * 48;
            bool rowHov = mouse.IsHit(VentDrawerX + 22, ry, leftRowW, 42);
            Raylib.DrawRectangle(VentDrawerX + 22, ry, leftRowW, 42, rowHov ? new Color(28, 36, 46, 255) : new Color(20, 26, 34, 255));
            Raylib.DrawRectangleLines(VentDrawerX + 22, ry, leftRowW, 42, rowHov ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
            UiText.Draw(TruncateText(item.DisplayName, 10, 220), VentDrawerX + 28, ry + 7, 10, Palette.CrtAmberPrimary);
            UiText.Draw(string.IsNullOrEmpty(item.TargetShipRegistration) ? item.ItemType : item.TargetShipRegistration,
                VentDrawerX + 28, ry + 23, 10, Palette.WeatheredGunmetalEdge);

            DrawSmallModalBtn(mouse, VentDrawerX + 22 + leftRowW - 110, ry + 6, 104, 30, "STASH ->", Palette.CrtAmberPrimary);
        }

        if (maxPocketScroll > 0)
        {
            DrawModalScrollbar(mouse, VentDrawerX + 16 + 372 - 16, VentDrawerY + 72, 260, _ventPocketScroll, maxPocketScroll);
        }

        // Right Column: Vent Stash (safe on clean-log standard sweeps; seized on dirty-log deep sweeps)
        UiText.Draw($"VENT STASH ({_gameState.VentStashItems.Count}/{_config.VentStashCapacity}) -- CLICK TO RETRIEVE TO COAT",
            VentDrawerX + 412, VentDrawerY + 50, 10, Palette.CrtAmberPrimary);
        Raylib.DrawRectangle(VentDrawerX + 412, VentDrawerY + 68, 372, 268, new Color(12, 16, 22, 255));
        Raylib.DrawRectangleLines(VentDrawerX + 412, VentDrawerY + 68, 372, 268, Palette.OxidizedBronzeWorn);

        int rightRowW = maxVentScroll > 0 ? 344 : 360;
        for (int r = 0; r < visibleRows; r++)
        {
            int idx = _ventStashScroll + r;
            if (idx >= _gameState.VentStashItems.Count)
                break;

            ContrabandPocketItem item = _gameState.VentStashItems[idx];
            int ry = VentDrawerY + 76 + r * 48;
            bool rowHov = mouse.IsHit(VentDrawerX + 418, ry, rightRowW, 42);
            Raylib.DrawRectangle(VentDrawerX + 418, ry, rightRowW, 42, rowHov ? new Color(32, 28, 22, 255) : new Color(22, 20, 16, 255));
            Raylib.DrawRectangleLines(VentDrawerX + 418, ry, rightRowW, 42, rowHov ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn);
            UiText.Draw(TruncateText(item.DisplayName, 10, 220), VentDrawerX + 424, ry + 7, 10, Palette.CrtAmberPrimary);
            UiText.Draw(string.IsNullOrEmpty(item.TargetShipRegistration) ? item.ItemType : item.TargetShipRegistration,
                VentDrawerX + 424, ry + 23, 10, Palette.WeatheredGunmetalEdge);
            DrawSmallModalBtn(mouse, VentDrawerX + 418 + rightRowW - 106, ry + 6, 100, 30, "<- TAKE", Palette.ClearanceTeal);
        }

        if (maxVentScroll > 0)
        {
            DrawModalScrollbar(mouse, VentDrawerX + 412 + 372 - 16, VentDrawerY + 72, 260, _ventStashScroll, maxVentScroll);
        }

        int unscrubbed = Math.Max(_gameState.UnscrubbedLogCount, _gameState.UnauthorizedLogCount);
        string ventNote = unscrubbed > 0
            ? $"WARNING: {unscrubbed} UNSCRUBBED LOG(S) -- SCRUB IN CRYPTO / LOGS OR RAID WILL BREACH VENT!"
            : "LOGS CLEAN: VENT STASH STAYS UNDISCOVERED DURING STANDARD KORDANI SWEEPS.";
        UiText.Draw(ventNote, VentDrawerX + 16, VentDrawerY + VentDrawerH - 32, 10,
            unscrubbed > 0 ? Palette.AlertCrimson : Palette.ClearanceTeal);
        DrawSmallModalBtn(mouse, VentDrawerX + VentDrawerW - 156, VentDrawerY + VentDrawerH - 44, 140, 32, "CLOSE [ESC]", Palette.ClearanceTeal);
    }

    private static void DrawModalScrollbar(VirtualMouseState mouse, int x, int y, int h, int scroll, int maxScroll)
    {
        bool upHov = mouse.IsHit(x, y, 14, 18);
        bool dnHov = mouse.IsHit(x, y + h - 18, 14, 18);
        Raylib.DrawRectangle(x, y, 14, 18, upHov ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, 14, 18, Palette.CrtAmberPrimary);
        UiText.Draw("^", x + 4, y + 4, 10, Palette.CrtAmberPrimary);

        Raylib.DrawRectangle(x, y + h - 18, 14, 18, dnHov ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y + h - 18, 14, 18, Palette.CrtAmberPrimary);
        UiText.Draw("v", x + 4, y + h - 14, 10, Palette.CrtAmberPrimary);

        int trackY = y + 20;
        int trackH = Math.Max(20, h - 40);
        Raylib.DrawRectangle(x + 4, trackY, 6, trackH, Palette.OutlineVoid);
        int thumbH = Math.Max(14, trackH / (maxScroll + 2));
        int thumbY = trackY + (maxScroll > 0 ? (int)MathF.Round((trackH - thumbH) * (scroll / (float)maxScroll)) : 0);
        Raylib.DrawRectangle(x + 3, thumbY, 8, thumbH, Palette.CrtAmberPrimary);
    }

    private static void DrawBerthAuditModal(VirtualMouseState mouse, BerthAuditResolution audit)
    {
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 4, 6, 214));
        Raylib.DrawRectangle(AuditModalX, AuditModalY, AuditModalW, AuditModalH, new Color(14, 10, 12, 255));
        Color frameCol = audit.WasCaught ? Palette.AlertCrimson : Palette.CrtAmberPrimary;
        Raylib.DrawRectangleLines(AuditModalX, AuditModalY, AuditModalW, AuditModalH, frameCol);
        Raylib.DrawRectangleLines(AuditModalX + 3, AuditModalY + 3, AuditModalW - 6, AuditModalH - 6, Palette.BrownDwarfGlowRim);

        Raylib.DrawRectangle(AuditModalX + 6, AuditModalY + 6, AuditModalW - 12, 42, new Color(56, 16, 18, 255));
        string hdr = audit.Kind == BerthAuditKind.ThreeStrikeBerthAudit
            ? "03:15 WAKE-UP RAID // KORDANI TWO-TIER 3-STRIKE BERTH AUDIT"
            : "03:15 WAKE-UP RAID // KORDANI STELLAR STOKER SABOTAGE AUDIT";
        int hw = UiText.MeasureWidth(hdr, 10);
        UiText.Draw(hdr, AuditModalX + (AuditModalW - hw) / 2, AuditModalY + 22, 10, Palette.CrtAmberPrimary);

        UiText.Draw("KORDANI SECURITY TROOPERS UNSEALED BERTH 04 DURING SLEEP CYCLE.",
            AuditModalX + 24, AuditModalY + 62, 10, Palette.AlertCrimson);

        Raylib.DrawRectangle(AuditModalX + 24, AuditModalY + 86, AuditModalW - 48, 344, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(AuditModalX + 24, AuditModalY + 86, AuditModalW - 48, 344, Palette.WeatheredGunmetalEdge);

        int ly = AuditModalY + 104;
        foreach (string line in audit.InspectionLogLines)
        {
            Color lCol = line.Contains("VIOLATION") || line.Contains("SEVERE") || line.Contains("BREACHED") || line.Contains("CONFISCATED")
                ? Palette.AlertCrimson
                : (line.Contains("CLEAN") || line.Contains("CLEAR") || line.Contains("CONCEALED") || line.Contains("UNDISCOVERED")
                    ? Palette.ClearanceTeal
                    : Palette.CrtAmberPrimary);
            UiText.Draw(TruncateText(line, 10, AuditModalW - 76), AuditModalX + 38, ly, 10, lCol);
            ly += 30;
        }

        string btnLabel = audit.WasCaught
            ? "[ SECURITY DETAINMENT -- VIEW VERDICT ]"
            : "[ CLEAN AUDIT -- PROCEED TO MORNING QUARTERS ]";
        DrawSmallModalBtn(mouse, AuditModalX + (AuditModalW - 380) / 2, AuditModalY + AuditModalH - 58, 380, 42, btnLabel,
            audit.WasCaught ? Palette.AlertCrimson : Palette.ClearanceTeal);
    }

    private void DrawFailureEndingScreen(VirtualMouseState mouse)
    {
        bool isDetainment = _gameState.EndingFlags.TryGetValue("Fail_SecurityDetainment", out bool sec) && sec;
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 4, 6, 240));
        Raylib.DrawRectangle(280, 140, 720, 440, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(280, 140, 720, 440, Palette.AlertCrimson);

        string title = isDetainment
            ? "FAILURE ENDING // SECURITY DETAINMENT"
            : "FAILURE ENDING // DEBT FORECLOSURE";
        int tw = UiText.MeasureWidth(title, 20);
        UiText.Draw(title, 280 + (720 - tw) / 2, 184, 20, Palette.AlertCrimson);

        string body1 = isDetainment
            ? "KORDANI SECURITY TROOPERS BREACHED BERTH 04 AIRLOCK AFTER SEIZING CONTRABAND / LOGS."
            : "TWO MISSED NIGHTS OF BERTH LEASE TRIGGERED BRENGOP MERCANTILE FORECLOSURE.";
        string body2 = isDetainment
            ? "PORT OFFICER CREDENTIALS REVOKED. TRANSFERRED TO KORR-VATH DETENTION BLOCK."
            : "PORT OFFICER BADGE STRIPPED. PERMANENTLY REASSIGNED TO LOWER REACTOR DECKS.";

        UiText.Draw(body1, 280 + (720 - UiText.MeasureWidth(body1, 10)) / 2, 268, 10, Palette.CrtAmberPrimary);
        UiText.Draw(body2, 280 + (720 - UiText.MeasureWidth(body2, 10)) / 2, 294, 10, Palette.CrtAmberDimmed);
        UiText.Draw($"FINAL RECORD: DAY {_gameState.Day:D2}  |  SCRIP: {_gameState.PactScrip} SC  |  STRIKES: {_gameState.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}",
            416, 356, 10, Palette.WeatheredGunmetalEdge);

        DrawSmallModalBtn(mouse, 460, 500, 360, 44, "[ RETURN TO MAIN MENU ]", Palette.CrtAmberPrimary);
    }

    private static void DrawModalCloseBtn(VirtualMouseState mouse, int x, int y)
    {
        bool hov = mouse.IsHit(x, y, 28, 24);
        Raylib.DrawRectangle(x, y, 28, 24, hov ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, 28, 24, Palette.AlertCrimson);
        UiText.Draw("X", x + 10, y + 7, 10, hov ? Palette.OutlineVoid : Palette.AlertCrimson);
    }

    private static void DrawSmallModalBtn(VirtualMouseState mouse, int x, int y, int w, int h, string label, Color accent)
    {
        bool hov = mouse.IsHit(x, y, w, h);
        Raylib.DrawRectangle(x, y, w, h, hov ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(x, y, w, h, hov ? Palette.CrtAmberPrimary : accent);
        int tw = UiText.MeasureWidth(label, 10);
        UiText.Draw(label, x + Math.Max(4, (w - tw) / 2), y + (h - 10) / 2, 10, hov ? Palette.CrtAmberPrimary : accent);
    }

    private static string TruncateText(string value, int fontSize, int maxPx)
    {
        if (string.IsNullOrEmpty(value) || maxPx <= 0)
            return string.Empty;
        if (UiText.MeasureWidth(value, fontSize) <= maxPx)
            return value;
        int len = value.Length;
        while (len > 1 && UiText.MeasureWidth(value[..len], fontSize) > maxPx)
            len--;
        return value[..len];
    }

    public void Unload() { }
}
