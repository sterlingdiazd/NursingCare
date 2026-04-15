using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Infrastructure.Authentication;

public sealed class TokenGenerator : ITokenGenerator
{
    private readonly IConfiguration _configuration;

    public TokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TokenResult GenerateToken(User user)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var jwtKey = jwtSection["Key"];
        var jwtIssuer = jwtSection["Issuer"];
        var jwtAudience = jwtSection["Audience"];

        // Replace JWT_KEY placeholder with environment variable if it exists
        if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Contains("{JWT_KEY}"))
        {
            var envJwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
            if (!string.IsNullOrEmpty(envJwtKey))
            {
                jwtKey = jwtKey.Replace("{JWT_KEY}", envJwtKey);
            }
        }

        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Contains("{JWT_KEY}"))
        {
            throw new InvalidOperationException("JWT configuration is missing 'Jwt:Key'. Set JWT_KEY environment variable or update appsettings.json.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
        };

        claims.Add(new Claim(
            AuthClaimTypes.NurseProfileActive,
            IsNurseProfileActive(user).ToString().ToLowerInvariant()));

        // Add roles as claims
        foreach (var userRole in user.UserRoles)
        {
            // Normalize role claim casing to keep authorization checks consistent
            // even when historical data stored role names using mixed casing.
            claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Name.ToUpperInvariant()));
        }

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials
        );

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    private static bool IsNurseProfileActive(User user)
        => user.ProfileType != UserProfileType.NURSE || user.NurseProfile?.IsActive == true;
}
