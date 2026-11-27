using System.Numerics;
using Raylib_cs;
using SubspaceProtocols.Core;
using SubspaceProtocols.Data;
using SubspaceProtocols.Procedural;
using SubspaceProtocols.Simulation;

namespace SubspaceProtocols.UI;

public sealed partial class DeskScreen
{
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
        _warpStripShipPrefix = _activeShip.Slate.DeclaredPrefix;
        _warpStripShipName = _activeShip.Slate.ShipName;
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

    // =========================================================================
    // Day 2+ SPS Hold-to-Lock Anomaly Scanner & Lens Positioning
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
            (_activeShipHardpoints != null || _activeShipHardpointsV2.HasValue) &&
            shipInInspectableBayState &&
            !_activeShip.GroundTruth.ActualShieldsUp &&
            mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH);

        if (!canScanHardpoints || _activeShip == null)
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

        SpsAnomalyKind hoveredAnomaly = SpsAnomalyKind.None;

        // Detect ALL currently hot weapon mounts (both openly declared honest armed ships AND spoofed liars)
        bool hasHotWeapons = ShiftController.ShouldSpsDetectHotWeapons(_activeShip);
        if (hasHotWeapons && TryGetActiveWeaponsSpsTarget(out HardpointRect wpnRect560, out Vector2 wpnCenter))
        {
            if (IsReticleNearSpsTarget(reticleCx, reticleCy, wpnRect560, wpnCenter))
            {
                // Auto-SPS Weapons Compliance (Day 2+): sweeping hot weapons automatically triggers
                // "Power down weapons." once per ship (debounced via _autoSpsWeaponsOrderTriggered).
                if (!_autoSpsWeaponsOrderTriggered)
                {
                    _autoSpsWeaponsOrderTriggered = true;
                    AudioHooks.Play("terminal_key_click");
                    ShipSpec targetShip = _activeShip;
                    EnqueueOfficerLine("Power down weapons.", CommsTextTone.ClearanceTeal, onComplete: () =>
                    {
                        if (_activeShip == targetShip && !_isDetainingShip)
                        {
                            _pendingWeaponsOrder = true;
                            _pendingShieldsOrder = false;
                            _complianceTimer = targetShip.ComplianceDelaySeconds;
                        }
                    });
                }

                // Dishonest ships (HasSpoofedWeapons or hostile Day 22+ refusal) remain hot so the player can
                // hold-to-lock [!] HOT WEAPONS onto the SPS Anomaly Mini-Screen for Protocol Review.
                bool staysHotForProtocolReview =
                    _activeShip.GroundTruth.HasSpoofedWeapons ||
                    (_gameState.Day >= _config.DayUnlockHostilityDestroy &&
                     _gameState.GetRep(_activeShip.Slate.DeclaredPrefix) <= Constants.RepExtremeHostileThreshold &&
                     _activeShip.GroundTruth.WillRefuseWeaponsCompliance);

                bool alreadyLogged = _spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.HotWeaponMount && !e.IsCooled);
                if (staysHotForProtocolReview && !alreadyLogged)
                {
                    hoveredAnomaly = SpsAnomalyKind.HotWeaponMount;
                }
            }
        }

        if (hoveredAnomaly == SpsAnomalyKind.None)
        {
            bool hasUnloggedCargoPower =
                IsCargoPowerUnlocked &&
                _activeShip.GroundTruth.ActualCargoPowerDraw &&
                !_spsLoggedAnomalies.Any(e => e.Kind == SpsAnomalyKind.VioletCargoPower && !e.IsCooled);

            if (hasUnloggedCargoPower && TryGetActiveCargoSpsTarget(out HardpointRect cargoRect560, out Vector2 cargoCenter))
            {
                if (IsReticleNearSpsTarget(reticleCx, reticleCy, cargoRect560, cargoCenter))
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

    private Vector2 ComputeSpsSwayOffset(float t)
    {
        float ampX;
        float ampY;
        float speed;

        if (_gameState.CurrentHungerTier == HungerTier.HighTierRations)
        {
            return Vector2.Zero;
        }

        if (_gameState.CurrentHungerTier == HungerTier.LowNoFood ||
            _gameState.ConsecutiveDryOrNoFoodDays >= 2 ||
            _gameState.DaysWithoutFood >= 2)
        {
            ampX = 36.0f;
            ampY = 26.0f;
            speed = 2.40f;
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
    // Hardware Panel Click Dispatch
    // =========================================================================
    private bool TryHandleHardwarePanelClicks(VirtualMouseState mouse)
    {
        if (_preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive)
            return false;

        // 0. RE-TRANSMIT Click on Slate Bus or Next Hail slot when DownloadAborted
        if (_phase == EncounterPhase.DownloadAborted &&
            (mouse.IsHit(_layout.SlateBus.X, _layout.SlateBus.Y, _layout.SlateBus.W, _layout.SlateBus.H) ||
             mouse.IsHit(_layout.NextHailBtn.X, _layout.NextHailBtn.Y, _layout.NextHailBtn.W, _layout.NextHailBtn.H)))
        {
            TryTriggerReTransmit();
            return true;
        }

        // 1. Physical Left-Side DETAIN LEVER: (8, 40, 96, 216)
        if (mouse.IsHit(DetainLeverX, DetainLeverY, DetainLeverW, DetainLeverH))
        {
            bool canDetain = _activeShip != null &&
                _phase is EncounterPhase.OpeningGreetingDelay
                    or EncounterPhase.AwaitingAutoShieldsDrop
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

        // 1b. Day 15+ Stellar Stoker [ANCHOR] trigger: (12, 330, 88, 18) inside (8, 264, 96, 88)
        if (IsStokerOperational && mouse.IsHit(StokerAnchorBtnX, StokerAnchorBtnY, StokerAnchorBtnW, StokerAnchorBtnH))
        {
            TryAnchorStokerMassForActiveShip();
            return true;
        }

        // 2. NEXT HAIL (or calm premature-wafer [RE-TRANSMIT], or [INSPECT ROUTE] pushback on Day 3 Ghost Route ship)
        if (mouse.IsHit(_layout.NextHailBtn.X, _layout.NextHailBtn.Y, _layout.NextHailBtn.W, _layout.NextHailBtn.H))
        {
            if (CanReTransmitPrematurelyWipedWafers)
            {
                TriggerPrematurelyWipedWaferReTransmit();
            }
            else if (CanTriggerGhostRoutePushback)
            {
                TriggerGhostRoutePushback();
            }
            else if (_shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty)
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

        // 3. PROTOCOL REVIEW Toggle (Day 1+)
        if (IsProtocolReviewUnlocked &&
            mouse.IsHit(_layout.ProtocolReviewBtn.X, _layout.ProtocolReviewBtn.Y, _layout.ProtocolReviewBtn.W, _layout.ProtocolReviewBtn.H))
        {
            if (_activeShip != null && IsShipInspectableForReview && _faultStage == FaultSequenceStage.None)
            {
                _protocolReviewModeActive = !_protocolReviewModeActive;
                _reviewFirstSelection = ReviewTarget.None;
                _noFaultTraceTimer = 0f;
                _noFaultTargetA = ReviewTarget.None;
                _noFaultTargetB = ReviewTarget.None;
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

        // 7. Grab and drag the visible docked 184x216 Data Slate directly out of Clearance Upload Dock
        //    Works BOTH mid-encryption (1%-99%, cleanly aborting/resetting to 0% once pulled >= 8px) AND after 100% cleared!
        if (IsDataSlateInClearanceDock &&
            _phase != EncounterPhase.DetainClamping &&
            mouse.IsHit(_layout.ClearanceDock.X, _layout.ClearanceDock.Y, _layout.ClearanceDock.W, _layout.ClearanceDock.H))
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
        UiText.Draw("VOID GATE 3 // BOOTH 04", 8, textY, 10, Palette.CrtAmberDimmed, denseRow: true);
        UiText.Draw($"DAY {_gameState.Day:D2}", 200, textY, 10, Palette.CrtAmberPrimary, denseRow: true);

        // Station Cycle box (256, 4, 112, 24) -- clickable in Protocol Review on Day 8+ (Manual Part 1 unlock)
        (int curYear, int curMonth) = Constants.GetStationCycle(_gameState.Day);
        string cycleLabel = $"CYC {Constants.FormatStationCycle(curYear, curMonth)}";
        bool cycleSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.TopHeaderCycle;
        bool cycleFault = IsTargetFaultHighlighted(ReviewTarget.TopHeaderCycle);
        Raylib.DrawRectangle(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleFault ? Palette.BrownDwarfGlowDeep : (cycleSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid));
        Raylib.DrawRectangleLines(TopCycleX, TopCycleY, TopCycleW, TopCycleH,
            cycleFault ? GetFaultPulseColor() : (cycleSelected ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn));
        if (_protocolReviewModeActive && IsReviewTargetUnlocked(ReviewTarget.TopHeaderCycle))
        {
            DrawReviewHotspotOutline(mouse, TopCycleX, TopCycleY, TopCycleW, TopCycleH, cycleSelected);
        }
        int cycW = UiText.MeasureWidth(cycleLabel, 10, denseRow: true);
        UiText.Draw(cycleLabel, TopCycleX + (TopCycleW - cycW) / 2, TopCycleY + (TopCycleH - 10) / 2, 10,
            cycleSelected ? Palette.ClearanceTeal : Palette.CrtAmberPrimary, denseRow: true);

        // Pact Scrip, Daily Warnings, Security Strikes (Day 4+ strictly /3), Processed
        int sx = 380;
        string scripStr = $"PACT SCRIP: {_gameState.PactScrip} SC";
        UiText.Draw(scripStr, sx, textY, 10, Palette.ClearanceTeal, denseRow: true);
        sx += Math.Max(176, UiText.MeasureWidth(scripStr, 10, denseRow: true) + 16);

        string warnStr = $"WARNINGS: {_gameState.ClericalWarningsThisShift}/{Constants.FreeClericalWarningsPerShift}";
        UiText.Draw(
            warnStr,
            sx, textY, 10,
            _gameState.ClericalWarningsThisShift >= Constants.FreeClericalWarningsPerShift ? Palette.AlertCrimson : Palette.CrtAmberPrimary,
            denseRow: true);
        sx += Math.Max(132, UiText.MeasureWidth(warnStr, 10, denseRow: true) + 16);

        if (AreSecurityStrikesUnlocked)
        {
            string strikeStr = $"SECURITY STRIKES: {_gameState.SecurityStrikes}/{_config.MaxStrikesBeforeAudit}";
            UiText.Draw(
                strikeStr,
                sx, textY, 10,
                _gameState.SecurityStrikes > 0 ? Palette.AlertCrimson : Palette.CrtAmberPrimary,
                denseRow: true);
            sx += Math.Max(192, UiText.MeasureWidth(strikeStr, 10, denseRow: true) + 16);
        }

        string procStr = $"PROCESSED: {_gameState.ShipsProcessedToday}";
        int procMaxW = Math.Max(40, PowerModuleX - 8 - sx);
        UiText.Draw(TruncateByPixelWidth(procStr, 10, procMaxW, denseRow: true), sx, textY, 10, Palette.CrtAmberDimmed, denseRow: true);

        // Diegetic Top-Right POWER Indicator Module: (1172, 2, 56, 28) -- exact 8px gutter left of Pause [||] (1236)
        DrawTopPowerIndicatorModule();

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

    private void DrawTopPowerIndicatorModule()
    {
        float b = Math.Clamp(PowerIndicatorBrightness, 0f, 1f);

        Raylib.DrawRectangle(PowerModuleX, PowerModuleY, PowerModuleW, PowerModuleH, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(PowerModuleX, PowerModuleY, PowerModuleW, PowerModuleH,
            b > 0.05f ? Palette.WeatheredGunmetalEdge : new Color(36, 42, 50, 255));

        // Physical indicator LED centered on top
        const int ledW = 16;
        const int ledH = 6;
        int ledX = PowerModuleX + (PowerModuleW - ledW) / 2;
        int ledY = PowerModuleY + 4;
        Raylib.DrawRectangle(ledX - 1, ledY - 1, ledW + 2, ledH + 2, Palette.OutlineVoid);

        Color ledOff = new(20, 24, 28, 255);
        Color ledOn = Palette.CrtAmberPrimary;
        Color ledCol = BlendRgb(ledOff, ledOn, b);
        Raylib.DrawRectangle(ledX, ledY, ledW, ledH, ledCol);
        if (b > 0.55f)
        {
            Color hotCore = BlendRgb(ledOn, new Color(255, 244, 206, 255), (b - 0.55f) / 0.45f);
            Raylib.DrawRectangle(ledX + 3, ledY + 1, ledW - 6, ledH - 3, hotCore);
        }

        // Crisp POWER text centered directly underneath LED
        const string pwrLabel = "POWER";
        int pw = UiText.MeasureWidth(pwrLabel, 10);
        Color txtOff = new(50, 56, 64, 255);
        Color txtOn = Palette.CrtAmberPrimary;
        UiText.Draw(pwrLabel, PowerModuleX + (PowerModuleW - pw) / 2, PowerModuleY + 15, 10, BlendRgb(txtOff, txtOn, b));
    }

    // =========================================================================
    // 2. Physical Left-Side Detain Lever: (8, 40, 96, 216)
    // =========================================================================
    private void DrawLeftDetainLever(VirtualMouseState mouse)
    {
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive;
        bool canDetain = !lockedByPreShift && _activeShip != null &&
                         (_phase is EncounterPhase.OpeningGreetingDelay
                             or EncounterPhase.AwaitingAutoShieldsDrop
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
        int lw = UiText.MeasureWidth(label, 10);
        UiText.Draw(label, DetainLeverX + (DetainLeverW - lw) / 2, DetainLeverY + 12, 10,
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

        // Bottom indicator lamp (clicks off during shift-end power-down beat)
        bool lampOff = ClockOutPowerDownProgress >= 0.68f;
        bool blink = ((_animTick / 8) % 2) == 0;
        Color ledCol = lampOff
            ? new Color(20, 24, 30, 255)
            : (_detainLeverPulled
                ? (blink ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
                : (canDetain ? Palette.AlertCrimson : Palette.WeatheredGunmetalMid));
        Raylib.DrawRectangle(DetainLeverX + 14, DetainLeverY + DetainLeverH - 22, DetainLeverW - 28, 14, Palette.OutlineVoid);
        Raylib.DrawRectangle(DetainLeverX + 16, DetainLeverY + DetainLeverH - 20, DetainLeverW - 32, 10, ledCol);
        Raylib.DrawRectangleLines(DetainLeverX + 14, DetainLeverY + DetainLeverH - 22, DetainLeverW - 28, 14, Palette.WeatheredGunmetalEdge);
    }

    // =========================================================================
    // 3. Armored External Space Viewport (Real Glass Window): (112, 40, 448, 216)
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
        if (IsBrownoutAttenuatingPassiveSurfaces)
        {
            ApplyBrownoutPassiveSurfaceLighting(ViewportX, ViewportY, ViewportW, ViewportGlassInset);
            ApplyBrownoutPassiveSurfaceLighting(ViewportX, ViewportY + ViewportH - ViewportGlassInset, ViewportW, ViewportGlassInset);
            ApplyBrownoutPassiveSurfaceLighting(ViewportX, ViewportInnerY, ViewportGlassInset, ViewportInnerH);
            ApplyBrownoutPassiveSurfaceLighting(ViewportInnerX + ViewportInnerW, ViewportInnerY, ViewportGlassInset, ViewportInnerH);
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
        float sunsetGlow = ClockOutPowerDownProgress;
        int horizonGlowH = 26 + (int)MathF.Round(sunsetGlow * 54f);
        Raylib.DrawRectangle(ViewportInnerX, horizonY - horizonGlowH, ViewportInnerW, horizonGlowH, Palette.BrownDwarfGlowDeep);
        if (sunsetGlow > 0.01f)
        {
            byte rimAlpha = (byte)Math.Clamp((int)MathF.Round(sunsetGlow * 170f), 0, 200);
            Raylib.DrawRectangle(ViewportInnerX, horizonY - 34, ViewportInnerW, 34, new Color((byte)148, (byte)32, (byte)28, rimAlpha));
        }

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

        if (sunsetGlow > 0.01f)
        {
            byte haloA = (byte)Math.Clamp((int)MathF.Round(sunsetGlow * 140f), 0, 180);
            Raylib.DrawCircle(dwarfX, dwarfY, 44, new Color((byte)132, (byte)26, (byte)24, haloA));
            Raylib.DrawCircle(dwarfX, dwarfY, 32, new Color((byte)176, (byte)38, (byte)32, haloA));
        }
        Raylib.DrawCircle(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircle(dwarfX - 2, dwarfY - 2, 18, Palette.BrownDwarfGlowRim);
        Raylib.DrawRectangle(dwarfX - 20, dwarfY - 5, 40, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawRectangle(dwarfX - 18, dwarfY + 3, 36, 2, Palette.BrownDwarfGlowDeep);
        Raylib.DrawCircleLines(dwarfX, dwarfY, 24, Palette.BrownDwarfGlowRim);

        // Exterior docking bay sill along lower edge of window (shows subtle station lighting dips during brownout DIM/OFF)
        bool stationBrownoutDip = IsBrownoutAttenuatingPassiveSurfaces;
        Color sillCol = stationBrownoutDip
            ? new Color(18, 22, 28, 255)
            : Palette.WeatheredGunmetalMid;
        Color sillLampCol = sunsetGlow >= 0.45f
            ? Palette.BrownDwarfGlowRim
            : (stationBrownoutDip
                ? (_brownoutState == BoothBrownoutState.Off ? new Color(58, 38, 18, 255) : new Color(128, 92, 42, 255))
                : Palette.CrtAmberDimmed);
        Raylib.DrawRectangle(ViewportInnerX, horizonY, ViewportInnerW, ViewportInnerY + ViewportInnerH - horizonY, Palette.OutlineVoid);
        Raylib.DrawLine(ViewportInnerX, horizonY, ViewportInnerX + ViewportInnerW, horizonY,
            sunsetGlow > 0.25f ? Palette.AlertCrimson : Palette.BrownDwarfGlowRim);
        for (int gx = ViewportInnerX + 20; gx < ViewportInnerX + ViewportInnerW - 18; gx += 48)
        {
            Raylib.DrawRectangle(gx, horizonY - 5, 12, 6, sillCol);
            Raylib.DrawPixel(gx + 5, horizonY - 3, sillLampCol);
        }

        // Docked / Approaching Ship Sprite
        if (IsPreShiftTalkingHeadHailActive)
        {
            // Keep viewport cleanly empty during pre-shift internal Kordani hail
        }
        else if (_activeShip != null && _cachedShipLiveTex.Id != 0)
        {
            GetActiveShipRenderRects(shakeX, shakeY, out Rectangle src, out Rectangle dst);
            int shipDrawX = (int)dst.X;
            int shipDrawY = (int)dst.Y;
            Raylib.DrawTexturePro(_cachedShipLiveTex, src, dst, Vector2.Zero, 0f, Color.White);

            // Protocol Review hover or Fault highlight on live ship hull
            Rectangle hullRect = GetActiveShipHullScreenRect(shakeX, shakeY);
            bool hullFault = IsTargetFaultHighlighted(ReviewTarget.ViewportShipHull);
            bool hullReviewHover = _protocolReviewModeActive &&
                                   IsReviewTargetUnlocked(ReviewTarget.ViewportShipHull) &&
                                   IsShipInspectableForReview &&
                                   mouse.IsHit(ViewportInnerX, ViewportInnerY, ViewportInnerW, ViewportInnerH) &&
                                   (!_spsEnabled || !mouse.IsHit(_lastSpsLensRect));
            if (hullFault || hullReviewHover)
            {
                Color hiCol = hullFault ? GetFaultPulseColor() : Palette.CrtAmberPrimary;
                int hx = (int)hullRect.X;
                int hy = (int)hullRect.Y;
                int hw = (int)hullRect.Width;
                int hh = (int)hullRect.Height;
                Raylib.DrawRectangleLines(hx - 3, hy - 3, hw + 6, hh + 6, hiCol);
                if (hullFault)
                {
                    Raylib.DrawRectangleLines(hx - 4, hy - 4, hw + 8, hh + 8, hiCol);
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

    private bool IsReticleNearSpsTarget(float reticleCx, float reticleCy, HardpointRect viewport560Rect, Vector2 screenCenter)
    {
        if (!IsUsingShipArtV2)
        {
            float dxV1 = reticleCx - screenCenter.X;
            float dyV1 = reticleCy - screenCenter.Y;
            return dxV1 * dxV1 + dyV1 * dyV1 <= SpsLockRadiusPx * SpsLockRadiusPx;
        }

        int slidePx = GetShipSlidePixels();
        float rx = ShipV2AnchorX + slidePx + viewport560Rect.X;
        float ry = ShipV2AnchorY + viewport560Rect.Y;
        float closestX = Math.Clamp(reticleCx, rx, rx + viewport560Rect.W);
        float closestY = Math.Clamp(reticleCy, ry, ry + viewport560Rect.H);
        float dx = reticleCx - closestX;
        float dy = reticleCy - closestY;
        return dx * dx + dy * dy <= SpsLockRadiusPx * SpsLockRadiusPx;
    }

    private void DrawAnimatedDetainDockingClamps(int shipDrawX, int shipDrawY, int horizonY)
    {
        float impactTime = DetainSequenceDurationSeconds * ShiftController.DetainClampImpactFraction;
        float rawT = _phase == EncounterPhase.DetainClamping
            ? Math.Clamp(_detainClampElapsed / impactTime, 0f, 1f)
            : 1.0f;
        // Snappy mechanical ease-out slam
        float clampT = 1.0f - MathF.Pow(1.0f - rawT, 3.0f);

        bool locked = rawT >= 0.99f;
        bool flash = ((_animTick / 5) % 2) == 0;
        Color strobeCol = locked && flash ? Palette.CrtAmberPrimary : Palette.AlertCrimson;

        if (IsUsingShipArtV2 && _activeShipHardpointsV2.HasValue)
        {
            ClampGrabZone[] zones =
            [
                _activeShipHardpointsV2.Value.ForeClampViewport560,
                _activeShipHardpointsV2.Value.AftClampViewport560
            ];

            foreach (ClampGrabZone zone in zones)
            {
                int cx = ((shipDrawX + zone.X - 6) / 2) * 2;
                int topTargetY = Math.Clamp(((shipDrawY + zone.TopY) / 2) * 2, ViewportInnerY + 6, horizonY - 16);
                int botTargetY = Math.Clamp(((shipDrawY + zone.BottomY) / 2) * 2, topTargetY + 8, horizonY - 6);

                int topJawY = ViewportInnerY + (int)MathF.Round((topTargetY - ViewportInnerY) * clampT / 2.0f) * 2;
                int botJawY = horizonY - (int)MathF.Round((horizonY - botTargetY) * clampT / 2.0f) * 2;

                // Top hydraulic gantry column + 2px pixel-locked pincer jaw biting directly onto hull grab pad
                int topLen = Math.Max(4, topJawY - ViewportInnerY);
                Raylib.DrawRectangle(cx, ViewportInnerY, 12, topLen, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx + 2, ViewportInnerY, 8, topLen, Palette.WeatheredGunmetalMid);
                Raylib.DrawRectangle(cx + 4, ViewportInnerY, 4, topLen, Palette.WeatheredGunmetalEdge);
                Raylib.DrawRectangle(cx - 6, topJawY - 10, 24, 12, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx - 4, topJawY - 8, 20, 8, Palette.OxidizedBronzeShadow);
                Raylib.DrawRectangle(cx - 4, topJawY - 8, 20, 2, Palette.OxidizedBronzeWorn);
                Raylib.DrawRectangle(cx - 4, topJawY, 6, 6, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx - 2, topJawY, 4, 4, strobeCol);
                Raylib.DrawRectangle(cx + 10, topJawY, 6, 6, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx + 10, topJawY, 4, 4, strobeCol);

                // Bottom hydraulic gantry column + pincer jaw
                int botLen = Math.Max(4, horizonY - botJawY);
                Raylib.DrawRectangle(cx, botJawY, 12, botLen, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx + 2, botJawY, 8, botLen, Palette.WeatheredGunmetalMid);
                Raylib.DrawRectangle(cx + 4, botJawY, 4, botLen, Palette.WeatheredGunmetalEdge);
                Raylib.DrawRectangle(cx - 6, botJawY - 2, 24, 12, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx - 4, botJawY, 20, 8, Palette.OxidizedBronzeShadow);
                Raylib.DrawRectangle(cx - 4, botJawY, 20, 2, Palette.OxidizedBronzeWorn);
                Raylib.DrawRectangle(cx - 4, botJawY - 6, 6, 6, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx - 2, botJawY - 4, 4, 4, strobeCol);
                Raylib.DrawRectangle(cx + 10, botJawY - 6, 6, 6, Palette.OutlineVoid);
                Raylib.DrawRectangle(cx + 10, botJawY - 4, 4, 4, strobeCol);

                if (locked)
                {
                    Raylib.DrawRectangle(cx + 5, topJawY + 4, 2, Math.Max(2, botJawY - topJawY - 8), strobeCol);
                    if (_viewportImpactShakeTimer > 0f)
                    {
                        Raylib.DrawRectangle(cx - 8, topJawY + 2, 2, 2, Palette.CrtAmberPrimary);
                        Raylib.DrawRectangle(cx + 18, topJawY + 2, 2, 2, Palette.CrtAmberPrimary);
                        Raylib.DrawRectangle(cx - 8, botJawY - 4, 2, 2, Palette.CrtAmberPrimary);
                        Raylib.DrawRectangle(cx + 18, botJawY - 4, 2, 2, Palette.CrtAmberPrimary);
                    }
                }
            }
            return;
        }

        int foreX = shipDrawX + 52;
        int aftX = shipDrawX + ShipRenderW - 64;

        int v1TopTargetY = shipDrawY + 28;
        int v1BotTargetY = shipDrawY + ShipRenderH - 28;

        int v1TopJawY = ViewportInnerY + (int)MathF.Round((v1TopTargetY - ViewportInnerY) * clampT);
        int v1BotJawY = horizonY - (int)MathF.Round((horizonY - v1BotTargetY) * clampT);

        int[] clampXs = [foreX, aftX];
        foreach (int cx in clampXs)
        {
            // Top hydraulic gantry column + locking pincer claw
            int topLen = Math.Max(4, v1TopJawY - ViewportInnerY);
            Raylib.DrawRectangle(cx, ViewportInnerY, 12, topLen, Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(cx, ViewportInnerY, 12, topLen, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx + 4, ViewportInnerY, 4, topLen, Palette.WeatheredGunmetalEdge);
            // Pincer jaw head
            Raylib.DrawRectangle(cx - 5, v1TopJawY - 8, 22, 10, Palette.OxidizedBronzeShadow);
            Raylib.DrawRectangleLines(cx - 5, v1TopJawY - 8, 22, 10, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx - 3, v1TopJawY, 6, 6, strobeCol);
            Raylib.DrawRectangle(cx + 9, v1TopJawY, 6, 6, strobeCol);

            // Bottom hydraulic gantry column + locking pincer claw
            int botLen = Math.Max(4, horizonY - v1BotJawY);
            Raylib.DrawRectangle(cx, v1BotJawY, 12, botLen, Palette.WeatheredGunmetalMid);
            Raylib.DrawRectangleLines(cx, v1BotJawY, 12, botLen, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx + 4, v1BotJawY, 4, botLen, Palette.WeatheredGunmetalEdge);
            // Pincer jaw head
            Raylib.DrawRectangle(cx - 5, v1BotJawY - 2, 22, 10, Palette.OxidizedBronzeShadow);
            Raylib.DrawRectangleLines(cx - 5, v1BotJawY - 2, 22, 10, Palette.OutlineVoid);
            Raylib.DrawRectangle(cx - 3, v1BotJawY - 6, 6, 6, strobeCol);
            Raylib.DrawRectangle(cx + 9, v1BotJawY - 6, 6, 6, strobeCol);

            // Magnetic lock beam connecting upper & lower jaws once snapped shut
            if (locked)
            {
                Raylib.DrawLine(cx + 6, v1TopJawY + 4, cx + 6, v1BotJawY - 4, strobeCol);
                if (_viewportImpactShakeTimer > 0f)
                {
                    // Brief contact spark pixels
                    Raylib.DrawPixel(cx - 7, v1TopJawY + 2, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx + 18, v1TopJawY + 3, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx - 6, v1BotJawY - 2, Palette.CrtAmberPrimary);
                    Raylib.DrawPixel(cx + 17, v1BotJawY - 3, Palette.CrtAmberPrimary);
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

        bool spsPoweredOff = ClockOutPowerDownProgress >= 0.36f;
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive || spsPoweredOff;

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
        Color ledCol = (!spsPoweredOff && _spsEnabled) ? Palette.SpsCommsActive : new Color(26, 30, 36, 255);
        Raylib.DrawRectangle(SpsToggleX + 12, SpsToggleY + 9, SpsToggleW - 24, 5, ledCol);

        const string spsTitle = "SPS PROBE";
        int tw = UiText.MeasureWidth(spsTitle, 10);
        UiText.Draw(spsTitle, SpsToggleX + (SpsToggleW - tw) / 2, SpsToggleY + 22, 10,
            spsPoweredOff ? Palette.WeatheredGunmetalEdge : (_spsEnabled ? Palette.SpsShieldArcIce : Palette.CrtAmberPrimary));

        string spsState = spsPoweredOff ? "[ OFFLINE ]" : (_spsEnabled ? "[ ACTIVE ]" : "[ STANDBY ]");
        int sw = UiText.MeasureWidth(spsState, 10);
        UiText.Draw(spsState, SpsToggleX + (SpsToggleW - sw) / 2, SpsToggleY + 38, 10,
            spsPoweredOff ? Palette.WeatheredGunmetalEdge : (_spsEnabled ? Palette.SpsCommsActive : Palette.CrtAmberDimmed));

        // Bottom: SPS Anomaly Mini-Screen (568, 108, 104, 148)
        // Diagnostic log on Day 2; strictly selectable Protocol Review evidence on Day 3+ (never a command trigger!)
        bool hasLogged = _spsLoggedAnomalies.Count > 0;
        bool hasActiveAnomaly = _spsLoggedAnomalies.Any(e => !e.IsCooled);
        bool locking = _spsLockProgress > 0.01f;
        bool miniFault = IsTargetFaultHighlighted(ReviewTarget.SpsMiniScreenHotWeapons) ||
                         IsTargetFaultHighlighted(ReviewTarget.SpsMiniScreenCargoPower);

        Color frameCol = spsPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : (miniFault
                ? GetFaultPulseColor()
                : (hasActiveAnomaly ? Palette.SpsWeaponsHotOrange : (_spsEnabled ? Palette.SpsCommsActive : Palette.WeatheredGunmetalEdge)));

        Raylib.DrawRectangle(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, spsPoweredOff ? new Color(8, 10, 14, 255) : Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH, frameCol);
        if (miniFault && !spsPoweredOff)
        {
            Raylib.DrawRectangleLines(SpsMiniX - 1, SpsMiniY - 1, SpsMiniW + 2, SpsMiniH + 2, frameCol);
        }

        Raylib.DrawRectangle(SpsMiniX + 1, SpsMiniY + 1, SpsMiniW - 2, 16, Palette.OutlineVoid);
        Raylib.DrawLine(SpsMiniX + 1, SpsMiniY + 17, SpsMiniX + SpsMiniW - 1, SpsMiniY + 17, Palette.WeatheredGunmetalEdge);
        const string logHeader = "SPS LOG";
        UiText.Draw(logHeader, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(logHeader, 10)) / 2, SpsMiniY + 4, 10,
            spsPoweredOff ? Palette.WeatheredGunmetalEdge : (_spsEnabled || hasLogged ? Palette.SpsCommsActive : Palette.CrtAmberDimmed));

        if (spsPoweredOff)
        {
            return;
        }

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
                    UiText.Draw("[OK] COOLED", rx + 6, ry + 4, 10, Palette.ClearanceTeal);
                    UiText.Draw("WPN OFFLINE", rx + 6, ry + 16, 10, Palette.WeatheredGunmetalEdge);
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
                    : (entry.Kind == SpsAnomalyKind.HotWeaponMount ? "[!] HOT WEAPONS" : "[!] POWERED CARGO");

                Raylib.DrawRectangle(rx, ry, rw, rh, rowBg);
                Raylib.DrawRectangleLines(rx, ry, rw, rh, rowBorder);
                if (inReviewMode && !rowFault && IsReviewTargetUnlocked(rowTarget))
                {
                    DrawReviewHotspotOutline(mouse, rx, ry, rw, rh, rowSelected);
                }

                UiText.Draw(TruncateByPixelWidth(topLabel, 10, rw - 6), rx + 3, ry + 4, 10, topFg);
                UiText.Draw(TruncateByPixelWidth(entry.Label, 10, rw - 6), rx + 3, ry + 16, 10, Palette.CrtAmberPrimary);
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
            UiText.Draw(lockTxt, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(lockTxt, 10)) / 2, SpsMiniY + 46, 10, Palette.CrtAmberPrimary);
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
                UiText.Draw(s1, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(s1, 10)) / 2, SpsMiniY + 48, 10, Palette.SpsShieldArcIce);
                UiText.Draw(s2, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(s2, 10)) / 2, SpsMiniY + 62, 10, Palette.SpsShieldArcIce);
            }
            else if (_activeShip != null && _activeShip.GroundTruth.IsDay2GhostRouteSuspect)
            {
                // Uncanny non-violating sensor cue on Day 2 Ghost Route ship:
                // Abnormally warm warp pylons with ZERO [!] FAULT entries logged
                const string g1 = "PYLON WARM";
                const string g2 = "RESIDUE HIGH";
                const string g3 = "0 FAULTS";
                UiText.Draw(g1, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(g1, 10)) / 2, SpsMiniY + 46, 10, Palette.CrtAmberPrimary);
                UiText.Draw(g2, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(g2, 10)) / 2, SpsMiniY + 62, 10, Palette.CrtAmberDimmed);
                UiText.Draw(g3, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(g3, 10)) / 2, SpsMiniY + 78, 10, Palette.ClearanceTeal);
            }
            else
            {
                const string c1 = "SCANNING";
                const string c2 = "HOLD 0.5S";
                const string c3 = "ON SIGNAL";
                UiText.Draw(c1, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(c1, 10)) / 2, SpsMiniY + 48, 10, Palette.SpsCommsActive);
                UiText.Draw(c2, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(c2, 10)) / 2, SpsMiniY + 64, 10, Palette.CrtAmberDimmed);
                UiText.Draw(c3, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(c3, 10)) / 2, SpsMiniY + 78, 10, Palette.CrtAmberDimmed);
            }
        }
        else
        {
            const string st = "STANDBY";
            UiText.Draw(st, SpsMiniX + (SpsMiniW - UiText.MeasureWidth(st, 10)) / 2, SpsMiniY + 68, 10, Palette.WeatheredGunmetalEdge);
        }
    }

    // =========================================================================
    // 5. Hailing Viewscreen & Transponder ID (A: Classic 2x | B1/B2: 3x 192px or 2x 128px + Separate Panel)
    // =========================================================================
    private void GetCurrentFeedStatus(out string feedLine, out Color feedDotCol)
    {
        if (_preShiftStage is PreShiftStage.Day1VorstLiveHail or PreShiftStage.Day3KordaniLiveHail)
        {
            feedLine = "LIVE FEED // KDF COMMAND";
            feedDotCol = Palette.AlertCrimson;
        }
        else if (IsPreShiftBrengopHailActive)
        {
            feedLine = "LIVE FEED // BMC LOGISTICS";
            feedDotCol = Palette.CrtAmberPrimary;
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
    }

    private void DrawHailingViewscreen(VirtualMouseState mouse)
    {
        _ = mouse;
        DeskRect vs = _layout.Viewscreen;
        DeskRect pr = _layout.Portrait;
        GetCurrentFeedStatus(out string feedLine, out Color feedDotCol);

        float viewscreenCollapseT = Math.Clamp(ClockOutPowerDownProgress / 0.28f, 0f, 1f);

        // Outer Viewscreen Housing
        Color preShiftAccentCol = IsPreShiftBrengopHailActive ? Palette.CrtAmberPrimary : Palette.AlertCrimson;
        Raylib.DrawRectangle(vs.X, vs.Y, vs.W, vs.H, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(vs.X, vs.Y, vs.W, vs.H,
            IsPreShiftTalkingHeadHailActive ? preShiftAccentCol : Palette.OxidizedBronzeWorn);

        if (_layout.IsTransponderSeparatePanel)
        {
            // Subtle armored bezel corner rivets for dedicated B1/B2 Hailing Viewscreen
            Raylib.DrawRectangle(vs.X + 4, vs.Y + 4, 2, 2, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(vs.Right - 6, vs.Y + 4, 2, 2, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(vs.X + 4, vs.Bottom - 6, 2, 2, Palette.WeatheredGunmetalEdge);
            Raylib.DrawRectangle(vs.Right - 6, vs.Bottom - 6, 2, 2, Palette.WeatheredGunmetalEdge);

            if (!_layout.Use3xPortrait)
            {
                // B1/B2 2x Mode: Clean diegetic viewscreen bezel framing the 128x128 portrait at (1098, 72)
                // + LIVE FEED / AUDIO ONLY status bar directly underneath
                int bzX = vs.X + 10;
                int bzY = vs.Y + 8;
                int bzW = vs.W - 20;
                int bzH = vs.H - 16;
                Raylib.DrawRectangle(bzX, bzY, bzW, bzH, new Color(22, 26, 32, 255));
                Raylib.DrawRectangleLines(bzX, bzY, bzW, bzH, Palette.WeatheredGunmetalEdge);

                const string bezelTitle = "HAILING VIEWSCREEN";
                int btw = UiText.MeasureWidth(bezelTitle, 10);
                UiText.Draw(bezelTitle, vs.X + (vs.W - btw) / 2, bzY + 6, 10, Palette.CrtAmberDimmed);

                // Subtle CRT bracket ticks flanking the 128x128 portrait
                Raylib.DrawLine(pr.X - 6, pr.Y, pr.X - 6, pr.Y + 14, Palette.OxidizedBronzeWorn);
                Raylib.DrawLine(pr.Right + 5, pr.Y, pr.Right + 5, pr.Y + 14, Palette.OxidizedBronzeWorn);
                Raylib.DrawLine(pr.X - 6, pr.Bottom - 14, pr.X - 6, pr.Bottom, Palette.OxidizedBronzeWorn);
                Raylib.DrawLine(pr.Right + 5, pr.Bottom - 14, pr.Right + 5, pr.Bottom, Palette.OxidizedBronzeWorn);

                // Status bar cleanly underneath the 2x portrait (1072, 212, 176, 26)
                int sbX = vs.X + 20;
                int sbY = pr.Bottom + 12;
                int sbW = vs.W - 40;
                const int sbH = 26;
                Raylib.DrawRectangle(sbX, sbY, sbW, sbH, Palette.DeepSpaceVoid);
                Raylib.DrawRectangleLines(sbX, sbY, sbW, sbH, Palette.WeatheredGunmetalEdge);
                Raylib.DrawRectangle(sbX + 8, sbY + 10, 6, 6, feedDotCol);
                string shortFeed = _preShiftStage switch
                {
                    PreShiftStage.Day1VorstLiveHail or PreShiftStage.Day3KordaniLiveHail => "LIVE FEED // VORST",
                    PreShiftStage.Day2NixaLiveHail or PreShiftStage.Day8NixaLiveHail or PreShiftStage.Day15ForemanLiveHail => "LIVE FEED // NIXA",
                    _ => feedLine
                };
                UiText.Draw(TruncateByPixelWidth(shortFeed, 10, sbW - 24), sbX + 20, sbY + 8, 10,
                    IsPreShiftTalkingHeadHailActive ? preShiftAccentCol : Palette.CrtAmberPrimary);
                if (viewscreenCollapseT > 0.001f)
                {
                    DrawCrtPowerDownCollapse(sbX + 1, sbY + 1, sbW - 2, sbH - 2, viewscreenCollapseT);
                }
            }
        }

        // Portrait Frame (crisp nearest-neighbor 64x64 -> 128x128 [2x] or 192x192 [3x])
        Raylib.DrawRectangle(pr.X, pr.Y, pr.W, pr.H, Palette.DeepSpaceVoid);

        bool viewscreenOpen = _activeShip != null &&
                              _phase != EncounterPhase.ShipGlidingIn &&
                              _cachedLivePortraitTex.Id != 0;

        if (IsPreShiftTalkingHeadHailActive && _cachedPreShiftKordaniPortraitTex.Id != 0)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = pr.ToRaylib();
            Raylib.DrawTexturePro(_cachedPreShiftKordaniPortraitTex, src, dst, Vector2.Zero, 0f, Color.White);
        }
        else if (viewscreenOpen)
        {
            Rectangle src = new(0, 0, Constants.PortraitWidth, Constants.PortraitHeight);
            Rectangle dst = pr.ToRaylib();
            Raylib.DrawTexturePro(_cachedLivePortraitTex, src, dst, Vector2.Zero, 0f, Color.White);

            // Brief static flicker on captain's viewscreen during staggered confrontation reaction pause
            if (_portraitStaticFlickerTimer > 0f)
            {
                DrawPortraitStaticFlickerOverlay(pr.X, pr.Y, pr.W, pr.H);
            }
        }
        else
        {
            const string s1 = "VIEWSCREEN";
            const string s2 = "STANDBY";
            int midY = pr.Y + pr.H / 2;
            UiText.Draw(s1, pr.X + (pr.W - UiText.MeasureWidth(s1, 10)) / 2, midY - 12, 10, Palette.CrtAmberDimmed);
            UiText.Draw(s2, pr.X + (pr.W - UiText.MeasureWidth(s2, 10)) / 2, midY + 4, 10, Palette.WeatheredGunmetalEdge);
        }

        if (viewscreenCollapseT > 0.001f)
        {
            DrawCrtPowerDownCollapse(pr.X, pr.Y, pr.W, pr.H, viewscreenCollapseT);
        }

        Raylib.DrawRectangleLines(pr.X - 1, pr.Y - 1, pr.W + 2, pr.H + 2,
            IsPreShiftTalkingHeadHailActive ? preShiftAccentCol : Palette.CrtAmberDimmed);
    }

    private void DrawTransponderIdPanel(VirtualMouseState mouse)
    {
        // Rendered in Step 5 (after DrawLowerDeskSurfaceAndConduit) so BOTH Classic A (688,184,204,64)
        // AND B1/B2 standalone Row 2 panel (1052,264,220,88) sit cleanly above the lower desk surface!
        DeskRect fb = _layout.FeedBox;
        GetCurrentFeedStatus(out string feedLine, out Color feedDotCol);
        float viewscreenCollapseT = Math.Clamp(ClockOutPowerDownProgress / 0.28f, 0f, 1f);
        Color preShiftAccentCol = IsPreShiftBrengopHailActive ? Palette.CrtAmberPrimary : Palette.AlertCrimson;

        bool idFault = IsTargetFaultHighlighted(ReviewTarget.VisualSignalTransponderId);
        bool idSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.VisualSignalTransponderId;

        Raylib.DrawRectangle(fb.X, fb.Y, fb.W, fb.H,
            idFault ? Palette.BrownDwarfGlowDeep : (idSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid));
        Raylib.DrawRectangleLines(fb.X, fb.Y, fb.W, fb.H,
            idFault
                ? GetFaultPulseColor()
                : (idSelected
                    ? Palette.ClearanceTeal
                    : (_layout.IsTransponderSeparatePanel ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge)));

        if (idFault)
        {
            Raylib.DrawRectangleLines(fb.X - 1, fb.Y - 1, fb.W + 2, fb.H + 2, GetFaultPulseColor());
        }
        else if (_protocolReviewModeActive && _activeShip != null && IsShipInspectableForReview)
        {
            DrawReviewHotspotOutline(mouse, fb.X, fb.Y, fb.W, fb.H, idSelected);
        }

        if (_layout.IsTransponderSeparatePanel)
        {
            // Dedicated B1/B2 SIGNAL / TRANSPONDER ID Readout Panel: (1052, 264, 220, 88)
            Raylib.DrawRectangle(fb.X + 1, fb.Y + 1, fb.W - 2, 20, Palette.OutlineVoid);
            Raylib.DrawRectangle(fb.X + 8, fb.Y + 8, 6, 6, feedDotCol);
            UiText.Draw(TruncateByPixelWidth(feedLine, 10, fb.W - 24), fb.X + 18, fb.Y + 6, 10,
                IsPreShiftTalkingHeadHailActive ? preShiftAccentCol : Palette.CrtAmberDimmed);
            Raylib.DrawLine(fb.X + 2, fb.Y + 22, fb.Right - 2, fb.Y + 22, Palette.WeatheredGunmetalEdge);

            UiText.Draw("SIGNAL / TRANSPONDER ID", fb.X + 8, fb.Y + 28, 10, Palette.WeatheredGunmetalEdge);
        }
        else
        {
            // Classic A embedded FeedBox: (688, 184, 204, 64)
            Raylib.DrawRectangle(fb.X + 8, fb.Y + 8, 6, 6, feedDotCol);
            UiText.Draw(TruncateByPixelWidth(feedLine, 10, fb.W - 24), fb.X + 18, fb.Y + 6, 10,
                IsPreShiftTalkingHeadHailActive ? preShiftAccentCol : Palette.CrtAmberDimmed);
            Raylib.DrawLine(fb.X + 4, fb.Y + 21, fb.Right - 4, fb.Y + 21, Palette.WeatheredGunmetalEdge);

            UiText.Draw("TRANSPONDER ID", fb.X + 8, fb.Y + 25, 10, Palette.WeatheredGunmetalEdge);
        }

        string rawIdText = _preShiftStage switch
        {
            PreShiftStage.Day1VorstLiveHail or PreShiftStage.Day3KordaniLiveHail => Day2And3ScriptedDialogue.Day1VorstTransponder,
            PreShiftStage.Day2NixaLiveHail or PreShiftStage.Day8NixaLiveHail or PreShiftStage.Day15ForemanLiveHail => Day2And3ScriptedDialogue.RegeerNixaTransponder,
            _ => _activeShip != null && _phase != EncounterPhase.ShipGlidingIn
                ? _activeShip.GroundTruth.RawTransponderId
                : "---"
        };

        bool isGhostFlickering = _ghostRouteTransponderFlickerTimer > 0f &&
                                 _activeShip != null &&
                                 _activeShip.GroundTruth.IsDay2GhostRouteSuspect &&
                                 ((_animTick / 2) % 2 == 0);
        if (isGhostFlickering && rawIdText.Length >= 7)
        {
            rawIdText = $"{rawIdText[..4]}??{rawIdText[6..]}";
        }

        Color idCol = idFault
            ? GetFaultPulseColor()
            : (isGhostFlickering
                ? Palette.CrtAmberDimmed
                : (idSelected ? Palette.ClearanceTeal : Palette.CrtAmberPrimary));

        int idFontSize = UiText.MeasureWidth(rawIdText, 20) <= fb.W - 16 ? 20 : 10;
        int idDrawY = _layout.IsTransponderSeparatePanel
            ? (idFontSize == 20 ? fb.Y + 46 : fb.Y + 51)
            : (idFontSize == 20 ? fb.Y + 39 : fb.Y + 44);
        UiText.Draw(rawIdText, fb.X + 8, idDrawY, idFontSize, idCol);

        // Subtle signal carrier bar along bottom edge of B1/B2 88px Signal/Transponder panel
        if (_layout.IsTransponderSeparatePanel)
        {
            bool carrierLocked = IsPreShiftTalkingHeadHailActive ||
                                 (_activeShip != null && _phase != EncounterPhase.ShipGlidingIn);
            int barY = fb.Bottom - 11;
            const int segCount = 12;
            for (int s = 0; s < segCount; s++)
            {
                int sx = fb.X + 8 + s * 17;
                Color segCol;
                if (!carrierLocked)
                {
                    segCol = new Color(24, 29, 36, 255);
                }
                else if (idFault)
                {
                    segCol = Palette.AlertCrimson;
                }
                else if (IsPreShiftTalkingHeadHailActive)
                {
                    Color barCol = IsPreShiftBrengopHailActive ? Palette.CrtAmberPrimary : Palette.AlertCrimson;
                    segCol = s < 10 ? barCol : Palette.OxidizedBronzeShadow;
                }
                else
                {
                    int activeBars = _activeShip != null && _activeShip.GroundTruth.UsesStaticViewscreen ? 6 : 11;
                    segCol = s < activeBars
                        ? (s < 8 ? Palette.ClearanceTeal : Palette.CrtAmberDimmed)
                        : new Color(24, 29, 36, 255);
                }
                Raylib.DrawRectangle(sx, barY, 13, 4, segCol);
            }
        }

        if (viewscreenCollapseT > 0.001f)
        {
            DrawCrtPowerDownCollapse(fb.X + 1, fb.Y + 1, fb.W - 2, fb.H - 2, viewscreenCollapseT);
        }
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
    // 7. Lower Desk Surface, One-Way Data Conduit Cable & Subtle Lap Pocket
    // =========================================================================
    private void DrawLowerDeskSurfaceAndConduit()
    {
        // Flat top-down dark industrial gunmetal desk mat
        Raylib.DrawRectangle(0, 262, Constants.VirtualWidth, Constants.VirtualHeight - 262, new Color(30, 36, 44, 255));

        // Subtle recessed desk mat seams aligned dynamically with strict 8px gutters
        Raylib.DrawLine(8, 356, _layout.DeskSeamRightX, 356, new Color(22, 27, 34, 255));
        Raylib.DrawLine(_layout.DeskSeamVerticalX, 356, _layout.DeskSeamVerticalX, 712, new Color(22, 27, 34, 255));

        // Contraband Lap Pocket: (8, 656, 280, 56) -- subtle dark unlit beveled lip flush with x = 8 and y = 712
        bool dragStowReady = _draggingItemId == DeskItemId.ContrabandSlate &&
                             _activeDeskContrabandItem != null &&
                             !_activeDeskContrabandItem.IsBurnt &&
                             _gameState.PocketItems.Count < _gameState.PocketCapacity;
        Raylib.DrawRectangle(LapPocketX, LapPocketY, LapPocketW, LapPocketH, new Color(18, 22, 28, 255));
        Raylib.DrawRectangle(LapPocketX + 3, LapPocketY + 6, LapPocketW - 6, LapPocketH - 6, new Color(12, 15, 20, 255));
        Raylib.DrawRectangle(LapPocketX + 6, LapPocketY + 14, LapPocketW - 12, LapPocketH - 14, Palette.OutlineVoid);
        // Subtle unlit beveled top & side lip (subtle dark bronze highlight only while stowing a Contraband Slate)
        Color lipTopCol = dragStowReady ? Palette.OxidizedBronzeWorn : new Color(46, 54, 65, 255);
        Raylib.DrawLine(LapPocketX, LapPocketY, LapPocketX + LapPocketW - 1, LapPocketY, lipTopCol);
        Raylib.DrawLine(LapPocketX, LapPocketY + 1, LapPocketX + LapPocketW - 1, LapPocketY + 1, Palette.OutlineVoid);
        Raylib.DrawLine(LapPocketX, LapPocketY, LapPocketX, LapPocketY + LapPocketH - 1, new Color(36, 43, 52, 255));
        Raylib.DrawLine(LapPocketX + LapPocketW - 1, LapPocketY, LapPocketX + LapPocketW - 1, LapPocketY + LapPocketH - 1, new Color(14, 17, 22, 255));
        Raylib.DrawLine(LapPocketX, LapPocketY + LapPocketH - 1, LapPocketX + LapPocketW - 1, LapPocketY + LapPocketH - 1, new Color(14, 17, 22, 255));

        // Subtle physical Contraband Slate edges peeking cleanly from inside the unlit lap pocket lip (1..3 slots)
        int cap = Math.Clamp(_gameState.PocketCapacity, 1, 3);
        int slotSpan = (LapPocketW - 16) / cap;
        for (int i = 0; i < _gameState.PocketItems.Count && i < cap; i++)
        {
            ContrabandPocketItem pItem = _gameState.PocketItems[i];
            int px = LapPocketX + 8 + i * slotSpan + 2;
            int pw = Math.Max(48, slotSpan - 6);
            int py = LapPocketY + 16;
            int ph = LapPocketH - 22;

            Color bodyCol = pItem.ItemType switch
            {
                ContrabandPocketItem.TypeBlankDataSlate => new Color(44, 42, 34, 255),
                ContrabandPocketItem.TypeCopiedDataSlate => new Color(56, 44, 24, 255),
                ContrabandPocketItem.TypeMissionDataSlate => new Color(36, 46, 30, 255),
                _ => Palette.WeatheredGunmetalMid
            };
            Color rimCol = pItem.ItemType switch
            {
                ContrabandPocketItem.TypeCopiedDataSlate => Palette.CrtAmberPrimary,
                ContrabandPocketItem.TypeMissionDataSlate => Palette.AlertCrimson,
                _ => Palette.OxidizedBronzeWorn
            };
            string shortTag = pItem.ItemType switch
            {
                ContrabandPocketItem.TypeBlankDataSlate => "BLANK",
                ContrabandPocketItem.TypeCopiedDataSlate => string.IsNullOrEmpty(pItem.TargetShipPrefix) ? "STOLEN" : pItem.TargetShipPrefix.TrimEnd('-'),
                ContrabandPocketItem.TypeMissionDataSlate => "OP SLATE",
                _ => "SLATE"
            };

            Raylib.DrawRectangle(px, py, pw, ph, bodyCol);
            Raylib.DrawRectangleLines(px, py, pw, ph, rimCol);
            UiText.Draw(TruncateByPixelWidth(shortTag, 10, pw - 8), px + 5, py + 11, 10, rimCol);
        }

        // Strictly One-Way Armored Data Conduit Cable: Slate Bus -> Wafer Writer
        DrawArmoredDataConduitCable();
    }

    private void DrawArmoredDataConduitCable()
    {
        // Strictly One-Way Receive-Only Conduit (Slate Bus -> Wafer Writer):
        // - Inactive State (Days 1-11 / Wafer Writer offline): completely dark & inert, NEVER animates pulses!
        // - Active State (Day 12+): pulses warm amber ONLY downward during incoming protocol download/demux (RX PACKET),
        //   NEVER during Clearance Dock encryption, Slate Bus TX CLEARANCE upload, Blank Slate port-swap, or Detaining.
        bool waferOnline = IsWaferWriterOperational;
        bool pulseRxActive = IsConduitPulseRxActive;

        int startX = _layout.ConduitStartX;
        int startY = _layout.ConduitStartY;
        int gutterX = _layout.ConduitGutterX;
        int bendY = _layout.ConduitBendY;
        int endX = _layout.ConduitEndX;
        int endY = _layout.ConduitEndY;

        Color sheathCol = waferOnline ? new Color(46, 55, 66, 255) : new Color(32, 38, 46, 255);
        Color fiberCore = waferOnline ? new Color(48, 36, 22, 255) : new Color(16, 20, 26, 255);
        Color clampCol = waferOnline ? Palette.WeatheredGunmetalEdge : new Color(46, 54, 64, 255);

        if (_layout.IsConduitStraightDrop)
        {
            // B1 / B2: Straight vertical drop from bottom-right shoulder of Slate Bus (966, 352)
            // directly into top-right shoulder of Wafer Writer (966, 624), clear of docked Data Slate & Wipe Feed Slot.
            int dropH = Math.Max(4, endY - startY);
            Raylib.DrawRectangle(startX - 3, startY, 6, dropH, Palette.OutlineVoid);
            Raylib.DrawRectangle(startX - 2, startY, 4, dropH, sheathCol);
            Raylib.DrawRectangle(startX - 1, startY, 2, dropH, fiberCore);

            if (pulseRxActive)
            {
                int segOffset = (_animTick * 3) % 24;
                for (int y = startY + segOffset; y < endY; y += 24)
                {
                    int h = Math.Min(10, endY - y);
                    Raylib.DrawRectangle(startX - 1, y, 2, h, Palette.CrtAmberPrimary);
                }
            }

            // Top & bottom intake grommets + steel mounting clamps
            Raylib.DrawRectangle(startX - 4, startY, 8, 4, clampCol);
            Raylib.DrawRectangle(endX - 4, endY - 4, 8, 4, clampCol);
            for (int cy = startY + 48; cy < endY - 32; cy += 68)
            {
                Raylib.DrawRectangle(startX - 4, cy, 8, 4, clampCol);
                Raylib.DrawRectangleLines(startX - 4, cy, 8, 4, Palette.OutlineVoid);
            }
            return;
        }

        // Classic A: Exits right edge of Slate Bus (900, 286) -> drops down 8px gutter x = 904 ->
        // turns into Wafer Writer top-right shoulder (894, 624)
        Raylib.DrawRectangle(startX, startY - 3, gutterX - startX + 3, 6, Palette.OutlineVoid);
        Raylib.DrawRectangle(gutterX - 3, startY - 3, 6, bendY - startY + 6, Palette.OutlineVoid);
        Raylib.DrawRectangle(endX - 3, bendY - 3, gutterX - endX + 6, 6, Palette.OutlineVoid);
        Raylib.DrawRectangle(endX - 3, bendY - 3, 6, endY - bendY + 3, Palette.OutlineVoid);

        Raylib.DrawRectangle(startX, startY - 2, gutterX - startX + 2, 4, sheathCol);
        Raylib.DrawRectangle(gutterX - 2, startY - 2, 4, bendY - startY + 4, sheathCol);
        Raylib.DrawRectangle(endX - 2, bendY - 2, gutterX - endX + 4, 4, sheathCol);
        Raylib.DrawRectangle(endX - 2, bendY - 2, 4, endY - bendY + 2, sheathCol);

        Raylib.DrawRectangle(startX, startY - 1, gutterX - startX + 1, 2, fiberCore);
        Raylib.DrawRectangle(gutterX - 1, startY - 1, 2, bendY - startY + 2, fiberCore);
        Raylib.DrawRectangle(endX - 1, bendY - 1, gutterX - endX + 2, 2, fiberCore);
        Raylib.DrawRectangle(endX - 1, bendY - 1, 2, endY - bendY + 1, fiberCore);

        if (pulseRxActive)
        {
            int segOffset = (_animTick * 3) % 24;
            Raylib.DrawRectangle(startX, startY - 1, gutterX - startX + 1, 2, Palette.CrtAmberPrimary);
            for (int y = startY + segOffset; y < bendY; y += 24)
            {
                int h = Math.Min(10, bendY - y);
                Raylib.DrawRectangle(gutterX - 1, y, 2, h, Palette.CrtAmberPrimary);
            }
            if ((segOffset % 24) < 12)
            {
                Raylib.DrawRectangle(endX - 1, bendY - 1, gutterX - endX + 2, 2, Palette.CrtAmberPrimary);
            }
            for (int y = bendY + (segOffset % 14); y < endY; y += 14)
            {
                int h = Math.Min(7, endY - y);
                Raylib.DrawRectangle(endX - 1, y, 2, h, Palette.CrtAmberPrimary);
            }
        }

        Raylib.DrawRectangle(startX, startY - 4, 3, 8, clampCol);
        Raylib.DrawRectangle(endX - 4, endY - 4, 8, 4, clampCol);
        for (int cy = 368; cy < bendY - 24; cy += 68)
        {
            Raylib.DrawRectangle(gutterX - 4, cy, 8, 4, clampCol);
            Raylib.DrawRectangleLines(gutterX - 4, cy, 8, 4, Palette.OutlineVoid);
        }
    }

    // =========================================================================
    // 7b. Wafer Writer (A: 680,624,220,88 | B1/B2: 752,624,220,88) + Top Wipe Feed Slot
    // =========================================================================
    private void DrawWaferWriterModule(VirtualMouseState mouse)
    {
        DeskRect ww = _layout.WaferWriter;
        DeskRect slot = _layout.WaferFeedSlot;

        if (!IsWaferWriterOperational)
        {
            DrawBrushedMetalFillerPlate(ww.X, ww.Y, ww.W, ww.H);
        }
        else
        {
            Raylib.DrawRectangle(ww.X, ww.Y, ww.W, ww.H, Palette.OutlineVoid);
            Raylib.DrawRectangle(ww.X + 1, ww.Y + 1, ww.W - 2, ww.H - 2, Palette.WeatheredGunmetalMid);
            Color bezelBorder = _waferWriteHeadFlashTimer > 0f
                ? new Color(196, 152, 108, 255)
                : (_heldPendingWafer != null || _waferHopperCount == 0 ? Palette.AlertCrimson : Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangleLines(ww.X, ww.Y, ww.W, ww.H, bezelBorder);
        }

        // Visible Wafer Wipe Feed Slot along TOP edge (w = 196, h = 16)
        bool wipeFlashing = IsWaferWriterOperational && _waferWipeFlashTimer > 0f;
        Raylib.DrawRectangle(slot.X, slot.Y, slot.W, slot.H,
            wipeFlashing ? new Color(242, 246, 252, 255) : new Color(18, 22, 28, 255));
        Raylib.DrawRectangleLines(slot.X, slot.Y, slot.W, slot.H,
            wipeFlashing
                ? Color.White
                : (IsWaferWriterOperational ? Palette.OxidizedBronzeWorn : new Color(48, 56, 68, 255)));
        Raylib.DrawRectangle(slot.X + 10, slot.Y + 5, slot.W - 20, 6,
            wipeFlashing ? Color.White : Palette.OutlineVoid);
        Raylib.DrawLine(slot.X + 10, slot.Y + 11, slot.Right - 11, slot.Y + 11,
            wipeFlashing
                ? Palette.ClearanceTeal
                : (IsWaferWriterOperational ? Palette.CrtAmberDimmed : new Color(42, 49, 60, 255)));
        Raylib.DrawRectangle(slot.X + 4, slot.Y + 6, 3, 4, new Color(48, 56, 68, 255));
        Raylib.DrawRectangle(slot.Right - 7, slot.Y + 6, 3, 4, new Color(48, 56, 68, 255));

        if (!IsWaferWriterOperational)
        {
            DrawInactiveStencilText(ww.X, ww.Y + slot.H, ww.W, ww.H - slot.H,
                "WAFER WRITER", "[PLAYTEST]");
            return;
        }

        // Left-side ejection tray lip & muted-brown Cargo Manifest write-head flash bezel (zero punch-press arm!)
        int trayX = ww.X + 4;
        int trayY = ww.Y + 22;
        int trayH = ww.H - 28;
        Color writeHeadCol = _waferWriteHeadFlashTimer > 0f
            ? new Color(208, 166, 122, 255)
            : (IsConduitPulseRxActive ? new Color(138, 102, 72, 255) : new Color(54, 48, 44, 255));
        Raylib.DrawRectangle(trayX, trayY, 8, trayH, Palette.OutlineVoid);
        Raylib.DrawRectangle(trayX + 1, trayY + 2, 5, trayH - 4, writeHeadCol);
        Raylib.DrawRectangleLines(trayX, trayY, 8, trayH, Palette.WeatheredGunmetalEdge);

        // 3-Wafer Ceramic-Polymer Hopper Indicator Pips + Empty-Hopper Flashing Error LED
        bool pulseLed = ((_animTick / 8) % 2) == 0;
        int ledX = ww.Right - 14;
        int ledY = ww.Y + 22;
        Color hopperLedCol = (_waferHopperCount == 0 || _heldPendingWafer != null)
            ? (pulseLed ? Palette.AlertCrimson : new Color(92, 20, 24, 255))
            : Palette.ClearanceTeal;
        Raylib.DrawRectangle(ledX, ledY, 8, 8, hopperLedCol);
        Raylib.DrawRectangleLines(ledX, ledY, 8, 8, Palette.OutlineVoid);

        for (int p = 0; p < MaxWaferHopperCount; p++)
        {
            int pipX = ww.Right - 46 + p * 10;
            int pipY = ww.Y + 22;
            bool hasBlank = p < _waferHopperCount;
            Raylib.DrawRectangle(pipX, pipY, 8, 8, hasBlank ? new Color(196, 190, 176, 255) : Palette.OutlineVoid);
            Raylib.DrawRectangleLines(pipX, pipY, 8, 8, Palette.WeatheredGunmetalEdge);
        }

        const string title = "WAFER DEMUX";
        UiText.Draw(title, ww.X + 18, ww.Y + 21, 10, Palette.CrtAmberPrimary, denseRow: true);

        bool missingPacketActive = _activeShip != null &&
                                   _activeShip.GroundTruth.ExpectsCargoManifestWafer &&
                                   _activeShip.GroundTruth.CargoManifestInitiallyOmitted &&
                                   !_activeShip.GroundTruth.CargoManifestTransmitted &&
                                   !_isTransmittingMissingWafer &&
                                   IsShipInspectableForReview;

        string statusLine;
        Color statusCol;
        if (_heldPendingWafer != null || _waferHopperCount == 0)
        {
            statusLine = _heldPendingWafer != null ? "HOPPER EMPTY // HOLD" : "HOPPER: 0/3 BLANKS";
            statusCol = Palette.AlertCrimson;
        }
        else if (IsConduitPulseRxActive)
        {
            statusLine = "DEMUXING MANIFEST...";
            statusCol = Palette.ClearanceTeal;
        }
        else if (missingPacketActive)
        {
            statusLine = "NO CARGO PACKET RX";
            statusCol = Palette.AlertCrimson;
        }
        else
        {
            statusLine = $"BLANKS: {_waferHopperCount}/{MaxWaferHopperCount} // TOP WIPE";
            statusCol = Palette.CrtAmberDimmed;
        }

        UiText.Draw(
            UiText.TruncateToWidth(statusLine, 10, ww.W - 24, denseRow: true),
            ww.X + 18,
            ww.Y + 40,
            10,
            statusCol,
            denseRow: true);

        const string slotHint = "^ DROP WAFER IN TOP SLOT ^";
        UiText.Draw(
            UiText.TruncateToWidth(slotHint, 10, ww.W - 24, denseRow: true),
            ww.X + 18,
            ww.Y + 58,
            10,
            wipeFlashing ? Color.White : Palette.WeatheredGunmetalEdge,
            denseRow: true);

        if (IsTargetFaultHighlighted(ReviewTarget.WaferWriterModule))
        {
            Color col = GetFaultPulseColor();
            Raylib.DrawRectangleLines(ww.X, ww.Y, ww.W, ww.H, col);
            Raylib.DrawRectangleLines(ww.X - 1, ww.Y - 1, ww.W + 2, ww.H + 2, col);
        }
        else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.WaferWriterModule))
        {
            DrawReviewHotspotOutline(mouse, ww.X, ww.Y, ww.W, ww.H, _reviewFirstSelection == ReviewTarget.WaferWriterModule);
        }
    }

    // =========================================================================
    // 8. Row 2 Hardware: Warp Printer (112, 264, 320, 88) & Glowing Warp Checker (440, 264, 232, 88)
    // =========================================================================
    private void DrawWarpResiduePrinterAndRefSpot(VirtualMouseState mouse)
    {
        if (!IsSpectrometerOperational)
        {
            string dayTag = $"DAY {_config.DayUnlockWarpScanner:D2}";
            DrawInactiveHardwareBay(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH, "WARP PRINTER", dayTag);
            DrawInactiveHardwareBay(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH, "WARP CHECKER", dayTag);
            return;
        }

        bool warpPoweredOff = ClockOutPowerDownProgress >= 0.58f;

        // Day 3+: Warp Residue Printer + L->R Tray: (112, 264, 320, 88)
        Raylib.DrawRectangle(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(WarpPrinterX, WarpPrinterY, WarpPrinterW, WarpPrinterH,
            warpPoweredOff ? Palette.WeatheredGunmetalEdge : Palette.ClearanceTeal);

        // Left Printer Housing: (112, 264, 88, 88)
        Raylib.DrawRectangle(WarpPrinterX + 1, WarpPrinterY + 1, WarpHousingW - 2, WarpPrinterH - 2, Palette.WeatheredGunmetalMid);
        Raylib.DrawLine(WarpPrinterX + WarpHousingW - 1, WarpPrinterY + 1, WarpPrinterX + WarpHousingW - 1, WarpPrinterY + WarpPrinterH - 2, Palette.OutlineVoid);
        const string prnTitle = "WARP COIL";
        UiText.Draw(prnTitle, WarpPrinterX + (WarpHousingW - UiText.MeasureWidth(prnTitle, 10)) / 2, WarpPrinterY + 5, 10,
            warpPoweredOff ? Palette.WeatheredGunmetalEdge : Palette.ClearanceTeal);

        // Compact status screen inside left housing (80x32)
        int scrX = WarpPrinterX + 4;
        int scrY = WarpPrinterY + 18;
        int scrW = WarpHousingW - 8;
        const int scrH = 34;
        Raylib.DrawRectangle(scrX, scrY, scrW, scrH, warpPoweredOff ? new Color(8, 10, 14, 255) : Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(scrX, scrY, scrW, scrH, warpPoweredOff ? Palette.WeatheredGunmetalEdge : Palette.CrtAmberDimmed);

        if (!warpPoweredOff)
        {
            if (_warpPhase == WarpPrinterPhase.ScanningWarpField)
            {
                float sweep = Math.Clamp(_warpPhaseElapsed / WarpScanDurationSeconds, 0f, 1f);
                int barW = Math.Max(1, (int)MathF.Round((scrW - 8) * sweep));
                Raylib.DrawRectangle(scrX + 4, scrY + 6, barW, 6, Palette.ClearanceTeal);
                UiText.Draw("SCANNING", scrX + 12, scrY + 18, 10, Palette.ClearanceTeal);
            }
            else if (_warpPhase == WarpPrinterPhase.PrintingStrip)
            {
                UiText.Draw("PRINTING", scrX + 12, scrY + 6, 10, Palette.CrtAmberPrimary);
                UiText.Draw("L -> R", scrX + 20, scrY + 19, 10, Palette.CrtAmberDimmed);
            }
            else if (_warpPhase == WarpPrinterPhase.AnchoredReadyToTear)
            {
                UiText.Draw("READY", scrX + 20, scrY + 6, 10, Palette.ClearanceTeal);
                UiText.Draw("TEAR ->", scrX + 14, scrY + 19, 10, Palette.CrtAmberPrimary);
            }
            else if (_warpPhase == WarpPrinterPhase.TornOffFreeDocument)
            {
                UiText.Draw("COIL HOT", scrX + 12, scrY + 6, 10, Palette.ClearanceTeal);
                UiText.Draw("STRIP OUT", scrX + 8, scrY + 19, 10, Palette.CrtAmberDimmed);
            }
            else
            {
                UiText.Draw("ONLINE", scrX + 18, scrY + 6, 10, Palette.ClearanceTeal);
                UiText.Draw("READY", scrX + 20, scrY + 19, 10, Palette.CrtAmberDimmed);
            }
        }

        bool canScan = !warpPoweredOff && CanStartManualWarpScan();
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
        // Clicks off into dark unlit smoked glass during shift-end power-down beat (p >= 0.58)
        Raylib.DrawRectangle(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH,
            warpPoweredOff ? new Color(16, 18, 22, 255) : new Color(24, 20, 14, 255));
        Raylib.DrawRectangleLines(WarpRefSpotX, WarpRefSpotY, WarpRefSpotW, WarpRefSpotH,
            warpPoweredOff ? Palette.WeatheredGunmetalEdge : Palette.CrtAmberPrimary);
        Raylib.DrawRectangleLines(WarpRefSpotX + 2, WarpRefSpotY + 2, WarpRefSpotW - 4, WarpRefSpotH - 4,
            warpPoweredOff ? Palette.OutlineVoid : Palette.OxidizedBronzeWorn);

        // Selector Bar: (448, 270, 216, 22)
        StarSystemSignatureInfo sys = WarpSignatureGenerator.SectorSystems[
            Math.Clamp(_warpRefSpotSystemIndex, 0, WarpSignatureGenerator.SectorSystems.Length - 1)];
        DrawSmallButton(mouse, WarpSelectorX, WarpSelectorY, 24, WarpSelectorH, "<");
        Raylib.DrawRectangle(WarpSelectorX + 26, WarpSelectorY, WarpSelectorW - 52, WarpSelectorH, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(WarpSelectorX + 26, WarpSelectorY, WarpSelectorW - 52, WarpSelectorH, Palette.OxidizedBronzeShadow);
        string sysTitle = warpPoweredOff ? "REF: OFFLINE" : $"REF: {sys.DisplayLabel}";
        int stw = UiText.MeasureWidth(sysTitle, 10);
        UiText.Draw(sysTitle, WarpSelectorX + (WarpSelectorW - stw) / 2, WarpSelectorY + (WarpSelectorH - 10) / 2, 10,
            warpPoweredOff ? Palette.WeatheredGunmetalEdge : (sys.IsRestricted ? Palette.AlertCrimson : Palette.CrtAmberPrimary));
        DrawSmallButton(mouse, WarpSelectorX + WarpSelectorW - 24, WarpSelectorY, 24, WarpSelectorH, ">");

        // Backlit Waveform Glass Window: EXACT (448, 294, 216, 52)
        if (warpPoweredOff)
        {
            Raylib.DrawRectangle(WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH, new Color(10, 12, 15, 255));
            Raylib.DrawLine(WarpGlassX + 1, WarpGlassY + WarpGlassH / 2, WarpGlassX + WarpGlassW - 2, WarpGlassY + WarpGlassH / 2, new Color(24, 28, 34, 255));
            Raylib.DrawRectangleLines(WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH, Palette.WeatheredGunmetalEdge);
        }
        else
        {
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
    }

    private void DrawSlateBusSocket()
    {
        DeskRect bus = _layout.SlateBus;
        int dockedX = _layout.SlateBusDockedX;

        bool busPoweredOff = ClockOutPowerDownProgress >= 0.50f;
        bool pulse = ((_animTick / 10) % 2) == 0;
        bool draggingSlateNearBus = !busPoweredOff &&
                                    _draggingItemId == DeskItemId.DataSlate &&
                                    _deskItems[DeskItemId.DataSlate].X < bus.Right + 16 &&
                                    _deskItems[DeskItemId.DataSlate].X + _deskItems[DeskItemId.DataSlate].Width > bus.X - 16 &&
                                    _deskItems[DeskItemId.DataSlate].Y <= bus.Bottom + 24;

        bool anySlateInBus = IsDataSlateInSlateBus || _contrabandSlateInSlateBus;
        bool burntInBus = _contrabandSlateInSlateBus && _activeDeskContrabandItem != null && _activeDeskContrabandItem.IsBurnt;
        bool needsSlateForDownload = _phase == EncounterPhase.AwaitingPortForDownload;
        bool needsSlateForClearance = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort;
        bool isTransmittingClearance = _phase == EncounterPhase.TransmittingClearance;
        bool isDownloading = _phase == EncounterPhase.DownloadingProtocols;
        // NEVER flash red RE-PLUG DATA SLATE while Blank Data Slate download is still in progress or silent capture is active!
        bool isAborted = _phase == EncounterPhase.DownloadAborted && !_isDownloadingToBlankSlate;
        bool isCrimsonAbortAlert = isAborted && !_silentBlankSlateCaptureMode;

        Color borderCol = busPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : (isCrimsonAbortAlert
                ? (pulse ? Palette.AlertCrimson : Palette.CrtAmberPrimary)
                : (isTransmittingClearance || needsSlateForClearance
                    ? (pulse ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                    : (isDownloading
                        ? Palette.ClearanceTeal
                        : (needsSlateForDownload || draggingSlateNearBus
                            ? Palette.CrtAmberPrimary
                            : (burntInBus
                                ? Palette.WeatheredGunmetalEdge
                                : (anySlateInBus ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge))))));

        Raylib.DrawRectangle(bus.X, bus.Y, bus.W, bus.H, Palette.OutlineVoid);
        Raylib.DrawRectangle(bus.X + 2, bus.Y + 2, bus.W - 4, bus.H - 6, Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangleLines(bus.X, bus.Y, bus.W, bus.H, borderCol);

        // Corner bolts
        Raylib.DrawRectangle(bus.X + 4, bus.Y + 4, 2, 2, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(bus.Right - 6, bus.Y + 4, 2, 2, Palette.WeatheredGunmetalEdge);

        // 6 physical contact pins along bottom lip mating cleanly with docked Data Slate
        for (int p = 0; p < 6; p++)
        {
            int px = dockedX + 14 + p * 28;
            Color pinCol = busPoweredOff
                ? new Color(34, 38, 44, 255)
                : (isCrimsonAbortAlert
                    ? Palette.AlertCrimson
                    : (isTransmittingClearance || isDownloading
                        ? (pulse ? Palette.ClearanceTeal : Palette.SpsShieldArcIce)
                        : (burntInBus
                            ? new Color(56, 52, 50, 255)
                            : (anySlateInBus ? Palette.ClearanceTeal : Palette.OxidizedBronzeWorn))));
            Raylib.DrawRectangle(px, bus.Bottom - 4, 12, 4, pinCol);
        }

        // Status LED
        Color ledCol = busPoweredOff
            ? new Color(24, 28, 34, 255)
            : (isCrimsonAbortAlert
                ? Palette.AlertCrimson
                : (isTransmittingClearance || isDownloading || needsSlateForClearance
                    ? Palette.ClearanceTeal
                    : (burntInBus
                        ? new Color(64, 68, 76, 255)
                        : (anySlateInBus ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge))));
        Raylib.DrawCircle(bus.X + 14, bus.Y + 18, 4, ledCol);

        if (busPoweredOff)
        {
            UiText.Draw("SLATE BUS", bus.X + 26, bus.Y + 7, 10, Palette.WeatheredGunmetalEdge);
            UiText.Draw("STATUS: POWERED OFF", bus.X + 26, bus.Y + 21, 10, Palette.WeatheredGunmetalEdge);
            return;
        }

        if (_isUploadingCovertMissionSlate)
        {
            float txProg = Math.Clamp(_covertUploadElapsed / CovertUploadDurationSeconds, 0f, 1f);
            int barX = bus.X + 26;
            int barY = bus.Y + 10;
            int barW = bus.W - 36;
            const int barH = 18;
            int fillW = Math.Clamp((int)MathF.Round(barW * txProg), 1, barW);
            Raylib.DrawRectangle(barX, barY, barW, barH, Palette.DeepSpaceVoid);
            Raylib.DrawRectangle(barX, barY, fillW, barH, Palette.CrtAmberPrimary);
            Raylib.DrawRectangleLines(barX, barY, barW, barH, Palette.CrtAmberPrimary);
            string txTxt = $"TX COVERT {(int)MathF.Round(txProg * 100f)}%";
            int txW = UiText.MeasureWidth(txTxt, 10);
            int txtX = barX + (barW - txW) / 2;
            int txtY = barY + 4;
            UiText.Draw(txTxt, txtX, txtY, 10, Palette.CrtAmberPrimary);
            Raylib.BeginScissorMode(barX, barY, fillW, barH);
            UiText.Draw(txTxt, txtX, txtY, 10, Palette.OutlineVoid);
            Raylib.EndScissorMode();
            return;
        }

        if (isTransmittingClearance)
        {
            float txProg = Math.Clamp(_clearanceTransmitElapsed / ClearanceTransmitDurationSeconds, 0f, 1f);
            int barX = bus.X + 26;
            int barY = bus.Y + 10;
            int barW = bus.W - 36;
            const int barH = 18;
            int fillW = Math.Clamp((int)MathF.Round(barW * txProg), 1, barW);
            Raylib.DrawRectangle(barX, barY, barW, barH, Palette.DeepSpaceVoid);
            Raylib.DrawRectangle(barX, barY, fillW, barH, Palette.ClearanceTeal);
            Raylib.DrawRectangleLines(barX, barY, barW, barH, Palette.ClearanceTeal);
            string txTxt = $"TX CLEARANCE {(int)MathF.Round(txProg * 100f)}%";
            int txW = UiText.MeasureWidth(txTxt, 10);
            int txtX = barX + (barW - txW) / 2;
            int txtY = barY + 4;
            UiText.Draw(txTxt, txtX, txtY, 10, Palette.ClearanceTeal);
            Raylib.BeginScissorMode(barX, barY, fillW, barH);
            UiText.Draw(txTxt, txtX, txtY, 10, Palette.OutlineVoid);
            Raylib.EndScissorMode();
            return;
        }

        string line1 = "SLATE BUS";
        string line2;
        Color line2Col;

        if (isDownloading)
        {
            // Exact same 1.80s RX PACKET readout for both Primary Data Slate and Blank Data Slate!
            float rxProg = _isDownloadingToBlankSlate ? BlankSlateDownloadProgress : ProtocolDownloadProgress;
            line2 = $"RX PACKET {(int)MathF.Round(rxProg * 100f)}%";
            line2Col = Palette.ClearanceTeal;
        }
        else if (burntInBus)
        {
            line2 = "CRYSTAL BURNT // UNPLUG";
            line2Col = Palette.WeatheredGunmetalEdge;
        }
        else if (isAborted)
        {
            if (_silentBlankSlateCaptureMode)
            {
                line2 = anySlateInBus ? "STATUS: SLATE LINKED" : "STATUS: UNPLUGGED";
                line2Col = anySlateInBus ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed;
            }
            else
            {
                bool readyToReTx = IsDataSlateInSlateBus || HasValidBlankSlateInSlateBus;
                line2 = readyToReTx ? "CLICK [RE-TRANSMIT]" : "RE-PLUG DATA SLATE";
                line2Col = readyToReTx ? Palette.ClearanceTeal : Palette.AlertCrimson;
            }
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
        else if (_contrabandSlateInSlateBus && _activeDeskContrabandItem != null)
        {
            line2 = _activeDeskContrabandItem.IsMissionSlate
                ? "COVERT SLATE LINKED"
                : "STATUS: SLATE LINKED";
            line2Col = Palette.CrtAmberPrimary;
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

        UiText.Draw(line1, bus.X + 26, bus.Y + 7, 10, Palette.CrtAmberDimmed);
        UiText.Draw(TruncateByPixelWidth(line2, 10, bus.W - 34), bus.X + 26, bus.Y + 21, 10, line2Col);
    }

    private void DrawCommsCommandStrip(VirtualMouseState mouse)
    {
        DeskRect strip = _layout.CommsStrip;
        DeskRect nextBtn = _layout.NextHailBtn;

        // During Day 1 Vorst / Day 4 Kordani Pre-Shift Live Hail: dedicated single acknowledge/skip button spanning CommsStrip
        if (IsPreShiftTalkingHeadHailActive)
        {
            bool finishedLines = !_commsQueue.HasPendingOrActive;
            string btnTxt = finishedLines
                ? (_preShiftStage == PreShiftStage.Day1VorstLiveHail
                    ? "ACKNOWLEDGE HAIL -- BEGIN DAY 01 WATCH"
                    : $"ACKNOWLEDGE HAIL -- BEGIN DAY {_gameState.Day:D2} WATCH")
                : "SKIP TRANSMISSION TEXT >>";
            DrawHardwareButton(
                mouse,
                strip.X, strip.Y, strip.W, strip.H,
                btnTxt,
                enabled: true,
                highlight: true,
                activeColor: finishedLines ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
            return;
        }

        bool stripPoweredOff = ClockOutPowerDownProgress >= 0.44f;

        // Full-width NEXT HAIL (or RE-TRANSMIT when DownloadAborted / premature Cert-Wafer wipe, or INSPECT ROUTE on Day 3 Ghost Route ship)
        if (_phase == EncounterPhase.DownloadAborted)
        {
            bool canReTx = !stripPoweredOff && (IsDataSlateInSlateBus || HasValidBlankSlateInSlateBus) && _pendingReTransmitRestartTimer <= 0f;
            DrawHardwareButton(
                mouse, nextBtn.X, nextBtn.Y, nextBtn.W, nextBtn.H,
                _pendingReTransmitRestartTimer > 0f ? "WAIT..." : "RE-TRANSMIT",
                enabled: canReTx,
                highlight: canReTx && !_silentBlankSlateCaptureMode,
                activeColor: _silentBlankSlateCaptureMode ? Palette.CrtAmberPrimary : Palette.ClearanceTeal);
        }
        else if (_isReTransmittingWipedWafers || CanReTransmitPrematurelyWipedWafers)
        {
            // Calm normal styling for premature Cert-Wafer wipe recovery (zero red alarm or emergency flashing)
            bool canReTxWafers = !stripPoweredOff && CanReTransmitPrematurelyWipedWafers;
            DrawHardwareButton(
                mouse, nextBtn.X, nextBtn.Y, nextBtn.W, nextBtn.H,
                _isReTransmittingWipedWafers ? "WAIT..." : "RE-TRANSMIT",
                enabled: canReTxWafers,
                highlight: false,
                activeColor: Palette.CrtAmberPrimary);
        }
        else if (CanTriggerGhostRoutePushback)
        {
            DrawHardwareButton(
                mouse, nextBtn.X, nextBtn.Y, nextBtn.W, nextBtn.H,
                "INSPECT ROUTE",
                enabled: !stripPoweredOff,
                highlight: !stripPoweredOff,
                activeColor: Palette.CrtAmberPrimary);
        }
        else
        {
            bool canHail = !stripPoweredOff && _shiftController.CanTriggerNextHail && _phase == EncounterPhase.BayEmpty;
            string hailLabel = stripPoweredOff ? "OFFLINE" : (_shiftController.IsShiftTimerExpired ? "WATCH ENDED" : "NEXT HAIL");
            DrawHardwareButton(
                mouse, nextBtn.X, nextBtn.Y, nextBtn.W, nextBtn.H,
                hailLabel,
                enabled: canHail,
                highlight: canHail,
                activeColor: Palette.CrtAmberPrimary);
        }
    }

    private void DrawProtocolReviewButton(VirtualMouseState mouse)
    {
        DeskRect btn = _layout.ProtocolReviewBtn;
        bool locked = _preShiftStage != PreShiftStage.None || _faultStage != FaultSequenceStage.None || ClockOutPowerDownProgress >= 0.40f;
        bool canReview = !locked && _activeShip != null && IsShipInspectableForReview;
        bool faultTraceActive = HasActiveProtocolFaultTrace;
        bool duplicateNoteActive = _duplicateFaultNoteTimer > 0f && !faultTraceActive;
        bool matchFlash = _noFaultTraceTimer > 0f && !faultTraceActive && !duplicateNoteActive;

        string label;
        Color activeCol;
        if (faultTraceActive)
        {
            label = "PROTOCOL VIOLATED";
            activeCol = Palette.AlertCrimson;
        }
        else if (duplicateNoteActive)
        {
            label = "MISMATCH ALREADY FOUND";
            activeCol = Palette.CrtAmberPrimary;
        }
        else if (matchFlash)
        {
            label = "PROTOCOLS MATCH // NO FAULT";
            activeCol = Palette.ClearanceTeal;
        }
        else if (_protocolReviewModeActive)
        {
            label = _reviewFirstSelection == ReviewTarget.None
                ? "PROTOCOL REVIEW: SELECT CLAIM (1/2)"
                : "PROTOCOL REVIEW: SELECT REALITY (2/2)";
            activeCol = Palette.CrtAmberPrimary;
        }
        else
        {
            label = "PROTOCOL REVIEW";
            activeCol = Palette.CrtAmberPrimary;
        }

        DrawHardwareButton(
            mouse, btn.X, btn.Y, btn.W, btn.H,
            label,
            enabled: canReview,
            highlight: _protocolReviewModeActive || faultTraceActive || duplicateNoteActive || matchFlash,
            activeColor: activeCol);
    }

    // =========================================================================
    // 9. Clearance Upload Dock (A/B1: 1072,360,200,272 | B2 Flush: 1052,360,220,272)
    // =========================================================================
    private void DrawClearanceUploadDock(VirtualMouseState mouse)
    {
        DeskRect dock = _layout.ClearanceDock;
        DeskRect slot = _layout.ClearanceSlot;
        DeskRect bar = _layout.ClearanceBar;

        bool dockPoweredOff = ClockOutPowerDownProgress >= 0.68f;
        bool lockedByPreShift = _preShiftStage != PreShiftStage.None || _shiftController.IsPreShiftSequenceActive || dockPoweredOff;
        bool canSlotForClearance = !lockedByPreShift &&
                                   (_phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.DownloadAborted) &&
                                   _slateTelemetry == DataSlateTelemetryState.Loaded;
        bool draggingSlateOverDock = canSlotForClearance &&
                                     _draggingItemId == DeskItemId.DataSlate &&
                                     IsDraggingDataSlateOverClearanceDock(mouse, _deskItems[DeskItemId.DataSlate]);

        bool isUploading = _phase == EncounterPhase.UploadingClearance;
        bool awaitingReturn = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort;
        bool isTransmitting = _phase == EncounterPhase.TransmittingClearance;
        bool isClearedLed = awaitingReturn || isTransmitting;

        Color dockBorder = dockPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : ((IsDataSlateInClearanceDock || isUploading || awaitingReturn || isTransmitting)
                ? (isUploading ? Palette.OxidizedBronzeWorn : Palette.ClearanceTeal)
                : (draggingSlateOverDock ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge));

        // Outer Dock Chassis
        Raylib.DrawRectangle(dock.X, dock.Y, dock.W, dock.H, Palette.OutlineVoid);
        Raylib.DrawRectangle(dock.X + 2, dock.Y + 2, dock.W - 4, dock.H - 4, new Color(24, 29, 36, 255));
        Raylib.DrawRectangleLines(dock.X, dock.Y, dock.W, dock.H, dockBorder);

        // Top Header + Connector Pins
        Raylib.DrawRectangle(dock.X + 2, dock.Y + 2, dock.W - 4, ClearanceHeaderH - 2, Palette.WeatheredGunmetalMid);

        // Single calm Status Indicator LED (left side of header): steady muted amber while encrypting (1%-99%), solid teal at 100% cleared
        int ledX = dock.X + 8;
        int ledY = dock.Y + 5;
        Color ledBezel = lockedByPreShift
            ? Palette.WeatheredGunmetalEdge
            : (isUploading
                ? Palette.OxidizedBronzeWorn
                : (isClearedLed
                    ? Palette.ClearanceTeal
                    : (draggingSlateOverDock ? Palette.OxidizedBronzeWorn : Palette.WeatheredGunmetalEdge)));
        Color ledCore = lockedByPreShift
            ? new Color(28, 34, 42, 255)
            : (isUploading
                ? Palette.CrtAmberDimmed
                : (isClearedLed
                    ? Palette.ClearanceTeal
                    : (draggingSlateOverDock ? Palette.CrtAmberDimmed : new Color(38, 46, 56, 255))));

        Raylib.DrawRectangle(ledX, ledY, 10, 10, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(ledX, ledY, 10, 10, ledBezel);
        Raylib.DrawRectangle(ledX + 2, ledY + 2, 6, 6, ledCore);
        if (!lockedByPreShift && (isUploading || isClearedLed))
        {
            Color ledPip = isUploading
                ? Palette.CrtAmberPrimary
                : new Color(186, 255, 245, 255);
            Raylib.DrawRectangle(ledX + 3, ledY + 3, 2, 2, ledPip);
        }

        const string title = "CLEARANCE DOCK";
        int titleW = UiText.MeasureWidth(title, 10);
        UiText.Draw(title, dock.X + (dock.W - titleW) / 2, dock.Y + 5, 10,
            lockedByPreShift ? Palette.WeatheredGunmetalEdge : Palette.ClearanceTeal);

        // 6 Connector Pins along bottom edge of header (aligned cleanly with centered 184px bay)
        for (int pin = 0; pin < 6; pin++)
        {
            int px = slot.X + 14 + pin * 28;
            Color pinCol = dockPoweredOff
                ? new Color(34, 38, 44, 255)
                : (isClearedLed
                    ? Palette.ClearanceTeal
                    : Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangle(px, dock.Y + ClearanceHeaderH - 4, 12, 4, pinCol);
        }

        // Visible Docked Data Slate Bay: exact 184x216 centered inside dock chassis
        Color slotBg = draggingSlateOverDock ? new Color(16, 42, 46, 255) : Palette.DeepSpaceVoid;
        Raylib.DrawRectangle(slot.X, slot.Y, slot.W, slot.H, slotBg);
        Raylib.DrawRectangleLines(slot.X, slot.Y, slot.W, slot.H, dockBorder);

        // Recessed left/right clamp latch guide tracks machined into the dock's side walls (6px in A/B1, 16px in B2!)
        int sideWallW = Math.Max(6, slot.X - (dock.X + 2));
        int latchTrackY = slot.Y + (slot.H / 2) - 18;
        const int latchTrackH = 36;
        Raylib.DrawRectangle(dock.X + 2, latchTrackY, sideWallW, latchTrackH, Palette.OutlineVoid);
        Raylib.DrawLine(dock.X + 2, latchTrackY, dock.X + 2 + sideWallW - 1, latchTrackY, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(dock.X + 2, latchTrackY + latchTrackH - 1, dock.X + 2 + sideWallW - 1, latchTrackY + latchTrackH - 1, Palette.WeatheredGunmetalEdge);
        Raylib.DrawRectangle(slot.Right, latchTrackY, sideWallW, latchTrackH, Palette.OutlineVoid);
        Raylib.DrawLine(slot.Right, latchTrackY, slot.Right + sideWallW - 1, latchTrackY, Palette.WeatheredGunmetalEdge);
        Raylib.DrawLine(slot.Right, latchTrackY + latchTrackH - 1, slot.Right + sideWallW - 1, latchTrackY + latchTrackH - 1, Palette.WeatheredGunmetalEdge);

        // When no slate is seated inside the bay, render the open/retracting latches behind dragged items;
        // when seated, DrawVirtual renders them OVER the docked Data Slate so they bite over its sides.
        if (!IsDataSlateInClearanceDock)
        {
            DrawClearanceDockLatches(isEncrypting: isUploading);
        }

        // Only draw empty-bay instructions when Data Slate is NOT seated inside;
        // when seated, the REAL 184x216 Data Slate renders right on top inside slot!
        if (!IsDataSlateInClearanceDock)
        {
            if (draggingSlateOverDock)
            {
                const string dropTxt = ">> DROP TO ENCRYPT <<";
                int dw = UiText.MeasureWidth(dropTxt, 10);
                UiText.Draw(dropTxt, slot.X + (slot.W - dw) / 2, slot.Y + 100, 10, Palette.CrtAmberPrimary);
            }
            else if (isTransmitting)
            {
                const string txTxt = "TX VIA SLATE BUS";
                int tw = UiText.MeasureWidth(txTxt, 10);
                UiText.Draw(txTxt, slot.X + (slot.W - tw) / 2, slot.Y + 100, 10, Palette.ClearanceTeal);
            }
            else
            {
                const string idle1 = "DROP LOADED";
                const string idle2 = "DATA SLATE HERE";
                UiText.Draw(idle1, slot.X + (slot.W - UiText.MeasureWidth(idle1, 10)) / 2, slot.Y + 90, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(idle2, slot.X + (slot.W - UiText.MeasureWidth(idle2, 10)) / 2, slot.Y + 106, 10, Palette.WeatheredGunmetalEdge);
            }
        }

        // Encryption Progress Bar underneath the visible docked Data Slate
        Raylib.DrawRectangle(bar.X, bar.Y, bar.W, bar.H, Palette.DeepSpaceVoid);
        Raylib.DrawRectangleLines(bar.X, bar.Y, bar.W, bar.H,
            isUploading || awaitingReturn ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);

        float prog = isUploading
            ? Math.Clamp(_clearanceUploadElapsed / ClearanceUploadDurationSeconds, 0f, 1f)
            : (awaitingReturn || isTransmitting ? 1.0f : 0.0f);

        if (prog > 0.001f)
        {
            int innerW = bar.W - 4;
            int fillW = Math.Clamp((int)MathF.Round(innerW * prog), 1, innerW);
            Raylib.DrawRectangle(bar.X + 2, bar.Y + 2, fillW, bar.H - 4, Palette.ClearanceTeal);

            string pctStr = prog >= 0.999f ? "CLEARED 100%" : $"ENCRYPTING {(int)MathF.Round(prog * 100f)}%";
            int pw = UiText.MeasureWidth(pctStr, 10);
            int txtX = bar.X + (bar.W - pw) / 2;
            int txtY = bar.Y + 5;
            // Inverted-color retro CRT bar readout: teal on dark unfilled track, dark void on filled teal bar
            UiText.Draw(pctStr, txtX, txtY, 10, Palette.ClearanceTeal);
            Raylib.BeginScissorMode(bar.X + 2, bar.Y + 2, fillW, bar.H - 4);
            UiText.Draw(pctStr, txtX, txtY, 10, Palette.OutlineVoid);
            Raylib.EndScissorMode();
        }
        else
        {
            const string barIdle = "ENCRYPTION IDLE";
            int iw = UiText.MeasureWidth(barIdle, 10);
            UiText.Draw(barIdle, bar.X + (bar.W - iw) / 2, bar.Y + 5, 10, Palette.WeatheredGunmetalEdge);
        }
    }

    private void DrawClearanceDockLatches(bool isEncrypting)
    {
        DeskRect dock = _layout.ClearanceDock;
        DeskRect slot = _layout.ClearanceSlot;
        int sideWallW = Math.Max(6, slot.X - (dock.X + 2));

        int latchCenterY = slot.Y + slot.H / 2;
        int baseY = latchCenterY - 14;
        const int baseH = 28;
        int jawY = latchCenterY - 12;
        const int jawH = 24;

        float clampedP = Math.Clamp(_dockLatchProgress, 0f, 1f);
        // Snappy mechanical ease-out quad as the clamp jaws slide inward over the slate edges
        float easedP = 1.0f - (1.0f - clampedP) * (1.0f - clampedP);
        int travelPx = (int)MathF.Round(6.0f * easedP);

        // Steady muted brass/amber latch pips (no rapid blinking)
        Color pipCol = isEncrypting ? Palette.CrtAmberDimmed : Palette.OxidizedBronzeWorn;
        Color jawFill = Palette.WeatheredGunmetalEdge;
        Color jawShade = Palette.WeatheredGunmetalMid;
        Color jawHighlight = new(120, 134, 152, 255);
        Color jawTipEdge = new(184, 198, 214, 255);
        Color dropShadow = new(8, 11, 16, 210);

        // Left mechanical clamp latch (slides rightward from left bay wall over slate edge)
        int leftWallX = dock.X + 2;
        int leftJawW = sideWallW + travelPx;
        if (travelPx > 0)
        {
            Raylib.DrawRectangle(slot.X + travelPx, jawY + 1, 1, jawH, dropShadow);
            Raylib.DrawRectangle(slot.X, jawY + jawH, travelPx + 1, 1, dropShadow);
        }
        Raylib.DrawRectangle(leftWallX, baseY, sideWallW, baseH, Palette.OutlineVoid);
        Raylib.DrawRectangle(leftWallX, jawY, leftJawW, jawH, Palette.OutlineVoid);
        Raylib.DrawRectangle(leftWallX + 1, baseY + 1, sideWallW - 2, baseH - 2, jawShade);
        Raylib.DrawRectangle(leftWallX + 1, baseY + 1, sideWallW - 2, 2, jawHighlight);
        Raylib.DrawRectangle(leftWallX + 1, jawY + 1, leftJawW - 2, jawH - 2, jawFill);
        Raylib.DrawLine(leftWallX + 1, jawY + 1, leftWallX + leftJawW - 2, jawY + 1, jawHighlight);
        Raylib.DrawLine(leftWallX + 1, jawY + jawH - 2, leftWallX + leftJawW - 2, jawY + jawH - 2, jawShade);
        Raylib.DrawRectangle(leftWallX + leftJawW - 2, jawY + 2, 1, jawH - 4, jawTipEdge);
        if (travelPx >= 2)
        {
            Raylib.DrawLine(slot.X, jawY + 6, leftWallX + leftJawW - 3, jawY + 6, jawShade);
            Raylib.DrawLine(slot.X, jawY + 12, leftWallX + leftJawW - 3, jawY + 12, jawShade);
            Raylib.DrawLine(slot.X, jawY + 18, leftWallX + leftJawW - 3, jawY + 18, jawShade);
        }
        int leftPipX = leftWallX + Math.Max(2, (sideWallW - 2) / 2);
        Raylib.DrawRectangle(leftPipX, latchCenterY - 4, 2, 8, pipCol);

        // Right mechanical clamp latch (mirrored: slides leftward from right bay wall over slate edge)
        int rightWallX = slot.Right;
        int rightJawX = rightWallX - travelPx;
        int rightJawW = sideWallW + travelPx;
        if (travelPx > 0)
        {
            Raylib.DrawRectangle(rightJawX - 1, jawY + 1, 1, jawH, dropShadow);
            Raylib.DrawRectangle(rightJawX - 1, jawY + jawH, travelPx + 1, 1, dropShadow);
        }
        Raylib.DrawRectangle(rightWallX, baseY, sideWallW, baseH, Palette.OutlineVoid);
        Raylib.DrawRectangle(rightJawX, jawY, rightJawW, jawH, Palette.OutlineVoid);
        Raylib.DrawRectangle(rightWallX + 1, baseY + 1, sideWallW - 2, baseH - 2, jawShade);
        Raylib.DrawRectangle(rightWallX + 1, baseY + 1, sideWallW - 2, 2, jawHighlight);
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
        int rightPipX = rightWallX + Math.Max(2, (sideWallW - 2) / 2);
        Raylib.DrawRectangle(rightPipX, latchCenterY - 4, 2, 8, pipCol);

        if (IsBrownoutAttenuatingPassiveSurfaces)
        {
            ApplyBrownoutPassiveSurfaceLighting(leftWallX, baseY, leftJawW, baseH);
            ApplyBrownoutPassiveSurfaceLighting(rightJawX, baseY, rightJawW, baseH);
        }
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
        UiText.Draw(TruncateByPixelWidth(_activeCitationStrip.HeaderText, 10, cw - 16), cx + 10, cy + 7, 10, Palette.OutlineVoid);

        UiText.Draw(TruncateByPixelWidth(_activeCitationStrip.ViolationReason, 10, cw - 20), cx + 10, cy + 28, 10, Palette.CrtAmberPrimary);
        UiText.Draw(TruncateByPixelWidth(_activeCitationStrip.PenaltyText, 10, cw - 20), cx + 10, cy + 46, 10,
            _activeCitationStrip.IsSecurityStrike ? Palette.AlertCrimson : Palette.ClearanceTeal);
        UiText.Draw("[CLICK TO DISMISS CITATION]", cx + 10, cy + 66, 10, Palette.CrtAmberDimmed);
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
        if (h <= 40 && UiText.MeasureWidth(inlineCandidate, 10) <= maxTextPx)
        {
            int tw = UiText.MeasureWidth(inlineCandidate, 10);
            int tx = x + (w - tw) / 2;
            int ty = y + (h - 10) / 2;
            string prefix = string.IsNullOrEmpty(unlockTag) ? $"{moduleName} -- " : $"{moduleName} -- {unlockTag} ";
            UiText.Draw(prefix, tx, ty, 10, stencilNameCol);
            UiText.Draw("[PLAYTEST]", tx + UiText.MeasureWidth(prefix, 10), ty, 10, playtestBadgeCol);
            return;
        }

        // Tall / compact bays: clean 2- or 3-line centered stencil
        if (h >= 72 && !string.IsNullOrEmpty(unlockTag) && UiText.MeasureWidth(moduleName, 10) <= maxTextPx)
        {
            string line1 = TruncateByPixelWidth(moduleName, 10, maxTextPx);
            string line2 = unlockTag;
            const string line3 = "[PLAYTEST]";
            int startY = y + (h - 36) / 2;
            UiText.Draw(line1, x + (w - UiText.MeasureWidth(line1, 10)) / 2, startY, 10, stencilNameCol);
            UiText.Draw(line2, x + (w - UiText.MeasureWidth(line2, 10)) / 2, startY + 13, 10, stencilNameCol);
            UiText.Draw(line3, x + (w - UiText.MeasureWidth(line3, 10)) / 2, startY + 26, 10, playtestBadgeCol);
            return;
        }

        string topText = string.IsNullOrEmpty(unlockTag)
            ? TruncateByPixelWidth(moduleName, 10, maxTextPx)
            : TruncateByPixelWidth(UiText.MeasureWidth($"{moduleName} -- {unlockTag}", 10) <= maxTextPx
                ? $"{moduleName} -- {unlockTag}"
                : moduleName, 10, maxTextPx);
        string botText = !string.IsNullOrEmpty(unlockTag) && topText == moduleName
            ? $"{unlockTag} [PLAYTEST]"
            : "[PLAYTEST]";
        botText = TruncateByPixelWidth(botText, 10, maxTextPx);

        int lineGap = h <= 44 ? 12 : 14;
        int blockH = 10 + lineGap;
        int topY = y + Math.Max(4, (h - blockH) / 2);
        UiText.Draw(topText, x + (w - UiText.MeasureWidth(topText, 10)) / 2, topY, 10, stencilNameCol);
        UiText.Draw(botText, x + (w - UiText.MeasureWidth(botText, 10)) / 2, topY + lineGap, 10, playtestBadgeCol);
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
    // Shutdown Effects, Crimson Brown Dwarf Wash & Shared Hardware Draw Helpers
    // =========================================================================
    private static void DrawCrtPowerDownCollapse(int x, int y, int w, int h, float collapseT)
    {
        float t = Math.Clamp(collapseT, 0f, 1f);
        int midY = y + h / 2;
        Color deadGlass = new(8, 10, 14, 255);

        if (t < 0.65f)
        {
            float vFrac = t / 0.65f;
            int closedHalfH = (int)MathF.Round((h * 0.5f - 1f) * vFrac);
            if (closedHalfH > 0)
            {
                Raylib.DrawRectangle(x, y, w, closedHalfH, deadGlass);
                Raylib.DrawRectangle(x, y + h - closedHalfH, w, closedHalfH, deadGlass);
            }
            int beamYTop = y + closedHalfH;
            int beamYBot = y + h - closedHalfH - 1;
            Raylib.DrawLine(x + 2, beamYTop, x + w - 3, beamYTop, Palette.CrtAmberPrimary);
            if (beamYBot != beamYTop)
            {
                Raylib.DrawLine(x + 2, beamYBot, x + w - 3, beamYBot, Palette.CrtAmberPrimary);
            }
        }
        else
        {
            Raylib.DrawRectangle(x, y, w, h, deadGlass);
            // Subtle unlit glass reflection line
            Raylib.DrawLine(x + 8, y + 6, x + Math.Min(w - 6, 28), y + Math.Min(h - 6, 26), new Color(22, 28, 34, 255));

            if (t < 0.92f)
            {
                float hFrac = 1.0f - (t - 0.65f) / 0.27f;
                int lineW = Math.Max(4, (int)MathF.Round((w - 8) * hFrac));
                int lx = x + (w - lineW) / 2;
                Raylib.DrawRectangle(lx, midY - 1, lineW, 2, Palette.CrtAmberPrimary);
                Raylib.DrawLine(lx + 2, midY, lx + lineW - 2, midY, new Color(255, 244, 210, 255));
            }
        }
    }

    private static void DrawClockOutBrownDwarfCrimsonWash(float progress)
    {
        float p = Math.Clamp(progress, 0f, 1f);

        // 1. Subtle ambient dim using BlendMode.Multiplied (preserves 100% opaque RenderTexture alpha -- zero milky/pink haze!)
        //    Keeps Booth 04 ambient room lighting dim-on (never blacking out the screen).
        byte dimR = (byte)Math.Clamp((int)MathF.Round(255f - p * 54f), 190, 255);
        byte dimG = (byte)Math.Clamp((int)MathF.Round(255f - p * 84f), 155, 255);
        byte dimB = (byte)Math.Clamp((int)MathF.Round(255f - p * 86f), 152, 255);
        Raylib.BeginBlendMode(BlendMode.Multiplied);
        Raylib.DrawRectangle(0, TopBarH, Constants.VirtualWidth, Constants.VirtualHeight - TopBarH,
            new Color(dimR, dimG, dimB, (byte)255));
        Raylib.EndBlendMode();

        // 2. Smooth directional crimson-red Brown Dwarf glow radiating from the armored viewport (112, 40, 448, 216)
        //    using BlendMode.Additive on fine 16x16 pixel cells so there are zero blocky stair-steps and zero alpha artifacts!
        float originX = ViewportX + ViewportW * 0.62f; // Setting Brown Dwarf sits low-right of viewport
        float originY = ViewportY + ViewportH - 18f;

        Raylib.BeginBlendMode(BlendMode.Additive);
        const int cell = 16;
        for (int y = TopBarH; y < Constants.VirtualHeight; y += cell)
        {
            float cy = y + cell * 0.5f;
            float dy = cy - originY;
            float yWeight = dy < 0f ? Math.Abs(dy) * 1.45f : dy * 0.88f;

            for (int x = 0; x < Constants.VirtualWidth; x += cell)
            {
                float cx = x + cell * 0.5f;
                // Gentle diagonal beam spill downward-right across the quiet desk surface
                float beamShift = Math.Max(0f, dy) * 0.16f;
                float dx = (cx - (originX + beamShift)) * 0.82f;
                float dist = MathF.Sqrt(dx * dx + yWeight * yWeight);

                float norm = Math.Clamp(1.0f - dist / 640.0f, 0f, 1f);
                float smooth = norm * norm * (3.0f - 2.0f * norm);
                float strength = smooth * p;
                if (strength <= 0.015f)
                    continue;

                byte addR = (byte)Math.Clamp((int)MathF.Round(strength * 92f), 0, 110);
                byte addG = (byte)Math.Clamp((int)MathF.Round(strength * 14f), 0, 20);
                byte addB = (byte)Math.Clamp((int)MathF.Round(strength * 12f), 0, 18);
                if (addR > 0)
                {
                    Raylib.DrawRectangle(x, y, cell, cell, new Color(addR, addG, addB, (byte)255));
                }
            }
        }

        // Crisp crimson specular rim glow along the bottom sill of the armored viewport
        byte sillR = (byte)Math.Clamp((int)MathF.Round(p * 175f), 0, 190);
        byte sillG = (byte)Math.Clamp((int)MathF.Round(p * 30f), 0, 38);
        byte sillB = (byte)Math.Clamp((int)MathF.Round(p * 26f), 0, 34);
        Raylib.DrawRectangle(ViewportX, ViewportY + ViewportH, ViewportW, 3, new Color(sillR, sillG, sillB, (byte)255));
        Raylib.EndBlendMode();
    }

    private static Rectangle GetClockOutLeavePromptRect()
    {
        const int fontSize = 20;
        int tw = UiText.MeasureWidth(ClockOutLeavePromptText, fontSize);
        int px = (Constants.VirtualWidth - tw) / 2;
        const int py = 486;
        return new Rectangle(px - 10, py - 6, tw + 20, fontSize + 12);
    }

    private void DrawClockOutLeavePrompt(VirtualMouseState mouse)
    {
        // Understated clickable plain-text prompt centered over the quiet powered-down workspace
        // (strictly NOT a framed console button -- clean text with subtle hover highlight & thin underline)
        const int fontSize = 20;
        int tw = UiText.MeasureWidth(ClockOutLeavePromptText, fontSize);
        int tx = (Constants.VirtualWidth - tw) / 2;
        const int ty = 486;
        Rectangle hitRect = GetClockOutLeavePromptRect();
        bool hovered = mouse.IsHit(hitRect);

        // Crisp 1px drop shadow for clean legibility over the dim crimson desk
        UiText.Draw(ClockOutLeavePromptText, tx + 1, ty + 1, fontSize, Palette.OutlineVoid);
        Color textCol = hovered ? Palette.CrtAmberPrimary : new Color(226, 206, 188, 255);
        UiText.Draw(ClockOutLeavePromptText, tx, ty, fontSize, textCol);

        if (hovered)
        {
            Raylib.DrawLine(tx, ty + fontSize + 2, tx + tw, ty + fontSize + 2, Palette.CrtAmberPrimary);
        }
    }

    private static void DrawThickLine(int x0, int y0, int x1, int y1, int thickness, Color col)
    {
        Raylib.DrawLineEx(new Vector2(x0, y0), new Vector2(x1, y1), thickness, col);
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
        int tw = UiText.MeasureWidth(label, 10);
        UiText.Draw(label, x + Math.Max(4, (w - tw) / 2), y + (h - 10) / 2, 10, textCol);
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
        int tw = UiText.MeasureWidth(label, 10);
        UiText.Draw(label, x + Math.Max(4, (w - tw) / 2), y + (h - 10) / 2, 10, textOverride ?? Palette.CrtAmberPrimary);
    }

    // =========================================================================
    // Day 15+ Stellar Stoker Dual-Readout Module & 3-State Brownout Lighting
    // =========================================================================
    private bool CanAnchorStokerMassNow() =>
        IsStokerOperational &&
        ClockOutPowerDownProgress < 0.58f &&
        _preShiftStage == PreShiftStage.None &&
        !_shiftController.IsPreShiftSequenceActive &&
        _activeShip != null &&
        !_activeShip.GroundTruth.ActualShieldsUp &&
        _phase is EncounterPhase.OpeningGreetingDelay
            or EncounterPhase.AwaitingPortForDownload
            or EncounterPhase.DownloadingProtocols
            or EncounterPhase.DownloadAborted
            or EncounterPhase.InspectingSlateAndShip
            or EncounterPhase.AwaitingClearanceReturnToTopPort;

    internal void TryAnchorStokerMassForActiveShip()
    {
        if (!CanAnchorStokerMassNow() || _activeShip == null)
        {
            AudioHooks.Play("citation_buzz");
            return;
        }

        _stokerMassAnchoredForShip = true;
        _stokerMeasuredMass = _activeShip.GroundTruth.ActualMass;
        RefreshStokerAnchorPreviewForActiveShip();
        AudioHooks.Play("terminal_key_confirm");
    }

    private void RefreshStokerAnchorPreviewForActiveShip()
    {
        if (_activeShip == null || !_stokerMassAnchoredForShip || !IsStokerOperational)
        {
            _stokerPreviewShock = 0f;
            return;
        }

        StokerCalibrationResult eval = ValidationEngine.EvaluateStokerMassCalibration(
            _activeShip,
            _gameState.Day,
            _config,
            _stokerSafeLoad,
            _stokerShock);
        _stokerPreviewShock = eval.PreviewShockDelta;
        _devDebug.LiveStokerPreviewShock = _stokerPreviewShock;
    }

    private void TriggerStokerOverloadDuringShift()
    {
        if (_gameState.StokerOverloadedToday)
            return;

        // Overload assesses -35 SC fine at Clock-Out, triggers Sleep Sabotage Audit (+0 Security Strikes!),
        // and schedules next-day Corridor/Booth 04 3-state power brownouts + reactive FORUM fallout.
        _gameState.StokerOverloadedToday = true;
        _gameState.PendingStokerBerthSearch = true;
        _gameState.NextDayPowerBrownout = true;
        _gameState.LastShiftStokerOverload = true;
        _gameState.SaveToDisk();

        AudioHooks.Play("detain_alarm");
        AddSystemLogLine(
            "[STOKER OVERLOAD] COIL STRAIN EXCEEDED 100% -- STRUCTURAL FINE & SABOTAGE AUDIT FLAGGED.",
            CommsTextTone.AlertCrimson);
    }

    private void DrawStellarStokerModule(VirtualMouseState mouse)
    {
        if (!IsStokerOperational)
        {
            DrawInactiveHardwareBay(StokerDialX, StokerDialY, StokerDialW, StokerDialH, "STOKER DIAL", $"DAY {_config.DayUnlockStoker:D2}");
            return;
        }

        bool stokerPoweredOff = ClockOutPowerDownProgress >= 0.58f;
        bool overloaded = _gameState.StokerOverloadedToday || (_stokerSafeLoad + _stokerShock) > 100.0f;
        bool pulse = ((_animTick / 8) % 2) == 0;

        // Scuffed, sickly-yellow Brengop industrial housing at exact (8, 264, 96, 88) beneath Detain Lever
        Color housingBase = new(82, 74, 22, 255);      // Sickly-olive yellow industrial chassis
        Color housingMid = new(118, 106, 32, 255);     // Worn Brengop yellow faceplate
        Color housingBevel = new(168, 152, 48, 255);   // Scuffed yellow top/left highlight
        Color scuffDark = new(46, 42, 14, 255);        // Grease & abrasion scuffs
        Color borderCol = stokerPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : (overloaded && pulse ? Palette.AlertCrimson : housingBevel);

        Raylib.DrawRectangle(StokerDialX, StokerDialY, StokerDialW, StokerDialH, Palette.OutlineVoid);
        Raylib.DrawRectangle(StokerDialX + 1, StokerDialY + 1, StokerDialW - 2, StokerDialH - 2, housingBase);
        Raylib.DrawRectangle(StokerDialX + 2, StokerDialY + 2, StokerDialW - 4, StokerDialH - 4, housingMid);
        Raylib.DrawLine(StokerDialX + 1, StokerDialY + 1, StokerDialX + StokerDialW - 2, StokerDialY + 1, housingBevel);
        Raylib.DrawLine(StokerDialX + 1, StokerDialY + 1, StokerDialX + 1, StokerDialY + StokerDialH - 2, housingBevel);

        // Industrial wear scuffs & corner rivets
        Raylib.DrawRectangle(StokerDialX + 6, StokerDialY + 31, 18, 2, scuffDark);
        Raylib.DrawRectangle(StokerDialX + StokerDialW - 22, StokerDialY + 64, 16, 2, scuffDark);
        Raylib.DrawPixel(StokerDialX + 3, StokerDialY + 3, Palette.OutlineVoid);
        Raylib.DrawPixel(StokerDialX + StokerDialW - 4, StokerDialY + 3, Palette.OutlineVoid);
        Raylib.DrawPixel(StokerDialX + 3, StokerDialY + StokerDialH - 4, Palette.OutlineVoid);
        Raylib.DrawPixel(StokerDialX + StokerDialW - 4, StokerDialY + StokerDialH - 4, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(StokerDialX, StokerDialY, StokerDialW, StokerDialH, borderCol);

        // 1. 2-Line Stacked Digital LED Readout (12, 268, 88, 26)
        DrawStokerDigitalLedBox(mouse);

        // 2. Analog Strain Dial (0% to 100%; 0-65% normal band, 65-100% redline danger band) at (12, 296, 88, 32)
        DrawStokerAnalogStrainDial(stokerPoweredOff, overloaded);

        // 3. Clickable [ANCHOR] (ANCHOR MASS) trigger at (12, 330, 88, 18)
        bool canAnchor = CanAnchorStokerMassNow();
        bool anchorHover = canAnchor && mouse.IsHit(StokerAnchorBtnX, StokerAnchorBtnY, StokerAnchorBtnW, StokerAnchorBtnH);
        Color btnBg = !canAnchor
            ? new Color(28, 26, 14, 255)
            : (anchorHover ? new Color(92, 76, 20, 255) : new Color(44, 40, 14, 255));
        Color btnBorder = !canAnchor
            ? new Color(76, 70, 32, 255)
            : (anchorHover
                ? Palette.CrtAmberPrimary
                : (_stokerMassAnchoredForShip ? Palette.ClearanceTeal : housingBevel));
        Color btnTextCol = !canAnchor
            ? new Color(92, 86, 48, 255)
            : (_stokerMassAnchoredForShip ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);

        Raylib.DrawRectangle(StokerAnchorBtnX, StokerAnchorBtnY, StokerAnchorBtnW, StokerAnchorBtnH, btnBg);
        Raylib.DrawRectangleLines(StokerAnchorBtnX, StokerAnchorBtnY, StokerAnchorBtnW, StokerAnchorBtnH, btnBorder);
        string anchorLabel = _stokerMassAnchoredForShip ? "ANCHORED" : "[ANCHOR]";
        string safeAnchorLabel = UiText.TruncateToWidth(anchorLabel, 10, StokerAnchorBtnW - 6, denseRow: true);
        int aw = UiText.MeasureWidth(safeAnchorLabel, 10, denseRow: true);
        UiText.Draw(
            safeAnchorLabel,
            StokerAnchorBtnX + Math.Max(2, (StokerAnchorBtnW - aw) / 2),
            StokerAnchorBtnY + 4,
            10,
            btnTextCol,
            denseRow: true);
    }

    private void DrawStokerDigitalLedBox(VirtualMouseState mouse)
    {
        bool stokerPoweredOff = ClockOutPowerDownProgress >= 0.58f;
        bool ledFault = IsTargetFaultHighlighted(ReviewTarget.StokerMeasuredMassLed);
        bool ledSelected = _protocolReviewModeActive && _reviewFirstSelection == ReviewTarget.StokerMeasuredMassLed;

        Color ledBg = stokerPoweredOff
            ? new Color(8, 10, 14, 255)
            : (ledFault ? Palette.BrownDwarfGlowDeep : (ledSelected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid));
        Color ledBorder = stokerPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : (ledFault
                ? GetFaultPulseColor()
                : (ledSelected ? Palette.ClearanceTeal : new Color(168, 152, 48, 255)));

        Raylib.DrawRectangle(StokerLedX, StokerLedY, StokerLedW, StokerLedH, ledBg);
        Raylib.DrawRectangleLines(StokerLedX, StokerLedY, StokerLedW, StokerLedH, ledBorder);

        if (!stokerPoweredOff)
        {
            const int maxTextW = StokerLedW - 6; // 82px inside 88px LED cavity (fits 96px bezel cleanly at +3 offset)
            string topRow = UiText.TruncateToWidth("MEASURED", 10, maxTextW, denseRow: true);
            string botRaw = (_stokerMassAnchoredForShip && _activeShip != null)
                ? $"{_stokerMeasuredMass:N0} T"
                : "---- T";
            string botRow = UiText.TruncateToWidth(botRaw, 10, maxTextW, denseRow: true);

            int topW = UiText.MeasureWidth(topRow, 10, denseRow: true);
            int botW = UiText.MeasureWidth(botRow, 10, denseRow: true);
            Color botCol = ledFault
                ? GetFaultPulseColor()
                : ((_stokerMassAnchoredForShip && _activeShip != null) ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);

            UiText.Draw(topRow, StokerLedX + Math.Max(2, (StokerLedW - topW) / 2), StokerLedY + 2, 10, Palette.CrtAmberDimmed, denseRow: true);
            UiText.Draw(botRow, StokerLedX + Math.Max(2, (StokerLedW - botW) / 2), StokerLedY + 13, 10, botCol, denseRow: true);

            if (ledFault)
            {
                Raylib.DrawRectangleLines(StokerLedX - 1, StokerLedY - 1, StokerLedW + 2, StokerLedH + 2, GetFaultPulseColor());
            }
            else if (_protocolReviewModeActive &&
                     IsShipInspectableForReview &&
                     _stokerMassAnchoredForShip &&
                     IsReviewTargetUnlocked(ReviewTarget.StokerMeasuredMassLed))
            {
                DrawReviewHotspotOutline(mouse, StokerLedX, StokerLedY, StokerLedW, StokerLedH, ledSelected);
            }
        }
    }

    private void DrawStokerAnalogStrainDial(bool stokerPoweredOff, bool overloaded)
    {
        Raylib.DrawRectangle(StokerGaugeX, StokerGaugeY, StokerGaugeW, StokerGaugeH, new Color(18, 20, 16, 255));
        Raylib.DrawRectangleLines(StokerGaugeX, StokerGaugeY, StokerGaugeW, StokerGaugeH, new Color(68, 62, 24, 255));

        int cx = StokerGaugeX + StokerGaugeW / 2; // 56
        int cy = StokerGaugeY + StokerGaugeH - 5; // 323
        const int radius = 23;
        float safeCapFrac = Math.Clamp(_config.StokerSafeLoadCap / 100.0f, 0.30f, 0.90f);

        // Semicircular analog strain scale: 0..65% normal operating band, 65..100% redline danger band
        const int arcSteps = 28;
        int prevOuterX = 0, prevOuterY = 0, prevInnerX = 0, prevInnerY = 0;
        for (int s = 0; s <= arcSteps; s++)
        {
            float frac = s / (float)arcSteps;
            float rad = MathF.PI * (1.0f - frac);
            int ox = cx + (int)MathF.Round(MathF.Cos(rad) * radius);
            int oy = cy - (int)MathF.Round(MathF.Sin(rad) * radius);
            int ix = cx + (int)MathF.Round(MathF.Cos(rad) * (radius - 4));
            int iy = cy - (int)MathF.Round(MathF.Sin(rad) * (radius - 4));

            if (s > 0)
            {
                bool inRedline = frac > safeCapFrac;
                Color bandOuter = stokerPoweredOff
                    ? Palette.WeatheredGunmetalEdge
                    : (inRedline ? Palette.AlertCrimson : new Color(92, 176, 118, 255));
                Color bandInner = stokerPoweredOff
                    ? Palette.WeatheredGunmetalMid
                    : (inRedline ? Palette.BrownDwarfGlowDeep : new Color(46, 94, 64, 255));
                Raylib.DrawLine(prevOuterX, prevOuterY, ox, oy, bandOuter);
                Raylib.DrawLine(prevInnerX, prevInnerY, ix, iy, bandInner);
            }

            prevOuterX = ox;
            prevOuterY = oy;
            prevInnerX = ix;
            prevInnerY = iy;
        }

        // Crisp 65% redline threshold tick mark
        float capRad = MathF.PI * (1.0f - safeCapFrac);
        int capX0 = cx + (int)MathF.Round(MathF.Cos(capRad) * (radius - 6));
        int capY0 = cy - (int)MathF.Round(MathF.Sin(capRad) * (radius - 6));
        int capX1 = cx + (int)MathF.Round(MathF.Cos(capRad) * (radius + 1));
        int capY1 = cy - (int)MathF.Round(MathF.Sin(capRad) * (radius + 1));
        Raylib.DrawLine(capX0, capY0, capX1, capY1, Palette.CrtAmberPrimary);

        float committedFrac = Math.Clamp((_stokerSafeLoad + _stokerShock) / 100.0f, 0f, 1.08f);
        float previewFrac = Math.Clamp((_stokerSafeLoad + _stokerShock + _stokerPreviewShock) / 100.0f, 0f, 1.08f);

        // If [ANCHOR] is previewing an uncalibrated +33% spike, show subtle base needle + jumping preview needle
        if (_stokerPreviewShock > 0.01f && !stokerPoweredOff)
        {
            float baseRad = MathF.PI * (1.0f - Math.Min(1.0f, committedFrac));
            int bx = cx + (int)MathF.Round(MathF.Cos(baseRad) * (radius - 5));
            int by = cy - (int)MathF.Round(MathF.Sin(baseRad) * (radius - 5));
            Raylib.DrawLine(cx, cy, bx, by, Palette.CrtAmberDimmed);
        }

        float needleFrac = Math.Min(1.04f, previewFrac);
        float needleRad = MathF.PI * (1.0f - needleFrac);
        int nx = cx + (int)MathF.Round(MathF.Cos(needleRad) * (radius - 2));
        int ny = cy - (int)MathF.Round(MathF.Sin(needleRad) * (radius - 2));
        Color needleCol = stokerPoweredOff
            ? Palette.WeatheredGunmetalEdge
            : ((overloaded || previewFrac > safeCapFrac) ? Palette.AlertCrimson : new Color(242, 224, 138, 255));
        Raylib.DrawLine(cx, cy, nx, ny, needleCol);
        Raylib.DrawRectangle(cx - 2, cy - 2, 5, 4, new Color(168, 152, 48, 255));
    }

    // =========================================================================
    // 3-State Next-Day Booth 04 & Viewport Power Brownout Machine
    // =========================================================================
    private void InitializeBrownoutStateForShift()
    {
        int seed = (_gameState.RunSeed != 0 ? _gameState.RunSeed : 2026) ^ (_gameState.Day * 104729);
        _brownoutRng = new Random(seed);
        _brownoutState = BoothBrownoutState.On;
        _brownoutTriggerCount = 0;
        _brownoutCycleElapsed = 0f;
        _brownoutActiveHoldElapsed = 0f;
        _brownoutEnteredThisCycle = false;
        _forcedOffElapsed = 0f;
        _lastAppliedBrownoutForceMode = _devDebug.BrownoutForceMode;
        RollNextBrownoutCycleParameters();
    }

    private float SampleBrownoutRange(float min, float max) =>
        min + (float)_brownoutRng.NextDouble() * (max - min);

    private void RollNextBrownoutCycleParameters()
    {
        float shiftScale = Math.Max(0.25f, _shiftController.ActiveShiftDurationSeconds / (float)Constants.ShiftDurationSeconds);
        _brownoutCycleDuration = SampleBrownoutRange(45.0f, 55.0f) * shiftScale;
        // Alternate with deterministic variety so both DIM and OFF states occur reliably across a shift
        _brownoutNextTargetState = ((_brownoutTriggerCount % 2) == 0)
            ? (_brownoutRng.NextDouble() < 0.65 ? BoothBrownoutState.Dim : BoothBrownoutState.Off)
            : (_brownoutRng.NextDouble() < 0.75 ? BoothBrownoutState.Off : BoothBrownoutState.Dim);
    }

    private void UpdateBrownoutLightingState(float deltaTime)
    {
        // F1 Dev Override: FORCE ON / FORCE DIM / FORCE OFF
        if (_devDebug.BrownoutForceMode != _lastAppliedBrownoutForceMode)
        {
            _lastAppliedBrownoutForceMode = _devDebug.BrownoutForceMode;
            if (_devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOff)
            {
                _forcedOffElapsed = 0f;
            }
        }

        switch (_devDebug.BrownoutForceMode)
        {
            case DebugBrownoutForceMode.ForceOn:
                _brownoutState = BoothBrownoutState.On;
                return;
            case DebugBrownoutForceMode.ForceDim:
                _brownoutState = BoothBrownoutState.Dim;
                return;
            case DebugBrownoutForceMode.ForceOff:
                _brownoutState = BoothBrownoutState.Off;
                _forcedOffElapsed += Math.Max(0f, deltaTime);
                return;
        }

        if (!_gameState.NextDayPowerBrownout ||
            _preShiftStage != PreShiftStage.None ||
            !_shiftController.HasShiftTimerStarted)
        {
            _brownoutState = BoothBrownoutState.On;
            return;
        }

        AdvanceAutoBrownoutCycle(deltaTime);
    }

    internal void SmokeAdvanceBrownoutTimer(float seconds)
    {
        if (seconds <= 0f)
            return;
        _shiftController.NotifyFirstHailTriggered();
        AdvanceAutoBrownoutCycle(seconds);
        PowerIndicatorBrightness = ComputeActiveWatchPowerLedBrightness();
    }

    private void AdvanceAutoBrownoutCycle(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        float shiftScale = Math.Max(0.25f, _shiftController.ActiveShiftDurationSeconds / (float)Constants.ShiftDurationSeconds);
        float holdSeconds = BrownoutBaseHoldSeconds * shiftScale;

        float rem = deltaTime;
        while (rem > 0f)
        {
            if (_brownoutTriggerCount >= MaxBrownoutTriggersPerShift && !_brownoutEnteredThisCycle)
            {
                _brownoutState = BoothBrownoutState.On;
                _brownoutActiveHoldElapsed = 0f;
                return;
            }

            float onDuration = Math.Max(5.0f * shiftScale, _brownoutCycleDuration - holdSeconds);
            float nextBoundary = !_brownoutEnteredThisCycle
                ? Math.Max(0.0001f, onDuration - _brownoutCycleElapsed)
                : Math.Max(0.0001f, _brownoutCycleDuration - _brownoutCycleElapsed);
            float step = Math.Min(rem, nextBoundary);
            _brownoutCycleElapsed += step;
            rem -= step;

            if (!_brownoutEnteredThisCycle && _brownoutCycleElapsed + 0.00005f >= onDuration)
            {
                _brownoutEnteredThisCycle = true;
                _brownoutTriggerCount++;
                _brownoutState = _brownoutNextTargetState;
                _brownoutActiveHoldElapsed = Math.Max(0f, _brownoutCycleElapsed - onDuration);
                AudioHooks.Play("terminal_key_click");
            }
            else if (_brownoutEnteredThisCycle)
            {
                _brownoutActiveHoldElapsed = Math.Max(0f, _brownoutCycleElapsed - onDuration);
                if (_brownoutCycleElapsed + 0.00005f >= _brownoutCycleDuration)
                {
                    _brownoutCycleElapsed = Math.Max(0f, _brownoutCycleElapsed - _brownoutCycleDuration);
                    _brownoutEnteredThisCycle = false;
                    _brownoutState = BoothBrownoutState.On;
                    _brownoutActiveHoldElapsed = 0f;
                    if (_brownoutTriggerCount < MaxBrownoutTriggersPerShift)
                    {
                        RollNextBrownoutCycleParameters();
                    }
                }
            }
            else
            {
                _brownoutState = BoothBrownoutState.On;
                _brownoutActiveHoldElapsed = 0f;
            }
        }
    }

    private float GetEffectiveBrownoutOffElapsed() =>
        _devDebug.BrownoutForceMode == DebugBrownoutForceMode.ForceOff
            ? _forcedOffElapsed
            : _brownoutActiveHoldElapsed;

    private bool IsOffFlickerMomentarilyOn()
    {
        // Crisp 6-step flicker transition at the start of OFF: ON -> OFF -> ON -> OFF -> ON -> OFF (0.10s per step)
        float offT = GetEffectiveBrownoutOffElapsed();
        if (offT >= BrownoutOffFlickerTotalSeconds)
            return false;

        int stepIdx = Math.Clamp((int)(offT / BrownoutOffFlickerStepSeconds), 0, 5);
        return (stepIdx % 2) == 0;
    }

    private float ComputeBrownoutAmbientFactor()
    {
        return _brownoutState switch
        {
            BoothBrownoutState.Dim => 0.58f,
            BoothBrownoutState.Off => IsOffFlickerMomentarilyOn() ? 1.0f : 0.24f,
            _ => 1.0f
        };
    }

    private float ComputeBrownoutSpillFactor()
    {
        return _brownoutState switch
        {
            BoothBrownoutState.Dim => 0.28f,
            BoothBrownoutState.Off => IsOffFlickerMomentarilyOn() ? 0.0f : 1.0f,
            _ => 0.0f
        };
    }

    private float ComputeActiveWatchPowerLedBrightness()
    {
        return _brownoutState switch
        {
            BoothBrownoutState.Dim => 0.42f,
            BoothBrownoutState.Off => IsOffFlickerMomentarilyOn() ? 1.0f : 0.08f,
            _ => 1.0f
        };
    }

    private void ApplyBrownoutPassiveSurfaceLighting(int rx, int ry, int rw, int rh)
    {
        if (rw <= 0 || rh <= 0)
            return;

        float ambient = BrownoutAmbientFactor;
        if (ambient >= 0.999f)
            return;

        float spill = BrownoutSpillFactor;
        int clipX = Math.Max(0, rx);
        int clipY = Math.Max(TopBarH, ry);
        int clipR = Math.Min(Constants.VirtualWidth, rx + rw);
        int clipB = Math.Min(Constants.VirtualHeight, ry + rh);
        int clipW = clipR - clipX;
        int clipH = clipB - clipY;
        if (clipW <= 0 || clipH <= 0)
            return;

        Raylib.BeginScissorMode(clipX, clipY, clipW, clipH);

        // 1. Opaque-safe multiplicative ambient attenuation on passive surfaces (preserves alpha == 255)
        byte mulR = (byte)Math.Clamp((int)MathF.Round(255f * ambient), 48, 255);
        byte mulG = (byte)Math.Clamp((int)MathF.Round(255f * (ambient * 0.98f)), 46, 255);
        byte mulB = (byte)Math.Clamp((int)MathF.Round(255f * Math.Min(1f, ambient * 1.04f)), 52, 255);
        Raylib.BeginBlendMode(BlendMode.Multiplied);
        Raylib.DrawRectangle(clipX, clipY, clipW, clipH, new Color(mulR, mulG, mulB, (byte)255));
        Raylib.EndBlendMode();

        // 2. Reddish Brown Dwarf viewport spill + yellowish CRT & Glowing Warp Checker spill across passive metal/ceramic
        if (spill > 0.01f)
        {
            float vpCx = ViewportX + ViewportW * 0.52f;
            float vpCy = ViewportY + ViewportH - 14f;
            float trCx = _layout.Transcript.X + _layout.Transcript.W * 0.5f;
            float trCy = _layout.Transcript.Y + _layout.Transcript.H * 0.5f;
            float vsCx = _layout.Viewscreen.X + _layout.Viewscreen.W * 0.5f;
            float vsCy = _layout.Viewscreen.Y + _layout.Viewscreen.H * 0.5f;
            float warpCx = WarpGlassX + WarpGlassW * 0.5f;
            float warpCy = WarpGlassY + WarpGlassH * 0.5f;
            bool warpBacklit = IsSpectrometerOperational;

            int startX = (clipX / 16) * 16;
            int startY = (clipY / 16) * 16;

            Raylib.BeginBlendMode(BlendMode.Additive);
            for (int y = startY; y < clipB; y += 16)
            {
                float cy = y + 8f;
                for (int x = startX; x < clipR; x += 16)
                {
                    float cx = x + 8f;

                    // Reddish Brown Dwarf glow radiating downward/outward from armored viewport
                    float vdx = (cx - vpCx) * 0.85f;
                    float vdy = (cy - vpCy) * (cy < vpCy ? 1.35f : 0.88f);
                    float vpDist = MathF.Sqrt(vdx * vdx + vdy * vdy);
                    float vpNorm = Math.Clamp(1.0f - vpDist / 520.0f, 0f, 1f);
                    float redGlow = vpNorm * vpNorm * spill;

                    // Yellowish glow from CRT Comms Log, Hailing Viewscreen, and backlit Glowing Warp Checker
                    float tDist = MathF.Sqrt((cx - trCx) * (cx - trCx) + (cy - trCy) * (cy - trCy));
                    float vScrDist = MathF.Sqrt((cx - vsCx) * (cx - vsCx) + (cy - vsCy) * (cy - vsCy));
                    float tNorm = Math.Clamp(1.0f - tDist / 320.0f, 0f, 1f);
                    float vsNorm = Math.Clamp(1.0f - vScrDist / 260.0f, 0f, 1f);
                    float wNorm = 0f;
                    if (warpBacklit)
                    {
                        float wDist = MathF.Sqrt((cx - warpCx) * (cx - warpCx) + (cy - warpCy) * (cy - warpCy));
                        wNorm = Math.Clamp(1.0f - wDist / 250.0f, 0f, 1f);
                    }
                    float yellowGlow = Math.Max(tNorm * tNorm, Math.Max(vsNorm * vsNorm * 0.85f, wNorm * wNorm * 0.95f)) * spill;

                    byte addR = (byte)Math.Clamp((int)MathF.Round(redGlow * 54f + yellowGlow * 42f), 0, 88);
                    byte addG = (byte)Math.Clamp((int)MathF.Round(redGlow * 10f + yellowGlow * 30f), 0, 56);
                    byte addB = (byte)Math.Clamp((int)MathF.Round(redGlow * 9f + yellowGlow * 7f), 0, 22);
                    if (addR > 0 || addG > 0)
                    {
                        Raylib.DrawRectangle(x, y, 16, 16, new Color(addR, addG, addB, (byte)255));
                    }
                }
            }
            Raylib.EndBlendMode();
        }

        Raylib.EndScissorMode();
    }

    private void RedrawEmissiveHardwareScreensUnattenuated(VirtualMouseState mouse)
    {
        // Keep emissive CRTs, Stoker Digital LED, and backlit Glowing Warp Checker 100% unattenuated during brownouts!
        DrawCrtCommsDialogueLog(mouse);
        DrawTransponderIdPanel(mouse);
        DrawHailingViewscreenEmissiveInterior();

        if (IsSpsUnlocked)
        {
            Raylib.BeginScissorMode(SpsMiniX, SpsMiniY, SpsMiniW, SpsMiniH);
            DrawSpsControlColumn(mouse);
            Raylib.EndScissorMode();
        }

        if (IsStokerOperational)
        {
            DrawStokerDigitalLedBox(mouse);
        }

        if (IsSpectrometerOperational)
        {
            // Unattenuated Warp Coil mini-CRT + backlit Glowing Warp Checker waveform glass
            Raylib.BeginScissorMode(WarpPrinterX + 4, WarpPrinterY + 18, WarpHousingW - 8, 34);
            DrawWarpResiduePrinterAndRefSpot(mouse);
            Raylib.EndScissorMode();

            Raylib.BeginScissorMode(WarpGlassX, WarpGlassY, WarpGlassW, WarpGlassH);
            DrawWarpResiduePrinterAndRefSpot(mouse);
            Raylib.EndScissorMode();
        }
    }

    private void DrawHailingViewscreenEmissiveInterior()
    {
        DeskRect vs = _layout.Viewscreen;
        DeskRect pr = _layout.Portrait;
        Raylib.BeginScissorMode(pr.X - 1, pr.Y - 1, pr.W + 2, pr.H + 2);
        DrawHailingViewscreen(default);
        Raylib.EndScissorMode();

        if (_layout.IsTransponderSeparatePanel && !_layout.Use3xPortrait)
        {
            int sbX = vs.X + 20;
            int sbY = pr.Bottom + 12;
            int sbW = vs.W - 40;
            Raylib.BeginScissorMode(sbX, sbY, sbW, 26);
            DrawHailingViewscreen(default);
            Raylib.EndScissorMode();
        }
    }
}
