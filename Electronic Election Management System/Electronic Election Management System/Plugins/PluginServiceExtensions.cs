using Electronic_Election_Management_System.Data;
using Serilog;
using Serilog.Extensions.Logging;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Keeps the plugin system's wiring in one place: Program.cs asks for it by name and never learns
/// what it is made of.
/// </summary>
public static class PluginServiceExtensions
{
    /// <summary>
    /// Reads the plugin folder and registers what it found, returning the host so the caller can
    /// query it immediately.
    /// </summary>
    /// <remarks>
    /// Loading happens here, while services are still being registered, rather than after the
    /// application is built. The database provider is itself a plugin, so it has to be in hand
    /// before the DbContext can be configured - and the DI container does not exist yet at that
    /// point, hence the hand-built logger.
    /// </remarks>
    public static IPluginHost AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        var options = PluginOptions.LoadAndValidate(configuration);

        var host = new PluginHost(options, new SerilogLoggerFactory(Log.Logger).CreateLogger<PluginHost>());
        host.Load();

        services.AddSingleton(options);
        services.AddSingleton<IPluginHost>(host);
        return host;
    }

    /// <summary>
    /// Reconciles the loaded scoring plugins with the ScoringSchemes table.
    /// </summary>
    public static Task UsePluginsAsync(this WebApplication app, ElectionDbContext db) =>
        ScoringSchemeSynchronizer.SyncAsync(db, app.Services.GetRequiredService<IPluginHost>(), app.Logger);
}
