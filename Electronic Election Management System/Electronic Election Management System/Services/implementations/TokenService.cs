using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Electronic_Election_Management_System.Configuration;
using Electronic_Election_Management_System.Models;
using Microsoft.IdentityModel.Tokens;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Token service using JWT.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _jwtOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenService"/> class.
        /// </summary>
        /// <param name="jwtOptions">Validated JWT configuration.</param>
        public TokenService(JwtOptions jwtOptions)
        {
            _jwtOptions = jwtOptions;
        }

        /// <summary>
        /// Generates a JWT token for the given user.
        /// </summary>
        /// <param name="user">The user for whom the token is to be generated.</param>
        /// <returns>A tuple containing the JWT token and its expiration time.</returns>
        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiresInMinutes);

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

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }
    }
}
