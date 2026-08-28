using System.Reflection;
using Electronic_Election_Management_System.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Discovers every <see cref="IPlugin"/> implementation in the configured folder, whatever
/// contract it serves.
/// </summary>
/// <remarks>
/// <para>
/// Every failure here is logged and skipped rather than thrown. A malformed assembly dropped in
/// the folder must not stop the application from starting, and features that use no plugin must
/// keep working regardless of what else is sitting in there.
/// </para>
/// <para>
/// <see cref="IPlugin.Key"/> must be unique across every plugin in the folder, not merely within
/// one contract: the host keeps a single list and reports a repeated key as an error.
/// </para>
/// </remarks>
public sealed class PluginHost : IPluginHost
{
    private readonly PluginOptions _options;
    private readonly ILogger<PluginHost> _logger;

    private List<IPlugin> _plugins = [];

    public PluginHost(PluginOptions options, ILogger<PluginHost> logger)
    {
        _options = options;
        _logger = logger;
    }

    public IReadOnlyList<T> GetAll<T>() where T : class, IPlugin => _plugins.OfType<T>().ToList();

    public bool TryGet<T>(string key, out T plugin) where T : class, IPlugin
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var found = _plugins.OfType<T>()
                .FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

            if (found is not null)
            {
                plugin = found;
                return true;
            }
        }

        plugin = null!;
        return false;
    }

    public void Load()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Plugins are disabled (Plugins:Enabled is false).");
            return;
        }

        var directory = Path.Combine(AppContext.BaseDirectory, _options.Directory);
        if (!Directory.Exists(directory))
        {
            _logger.LogInformation(
                "Plugins are enabled but {Directory} does not exist; none loaded.", directory);
            return;
        }

        var discovered = new List<IPlugin>();

        foreach (var file in Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            // A plugin ships with its own dependencies, so the folder holds far more than
            // plugins. Anything the host must own is skipped outright: loading it here would
            // create a second copy of a type that crosses the boundary, and every plugin using
            // it would then be rejected with no error raised anywhere. Everything else is
            // inspected and simply yields no plugin types.
            if (PluginLoadContext.SharedAssemblies.Contains(Path.GetFileNameWithoutExtension(file)))
            {
                _logger.LogDebug("Skipping {File}: the host owns this assembly.", file);
                continue;
            }

            foreach (var plugin in CreatePluginsFrom(file))
            {
                var existing = discovered.FirstOrDefault(
                    p => string.Equals(p.Key, plugin.Key, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    _logger.LogError(
                        "Duplicate plugin key {Key}: {Existing} already holds it, so {Ignored} " +
                        "from {File} is ignored.",
                        plugin.Key, existing.GetType().FullName, plugin.GetType().FullName, file);
                    continue;
                }

                discovered.Add(plugin);
                _logger.LogInformation("Loaded plugin {Key} ({DisplayName}) from {File}.",
                    plugin.Key, plugin.DisplayName, Path.GetFileName(file));
            }
        }

        _plugins = discovered;
        _logger.LogInformation("Loaded {Count} plugin(s).", _plugins.Count);
    }

    private List<IPlugin> CreatePluginsFrom(string file)
    {
        var plugins = new List<IPlugin>();

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
            if (type is null || !typeof(IPlugin).IsAssignableFrom(type)) continue;
            if (type.IsInterface || type.IsAbstract) continue;

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                _logger.LogError(
                    "{Type} implements IPlugin but has no public parameterless constructor.",
                    type.FullName);
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is not IPlugin plugin) continue;

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
