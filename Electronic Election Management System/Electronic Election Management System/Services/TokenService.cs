using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Electronic_Election_Management_System.Models;
using Microsoft.IdentityModel.Tokens;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Token service using JWT.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenService"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Generates a JWT token for the given user.
        /// </summary>
        /// <param name="user">The user for whom the token is to be generated.</param>
        /// <returns>A tuple containing the JWT token and its expiration time.</returns>
        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var jwtSection = _configuration.GetSection("Jwt");
            string key = jwtSection["Key"]!;
            string issuer = jwtSection["Issuer"]!;
            string audience = jwtSection["Audience"]!;
            int expiresInMinutes = int.Parse(jwtSection["ExpiresInMinutes"] ?? "120");

            var expiresAt = DateTime.UtcNow.AddMinutes(expiresInMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // Security stamp — used to immediately invalidate JWT tokens when the role or credentials change
                new Claim("securityStamp", user.SecurityStamp)
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }
    }
}
