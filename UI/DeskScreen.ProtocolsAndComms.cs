using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

public sealed partial class DeskScreen
{
    // =========================================================================
    // Pre-Shift Live Kordani Viewscreen Hails (Day 1 Commander Vorst & Day 3 Officer)
    // =========================================================================
    private void StartDay1PreShiftVorstLiveHail()
    {
        _preShiftStage = PreShiftStage.Day1VorstLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer officerBuf = PortraitRenderer.RenderKordaniSecurityOfficerBuffer();
        _cachedPreShiftKordaniPortraitTex = officerBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.Day1CommanderVorstHailLines;

        AudioHooks.Play("hail_incoming_chime");
        AudioHooks.BeginEncounterVoice(
            "Kordani",
            (int)DeterministicRng.HashSeedFnv1a("day1-vorst-hail"),
            useRadioSquelch: false);

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        AddSystemLogLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.Day1VorstOfficerName}", CommsTextTone.AlertCrimson);

        for (int i = 0; i < _kordaniHailLines.Length; i++)
        {
            EnqueueTalkingHeadLine(
                _kordaniHailLines[i],
                CommsTextTone.AmberPrimary,
                preDelaySeconds: i == 0 ? 0.25f : 0f,
                onStart: () => AudioHooks.Play("comms_reply_blip"));
        }
    }

    private void StartDay2PreShiftNixaLiveHail()
    {
        _preShiftStage = PreShiftStage.Day2NixaLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer nixaBuf = PortraitRenderer.RenderRegeerNixaBuffer();
        _cachedPreShiftKordaniPortraitTex = nixaBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.Day2RegeerNixaHailLines;

        AudioHooks.Play("hail_incoming_chime");
        AudioHooks.BeginEncounterVoice(
            "Brengop",
            (int)DeterministicRng.HashSeedFnv1a("regeer-nixa-hail"),
            useRadioSquelch: false);

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        AddSystemLogLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.RegeerNixaOfficerName}", CommsTextTone.AmberPrimary);

        for (int i = 0; i < _kordaniHailLines.Length; i++)
        {
            EnqueueTalkingHeadLine(
                _kordaniHailLines[i],
                CommsTextTone.AmberPrimary,
                preDelaySeconds: i == 0 ? 0.25f : 0f,
                onStart: () => AudioHooks.Play("comms_reply_blip"));
        }
    }

    private void StartDay8PreShiftNixaLiveHail()
    {
        _preShiftStage = PreShiftStage.Day8NixaLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer nixaBuf = PortraitRenderer.RenderRegeerNixaBuffer();
        _cachedPreShiftKordaniPortraitTex = nixaBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.Day8RegeerNixaHailLines;

        AudioHooks.Play("hail_incoming_chime");
        AudioHooks.BeginEncounterVoice(
            "Brengop",
            (int)DeterministicRng.HashSeedFnv1a("regeer-nixa-hail"),
            useRadioSquelch: false);

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        AddSystemLogLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.RegeerNixaOfficerName}", CommsTextTone.AmberPrimary);

        for (int i = 0; i < _kordaniHailLines.Length; i++)
        {
            EnqueueTalkingHeadLine(
                _kordaniHailLines[i],
                CommsTextTone.AmberPrimary,
                preDelaySeconds: i == 0 ? 0.25f : 0f,
                onStart: () => AudioHooks.Play("comms_reply_blip"));
        }
    }

    private void StartDay3PreShiftKordaniLiveHail()
    {
        _preShiftStage = PreShiftStage.Day3KordaniLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer officerBuf = PortraitRenderer.RenderKordaniSecurityOfficerBuffer();
        _cachedPreShiftKordaniPortraitTex = officerBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.BuildDay4KordaniHailLines(
            _gameState.GhostRouteSuspectDetained,
            _gameState.Day2SuspectRegistration,
            _gameState.Day2SuspectDeclaredOrigin);

        AudioHooks.Play("hail_incoming_chime");
        AudioHooks.BeginEncounterVoice(
            "Kordani",
            (int)DeterministicRng.HashSeedFnv1a("day1-vorst-hail"),
            useRadioSquelch: false);

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        // Wire Day 4 Morning Shift Directive (Variants A & B based on GhostRouteSuspectDetained)
        // immediately prior to the mandatory Day 4 Live Commander Vorst Hail while timer remains paused at t = 0
        string[] day4DirectiveLines = Day2And3ScriptedDialogue.BuildDay4ShiftDirectiveLines(
            _gameState.GhostRouteSuspectDetained,
            _gameState.Day2SuspectRegistration,
            _gameState.Day2SuspectDeclaredOrigin);
        for (int i = 0; i < day4DirectiveLines.Length; i++)
        {
            AddSystemLogLine(day4DirectiveLines[i], i == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
        }
        AddSystemLogLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.Day3KordaniOfficerName}", CommsTextTone.AlertCrimson);

        for (int i = 0; i < _kordaniHailLines.Length; i++)
        {
            EnqueueTalkingHeadLine(
                _kordaniHailLines[i],
                CommsTextTone.AmberPrimary,
                preDelaySeconds: i == 0 ? 0.25f : 0f,
                onStart: () => AudioHooks.Play("comms_reply_blip"));
        }
    }

    private void StartDay15PreShiftForemanLiveHail()
    {
        _preShiftStage = PreShiftStage.Day15ForemanLiveHail;
        _shiftController.IsPreShiftSequenceActive = true;
        _activeShip = null;

        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        PixelBuffer nixaBuf = PortraitRenderer.RenderRegeerNixaBuffer();
        _cachedPreShiftKordaniPortraitTex = nixaBuf.ToTexture2D();

        _kordaniHailLines = Day2And3ScriptedDialogue.Day15BrengopForemanHailLines;

        AudioHooks.Play("hail_incoming_chime");
        AudioHooks.BeginEncounterVoice(
            "Brengop",
            (int)DeterministicRng.HashSeedFnv1a("regeer-nixa-hail"),
            useRadioSquelch: false);

        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;

        AddSystemLogLine($"[INTERNAL HAIL] {Day2And3ScriptedDialogue.RegeerNixaOfficerName}", CommsTextTone.AmberPrimary);

        for (int i = 0; i < _kordaniHailLines.Length; i++)
        {
            EnqueueTalkingHeadLine(
                _kordaniHailLines[i],
                CommsTextTone.AmberPrimary,
                preDelaySeconds: i == 0 ? 0.25f : 0f,
                onStart: () => AudioHooks.Play("comms_reply_blip"));
        }
    }

    private void HandlePreShiftStageInput(VirtualMouseState mouse)
    {
        if (!IsPreShiftTalkingHeadHailActive)
            return;

        if (Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            HandlePreShiftAdvanceAction();
            return;
        }

        if (!mouse.LeftPressed)
            return;

        if (mouse.IsHit(_layout.CommsStrip.X, _layout.CommsStrip.Y, _layout.CommsStrip.W, _layout.CommsStrip.H))
        {
            // If lines are still streaming or held, flush all immediately; otherwise acknowledge and begin watch
            if (_commsQueue.HasPendingOrActive)
            {
                _commsQueue.FlushAllImmediately();
                AudioHooks.Play("terminal_key_click");
                return;
            }

            ClosePreShiftLiveHail();
        }
    }

    private void HandlePreShiftAdvanceAction()
    {
        if (_commsQueue.IsTypingActiveLine)
        {
            _commsQueue.FinishActiveLineImmediately(_config.TalkingHeadAdvanceMode, _config.TalkingHeadLinePauseSeconds);
            AudioHooks.Play("terminal_key_click");
            return;
        }

        if (_commsQueue.IsWaitingTalkingHeadClickAdvance)
        {
            _commsQueue.AdvanceTalkingHeadAfterClick();
            AudioHooks.Play("terminal_key_click");
            return;
        }

        if (_commsQueue.IsWaitingTalkingHeadAutoPause)
        {
            _commsQueue.SkipTalkingHeadPauseImmediately();
            AudioHooks.Play("terminal_key_click");
            return;
        }

        if (_commsQueue.IsWaitingPreDelay)
        {
            _commsQueue.SkipPreDelayImmediately();
            AudioHooks.Play("terminal_key_click");
            return;
        }

        if (!_commsQueue.HasPendingOrActive)
        {
            ClosePreShiftLiveHail();
        }
    }

    private void ClosePreShiftLiveHail()
    {
        AudioHooks.StopActiveCommsVoices();
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);

        PreShiftStage completedStage = _preShiftStage;
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;

        AudioHooks.Play("terminal_key_confirm");

        if (completedStage == PreShiftStage.Day1VorstLiveHail)
        {
            _gameState.Day1VorstHailCompleted = true;
            _gameState.SaveToDisk();

            AddSystemLogLine("[SYS] INTERNAL HAIL CLOSED -- BOOTH 04 CONSOLE ONLINE.", CommsTextTone.ClearanceTeal);
            string[] day1Directive = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                _gameState.Day,
                _gameState.Day2SuspectDetained,
                _gameState.Day2SuspectRegistration,
                _gameState.Day2SuspectDeclaredOrigin,
                _config);
            for (int d = 0; d < day1Directive.Length; d++)
            {
                AddSystemLogLine(day1Directive[d], d == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
            }
            AddSystemLogLine("PRESS [NEXT HAIL] TO BEGIN DAY 01 WATCH.", CommsTextTone.AmberDimmed);
        }
        else if (completedStage == PreShiftStage.Day2NixaLiveHail)
        {
            _gameState.Day2NixaHailCompleted = true;
            _gameState.SaveToDisk();

            AddSystemLogLine("[SYS] CONSORTIUM HAIL CLOSED -- SPS PROBE ONLINE.", CommsTextTone.ClearanceTeal);
            string[] day2Directive = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                _gameState.Day,
                _gameState.Day2SuspectDetained,
                _gameState.Day2SuspectRegistration,
                _gameState.Day2SuspectDeclaredOrigin,
                _config);
            for (int d = 0; d < day2Directive.Length; d++)
            {
                AddSystemLogLine(day2Directive[d], d == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
            }
            AddSystemLogLine("PRESS [NEXT HAIL] TO BEGIN DAY 02 WATCH.", CommsTextTone.AmberDimmed);
        }
        else if (completedStage == PreShiftStage.Day8NixaLiveHail)
        {
            _gameState.Day8NixaHailCompleted = true;
            _gameState.SaveToDisk();

            AddSystemLogLine("[SYS] CONSORTIUM HAIL CLOSED -- BRENGOP REFERENCE MANUAL ISSUED.", CommsTextTone.ClearanceTeal);
            string[] day8Directive = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                _gameState.Day,
                _gameState.Day2SuspectDetained,
                _gameState.Day2SuspectRegistration,
                _gameState.Day2SuspectDeclaredOrigin,
                _config);
            for (int d = 0; d < day8Directive.Length; d++)
            {
                AddSystemLogLine(day8Directive[d], d == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
            }
            AddSystemLogLine($"PRESS [NEXT HAIL] TO BEGIN DAY {_gameState.Day:D2} WATCH.", CommsTextTone.AmberDimmed);
        }
        else if (completedStage == PreShiftStage.Day15ForemanLiveHail)
        {
            _gameState.Day15ForemanHailCompleted = true;
            _gameState.SaveToDisk();

            AddSystemLogLine("[SYS] CONSORTIUM HAIL CLOSED -- STELLAR STOKER MASS DIAL ONLINE.", CommsTextTone.ClearanceTeal);
            string[] day15Directive = Day2And3ScriptedDialogue.BuildDailyShiftDirectiveLines(
                _gameState.Day,
                _gameState.Day2SuspectDetained,
                _gameState.Day2SuspectRegistration,
                _gameState.Day2SuspectDeclaredOrigin,
                _config);
            for (int d = 0; d < day15Directive.Length; d++)
            {
                AddSystemLogLine(day15Directive[d], d == 0 ? CommsTextTone.AmberPrimary : CommsTextTone.AmberDimmed);
            }
            AddSystemLogLine($"PRESS [NEXT HAIL] TO BEGIN DAY {_gameState.Day:D2} WATCH.", CommsTextTone.AmberDimmed);
        }
        else
        {
            _gameState.Day4PreShiftHailCompleted = true;
            _gameState.SecurityStrikesActive = true;
            _gameState.SaveToDisk();

            AddSystemLogLine("[SYS] INTERNAL HAIL CLOSED -- WARP SPECTROMETER & SECURITY STRIKES ONLINE.", CommsTextTone.ClearanceTeal);
            AddSystemLogLine($"PRESS [NEXT HAIL] TO BEGIN DAY {_gameState.Day:D2} WATCH.", CommsTextTone.AmberDimmed);
        }

        TryAutoUnfoldBookletForCurrentDay();
    }

    // =========================================================================
    // Tactile Inspection Weight: 0.5s PROTOCOL FAULT Beat Integrated into Typewriter Queue
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
        _activeShip.FlaggedInfractions.Add(violationType);
        _verifiedViolationSummary = summary;
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _reviewStatusNote = "PROTOCOL VIOLATED";
        _duplicateFaultNoteTimer = 0f;
        _noFaultTraceTimer = 0f;
        _noFaultTargetA = ReviewTarget.None;
        _noFaultTargetB = ReviewTarget.None;
        _faultTraceTimer = FaultTraceDurationSeconds;

        BuildConfrontationLines(violationType, out string officerPrompt, out string captainExcuse);

        // Cleanly complete any earlier routine dialogue so the 0.5s PROTOCOL FAULT beat never collides or overlaps
        _commsQueue.FlushAllImmediately(invokeCallbacks: false);

        _faultStage = FaultSequenceStage.FaultBeat;
        _faultHighlightA = highlightA;
        _faultHighlightB = highlightB;

        AudioHooks.Play("protocol_fault_pulse");

        string tag = IsProtocolReviewUnlocked ? "[PROTOCOL FAULT]" : "[SPS FAULT]";
        ShipSpec targetShip = _activeShip;

        // 1. 0.50s PROTOCOL FAULT beat -> commits fault banner cleanly
        AddSystemLogLine(
            $"{tag} {summary}",
            CommsTextTone.AlertCrimson,
            preDelaySeconds: FaultBeatDurationSeconds,
            onStart: () =>
            {
                if (_activeShip != targetShip)
                    return;
                AudioHooks.Play("terminal_key_confirm");
                if (_faultStage == FaultSequenceStage.FaultBeat)
                {
                    _faultStage = FaultSequenceStage.OfficerAccusationPause;
                }
            });

        // 2. Officer accusation streams at 65 chars/sec -> triggers viewscreen static flicker on completion
        EnqueueOfficerLine(
            officerPrompt,
            CommsTextTone.ClearanceTeal,
            onComplete: () =>
            {
                if (_activeShip == targetShip)
                {
                    _portraitStaticFlickerTimer = 0.28f;
                }
            });

        // 3. Automatic 0.55-0.65s (0.60s) [RECEIVING...] subspace lag beat -> Captain excuse streams cleanly
        EnqueueCaptainLine(
            $"CAPT: \"{captainExcuse}\"",
            CommsTextTone.AmberPrimary,
            onStart: () =>
            {
                if (_activeShip != targetShip)
                    return;
                AudioHooks.Play("comms_reply_blip");
                _faultStage = FaultSequenceStage.None;
            });
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

            case ShipInfractionType.SpokenRouteDiscrepancy:
                officerPrompt = $"You stated departure from {_activeShip.GroundTruth.SpokenOriginSystem} on comms, contradicting your filed origin {_activeShip.Slate.OriginSystem}.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseSpokenRoute;
                break;

            case ShipInfractionType.SpokenCargoDiscrepancy:
                officerPrompt = $"You declared {_activeShip.GroundTruth.SpokenCargoItem} on comms, contradicting your Cargo Manifest ({_activeShip.GroundTruth.WaferCargoManifestItem}).";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseSpokenCargo;
                break;

            case ShipInfractionType.BannedCargoItem:
                officerPrompt = $"Your Cargo Manifest lists {_activeShip.GroundTruth.WaferCargoManifestItem}, which is permanently banned at Void Gate 3.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseBannedCargo;
                break;

            case ShipInfractionType.CargoManifestDiscrepancy:
                officerPrompt = $"Your Cargo Manifest cert-wafer lists {_activeShip.GroundTruth.WaferCargoManifestItem}, contradicting your Data Slate ({_activeShip.Slate.CargoManifest}).";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseManifestMismatch;
                break;

            case ShipInfractionType.WithheldIllicitCargoManifest:
                officerPrompt = "Your subspace bundle omitted the mandatory Cargo Manifest cert-wafer. Produce it or prepare for detention.";
                captainExcuse = _activeShip.ConfrontationGuiltyExcuseWithheldManifest;
                break;

            case ShipInfractionType.StokerMassMismatch:
                // TODO: USER DIALOGUE PASS
                officerPrompt = $"Stellar Stoker anchor reads {_activeShip.GroundTruth.ActualMass:N0} T, exceeding your declared {_activeShip.Slate.DeclaredTonnage:N0} T.";
                captainExcuse = string.IsNullOrWhiteSpace(_activeShip.ConfrontationGuiltyExcuseMassMismatch)
                    ? "Ballast trim must have run heavy. That scale's off!" // TODO: USER DIALOGUE PASS
                    : _activeShip.ConfrontationGuiltyExcuseMassMismatch;
                break;

            default:
                officerPrompt = "Protocol discrepancy verified on your vessel.";
                captainExcuse = _activeShip.RefusalLine;
                break;
        }
    }

    // =========================================================================
    // Day 1+ Protocol Review Mode (Claim vs. Reality)
    // =========================================================================
    private bool IsReviewTargetUnlocked(ReviewTarget target) =>
        target switch
        {
            ReviewTarget.DataSlatePrefix => true,
            ReviewTarget.VisualSignalTransponderId => true,
            ReviewTarget.DataSlateWeapons => IsSpsUnlocked,
            ReviewTarget.SpsLensConduit => IsSpsUnlocked,
            ReviewTarget.SpsMiniScreenHotWeapons => IsSpsUnlocked,
            ReviewTarget.DataSlateOrigin => IsSpectrometerOperational,
            ReviewTarget.WarpStripPaper => IsSpectrometerOperational,
            ReviewTarget.TopHeaderCycle => IsManualUnlocked,
            ReviewTarget.DataSlateOrg => IsManualUnlocked,
            ReviewTarget.DataSlateExpiry => IsManualUnlocked,
            ReviewTarget.DataSlateSpecies => IsManualUnlocked,
            ReviewTarget.DataSlateClass => IsManualHullsEnforced,
            ReviewTarget.ViewportShipHull => IsManualHullsEnforced || _captainRefusedCompliance,
            ReviewTarget.DataSlateCargo => IsCargoPowerUnlocked,
            ReviewTarget.SpsMiniScreenCargoPower => IsCargoPowerUnlocked,
            ReviewTarget.ReferenceManualPrefixEntry => IsManualUnlocked,
            ReviewTarget.ReferenceManualSpeciesEntry => IsManualUnlocked,
            ReviewTarget.ReferenceManualHullEntry => IsManualHullsEnforced,
            ReviewTarget.CommsTranscriptStatement => IsSpectrometerOperational || IsCargoPowerUnlocked,
            ReviewTarget.CargoManifestWafer => IsWaferWriterOperational,
            ReviewTarget.ReferenceManualCargoBannedEntry => IsCargoManualTabUnlocked,
            ReviewTarget.WaferWriterModule => IsWaferWriterOperational,
            ReviewTarget.DataSlateMass => IsStokerOperational,
            ReviewTarget.StokerMeasuredMassLed => IsStokerOperational && _stokerMassAnchoredForShip,
            _ => false
        };

    private bool TryHandleProtocolReviewClick(VirtualMouseState mouse)
    {
        if (_faultStage != FaultSequenceStage.None)
            return false;

        // Allow clicking [ANCHOR] on the Stellar Stoker even while Protocol Review mode is active
        if (IsStokerOperational && mouse.IsHit(StokerAnchorBtnX, StokerAnchorBtnY, StokerAnchorBtnW, StokerAnchorBtnH))
        {
            TryAnchorStokerMassForActiveShip();
            return true;
        }

        ReviewTarget hitTarget = DetectProtocolReviewTargetAtMouse(mouse);
        if (hitTarget == ReviewTarget.None || _activeShip == null)
            return false;

        if (hitTarget == ReviewTarget.ReferenceManualPrefixEntry &&
            _deskItems.TryGetValue(DeskItemId.ReferenceManual, out DraggableDeskItem? manualItem))
        {
            int rowStartY = manualItem.Y + 39;
            int clickedIdx = (mouse.Y - rowStartY) / 12;
            if (clickedIdx >= 0 && clickedIdx < ManualPrefixDirectory.Length)
            {
                SyncManualSelectionToDirectoryIndex(clickedIdx);
            }
        }

        AudioHooks.Play("terminal_key_click");

        // Standalone violation: Day 14+ compliance refusal
        if (hitTarget == ReviewTarget.ViewportShipHull && _captainRefusedCompliance)
        {
            TryTriggerOrReportDuplicateFault(
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

        if (IsReviewTargetUnlocked(ReviewTarget.TopHeaderCycle) &&
            mouse.IsHit(TopCycleX, TopCycleY, TopCycleW, TopCycleH))
        {
            return ReviewTarget.TopHeaderCycle;
        }

        if (IsReviewTargetUnlocked(ReviewTarget.VisualSignalTransponderId) &&
            mouse.IsHit(_layout.FeedBox.X, _layout.FeedBox.Y, _layout.FeedBox.W, _layout.FeedBox.H))
        {
            return ReviewTarget.VisualSignalTransponderId;
        }

        // Clickable Captain Auto-Interrogation Statement inside CRT Comms Log (scroll-aware hitbox)
        if (IsReviewTargetUnlocked(ReviewTarget.CommsTranscriptStatement) &&
            TryGetVisibleTranscriptStatementRect(out Rectangle stmtRect) &&
            mouse.IsHit(stmtRect))
        {
            return ReviewTarget.CommsTranscriptStatement;
        }

        // Day 12+ Printed Cargo Manifest Cert-Wafers on desk (topmost first)
        if (IsReviewTargetUnlocked(ReviewTarget.CargoManifestWafer) && _deskWafers.Count > 0)
        {
            for (int wIdx = _deskWafers.Count - 1; wIdx >= 0; wIdx--)
            {
                CertWaferCard wCard = _deskWafers[wIdx];
                if (mouse.IsHit(wCard.X, wCard.Y, WaferCardW, WaferCardH))
                {
                    if (string.Equals(wCard.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
                        return ReviewTarget.CargoManifestWafer;
                    break;
                }
            }
        }

        // Day 15+ Stellar Stoker 2-line Digital LED readout (once [ANCHOR] has measured physical mass)
        if (IsReviewTargetUnlocked(ReviewTarget.StokerMeasuredMassLed) &&
            mouse.IsHit(StokerLedX, StokerLedY, StokerLedW, StokerLedH))
        {
            return ReviewTarget.StokerMeasuredMassLed;
        }

        // Day 12+ Wafer Writer module (for flagging omitted / withheld Cargo Manifest packet)
        if (IsReviewTargetUnlocked(ReviewTarget.WaferWriterModule) &&
            mouse.IsHit(_layout.WaferWriter.X, _layout.WaferWriter.Y, _layout.WaferWriter.W, _layout.WaferWriter.H))
        {
            return ReviewTarget.WaferWriterModule;
        }

        // Day 2+ SPS Anomaly Mini-Screen: strictly selectable evidence source inside Protocol Review mode
        if (IsSpsUnlocked && _spsLoggedAnomalies.Count > 0)
        {
            int maxRows = Math.Min(3, _spsLoggedAnomalies.Count);
            for (int i = 0; i < maxRows; i++)
            {
                SpsAnomalyEntry entry = _spsLoggedAnomalies[i];
                if (entry.IsCooled)
                    continue;

                ReviewTarget rowTarget = entry.Kind == SpsAnomalyKind.HotWeaponMount
                    ? ReviewTarget.SpsMiniScreenHotWeapons
                    : ReviewTarget.SpsMiniScreenCargoPower;
                if (!IsReviewTargetUnlocked(rowTarget))
                    continue;

                int ry = SpsMiniY + 24 + i * 36;
                if (mouse.IsHit(SpsMiniX + 4, ry, SpsMiniW - 8, 30))
                {
                    return rowTarget;
                }
            }
        }

        if (IsReviewTargetUnlocked(ReviewTarget.SpsLensConduit) && _spsEnabled && mouse.IsHit(_lastSpsLensRect))
        {
            Rectangle shipRect = GetActiveShipHullScreenRect();
            bool lensOverlapsShip =
                _lastSpsLensRect.X < shipRect.X + shipRect.Width &&
                _lastSpsLensRect.X + _lastSpsLensRect.Width > shipRect.X &&
                _lastSpsLensRect.Y < shipRect.Y + shipRect.Height &&
                _lastSpsLensRect.Y + _lastSpsLensRect.Height > shipRect.Y;

            if (lensOverlapsShip)
                return ReviewTarget.SpsLensConduit;
        }

        if (IsReviewTargetUnlocked(ReviewTarget.ViewportShipHull) &&
            mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH))
        {
            return ReviewTarget.ViewportShipHull;
        }

        if (IsReviewTargetUnlocked(ReviewTarget.WarpStripPaper) &&
            _warpPhase == WarpPrinterPhase.AnchoredReadyToTear &&
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

            if (id == DeskItemId.WarpStrip &&
                IsReviewTargetUnlocked(ReviewTarget.WarpStripPaper) &&
                _warpPhase == WarpPrinterPhase.TornOffFreeDocument)
            {
                return ReviewTarget.WarpStripPaper;
            }

            if (id == DeskItemId.DataSlate && IsShipInspectableForReview && IsSlateReadableForReview)
            {
                int rightHotW = item.Width - 78; // 106px

                if (_gameState.Day == 1)
                {
                    if (mouse.IsHit(item.X + 73, item.Y + 22, rightHotW, 30) ||
                        mouse.IsHit(item.X + 73, item.Y + 55, rightHotW, 26))
                    {
                        return ReviewTarget.DataSlatePrefix;
                    }
                    return ReviewTarget.None;
                }

                if (_gameState.Day < _config.DayUnlockWarpScanner)
                {
                    if (mouse.IsHit(item.X + 73, item.Y + 21, rightHotW, 26))
                        return ReviewTarget.DataSlatePrefix;

                    if (mouse.IsHit(item.X + 73, item.Y + 50, rightHotW, 15))
                        return ReviewTarget.DataSlateWeapons;

                    return ReviewTarget.None;
                }

                if (mouse.IsHit(item.X + 73, item.Y + 21, rightHotW, 22))
                    return ReviewTarget.DataSlatePrefix;

                if (mouse.IsHit(item.X + 73, item.Y + 44, rightHotW, 13))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateClass) ? ReviewTarget.DataSlateClass : ReviewTarget.None;

                if (mouse.IsHit(item.X + 73, item.Y + 58, rightHotW, 13))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateWeapons) ? ReviewTarget.DataSlateWeapons : ReviewTarget.None;

                if (mouse.IsHit(item.X + 73, item.Y + 72, rightHotW, 13))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateMass) ? ReviewTarget.DataSlateMass : ReviewTarget.None;

                int rowW = item.Width - 8;
                if (mouse.IsHit(item.X + 4, item.Y + 103, rowW, 12))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateOrg) ? ReviewTarget.DataSlateOrg : ReviewTarget.None;

                if (mouse.IsHit(item.X + 4, item.Y + 115, rowW, 12))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateExpiry) ? ReviewTarget.DataSlateExpiry : ReviewTarget.None;

                if (mouse.IsHit(item.X + 4, item.Y + 139, rowW, 12))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateSpecies) ? ReviewTarget.DataSlateSpecies : ReviewTarget.None;

                if (mouse.IsHit(item.X + 4, item.Y + 151, rowW, 12))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateOrigin) ? ReviewTarget.DataSlateOrigin : ReviewTarget.None;

                if (mouse.IsHit(item.X + 4, item.Y + 175, rowW, 24))
                    return IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo) ? ReviewTarget.DataSlateCargo : ReviewTarget.None;
            }
            else if (id == DeskItemId.ReferenceManual && IsManualUnlocked)
            {
                int mx = item.X;
                int my = item.Y;
                int maxTab = IsCargoManualTabUnlocked ? 3 : (IsManualHullsEnforced ? 2 : 1);
                int activeTab = Math.Clamp(_manualActiveTab, 0, maxTab);

                // Leave tab header bar and faction/class cycling buttons free for navigation
                if (mouse.IsHit(mx, my, item.Width, 38))
                    return ReviewTarget.None;

                if (activeTab == 0)
                {
                    int rowsH = ManualPrefixDirectory.Length * 12;
                    if (mouse.IsHit(mx + 6, my + 39, item.Width - 12, rowsH))
                        return ReviewTarget.ReferenceManualPrefixEntry;
                }
                else if (activeTab == 1)
                {
                    if (mouse.IsHit(mx + 6, my + 60, item.Width - 12, item.Height - 66))
                        return ReviewTarget.ReferenceManualSpeciesEntry;
                }
                else if (activeTab == 2 && IsManualHullsEnforced)
                {
                    if (mouse.IsHit(mx + 6, my + 60, item.Width - 12, 88))
                        return ReviewTarget.ReferenceManualHullEntry;
                }
                else if (activeTab == 3 && IsCargoManualTabUnlocked)
                {
                    int bannedRowsH = CargoCatalog.Load().BannedCargoList.Count * 20;
                    if (mouse.IsHit(mx + 6, my + 54, item.Width - 12, bannedRowsH))
                        return ReviewTarget.ReferenceManualCargoBannedEntry;
                }

                return ReviewTarget.None;
            }

            return ReviewTarget.None;
        }

        return ReviewTarget.None;
    }

    private void TryTriggerOrReportDuplicateFault(
        ShipInfractionType violationType,
        string summary,
        ReviewTarget highlightA,
        ReviewTarget highlightB)
    {
        if (_activeShip == null)
            return;

        if (_activeShip.FlaggedInfractions.Contains(violationType))
        {
            _protocolReviewModeActive = false;
            _reviewFirstSelection = ReviewTarget.None;
            _duplicateFaultNoteTimer = DuplicateFaultNoteDurationSeconds;
            _reviewStatusNote = "MISMATCH ALREADY FOUND";
            AddSystemLogLine("[SYS] PROTOCOL REVIEW: MISMATCH ALREADY FOUND.", CommsTextTone.AmberPrimary);
            AudioHooks.Play("terminal_key_click");
            return;
        }

        BeginProtocolFaultSequence(violationType, summary, highlightA, highlightB);
    }

    private void EvaluateProtocolReviewPair(ReviewTarget a, ReviewTarget b)
    {
        if (_activeShip == null)
            return;

        bool IsPair(ReviewTarget x, ReviewTarget y) => (a == x && b == y) || (a == y && b == x);
        static bool IsManualRegistryTarget(ReviewTarget t) =>
            t is ReviewTarget.ReferenceManualPrefixEntry or ReviewTarget.ReferenceManualSpeciesEntry;

        ShipGroundTruth truth = _activeShip.GroundTruth;

        // 0a. Day 15+ Stellar Stoker MEASURED LED vs. Data Slate MASS (or Cargo Manifest Wafer / Data Slate Cargo)
        if (IsPair(ReviewTarget.StokerMeasuredMassLed, ReviewTarget.DataSlateMass) ||
            IsPair(ReviewTarget.StokerMeasuredMassLed, ReviewTarget.CargoManifestWafer) ||
            IsPair(ReviewTarget.StokerMeasuredMassLed, ReviewTarget.DataSlateCargo))
        {
            if (IsStokerOperational && _stokerMassAnchoredForShip)
            {
                if (IsWaferWriterOperational &&
                    truth.ExpectsCargoManifestWafer &&
                    truth.CargoManifestInitiallyOmitted &&
                    !truth.CargoManifestTransmitted &&
                    !truth.OmittedManifestIsDishonest)
                {
                    // Honest captain omitted a legal Cargo Manifest accounting for extra freight mass -> transmits wafer!
                    _protocolReviewModeActive = false;
                    _reviewFirstSelection = ReviewTarget.None;
                    _noFaultTraceTimer = NoFaultTraceDurationSeconds;
                    _noFaultTargetA = a;
                    _noFaultTargetB = b;
                    _reviewStatusNote = "REQUESTING MANIFEST...";
                    _isTransmittingMissingWafer = true;
                    _missingWaferTransmitElapsed = 0f;
                    AudioHooks.Play("terminal_key_confirm");
                    EnqueueOfficerLine("Your hull mass exceeds your Data Slate. Transmit your Cargo Manifest cert-wafer.", CommsTextTone.ClearanceTeal);
                    EnqueueCaptainLine(
                        $"CAPT: \"{_activeShip.HonestOmittedManifestReplyLine}\"",
                        CommsTextTone.AmberPrimary,
                        preDelaySeconds: 0.18f,
                        showReceivingIndicatorDuringPreDelay: true,
                        suppressAutoReceivingLag: true);
                    return;
                }

                StokerCalibrationResult cal = ValidationEngine.EvaluateStokerMassCalibration(
                    _activeShip,
                    _gameState.Day,
                    _config,
                    _stokerSafeLoad,
                    _stokerShock);

                if (!cal.IsCalibrated || truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.StokerMassMismatch))
                {
                    TryTriggerOrReportDuplicateFault(
                        ShipInfractionType.StokerMassMismatch,
                        $"MEASURED {truth.ActualMass:N0} T vs DECLARED {_activeShip.Slate.DeclaredTonnage:N0} T",
                        a, b);
                }
                else
                {
                    LogNoDiscrepancyDetected(a, b);
                }
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 0. Data Slate REG CODE vs. Raw Transponder ID
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.TransponderIdMismatch) ||
                !string.Equals(truth.RawTransponderId, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.TransponderIdMismatch,
                    $"RAW ID '{truth.RawTransponderId}' vs SLATE '{_activeShip.Slate.RegistrationCode}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 1. Data Slate EXP vs. Top Console Station Cycle
        if (IsPair(ReviewTarget.DataSlateExpiry, ReviewTarget.TopHeaderCycle))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.ExpiredRegistration))
            {
                (int curY, int curM) = Constants.GetStationCycle(_gameState.Day);
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.ExpiredRegistration,
                    $"EXPIRED REG ({_activeShip.Slate.RegistrationExpiryDisplay} < CYC {Constants.FormatStationCycle(curY, curM)})",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 2. Data Slate Prefix/ORG/Species vs. Brengop Reference Manual Registrar Entries (or direct Slate Prefix vs Org)
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateOrg) ||
            ((a is ReviewTarget.DataSlatePrefix or ReviewTarget.DataSlateOrg) && IsManualRegistryTarget(b)) ||
            ((b is ReviewTarget.DataSlatePrefix or ReviewTarget.DataSlateOrg) && IsManualRegistryTarget(a)))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixOrganizationMismatch))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.PrefixOrganizationMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs ORG '{_activeShip.Slate.DeclaredFactionName}'",
                    a, b);
            }
            else if (IsManualRegistryTarget(a) || IsManualRegistryTarget(b))
            {
                if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixSpeciesMismatch))
                {
                    TryTriggerOrReportDuplicateFault(
                        ShipInfractionType.PrefixSpeciesMismatch,
                        $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' PROHIBITS {_activeShip.Slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}",
                        a, b);
                }
                else
                {
                    LogNoDiscrepancyDetected(a, b);
                }
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 3. Data Slate Prefix/ORG/SPEC vs. Data Slate SPEC or Brengop Reference Manual Registrar
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.DataSlateSpecies) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.DataSlateSpecies) ||
            (a == ReviewTarget.DataSlateSpecies && IsManualRegistryTarget(b)) ||
            (b == ReviewTarget.DataSlateSpecies && IsManualRegistryTarget(a)))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixSpeciesMismatch))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.PrefixSpeciesMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' PROHIBITS {_activeShip.Slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}",
                    a, b);
            }
            else if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixOrganizationMismatch) &&
                     (IsManualRegistryTarget(a) || IsManualRegistryTarget(b)))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.PrefixOrganizationMismatch,
                    $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs ORG '{_activeShip.Slate.DeclaredFactionName}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 4. Data Slate Prefix/ORG/CLASS or Viewport Hull vs. Viewport Hull or Brengop Reference Manual HULLS tab (Day 10+)
        if (IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.DataSlateClass, ReviewTarget.ViewportShipHull) ||
            IsPair(ReviewTarget.ViewportShipHull, ReviewTarget.ReferenceManualHullEntry) ||
            IsPair(ReviewTarget.DataSlateClass, ReviewTarget.ReferenceManualHullEntry) ||
            IsPair(ReviewTarget.DataSlatePrefix, ReviewTarget.ReferenceManualHullEntry) ||
            IsPair(ReviewTarget.DataSlateOrg, ReviewTarget.ReferenceManualHullEntry))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.PrefixHullSilhouetteMismatch))
            {
                string summary = truth.IsClassSilhouetteMismatch
                    ? $"CLASS '{ShortClass(_activeShip.Slate.DeclaredShipClass)}' vs {ShortClass(truth.ActualShipClass)} HULL"
                    : $"PREFIX '{_activeShip.Slate.DeclaredPrefix}' vs {ShipGenerator.GetHullFactionReadableName(truth.ActualVisualFaction)} HULL";
                TryTriggerOrReportDuplicateFault(ShipInfractionType.PrefixHullSilhouetteMismatch, summary, a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 5. Data Slate WPN: OFFLINE vs. SPS Anomaly Mini-Screen [!] HOT WPN or SPS Lens Conduit
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

                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.SpoofedWeaponsHot,
                    "SPOOFED WEAPONS: SLATE 'OFFLINE' vs HOT SPS MOUNT",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 6. Data Slate CARGO / Cargo Manifest Wafer vs. SPS Anomaly Mini-Screen [!] POWERED CARGO or SPS Lens Conduit
        if (IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.SpsMiniScreenCargoPower) ||
            IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.SpsLensConduit) ||
            IsPair(ReviewTarget.CargoManifestWafer, ReviewTarget.SpsMiniScreenCargoPower) ||
            IsPair(ReviewTarget.CargoManifestWafer, ReviewTarget.SpsLensConduit))
        {
            if (IsCargoPowerUnlocked && truth.ActualCargoPowerDraw && !truth.ActualShieldsUp)
            {
                foreach (SpsAnomalyEntry entry in _spsLoggedAnomalies)
                {
                    if (entry.Kind == SpsAnomalyKind.VioletCargoPower && !entry.IsCooled)
                    {
                        entry.WasVerified = true;
                    }
                }

                if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.BannedCargoItem))
                {
                    TryTriggerOrReportDuplicateFault(
                        ShipInfractionType.BannedCargoItem,
                        $"SPS VIOLET POWERED CONTRABAND ({truth.WaferCargoManifestItem.ToUpperInvariant()})",
                        a, b);
                }
                else
                {
                    TryTriggerOrReportDuplicateFault(
                        ShipInfractionType.UnexplainedCargoPowerDraw,
                        "SPS VIOLET POWER DRAW IN DECLARED HOLD",
                        a, b);
                }
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 7. Data Slate Origin vs. Thermal Warp Strip
        if (IsPair(ReviewTarget.DataSlateOrigin, ReviewTarget.WarpStripPaper))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.FalsifiedOriginWaveform,
                    $"WARP STRIP '{truth.ActualOriginSystem.ToUpperInvariant()}' vs SLATE '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 8. Clickable Comms Transcript Statement vs. Origin (DataSlateOrigin / WarpStripPaper) on Day 4+
        if (IsPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.DataSlateOrigin) ||
            IsPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.WarpStripPaper))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.SpokenRouteDiscrepancy))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.SpokenRouteDiscrepancy,
                    $"COMMS ORIGIN '{truth.SpokenOriginSystem.ToUpperInvariant()}' vs FILED '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'",
                    a, b);
            }
            else if ((a == ReviewTarget.WarpStripPaper || b == ReviewTarget.WarpStripPaper) &&
                     truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.FalsifiedOriginWaveform))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.FalsifiedOriginWaveform,
                    $"WARP STRIP '{truth.ActualOriginSystem.ToUpperInvariant()}' vs SPOKEN '{_activeShip.Slate.OriginSystem.ToUpperInvariant()}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 9. Clickable Comms Transcript Statement vs. Cargo Manifest Cert-Wafer or Data Slate Cargo on Day 12+
        if (IsPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.CargoManifestWafer) ||
            IsPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.DataSlateCargo))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.SpokenCargoDiscrepancy))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.SpokenCargoDiscrepancy,
                    $"COMMS CARGO '{truth.SpokenCargoItem.ToUpperInvariant()}' vs MANIFEST '{truth.WaferCargoManifestItem.ToUpperInvariant()}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 10. Cargo Manifest Cert-Wafer / Data Slate Cargo vs. Manual CARGO Banned List on Day 12+
        if (IsPair(ReviewTarget.CargoManifestWafer, ReviewTarget.ReferenceManualCargoBannedEntry) ||
            IsPair(ReviewTarget.DataSlateCargo, ReviewTarget.ReferenceManualCargoBannedEntry))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.BannedCargoItem) ||
                CargoCatalog.Load().IsBannedCargo(truth.WaferCargoManifestItem, out _) ||
                CargoCatalog.Load().IsBannedCargo(_activeShip.Slate.CargoManifest, out _))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.BannedCargoItem,
                    $"BANNED CONTRABAND CARGO: '{truth.WaferCargoManifestItem.ToUpperInvariant()}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 11. Cargo Manifest Cert-Wafer vs. Data Slate Cargo on Day 12+
        if (IsPair(ReviewTarget.CargoManifestWafer, ReviewTarget.DataSlateCargo))
        {
            if (truth.ActiveInfractions.Any(inf => inf.Type == ShipInfractionType.CargoManifestDiscrepancy) ||
                (!string.IsNullOrWhiteSpace(truth.WaferCargoManifestItem) &&
                 !_activeShip.Slate.CargoManifest.Contains(truth.WaferCargoManifestItem, StringComparison.OrdinalIgnoreCase) &&
                 !truth.WaferCargoManifestItem.Contains("Empty", StringComparison.OrdinalIgnoreCase)))
            {
                TryTriggerOrReportDuplicateFault(
                    ShipInfractionType.CargoManifestDiscrepancy,
                    $"MANIFEST WAFER '{truth.WaferCargoManifestItem.ToUpperInvariant()}' vs SLATE '{_activeShip.Slate.CargoManifest}'",
                    a, b);
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        // 12. Missing / Withheld Cargo Manifest Packet Interrogation (WaferWriterModule vs DataSlateCargo / Prefix / Comms)
        if (a == ReviewTarget.WaferWriterModule || b == ReviewTarget.WaferWriterModule)
        {
            if (IsWaferWriterOperational &&
                truth.ExpectsCargoManifestWafer &&
                truth.CargoManifestInitiallyOmitted &&
                !truth.CargoManifestTransmitted)
            {
                if (!truth.OmittedManifestIsDishonest)
                {
                    _protocolReviewModeActive = false;
                    _reviewFirstSelection = ReviewTarget.None;
                    _noFaultTraceTimer = NoFaultTraceDurationSeconds;
                    _noFaultTargetA = a;
                    _noFaultTargetB = b;
                    _reviewStatusNote = "REQUESTING MANIFEST...";
                    _isTransmittingMissingWafer = true;
                    _missingWaferTransmitElapsed = 0f;
                    AudioHooks.Play("terminal_key_confirm");
                    EnqueueOfficerLine("Your subspace bundle omitted a Cargo Manifest cert-wafer. Transmit it now.", CommsTextTone.ClearanceTeal);
                    EnqueueCaptainLine(
                        $"CAPT: \"{_activeShip.HonestOmittedManifestReplyLine}\"",
                        CommsTextTone.AmberPrimary,
                        preDelaySeconds: 0.18f,
                        showReceivingIndicatorDuringPreDelay: true,
                        suppressAutoReceivingLag: true);
                }
                else
                {
                    TryTriggerOrReportDuplicateFault(
                        ShipInfractionType.WithheldIllicitCargoManifest,
                        "WITHHELD CARGO MANIFEST PACKET",
                        a, b);
                }
            }
            else
            {
                LogNoDiscrepancyDetected(a, b);
            }
            return;
        }

        LogNoDiscrepancyDetected(a, b);
    }

    private void LogNoDiscrepancyDetected(ReviewTarget a, ReviewTarget b)
    {
        _protocolReviewModeActive = false;
        _reviewFirstSelection = ReviewTarget.None;
        _noFaultTraceTimer = NoFaultTraceDurationSeconds;
        _noFaultTargetA = a;
        _noFaultTargetB = b;
        _reviewStatusNote = "PROTOCOLS MATCH // NO FAULT";
        AddSystemLogLine("[SYS] PROTOCOL REVIEW: PROTOCOLS MATCH // NO FAULT.", CommsTextTone.ClearanceTeal);
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
            ReviewTarget.ReferenceManualPrefixEntry => "MANUAL: SHIP REG",
            ReviewTarget.ReferenceManualSpeciesEntry => "MANUAL: SPECIES REG",
            ReviewTarget.ReferenceManualHullEntry => "MANUAL: HULL CHART",
            ReviewTarget.CommsTranscriptStatement => "COMMS STATEMENT",
            ReviewTarget.CargoManifestWafer => "CARGO MANIFEST WAFER",
            ReviewTarget.ReferenceManualCargoBannedEntry => "MANUAL: BANNED CARGO",
            ReviewTarget.WaferWriterModule => "WAFER WRITER DEMUX",
            ReviewTarget.DataSlateMass => "SLATE DECLARED MASS",
            ReviewTarget.StokerMeasuredMassLed => "STOKER MEASURED LED",
            _ => "NONE"
        };

    // =========================================================================
    // Thematic CRT Dialogue Transcript Scrolling & Click-to-Finish (w = 16 amber pixel scrollbar)
    // =========================================================================
    private void HandleTranscriptScrollInput(VirtualMouseState mouse)
    {
        if (MathF.Abs(mouse.WheelMove) < 0.01f)
            return;

        if (!mouse.IsHit(_layout.Transcript.X, _layout.Transcript.Y, _layout.Transcript.W, _layout.Transcript.H))
            return;

        List<WrappedTranscriptRow> wrapped = WrapTranscriptForDisplay(_layout.TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - _layout.TranscriptVisibleRows);
        if (maxTop == 0)
            return;

        int delta = mouse.WheelMove > 0 ? -2 : 2;
        _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine + delta, 0, maxTop);
        _transcriptAutoFollowBottom = (_transcriptScrollTopLine >= maxTop);
    }

    private bool TryHandleTranscriptScrollButtonClicks(VirtualMouseState mouse)
    {
        int barX = _layout.Transcript.Right - ScrollbarW - 2;
        int upBtnY = _layout.Transcript.Y + 18;
        int downBtnY = _layout.Transcript.Bottom - 18;

        List<WrappedTranscriptRow> wrapped = WrapTranscriptForDisplay(_layout.TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - _layout.TranscriptVisibleRows);

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

    private bool TryHandleTranscriptClickToFinish(VirtualMouseState mouse)
    {
        int clickableW = _layout.Transcript.W - ScrollbarW - 4;
        if (!mouse.IsHit(_layout.Transcript.X, _layout.Transcript.Y, clickableW, _layout.Transcript.H))
            return false;

        if (_commsQueue.IsTypingActiveLine &&
            _commsQueue.FinishActiveLineImmediately(_config.TalkingHeadAdvanceMode, _config.TalkingHeadLinePauseSeconds))
        {
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        if (_commsQueue.IsWaitingTalkingHeadClickAdvance && _commsQueue.AdvanceTalkingHeadAfterClick())
        {
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        if (_commsQueue.IsWaitingTalkingHeadAutoPause && _commsQueue.SkipTalkingHeadPauseImmediately())
        {
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        if (IsPreShiftTalkingHeadHailActive && _commsQueue.IsWaitingPreDelay && _commsQueue.SkipPreDelayImmediately())
        {
            AudioHooks.Play("terminal_key_click");
            return true;
        }

        return false;
    }

    // =========================================================================
    // CRT Comms Dialogue Log (A: 908,40,364,216 [14 rows] | B1/B2: 680,40,364,172 [11 rows])
    // =========================================================================
    private void DrawCrtCommsDialogueLog(VirtualMouseState mouse)
    {
        DeskRect tr = _layout.Transcript;
        int visibleRows = _layout.TranscriptVisibleRows;
        bool faultLocked = HasActiveProtocolFaultTrace;

        Raylib.DrawRectangle(tr.X, tr.Y, tr.W, tr.H, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(tr.X, tr.Y, tr.W, tr.H,
            faultLocked ? Palette.AlertCrimson : Palette.OxidizedBronzeWorn);

        // Header bar
        Raylib.DrawRectangle(tr.X + 1, tr.Y + 1, tr.W - 2, 16, Palette.OutlineVoid);
        Raylib.DrawLine(tr.X + 1, tr.Y + 17, tr.Right - 1, tr.Y + 17, Palette.WeatheredGunmetalEdge);
        string channelTitle = _preShiftStage switch
        {
            PreShiftStage.Day1VorstLiveHail or PreShiftStage.Day3KordaniLiveHail => "INTERNAL COMMS // CMDR. VORST",
            PreShiftStage.Day2NixaLiveHail or PreShiftStage.Day8NixaLiveHail or PreShiftStage.Day15ForemanLiveHail => "INTERNAL COMMS // REGEER NIXA",
            _ => "CRT COMMS DIALOGUE LOG"
        };
        Color channelCol = IsPreShiftTalkingHeadHailActive
            ? (IsPreShiftBrengopHailActive ? Palette.CrtAmberPrimary : Palette.AlertCrimson)
            : Palette.CrtAmberPrimary;
        UiText.Draw(TruncateByPixelWidth(channelTitle, 10, tr.W - 14), tr.X + 8, tr.Y + 4, 10, channelCol);

        List<WrappedTranscriptRow> wrapped = WrapTranscriptForDisplay(_layout.TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - visibleRows);
        if (_transcriptAutoFollowBottom)
        {
            _transcriptScrollTopLine = maxTop;
        }
        else
        {
            _transcriptScrollTopLine = Math.Clamp(_transcriptScrollTopLine, 0, maxTop);
        }

        int drawY = tr.Y + 22;
        int endIdx = Math.Min(wrapped.Count, _transcriptScrollTopLine + visibleRows);
        bool cursorBlinkOn = ((int)(_elapsedVisualTime * 7.0f) % 2) == 0;
        int maxCursorRight = tr.Right - ScrollbarW - 6;
        int firstVisibleStmtRow = -1;
        int lastVisibleStmtRow = -1;

        for (int i = _transcriptScrollTopLine; i < endIdx; i++)
        {
            if (wrapped[i].IsStatementEvidence)
            {
                int visOffset = i - _transcriptScrollTopLine;
                if (firstVisibleStmtRow < 0) firstVisibleStmtRow = visOffset;
                lastVisibleStmtRow = visOffset;
            }
        }

        if (firstVisibleStmtRow >= 0)
        {
            int stmtX = tr.X + 4;
            int stmtY = tr.Y + 21 + firstVisibleStmtRow * 13;
            int stmtW = tr.W - ScrollbarW - 8;
            int stmtH = (lastVisibleStmtRow - firstVisibleStmtRow + 1) * 13 + 1;

            if (IsTargetFaultHighlighted(ReviewTarget.CommsTranscriptStatement))
            {
                Color fCol = GetFaultPulseColor();
                Raylib.DrawRectangle(stmtX, stmtY, stmtW, stmtH, Palette.BrownDwarfGlowDeep);
                Raylib.DrawRectangleLines(stmtX, stmtY, stmtW, stmtH, fCol);
            }
            else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.CommsTranscriptStatement))
            {
                bool hovered = mouse.IsHit(stmtX, stmtY, stmtW, stmtH);
                bool selected = _reviewFirstSelection == ReviewTarget.CommsTranscriptStatement;
                if (hovered || selected)
                {
                    Raylib.DrawRectangle(stmtX, stmtY, stmtW, stmtH, new Color(46, 36, 18, 180));
                }
                DrawReviewHotspotOutline(mouse, stmtX, stmtY, stmtW, stmtH, selected);
            }
        }

        for (int i = _transcriptScrollTopLine; i < endIdx; i++)
        {
            WrappedTranscriptRow row = wrapped[i];
            UiText.Draw(row.Line, tr.X + 8, drawY, 10, row.Col);
            if (row.ShowTypingCursor && cursorBlinkOn)
            {
                int cx = tr.X + 8 + UiText.MeasureWidth(row.Line, 10) + 2;
                if (cx + 5 <= maxCursorRight)
                {
                    Raylib.DrawRectangle(cx, drawY + 1, 5, 8, row.Col);
                }
            }
            drawY += 13;
        }

        DrawThematicCrtScrollbar(mouse, wrapped.Count, maxTop, visibleRows);

        float crtCollapseT = Math.Clamp((ClockOutPowerDownProgress - 0.06f) / 0.28f, 0f, 1f);
        if (crtCollapseT > 0.001f)
        {
            DrawCrtPowerDownCollapse(tr.X + 1, tr.Y + 18, tr.W - 2, tr.H - 19, crtCollapseT);
        }
    }

    private void DrawThematicCrtScrollbar(VirtualMouseState mouse, int totalLines, int maxTop, int visibleRows)
    {
        DeskRect tr = _layout.Transcript;
        int barX = tr.Right - ScrollbarW - 2;
        int upY = tr.Y + 18;
        int downY = tr.Bottom - 18;
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

        if (totalLines > visibleRows && maxTop > 0)
        {
            float ratio = (float)visibleRows / totalLines;
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
    // Protocol Review Circuit Trace Overlay
    // =========================================================================
    private bool IsTargetFaultHighlighted(ReviewTarget target) =>
        target != ReviewTarget.None &&
        HasActiveProtocolFaultTrace &&
        (_faultHighlightA == target || _faultHighlightB == target);

    // Calm steady crimson border (no aggressive strobing/flashing)
    private Color GetFaultPulseColor() => Palette.AlertCrimson;

    private void DrawCircuitTraceOverlay(ReviewTarget targetA, ReviewTarget targetB, Color traceCol)
    {
        if (!TryGetReviewTargetScreenRect(targetA, out Rectangle rectA) ||
            !TryGetReviewTargetScreenRect(targetB, out Rectangle rectB))
        {
            return;
        }

        // Highlight both linked hitboxes with a crisp steady outline (teal on match, crimson on fault)
        int ax = (int)MathF.Round(rectA.X);
        int ay = (int)MathF.Round(rectA.Y);
        int aw = (int)MathF.Round(rectA.Width);
        int ah = (int)MathF.Round(rectA.Height);
        int bx = (int)MathF.Round(rectB.X);
        int by = (int)MathF.Round(rectB.Y);
        int bw = (int)MathF.Round(rectB.Width);
        int bh = (int)MathF.Round(rectB.Height);

        Raylib.DrawRectangleLines(ax, ay, aw, ah, traceCol);
        Raylib.DrawRectangleLines(bx, by, bw, bh, traceCol);

        int cxA = ax + aw / 2;
        int cyA = ay + ah / 2;
        int cxB = bx + bw / 2;
        int cyB = by + bh / 2;
        int elbowX = (cxA + cxB) / 2;

        // Crisp thin right-angle circuit/trace line connecting the two hitboxes (zero flashing, zero comms-blocking banner)
        Raylib.DrawLine(cxA, cyA, elbowX, cyA, traceCol);
        Raylib.DrawLine(elbowX, cyA, elbowX, cyB, traceCol);
        Raylib.DrawLine(elbowX, cyB, cxB, cyB, traceCol);

        // Square terminal endpoint nodes and elbow pads
        Raylib.DrawRectangle(cxA - 2, cyA - 2, 5, 5, traceCol);
        Raylib.DrawRectangle(elbowX - 1, cyA - 1, 3, 3, traceCol);
        Raylib.DrawRectangle(elbowX - 1, cyB - 1, 3, 3, traceCol);
        Raylib.DrawRectangle(cxB - 2, cyB - 2, 5, 5, traceCol);
    }

    private bool TryGetReviewTargetScreenRect(ReviewTarget target, out Rectangle rect)
    {
        rect = default;
        if (target == ReviewTarget.None)
            return false;

        DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
        int sx = slate.X;
        int sy = slate.Y + GetDataSlateVisualOffsetY();
        int sw = slate.Width;
        int rightHotW = sw - 78;
        int rowW = sw - 8;

        switch (target)
        {
            case ReviewTarget.VisualSignalTransponderId:
                rect = _layout.FeedBox.ToRaylib();
                return true;
            case ReviewTarget.TopHeaderCycle:
                rect = new Rectangle(TopCycleX, TopCycleY, TopCycleW, TopCycleH);
                return true;
            case ReviewTarget.ViewportShipHull:
                rect = GetActiveShipHullScreenRect();
                return true;
            case ReviewTarget.SpsLensConduit:
                rect = (_spsEnabled && _lastSpsLensRect.Width > 0 && _lastSpsLensRect.Height > 0)
                    ? _lastSpsLensRect
                    : GetActiveShipHullScreenRect();
                return true;
            case ReviewTarget.WarpStripPaper:
                if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear)
                {
                    rect = new Rectangle(StripAnchorX, StripAnchorY, StripWidth, StripFullHeight);
                    return true;
                }
                DraggableDeskItem strip = _deskItems[DeskItemId.WarpStrip];
                rect = new Rectangle(strip.X, strip.Y, strip.Width, strip.Height);
                return strip.IsVisible;
            case ReviewTarget.SpsMiniScreenHotWeapons:
            case ReviewTarget.SpsMiniScreenCargoPower:
            {
                SpsAnomalyKind kind = target == ReviewTarget.SpsMiniScreenHotWeapons
                    ? SpsAnomalyKind.HotWeaponMount
                    : SpsAnomalyKind.VioletCargoPower;
                int rowIdx = Math.Max(0, _spsLoggedAnomalies.FindIndex(e => e.Kind == kind && !e.IsCooled));
                rect = new Rectangle(SpsMiniX + 4, SpsMiniY + 24 + rowIdx * 36, SpsMiniW - 8, 30);
                return true;
            }
            case ReviewTarget.DataSlatePrefix:
                rect = _gameState.Day == 1
                    ? new Rectangle(sx + 73, sy + 22, rightHotW, 30)
                    : (_gameState.Day < _config.DayUnlockWarpScanner
                        ? new Rectangle(sx + 73, sy + 21, rightHotW, 26)
                        : new Rectangle(sx + 73, sy + 21, rightHotW, 22));
                return true;
            case ReviewTarget.DataSlateClass:
                rect = new Rectangle(sx + 73, sy + 44, rightHotW, 13);
                return true;
            case ReviewTarget.DataSlateWeapons:
                rect = _gameState.Day < _config.DayUnlockWarpScanner
                    ? new Rectangle(sx + 73, sy + 50, rightHotW, 15)
                    : new Rectangle(sx + 73, sy + 58, rightHotW, 13);
                return true;
            case ReviewTarget.DataSlateMass:
                rect = new Rectangle(sx + 73, sy + 72, rightHotW, 13);
                return IsStokerOperational;
            case ReviewTarget.StokerMeasuredMassLed:
                rect = new Rectangle(StokerLedX, StokerLedY, StokerLedW, StokerLedH);
                return IsStokerOperational && _stokerMassAnchoredForShip;
            case ReviewTarget.DataSlateOrg:
                rect = new Rectangle(sx + 4, sy + 103, rowW, 12);
                return true;
            case ReviewTarget.DataSlateExpiry:
                rect = new Rectangle(sx + 4, sy + 115, rowW, 12);
                return true;
            case ReviewTarget.DataSlateSpecies:
                rect = new Rectangle(sx + 4, sy + 139, rowW, 12);
                return true;
            case ReviewTarget.DataSlateOrigin:
                rect = new Rectangle(sx + 4, sy + 151, rowW, 12);
                return true;
            case ReviewTarget.DataSlateCargo:
                rect = new Rectangle(sx + 4, sy + 175, rowW, 24);
                return true;
            case ReviewTarget.ReferenceManualPrefixEntry:
            {
                DraggableDeskItem manual = _deskItems[DeskItemId.ReferenceManual];
                rect = new Rectangle(manual.X + 6, manual.Y + 39, manual.Width - 12, ManualPrefixDirectory.Length * 12);
                return manual.IsVisible;
            }
            case ReviewTarget.ReferenceManualSpeciesEntry:
            {
                DraggableDeskItem manual = _deskItems[DeskItemId.ReferenceManual];
                rect = new Rectangle(manual.X + 6, manual.Y + 60, manual.Width - 12, manual.Height - 66);
                return manual.IsVisible;
            }
            case ReviewTarget.ReferenceManualHullEntry:
            {
                DraggableDeskItem manual = _deskItems[DeskItemId.ReferenceManual];
                rect = new Rectangle(manual.X + 6, manual.Y + 60, manual.Width - 12, 88);
                return manual.IsVisible;
            }
            case ReviewTarget.ReferenceManualCargoBannedEntry:
            {
                DraggableDeskItem manual = _deskItems[DeskItemId.ReferenceManual];
                int bannedH = CargoCatalog.Load().BannedCargoList.Count * 20;
                rect = new Rectangle(manual.X + 6, manual.Y + 54, manual.Width - 12, bannedH);
                return manual.IsVisible;
            }
            case ReviewTarget.CommsTranscriptStatement:
            {
                if (TryGetVisibleTranscriptStatementRect(out Rectangle stmtRect))
                {
                    rect = stmtRect;
                    return true;
                }
                DeskRect tr = _layout.Transcript;
                rect = new Rectangle(tr.X + 4, tr.Bottom - 20, tr.W - ScrollbarW - 8, 14);
                return true;
            }
            case ReviewTarget.CargoManifestWafer:
            {
                for (int i = _deskWafers.Count - 1; i >= 0; i--)
                {
                    CertWaferCard wCard = _deskWafers[i];
                    if (_activeShip != null &&
                        string.Equals(wCard.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
                    {
                        rect = new Rectangle(wCard.X, wCard.Y, WaferCardW, WaferCardH);
                        return true;
                    }
                }
                return false;
            }
            case ReviewTarget.WaferWriterModule:
                rect = _layout.WaferWriter.ToRaylib();
                return IsWaferWriterOperational;
            default:
                return false;
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
    // CommsTypewriterQueue Helpers & Transcript Wrapping
    // =========================================================================
    private readonly record struct WrappedTranscriptRow(
        string Line,
        Color Col,
        bool ShowTypingCursor = false,
        bool IsStatementEvidence = false);

    internal bool TryGetVisibleTranscriptStatementRect(out Rectangle rect)
    {
        rect = default;
        DeskRect tr = _layout.Transcript;
        int visibleRows = _layout.TranscriptVisibleRows;
        List<WrappedTranscriptRow> wrapped = WrapTranscriptForDisplay(_layout.TranscriptTextMaxPx);
        int maxTop = Math.Max(0, wrapped.Count - visibleRows);
        int topLine = _transcriptAutoFollowBottom ? maxTop : Math.Clamp(_transcriptScrollTopLine, 0, maxTop);
        int endIdx = Math.Min(wrapped.Count, topLine + visibleRows);

        int firstVis = -1;
        int lastVis = -1;
        for (int i = topLine; i < endIdx; i++)
        {
            if (wrapped[i].IsStatementEvidence)
            {
                int visRow = i - topLine;
                if (firstVis < 0) firstVis = visRow;
                lastVis = visRow;
            }
        }

        if (firstVis < 0)
            return false;

        rect = new Rectangle(
            tr.X + 4,
            tr.Y + 21 + firstVis * 13,
            tr.W - ScrollbarW - 8,
            (lastVis - firstVis + 1) * 13 + 1);
        return true;
    }

    private static Color ToneToColor(CommsTextTone tone) =>
        tone switch
        {
            CommsTextTone.AmberDimmed => Palette.CrtAmberDimmed,
            CommsTextTone.ClearanceTeal => Palette.ClearanceTeal,
            CommsTextTone.AlertCrimson => Palette.AlertCrimson,
            _ => Palette.CrtAmberPrimary
        };

    private void AddSystemLogLine(
        string text,
        CommsTextTone tone,
        float preDelaySeconds = 0f,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _commsQueue.EnqueueInstantLine(text, tone, preDelaySeconds, onStart, onComplete);
    }

    private void EnqueueOfficerLine(
        string quotedMessage,
        CommsTextTone tone = CommsTextTone.ClearanceTeal,
        float preDelaySeconds = 0f,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(quotedMessage))
            return;

        const string prefix = "> BOOTH 04: ";
        string formatted = $"{prefix}\"{quotedMessage}\"";
        _commsQueue.EnqueueSpokenLine(
            formatted,
            CommsSpeakerKind.Officer,
            tone,
            instantPrefixLength: prefix.Length,
            preDelaySeconds: preDelaySeconds,
            showReceivingIndicatorDuringPreDelay: false,
            suppressAutoReceivingLag: true,
            onStart: onStart,
            onComplete: onComplete);
    }

    private void EnqueueTalkingHeadLine(
        string fullFormattedLine,
        CommsTextTone tone = CommsTextTone.AmberPrimary,
        float preDelaySeconds = 0f,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(fullFormattedLine))
            return;

        int prefixLen = CommsTypewriterQueue.DetectSpeakerPrefixLength(fullFormattedLine);
        _commsQueue.EnqueueSpokenLine(
            fullFormattedLine,
            CommsSpeakerKind.Captain,
            tone,
            instantPrefixLength: prefixLen,
            preDelaySeconds: preDelaySeconds,
            showReceivingIndicatorDuringPreDelay: false,
            suppressAutoReceivingLag: true,
            isTalkingHeadLine: true,
            onStart: onStart,
            onComplete: onComplete);
    }

    private void EnqueueCaptainLine(
        string fullFormattedLine,
        CommsTextTone tone = CommsTextTone.AmberPrimary,
        float preDelaySeconds = 0f,
        bool showReceivingIndicatorDuringPreDelay = false,
        bool suppressAutoReceivingLag = false,
        bool isCaptainStatementEvidence = false,
        Action? onStart = null,
        Action? onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(fullFormattedLine))
            return;

        int prefixLen = CommsTypewriterQueue.DetectSpeakerPrefixLength(fullFormattedLine);
        _commsQueue.EnqueueSpokenLine(
            fullFormattedLine,
            CommsSpeakerKind.Captain,
            tone,
            instantPrefixLength: prefixLen,
            preDelaySeconds: preDelaySeconds,
            showReceivingIndicatorDuringPreDelay: showReceivingIndicatorDuringPreDelay,
            suppressAutoReceivingLag: suppressAutoReceivingLag,
            isCaptainStatementEvidence: isCaptainStatementEvidence,
            onStart: onStart,
            onComplete: onComplete);
    }

    private List<WrappedTranscriptRow> WrapTranscriptForDisplay(int maxPixelWidth)
    {
        List<WrappedTranscriptRow> output = [];
        foreach (CommsTranscriptLine entry in _commsQueue.CommittedLines)
        {
            Color col = ToneToColor(entry.Tone);
            foreach (string line in WrapTextByPixelWidth(entry.Text, fontSize: 10, maxPixelWidth, maxLines: 64))
            {
                output.Add(new WrappedTranscriptRow(line, col, IsStatementEvidence: entry.IsCaptainStatementEvidence));
            }
        }

        if (_commsQueue.IsTypingActiveLine)
        {
            // Pre-wrap the FULL line once so words never jump lines mid-word while characters stream in
            string fullText = _commsQueue.ActiveFullText;
            int visibleCount = _commsQueue.ActiveVisibleCharCount;
            Color activeCol = ToneToColor(_commsQueue.ActiveTone);
            bool activeStmt = _commsQueue.ActiveIsCaptainStatementEvidence;

            List<string> fullWrapped = WrapTextByPixelWidth(fullText, fontSize: 10, maxPixelWidth, maxLines: 64);
            int remaining = visibleCount;
            int lastAddedIdx = -1;

            for (int r = 0; r < fullWrapped.Count; r++)
            {
                string rowText = fullWrapped[r];
                if (remaining <= 0)
                    break;

                if (remaining >= rowText.Length)
                {
                    output.Add(new WrappedTranscriptRow(rowText, activeCol, IsStatementEvidence: activeStmt));
                    lastAddedIdx = output.Count - 1;
                    // Account for the space separator between wrapped lines
                    remaining -= rowText.Length + 1;
                }
                else
                {
                    output.Add(new WrappedTranscriptRow(rowText[..remaining], activeCol, ShowTypingCursor: true, IsStatementEvidence: activeStmt));
                    lastAddedIdx = output.Count - 1;
                    remaining = 0;
                    break;
                }
            }

            if (lastAddedIdx >= 0 && !output[lastAddedIdx].ShowTypingCursor)
            {
                WrappedTranscriptRow tail = output[lastAddedIdx];
                output[lastAddedIdx] = tail with { ShowTypingCursor = true };
            }
        }
        else if (_commsQueue.IsWaitingReceiving)
        {
            float pulseT = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_elapsedVisualTime * 8.0f));
            Color pulseCol = BlendRgb(Palette.WeatheredGunmetalEdge, Palette.CrtAmberPrimary, pulseT);
            output.Add(new WrappedTranscriptRow("[RECEIVING...]", pulseCol));
        }
        else if (_commsQueue.IsWaitingTalkingHeadClickAdvance)
        {
            float pulseT = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_elapsedVisualTime * 6.5f));
            Color pulseCol = BlendRgb(Palette.CrtAmberDimmed, Palette.CrtAmberPrimary, pulseT);
            output.Add(new WrappedTranscriptRow("[CLICK / SPACE]", pulseCol));
        }

        return output;
    }
}
