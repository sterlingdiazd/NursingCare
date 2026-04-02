namespace NursingCareBackend.Api;

public static class CorsExtension
{
  private static readonly string[] defaultWebOrigins = ["http://localhost:3000"];
  private static readonly string[] defaultMobileOrigins = ["http://localhost:8081", "exp://localhost:8081"];

  public static void AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddCors(options =>
    {
      var webOrigins = configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? defaultWebOrigins;
      var mobileOrigins = configuration.GetSection("Cors:MobileOrigins").Get<string[]>() ?? defaultMobileOrigins;
      var configuredOrigins = webOrigins
        .Concat(mobileOrigins)
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

      options.AddPolicy("AllowAllDev", builder =>
            {
              builder
                  .SetIsOriginAllowed(origin =>
                  {
                    if (string.IsNullOrWhiteSpace(origin))
                    {
                      return false;
                    }

                    if (configuredOrigins.Contains(origin))
                    {
                      return true;
                    }

                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                      return false;
                    }

                    var host = uri.Host;
                    return host.EndsWith(".sslip.io", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
                  })
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            });
    });
  }
}
