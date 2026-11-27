using System.Text.RegularExpressions;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

/// <summary>
/// Centered Personal Terminal Modal at exact (80, 48, 1120, 624) -- 80px left/right margin,
/// 48px top/bottom margin -- rendered over the live, dimmed, unclickable Quarters room backdrop.
/// - Days 1-5 (TerminalHackedUnlocked == false): Clean 3-Tab Official Terminal on the left
///   (MESSAGES | FORUM | PACT REQ, cool slate-blue accent); unofficial tabs completely hidden &amp; unclickable.
/// - Day 6+ Hack Reveal: Clicking the anomalous unread "CHECK YOUR CHUTE" transmission plays a ~1.0s
///   diegetic glitch/hijack sequence and unlocks the two unofficial tabs on the right
///   (CRYPTO / LOGS | DECK 4, deep indigo / purple-blue accent), auto-activating the Day 6 MNG- intercept job.
/// - Missions-Only Espionage: Strictly Type 1 (Covert Delivery) and Type 2 (Intercept &amp; Transmit),
///   with direct [TRANSMIT -- +XX SC] dispatch in CRYPTO / LOGS and Option A 50 SC Audit Deferral on DECK 4.
/// </summary>
public sealed class TerminalOverlay
{
    // Centered Modal Frame: 80px left/right margin, 48px top/bottom margin on 1280x720
    public const int FrameX = 80;
    public const int FrameY = 48;
    public const int FrameW = 1120;
    public const int FrameH = 624;

    // Day 6 Terminal Hack Glitch Reveal Duration (~1.0s)
    public const float HackGlitchDurationSeconds = 1.00f;

    // Top 5-Tab Bar + Official/Unofficial Divider + [X] Close Button (y = 56..96, h = 40)
    private const int TabY = 56;
    private const int TabH = 40;
    private const int TabDividerX = 704;
    public static readonly (int X, int W, string Label, bool IsUnofficial)[] TabSpecs =
    [
        (88, 196, "MESSAGES", false),
        (292, 196, "FORUM", false),
        (496, 196, "PACT REQ", false),
        (716, 212, "CRYPTOGRAPHY", true),
        (936, 200, "DECK 4", true)
    ];
    private const int CloseBtnX = 1144;
    private const int CloseBtnY = 56;
    private const int CloseBtnW = 48;
    private const int CloseBtnH = 40;

    // Active Tab Content Viewport (88, 104, 1104, 512 -> y = 104..616)
    private const int ViewX = 88;
    private const int ViewY = 104;
    private const int ViewW = 1104;
    private const int ViewH = 512;

    // Bottom Terminal Status Bar (88, 624, 1104, 40 -> y = 624..664)
    private const int FooterX = 88;
    private const int FooterY = 624;
    private const int FooterW = 1104;
    private const int FooterH = 40;

    // Tab 0: MESSAGES (y = 112..608)
    private const int InboxX = 96;
    private const int InboxY = 112;
    private const int InboxW = 344;
    private const int InboxH = 496;
    private const int ReaderX = 448;
    private const int ReaderY = 112;
    private const int ReaderW = 736;
    private const int ReaderH = 496;

    // Tab 1: FORUM (y = 112..608)
    private const int ForumHdrX = 96;
    private const int ForumHdrY = 112;
    private const int ForumHdrW = 1088;
    private const int ForumHdrH = 44;
    private const int ForumListX = 96;
    private const int ForumListY = 164;
    private const int ForumListW = 376;
    private const int ForumListH = 444;
    private const int ForumStreamX = 480;
    private const int ForumStreamY = 164;
    private const int ForumStreamW = 704;
    private const int ForumStreamH = 444;

    // Tab 2: PACT REQ rows inside (88, 104, 1104, 512)
    private const int PactRowX = 104;
    private const int PactRowW = 1072;
    private const int PactBtnW = 184;
    private const int PactBtnX = PactRowX + PactRowW - PactBtnW - 12;

    // Tab 3: CRYPTO / LOGS (y = 112..608)
    private const int CryptoLeftX = 96;
    private const int CryptoLeftY = 112;
    private const int CryptoLeftW = 464;
    private const int CryptoLeftH = 496;
    private const int CryptoRightX = 568;
    private const int CryptoRightY = 112;
    private const int CryptoRightW = 616;
    private const int CryptoRightH = 496;

    private const int DispatchBoxX = CryptoLeftX + 12;
    private const int DispatchBoxY = CryptoLeftY + 186;
    private const int DispatchBoxW = CryptoLeftW - 24;
    private const int DispatchBoxH = 298;

    // Tab 4: DECK 4 (y = 192..604)
    private const int Deck4LeftX = 104;
    private const int Deck4LeftY = 192;
    private const int Deck4LeftW = 536;
    private const int Deck4LeftH = 412;
    private const int Deck4RightX = 656;
    private const int Deck4RightY = 192;
    private const int Deck4RightW = 520;
    private const int Deck4RightH = 412;

    private readonly GameState _gameState;
    private readonly Core.ConfigFlags _config;
    private readonly EconomyEngine _economy;
    private readonly IntelEconomy _intelEconomy;
    private readonly WorldSim _worldSim;

    private ForumDatabase _forumDb = new();
    private CovertMissionDatabase _covertMissionDb = CovertMissionDatabase.Load();
    private List<ForumThreadModel> _visibleForumThreads = [];

    private int _activeTab;
    private string? _selectedEmailId;
    private int _inboxScroll;
    private int _readerScroll;

    private int _selectedForumThreadIndex;
    private int _forumListScroll;
    private int _forumPostScroll;

    private int _logScroll;
    private int _dispatchScroll;
    private int _deck4ContractScroll;
    private float _visualTime;

    // Day 6 Glitch / Hijack Reveal Animation State
    private bool _hackGlitchActive;
    private float _hackGlitchElapsed;

    // Transient status feedback line inside bottom status bar
    private string _statusMessage = string.Empty;
    private Color _statusColor = Palette.ClearanceTeal;
    private float _statusTimer;

    public bool IsOpen { get; private set; }
    public int ActiveTab => _activeTab;
    public string? SelectedEmailId => _selectedEmailId;
    public bool HasSelectedEmail => !string.IsNullOrEmpty(_selectedEmailId);
    public bool IsHackGlitchActive => _hackGlitchActive;
    public float HackGlitchProgress => _hackGlitchActive
        ? Math.Clamp(_hackGlitchElapsed / HackGlitchDurationSeconds, 0f, 1f)
        : 0f;

    public TerminalOverlay(
        GameState gameState,
        Core.ConfigFlags config,
        EconomyEngine economy,
        IntelEconomy intelEconomy,
        WorldSim worldSim)
    {
        _gameState = gameState;
        _config = config;
        _economy = economy;
        _intelEconomy = intelEconomy;
        _worldSim = worldSim;
    }

