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

    // 2^20 is already past any meaningful ballot; the cap only stops the shift overflowing.
    private const int MaxExponent = 20;

    public int GetPoints(RankingContext context)
    {
        if (context.Rank < 1 || context.Rank > context.OptionsCount) return 0;

        return 1 << Math.Min(context.OptionsCount - context.Rank, MaxExponent);
    }
}
