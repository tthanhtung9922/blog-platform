using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Blog.IntegrationTests.Helpers;

/// <summary>
/// Generates valid signed JWT tokens for integration test authentication.
/// Uses the same signing key as the application (from appsettings.json).
/// Phase 2: No real OAuth flows. Phase 3 adds real token endpoints.
/// </summary>
public static class JwtTokenHelper
{
    // Must match Jwt:SigningKey in appsettings.json
    private const string SigningKey = "CHANGE_ME_IN_PRODUCTION_USE_A_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS";
    private const string Issuer = "blog-platform";
    private const string Audience = "blog-platform-api";

    public static string GenerateJwt(string role, Guid? userId = null)
    {
        var id = (userId ?? Guid.NewGuid()).ToString();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, id),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
