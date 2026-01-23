namespace NursingCareBackend.Api;

public static class CorsExtension
{
  private static readonly string[] defaultWebOrigins = ["http://localhost:3000"];
  private static readonly string[] defaultMobileOrigins = ["http://localhost:19006"];

  public static void AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
  {
    // Mobile app policy
    services.AddCors(options =>
    {
      var webOrigins = configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? defaultWebOrigins;
      var mobileOrigins = configuration.GetSection("Cors:MobileOrigins").Get<string[]>() ?? defaultMobileOrigins;

      options.AddPolicy("AllowAllDev", builder =>
            {
              builder
                  .WithOrigins([.. webOrigins, .. mobileOrigins])
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            });
    });
  }
}
