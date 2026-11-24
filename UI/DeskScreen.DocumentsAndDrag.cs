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
    // Contraband Lap Pocket & Loose Desk Contraband Recovery
    // =========================================================================
    private void TryHandleLapPocketClick(VirtualMouseState mouse)
    {
        if (!mouse.IsHit(LapPocketX, LapPocketY, LapPocketW, LapPocketH))
            return;

        if (_gameState.PocketItems.Count == 0)
            return;

        int slotW = Math.Max(60, (LapPocketW - 16) / Math.Max(1, _gameState.PocketCapacity));
        int clickedIdx = Math.Clamp((mouse.X - (LapPocketX + 8)) / slotW, 0, _gameState.PocketItems.Count - 1);
        ContrabandPocketItem picked = _gameState.PocketItems[clickedIdx];

        // Blank, Copied, or Covert Mission Data Slate -> pull out onto Booth 04 desk!
        if (_activeDeskContrabandItem != null)
        {
            if (_activeDeskContrabandItem.IsBurnt)
            {
                if (_contrabandSlateInSlateBus)
                {
                    AudioHooks.Play("citation_buzz");
                    AddSystemLogLine("[SLATE BUS] UNPLUG THE BURNT CRYSTAL SLATE FROM SLATE BUS FIRST.", CommsTextTone.AmberPrimary);
                    return;
                }

                // Park set-aside burnt slate visually on desk until Clock-Out so player can pull a fresh slate
                DraggableDeskItem prevBurnt = _deskItems[DeskItemId.ContrabandSlate];
                _parkedBurntSlatesOnDesk.Add((prevBurnt.X, prevBurnt.Y));
                _activeDeskContrabandItem = null;
            }
            else
            {
                AudioHooks.Play("citation_buzz");
                AddSystemLogLine("[LAP POCKET] STOW THE ACTIVE COVERT SLATE FIRST BEFORE PULLING ANOTHER.", CommsTextTone.AmberPrimary);
                return;
            }
        }

        _activeDeskContrabandItem = picked;
        _gameState.PocketItems.RemoveAt(clickedIdx);
        _contrabandSlateInSlateBus = false;

        DeskRect bounds = _layout.FreeDragBounds;
        DraggableDeskItem cItem = _deskItems[DeskItemId.ContrabandSlate];
        cItem.X = Math.Clamp(mouse.X - ContrabandSlateW / 2, bounds.X, bounds.Right - ContrabandSlateW);
        cItem.Y = Math.Clamp(LapPocketY - ContrabandSlateH - 12, _layout.FreeDeskRestMinY, bounds.Bottom - ContrabandSlateH);
        cItem.IsVisible = true;
        BringDeskItemToFront(DeskItemId.ContrabandSlate);

        _draggingItemId = DeskItemId.ContrabandSlate;
        _dragOffsetX = ContrabandSlateW / 2;
        _dragOffsetY = ContrabandSlateH / 2;
        _dragStartMouseX = mouse.X;
        _dragStartMouseY = mouse.Y;
        AudioHooks.Play("terminal_key_click");
        _gameState.SaveToDisk();
    }

    private bool IsItemOverLapPocket(VirtualMouseState mouse, DraggableDeskItem item)
    {
        if (mouse.IsHit(LapPocketX - 6, LapPocketY - 12, LapPocketW + 12, LapPocketH + 20))
            return true;

        return item.X < LapPocketX + LapPocketW &&
               item.X + item.Width > LapPocketX &&
               item.Y + item.Height >= LapPocketY + 8;
    }

    private bool TryStowContrabandDeskSlateInLapPocket()
    {
        if (_activeDeskContrabandItem == null)
            return false;

        if (_activeDeskContrabandItem.IsBurnt ||
            _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBurntMissionSlate)
        {
            AudioHooks.Play("citation_buzz");
            AddSystemLogLine("[LAP POCKET] CRYSTAL BURNT // ONE-TIME USE -- CANNOT BE STOWED. SET ASIDE ON DESK.", CommsTextTone.AmberDimmed);
            return false;
        }

        if (_gameState.PocketItems.Count >= _gameState.PocketCapacity)
        {
            AudioHooks.Play("citation_buzz");
            AddSystemLogLine($"[LAP POCKET FULL] CAPACITY {_gameState.PocketItems.Count}/{_gameState.PocketCapacity}.", CommsTextTone.AlertCrimson);
            return false;
        }

        _gameState.PocketItems.Add(_activeDeskContrabandItem);
        _activeDeskContrabandItem = null;
        _contrabandSlateInSlateBus = false;
        _deskItems[DeskItemId.ContrabandSlate].IsVisible = false;
        _gameState.SaveToDisk();
        AudioHooks.Play("terminal_key_confirm");
        return true;
    }

    /// <summary>
    /// Automatically discards any burnt single-use covert slates sitting on the desk at Clock-Out,
    /// while guaranteeing any valid loose Contraband Slate is recovered cleanly into Pocket/Vent/Chute
    /// (called both at shift Clock-Out and on DeskScreen.OnExit mid-shift transitions).
    /// </summary>
    public void TryAutoRecoverLooseContrabandAtClockOut()
    {
        _parkedBurntSlatesOnDesk.Clear();
        _isDownloadingToBlankSlate = false;
        _isUploadingCovertMissionSlate = false;
        _covertUploadElapsed = 0f;

        if (_activeDeskContrabandItem == null)
            return;

        if (_activeDeskContrabandItem.IsBurnt ||
            _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBurntMissionSlate)
        {
            // Automatically discard burnt single-use slates at shift Clock-Out / exit
            _activeDeskContrabandItem = null;
            _contrabandSlateInSlateBus = false;
            if (_deskItems.TryGetValue(DeskItemId.ContrabandSlate, out DraggableDeskItem? burntItem))
            {
                burntItem.IsVisible = false;
            }
            return;
        }

        if (_gameState.PocketItems.Count < _gameState.PocketCapacity)
        {
            _gameState.PocketItems.Add(_activeDeskContrabandItem);
        }
        else if (_gameState.VentStashItems.Count < _config.VentStashCapacity)
        {
            _gameState.VentStashItems.Add(_activeDeskContrabandItem);
        }
        else
        {
            _gameState.ServiceChuteItems.Add(_activeDeskContrabandItem);
        }

        _activeDeskContrabandItem = null;
        _contrabandSlateInSlateBus = false;
        if (_deskItems.TryGetValue(DeskItemId.ContrabandSlate, out DraggableDeskItem? cItem))
        {
            cItem.IsVisible = false;
        }
        _gameState.SaveToDisk();
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
            else if (id == DeskItemId.ContrabandSlate)
            {
                if (_isUploadingCovertMissionSlate)
                {
                    // Hold locked while active Covert TX transfer is running
                    _draggingItemId = null;
                    AudioHooks.Play("citation_buzz");
                    return true;
                }
                if (!(_isDownloadingToBlankSlate && _phase == EncounterPhase.DownloadingProtocols && _contrabandSlateInSlateBus))
                {
                    _contrabandSlateInSlateBus = false;
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
        DeskRect dock = _layout.ClearanceDock;
        if (mouse.IsHit(dock.X, dock.Y, dock.W, dock.H))
            return true;

        return slate.X + slate.Width >= dock.X - 18 &&
               slate.Y + slate.Height >= dock.Y &&
               slate.Y <= dock.Bottom;
    }

    private bool IsDraggingDataSlateOverSlateBus(VirtualMouseState mouse, DraggableDeskItem slate)
    {
        DeskRect bus = _layout.SlateBus;
        if (mouse.IsHit(bus.X - 12, bus.Y - 8, bus.W + 24, bus.H + 36))
            return true;

        return slate.X < bus.Right + 16 &&
               slate.X + slate.Width > bus.X - 16 &&
               slate.Y <= bus.Bottom + 24;
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
                !_isDownloadingToBlankSlate &&
                IsDataSlateInSlateBus)
            {
                int pullDx = mouse.X - _dragStartMouseX;
                int pullDy = mouse.Y - _dragStartMouseY;
                float pullDist = MathF.Sqrt(pullDx * pullDx + pullDy * pullDy);
                if (pullDist < UnplugMidDownloadDragThresholdPx)
                    return;

                AbortProtocolDownloadMidStream();
            }
            else if (item.Id == DeskItemId.ContrabandSlate &&
                     _phase == EncounterPhase.DownloadingProtocols &&
                     _isDownloadingToBlankSlate &&
                     _contrabandSlateInSlateBus)
            {
                int pullDx = mouse.X - _dragStartMouseX;
                int pullDy = mouse.Y - _dragStartMouseY;
                float pullDist = MathF.Sqrt(pullDx * pullDx + pullDy * pullDy);
                if (pullDist < UnplugMidDownloadDragThresholdPx)
                    return;

                if (ShiftController.CanInterruptTransfer(_protocolDownloadElapsed, ProtocolDownloadDurationSeconds))
                {
                    AbortBlankSlateDownloadMidStream();
                }
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
            // the Glowing Warp Checker glass (y >= 264) while keeping non-dockable documents below Row 2.
            DeskRect bounds = _layout.FreeDragBounds;
            DeskRect dock = _layout.ClearanceDock;
            int minDragY = item.Id switch
            {
                DeskItemId.WarpStrip => WarpRefSpotY,
                DeskItemId.ReferenceManual => _layout.FreeDeskRestMinY,
                _ => bounds.Y
            };
            int maxDragX = item.Id == DeskItemId.DataSlate
                ? (dock.Right - item.Width)
                : (bounds.Right - item.Width);
            int maxDragY = bounds.Bottom - item.Height;

            item.X = Math.Clamp(mouse.X - _dragOffsetX, bounds.X, maxDragX);
            item.Y = Math.Clamp(mouse.Y - _dragOffsetY, minDragY, maxDragY);
            return;
        }

        if (mouse.LeftReleased || !mouse.LeftDown)
        {
            DeskRect bounds = _layout.FreeDragBounds;
            DeskRect bus = _layout.SlateBus;
            DeskRect slot = _layout.ClearanceSlot;
            int restMinY = _layout.FreeDeskRestMinY;

            if (item.Id == DeskItemId.DataSlate && _preShiftStage == PreShiftStage.None)
            {
                if (_phase == EncounterPhase.DownloadingProtocols && IsDataSlateInSlateBus)
                {
                    item.X = _layout.SlateBusDockedX;
                    item.Y = _layout.SlateBusDockedY;
                    _draggingItemId = null;
                    return;
                }

                if (_phase == EncounterPhase.UploadingClearance && IsDataSlateInClearanceDock)
                {
                    item.X = slot.X;
                    item.Y = slot.Y;
                    _draggingItemId = null;
                    return;
                }

                if (_phase == EncounterPhase.TransmittingClearance && IsDataSlateInSlateBus)
                {
                    item.X = _layout.SlateBusDockedX;
                    item.Y = _layout.SlateBusDockedY;
                    _draggingItemId = null;
                    return;
                }

                bool overSlateBus = IsDraggingDataSlateOverSlateBus(mouse, item);
                bool canSlotInClearanceDock = (_phase is EncounterPhase.InspectingSlateAndShip or EncounterPhase.DownloadAborted) &&
                                              _slateTelemetry == DataSlateTelemetryState.Loaded;
                bool canReSeatClearedSlate = _phase == EncounterPhase.AwaitingClearanceReturnToTopPort &&
                                             _slateTelemetry == DataSlateTelemetryState.ClearedReadyToTransmit;
                bool overClearanceDock = IsDraggingDataSlateOverClearanceDock(mouse, item);

                if (overSlateBus)
                {
                    if (_contrabandSlateInSlateBus)
                    {
                        AudioHooks.Play("citation_buzz");
                        AddSystemLogLine("[SLATE BUS] PORT OCCUPIED -- UNPLUG ACTIVE DATA SLATE FIRST.", CommsTextTone.AlertCrimson);
                        item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                        item.Y = Math.Clamp(item.Y, restMinY, bounds.Bottom - item.Height);
                        _slateLocation = DataSlateLocation.DeskFree;
                    }
                    else
                    {
                        item.X = _layout.SlateBusDockedX;
                        item.Y = _layout.SlateBusDockedY;
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
                }
                else if (overClearanceDock && canSlotInClearanceDock)
                {
                    // Snap visibly into Clearance Upload Dock bay and start encryption!
                    AudioHooks.Play("dock_lock");
                    BeginClearanceUploadInDock();
                }
                else if (overClearanceDock && canReSeatClearedSlate)
                {
                    // Re-seat already cleared slate cleanly in the dock bay without re-running encryption
                    item.X = slot.X;
                    item.Y = slot.Y;
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
                    item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                    item.Y = Math.Clamp(item.Y, restMinY, bounds.Bottom - item.Height);
                    _slateLocation = DataSlateLocation.DeskFree;
                }
            }
            else if (item.Id == DeskItemId.ContrabandSlate && _activeDeskContrabandItem != null)
            {
                if (_phase == EncounterPhase.DownloadingProtocols &&
                    _isDownloadingToBlankSlate &&
                    _contrabandSlateInSlateBus)
                {
                    item.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
                    item.Y = _layout.SlateBusDockedY;
                    _draggingItemId = null;
                    return;
                }

                bool overLap = IsItemOverLapPocket(mouse, item);
                bool overBus = IsDraggingDataSlateOverSlateBus(mouse, item);
                bool overClearanceDock = IsDraggingDataSlateOverClearanceDock(mouse, item);
                bool isBurnt = _activeDeskContrabandItem.IsBurnt ||
                               _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBurntMissionSlate;

                if (overLap)
                {
                    if (TryStowContrabandDeskSlateInLapPocket())
                    {
                        _draggingItemId = null;
                        return;
                    }
                    // Burnt slate or full pocket: keep resting cleanly on desk above Lap Pocket
                    item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                    item.Y = Math.Clamp(Math.Min(item.Y, LapPocketY - item.Height - 8), restMinY, bounds.Bottom - item.Height);
                }
                else if (isBurnt && overBus)
                {
                    // Burnt slates are permanently locked out of Slate Bus
                    _contrabandSlateInSlateBus = false;
                    AudioHooks.Play("citation_buzz");
                    AddSystemLogLine("[SLATE BUS] CRYSTAL BURNT // ONE-TIME USE -- CANNOT RE-UPLOAD.", CommsTextTone.AlertCrimson);
                    item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                    item.Y = Math.Clamp(Math.Max(item.Y, bus.Bottom + 16), restMinY, bounds.Bottom - item.Height);
                }
                else if (overClearanceDock)
                {
                    // Contraband / burnt slates can never be slotted into the Clearance Upload Dock
                    AudioHooks.Play("citation_buzz");
                    if (isBurnt)
                    {
                        AddSystemLogLine("[CLEARANCE DOCK] CRYSTAL BURNT // ONE-TIME USE -- REJECTED.", CommsTextTone.AlertCrimson);
                    }
                    item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                    item.Y = Math.Clamp(item.Y, restMinY, bounds.Bottom - item.Height);
                }
                else if (overBus)
                {
                    if (IsDataSlateInSlateBus)
                    {
                        AudioHooks.Play("citation_buzz");
                        AddSystemLogLine("[SLATE BUS] PORT OCCUPIED -- UNPLUG ACTIVE DATA SLATE FIRST.", CommsTextTone.AlertCrimson);
                        item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                        item.Y = Math.Clamp(item.Y, restMinY, bounds.Bottom - item.Height);
                    }
                    else
                    {
                        item.X = _layout.SlateBusDockedX + (DataSlateW - ContrabandSlateW) / 2;
                        item.Y = _layout.SlateBusDockedY;
                        _contrabandSlateInSlateBus = true;
                        AudioHooks.Play("dock_lock");

                        if (_activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBlankDataSlate &&
                            _activeShip != null)
                        {
                            if (_phase == EncounterPhase.AwaitingPortForDownload)
                            {
                                BeginBlankSlateInterceptDownload(triggeredByReTransmit: false);
                            }
                            else if (_phase == EncounterPhase.InspectingSlateAndShip)
                            {
                                BeginBlankSlateInterceptDownload(triggeredByReTransmit: true);
                            }
                            // When _phase == EncounterPhase.DownloadAborted, dock cleanly in Slate Bus
                            // and wait for explicit [RE-TRANSMIT] click, matching Primary Data Slate parity.
                        }
                        else if (_activeDeskContrabandItem.IsMissionSlate &&
                                 _activeShip != null &&
                                 _phase is EncounterPhase.InspectingSlateAndShip
                                     or EncounterPhase.AwaitingPortForDownload
                                     or EncounterPhase.DownloadAborted
                                     or EncounterPhase.AwaitingClearanceReturnToTopPort)
                        {
                            BeginCovertMissionSlateUpload();
                        }
                    }
                }
                else
                {
                    item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                    item.Y = Math.Clamp(item.Y, restMinY, bounds.Bottom - item.Height);
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
                int minRestY = item.Id == DeskItemId.WarpStrip ? WarpRefSpotY : restMinY;
                item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
                item.Y = Math.Clamp(item.Y, minRestY, bounds.Bottom - item.Height);
            }

            _draggingItemId = null;
        }
    }

    private void BringDeskItemToFront(DeskItemId id)
    {
        _zOrder.Remove(id);
        _zOrder.Add(id);
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
                DeskRect bounds = _layout.FreeDragBounds;
                strip.X = Math.Clamp(mouse.X - StripWidth / 2, bounds.X, bounds.Right - strip.Width);
                strip.Y = Math.Clamp(mouse.Y - StripFullHeight / 2, WarpRefSpotY, bounds.Bottom - strip.Height);
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
    // Contraband Desk Slate Rendering
    // =========================================================================
    private void DrawContrabandDeskSlate(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        if (_activeDeskContrabandItem == null)
            return;

        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;

        bool isBurnt = _activeDeskContrabandItem.IsBurnt ||
                       _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeBurntMissionSlate;
        if (isBurnt)
        {
            DrawBurntContrabandSlateBody(x, y, w, h, isTopmost, _contrabandSlateInSlateBus);
            return;
        }

        bool isMission = _activeDeskContrabandItem.IsMissionSlate;
        bool isCopied = _activeDeskContrabandItem.ItemType == ContrabandPocketItem.TypeCopiedDataSlate;
        bool isBlankRx = IsBlankSlateDownloading && _contrabandSlateInSlateBus && !isMission && !isCopied;
        bool pulse = ((_animTick / 8) % 2) == 0;

        // Visually distinct from heavy dark-steel Primary Data Slate:
        // - Unmarked black Blank Data Slate: sleek unmarked matte-black casing (matching Primary RX animation during download)
        // - Captured Packet slate: dark matte charcoal-amber casing with teal captured readout
        // - Covert Mission slate: dark tactical olive-drab casing with alert crimson warning stripe
        Color shellBg = isMission
            ? new Color(28, 36, 24, 255)
            : (isCopied ? new Color(34, 30, 22, 255) : new Color(16, 18, 22, 255));
        Color borderCol = isBlankRx
            ? Palette.ClearanceTeal
            : (isMission
                ? Palette.AlertCrimson
                : (isCopied ? Palette.CrtAmberPrimary : new Color(56, 62, 72, 255)));
        Color headerCol = isBlankRx
            ? Palette.ClearanceTeal
            : (isMission
                ? Palette.AlertCrimson
                : (isCopied ? Palette.ClearanceTeal : Palette.CrtAmberPrimary));

        Raylib.DrawRectangle(x + 3, y + 3, w, h, new Color(4, 6, 8, 175));
        Raylib.DrawRectangle(x, y, w, h, shellBg);
        Raylib.DrawRectangleLines(x, y, w, h, isBlankRx ? Palette.ClearanceTeal : (isTopmost ? Palette.CrtAmberPrimary : borderCol));
        Raylib.DrawRectangleLines(x + 2, y + 2, w - 4, h - 4, Palette.OutlineVoid);

        // Top contact pins (pulse identically to Primary Data Slate during 1.80s RX download)
        for (int p = 0; p < 5; p++)
        {
            Color pinCol = isBlankRx
                ? (pulse ? Palette.SpsShieldArcIce : Palette.ClearanceTeal)
                : (_contrabandSlateInSlateBus ? Palette.CrtAmberPrimary : Palette.OxidizedBronzeWorn);
            Raylib.DrawRectangle(x + 20 + p * 26, y - 2, 12, 4, pinCol);
        }

        // Top header strip
        Raylib.DrawRectangle(x + 6, y + 8, w - 12, 22, Palette.OutlineVoid);
        float rxProg = BlankSlateDownloadProgress;
        string hdr = isBlankRx
            ? $"RX PACKET {(int)MathF.Round(rxProg * 100f)}%"
            : (isMission
                ? "COVERT MISSION SLATE"
                : (isCopied ? "CAPTURED PACKET" : "UNMARKED BLANK SLATE"));
        Raylib.DrawText(TruncateByPixelWidth(hdr, 10, w - 20), x + 10, y + 14, 10, headerCol);

        // Identical segmented progress bar directly below header during Blank Slate 1.80s download
        if (isBlankRx)
        {
            const int segs = 11;
            int filled = (int)MathF.Floor(rxProg * segs);
            for (int s = 0; s < segs; s++)
            {
                Raylib.DrawRectangle(x + 9 + s * 13, y + 32, 10, 2, s <= filled ? Palette.ClearanceTeal : Palette.WeatheredGunmetalMid);
            }
        }

        // Inner CRT readout face
        Raylib.DrawRectangle(x + 8, y + 36, w - 16, h - 68, new Color(10, 13, 15, 255));
        Raylib.DrawRectangleLines(x + 8, y + 36, w - 16, h - 68, isBlankRx ? Palette.ClearanceTeal : borderCol);

        if (isBlankRx)
        {
            // Identical packet-stream download animation matching Primary Data Slate's 1.80s RX readout
            int pct = (int)MathF.Round(rxProg * 100f);
            Raylib.DrawText("CARRIER: LOCKED", x + 14, y + 44, 10, Palette.ClearanceTeal);

            int barX = x + 14;
            int barY = y + 60;
            int barW = w - 28;
            int fillW = Math.Clamp((int)MathF.Round(barW * rxProg), 0, barW);
            Raylib.DrawRectangle(barX, barY, barW, 10, Palette.DeepSpaceVoid);
            if (fillW > 0)
            {
                Raylib.DrawRectangle(barX, barY, fillW, 10, Palette.ClearanceTeal);
            }
            Raylib.DrawRectangleLines(barX, barY, barW, 10, Palette.WeatheredGunmetalEdge);

            // Progressive packet stream rows revealing smoothly across the 1.80s duration
            int frameShift = (_animTick / 4) & 0xFF;
            if (rxProg >= 0.15f)
                Raylib.DrawText($"HDR: 0x{(0x4A + frameShift) & 0xFF:X2} {(0x91 + frameShift * 3) & 0xFF:X2} SYNC", x + 14, y + 78, 10, Palette.CrtAmberPrimary);
            if (rxProg >= 0.40f)
            {
                string pfxHint = _activeShip != null ? _activeShip.Slate.DeclaredPrefix : "---";
                Raylib.DrawText($"BUS: {pfxHint} STREAM", x + 14, y + 94, 10, Palette.ClearanceTeal);
            }
            if (rxProg >= 0.65f)
                Raylib.DrawText($"CHK: 0x{(0xC0 + frameShift * 5) & 0xFF:X2}F{(frameShift & 0xF):X1} OK", x + 14, y + 110, 10, Palette.CrtAmberDimmed);
            if (rxProg >= 0.85f)
                Raylib.DrawText("SEALING BUFFER...", x + 14, y + 126, 10, Palette.ClearanceTeal);
            else
                Raylib.DrawText($"PACKET STREAM {pct:D2}%", x + 14, y + 126, 10, Palette.CrtAmberDimmed);
        }
        else if (isMission)
        {
            Raylib.DrawText(_activeDeskContrabandItem.DisplayName, x + 14, y + 46, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText($"TGT: {_activeDeskContrabandItem.MissionTargetReg}", x + 14, y + 66, 10, Palette.ClearanceTeal);
            Raylib.DrawText("SEALED ONE-TIME USE", x + 14, y + 86, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("SLOT INTO SLATE BUS", x + 14, y + 104, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("AVOID KDF- / BMC-!", x + 14, y + 128, 10, Palette.AlertCrimson);
        }
        else if (isCopied)
        {
            Raylib.DrawText($"REG: {_activeDeskContrabandItem.TargetShipRegistration}", x + 14, y + 48, 10, Palette.ClearanceTeal);
            Raylib.DrawText($"PFX: {_activeDeskContrabandItem.TargetShipPrefix}", x + 14, y + 66, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText(TruncateByPixelWidth($"ORG: {_activeDeskContrabandItem.OriginStarSystem}", 10, w - 28), x + 14, y + 84, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("STOW IN LAP POCKET", x + 14, y + 114, 10, Palette.ClearanceTeal);
            Raylib.DrawText("SEND VIA CRYPTO.", x + 14, y + 128, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            Raylib.DrawText("BUFFER: [EMPTY]", x + 14, y + 48, 10, Palette.CrtAmberPrimary);
            Raylib.DrawText("UNPLUG PRIMARY SLATE", x + 14, y + 74, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("& SLOT THIS INTO", x + 14, y + 88, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("SLATE BUS TO COPY", x + 14, y + 102, 10, Palette.CrtAmberDimmed);
            Raylib.DrawText("INCOMING PROTOCOLS.", x + 14, y + 116, 10, Palette.CrtAmberDimmed);
        }

        string foot = isBlankRx
            ? "HOLD IN SLATE BUS"
            : (_contrabandSlateInSlateBus ? "[LINKED IN BUS]" : "DRAG -> BUS / POCKET");
        Raylib.DrawText(TruncateByPixelWidth(foot, 10, w - 16), x + 10, y + h - 22, 10,
            isBlankRx ? Palette.ClearanceTeal : Palette.WeatheredGunmetalEdge);
    }

    private static void DrawBurntContrabandSlateBody(int x, int y, int w, int h, bool isTopmost, bool inBus)
    {
        Color shellBg = new(18, 19, 21, 255);
        Color borderCol = isTopmost ? new Color(118, 122, 130, 255) : new Color(56, 60, 66, 255);
        Color charredGlass = new(9, 10, 12, 255);
        Color crackHighlight = new(124, 132, 142, 255);
        Color crackMid = new(74, 80, 88, 255);
        Color dimReadout = new(96, 100, 108, 255);

        Raylib.DrawRectangle(x + 3, y + 3, w, h, new Color(4, 6, 8, 175));
        Raylib.DrawRectangle(x, y, w, h, shellBg);
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);
        Raylib.DrawRectangleLines(x + 2, y + 2, w - 4, h - 4, Palette.OutlineVoid);

        // Charred blackened contact pins
        for (int p = 0; p < 5; p++)
        {
            Raylib.DrawRectangle(x + 20 + p * 26, y - 2, 12, 4, new Color(42, 38, 36, 255));
        }

        // Dead header strip
        Raylib.DrawRectangle(x + 6, y + 8, w - 12, 22, new Color(12, 13, 15, 255));
        Raylib.DrawText("CRYSTAL BURNT", x + 10, y + 14, 10, new Color(110, 114, 122, 255));

        // Dark charred glass face with crisp diagonal crystal-fracture cracks
        int gx = x + 8;
        int gy = y + 36;
        int gw = w - 16;
        int gh = h - 68;
        Raylib.DrawRectangle(gx, gy, gw, gh, charredGlass);
        Raylib.DrawRectangleLines(gx, gy, gw, gh, new Color(44, 48, 54, 255));

        // Opaque dark scorch patches around central thermal burn node
        Raylib.DrawRectangle(gx + 18, gy + 16, 46, 32, new Color(16, 14, 14, 255));
        Raylib.DrawRectangle(gx + 54, gy + 44, 58, 42, new Color(18, 15, 14, 255));
        Raylib.DrawRectangle(gx + 32, gy + 74, 48, 28, new Color(14, 13, 14, 255));

        // Crisp diagonal crystal-fracture cracks spanning the charred glass (shadow + quartz specular edge)
        Raylib.DrawLine(gx + 4, gy + 10, gx + 76, gy + 58, Palette.OutlineVoid);
        Raylib.DrawLine(gx + 5, gy + 9, gx + 77, gy + 57, crackHighlight);
        Raylib.DrawLine(gx + 76, gy + 58, gx + gw - 6, gy + gh - 8, Palette.OutlineVoid);
        Raylib.DrawLine(gx + 77, gy + 57, gx + gw - 5, gy + gh - 9, crackHighlight);

        // Cross-diagonal fracture & branching quartz splinters
        Raylib.DrawLine(gx + gw - 12, gy + 14, gx + 68, gy + 62, crackMid);
        Raylib.DrawLine(gx + gw - 11, gy + 13, gx + 69, gy + 61, crackHighlight);
        Raylib.DrawLine(gx + 68, gy + 62, gx + 14, gy + gh - 14, crackMid);
        Raylib.DrawLine(gx + 44, gy + 36, gx + 22, gy + 68, crackMid);
        Raylib.DrawLine(gx + 92, gy + 74, gx + 124, gy + 52, crackMid);
        Raylib.DrawLine(gx + 76, gy + 58, gx + 88, gy + gh - 10, crackMid);

        // Dim dead crystal readout cleanly legible over charred glass
        Raylib.DrawRectangle(gx + 6, gy + 42, gw - 12, 34, new Color(10, 11, 13, 218));
        const string b1 = "CRYSTAL BURNT";
        const string b2 = "// ONE-TIME USE";
        Raylib.DrawText(b1, gx + (gw - Raylib.MeasureText(b1, 10)) / 2, gy + 47, 10, dimReadout);
        Raylib.DrawText(b2, gx + (gw - Raylib.MeasureText(b2, 10)) / 2, gy + 61, 10, new Color(76, 80, 88, 255));

        string foot = inBus ? "UNPLUG TO FREE BUS" : "SPENT -- LEAVE ON DESK";
        Raylib.DrawText(TruncateByPixelWidth(foot, 10, w - 16), x + 10, y + h - 22, 10, new Color(82, 86, 94, 255));
    }

    // =========================================================================
    // Primary Data Slate: exact 184 x 216
    // Visibly docks beneath Slate Bus AND inside Clearance Dock
    // Day 1 = Clean Basic Registration | Day 2 = +WPN/SHLD | Day 3+ = Full
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
            : (isUploading
                ? Palette.OxidizedBronzeWorn
                : (isCleared || isTransmitting
                    ? Palette.ClearanceTeal
                    : (isTopmost ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge)));

        // Heavy Dark-Steel Chassis (184 x 216)
        Raylib.DrawRectangle(x, y, w, h, new Color(22, 27, 34, 255));
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);
        Raylib.DrawRectangleLines(x + 1, y + 1, w - 2, h - 2, Palette.WeatheredGunmetalMid);

        // 6 top contact tabs mating with Slate Bus OR Clearance Dock connector pins
        // (Steady muted brass/amber when docked in Clearance Dock; teal when linked in Slate Bus)
        for (int pin = 0; pin < 6; pin++)
        {
            int px = x + 14 + pin * 28;
            Color tabCol = IsDataSlateInSlateBus
                ? ((isDownloading || isTransmitting) && pulse ? Palette.SpsShieldArcIce : Palette.ClearanceTeal)
                : Palette.OxidizedBronzeWorn;
            Raylib.DrawRectangle(px, y - 1, 12, 2, tabCol);
        }

        // Top Header Bar (y + 2 .. y + 17)
        Color headerBg = isAborted || isDetainWiping
            ? Palette.BrownDwarfGlowDeep
            : (isCleared || isTransmitting ? Palette.OxidizedBronzeShadow : Palette.WeatheredGunmetalMid);
        Raylib.DrawRectangle(x + 2, y + 2, w - 4, 16, headerBg);

        float dl = ProtocolDownloadProgress;
        float txProg = Math.Clamp(_clearanceTransmitElapsed / ClearanceTransmitDurationSeconds, 0f, 1f);

        // No duplicate top ENCRYPTING XX% bar over the slate face during encryption (keep only the bottom progress bar)
        string slateHeader = isDetainWiping
            ? "CLAMP LOCK -- WIPING"
            : _slateTelemetry switch
            {
                DataSlateTelemetryState.Downloading => $"RX PACKET {(int)MathF.Round(dl * 100f)}%",
                DataSlateTelemetryState.DownloadAborted => "LINK LOST -- ABORTED",
                DataSlateTelemetryState.UploadingClearance => "DATA SLATE [DOCKED]",
                DataSlateTelemetryState.ClearedReadyToTransmit => "CLEARED -> SLATE BUS",
                DataSlateTelemetryState.TransmittingClearance => $"TX CLEAR {(int)MathF.Round(txProg * 100f)}%",
                DataSlateTelemetryState.Loaded when IsDataSlateInSlateBus => "DATA SLATE [LINKED]",
                DataSlateTelemetryState.Loaded when IsDataSlateInClearanceDock => "DATA SLATE [DOCKED]",
                DataSlateTelemetryState.Loaded => "PRIMARY DATA SLATE",
                _ => IsDataSlateInSlateBus ? "DATA SLATE [LINKED]" : "DATA SLATE [FREE]"
            };
        Color headerFg = isAborted || isDetainWiping
            ? Palette.AlertCrimson
            : (isDownloading || isCleared || isTransmitting ? Palette.ClearanceTeal : Palette.CrtAmberPrimary);
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

        // Progressive Transfer Segmented Bar (y + 19) for RX Download / TX Clearance only (NOT during Dock Encryption!)
        if (isDownloading || isTransmitting)
        {
            float segProg = isDownloading ? dl : txProg;
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
            isCleared ? Palette.ClearanceTeal : Palette.CrtAmberDimmed);

        int rightColX = x + 75;
        int rightColMaxPx = w - 80; // 104px
        int fieldX = x + 7;
        int fieldMaxPx = w - 14;    // 170px

        if (_gameState.Day == 1)
        {
            // =================================================================
            // DAY 1 CLEAN MINIMAL DATA SLATE (REG CODE Highlightable in Protocol Review!)
            // =================================================================
            if (dl >= 0.15f)
            {
                Raylib.DrawRectangle(x + 73, y + 22, w - 78, 30, Palette.OutlineVoid);
                Raylib.DrawRectangleLines(x + 73, y + 22, w - 78, 30, Palette.OxidizedBronzeWorn);
                DrawSlateFieldHighlight(mouse, x + 73, y + 22, w - 78, 30, ReviewTarget.DataSlatePrefix);
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
            // DAY 2 DATA SLATE (REG CODE + WPN Highlightable in Protocol Review!)
            // =================================================================
            bool wpnFault = IsTargetFaultHighlighted(ReviewTarget.DataSlateWeapons);
            DrawSlateFieldHighlight(mouse, x + 73, y + 21, w - 78, 26, ReviewTarget.DataSlatePrefix);
            DrawSlateFieldHighlight(mouse, x + 73, y + 50, w - 78, 15, ReviewTarget.DataSlateWeapons);

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
            // Subtle hint shown ONLY while hovering the docked slate during encryption
            if (mouse.IsHit(x, y, w, h))
            {
                Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.OxidizedBronzeShadow);
                const string msg = "DRAG OUT TO ABORT";
                Raylib.DrawText(msg, x + (w - Raylib.MeasureText(msg, 10)) / 2, y + h - 14, 10, Palette.CrtAmberPrimary);
            }
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

        if (_protocolReviewModeActive && IsShipInspectableForReview && IsSlateReadableForReview && IsReviewTargetUnlocked(target))
        {
            DrawReviewHotspotOutline(mouse, rx, ry, rw, rh, _reviewFirstSelection == target);
        }
    }

    // =========================================================================
    // Day 4+ Movable Desk Reference Manual (1. PREFIXES & 2. HULLS ONLY; tightened 280x192 layout)
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

    // =========================================================================
    // Day 3+ Extruding / Torn-Off Thermal Magnetic Warp Strip
    // =========================================================================
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
}