    public bool IsTabAccessible(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabSpecs.Length)
            return false;
        return !TabSpecs[tabIndex].IsUnofficial || _gameState.TerminalHackedUnlocked;
    }

    public void Open(int initialTab = 0)
    {
        IsOpen = true;
        int clamped = Math.Clamp(initialTab, 0, TabSpecs.Length - 1);
        _activeTab = IsTabAccessible(clamped) ? clamped : 0;

        // MESSAGES MUST NEVER auto-select or auto-open an email on Open!
        _selectedEmailId = null;
        _inboxScroll = 0;
        _readerScroll = 0;
        _forumListScroll = 0;
        _forumPostScroll = 0;
        _logScroll = 0;
        _dispatchScroll = 0;
        _deck4ContractScroll = 0;
        _hackGlitchActive = false;
        _hackGlitchElapsed = 0f;
        _statusMessage = string.Empty;
        _statusTimer = 0f;

        RefreshForumThreads();
        _covertMissionDb = CovertMissionDatabase.Load();
        AudioHooks.Play("terminal_power_on");
    }

    public void Close()
    {
        if (_hackGlitchActive)
        {
            CompleteHackGlitchUnlock();
        }
        IsOpen = false;
        _selectedEmailId = null;
        AudioHooks.Play("terminal_power_off");
    }

    public void SwitchTabForSmoke(int tabIndex, bool selectFirstEmail = false)
    {
        int clamped = Math.Clamp(tabIndex, 0, TabSpecs.Length - 1);
        int effectiveTab = IsTabAccessible(clamped) ? clamped : 0;

        if (!IsOpen)
        {
            Open(effectiveTab);
        }
        else
        {
            _activeTab = effectiveTab;
            if (_activeTab == 0)
            {
                _selectedEmailId = null;
                _readerScroll = 0;
            }
        }

        if (_activeTab == 0 && selectFirstEmail)
        {
            List<TerminalEmail> sorted = GetSortedInboxEmails();
            if (sorted.Count > 0)
            {
                OpenEmailExplicitly(sorted[0]);
            }
        }
        else if (_activeTab == 1)
        {
            RefreshForumThreads();
        }
    }

    internal bool SmokeClickEmailById(string emailId)
    {
        List<TerminalEmail> sorted = GetSortedInboxEmails();
        int idx = sorted.FindIndex(e => e.EmailId.Equals(emailId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return false;

        OpenEmailExplicitly(sorted[idx]);
        return true;
    }

    /// <summary>
    /// ESC closes the Terminal overlay cleanly before Pause Menu handles ESC.
    /// </summary>
    public bool TryConsumeEscape()
    {
        if (!IsOpen)
            return false;

        Close();
        return true;
    }

    public void RefreshForumThreads()
    {
        _forumDb = ForumDatabase.Load();
        _visibleForumThreads = _economy.GetVisibleForumThreads(_gameState, _forumDb, _config);
        if (_selectedForumThreadIndex >= _visibleForumThreads.Count)
        {
            _selectedForumThreadIndex = 0;
        }
    }

    /// <summary>
    /// Unread emails sort cleanly to the top while preserving newest-first order within Unread and Read groups.
    /// </summary>
    public List<TerminalEmail> GetSortedInboxEmails() =>
        _gameState.Emails
            .OrderBy(e => e.IsRead ? 1 : 0)
            .ToList();

    private TerminalEmail? FindSelectedEmail()
    {
        if (string.IsNullOrEmpty(_selectedEmailId))
            return null;

        return _gameState.Emails.FirstOrDefault(
            e => e.EmailId.Equals(_selectedEmailId, StringComparison.OrdinalIgnoreCase));
    }

    private void OpenEmailExplicitly(TerminalEmail mail)
    {
        _selectedEmailId = mail.EmailId;
        _readerScroll = 0;

        bool changed = false;
        if (!mail.IsRead)
        {
            mail.IsRead = true;
            changed = true;
        }

        if ((mail.IsDay1BrengopEmail || mail.EmailId.Equals(TerminalEmail.Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase)) &&
            !_gameState.Day1BrengopEmailRead)
        {
            _gameState.Day1BrengopEmailRead = true;
            changed = true;
        }

        // Day 6 Terminal Hijack Sequence fires ONLY when the player explicitly clicks CHECK YOUR CHUTE!
        if (mail.IsTerminalHackEmail && !_gameState.TerminalHackedUnlocked && !_hackGlitchActive)
        {
            _hackGlitchActive = true;
            _hackGlitchElapsed = 0f;
            AudioHooks.Play("detain_alarm");
        }

        if (changed)
        {
            _gameState.SaveToDisk();
        }
    }

    private void CompleteHackGlitchUnlock()
    {
        _hackGlitchActive = false;
        _hackGlitchElapsed = 0f;
        _economy.ActivateDay6IntroHackMission(_gameState);
        SetStatus("PORT 04 TUNNEL MOUNTED -- [CRYPTO / LOGS] & [DECK 4] UNLOCKED.", isError: false);
        AudioHooks.Play("terminal_key_confirm");
    }

    private void SetStatus(string message, bool isError = false)
    {
        _statusMessage = message;
        _statusColor = isError ? Palette.AlertCrimson : Palette.ClearanceTeal;
        _statusTimer = 5.0f;
    }

    public void Update(float deltaTime, VirtualMouseState mouse)
    {
        if (!IsOpen)
            return;

        _visualTime += deltaTime;
        if (_statusTimer > 0f)
        {
            _statusTimer = Math.Max(0f, _statusTimer - deltaTime);
            if (_statusTimer <= 0f)
                _statusMessage = string.Empty;
        }

        // Guard if F1 Debug toggled TerminalHackedUnlocked OFF while on tab 3/4
        if (!IsTabAccessible(_activeTab))
        {
            _activeTab = 0;
            _selectedEmailId = null;
        }

        // Advance Day 6 ~1.0s Glitch / Hijack Reveal Sequence
        if (_hackGlitchActive)
        {
            _hackGlitchElapsed += deltaTime;
            if (_hackGlitchElapsed >= HackGlitchDurationSeconds)
            {
                CompleteHackGlitchUnlock();
            }
            return;
        }

        // Scroll wheel handling for active tab panes
        if (MathF.Abs(mouse.WheelMove) > 0.01f)
        {
            int step = mouse.WheelMove > 0 ? -2 : 2;
            HandleTabMouseWheel(mouse, step);
        }

        if (!mouse.LeftPressed)
            return;

        // Close [X] button (1144, 56, 48, 40)
        if (mouse.IsHit(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH))
        {
            Close();
            return;
        }

        // Top Tab Buttons (unofficial tabs completely unclickable when TerminalHackedUnlocked == false)
        for (int i = 0; i < TabSpecs.Length; i++)
        {
            if (!IsTabAccessible(i))
                continue;

            if (mouse.IsHit(TabSpecs[i].X, TabY, TabSpecs[i].W, TabH))
            {
                _activeTab = i;
                AudioHooks.Play("terminal_key_tab");
                if (_activeTab == 0)
                {
                    // Switching to MESSAGES NEVER auto-selects or auto-opens an email!
                    _selectedEmailId = null;
                    _readerScroll = 0;
                }
                else if (_activeTab == 1)
                {
                    RefreshForumThreads();
                }
                return;
            }
        }

        // Unclickable live Quarters room backdrop outside frame absorbs nothing
        if (!mouse.IsHit(FrameX, FrameY, FrameW, FrameH))
            return;

        switch (_activeTab)
        {
            case 0:
                HandleMessagesInput(mouse);
                break;
            case 1:
                HandleForumInput(mouse);
                break;
            case 2:
                HandlePactReqInput(mouse);
                break;
            case 3:
                HandleCryptoLogsInput(mouse);
                break;
            case 4:
                HandleDeck4Input(mouse);
                break;
        }
    }

    private void HandleTabMouseWheel(VirtualMouseState mouse, int step)
    {
        switch (_activeTab)
        {
            case 0:
                if (mouse.IsHit(InboxX, InboxY, InboxW, InboxH))
                {
                    int maxInbox = Math.Max(0, _gameState.Emails.Count - 8);
                    _inboxScroll = Math.Clamp(_inboxScroll + step, 0, maxInbox);
                }
                else if (mouse.IsHit(ReaderX, ReaderY, ReaderW, ReaderH))
                {
                    _readerScroll = Math.Max(0, _readerScroll + step);
                }
                break;

            case 1:
                if (mouse.IsHit(ForumListX, ForumListY, ForumListW, ForumListH))
                {
                    int maxThreads = Math.Max(0, _visibleForumThreads.Count - 7);
                    _forumListScroll = Math.Clamp(_forumListScroll + step, 0, maxThreads);
                }
                else if (mouse.IsHit(ForumStreamX, ForumStreamY, ForumStreamW, ForumStreamH))
                {
                    _forumPostScroll = Math.Max(0, _forumPostScroll + step);
                }
                break;

            case 3:
                if (mouse.IsHit(CryptoRightX, CryptoRightY, CryptoRightW, CryptoRightH))
                {
                    int maxLogs = Math.Max(0, _gameState.SecurityLogs.Count - 17);
                    _logScroll = Math.Clamp(_logScroll + step, 0, maxLogs);
                }
                else if (mouse.IsHit(DispatchBoxX, DispatchBoxY, DispatchBoxW, DispatchBoxH))
                {
                    int maxDisp = Math.Max(0, _intelEconomy.GetActiveInterceptDispatches(_gameState, _covertMissionDb).Count - 3);
                    _dispatchScroll = Math.Clamp(_dispatchScroll + Math.Sign(step), 0, maxDisp);
                }
                break;

            case 4:
                if (mouse.IsHit(Deck4LeftX, Deck4LeftY, Deck4LeftW, 186))
                {
                    int maxContracts = Math.Max(0, _economy.GetDeck4ContractListings(_gameState, _covertMissionDb).Count - 2);
                    _deck4ContractScroll = Math.Clamp(_deck4ContractScroll + Math.Sign(step), 0, maxContracts);
                }
                break;
        }
    }

    // =========================================================================
    // TAB 0: MESSAGES INPUT (Unread-First Sorted List, Explicit Click to Read)
    // =========================================================================
    private void HandleMessagesInput(VirtualMouseState mouse)
    {
        List<TerminalEmail> sorted = GetSortedInboxEmails();
        int maxInboxScroll = Math.Max(0, sorted.Count - 8);
        _inboxScroll = Math.Clamp(_inboxScroll, 0, maxInboxScroll);

        int inboxBarX = InboxX + InboxW - 18;
        if (mouse.IsHit(inboxBarX, InboxY + 28, 16, 16))
        {
            _inboxScroll = Math.Max(0, _inboxScroll - 1);
            AudioHooks.Play("terminal_key_click");
            return;
        }
        if (mouse.IsHit(inboxBarX, InboxY + InboxH - 18, 16, 16))
        {
            _inboxScroll = Math.Min(maxInboxScroll, _inboxScroll + 1);
            AudioHooks.Play("terminal_key_click");
            return;
        }

        for (int row = 0; row < 8; row++)
        {
            int idx = _inboxScroll + row;
            if (idx >= sorted.Count)
                break;

            int ry = InboxY + 30 + row * 56;
            if (mouse.IsHit(InboxX + 6, ry, InboxW - 28, 52))
            {
                OpenEmailExplicitly(sorted[idx]);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int rBarX = ReaderX + ReaderW - 18;
        if (mouse.IsHit(rBarX, ReaderY + 84, 16, 16))
        {
            _readerScroll = Math.Max(0, _readerScroll - 2);
            AudioHooks.Play("terminal_key_click");
            return;
        }
        if (mouse.IsHit(rBarX, ReaderY + ReaderH - 18, 16, 16))
        {
            _readerScroll += 2;
            AudioHooks.Play("terminal_key_click");
            return;
        }

        TerminalEmail? selectedMail = FindSelectedEmail();
        if (selectedMail != null && selectedMail.IsEncrypted && _gameState.TerminalHackedUnlocked)
        {
            if (mouse.IsHit(ReaderX + 16, ReaderY + ReaderH - 48, 320, 32))
            {
                _activeTab = 3;
                AudioHooks.Play("terminal_key_tab");
                return;
            }
        }
    }

    // =========================================================================
    // TAB 1: FORUM INPUT
    // =========================================================================
    private void HandleForumInput(VirtualMouseState mouse)
    {
        int listBarX = ForumListX + ForumListW - 18;
        if (mouse.IsHit(listBarX, ForumListY + 26, 16, 16))
        {
            _forumListScroll = Math.Max(0, _forumListScroll - 1);
            AudioHooks.Play("terminal_key_click");
            return;
        }
        if (mouse.IsHit(listBarX, ForumListY + ForumListH - 18, 16, 16))
        {
            _forumListScroll = Math.Min(Math.Max(0, _visibleForumThreads.Count - 7), _forumListScroll + 1);
            AudioHooks.Play("terminal_key_click");
            return;
        }

        for (int row = 0; row < 7; row++)
        {
            int idx = _forumListScroll + row;
            if (idx >= _visibleForumThreads.Count)
                break;

            int ry = ForumListY + 28 + row * 58;
            if (mouse.IsHit(ForumListX + 6, ry, ForumListW - 28, 54))
            {
                _selectedForumThreadIndex = idx;
                _forumPostScroll = 0;
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int pBarX = ForumStreamX + ForumStreamW - 18;
        if (mouse.IsHit(pBarX, ForumStreamY + 34, 16, 16))
        {
            _forumPostScroll = Math.Max(0, _forumPostScroll - 2);
            AudioHooks.Play("terminal_key_click");
            return;
        }
        if (mouse.IsHit(pBarX, ForumStreamY + ForumStreamH - 18, 16, 16))
        {
            _forumPostScroll += 2;
            AudioHooks.Play("terminal_key_click");
            return;
        }
    }

    // =========================================================================
    // TAB 2: PACT REQ INPUT (Pure Flat-SC Pricing -- Food Rations Only)
    // =========================================================================
    private void HandlePactReqInput(VirtualMouseState mouse)
    {
        if (mouse.IsHit(PactBtnX, 174, PactBtnW, 34))
        {
            var (ok, msg) = _economy.TryOrderNightlyRation(_gameState, NightlyRationOrder.DryRationBricks, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "food_replicator_chime" : "citation_buzz");
            return;
        }

        if (mouse.IsHit(PactBtnX, 228, PactBtnW, 34))
        {
            var (ok, msg) = _economy.TryOrderNightlyRation(_gameState, NightlyRationOrder.NutrientPaste, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "food_replicator_chime" : "citation_buzz");
            return;
        }

        if (mouse.IsHit(PactBtnX, 282, PactBtnW, 34))
        {
            var (ok, msg) = _economy.TryOrderNightlyRation(_gameState, NightlyRationOrder.HighTierRations, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "food_replicator_chime" : "citation_buzz");
            return;
        }
    }

    // =========================================================================
    // TAB 3: CRYPTO / LOGS INPUT (Streamlined Missions-Only Direct Dispatch)
    // =========================================================================
    private void HandleCryptoLogsInput(VirtualMouseState mouse)
    {
        // Button 1: DECRYPT MESSAGE [0 SC] (logs [UNAUTH_IO])
        if (mouse.IsHit(CryptoLeftX + 12, CryptoLeftY + 38, CryptoLeftW - 24, 44))
        {
            var (ok, msg, _) = _economy.TryDecryptNextMessage(_gameState, _config);
            SetStatus(msg, isError: !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }

        // Button 2: SCRUB LOGS [15 SC] (purges logs so Vent Stash stays safe in Two-Tier Audits)
        if (mouse.IsHit(CryptoLeftX + 12, CryptoLeftY + 90, CryptoLeftW - 24, 44))
        {
            var (ok, msg, _) = _economy.TryScrubSecurityLogs(_gameState, _config);
            SetStatus(msg, isError: !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }

        // Direct Mission Dispatch Panel (Intercept & Transmit)
        List<MissionDispatchMatch> dispatches = _intelEconomy.GetActiveInterceptDispatches(_gameState, _covertMissionDb);
        int maxDispScroll = Math.Max(0, dispatches.Count - 3);
        _dispatchScroll = Math.Clamp(_dispatchScroll, 0, maxDispScroll);

        if (maxDispScroll > 0)
        {
            int dBarX = DispatchBoxX + DispatchBoxW - 18;
            if (mouse.IsHit(dBarX, DispatchBoxY + 42, 16, 16))
            {
                _dispatchScroll = Math.Max(0, _dispatchScroll - 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
            if (mouse.IsHit(dBarX, DispatchBoxY + DispatchBoxH - 18, 16, 16))
            {
                _dispatchScroll = Math.Min(maxDispScroll, _dispatchScroll + 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int cardW = maxDispScroll > 0 ? DispatchBoxW - 30 : DispatchBoxW - 16;
        for (int r = 0; r < 3; r++)
        {
            int idx = _dispatchScroll + r;
            if (idx >= dispatches.Count)
                break;

            MissionDispatchMatch match = dispatches[idx];
            int cy = DispatchBoxY + 44 + r * 80;
            int txBtnW = 166;
            int txBtnX = DispatchBoxX + 8 + cardW - txBtnW - 6;
            int txBtnY = cy + 38;

            if (match.CanTransmit && match.MatchingSlate != null &&
                mouse.IsHit(txBtnX, txBtnY, txBtnW, 28))
            {
                MissionTransmitResult res = _intelEconomy.ExecuteMissionInterceptTransmit(
                    _gameState,
                    _worldSim,
                    match.Mission.MissionId,
                    match.MatchingSlate,
                    match.SlateIsInVentStash,
                    _covertMissionDb,
                    _config);
                SetStatus(res.Message, isError: !res.Success);
                if (!res.Success)
                {
                    AudioHooks.Play("citation_buzz");
                }
                return;
            }
        }

        // Security Log Pane Scroll Arrows
        int logBarX = CryptoRightX + CryptoRightW - 18;
        if (mouse.IsHit(logBarX, CryptoRightY + 42, 16, 16))
        {
            _logScroll = Math.Max(0, _logScroll - 2);
            AudioHooks.Play("terminal_key_click");
            return;
        }
        if (mouse.IsHit(logBarX, CryptoRightY + CryptoRightH - 18, 16, 16))
        {
            _logScroll = Math.Min(Math.Max(0, _gameState.SecurityLogs.Count - 17), _logScroll + 2);
            AudioHooks.Play("terminal_key_click");
            return;
        }
    }

    // =========================================================================
    // TAB 4: DECK 4 INPUT (Contracts & Drops + Option A 50 SC Strike Clearing)
    // =========================================================================
    private void HandleDeck4Input(VirtualMouseState mouse)
    {
        // Top of Left Panel: Covert Contracts Board
        List<EconomyEngine.Deck4ContractListing> contracts = _economy.GetDeck4ContractListings(_gameState, _covertMissionDb);
        int maxContractScroll = Math.Max(0, contracts.Count - 2);
        _deck4ContractScroll = Math.Clamp(_deck4ContractScroll, 0, maxContractScroll);

        if (maxContractScroll > 0)
        {
            int cBarX = Deck4LeftX + Deck4LeftW - 20;
            if (mouse.IsHit(cBarX, Deck4LeftY + 30, 16, 16))
            {
                _deck4ContractScroll = Math.Max(0, _deck4ContractScroll - 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
            if (mouse.IsHit(cBarX, Deck4LeftY + 162, 16, 16))
            {
                _deck4ContractScroll = Math.Min(maxContractScroll, _deck4ContractScroll + 1);
                AudioHooks.Play("terminal_key_click");
                return;
            }
        }

        int cardW = maxContractScroll > 0 ? Deck4LeftW - 34 : Deck4LeftW - 20;
        for (int r = 0; r < 2; r++)
        {
            int idx = _deck4ContractScroll + r;
            if (idx >= contracts.Count)
                break;

            EconomyEngine.Deck4ContractListing listing = contracts[idx];
            int cy = Deck4LeftY + 30 + r * 76;
            int accBtnX = Deck4LeftX + 10 + cardW - 108;
            int accBtnY = cy + 34;

            if (!listing.IsActive && mouse.IsHit(accBtnX, accBtnY, 102, 30))
            {
                var (ok, msg) = _economy.TryAcceptCovertContract(_gameState, listing.Mission.MissionId, _covertMissionDb, _config);
                SetStatus(msg, !ok);
                AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
                return;
            }
        }

        // Bottom of Left Panel: Gear Orders (Blank Slate, Shielded Lining, Pocket Capacity Upgrade)
        int lBtnX = Deck4LeftX + Deck4LeftW - 152;

        // 1. BLANK DATA SLATE [ORDER - 15 SC]
        if (mouse.IsHit(lBtnX, Deck4LeftY + 220, 140, 34))
        {
            var (ok, msg) = _economy.TryOrderBlankDataSlate(_gameState, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }

        // 2. SHIELDED POCKET LINING [ORDER - 90 SC]
        if (mouse.IsHit(lBtnX, Deck4LeftY + 284, 140, 34))
        {
            var (ok, msg) = _economy.TryOrderShieldedPocketLining(_gameState, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }

        // 3. POCKET CAPACITY UPGRADE (D06: 60 SC / D09: 120 SC)
        if (mouse.IsHit(lBtnX, Deck4LeftY + 348, 140, 34))
        {
            var (ok, msg) = _economy.TryPurchasePocketCapacityUpgrade(_gameState, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }

        // Right Column: // BACK-CHANNEL // STRIKE EXPUNGEMENT -> Audit Deferral Fee [50 SC -> -1 STRIKE] (Sole Regular Strike Clearer)
        int rBtnX = Deck4RightX + 16;
        int rBtnW = Deck4RightW - 32;
        if (mouse.IsHit(rBtnX, Deck4RightY + 116, rBtnW, 42))
        {
            var (ok, msg) = _economy.TryPurchaseAuditDeferralFee(_gameState, _config);
            SetStatus(msg, !ok);
            AudioHooks.Play(ok ? "terminal_key_confirm" : "citation_buzz");
            return;
        }
    }

    // =========================================================================
    // RENDERING: Centered Modal (80, 48, 1120, 624) over Dimmed Quarters Room
    // =========================================================================
    public void DrawVirtual(VirtualMouseState mouse)
    {
        if (!IsOpen)
            return;

        // Subtle screen jitter during the Day 6 ~1.0s terminal glitch/hijack reveal
        int jx = 0;
        int jy = 0;
        if (_hackGlitchActive)
        {
            float p = HackGlitchProgress;
            float amp = (1.0f - p) * 4.0f;
            int tick = (int)(_hackGlitchElapsed * 48f);
            jx = (int)MathF.Round(((tick % 3) - 1) * amp);
            jy = (int)MathF.Round((((tick / 2) % 3) - 1) * (amp * 0.5f));
        }

        // Dimmed unclickable Quarters room evenly visible around all four margins (80px L/R, 48px T/B)
        Raylib.DrawRectangle(0, 0, Constants.VirtualWidth, Constants.VirtualHeight, new Color(8, 11, 16, 128));

        // Slim 8px dark industrial bezel at exact (80, 48, 1120, 624)
        Raylib.DrawRectangle(FrameX + 4 + jx, FrameY + 4 + jy, FrameW, FrameH, new Color(4, 6, 8, 200));
        Raylib.DrawRectangle(FrameX + jx, FrameY + jy, FrameW, FrameH, new Color(22, 27, 34, 255));
        Raylib.DrawRectangleLines(FrameX + jx, FrameY + jy, FrameW, FrameH,
            _hackGlitchActive ? Palette.AlertCrimson : Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangleLines(FrameX + 1 + jx, FrameY + 1 + jy, FrameW - 2, FrameH - 2, Palette.OutlineVoid);

        // Draw Active Tab Content strictly inside (88, 104, 1104, 512)
        switch (_activeTab)
        {
            case 0:
                DrawMessagesTab(mouse);
                break;
            case 1:
                DrawForumTab(mouse);
                break;
            case 2:
                DrawPactReqTab(mouse);
                break;
            case 3:
                DrawCryptoLogsTab(mouse);
                break;
            case 4:
                DrawDeck4Tab(mouse);
                break;
        }

        // Draw Top Tab Bar + [X] and Bottom Status Bar AFTER tab content so zero tab elements can ever clip/overlap them
        DrawTopTabBar(mouse, jx, jy);
        DrawStatusFooter();

        // Day 6 Diegetic Glitch / Hijack Overlay inside terminal frame
        if (_hackGlitchActive)
        {
            DrawHackGlitchOverlay(jx, jy);
        }
    }

    private void DrawHackGlitchOverlay(int jx, int jy)
    {
        float p = HackGlitchProgress;
        int tick = (int)(_hackGlitchElapsed * 36f);

        Raylib.BeginScissorMode(FrameX, FrameY, FrameW, FrameH);

        // 1. Brief CRT static tear horizontal slices across the terminal viewport
        int bandCount = p < 0.72f ? 14 : 6;
        for (int i = 0; i < bandCount; i++)
        {
            uint h = Procedural.DeterministicRng.HashSeedFnv1a($"hack_tear_{tick}_{i}");
            int by = FrameY + 48 + (int)(h % (uint)(FrameH - 64));
            int bh = 2 + (int)((h >> 6) % 6);
            int bx = FrameX + 8 + (int)((h >> 12) % 180) + jx;
            int bw = FrameW - 16 - (int)((h >> 18) % 240);
            byte alpha = (byte)Math.Clamp((int)((1.0f - p * 0.65f) * 185f), 25, 200);
            Color c = (i % 3) switch
            {
                0 => new Color((byte)220, (byte)38, (byte)38, alpha),   // Alert crimson tear
                1 => new Color((byte)255, (byte)184, (byte)77, alpha),  // CRT amber tear
                _ => new Color((byte)167, (byte)139, (byte)250, alpha)  // Deep indigo back-channel bleed
            };
            Raylib.DrawRectangle(bx, by + jy, Math.Max(32, bw), bh, c);
        }

        // 2. Amber / red hex scroll flash banner sweeping through the center
        if (p < 0.88f)
        {
            int boxX = FrameX + 168 + jx;
            int boxY = FrameY + 196 + jy;
            int boxW = FrameW - 336;
            int boxH = 196;
            Raylib.DrawRectangle(boxX, boxY, boxW, boxH, new Color(12, 8, 14, 236));
            Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, (tick % 2 == 0) ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
            Raylib.DrawRectangleLines(boxX + 2, boxY + 2, boxW - 4, boxH - 4, Palette.FromHex("#a78bfa"));

            UiText.Draw("[SYS_OVERRIDE // PORT 04 TUNNEL INJECTED]", boxX + 18, boxY + 14, 10, Palette.AlertCrimson);
            Raylib.DrawLine(boxX + 14, boxY + 30, boxX + boxW - 14, boxY + 30, Palette.AlertCrimson);

            string[] hexLines =
            [
                "0xD4:4F9A  INJECT_BUS -> 7E 9A 0F 44 C1 8B 2E 77 A0 5D 19 E4",
                "0xD4:4FAA  BYPASS_AUTH -> KORDANI_WATCHDOG_MASK [OK]",
                "0xD4:4FBA  MOUNT_TAB_03 -> [CRYPTO / LOGS] // CIPHER_DIAG",
                "0xD4:4FCA  MOUNT_TAB_04 -> [DECK 4] // VENT_RELAY_2400",
                "0xD4:4FDA  CONTRACT_SYNC -> DAY 06 MNG- INTERCEPT [45 SC]"
            ];
            int showLines = Math.Clamp((int)(p * (hexLines.Length + 2)), 1, hexLines.Length);
            for (int li = 0; li < showLines; li++)
            {
                Color lineCol = li >= 2 ? Palette.FromHex("#a78bfa") : ((li % 2 == 0) ? Palette.CrtAmberPrimary : Palette.AlertCrimson);
                UiText.Draw(hexLines[li], boxX + 18, boxY + 44 + li * 22, 10, lineCol);
            }

            // Progress bar at bottom of hijack box
            int barX = boxX + 18;
            int barY = boxY + boxH - 26;
            int barW = boxW - 36;
            int fillW = (int)MathF.Round(barW * p);
            Raylib.DrawRectangle(barX, barY, barW, 10, Palette.OutlineVoid);
            if (fillW > 0)
            {
                Raylib.DrawRectangle(barX, barY, fillW, 10, p >= 0.55f ? Palette.FromHex("#a78bfa") : Palette.AlertCrimson);
            }
            Raylib.DrawRectangleLines(barX, barY, barW, 10, Palette.CrtAmberPrimary);
        }

        Raylib.EndScissorMode();
    }

    private void DrawTopTabBar(VirtualMouseState mouse, int jx, int jy)
    {
        // Solid bezel backing behind top bar (y = 50..102) to guarantee crisp separation
        Raylib.DrawRectangle(FrameX + 2 + jx, FrameY + 2 + jy, FrameW - 4, 52, new Color(22, 27, 34, 255));

        bool revealUnofficialDuringGlitch = _hackGlitchActive && HackGlitchProgress >= 0.50f && (((int)(_hackGlitchElapsed * 24f) % 3) != 0 || HackGlitchProgress >= 0.78f);
        bool showUnofficialTabs = _gameState.TerminalHackedUnlocked || revealUnofficialDuringGlitch;

        if (showUnofficialTabs)
        {
            // Clean visual divider between Official Left (MESSAGES | FORUM | PACT REQ) and Unofficial Right (CRYPTO / LOGS | DECK 4)
            Raylib.DrawRectangle(TabDividerX - 2 + jx, TabY + 2 + jy, 4, TabH - 4, Palette.OutlineVoid);
            Raylib.DrawLine(TabDividerX - 1 + jx, TabY + 4 + jy, TabDividerX - 1 + jx, TabY + TabH - 4 + jy, new Color(72, 118, 168, 255));
            Raylib.DrawLine(TabDividerX + 1 + jx, TabY + 4 + jy, TabDividerX + 1 + jx, TabY + TabH - 4 + jy, new Color(118, 92, 196, 255));
            Raylib.DrawRectangle(TabDividerX - 3 + jx, TabY + jy, 6, 3, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(TabDividerX - 3 + jx, TabY + TabH - 3 + jy, 6, 3, Palette.WeatheredGunmetalEdge);
        }

        int unread = _gameState.UnreadEmailCount;
        bool ledPulse = ((int)(_visualTime / 0.6f) % 2) == 0;
        for (int i = 0; i < TabSpecs.Length; i++)
        {
            var (tx, tw, label, isUnofficial) = TabSpecs[i];
            if (isUnofficial && !showUnofficialTabs)
                continue;

            int drawX = tx + jx;
            int drawY = TabY + jy;
            bool selected = _activeTab == i;
            bool hover = !_hackGlitchActive && IsTabAccessible(i) && mouse.IsHit(tx, TabY, tw, TabH);

            Color tabBg = selected
                ? GetTabActiveAccentBg(isUnofficial)
                : (hover
                    ? (isUnofficial ? new Color(32, 24, 56, 255) : new Color(24, 38, 56, 255))
                    : (isUnofficial ? new Color(19, 15, 34, 255) : new Color(14, 21, 32, 255)));
            Color tabBorder = selected
                ? GetTabAccentColor(isUnofficial)
                : (hover
                    ? GetTabAccentColor(isUnofficial)
                    : (isUnofficial ? new Color(78, 58, 134, 255) : new Color(46, 72, 104, 255)));
            Color tabFg = selected
                ? GetTabAccentColor(isUnofficial)
                : (hover
                    ? Color.White
                    : (isUnofficial ? new Color(178, 162, 228, 255) : new Color(156, 184, 216, 255)));

            if (isUnofficial && _hackGlitchActive)
            {
                tabBorder = Palette.FromHex("#a78bfa");
                tabBg = new Color(38, 22, 72, 255);
                tabFg = Color.White;
            }
            else if (i == 2 && !selected &&
                     _gameState.Day == 1 &&
                     _gameState.QuartersPhase == QuartersPhase.Evening &&
                     _gameState.TonightRationOrder == NightlyRationOrder.None)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_visualTime * 3.2f);
                tabBorder = Palette.CrtAmberPrimary;
                tabBg = new Color((byte)(28 + (int)MathF.Round(pulse * 20f)), (byte)(22 + (int)MathF.Round(pulse * 12f)), (byte)12, (byte)255);
                tabFg = Palette.CrtAmberPrimary;
            }

            Raylib.DrawRectangle(drawX, drawY, tw, TabH, tabBg);
            Raylib.DrawRectangleLines(drawX, drawY, tw, TabH, tabBorder);
            if (selected)
            {
                Raylib.DrawRectangle(drawX + 2, drawY + 2, tw - 4, 3, GetTabAccentColor(isUnofficial));
            }

            string display = (i == 0 && unread > 0) ? $"{label} ({unread})" : label;
            int labelW = UiText.MeasureWidth(display, 10);
            int textX = drawX + (tw - labelW) / 2;
            UiText.Draw(display, textX, drawY + 15, 10, tabFg);

            if (i == 0 && unread > 0)
            {
                Raylib.DrawRectangle(drawX + 12, drawY + 16, 8, 8, ledPulse ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            }
        }

        // Close [X] Button: (1144, 56, 48, 40)
        bool closeHover = !_hackGlitchActive && mouse.IsHit(CloseBtnX, CloseBtnY, CloseBtnW, CloseBtnH);
        Raylib.DrawRectangle(CloseBtnX + jx, CloseBtnY + jy, CloseBtnW, CloseBtnH, closeHover ? Palette.AlertCrimson : new Color(28, 20, 22, 255));
        Raylib.DrawRectangleLines(CloseBtnX + jx, CloseBtnY + jy, CloseBtnW, CloseBtnH, closeHover ? Palette.CrtAmberPrimary : Palette.AlertCrimson);
        int xw = UiText.MeasureWidth("[X]", 10);
        UiText.Draw("[X]", CloseBtnX + jx + (CloseBtnW - xw) / 2, CloseBtnY + jy + 15, 10, closeHover ? Palette.OutlineVoid : Palette.AlertCrimson);
    }

    private static Color GetTabAccentColor(bool isUnofficial) =>
        isUnofficial
            ? Palette.FromHex("#a78bfa") // Distinct deep indigo / purple-blue accent for unofficial back-channel tabs
            : Palette.FromHex("#7dd3fc"); // Cool slate-blue accent for official station tabs

    private static Color GetTabActiveAccentBg(bool isUnofficial) =>
        isUnofficial
            ? new Color(30, 20, 58, 255)
            : new Color(18, 34, 52, 255);

    // =========================================================================
    // TAB 0 RENDER: MESSAGES (Blank Standby Reader + Unmistakable Unread/Read)
    // =========================================================================
    private void DrawMessagesTab(VirtualMouseState mouse)
    {
        Raylib.DrawRectangle(ViewX, ViewY, ViewW, ViewH, new Color(13, 18, 24, 255));
        Raylib.DrawRectangleLines(ViewX, ViewY, ViewW, ViewH, Palette.WeatheredGunmetalEdge);

        List<TerminalEmail> sorted = GetSortedInboxEmails();
        int maxInboxScroll = Math.Max(0, sorted.Count - 8);
        _inboxScroll = Math.Clamp(_inboxScroll, 0, maxInboxScroll);

        // Left Inbox List: (96, 112, 344, 496)
        Raylib.DrawRectangle(InboxX, InboxY, InboxW, InboxH, new Color(10, 14, 20, 255));
        Raylib.DrawRectangleLines(InboxX, InboxY, InboxW, InboxH, Palette.ClearanceTeal);
        Raylib.DrawRectangle(InboxX + 1, InboxY + 1, InboxW - 2, 24, new Color(18, 34, 40, 255));
        UiText.Draw($"INBOX // {sorted.Count} MSG ({_gameState.UnreadEmailCount} UNREAD)", InboxX + 8, InboxY + 7, 10, Palette.ClearanceTeal);

        int inboxBarX = InboxX + InboxW - 18;
        DrawPixelScrollButtons(mouse, inboxBarX, InboxY + 28, InboxY + InboxH - 18, _inboxScroll > 0, _inboxScroll < maxInboxScroll);

        if (sorted.Count == 0)
        {
            UiText.Draw("INBOX EMPTY", InboxX + 116, InboxY + 216, 10, Palette.WeatheredGunmetalEdge);
            UiText.Draw("NO SUBSPACE MAIL RECEIVED.", InboxX + 76, InboxY + 236, 10, Palette.WeatheredGunmetalEdge);
            if (_gameState.Day == 1 && _gameState.QuartersPhase == QuartersPhase.Morning)
            {
                UiText.Draw("MORNING SHIP ROSTERS BEGIN DAY 02.", InboxX + 56, InboxY + 264, 10, Palette.CrtAmberDimmed);
            }
        }
        else
        {
            for (int row = 0; row < 8; row++)
            {
                int idx = _inboxScroll + row;
                if (idx >= sorted.Count)
                    break;

                TerminalEmail mail = sorted[idx];
                int ry = InboxY + 30 + row * 56;
                bool sel = !string.IsNullOrEmpty(_selectedEmailId) &&
                           mail.EmailId.Equals(_selectedEmailId, StringComparison.OrdinalIgnoreCase);
                bool hov = !_hackGlitchActive && mouse.IsHit(InboxX + 6, ry, InboxW - 28, 52);
                bool unread = !mail.IsRead;

                Color rowBg;
                Color rowBorder;
                if (unread)
                {
                    rowBg = sel
                        ? new Color(28, 48, 54, 255)
                        : (mail.IsTerminalHackEmail
                            ? (hov ? new Color(48, 22, 26, 255) : new Color(36, 16, 20, 255))
                            : (hov ? new Color(36, 38, 22, 255) : new Color(26, 28, 17, 255)));
                    rowBorder = sel
                        ? Palette.ClearanceTeal
                        : (mail.IsTerminalHackEmail || mail.IsEncrypted ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
                }
                else
                {
                    // Visibly dimmed/muted read row with recessed border
                    rowBg = sel
                        ? new Color(18, 32, 38, 255)
                        : (hov ? new Color(15, 20, 26, 255) : new Color(11, 14, 19, 255));
                    rowBorder = sel ? Palette.ClearanceTeal : new Color(32, 38, 46, 255);
                }

                Raylib.DrawRectangle(InboxX + 6, ry, InboxW - 28, 52, rowBg);
                Raylib.DrawRectangleLines(InboxX + 6, ry, InboxW - 28, 52, rowBorder);

                if (unread)
                {
                    Color accentBar = mail.IsTerminalHackEmail || mail.IsEncrypted
                        ? Palette.AlertCrimson
                        : Palette.CrtAmberPrimary;
                    Raylib.DrawRectangle(InboxX + 6, ry, 3, 52, accentBar);

                    // Clear leading [NEW] badge + bold high-contrast subject
                    const string badge = "[NEW]";
                    int badgeW = UiText.MeasureWidth(badge, 10);
                    Raylib.DrawRectangle(InboxX + 12, ry + 5, badgeW + 4, 13, accentBar);
                    UiText.Draw(badge, InboxX + 14, ry + 7, 10, Palette.OutlineVoid);

                    int subjX = InboxX + 20 + badgeW;
                    string prefixTag = mail.IsEncrypted ? "[ENC] " : (mail.IsMorningRoster ? "[ROSTER] " : string.Empty);
                    string subj = TruncateText(prefixTag + mail.Subject, 10, InboxW - 46 - badgeW);
                    Color subjCol = mail.IsTerminalHackEmail || mail.IsEncrypted ? Palette.CrtAmberPrimary : Color.White;
                    // Crisp 1px pixel-bold subject
                    UiText.Draw(subj, subjX, ry + 7, 10, subjCol);
                    UiText.Draw(subj, subjX + 1, ry + 7, 10, subjCol);

                    UiText.Draw(TruncateText(mail.Sender, 10, InboxW - 44), InboxX + 14, ry + 23, 10, new Color(216, 224, 232, 255));
                    UiText.Draw($"DAY {mail.DayReceived:D2} {mail.PhaseReceived}", InboxX + 14, ry + 37, 10, Palette.CrtAmberDimmed);
                }
                else
                {
                    // Dimmed/muted read row with NO unread badge
                    string prefixTag = mail.IsEncrypted ? "[ENC] " : (mail.IsMorningRoster ? "[ROSTER] " : string.Empty);
                    string subj = TruncateText(prefixTag + mail.Subject, 10, InboxW - 44);
                    Color subjCol = sel ? Palette.ClearanceTeal : new Color(118, 130, 142, 255);
                    UiText.Draw(subj, InboxX + 12, ry + 7, 10, subjCol);
                    UiText.Draw(TruncateText(mail.Sender, 10, InboxW - 44), InboxX + 12, ry + 23, 10, new Color(86, 96, 108, 255));
                    UiText.Draw($"DAY {mail.DayReceived:D2} {mail.PhaseReceived}", InboxX + 12, ry + 37, 10, new Color(60, 68, 78, 255));
                }
            }
        }

        // Right Email Reader: (448, 112, 736, 496) -- Blank/Standby until player clicks a row!
        Raylib.DrawRectangle(ReaderX, ReaderY, ReaderW, ReaderH, new Color(11, 15, 20, 255));
        Raylib.DrawRectangleLines(ReaderX, ReaderY, ReaderW, ReaderH, Palette.WeatheredGunmetalEdge);

        TerminalEmail? activeMail = FindSelectedEmail();
        if (activeMail != null)
        {
            Raylib.DrawRectangle(ReaderX + 1, ReaderY + 1, ReaderW - 2, 76, new Color(18, 25, 34, 255));
            Raylib.DrawLine(ReaderX + 1, ReaderY + 77, ReaderX + ReaderW - 2, ReaderY + 77,
                activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.ClearanceTeal);

            if (!string.IsNullOrWhiteSpace(activeMail.Recipient))
            {
                UiText.Draw($"FROM:    {TruncateText(activeMail.Sender, 10, ReaderW - 88)}", ReaderX + 14, ReaderY + 8, 10,
                    activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.ClearanceTeal);
                UiText.Draw($"TO:      {TruncateText(activeMail.Recipient, 10, ReaderW - 88)}", ReaderX + 14, ReaderY + 23, 10,
                    Palette.ClearanceTeal);
                UiText.Draw($"SUBJECT: {TruncateText(activeMail.Subject, 10, ReaderW - 88)}", ReaderX + 14, ReaderY + 38, 10,
                    activeMail.IsEncrypted || activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
                UiText.Draw(TruncateText($"TIME:    {activeMail.CycleTimestamp}  |  DAY {activeMail.DayReceived:D2} ({activeMail.PhaseReceived})", 10, ReaderW - 28),
                    ReaderX + 14, ReaderY + 53, 10, Palette.WeatheredGunmetalEdge);
            }
            else
            {
                UiText.Draw(TruncateText($"FROM:    {activeMail.Sender}", 10, ReaderW - 28), ReaderX + 14, ReaderY + 10, 10,
                    activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.ClearanceTeal);
                UiText.Draw($"SUBJECT: {TruncateText(activeMail.Subject, 10, ReaderW - 104)}", ReaderX + 14, ReaderY + 28, 10,
                    activeMail.IsEncrypted || activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
                UiText.Draw(TruncateText($"TIME:    {activeMail.CycleTimestamp}  |  DAY {activeMail.DayReceived:D2} ({activeMail.PhaseReceived})", 10, ReaderW - 28),
                    ReaderX + 14, ReaderY + 46, 10, Palette.WeatheredGunmetalEdge);

                string statusLine = !string.IsNullOrWhiteSpace(activeMail.CustomStatusLine)
                    ? $"STATUS:  {activeMail.CustomStatusLine}"
                    : (activeMail.IsEncrypted
                        ? "STATUS:  CIPHER LOCKED (DECRYPT IN CRYPTOGRAPHY -- 0 SC)"
                        : (activeMail.IsTerminalHackEmail ? "STATUS:  ANOMALOUS RELAY // PORT 04 OVERRIDE" : "STATUS:  VERIFIED"));
                UiText.Draw(TruncateText(statusLine, 10, ReaderW - 28), ReaderX + 14, ReaderY + 61, 10,
                    activeMail.IsEncrypted || activeMail.IsTerminalHackEmail ? Palette.AlertCrimson : Palette.ClearanceTeal);
            }

            List<string> bodyLines = WrapMultilineText(StripAuthoringTodoTags(activeMail.Body), 10, ReaderW - 48);
            int visibleRows = (activeMail.IsEncrypted && _gameState.TerminalHackedUnlocked) ? 21 : 26;
            int maxScroll = Math.Max(0, bodyLines.Count - visibleRows);
            _readerScroll = Math.Clamp(_readerScroll, 0, maxScroll);

            int rBarX = ReaderX + ReaderW - 18;
            DrawPixelScrollButtons(mouse, rBarX, ReaderY + 84, ReaderY + ReaderH - 18, _readerScroll > 0, _readerScroll < maxScroll);

            int textY = ReaderY + 88;
            for (int i = 0; i < visibleRows && (_readerScroll + i) < bodyLines.Count; i++)
            {
                string ln = bodyLines[_readerScroll + i];
                Color col = activeMail.IsEncrypted
                    ? Palette.CrtAmberDimmed
                    : (activeMail.IsTerminalHackEmail && ln.StartsWith("[SYS_OVERRIDE", StringComparison.Ordinal)
                        ? Palette.AlertCrimson
                        : (ln.StartsWith("NOTICE", StringComparison.Ordinal) ||
                           ln.StartsWith("DETAIN", StringComparison.Ordinal) ||
                           ln.StartsWith("DAILY LEDGER", StringComparison.Ordinal) ||
                           ln.StartsWith("FIDUCIARY OBLIGATIONS", StringComparison.Ordinal)
                            ? Palette.CrtAmberPrimary
                            : new Color(216, 224, 232, 255)));
                UiText.Draw(ln, ReaderX + 16, textY + i * 15, 10, col);
            }

            if (activeMail.IsEncrypted && _gameState.TerminalHackedUnlocked)
            {
                DrawActionButton(mouse, ReaderX + 16, ReaderY + ReaderH - 48, 320, 32,
                    "OPEN [CRYPTO / LOGS] TO DECRYPT (0 SC)", Palette.CrtAmberPrimary, true);
            }
        }
        else
        {
            const string standby = "SELECT A TRANSMISSION TO VIEW";
            int sw = UiText.MeasureWidth(standby, 10);
            UiText.Draw(standby, ReaderX + (ReaderW - sw) / 2, ReaderY + ReaderH / 2 - 12, 10, Palette.WeatheredGunmetalEdge);
            const string sub = "READER PANE IN STANDBY";
            int subW = UiText.MeasureWidth(sub, 10);
            UiText.Draw(sub, ReaderX + (ReaderW - subW) / 2, ReaderY + ReaderH / 2 + 8, 10, new Color(48, 56, 68, 255));
        }
    }

    // =========================================================================
    // TAB 1 RENDER: FORUM (Late-90s / Early-2000s Sci-Fi Web Forum)
    // =========================================================================
    private void DrawForumTab(VirtualMouseState mouse)
    {
        Color forumBg = new(12, 16, 28, 255);
        Color forumHeaderBg = new(22, 34, 60, 255);
        Color forumBorder = Palette.FromHex("#3b82f6");
        Color forumLink = Palette.FromHex("#7dd3fc");

        Raylib.DrawRectangle(ViewX, ViewY, ViewW, ViewH, forumBg);
        Raylib.DrawRectangleLines(ViewX, ViewY, ViewW, ViewH, forumBorder);

        Raylib.DrawRectangle(ForumHdrX, ForumHdrY, ForumHdrW, ForumHdrH, forumHeaderBg);
        Raylib.DrawRectangleLines(ForumHdrX, ForumHdrY, ForumHdrW, ForumHdrH, forumBorder);
        UiText.Draw(_forumDb.BoardName, ForumHdrX + 12, ForumHdrY + 8, 10, forumLink);
        string boardMeta = $"HABITAT RING B LOCAL NODE  |  ACTIVE THREADS: {_visibleForumThreads.Count}  |  DAY {_gameState.Day:D2}";
        UiText.Draw(boardMeta, ForumHdrX + 12, ForumHdrY + 25, 10, new Color(156, 184, 222, 255));

        Raylib.DrawRectangle(ForumListX, ForumListY, ForumListW, ForumListH, new Color(10, 14, 24, 255));
        Raylib.DrawRectangleLines(ForumListX, ForumListY, ForumListW, ForumListH, forumBorder);
        Raylib.DrawRectangle(ForumListX + 1, ForumListY + 1, ForumListW - 2, 22, new Color(18, 28, 50, 255));
        UiText.Draw("THREAD INDEX (BOARD / SUBJECT)", ForumListX + 8, ForumListY + 6, 10, forumLink);

        int lBarX = ForumListX + ForumListW - 18;
        DrawPixelScrollButtons(mouse, lBarX, ForumListY + 26, ForumListY + ForumListH - 18,
            _forumListScroll > 0, _forumListScroll < Math.Max(0, _visibleForumThreads.Count - 7));

        for (int row = 0; row < 7; row++)
        {
            int idx = _forumListScroll + row;
            if (idx >= _visibleForumThreads.Count)
                break;

            ForumThreadModel th = _visibleForumThreads[idx];
            int ry = ForumListY + 28 + row * 58;
            if (ry + 54 > ForumListY + ForumListH - 4)
                break;

            bool sel = idx == _selectedForumThreadIndex;
            bool hov = !_hackGlitchActive && mouse.IsHit(ForumListX + 6, ry, ForumListW - 28, 54);

            Color rBg = sel ? new Color(24, 38, 68, 255) : (hov ? new Color(18, 26, 46, 255) : new Color(14, 20, 34, 255));
            Color tagCol = th.BoardTag is "OPS" or "RUMORS" ? Palette.CrtAmberPrimary : forumLink;
            Raylib.DrawRectangle(ForumListX + 6, ry, ForumListW - 28, 54, rBg);
            Raylib.DrawRectangleLines(ForumListX + 6, ry, ForumListW - 28, 54, sel ? forumLink : new Color(36, 54, 88, 255));

            UiText.Draw($"[{th.BoardTag}] ({th.Posts.Count} POSTS)", ForumListX + 12, ry + 6, 10, tagCol);
            List<string> tLines = WrapMultilineText(StripAuthoringTodoTags(th.Title), 10, ForumListW - 44);
            for (int tl = 0; tl < Math.Min(2, tLines.Count); tl++)
            {
                UiText.Draw(TruncateText(tLines[tl], 10, ForumListW - 44), ForumListX + 12, ry + 21 + tl * 13, 10,
                    sel ? Color.White : new Color(196, 212, 236, 255));
            }
        }

        Raylib.DrawRectangle(ForumStreamX, ForumStreamY, ForumStreamW, ForumStreamH, new Color(10, 14, 24, 255));
        Raylib.DrawRectangleLines(ForumStreamX, ForumStreamY, ForumStreamW, ForumStreamH, forumBorder);

        if (_selectedForumThreadIndex >= 0 && _selectedForumThreadIndex < _visibleForumThreads.Count)
        {
            ForumThreadModel active = _visibleForumThreads[_selectedForumThreadIndex];
            Raylib.DrawRectangle(ForumStreamX + 1, ForumStreamY + 1, ForumStreamW - 2, 30, new Color(20, 32, 56, 255));
            Raylib.DrawLine(ForumStreamX + 1, ForumStreamY + 31, ForumStreamX + ForumStreamW - 2, ForumStreamY + 31, forumBorder);
            UiText.Draw(TruncateText($"[{active.BoardTag}] {StripAuthoringTodoTags(active.Title)}", 10, ForumStreamW - 24),
                ForumStreamX + 10, ForumStreamY + 10, 10, Palette.CrtAmberPrimary);

            List<(string Text, Color Col, bool IsHeader, bool IsDivider)> streamLines = [];
            for (int p = 0; p < active.Posts.Count; p++)
            {
                ForumPostModel post = active.Posts[p];
                streamLines.Add(($"@{post.Handle}  [{post.RoleTag}]   --   {post.Timestamp}", forumLink, true, false));
                foreach (string wrapped in WrapMultilineText(StripAuthoringTodoTags(post.Body), 10, ForumStreamW - 48))
                {
                    streamLines.Add((wrapped, new Color(214, 224, 240, 255), false, false));
                }
                streamLines.Add((string.Empty, Color.Blank, false, true));
            }

            const int visLines = 24;
            int maxPostScroll = Math.Max(0, streamLines.Count - visLines);
            _forumPostScroll = Math.Clamp(_forumPostScroll, 0, maxPostScroll);

            int pBarX = ForumStreamX + ForumStreamW - 18;
            DrawPixelScrollButtons(mouse, pBarX, ForumStreamY + 34, ForumStreamY + ForumStreamH - 18,
                _forumPostScroll > 0, _forumPostScroll < maxPostScroll);

            int py = ForumStreamY + 38;
            int streamBottomLimit = ForumStreamY + ForumStreamH - 12;
            for (int i = 0; i < visLines && (_forumPostScroll + i) < streamLines.Count; i++)
            {
                if (py + 12 > streamBottomLimit)
                    break;

                var row = streamLines[_forumPostScroll + i];
                if (row.IsHeader)
                {
                    Raylib.DrawRectangle(ForumStreamX + 8, py - 2, ForumStreamW - 32, 14, new Color(18, 28, 48, 255));
                    UiText.Draw(TruncateText(row.Text, 10, ForumStreamW - 44), ForumStreamX + 14, py, 10, row.Col);
                }
                else if (row.IsDivider)
                {
                    Raylib.DrawLine(ForumStreamX + 12, py + 6, ForumStreamX + ForumStreamW - 28, py + 6, new Color(32, 48, 78, 255));
                }
                else
                {
                    UiText.Draw(TruncateText(row.Text, 10, ForumStreamW - 44), ForumStreamX + 16, py, 10, row.Col);
                }
                py += 16;
            }
        }
    }

    // =========================================================================
    // TAB 2 RENDER: PACT REQ (Pure Flat-SC Brengop SKU Ledger)
    // =========================================================================
    private void DrawPactReqTab(VirtualMouseState mouse)
    {
        Color bg = new(18, 17, 11, 255);
        Color headerBg = new(38, 34, 14, 255);
        Color bmcYellow = Palette.FromHex("#bdb040");
        Color bmcMuted = Palette.FromHex("#968826");
        Color bmcBrass = Palette.FromHex("#6b5324");

        Raylib.DrawRectangle(ViewX, ViewY, ViewW, ViewH, bg);
        Raylib.DrawRectangleLines(ViewX, ViewY, ViewW, ViewH, bmcYellow);

        Raylib.DrawRectangle(ViewX + 8, ViewY + 8, ViewW - 16, 44, headerBg);
        Raylib.DrawRectangleLines(ViewX + 8, ViewY + 8, ViewW - 16, 44, bmcMuted);
        UiText.Draw("BRENGOP MERCANTILE CONSORTIUM // OFFICIAL REQUISITION", ViewX + 20, ViewY + 16, 10, bmcYellow);
        string phaseTag = _gameState.QuartersPhase == QuartersPhase.Evening
            ? "EVENING REQUISITION WINDOW OPEN  |  FLAT PACT SCRIP (SC) BILLING  |  RATIONS DISPENSE ON REPLICATOR"
            : "MORNING SHIFT WINDOW  |  NIGHTLY RATIONS ORDERABLE IN EVENING QUARTERS";
        UiText.Draw(phaseTag, ViewX + 20, ViewY + 34, 10, bmcMuted);

        bool isEvening = _gameState.QuartersPhase == QuartersPhase.Evening;
        bool foodOrdered = _gameState.TonightRationOrder != NightlyRationOrder.None;

        DrawPactSkuRow(PactRowX, 168, PactRowW, 46, bmcBrass, bmcYellow,
            "SKU-014  DRY RATION BRICKS",
            $"{_config.DryRationBricksCost} SC  --  AUTOMATIC OVERNIGHT (2+ CONSECUTIVE DAYS -> MAX SPS CURSOR SWAY)");
        string r1Btn = _gameState.TonightRationOrder == NightlyRationOrder.DryRationBricks
            ? "[DISPENSED TONIGHT]"
            : (isEvening ? $"ORDER ({_config.DryRationBricksCost} SC)" : "[EVENING ONLY]");
        DrawActionButton(mouse, PactBtnX, 174, PactBtnW, 34, r1Btn,
            _gameState.TonightRationOrder == NightlyRationOrder.DryRationBricks ? Palette.ClearanceTeal : bmcYellow,
            isEvening && !foodOrdered && _gameState.PactScrip >= _config.DryRationBricksCost);

        DrawPactSkuRow(PactRowX, 222, PactRowW, 46, bmcBrass, bmcYellow,
            "SKU-029  NUTRIENT PASTE (STANDARD)",
            $"{_config.NutrientPasteCost} SC  --  BASELINE FOCUS (MEDIUM SPS SWAY; DISPENSES ON REPLICATOR)");
        string r2Btn = _gameState.TonightRationOrder == NightlyRationOrder.NutrientPaste
            ? "[DISPENSED TONIGHT]"
            : (isEvening ? $"ORDER ({_config.NutrientPasteCost} SC)" : "[EVENING ONLY]");
        DrawActionButton(mouse, PactBtnX, 228, PactBtnW, 34, r2Btn,
            _gameState.TonightRationOrder == NightlyRationOrder.NutrientPaste ? Palette.ClearanceTeal : bmcYellow,
            isEvening && !foodOrdered && _gameState.PactScrip >= _config.NutrientPasteCost);

        DrawPactSkuRow(PactRowX, 276, PactRowW, 46, bmcBrass, bmcYellow,
            "SKU-088  HIGH-TIER RATIONS",
            $"{_config.HighTierRationsCost} SC  --  ZERO SPS CURSOR SWAY NEXT SHIFT (DISPENSES ON REPLICATOR)");
        string r3Btn = _gameState.TonightRationOrder == NightlyRationOrder.HighTierRations
            ? "[DISPENSED TONIGHT]"
            : (isEvening ? $"ORDER ({_config.HighTierRationsCost} SC)" : "[EVENING ONLY]");
        DrawActionButton(mouse, PactBtnX, 282, PactBtnW, 34, r3Btn,
            _gameState.TonightRationOrder == NightlyRationOrder.HighTierRations ? Palette.ClearanceTeal : bmcYellow,
            isEvening && !foodOrdered && _gameState.PactScrip >= _config.HighTierRationsCost);

        const int summaryY = 344;
        Raylib.DrawRectangle(PactRowX, summaryY, PactRowW, 116, new Color(24, 22, 13, 255));
        Raylib.DrawRectangleLines(PactRowX, summaryY, PactRowW, 116, bmcBrass);
        UiText.Draw("DIETARY FOCUS & REQUISITION ADVISORY", PactRowX + 14, summaryY + 14, 10, bmcYellow);
        string currentSwayDesc = _gameState.CurrentHungerTier switch
        {
            HungerTier.HighTierRations => "CURRENT SWAY TIER: HIGH-TIER RATIONS (0 SPS CURSOR SWAY)",
            HungerTier.MediumNutrientPaste => "CURRENT SWAY TIER: NUTRIENT PASTE (MEDIUM SPS CURSOR SWAY)",
            _ => "CURRENT SWAY TIER: DRY / UNFED 2+ DAYS (MAX SPS CURSOR SWAY)"
        };
        UiText.Draw(currentSwayDesc, PactRowX + 14, summaryY + 38, 10, Palette.ClearanceTeal);
        UiText.Draw("FOOD RATIONS ARE THE SOLE DRIVER OF NEXT-SHIFT SPS PROBE STABILITY.",
            PactRowX + 14, summaryY + 58, 10, bmcMuted);
        UiText.Draw($"NIGHTLY BERTH LEASE (-{_config.BerthLeaseCost} SC) IS AUTO-SETTLED AT CLOCK-OUT.",
            PactRowX + 14, summaryY + 78, 10, bmcMuted);

        Raylib.DrawRectangle(ViewX + 8, ViewY + ViewH - 36, ViewW - 16, 28, new Color(28, 25, 12, 255));
        Raylib.DrawRectangleLines(ViewX + 8, ViewY + ViewH - 36, ViewW - 16, 28, bmcBrass);
        const string disc = "ALL DEDUCTIONS FINAL. BERTH LEASE AUTO-SETTLED AT CLOCK-OUT.";
        int dw = UiText.MeasureWidth(disc, 10);
        UiText.Draw(disc, ViewX + (ViewW - dw) / 2, ViewY + ViewH - 27, 10, bmcYellow);
    }

    // =========================================================================
    // TAB 3 RENDER: CRYPTO / LOGS (Streamlined Missions-Only Direct Dispatch)
    // =========================================================================
    private void DrawCryptoLogsTab(VirtualMouseState mouse)
    {
        Color diagBg = new(8, 13, 11, 255);
        Color diagGreen = Palette.FromHex("#4ade80");
        Color diagGreenDim = Palette.FromHex("#166534");

        Raylib.DrawRectangle(ViewX, ViewY, ViewW, ViewH, diagBg);
        Raylib.DrawRectangleLines(ViewX, ViewY, ViewW, ViewH, diagGreenDim);

        // Left Control & Mission Dispatch Pane: (96, 112, 464, 496)
        Raylib.DrawRectangle(CryptoLeftX, CryptoLeftY, CryptoLeftW, CryptoLeftH, new Color(10, 17, 13, 255));
        Raylib.DrawRectangleLines(CryptoLeftX, CryptoLeftY, CryptoLeftW, CryptoLeftH, diagGreen);
        Raylib.DrawRectangle(CryptoLeftX + 1, CryptoLeftY + 1, CryptoLeftW - 2, 28, new Color(14, 30, 20, 255));
        UiText.Draw("BERTH_04 // CRYPTO_DIAG_v2.3 (MISSIONS-ONLY)", CryptoLeftX + 12, CryptoLeftY + 9, 10, diagGreen);

        int encCount = _gameState.Emails.Count(e => e.IsEncrypted);
        int unscrubbed = Math.Max(_gameState.UnscrubbedLogCount, _gameState.UnauthorizedLogCount);
        int scrubCost = Math.Max(0, _config.ScrubSecurityLogsCost);

        // 1. DECRYPT MESSAGE [0 SC]
        DrawCryptoActionBtn(
            mouse,
            CryptoLeftX + 12, CryptoLeftY + 38, CryptoLeftW - 24, 44,
            "DECRYPT MESSAGE [0 SC]",
            encCount > 0 ? $"{encCount} CIPHERED PACKET(S) WAITING -- LOGS [UNAUTH_IO]" : "NO ENCRYPTED MAIL PENDING",
            encCount > 0,
            diagGreen);

        // 2. SCRUB LOGS [15 SC]
        DrawCryptoActionBtn(
            mouse,
            CryptoLeftX + 12, CryptoLeftY + 90, CryptoLeftW - 24, 44,
            $"SCRUB LOGS [{scrubCost} SC]",
            unscrubbed > 0 ? $"PURGE {unscrubbed} EXPOSED HEX ENTRY(S) -> KEEPS VENT SAFE" : "ALL SECURITY LOGS CLEAN",
            unscrubbed > 0 && _gameState.PactScrip >= scrubCost,
            unscrubbed > 0 ? Palette.AlertCrimson : diagGreen);

        // 3. Compact Trace Status Box (y = CryptoLeftY + 142, h = 36)
        int statY = CryptoLeftY + 142;
        Raylib.DrawRectangle(CryptoLeftX + 12, statY, CryptoLeftW - 24, 36, new Color(8, 12, 10, 255));
        Raylib.DrawRectangleLines(CryptoLeftX + 12, statY, CryptoLeftW - 24, 36, unscrubbed > 0 ? Palette.AlertCrimson : diagGreenDim);
        string traceLine = unscrubbed > 0
            ? $"TRACE LOGS: EXPOSED ({unscrubbed}) -- VENT AT RISK IN RAIDS"
            : "TRACE LOGS: CLEAN -- UNDER-DESK VENT SAFE IN RAIDS";
        UiText.Draw(TruncateText(traceLine, 10, CryptoLeftW - 44), CryptoLeftX + 20, statY + 13, 10,
            unscrubbed > 0 ? Palette.AlertCrimson : Palette.ClearanceTeal);

        // 4. Direct MISSION DISPATCH Panel (y = CryptoLeftY + 186, h = 298 -> bottom 596)
        Raylib.DrawRectangle(DispatchBoxX, DispatchBoxY, DispatchBoxW, DispatchBoxH, new Color(7, 11, 9, 255));
        Raylib.DrawRectangleLines(DispatchBoxX, DispatchBoxY, DispatchBoxW, DispatchBoxH, diagGreen);
        Raylib.DrawRectangle(DispatchBoxX + 1, DispatchBoxY + 1, DispatchBoxW - 2, 38, new Color(13, 24, 18, 255));
        UiText.Draw("// MISSION DISPATCH (INTERCEPT CONTRACTS)", DispatchBoxX + 10, DispatchBoxY + 6, 10, diagGreen);
        UiText.Draw("WARNING: TRANSMITTING WRITES [AUDIT_FLAG] TO HEX LOGS.", DispatchBoxX + 10, DispatchBoxY + 22, 10, Palette.AlertCrimson);

        List<MissionDispatchMatch> dispatches = _intelEconomy.GetActiveInterceptDispatches(_gameState, _covertMissionDb);
        int maxDispScroll = Math.Max(0, dispatches.Count - 3);
        _dispatchScroll = Math.Clamp(_dispatchScroll, 0, maxDispScroll);

        if (maxDispScroll > 0)
        {
            int dBarX = DispatchBoxX + DispatchBoxW - 18;
            DrawPixelScrollButtons(mouse, dBarX, DispatchBoxY + 42, DispatchBoxY + DispatchBoxH - 18,
                _dispatchScroll > 0, _dispatchScroll < maxDispScroll);
        }

        if (dispatches.Count == 0)
        {
            int unmatched = _intelEconomy.CountUnmatchedCapturedSlates(_gameState, _covertMissionDb);
            UiText.Draw("NO ACTIVE INTERCEPT CONTRACTS.", DispatchBoxX + 106, DispatchBoxY + 122, 10, Palette.WeatheredGunmetalEdge);
            UiText.Draw("ACCEPT INTERCEPT CONTRACTS ON [DECK 4].", DispatchBoxX + 82, DispatchBoxY + 144, 10, diagGreenDim);
            if (unmatched > 0)
            {
                UiText.Draw($"UNMATCHED CAPTURED SLATES HELD: {unmatched}", DispatchBoxX + 100, DispatchBoxY + 174, 10, Palette.CrtAmberDimmed);
            }
        }
        else
        {
            int cardW = maxDispScroll > 0 ? DispatchBoxW - 30 : DispatchBoxW - 16;
            for (int r = 0; r < 3; r++)
            {
                int idx = _dispatchScroll + r;
                if (idx >= dispatches.Count)
                    break;

                MissionDispatchMatch match = dispatches[idx];
                int cy = DispatchBoxY + 44 + r * 80;
                bool ready = match.CanTransmit && match.MatchingSlate != null;

                Color cBg = ready ? new Color(14, 34, 26, 255) : new Color(10, 15, 13, 255);
                Color cBorder = ready ? Palette.ClearanceTeal : diagGreenDim;
                Raylib.DrawRectangle(DispatchBoxX + 8, cy, cardW, 74, cBg);
                Raylib.DrawRectangleLines(DispatchBoxX + 8, cy, cardW, 74, cBorder);

                // Line 1: Requesting contact + Target Prefix/Registration
                UiText.Draw(TruncateText(StripAuthoringTodoTags(match.Mission.AssigningFaction), 10, cardW - 110),
                    DispatchBoxX + 16, cy + 7, 10, ready ? Palette.CrtAmberPrimary : diagGreen);
                string tgtTag = $"TGT: {match.EffectiveTargetLabel}";
                int tw = UiText.MeasureWidth(tgtTag, 10);
                UiText.Draw(tgtTag, DispatchBoxX + 8 + cardW - tw - 8, cy + 7, 10, Palette.ClearanceTeal);

                // Line 2: Contract title
                UiText.Draw(TruncateText(StripAuthoringTodoTags(match.Mission.Title), 10, cardW - 16),
                    DispatchBoxX + 16, cy + 22, 10, ready ? Color.White : new Color(138, 160, 146, 255));

                if (ready && match.MatchingSlate != null)
                {
                    string src = match.SlateIsInVentStash ? "VENT" : "POCKET";
                    UiText.Draw(TruncateText($"{match.MatchingSlate.TargetShipRegistration} [{src}]", 10, cardW - 186),
                        DispatchBoxX + 16, cy + 40, 10, Palette.ClearanceTeal);
                    UiText.Draw("LOGS [AUDIT_FLAG]", DispatchBoxX + 16, cy + 54, 10, Palette.AlertCrimson);

                    int txBtnW = 166;
                    int txBtnX = DispatchBoxX + 8 + cardW - txBtnW - 6;
                    DrawActionButton(mouse, txBtnX, cy + 38, txBtnW, 28,
                        $"[TRANSMIT -- +{match.Mission.RewardScrip} SC]",
                        Palette.ClearanceTeal, enabled: true);
                }
                else
                {
                    UiText.Draw(TruncateText($"AWAITING {match.EffectiveTargetLabel} CAPTURED SLATE (BOOTH 04 SLATE BUS)", 10, cardW - 16),
                        DispatchBoxX + 16, cy + 46, 10, Palette.WeatheredGunmetalEdge);
                }
            }
        }

        // Right Security Log Pane: (568, 112, 616, 496) -> y = 112..608
        Raylib.DrawRectangle(CryptoRightX, CryptoRightY, CryptoRightW, CryptoRightH, new Color(9, 14, 11, 255));
        Raylib.DrawRectangleLines(CryptoRightX, CryptoRightY, CryptoRightW, CryptoRightH, diagGreen);
        Raylib.DrawRectangle(CryptoRightX + 1, CryptoRightY + 1, CryptoRightW - 2, 28, new Color(14, 30, 20, 255));
        UiText.Draw("SECURITY HEX LOGS // SLATE_BUS & SUBSPACE I/O TABLE", CryptoRightX + 12, CryptoRightY + 9, 10, diagGreen);

        UiText.Draw("ADDR      DAY  FLAG          EVENT", CryptoRightX + 12, CryptoRightY + 36, 10, diagGreenDim);
        Raylib.DrawLine(CryptoRightX + 8, CryptoRightY + 50, CryptoRightX + CryptoRightW - 24, CryptoRightY + 50, diagGreenDim);

        int lBarX = CryptoRightX + CryptoRightW - 18;
        DrawPixelScrollButtons(mouse, lBarX, CryptoRightY + 42, CryptoRightY + CryptoRightH - 18,
            _logScroll > 0, _logScroll < Math.Max(0, _gameState.SecurityLogs.Count - 17));

        if (_gameState.SecurityLogs.Count == 0)
        {
            UiText.Draw("0x00:000  D01  [SYS_OK]      NO UNAUTHORIZED I/O LOGGED.",
                CryptoRightX + 16, CryptoRightY + 64, 10, diagGreenDim);
        }
        else
        {
            const int visLogs = 17;
            _logScroll = Math.Clamp(_logScroll, 0, Math.Max(0, _gameState.SecurityLogs.Count - visLogs));
            int descMaxPx = CryptoRightW - 224;
            for (int i = 0; i < visLogs && (_logScroll + i) < _gameState.SecurityLogs.Count; i++)
            {
                SecurityLogEntry entry = _gameState.SecurityLogs[_logScroll + i];
                int ly = CryptoRightY + 58 + i * 24;
                if (ly + 22 > CryptoRightY + CryptoRightH - 6)
                    break;

                if (i % 2 == 0)
                {
                    Raylib.DrawRectangle(CryptoRightX + 8, ly - 2, CryptoRightW - 30, 22, new Color(12, 20, 15, 255));
                }

                Color tagCol = entry.IsPurged
                    ? Palette.WeatheredGunmetalEdge
                    : (entry.IsSuspicious ? Palette.AlertCrimson : diagGreen);
                Color descCol = entry.IsPurged
                    ? new Color(92, 104, 112, 255)
                    : new Color(204, 226, 210, 255);

                UiText.Draw(entry.HexAddr, CryptoRightX + 12, ly + 4, 10, diagGreen);
                UiText.Draw($"D{entry.Day:D2}", CryptoRightX + 76, ly + 4, 10, Palette.CrtAmberDimmed);
                UiText.Draw(entry.Tag, CryptoRightX + 108, ly + 4, 10, tagCol);
                UiText.Draw(TruncateText(entry.Description, 10, descMaxPx), CryptoRightX + 196, ly + 4, 10, descCol);
            }
        }
    }

    // =========================================================================
    // TAB 4 RENDER: DECK 4 (Contracts & Drops + Option A 50 SC Strike Clearing)
    // =========================================================================
    private void DrawDeck4Tab(VirtualMouseState mouse)
    {
        Color dosBg = new(9, 8, 6, 255);
        Color dosAmber = Palette.CrtAmberPrimary;
        Color dosDim = Palette.CrtAmberDimmed;

        Raylib.DrawRectangle(ViewX, ViewY, ViewW, ViewH, dosBg);
        Raylib.DrawRectangleLines(ViewX, ViewY, ViewW, ViewH, dosDim);

        // Retro DOS/BBS Header Banner
        UiText.Draw("> TUNNEL: D4_VENT_RELAY // BAUD 2400 // NO_LOG", ViewX + 16, ViewY + 14, 10, dosAmber);
        UiText.Draw("SIG:", ViewX + ViewW - 118, ViewY + 14, 10, dosDim);
        int barsActive = ((int)(_visualTime * 3f) % 5 == 0) ? 4 : 5;
        for (int s = 0; s < 5; s++)
        {
            int sh = 4 + s * 3;
            Color sCol = s < barsActive ? dosAmber : new Color(48, 30, 12, 255);
            Raylib.DrawRectangle(ViewX + ViewW - 86 + s * 10, ViewY + 26 - sh, 6, sh, sCol);
        }

        string fullDoubleRule = new string('=', 156);
        UiText.Draw(TruncateText(fullDoubleRule, 10, ViewW - 32), ViewX + 16, ViewY + 34, 10, dosDim);
        UiText.Draw("[CHUTE DROP: 0500]  //  LOWER-DECK CONTRACT BOARD & EXCHANGE  --  ZERO BRENGOP RECEIPTS",
            ViewX + 16, ViewY + 52, 10, dosAmber);
        UiText.Draw(TruncateText(fullDoubleRule, 10, ViewW - 32), ViewX + 16, ViewY + 68, 10, dosDim);

        // Left Column: (104, 192, 536, 412) -- CONTRACTS & DROPS
        Raylib.DrawRectangle(Deck4LeftX, Deck4LeftY, Deck4LeftW, Deck4LeftH, new Color(13, 10, 7, 255));
        Raylib.DrawRectangleLines(Deck4LeftX, Deck4LeftY, Deck4LeftW, Deck4LeftH, dosDim);
        UiText.Draw("> CONTRACTS & DROPS [COVERT BOARD]", Deck4LeftX + 12, Deck4LeftY + 9, 10, dosAmber);
        Raylib.DrawLine(Deck4LeftX + 10, Deck4LeftY + 24, Deck4LeftX + Deck4LeftW - 10, Deck4LeftY + 24, dosDim);

        List<EconomyEngine.Deck4ContractListing> contracts = _economy.GetDeck4ContractListings(_gameState, _covertMissionDb);
        int maxContractScroll = Math.Max(0, contracts.Count - 2);
        _deck4ContractScroll = Math.Clamp(_deck4ContractScroll, 0, maxContractScroll);

        if (maxContractScroll > 0)
        {
            int cBarX = Deck4LeftX + Deck4LeftW - 20;
            DrawPixelScrollButtons(mouse, cBarX, Deck4LeftY + 30, Deck4LeftY + 162,
                _deck4ContractScroll > 0, _deck4ContractScroll < maxContractScroll);
        }

        if (contracts.Count == 0)
        {
            UiText.Draw("NO NEW COVERT CONTRACTS POSTED TODAY.", Deck4LeftX + 128, Deck4LeftY + 86, 10, dosDim);
            UiText.Draw("CHECK BACK LATER FOR NEW COVERT OPERATIONS.", Deck4LeftX + 106, Deck4LeftY + 108, 10, new Color(110, 76, 32, 255));
        }
        else
        {
            int cardW = maxContractScroll > 0 ? Deck4LeftW - 34 : Deck4LeftW - 20;
            for (int r = 0; r < 2; r++)
            {
                int idx = _deck4ContractScroll + r;
                if (idx >= contracts.Count)
                    break;

                EconomyEngine.Deck4ContractListing listing = contracts[idx];
                CovertMissionSpec spec = listing.Mission;
                int cy = Deck4LeftY + 30 + r * 76;

                Color cardBg = listing.IsActive ? new Color(22, 24, 14, 255) : new Color(18, 14, 9, 255);
                Color cardBorder = listing.IsActive ? Palette.ClearanceTeal : dosDim;
                Raylib.DrawRectangle(Deck4LeftX + 10, cy, cardW, 70, cardBg);
                Raylib.DrawRectangleLines(Deck4LeftX + 10, cy, cardW, 70, cardBorder);

                string typeTag = spec.IsDeliveryMission ? "[DELIVERY]" : "[INTERCEPT]";
                Color typeCol = spec.IsDeliveryMission ? Palette.AlertCrimson : Palette.ClearanceTeal;
                UiText.Draw(typeTag, Deck4LeftX + 18, cy + 7, 10, typeCol);
                int tagW = UiText.MeasureWidth(typeTag, 10);
                UiText.Draw(TruncateText(StripAuthoringTodoTags(spec.Title), 10, cardW - tagW - 92), Deck4LeftX + 24 + tagW, cy + 7, 10, dosAmber);

                string payTag = $"+{spec.RewardScrip} SC";
                int payW = UiText.MeasureWidth(payTag, 10);
                UiText.Draw(payTag, Deck4LeftX + 10 + cardW - payW - 8, cy + 7, 10, Palette.ClearanceTeal);

                string autoTarget = IntelEconomy.ResolveEffectiveTargetRegistration(_gameState, spec);
                string autoTargetLabel = !string.IsNullOrWhiteSpace(autoTarget) ? autoTarget : spec.TargetPrefix;
                string reqLine = spec.IsDeliveryMission
                    ? $"DROPS SEALED SLATE AT 0500 -> UPLOAD TO {spec.TargetRegistration}"
                    : (spec.AutoActivateOnHack
                        ? $"FREE 0500 BLANK SLATE -> INTERCEPT {autoTargetLabel} -> CRYPTO"
                        : $"REQUIRES BLANK SLATE ({_config.BlankDataSlateCost} SC) -> INTERCEPT {spec.TargetPrefix}");
                UiText.Draw(TruncateText(StripAuthoringTodoTags(reqLine), 10, cardW - 122), Deck4LeftX + 18, cy + 25, 10, new Color(214, 196, 152, 255));
                UiText.Draw(TruncateText($"CONTACT: {StripAuthoringTodoTags(spec.AssigningFaction)}", 10, cardW - 122),
                    Deck4LeftX + 18, cy + 44, 10, dosDim);

                int accBtnX = Deck4LeftX + 10 + cardW - 108;
                DrawActionButton(mouse, accBtnX, cy + 34, 102, 30,
                    listing.IsActive ? "[ACTIVE]" : "[ACCEPT]",
                    listing.IsActive ? Palette.ClearanceTeal : dosAmber,
                    enabled: !listing.IsActive);
            }
        }

        // Gear Purchases Section (Deck4LeftY + 188 .. 404)
        Raylib.DrawLine(Deck4LeftX + 10, Deck4LeftY + 186, Deck4LeftX + Deck4LeftW - 10, Deck4LeftY + 186, dosDim);
        UiText.Draw("> GREY-MARKET GEAR [0500 CHUTE / TAILORING]", Deck4LeftX + 12, Deck4LeftY + 193, 10, dosAmber);

        int lBtnX = Deck4LeftX + Deck4LeftW - 152;

        // Gear 1: BLANK DATA SLATE [ORDER - 15 SC]
        int pendingBlanks = _gameState.PendingChuteDeliveries.Count(i => i.ItemType == ContrabandPocketItem.TypeBlankDataSlate);
        UiText.Draw($"01. BLANK DATA SLATE (UNREG)  [{_config.BlankDataSlateCost} SC]", Deck4LeftX + 12, Deck4LeftY + 218, 10, dosAmber);
        string blankSub = pendingBlanks > 0
            ? $"    FOR INTERCEPT JOBS (IN 0500 TRANSIT: x{pendingBlanks})"
            : "    REQUIRED FOR DECK 4 INTERCEPT CONTRACTS (0500 DROP).";
        UiText.Draw(blankSub, Deck4LeftX + 12, Deck4LeftY + 236, 10, pendingBlanks > 0 ? Palette.ClearanceTeal : dosDim);
        DrawActionButton(mouse, lBtnX, Deck4LeftY + 220, 140, 34, $"[ORDER - {_config.BlankDataSlateCost} SC]",
            dosAmber, _gameState.PactScrip >= _config.BlankDataSlateCost);

        Raylib.DrawLine(Deck4LeftX + 10, Deck4LeftY + 266, Deck4LeftX + Deck4LeftW - 10, Deck4LeftY + 266, new Color(64, 40, 14, 255));

        // Gear 2: SHIELDED POCKET LINING [ORDER - 90 SC]
        UiText.Draw($"02. SHIELDED POCKET LINING  [{_config.ShieldedPocketLiningCost} SC]", Deck4LeftX + 12, Deck4LeftY + 282, 10, dosAmber);
        UiText.Draw("    CONCEALS 1 COAT SLATE DURING BERTH RAIDS (0500 DROP).", Deck4LeftX + 12, Deck4LeftY + 300, 10, dosDim);
        string shieldBtn = _gameState.HasShieldedPocketLining
            ? "[INSTALLED]"
            : (_gameState.ShieldedLiningOrdered ? "[0500 TRANSIT]" : $"[ORDER - {_config.ShieldedPocketLiningCost} SC]");
        bool canBuyShield = !_gameState.HasShieldedPocketLining && !_gameState.ShieldedLiningOrdered &&
                            _gameState.PactScrip >= _config.ShieldedPocketLiningCost;
        DrawActionButton(mouse, lBtnX, Deck4LeftY + 284, 140, 34, shieldBtn,
            _gameState.HasShieldedPocketLining ? Palette.ClearanceTeal : dosAmber, canBuyShield);

        Raylib.DrawLine(Deck4LeftX + 10, Deck4LeftY + 330, Deck4LeftX + Deck4LeftW - 10, Deck4LeftY + 330, new Color(64, 40, 14, 255));

        // Gear 3: POCKET CAPACITY UPGRADE (D06: 60 SC / D09: 120 SC)
        var (canUpg, nextCap, upgCost, minDay, _) = _economy.GetNextPocketUpgradeInfo(_gameState, _config);
        UiText.Draw($"03. POCKET CAPACITY UPGRADE  (NOW: {_gameState.PocketCapacity}/3)", Deck4LeftX + 12, Deck4LeftY + 346, 10, dosAmber);
        UiText.Draw(_gameState.PocketCapacity >= 3
            ? "    MAXIMUM TAILORED POCKET CAPACITY (3/3) REACHED."
            : $"    EXPANDS POCKET TO {nextCap} SLOTS (UNLOCKS DAY {minDay:D2}).",
            Deck4LeftX + 12, Deck4LeftY + 364, 10, canUpg ? Palette.ClearanceTeal : dosDim);
        bool canBuyUpg = canUpg && _gameState.PocketCapacity < 3 && _gameState.PactScrip >= upgCost;
        DrawActionButton(mouse, lBtnX, Deck4LeftY + 348, 140, 34,
            _gameState.PocketCapacity >= 3 ? "[MAX 3/3]" : (canUpg ? $"[ORDER - {upgCost} SC]" : $"DAY {minDay:D2} LOCK"),
            canBuyUpg ? dosAmber : dosDim, canBuyUpg);

        // Right Dashed ASCII Panel: (656, 192, 520, 412) -- // BACK-CHANNEL // STRIKE EXPUNGEMENT (Option A Sole Clearer)
        Raylib.DrawRectangle(Deck4RightX, Deck4RightY, Deck4RightW, Deck4RightH, new Color(13, 10, 7, 255));
        DrawDashedRect(Deck4RightX, Deck4RightY, Deck4RightW, Deck4RightH, dosAmber);

        const string fixerTitle = "// BACK-CHANNEL // STRIKE EXPUNGEMENT";
        int ftw = UiText.MeasureWidth(fixerTitle, 10);
        UiText.Draw(fixerTitle, Deck4RightX + (Deck4RightW - ftw) / 2, Deck4RightY + 14, 10, dosAmber);

        UiText.Draw($"AUDIT DEFERRAL FEE  [{_config.AuditDeferralFeeCost} SC -> -1 STRIKE]", Deck4RightX + 16, Deck4RightY + 56, 10, dosAmber);
        UiText.Draw("SOLE BACK-CHANNEL STRIKE CLEARER BEFORE 3/3 KORDANI RAID.", Deck4RightX + 16, Deck4RightY + 78, 10, dosDim);

        bool strikesOnline = _gameState.SecurityStrikesActive || _gameState.Day >= _config.DayUnlockWarpScanner;
        bool canDefer = strikesOnline && _gameState.SecurityStrikes > 0 && _gameState.PactScrip >= _config.AuditDeferralFeeCost;
        string deferLabel = !strikesOnline
            ? $"LOCKED -- SECURITY STRIKES ACTIVATE DAY {_config.DayUnlockWarpScanner:D2}"
            : (_gameState.SecurityStrikes <= 0
                ? $"NO ACTIVE STRIKES ON RECORD (0/{_config.MaxStrikesBeforeAudit})"
                : $"[ORDER - {_config.AuditDeferralFeeCost} SC]  (EXPUNGE 1 STRIKE)");
        DrawActionButton(mouse, Deck4RightX + 16, Deck4RightY + 116, Deck4RightW - 32, 42, deferLabel,
            canDefer ? dosAmber : dosDim, canDefer);

        string dashSep = string.Concat(Enumerable.Repeat("- ", 72));
        UiText.Draw(TruncateText(dashSep, 10, Deck4RightW - 32),
            Deck4RightX + 16, Deck4RightY + 188, 10, dosDim);

        // Compact Pocket / Vent / Chute / Active Contracts Status Block
        UiText.Draw("> BERTH SMUGGLING STATUS", Deck4RightX + 16, Deck4RightY + 212, 10, dosAmber);
        UiText.Draw($"  COAT POCKET SLOTS:    {_gameState.PocketItems.Count} / {_gameState.PocketCapacity}",
            Deck4RightX + 16, Deck4RightY + 236, 10, Palette.ClearanceTeal);
        UiText.Draw($"  UNDER-DESK VENT:      {_gameState.VentStashItems.Count} / {_config.VentStashCapacity}",
            Deck4RightX + 16, Deck4RightY + 254, 10, dosAmber);
        UiText.Draw($"  SHIELDED LINING:      {(_gameState.HasShieldedPocketLining ? "INSTALLED (1 SAFE)" : (_gameState.ShieldedLiningOrdered ? "IN TRANSIT (0500)" : "NOT INSTALLED"))}",
            Deck4RightX + 16, Deck4RightY + 272, 10, _gameState.HasShieldedPocketLining ? Palette.ClearanceTeal : dosDim);
        UiText.Draw($"  0500 CHUTE QUEUED:    {_gameState.PendingChuteDeliveries.Count} ITEM(S)",
            Deck4RightX + 16, Deck4RightY + 290, 10, dosDim);

        int activeCount = _gameState.ActiveInterceptMissionIds.Count + (string.IsNullOrWhiteSpace(_gameState.ActiveCovertMissionId) ? 0 : 1);
        UiText.Draw($"  ACTIVE CONTRACTS:     {activeCount}  |  COMPLETED: {_gameState.CovertTasksCompleted}",
            Deck4RightX + 16, Deck4RightY + 308, 10, Palette.ClearanceTeal);
        UiText.Draw("  STASH SLATES IN VENT & SCRUB LOGS BEFORE SLEEPING AT 3/3.",
            Deck4RightX + 16, Deck4RightY + 344, 10, dosDim);
    }

    // =========================================================================
    // BOTTOM TERMINAL STATUS BAR: (88, 624, 1104, 40) -- Pure SC, Always Visible
    // =========================================================================
    private void DrawStatusFooter()
    {
        Raylib.DrawRectangle(FrameX + 2, FooterY - 6, FrameW - 4, FooterH + 12, new Color(22, 27, 34, 255));

        Raylib.DrawRectangle(FooterX, FooterY, FooterW, FooterH, new Color(12, 16, 22, 255));
        Raylib.DrawRectangleLines(FooterX, FooterY, FooterW, FooterH, Palette.WeatheredGunmetalEdge);

        string rightText = !string.IsNullOrEmpty(_statusMessage)
            ? TruncateText(_statusMessage, 10, 460)
            : "[ESC] CLOSE";
        Color rightCol = !string.IsNullOrEmpty(_statusMessage)
            ? _statusColor
            : Palette.WeatheredGunmetalEdge;
        int rw = UiText.MeasureWidth(rightText, 10);
        int leftMaxW = Math.Max(120, FooterW - rw - 40);

        string leftStats = $"PACT SCRIP: {_gameState.PactScrip} SC   |   COAT POCKET: {_gameState.PocketItems.Count} / {_gameState.PocketCapacity}   |   VENT STASH: {_gameState.VentStashItems.Count} / {_config.VentStashCapacity}";
        UiText.Draw(TruncateText(leftStats, 10, leftMaxW), FooterX + 14, FooterY + 15, 10, Palette.CrtAmberPrimary);
        UiText.Draw(rightText, FooterX + FooterW - rw - 14, FooterY + 15, 10, rightCol);
    }

    // =========================================================================
    // UI DRAWING PRIMITIVES
    // =========================================================================
    private static void DrawPactSkuRow(int x, int y, int w, int h, Color border, Color titleCol, string title, string sub)
    {
        Raylib.DrawRectangle(x, y, w, h, new Color(22, 21, 13, 255));
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int textMaxW = Math.Max(40, w - 216);
        UiText.Draw(TruncateText(title, 10, textMaxW), x + 12, y + 8, 10, titleCol);
        UiText.Draw(TruncateText(sub, 10, textMaxW), x + 12, y + 25, 10, Palette.FromHex("#968826"));
    }

    private void DrawCryptoActionBtn(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string title,
        string subtitle,
        bool enabled,
        Color accent)
    {
        bool hov = !_hackGlitchActive && enabled && mouse.IsHit(x, y, w, h);
        Color bg = !enabled ? new Color(10, 14, 12, 255) : (hov ? new Color(24, 44, 30, 255) : new Color(14, 24, 18, 255));
        Color border = !enabled ? Palette.WeatheredGunmetalMid : (hov ? Palette.CrtAmberPrimary : accent);
        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        UiText.Draw(title, x + 12, y + 7, 10, enabled ? accent : Palette.WeatheredGunmetalEdge);
        UiText.Draw(TruncateText(subtitle, 10, w - 24), x + 12, y + 24, 10, enabled ? new Color(184, 210, 194, 255) : Palette.WeatheredGunmetalEdge);
    }

    private void DrawActionButton(
        VirtualMouseState mouse,
        int x,
        int y,
        int w,
        int h,
        string label,
        Color accent,
        bool enabled)
    {
        bool hov = !_hackGlitchActive && enabled && mouse.IsHit(x, y, w, h);
        Color bg = !enabled ? new Color(14, 16, 20, 255) : (hov ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Color border = !enabled ? Palette.WeatheredGunmetalMid : (hov ? Palette.CrtAmberPrimary : accent);
        Color fg = !enabled ? Palette.WeatheredGunmetalEdge : (hov ? Palette.CrtAmberPrimary : accent);
        Raylib.DrawRectangle(x, y, w, h, bg);
        Raylib.DrawRectangleLines(x, y, w, h, border);
        int tw = UiText.MeasureWidth(label, 10);
        UiText.Draw(label, x + Math.Max(6, (w - tw) / 2), y + (h - 10) / 2, 10, fg);
    }

    private static void DrawPixelScrollButtons(
        VirtualMouseState mouse,
        int x,
        int upY,
        int downY,
        bool canUp,
        bool canDown)
    {
        Raylib.DrawRectangle(x, upY + 16, 16, Math.Max(0, downY - (upY + 16)), new Color(12, 16, 22, 255));
        Raylib.DrawRectangleLines(x, upY + 16, 16, Math.Max(0, downY - (upY + 16)), Palette.WeatheredGunmetalMid);

        bool upHov = canUp && mouse.IsHit(x, upY, 16, 16);
        Raylib.DrawRectangle(x, upY, 16, 16, upHov ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, upY, 16, 16, canUp ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalMid);
        Color uCol = canUp ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawPixel(x + 8, upY + 5, uCol);
        Raylib.DrawLine(x + 7, upY + 6, x + 9, upY + 6, uCol);
        Raylib.DrawLine(x + 6, upY + 7, x + 10, upY + 7, uCol);
        Raylib.DrawLine(x + 5, upY + 8, x + 11, upY + 8, uCol);

        bool dnHov = canDown && mouse.IsHit(x, downY, 16, 16);
        Raylib.DrawRectangle(x, downY, 16, 16, dnHov ? Palette.OxidizedBronzeShadow : Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, downY, 16, 16, canDown ? Palette.CrtAmberDimmed : Palette.WeatheredGunmetalMid);
        Color dCol = canDown ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge;
        Raylib.DrawLine(x + 5, downY + 6, x + 11, downY + 6, dCol);
        Raylib.DrawLine(x + 6, downY + 7, x + 10, downY + 7, dCol);
        Raylib.DrawLine(x + 7, downY + 8, x + 9, downY + 8, dCol);
        Raylib.DrawPixel(x + 8, downY + 9, dCol);
    }

    private static void DrawDashedRect(int x, int y, int w, int h, Color col)
    {
        for (int dx = 0; dx < w; dx += 12)
        {
            int seg = Math.Min(6, w - dx);
            Raylib.DrawLine(x + dx, y, x + dx + seg, y, col);
            Raylib.DrawLine(x + dx, y + h - 1, x + dx + seg, y + h - 1, col);
        }
        for (int dy = 0; dy < h; dy += 12)
        {
            int seg = Math.Min(6, h - dy);
            Raylib.DrawLine(x, y + dy, x, y + dy + seg, col);
            Raylib.DrawLine(x + w - 1, y + dy, x + w - 1, y + dy + seg, col);
        }
    }

    private static readonly Regex AuthoringTodoTagRegex = new(
        @"\s*\[TODO(?::[^\]]*)?\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static string StripAuthoringTodoTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (!text.Contains("[TODO", StringComparison.OrdinalIgnoreCase))
            return text;

        return AuthoringTodoTagRegex.Replace(text, string.Empty).TrimEnd();
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

    private static List<string> WrapMultilineText(string text, int fontSize, int maxPx)
    {
        List<string> result = [];
        if (string.IsNullOrEmpty(text))
            return result;

        string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
        foreach (string para in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(para))
            {
                result.Add(string.Empty);
                continue;
            }

            if (UiText.MeasureWidth(para, fontSize) <= maxPx)
            {
                result.Add(para);
                continue;
            }

            string[] words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;
            foreach (string word in words)
            {
                string cand = current.Length == 0 ? word : $"{current} {word}";
                if (UiText.MeasureWidth(cand, fontSize) <= maxPx)
                {
                    current = cand;
                }
                else
                {
                    if (current.Length > 0) result.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0) result.Add(current);
        }
        return result;
    }
}
