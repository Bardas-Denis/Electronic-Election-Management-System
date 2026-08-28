using Electronic_Election_Management_System.Plugins;
using Electronic_Election_Management_System.Data;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Keeps the plugin system's wiring in one place: Program.cs asks for it by name and never learns
/// what it is made of.
/// </summary>
public static class PluginServiceExtensions
{
    public static IServiceCollection AddPlugins(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(PluginOptions.LoadAndValidate(configuration));
        services.AddSingleton<IPluginHost, PluginHost>();
        return services;
    }

    /// <summary>
    /// Reads the plugin folder, then reconciles it with the ScoringSchemes table.
    /// </summary>
    /// <remarks>
    /// Runs at startup rather than on first use, so a broken plugin folder shows up in the startup
    /// log instead of halfway through an election.
    /// </remarks>
    public static async Task UsePluginsAsync(this WebApplication app, ElectionDbContext db)
    {
        var host = app.Services.GetRequiredService<IPluginHost>();
        host.Load();
        await ScoringSchemeSynchronizer.SyncAsync(db, host, app.Logger);
    }
}
