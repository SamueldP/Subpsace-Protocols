using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Foundational ship queue & daily Expected Ship List (10 priority arrivals) generator stub.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ShipGenerator
{
    public List<string> GenerateExpectedShipRegistrations(int day)
    {
        List<string> expected = new(Constants.ExpectedShipsPerDay);
        for (int i = 0; i < Constants.ExpectedShipsPerDay; i++)
        {
            string prefix = Constants.AllTrackedPrefixes[(day + i) % Constants.AllTrackedPrefixes.Length];
            int regNum = 100 + ((day * 37 + i * 53) % 899);
            expected.Add($"{prefix}{regNum:D3}");
        }
        return expected;
    }
}
