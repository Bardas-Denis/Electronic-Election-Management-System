using System.Text;

namespace Electronic_Election_Management_System.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiresInMinutes { get; init; } = 60;

    public static JwtOptions LoadAndValidate(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set 'Jwt:Key' with .NET User Secrets " +
                "for development or the 'Jwt__Key' environment variable in production.");
        }

        if (Encoding.UTF8.GetByteCount(options.Key) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key must contain at least {MinimumSigningKeyBytes} bytes.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
            throw new InvalidOperationException("JWT issuer is not configured.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("JWT audience is not configured.");

        if (options.ExpiresInMinutes is < 1 or > 1440)
            throw new InvalidOperationException("JWT expiration must be between 1 and 1440 minutes.");

        return options;
    }
}
