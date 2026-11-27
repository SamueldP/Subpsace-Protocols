using SubspaceProtocols.Core;
using SubspaceProtocols.Data;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Full economy, requisition, Quarters sleep resolution, Morning Ship Roster delivery,
/// Berth Audit resolution, and FORUM filtering engine.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class EconomyEngine
{
    public const string Day1BrengopEmailId = TerminalEmail.Day1BrengopEmailId;
    public const string Day6IntroMissionId = "day6_mng_intro";
    public const string Day6HackEmailId = "day6_terminal_hack_drop";

    public bool EnsureDay1BrengopEmailSeeded(GameState state)
    {
        if (state.Day != 1 || state.Day1BrengopEmailRead)
            return false;

        if (state.Emails.Any(e => e.IsDay1BrengopEmail ||
                                  e.EmailId.Equals(Day1BrengopEmailId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        state.AddEmail(TerminalEmail.CreateDay1BrengopReassignmentEmail());
        state.SaveToDisk();
        return true;
    }

    public int CalculateBaseShiftPay(int accuratelyProcessedShips, int payPerShipScrip = Constants.BasePayPerShipScrip) =>
        Math.Max(0, accuratelyProcessedShips) * Math.Max(1, payPerShipScrip);

    /// <summary>
    /// Runs once right at clock-out (after the 2.5s Booth 04 power-down delay) to produce the
    /// Clock-Out End-of-Shift Ledger slip and auto-deduct Berth Lease (-30 SC).
    /// Note: Ship base pay and clerical fines were already applied to state.PactScrip live during the shift
    /// by ValidationEngine, so this method never double-pays or double-fines.
    /// </summary>
    public ShiftLedgerSummary SettleShiftClockOut(GameState state, Core.ConfigFlags config)
    {
        int payPerShip = state.BasePayPerShipScrip > 0 ? state.BasePayPerShipScrip : Constants.BasePayPerShipScrip;
        int grossPay = state.ShiftGrossPayEarned > 0
            ? state.ShiftGrossPayEarned
            : CalculateBaseShiftPay(state.ShipsProcessedToday, payPerShip);

        int stokerFine = 0;
        state.LastShiftStokerOverload = state.StokerOverloadedToday;
        if (state.StokerOverloadedToday)
        {
            stokerFine = Math.Max(0, config.StokerOverloadFine); // TODO: BALANCE
            state.PactScrip = Math.Max(0, state.PactScrip - stokerFine);
            state.PendingStokerBerthSearch = true;
            state.NextDayPowerBrownout = true;
        }
        else
        {
            state.NextDayPowerBrownout = false;
        }

        int leaseCost = Math.Max(0, config.BerthLeaseCost);
        bool leasePaid;
        bool leaseDeferred = false;
        bool foreclosure = false;

        if (state.PactScrip >= leaseCost)
        {
            state.PactScrip -= leaseCost;
            state.MissedBerthLeaseCount = 0;
            leasePaid = true;
        }
        else
        {
            leasePaid = false;
            state.MissedBerthLeaseCount++;
            if (state.MissedBerthLeaseCount >= Constants.MaxMissedBerthLeaseNights)
            {
                foreclosure = true;
                state.EndingFlags["Fail_DebtForeclosure"] = true;
            }
            else
            {
                leaseDeferred = true;
            }
        }

        // Check if any encrypted covert mission dispatch triggers tonight (Evening Quarters)
        DeliverScheduledEmailsForPhase(state, state.Day, QuartersPhase.Evening, config);

        // Clear transient CleanAuditFine banner after displaying on this shift's ledger
        int cleanAuditFineShown = state.LastCleanAuditFineApplied;
        state.LastCleanAuditFineApplied = 0;

        ShiftLedgerSummary summary = new()
        {
            ShiftDay = state.Day,
            ShipsProcessedAccurate = state.ShipsProcessedToday,
            PayPerShipScrip = payPerShip,
            GrossShiftPay = grossPay,
            ClericalWarnings = state.ClericalWarningsThisShift,
            ClericalFinesTotal = state.ShiftClericalFinesPaid,
            StokerOverloaded = state.StokerOverloadedToday,
            StokerOverloadFine = stokerFine,
            SecurityStrikesEnd = state.SecurityStrikes,
            MaxStrikesBeforeAudit = config.MaxStrikesBeforeAudit,
            CleanAuditFineApplied = cleanAuditFineShown,
            BerthLeaseCost = leaseCost,
            BerthLeasePaid = leasePaid,
            BerthLeaseDeferred = leaseDeferred,
            DebtForeclosureTriggered = foreclosure,
            MissedLeaseCountAfter = state.MissedBerthLeaseCount,
            ResultingNetScrip = state.PactScrip,
            PendingStokerSabotageAudit = state.PendingStokerBerthSearch
        };

        state.LastShiftSummary = summary;
        state.QuartersPhase = QuartersPhase.Evening;
        state.SaveToDisk();
        return summary;
    }

    /// <summary>
    /// Orders tonight's automatic food ration on PACT REQ (Evening Quarters only).
    /// Pure Pact Scrip (SC) flat pricing: Dry Ration Bricks (10 SC), Nutrient Paste (20 SC), High-Tier Rations (30 SC).
    /// </summary>
    public (bool Success, string Feedback) TryOrderNightlyRation(
        GameState state,
        NightlyRationOrder order,
        Core.ConfigFlags config)
    {
        if (state.QuartersPhase != QuartersPhase.Evening)
        {
            return (false, "NIGHTLY RATIONS CAN ONLY BE ORDERED IN EVENING QUARTERS.");
        }

        if (order == NightlyRationOrder.None)
        {
            return (false, "INVALID RATION ORDER.");
        }

        if (state.TonightRationOrder == order)
        {
            return (false, "RATION TIER ALREADY ACTIVE FOR TONIGHT.");
        }

        if (state.TonightRationOrder != NightlyRationOrder.None)
        {
            return (false, "TONIGHT'S RATION ALREADY DISPENSED ON REPLICATOR.");
        }

        int cost;
        string itemName;
        switch (order)
        {
            case NightlyRationOrder.DryRationBricks:
                cost = Math.Max(0, config.DryRationBricksCost);
                itemName = "DRY RATION BRICKS";
                break;

            case NightlyRationOrder.NutrientPaste:
                cost = Math.Max(0, config.NutrientPasteCost);
                itemName = "NUTRIENT PASTE";
                break;

            case NightlyRationOrder.HighTierRations:
                cost = Math.Max(0, config.HighTierRationsCost);
                itemName = "HIGH-TIER RATIONS";
                break;

            default:
                return (false, "UNKNOWN RATION SKU.");
        }

        if (state.PactScrip < cost)
        {
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED).");
        }

        state.PactScrip -= cost;
        state.TonightRationOrder = order;
        state.SaveToDisk();
        return (true, $"ORDERED {itemName} (-{cost} SC). REPLICATOR DISPENSED.");
    }

    // =========================================================================
    // Day 6 Morning Unsolicited Drop, Terminal Hack Activation & DECK 4 Contracts
    // =========================================================================

    /// <summary>
    /// On Day 6 Morning (Day == 6 &amp;&amp; !TerminalHackedUnlocked), drops one free unmarked black
    /// Blank Data Slate into the 0500 Service Chute and delivers the anomalous unread
    /// CHECK YOUR CHUTE email alongside the Day 6 Morning Ship Roster.
    /// </summary>
    public bool EnsureDay6MorningUnsolicitedDrop(GameState state)
    {
        if (state.Day != 6 || state.TerminalHackedUnlocked || state.Day6UnsolicitedDropDelivered)
            return false;

        (string targetReg, _) = ShipGenerator.EnsureDay6IntroTargetAssigned(state);
        state.Day6UnsolicitedDropDelivered = true;

        // 1. Free unmarked black Blank Data Slate in 0500 Service Chute
        state.ServiceChuteItems.Add(new ContrabandPocketItem
        {
            ItemId = state.NextUniqueItemId("d6_chute_blank"),
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "UNMARKED BLACK DATA SLATE"
        });

        // 2. Anomalous unread email in MESSAGES (Build 4.7 verbatim with rolled target reg)
        (int cy, int cm) = Constants.GetStationCycle(state.Day);
        state.AddEmail(new TerminalEmail
        {
            EmailId = Day6HackEmailId,
            DayReceived = state.Day,
            PhaseReceived = "MORNING",
            CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // 05:01",
            Sender = "unknown",
            Subject = "Check your chute",
            CustomStatusLine = "asf.as\u2026tje",
            Body = "Hi\u2026\n" +
                   "Check your Chute next to your door.\n" +
                   "You\u2019ll find a blank data-slate. Don\u2019t worry too much where it comes from\u2026 just know that you still have friends down on Deck 4\n" +
                   $"There is a MNG- rig ({targetReg}) on today\u2019s roster carrying telemetry someone is willing to pay for. Slot in the blank slate into the Bus before protocols hit.\n" +
                   "You can transmit the slate tonight via the Cryptography tab\n" +
                   "Don\u2019t forget to scrub your logs, I hear the Kordani are getting on edge about something.\n" +
                   "Regards\n" +
                   "A friend",
            IsRead = false,
            IsEncrypted = false,
            IsTerminalHackEmail = true
        });

        state.SaveToDisk();
        return true;
    }

    /// <summary>
    /// Finalizes the Day 6 Terminal Hack unlock, revealing CRYPTO / LOGS and DECK 4
    /// and auto-activating the Day 6 MNG- intro intercept contract.
    /// </summary>
    public void ActivateDay6IntroHackMission(GameState state)
    {
        ShipGenerator.EnsureDay6IntroTargetAssigned(state);
        state.TerminalHackedUnlocked = true;

        if (!state.CompletedInterceptMissionIds.Contains(Day6IntroMissionId, StringComparer.OrdinalIgnoreCase) &&
            !state.ActiveInterceptMissionIds.Contains(Day6IntroMissionId, StringComparer.OrdinalIgnoreCase))
        {
            state.ActiveInterceptMissionIds.Add(Day6IntroMissionId);
        }

        state.SaveToDisk();
    }

    public sealed record Deck4ContractListing(CovertMissionSpec Mission, bool IsActive);

    public List<Deck4ContractListing> GetDeck4ContractListings(GameState state, CovertMissionDatabase? db = null)
    {
        CovertMissionDatabase effectiveDb = db ?? CovertMissionDatabase.Load();
        List<Deck4ContractListing> results = [];

        foreach (CovertMissionSpec m in effectiveDb.Missions)
        {
            if (state.Day < m.MinAvailableDay)
                continue;

            if (m.IsDeliveryMission)
            {
                if (state.DeliveredCovertMissionIds.Contains(m.MissionId, StringComparer.OrdinalIgnoreCase))
                    continue;

                bool isActive = state.ActiveCovertMissionId.Equals(m.MissionId, StringComparison.OrdinalIgnoreCase);
                results.Add(new Deck4ContractListing(m, isActive));
            }
            else
            {
                if (state.CompletedInterceptMissionIds.Contains(m.MissionId, StringComparer.OrdinalIgnoreCase))
                    continue;

                bool isActive = state.ActiveInterceptMissionIds.Contains(m.MissionId, StringComparer.OrdinalIgnoreCase);
                if (m.AutoActivateOnHack && !isActive)
                    continue;

                results.Add(new Deck4ContractListing(m, isActive));
            }
        }

        // Active contracts first, then by unlock day
        results.Sort((a, b) =>
        {
            if (a.IsActive != b.IsActive) return a.IsActive ? -1 : 1;
            return a.Mission.MinAvailableDay.CompareTo(b.Mission.MinAvailableDay);
        });

        return results;
    }

    public (bool Success, string Feedback) TryAcceptCovertContract(
        GameState state,
        string missionId,
        CovertMissionDatabase? db = null,
        Core.ConfigFlags? config = null)
    {
        CovertMissionDatabase effectiveDb = db ?? CovertMissionDatabase.Load();
        CovertMissionSpec? spec = effectiveDb.Missions.FirstOrDefault(
            m => m.MissionId.Equals(missionId, StringComparison.OrdinalIgnoreCase));

        if (spec == null)
            return (false, "CONTRACT RECORD NOT FOUND.");

        if (state.Day < spec.MinAvailableDay)
            return (false, $"CONTRACT LOCKED UNTIL DAY {spec.MinAvailableDay:D2}.");

        if (spec.IsDeliveryMission)
        {
            if (state.DeliveredCovertMissionIds.Contains(spec.MissionId, StringComparer.OrdinalIgnoreCase))
                return (false, "DELIVERY CONTRACT ALREADY COMPLETED.");

            if (state.ActiveCovertMissionId.Equals(spec.MissionId, StringComparison.OrdinalIgnoreCase))
                return (false, "DELIVERY CONTRACT ALREADY ACTIVE.");

            if (!string.IsNullOrWhiteSpace(state.ActiveCovertMissionId))
                return (false, "COMPLETE ACTIVE DELIVERY CONTRACT FIRST.");

            state.ActiveCovertMissionId = spec.MissionId;
            state.ActiveCovertTargetReg = spec.TargetRegistration;
            state.ActiveCovertTargetPrefix = spec.TargetPrefix;
            state.ActiveCovertTargetShipName = spec.TargetShipName;
            state.ActiveCovertDeliveryDay = state.Day + 1;

            state.PendingChuteDeliveries.Add(new ContrabandPocketItem
            {
                ItemId = state.NextUniqueItemId("mission"),
                ItemType = ContrabandPocketItem.TypeMissionDataSlate,
                DisplayName = spec.SlatePayloadLabel,
                TargetShipPrefix = spec.TargetPrefix,
                TargetShipRegistration = spec.TargetRegistration,
                TargetShipName = spec.TargetShipName,
                IsMissionSlate = true,
                MissionId = spec.MissionId,
                MissionTargetReg = spec.TargetRegistration,
                MissionTargetPrefix = spec.TargetPrefix,
                MissionDescription = spec.PayloadDescription
            });

            state.Deck4UsedEver = true;
            RefreshMorningRosterForActiveContracts(state, config);
            state.SaveToDisk();
            return (true, $"ACCEPTED: SEALED {spec.SlatePayloadLabel} DROPS IN 0500 CHUTE TOMORROW.");
        }
        else
        {
            if (state.CompletedInterceptMissionIds.Contains(spec.MissionId, StringComparer.OrdinalIgnoreCase))
                return (false, "INTERCEPT CONTRACT ALREADY SETTLED.");

            if (state.ActiveInterceptMissionIds.Contains(spec.MissionId, StringComparer.OrdinalIgnoreCase))
                return (false, "INTERCEPT CONTRACT ALREADY ACTIVE.");

            state.ActiveInterceptMissionIds.Add(spec.MissionId);
            state.Deck4UsedEver = true;
            RefreshMorningRosterForActiveContracts(state, config);
            state.SaveToDisk();
            return (true, $"INTERCEPT ACCEPTED ({spec.TargetPrefix}) -- BRING BLANK SLATE TO BOOTH 04.");
        }
    }

    // =========================================================================
    // DECK 4 Grey-Market Requisitions & Fixers
    // =========================================================================
    public (bool Success, string Feedback) TryOrderBlankDataSlate(GameState state, Core.ConfigFlags config)
    {
        int cost = Math.Max(1, config.BlankDataSlateCost);
        if (state.PactScrip < cost)
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED).");

        state.PactScrip -= cost;
        state.Deck4UsedEver = true;
        state.PendingChuteDeliveries.Add(new ContrabandPocketItem
        {
            ItemId = state.NextUniqueItemId("blankslate"),
            ItemType = ContrabandPocketItem.TypeBlankDataSlate,
            DisplayName = "BLANK DATA SLATE (UNREG)"
        });
        state.SaveToDisk();
        return (true, $"RUNNER PINGED: BLANK DATA SLATE (-{cost} SC) -- 0500 CHUTE DROP.");
    }

    public (bool Success, string Feedback) TryOrderShieldedPocketLining(GameState state, Core.ConfigFlags config)
    {
        if (state.HasShieldedPocketLining)
            return (false, "SHIELDED POCKET LINING ALREADY INSTALLED.");
        if (state.ShieldedLiningOrdered)
            return (false, "SHIELDED LINING ALREADY IN TRANSIT FOR 0500 DROP.");

        int cost = Math.Max(1, config.ShieldedPocketLiningCost);
        if (state.PactScrip < cost)
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED).");

        state.PactScrip -= cost;
        state.ShieldedLiningOrdered = true;
        state.Deck4UsedEver = true;
        state.PendingChuteDeliveries.Add(new ContrabandPocketItem
        {
            ItemId = state.NextUniqueItemId("shieldlining"),
            ItemType = ContrabandPocketItem.TypeShieldedPocketLining,
            DisplayName = "SHIELDED POCKET LINING"
        });
        state.SaveToDisk();
        return (true, $"RUNNER PINGED: SHIELDED LINING (-{cost} SC) -- 0500 CHUTE DROP.");
    }

    public (bool CanUpgrade, int NextCap, int Cost, int MinDay, string StatusLabel) GetNextPocketUpgradeInfo(
        GameState state,
        Core.ConfigFlags config)
    {
        if (state.PocketCapacity <= 1)
        {
            bool unlocked = state.Day >= config.PocketUpgrade2MinDay;
            return (unlocked, 2, config.PocketUpgrade2Cost, config.PocketUpgrade2MinDay,
                unlocked ? $"UPGRADE 1->2 ({config.PocketUpgrade2Cost} SC)" : $"LOCKED UNTIL DAY {config.PocketUpgrade2MinDay:D2}");
        }

        if (state.PocketCapacity == 2)
        {
            bool unlocked = state.Day >= config.PocketUpgrade3MinDay;
            return (unlocked, 3, config.PocketUpgrade3Cost, config.PocketUpgrade3MinDay,
                unlocked ? $"UPGRADE 2->3 ({config.PocketUpgrade3Cost} SC)" : $"LOCKED UNTIL DAY {config.PocketUpgrade3MinDay:D2}");
        }

        return (false, 3, 0, 1, "MAX CAPACITY (3 / 3)");
    }

    public (bool Success, string Feedback) TryPurchasePocketCapacityUpgrade(GameState state, Core.ConfigFlags config)
    {
        var (canUpgrade, nextCap, cost, minDay, _) = GetNextPocketUpgradeInfo(state, config);
        if (state.PocketCapacity >= 3)
            return (false, "CONTRABAND POCKET ALREADY AT MAX CAPACITY (3/3).");
        if (!canUpgrade)
            return (false, $"TIER {nextCap} TAILORING UNLOCKS ON DAY {minDay:D2}.");
        if (state.PactScrip < cost)
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED).");

        state.PactScrip -= cost;
        state.PocketCapacity = nextCap;
        state.Deck4UsedEver = true;
        state.SaveToDisk();
        return (true, $"CONTRABAND POCKET EXPANDED TO {nextCap} SLOTS (-{cost} SC).");
    }

    public (bool Success, string Feedback) TryPurchaseAuditDeferralFee(GameState state, Core.ConfigFlags config)
    {
        int strikeDay = config.DayUnlockWarpScanner;
        if (state.Day < strikeDay && !state.SecurityStrikesActive)
            return (false, $"SECURITY STRIKES ARE INACTIVE UNTIL DAY {strikeDay:D2}.");
        if (state.SecurityStrikes <= 0)
            return (false, "NO ACTIVE SECURITY STRIKES ON RECORD.");

        int cost = Math.Max(1, config.AuditDeferralFeeCost);
        if (state.PactScrip < cost)
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED).");

        state.PactScrip -= cost;
        state.SecurityStrikes = Math.Max(0, state.SecurityStrikes - 1);
        if (state.SecurityStrikes < config.MaxStrikesBeforeAudit)
        {
            state.PendingThreeStrikeAudit = false;
        }
        state.Deck4UsedEver = true;
        state.SaveToDisk();
        return (true, $"AUDIT DEFERRAL PROCESSED (-{cost} SC) -- STRIKES NOW {state.SecurityStrikes}.");
    }

    // =========================================================================
    // CRYPTO / LOGS Operations (DECRYPT [0 SC + LOGS TRACE] & SCRUB LOGS [15 SC])
    // =========================================================================
    public (bool Success, string Feedback, TerminalEmail? DecryptedEmail) TryDecryptNextMessage(
        GameState state,
        Core.ConfigFlags? config = null)
    {
        TerminalEmail? target = state.Emails.FirstOrDefault(e => e.IsEncrypted);
        if (target == null)
            return (false, "NO ENCRYPTED MESSAGES WAITING IN INBOX.", null);

        target.IsEncrypted = false;
        target.Body = string.IsNullOrWhiteSpace(target.DecryptedBody) ? target.Body : target.DecryptedBody;
        target.IsRead = false; // Blink unread indicator so player notices newly decrypted mail

        // Decrypting cipher traffic leaves an [UNAUTH_IO] trace in Security Hex Logs
        state.AddSecurityLog("[UNAUTH_IO]", $"CIPHER DECRYPT: {target.Subject}", isSuspicious: true);

        // If linked to a Slate Bus Covert Delivery Mission, activate & queue the pre-loaded Mission Data Slate!
        if (!string.IsNullOrWhiteSpace(target.LinkedMissionId))
        {
            CovertMissionDatabase db = CovertMissionDatabase.Load();
            CovertMissionSpec? spec = db.Missions.FirstOrDefault(
                m => m.MissionId.Equals(target.LinkedMissionId, StringComparison.OrdinalIgnoreCase));
            if (spec != null &&
                spec.IsDeliveryMission &&
                !state.DeliveredCovertMissionIds.Contains(spec.MissionId, StringComparer.OrdinalIgnoreCase) &&
                !state.ActiveCovertMissionId.Equals(spec.MissionId, StringComparison.OrdinalIgnoreCase))
            {
                state.ActiveCovertMissionId = spec.MissionId;
                state.ActiveCovertTargetReg = spec.TargetRegistration;
                state.ActiveCovertTargetPrefix = spec.TargetPrefix;
                state.ActiveCovertTargetShipName = spec.TargetShipName;
                state.ActiveCovertDeliveryDay = state.QuartersPhase == QuartersPhase.Evening
                    ? state.Day + 1
                    : state.Day;

                ContrabandPocketItem missionSlate = new()
                {
                    ItemId = state.NextUniqueItemId("mission"),
                    ItemType = ContrabandPocketItem.TypeMissionDataSlate,
                    DisplayName = spec.SlatePayloadLabel,
                    TargetShipPrefix = spec.TargetPrefix,
                    TargetShipRegistration = spec.TargetRegistration,
                    TargetShipName = spec.TargetShipName,
                    IsMissionSlate = true,
                    MissionId = spec.MissionId,
                    MissionTargetReg = spec.TargetRegistration,
                    MissionTargetPrefix = spec.TargetPrefix,
                    MissionDescription = spec.PayloadDescription
                };

                if (state.QuartersPhase == QuartersPhase.Evening)
                {
                    state.PendingChuteDeliveries.Add(missionSlate);
                }
                else
                {
                    state.ServiceChuteItems.Add(missionSlate);
                }

                RefreshMorningRosterForActiveContracts(state, config);
            }
        }

        state.SaveToDisk();
        return (true, $"DECRYPTED: {target.Subject} (0 SC -- LOGGED [UNAUTH_IO]). CHECK MESSAGES.", target);
    }

    public void RefreshMorningRosterForActiveContracts(GameState state, Core.ConfigFlags? config = null)
    {
        if (state.Day < 2 || state.QuartersPhase != QuartersPhase.Morning)
            return;

        string rosterEmailId = $"morning_roster_day_{state.Day:D2}";
        TerminalEmail? existing = state.Emails.FirstOrDefault(
            e => e.EmailId.Equals(rosterEmailId, StringComparison.OrdinalIgnoreCase));
        bool wasRead = existing?.IsRead ?? false;

        if (existing != null)
        {
            state.Emails.Remove(existing);
        }
        state.TodayMorningRoster.Clear();

        Core.ConfigFlags effectiveCfg = config ?? new Core.ConfigFlags();
        ShipGenerator generator = new(effectiveCfg);
        EnsureMorningRosterGenerated(state, generator, effectiveCfg);

        TerminalEmail? regenerated = state.Emails.FirstOrDefault(
            e => e.EmailId.Equals(rosterEmailId, StringComparison.OrdinalIgnoreCase));
        if (regenerated != null)
        {
            regenerated.IsRead = wasRead;
        }
    }

    public (bool Success, string Feedback, int PurgedCount) TryScrubSecurityLogs(GameState state, Core.ConfigFlags? config = null)
    {
        int unscrubbed = state.SecurityLogs.Count(l => l.IsSuspicious && !l.IsPurged);
        if (unscrubbed == 0 && state.UnauthorizedLogCount == 0)
            return (false, "ALL SECURITY HEX LOGS ARE ALREADY CLEAN.", 0);

        int cost = Math.Max(0, config?.ScrubSecurityLogsCost ?? Constants.ScrubSecurityLogsCostScrip);
        if (state.PactScrip < cost)
            return (false, $"INSUFFICIENT SCRIP ({cost} SC REQUIRED TO SCRUB LOGS).", 0);

        state.PactScrip -= cost;
        foreach (SecurityLogEntry log in state.SecurityLogs)
        {
            if (log.IsSuspicious && !log.IsPurged)
            {
                log.IsPurged = true;
                log.Tag = "[PURGED]";
            }
        }
        state.UnauthorizedLogCount = 0;
        state.SaveToDisk();
        return (true, $"PURGED {unscrubbed} UNAUTHORIZED HEX LOG ENTRIES (-{cost} SC).", unscrubbed);
    }

    // =========================================================================
    // Sleep-Triggered Kordani Berth Audit & Sabotage Audit (Delegates to RaidController)
    // =========================================================================
    private readonly RaidController _raidController = new();

    public BerthAuditKind CheckPendingSleepAudit(GameState state, Core.ConfigFlags config) =>
        _raidController.CheckPendingSleepAudit(state, config);

    public BerthAuditResolution ResolveBerthAudit(
        GameState state,
        Core.ConfigFlags config,
        WorldSim worldSim,
        BerthAuditKind kind) =>
        _raidController.ResolveBerthAudit(state, config, worldSim, kind);

    // =========================================================================
    // Overnight Sleep Resolution -> Morning Quarters (NO second Berth Lease charge!)
    // =========================================================================
    public void CompleteSleepToMorning(
        GameState state,
        Core.ConfigFlags config,
        ShipGenerator generator)
    {
        // 1. Resolve overnight food & fatigue / SPS sway tier
        switch (state.TonightRationOrder)
        {
            case NightlyRationOrder.HighTierRations:
                state.CurrentHungerTier = HungerTier.HighTierRations;
                state.DaysWithoutFood = 0;
                state.ConsecutiveDryOrNoFoodDays = 0;
                break;

            case NightlyRationOrder.NutrientPaste:
                state.CurrentHungerTier = HungerTier.MediumNutrientPaste;
                state.DaysWithoutFood = 0;
                state.ConsecutiveDryOrNoFoodDays = 0;
                break;

            case NightlyRationOrder.DryRationBricks:
                state.ConsecutiveDryOrNoFoodDays++;
                state.DaysWithoutFood = 0;
                state.CurrentHungerTier = state.ConsecutiveDryOrNoFoodDays >= 2
                    ? HungerTier.LowNoFood
                    : HungerTier.MediumNutrientPaste;
                break;

            default: // Skipped food
                state.DaysWithoutFood++;
                state.ConsecutiveDryOrNoFoodDays++;
                state.CurrentHungerTier = state.ConsecutiveDryOrNoFoodDays >= 2
                    ? HungerTier.LowNoFood
                    : HungerTier.MediumNutrientPaste;
                break;
        }
        state.TonightRationOrder = NightlyRationOrder.None;

        // 2. Deliver 0500 Service Chute orders queued the night before
        if (state.PendingChuteDeliveries.Count > 0)
        {
            state.ServiceChuteItems.AddRange(state.PendingChuteDeliveries);
            state.PendingChuteDeliveries.Clear();
        }

        // 3. Roll Stoker brownout state forward: if Stoker overloaded yesterday -> brownout active today; else clear brownout
        bool brownoutToday = state.StokerOverloadedToday || state.NextDayPowerBrownout;
        state.NextDayPowerBrownout = state.StokerOverloadedToday;
        state.StokerOverloadedToday = false;
        if (!state.NextDayPowerBrownout && brownoutToday)
        {
            // Brownout lasted for the day following overload; clears cleanly on the next sleep
            state.NextDayPowerBrownout = false;
        }

        // 4. Advance Day counter to next Morning Quarters WITHOUT charging Berth Lease a second time
        int nextDay = state.Day + 1;
        state.AdvanceToDay(nextDay);
        state.QuartersPhase = QuartersPhase.Morning;

        // 5. Generate Day 2+ Morning Ship Roster email (10 expected ships, 1-3 real arrivals in pre-gen 16)
        EnsureMorningRosterGenerated(state, generator, config);

        // 5b. Day 6 Morning unsolicited 0500 Blank Data Slate drop & CHECK YOUR CHUTE hack email
        EnsureDay6MorningUnsolicitedDrop(state);

        // 6. Deliver any scheduled morning emails
        DeliverScheduledEmailsForPhase(state, state.Day, QuartersPhase.Morning, config);

        state.SaveToDisk();
    }

    /// <summary>
    /// Generates the automated Day 2+ Morning Ship Roster email into MESSAGES (absent on Day 1 Morning!).
    /// Lists 10 expected ships, of which 1 to 3 appear inside that day's deterministic 16-ship queue
    /// (including active mission target ships when scheduled; never pulled from random overflow;
    /// appearance on roster does NOT guarantee validity).
    /// </summary>
    public void EnsureMorningRosterGenerated(GameState state, ShipGenerator generator, Core.ConfigFlags? config = null)
    {
        if (state.Day < 2)
        {
            state.TodayMorningRoster.Clear();
            return;
        }

        if (state.Day == 6)
        {
            ShipGenerator.EnsureDay6IntroTargetAssigned(state);
        }

        string rosterEmailId = $"morning_roster_day_{state.Day:D2}";
        if (state.Emails.Any(e => e.EmailId.Equals(rosterEmailId, StringComparison.OrdinalIgnoreCase)) &&
            state.TodayMorningRoster.Count == 10)
        {
            return;
        }

        List<ShipSpec> dailyQueue = generator.GenerateDailyQueue(state, Constants.PreGeneratedQueueSize, reseedNonce: 0, config: config);
        List<MorningRosterEntry> roster = generator.GenerateMorningShipRoster(state, dailyQueue, config);
        state.TodayMorningRoster = roster;

        (int cy, int cm) = Constants.GetStationCycle(state.Day);
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"VOID GATE 3 // ADVANCE TRAFFIC ROSTER -- DAY {state.Day:D2}");
        sb.AppendLine($"CYCLE {Constants.FormatStationCycle(cy, cm)} // AUTOMATED 05:00 DISPATCH");
        sb.AppendLine("--------------------------------------------------------");
        sb.AppendLine("ADVANCE-FILED VESSELS EXPECTED IN SECTOR QUEUE (10):");
        sb.AppendLine();

        for (int i = 0; i < roster.Count; i++)
        {
            MorningRosterEntry r = roster[i];
            sb.AppendLine($"{i + 1:D2}. {r.RegistrationCode,-11} | {r.ShipClassCode,-3} | {r.ShipName}");
            sb.AppendLine($"    ORG: {r.OrganizationName}");
        }

        int watchSeconds = (config ?? new Core.ConfigFlags()).GetShiftDurationSeconds(state.Day);
        sb.AppendLine();
        sb.AppendLine("NOTICE: ADVANCE FILINGS DO NOT GUARANTEE COMPLIANCE.");
        sb.AppendLine($"ONLY A SUBSET WILL REACH BOOTH 04 DURING YOUR {watchSeconds}s WATCH.");
        sb.AppendLine("DETAIN ANY ROSTERED VESSEL CARRYING PROTOCOL VIOLATIONS.");

        state.AddEmail(new TerminalEmail
        {
            EmailId = rosterEmailId,
            DayReceived = state.Day,
            PhaseReceived = "MORNING",
            CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // 05:00",
            Sender = "VG-3 PORT TRAFFIC CONTROL",
            Subject = $"PORT TRAFFIC ROSTER // DAY {state.Day:D2}",
            Body = sb.ToString(),
            IsRead = false,
            IsEncrypted = false,
            IsMorningRoster = true
        });
    }

    public void DeliverScheduledEmailsForPhase(GameState state, int day, QuartersPhase phase, Core.ConfigFlags? config = null)
    {
        CovertMissionDatabase db = CovertMissionDatabase.Load();
        string phaseStr = phase.ToString();
        (int cy, int cm) = Constants.GetStationCycle(day);
        int minDeliveryEmailDay = config?.DayUnlockWaferCargo ?? Constants.DefaultDayUnlockWaferCargo;

        foreach (CovertMissionSpec m in db.Missions)
        {
            if (!m.IsDeliveryMission ||
                m.TriggerEmailDay < minDeliveryEmailDay ||
                m.TriggerEmailDay != day ||
                !m.TriggerEmailPhase.Equals(phaseStr, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (state.DeliveredCovertMissionIds.Contains(m.MissionId, StringComparer.OrdinalIgnoreCase) ||
                state.ActiveCovertMissionId.Equals(m.MissionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string emailId = $"covert_brief_{m.MissionId}";
            if (state.Emails.Any(e => e.EmailId.Equals(emailId, StringComparison.OrdinalIgnoreCase)))
                continue;

            state.AddEmail(new TerminalEmail
            {
                EmailId = emailId,
                DayReceived = day,
                PhaseReceived = phaseStr.ToUpperInvariant(),
                CycleTimestamp = $"CYC {Constants.FormatStationCycle(cy, cm)} // {(phase == QuartersPhase.Morning ? "05:10" : "21:40")}",
                Sender = m.AssigningFaction,
                Subject = m.EncryptedSubject,
                Body = "[ENCRYPTED SUBSPACE PACKET]\n" +
                       "CIPHER: PACT-EX-409 // AUTH HASH: 0x7F9C-A211\n" +
                       "7E A4 19 F0 88 C2 3B 51 99 0D E4 6A 11 B7 ...\n\n" +
                       "USE [CRYPTOGRAPHY] -> [DECRYPT MESSAGE (0 SC)] TO READ.",
                EncryptedPreview = "7E A4 19 F0 88 C2 3B 51 99 0D E4 6A 11 B7 ...",
                DecryptedBody = m.DecryptedBody,
                IsRead = false,
                IsEncrypted = true,
                LinkedMissionId = m.MissionId
            });
        }
    }

    // =========================================================================
    // FORUM Tab Thread Filtering (/data/forum.json)
    // =========================================================================
    public List<ForumThreadModel> GetVisibleForumThreads(GameState state, ForumDatabase db, Core.ConfigFlags? config = null)
    {
        int warpUnlockDay = config?.DayUnlockWarpScanner ?? 4;
        List<ForumThreadModel> visible = [];

        foreach (ForumThreadModel thread in db.Threads)
        {
            if (state.Day < thread.MinDay || state.Day > thread.MaxDay)
                continue;

            bool flagsMatch = true;
            foreach (string flag in thread.RequiredFlags)
            {
                if (!EvaluateForumFlag(state, flag, warpUnlockDay))
                {
                    flagsMatch = false;
                    break;
                }
            }
            if (!flagsMatch)
                continue;

            if (thread.PowerScoreCondition != null &&
                !string.IsNullOrWhiteSpace(thread.PowerScoreCondition.Prefix))
            {
                TrafficPowerState actual = WorldSim.EvaluateTrafficPowerState(
                    thread.PowerScoreCondition.Prefix,
                    state.GetPower(thread.PowerScoreCondition.Prefix));
                if (!actual.ToString().Equals(thread.PowerScoreCondition.State, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            visible.Add(thread);
        }

        // Prioritize urgent event-triggered threads at top of list
        visible.Sort((a, b) =>
        {
            bool aEvt = a.RequiredFlags.Count > 0 || a.PowerScoreCondition != null;
            bool bEvt = b.RequiredFlags.Count > 0 || b.PowerScoreCondition != null;
            if (aEvt != bEvt) return aEvt ? -1 : 1;
            return b.MinDay.CompareTo(a.MinDay);
        });

        return visible;
    }

    private static bool EvaluateForumFlag(GameState state, string flag, int warpUnlockDay) =>
        flag.ToLowerInvariant() switch
        {
            "stoker_overload" => state.LastShiftStokerOverload || state.StokerOverloadedToday || state.NextDayPowerBrownout || state.PendingStokerBerthSearch,
            "berth_search" => state.BerthSearchAuditOccurred || state.KordaniSabotageAuditOccurred,
            "sabotage_audit" => state.KordaniSabotageAuditOccurred,
            "day2_detained" => state.Day >= warpUnlockDay && (state.Day2SuspectEncountered || state.Day > warpUnlockDay) && state.Day2SuspectDetained,
            "day2_cleared" => state.Day >= warpUnlockDay && (state.Day2SuspectEncountered || state.Day > warpUnlockDay) && !state.Day2SuspectDetained,
            "missed_lease" => state.MissedBerthLeaseCount >= 1,
            "protected_detained" => state.ProtectedHullDetainedEver,
            "ashen_rift" => state.AshenRiftEncounteredEver,
            "deck4_used" => state.Deck4UsedEver,
            _ => state.QuestFlags.TryGetValue(flag, out bool qVal) && qVal
        };
}
