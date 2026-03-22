namespace NursingCareBackend.Application.Identity.Services;

public sealed class AdminBootstrapOptions
{
  public const string SectionName = "AdminBootstrap";

  public bool AllowInProduction { get; set; }
}
