using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NursingCareBackend.Api.Authorization;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Extensions;

public static class AuthenticationExtensions
{
  public static IServiceCollection AddJwtAuthentication(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    var jwtSection = configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"];
    var jwtIssuer = jwtSection["Issuer"];
    var jwtAudience = jwtSection["Audience"];

    if (string.IsNullOrWhiteSpace(jwtKey))
    {
      throw new InvalidOperationException("JWT configuration is missing 'Jwt:Key'.");
    }

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

    services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          NameClaimType = ClaimTypes.Email,
          RoleClaimType = ClaimTypes.Role,
          ValidIssuer = jwtIssuer,
          ValidAudience = jwtAudience,
          IssuerSigningKey = signingKey
        };
      });

    services.AddAuthorization(options =>
    {
      options.AddPolicy("CareRequestReader", policy =>
        policy
          .RequireRole(SystemRoles.Client, SystemRoles.Nurse, SystemRoles.Admin)
          .AddRequirements(new OperationalAccessRequirement()));

      options.AddPolicy("CareRequestCreator", policy =>
        policy
          .RequireRole(SystemRoles.Client, SystemRoles.Admin)
          .AddRequirements(new OperationalAccessRequirement()));

      options.AddPolicy("CareRequestApprover", policy =>
        policy.RequireRole(SystemRoles.Admin));

      options.AddPolicy("CareRequestCompleter", policy =>
        policy
          .RequireRole(SystemRoles.Nurse)
          .AddRequirements(new OperationalAccessRequirement()));

      options.AddPolicy("CareRequestCanceller", policy =>
        policy
          .RequireRole(SystemRoles.Client, SystemRoles.Admin)
          .AddRequirements(new OperationalAccessRequirement()));
    });

    services.AddScoped<IAuthorizationHandler, OperationalAccessHandler>();

    return services;
  }
}
