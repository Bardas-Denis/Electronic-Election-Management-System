using System.Reflection;
using System.Runtime.Loader;
using Eems.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// One load context per plugin file, so two plugins may depend on different versions of the same
/// library without colliding.
/// </summary>
/// <remarks>
/// Not collectible: plugins are read once at startup and never unloaded. A collectible context
/// only pays off with hot-reload, and it fails to unload for any reference left behind anywhere
/// in the process - a whole class of bug bought for a feature this application does not use.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string? ContractAssemblyName =
        typeof(IScoringPlugin).Assembly.GetName().Name;

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The contract has to come from the host. Returning null defers to the default context;
        // loading a local copy would define a second IScoringPlugin type, and every plugin would
        // then fail the IsAssignableFrom check in the registry with no error raised anywhere.
        if (string.Equals(assemblyName.Name, ContractAssemblyName, StringComparison.Ordinal))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
