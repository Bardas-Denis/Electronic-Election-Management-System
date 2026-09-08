
namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Binds the <c>Plugins</c> configuration section.
/// </summary>
public sealed class PluginOptions
{
    public const string SectionName = "Plugins";

    /// <summary>
    /// Whether the plugin folder is scanned at all.
    /// </summary>
    /// <remarks>
    /// On by default because the database providers are themselves plugins: with this off there is
    /// no way to reach a database, so the setup wizard offers no providers and a configured
    /// instance refuses to start. Turning it off only helps to isolate a plugin load failure.
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>Plugin folder, relative to the application directory.</summary>
    public string Directory { get; init; } = "plugins";

    public static PluginOptions LoadAndValidate(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<PluginOptions>()
            ?? new PluginOptions();

        if (string.IsNullOrWhiteSpace(options.Directory))
        {
            throw new InvalidOperationException("Plugins:Directory must not be empty.");
        }

        // An absolute path would let a configuration file point the loader anywhere on the host,
        // which is a wide door for something that ends up executing as this process.
        if (Path.IsPathRooted(options.Directory))
        {
            throw new InvalidOperationException(
                "Plugins:Directory must be relative to the application directory.");
        }

        return options;
    }
}
