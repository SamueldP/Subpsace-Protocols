using SubspaceProtocols.Core;

namespace SubspaceProtocols.Simulation;

/// <summary>
/// Foundational rule validation engine tracking the 2 free clerical warnings per shift,
/// -10 SC administrative fine, and Kordani Security Strikes.
/// Strictly decoupled from Raylib.
/// </summary>
public sealed class ValidationEngine
{
    public int ApplyClericalMistake(GameState state)
    {
        state.ClericalWarningsThisShift++;
        AudioHooks.Play("citation_printer_buzz");

        if (state.ClericalWarningsThisShift <= Constants.FreeClericalWarningsPerShift)
        {
            return 0; // Consumes 1 of 2 free warnings
        }

        state.PactScrip = Math.Max(0, state.PactScrip - Constants.ClericalErrorFineScrip);
        return Constants.ClericalErrorFineScrip;
    }

    public void AddSecurityStrike(GameState state, int count = 1)
    {
        state.SecurityStrikes += count;
        AudioHooks.Play("kordani_security_strike_alarm");

        if (state.SecurityStrikes >= Constants.SecurityStrikesAuditThreshold)
        {
            state.PendingThreeStrikeAudit = true;
        }

        if (state.SecurityStrikes >= state.MaxSecurityStrikesThreshold)
        {
            state.EndingFlags["Fail_SecurityDetainment"] = true;
        }
    }
}
