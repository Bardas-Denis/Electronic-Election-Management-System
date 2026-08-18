using System.Text.Json;
using System.Text.Json.Serialization;

namespace Electronic_Election_Management_System.Setup;

/// <summary>
/// Persisted database configuration. Written once by <c>POST /api/setup/save</c>
/// and read by Program.cs on every subsequent startup.
/// </summary>
public sealed record DbConfig(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("connectionString")] string ConnectionString);

/// <summary>
/// Thin static wrapper around <c>data/dbconfig.json</c>.
/// The file lives at <see cref="ConfigFilePath"/> relative to the binary directory so
/// that it stays in a predictable location regardless of the working directory at launch.
/// </summary>
public static class DbConfigStore
{
    /// <summary>
    /// Path to the configuration file, resolved relative to the current working directory.
    /// In Docker scenarios, this resolves to /app/data/dbconfig.json which is mounted to the persistent volume.
    /// </summary>
    public static readonly string ConfigFilePath =
        Path.Combine(Directory.GetCurrentDirectory(), "data", "dbconfig.json");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Returns <see langword="true"/> when <c>data/dbconfig.json</c> is present on disk.
    /// </summary>
    public static bool Exists() => File.Exists(ConfigFilePath);

    /// <summary>
    /// Attempts to load and deserialize <c>data/dbconfig.json</c>.
    /// </summary>
    /// <returns>
    /// A valid <see cref="DbConfig"/> when the file is present and well-formed;
    /// <see langword="null"/> when the file is absent, empty, or malformed.
    /// </returns>
    public static DbConfig? TryLoad()
    {
        if (!File.Exists(ConfigFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<DbConfig>(json, SerializerOptions);

            if (config is null
                || string.IsNullOrWhiteSpace(config.Provider)
                || string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                return null;
            }

            return config;
        }
        catch
        {
            // Malformed JSON is treated the same as absent
            return null;
        }
    }

    /// <summary>
    /// Serializes <paramref name="config"/> to <c>data/dbconfig.json</c>,
    /// creating the <c>data/</c> directory if it does not already exist.
    /// </summary>
    public static void Save(DbConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigFilePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(ConfigFilePath, json);
    }
}
