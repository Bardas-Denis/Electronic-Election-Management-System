using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public interface ITokenService
    {
        // Intoarce (token, expiresAt)
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
    }
}
