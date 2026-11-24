using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

public sealed partial class DeskScreen
{
    // Smoke-test deterministic visual staging helpers (never invoked in normal gameplay)
    internal void SmokeSetShiftElapsedSeconds(float seconds)
    {
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;
        _shiftController.NotifyFirstHailTriggered();
        _shiftController.ForceSetElapsedSeconds(seconds);
    }

    internal void SmokeTriggerPreShiftAdvanceKey()
    {
        if (IsPreShiftTalkingHeadHailActive)
        {
            HandlePreShiftAdvanceAction();
        }
    }

    internal void SmokeStageShipLoadedAndInspecting(bool enableSps = false, bool triggerDetainClamps = false, bool triggerFaultBeat = false)
    {
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
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
        _commsQueue.FlushAllImmediately(invokeCallbacks: false);
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
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
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

        int dockPressX = _layout.ClearanceSlot.X + 60;
        int dockPressY = _layout.ClearanceSlot.Y + 60;
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
        slate.X = _layout.SlateBusDockedX;
        slate.Y = _layout.SlateBusDockedY;
        BeginClearanceTransmissionToShip();
        if (!_slateBusSettleActive || GetDataSlateVisualOffsetY() <= 0 || !HasActiveClearanceSeal)
            throw new InvalidOperationException("Snapping cleared Data Slate into Slate Bus did not trigger matching seat tick or keep CLEARED seal.");

        _clearanceTransmitElapsed = ClearanceTransmitDurationSeconds * 0.50f;
        int busPressX = _layout.SlateBusDockedX + 60;
        int busPressY = _layout.SlateBusDockedY + 60;
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
        slate.X = _layout.SlateBusDockedX;
        slate.Y = _layout.SlateBusDockedY;
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

            // Day 1+ Protocol Review pairing WPN: ARMED (honest) with SPS LOG [!] HOT WPN must report NO DISCREPANCY,
            // flash PROTOCOLS MATCH // NO FAULT + circuit trace link for 0.6s, and automatically turn Protocol Review OFF!
            if (IsProtocolReviewUnlocked)
            {
                _protocolReviewModeActive = true;
                EvaluateProtocolReviewPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons);
                if (_faultStage != FaultSequenceStage.None || _activeShip.ProtocolReviewVerified ||
                    _protocolReviewModeActive || _noFaultTraceTimer <= 0.1f)
                {
                    throw new InvalidOperationException("Honest armed ship falsely triggered Protocol Fault or failed to auto-turn off Protocol Review on no-discrepancy match.");
                }
            }

            // Now press PWR DOWN WPN -> honest captain complies -> SPS visual cools and SPS LOG marks [OK] COOLED
            _pendingWeaponsOrder = true;
            _complianceTimer = 0f;
            ResolvePendingComplianceOrders();
            if (_activeShip.GroundTruth.ActualWeaponsHot || !_spsLoggedAnomalies[0].IsCooled)
            {
                throw new InvalidOperationException("PWR DOWN WPN compliance failed to cool weapons & mark SPS LOG cooled.");
            }

