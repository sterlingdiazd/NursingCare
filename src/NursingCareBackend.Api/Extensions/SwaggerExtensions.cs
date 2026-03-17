using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace NursingCareBackend.Api.Extensions;

public static class SwaggerExtensions
{
  public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
  {
    services.AddSwaggerGen(options =>
    {
      var jwtSecurityScheme = new OpenApiSecurityScheme
      {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste only the JWT token value."
      };

      options.AddSecurityDefinition("Bearer", jwtSecurityScheme);
      options.AddSecurityRequirement(document =>
      {
        var requirement = new OpenApiSecurityRequirement();
        requirement[new OpenApiSecuritySchemeReference("Bearer", document, null)] = [];
        return requirement;
      });
    });

    return services;
  }
}
