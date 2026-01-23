namespace NursingCareBackend.Api;

public static class CorsExtension
{
  public static void AddCorsPolicy(this IServiceCollection services)
  {
    // Mobile app policy
    services.AddCors(options =>
    {
      options.AddPolicy("AllowAllDev", builder =>
            {
              builder
                  .WithOrigins("http://localhost:3000", "http://localhost:19006")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            });
            
      // options.AddPolicy("AllowMobileApp",
      //   builder =>
      //   {
      //     builder
      //         .WithOrigins("http://localhost:19006")
      //         .AllowAnyHeader()
      //         .AllowAnyMethod();
      //   });

      // // Web app policy
      // options.AddPolicy("AllowWebApp",
      // policy =>
      // {
      //   policy
      //           .WithOrigins("http://localhost:3000")
      //           .AllowAnyHeader()
      //           .AllowAnyMethod();
      // });

    });
  }
}
