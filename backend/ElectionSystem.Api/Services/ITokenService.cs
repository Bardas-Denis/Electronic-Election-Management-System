using ElectionSystem.Api.Entities;

namespace ElectionSystem.Api.Services
{
    public interface ITokenService
    {
        // Intoarce (token, expiresAt)
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
    }
}
