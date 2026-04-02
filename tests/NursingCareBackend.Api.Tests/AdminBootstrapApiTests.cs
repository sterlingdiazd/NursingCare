using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace NursingCareBackend.Api.Tests;

public sealed class AdminBootstrapApiTests
{
  [Fact]
  public async Task POST_SetupAdmin_Should_Be_Blocked_After_First_Admin_Is_Created()
  {
    using var factory = new CustomWebApplicationFactory();
    var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/setup-admin", new
    {
      adminEmail = $"bootstrap-first-{Guid.NewGuid():N}@nursingcare.local",
      adminPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(payload);
    Assert.Equal(
      "La configuracion publica del primer administrador ya no esta disponible porque ya existe una cuenta administrativa.",
      payload!.Detail);
  }

  [Fact]
  public async Task POST_SetupAdmin_Should_Be_Disabled_In_Production_By_Default()
  {
    using var factory = new CustomWebApplicationFactory();
    using var productionFactory = factory.WithWebHostBuilder(builder =>
    {
      builder.UseEnvironment("Production");
    });

    var client = productionFactory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/auth/setup-admin", new
    {
      adminEmail = $"bootstrap-production-{Guid.NewGuid():N}@nursingcare.local",
      adminPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(payload);
    Assert.Equal(
      "La configuracion publica del primer administrador esta deshabilitada en produccion. Usa el portal administrativo despues de la instalacion inicial.",
      payload!.Detail);
  }

  private sealed class ProblemDetailsDto
  {
    public string Detail { get; set; } = string.Empty;
  }
}
