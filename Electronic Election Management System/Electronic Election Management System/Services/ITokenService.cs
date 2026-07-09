using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Generates a JSON Web Token (JWT) for the given user.
        /// </summary>
        /// <param name="user">The user for whom the token is to be generated.</param>
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
    }
}
