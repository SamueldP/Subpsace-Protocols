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
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;
        _shiftController.NotifyFirstHailTriggered();
        _shiftController.ForceSetElapsedSeconds(seconds);
    }

    internal void SmokeCompletePreShiftHail()
    {
        if (IsPreShiftTalkingHeadHailActive)
        {
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            ClosePreShiftLiveHail();
        }
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
        int maxTab = IsManualHullsEnforced ? 2 : 1;
        _manualActiveTab = Math.Clamp(tab, 0, maxTab);
        if (_manualActiveTab == 2 && IsManualHullsEnforced)
        {
            RebuildManualSilhouetteTexture();
        }
        else
        {
            PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
        }
    }

    internal (int WeaponScreenX, int WeaponScreenY) SmokeStageHonestArmedShipWithSps(bool applyWeaponsPowerDown = false, bool spoofed = false)
    {
        PixelBuffer.UnloadTexture(ref _cachedPreShiftKordaniPortraitTex);
        _preShiftStage = PreShiftStage.None;
        _shiftController.IsPreShiftSequenceActive = false;
        _shiftController.NotifyFirstHailTriggered();
        StartNextShipEncounter(spoofed ? DebugSpawnValidityMode.ForceInvalid : DebugSpawnValidityMode.ForceValid, reseedNonce: 7u);
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
        _activeShip.GroundTruth.HasSpoofedWeapons = spoofed;
        _activeShip.Slate.DeclaredShieldsStatus = "DOWN";
        _activeShip.Slate.DeclaredWeaponsStatus = spoofed ? "OFFLINE" : "ARMED";
        _spsLoggedAnomalies.Clear();
        _autoSpsWeaponsOrderTriggered = false;
        RebuildActiveShipTextures();

        _spsEnabled = IsSpsUnlocked;
        _spsProbeExtend = _spsEnabled ? 1.0f : 0.0f;

        int wx = 310;
        int wy = 165;
        if (TryGetActiveWeaponsSpsTarget(out _, out Vector2 wpnScreenCenter))
        {
            // Center swaying lens cleanly over forward weapon mount and sweep via UpdateSpsHoldToLock!
            wx = (int)MathF.Round(wpnScreenCenter.X);
            wy = (int)MathF.Round(wpnScreenCenter.Y);
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

    internal (int LiveTexW, int LiveTexH, int SpsTexW, int SpsTexH) SmokeGetActiveShipTextureSizes() =>
        (_cachedShipLiveTex.Width, _cachedShipLiveTex.Height, _cachedShipSpsTex.Width, _cachedShipSpsTex.Height);

    internal (int ManualTexW, int ManualTexH) SmokeGetManualTextureSize() =>
        (_cachedManualShipTex.Width, _cachedManualShipTex.Height);

    internal bool SmokeTryLockSpsCargoPower(out int lockX, out int lockY)
    {
        lockX = 310;
        lockY = 165;
        if (!TryGetActiveCargoSpsTarget(out _, out Vector2 cargoCenter))
            return false;

        lockX = (int)MathF.Round(cargoCenter.X);
        lockY = (int)MathF.Round(cargoCenter.Y);
        _currentSpsSwayOffset = Vector2.Zero;
        VirtualMouseState aimMouse = new() { X = lockX, Y = lockY, IsInsideViewport = true, Scale = 1.0f };
        UpdateSpsLensRect(aimMouse);
        UpdateSpsHoldToLock(SpsLockDurationSeconds + 0.05f, aimMouse);
        return _spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.VioletCargoPower && !e.IsCooled);
    }

    internal bool SmokeHasWeaponsHotSpsLog() =>
        _spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.HotWeaponMount && !e.IsCooled);

    internal bool SmokeStageAndLockV2CargoPower(out int lockX, out int lockY)
    {
        lockX = 310;
        lockY = 165;
        if (_activeShip == null)
        {
            SmokeStageShipLoadedAndInspecting(enableSps: true);
        }
        if (_activeShip == null)
            return false;

        _shipSlideOffset = 0f;
        _phase = EncounterPhase.InspectingSlateAndShip;
        _activeShip.GroundTruth.ActualShieldsUp = false;
        _activeShip.GroundTruth.ActualWeaponsHot = false;
        _activeShip.GroundTruth.HasSpoofedWeapons = false;
        _activeShip.GroundTruth.ActualCargoPowerDraw = true;
        _spsEnabled = IsSpsUnlocked;
        _spsProbeExtend = _spsEnabled ? 1.0f : 0.0f;
        _spsLoggedAnomalies.Clear();
        RebuildActiveShipTextures();
        return SmokeTryLockSpsCargoPower(out lockX, out lockY);
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

        // 3. Verify Auto-SPS Weapons Compliance on sweep (Day 2+) for Honest Armed & Spoofed Liars
        if (IsSpsUnlocked)
        {
            // Honest Armed Ship: sweeping triggers "Power down weapons." ONCE, waits for speech completion before arming,
            // does not lock [!] HOT WEAPONS fault, cools on compliance
            (int wx, int wy) = SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: false);
            if (!_autoSpsWeaponsOrderTriggered || _pendingWeaponsOrder || _spsLoggedAnomalies.Count != 0)
            {
                throw new InvalidOperationException("Auto-SPS sweep on honest armed ship armed compliance prematurely or locked a fault.");
            }
            _commsQueue.FlushAllImmediately(invokeCallbacks: true);
            if (!_pendingWeaponsOrder)
            {
                throw new InvalidOperationException("Auto-SPS sweep failed to arm _pendingWeaponsOrder after 'Power down weapons.' finished.");
            }
            int pwrLineCount = _commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal));
            if (pwrLineCount != 1)
            {
                throw new InvalidOperationException($"Expected exactly 1 'Power down weapons.' line on honest sweep, got {pwrLineCount}.");
            }

            // Re-sweeping repeatedly must NOT spam "Power down weapons."
            VirtualMouseState aimMouse = new() { X = wx, Y = wy, IsInsideViewport = true, Scale = 1.0f };
            UpdateSpsHoldToLock(0.20f, aimMouse);
            _commsQueue.FlushAllImmediately(invokeCallbacks: true);
            if (_commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException("Repeated SPS sweep over honest weapons spammed 'Power down weapons.'.");
            }

            _complianceTimer = 0f;
            ResolvePendingComplianceOrders();
            if (_activeShip == null || _activeShip.GroundTruth.ActualWeaponsHot || SmokeHasWeaponsHotSpsLog())
            {
                throw new InvalidOperationException("Honest armed ship failed to cool down weapons cleanly after Auto-SPS compliance.");
            }

            // 4. Spoofed Weapons Liar: sweep fires "Power down weapons." once, waits for speech completion before arming,
            //    captain bluffs/stalls while weapons stay hot, and 0.5s hold locks [!] HOT WEAPONS onto SPS Anomaly Mini-Screen!
            (int swx, int swy) = SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: true);
            if (!SmokeHasWeaponsHotSpsLog() || !_autoSpsWeaponsOrderTriggered || _pendingWeaponsOrder)
            {
                throw new InvalidOperationException("SPS failed to lock [!] HOT WEAPONS on spoofed vessel or armed compliance prematurely.");
            }

            _commsQueue.FlushAllImmediately(invokeCallbacks: true);
            if (!_pendingWeaponsOrder)
            {
                throw new InvalidOperationException("Spoofed Auto-SPS sweep failed to arm _pendingWeaponsOrder after 'Power down weapons.' completed.");
            }
            if (_commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException("Expected exactly 1 'Power down weapons.' line on spoofed ship sweep.");
            }

            // Second sweep must still stay debounced
            UpdateSpsHoldToLock(0.25f, new VirtualMouseState { X = swx, Y = swy, IsInsideViewport = true, Scale = 1.0f });
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            if (_commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException("Repeated SPS sweep over spoofed ship spammed 'Power down weapons.'.");
            }

            // Clicking SPS Mini-Screen directly outside Protocol Review must NOT trigger a fault or compliance order
            int miniClickX = SpsMiniX + 40;
            int miniClickY = SpsMiniY + 36;
            _pendingWeaponsOrder = false;
            Update(0.016f, new VirtualMouseState { X = miniClickX, Y = miniClickY, IsInsideViewport = true, LeftPressed = true, LeftDown = true, Scale = 1.0f });
            if (_faultStage != FaultSequenceStage.None || _pendingWeaponsOrder || _activeShip == null || !_activeShip.GroundTruth.ActualWeaponsHot)
            {
                throw new InvalidOperationException("Clicking SPS Anomaly Mini-Screen illegally triggered a command or accusation.");
            }

            if (IsProtocolReviewUnlocked && _activeShip != null)
            {
                _pendingWeaponsOrder = true;
                _complianceTimer = 0f;
                ResolvePendingComplianceOrders();
                _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                if (!_activeShip.GroundTruth.ActualWeaponsHot ||
                    _spsLoggedAnomalies[0].IsCooled ||
                    !_commsQueue.CommittedLines.Any(l => l.Text.Contains(_activeShip.SpoofedWeaponsBluffLine, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("1st ResolveWeaponsOrder on spoofed ship failed to bluff compliance while staying visibly hot in SPS.");
                }

                _protocolReviewModeActive = true;
                EvaluateProtocolReviewPair(ReviewTarget.DataSlateWeapons, ReviewTarget.SpsMiniScreenHotWeapons);
                if (_faultStage != FaultSequenceStage.FaultBeat || !_activeShip.ProtocolReviewVerified)
                {
                    throw new InvalidOperationException("Protocol Review failed to verify spoofed [!] HOT WEAPONS via SPS Anomaly Mini-Screen.");
                }
                _commsQueue.FlushAllImmediately(invokeCallbacks: false);
                ResetFaultSequenceState();
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

        // 2. Day 3 Ghost Route [INSPECT ROUTE] pushback button & uncanny warm Warp Pylon SPS cue
        _gameState.Day = Constants.DayGhostRouteSuspect;
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
            if (_activeShip != null)
            {
                _activeShip.GroundTruth.ActualShieldsUp = false;
                _activeShip.Slate.DeclaredShieldsStatus = "DOWN";
                RebuildActiveShipTextures();
            }
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

            // 4. Premature Day 3 (Pre-Warp) & Days 4-7 (Warp-unlocked, Pre-Manual) Protocol Review target gating
            _config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();

            _gameState.Day = Constants.DayGhostRouteSuspect; // Day 3 breather (before Day 4 Warp Scanner)
            if (IsReviewTargetUnlocked(ReviewTarget.DataSlateOrigin) || IsReviewTargetUnlocked(ReviewTarget.WarpStripPaper))
                throw new InvalidOperationException("Day 3 breather must keep DataSlateOrigin and WarpStripPaper locked before Day 4 Warp unlock.");

            for (int preDay = _config.DayUnlockWarpScanner; preDay < _config.DayUnlockManualPart1; preDay++)
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

                // Clicking ORG / EXP / SPEC / CLASS / CARGO / TopHeaderCycle on Days 4-7 returns ReviewTarget.None
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

            // Day 8 (DayUnlockManualPart1) unlocks TopHeaderCycle / Org / Expiry / Species (Class & Cargo still locked)
            _gameState.Day = _config.DayUnlockManualPart1;
            if (!IsReviewTargetUnlocked(ReviewTarget.TopHeaderCycle) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateOrg) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateExpiry) ||
                !IsReviewTargetUnlocked(ReviewTarget.DataSlateSpecies) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateClass) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo))
            {
                throw new InvalidOperationException("Day 8 (Manual Part 1) Protocol Review target unlock state mismatch.");
            }

            // Day 10 (DayUnlockManualHulls) unlocks DataSlateClass & ViewportShipHull (Cargo still locked)
            _gameState.Day = _config.DayUnlockManualHulls;
            if (!IsReviewTargetUnlocked(ReviewTarget.DataSlateClass) ||
                !IsReviewTargetUnlocked(ReviewTarget.ViewportShipHull) ||
                IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo))
            {
                throw new InvalidOperationException("Day 10 (Manual Part 2) Protocol Review target unlock state mismatch.");
            }

            // Day 12 (DayUnlockWaferCargo) unlocks DataSlateCargo
            _gameState.Day = _config.DayUnlockWaferCargo;
            if (!IsReviewTargetUnlocked(ReviewTarget.DataSlateCargo) ||
                !IsReviewTargetUnlocked(ReviewTarget.SpsMiniScreenCargoPower))
            {
                throw new InvalidOperationException("Day 12 (Cargo) Protocol Review target unlock state mismatch.");
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

    internal void SmokeVerifyBuild45AutoShieldsAndWeapons()
    {
        bool prevSuppress = _gameState.SuppressDiskPersistence;
        _gameState.SuppressDiskPersistence = true;

        try
        {
            // 1. Day 1 Auto-Shields-Down cadence -> visible drop -> automatic RX PACKET + Auto-Interrogation
            _gameState.ResetForNewSave(1);
            _gameState.Day1BrengopEmailRead = true;
            _gameState.Day1VorstHailCompleted = true;
            OnEnter();

            _shiftController.NotifyFirstHailTriggered();
            StartNextShipEncounter(DebugSpawnValidityMode.ForceValid, reseedNonce: 451u);
            if (_activeShip == null)
                throw new InvalidOperationException("Build 4.5 Auto-Shields test failed to spawn active ship.");

            _activeShip.GroundTruth.ActualShieldsUp = true;
            _activeShip.Slate.DeclaredShieldsStatus = "UP";
            RebuildActiveShipTextures();

            _shipApproachElapsed = ShipApproachDurationSeconds;
            _shipSlideOffset = 0f;
            _phase = EncounterPhase.OpeningGreetingDelay;
            _subspaceAutoTransferDelay = 0f;
            _commsQueue.Clear();

            VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            Update(0.02f, idle);

            if (_phase != EncounterPhase.AwaitingAutoShieldsDrop)
                throw new InvalidOperationException($"Expected AwaitingAutoShieldsDrop after shielded hail, got {_phase}.");

            for (int step = 0; step < 60 && _phase != EncounterPhase.InspectingSlateAndShip; step++)
            {
                Update(0.20f, idle);
            }

            if (_activeShip.GroundTruth.ActualShieldsUp ||
                _activeShip.Slate.DeclaredShieldsStatus != "DOWN" ||
                _phase != EncounterPhase.InspectingSlateAndShip ||
                _slateTelemetry != DataSlateTelemetryState.Loaded)
            {
                throw new InvalidOperationException(
                    $"Day 1 Auto-Shields cadence failed to drop shields or download RX PACKET automatically (phase={_phase}, shldUp={_activeShip.GroundTruth.ActualShieldsUp}, tel={_slateTelemetry}).");
            }

            List<string> committed = _commsQueue.EnumerateAllTextIncludingPending().ToList();
            int idxReq = committed.FindIndex(t => t.Contains("Send subspace protocols.", StringComparison.Ordinal));
            int idxPause = committed.FindIndex(t => t.Trim() == "...");
            int idxShld = committed.FindIndex(t => t.Contains("Shields down, please.", StringComparison.Ordinal));
            int idxInterr = committed.FindIndex(t => t.Contains("Confirm route, purpose of visit, and cargo.", StringComparison.Ordinal));
            if (idxReq < 0 || idxPause <= idxReq || idxShld <= idxPause || idxInterr <= idxShld)
            {
                throw new InvalidOperationException(
                    $"Day 1 Auto-Shields dialogue cadence out of order (req={idxReq}, pause={idxPause}, shld={idxShld}, interr={idxInterr}).");
            }

            // 2. Day 2 Auto-SPS Weapons Sweep Compliance (Honest cools; Spoofed locks [!] HOT WEAPONS)
            _gameState.AdvanceToDay(2);
            OnEnter();

            (int wx, int wy) = SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: false);
            VirtualMouseState spsAim = new() { X = wx, Y = wy, IsInsideViewport = true, Scale = 1.0f };
            for (int s = 0; s < 60 && _activeShip != null && _activeShip.GroundTruth.ActualWeaponsHot; s++)
            {
                _currentSpsSwayOffset = Vector2.Zero;
                Update(0.15f, spsAim);
            }
            _commsQueue.FlushAllImmediately(invokeCallbacks: true);
            if (_activeShip == null || _activeShip.GroundTruth.ActualWeaponsHot ||
                _commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal)) != 1 ||
                SmokeHasWeaponsHotSpsLog())
            {
                throw new InvalidOperationException("Day 2 Auto-SPS honest armed ship sweep failed to cool cleanly or debounced wrongly.");
            }

            (int swx, int swy) = SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: true);
            VirtualMouseState spsSpoofAim = new() { X = swx, Y = swy, IsInsideViewport = true, Scale = 1.0f };
            for (int s = 0; s < 16; s++)
            {
                _currentSpsSwayOffset = Vector2.Zero;
                Update(0.15f, spsSpoofAim);
            }
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            if (_activeShip == null || !_activeShip.GroundTruth.ActualWeaponsHot || !SmokeHasWeaponsHotSpsLog() ||
                _commsQueue.CommittedLines.Count(l => l.Text.Contains("Power down weapons.", StringComparison.Ordinal)) != 1)
            {
                throw new InvalidOperationException("Day 2 Auto-SPS spoofed ship sweep failed to lock [!] HOT WEAPONS or debounced wrongly.");
            }
        }
        finally
        {
            OnExit();
            _gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    internal int GetManualTabCountForCurrentDay() =>
        IsManualUnlocked ? (IsCargoManualTabUnlocked ? 4 : (IsManualHullsEnforced ? 3 : 2)) : 0;

    internal void SmokeVerifyBuild47DeskFeatures(VirtualCanvas canvas, ScreenManager screenManager, Core.ConfigFlags config)
    {
        bool prevSuppress = _gameState.SuppressDiskPersistence;
        _gameState.SuppressDiskPersistence = true;
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };

        try
        {
            // 1. Day 1 Vorst hail -> close -> Field Booklet auto-unfolds once without blocking gameplay
            _gameState.ResetForNewSave(1);
            _gameState.Day1BrengopEmailRead = true;
            _gameState.Day1VorstHailCompleted = false;
            screenManager.SwitchTo(ScreenId.Desk);
            if (!IsPreShiftHailActive || !IsDay1VorstHailActive)
                throw new InvalidOperationException("Day 1 Desk entry did not start Vorst live hail.");
            SmokeCompletePreShiftHail();
            if (!_fieldBooklet.IsOpen || _fieldBooklet.CurrentPage(_gameState, config).Tag != "day1_shift")
                throw new InvalidOperationException("Day 1 Field Booklet did not auto-unfold cleanly post-Vorst hail.");

            // Booklet must NOT block Next Hail button click
            Update(0.016f, new VirtualMouseState
            {
                X = _layout.NextHailBtn.X + 24,
                Y = _layout.NextHailBtn.Y + 14,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (_activeShip == null)
                throw new InvalidOperationException("Unfolded Field Booklet blocked Next Hail gameplay.");

            // 2. Day 2 Regeer Nixa hail (verbatim)
            _gameState.AdvanceToDay(2);
            _gameState.Day2NixaHailCompleted = false;
            screenManager.SwitchTo(ScreenId.Desk);
            if (!IsPreShiftHailActive || !IsDay2NixaHailActive ||
                !_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("Regeer Nixa, Consortium Logistics", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Day 2 Desk entry did not trigger verbatim Regeer Nixa live hail.");
            }
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();
            SmokeCompletePreShiftHail();
            if (!_fieldBooklet.IsOpen || _fieldBooklet.CurrentPage(_gameState, config).Tag != "day2_sps")
                throw new InvalidOperationException("Day 2 Field Booklet did not auto-unfold to day2_sps post-Nixa hail.");

            // 3. Protocol Review crimson trace auto-clear (~2.35s) + "MISMATCH ALREADY FOUND" + multi-infraction non-lockout
            _gameState.AdvanceToDay(config.DayUnlockManualHulls); // Day 10
            _gameState.Day8NixaHailCompleted = true;
            screenManager.SwitchTo(ScreenId.Desk);
            SmokeStageShipLoadedAndInspecting(enableSps: false, triggerDetainClamps: false, triggerFaultBeat: false);
            if (_activeShip == null)
                throw new InvalidOperationException("Failed to stage Day 10 ship for Protocol Review trace check.");

            _activeShip.FlaggedInfractions.Clear();
            _activeShip.ProtocolReviewVerified = false;
            _activeShip.GroundTruth.RawTransponderId = "BAD-999-X";
            _activeShip.Slate.RegistrationCode = "FTV-101-A";
            _activeShip.Slate.DeclaredPrefix = "FTV-";
            _activeShip.Slate.DeclaredCaptainSpecies = PortraitSpecies.Klaasari;
            _activeShip.GroundTruth.ActualCaptainSpecies = PortraitSpecies.Kordani;
            _activeShip.Slate.DeclaredFactionName = "GALACTIC UNION"; // PrefixOrganizationMismatch vs FTV-
            _activeShip.GroundTruth.ActiveInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.TransponderIdMismatch });
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.PrefixOrganizationMismatch });

            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);
            if (!HasActiveProtocolFaultTrace || !_activeShip.FlaggedInfractions.Contains(ShipInfractionType.TransponderIdMismatch))
                throw new InvalidOperationException("First Protocol Review mismatch failed to activate crimson trace & record FlaggedInfractions.");

            // Advance until confrontation completes & trace auto-clears (~2.5s after confrontation)
            for (int step = 0; step < 320 && (_commsQueue.HasPendingOrActive || _faultStage != FaultSequenceStage.None); step++)
            {
                Update(0.05f, idle);
            }
            for (int step = 0; step < 65 && HasActiveProtocolFaultTrace; step++)
            {
                Update(0.05f, idle);
            }
            if (HasActiveProtocolFaultTrace)
                throw new InvalidOperationException("Crimson Protocol Review trace remained stuck after ~2.4s instead of auto-clearing.");

            // Secondary check on same already-flagged violation -> MISMATCH ALREADY FOUND without locking
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlatePrefix, ReviewTarget.VisualSignalTransponderId);
            if (!string.Equals(_reviewStatusNote, "MISMATCH ALREADY FOUND", StringComparison.Ordinal) ||
                _duplicateFaultNoteTimer <= 0f ||
                _faultStage != FaultSequenceStage.None)
            {
                throw new InvalidOperationException($"Duplicate mismatch check failed: note='{_reviewStatusNote}', stage={_faultStage}.");
            }

            // Second distinct violation on same ship (DataSlateOrg vs ReferenceManualSpeciesEntry) still works
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlateOrg, ReviewTarget.ReferenceManualSpeciesEntry);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.PrefixOrganizationMismatch) || _faultStage == FaultSequenceStage.None)
                throw new InvalidOperationException("Second distinct violation on multi-infraction ship was blocked after first fault.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // 4. Brengop Reference Manual tab stagger: Days 8-9 == 2 tabs (no HULLS), Day 10+ == 3 tabs + Species/Hull hitboxes
            _gameState.AdvanceToDay(config.DayUnlockManualPart1); // Day 8
            _gameState.Day8NixaHailCompleted = false;
            screenManager.SwitchTo(ScreenId.Desk);
            if (!IsPreShiftHailActive || !IsDay8NixaHailActive ||
                !_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("Registry Tablet", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Day 8 Desk entry did not trigger verbatim Regeer Nixa hail.");
            }
            SmokeCompletePreShiftHail();
            if (GetManualTabCountForCurrentDay() != 2)
                throw new InvalidOperationException($"Expected 2 Manual tabs on Day 8, got {GetManualTabCountForCurrentDay()}.");

            _gameState.AdvanceToDay(9);
            if (GetManualTabCountForCurrentDay() != 2)
                throw new InvalidOperationException($"Expected 2 Manual tabs on Day 9, got {GetManualTabCountForCurrentDay()}.");

            // Species check on Day 8 using ReferenceManualSpeciesEntry
            SmokeStageShipLoadedAndInspecting();
            if (_activeShip == null)
                throw new InvalidOperationException("Failed to stage Day 9 ship.");
            _activeShip.FlaggedInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.PrefixSpeciesMismatch });
            _activeShip.Slate.DeclaredPrefix = "ZNI-";
            _activeShip.Slate.DeclaredFactionName = "ZENITH INTERSTELLAR";
            _activeShip.Slate.DeclaredCaptainSpecies = PortraitSpecies.Brengop; // Forbidden on ZNI-
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlateSpecies, ReviewTarget.ReferenceManualSpeciesEntry);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.PrefixSpeciesMismatch))
                throw new InvalidOperationException("ReferenceManualSpeciesEntry failed to verify PrefixSpeciesMismatch.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // Day 10 unlocks tab 3 (HULLS) + ReferenceManualHullEntry verification
            _gameState.AdvanceToDay(config.DayUnlockManualHulls);
            if (GetManualTabCountForCurrentDay() != 3)
                throw new InvalidOperationException($"Expected 3 Manual tabs on Day 10, got {GetManualTabCountForCurrentDay()}.");
            SmokeSetManualTab(2);
            _activeShip.FlaggedInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.PrefixHullSilhouetteMismatch });
            _activeShip.GroundTruth.IsClassSilhouetteMismatch = true;
            _activeShip.Slate.DeclaredShipClass = ShipClass.Civilian;
            _activeShip.GroundTruth.ActualShipClass = ShipClass.Cargo;
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlateClass, ReviewTarget.ReferenceManualHullEntry);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.PrefixHullSilhouetteMismatch))
                throw new InvalidOperationException("ReferenceManualHullEntry failed to verify PrefixHullSilhouetteMismatch on Day 10.");

            // 5. Day 6 Silent Blank Data Slate Capture & Re-Transmit (no red PORT ERROR / silent mode active)
            _gameState.AdvanceToDay(6);
            screenManager.SwitchTo(ScreenId.Desk);
            SmokeStageBlankSlateDownloading(progressFraction: 0.45f);
            if (!IsSilentBlankSlateCaptureActive)
                throw new InvalidOperationException("Blank Data Slate download did not activate silent capture mode.");
            AbortBlankSlateDownloadMidStream();
            if (!IsSilentBlankSlateCaptureActive ||
                _commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("[PORT ERROR]", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Day 6 Blank Slate unplug triggered forbidden crimson [PORT ERROR] banner.");
            }

            // Re-plug Blank Slate & verify silent Re-Transmit + completion
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
            _contrabandSlateInSlateBus = true;
            TryTriggerReTransmit();
            if (_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("Link dropped.", StringComparison.Ordinal)))
                throw new InvalidOperationException("Silent Blank Slate [RE-TRANSMIT] emitted annoyed link-dropped dialogue.");
            BeginBlankSlateInterceptDownload(triggeredByReTransmit: true, enqueueDialogue: false);
            _protocolDownloadElapsed = ProtocolDownloadDurationSeconds;
            CompleteSubspaceProtocolsDownload();
            if (!IsSilentBlankSlateCaptureActive || _activeDeskContrabandItem?.ItemType != ContrabandPocketItem.TypeCopiedDataSlate)
                throw new InvalidOperationException("Silent Blank Slate intercept did not finish cleanly to TypeCopiedDataSlate.");

            // Draw a full Desk frame with Clock-Out Leave prompt string verified
            SmokeStageClockOutQuietLeavePrompt();
            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();
            if (!string.Equals(ClockOutLeavePromptText, "Shift complete. Click to walk back to quarters.", StringComparison.Ordinal))
                throw new InvalidOperationException($"ClockOutLeavePromptText mismatch: '{ClockOutLeavePromptText}'.");
        }
        finally
        {
            OnExit();
            _gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    internal void SmokeVerifyBuild475DeskFeatures(VirtualCanvas canvas, ScreenManager screenManager, Core.ConfigFlags config)
    {
        bool prevSuppress = _gameState.SuppressDiskPersistence;
        _gameState.SuppressDiskPersistence = true;

        try
        {
            // [A1] Blank Data Slate mid-stream abort after inspection / clearance restores prior EncounterPhase cleanly
            _gameState.AdvanceToDay(6);
            screenManager.SwitchTo(ScreenId.Desk);
            SmokeStageBlankSlateDownloading(progressFraction: 0.45f);
            _slateTelemetry = DataSlateTelemetryState.Loaded;
            AbortBlankSlateDownloadMidStream();
            if (_phase != EncounterPhase.InspectingSlateAndShip)
                throw new InvalidOperationException($"[A1] Expected InspectingSlateAndShip after Loaded abort, got {_phase}.");
            if (!IsShipInspectableForReview || !IsProtocolReviewUnlocked)
                throw new InvalidOperationException("[A1] Protocol Review became disabled after post-inspection Blank Slate abort.");

            // Click PROTOCOL REVIEW button directly to verify responsiveness
            DeskRect reviewBtn = _layout.ProtocolReviewBtn;
            VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            Update(0.016f, new VirtualMouseState
            {
                X = reviewBtn.X + reviewBtn.W / 2,
                Y = reviewBtn.Y + reviewBtn.H / 2,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (!_protocolReviewModeActive)
                throw new InvalidOperationException("[A1] Clicking PROTOCOL REVIEW after Blank Slate abort did not enter Protocol Review.");
            _protocolReviewModeActive = false;

            // Post-clearance abort restores AwaitingClearanceReturnToTopPort
            SmokeStageBlankSlateDownloading(progressFraction: 0.45f);
            _slateTelemetry = DataSlateTelemetryState.ClearedReadyToTransmit;
            AbortBlankSlateDownloadMidStream();
            if (_phase != EncounterPhase.AwaitingClearanceReturnToTopPort)
                throw new InvalidOperationException($"[A1] Expected AwaitingClearanceReturnToTopPort after Cleared abort, got {_phase}.");

            // [A2] Auto-SPS keeps weapons hot and _pendingWeaponsOrder == false while "Power down weapons." is queued
            SmokeStageHonestArmedShipWithSps(applyWeaponsPowerDown: false, spoofed: false);
            if (!_autoSpsWeaponsOrderTriggered || _pendingWeaponsOrder || _activeShip == null || !_activeShip.GroundTruth.ActualWeaponsHot)
                throw new InvalidOperationException("[A2] Auto-SPS armed _pendingWeaponsOrder prematurely while Officer line was still queued.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: true);
            if (!_pendingWeaponsOrder)
                throw new InvalidOperationException("[A2] Auto-SPS did not arm _pendingWeaponsOrder when 'Power down weapons.' completed.");
            _complianceTimer = 0f;
            ResolvePendingComplianceOrders();
            if (_activeShip.GroundTruth.ActualWeaponsHot)
                throw new InvalidOperationException("[A2] Honest ship failed to cool down weapons after 'Power down weapons.' completed.");

            // [B2] Calm [RE-TRANSMIT] emits Officer line without duplicating Captain's SubspaceUploadLine
            SmokeStageBlankSlateDownloading(progressFraction: 0.45f);
            _slateTelemetry = DataSlateTelemetryState.Empty;
            AbortBlankSlateDownloadMidStream();
            _commsQueue.Clear();
            _deskItems[DeskItemId.ContrabandSlate].IsVisible = true;
            _contrabandSlateInSlateBus = true;
            TryTriggerReTransmit();
            string subLine = _activeShip!.SubspaceUploadLine;
            int prematureCaptCount = _commsQueue.EnumerateAllTextIncludingPending()
                .Count(t => t.Contains(subLine, StringComparison.Ordinal));
            if (prematureCaptCount != 0)
                throw new InvalidOperationException($"[B2] TryTriggerReTransmit duplicated/prematurely enqueued Captain SubspaceUploadLine ({prematureCaptCount}).");

            // [B5] Anchor suppression resets cleanly across Clear() and SnapActiveAndDropPendingQueue() even in pre-delay
            _commsQueue.Clear();
            AudioHooks.OnCommsLineStart(isOfficer: true, isCaptain: false, "Send subspace protocols.");
            if (!AudioHooks.IsAnchorBlipSuppressed)
                throw new InvalidOperationException("[B5] Expected IsAnchorBlipSuppressed == true on Officer anchor phrase.");
            _commsQueue.EnqueueSpokenLine("CAPT: \"Copy.\"", CommsSpeakerKind.Captain, CommsTextTone.AmberPrimary, preDelaySeconds: 0.50f);
            _commsQueue.SnapActiveAndDropPendingQueue();
            if (AudioHooks.IsAnchorBlipSuppressed)
                throw new InvalidOperationException("[B5] SnapActiveAndDropPendingQueue() leaked IsAnchorBlipSuppressed == true during pre-delay.");

            AudioHooks.OnCommsLineStart(isOfficer: true, isCaptain: false, "Send subspace protocols.");
            _commsQueue.Clear();
            if (AudioHooks.IsAnchorBlipSuppressed)
                throw new InvalidOperationException("[B5] CommsTypewriterQueue.Clear() leaked IsAnchorBlipSuppressed == true.");

            // [B1] Manual 3-tab width fit & FieldBooklet full-sentence wrap check under PixelOperatorMono-Bold at +3
            _gameState.AdvanceToDay(config.DayUnlockManualHulls);
            screenManager.SwitchTo(ScreenId.Desk);
            int prevOffset = FontManager.FontSizeOffset;
            string prevPreset = FontManager.ActivePresetName;
            try
            {
                if (FontManager.PresetNames.Contains("PixelOperatorMono-Bold"))
                    FontManager.SetActivePreset("PixelOperatorMono-Bold");
                FontManager.SetFontSizeOffset(FontManager.MaxFontSizeOffset);

                int wShip = UiText.MeasureWidth("SHIP", 10, denseRow: true);
                int wSpec = UiText.MeasureWidth("SPECIES", 10, denseRow: true);
                int wHulls = UiText.MeasureWidth("HULLS", 10, denseRow: true);
                int wCargo = UiText.MeasureWidth("CARGO", 10, denseRow: true);
                if (ManualW != 280 ||
                    wShip + 8 > 58 || wSpec + 8 > 80 || wHulls + 8 > 64 || wCargo + 8 > (ManualW - 216))
                {
                    throw new InvalidOperationException($"[B1] Manual 280px compact tab labels overflowed buttons: {wShip}, {wSpec}, {wHulls}, {wCargo}.");
                }

                List<FieldBookletPage> pages = FieldBookletWidget.GetUnlockedPages(_gameState, config);
                foreach (string checkTag in new[] { "day1_shift", "day2_sps" })
                {
                    FieldBookletPage pg = pages.First(p => p.Tag == checkTag);
                    List<string> wrapped = UiText.Wrap(pg.BodyText, 10, FieldBookletWidget.OpenW - 36, maxLines: 8, denseRow: true);
                    string rejoined = string.Join(" ", wrapped);
                    if (!rejoined.EndsWith(".", StringComparison.Ordinal) || rejoined.Length < pg.BodyText.Length - 2)
                        throw new InvalidOperationException($"[B1] FieldBooklet page '{checkTag}' truncated text at +3 offset: '{rejoined}'.");
                }

                // Render Desk frame to verify zero top-bar/manual/booklet crashes at +3 offset
                _fieldBooklet.IsOpen = true;
                canvas.BeginVirtualDraw();
                screenManager.DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
            }
            finally
            {
                FontManager.SetActivePreset(prevPreset);
                FontManager.SetFontSizeOffset(prevOffset);
            }
        }
        finally
        {
            OnExit();
            _gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    internal void SmokeVerifyBuild51DeskFeatures(VirtualCanvas canvas, ScreenManager screenManager, Core.ConfigFlags config)
    {
        bool prevSuppress = _gameState.SuppressDiskPersistence;
        DeskUpperLayoutMode savedLayoutMode = config.DeskUpperLayoutMode;
        WaferScoopMode savedScoopMode = config.WaferScoopMode;
        _gameState.SuppressDiskPersistence = true;
        VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };

        try
        {
            // 1. Days 1-11: One-way conduit stays completely dark/inert even during RX PACKET
            _gameState.ResetForNewSave(1);
            _gameState.AdvanceToDay(config.DayUnlockManualHulls); // Day 10 (< Day 12)
            _gameState.Day8NixaHailCompleted = true;
            screenManager.SwitchTo(ScreenId.Desk);
            SmokeStageShipLoadedAndInspecting();
            _phase = EncounterPhase.DownloadingProtocols;
            _isDownloadingToBlankSlate = false;
            if (IsWaferWriterOperational || IsConduitPulseRxActive)
                throw new InvalidOperationException("Days 1-11 Wafer Writer / conduit must stay completely dark and inert.");

            // 2. Day 4+ Clickable Comms Transcript Statement (ReviewTarget.CommsTranscriptStatement) + Spoken Route Discrepancy
            _gameState.AdvanceToDay(config.DayUnlockWarpScanner);
            _gameState.Day4PreShiftHailCompleted = true;
            screenManager.SwitchTo(ScreenId.Desk);
            SmokeStageShipLoadedAndInspecting();
            if (_activeShip == null)
                throw new InvalidOperationException("Failed to stage Day 4 ship for spoken route discrepancy test.");

            _activeShip.FlaggedInfractions.Clear();
            _activeShip.ProtocolReviewVerified = false;
            _activeShip.Slate.OriginSystem = "Veridia";
            _activeShip.GroundTruth.ActualOriginSystem = "Veridia";
            _activeShip.GroundTruth.SpokenOriginSystem = "Gharok";
            _activeShip.AutoInterrogationReplyLine = "Gharok to VG-3. Commercial trade. General crates.";
            _activeShip.ConfrontationGuiltyExcuseSpokenRoute = "Did I say Gharok? I meant Veridia!";
            _activeShip.GroundTruth.ActiveInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction
            {
                Type = ShipInfractionType.SpokenRouteDiscrepancy,
                Code = "PROTO-04S",
                CitationReason = "SPOKEN ORIGIN 'GHAROK' CONTRADICTED FILED ROUTE 'VERIDIA'"
            });

            _commsQueue.Clear();
            EnqueueCaptainLine(
                $"CAPT: \"{_activeShip.AutoInterrogationReplyLine}\"",
                CommsTextTone.AmberPrimary,
                suppressAutoReceivingLag: true,
                isCaptainStatementEvidence: true);
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);

            if (!TryGetVisibleTranscriptStatementRect(out Rectangle stmtRectD4) ||
                stmtRectD4.Width <= 0 || stmtRectD4.Height <= 0)
            {
                throw new InvalidOperationException("TryGetVisibleTranscriptStatementRect failed to locate Captain's spoken Auto-Interrogation line.");
            }

            _protocolReviewModeActive = true;
            Update(0.016f, new VirtualMouseState
            {
                X = (int)(stmtRectD4.X + 12),
                Y = (int)(stmtRectD4.Y + 6),
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (_reviewFirstSelection != ReviewTarget.CommsTranscriptStatement)
                throw new InvalidOperationException($"Clicking Captain transcript statement did not select CommsTranscriptStatement (got {_reviewFirstSelection}).");

            EvaluateProtocolReviewPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.DataSlateOrigin);
            if (!HasActiveProtocolFaultTrace || !_activeShip.FlaggedInfractions.Contains(ShipInfractionType.SpokenRouteDiscrepancy))
                throw new InvalidOperationException("Pairing CommsTranscriptStatement vs DataSlateOrigin failed to verify SpokenRouteDiscrepancy.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // 3. Day 12+ Entry: FieldBooklet "day12_cargo" auto-unfolds once without blocking gameplay, 4th Manual tab CARGO fits 280px
            _gameState.AdvanceToDay(config.DayUnlockWaferCargo);
            screenManager.SwitchTo(ScreenId.Desk);
            if (!_fieldBooklet.IsOpen || _fieldBooklet.CurrentPage(_gameState, config).Tag != "day12_cargo")
                throw new InvalidOperationException("Day 12 Field Booklet did not auto-unfold cleanly to day12_cargo.");
            if (GetManualTabCountForCurrentDay() != 4 || ManualW != 280)
                throw new InvalidOperationException($"Day 12 Manual expected 4 tabs inside 280px, got tabs={GetManualTabCountForCurrentDay()}, W={ManualW}.");

            // Check day12_cargo full-sentence wrap at +3 font offset
            int prevOff = FontManager.FontSizeOffset;
            try
            {
                FontManager.SetFontSizeOffset(FontManager.MaxFontSizeOffset);
                FieldBookletPage d12Page = FieldBookletWidget.GetUnlockedPages(_gameState, config).First(p => p.Tag == "day12_cargo");
                List<string> wrapped12 = UiText.Wrap(d12Page.BodyText, 10, FieldBookletWidget.OpenW - 36, maxLines: 8, denseRow: true);
                string joined12 = string.Join(" ", wrapped12);
                if (!joined12.EndsWith(".", StringComparison.Ordinal) || joined12.Length < d12Page.BodyText.Length - 2)
                    throw new InvalidOperationException($"FieldBooklet day12_cargo truncated at +3 offset: '{joined12}'.");
            }
            finally
            {
                FontManager.SetFontSizeOffset(prevOff);
            }

            // Unfolded Day 12 booklet must NOT block [NEXT HAIL]
            Update(0.016f, new VirtualMouseState
            {
                X = _layout.NextHailBtn.X + 24,
                Y = _layout.NextHailBtn.Y + 14,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (_activeShip == null)
                throw new InvalidOperationException("Day 12 unfolded Field Booklet blocked [NEXT HAIL].");

            // 4. Strictly One-Way Conduit on Day 12+: pulses ONLY on RX PACKET / missing-wafer demux, NEVER on Clearance or Detain
            _phase = EncounterPhase.DownloadingProtocols;
            _isDownloadingToBlankSlate = false;
            if (!IsConduitPulseRxActive)
                throw new InvalidOperationException("Day 12 conduit failed to pulse during incoming RX PACKET.");

            _phase = EncounterPhase.UploadingClearance;
            if (IsConduitPulseRxActive)
                throw new InvalidOperationException("Day 12 conduit pulsed illegally during Clearance Dock encryption.");

            _phase = EncounterPhase.TransmittingClearance;
            if (IsConduitPulseRxActive)
                throw new InvalidOperationException("Day 12 conduit pulsed illegally during Slate Bus TX CLEARANCE.");

            _phase = EncounterPhase.DetainClamping;
            if (IsConduitPulseRxActive)
                throw new InvalidOperationException("Day 12 conduit pulsed illegally during Detain clamping.");

            // 5. Left-Fan Ejection Trajectory across OptionA_Classic, OptionB1_CommsSwap, and OptionB2_CommsSwapFlush
            foreach (DeskUpperLayoutMode mode in Enum.GetValues<DeskUpperLayoutMode>())
            {
                DeskLayoutRects lay = DeskLayoutRects.Compute(mode, use3xPortraitInLayoutB: true);
                int fanRight = lay.WaferWriter.X - 16;
                for (int slot = 0; slot < MaxWaferHopperCount; slot++)
                {
                    var (fx, fy) = ComputeWaferLeftFanTarget(slot, lay);
                    DeskRect wRect = new(fx, fy, WaferCardW, WaferCardH);
                    DeskRect manualDefault = new(ManualDefaultX, ManualDefaultY, ManualW, ManualH);
                    DeskRect bookletClosed = new(8, 604, FieldBookletWidget.ClosedW, FieldBookletWidget.ClosedH);
                    DeskRect bookletOpen = new(8, 448, FieldBookletWidget.OpenW, FieldBookletWidget.OpenH);

                    if (wRect.Right > fanRight ||
                        wRect.Intersects(lay.SlateBusDockedSlate) ||
                        wRect.Intersects(lay.WaferWriter) ||
                        wRect.Intersects(manualDefault) ||
                        wRect.Intersects(bookletClosed) ||
                        wRect.Intersects(bookletOpen))
                    {
                        throw new InvalidOperationException(
                            $"Cert-Wafer fan slot {slot} ({wRect}) overlapped forbidden desk item in layout {mode}.");
                    }
                }
            }

            // 6. 3-Wafer Hopper Lifecycle: Auto-demux, Banned Cargo vs Manual Tab 3 CARGO, Spoken Cargo Discrepancy, Detain preserving wafers
            config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            SyncLayoutFromConfig();
            OnEnter();
            _fieldBooklet.IsOpen = false;
            SyncFieldBookletItemSize();

            SmokeStageShipLoadedAndInspecting();
            if (_activeShip == null)
                throw new InvalidOperationException("Failed to stage Day 12 ship.");

            _waferHopperCount = MaxWaferHopperCount;
            _deskWafers.Clear();
            _cargoWaferPrintedForActiveShip = false;
            _activeShip.GroundTruth.ExpectsCargoManifestWafer = true;
            _activeShip.GroundTruth.CargoManifestInitiallyOmitted = false;
            _activeShip.GroundTruth.CargoManifestTransmitted = true;
            _activeShip.GroundTruth.WaferCargoManifestItem = "Ashen Spores";
            _activeShip.Slate.CargoManifest = "ASHEN SPORES";
            _activeShip.GroundTruth.SpokenCargoItem = "Hydro-Grain";
            _activeShip.AutoInterrogationReplyLine = "Veridia to VG-3. Commercial trade. Hydro-Grain.";
            _activeShip.ConfrontationGuiltyExcuseBannedCargo = "Broker swore Ashen Spores had an exemption!";
            _activeShip.ConfrontationGuiltyExcuseSpokenCargo = "Quartermaster swapped our crates!";
            _activeShip.FlaggedInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Clear();
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.BannedCargoItem });
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.SpokenCargoDiscrepancy });

            CompleteSubspaceProtocolsDownload();
            Update(0.50f, idle); // Finish wafer ejection slide
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);

            if (ActiveDeskWaferCount != 1 || WaferHopperCount != 2)
                throw new InvalidOperationException($"Expected 1 printed Cert-Wafer and 2 blanks in hopper, got desk={ActiveDeskWaferCount}, hopper={WaferHopperCount}.");

            // Pair CargoManifestWafer vs Manual CARGO tab (ReferenceManualCargoBannedEntry)
            _manualActiveTab = 3;
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.CargoManifestWafer, ReviewTarget.ReferenceManualCargoBannedEntry);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.BannedCargoItem))
                throw new InvalidOperationException("Pairing CargoManifestWafer vs ReferenceManualCargoBannedEntry failed to verify BannedCargoItem.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // Pair CommsTranscriptStatement vs CargoManifestWafer on Day 12
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.CommsTranscriptStatement, ReviewTarget.CargoManifestWafer);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.SpokenCargoDiscrepancy))
                throw new InvalidOperationException("Pairing CommsTranscriptStatement vs CargoManifestWafer failed to verify SpokenCargoDiscrepancy.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // Pulling Detain Lever wipes Data Slate but preserves printed Cert-Wafers on desk!
            TriggerDetainLeverClampSequence();
            for (int step = 0; step < 30 && _phase == EncounterPhase.DetainClamping; step++)
            {
                Update(0.15f, idle);
            }
            if (_slateTelemetry != DataSlateTelemetryState.Empty || ActiveDeskWaferCount != 1 || WaferHopperCount != 2)
            {
                throw new InvalidOperationException(
                    $"Detain must wipe Data Slate while preserving Cert-Wafers on desk (slate={_slateTelemetry}, wafers={ActiveDeskWaferCount}, hopper={WaferHopperCount}).");
            }

            // 7. Missing / Withheld Cargo Manifest Rule (Honest transmit onto blank wafer vs. Dishonest fault)
            StartNextShipEncounter(DebugSpawnValidityMode.ForceValid, reseedNonce: 5101u);
            _shipApproachElapsed = ShipApproachDurationSeconds;
            _phase = EncounterPhase.InspectingSlateAndShip;
            _slateTelemetry = DataSlateTelemetryState.Loaded;
            _activeShip!.GroundTruth.ExpectsCargoManifestWafer = true;
            _activeShip.GroundTruth.CargoManifestInitiallyOmitted = true;
            _activeShip.GroundTruth.OmittedManifestIsDishonest = false;
            _activeShip.GroundTruth.CargoManifestTransmitted = false;
            _activeShip.GroundTruth.WaferCargoManifestItem = "Hydro-Grain";

            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.WaferWriterModule, ReviewTarget.DataSlateCargo);
            for (int step = 0; step < 20 && _isTransmittingMissingWafer; step++)
            {
                Update(0.10f, idle);
            }
            Update(0.40f, idle);
            if (!_activeShip.GroundTruth.CargoManifestTransmitted || ActiveDeskWaferCount != 2 || WaferHopperCount != 1 || _activeShip.ProtocolReviewVerified)
            {
                throw new InvalidOperationException(
                    $"Honest omitted Cargo Manifest interrogation failed to transmit wafer cleanly (tx={_activeShip.GroundTruth.CargoManifestTransmitted}, wafers={ActiveDeskWaferCount}, hopper={WaferHopperCount}).");
            }

            // Dishonest omitted manifest -> triggers WithheldIllicitCargoManifest fault!
            _activeShip.GroundTruth.CargoManifestInitiallyOmitted = true;
            _activeShip.GroundTruth.OmittedManifestIsDishonest = true;
            _activeShip.GroundTruth.CargoManifestTransmitted = false;
            _activeShip.GroundTruth.ActiveInfractions.Add(new ShipInfraction { Type = ShipInfractionType.WithheldIllicitCargoManifest });
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.WaferWriterModule, ReviewTarget.DataSlateCargo);
            if (!_activeShip.FlaggedInfractions.Contains(ShipInfractionType.WithheldIllicitCargoManifest) || !_activeShip.ProtocolReviewVerified)
                throw new InvalidOperationException("Dishonest omitted Cargo Manifest failed to trigger WithheldIllicitCargoManifest fault.");
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // 8. Empty Hopper Hold, Reminder Line & Top-Slot Wipe Auto-Recovery + All 3 WaferScoopMode Options
            // Consume last blank wafer (hopper -> 0, desk -> 3)
            QueueOrEjectCargoManifestWafer(_activeShip);
            Update(0.40f, idle);
            if (WaferHopperCount != 0 || ActiveDeskWaferCount != 3)
                throw new InvalidOperationException($"Expected empty hopper (0/3) and 3 wafers on desk, got hopper={WaferHopperCount}, desk={ActiveDeskWaferCount}.");

            // Next print with 0 blanks must hold and log the reminder!
            _commsQueue.Clear();
            QueueOrEjectCargoManifestWafer(_activeShip);
            if (!HasHeldPendingWafer ||
                !_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("we only gave you enough for what you need", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Empty hopper failed to hold print or emit reminder line.");
            }

            // Test WaferScoopMode.PostVerdictAutoScoop pre-verdict (no auto-scoop during active inspection) vs post-verdict stack scoop
            config.WaferScoopMode = WaferScoopMode.PostVerdictAutoScoop;
            _phase = EncounterPhase.InspectingSlateAndShip;
            _detainLeverPulled = false;
            if (IsEncounterPostVerdictForWaferScoop)
                throw new InvalidOperationException("PostVerdictAutoScoop reported post-verdict while InspectingSlateAndShip.");

            _phase = EncounterPhase.UploadingClearance;
            if (!IsEncounterPostVerdictForWaferScoop)
                throw new InvalidOperationException("PostVerdictAutoScoop did not unlock post-verdict scooping on UploadingClearance.");

            // Scoop 2 wafers and wipe in top slot -> emits wafer_wipe, refills blanks, and auto-prints the held wafer!
            AudioHooks.ClearEventHistory();
            List<int> twoToWipe = _deskWafers.Take(2).Select(w => w.InstanceId).ToList();
            int wipedCount = WipeWafersInFeedSlot(twoToWipe);
            Update(0.40f, idle);
            if (wipedCount != 2 ||
                !AudioHooks.LoggedEvents.Contains("wafer_wipe") ||
                HasHeldPendingWafer ||
                WaferHopperCount != 1 ||
                ActiveDeskWaferCount != 2)
            {
                throw new InvalidOperationException(
                    $"Top-slot stack wipe + held-wafer auto-print failed (wiped={wipedCount}, held={HasHeldPendingWafer}, hopper={WaferHopperCount}, desk={ActiveDeskWaferCount}).");
            }

            // Exercise CenterSwipeScoop & ModifierScoop modes + full Desk render with wafers on desk
            foreach (WaferScoopMode sm in new[] { WaferScoopMode.CenterSwipeScoop, WaferScoopMode.ModifierScoop, WaferScoopMode.PostVerdictAutoScoop })
            {
                config.WaferScoopMode = sm;
                _draggedWaferIds.Clear();
                if (_deskWafers.Count >= 2)
                {
                    CertWaferCard lead = _deskWafers[0];
                    CertWaferCard second = _deskWafers[1];
                    lead.X = second.X;
                    lead.Y = second.Y;
                    _draggedWaferIds.Add(lead.InstanceId);
                    TryScoopCrossedWafersIntoStack(lead, new VirtualMouseState
                    {
                        X = second.X + WaferCardW / 2,
                        Y = second.Y + WaferCardH / 2,
                        IsInsideViewport = true,
                        Scale = 1.0f
                    });
                    if (_draggedWaferIds.Count < 2)
                        throw new InvalidOperationException($"WaferScoopMode {sm} failed to scoop crossed wafer.");
                    _draggedWaferIds.Clear();
                }
            }

            // 9. SPS Violet [!] POWERED CARGO lock on Day 12+
            if (!SmokeStageAndLockV2CargoPower(out _, out _))
                throw new InvalidOperationException("Day 12 SPS Violet Cargo lock failed.");

            canvas.BeginVirtualDraw();
            screenManager.DrawVirtual(idle);
            canvas.EndVirtualDrawAndBlit();
        }
        finally
        {
            config.DeskUpperLayoutMode = savedLayoutMode;
            config.WaferScoopMode = savedScoopMode;
            SyncLayoutFromConfig();
            OnExit();
            _gameState.SuppressDiskPersistence = prevSuppress;
        }
    }

    internal void SmokeVerifyBuild52DeskFeatures(
        VirtualCanvas canvas,
        ScreenManager screenManager,
        PauseMenuOverlay pauseMenu,
        Core.ConfigFlags config,
        DevDebugState devDebug)
    {
        bool prevSuppress = _gameState.SuppressDiskPersistence;
        DeskUpperLayoutMode savedLayoutMode = config.DeskUpperLayoutMode;
        bool savedEnableStoker = config.EnableStokerDial;
        float savedSafeCap = config.StokerSafeLoadCap;
        float savedShock = config.StokerUncalibratedShock;
        DebugBrownoutForceMode savedBrownoutForce = devDebug.BrownoutForceMode;
        _gameState.SuppressDiskPersistence = true;

        try
        {
            VirtualMouseState idle = new() { X = 310, Y = 165, IsInsideViewport = true, Scale = 1.0f };
            config.DeskUpperLayoutMode = DeskUpperLayoutMode.OptionB2_CommsSwapFlush;
            config.EnableStokerDial = true;
            config.StokerSafeLoadCap = 65.0f;
            config.StokerUncalibratedShock = 33.0f;
            devDebug.BrownoutForceMode = DebugBrownoutForceMode.Auto;
            SyncLayoutFromConfig();

            // =================================================================
            // 0. Build 5.1 Quick Polish: Premature Wafer Wipe [RE-TRANSMIT] Recovery
            // =================================================================
            _gameState.ResetForNewSave(1);
            _gameState.AdvanceToDay(config.DayUnlockWaferCargo);
            screenManager.SwitchTo(ScreenId.Desk);
            _fieldBooklet.IsOpen = false;
            SyncFieldBookletItemSize();

            SmokeStageShipLoadedAndInspecting();
            if (_activeShip == null)
                throw new InvalidOperationException("Failed to stage Day 12 ship for premature wafer wipe test.");

            _waferHopperCount = MaxWaferHopperCount;
            _deskWafers.Clear();
            _demuxedWafersForActiveShip.Clear();
            _prematurelyWipedWafersForActiveShip.Clear();
            _cargoWaferPrintedForActiveShip = false;
            _activeShip.GroundTruth.ExpectsCargoManifestWafer = true;
            _activeShip.GroundTruth.CargoManifestInitiallyOmitted = false;
            _activeShip.GroundTruth.CargoManifestTransmitted = true;
            _activeShip.GroundTruth.WaferCargoManifestItem = "Hydro-Grain";
            _activeShip.Slate.CargoManifest = "HYDRO-GRAIN";

            CompleteSubspaceProtocolsDownload();
            Update(0.45f, idle);
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);

            if (ActiveDeskWaferCount != 1 || WaferHopperCount != 2 || CanReTransmitPrematurelyWipedWafers)
            {
                throw new InvalidOperationException(
                    $"Expected 1 demuxed Cargo Manifest wafer and [RE-TRANSMIT] inactive before wipe (desk={ActiveDeskWaferCount}, hopper={WaferHopperCount}).");
            }

            // Manually wipe the active ship's Cargo Manifest mid-inspection -> enables calm [RE-TRANSMIT]!
            int wipedId = _deskWafers[0].InstanceId;
            WipeWafersInFeedSlot([wipedId]);
            if (ActiveDeskWaferCount != 0 ||
                WaferHopperCount != 3 ||
                PrematurelyWipedWaferCount != 1 ||
                !CanReTransmitPrematurelyWipedWafers)
            {
                throw new InvalidOperationException(
                    $"Wiping Day 12 Cargo Manifest mid-inspection did not enable [RE-TRANSMIT] (wipedSet={PrematurelyWipedWaferCount}, canReTx={CanReTransmitPrematurelyWipedWafers}).");
            }

            // Click [RE-TRANSMIT] on Command Strip -> enqueues micro-lines, pulses one-way RX conduit, re-demuxes ONLY wiped wafer without duplicating Data Slate!
            _commsQueue.Clear();
            Update(0.016f, new VirtualMouseState
            {
                X = _layout.NextHailBtn.X + 24,
                Y = _layout.NextHailBtn.Y + 12,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });

            if (!_isReTransmittingWipedWafers || !IsConduitPulseRxActive || _slateTelemetry != DataSlateTelemetryState.Loaded)
            {
                throw new InvalidOperationException("Clicking premature-wafer [RE-TRANSMIT] did not pulse RX conduit or disturbed loaded Data Slate.");
            }
            if (!_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("Re-transmit auxiliary certificates.", StringComparison.Ordinal)) ||
                !_commsQueue.EnumerateAllTextIncludingPending().Any(t => t.Contains("Already sent them. Sending again.", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Clicking premature-wafer [RE-TRANSMIT] did not enqueue officer & captain micro-lines.");
            }

            for (int step = 0; step < 20 && _isReTransmittingWipedWafers; step++)
            {
                Update(0.10f, idle);
            }
            Update(0.45f, idle);
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);

            if (ActiveDeskWaferCount != 1 ||
                WaferHopperCount != 2 ||
                PrematurelyWipedWaferCount != 0 ||
                CanReTransmitPrematurelyWipedWafers ||
                _slateTelemetry != DataSlateTelemetryState.Loaded)
            {
                throw new InvalidOperationException(
                    $"Premature-wafer [RE-TRANSMIT] failed to re-demux cleanly (desk={ActiveDeskWaferCount}, hopper={WaferHopperCount}, remSet={PrematurelyWipedWaferCount}).");
            }

            // Verify withheld/omitted packet rule is NEVER bypassed when wiping a previous ship's leftover wafer
            CertWaferCard leftoverWafer = _deskWafers[0];
            StartNextShipEncounter(DebugSpawnValidityMode.ForceInvalid, reseedNonce: 5201u);
            _shipApproachElapsed = ShipApproachDurationSeconds;
            _phase = EncounterPhase.InspectingSlateAndShip;
            _slateTelemetry = DataSlateTelemetryState.Loaded;
            _activeShip!.GroundTruth.ExpectsCargoManifestWafer = true;
            _activeShip.GroundTruth.CargoManifestInitiallyOmitted = true;
            _activeShip.GroundTruth.OmittedManifestIsDishonest = true;
            _activeShip.GroundTruth.CargoManifestTransmitted = false;
            WipeWafersInFeedSlot([leftoverWafer.InstanceId]);
            if (CanReTransmitPrematurelyWipedWafers || PrematurelyWipedWaferCount != 0)
            {
                throw new InvalidOperationException("Wiping a previous ship's wafer bypassed withheld-packet logic on a ship that never transmitted a Cargo Manifest!");
            }

            // =================================================================
            // 1. Day 15 Brengop Foreman Hail, "day15_stoker" Booklet, 2-Line LED Fit, [ANCHOR] + Detain Preview Reset, & Overload
            // =================================================================
            _gameState.AdvanceToDay(config.DayUnlockStoker);
            _gameState.Day15ForemanHailCompleted = false;
            screenManager.SwitchTo(ScreenId.Desk);

            if (!IsDay15ForemanHailActive || !IsPreShiftHailActive || _activeShip != null || !HasPreShiftPortraitTextureLoaded)
            {
                throw new InvalidOperationException("Day 15 Desk entry did not trigger Brengop Logistics Foreman Live Viewscreen Hail.");
            }
            if (MathF.Abs(StokerSafeLoad - config.StokerIdleBaseline) > 0.01f || StokerShock > 0.01f)
            {
                throw new InvalidOperationException($"Day 15 Stoker did not start at 15% idle baseline (got {StokerSafeLoad}%).");
            }

            SmokeCompletePreShiftHail();
            if (IsDay15ForemanHailActive || HasPreShiftPortraitTextureLoaded || !_gameState.Day15ForemanHailCompleted)
            {
                throw new InvalidOperationException("Closing Day 15 Foreman hail did not unload pre-shift portrait texture or mark completion.");
            }
            if (!_fieldBooklet.IsOpen || _fieldBooklet.CurrentPage(_gameState, config).Tag != "day15_stoker")
            {
                throw new InvalidOperationException("Day 15 Field Booklet did not auto-unfold to day15_stoker after Foreman hail.");
            }

            // Verify 2-Line Stoker LED & day15_stoker booklet page never clip/overflow at MaxFontSizeOffset (+3) under PixelOperatorMono-Bold
            int prevFontOff = FontManager.FontSizeOffset;
            try
            {
                FontManager.SetFontSizeOffset(FontManager.MaxFontSizeOffset);
                int maxLedTextW = StokerLedW - 6; // 82px inside 96px bezel
                string ledTop = UiText.TruncateToWidth("MEASURED", 10, maxLedTextW, denseRow: true);
                string ledBot = UiText.TruncateToWidth("9,850 T", 10, maxLedTextW, denseRow: true);
                string ancTxt = UiText.TruncateToWidth("[ANCHOR]", 10, StokerAnchorBtnW - 6, denseRow: true);
                if (ledTop != "MEASURED" || ledBot != "9,850 T" || ancTxt != "[ANCHOR]" ||
                    UiText.MeasureWidth(ledTop, 10, denseRow: true) > maxLedTextW ||
                    UiText.MeasureWidth(ledBot, 10, denseRow: true) > maxLedTextW)
                {
                    throw new InvalidOperationException($"Stoker 2-line LED or [ANCHOR] clipped inside 96px bezel at +3 offset: '{ledTop}' / '{ledBot}' / '{ancTxt}'.");
                }

                FieldBookletPage d15Page = FieldBookletWidget.GetUnlockedPages(_gameState, config).First(p => p.Tag == "day15_stoker");
                List<string> wrapped15 = UiText.Wrap(d15Page.BodyText, 10, FieldBookletWidget.OpenW - 36, maxLines: 8, denseRow: true);
                string joined15 = string.Join(" ", wrapped15);
                if (!joined15.EndsWith(".", StringComparison.Ordinal) || joined15.Length < d15Page.BodyText.Length - 2)
                {
                    throw new InvalidOperationException($"FieldBooklet day15_stoker truncated at +3 offset: '{joined15}'.");
                }
            }
            finally
            {
                FontManager.SetFontSizeOffset(prevFontOff);
            }

            _fieldBooklet.IsOpen = false;
            SyncFieldBookletItemSize();

            // Stage overweight uncalibrated ship -> click [ANCHOR] -> verify +33% preview jump -> Protocol Review -> Detain drops preview back to 0%!
            SmokeStageShipLoadedAndInspecting();
            _activeShip!.GroundTruth.ActualShieldsUp = false;
            _activeShip.Slate.DeclaredTonnage = 1400;
            _activeShip.GroundTruth.ActualMass = 2280;
            _activeShip.GroundTruth.CargoManifestAccountedExtraMass = 0;
            _activeShip.FlaggedInfractions.Clear();
            _activeShip.ProtocolReviewVerified = false;
            _activeShip.GroundTruth.ActiveInfractions =
            [
                new ShipInfraction { Type = ShipInfractionType.StokerMassMismatch, Code = "PROTO-15M", CitationReason = "UNDECLARED OVERWEIGHT MASS" }
            ];

            Update(0.016f, new VirtualMouseState
            {
                X = StokerAnchorBtnX + 20,
                Y = StokerAnchorBtnY + 8,
                IsInsideViewport = true,
                LeftPressed = true,
                Scale = 1.0f
            });
            if (!IsStokerMassAnchoredForShip ||
                StokerMeasuredMass != 2280 ||
                MathF.Abs(StokerPreviewShock - 33.0f) > 0.01f ||
                MathF.Abs(StokerTotalStrain - 48.0f) > 0.05f)
            {
                throw new InvalidOperationException(
                    $"Clicking [ANCHOR] on overweight ship failed to show 2,280 T and +33% preview jump (measured={StokerMeasuredMass}, preview={StokerPreviewShock}).");
            }

            // Protocol Review pairing: DataSlateMass vs StokerMeasuredMassLed
            _protocolReviewModeActive = true;
            EvaluateProtocolReviewPair(ReviewTarget.DataSlateMass, ReviewTarget.StokerMeasuredMassLed);
            if (!_activeShip.ProtocolReviewVerified || !_activeShip.FlaggedInfractions.Contains(ShipInfractionType.StokerMassMismatch))
            {
                throw new InvalidOperationException("Pairing DataSlateMass vs StokerMeasuredMassLed failed to verify StokerMassMismatch.");
            }
            _commsQueue.FlushAllImmediately(invokeCallbacks: false);
            _faultStage = FaultSequenceStage.None;
            _faultTraceTimer = 0f;

            // Pull Detain Lever -> live +33% preview needle jump drops back cleanly with 0% shock added!
            TriggerDetainLeverClampSequence();
            if (StokerPreviewShock > 0.001f || StokerShock > 0.001f)
            {
                throw new InvalidOperationException($"Pulling Detain Lever did not drop [ANCHOR] preview spike cleanly (preview={StokerPreviewShock}, shock={StokerShock}).");
            }
            for (int step = 0; step < 30 && _phase == EncounterPhase.DetainClamping; step++)
            {
                Update(0.15f, idle);
            }

            // Waving 1 uncalibrated ship through stays <= 98% without overloading; waving a 2nd crosses > 100% with +0 Security Strikes!
            int strikesBeforeOverload = _gameState.SecurityStrikes;
            for (int slip = 1; slip <= 2; slip++)
            {
                SmokeStageShipLoadedAndInspecting();
                _activeShip!.GroundTruth.ActualShieldsUp = false;
                _activeShip.GroundTruth.ActualWeaponsHot = false;
                _activeShip.GroundTruth.HasSpoofedWeapons = false;
                _activeShip.GroundTruth.ActualCargoPowerDraw = false;
                _activeShip.GroundTruth.CargoManifestInitiallyOmitted = false;
                _activeShip.Slate.DeclaredTonnage = 1200;
                _activeShip.GroundTruth.ActualMass = 2100;
                _activeShip.GroundTruth.CargoManifestAccountedExtraMass = 0;
                _activeShip.GroundTruth.ActiveInfractions =
                [
                    new ShipInfraction { Type = ShipInfractionType.StokerMassMismatch, Code = "PROTO-15M", CitationReason = "UNDECLARED OVERWEIGHT MASS" }
                ];
                _pendingDecision = PlayerDecision.GrantAccess;
                FinalizeShipResolution();

                if (slip == 1 && (_gameState.StokerOverloadedToday || StokerTotalStrain > 98.01f))
                {
                    throw new InvalidOperationException($"1st uncalibrated ship should cap <= 98% without overloading (strain={StokerTotalStrain}%).");
                }
                if (slip == 2 && (!_gameState.StokerOverloadedToday || StokerTotalStrain <= 100.0f))
                {
                    throw new InvalidOperationException($"2nd uncalibrated ship failed to overload Stellar Stoker > 100% (strain={StokerTotalStrain}%).");
                }
            }
            if (_gameState.SecurityStrikes != strikesBeforeOverload ||
                !_gameState.PendingStokerBerthSearch ||
                !_gameState.NextDayPowerBrownout ||
                !_gameState.LastShiftStokerOverload)
            {
                throw new InvalidOperationException("Stoker overload illegally added Security Strikes or missed PendingStokerBerthSearch / NextDayPowerBrownout.");
            }

            // =================================================================
            // 2. 3-State Next-Day Booth 04 & Viewport Brownout Machine + GPU Pixel Proof
            // =================================================================
            _gameState.AdvanceToDay(config.DayUnlockStoker + 1);
            _gameState.NextDayPowerBrownout = true;
            screenManager.SwitchTo(ScreenId.Desk);
            _fieldBooklet.IsOpen = false;
            SyncFieldBookletItemSize();

            // Verify 6-step crisp OFF flicker sequence (ON -> OFF -> ON -> OFF -> ON -> OFF) + POWER LED sync via ForceOff
            devDebug.BrownoutForceMode = DebugBrownoutForceMode.ForceOff;
            Update(0.01f, idle); // t = 0.01s -> Step 0 (ON)
            float[] expectedLedSteps = [1.0f, 0.08f, 1.0f, 0.08f, 1.0f, 0.08f];
            for (int stepIdx = 0; stepIdx < 6; stepIdx++)
            {
                if (MathF.Abs(PowerIndicatorBrightness - expectedLedSteps[stepIdx]) > 0.02f)
                {
                    throw new InvalidOperationException(
                        $"OFF flicker step {stepIdx} expected POWER LED brightness {expectedLedSteps[stepIdx]}, got {PowerIndicatorBrightness}.");
                }
                if (stepIdx < 5)
                {
                    Update(BrownoutOffFlickerStepSeconds, idle);
                }
            }
            Update(0.25f, idle); // Past 0.60s -> steady OFF hold for remainder of 10s
            if (IsInBrownoutOffFlicker || MathF.Abs(PowerIndicatorBrightness - 0.08f) > 0.02f || MathF.Abs(BrownoutAmbientFactor - 0.24f) > 0.02f)
            {
                throw new InvalidOperationException("Brownout OFF state did not settle into dark overhead hold after 6-step flicker.");
            }

            // Verify DIM state syncs POWER LED to ~0.42f and dims ambient while keeping full legibility
            devDebug.BrownoutForceMode = DebugBrownoutForceMode.ForceDim;
            Update(0.016f, idle);
            if (ActiveBrownoutState != BoothBrownoutState.Dim || MathF.Abs(PowerIndicatorBrightness - 0.42f) > 0.02f)
            {
                throw new InvalidOperationException($"Brownout DIM state failed to sync POWER LED (got {PowerIndicatorBrightness}).");
            }

            // Verify GPU framebuffer pixels: emissive CRT Comms Log & backlit Data Slate stay unattenuated in OFF,
            // while passive desk metal & matte ceramic Cert-Wafers attenuate with alpha == 255!
            SmokeStageShipLoadedAndInspecting();
            _waferHopperCount = MaxWaferHopperCount;
            _deskWafers.Clear();
            EjectWaferFromHopper(new CertWaferCard
            {
                InstanceId = _nextWaferInstanceId++,
                WaferType = CertWaferType.CargoManifest,
                ShipRegistration = _activeShip!.Slate.RegistrationCode,
                PrimaryCargoItem = "Hull Plating",
                SecondaryNote = "STAGE-2 CARGO MANIFEST"
            });
            Update(0.45f, idle);

            if (Raylib.IsWindowReady())
            {
                CertWaferCard waferOnDesk = _deskWafers[0];
                DraggableDeskItem slateItem = _deskItems[DeskItemId.DataSlate];
                int crtSampleX = _layout.Transcript.X + 80;
                int crtSampleY = _layout.Transcript.Y + 8; // Header text / phosphor region inside CRT Comms Log
                int slateHeaderX = slateItem.X + 12;
                int slateHeaderY = slateItem.Y + 8;
                int waferPaperX = waferOnDesk.X + 40;
                int waferPaperY = waferOnDesk.Y + 34; // Matte e-ink plate on Cert-Wafer

                devDebug.BrownoutForceMode = DebugBrownoutForceMode.ForceOn;
                Update(0.016f, idle);
                canvas.BeginVirtualDraw();
                DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
                Image imgOn = canvas.CaptureVirtualFrameImage();

                devDebug.BrownoutForceMode = DebugBrownoutForceMode.ForceOff;
                Update(0.01f, idle);
                Update(0.80f, idle); // Settle past 0.60s flicker into steady OFF hold
                canvas.BeginVirtualDraw();
                DrawVirtual(idle);
                canvas.EndVirtualDrawAndBlit();
                Image imgOff = canvas.CaptureVirtualFrameImage();

                try
                {
                    Color waferColOn = Raylib.GetImageColor(imgOn, waferPaperX, waferPaperY);
                    Color waferColOff = Raylib.GetImageColor(imgOff, waferPaperX, waferPaperY);
                    Color slateColOn = Raylib.GetImageColor(imgOn, slateHeaderX, slateHeaderY);
                    Color slateColOff = Raylib.GetImageColor(imgOff, slateHeaderX, slateHeaderY);
                    Color crtColOn = Raylib.GetImageColor(imgOn, crtSampleX, crtSampleY);
                    Color crtColOff = Raylib.GetImageColor(imgOff, crtSampleX, crtSampleY);

                    int waferLumOn = waferColOn.R + waferColOn.G + waferColOn.B;
                    int waferLumOff = waferColOff.R + waferColOff.G + waferColOff.B;
                    if (waferColOff.A != 255 || waferLumOff >= waferLumOn - 40)
                    {
                        throw new InvalidOperationException(
                            $"Matte ceramic Cert-Wafer failed to dim cleanly during brownout OFF (lumOn={waferLumOn}, lumOff={waferLumOff}, A={waferColOff.A}).");
                    }
                    if (slateColOn.R != slateColOff.R || slateColOn.G != slateColOff.G || slateColOn.B != slateColOff.B ||
                        crtColOn.R != crtColOff.R || crtColOn.G != crtColOff.G || crtColOn.B != crtColOff.B)
                    {
                        throw new InvalidOperationException(
                            $"Backlit Data Slate or emissive CRT Comms Log was attenuated during brownout OFF (slate {slateColOn}->{slateColOff}, crt {crtColOn}->{crtColOff}).");
                    }
                }
                finally
                {
                    Raylib.UnloadImage(imgOn);
                    Raylib.UnloadImage(imgOff);
                }
            }

            // Verify auto 45-55s brownout cycle across full 270s shift caps strictly at <= 6 triggers and reaches both DIM & OFF
            devDebug.BrownoutForceMode = DebugBrownoutForceMode.Auto;
            InitializeBrownoutStateForShift();
            bool sawDim = false;
            bool sawOff = false;
            for (int sec = 0; sec < 330; sec++)
            {
                SmokeAdvanceBrownoutTimer(1.0f);
                if (ActiveBrownoutState == BoothBrownoutState.Dim) sawDim = true;
                if (ActiveBrownoutState == BoothBrownoutState.Off) sawOff = true;
            }
            if (BrownoutTriggerCount is < 5 or > MaxBrownoutTriggersPerShift || !sawDim || !sawOff)
            {
                throw new InvalidOperationException(
                    $"Auto brownout 270s cycle failed bounds or state coverage (triggers={BrownoutTriggerCount}, sawDim={sawDim}, sawOff={sawOff}).");
            }

            // Verify ESC Pause Menu open state keeps brownout timer frozen when DeskScreen.Update is skipped
            InitializeBrownoutStateForShift();
            SmokeAdvanceBrownoutTimer(12.0f);
            float cycleBeforePause = _brownoutCycleElapsed;
            pauseMenu.Toggle();
            if (!pauseMenu.IsOpen)
                throw new InvalidOperationException("PauseMenu failed to open for brownout freeze check.");
            pauseMenu.Toggle();
            if (MathF.Abs(_brownoutCycleElapsed - cycleBeforePause) > 0.0001f)
                throw new InvalidOperationException("Brownout cycle timer drifted while ESC Pause Menu was open.");
        }
        finally
        {
            pauseMenu.Close();
            devDebug.BrownoutForceMode = savedBrownoutForce;
            config.DeskUpperLayoutMode = savedLayoutMode;
            config.EnableStokerDial = savedEnableStoker;
            config.StokerSafeLoadCap = savedSafeCap;
            config.StokerUncalibratedShock = savedShock;
            SyncLayoutFromConfig();
            OnExit();
            _gameState.SuppressDiskPersistence = prevSuppress;
        }
    }
}
