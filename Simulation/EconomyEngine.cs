using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Foundational economy engine handling shift pay (15 SC/ship), Berth Lease (30 SC),
/// Power Unit conversion (5 SC/PU), 3-tier food system, and Multi-Buyer Intel sales.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class EconomyEngine
{
    public int CalculateBaseShiftPay(int accuratelyProcessedShips) =>
        Math.Max(0, accuratelyProcessedShips) * Constants.BasePayPerShipScrip;

    public bool TryPayBerthLease(GameState state)
    {
        if (state.PactScrip >= Constants.BerthLeaseNightlyScrip)
        {
            state.PactScrip -= Constants.BerthLeaseNightlyScrip;
            state.MissedBerthLeaseCount = 0;
            return true;
        }

        state.MissedBerthLeaseCount++;
        if (state.MissedBerthLeaseCount >= Constants.MaxMissedBerthLeaseNights)
        {
            state.EndingFlags["Fail_DebtForeclosure"] = true;
        }
        return false;
    }

    public bool TryPurchasePowerUnits(GameState state, int powerUnitsToBuy)
    {
        int cost = powerUnitsToBuy * Constants.ScripPerPowerUnit;
        if (powerUnitsToBuy <= 0 || state.PactScrip < cost)
            return false;

        state.PactScrip -= cost;
        state.PowerUnits += powerUnitsToBuy;
        return true;
    }
}
