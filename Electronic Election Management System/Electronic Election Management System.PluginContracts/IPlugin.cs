namespace Electronic_Election_Management_System.PluginContracts;

/// <summary>
/// The base every plugin implements, whatever it actually does.
/// </summary>
/// <remarks>
/// Implementations need a public parameterless constructor, and must be thread-safe: one instance
/// is shared by every request for the lifetime of the process.
/// </remarks>
public interface IPlugin
{
    /// <summary>
    /// Stable identifier, persisted wherever this plugin is referenced. Changing it orphans
    /// everything already pointing at the plugin, so treat it as permanent.
    /// </summary>
    string Key { get; }

    /// <summary>Name shown to users.</summary>
    string DisplayName { get; }
}
