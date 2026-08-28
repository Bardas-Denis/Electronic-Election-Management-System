
namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Binds the <c>Plugins</c> configuration section.
/// </summary>
public sealed class PluginOptions
{
    public const string SectionName = "Plugins";

    /// <summary>
    /// Loading third-party assemblies stays off unless it is switched on explicitly. The default
    /// is what a deployment gets when nobody thought about plugins at all.
    /// </summary>
    public bool Enabled { get; init; }

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
