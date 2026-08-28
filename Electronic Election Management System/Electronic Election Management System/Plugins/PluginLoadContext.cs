using System.Reflection;
using System.Runtime.Loader;
using Electronic_Election_Management_System.PluginContracts;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// One load context per plugin file, so two plugins may depend on different versions of the same
/// library without colliding.
/// </summary>
/// <remarks>
/// <para>
/// The demo this was modelled on shares a single context between every plugin. That is fine while
/// plugins have no dependencies of their own, but a plugin carrying its own NuGet packages - a
/// database provider, say - needs its dependencies resolved from beside its own file. Hence one
/// context per file, each with its own <see cref="AssemblyDependencyResolver"/>.
/// </para>
/// <para>
/// Not collectible: plugins are read once at startup and never unloaded. A collectible context
/// only pays off with hot-reload, and it fails to unload for any reference left behind anywhere
/// in the process - a whole class of bug bought for a feature this application does not use.
/// </para>
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// Assemblies a plugin must never load its own copy of.
    /// </summary>
    /// <remarks>
    /// These carry types that cross the boundary: the host builds a DbContextOptionsBuilder and
    /// hands it to a provider, and passes entities defined in the Data assembly. A second copy of any of
    /// them is a distinct type to the runtime, so the call would fail with a type mismatch that
    /// reads like nonsense.
    ///
    /// Matched by exact name, never by prefix: "Microsoft.EntityFrameworkCore" as a prefix would
    /// also capture Microsoft.EntityFrameworkCore.Sqlite, which is precisely the assembly a
    /// provider plugin is supposed to bring along itself.
    /// </remarks>
    internal static readonly HashSet<string> SharedAssemblies = new(StringComparer.Ordinal)
    {
        // Taken from the types themselves, so renaming either project cannot silently break
        // sharing - the symptom would be a type mismatch at runtime, far from the cause.
        typeof(PluginContracts.IPlugin).Assembly.GetName().Name!,
        typeof(Data.ElectionDbContext).Assembly.GetName().Name!,
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Abstractions",
        "Microsoft.EntityFrameworkCore.Relational",
        "Microsoft.Extensions.Logging.Abstractions",
    };

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Returning null defers to the default context, where the host already has these loaded.
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <summary>
    /// Resolves a plugin's native dependencies from beside its own files.
    /// </summary>
    /// <remarks>
    /// Without this a SQLite provider cannot find e_sqlite3: the managed wrapper loads, then
    /// fails at the first query with a DllNotFoundException that names nothing useful.
    /// </remarks>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