            // 4. Verify Day 2+ Weapons Compliance Bluff (1st PWR DOWN WPN -> bluff while hot; 2nd -> defensive refusal) & Protocol Review
            if (IsProtocolReviewUnlocked)
            {
                SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false);
                if (_activeShip != null)
                {
                    _activeShip.GroundTruth.HasSpoofedWeapons = true;
                    _activeShip.Slate.DeclaredWeaponsStatus = "OFFLINE";
                    _activeShip.WeaponsPowerDownOrdersIssued = 0;

                    _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                    int linesBeforeBluff = _commsQueue.CommittedLines.Count;
                    _pendingWeaponsOrder = true;
                    _complianceTimer = 0f;
                    ResolvePendingComplianceOrders();
                    _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                    if (!_activeShip.GroundTruth.ActualWeaponsHot ||
                        _spsLoggedAnomalies[0].IsCooled ||
                        _commsQueue.CommittedLines.Count <= linesBeforeBluff ||
                        !_commsQueue.CommittedLines[^1].Text.Contains(_activeShip.SpoofedWeaponsBluffLine, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("1st PWR DOWN WPN on spoofed ship failed to bluff compliance while staying visibly hot in SPS.");
                    }

                    int linesBeforeRefusal = _commsQueue.CommittedLines.Count;
                    _pendingWeaponsOrder = true;
                    _complianceTimer = 0f;
                    ResolvePendingComplianceOrders();
                    _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                    if (!_activeShip.GroundTruth.ActualWeaponsHot ||
                        _spsLoggedAnomalies[0].IsCooled ||
                        _commsQueue.CommittedLines.Count <= linesBeforeRefusal ||
                        !_commsQueue.CommittedLines[^1].Text.Contains(_activeShip.SpoofedWeaponsDeflectionLine, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("2nd PWR DOWN WPN on spoofed ship failed to trigger defensive refusal while staying visibly hot in SPS.");
                    }

                    _protocolReviewModeActive = true;
                    EvaluateProtocolReviewPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons);
                    if (_faultStage != FaultSequenceStage.FaultBeat || !_activeShip.ProtocolReviewVerified)
                    {
                        throw new InvalidOperationException("Protocol Review failed to verify spoofed weapons via SPS Anomaly Mini-Screen.");
                    }
                    _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                    ResetFaultSequenceState();
                }
            }
        }
    }

    internal void SmokeStageClockOutLedger()
    {
        _activeShip = null;
        _shiftController.HasActiveShipInBay = false;
        _phase = EncounterPhase.BayEmpty;
        _shiftController.ForceEndShiftNow();
        _clockOutPowerDownActive = false;
        _clockOutAwaitingLeavePrompt = false;
        _clockOutSettled = true;
        PowerIndicatorBrightness = 0.0f;
        ShiftLedgerSummary summary = _economyEngine.SettleShiftClockOut(_gameState, _config);
        _ledgerOverlay.Open(summary);
    }

    internal void SmokeStageBlankSlatePortSwap()
    {
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        _slateLocation = DataSlateLocation.DeskFree;
        DraggableDeskItem primary = _deskItems[DeskItemId.DataSlate];
        primary.X = 330;
        primary.Y = 420;

        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = _gameState.NextUniqueItemId("smoke_blank"),
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "UNMARKED BLACK DATA SLATE"
        };
        _contrabandSlateInSlateBus = true;
        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
        cItem.Y = _layout.SlateBusDockedY;
        cItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.ContrabandSlate);
    }

    internal void SmokeStageBlankSlateDownloading(float progressFraction)
    {
        SmokeStageBlankSlatePortSwap();
        _slateTelemetry = DataSlateTelemetryState.Empty;
        _phase = EncounterPhase.AwaitingPortForDownload;
        BeginBlankSlateInterceptDownload(triggeredByReTransmit: false);
        _protocolDownloadElapsed = Math.Clamp(progressFraction, 0.05f, 0.95f) * ProtocolDownloadDurationSeconds;
    }

    internal void SmokeStageCrimsonFaultTrace()
    {
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        if (_activeShip != null)
        {
            _activeShip.GroundTruth.RawTransponderId = "KDF-909-X";
            _activeShip.GroundTruth.ActiveInfractions =
            [
                new ShipInfraction { Type = ShipInfractionType.TransponderIdMismatch, Code = "PROTO-01T", CitationReason = "VISUAL TRACE CHECK" }
            ];
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None; // Settled steady crimson trace
        }
    }

    internal void SmokeStageBurntCovertSlate()
    {
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        _slateLocation = DataSlateLocation.DeskFree;
        DraggableDeskItem primary = _deskItems[DeskItemId.DataSlate];
        primary.X = 330;
        primary.Y = 420;
        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = "vis_burnt_covert",
            ItemType = ContrabandPocketItem.TypeBurntMissionSlate,
            IsBurnt = true,
            IsMissionSlate = false,
            DisplayName = "BURNT MISSION SLATE"
        };
        _contrabandSlateInSlateBus = true;
        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
        cItem.Y = _layout.SlateBusDockedY;
        cItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.ContrabandSlate);
    }

    internal void SmokeStageClockOutQuietLeavePrompt()
    {
        _activeShip = null;
        _shiftController.HasActiveShipInBay = false;
        _phase = EncounterPhase.BayEmpty;
        _shiftController.ForceEndShiftNow();
        TryAutoRecoverLooseContrabandAtClockOut();
        _clockOutPowerDownActive = false;
        _clockOutPowerDownElapsed = _config.ClockOutPowerDownDelaySeconds;
        _clockOutAwaitingLeavePrompt = true;
        _clockOutSettled = false;
        PowerIndicatorBrightness = 0.0f;
        _ledgerOverlay.Close();
    }

    internal void SmokeVerifyPrompt4DeskEspionage()
    {
        // 1. Blank Data Slate Port-Swap Intercept -> logs [UNAUTH_IO], produces CopiedDataSlate, stows cleanly into Lap Pocket
        _gameState.PocketCapacity = 2;
        _gameState.PocketItems.Clear();
        _gameState.SecurityLogs.Clear();
        _gameState.UnauthorizedLogCount = 0;

        SmokeStageBlankSlatePortSwap();
        int transcriptStartIdx = _commsQueue.CommittedLines.Count;
        BeginBlankSlateInterceptDownload(triggeredByReTransmit: true);
        _protocolDownloadElapsed = ProtocolDownloadDurationSeconds;
        CompleteSubspaceProtocolsDownload();
        _commsQueue.FlushAllImmediately(invokeCallbacks: false);

        if (_activeDeskContrabandItem == null ||
            _activeDeskContrabandItem.ItemType != ContrabandPocketItem.TypeCopiedDataSlate ||
            _gameState.UnscrubbedLogCount < 1)
        {
            throw new InvalidOperationException("Blank Data Slate Slate Bus port-swap failed to capture telemetry or log [UNAUTH_IO].");
        }

        // Verify Silent Blank Slate Intercept printed ZERO fake/blank/unauth warnings in CRT Comms Log
        for (int t = transcriptStartIdx; t < _commsQueue.CommittedLines.Count; t++)
        {
            string line = _commsQueue.CommittedLines[t].Text.ToUpperInvariant();
            if (line.Contains("FAKE") || line.Contains("BLANK") || line.Contains("UNAUTH") || line.Contains("STOLEN"))
            {
                throw new InvalidOperationException($"Silent Blank Data Slate intercept leaked suspicious CRT Comms Log line: {_commsQueue.CommittedLines[t].Text}");
            }
        }

        if (!TryStowContrabandDeskSlateInLapPocket() || _gameState.PocketItems.Count != 1)
        {
            throw new InvalidOperationException("Failed to stow captured stolen Data Slate into Contraband Lap Pocket.");
        }

        // 2. Two-Way Slate Bus Covert Mission Uploads (All 3 Outcomes):
        // Outcome A: Intended Mission Recipient -> completes mission & consumes slate
        _gameState.ActiveCovertMissionId = "slate_bus_mission_01";
        _gameState.ActiveCovertTargetPrefix = "FTV-";
        _gameState.ActiveCovertTargetReg = "FTV-719-R";
        _gameState.ActiveCovertTargetShipName = "CINDER LARK";
        _gameState.ActiveCovertDeliveryDay = _gameState.Day;
        _gameState.DeliveredCovertMissionIds.Clear();

        SmokeStageShipLoadedAndInspecting(enableSps: false);
        if (_activeShip != null)
        {
            _activeShip.Slate.DeclaredPrefix = "FTV-";
            _activeShip.Slate.RegistrationCode = "FTV-719-R";
            _activeShip.GroundTruth.IsCovertMissionTarget = true;
            _activeShip.GroundTruth.CovertMissionId = "slate_bus_mission_01";
        }
        _slateLocation = DataSlateLocation.DeskFree;
        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = "smoke_mission_01",
            ItemType = ContrabandPocketItem.TypeMissionDataSlate,
            DisplayName = "COVERT SLATE [OP-01]",
            IsMissionSlate = true,
            MissionId = "slate_bus_mission_01",
            MissionTargetReg = "FTV-719-R",
            MissionTargetPrefix = "FTV-"
        };
        BeginCovertMissionSlateUpload();
        CompleteCovertMissionSlateUpload();
        if (!_gameState.QuestFlags.TryGetValue("slate_bus_mission_01_completed", out bool done) || !done ||
            _activeDeskContrabandItem == null ||
            !_activeDeskContrabandItem.IsBurnt ||
            _activeDeskContrabandItem.ItemType != ContrabandPocketItem.TypeBurntMissionSlate ||
            !_contrabandSlateInSlateBus)
        {
            throw new InvalidOperationException("Covert Mission Upload to Intended Recipient failed to complete mission or transition slate into burnt single-use glass.");
        }

        // Outcome B: Confused Wrong Civilian -> no mission progress, ejects slate cleanly
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        if (_activeShip != null)
        {
            _activeShip.Slate.DeclaredPrefix = "PRJ-";
            _activeShip.Slate.RegistrationCode = "PRJ-901-Z";
            _activeShip.GroundTruth.IsCovertMissionTarget = false;
        }
        int strikesBeforeWrong = _gameState.SecurityStrikes;
        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = "smoke_mission_wrong",
            ItemType = ContrabandPocketItem.TypeMissionDataSlate,
            DisplayName = "COVERT SLATE [OP-02]",
            IsMissionSlate = true,
            MissionId = "slate_bus_mission_02",
            MissionTargetReg = "FTC-408-M"
        };
        BeginCovertMissionSlateUpload();
        CompleteCovertMissionSlateUpload();
        if (_gameState.SecurityStrikes != strikesBeforeWrong || _activeDeskContrabandItem == null || _contrabandSlateInSlateBus)
        {
            throw new InvalidOperationException("Covert Mission Upload to Confused Civilian did not eject cleanly without strikes.");
        }

        // Outcome C: Accidental Upload to Pact Cruiser (KDF-) on Day 3+ -> immediate +1 Security Strike!
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        if (_activeShip != null)
        {
            _activeShip.Slate.DeclaredPrefix = "KDF-";
            _activeShip.Slate.RegistrationCode = "KDF-777-V";
            _activeShip.GroundTruth.IsCovertMissionTarget = false;
        }
        int strikesBeforePact = _gameState.SecurityStrikes;
        BeginCovertMissionSlateUpload();
        CompleteCovertMissionSlateUpload();
        if (AreSecurityStrikesUnlocked && _gameState.SecurityStrikes != strikesBeforePact + 1)
        {
            throw new InvalidOperationException("Accidental Covert Upload to KDF- Pact Cruiser did not add +1 Security Strike on Day 3+.");
        }

        _activeDeskContrabandItem = null;
        _deskItems[DeskItemId.ContrabandSlate].IsVisible = false;
        _activeCitationStrip = null;
    }

    internal void SmokeVerifyBuild43DeskFeatures()
    {
        // 1. Loose contraband slate recovery into VentStashItems at clock-out when PocketItems is full
        _gameState.PocketCapacity = 1;
        _gameState.PocketItems.Clear();
        _gameState.VentStashItems.Clear();
        _gameState.PocketItems.Add(new ContrabandPocketItem
        {
            ItemId = "pocket_full_item",
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "BLANK DATA SLATE"
        });
        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = "loose_desk_stolen_slate",
            ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
            DisplayName = "STOLEN SLATE: FTV-401-X",
            TargetShipRegistration = "FTV-401-X",
            TargetShipPrefix = "FTV-"
        };
        _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
        TryAutoRecoverLooseContrabandAtClockOut();
        if (_activeDeskContrabandItem != null ||
            _gameState.VentStashItems.Count != 1 ||
            _gameState.VentStashItems[0].ItemId != "loose_desk_stolen_slate")
        {
            throw new InvalidOperationException("Loose Contraband Slate on desk was not recovered into VentStashItems when PocketItems was full at clock-out.");
        }

        // 2. Day 2 Ghost Route [INSPECT ROUTE] pushback button & uncanny warm Warp Pylon SPS cue
        _gameState.Day = 2;
        SmokeStageShipLoadedAndInspecting(enableSps: true);
        if (_activeShip != null)
        {
            _activeShip.GroundTruth.IsDay2GhostRouteSuspect = true;
            _activeShip.SuspectRoutePushbackPromptLine = "Your transit log has a four-hour gap between Velkar Colony and Gate 3.";
            _activeShip.SuspectRouteDeflectionLine = "Solar flare near Velkar outer beacon. My papers are in order, Booth 04.";
            _spsLoggedAnomalies.Clear();
            _ghostRoutePushbackUsed = false;

            ShipRenderOptions spsOpts = _activeShip.BuildLiveShipRenderOptions(spsEnabled: true, _animTick);
            if (!spsOpts.UncannyWarpPylonPulse)
            {
                throw new InvalidOperationException("Day 2 Ghost Route ship did not enable UncannyWarpPylonPulse in SPS render options.");
            }
            if (!CanTriggerGhostRoutePushback)
            {
                throw new InvalidOperationException("Day 2 Ghost Route ship did not enable [INSPECT ROUTE] comms pushback button.");
            }

            // Click [INSPECT ROUTE] button at active NextHailBtn position
            Update(0.016f, new VirtualMouseState
            {
                X = _layout.NextHailBtn.X + 20,
                Y = _layout.NextHailBtn.Y + 12,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });

            // Stream Officer pushback + [RECEIVING...] + Captain deflection cleanly via typewriter queue
            VirtualMouseState idleM = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            for (int step = 0; step < 40 && _commsQueue.HasPendingOrActive; step++)
            {
                Update(0.20f, idleM);
            }

            if (!_ghostRoutePushbackUsed ||
                !_commsQueue.CommittedLines.Any(l => l.Text.Contains("four-hour gap", StringComparison.OrdinalIgnoreCase)) ||
                !_commsQueue.CommittedLines.Any(l => l.Text.Contains("Solar flare", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Clicking [INSPECT ROUTE] did not stream SuspectRoutePushbackPromptLine and SuspectRouteDeflectionLine.");
            }
        }
    }

    internal void SmokeVerifyBuild431DeskFeatures()
    {
        // 1. Blank Data Slate identical 1.80s download animation & NO premature red RE-PLUG DATA SLATE flash
        _gameState.Day = 6;
        _gameState.PocketCapacity = 2;
        _gameState.PocketItems.Clear();
        _gameState.VentStashItems.Clear();

        SmokeStageBlankSlatePortSwap();
        _slateTelemetry = DataSlateTelemetryState.Empty;
        _phase = EncounterPhase.AwaitingPortForDownload;
        BeginBlankSlateInterceptDownload(triggeredByReTransmit: false);

        if (!IsBlankSlateDownloading || _phase != EncounterPhase.DownloadingProtocols)
            throw new InvalidOperationException("Blank Data Slate did not enter DownloadingProtocols for 1.80s RX animation.");

        // Advance halfway (0.90s at 1.0x) -> MUST still be ~50% downloading, NOT DownloadAborted, NOT CopiedDataSlate!
        float prevAnim = _devDebug.AnimationSpeedMultiplier;
        _devDebug.AnimationSpeedPresetIndex = 1; // 1.0x
        UpdateEncounterPhase(0.90f, 0.90f);
        if (!IsBlankSlateDownloading ||
            MathF.Abs(BlankSlateDownloadProgress - 0.50f) > 0.05f ||
            _phase == EncounterPhase.DownloadAborted ||
            _activeDeskContrabandItem?.ItemType != ContrabandPocketItem.TypeBlankDataSlate)
        {
            throw new InvalidOperationException("Blank Data Slate mid-download check (0.90s / 1.80s) failed or flashed DownloadAborted early.");
        }

        // Advance remaining 0.95s -> completes at 100%, flips to CopiedDataSlate, THEN sets DownloadAborted for Primary Slate
        UpdateEncounterPhase(0.95f, 0.95f);
        if (IsBlankSlateDownloading ||
            _activeDeskContrabandItem?.ItemType != ContrabandPocketItem.TypeCopiedDataSlate ||
            _phase != EncounterPhase.DownloadAborted)
        {
            throw new InvalidOperationException("Blank Data Slate did not switch to CAPTURED PACKET and prompt RE-PLUG DATA SLATE strictly AFTER 100%.");
        }
        _ = prevAnim;

        // 2. Crimson PROTOCOL VIOLATED circuit trace link between two mismatched hitboxes
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        if (_activeShip != null)
        {
            _activeShip.GroundTruth.RawTransponderId = "ZZZ-999-X";
            _activeShip.GroundTruth.ActiveInfractions =
            [
                new ShipInfraction { Type = ShipInfractionType.TransponderIdMismatch, Code = "PROTO-01T", CitationReason = "SMOKE MISMATCH" }
            ];
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);
            if (!HasActiveProtocolFaultTrace ||
                !TryGetReviewTargetScreenRect(_faultHighlightA, out _) ||
                !TryGetReviewTargetScreenRect(_faultHighlightB, out _))
            {
                throw new InvalidOperationException("Confirmed Protocol Review fault did not activate crimson circuit trace between mismatched hitboxes.");
            }
        }

        // 3. Burnt / Cracked Covert Mission Slate non-reusability (no re-upload, no Clearance Dock, no Lap Pocket) & Clock-Out discard
        _gameState.PocketItems.Clear();
        _gameState.VentStashItems.Clear();
        _gameState.ServiceChuteItems.Clear();
        _activeDeskContrabandItem = new ContrabandPocketItem
        {
            ItemId = "smoke_burnt_1",
            ItemType = ContrabandPocketItem.TypeBurntMissionSlate,
            IsBurnt = true,
            IsMissionSlate = false,
            DisplayName = "BURNT MISSION SLATE"
        };
        _contrabandSlateInSlateBus = false;
        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.IsVisible = true;
        cItem.X = 460;
        cItem.Y = 440;

        // Attempt to stow in Lap Pocket -> MUST fail
        if (TryStowContrabandDeskSlateInLapPocket() || _gameState.PocketItems.Count != 0)
            throw new InvalidOperationException("Burnt covert slate illegally stowed into Contraband Lap Pocket.");

        // Attempt to drop back into Slate Bus -> MUST be rejected
        _draggingItemId = DeskItemId.ContrabandSlate;
        cItem.X = _layout.SlateBusDockedX;
        cItem.Y = _layout.SlateBusDockedY;
        UpdateActiveDrag(new VirtualMouseState { X = _layout.SlateBus.X + 40, Y = _layout.SlateBus.Y + 20, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });
        if (_contrabandSlateInSlateBus || _isUploadingCovertMissionSlate)
            throw new InvalidOperationException("Burnt covert slate illegally re-slotted into Slate Bus.");

        // Attempt to drop into Clearance Dock -> MUST be rejected
        _draggingItemId = DeskItemId.ContrabandSlate;
        cItem.X = _layout.ClearanceSlot.X;
        cItem.Y = _layout.ClearanceSlot.Y;
        UpdateActiveDrag(new VirtualMouseState { X = _layout.ClearanceDock.X + 40, Y = _layout.ClearanceDock.Y + 40, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });
        if (_phase == EncounterPhase.UploadingClearance)
            throw new InvalidOperationException("Burnt covert slate illegally slotted into Clearance Dock.");

        // Clock-Out automatic discard (must NOT enter Pocket, Vent, or Chute!)
        TryAutoRecoverLooseContrabandAtClockOut();
        if (_activeDeskContrabandItem != null ||
            _gameState.PocketItems.Count != 0 ||
            _gameState.VentStashItems.Count != 0 ||
            _gameState.ServiceChuteItems.Count != 0)
        {
            throw new InvalidOperationException("Burnt covert slate was not cleanly discarded at shift Clock-Out.");
        }

        // 4. Top-Right POWER LED fade + Slower ~4.0s Sequential Shutdown + Manual Clickable Leave Prompt (no auto-pop LedgerOverlay)
        _activeShip = null;
        _shiftController.HasActiveShipInBay = false;
        _phase = EncounterPhase.BayEmpty;
        _clockOutPowerDownActive = false;
        _clockOutPowerDownElapsed = 0f;
        _clockOutAwaitingLeavePrompt = false;
        _clockOutSettled = false;
        _ledgerOverlay.Close();
        PowerIndicatorBrightness = 1.0f;
        _shiftController.ForceEndShiftNow();

        VirtualMouseState idle = new() { X = 100, Y = 100, IsInsideViewport = true, Scale = 1.0f };
        // Step 1.0s (Stage 1 CRT collapse) -> POWER LED still bright 1.0, Ledger still closed
        Update(1.0f, idle);
        if (!IsClockOutPowerDownActive || PowerIndicatorBrightness < 0.99f || _ledgerOverlay.IsOpen)
            throw new InvalidOperationException("Shutdown Stage 1 (1.0s) failed POWER LED or premature Ledger check.");

        // Step to 3.3s (Stage 3 POWER fade) -> POWER LED partially faded (0 < brightness < 1), Ledger still closed
        Update(2.3f, idle);
        if (!IsClockOutPowerDownActive || PowerIndicatorBrightness <= 0.01f || PowerIndicatorBrightness >= 0.99f || _ledgerOverlay.IsOpen)
            throw new InvalidOperationException($"Shutdown Stage 3 (3.3s) failed smooth POWER LED fade (brightness={PowerIndicatorBrightness:0.00}).");

        // Step past 4.0s -> holds on quiet booth with clickable leave prompt, POWER LED == 0.0, Ledger STILL NOT auto-popped!
        Update(1.0f, idle);
        if (!IsAwaitingClockOutLeavePrompt || PowerIndicatorBrightness > 0.001f || _ledgerOverlay.IsOpen)
            throw new InvalidOperationException("Shutdown failed to hold on quiet booth leave prompt with POWER == 0 and Ledger closed.");

        // Explicitly click the plain-text leave prompt -> opens LedgerOverlay cleanly!
        Rectangle leaveRect = GetClockOutLeavePromptRect();
        Update(0.016f, new VirtualMouseState
        {
            X = (int)(leaveRect.X + leaveRect.Width * 0.5f),
            Y = (int)(leaveRect.Y + leaveRect.Height * 0.5f),
            IsInsideViewport = true,
            LeftPressed = true,
            Scale = 1.0f
        });
        if (!_ledgerOverlay.IsOpen || IsAwaitingClockOutLeavePrompt)
            throw new InvalidOperationException("Clicking the plain-text leave prompt did not open LedgerOverlay.");

        _ledgerOverlay.Close();
        _clockOutSettled = false;
        _clockOutAwaitingLeavePrompt = false;
        PowerIndicatorBrightness = 1.0f;
    }

    internal void SmokeStageTypewriterReceivingBeat()
    {
        SmokeStageShipLoadedAndInspecting(enableSps: false);
        _commsQueue.Clear();
        _transcriptScrollTopLine = 0;
        _transcriptAutoFollowBottom = true;
        EnqueueOfficerLine("Confirm route, purpose of visit, and cargo.", CommsTextTone.ClearanceTeal);
        EnqueueCaptainLine("CAPT: \"Transit from Oruun-4 carrying refined ore pellets to Void Gate 3.\"", CommsTextTone.AmberPrimary);
        // Snap officer line to 100% so the CRT displays the active pulsing [RECEIVING...] lag beat cleanly
        _commsQueue.FinishActiveLineImmediately();
    }

    internal void SmokeVerifyBuild432LayoutAndTypewriter(Action<VirtualMouseState>? renderFrame = null)
    {
        DeskUpperLayoutMode originalMode = _config.DeskUpperLayoutMode;
        bool original3x = _devDebug.Use3xPortraitInLayoutB;
        int originalAnimIdx = _devDebug.AnimationSpeedPresetIndex;
        _devDebug.AnimationSpeedPresetIndex = 1; // 1.0x

        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };

        try
        {
            // 1. Validate all 3 layout modes + 2x/3x portrait scaling geometry & identical Left/Top HUD regions
            DeskLayoutRects layoutA = DeskLayoutRects.Compute(DeskUpperLayoutMode.OptionA_Classic, use3xPortraitInLayoutB: true);
            DeskLayoutRects layoutB1_3x = DeskLayoutRects.Compute(DeskUpperLayoutMode.OptionB1_CommsSwap, use3xPortraitInLayoutB: true);
            DeskLayoutRects layoutB1_2x = DeskLayoutRects.Compute(DeskUpperLayoutMode.OptionB1_CommsSwap, use3xPortraitInLayoutB: false);
            DeskLayoutRects layoutB2_3x = DeskLayoutRects.Compute(DeskUpperLayoutMode.OptionB2_CommsSwapFlush, use3xPortraitInLayoutB: true);
            DeskLayoutRects layoutB2_2x = DeskLayoutRects.Compute(DeskUpperLayoutMode.OptionB2_CommsSwapFlush, use3xPortraitInLayoutB: false);

            DeskLayoutRects[] allLayouts = [layoutA, layoutB1_3x, layoutB1_2x, layoutB2_3x, layoutB2_2x];
            foreach (DeskLayoutRects l in allLayouts)
            {
                l.ValidateGeometry();
                if (l.TopBar != layoutA.TopBar ||
                    l.DetainLever != layoutA.DetainLever ||
                    l.Viewport != layoutA.Viewport ||
                    l.SpsToggle != layoutA.SpsToggle ||
                    l.SpsMini != layoutA.SpsMini ||
                    l.StokerDial != layoutA.StokerDial ||
                    l.WarpPrinter != layoutA.WarpPrinter ||
                    l.WarpRefSpot != layoutA.WarpRefSpot ||
                    l.LapPocket != layoutA.LapPocket)
                {
                    throw new InvalidOperationException($"Left region or Top HUD bar drifted in layout mode {l.Mode}.");
                }
            }

            // Verify exact requested coordinates across A, B1, B2 and 3x/2x portrait sizes
            if (layoutA.Viewscreen != new DeskRect(680, 40, 220, 216) ||
                layoutA.Portrait != new DeskRect(726, 48, 128, 128) ||
                layoutA.FeedBox != new DeskRect(688, 184, 204, 64) ||
                layoutA.Transcript != new DeskRect(908, 40, 364, 216) ||
                layoutA.SlateBus != new DeskRect(680, 264, 220, 44) ||
                layoutA.SlateBusDockedSlate != new DeskRect(698, 308, 184, 216) ||
                layoutA.WaferWriter != new DeskRect(680, 624, 220, 88) ||
                layoutA.ClearanceDock != new DeskRect(1072, 360, 200, 272) ||
                layoutA.FreeDragBounds != new DeskRect(8, 308, 1056, 404))
            {
                throw new InvalidOperationException("OptionA_Classic coordinates mismatch.");
            }

            if (layoutB1_3x.Transcript != new DeskRect(680, 40, 364, 172) ||
                layoutB1_3x.CommsStrip != new DeskRect(680, 220, 364, 36) ||
                layoutB1_3x.ProtocolReviewBtn != new DeskRect(680, 264, 364, 36) ||
                layoutB1_3x.SlateBus != new DeskRect(752, 308, 220, 44) ||
                layoutB1_3x.SlateBusDockedSlate != new DeskRect(770, 352, 184, 216) ||
                layoutB1_3x.WaferWriter != new DeskRect(752, 624, 220, 88) ||
                layoutB1_3x.Viewscreen != new DeskRect(1052, 40, 220, 216) ||
                layoutB1_3x.Portrait != new DeskRect(1066, 52, 192, 192) ||
                layoutB1_2x.Portrait != new DeskRect(1098, 72, 128, 128) ||
                layoutB1_3x.FeedBox != new DeskRect(1052, 264, 220, 88) ||
                layoutB1_3x.ClearanceDock != new DeskRect(1072, 360, 200, 272) ||
                layoutB1_3x.FreeDragBounds != new DeskRect(8, 352, 1056, 360))
            {
                throw new InvalidOperationException("OptionB1_CommsSwap coordinates or portrait scaling mismatch.");
            }

            if (layoutB2_3x.ClearanceDock != new DeskRect(1052, 360, 220, 272) ||
                layoutB2_3x.ClearanceSlot != new DeskRect(1070, 384, 184, 216) ||
                layoutB2_3x.ClearanceBar != new DeskRect(1070, 604, 184, 20) ||
                layoutB2_3x.DestroyOverride != new DeskRect(1052, 640, 220, 72) ||
                layoutB2_3x.FreeDragBounds != new DeskRect(8, 352, 1036, 360))
            {
                throw new InvalidOperationException("OptionB2_CommsSwapFlush lower-right flush coordinates mismatch.");
            }

            // 2. Live mid-shift toggle safety: docked Slate Bus, docked Clearance Dock, docked Contraband Slate,
            //    loose desk bounds clamping, mid-drag preservation, GPU render of all 5 variants, and dynamic trace endpoints
            _gameState.Day = 3;
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
            _devDebug.Use3xPortraitInLayoutB = true;
            SyncLayoutFromConfig();

            SmokeStageShipLoadedAndInspecting(enableSps: true);
            DraggableDeskItem primarySlate = _deskItems[DeskItemId.DataSlate];
            if (primarySlate.X != 698 || primarySlate.Y != 308)
                throw new InvalidOperationException($"Classic Slate Bus docked position expected (698,308), got ({primarySlate.X},{primarySlate.Y}).");
            renderFrame?.Invoke(idle);

            // Switch A -> B1 (3x & 2x) -> B2 (3x & 2x) with Primary Slate docked in Slate Bus
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB1_CommsSwap;
            SyncLayoutFromConfig();
            if (primarySlate.X != 770 || primarySlate.Y != 352)
                throw new InvalidOperationException($"Live switch A->B1 failed to re-anchor Slate Bus docked slate to (770,352), got ({primarySlate.X},{primarySlate.Y}).");
            renderFrame?.Invoke(idle);
            _devDebug.Use3xPortraitInLayoutB = false;
            SyncLayoutFromConfig();
            renderFrame?.Invoke(idle);

            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            _devDebug.Use3xPortraitInLayoutB = true;
            SyncLayoutFromConfig();
            if (primarySlate.X != 770 || primarySlate.Y != 352)
                throw new InvalidOperationException($"Live switch B1->B2 failed to preserve Slate Bus docked slate at (770,352), got ({primarySlate.X},{primarySlate.Y}).");
            renderFrame?.Invoke(idle);
            _devDebug.Use3xPortraitInLayoutB = false;
            SyncLayoutFromConfig();
            renderFrame?.Invoke(idle);
            _devDebug.Use3xPortraitInLayoutB = true;
            SyncLayoutFromConfig();

            // Dock Primary Slate into Clearance Upload Dock, then toggle B2 -> A -> B1 -> B2
            SmokeStageDockedClearanceSlate(encryptionProgress: 0.50f);
            if (primarySlate.X != 1070 || primarySlate.Y != 384)
                throw new InvalidOperationException($"B2 Clearance Dock bay expected (1070,384), got ({primarySlate.X},{primarySlate.Y}).");

            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
            SyncLayoutFromConfig();
            if (primarySlate.X != 1080 || primarySlate.Y != 384)
                throw new InvalidOperationException($"Live switch B2->A failed to re-anchor Clearance Dock slate to (1080,384), got ({primarySlate.X},{primarySlate.Y}).");

            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();
            if (primarySlate.X != 1070 || primarySlate.Y != 384)
                throw new InvalidOperationException($"Live switch A->B2 failed to re-anchor Clearance Dock slate to (1070,384), got ({primarySlate.X},{primarySlate.Y}).");

            // Contraband Blank Slate docked in Slate Bus + loose Primary Slate near B1 right edge -> B1 -> B2 clamps loose slate cleanly!
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB1_CommsSwap;
            SyncLayoutFromConfig();
            SmokeStageBlankSlatePortSwap();
            primarySlate.X = 876; // valid in B1 (876+184=1060 <= 1064), exceeds B2 (1044)
            primarySlate.Y = 420;
            DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];

            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();
            if (primarySlate.X + primarySlate.Width > _layout.FreeDragBounds.Right ||
                primarySlate.Y < _layout.FreeDragBounds.Y ||
                cItem.X != _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2 ||
                cItem.Y != _layout.SlateBusDockedY)
            {
                throw new InvalidOperationException("Live switch B1->B2 failed to clamp loose desk slate or keep Blank Slate anchored in Slate Bus.");
            }

            // Mid-drag preservation: dragging slate must remain cleanly under cursor across layout switch
            _slateLocation = DataSlateLocation.DeskFree;
            primarySlate.X = 500;
            primarySlate.Y = 460;
            _draggingItemId = DeskItemId.DataSlate;
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
            SyncLayoutFromConfig();
            if (primarySlate.X != 500 || primarySlate.Y != 460)
                throw new InvalidOperationException("Live layout switch moved a mid-drag slate away from the cursor.");
            _draggingItemId = null;

            // Dynamic Protocol Review circuit trace endpoints (BOTH crimson fault & teal match) across live toggle
            SmokeStageCrimsonFaultTrace();
            if (!TryGetReviewTargetScreenRect(ReviewTarget.VisualSignalTransponderId, out Rectangle feedRectA) ||
                (int)feedRectA.X != 688 || (int)feedRectA.Y != 184)
            {
                throw new InvalidOperationException("OptionA_Classic Transponder ID trace endpoint mismatch.");
            }

            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();
            if (!TryGetReviewTargetScreenRect(ReviewTarget.VisualSignalTransponderId, out Rectangle feedRectB) ||
                (int)feedRectB.X != 1052 || (int)feedRectB.Y != 264 || (int)feedRectB.Width != 220 || (int)feedRectB.Height != 88)
            {
                throw new InvalidOperationException("OptionB2 Transponder ID crimson trace endpoint did not dynamically re-anchor to (1052,264,220,88).");
            }
            if (!TryGetReviewTargetScreenRect(ReviewTarget.SpsMiniScreenHotWeapons, out Rectangle spsRect) ||
                (int)spsRect.X != SpsMiniX + 4)
            {
                throw new InvalidOperationException("SPS Anomaly Mini-Screen trace endpoint failed to anchor cleanly.");
            }
            renderFrame?.Invoke(idle);

            // Teal NO FAULT trace dynamic re-anchoring
            ResetFaultSequenceState();
            if (_activeShip != null)
            {
                _activeShip.ProtocolReviewVerified = false;
                _activeShip.GroundTruth.RawTransponderId = _activeShip.Slate.RegistrationCode;
                _activeShip.GroundTruth.ActiveInfractions.Clear();
                _protocolReviewModeActive = true;
                EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);
                if (_noFaultTraceTimer <= 0.1f || _noFaultTargetB != ReviewTarget.VisualSignalTransponderId)
                    throw new InvalidOperationException("Teal PROTOCOLS MATCH trace failed to activate.");

                _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
                SyncLayoutFromConfig();
                if (!TryGetReviewTargetScreenRect(_noFaultTargetB, out Rectangle tealRectA) || (int)tealRectA.X != 688)
                    throw new InvalidOperationException("Teal trace endpoint failed to re-anchor to Classic A.");

                _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
                SyncLayoutFromConfig();
                if (!TryGetReviewTargetScreenRect(_noFaultTargetB, out Rectangle tealRectB) || (int)tealRectB.X != 1052)
                    throw new InvalidOperationException("Teal trace endpoint failed to re-anchor to B2.");
                renderFrame?.Invoke(idle);
            }

            // 3. Non-Blocking RX PACKET Download While Greeting & Officer Line Stream in Typewriter Queue
            _preShiftStage = PreShiftStage.None;
            _shiftController.IsPreShiftSequenceActive = false;
            _shiftController.NotifyFirstHailTriggered();
            _slateLocation = DataSlateLocation.SlateBus;
            _contrabandSlateInSlateBus = false;
            _activeDeskContrabandItem = null;
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = false;
            SyncLayoutFromConfig();

            StartNextShipEncounter(DebugSpawnValidityMode.ForceValid, reseedNonce: 42u);
            // Finish approach glide -> greeting starts streaming character-by-character!
            Update(ShipApproachDurationSeconds + 0.05f, idle);
            if (_phase != EncounterPhase.OpeningGreetingDelay || !_commsQueue.IsTypingActiveLine)
                throw new InvalidOperationException("Ship arrival did not begin character-by-character greeting stream.");

            // Advance 0.36s -> InitiateSubspaceProtocolRequest fires "Send subspace protocols." and MUST start 1.80s RX PACKET immediately!
            Update(OpeningGreetingBeatSeconds + 0.02f, idle);
            if (_phase != EncounterPhase.DownloadingProtocols || !_commsQueue.HasPendingOrActive)
                throw new InvalidOperationException("Typewriter streaming illegally blocked 1.80s RX PACKET download.");

            // Advance 1.85s -> Data Slate MUST complete download and enter InspectingSlateAndShip non-blockingly!
            Update(ProtocolDownloadDurationSeconds + 0.05f, idle);
            if (_phase != EncounterPhase.InspectingSlateAndShip || _slateTelemetry != DataSlateTelemetryState.Loaded)
                throw new InvalidOperationException("RX PACKET download did not reach Loaded non-blockingly while dialogue queued.");

            // 4. Interactive Click-to-Finish Current Line (snaps active line 100%, preserves queued Captain reply + [RECEIVING...]!)
            _commsQueue.Clear();
            EnqueueOfficerLine("Confirm route, purpose of visit, and cargo.", CommsTextTone.ClearanceTeal);
            EnqueueCaptainLine("CAPT: \"Returning from Veridia-7 with sealed hydroponics.\"", CommsTextTone.AmberPrimary);
            Update(0.15f, idle); // Officer mid-type!
            if (!_commsQueue.IsTypingActiveLine || _commsQueue.ActiveSpeaker != CommsSpeakerKind.Officer || _commsQueue.QueuedCount != 1)
                throw new InvalidOperationException("Officer line was not actively typing with Captain reply queued.");

            int committedBeforeClick = _commsQueue.CommittedLines.Count;
            Update(0.016f, new VirtualMouseState
            {
                X = _layout.Transcript.X + 40,
                Y = _layout.Transcript.Y + 40,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (_commsQueue.CommittedLines.Count != committedBeforeClick + 1 ||
                !_commsQueue.CommittedLines[^1].Text.Contains("Confirm route", StringComparison.Ordinal) ||
                !_commsQueue.IsWaitingReceiving)
            {
                throw new InvalidOperationException("Clicking CRT Comms Log did not snap active Officer line to 100% while preserving queued Captain [RECEIVING...] beat.");
            }
            renderFrame?.Invoke(idle);

            // 5. Auto-scroll pauses on manual scroll-up and resumes on scroll-down to bottom
            for (int k = 0; k < 18; k++)
            {
                AddSystemLogLine($"[LOG {k:D2}] SCROLL TEST TELEMETRY ENTRY FOR B2 11-ROW VIEWPORT.", CommsTextTone.AmberDimmed);
            }
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            renderFrame?.Invoke(idle);
            if (!_transcriptAutoFollowBottom)
                throw new InvalidOperationException("Auto-scroll should remain locked to bottom before manual scroll-up.");

            Update(0.016f, new VirtualMouseState
            {
                X = _layout.Transcript.X + 40,
                Y = _layout.Transcript.Y + 40,
                IsInsideViewport = true,
                WheelMove = 1.0f,
                Scale = 1.0f
            });
            if (_transcriptAutoFollowBottom)
                throw new InvalidOperationException("Manual scroll-up did not pause CRT Comms Log auto-scroll.");

            EnqueueOfficerLine("Still reading earlier transcript entries.", CommsTextTone.ClearanceTeal);
            Update(0.20f, idle);
            if (_transcriptAutoFollowBottom)
                throw new InvalidOperationException("Streaming new characters illegally overrode paused manual scroll position.");

            // Scroll back to bottom -> resumes auto-follow
            for (int s = 0; s < 10; s++)
            {
                Update(0.016f, new VirtualMouseState
                {
                    X = _layout.Transcript.X + 40,
                    Y = _layout.Transcript.Y + 40,
                    IsInsideViewport = true,
                    WheelMove = -1.0f,
                    Scale = 1.0f
                });
            }
            if (!_transcriptAutoFollowBottom)
                throw new InvalidOperationException("Scrolling back to bottom did not resume CRT Comms Log auto-scroll.");

            // 6. Verify 0.5s PROTOCOL FAULT beat cleanly integrates into typewriter queue without overlap
            SmokeStageShipLoadedAndInspecting(enableSps: false);
            if (_activeShip != null)
            {
                _activeShip.GroundTruth.RawTransponderId = "KDF-888-Z";
                _activeShip.GroundTruth.ActiveInfractions =
                [
                    new ShipInfraction { Type = ShipInfractionType.TransponderIdMismatch, Code = "PROTO-01T", CitationReason = "FAULT QUEUE CHECK" }
                ];
                _protocolReviewModeActive = true;
                EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);

                Update(0.25f, idle); // Mid 0.50s fault beat
                if (_faultStage != FaultSequenceStage.FaultBeat ||
                    _commsQueue.CommittedLines.Any(l => l.Text.Contains("[PROTOCOL FAULT]", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("0.5s PROTOCOL FAULT beat printed prematurely at 0.25s.");
                }

                Update(0.35f, idle); // Past 0.50s beat -> fault banner committed, Officer accusation now typing!
                if (!_commsQueue.CommittedLines.Any(l => l.Text.Contains("[PROTOCOL FAULT]", StringComparison.Ordinal)) ||
                    !_commsQueue.IsTypingActiveLine ||
                    _commsQueue.ActiveSpeaker != CommsSpeakerKind.Officer)
                {
                    throw new InvalidOperationException("PROTOCOL FAULT beat failed to transition cleanly into Officer typewriter line.");
                }

                // Snap Officer accusation -> must enter [RECEIVING...] lag before Captain excuse!
                _commsQueue.FinishActiveLineImmediately();
                if (!_commsQueue.IsWaitingReceiving)
                    throw new InvalidOperationException("PROTOCOL FAULT confrontation missed [RECEIVING...] lag between Officer and Captain.");

                Update(ShiftController.SubspaceReceivingLagSeconds + 0.15f, idle);
                if (!_commsQueue.IsTypingActiveLine || _commsQueue.ActiveSpeaker != CommsSpeakerKind.Captain || _faultStage != FaultSequenceStage.None)
                    throw new InvalidOperationException("PROTOCOL FAULT Captain excuse did not start typing cleanly after [RECEIVING...] beat.");
            }
        }
        finally
        {
            _config.DeskUpperLayoutMode = originalMode;
            _devDebug.Use3xPortraitInLayoutB = original3x;
            _devDebug.AnimationSpeedPresetIndex = originalAnimIdx;
            SyncLayoutFromConfig();
        }
    }

    internal void SmokeVerifyBuild435DeskHardening(ScreenManager screenManager)
    {
        DeskUpperLayoutMode originalMode = _config.DeskUpperLayoutMode;
        bool original3x = _devDebug.Use3xPortraitInLayoutB;
        int originalAnimIdx = _devDebug.AnimationSpeedPresetIndex;
        _devDebug.AnimationSpeedPresetIndex = 1; // 1.0x

        try
        {
            // 1. Mid-shift DeskScreen.OnExit recovers loose non-burnt Covert Mission Slate & Blank/Copied slates
            _gameState.AdvanceToDay(7);
            _gameState.PocketCapacity = 1;
            _gameState.PocketItems.Clear();
            _gameState.VentStashItems.Clear();
            _gameState.ServiceChuteItems.Clear();
            _gameState.SecurityLogs.Clear();

            screenManager.SwitchTo(ScreenId.Desk);
            _activeDeskContrabandItem = new ContrabandPocketItem
            {
                ItemId = "smoke435_mission_1",
                ItemType = ContrabandPocketItem.TypeMissionDataSlate,
                DisplayName = "COVERT SLATE [435]",
                IsMissionSlate = true,
                MissionId = "slate_bus_mission_01",
                MissionTargetReg = "FTV-719-R"
            };
            _contrabandSlateInSlateBus = true;
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
            screenManager.SwitchTo(ScreenId.MainMenu);
            if (_activeDeskContrabandItem != null ||
                _gameState.PocketItems.Count != 1 ||
                _gameState.PocketItems[0].ItemId != "smoke435_mission_1")
            {
                throw new InvalidOperationException("DeskScreen.OnExit (MainMenu switch) failed to recover loose Covert Mission Slate into PocketItems.");
            }

            // When PocketItems is full, OnExit recovers into VentStashItems
            screenManager.SwitchTo(ScreenId.Desk);
            _activeDeskContrabandItem = new ContrabandPocketItem
            {
                ItemId = "smoke435_stolen_vent",
                ItemType = ContrabandPocketItem.TypeCopiedDataSlate,
                DisplayName = "STOLEN SLATE: BMC-901-A",
                TargetShipRegistration = "BMC-901-A",
                TargetShipPrefix = "BMC-"
            };
            _contrabandSlateInSlateBus = false;
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
            screenManager.SwitchTo(ScreenId.Quarters);
            if (_activeDeskContrabandItem != null ||
                _gameState.VentStashItems.Count != 1 ||
                _gameState.VentStashItems[0].ItemId != "smoke435_stolen_vent")
            {
                throw new InvalidOperationException("DeskScreen.OnExit (Quarters switch) failed to recover loose slate into VentStashItems when PocketItems was full.");
            }

            // 2. Blank Data Slate 1-99% mid-download pull-out abort + [RE-TRANSMIT] parity
            _gameState.PocketItems.Clear();
            _gameState.VentStashItems.Clear();
            _gameState.SecurityLogs.Clear();
            _gameState.AdvanceToDay(6);
            screenManager.SwitchTo(ScreenId.Desk);

            SmokeStageBlankSlateDownloading(0.50f);
            if (!IsBlankSlateDownloading || _phase != EncounterPhase.DownloadingProtocols)
                throw new InvalidOperationException("Failed to stage Blank Data Slate downloading at 50%.");

            DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
            int cPressX = cItem.X + 40;
            int cPressY = cItem.Y + 40;
            Update(0.016f, new VirtualMouseState { X = cPressX, Y = cPressY, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            // Drag >= 10px out of Slate Bus -> aborts mid-download cleanly, stays BlankDataSlate, logs zero [UNAUTH_IO]
            Update(0.016f, new VirtualMouseState { X = cPressX, Y = cPressY + 48, IsInsideViewport = true, LeftDown = true, Scale = 1.0f });
            if (_phase != EncounterPhase.DownloadAborted ||
                _isDownloadingToBlankSlate ||
                _contrabandSlateInSlateBus ||
                _activeDeskContrabandItem?.ItemType != ContrabandPocketItem.TypeBlankDataSlate ||
                _gameState.UnscrubbedLogCount != 0)
            {
                throw new InvalidOperationException("Pulling Blank Data Slate mid-download (1-99%) did not cleanly abort to DownloadAborted without copying.");
            }
            Update(0.016f, new VirtualMouseState { X = 420, Y = 440, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });

            // Drag Blank Slate back onto Slate Bus while DownloadAborted -> docks cleanly and waits for [RE-TRANSMIT]
            Update(0.016f, new VirtualMouseState { X = cItem.X + 40, Y = cItem.Y + 40, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            Update(0.016f, new VirtualMouseState { X = _layout.SlateBus.X + 60, Y = _layout.SlateBus.Y + 20, IsInsideViewport = true, LeftDown = true, Scale = 1.0f });
            Update(0.016f, new VirtualMouseState { X = _layout.SlateBus.X + 60, Y = _layout.SlateBus.Y + 20, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });
            if (!_contrabandSlateInSlateBus || _phase != EncounterPhase.DownloadAborted || _isDownloadingToBlankSlate)
                throw new InvalidOperationException("Re-plugging Blank Data Slate in DownloadAborted did not dock cleanly awaiting [RE-TRANSMIT].");

            // Click [RE-TRANSMIT] -> restarts download & completes 1.80s capture into CopiedDataSlate!
            Update(0.016f, new VirtualMouseState { X = _layout.NextHailBtn.X + 20, Y = _layout.NextHailBtn.Y + 12, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            if (_pendingReTransmitRestartTimer <= 0f)
                throw new InvalidOperationException("Clicking [RE-TRANSMIT] with docked Blank Data Slate did not arm re-transmit restart.");

            VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            Update(ReTransmitAnnoyedDelaySeconds + 0.05f, idle);
            if (!IsBlankSlateDownloading || _phase != EncounterPhase.DownloadingProtocols)
                throw new InvalidOperationException("[RE-TRANSMIT] with docked Blank Data Slate did not restart Blank Slate RX download.");

            Update(ProtocolDownloadDurationSeconds + 0.05f, idle);
            if (IsBlankSlateDownloading || _activeDeskContrabandItem?.ItemType != ContrabandPocketItem.TypeCopiedDataSlate || _gameState.UnscrubbedLogCount < 1)
                throw new InvalidOperationException("[RE-TRANSMIT] Blank Data Slate download did not complete into CopiedDataSlate.");

            // 3. Topmost document click priority over Row 2 hardware + OptionA FreeDeskRestMinY clamping + Clock-Out drag cleanup
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionA_Classic;
            SyncLayoutFromConfig();
            _gameState.Day = 3;
            SmokeStageShipLoadedAndInspecting(enableSps: false);
            _activeShip!.GroundTruth.ActualShieldsUp = false;
            _warpPhase = WarpPrinterPhase.TornOffFreeDocument;
            DraggableDeskItem strip = _deskItems[DeskItemId.WarpStrip];
            strip.IsVisible = true;
            // Place WarpStrip directly over [SCAN WARP] button (WarpPrinterX + 4, WarpPrinterY + 56) and reset warpPhase to Idle?
            // Test with WarpRefSpot [<] button (WarpSelectorX, WarpSelectorY) which is right underneath StripSnap / WarpRefSpot!
            strip.X = WarpSelectorX - 4;
            strip.Y = WarpRefSpotY;
            BringDeskItemToFront(DeskItemId.WarpStrip);
            int refIdxBefore = _warpRefSpotSystemIndex;
            Update(0.016f, new VirtualMouseState { X = WarpSelectorX + 8, Y = WarpSelectorY + 8, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            if (_draggingItemId != DeskItemId.WarpStrip || _warpRefSpotSystemIndex != refIdxBefore)
                throw new InvalidOperationException("Topmost WarpStrip did not win click priority over buried Row 2 WarpRefSpot [<] button.");
            Update(0.016f, new VirtualMouseState { X = 500, Y = 450, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });

            // OptionA_Classic: free-resting DataSlate released at y = 320 (over Row 2) clamps to FreeDeskRestMinY (356),
            // while SlateBusDockedSlate remains cleanly at (698, 308)!
            DraggableDeskItem slate = _deskItems[DeskItemId.DataSlate];
            _slateLocation = DataSlateLocation.DeskFree;
            slate.X = 300;
            slate.Y = 320;
            _draggingItemId = DeskItemId.DataSlate;
            UpdateActiveDrag(new VirtualMouseState { X = 320, Y = 330, IsInsideViewport = true, LeftDown = false, LeftReleased = true, Scale = 1.0f });
            if (slate.Y < _layout.FreeDeskRestMinY || _layout.FreeDeskRestMinY != 356)
                throw new InvalidOperationException($"OptionA_Classic loose DataSlate did not clamp to FreeDeskRestMinY == 356 (got Y={slate.Y}).");

            // Clock-Out clears active drag, anchored strip pull, and protocol review state immediately
            _activeShip = null;
            _shiftController.HasActiveShipInBay = false;
            _phase = EncounterPhase.BayEmpty;
            _draggingItemId = DeskItemId.DataSlate;
            _isPullingAnchoredStrip = true;
            _protocolReviewModeActive = true;
            _clockOutPowerDownActive = false;
            _clockOutSettled = false;
            _shiftController.ForceEndShiftNow();
            Update(0.016f, idle);
            if (!_clockOutPowerDownActive || _draggingItemId.HasValue || _isPullingAnchoredStrip || _protocolReviewModeActive)
                throw new InvalidOperationException("Clock-Out power-down start did not clear active drag, anchored strip pull, and protocol review state.");

            // Reset clock-out state for remaining assertions
            _clockOutPowerDownActive = false;
            _clockOutPowerDownElapsed = 0f;
            _clockOutAwaitingLeavePrompt = false;
            _clockOutSettled = false;
            PowerIndicatorBrightness = 1.0f;

            // 4. Premature Day 3 & Day 4 Protocol Review target gating (Day 3-4 vs Day 5/7/10)
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();
            for (int preDay = 3; preDay <= 4; preDay++)
            {
                _gameState.Day = preDay;
                SmokeStageShipLoadedAndInspecting(enableSps: false);
                _slateLocation = DataSlateLocation.DeskFree;
                slate.X = 400;
                slate.Y = 400;
                BringDeskItemToFront(DeskItemId.DataSlate);

                ReviewTarget[] lockedPreManual =
                [
                    ReviewTarget.TopHeaderCycle,
                    ReviewTarget.DataSlateOrg,
                    ReviewTarget.DataSlateExpiry,
                    ReviewTarget.DataSlateSpecies,
                    ReviewTarget.DataSlateClass,
                    ReviewTarget.DataSlateCargo,
                    ReviewTarget.ViewportShipHull
                ];
                foreach (ReviewTarget t in lockedPreManual)
                {
                    if (IsReviewTargetUnlocked(t))
                        throw new InvalidOperationException($"Protocol Review target {t} should be locked on Day {preDay}.");
                }
                if (!IsReviewTargetUnlocked(ReviewTarget.DataSlateOrigin) || !IsReviewTargetUnlocked(ReviewTarget.WarpStripPaper))
                    throw new InvalidOperationException($"DataSlateOrigin and WarpStripPaper must be unlocked on Day {preDay}.");

                // Clicking ORG / EXP / SPEC / CLASS / CARGO / TopHeaderCycle on Day 3-4 returns ReviewTarget.None
                if (DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = TopCycleX + 20, Y = TopCycleY + 10, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None ||
                    DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = slate.X + 80, Y = slate.Y + 48, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None ||
                    DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = slate.X + 20, Y = slate.Y + 108, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None ||
                    DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = slate.X + 20, Y = slate.Y + 120, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None ||
                    DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = slate.X + 20, Y = slate.Y + 144, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None ||
                    DetectProtocolReviewTargetAtMouse(new VirtualMouseState { X = slate.X + 20, Y = slate.Y + 184, IsInsideViewport = true, Scale = 1.0f }) != ReviewTarget.None)
                {
                    throw new InvalidOperationException($"Premature Manual/Cargo Protocol Review hitboxes responded on Day {preDay}.");
                }
            }

            // Day 5 (DayUnlockManualPart1) unlocks TopHeaderCycle / Org / Expiry / Species (Class & Cargo still locked)
            _gameState.Day = _config.DayUnlockManualPart1;
            if (!IsReviewTargetUnlocked(ReviewTarget.TopHeaderCycle) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateOrg) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateExpiry) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateSpecies) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateClass) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo))
            {
                throw new InvalidOperationException("Day 5 (Manual Part 1) Protocol Review target unlock state mismatch.");
            }

            // Day 7 (DayUnlockManualHulls) unlocks DataSlateClass & ViewportShipHull (Cargo still locked)
            _gameState.Day = _config.DayUnlockManualHulls;
            if (!IsReviewTargetUnlocked(ReviewTarget.DataSlateClass) ||
                !IsReviewTargetUnlocked(ReviewTarget.ViewportShipHull) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo))
            {
                throw new InvalidOperationException("Day 7 (Manual Part 2) Protocol Review target unlock state mismatch.");
            }

            // Day 10 (DayUnlockWaferCargo) unlocks DataSlateCargo
            _gameState.Day = _config.DayUnlockWaferCargo;
            if (!IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo) ||
                !IsReviewTargetUnlocked(ReviewTarget.SpsMiniScreenCargoPower))
            {
                throw new InvalidOperationException("Day 10 (Cargo) Protocol Review target unlock state mismatch.");
            }
        }
        finally
        {
            _config.DeskUpperLayoutMode = originalMode;
            _devDebug.Use3xPortraitInLayoutB = original3x;
            _devDebug.AnimationSpeedPresetIndex = originalAnimIdx;
            SyncLayoutFromConfig();
            _gameState.PocketItems.Clear();
            _gameState.VentStashItems.Clear();
            _gameState.ServiceChuteItems.Clear();
            _gameState.SecurityLogs.Clear();
            _activeDeskContrabandItem = null;
            _contrabandSlateInSlateBus = false;
            OnExit();
        }
    }
}
