using System.Reflection;
using Eems.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Discovers <see cref="IScoringPlugin"/> implementations in the configured folder.
/// </summary>
/// <remarks>
/// Every failure here is logged and skipped rather than thrown. A malformed assembly dropped in
/// the folder must not stop the API from starting, and elections that use no plugin must keep
/// working regardless of what else is sitting in there.
/// </remarks>
public sealed class ScoringPluginRegistry : IScoringPluginRegistry
{
    private readonly PluginOptions _options;
    private readonly ILogger<ScoringPluginRegistry> _logger;

    private Dictionary<string, IScoringPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public ScoringPluginRegistry(PluginOptions options, ILogger<ScoringPluginRegistry> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IReadOnlyCollection<IScoringPlugin> Plugins => _plugins.Values;

    public bool TryGet(string key, out IScoringPlugin plugin)
    {
        if (!string.IsNullOrWhiteSpace(key) && _plugins.TryGetValue(key, out var found))
        {
            plugin = found;
            return true;
        }

        plugin = null!;
        return false;
    }

    public void Load()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Scoring plugins are disabled (Plugins:Enabled is false).");
            return;
        }

        var directory = Path.Combine(AppContext.BaseDirectory, _options.Directory);
        if (!Directory.Exists(directory))
        {
            _logger.LogInformation(
                "Scoring plugins are enabled but {Directory} does not exist; none loaded.",
                directory);
            return;
        }

        var contractName = typeof(IScoringPlugin).Assembly.GetName().Name;
        var discovered = new Dictionary<string, IScoringPlugin>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            // A stray copy of the contract would load as a plugin and define a second, unrelated
            // IScoringPlugin type. Skipping it costs a line here; diagnosing "my plugin is
            // silently ignored" costs an afternoon.
            if (string.Equals(Path.GetFileNameWithoutExtension(file), contractName,
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Ignoring {File}: the plugin folder must not hold a copy of the contract " +
                    "assembly. Set Private=\"false\" on the plugin's ProjectReference.", file);
                continue;
            }

            foreach (var plugin in CreatePluginsFrom(file))
            {
                if (discovered.TryGetValue(plugin.Key, out var existing))
                {
                    _logger.LogError(
                        "Duplicate plugin key {Key}: {Existing} already holds it, so {Ignored} " +
                        "from {File} is ignored.",
                        plugin.Key, existing.GetType().FullName, plugin.GetType().FullName, file);
                    continue;
                }

                discovered.Add(plugin.Key, plugin);
                _logger.LogInformation(
                    "Loaded scoring plugin {Key} ({DisplayName}) from {File}.",
                    plugin.Key, plugin.DisplayName, Path.GetFileName(file));
            }
        }

        _plugins = discovered;
        _logger.LogInformation("Loaded {Count} scoring plugin(s).", _plugins.Count);
    }

    private List<IScoringPlugin> CreatePluginsFrom(string file)
    {
        var plugins = new List<IScoringPlugin>();

        Assembly assembly;
        try
        {
            assembly = new PluginLoadContext(file).LoadFromAssemblyPath(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load plugin assembly {File}.", file);
            return plugins;
        }

        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial success still counts: the types that did resolve may hold a usable plugin.
            _logger.LogError(ex, "Some types in {File} could not be loaded.", file);
            types = ex.Types;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not inspect types in {File}.", file);
            return plugins;
        }

        foreach (var type in types)
        {
            if (type is null || !typeof(IScoringPlugin).IsAssignableFrom(type)) continue;
            if (type.IsInterface || type.IsAbstract) continue;

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                _logger.LogError(
                    "{Type} implements IScoringPlugin but has no public parameterless constructor.",
                    type.FullName);
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is not IScoringPlugin plugin) continue;

                if (string.IsNullOrWhiteSpace(plugin.Key))
                {
                    _logger.LogError("{Type} returned an empty Key and was ignored.", type.FullName);
                    continue;
                }

                plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not instantiate {Type}.", type.FullName);
            }
        }

        return plugins;
    }
}
