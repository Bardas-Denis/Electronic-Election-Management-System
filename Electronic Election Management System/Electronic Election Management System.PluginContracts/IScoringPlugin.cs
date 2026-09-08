namespace Electronic_Election_Management_System.PluginContracts;

/// <summary>
/// A ranking-to-points algorithm supplied by an external assembly.
/// </summary>
public interface IScoringPlugin : IPlugin
{
    /// <summary>Points this option earns from one ballot.</summary>
    int GetPoints(RankingContext context);
}
