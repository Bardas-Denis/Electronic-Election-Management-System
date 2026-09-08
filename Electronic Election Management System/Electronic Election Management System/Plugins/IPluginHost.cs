using Electronic_Election_Management_System.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Everything loaded from the plugin folder. Registered as a singleton and populated once, at
/// startup; the folder is the single source of truth for what exists.
/// </summary>
public interface IPluginHost
{
    /// <summary>Scans the plugin folder. Called once, at startup.</summary>
    void Load();

    /// <summary>Every loaded plugin implementing <typeparamref name="T"/>.</summary>
    IReadOnlyList<T> GetAll<T>() where T : class, IPlugin;

    /// <summary>The plugin implementing <typeparamref name="T"/> with this key, if it is loaded.</summary>
    bool TryGet<T>(string key, out T plugin) where T : class, IPlugin;
}
