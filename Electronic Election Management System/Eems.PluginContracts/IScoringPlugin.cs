namespace Eems.PluginContracts;

/// <summary>
/// A ranking-to-points algorithm supplied by an external assembly.
/// </summary>
/// <remarks>
/// Implementations need a public parameterless constructor, and must be thread-safe: one
/// instance is shared by every request for the lifetime of the process.
/// </remarks>
public interface IScoringPlugin
{
    /// <summary>
    /// Stable identifier, persisted in <c>ScoringScheme.PluginKey</c>. Changing it orphans every
    /// election already using this plugin, so treat it as permanent.
    /// </summary>
    string Key { get; }

    /// <summary>Name shown in the scoring-scheme picker.</summary>
    string DisplayName { get; }

    /// <summary>Points this option earns from one ballot.</summary>
    int GetPoints(RankingContext context);
}
