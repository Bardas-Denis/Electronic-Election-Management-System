using Eems.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// The set of scoring plugins available to this process. Registered as a singleton and populated
/// once, at startup.
/// </summary>
public interface IScoringPluginRegistry
{
    IReadOnlyCollection<IScoringPlugin> Plugins { get; }

    bool TryGet(string key, out IScoringPlugin plugin);

    /// <summary>Scans the plugin folder. Called once, at startup.</summary>
    void Load();
}
