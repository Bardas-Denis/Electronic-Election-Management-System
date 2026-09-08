using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Teaches the default load context to find assemblies and native libraries that live in the
/// plugin folder.
/// </summary>
/// <remarks>
/// <para>
/// Plugins are loaded into the default context rather than one of their own. That is not the
/// textbook plugin design, and it is deliberate: an EF Core database provider cannot work from a
/// separate context. Its option extensions derive from types in Relational, which the host owns,
/// and the runtime reports the inherited members as unimplemented the moment the two contexts
/// differ. Once the provider is shared, everything it touches has to be shared too.
/// </para>
/// <para>
/// The cost is that two plugins cannot use different versions of the same library. The benefit is
/// that plugins can extend anything the host exposes, databases included.
/// </para>
/// </remarks>
internal sealed class PluginAssemblyResolver
{
    private readonly List<AssemblyDependencyResolver> _resolvers = [];

    public PluginAssemblyResolver()
    {
        AssemblyLoadContext.Default.Resolving += ResolveAssembly;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanagedDll;
    }

    /// <summary>Registers one plugin file, so its dependencies become resolvable.</summary>
    public void Register(string pluginPath) => _resolvers.Add(new AssemblyDependencyResolver(pluginPath));

    private Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        foreach (var resolver in _resolvers)
        {
            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (path is not null && File.Exists(path))
            {
                return context.LoadFromAssemblyPath(path);
            }
        }

        return null;
    }

    /// <remarks>
    /// Without this SQLite finds its managed wrapper but not e_sqlite3, and fails at the first
    /// query with a DllNotFoundException that names nothing useful.
    /// </remarks>
    private IntPtr ResolveUnmanagedDll(Assembly assembly, string unmanagedDllName)
    {
        foreach (var resolver in _resolvers)
        {
            var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path is not null && File.Exists(path))
            {
                return NativeLibrary.Load(path);
            }
        }

        return IntPtr.Zero;
    }
}
