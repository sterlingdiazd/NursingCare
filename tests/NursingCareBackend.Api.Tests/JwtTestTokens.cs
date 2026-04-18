using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Tests;

internal static class JwtTestTokens
{
  public static readonly Guid TestNurseUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");

  public static string CreateWriterToken(IServiceProvider services)
    => CreateToken(services, userId: null, includeUserId: true, "NURSE");

  public static string CreateNurseToken(IServiceProvider services)
    => CreateToken(services, userId: TestNurseUserId, includeUserId: true, "NURSE");

  public static string CreateAdminToken(IServiceProvider services)
    => CreateToken(services, userId: Guid.Parse("00000000-0000-0000-0000-000000000001"), includeUserId: true, "ADMIN");

  public static string CreateTokenWithoutUserId(IServiceProvider services, params string[] roles)
    => CreateToken(services, userId: null, includeUserId: false, roles);

  public static string CreateToken(IServiceProvider services, params string[] roles)
    => CreateToken(services, userId: null, includeUserId: true, roles);

  private static string CreateToken(IServiceProvider services, Guid? userId, bool includeUserId, params string[] roles)
  {
    var configuration = services.GetRequiredService<IConfiguration>();
    var jwtSection = configuration.GetSection("Jwt");

    var key = jwtSection["Key"]
              ?? throw new InvalidOperationException("Jwt:Key configuration is missing for tests.");

    var issuer = jwtSection["Issuer"];
    var audience = jwtSection["Audience"];

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
      new(ClaimTypes.Email, "test.user@nursingcare.local"),
      new(
        AuthClaimTypes.NurseProfileActive,
        roles.Contains("NURSE", StringComparer.OrdinalIgnoreCase) ? "true" : "false")
    };

    if (includeUserId)
    {
      var userIdToUse = userId ?? Guid.NewGuid();
      claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdToUse.ToString()));
    }

    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var token = new JwtSecurityToken(
      issuer: issuer,
      audience: audience,
      claims: claims,
      expires: DateTime.UtcNow.AddHours(1),
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}
