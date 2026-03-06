using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace NursingCareBackend.Api.Tests;

internal static class JwtTestTokens
{
  public static string CreateWriterToken(IServiceProvider services)
  {
    var configuration = services.GetRequiredService<IConfiguration>();
    var jwtSection = configuration.GetSection("Jwt");

    var key = jwtSection["Key"]
              ?? throw new InvalidOperationException("Jwt:Key configuration is missing for tests.");

    var issuer = jwtSection["Issuer"];
    var audience = jwtSection["Audience"];

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
      new Claim(ClaimTypes.Role, "Nurse")
    };

    var token = new JwtSecurityToken(
      issuer: issuer,
      audience: audience,
      claims: claims,
      expires: DateTime.UtcNow.AddHours(1),
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}

