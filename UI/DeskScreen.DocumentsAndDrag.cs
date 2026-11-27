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

            if (id == DeskItemId.FieldBooklet && TryHandleFieldBookletInternalClick(mouse, item))
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

    private void SyncManualSelectionToDirectoryIndex(int dirIdx)
    {
        _manualDirectoryIndex = Math.Clamp(dirIdx, 0, ManualPrefixDirectory.Length - 1);
        ShipFaction targetFaction = ManualPrefixDirectory[_manualDirectoryIndex].VisualFaction;
        for (int f = 0; f < ShipRenderer.AllFactions.Count; f++)
        {
            if (ShipRenderer.AllFactions[f] == targetFaction)
            {
                _manualFactionIndex = f;
                break;
            }
        }
        _manualClassIndex = 0;
    }

    private bool TryHandleReferenceManualInternalClick(VirtualMouseState mouse, DraggableDeskItem manual)
    {
        if (!IsManualUnlocked)
            return false;

        int mx = manual.X;
        int my = manual.Y;
        int w = manual.Width;
        bool hullsUnlocked = IsManualHullsEnforced;
        bool cargoUnlocked = IsCargoManualTabUnlocked;

        if (!hullsUnlocked)
        {
            // Days 8-9: ONLY 2 tabs — SHIP & SPECIES (inside 280px tablet width)
            int halfW = (w - 12) / 2;
            if (mouse.IsHit(mx + 4, my + 20, halfW, 16))
            {
                _manualActiveTab = 0;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(mx + 8 + halfW, my + 20, halfW, 16))
            {
                _manualActiveTab = 1;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (_manualActiveTab > 1)
                _manualActiveTab = 1;
        }
        else if (!cargoUnlocked)
        {
            // Days 10-11: 3 tabs — SHIP | SPECIES | HULLS (inside 280px tablet width)
            if (mouse.IsHit(mx + 4, my + 20, 84, 16))
            {
                _manualActiveTab = 0;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(mx + 90, my + 20, 100, 16))
            {
                _manualActiveTab = 1;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(mx + 192, my + 20, w - 196, 16))
            {
                _manualActiveTab = 2;
                AudioHooks.Play("terminal_key_tab");
                RebuildManualSilhouetteTexture();
                return true;
            }
        }
        else
        {
            // Day 12+: 4 compact tabs — SHIP | SPECIES | HULLS | CARGO (fits cleanly inside 280px; STARS permanently absent)
            if (mouse.IsHit(mx + 4, my + 20, 58, 16))
            {
                _manualActiveTab = 0;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(mx + 64, my + 20, 80, 16))
            {
                _manualActiveTab = 1;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
            if (mouse.IsHit(mx + 146, my + 20, 64, 16))
            {
                _manualActiveTab = 2;
                AudioHooks.Play("terminal_key_tab");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (mouse.IsHit(mx + 212, my + 20, w - 216, 16))
            {
                _manualActiveTab = 3;
                PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                AudioHooks.Play("terminal_key_tab");
                return true;
            }
        }

        if (_manualActiveTab == 0)
        {
            int rowStartY = my + 39;
            if (mouse.X >= mx + 6 && mouse.X <= mx + w - 6 &&
                mouse.Y >= rowStartY && mouse.Y < rowStartY + ManualPrefixDirectory.Length * 12)
            {
                int clickedIdx = (mouse.Y - rowStartY) / 12;
                if (clickedIdx >= 0 && clickedIdx < ManualPrefixDirectory.Length)
                {
                    SyncManualSelectionToDirectoryIndex(clickedIdx);
                    _manualActiveTab = 1;
                    PixelBuffer.UnloadTexture(ref _cachedManualShipTex);
                    AudioHooks.Play("terminal_key_click");
                    return true;
                }
            }
        }
        else if (_manualActiveTab == 1)
        {
            int dirCount = ManualPrefixDirectory.Length;
            int btnRightX = mx + w - 82;
            int devSyncX = mx + (w - 76) / 2;

            if (mouse.IsHit(mx + 6, my + 40, 76, 16))
            {
                SyncManualSelectionToDirectoryIndex((_manualDirectoryIndex - 1 + dirCount) % dirCount);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
            if (_devDebug.DevManualSyncEnabled && mouse.IsHit(devSyncX, my + 40, 76, 16))
            {
                if (_activeShip != null)
                {
                    for (int d = 0; d < dirCount; d++)
                    {
                        if (ManualPrefixDirectory[d].Prefix.Equals(_activeShip.Slate.DeclaredPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            SyncManualSelectionToDirectoryIndex(d);
                            break;
                        }
                    }
                    AudioHooks.Play("terminal_key_confirm");
                }
                return true;
            }
            if (mouse.IsHit(btnRightX, my + 40, 76, 16))
            {
                SyncManualSelectionToDirectoryIndex((_manualDirectoryIndex + 1) % dirCount);
                AudioHooks.Play("terminal_key_click");
                return true;
            }
        }
        else if (_manualActiveTab == 2 && hullsUnlocked)
        {
            int factionCount = ShipRenderer.AllFactions.Count;
            int btnRightX = mx + w - 82;
            int devSyncX = mx + (w - 76) / 2;

            if (mouse.IsHit(mx + 6, my + 40, 76, 16))
            {
                _manualFactionIndex = (_manualFactionIndex - 1 + factionCount) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (_devDebug.DevManualSyncEnabled && mouse.IsHit(devSyncX, my + 40, 76, 16))
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
            if (mouse.IsHit(btnRightX, my + 40, 76, 16))
            {
                _manualFactionIndex = (_manualFactionIndex + 1) % factionCount;
                _manualClassIndex = 0;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }

            ShipFaction currentFaction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowed = ShipRenderer.GetAllowedClasses(currentFaction);
            if (mouse.IsHit(mx + 6, my + 150, 62, 16))
            {
                _manualClassIndex = (_manualClassIndex - 1 + allowed.Count) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
            if (mouse.IsHit(mx + w - 68, my + 150, 62, 16))
            {
                _manualClassIndex = (_manualClassIndex + 1) % allowed.Count;
                AudioHooks.Play("terminal_key_click");
                RebuildManualSilhouetteTexture();
                return true;
            }
        }
        else if (_manualActiveTab == 3 && cargoUnlocked)
        {
            IReadOnlyList<BannedCargoEntry> banned = CargoCatalog.Load().BannedCargoList;
            int listStartY = my + 54;
            if (mouse.X >= mx + 6 && mouse.X <= mx + w - 6 &&
                mouse.Y >= listStartY && mouse.Y < listStartY + banned.Count * 20)
            {
                int idx = (mouse.Y - listStartY) / 20;
                if (idx >= 0 && idx < banned.Count)
                {
                    _selectedManualBannedCargoIndex = idx;
                    AudioHooks.Play("terminal_key_click");
                    return true;
                }
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
                DeskItemId.FieldBooklet => _layout.FreeDeskRestMinY,
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
                            else if (_phase == EncounterPhase.InspectingSlateAndShip ||
                                     _phase == EncounterPhase.AwaitingClearanceReturnToTopPort)
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

                if (item.Id == DeskItemId.FieldBooklet && !_fieldBooklet.IsOpen)
                {
                    int dx = mouse.X - _dragStartMouseX;
                    int dy = mouse.Y - _dragStartMouseY;
                    if (dx * dx + dy * dy <= 25)
                    {
                        _fieldBooklet.IsOpen = true;
                        _fieldBooklet.SyncWithGameState(_gameState, _config, jumpToNewestOnNewUnlock: true);
                        SyncFieldBookletItemSize();
                        BringDeskItemToFront(DeskItemId.FieldBooklet);
                        AudioHooks.Play("terminal_key_tab");
                    }
                }
            }

            _draggingItemId = null;
        }
    }

    private bool TryHandleFieldBookletInternalClick(VirtualMouseState mouse, DraggableDeskItem booklet)
    {
        if (!_fieldBooklet.HandleInternalClick(mouse, booklet.X, booklet.Y, _gameState, _config))
            return false;

        SyncFieldBookletItemSize();
        BringDeskItemToFront(DeskItemId.FieldBooklet);
        return true;
    }

    private void SyncFieldBookletItemSize()
    {
        if (!_deskItems.TryGetValue(DeskItemId.FieldBooklet, out DraggableDeskItem? item))
            return;

        item.Width = _fieldBooklet.CurrentWidth;
        item.Height = _fieldBooklet.CurrentHeight;
        DeskRect bounds = _layout.FreeDragBounds;
        item.X = Math.Clamp(item.X, bounds.X, bounds.Right - item.Width);
        item.Y = Math.Clamp(item.Y, _layout.FreeDeskRestMinY, bounds.Bottom - item.Height);
    }

    internal void TryAutoUnfoldBookletForCurrentDay()
    {
        string? unlockTag = FieldBookletWidget.GetUnlockTagForDay(_gameState.Day, _config);
        if (string.IsNullOrEmpty(unlockTag))
            return;

        if (_gameState.AutoUnfoldedBookletTags.Contains(unlockTag, StringComparer.OrdinalIgnoreCase))
            return;

        _gameState.AutoUnfoldedBookletTags.Add(unlockTag);
        _fieldBooklet.IsOpen = true;
        _fieldBooklet.SelectPageByTag(unlockTag, _gameState, _config);
        SyncFieldBookletItemSize();
        BringDeskItemToFront(DeskItemId.FieldBooklet);
        _gameState.SaveToDisk();
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
        UiText.Draw(TruncateByPixelWidth(hdr, 10, w - 20), x + 10, y + 14, 10, headerCol);

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
            UiText.Draw("CARRIER: LOCKED", x + 14, y + 44, 10, Palette.ClearanceTeal);

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
                UiText.Draw($"HDR: 0x{(0x4A + frameShift) & 0xFF:X2} {(0x91 + frameShift * 3) & 0xFF:X2} SYNC", x + 14, y + 78, 10, Palette.CrtAmberPrimary);
            if (rxProg >= 0.40f)
            {
                string pfxHint = _activeShip != null ? _activeShip.Slate.DeclaredPrefix : "---";
                UiText.Draw($"BUS: {pfxHint} STREAM", x + 14, y + 94, 10, Palette.ClearanceTeal);
            }
            if (rxProg >= 0.65f)
                UiText.Draw($"CHK: 0x{(0xC0 + frameShift * 5) & 0xFF:X2}F{(frameShift & 0xF):X1} OK", x + 14, y + 110, 10, Palette.CrtAmberDimmed);
            if (rxProg >= 0.85f)
                UiText.Draw("SEALING BUFFER...", x + 14, y + 126, 10, Palette.ClearanceTeal);
            else
                UiText.Draw($"PACKET STREAM {pct:D2}%", x + 14, y + 126, 10, Palette.CrtAmberDimmed);
        }
        else if (isMission)
        {
            UiText.Draw(_activeDeskContrabandItem.DisplayName, x + 14, y + 46, 10, Palette.CrtAmberPrimary);
            UiText.Draw($"TGT: {_activeDeskContrabandItem.MissionTargetReg}", x + 14, y + 66, 10, Palette.ClearanceTeal);
            UiText.Draw("SEALED ONE-TIME USE", x + 14, y + 86, 10, Palette.CrtAmberDimmed);
            UiText.Draw("SLOT INTO SLATE BUS", x + 14, y + 104, 10, Palette.CrtAmberDimmed);
            UiText.Draw("AVOID KDF- / BMC-!", x + 14, y + 128, 10, Palette.AlertCrimson);
        }
        else if (isCopied)
        {
            UiText.Draw($"REG: {_activeDeskContrabandItem.TargetShipRegistration}", x + 14, y + 48, 10, Palette.ClearanceTeal);
            UiText.Draw($"PFX: {_activeDeskContrabandItem.TargetShipPrefix}", x + 14, y + 66, 10, Palette.CrtAmberPrimary);
            UiText.Draw(TruncateByPixelWidth($"ORG: {_activeDeskContrabandItem.OriginStarSystem}", 10, w - 28), x + 14, y + 84, 10, Palette.CrtAmberDimmed);
            UiText.Draw("STOW IN LAP POCKET", x + 14, y + 114, 10, Palette.ClearanceTeal);
            UiText.Draw("SEND VIA CRYPTO.", x + 14, y + 128, 10, Palette.CrtAmberDimmed);
        }
        else
        {
            UiText.Draw("BUFFER: [EMPTY]", x + 14, y + 48, 10, Palette.CrtAmberPrimary);
            UiText.Draw("UNPLUG PRIMARY SLATE", x + 14, y + 74, 10, Palette.CrtAmberDimmed);
            UiText.Draw("& SLOT THIS INTO", x + 14, y + 88, 10, Palette.CrtAmberDimmed);
            UiText.Draw("SLATE BUS TO COPY", x + 14, y + 102, 10, Palette.CrtAmberDimmed);
            UiText.Draw("INCOMING PROTOCOLS.", x + 14, y + 116, 10, Palette.CrtAmberDimmed);
        }

        string foot = isBlankRx
            ? "HOLD IN SLATE BUS"
            : (_contrabandSlateInSlateBus ? "[LINKED IN BUS]" : "DRAG -> BUS / POCKET");
        UiText.Draw(TruncateByPixelWidth(foot, 10, w - 16), x + 10, y + h - 22, 10,
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
        UiText.Draw("CRYSTAL BURNT", x + 10, y + 14, 10, new Color(110, 114, 122, 255));

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
        UiText.Draw(b1, gx + (gw - UiText.MeasureWidth(b1, 10)) / 2, gy + 47, 10, dimReadout);
        UiText.Draw(b2, gx + (gw - UiText.MeasureWidth(b2, 10)) / 2, gy + 61, 10, new Color(76, 80, 88, 255));

        string foot = inBus ? "UNPLUG TO FREE BUS" : "SPENT -- LEAVE ON DESK";
        UiText.Draw(TruncateByPixelWidth(foot, 10, w - 16), x + 10, y + h - 22, 10, new Color(82, 86, 94, 255));
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
        UiText.Draw(TruncateByPixelWidth(slateHeader, 10, w - 12), x + 6, y + 5, 10, headerFg);

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
                UiText.Draw(a1, x + (w - UiText.MeasureWidth(a1, 10)) / 2, y + 66, 10, Palette.AlertCrimson);
                UiText.Draw(a2, x + (w - UiText.MeasureWidth(a2, 10)) / 2, y + 92, 10, Palette.ClearanceTeal);
                UiText.Draw(a3, x + (w - UiText.MeasureWidth(a3, 10)) / 2, y + 110, 10, Palette.ClearanceTeal);
            }
            else if (_slateWipeFlashTimer > 0f)
            {
                const string w1 = "CLAMPS ENGAGED";
                const string w2 = "TELEMETRY WIPED";
                const string w3 = "BUFFER CLEAN";
                UiText.Draw(w1, x + (w - UiText.MeasureWidth(w1, 10)) / 2, y + 74, 10, Palette.AlertCrimson);
                UiText.Draw(w2, x + (w - UiText.MeasureWidth(w2, 10)) / 2, y + 94, 10, Palette.CrtAmberPrimary);
                UiText.Draw(w3, x + (w - UiText.MeasureWidth(w3, 10)) / 2, y + 114, 10, Palette.ClearanceTeal);
            }
            else
            {
                const string e1 = "PRIMARY DATA SLATE";
                const string e2 = "BUFFER: EMPTY";
                string hint = IsDataSlateInSlateBus ? "READY IN SLATE BUS" : "PLUG INTO SLATE BUS";
                UiText.Draw(e1, x + (w - UiText.MeasureWidth(e1, 10)) / 2, y + 74, 10, Palette.CrtAmberDimmed);
                UiText.Draw(e2, x + (w - UiText.MeasureWidth(e2, 10)) / 2, y + 96, 10, Palette.CrtAmberPrimary);
                UiText.Draw(hint, x + (w - UiText.MeasureWidth(hint, 10)) / 2, y + 118, 10,
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
                UiText.Draw("REG CODE", rightColX + 1, y + 25, 10, Palette.CrtAmberDimmed);
                UiText.Draw(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx - 4), rightColX + 1, y + 38, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.35f)
            {
                UiText.Draw("PREFIX:", rightColX, y + 57, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(slate.DeclaredPrefix, rightColX, y + 69, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawLine(x + 6, y + 89, x + w - 6, y + 89, Palette.WeatheredGunmetalEdge);

            if (dl >= 0.45f)
            {
                UiText.Draw("VESSEL", fieldX, y + 94, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(TruncateByPixelWidth(slate.ShipName, 10, fieldMaxPx), fieldX, y + 105, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.60f)
            {
                UiText.Draw("REGISTRY", fieldX, y + 120, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(TruncateByPixelWidth(slate.DeclaredFactionName, 10, fieldMaxPx), fieldX, y + 131, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.75f)
            {
                UiText.Draw("CAPTAIN", fieldX, y + 146, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(TruncateByPixelWidth(slate.DeclaredCaptainName, 10, fieldMaxPx), fieldX, y + 157, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.88f)
            {
                UiText.Draw("ROUTE", fieldX, y + 172, 10, Palette.WeatheredGunmetalEdge);
                UiText.Draw(TruncateByPixelWidth($"{slate.OriginSystem} -> {slate.DestinationSystem}", 10, fieldMaxPx),
                    fieldX, y + 183, 10, Palette.CrtAmberDimmed);
            }
        }
        else if (_gameState.Day < _config.DayUnlockWarpScanner)
        {
            // =================================================================
            // DAY 2-3 PRE-WARP DATA SLATE (REG CODE + WPN Highlightable in Protocol Review!)
            // =================================================================
            bool wpnFault = IsTargetFaultHighlighted(ReviewTarget.DataSlateWeapons);
            DrawSlateFieldHighlight(mouse, x + 73, y + 21, w - 78, 26, ReviewTarget.DataSlatePrefix);
            DrawSlateFieldHighlight(mouse, x + 73, y + 50, w - 78, 15, ReviewTarget.DataSlateWeapons);

            if (dl >= 0.15f)
            {
                UiText.Draw("REG CODE:", rightColX, y + 22, 10, Palette.CrtAmberDimmed);
                UiText.Draw(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx), rightColX, y + 34, 10, Palette.CrtAmberPrimary);
            }
            if (dl >= 0.30f)
            {
                UiText.Draw(TruncateByPixelWidth($"WPN: {slate.DeclaredWeaponsStatus}", 10, rightColMaxPx), rightColX, y + 52, 10,
                    wpnFault ? GetFaultPulseColor() : Palette.CrtAmberPrimary);
                UiText.Draw(TruncateByPixelWidth($"SHLD: {slate.DeclaredShieldsStatus}", 10, rightColMaxPx), rightColX, y + 68, 10, Palette.CrtAmberDimmed);
            }

            Raylib.DrawLine(x + 6, y + 89, x + w - 6, y + 89, Palette.WeatheredGunmetalEdge);

            if (dl >= 0.45f) UiText.Draw(TruncateByPixelWidth($"SHIP: {slate.ShipName}", 10, fieldMaxPx), fieldX, y + 94, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.58f) UiText.Draw(TruncateByPixelWidth($"ORG:  {slate.DeclaredFactionName}", 10, fieldMaxPx), fieldX, y + 108, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.70f) UiText.Draw(TruncateByPixelWidth($"CAPT: {slate.DeclaredCaptainName}", 10, fieldMaxPx), fieldX, y + 122, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.80f) UiText.Draw(TruncateByPixelWidth($"ORIG: {slate.OriginSystem}", 10, fieldMaxPx), fieldX, y + 136, 10, Palette.CrtAmberPrimary);
            if (dl >= 0.88f) UiText.Draw(TruncateByPixelWidth($"DEST: {slate.DestinationSystem}", 10, fieldMaxPx), fieldX, y + 150, 10, Palette.CrtAmberDimmed);
            if (dl >= 0.94f)
            {
                List<string> cargoLines = WrapTextByPixelWidth($"CARGO: {slate.CargoManifest}", 10, fieldMaxPx, maxLines: 2);
                for (int c = 0; c < cargoLines.Count; c++)
                {
                    UiText.Draw(cargoLines[c], fieldX, y + 164 + c * 13, 10, Palette.CrtAmberDimmed);
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
            DrawSlateFieldHighlight(mouse, x + 73, y + 72, rightHotW, 13, ReviewTarget.DataSlateMass);
            DrawSlateFieldHighlight(mouse, x + 4, y + 103, rowW, 12, ReviewTarget.DataSlateOrg);
            DrawSlateFieldHighlight(mouse, x + 4, y + 115, rowW, 12, ReviewTarget.DataSlateExpiry);
            DrawSlateFieldHighlight(mouse, x + 4, y + 139, rowW, 12, ReviewTarget.DataSlateSpecies);
            DrawSlateFieldHighlight(mouse, x + 4, y + 151, rowW, 12, ReviewTarget.DataSlateOrigin);
            DrawSlateFieldHighlight(mouse, x + 4, y + 175, rowW, 24, ReviewTarget.DataSlateCargo);

            if (dl >= 0.12f)
            {
                UiText.Draw("REG CODE:", rightColX, y + 22, 10, Palette.CrtAmberDimmed, denseRow: true);
                UiText.Draw(TruncateByPixelWidth(slate.RegistrationCode, 10, rightColMaxPx, denseRow: true), rightColX, y + 32, 10, Palette.CrtAmberPrimary, denseRow: true);
            }
            if (dl >= 0.22f)
            {
                UiText.Draw(TruncateByPixelWidth($"CLASS: {ShortClass(slate.DeclaredShipClass)}", 10, rightColMaxPx, denseRow: true), rightColX, y + 45, 10, Palette.CrtAmberPrimary, denseRow: true);
            }
            if (dl >= 0.32f)
            {
                UiText.Draw(TruncateByPixelWidth($"WPN: {slate.DeclaredWeaponsStatus}", 10, rightColMaxPx, denseRow: true), rightColX, y + 59, 10, Palette.CrtAmberPrimary, denseRow: true);
                Color massTextCol = IsStokerOperational ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed;
                UiText.Draw(TruncateByPixelWidth($"MASS: {slate.DeclaredTonnage} T", 10, rightColMaxPx, denseRow: true), rightColX, y + 73, 10, massTextCol, denseRow: true);
            }

            Raylib.DrawLine(x + 4, y + 89, x + w - 4, y + 89, Palette.WeatheredGunmetalEdge);
            int fieldY = y + 92;
            const int lineStep = 12;

            if (dl >= 0.42f) UiText.Draw(TruncateByPixelWidth($"SHIP: {slate.ShipName}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY, 10, Palette.CrtAmberPrimary, denseRow: true);
            if (dl >= 0.50f) UiText.Draw(TruncateByPixelWidth($"ORG:  {slate.DeclaredFactionName}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep, 10, Palette.CrtAmberPrimary, denseRow: true);
            if (dl >= 0.58f) UiText.Draw(TruncateByPixelWidth($"EXP:  {slate.RegistrationExpiryDisplay}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep * 2, 10, Palette.CrtAmberPrimary, denseRow: true);
            if (dl >= 0.66f) UiText.Draw(TruncateByPixelWidth($"CAPT: {slate.DeclaredCaptainName}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep * 3, 10, Palette.CrtAmberPrimary, denseRow: true);
            if (dl >= 0.74f) UiText.Draw(TruncateByPixelWidth($"SPEC: {slate.DeclaredCaptainSpecies.ToString().ToUpperInvariant()}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep * 4, 10, Palette.CrtAmberDimmed, denseRow: true);
            if (dl >= 0.82f) UiText.Draw(TruncateByPixelWidth($"ORIG: {slate.OriginSystem}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep * 5, 10, Palette.CrtAmberPrimary, denseRow: true);
            if (dl >= 0.88f) UiText.Draw(TruncateByPixelWidth($"DEST: {slate.DestinationSystem}", 10, fieldMaxPx, denseRow: true), fieldX, fieldY + lineStep * 6, 10, Palette.CrtAmberDimmed, denseRow: true);
            if (dl >= 0.94f)
            {
                List<string> cargoLines = WrapTextByPixelWidth($"CARGO: {slate.CargoManifest}", 10, fieldMaxPx, maxLines: 2, denseRow: true);
                int cargoBaseY = fieldY + lineStep * 7;
                for (int c = 0; c < cargoLines.Count; c++)
                {
                    UiText.Draw(cargoLines[c], fieldX, cargoBaseY + c * 12, 10, Palette.CrtAmberPrimary, denseRow: true);
                }
            }
        }

        // Bottom Status Banner (y + 201 .. y + 214 -- zero overlap with lower fields)
        if (isDetainWiping)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.AlertCrimson);
            const string msg = "WIPING TELEMETRY...";
            UiText.Draw(msg, x + (w - UiText.MeasureWidth(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isUploading)
        {
            // Subtle hint shown ONLY while hovering the docked slate during encryption
            if (mouse.IsHit(x, y, w, h))
            {
                Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.OxidizedBronzeShadow);
                const string msg = "DRAG OUT TO ABORT";
                UiText.Draw(msg, x + (w - UiText.MeasureWidth(msg, 10)) / 2, y + h - 14, 10, Palette.CrtAmberPrimary);
            }
        }
        else if (isCleared)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.ClearanceTeal);
            const string msg = ">> DRAG TO SLATE BUS <<";
            UiText.Draw(msg, x + (w - UiText.MeasureWidth(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isTransmitting)
        {
            Raylib.DrawRectangle(x + 3, y + h - 15, w - 6, 13, Palette.ClearanceTeal);
            const string msg = "TX -- DRAG TO ABORT";
            UiText.Draw(msg, x + (w - UiText.MeasureWidth(msg, 10)) / 2, y + h - 14, 10, Palette.OutlineVoid);
        }
        else if (isDownloading)
        {
            const string msg = "HOLD IN SLATE BUS";
            UiText.Draw(msg, x + (w - UiText.MeasureWidth(msg, 10)) / 2, y + h - 14, 10, Palette.ClearanceTeal);
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
        int primaryW = UiText.MeasureWidth(primaryText, 20);
        int primaryX = boxX + (sealW - primaryW) / 2;
        int primaryY = boxY + 8;
        UiText.Draw(primaryText, primaryX + 1, primaryY + 1, 20, Palette.OutlineVoid);
        UiText.Draw(primaryText, primaryX, primaryY, 20, Palette.ClearanceTeal);

        Raylib.DrawLine(boxX + 12, boxY + 30, boxX + sealW - 13, boxY + 30, new Color(45, 212, 191, 150));

        const string subText = "TO DOCK";
        int subW = UiText.MeasureWidth(subText, 10);
        int subX = boxX + (sealW - subW) / 2;
        int subY = boxY + 33;
        UiText.Draw(subText, subX + 1, subY + 1, 10, Palette.OutlineVoid);
        UiText.Draw(subText, subX, subY, 10, Palette.SpsShieldArcIce);
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
    // Day 8+ Brengop Reference Manual (Consortium Registry Tablet)
    // Days 8-9: 2 tabs ONLY (SHIP REGISTRAR | SPECIES REGISTRAR) -- NO Hulls
    // Day 10+:  3 tabs (SHIP REGISTRAR | SPECIES REGISTRAR | HULLS)
    // =========================================================================
    private void DrawDeskReferenceManual(VirtualMouseState mouse, DraggableDeskItem item, bool isTopmost)
    {
        int x = item.X;
        int y = item.Y;
        int w = item.Width;
        int h = item.Height;
        bool hullsUnlocked = IsManualHullsEnforced;
        bool cargoUnlocked = IsCargoManualTabUnlocked;
        int maxTab = cargoUnlocked ? 3 : (hullsUnlocked ? 2 : 1);
        _manualActiveTab = Math.Clamp(_manualActiveTab, 0, maxTab);

        Color borderCol = isTopmost ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed;
        Raylib.DrawRectangle(x, y, w, h, Palette.OutlineVoid);
        Raylib.DrawRectangleLines(x, y, w, h, borderCol);

        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 16, Palette.OxidizedBronzeShadow);
        UiText.Draw(UiText.TruncateToWidth("BRENGOP REFERENCE MANUAL", 10, w - 14, denseRow: true), x + 7, y + 4, 10, Palette.CrtAmberPrimary, denseRow: true);

        if (!hullsUnlocked)
        {
            int halfW = (w - 12) / 2;
            DrawManualTabButton(mouse, x + 4, y + 20, halfW, 16, "SHIP", _manualActiveTab == 0);
            DrawManualTabButton(mouse, x + 8 + halfW, y + 20, halfW, 16, "SPECIES", _manualActiveTab == 1);
        }
        else if (!cargoUnlocked)
        {
            DrawManualTabButton(mouse, x + 4, y + 20, 84, 16, "SHIP", _manualActiveTab == 0);
            DrawManualTabButton(mouse, x + 90, y + 20, 100, 16, "SPECIES", _manualActiveTab == 1);
            DrawManualTabButton(mouse, x + 192, y + 20, w - 196, 16, "HULLS", _manualActiveTab == 2);
        }
        else
        {
            DrawManualTabButton(mouse, x + 4, y + 20, 58, 16, "SHIP", _manualActiveTab == 0);
            DrawManualTabButton(mouse, x + 64, y + 20, 80, 16, "SPECIES", _manualActiveTab == 1);
            DrawManualTabButton(mouse, x + 146, y + 20, 64, 16, "HULLS", _manualActiveTab == 2);
            DrawManualTabButton(mouse, x + 212, y + 20, w - 216, 16, "CARGO", _manualActiveTab == 3);
        }

        if (_manualActiveTab == 0)
        {
            int ry = y + 39;
            int listH = ManualPrefixDirectory.Length * 12;
            for (int i = 0; i < ManualPrefixDirectory.Length; i++)
            {
                var entry = ManualPrefixDirectory[i];
                bool rowHovered = mouse.IsHit(x + 6, ry, w - 12, 12);
                bool rowActive = i == _manualDirectoryIndex;
                if (rowHovered || rowActive)
                {
                    Raylib.DrawRectangle(x + 6, ry, w - 12, 12, Palette.OxidizedBronzeShadow);
                }
                string rowText = $"{entry.Prefix}  {entry.OrgName}";
                UiText.Draw(TruncateByPixelWidth(rowText, 10, w - 18, denseRow: true), x + 10, ry + 1, 10,
                    rowHovered ? Palette.ClearanceTeal : (rowActive ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed),
                    denseRow: true);
                ry += 12;
            }

            if (IsTargetFaultHighlighted(ReviewTarget.ReferenceManualPrefixEntry))
            {
                Color col = GetFaultPulseColor();
                Raylib.DrawRectangleLines(x + 6, y + 39, w - 12, listH, col);
            }
            else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.ReferenceManualPrefixEntry))
            {
                DrawReviewHotspotOutline(mouse, x + 6, y + 39, w - 12, listH, _reviewFirstSelection == ReviewTarget.ReferenceManualPrefixEntry);
            }
        }
        else if (_manualActiveTab == 1)
        {
            var dirEntry = ManualPrefixDirectory[Math.Clamp(_manualDirectoryIndex, 0, ManualPrefixDirectory.Length - 1)];
            int btnRightX = x + w - 82;
            int devSyncX = x + (w - 76) / 2;

            DrawSmallButton(mouse, x + 6, y + 40, 76, 16, "< FACTION");
            if (_devDebug.DevManualSyncEnabled)
            {
                DrawSmallButton(mouse, devSyncX, y + 40, 76, 16, "DEV SYNC", Palette.ClearanceTeal);
            }
            DrawSmallButton(mouse, btnRightX, y + 40, 76, 16, "FACTION >");

            int cardX = x + 6;
            int cardY = y + 60;
            int cardW = w - 12;
            int cardH = h - 66;
            Raylib.DrawRectangle(cardX, cardY, cardW, cardH, Palette.DeepSpaceVoid);
            Raylib.DrawRectangleLines(cardX, cardY, cardW, cardH, Palette.WeatheredGunmetalEdge);

            UiText.Draw($"PREFIX: {dirEntry.Prefix}", cardX + 8, cardY + 8, 10, Palette.ClearanceTeal);
            UiText.Draw(TruncateByPixelWidth($"ORGANIZATION: {dirEntry.OrgName}", 10, cardW - 16), cardX + 8, cardY + 26, 10, Palette.CrtAmberPrimary);

            Raylib.DrawLine(cardX + 6, cardY + 44, cardX + cardW - 6, cardY + 44, Palette.WeatheredGunmetalEdge);

            string speciesRule = GetAuthorizedSpeciesRuleLine(dirEntry.Prefix);
            UiText.Draw("AUTHORIZED CAPTAIN SPECIES:", cardX + 8, cardY + 52, 10, Palette.CrtAmberDimmed);
            UiText.Draw(TruncateByPixelWidth(speciesRule, 10, cardW - 16), cardX + 8, cardY + 68, 10, Palette.ClearanceTeal);

            string expiryHint = "REG. EXPIRY: MUST MATCH/EXCEED CYC";
            UiText.Draw(TruncateByPixelWidth(expiryHint, 10, cardW - 16), cardX + 8, cardY + 92, 10, Palette.CrtAmberPrimary);
            UiText.Draw("SOURCE: CONSORTIUM REGISTRY TABLET", cardX + 8, cardY + 110, 10, Palette.CrtAmberDimmed);

            if (IsTargetFaultHighlighted(ReviewTarget.ReferenceManualSpeciesEntry))
            {
                Color col = GetFaultPulseColor();
                Raylib.DrawRectangleLines(cardX, cardY, cardW, cardH, col);
                Raylib.DrawRectangleLines(cardX - 1, cardY - 1, cardW + 2, cardH + 2, col);
            }
            else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.ReferenceManualSpeciesEntry))
            {
                DrawReviewHotspotOutline(mouse, cardX, cardY, cardW, cardH, _reviewFirstSelection == ReviewTarget.ReferenceManualSpeciesEntry);
            }
        }
        else if (_manualActiveTab == 2 && hullsUnlocked)
        {
            ShipFaction faction = ShipRenderer.AllFactions[_manualFactionIndex];
            IReadOnlyList<ShipClass> allowedClasses = ShipRenderer.GetAllowedClasses(faction);
            ShipClass selectedClass = allowedClasses[Math.Clamp(_manualClassIndex, 0, allowedClasses.Count - 1)];

            int btnRightX = x + w - 82;
            int devSyncX = x + (w - 76) / 2;
            DrawSmallButton(mouse, x + 6, y + 40, 76, 16, "< FACTION");
            if (_devDebug.DevManualSyncEnabled)
            {
                DrawSmallButton(mouse, devSyncX, y + 40, 76, 16, "DEV SYNC", Palette.ClearanceTeal);
            }
            DrawSmallButton(mouse, btnRightX, y + 40, 76, 16, "FACTION >");

            int hullBoxX = x + 6;
            int hullBoxY = y + 60;
            int hullBoxW = w - 12;
            int hullBoxH = 88;
            Raylib.DrawRectangle(hullBoxX, hullBoxY, hullBoxW, hullBoxH, Palette.DeepSpaceVoid);
            Raylib.DrawRectangleLines(hullBoxX, hullBoxY, hullBoxW, hullBoxH, Palette.WeatheredGunmetalEdge);

            string factionTitle = ShipRenderer.GetFactionDisplayName(faction);
            UiText.Draw(TruncateByPixelWidth(factionTitle, 10, hullBoxW - 12), hullBoxX + 6, hullBoxY + 4, 10, Palette.CrtAmberPrimary);

            int chartX = x + (w - Constants.ShipWidth) / 2;
            int chartY = hullBoxY + 18;
            if (_cachedManualShipTex.Id != 0)
            {
                Raylib.DrawTexture(_cachedManualShipTex, chartX, chartY, Color.White);
            }
            Raylib.DrawRectangleLines(chartX - 1, chartY - 1, Constants.ShipWidth + 2, Constants.ShipHeight + 2, Palette.CrtAmberDimmed);

            if (IsTargetFaultHighlighted(ReviewTarget.ReferenceManualHullEntry))
            {
                Color col = GetFaultPulseColor();
                Raylib.DrawRectangleLines(hullBoxX, hullBoxY, hullBoxW, hullBoxH, col);
                Raylib.DrawRectangleLines(hullBoxX - 1, hullBoxY - 1, hullBoxW + 2, hullBoxH + 2, col);
            }
            else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.ReferenceManualHullEntry))
            {
                DrawReviewHotspotOutline(mouse, hullBoxX, hullBoxY, hullBoxW, hullBoxH, _reviewFirstSelection == ReviewTarget.ReferenceManualHullEntry);
            }

            DrawSmallButton(mouse, x + 6, y + 150, 62, 16, "< PREV");
            string classCaption = $"{ShortClass(selectedClass)} ({_manualClassIndex + 1}/{allowedClasses.Count})";
            int capW = UiText.MeasureWidth(classCaption, 10);
            UiText.Draw(classCaption, x + (w - capW) / 2, y + 153, 10, Palette.ClearanceTeal);
            DrawSmallButton(mouse, x + w - 68, y + 150, 62, 16, "NEXT >");

            string allowedList = string.Join("/", allowedClasses.Select(ShortClass));
            string subNote = faction == ShipFaction.FreeTraders
                ? $"CLASSES: {allowedList} (SHARED FTV/HBR)"
                : $"ALLOWED CLASSES: {allowedList}";
            UiText.Draw(TruncateByPixelWidth(subNote, 10, w - 16, denseRow: true), x + 8, y + 172, 10, Palette.CrtAmberDimmed, denseRow: true);
        }
        else if (_manualActiveTab == 3 && cargoUnlocked)
        {
            CargoCatalog catalog = CargoCatalog.Load();
            UiText.Draw(
                UiText.TruncateToWidth("PERMANENT BANNED CARGO (0 EXCEPTIONS):", 10, w - 14, denseRow: true),
                x + 7,
                y + 40,
                10,
                Palette.AlertCrimson,
                denseRow: true);

            int listX = x + 6;
            int listY = y + 54;
            int listW = w - 12;
            int listH = catalog.BannedCargoList.Count * 20;

            for (int i = 0; i < catalog.BannedCargoList.Count; i++)
            {
                BannedCargoEntry bEntry = catalog.BannedCargoList[i];
                int ry = listY + i * 20;
                bool hovered = mouse.IsHit(listX, ry, listW, 19);
                bool selected = i == _selectedManualBannedCargoIndex;

                Raylib.DrawRectangle(listX, ry, listW, 19,
                    hovered || selected ? Palette.OxidizedBronzeShadow : Palette.DeepSpaceVoid);
                Raylib.DrawRectangleLines(listX, ry, listW, 19,
                    hovered ? Palette.CrtAmberPrimary : Palette.WeatheredGunmetalEdge);

                Color tagCol = bEntry.SpsViolet
                    ? Palette.FromHex("#c084fc")
                    : (bEntry.BannedBio ? Palette.AlertCrimson : Palette.CrtAmberPrimary);
                string leftText = $"{i + 1}. {bEntry.Name.ToUpperInvariant()}";
                string rightTag = bEntry.SpsViolet ? "[VIOLET]" : (bEntry.DenseMass ? "[MASS]" : "[BIO]");
                int tagW = UiText.MeasureWidth(rightTag, 10, denseRow: true);
                UiText.Draw(
                    UiText.TruncateToWidth(leftText, 10, listW - tagW - 14, denseRow: true),
                    listX + 5,
                    ry + 4,
                    10,
                    Palette.CrtAmberPrimary,
                    denseRow: true);
                UiText.Draw(rightTag, listX + listW - tagW - 5, ry + 4, 10, tagCol, denseRow: true);
            }

            if (IsTargetFaultHighlighted(ReviewTarget.ReferenceManualCargoBannedEntry))
            {
                Color col = GetFaultPulseColor();
                Raylib.DrawRectangleLines(listX, listY, listW, listH, col);
                Raylib.DrawRectangleLines(listX - 1, listY - 1, listW + 2, listH + 2, col);
            }
            else if (_protocolReviewModeActive && IsShipInspectableForReview && IsReviewTargetUnlocked(ReviewTarget.ReferenceManualCargoBannedEntry))
            {
                DrawReviewHotspotOutline(mouse, listX, listY, listW, listH, _reviewFirstSelection == ReviewTarget.ReferenceManualCargoBannedEntry);
            }

            UiText.Draw(
                UiText.TruncateToWidth("INERT & REG. BIO LEGAL W/ MANIFEST.", 10, w - 14, denseRow: true),
                x + 7,
                y + 158,
                10,
                Palette.ClearanceTeal,
                denseRow: true);
            UiText.Draw(
                UiText.TruncateToWidth("PAIR BANNED ITEM -> DETAIN IMMEDIATELY.", 10, w - 14, denseRow: true),
                x + 7,
                y + 172,
                10,
                Palette.CrtAmberDimmed,
                denseRow: true);
        }
    }

    // =========================================================================
    // Day 12+ Ceramic-Polymer Cert-Wafers, 3-Wafer Hopper, Top Wipe & Scoop Modes
    // =========================================================================
    internal (int X, int Y) ComputeWaferLeftFanTarget(int slotIndex, DeskLayoutRects? layoutOverride = null)
    {
        DeskLayoutRects lay = layoutOverride ?? _layout;
        DeskRect bounds = lay.FreeDragBounds;
        int fanRight = lay.WaferWriter.X - 16;
        const int fanStepX = 88;
        int minSafeLeft = Math.Max(ManualDefaultX + ManualW + 8, 8 + FieldBookletWidget.OpenW + 8); // 332

        int idx = Math.Clamp(slotIndex, 0, 2);
        int targetX = Math.Clamp(fanRight - WaferCardW - idx * fanStepX, minSafeLeft, fanRight - WaferCardW);
        int targetY = Math.Clamp(540 - idx * 8, lay.FreeDeskRestMinY, bounds.Bottom - WaferCardH);
        return (targetX, targetY);
    }

    private void ReconcileDeskWafersForLayout(DeskLayoutRects layout)
    {
        DeskRect bounds = layout.FreeDragBounds;
        int maxRight = layout.WaferWriter.X - 16;
        int minLeft = Math.Max(bounds.X, Math.Max(ManualDefaultX + ManualW + 8, 8 + FieldBookletWidget.OpenW + 8));

        for (int i = 0; i < _deskWafers.Count; i++)
        {
            CertWaferCard w = _deskWafers[i];
            if (_draggedWaferIds.Contains(w.InstanceId))
                continue;

            if (w.IsEjecting || w.X + WaferCardW > maxRight || w.X < bounds.X)
            {
                var (tx, ty) = ComputeWaferLeftFanTarget(i, layout);
                w.EjectTargetX = tx;
                w.EjectTargetY = ty;
                if (!w.IsEjecting)
                {
                    w.X = Math.Clamp(w.X, minLeft, maxRight - WaferCardW);
                    w.Y = Math.Clamp(w.Y, layout.FreeDeskRestMinY, bounds.Bottom - WaferCardH);
                }
            }
            else
            {
                w.X = Math.Clamp(w.X, bounds.X, maxRight - WaferCardW);
                w.Y = Math.Clamp(w.Y, layout.FreeDeskRestMinY, bounds.Bottom - WaferCardH);
            }
        }
    }

    internal void QueueOrEjectCargoManifestWafer(ShipSpec ship)
    {
        if (!IsWaferWriterOperational)
            return;

        CertWaferCard card = BuildCargoManifestWaferCard(ship);

        if (_waferHopperCount <= 0)
        {
            _heldPendingWafer = card;
            AudioHooks.Play("citation_buzz");
            if (!_emptyHopperReminderLoggedForHeldWafer)
            {
                _emptyHopperReminderLoggedForHeldWafer = true;
                AddSystemLogLine("[WAFER WRITER] HOPPER EMPTY (0/3) -- PRINT HELD. WIPE A WAFER IN TOP SLOT.", CommsTextTone.AlertCrimson);
                // TODO: USER DIALOGUE PASS — Empty Hopper Reminder line
                AddSystemLogLine("NOTE: \"Oh yes, don't forget to wipe the wafers clean\u2014we only gave you enough for what you need.\"", CommsTextTone.AmberPrimary);
            }
            return;
        }

        EjectWaferFromHopper(card);
    }

    private CertWaferCard BuildCargoManifestWaferCard(ShipSpec ship)
    {
        string primaryCargo = string.IsNullOrWhiteSpace(ship.GroundTruth.WaferCargoManifestItem)
            ? ship.Slate.CargoManifest
            : ship.GroundTruth.WaferCargoManifestItem;
        string secondaryNote = string.IsNullOrWhiteSpace(ship.GroundTruth.WaferCargoSecondaryNote)
            ? "STAGE-2 CARGO MANIFEST"
            : ship.GroundTruth.WaferCargoSecondaryNote;

        return new CertWaferCard
        {
            InstanceId = _nextWaferInstanceId++,
            WaferType = CertWaferType.CargoManifest,
            ShipRegistration = ship.Slate.RegistrationCode,
            PrimaryCargoItem = primaryCargo,
            SecondaryNote = secondaryNote
        };
    }

    private void EjectWaferFromHopper(CertWaferCard card)
    {
        _waferHopperCount = Math.Max(0, _waferHopperCount - 1);
        if (card.WaferType == CertWaferType.CargoManifest)
        {
            _cargoWaferPrintedForActiveShip = true;
        }
        if (_activeShip != null &&
            string.Equals(card.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
        {
            _demuxedWafersForActiveShip.Add(card.WaferType);
            _prematurelyWipedWafersForActiveShip.Remove(card.WaferType);
        }
        _waferWriteHeadFlashTimer = 0.42f;
        AudioHooks.Play("subspace_transfer");

        var (targetX, targetY) = ComputeWaferLeftFanTarget(_deskWafers.Count);
        card.EjectStartX = _layout.WaferWriter.X + 8;
        card.EjectStartY = Math.Max(_layout.FreeDeskRestMinY, _layout.WaferWriter.Y - WaferCardH + 40);
        card.EjectTargetX = targetX;
        card.EjectTargetY = targetY;
        card.X = card.EjectStartX;
        card.Y = card.EjectStartY;
        card.EjectElapsed = 0f;
        card.IsEjecting = true;

        _deskWafers.Add(card);
    }

    private void TriggerPrematurelyWipedWaferReTransmit()
    {
        if (!CanReTransmitPrematurelyWipedWafers || _activeShip == null)
            return;

        _isReTransmittingWipedWafers = true;
        _reTransmitWipedWaferElapsed = 0f;
        AudioHooks.Play("terminal_key_confirm");
        EnqueueOfficerLine("Re-transmit auxiliary certificates.", CommsTextTone.ClearanceTeal);
        // TODO: USER DIALOGUE PASS
        EnqueueCaptainLine(
            "CAPT: \"Already sent them. Sending again.\"",
            CommsTextTone.AmberPrimary,
            preDelaySeconds: 0.16f,
            showReceivingIndicatorDuringPreDelay: true,
            suppressAutoReceivingLag: true);
    }

    private void ReDemuxPrematurelyWipedWafers(ShipSpec ship)
    {
        if (!IsWaferWriterOperational || _prematurelyWipedWafersForActiveShip.Count == 0)
            return;

        CertWaferType[] toReprint = _prematurelyWipedWafersForActiveShip
            .OrderBy(static t => (int)t)
            .ToArray();

        foreach (CertWaferType waferType in toReprint)
        {
            if (!_demuxedWafersForActiveShip.Contains(waferType))
            {
                _prematurelyWipedWafersForActiveShip.Remove(waferType);
                continue;
            }

            bool alreadyOnDesk = _deskWafers.Any(w =>
                w.WaferType == waferType &&
                string.Equals(w.ShipRegistration, ship.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase));
            if (alreadyOnDesk)
            {
                _prematurelyWipedWafersForActiveShip.Remove(waferType);
                continue;
            }

            if (_waferHopperCount <= 0)
            {
                break;
            }

            switch (waferType)
            {
                case CertWaferType.CargoManifest:
                    EjectWaferFromHopper(BuildCargoManifestWaferCard(ship));
                    break;
            }
        }
    }

    internal void ForceEmptyWaferHopperForDebug()
    {
        _waferHopperCount = 0;
        if (IsWaferWriterOperational)
        {
            while (_deskWafers.Count < MaxWaferHopperCount)
            {
                var (tx, ty) = ComputeWaferLeftFanTarget(_deskWafers.Count);
                _deskWafers.Add(new CertWaferCard
                {
                    InstanceId = _nextWaferInstanceId++,
                    ShipRegistration = _activeShip?.Slate.RegistrationCode ?? $"FTV-{100 + _deskWafers.Count}-D",
                    PrimaryCargoItem = "Hull Plating",
                    SecondaryNote = "SPENT CERAMIC WAFER",
                    X = tx,
                    Y = ty,
                    EjectTargetX = tx,
                    EjectTargetY = ty,
                    IsEjecting = false
                });
            }
        }
        AddSystemLogLine("[DEV] WAFER HOPPER FORCED EMPTY (0/3 BLANKS).", CommsTextTone.AlertCrimson);
    }

    private void UpdateWaferWriterAndWafers(float deltaTime, float animDt)
    {
        if (_waferWriteHeadFlashTimer > 0f)
        {
            _waferWriteHeadFlashTimer = Math.Max(0f, _waferWriteHeadFlashTimer - deltaTime);
        }

        if (_waferWipeFlashTimer > 0f)
        {
            _waferWipeFlashTimer = Math.Max(0f, _waferWipeFlashTimer - deltaTime);
        }

        if (_pendingSequentialWipeFlashes > 0)
        {
            _sequentialWipeStepTimer -= deltaTime;
            if (_sequentialWipeStepTimer <= 0f)
            {
                _pendingSequentialWipeFlashes--;
                _sequentialWipeStepTimer = WaferWipeSequentialStepSeconds;
                _waferWipeFlashTimer = 0.20f;
                AudioHooks.Play("wafer_wipe");
            }
        }

        if (_isTransmittingMissingWafer && _activeShip != null)
        {
            _missingWaferTransmitElapsed += animDt;
            if (_missingWaferTransmitElapsed >= MissingWaferTransmitDurationSeconds)
            {
                _isTransmittingMissingWafer = false;
                _missingWaferTransmitElapsed = 0f;
                _activeShip.GroundTruth.CargoManifestTransmitted = true;
                QueueOrEjectCargoManifestWafer(_activeShip);
                if (_stokerMassAnchoredForShip && IsStokerOperational)
                {
                    RefreshStokerAnchorPreviewForActiveShip();
                }
            }
        }

        if (_isReTransmittingWipedWafers && _activeShip != null)
        {
            _reTransmitWipedWaferElapsed += animDt;
            if (_reTransmitWipedWaferElapsed >= MissingWaferTransmitDurationSeconds)
            {
                _isReTransmittingWipedWafers = false;
                _reTransmitWipedWaferElapsed = 0f;
                ReDemuxPrematurelyWipedWafers(_activeShip);
            }
        }

        for (int i = 0; i < _deskWafers.Count; i++)
        {
            CertWaferCard w = _deskWafers[i];
            if (!w.IsEjecting)
                continue;

            w.EjectElapsed += animDt;
            float t = Math.Clamp(w.EjectElapsed / WaferEjectDurationSeconds, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - t, 3f);
            w.X = (int)MathF.Round(w.EjectStartX + (w.EjectTargetX - w.EjectStartX) * eased);
            w.Y = (int)MathF.Round(w.EjectStartY + (w.EjectTargetY - w.EjectStartY) * eased);
            if (t >= 1f)
            {
                w.X = w.EjectTargetX;
                w.Y = w.EjectTargetY;
                w.IsEjecting = false;
            }
        }
    }

    private bool TryHandleDeskWafersMousePress(VirtualMouseState mouse)
    {
        if (!IsWaferWriterOperational || _deskWafers.Count == 0)
            return false;

        for (int i = _deskWafers.Count - 1; i >= 0; i--)
        {
            CertWaferCard clicked = _deskWafers[i];
            if (!mouse.IsHit(clicked.X, clicked.Y, WaferCardW, WaferCardH))
                continue;

            clicked.IsEjecting = false;
            _deskWafers.RemoveAt(i);
            _deskWafers.Add(clicked);

            _draggedWaferIds.Clear();
            _draggedWaferIds.Add(clicked.InstanceId);
            _waferDragOffsetX = mouse.X - clicked.X;
            _waferDragOffsetY = mouse.Y - clicked.Y;
            _waferScoopGestureActive = _config.WaferScoopMode switch
            {
                WaferScoopMode.PostVerdictAutoScoop => IsEncounterPostVerdictForWaferScoop,
                WaferScoopMode.CenterSwipeScoop => true,
                WaferScoopMode.ModifierScoop => Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift),
                _ => false
            };

            AudioHooks.Play("terminal_key_click");
            return true;
        }

        return false;
    }

    private bool TryHandleWaferRightMouseInput(VirtualMouseState mouse)
    {
        if (!IsWaferWriterOperational || _deskWafers.Count == 0)
            return false;

        if (!Raylib.IsMouseButtonPressed(MouseButton.Right))
            return false;

        if (_config.WaferScoopMode == WaferScoopMode.ModifierScoop)
        {
            for (int i = _deskWafers.Count - 1; i >= 0; i--)
            {
                CertWaferCard clicked = _deskWafers[i];
                if (!mouse.IsHit(clicked.X, clicked.Y, WaferCardW, WaferCardH))
                    continue;

                clicked.IsEjecting = false;
                _deskWafers.RemoveAt(i);
                _deskWafers.Add(clicked);
                _draggedWaferIds.Clear();
                _draggedWaferIds.Add(clicked.InstanceId);
                _waferDragOffsetX = mouse.X - clicked.X;
                _waferDragOffsetY = mouse.Y - clicked.Y;
                _waferScoopGestureActive = true;
                AudioHooks.Play("terminal_key_click");
                return true;
            }
        }
        else if (_config.WaferScoopMode == WaferScoopMode.CenterSwipeScoop && _deskWafers.Count >= 2)
        {
            // Right-clicking overlapping wafers on desk fans them neatly into left-fan slots
            for (int i = _deskWafers.Count - 1; i >= 0; i--)
            {
                CertWaferCard w = _deskWafers[i];
                if (mouse.IsHit(w.X, w.Y, WaferCardW, WaferCardH))
                {
                    for (int k = 0; k < _deskWafers.Count; k++)
                    {
                        var (fx, fy) = ComputeWaferLeftFanTarget(k);
                        _deskWafers[k].X = fx;
                        _deskWafers[k].Y = fy;
                        _deskWafers[k].IsEjecting = false;
                    }
                    AudioHooks.Play("terminal_key_click");
                    return true;
                }
            }
        }

        return false;
    }

    private void UpdateActiveWaferDrag(VirtualMouseState mouse)
    {
        if (_draggedWaferIds.Count == 0)
            return;

        CertWaferCard? lead = _deskWafers.FirstOrDefault(w => w.InstanceId == _draggedWaferIds[0]);
        if (lead == null)
        {
            _draggedWaferIds.Clear();
            return;
        }

        bool stillHolding = mouse.LeftDown ||
                            (_config.WaferScoopMode == WaferScoopMode.ModifierScoop &&
                             _waferScoopGestureActive &&
                             Raylib.IsMouseButtonDown(MouseButton.Right));

        // CenterSwipeScoop: right-click while holding a multi-wafer stack drops the top/last scooped wafer onto desk
        if (stillHolding &&
            _config.WaferScoopMode == WaferScoopMode.CenterSwipeScoop &&
            _draggedWaferIds.Count > 1 &&
            Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            int droppedId = _draggedWaferIds[^1];
            _draggedWaferIds.RemoveAt(_draggedWaferIds.Count - 1);
            CertWaferCard? dropped = _deskWafers.FirstOrDefault(w => w.InstanceId == droppedId);
            if (dropped != null)
            {
                DeskRect b = _layout.FreeDragBounds;
                dropped.X = Math.Clamp(lead.X - 20, b.X, b.Right - WaferCardW);
                dropped.Y = Math.Clamp(lead.Y + 12, _layout.FreeDeskRestMinY, b.Bottom - WaferCardH);
            }
            AudioHooks.Play("terminal_key_click");
        }

        if (stillHolding)
        {
            // Refresh scoop eligibility dynamically (e.g. Shift held mid-drag in ModifierScoop, or verdict reached in PostVerdictAutoScoop)
            bool canScoopNow = _config.WaferScoopMode switch
            {
                WaferScoopMode.PostVerdictAutoScoop => IsEncounterPostVerdictForWaferScoop,
                WaferScoopMode.CenterSwipeScoop => true,
                WaferScoopMode.ModifierScoop => _waferScoopGestureActive || Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift),
                _ => false
            };

            DeskRect bounds = _layout.FreeDragBounds;
            int maxDragX = Math.Max(bounds.Right - WaferCardW, _layout.WaferFeedSlot.Right - WaferCardW / 2);
            lead.X = Math.Clamp(mouse.X - _waferDragOffsetX, bounds.X, maxDragX);
            lead.Y = Math.Clamp(mouse.Y - _waferDragOffsetY, _layout.FreeDeskRestMinY, bounds.Bottom - WaferCardH + 24);

            if (canScoopNow)
            {
                TryScoopCrossedWafersIntoStack(lead, mouse);
            }

            // Position scooped followers in a crisp staggered card stack (+8px, +8px per card)
            for (int s = 1; s < _draggedWaferIds.Count; s++)
            {
                CertWaferCard? follower = _deskWafers.FirstOrDefault(w => w.InstanceId == _draggedWaferIds[s]);
                if (follower != null)
                {
                    follower.IsEjecting = false;
                    follower.X = lead.X + s * 8;
                    follower.Y = lead.Y + s * 8;
                }
            }
            return;
        }

        // Released drag: check if dropped into the top-edge Wafer Wipe Feed Slot!
        if (IsWaferOverTopWipeFeedSlot(mouse, lead))
        {
            WipeWafersInFeedSlot(_draggedWaferIds);
            _draggedWaferIds.Clear();
            _waferScoopGestureActive = false;
            return;
        }

        DeskRect restBounds = _layout.FreeDragBounds;
        for (int s = 0; s < _draggedWaferIds.Count; s++)
        {
            CertWaferCard? card = _deskWafers.FirstOrDefault(w => w.InstanceId == _draggedWaferIds[s]);
            if (card != null)
            {
                card.X = Math.Clamp(lead.X + s * 10, restBounds.X, restBounds.Right - WaferCardW);
                card.Y = Math.Clamp(lead.Y + s * 8, _layout.FreeDeskRestMinY, restBounds.Bottom - WaferCardH);
            }
        }

        _draggedWaferIds.Clear();
        _waferScoopGestureActive = false;
    }

    internal void TryScoopCrossedWafersIntoStack(CertWaferCard lead, VirtualMouseState mouse)
    {
        for (int i = 0; i < _deskWafers.Count; i++)
        {
            CertWaferCard other = _deskWafers[i];
            if (_draggedWaferIds.Contains(other.InstanceId))
                continue;

            bool hit;
            if (_config.WaferScoopMode == WaferScoopMode.CenterSwipeScoop)
            {
                int cx = other.X + WaferCardW / 2;
                int cy = other.Y + WaferCardH / 2;
                hit = mouse.IsHit(cx - 28, cy - 28, 56, 56) ||
                      (Math.Abs((lead.X + WaferCardW / 2) - cx) <= 28 && Math.Abs((lead.Y + WaferCardH / 2) - cy) <= 28);
            }
            else
            {
                hit = lead.X < other.X + WaferCardW - 12 &&
                      lead.X + WaferCardW > other.X + 12 &&
                      lead.Y < other.Y + WaferCardH - 12 &&
                      lead.Y + WaferCardH > other.Y + 12;
            }

            if (hit)
            {
                other.IsEjecting = false;
                _draggedWaferIds.Add(other.InstanceId);
                AudioHooks.Play("terminal_key_click");
            }
        }
    }

    private bool IsWaferOverTopWipeFeedSlot(VirtualMouseState mouse, CertWaferCard lead)
    {
        DeskRect slot = _layout.WaferFeedSlot;
        if (mouse.IsHit(slot.X - 10, slot.Y - 18, slot.W + 20, slot.H + 34))
            return true;

        return lead.X + WaferCardW > slot.X + 8 &&
               lead.X < slot.Right - 8 &&
               lead.Y + WaferCardH >= slot.Y - 6 &&
               lead.Y <= slot.Bottom + 24;
    }

    internal int WipeWafersInFeedSlot(IReadOnlyList<int> waferIdsToWipe)
    {
        int wiped = 0;
        List<CertWaferCard> wipedCards = [];
        for (int i = 0; i < waferIdsToWipe.Count; i++)
        {
            int id = waferIdsToWipe[i];
            int idx = _deskWafers.FindIndex(w => w.InstanceId == id);
            if (idx >= 0)
            {
                wipedCards.Add(_deskWafers[idx]);
                _deskWafers.RemoveAt(idx);
                wiped++;
            }
        }

        if (wiped <= 0)
            return 0;

        // Track prematurely wiped Cert-Wafer types that had already demuxed for the active docked ship
        // (never tracking certificates that were never transmitted/demuxed, and never tracking post-detain cleanup).
        if (_activeShip != null &&
            !_detainLeverPulled &&
            _phase is EncounterPhase.InspectingSlateAndShip
                or EncounterPhase.UploadingClearance
                or EncounterPhase.AwaitingClearanceReturnToTopPort)
        {
            for (int i = 0; i < wipedCards.Count; i++)
            {
                CertWaferCard wipedCard = wipedCards[i];
                if (!string.Equals(wipedCard.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!_demuxedWafersForActiveShip.Contains(wipedCard.WaferType))
                    continue;

                bool anotherStillOnDesk = _deskWafers.Any(w =>
                    w.WaferType == wipedCard.WaferType &&
                    string.Equals(w.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase));
                if (!anotherStillOnDesk)
                {
                    _prematurelyWipedWafersForActiveShip.Add(wipedCard.WaferType);
                }
            }
        }

        _waferHopperCount = Math.Min(MaxWaferHopperCount, _waferHopperCount + wiped);
        _waferWipeFlashTimer = 0.24f;
        AudioHooks.Play("wafer_wipe");

        if (wiped > 1)
        {
            _pendingSequentialWipeFlashes += (wiped - 1);
            _sequentialWipeStepTimer = WaferWipeSequentialStepSeconds;
        }

        AddSystemLogLine(
            wiped == 1
                ? $"[WAFER WRITER] CERT-WAFER WIPED BLANK ({_waferHopperCount}/{MaxWaferHopperCount} IN HOPPER)."
                : $"[WAFER WRITER] WIPED {wiped} STACKED CERT-WAFERS ({_waferHopperCount}/{MaxWaferHopperCount} IN HOPPER).",
            CommsTextTone.ClearanceTeal);

        // If a pending Cargo Manifest print was held due to an empty hopper, release and eject it immediately!
        if (_heldPendingWafer != null && _waferHopperCount > 0)
        {
            CertWaferCard held = _heldPendingWafer;
            _heldPendingWafer = null;
            _emptyHopperReminderLoggedForHeldWafer = false;
            EjectWaferFromHopper(held);
        }

        return wiped;
    }

    private void DrawDeskCertWafers(VirtualMouseState mouse)
    {
        for (int i = 0; i < _deskWafers.Count; i++)
        {
            CertWaferCard card = _deskWafers[i];
            bool isTopmost = i == _deskWafers.Count - 1;
            DrawSingleCertWaferCard(mouse, card, isTopmost);
        }
    }

    private void DrawSingleCertWaferCard(VirtualMouseState mouse, CertWaferCard card, bool isTopmost)
    {
        int x = card.X;
        int y = card.Y;
        const int w = WaferCardW;
        const int h = WaferCardH;

        // Air-gapped matte ceramic-polymer Cargo Manifest Cert-Wafer (muted brown & charcoal dull-grey, zero glow/backlight!)
        Color ceramicBody = new(56, 59, 64, 255);       // Dull charcoal-grey ceramic polymer
        Color ceramicBevel = new(78, 82, 88, 255);
        Color cargoHeaderBand = new(94, 71, 54, 255);   // Muted brown Cargo Manifest band
        Color matteEInkPaper = new(198, 191, 176, 255); // Unlit ceramic e-ink plate
        Color eInkRule = new(156, 148, 134, 255);
        Color inkDark = new(28, 26, 24, 255);
        Color inkSubtle = new(82, 66, 54, 255);

        Raylib.DrawRectangle(x + 2, y + 2, w, h, new Color(6, 8, 12, 165));
        Raylib.DrawRectangle(x, y, w, h, ceramicBody);
        Raylib.DrawRectangleLines(x, y, w, h, isTopmost ? new Color(156, 134, 112, 255) : Palette.OutlineVoid);
        Raylib.DrawLine(x + 1, y + 1, x + w - 2, y + 1, ceramicBevel);

        // Top-right corner orientation notch (air-gapped zero-pins ceramic token)
        Raylib.DrawRectangle(x + w - 7, y, 7, 7, Palette.OutlineVoid);

        // Muted brown Cargo Manifest header strip
        Raylib.DrawRectangle(x + 4, y + 4, w - 12, 18, cargoHeaderBand);
        UiText.Draw(
            UiText.TruncateToWidth("CARGO MANIFEST", 10, w - 18, denseRow: true),
            x + 8,
            y + 7,
            10,
            new Color(232, 224, 210, 255),
            denseRow: true);

        // Unlit matte e-ink recess
        int ex = x + 6;
        int ey = y + 26;
        int ew = w - 12;
        int eh = h - 46;
        Raylib.DrawRectangle(ex, ey, ew, eh, matteEInkPaper);
        Raylib.DrawRectangleLines(ex, ey, ew, eh, new Color(40, 42, 46, 255));

        UiText.Draw(
            UiText.TruncateToWidth($"VESSEL: {card.ShipRegistration}", 10, ew - 8, denseRow: true),
            ex + 4,
            ey + 5,
            10,
            inkSubtle,
            denseRow: true);
        Raylib.DrawLine(ex + 4, ey + 19, ex + ew - 5, ey + 19, eInkRule);

        UiText.Draw("DECLARED HOLD:", ex + 4, ey + 24, 10, inkSubtle, denseRow: true);

        List<string> itemLines = WrapTextByPixelWidth(card.PrimaryCargoItem.ToUpperInvariant(), 10, ew - 10, maxLines: 2, denseRow: true);
        for (int l = 0; l < itemLines.Count; l++)
        {
            UiText.Draw(itemLines[l], ex + 5, ey + 38 + l * 13, 10, inkDark, denseRow: true);
        }

        Raylib.DrawLine(ex + 4, ey + 68, ex + ew - 5, ey + 68, eInkRule);
        UiText.Draw(
            UiText.TruncateToWidth(card.SecondaryNote, 10, ew - 8, denseRow: true),
            ex + 4,
            ey + 74,
            10,
            inkSubtle,
            denseRow: true);
        UiText.Draw(
            UiText.TruncateToWidth("STAGE-2 E-INK WAFER", 10, ew - 8, denseRow: true),
            ex + 4,
            ey + 90,
            10,
            inkSubtle,
            denseRow: true);

        string foot = _draggedWaferIds.Count > 1 && _draggedWaferIds[0] == card.InstanceId
            ? $"STACK ({_draggedWaferIds.Count}) -> TOP WIPE"
            : "WIPE IN TOP SLOT";
        UiText.Draw(
            UiText.TruncateToWidth(foot, 10, w - 12, denseRow: true),
            x + 6,
            y + h - 16,
            10,
            new Color(172, 166, 154, 255),
            denseRow: true);

        // Matte ceramic-polymer e-ink has zero backlight: attenuate cleanly with Booth 04 brownout lighting!
        if (IsBrownoutAttenuatingPassiveSurfaces)
        {
            ApplyBrownoutPassiveSurfaceLighting(x, y, w, h);
        }

        bool isActiveShipWafer = _activeShip != null &&
                                 string.Equals(card.ShipRegistration, _activeShip.Slate.RegistrationCode, StringComparison.OrdinalIgnoreCase);
        if (isActiveShipWafer && IsTargetFaultHighlighted(ReviewTarget.CargoManifestWafer))
        {
            Color col = GetFaultPulseColor();
            Raylib.DrawRectangleLines(x, y, w, h, col);
            Raylib.DrawRectangleLines(x - 1, y - 1, w + 2, h + 2, col);
        }
        else if (isActiveShipWafer &&
                 _protocolReviewModeActive &&
                 IsShipInspectableForReview &&
                 IsReviewTargetUnlocked(ReviewTarget.CargoManifestWafer))
        {
            DrawReviewHotspotOutline(mouse, x, y, w, h, _reviewFirstSelection == ReviewTarget.CargoManifestWafer);
        }
    }

    private static string GetAuthorizedSpeciesRuleLine(string prefix) =>
        prefix.ToUpperInvariant() switch
        {
            "KDF-" => "KORDANI ONLY",
            "BMC-" => "BRENGOP ONLY",
            "KRW-" => "KRUSHVAK ONLY",
            "ORN-" => "ORUN ONLY",
            "TLV-" => "TOLVAK ONLY",
            "PRJ-" => "PRUNJUN ONLY",
            "HBR-" => "HUN-BORIAN ONLY",
            "GUV-" => "GU MEMBERS (HUM / HEL / KLA / THN / VEL)",
            _ => "MULTI-SPECIES PERMITTED"
        };

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
        string clipped = UiText.TruncateToWidth(label, 10, Math.Max(8, w - 8), denseRow: true);
        int tw = UiText.MeasureWidth(clipped, 10, denseRow: true);
        UiText.Draw(clipped, x + Math.Max(4, (w - tw) / 2), y + 4, 10, fg, denseRow: true);
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
            UiText.Draw(TruncateByPixelWidth(gripLeft, 10, Math.Max(20, w - 76)), x + 6, gripY + 1, 10, Palette.CrtAmberPrimary);
        }
        if (w >= 156)
        {
            string gripRight = !isTornOff
                ? (printProgress >= 1.0f ? "DRAG TO TEAR" : "PRINTING...")
                : (hasSubstantialOverlap ? "BACKLIT FILM" : "DRAG TO CHECKER");
            Color rightCol = !isTornOff
                ? (printProgress >= 1.0f ? Palette.ClearanceTeal : Palette.CrtAmberDimmed)
                : (hasSubstantialOverlap ? Palette.CrtAmberPrimary : Palette.CrtAmberDimmed);
            int rw = UiText.MeasureWidth(gripRight, 10);
            UiText.Draw(gripRight, x + w - rw - 5, gripY + 1, 10, rightCol);
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

        if (IsBrownoutAttenuatingPassiveSurfaces && !hasBacklightOverlap)
        {
            ApplyBrownoutPassiveSurfaceLighting(x, y, w, h);
        }
    }
}
