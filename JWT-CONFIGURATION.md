# JWT signing key configuration

The JWT signing key is intentionally not stored in `appsettings.json` or source control.
The API refuses to start when the key is missing or shorter than 32 bytes.

## Local development

From the backend project directory, generate and store a local key with .NET User Secrets:

```powershell
$jwtRng = [Security.Cryptography.RandomNumberGenerator]::Create()
$jwtBytes = New-Object byte[] 64
$jwtRng.GetBytes($jwtBytes)
$jwtRng.Dispose()
$jwtKey = [Convert]::ToBase64String($jwtBytes)
dotnet user-secrets set "Jwt:Key" $jwtKey
Remove-Variable jwtKey, jwtBytes, jwtRng
```

User Secrets are stored outside the repository and are loaded automatically in the
`Development` environment.

## Production

Configure the key through the deployment platform's secret manager or environment:

```text
Jwt__Key=<a cryptographically random value of at least 32 bytes>
```

ASP.NET Core converts the double underscore to `Jwt:Key`. Do not place the production
value in an `.env` file that is copied to a server image or committed to Git.

Changing this key invalidates all JWTs signed with the previous key, so users must sign
in again after rotation.
