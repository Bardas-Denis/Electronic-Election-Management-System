using Electronic_Election_Management_System.Data;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Keeps the plugin system's wiring inside this folder: Program.cs asks for it by name and never
/// learns what it is made of.
/// </summary>
public static class PluginServiceExtensions
{
    public static IServiceCollection AddScoringPlugins(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(PluginOptions.LoadAndValidate(configuration));
        services.AddSingleton<IScoringPluginRegistry, ScoringPluginRegistry>();
        return services;
    }

    /// <summary>
    /// Reads the plugin folder, then reconciles it with the ScoringSchemes table.
    /// </summary>
    /// <remarks>
    /// Runs at startup rather than on first use, so a broken plugin folder shows up in the
    /// startup log instead of halfway through an election.
    /// </remarks>
    public static async Task UseScoringPluginsAsync(this WebApplication app, ElectionDbContext db)
    {
        var registry = app.Services.GetRequiredService<IScoringPluginRegistry>();
        registry.Load();
        await ScoringSchemeSynchronizer.SyncAsync(db, registry, app.Logger);
    }
}
