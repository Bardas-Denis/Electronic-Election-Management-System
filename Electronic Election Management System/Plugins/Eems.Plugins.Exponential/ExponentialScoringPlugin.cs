using Electronic_Election_Management_System.PluginContracts;

namespace Eems.Plugins.Exponential;

/// <summary>
/// Doubles the points at every place: last place scores 1, the one above it 2, and so on.
/// </summary>
/// <remarks>
/// Chosen precisely because the built-in schemes cannot express it - what first place is worth
/// depends on how many options the question had, so no fixed points list would do.
/// </remarks>
public sealed class ExponentialScoringPlugin : IScoringPlugin
{
    // Persisted in ScoringScheme.PluginKey; changing it orphans existing elections.
    public string Key => "exponential";

    public string DisplayName => "Exponential (doubling)";

    // The doubling has to stop somewhere: this contract returns an int, and a question may carry
    // up to ValidationRules.MaxOptionsPerQuestion (50) options, so 2^49 is not on the table.
    private const int MaxExponent = 20;

    public int GetPoints(RankingContext context)
    {
        if (context.Rank < 1 || context.Rank > context.OptionsCount) return 0;

        // The cap bounds how far down the ballot the doubling runs, not the exponent of a given
        // rank. Clamping the exponent instead would tie every rank further than the cap from last
        // place, collapsing the top of a long ballot - where the order matters most - into one score.
        var exponent = Math.Min(context.OptionsCount, MaxExponent + 1) - context.Rank;
        return exponent > 0 ? 1 << exponent : 1;
    }
}
