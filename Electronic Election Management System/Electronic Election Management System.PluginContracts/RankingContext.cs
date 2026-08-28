namespace Electronic_Election_Management_System.PluginContracts;

/// <summary>
/// Everything a scoring plugin is allowed to see about a single ranked vote.
/// </summary>
/// <remarks>
/// Carries no voter identity and no EF types, deliberately: a plugin must not be able to
/// attribute a ballot, and the contract must not drag the database model into every plugin.
/// </remarks>
public sealed record RankingContext
{
    /// <summary>1-based position the voter gave this option; 1 is first place.</summary>
    public required int Rank { get; init; }

    /// <summary>How many options the question offered.</summary>
    public required int OptionsCount { get; init; }
}
